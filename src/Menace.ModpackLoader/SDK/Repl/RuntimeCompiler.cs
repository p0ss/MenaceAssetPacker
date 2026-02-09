using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Menace.SDK.Repl;

/// <summary>
/// Roslyn-based runtime C# compiler. Wraps expressions/statements in a class,
/// compiles to in-memory assembly, and loads it.
/// Security: Blocks dangerous namespaces and disables unsafe code.
/// </summary>
public class RuntimeCompiler
{
    private readonly IReadOnlyList<MetadataReference> _references;
    private int _compilationCounter;

    private static readonly string[] DefaultUsings =
    {
        "System",
        "System.Linq",
        "System.Collections.Generic",
        "Menace.SDK",
        "UnityEngine"
    };

    /// <summary>
    /// Namespaces that are blocked for security reasons in REPL code.
    /// These could be used for arbitrary code execution, file system access, or network access.
    /// </summary>
    private static readonly HashSet<string> BlockedNamespaces = new(StringComparer.Ordinal)
    {
        "System.Reflection.Emit",
        "System.Runtime.InteropServices",
        "System.Diagnostics.Process",
        "System.IO.File",
        "System.IO.Directory",
        "System.IO.FileStream",
        "System.IO.StreamWriter",
        "System.IO.StreamReader",
        "System.Net.Http",
        "System.Net.Sockets",
        "System.Net.WebClient",
    };

    /// <summary>
    /// Specific dangerous patterns that should be blocked.
    /// </summary>
    private static readonly string[] BlockedPatterns =
    {
        "Process.Start",
        "Assembly.Load",
        "Assembly.LoadFrom",
        "Assembly.LoadFile",
        "Activator.CreateInstance",
        "Type.InvokeMember",
        "AppDomain.CreateDomain",
        "File.WriteAllText",
        "File.WriteAllBytes",
        "File.Delete",
        "Directory.Delete",
        "Environment.Exit",
    };

    public RuntimeCompiler(IReadOnlyList<MetadataReference> references)
    {
        _references = references ?? throw new ArgumentNullException(nameof(references));
    }

    /// <summary>
    /// Compile a single expression (e.g., "GameType.Find(\"Agent\").IsValid").
    /// The expression is wrapped in a return statement.
    /// </summary>
    public CompilationResult CompileExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return CompilationResult.Fail("Empty expression");

        var body = $"        return {expression};";
        return CompileWrapped(body);
    }

    /// <summary>
    /// Compile one or more statements (e.g., "var t = GameType.Find(\"Agent\"); return t.FullName;").
    /// If the statements don't contain a return, "return null;" is appended.
    /// </summary>
    public CompilationResult CompileStatements(string statements)
    {
        if (string.IsNullOrWhiteSpace(statements))
            return CompilationResult.Fail("Empty statements");

        var trimmed = statements.Trim();
        var hasReturn = trimmed.Contains("return ");
        var body = hasReturn
            ? $"        {trimmed}"
            : $"        {trimmed}\n        return null;";

        return CompileWrapped(body);
    }

    /// <summary>
    /// Auto-detect whether input is an expression or statements, and compile accordingly.
    /// Statements are detected by the presence of ';' or '{'.
    /// </summary>
    public CompilationResult Compile(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return CompilationResult.Fail("Empty input");

        var trimmed = input.Trim();

        // If it ends with ; or contains { or has multiple ;, treat as statements
        if (trimmed.Contains('{') || trimmed.Count(c => c == ';') > 1 ||
            (trimmed.EndsWith(';') && !trimmed.StartsWith("return ")))
        {
            return CompileStatements(trimmed);
        }

        // Remove trailing ; for expression mode
        if (trimmed.EndsWith(';'))
            trimmed = trimmed[..^1].Trim();

        return CompileExpression(trimmed);
    }

    private CompilationResult CompileWrapped(string body)
    {
        _compilationCounter++;
        var className = $"ReplExpr_{_compilationCounter}";

        var usings = string.Join("\n", DefaultUsings.Select(u => $"using {u};"));
        var source = $@"{usings}

public static class {className}
{{
    public static object Execute()
    {{
{body}
    }}
}}";

        // Security check: Block dangerous code patterns before compilation
        var securityError = CheckForDangerousCode(source);
        if (securityError != null)
        {
            return CompilationResult.Fail($"Security: {securityError}");
        }

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var compilation = CSharpCompilation.Create(
                $"ReplCompilation_{_compilationCounter}",
                new[] { syntaxTree },
                _references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Debug)
                    .WithAllowUnsafe(false)); // Disable unsafe code for security

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            var errors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage())
                .ToList();

            var warnings = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Warning)
                .Select(d => d.GetMessage())
                .ToList();

            if (!emitResult.Success)
            {
                return new CompilationResult
                {
                    Success = false,
                    Errors = errors,
                    Warnings = warnings,
                    ClassName = className
                };
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());

            return new CompilationResult
            {
                Success = true,
                LoadedAssembly = assembly,
                Errors = errors,
                Warnings = warnings,
                ClassName = className
            };
        }
        catch (Exception ex)
        {
            return CompilationResult.Fail($"Compilation exception: {ex.Message}");
        }
    }

    public class CompilationResult
    {
        public bool Success;
        public Assembly LoadedAssembly;
        public IReadOnlyList<string> Errors = Array.Empty<string>();
        public IReadOnlyList<string> Warnings = Array.Empty<string>();
        internal string ClassName;

        internal static CompilationResult Fail(string error)
        {
            return new CompilationResult
            {
                Success = false,
                Errors = new[] { error }
            };
        }
    }

    /// <summary>
    /// Check source code for dangerous patterns that could be used for malicious purposes.
    /// Returns an error message if dangerous code is detected, null otherwise.
    /// </summary>
    private static string CheckForDangerousCode(string source)
    {
        // Check for blocked namespaces
        foreach (var ns in BlockedNamespaces)
        {
            if (source.Contains(ns, StringComparison.Ordinal))
            {
                return $"Blocked namespace detected: {ns}";
            }
        }

        // Check for blocked patterns
        foreach (var pattern in BlockedPatterns)
        {
            if (source.Contains(pattern, StringComparison.Ordinal))
            {
                return $"Blocked operation detected: {pattern}";
            }
        }

        // Check for unsafe keyword
        if (source.Contains("unsafe ", StringComparison.Ordinal) ||
            source.Contains("unsafe{", StringComparison.Ordinal))
        {
            return "Unsafe code is not allowed in REPL";
        }

        // Check for pointer syntax
        if (source.Contains("fixed ", StringComparison.Ordinal) ||
            source.Contains("stackalloc ", StringComparison.Ordinal))
        {
            return "Pointer operations are not allowed in REPL";
        }

        return null;
    }
}
