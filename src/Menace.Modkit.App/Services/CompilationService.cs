using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Compiles modpack C# source files into a DLL using Roslyn.
/// Targets net6.0 (MelonLoader's runtime), not the modkit's own framework.
/// </summary>
public class CompilationService
{
    private readonly SecurityScanner _securityScanner = new();

    /// <summary>
    /// Compile a modpack's source code into a DLL.
    /// </summary>
    public async Task<CompilationResult> CompileModpackAsync(
        ModpackManifest manifest,
        CancellationToken ct = default)
    {
        var result = new CompilationResult();

        if (!manifest.Code.HasAnySources)
        {
            result.Success = false;
            result.Diagnostics.Add(new CompilationDiagnostic
            {
                Severity = Models.DiagnosticSeverity.Error,
                Message = "No source files to compile"
            });
            return result;
        }

        return await Task.Run(() => CompileCoreAsync(manifest, result, ct), ct).ConfigureAwait(false);
    }

    private async Task<CompilationResult> CompileCoreAsync(
        ModpackManifest manifest,
        CompilationResult result,
        CancellationToken ct)
    {
        var modpackDir = manifest.Path;

        // Collect source files (absolute paths)
        var sourceFiles = manifest.Code.Sources
            .Select(s => Path.Combine(modpackDir, s))
            .Where(File.Exists)
            .ToList();

        if (sourceFiles.Count == 0)
        {
            result.Success = false;
            result.Diagnostics.Add(new CompilationDiagnostic
            {
                Severity = Models.DiagnosticSeverity.Error,
                Message = "No source files found on disk"
            });
            return result;
        }

        // Security scan
        result.SecurityWarnings = _securityScanner.ScanSources(sourceFiles);

        // Parse source files
        var syntaxTrees = new List<SyntaxTree>();
        foreach (var file in sourceFiles)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var source = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                var tree = CSharpSyntaxTree.ParseText(
                    source,
                    CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10),
                    path: file);
                syntaxTrees.Add(tree);
            }
            catch (Exception ex)
            {
                result.Diagnostics.Add(new CompilationDiagnostic
                {
                    Severity = Models.DiagnosticSeverity.Error,
                    Message = $"Failed to parse: {ex.Message}",
                    File = Path.GetFileName(file)
                });
            }
        }

        if (syntaxTrees.Count == 0)
        {
            result.Success = false;
            return result;
        }

        // Inject nullable attribute polyfill — the IL2CPP target assemblies don't
        // include System.Runtime.CompilerServices.NullableAttribute, but Roslyn
        // emits references to it for any generic types with reference type args
        // (e.g. Dictionary<int, string>), even with NullableContextOptions.Disable.
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(
            NullableAttributePolyfill,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10),
            path: "<NullablePolyfill>"));

        // Resolve references
        var gameInstallPath = AppSettings.Instance.GameInstallPath;
        var resolver = new ReferenceResolver(gameInstallPath);
        var references = await resolver.ResolveReferencesAsync(manifest.Code.References, ct).ConfigureAwait(false);

        // Report any resolution issues as warnings
        foreach (var issue in resolver.ResolutionIssues)
        {
            result.Diagnostics.Add(new CompilationDiagnostic
            {
                Severity = Models.DiagnosticSeverity.Warning,
                Message = $"[Reference Resolution] {issue}"
            });
        }

        if (references.Count == 0)
        {
            result.Success = false;
            result.Diagnostics.Add(new CompilationDiagnostic
            {
                Severity = Models.DiagnosticSeverity.Error,
                Message = "No references resolved. Check that the game install path is set correctly and MelonLoader is installed."
            });
            return result;
        }

        // Sanitize assembly name
        var assemblyName = SanitizeAssemblyName(manifest.Name);

        // Create compilation
        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithPlatform(Platform.AnyCpu)
                .WithAllowUnsafe(true)
                .WithNullableContextOptions(NullableContextOptions.Disable));

        // Output path
        var buildDir = Path.Combine(modpackDir, "build");
        Directory.CreateDirectory(buildDir);
        var outputPath = Path.Combine(buildDir, $"{assemblyName}.dll");

        // Emit
        using var stream = new FileStream(outputPath, FileMode.Create);
        var emitResult = compilation.Emit(stream);

        // Convert diagnostics
        foreach (var diag in emitResult.Diagnostics)
        {
            if (diag.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden)
                continue;

            var location = diag.Location.GetMappedLineSpan();
            result.Diagnostics.Add(new CompilationDiagnostic
            {
                Severity = diag.Severity switch
                {
                    Microsoft.CodeAnalysis.DiagnosticSeverity.Error => Models.DiagnosticSeverity.Error,
                    Microsoft.CodeAnalysis.DiagnosticSeverity.Warning => Models.DiagnosticSeverity.Warning,
                    _ => Models.DiagnosticSeverity.Info
                },
                Message = diag.GetMessage(),
                File = location.HasMappedPath ? Path.GetFileName(location.Path) : null,
                Line = location.StartLinePosition.Line + 1,
                Column = location.StartLinePosition.Character + 1
            });
        }

        result.Success = emitResult.Success;
        if (emitResult.Success)
        {
            result.OutputDllPath = outputPath;
        }

        return result;
    }

    private static string SanitizeAssemblyName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Replace(" ", "_").Replace(".", "_");
    }

    private const string NullableAttributePolyfill = @"
// Auto-injected polyfill for IL2CPP targets that lack these attributes.
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    internal sealed class NullableAttribute : Attribute
    {
        public NullableAttribute(byte _) { }
        public NullableAttribute(byte[] _) { }
    }

    [AttributeUsage(AttributeTargets.All)]
    internal sealed class NullableContextAttribute : Attribute
    {
        public NullableContextAttribute(byte _) { }
    }
}
";
}
