using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Scans C# source files for potentially dangerous patterns.
/// Results are advisory warnings, not blocking.
/// </summary>
public class SecurityScanner
{
    private static readonly List<SecurityRule> Rules = new()
    {
        // Networking
        new("Networking", SecuritySeverity.Danger, @"\bHttpClient\b", "Uses HttpClient (network access)"),
        new("Networking", SecuritySeverity.Danger, @"\bWebRequest\b", "Uses WebRequest (network access)"),
        new("Networking", SecuritySeverity.Danger, @"\bSocket\b", "Uses Socket (network access)"),
        new("Networking", SecuritySeverity.Danger, @"\bTcpClient\b", "Uses TcpClient (network access)"),
        new("Networking", SecuritySeverity.Danger, @"\bUdpClient\b", "Uses UdpClient (network access)"),
        new("Networking", SecuritySeverity.Danger, @"\bDns\.", "Uses DNS resolution"),

        // URLs
        new("URLs", SecuritySeverity.Danger, @"""https?://", "Contains URL string literal"),
        new("URLs", SecuritySeverity.Danger, @"""ftp://", "Contains FTP URL string literal"),

        // Process spawning
        new("Process", SecuritySeverity.Danger, @"\bProcess\.Start\b", "Spawns external process"),
        new("Process", SecuritySeverity.Danger, @"\bProcessStartInfo\b", "Creates process start info"),
        new("Process", SecuritySeverity.Danger, @"cmd\.exe|powershell", "References command shell"),

        // Filesystem escape
        new("Filesystem", SecuritySeverity.Warning, @"\bEnvironment\.GetFolderPath\b", "Accesses system folder paths"),
        new("Filesystem", SecuritySeverity.Warning, @"\bDriveInfo\b", "Accesses drive information"),
        new("Filesystem", SecuritySeverity.Warning, @"\bDirectory\.GetDirectoryRoot\b", "Accesses directory root"),

        // Reflection on system
        new("Reflection", SecuritySeverity.Warning, @"\bAssembly\.Load\b", "Dynamically loads assemblies"),
        new("Reflection", SecuritySeverity.Warning, @"\bAssembly\.LoadFrom\b", "Dynamically loads assemblies from path"),
        new("Reflection", SecuritySeverity.Warning, @"Type\.GetType\(""System\.", "Reflects on System types"),

        // P/Invoke
        new("P/Invoke", SecuritySeverity.Warning, @"\[DllImport\b", "Uses P/Invoke (native interop)"),
        new("P/Invoke", SecuritySeverity.Warning, @"\bextern\s+", "Declares extern method"),

        // Registry
        new("Registry", SecuritySeverity.Danger, @"\bRegistry\b", "Accesses Windows registry"),
        new("Registry", SecuritySeverity.Danger, @"\bRegistryKey\b", "Accesses Windows registry"),

        // Suspicious combos
        new("Obfuscation", SecuritySeverity.Danger, @"Convert\.FromBase64String.*Assembly\.Load|Assembly\.Load.*Convert\.FromBase64String",
            "Decodes Base64 and loads assembly (possible code injection)")
    };

    /// <summary>
    /// Scan all source files and return security warnings.
    /// </summary>
    public List<SecurityWarning> ScanSources(List<string> sourceFilePaths)
    {
        var warnings = new List<SecurityWarning>();

        foreach (var filePath in sourceFilePaths)
        {
            if (!File.Exists(filePath)) continue;

            var lines = File.ReadAllLines(filePath);
            var fileName = Path.GetFileName(filePath);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Skip comments
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//")) continue;

                foreach (var rule in Rules)
                {
                    if (rule.CompiledPattern.IsMatch(line))
                    {
                        warnings.Add(new SecurityWarning
                        {
                            Severity = rule.Severity,
                            Category = rule.Category,
                            Pattern = rule.Pattern,
                            Message = rule.Description,
                            File = fileName,
                            Line = i + 1
                        });
                    }
                }
            }
        }

        return warnings;
    }

    private class SecurityRule
    {
        public string Category { get; }
        public SecuritySeverity Severity { get; }
        public string Pattern { get; }
        public string Description { get; }
        public Regex CompiledPattern { get; }

        public SecurityRule(string category, SecuritySeverity severity, string pattern, string description)
        {
            Category = category;
            Severity = severity;
            Pattern = pattern;
            Description = description;
            CompiledPattern = new Regex(pattern, RegexOptions.Compiled);
        }
    }
}
