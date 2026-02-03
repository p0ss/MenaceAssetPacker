using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Manages modpack staging, vanilla data, and active mods.
/// Uses ModpackManifest (v2) internally; auto-migrates legacy v1 manifests on load.
/// </summary>
public class ModpackManager
{
    private readonly string _stagingBasePath;

    public ModpackManager()
    {
        _stagingBasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MenaceModkit", "staging");

        EnsureDirectoriesExist();
        SeedBundledRuntimeDlls();
        SeedBundledModpacks();
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
            catch { }
        }

        return result;
    }

    // ---------------------------------------------------------------
    // Asset operations
    // ---------------------------------------------------------------

    public void SaveStagingAsset(string modpackName, string relativePath, string sourceFile)
    {
        var assetDir = Path.Combine(ResolveStagingDir(modpackName), "assets");
        var destPath = Path.Combine(assetDir, relativePath);
        var destDir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);
        File.Copy(sourceFile, destPath, true);
    }

    public string? GetStagingAssetPath(string modpackName, string relativePath)
    {
        var path = Path.Combine(ResolveStagingDir(modpackName), "assets", relativePath);
        return File.Exists(path) ? path : null;
    }

    public void RegisterAssetInManifest(string modpackName, string relativePath)
    {
        var modpackDir = ResolveStagingDir(modpackName);
        var manifest = LoadManifest(modpackDir);
        if (manifest == null) return;

        manifest.Assets[relativePath] = Path.Combine("assets", relativePath);
        manifest.SaveToFile();
    }

    public void RemoveStagingAsset(string modpackName, string relativePath)
    {
        var path = Path.Combine(ResolveStagingDir(modpackName), "assets", relativePath);
        if (File.Exists(path))
            File.Delete(path);
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

        var archivePath = Path.Combine(exportPath, $"{modpackName}.zip");
        System.IO.Compression.ZipFile.CreateFromDirectory(stagingPath, archivePath);
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
        var infoPath = Path.Combine(modpackDir, "modpack.json");
        try
        {
            var manifest = ModpackManifest.LoadFromFile(infoPath);
            manifest.Path = modpackDir;
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
        foreach (var dirName in BundledRuntimeDllDirs)
        {
            var srcDir = Path.Combine(bundledBase, dirName);
            if (!Directory.Exists(srcDir)) continue;

            foreach (var srcFile in Directory.GetFiles(srcDir, "*.dll"))
            {
                var destFile = Path.Combine(RuntimeDllsPath, Path.GetFileName(srcFile));
                bool needsCopy = !File.Exists(destFile);
                if (!needsCopy)
                {
                    var srcInfo = new FileInfo(srcFile);
                    var destInfo = new FileInfo(destFile);
                    needsCopy = srcInfo.Length != destInfo.Length;
                }
                if (needsCopy)
                    File.Copy(srcFile, destFile, true);
            }
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
                try { Directory.Delete(buildDir, true); } catch { }
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
