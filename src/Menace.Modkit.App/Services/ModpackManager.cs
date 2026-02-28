using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Menace.Modkit.App.Models;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Manages modpack staging, vanilla data, and active mods.
/// Uses ModpackManifest (v2) internally; auto-migrates legacy v1 manifests on load.
/// </summary>
public class ModpackManager
{
    /// <summary>
    /// Regex pattern for valid modpack names.
    /// Allows alphanumeric characters, underscores, hyphens, periods, and spaces.
    /// </summary>
    private static readonly Regex ValidModpackNameRegex = new(@"^[a-zA-Z0-9_\-. ]+$", RegexOptions.Compiled);

    /// <summary>
    /// Maximum length for modpack names to prevent filesystem issues.
    /// </summary>
    private const int MaxModpackNameLength = 64;

    private readonly string _stagingBasePath;

    public ModpackManager()
    {
        _stagingBasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MenaceModkit", "staging");

        EnsureDirectoriesExist();
        SeedBundledRuntimeDlls();
        SeedBundledModpacks();
        SeedDownloadedAddons();
    }

    public string StagingBasePath => _stagingBasePath;

    /// <summary>
    /// Directory containing runtime DLLs (ModpackLoader, DataExtractor, DevMode)
    /// that should be deployed to the game's Mods/ root alongside modpacks.
    /// </summary>
    public string RuntimeDllsPath =>
        Path.Combine(Path.GetDirectoryName(_stagingBasePath)!, "runtime");

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

    public string GetGameInstallPath() => AppSettings.Instance.GameInstallPath;

    public bool HasVanillaData()
    {
        return !string.IsNullOrEmpty(VanillaDataPath) &&
               Directory.Exists(VanillaDataPath) &&
               Directory.GetFiles(VanillaDataPath, "*.json").Any();
    }

    // ---------------------------------------------------------------
    // Modpack CRUD
    // ---------------------------------------------------------------

    public List<ModpackManifest> GetStagingModpacks()
    {
        if (!Directory.Exists(_stagingBasePath))
            return new List<ModpackManifest>();

        return Directory.GetDirectories(_stagingBasePath)
            .Select(dir => LoadManifest(dir))
            .Where(m => m != null)
            .ToList()!;
    }

    public List<ModpackManifest> GetActiveMods()
    {
        if (string.IsNullOrEmpty(ModsBasePath) || !Directory.Exists(ModsBasePath))
            return new List<ModpackManifest>();

        return Directory.GetDirectories(ModsBasePath)
            .Select(dir => LoadManifest(dir))
            .Where(m => m != null)
            .ToList()!;
    }

