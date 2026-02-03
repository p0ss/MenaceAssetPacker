using System;
using System.Collections.Generic;
using System.Reflection;

namespace Menace.SDK.Repl;

/// <summary>
/// Evaluates REPL input strings by compiling and executing them via RuntimeCompiler.
/// Maintains a history of inputs and results.
/// </summary>
public class ConsoleEvaluator
{
    private readonly RuntimeCompiler _compiler;
    private readonly List<(string Input, EvalResult Result)> _history = new();

    public ConsoleEvaluator(RuntimeCompiler compiler)
    {
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
    }

    public IReadOnlyList<(string Input, EvalResult Result)> History => _history;

    /// <summary>
    /// Evaluate a REPL input string. Auto-detects expression vs statements.
    /// </summary>
    public EvalResult Evaluate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            var empty = new EvalResult
            {
                Success = false,
                Error = "Empty input"
            };
            _history.Add((input ?? "", empty));
            return empty;
        }

        var compilation = _compiler.Compile(input);
        if (!compilation.Success)
        {
            var result = new EvalResult
            {
                Success = false,
                Error = string.Join("\n", compilation.Errors)
            };
            _history.Add((input, result));
            return result;
        }

        try
        {
            // Find and invoke the Execute method on the generated class
            var type = compilation.LoadedAssembly.GetType(compilation.ClassName);
            if (type == null)
            {
                var result = new EvalResult
                {
                    Success = false,
                    Error = $"Generated class '{compilation.ClassName}' not found"
                };
                _history.Add((input, result));
                return result;
            }

            var method = type.GetMethod("Execute",
                BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                var result = new EvalResult
                {
                    Success = false,
                    Error = "Execute method not found"
                };
                _history.Add((input, result));
                return result;
            }

            var value = method.Invoke(null, null);
            var evalResult = new EvalResult
            {
                Success = true,
                Value = value,
                DisplayText = FormatValue(value)
            };

            _history.Add((input, evalResult));
            return evalResult;
        }
        catch (TargetInvocationException ex)
        {
            var inner = ex.InnerException ?? ex;
            var result = new EvalResult
            {
                Success = false,
                Error = $"{inner.GetType().Name}: {inner.Message}"
            };
            _history.Add((input, result));
            return result;
        }
        catch (Exception ex)
        {
            var result = new EvalResult
            {
                Success = false,
                Error = $"Execution error: {ex.Message}"
            };
            _history.Add((input, result));
            return result;
        }
    }

    /// <summary>
    /// Clear evaluation history.
    /// </summary>
    public void ClearHistory()
    {
        _history.Clear();
    }

    private static string FormatValue(object value)
    {
        if (value == null)
            return "null";

        if (value is GameObj obj)
        {
            if (obj.IsNull) return "GameObj.Null";
            var name = obj.GetName();
            var typeName = obj.GetTypeName();
            return name != null
                ? $"{{{typeName} '{name}' @ 0x{obj.Pointer:X}}}"
                : $"{{{typeName} @ 0x{obj.Pointer:X}}}";
        }

        if (value is GameType gt)
        {
            return gt.IsValid ? $"GameType({gt.FullName})" : "GameType.Invalid";
        }

        if (value is string s)
            return $"\"{s}\"";

        if (value is bool b)
            return b ? "true" : "false";

        // Check for IEnumerable-like types
        var type = value.GetType();
        if (type.IsArray)
        {
            var arr = (Array)value;
            return $"{type.GetElementType()?.Name}[{arr.Length}]";
        }

        // Check for Count property (collections)
        var countProp = type.GetProperty("Count");
        if (countProp != null)
        {
            try
            {
                var count = countProp.GetValue(value);
                return $"{type.Name} (Count = {count})";
            }
            catch { }
        }

        var str = value.ToString();
        return str?.Length > 500 ? str[..500] + "..." : str ?? "null";
    }

    public class EvalResult
    {
        public bool Success;
        public object Value;
        public string DisplayText;
        public string Error;
    }
}
