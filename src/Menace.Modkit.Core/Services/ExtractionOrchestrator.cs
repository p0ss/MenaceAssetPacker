using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Menace.Modkit.Core.Models;

namespace Menace.Modkit.Core.Services;

/// <summary>
/// Orchestrates extraction pipeline with smart caching and version tracking
/// </summary>
public class ExtractionOrchestrator
{
    private readonly string _gameInstallPath;
    private readonly string _cacheDirectory;
    private readonly string _manifestPath;
    private ExtractionManifest _manifest;

    public ExtractionOrchestrator(string gameInstallPath)
    {
        _gameInstallPath = gameInstallPath;
        _cacheDirectory = Path.Combine(gameInstallPath, "UserData", "ExtractionCache");
        _manifestPath = Path.Combine(_cacheDirectory, "manifest.json");

        Directory.CreateDirectory(_cacheDirectory);
        _manifest = LoadManifest();
    }

    /// <summary>
    /// Determine what needs to be extracted/regenerated
    /// </summary>
    public async Task<ExtractionPlan> PlanExtractionAsync()
    {
        var plan = new ExtractionPlan();

        // Calculate current hashes
        var gameAssemblyPath = GetGameAssemblyPath();
        var metadataPath = GetMetadataPath();

        var gameAssemblyHash = await ComputeFileHashAsync(gameAssemblyPath);
        var metadataHash = await ComputeFileHashAsync(metadataPath);

        // Check IL2CPP dump
        plan.NeedsIL2CppDump = _manifest.NeedsIL2CppDumpUpdate(gameAssemblyHash, metadataHash);

        // Check template code generation
        plan.NeedsTemplateCodeGen = _manifest.NeedsTemplateCodeUpdate(gameAssemblyHash, metadataHash);

        // Check data extraction
        var dataExtractorPath = Path.Combine(_gameInstallPath, "Mods", "Menace.DataExtractor.dll");
        var dataExtractorHash = File.Exists(dataExtractorPath)
            ? await ComputeFileHashAsync(dataExtractorPath)
            : null;
        plan.NeedsDataExtraction = _manifest.NeedsDataExtraction(gameAssemblyHash, dataExtractorHash);

        // Check asset ripping
        plan.NeedsAssetRip = _manifest.NeedsAssetRip(gameAssemblyHash);

        // Store hashes for later update
        plan.CurrentGameAssemblyHash = gameAssemblyHash;
        plan.CurrentMetadataHash = metadataHash;
        plan.CurrentDataExtractorHash = dataExtractorHash;

        return plan;
    }

    /// <summary>
    /// Update manifest after successful extraction
    /// </summary>
    public void UpdateManifest(ExtractionPlan plan, ExtractionResults results)
    {
        if (results.IL2CppDumpGenerated)
        {
            _manifest.GameAssemblyHash = plan.CurrentGameAssemblyHash;
            _manifest.MetadataHash = plan.CurrentMetadataHash;
            _manifest.IL2CppDumpTimestamp = DateTime.UtcNow;
            _manifest.MinimalDumpPath = results.MinimalDumpPath;
            _manifest.FullDumpPath = results.FullDumpPath;
        }

        if (results.TemplateCodeGenerated)
        {
            _manifest.TemplateCodeGenerationTimestamp = DateTime.UtcNow;
        }

        if (results.DataExtracted)
        {
            _manifest.DataExtractionTimestamp = DateTime.UtcNow;
            _manifest.DataExtractorHash = plan.CurrentDataExtractorHash;
            _manifest.ExtractedTemplates = results.ExtractedTemplateTypes ?? new List<string>();
        }

        if (results.AssetsRipped)
        {
            _manifest.AssetRipTimestamp = DateTime.UtcNow;
        }

        SaveManifest();
    }

    /// <summary>
    /// Get path to minimal IL2CPP dump (templates only, for fast regeneration)
    /// </summary>
    public string GetMinimalDumpPath()
    {
        return Path.Combine(_cacheDirectory, "il2cpp_templates.dump");
    }

    /// <summary>
    /// Get path to full IL2CPP dump (kept for reference)
    /// </summary>
    public string GetFullDumpPath()
    {
        return Path.Combine(_cacheDirectory, "il2cpp_full.dump");
    }

    private ExtractionManifest LoadManifest()
    {
        if (File.Exists(_manifestPath))
        {
            try
            {
                var json = File.ReadAllText(_manifestPath);
                return JsonSerializer.Deserialize<ExtractionManifest>(json)
                    ?? new ExtractionManifest();
            }
            catch
            {
                return new ExtractionManifest();
            }
        }

        return new ExtractionManifest();
    }

    private void SaveManifest()
    {
        var json = JsonSerializer.Serialize(_manifest, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(_manifestPath, json);
    }

    private async Task<string> ComputeFileHashAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return string.Empty;

        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private string GetGameAssemblyPath()
    {
        // Try .so first (Linux), then .dll (Windows)
        var soPath = Path.Combine(_gameInstallPath, "GameAssembly.so");
        if (File.Exists(soPath))
            return soPath;

        return Path.Combine(_gameInstallPath, "GameAssembly.dll");
    }

    private string GetMetadataPath()
    {
        var dataFolder = FindGameDataFolder(_gameInstallPath) ?? "Menace_Data";
        return Path.Combine(_gameInstallPath, dataFolder, "il2cpp_data", "Metadata", "global-metadata.dat");
    }

    /// <summary>
    /// Finds the game data folder name using case-insensitive search on Linux.
    /// Returns the actual folder name found, or null if not found.
    /// </summary>
    private static string? FindGameDataFolder(string gameInstallPath)
    {
        if (!Directory.Exists(gameInstallPath))
            return null;

        // Direct check (works on case-insensitive filesystems like Windows/macOS)
        var expectedPath = Path.Combine(gameInstallPath, "Menace_Data");
        if (Directory.Exists(expectedPath))
            return "Menace_Data";

        // On case-sensitive filesystems (Linux), search for the folder
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(gameInstallPath))
                {
                    var dirName = Path.GetFileName(dir);
                    if (dirName != null && dirName.Equals("Menace_Data", StringComparison.OrdinalIgnoreCase))
                        return dirName;
                }
            }
            catch
            {
                // Directory access issues
            }
        }

        return null;
    }
}

/// <summary>
/// Plan for what extraction operations are needed
/// </summary>
public class ExtractionPlan
{
    public bool NeedsIL2CppDump { get; set; }
    public bool NeedsTemplateCodeGen { get; set; }
    public bool NeedsDataExtraction { get; set; }
    public bool NeedsAssetRip { get; set; }

    public string CurrentGameAssemblyHash { get; set; } = string.Empty;
    public string CurrentMetadataHash { get; set; } = string.Empty;
    public string? CurrentDataExtractorHash { get; set; }

    public bool NeedsAnyOperation => NeedsIL2CppDump || NeedsTemplateCodeGen ||
                                     NeedsDataExtraction || NeedsAssetRip;
}

/// <summary>
/// Results of extraction operations
/// </summary>
public class ExtractionResults
{
    public bool IL2CppDumpGenerated { get; set; }
    public bool TemplateCodeGenerated { get; set; }
    public bool DataExtracted { get; set; }
    public bool AssetsRipped { get; set; }

    public string? MinimalDumpPath { get; set; }
    public string? FullDumpPath { get; set; }
    public List<string>? ExtractedTemplateTypes { get; set; }
}