    /// <summary>
    /// Get active modpacks ordered by LoadOrder (ascending), then by name.
    /// </summary>
    public List<ModpackManifest> GetOrderedActiveModpacks()
    {
        return GetActiveMods()
            .OrderBy(m => m.LoadOrder)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public ModpackManifest CreateModpack(string name, string author, string description)
    {
        ValidateModpackName(name);

        var modpackDir = Path.Combine(_stagingBasePath, SanitizeName(name));
        Directory.CreateDirectory(modpackDir);
        Directory.CreateDirectory(Path.Combine(modpackDir, "stats"));
        Directory.CreateDirectory(Path.Combine(modpackDir, "clones"));
        Directory.CreateDirectory(Path.Combine(modpackDir, "assets"));
        Directory.CreateDirectory(Path.Combine(modpackDir, "src"));

        var manifest = new ModpackManifest
        {
            Name = name,
            Author = author,
            Description = description,
            Version = "1.0.0",
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now,
            Path = modpackDir
        };

        manifest.SaveToFile();
        return manifest;
    }

    /// <summary>
    /// Delete a staging modpack entirely.
    /// </summary>
    public bool DeleteStagingModpack(string modpackName)
    {
        var dir = Path.Combine(_stagingBasePath, modpackName);
        if (!Directory.Exists(dir))
            return false;

        Directory.Delete(dir, true);
        return true;
    }

    /// <summary>
    /// Remove a single deployed mod from the game's Mods/ folder.
    /// </summary>
    public bool UndeployMod(string modpackName)
    {
        if (string.IsNullOrEmpty(ModsBasePath))
            return false;

        var dir = Path.Combine(ModsBasePath, modpackName);
        if (!Directory.Exists(dir))
            return false;

        Directory.Delete(dir, true);
        return true;
    }

    // ---------------------------------------------------------------
    // Template / stats operations
    // ---------------------------------------------------------------

    public string? GetVanillaTemplatePath(string templateType)
    {
        if (string.IsNullOrEmpty(VanillaDataPath))
            return null;
        var path = Path.Combine(VanillaDataPath, $"{templateType}.json");
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Resolve a modpack display name to its staging directory path.
    /// The directory name may differ from the manifest Name field
    /// (e.g. dir "DevMode-modpack" → manifest name "DevMode").
    /// Falls back to using the name directly if no match is found.
    /// </summary>
    public string ResolveStagingDir(string modpackName)
    {
        // Fast path: directory name matches manifest name
        var direct = Path.Combine(_stagingBasePath, modpackName);
        if (Directory.Exists(direct))
            return direct;

        // Scan staging directories for a manifest whose Name matches
        if (Directory.Exists(_stagingBasePath))
        {
            foreach (var dir in Directory.GetDirectories(_stagingBasePath))
            {
                var manifest = LoadManifest(dir);
                if (manifest != null && string.Equals(manifest.Name, modpackName, StringComparison.OrdinalIgnoreCase))
                    return dir;
            }
        }

        // Nothing found — return the direct path (will be created on write)
        return direct;
    }

    public string? GetStagingTemplatePath(string modpackName, string templateType)
    {
        var path = Path.Combine(ResolveStagingDir(modpackName), "stats", $"{templateType}.json");
        return File.Exists(path) ? path : null;
    }

    public void SaveStagingTemplate(string modpackName, string templateType, string jsonContent)
    {
        var modpackDir = ResolveStagingDir(modpackName);
        var statsDir = Path.Combine(modpackDir, "stats");
        Directory.CreateDirectory(statsDir);

        var path = Path.Combine(statsDir, $"{templateType}.json");
        File.WriteAllText(path, jsonContent);

        TouchModified(modpackDir);
    }

    /// <summary>
    /// Delete a staging template file (when all overrides for that type are removed).
    /// </summary>
    public void DeleteStagingTemplate(string modpackName, string templateType)
    {
        var modpackDir = ResolveStagingDir(modpackName);
        var path = Path.Combine(modpackDir, "stats", $"{templateType}.json");

        if (File.Exists(path))
        {
            File.Delete(path);
            TouchModified(modpackDir);
        }
    }

    // ---------------------------------------------------------------
    // Clone operations
    // ---------------------------------------------------------------

    /// <summary>
    /// Save clone definitions for a specific template type.
    /// JSON format: { "newName": "sourceName", ... }
    /// </summary>
    public void SaveStagingClones(string modpackName, string templateType, string jsonContent)
    {
        var modpackDir = ResolveStagingDir(modpackName);
        var clonesDir = Path.Combine(modpackDir, "clones");
        Directory.CreateDirectory(clonesDir);

        var path = Path.Combine(clonesDir, $"{templateType}.json");
        File.WriteAllText(path, jsonContent);

        TouchModified(modpackDir);
    }

    /// <summary>
    /// Delete clone definitions for a specific template type.
    /// </summary>
    public void DeleteStagingClones(string modpackName, string templateType)
    {
        var path = Path.Combine(ResolveStagingDir(modpackName), "clones", $"{templateType}.json");
        if (File.Exists(path))
        {
            File.Delete(path);
            TouchModified(ResolveStagingDir(modpackName));
        }
    }

    /// <summary>
    /// Load all clone definitions from a staging modpack.
    /// Returns templateType → { newName → sourceName }
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> LoadStagingClones(string modpackName)
    {
        var result = new Dictionary<string, Dictionary<string, string>>();
        var clonesDir = Path.Combine(ResolveStagingDir(modpackName), "clones");

        if (!Directory.Exists(clonesDir))
            return result;

        foreach (var file in Directory.GetFiles(clonesDir, "*.json"))
        {
            var templateType = Path.GetFileNameWithoutExtension(file);
            try
            {
                var json = File.ReadAllText(file);
                var clones = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (clones != null && clones.Count > 0)
                    result[templateType] = clones;
            }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
            {
                ModkitLog.Warn($"[ModpackManager] Failed to load clones from {file}: {ex.Message}");
            }
        }

        return result;
    }

    // ---------------------------------------------------------------
    // Asset operations
    // ---------------------------------------------------------------

    public void SaveStagingAsset(string modpackName, string relativePath, string sourceFile)
    {
        var modpackDir = ResolveStagingDir(modpackName);
        var assetDir = Path.Combine(modpackDir, "assets");
        var destPath = Path.Combine(assetDir, relativePath);
        var destDir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);
        File.Copy(sourceFile, destPath, true);

        SyncAssetManifest(modpackDir);
        TouchModified(modpackDir);
    }

    public string? GetStagingAssetPath(string modpackName, string relativePath)
    {
        var path = Path.Combine(ResolveStagingDir(modpackName), "assets", relativePath);
        return File.Exists(path) ? path : null;
    }

    public void RemoveStagingAsset(string modpackName, string relativePath)
    {
        var modpackDir = ResolveStagingDir(modpackName);
        var path = Path.Combine(modpackDir, "assets", relativePath);
        if (File.Exists(path))
            File.Delete(path);

        SyncAssetManifest(modpackDir);
        TouchModified(modpackDir);
    }

    public List<string> GetStagingAssetPaths(string modpackName)
    {
        var assetsDir = Path.Combine(ResolveStagingDir(modpackName), "assets");
        if (!Directory.Exists(assetsDir))
            return new List<string>();

        return Directory.GetFiles(assetsDir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(assetsDir, f))
            .ToList();
    }

    // ---------------------------------------------------------------
    // Unity Bundle operations
    // ---------------------------------------------------------------

    /// <summary>
    /// Import a Unity AssetBundle (.bundle) file into a modpack.
    /// Bundles are stored in the bundles/ subfolder and registered in the manifest.
    /// Use this for complex Unity assets (animated prefabs, custom shaders, etc.)
    /// that can't be created through the normal Modkit workflow.
    /// </summary>
    public void ImportUnityBundle(string modpackName, string bundleSourcePath)
    {
        var modpackDir = ResolveStagingDir(modpackName);
        var bundlesDir = Path.Combine(modpackDir, "bundles");
        Directory.CreateDirectory(bundlesDir);

        var fileName = Path.GetFileName(bundleSourcePath);
        var destPath = Path.Combine(bundlesDir, fileName);
        File.Copy(bundleSourcePath, destPath, true);

        // Update manifest
        var manifest = LoadManifest(modpackDir);
        if (manifest != null)
        {
            var bundlePath = $"bundles/{fileName}";
            if (!manifest.Bundles.Contains(bundlePath))
            {
                manifest.Bundles.Add(bundlePath);
                manifest.SaveToFile();
            }
        }

        TouchModified(modpackDir);
    }

    /// <summary>
    /// Remove a Unity bundle from a modpack.
    /// </summary>
    public void RemoveUnityBundle(string modpackName, string bundleFileName)
    {
        var modpackDir = ResolveStagingDir(modpackName);
        var bundlePath = Path.Combine(modpackDir, "bundles", bundleFileName);
        if (File.Exists(bundlePath))
            File.Delete(bundlePath);

        // Update manifest
        var manifest = LoadManifest(modpackDir);
        if (manifest != null)
        {
            manifest.Bundles.RemoveAll(b => b.EndsWith(bundleFileName, StringComparison.OrdinalIgnoreCase));
            manifest.SaveToFile();
        }

        TouchModified(modpackDir);
    }

    /// <summary>
    /// Get list of Unity bundles in a modpack.
    /// </summary>
    public List<string> GetUnityBundles(string modpackName)
    {
        var bundlesDir = Path.Combine(ResolveStagingDir(modpackName), "bundles");
        if (!Directory.Exists(bundlesDir))
            return new List<string>();

        return Directory.GetFiles(bundlesDir, "*.bundle", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .ToList()!;
    }

    // ---------------------------------------------------------------
    // Source code operations (Phase 0 + Phase 3)
    // ---------------------------------------------------------------

    /// <summary>
    /// Get all .cs source file paths (relative to modpack root) in a staging modpack.
    /// </summary>
    public List<string> GetStagingSources(string modpackName)
    {
        var modpackDir = ResolveStagingDir(modpackName);
        var srcDir = Path.Combine(modpackDir, "src");
        if (!Directory.Exists(srcDir))
            return new List<string>();

        return Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(modpackDir, f))
            .ToList();
    }

    /// <summary>
    /// Save a source file to the modpack's src/ directory.
    /// </summary>
    public void SaveStagingSource(string modpackName, string relativePath, string content)
    {
        var modpackDir = ResolveStagingDir(modpackName);
        var fullPath = Path.Combine(modpackDir, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(fullPath, content);
        SyncSourceManifest(modpackDir);
        TouchModified(modpackDir);
    }

    /// <summary>
    /// Add a new source file (creates it with a template).
    /// </summary>
    public void AddStagingSource(string modpackName, string relativePath)
    {
        var modpackDir = ResolveStagingDir(modpackName);
        var fullPath = Path.Combine(modpackDir, relativePath);

        if (File.Exists(fullPath))
            return;

        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var className = Path.GetFileNameWithoutExtension(relativePath);
        File.WriteAllText(fullPath, $"using MelonLoader;\n\nnamespace {SanitizeName(modpackName)};\n\npublic class {className}\n{{\n}}\n");

        SyncSourceManifest(modpackDir);
        TouchModified(modpackDir);
    }

    /// <summary>
    /// Remove a source file from a staging modpack.
    /// </summary>
    public void RemoveStagingSource(string modpackName, string relativePath)
    {
        var modpackDir = ResolveStagingDir(modpackName);
        var fullPath = Path.Combine(modpackDir, relativePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        SyncSourceManifest(modpackDir);
        TouchModified(modpackDir);
    }

    /// <summary>
    /// Read the content of a source file.
    /// </summary>
    public string? ReadStagingSource(string modpackName, string relativePath)
    {
        var fullPath = Path.Combine(ResolveStagingDir(modpackName), relativePath);
        return File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
    }

    // ---------------------------------------------------------------
    // Deploy (legacy single-modpack deploy — kept for backward compat,
    // Phase 2 introduces DeployManager for the full pipeline)
    // ---------------------------------------------------------------

    public void DeployModpack(string modpackName)
    {
        if (string.IsNullOrEmpty(ModsBasePath))
            throw new InvalidOperationException("Game install path not set");

        var stagingPath = ResolveStagingDir(modpackName);
        var modsPath = Path.Combine(ModsBasePath, Path.GetFileName(stagingPath));

        if (!Directory.Exists(stagingPath))
            throw new DirectoryNotFoundException($"Staging modpack not found: {modpackName}");

        CopyDirectory(stagingPath, modsPath);
        BuildRuntimeManifest(stagingPath, modsPath);
    }

    public void ExportModpack(string modpackName, string exportPath)
    {
        var stagingPath = ResolveStagingDir(modpackName);
        if (!Directory.Exists(stagingPath))
            throw new DirectoryNotFoundException($"Staging modpack not found: {modpackName}");

        // exportPath is already the full path from the save file picker
        // Delete existing file if present (user confirmed overwrite in picker)
        if (File.Exists(exportPath))
            File.Delete(exportPath);

        ZipFile.CreateFromDirectory(stagingPath, exportPath);
        ModkitLog.Info($"[ModpackManager] Exported modpack to: {exportPath}");
    }

    /// <summary>
    /// Import a modpack from an archive file (.zip, .7z, .rar, .tar.gz, etc.) into the staging directory.
    /// If no manifest.json exists, one will be inferred from the archive contents.
    /// Returns the manifest of the imported modpack, or null if import failed.
    /// </summary>
    public ModpackManifest? ImportModpackFromArchive(string archivePath)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException($"Archive file not found: {archivePath}");

        // Extract to a temp directory first to inspect the contents
        var tempDir = Path.Combine(Path.GetTempPath(), $"modkit_import_{Guid.NewGuid():N}");
        try
        {
            ExtractArchive(archivePath, tempDir);

            // Find the manifest - it might be at root or in a subfolder
            var manifestPath = FindManifestInDirectory(tempDir);
            string contentDir;

            if (manifestPath != null)
            {
                contentDir = Path.GetDirectoryName(manifestPath)!;
            }
            else
            {
                // No manifest found - infer one from archive contents
                ModkitLog.Info($"[ModpackManager] No modpack.json found, inferring from contents...");
                contentDir = FindContentDirectory(tempDir);
                var inferredManifest = InferManifestFromContents(archivePath, contentDir);
                inferredManifest.SaveToFile(Path.Combine(contentDir, "modpack.json"));
                ModkitLog.Info($"[ModpackManager] Created inferred manifest: {inferredManifest.Name}");
            }

            var manifest = LoadManifest(contentDir);
            if (manifest == null)
            {
                throw new InvalidOperationException("Failed to load manifest");
            }

            // Determine target directory name (sanitize the modpack name)
            var safeName = SanitizeDirectoryName(manifest.Name);
            var targetDir = Path.Combine(_stagingBasePath, safeName);

            // Handle name conflicts by appending a number
            var originalName = safeName;
            var counter = 1;
            while (Directory.Exists(targetDir))
            {
                safeName = $"{originalName}_{counter}";
                targetDir = Path.Combine(_stagingBasePath, safeName);
                counter++;
            }

            // Copy the modpack contents to staging
            Directory.CreateDirectory(_stagingBasePath);
            CopyDirectory(contentDir, targetDir);

            // Update manifest path and reload
            manifest = LoadManifest(targetDir);
            if (manifest != null)
            {
                ModkitLog.Info($"[ModpackManager] Imported modpack: {manifest.Name}");
            }

            return manifest;
        }
        finally
        {
            // Clean up temp directory
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ModkitLog.Info($"[ModpackManager] Failed to clean up temp directory: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Find the directory containing the modpack content.
    /// If there's a single subfolder, use that (common for archives with a wrapper folder).
    /// Otherwise use the root.
    /// </summary>
    private static string FindContentDirectory(string extractedDir)
    {
        var subDirs = Directory.GetDirectories(extractedDir);
        var files = Directory.GetFiles(extractedDir);

        // If there's exactly one subfolder and no files at root, use the subfolder
        if (subDirs.Length == 1 && files.Length == 0)
            return subDirs[0];

        return extractedDir;
    }

    /// <summary>
    /// Infer a manifest from the archive contents.
    /// Detects stats/, assets/, source/ folders and populates the manifest accordingly.
    /// </summary>
    private static ModpackManifest InferManifestFromContents(string archivePath, string contentDir)
    {
        // Use archive filename (without extension) as the modpack name
        var archiveName = Path.GetFileNameWithoutExtension(archivePath);
        // Handle double extensions like .tar.gz
        if (archiveName.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
            archiveName = Path.GetFileNameWithoutExtension(archiveName);

        var manifest = new ModpackManifest
        {
            Name = archiveName,
            Author = "Unknown",
            Description = $"Imported from {Path.GetFileName(archivePath)}",
            Version = "1.0.0",
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now
        };

        // Detect what content exists
        var hasStats = Directory.Exists(Path.Combine(contentDir, "stats"));
        var hasAssets = Directory.Exists(Path.Combine(contentDir, "assets"));
        var hasSource = Directory.Exists(Path.Combine(contentDir, "source"));

        // Build description based on detected content
        var contentTypes = new List<string>();
        if (hasStats) contentTypes.Add("stat changes");
        if (hasAssets) contentTypes.Add("assets");
        if (hasSource) contentTypes.Add("code");

        if (contentTypes.Count > 0)
        {
            manifest.Description = $"Imported mod with {string.Join(", ", contentTypes)}";
        }

        return manifest;
    }

    /// <summary>
    /// Backwards-compatible alias for ImportModpackFromArchive.
    /// </summary>
    public ModpackManifest? ImportModpackFromZip(string zipPath) => ImportModpackFromArchive(zipPath);

    /// <summary>
    /// Extract an archive file to a directory using SharpCompress.
    /// Supports .zip, .7z, .rar, .tar, .tar.gz, .tar.bz2, etc.
    /// Validates each entry to prevent path traversal (Zip Slip) attacks.
    /// </summary>
    private static void ExtractArchive(string archivePath, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        using var archive = ArchiveFactory.Open(archivePath);
        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
        {
            var entryKey = entry.Key;
            if (string.IsNullOrEmpty(entryKey))
                continue;

            // Validate entry path to prevent path traversal attacks
            var destPath = PathValidator.ValidateArchiveEntryPath(destinationDir, entryKey);

            // Ensure the directory exists
            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir))
                Directory.CreateDirectory(destDir);

            entry.WriteToFile(destPath, new ExtractionOptions
            {
                ExtractFullPath = false,
                Overwrite = true
            });
        }
    }

    private static string? FindManifestInDirectory(string dir)
    {
        // Check root first - try both modpack.json (our format) and manifest.json (legacy/external)
        var rootModpack = Path.Combine(dir, "modpack.json");
        if (File.Exists(rootModpack))
            return rootModpack;

        var rootManifest = Path.Combine(dir, "manifest.json");
        if (File.Exists(rootManifest))
            return rootManifest;

        // Check one level of subdirectories (in case archive contains a wrapper folder)
        foreach (var subDir in Directory.GetDirectories(dir))
        {
            var subModpack = Path.Combine(subDir, "modpack.json");
            if (File.Exists(subModpack))
                return subModpack;

            var subManifest = Path.Combine(subDir, "manifest.json");
            if (File.Exists(subManifest))
                return subManifest;
        }

        return null;
    }

    private static string SanitizeDirectoryName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new System.Text.StringBuilder();
        foreach (var c in name)
        {
            result.Append(invalid.Contains(c) ? '_' : c);
        }
        return result.ToString().Trim();
    }

    // ---------------------------------------------------------------
    // Manifest persistence
    // ---------------------------------------------------------------

    public void UpdateModpackMetadata(ModpackManifest manifest)
    {
        manifest.ModifiedDate = DateTime.Now;
        manifest.SaveToFile();
    }

    /// <summary>
    /// Persist load-order values to a central config file alongside staging.
    /// </summary>
    public void SaveLoadOrder(List<(string modpackName, int order)> ordering)
    {
        foreach (var (modpackName, order) in ordering)
        {
            var dir = Path.Combine(_stagingBasePath, modpackName);
            var manifest = LoadManifest(dir);
            if (manifest != null)
            {
                manifest.LoadOrder = order;
                manifest.SaveToFile();
            }
        }
    }

    // ---------------------------------------------------------------
    // Internal helpers
    // ---------------------------------------------------------------

    private ModpackManifest? LoadManifest(string modpackDir)
    {
        // Try modpack.json first (our format), then manifest.json (legacy/external)
        var modpackPath = Path.Combine(modpackDir, "modpack.json");
        var manifestPath = Path.Combine(modpackDir, "manifest.json");

        var infoPath = File.Exists(modpackPath) ? modpackPath : manifestPath;

        try
        {
            var manifest = ModpackManifest.LoadFromFile(infoPath);
            if (manifest == null)
                return null;

            manifest.Path = modpackDir;

            // Auto-fix empty names by deriving from directory name
            if (string.IsNullOrWhiteSpace(manifest.Name))
            {
                var dirName = Path.GetFileName(modpackDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                // Strip common suffixes like "-modpack"
                var fixedName = dirName.Replace("-modpack", "").Replace("_modpack", "").Trim();
                if (!string.IsNullOrWhiteSpace(fixedName))
                {
                    manifest.Name = fixedName;
                    manifest.SaveToFile(modpackPath);
                    ModkitLog.Info($"[ModpackManager] Auto-fixed empty modpack name to '{fixedName}' (from directory '{dirName}')");
                }
                else
                {
                    ModkitLog.Warn($"[ModpackManager] Modpack at '{modpackDir}' has empty name - this may cause issues");
                }
            }

            // If loaded from manifest.json, save as modpack.json for consistency
            if (infoPath == manifestPath && File.Exists(manifestPath))
            {
                manifest.SaveToFile(modpackPath);
                // Optionally delete the old manifest.json
                // File.Delete(manifestPath);
            }

            return manifest;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Synchronize the manifest's Code.Sources list with the actual files in src/.
    /// </summary>
    private void SyncSourceManifest(string modpackDir)
    {
        var manifest = LoadManifest(modpackDir);
        if (manifest == null) return;

        var srcDir = Path.Combine(modpackDir, "src");
        if (Directory.Exists(srcDir))
        {
            manifest.Code.Sources = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(modpackDir, f))
                .ToList();
        }
        else
        {
            manifest.Code.Sources.Clear();
        }

        manifest.SaveToFile();
    }

    /// <summary>
    /// Synchronize the manifest's Assets dictionary with the actual files in assets/.
    /// Maps game asset paths (relative path of the file) to the replacement file path.
    /// </summary>
    private void SyncAssetManifest(string modpackDir)
    {
        var manifest = LoadManifest(modpackDir);
        if (manifest == null) return;

        var assetsDir = Path.Combine(modpackDir, "assets");
        manifest.Assets.Clear();

        if (Directory.Exists(assetsDir))
        {
            foreach (var file in Directory.GetFiles(assetsDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(assetsDir, file);
                // Map game asset path → replacement file path (relative to modpack root)
                manifest.Assets[relativePath] = Path.Combine("assets", relativePath);
            }
        }

        manifest.SaveToFile();
    }

    private void TouchModified(string modpackDir)
    {
        var manifest = LoadManifest(modpackDir);
        if (manifest != null)
        {
            manifest.ModifiedDate = DateTime.Now;
            manifest.SaveToFile();
        }
    }

    /// <summary>
    /// Builds a modpack.json in the deploy directory that the runtime ModpackLoader can read.
    /// Produces a hybrid manifest: v2 fields for new loaders, plus legacy "templates" for v1 loaders.
    /// </summary>
    private void BuildRuntimeManifest(string stagingPath, string deployPath)
    {
        var manifest = LoadManifest(stagingPath);

        var runtimeObj = new JsonObject
        {
            ["manifestVersion"] = 2,
            ["name"] = manifest?.Name ?? Path.GetFileName(stagingPath),
            ["version"] = manifest?.Version ?? "1.0.0",
            ["author"] = manifest?.Author ?? "Unknown",
            ["loadOrder"] = manifest?.LoadOrder ?? 100
        };

        // Collect template overrides from stats/*.json → build "patches" and legacy "templates"
        var patches = new JsonObject();
        var legacyTemplates = new JsonObject();
        var statsDir = Path.Combine(stagingPath, "stats");
        if (Directory.Exists(statsDir))
        {
            foreach (var statsFile in Directory.GetFiles(statsDir, "*.json"))
            {
                var templateType = Path.GetFileNameWithoutExtension(statsFile);
                try
                {
                    var node = JsonNode.Parse(File.ReadAllText(statsFile));
                    if (node != null)
                    {
                        patches[templateType] = JsonNode.Parse(node.ToJsonString());
                        legacyTemplates[templateType] = node;
                    }
                }
                catch { }
            }
        }

        // Merge existing manifest patches
        if (manifest?.Patches != null)
        {
            var patchJson = JsonSerializer.Serialize(manifest.Patches);
            var patchNode = JsonNode.Parse(patchJson)?.AsObject();
            if (patchNode != null)
            {
                foreach (var kvp in patchNode)
                {
                    if (!patches.ContainsKey(kvp.Key) && kvp.Value != null)
                        patches[kvp.Key] = JsonNode.Parse(kvp.Value.ToJsonString());
                }
            }
        }

        // Assets: start from manifest entries, then scan for unregistered files
        var assetsObj = new JsonObject();
        if (manifest?.Assets != null && manifest.Assets.Count > 0)
        {
            foreach (var kvp in manifest.Assets)
                assetsObj[kvp.Key] = kvp.Value;
        }

        // Fallback scan: pick up any files in assets/ not already in the manifest
        var assetsDir = Path.Combine(stagingPath, "assets");
        if (Directory.Exists(assetsDir))
        {
            foreach (var file in Directory.GetFiles(assetsDir, "*", SearchOption.AllDirectories))
            {
                var relPath = Path.GetRelativePath(assetsDir, file);
                if (!assetsObj.ContainsKey(relPath))
                    assetsObj[relPath] = Path.Combine("assets", relPath);
            }
        }

        // Clones
        var clones = new JsonObject();
        var clonesDir = Path.Combine(stagingPath, "clones");
        if (Directory.Exists(clonesDir))
        {
            foreach (var file in Directory.GetFiles(clonesDir, "*.json"))
            {
                var templateType = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var node = JsonNode.Parse(File.ReadAllText(file));
                    if (node != null)
                        clones[templateType] = node;
                }
                catch { }
            }
        }

        runtimeObj["clones"] = clones;
        runtimeObj["patches"] = patches;
        runtimeObj["templates"] = legacyTemplates;  // backward compat for v1 loader
        runtimeObj["assets"] = assetsObj;

        // Code info
        if (manifest?.Code != null && manifest.Code.HasAnyCode)
        {
            var codeObj = new JsonObject();
            codeObj["sources"] = new JsonArray(manifest.Code.Sources.Select(s => (JsonNode)JsonValue.Create(s)!).ToArray());
            codeObj["references"] = new JsonArray(manifest.Code.References.Select(r => (JsonNode)JsonValue.Create(r)!).ToArray());
            codeObj["prebuiltDlls"] = new JsonArray(manifest.Code.PrebuiltDlls.Select(d => (JsonNode)JsonValue.Create(d)!).ToArray());
            runtimeObj["code"] = codeObj;
        }

        // Bundles
        if (manifest?.Bundles != null && manifest.Bundles.Count > 0)
        {
            runtimeObj["bundles"] = new JsonArray(manifest.Bundles.Select(b => (JsonNode)JsonValue.Create(b)!).ToArray());
        }

        runtimeObj["securityStatus"] = manifest?.SecurityStatus.ToString() ?? "Unreviewed";

        var deployManifestPath = Path.Combine(deployPath, "modpack.json");
        File.WriteAllText(deployManifestPath, runtimeObj.ToJsonString(
            new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Get runtime DLLs available in the runtime/ directory.
    /// Returns list of (fileName, fullPath) pairs.
    /// </summary>
    public List<(string FileName, string FullPath)> GetRuntimeDlls()
    {
        if (!Directory.Exists(RuntimeDllsPath))
            return new List<(string, string)>();

        return Directory.GetFiles(RuntimeDllsPath, "*.dll")
            .Select(f => (Path.GetFileName(f), f))
            .ToList();
    }

    private void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(_stagingBasePath);
        Directory.CreateDirectory(RuntimeDllsPath);

        if (!string.IsNullOrEmpty(VanillaDataPath))
            Directory.CreateDirectory(VanillaDataPath);

        if (!string.IsNullOrEmpty(ModsBasePath))
            Directory.CreateDirectory(ModsBasePath);
    }

    /// <summary>
    /// Infrastructure DLL directories under third_party/bundled/ that should be
    /// copied into the runtime/ directory for automatic deployment with modpacks.
    /// </summary>
    private static readonly string[] BundledRuntimeDllDirs = { "DataExtractor", "ModpackLoader" };

    /// <summary>
    /// Copies bundled infrastructure DLLs into the runtime/ directory so they are
    /// automatically deployed alongside modpacks by DeployRuntimeDlls.
    /// Only overwrites when the bundled copy differs (size check).
    /// </summary>
    private void SeedBundledRuntimeDlls()
    {
        var bundledBase = Path.Combine(AppContext.BaseDirectory, "third_party", "bundled");
        var bundledDlls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dirName in BundledRuntimeDllDirs)
        {
            var srcDir = Path.Combine(bundledBase, dirName);
            if (!Directory.Exists(srcDir)) continue;

            foreach (var srcFile in Directory.GetFiles(srcDir, "*.dll"))
            {
                bundledDlls[Path.GetFileName(srcFile)] = srcFile;
            }
        }

        // Remove stale runtime DLLs that are no longer bundled.
        foreach (var existingFile in Directory.GetFiles(RuntimeDllsPath, "*.dll"))
        {
            var fileName = Path.GetFileName(existingFile);
            if (!bundledDlls.ContainsKey(fileName))
            {
                File.Delete(existingFile);
                ModkitLog.Info($"[ModpackManager] Removed stale runtime DLL: {fileName}");
            }
        }

        foreach (var (fileName, srcFile) in bundledDlls)
        {
            var destFile = Path.Combine(RuntimeDllsPath, fileName);
            bool needsCopy = !File.Exists(destFile);
            if (!needsCopy)
            {
                var srcInfo = new FileInfo(srcFile);
                var destInfo = new FileInfo(destFile);
                needsCopy = srcInfo.Length != destInfo.Length ||
                            srcInfo.LastWriteTimeUtc > destInfo.LastWriteTimeUtc;
            }

            if (needsCopy)
                File.Copy(srcFile, destFile, true);
        }
    }

    /// <summary>
    /// Copies bundled modpacks from third_party/bundled/modpacks/ into the staging
    /// directory if they don't already exist there.
    /// </summary>
    private void SeedBundledModpacks()
    {
        var bundledDir = Path.Combine(AppContext.BaseDirectory, "third_party", "bundled", "modpacks");
        if (!Directory.Exists(bundledDir))
            return;

        foreach (var modpackDir in Directory.GetDirectories(bundledDir))
        {
            var dirName = Path.GetFileName(modpackDir);
            var targetDir = Path.Combine(_stagingBasePath, dirName);

            if (!Directory.Exists(targetDir))
            {
                CopyDirectory(modpackDir, targetDir);
                continue;
            }

            // Update existing staging copy: overwrite files where the bundled
            // version is newer, so source fixes propagate without requiring
            // the user to delete their staging directory.
            UpdateDirectoryFromBundled(modpackDir, targetDir);
        }
    }

    /// <summary>
    /// Copies downloaded addon modpacks from the component cache into the staging
    /// directory so they appear in the mod list and can be deployed by the user.
    /// Called at startup and can be called after addon downloads complete.
    /// Only seeds addons that aren't already in staging - does NOT update existing
    /// staged addons to preserve user edits.
    /// </summary>
    public void SeedDownloadedAddons()
    {
        var addonsDir = Path.Combine(
            ComponentManager.Instance.ComponentsCachePath, "addons");

        if (!Directory.Exists(addonsDir))
            return;

        foreach (var addonDir in Directory.GetDirectories(addonsDir))
        {
            // Skip empty directories (addon downloaded but extraction failed)
            var manifestPath = Path.Combine(addonDir, "modpack.json");
            if (!File.Exists(manifestPath))
                continue;

            var dirName = Path.GetFileName(addonDir);
            var targetDir = Path.Combine(_stagingBasePath, dirName);

            // Only seed if not already in staging - don't overwrite user edits
            if (!Directory.Exists(targetDir))
            {
                CopyDirectory(addonDir, targetDir);
            }
        }
    }

    /// <summary>
    /// Copy files from bundled source to staging where the bundled file is newer.
    /// Does not delete files the user may have added to staging.
    /// When any source file is updated, deletes the build/ cache to force recompilation.
    /// </summary>
    private void UpdateDirectoryFromBundled(string sourceDir, string destDir)
    {
        bool anyUpdated = false;
        UpdateDirectoryFromBundledCore(sourceDir, destDir, ref anyUpdated);

        if (anyUpdated)
        {
            // Source files changed — delete stale build cache so next deploy recompiles
            var buildDir = Path.Combine(destDir, "build");
            if (Directory.Exists(buildDir))
            {
                try { Directory.Delete(buildDir, true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    ModkitLog.Warn($"[ModpackManager] Failed to delete build cache: {ex.Message}");
                }
            }
        }
    }

    private void UpdateDirectoryFromBundledCore(string sourceDir, string destDir, ref bool anyUpdated)
    {
        Directory.CreateDirectory(destDir);
        foreach (var sourceFile in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(sourceFile));
            if (!File.Exists(destFile) || !FilesAreEqual(sourceFile, destFile))
            {
                File.Copy(sourceFile, destFile, true);
                anyUpdated = true;
            }
        }
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            // Don't descend into build/ — that's output, not source
            if (Path.GetFileName(subDir).Equals("build", StringComparison.OrdinalIgnoreCase))
                continue;
            UpdateDirectoryFromBundledCore(subDir, Path.Combine(destDir, Path.GetFileName(subDir)), ref anyUpdated);
        }
    }

    private static bool FilesAreEqual(string path1, string path2)
    {
        var info1 = new FileInfo(path1);
        var info2 = new FileInfo(path2);
        if (info1.Length != info2.Length)
            return false;
        return File.ReadAllBytes(path1).AsSpan().SequenceEqual(File.ReadAllBytes(path2));
    }

    private string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }

    /// <summary>
    /// Validates a modpack name for security and compatibility.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the name is invalid.</exception>
    private static void ValidateModpackName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Modpack name is required", nameof(name));

        if (name.Length > MaxModpackNameLength)
            throw new ArgumentException($"Modpack name must be {MaxModpackNameLength} characters or less", nameof(name));

        if (!ValidModpackNameRegex.IsMatch(name))
            throw new ArgumentException("Modpack name contains invalid characters. Use only letters, numbers, spaces, underscores, hyphens, and periods.", nameof(name));

        // Check for reserved Windows names
        var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        var baseName = name.Split('.')[0].ToUpperInvariant();
        if (reservedNames.Contains(baseName))
            throw new ArgumentException($"'{name}' is a reserved system name and cannot be used", nameof(name));
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
