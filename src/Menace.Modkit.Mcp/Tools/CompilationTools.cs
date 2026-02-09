using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.Mcp.Tools;

/// <summary>
/// MCP tools for compiling modpack source code.
/// </summary>
[McpServerToolType]
public static class CompilationTools
{
    [McpServerTool(Name = "compile_modpack")]
    [Description("Compile a modpack's C# source code into a DLL. Returns compilation diagnostics including errors, warnings, and security scan results.")]
    public static async Task<string> CompileModpack(
        ModpackManager modpackManager,
        CompilationService compilationService,
        [Description("The name of the modpack to compile")] string name,
        CancellationToken cancellationToken = default)
    {
        var modpacks = modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{name}' not found" }, JsonOptions);
        }

        if (!manifest.Code.HasAnySources)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "Modpack has no source files to compile",
                sources = manifest.Code.Sources
            }, JsonOptions);
        }

        try
        {
            var result = await compilationService.CompileModpackAsync(manifest, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = result.Success,
                outputDllPath = result.OutputDllPath,
                diagnostics = result.Diagnostics.Select(d => new
                {
                    severity = d.Severity.ToString(),
                    message = d.Message,
                    file = d.File,
                    line = d.Line,
                    column = d.Column
                }),
                securityWarnings = result.SecurityWarnings.Select(w => new
                {
                    severity = w.Severity.ToString(),
                    category = w.Category,
                    message = w.Message,
                    file = w.File,
                    line = w.Line
                }),
                errorCount = result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error),
                warningCount = result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning)
            }, JsonOptions);
        }
        catch (OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { error = "Compilation cancelled" }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "security_scan", ReadOnly = true)]
    [Description("Scan a modpack's source code for potentially dangerous patterns. This is an advisory scan that identifies networking, process spawning, filesystem access, and other security-sensitive patterns.")]
    public static string SecurityScan(
        ModpackManager modpackManager,
        SecurityScanner securityScanner,
        [Description("The name of the modpack to scan")] string name)
    {
        var modpacks = modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{name}' not found" }, JsonOptions);
        }

        if (!manifest.Code.HasAnySources)
        {
            return JsonSerializer.Serialize(new
            {
                modpack = name,
                hasSource = false,
                warnings = Array.Empty<object>(),
                summary = "No source files to scan"
            }, JsonOptions);
        }

        var sourceFiles = manifest.Code.Sources
            .Select(s => Path.Combine(manifest.Path, s))
            .Where(File.Exists)
            .ToList();

        var warnings = securityScanner.ScanSources(sourceFiles);

        // Group by category
        var byCategory = warnings
            .GroupBy(w => w.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        return JsonSerializer.Serialize(new
        {
            modpack = name,
            hasSource = true,
            sourceFileCount = sourceFiles.Count,
            warnings = warnings.Select(w => new
            {
                severity = w.Severity.ToString(),
                category = w.Category,
                message = w.Message,
                file = w.File,
                line = w.Line
            }),
            warningsByCategory = byCategory,
            totalWarnings = warnings.Count,
            dangerCount = warnings.Count(w => w.Severity == SecuritySeverity.Danger),
            warningOnlyCount = warnings.Count(w => w.Severity == SecuritySeverity.Warning)
        }, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
