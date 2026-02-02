using System.Collections.Generic;

namespace Menace.Modkit.App.Models;

/// <summary>
/// Result of compiling a modpack's source code via Roslyn.
/// </summary>
public class CompilationResult
{
    public bool Success { get; set; }
    public string? OutputDllPath { get; set; }
    public List<CompilationDiagnostic> Diagnostics { get; set; } = new();
    public List<SecurityWarning> SecurityWarnings { get; set; } = new();
}

public class CompilationDiagnostic
{
    public DiagnosticSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? File { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }

    public override string ToString()
    {
        var location = string.IsNullOrEmpty(File) ? "" : $"{File}({Line},{Column}): ";
        return $"{location}{Severity}: {Message}";
    }
}

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public class SecurityWarning
{
    public SecuritySeverity Severity { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? File { get; set; }
    public int Line { get; set; }

    public override string ToString()
    {
        var location = string.IsNullOrEmpty(File) ? "" : $"{File}({Line}): ";
        return $"{location}[{Severity}] {Category}: {Message}";
    }
}

public enum SecuritySeverity
{
    Warning,
    Danger
}
