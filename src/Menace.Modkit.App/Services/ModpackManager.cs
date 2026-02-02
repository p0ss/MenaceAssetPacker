using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Manages modpack staging, vanilla data, and active mods
/// </summary>
public class ModpackManager
{
    private readonly string _vanillaDataPath;
    private readonly string _stagingBasePath;
    private readonly string _modsBasePath;

    public ModpackManager()
    {
        // Staging area for work-in-progress mods (always in Documents)
        _stagingBasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MenaceModkit", "staging");

        // Game paths will be computed from AppSettings
        _vanillaDataPath = string.Empty;
        _modsBasePath = string.Empty;

        EnsureDirectoriesExist();
    }

    public string VanillaDataPath
    {
        get
        {
            var gameInstallPath = AppSettings.Instance.GameInstallPath;
            if (string.IsNullOrEmpty(gameInstallPath))
                return string.Empty;

            return Path.Combine(gameInstallPath, "UserData", "ExtractedData");
        }
    }

    public string StagingBasePath => _stagingBasePath;

    public string ModsBasePath
    {
        get
        {
            var gameInstallPath = AppSettings.Instance.GameInstallPath;
            if (string.IsNullOrEmpty(gameInstallPath))
                return string.Empty;

            return Path.Combine(gameInstallPath, "Mods");
        }
    }

    /// <summary>
    /// Get the game install path
    /// </summary>
    public string GetGameInstallPath()
    {
        return AppSettings.Instance.GameInstallPath;
    }

    /// <summary>
    /// Check if vanilla data exists
    /// </summary>
    public bool HasVanillaData()
    {
        return !string.IsNullOrEmpty(VanillaDataPath) &&
               Directory.Exists(VanillaDataPath) &&
               Directory.GetFiles(VanillaDataPath, "*.json").Any();
    }

    /// <summary>
    /// Get all staging modpacks
    /// </summary>
    public List<ModpackInfo> GetStagingModpacks()
    {
        if (!Directory.Exists(_stagingBasePath))
            return new List<ModpackInfo>();

        return Directory.GetDirectories(_stagingBasePath)
            .Select(dir => LoadModpackInfo(dir))
            .Where(info => info != null)
            .ToList()!;
    }

    /// <summary>
    /// Get all active mods
    /// </summary>
    public List<ModpackInfo> GetActiveMods()
    {
        if (string.IsNullOrEmpty(ModsBasePath) || !Directory.Exists(ModsBasePath))
            return new List<ModpackInfo>();

        return Directory.GetDirectories(ModsBasePath)
            .Select(dir => LoadModpackInfo(dir))
            .Where(info => info != null)
            .ToList()!;
    }

    /// <summary>
    /// Create a new modpack in staging
    /// </summary>
    public ModpackInfo CreateModpack(string name, string author, string description)
    {
        var modpackDir = Path.Combine(_stagingBasePath, SanitizeName(name));
        Directory.CreateDirectory(modpackDir);
        Directory.CreateDirectory(Path.Combine(modpackDir, "stats"));
        Directory.CreateDirectory(Path.Combine(modpackDir, "assets"));

        var info = new ModpackInfo
        {
            Name = name,
            Author = author,
            Description = description,
            Version = "1.0.0",
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now,
            Path = modpackDir
        };

        SaveModpackInfo(info);
        return info;
    }

