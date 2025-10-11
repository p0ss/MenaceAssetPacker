using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

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
        // Vanilla extracted data (read-only)
        _vanillaDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".steam", "debian-installation", "steamapps", "common", "Menace Demo",
            "UserData", "ExtractedData");

        // Staging area for work-in-progress mods
        _stagingBasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MenaceModkit", "staging");

        // Active mods deployed to game
        _modsBasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".steam", "debian-installation", "steamapps", "common", "Menace Demo",
            "Mods");

        EnsureDirectoriesExist();
    }

    public string VanillaDataPath => _vanillaDataPath;
    public string StagingBasePath => _stagingBasePath;
    public string ModsBasePath => _modsBasePath;

    /// <summary>
    /// Check if vanilla data exists
    /// </summary>
    public bool HasVanillaData()
    {
        return Directory.Exists(_vanillaDataPath) &&
               Directory.GetFiles(_vanillaDataPath, "*.json").Any();
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
        if (!Directory.Exists(_modsBasePath))
            return new List<ModpackInfo>();

        return Directory.GetDirectories(_modsBasePath)
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
        var path = Path.Combine(_vanillaDataPath, $"{templateType}.json");
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
    /// Deploy modpack to active mods (copy staging to Mods folder)
    /// </summary>
    public void DeployModpack(string modpackName)
    {
        var stagingPath = Path.Combine(_stagingBasePath, modpackName);
        var modsPath = Path.Combine(_modsBasePath, modpackName);

        if (!Directory.Exists(stagingPath))
            throw new DirectoryNotFoundException($"Staging modpack not found: {modpackName}");

        // Copy entire staging directory to mods
        CopyDirectory(stagingPath, modsPath);
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
    /// Update modpack metadata (save manifest)
    /// </summary>
    public void UpdateModpackMetadata(ModpackInfo modpack)
    {
        var manifestPath = Path.Combine(modpack.Path, "modpack.json");
        var json = JsonSerializer.Serialize(modpack, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(manifestPath, json);
    }

    private void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(_stagingBasePath);
        Directory.CreateDirectory(_modsBasePath);
    }

    private ModpackInfo? LoadModpackInfo(string modpackDir)
    {
        var infoPath = Path.Combine(modpackDir, "modpack.json");

        if (!File.Exists(infoPath))
            return null;

        try
        {
            var json = File.ReadAllText(infoPath);
            var info = JsonSerializer.Deserialize<ModpackInfo>(json);
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
        var infoPath = Path.Combine(info.Path, "modpack.json");
        var json = JsonSerializer.Serialize(info, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(infoPath, json);
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