    /// <summary>
    /// Get vanilla template data
    /// </summary>
    public string? GetVanillaTemplatePath(string templateType)
    {
        if (string.IsNullOrEmpty(VanillaDataPath))
            return null;

        var path = Path.Combine(VanillaDataPath, $"{templateType}.json");
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Get staging template data for a modpack
    /// </summary>
    public string? GetStagingTemplatePath(string modpackName, string templateType)
    {
        var path = Path.Combine(_stagingBasePath, modpackName, "stats", $"{templateType}.json");
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Save modified template to staging
    /// </summary>
    public void SaveStagingTemplate(string modpackName, string templateType, string jsonContent)
    {
        var modpackDir = Path.Combine(_stagingBasePath, modpackName);
        var statsDir = Path.Combine(modpackDir, "stats");
        Directory.CreateDirectory(statsDir);

        var path = Path.Combine(statsDir, $"{templateType}.json");
        File.WriteAllText(path, jsonContent);

        // Update modpack modified date
        var info = LoadModpackInfo(modpackDir);
        if (info != null)
        {
            info.ModifiedDate = DateTime.Now;
            SaveModpackInfo(info);
        }
    }

    /// <summary>
    /// Deploy modpack to active mods (copy staging to Mods folder).
    /// Builds a runtime-compatible modpack.json that merges metadata,
    /// stats/*.json template overrides, and asset entries.
    /// </summary>
    public void DeployModpack(string modpackName)
    {
        if (string.IsNullOrEmpty(ModsBasePath))
            throw new InvalidOperationException("Game install path not set");

        var stagingPath = Path.Combine(_stagingBasePath, modpackName);
        var modsPath = Path.Combine(ModsBasePath, modpackName);

        if (!Directory.Exists(stagingPath))
            throw new DirectoryNotFoundException($"Staging modpack not found: {modpackName}");

        // Copy entire staging directory to mods
        CopyDirectory(stagingPath, modsPath);

        // Overwrite the deployed modpack.json with a runtime-compatible manifest
        BuildRuntimeManifest(stagingPath, modsPath);
    }

    /// <summary>
    /// Builds a modpack.json in the deploy directory that the runtime ModpackLoader can read.
    /// The loader expects lowercase keys: name, version, author, templates, assets.
    /// Templates are merged from stats/*.json files and any existing manifest entries.
    /// </summary>
    private void BuildRuntimeManifest(string stagingPath, string deployPath)
    {
        var info = LoadModpackInfo(stagingPath);

        var manifest = new JsonObject
        {
            ["name"] = info?.Name ?? System.IO.Path.GetFileName(stagingPath),
            ["version"] = info?.Version ?? "1.0.0",
            ["author"] = info?.Author ?? "Unknown"
        };

        // Collect template overrides from stats/*.json files
        var templates = new JsonObject();
        var statsDir = System.IO.Path.Combine(stagingPath, "stats");
        if (Directory.Exists(statsDir))
        {
            foreach (var statsFile in Directory.GetFiles(statsDir, "*.json"))
            {
                var templateType = System.IO.Path.GetFileNameWithoutExtension(statsFile);
                try
                {
                    var node = JsonNode.Parse(File.ReadAllText(statsFile));
                    if (node != null)
                        templates[templateType] = node;
                }
                catch { /* skip malformed files */ }
            }
        }

        // Also merge any templates/assets already in the staging manifest (e.g. from AssetBrowser)
        JsonObject? assets = null;
        var stagingManifestPath = System.IO.Path.Combine(stagingPath, "modpack.json");
        if (File.Exists(stagingManifestPath))
        {
            try
            {
                var existing = JsonNode.Parse(File.ReadAllText(stagingManifestPath))?.AsObject();
                if (existing != null)
                {
                    // Merge templates from manifest (stats/ files take priority)
                    var existingTemplates = (existing["templates"] ?? existing["Templates"]) as JsonObject;
                    if (existingTemplates != null)
                    {
                        foreach (var kvp in existingTemplates)
                        {
                            if (!templates.ContainsKey(kvp.Key) && kvp.Value != null)
                                templates[kvp.Key] = JsonNode.Parse(kvp.Value.ToJsonString());
                        }
                    }

                    // Preserve asset entries
                    var existingAssets = (existing["assets"] ?? existing["Assets"]) as JsonObject;
                    if (existingAssets != null)
                        assets = JsonNode.Parse(existingAssets.ToJsonString())?.AsObject();
                }
            }
            catch { }
        }

        manifest["templates"] = templates;
        manifest["assets"] = assets ?? new JsonObject();

        var deployManifestPath = System.IO.Path.Combine(deployPath, "modpack.json");
        File.WriteAllText(deployManifestPath, manifest.ToJsonString(
            new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Export modpack as a distributable package
    /// </summary>
    public void ExportModpack(string modpackName, string exportPath)
    {
        var stagingPath = Path.Combine(_stagingBasePath, modpackName);

        if (!Directory.Exists(stagingPath))
            throw new DirectoryNotFoundException($"Staging modpack not found: {modpackName}");

        // Create zip/tar archive
        var archivePath = Path.Combine(exportPath, $"{modpackName}.zip");
        System.IO.Compression.ZipFile.CreateFromDirectory(stagingPath, archivePath);
    }

    /// <summary>
    /// Update modpack metadata (save manifest), preserving existing templates/assets fields
    /// </summary>
    public void UpdateModpackMetadata(ModpackInfo modpack)
    {
        WriteModpackMetadata(modpack);
    }

    private void EnsureDirectoriesExist()
    {
        // Always create staging directory (it's in Documents, always valid)
        Directory.CreateDirectory(_stagingBasePath);

        // Only create game directories if game path is set
        if (!string.IsNullOrEmpty(VanillaDataPath))
        {
            Directory.CreateDirectory(VanillaDataPath);
        }

        if (!string.IsNullOrEmpty(ModsBasePath))
        {
            Directory.CreateDirectory(ModsBasePath);
        }
    }

    private static readonly JsonSerializerOptions _caseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private ModpackInfo? LoadModpackInfo(string modpackDir)
    {
        var infoPath = Path.Combine(modpackDir, "modpack.json");

        if (!File.Exists(infoPath))
            return null;

        try
        {
            var json = File.ReadAllText(infoPath);
            var info = JsonSerializer.Deserialize<ModpackInfo>(json, _caseInsensitiveOptions);
            if (info != null)
            {
                info.Path = modpackDir;
            }
            return info;
        }
        catch
        {
            return null;
        }
    }

    private void SaveModpackInfo(ModpackInfo info)
    {
        WriteModpackMetadata(info);
    }

    /// <summary>
    /// Write modpack metadata to modpack.json, preserving any existing
    /// non-metadata fields (templates, assets) that other tools may have written.
    /// </summary>
    private void WriteModpackMetadata(ModpackInfo info)
    {
        var infoPath = Path.Combine(info.Path, "modpack.json");

        // Read existing manifest to preserve templates/assets fields
        JsonObject manifest;
        if (File.Exists(infoPath))
        {
            try { manifest = JsonNode.Parse(File.ReadAllText(infoPath))?.AsObject() ?? new JsonObject(); }
            catch { manifest = new JsonObject(); }
        }
        else
        {
            manifest = new JsonObject();
        }

        // Update only metadata fields
        manifest["Name"] = info.Name;
        manifest["Author"] = info.Author;
        manifest["Description"] = info.Description;
        manifest["Version"] = info.Version;
        manifest["CreatedDate"] = info.CreatedDate.ToString("o");
        manifest["ModifiedDate"] = info.ModifiedDate.ToString("o");

        File.WriteAllText(infoPath, manifest.ToJsonString(
            new JsonSerializerOptions { WriteIndented = true }));
    }

    private string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }

    private void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }
}

/// <summary>
/// Metadata for a modpack
/// </summary>
public class ModpackInfo
{
    public string Name { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public string Path { get; set; } = string.Empty;
}
