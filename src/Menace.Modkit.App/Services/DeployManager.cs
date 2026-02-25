using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Menace.Modkit.App.Models;
using Menace.Modkit.Core.Bundles;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Owns the game's Mods/ folder. Handles full deploy/undeploy pipeline:
/// resolve active modpacks in load order → merge data patches (last-wins) →
/// copy assets → write merged runtime manifests → clean removed mods.
/// </summary>
public class DeployManager
{
    private readonly ModpackManager _modpackManager;
    private readonly CompilationService _compilationService = new();
    private string DeployStateFilePath =>
        Path.Combine(Path.GetDirectoryName(_modpackManager.StagingBasePath)!, "deploy-state.json");

    public DeployManager(ModpackManager modpackManager)
    {
        _modpackManager = modpackManager;
    }

    /// <summary>
    /// Deploy a single staging modpack to the game's Mods/ folder (with compilation).
    /// </summary>
    public async Task<DeployResult> DeploySingleAsync(ModpackManifest modpack, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var modsBasePath = _modpackManager.ModsBasePath;
        if (string.IsNullOrEmpty(modsBasePath))
            return new DeployResult { Success = false, Message = "Game install path not set" };

        try
        {
            // Deploy runtime DLLs first so ModpackLoader.dll is available as a reference
            progress?.Report("Deploying runtime DLLs...");
            await Task.Run(() => DeployRuntimeDlls(modsBasePath), ct);

            // Compile source code if present
            if (modpack.Code.HasAnySources)
            {
                progress?.Report($"Compiling {modpack.Name}...");
                ModkitLog.Info($"Compiling {modpack.Name}: sources={string.Join(", ", modpack.Code.Sources)}, refs={string.Join(", ", modpack.Code.References)}");
                var compileResult = await _compilationService.CompileModpackAsync(modpack, ct);
                foreach (var diag in compileResult.Diagnostics)
                    ModkitLog.Info($"  [{diag.Severity}] {diag.File}:{diag.Line} — {diag.Message}");
                if (!compileResult.Success)
                {
                    var errors = string.Join("\n", compileResult.Diagnostics
                        .Where(d => d.Severity == Models.DiagnosticSeverity.Error)
                        .Select(d => $"{d.File}:{d.Line} — {d.Message}"));
                    var msg = $"Compile failed for {modpack.Name}:\n{errors}";
                    ModkitLog.Error(msg);
                    return new DeployResult { Success = false, Message = msg };
                }
                ModkitLog.Info($"Compiled {modpack.Name} → {compileResult.OutputDllPath}");
            }

            progress?.Report($"Deploying {modpack.Name}...");
            await Task.Run(() => DeployModpack(modpack, modsBasePath), ct);

            // Restore original game data before compiling to ensure we read from vanilla files
            // This prevents the compiler from finding clones that were added in previous deployments
            var gameInstallPath = _modpackManager.GetGameInstallPath();
            if (!string.IsNullOrEmpty(gameInstallPath))
            {
                var gameDataDir = Directory.GetDirectories(gameInstallPath, "*_Data").FirstOrDefault();
                if (!string.IsNullOrEmpty(gameDataDir))
                {
                    foreach (var (backupName, originalName) in new[] {
                        ("resources.assets.original", "resources.assets"),
                        ("globalgamemanagers.original", "globalgamemanagers") })
                    {
                        var backupPath = Path.Combine(gameDataDir, backupName);
                        var originalPath = Path.Combine(gameDataDir, originalName);
                        if (File.Exists(backupPath))
                        {
                            // Restore from backup so compilation reads vanilla data
                            ModkitLog.Info($"[DeployManager] Restoring {originalName} from backup for clean compile");
                            File.Copy(backupPath, originalPath, overwrite: true);
                        }
                    }
                }
            }

            // Compile merged bundle with all staging modpacks (not just this one)
            // This ensures clones, patches, and audio work correctly
            progress?.Report("Compiling asset bundles...");
            var allModpacks = _modpackManager.GetStagingModpacks()
                .Where(m => !IsDevOnlyModpack(m.Name) || AppSettings.Instance.EnableDeveloperTools)
                .OrderBy(m => m.LoadOrder)
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            await TryCompileBundleAsync(allModpacks, modsBasePath, ct);

            // Deploy patched game data
            progress?.Report("Deploying patched game data...");
            await Task.Run(() => DeployPatchedGameData(modsBasePath), ct);

            ModkitLog.Info($"Deployed {modpack.Name} to {modsBasePath}");
            progress?.Report($"Deployed {modpack.Name}");
            return new DeployResult { Success = true, Message = $"Deployed {modpack.Name}", DeployedCount = 1 };
        }
        catch (OperationCanceledException)
        {
            return new DeployResult { Success = false, Message = "Deployment cancelled" };
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"Deploy single failed: {ex}");
            return new DeployResult { Success = false, Message = $"Deploy failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// Deploy all active staging modpacks to the game's Mods/ folder.
    /// </summary>
    public async Task<DeployResult> DeployAllAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var modsBasePath = _modpackManager.ModsBasePath;
        if (string.IsNullOrEmpty(modsBasePath))
            return new DeployResult { Success = false, Message = "Game install path not set" };

        // Get staging modpacks, ordered by load order, excluding dev-only unless enabled.
        // Use DistinctBy on Name to avoid deploying duplicate modpacks if multiple
        // staging directories have the same manifest Name.
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var modpacks = _modpackManager.GetStagingModpacks()
            .Where(m => !IsDevOnlyModpack(m.Name) || AppSettings.Instance.EnableDeveloperTools)
            .OrderBy(m => m.LoadOrder)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Where(m => seenNames.Add(m.Name)) // Keep first occurrence only
            .ToList();

        if (modpacks.Count == 0)
            return new DeployResult { Success = false, Message = "No staging modpacks found" };

        var previousState = DeployState.LoadFrom(DeployStateFilePath);
        var deployedFiles = new List<string>();
        var deployedModpacks = new List<DeployedModpack>();

        try
        {
            // Step 1: Clean previously deployed files that are no longer needed
            progress?.Report("Cleaning old deployment...");
            await Task.Run(() => CleanPreviousDeployment(previousState, modsBasePath), ct);

            // Step 2: Deploy runtime DLLs first (ModpackLoader, DataExtractor, etc.)
            // Must happen before compilation so modpacks can reference Menace.ModpackLoader.dll
            progress?.Report("Deploying runtime DLLs...");
            var runtimeFiles = await Task.Run(() => DeployRuntimeDlls(modsBasePath), ct);
            deployedFiles.AddRange(runtimeFiles);

            // Step 3: Compile and deploy each modpack
            int total = modpacks.Count;
            for (int i = 0; i < total; i++)
            {
                ct.ThrowIfCancellationRequested();
                var modpack = modpacks[i];

                // Compile source code if present
                if (modpack.Code.HasAnySources)
                {
                    progress?.Report($"Compiling {modpack.Name} ({i + 1}/{total})...");
                    ModkitLog.Info($"Compiling {modpack.Name}: sources={string.Join(", ", modpack.Code.Sources)}, refs={string.Join(", ", modpack.Code.References)}");
                    var compileResult = await _compilationService.CompileModpackAsync(modpack, ct);
                    foreach (var diag in compileResult.Diagnostics)
                        ModkitLog.Info($"  [{diag.Severity}] {diag.File}:{diag.Line} — {diag.Message}");
                    if (!compileResult.Success)
                    {
                        var errors = string.Join("\n", compileResult.Diagnostics
                            .Where(d => d.Severity == Models.DiagnosticSeverity.Error)
                            .Select(d => $"{d.File}:{d.Line} — {d.Message}"));
                        var msg = $"Compilation failed for {modpack.Name}:\n{errors}";
                        ModkitLog.Error(msg);
                        return new DeployResult { Success = false, Message = msg };
                    }
                    ModkitLog.Info($"Compiled {modpack.Name} → {compileResult.OutputDllPath}");
                }

                progress?.Report($"Deploying {modpack.Name} ({i + 1}/{total})...");

                var files = await Task.Run(() => DeployModpack(modpack, modsBasePath), ct);
                deployedFiles.AddRange(files);

                deployedModpacks.Add(new DeployedModpack
                {
                    Name = modpack.Name,
                    Version = modpack.Version,
                    LoadOrder = modpack.LoadOrder,
                    ContentHash = ComputeDirectoryHash(modpack.Path),
                    SecurityStatus = modpack.SecurityStatus
                });
            }

            // Step 4: Try to compile merged patches into an asset bundle
            progress?.Report("Compiling asset bundles...");
            var bundleFiles = await TryCompileBundleAsync(modpacks, modsBasePath, ct);
            deployedFiles.AddRange(bundleFiles);

            // Step 5: Deploy patched game data files (resources.assets, globalgamemanagers)
            progress?.Report("Deploying patched game data...");
            await Task.Run(() => DeployPatchedGameData(modsBasePath), ct);

            // Step 6: Save deploy state
            var state = new DeployState
            {
                DeployedModpacks = deployedModpacks,
                DeployedFiles = deployedFiles,
                LastDeployTimestamp = DateTime.Now
            };
            state.SaveTo(DeployStateFilePath);

            progress?.Report($"Deployed {total} modpack(s) successfully");
            return new DeployResult
            {
                Success = true,
                Message = $"Deployed {total} modpack(s)",
                DeployedCount = total
            };
        }
        catch (OperationCanceledException)
        {
            return new DeployResult { Success = false, Message = "Deployment cancelled" };
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"Deploy all failed: {ex}");
            return new DeployResult { Success = false, Message = $"Deploy failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// Remove all deployed mods from the game's Mods/ folder.
    /// Core infrastructure DLLs (ModpackLoader, DataExtractor) are preserved.
    /// </summary>
    public async Task<DeployResult> UndeployAllAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var modsBasePath = _modpackManager.ModsBasePath;
        if (string.IsNullOrEmpty(modsBasePath))
            return new DeployResult { Success = false, Message = "Game install path not set" };

        var state = DeployState.LoadFrom(DeployStateFilePath);

        try
        {
            progress?.Report("Removing deployed mods...");

            await Task.Run(() =>
            {
                // Remove deployed modpack directories
                foreach (var mp in state.DeployedModpacks)
                {
                    // Skip empty/invalid names to avoid deleting Mods folder itself
                    if (string.IsNullOrWhiteSpace(mp.Name))
                    {
                        ModkitLog.Warn($"[DeployManager] Skipping invalid modpack with empty name");
                        continue;
                    }

                    var dir = Path.Combine(modsBasePath, mp.Name);

                    // Safety: never delete the Mods folder itself
                    if (Path.GetFullPath(dir) == Path.GetFullPath(modsBasePath))
                    {
                        ModkitLog.Warn($"[DeployManager] Skipping deletion of Mods folder itself");
                        continue;
                    }

                    if (Directory.Exists(dir))
                    {
                        ModkitLog.Info($"[DeployManager] Removing modpack directory: {mp.Name}");
                        Directory.Delete(dir, true);
                    }
                }

                // Also remove any tracked loose files, but protect core DLLs
                foreach (var file in state.DeployedFiles)
                {
                    var fileName = Path.GetFileName(file);

                    // Never remove core infrastructure DLLs (Menace.*.dll)
                    if (fileName.StartsWith("Menace.", StringComparison.OrdinalIgnoreCase) &&
                        fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        ModkitLog.Info($"[DeployManager] Protected from removal: {file}");
                        continue;
                    }

                    var fullPath = Path.Combine(modsBasePath, file);
                    if (File.Exists(fullPath))
                    {
                        ModkitLog.Info($"[DeployManager] Removing: {file}");
                        File.Delete(fullPath);
                    }
                }

                // Clean up deployment artifacts that shouldn't persist
                var artifactDirs = new[] { "compiled", "dll", "dlls" };
                foreach (var artifactName in artifactDirs)
                {
                    var artifactDir = Path.Combine(modsBasePath, artifactName);
                    if (Directory.Exists(artifactDir))
                    {
                        ModkitLog.Info($"[DeployManager] Removing artifact directory: {artifactName}");
                        Directory.Delete(artifactDir, true);
                    }
                }

                // Log preserved core DLLs
                foreach (var dllPath in Directory.GetFiles(modsBasePath, "Menace.*.dll"))
                {
                    ModkitLog.Info($"[DeployManager] Core DLL preserved: {Path.GetFileName(dllPath)}");
                }
            }, ct);

            // Restore original game data files
            progress?.Report("Restoring original game data...");
            await Task.Run(() => RestoreOriginalGameData(modsBasePath), ct);

            // Clear deploy state
            var emptyState = new DeployState();
            emptyState.SaveTo(DeployStateFilePath);

            // Force garbage collection to release any cached file handles or data
            // This fixes an issue where undeploy+redeploy without app restart would fail
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            progress?.Report("All mods undeployed");
            return new DeployResult { Success = true, Message = "All mods undeployed" };
        }
        catch (Exception ex)
        {
            return new DeployResult { Success = false, Message = $"Undeploy failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// Get the current deploy state (what's deployed vs what's in staging).
    /// </summary>
    public DeployState GetDeployState()
    {
        return DeployState.LoadFrom(DeployStateFilePath);
    }

    /// <summary>
    /// Check if any staging modpack has changed since last deploy.
    /// </summary>
    public bool HasChangedSinceDeploy()
    {
        var state = GetDeployState();
        var staging = _modpackManager.GetStagingModpacks();

        // Different count
        if (state.DeployedModpacks.Count != staging.Count)
            return true;

        // Check each modpack for changes
        foreach (var deployed in state.DeployedModpacks)
        {
            var stagingMatch = staging.FirstOrDefault(s => s.Name == deployed.Name);
            if (stagingMatch == null)
                return true; // modpack removed from staging

            var currentHash = ComputeDirectoryHash(stagingMatch.Path);
            if (currentHash != deployed.ContentHash)
                return true; // content changed
        }

        // Check for new staging modpacks not yet deployed
        foreach (var s in staging)
        {
            if (!state.DeployedModpacks.Any(d => d.Name == s.Name))
                return true;
        }

        return false;
    }

    // ---------------------------------------------------------------
    // Internal
    // ---------------------------------------------------------------

    /// <summary>
    /// Merge all modpack patches and clones, then attempt to compile them into an asset bundle.
    /// Returns list of deployed files (relative to modsBasePath). Falls back silently
    /// if compilation fails — the runtime JSON loader will handle the patches instead.
    /// </summary>
    private async Task<List<string>> TryCompileBundleAsync(
        List<ModpackManifest> modpacks, string modsBasePath, CancellationToken ct)
    {
        var files = new List<string>();

        // Collect ordered patch sets from all modpacks
        var orderedPatchSets = new List<Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>>();

        // Collect clone definitions from all modpacks
        var mergedClones = new MergedCloneSet();

        foreach (var modpack in modpacks)
        {
            // Clones from clones/*.json files
            var clonesDir = Path.Combine(modpack.Path, "clones");
            if (Directory.Exists(clonesDir))
            {
                var modpackClones = new Dictionary<string, Dictionary<string, string>>();
                foreach (var cloneFile in Directory.GetFiles(clonesDir, "*.json"))
                {
                    var templateType = Path.GetFileNameWithoutExtension(cloneFile);
                    try
                    {
                        var json = File.ReadAllText(cloneFile);
                        var cloneMap = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                        if (cloneMap != null && cloneMap.Count > 0)
                            modpackClones[templateType] = cloneMap;
                    }
                    catch { }
                }
                if (modpackClones.Count > 0)
                    mergedClones.AddFromModpack(modpackClones);
            }

            // Patches from stats/*.json files
            var statsDir = Path.Combine(modpack.Path, "stats");
            if (Directory.Exists(statsDir))
            {
                var statsPatches = new Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>();
                foreach (var statsFile in Directory.GetFiles(statsDir, "*.json"))
                {
                    var templateType = Path.GetFileNameWithoutExtension(statsFile);
                    try
                    {
                        var json = File.ReadAllText(statsFile);
                        var instances = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(json);
                        if (instances != null)
                            statsPatches[templateType] = instances;
                    }
                    catch { }
                }
                if (statsPatches.Count > 0)
                    orderedPatchSets.Add(statsPatches);
            }

            // Patches from manifest
            if (modpack.Patches.Count > 0)
                orderedPatchSets.Add(modpack.Patches);
        }

        // Collect audio entries from all modpacks for native AudioClip creation
        var modpackAssetsDirs = modpacks
            .Select(m => Path.Combine(m.Path, "assets"))
            .Where(Directory.Exists)
            .ToList();
        var audioCollectResult = AudioBundler.CollectAudioEntriesFromModpacks(modpackAssetsDirs);
        var audioEntries = audioCollectResult.Entries;

        if (audioCollectResult.Warnings.Count > 0)
        {
            foreach (var warn in audioCollectResult.Warnings)
                ModkitLog.Warn($"[DeployManager] Audio: {warn}");
        }

        // Collect texture and model entries from all modpacks for native asset creation
        var allTextureEntries = new List<BundleCompiler.TextureEntry>();
        var allModelEntries = new List<BundleCompiler.ModelEntry>();
        foreach (var modpack in modpacks)
        {
            var assetsDir = Path.Combine(modpack.Path, "assets");
            var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".png", ".jpg", ".jpeg", ".tga", ".bmp" };

            // First try modpack.Assets dictionary for proper game path mappings
            if (modpack.Assets != null && modpack.Assets.Count > 0)
            {
                foreach (var (gameAssetPath, localPath) in modpack.Assets)
                {
                    var ext = Path.GetExtension(gameAssetPath);
                    if (!imageExtensions.Contains(ext))
                        continue;

                    var fullLocalPath = Path.Combine(modpack.Path, localPath);
                    if (!File.Exists(fullLocalPath))
                        continue;

                    var assetName = Path.GetFileNameWithoutExtension(gameAssetPath);

                    // Build resource path from game asset path (strip extension, lowercase)
                    // Unity ResourceManager uses paths RELATIVE to Resources folder
                    // e.g., "Assets/Resources/ui/textures/bg.png" -> "ui/textures/bg"
                    var resourcePath = gameAssetPath;
                    if (resourcePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                        resourcePath = resourcePath[..^ext.Length];
                    resourcePath = resourcePath.Replace('\\', '/');

                    // Strip "Assets/Resources/" prefix if present (Unity uses relative paths)
                    if (resourcePath.StartsWith("Assets/Resources/", StringComparison.OrdinalIgnoreCase))
                        resourcePath = resourcePath.Substring("Assets/Resources/".Length);
                    else if (resourcePath.StartsWith("assets/resources/", StringComparison.OrdinalIgnoreCase))
                        resourcePath = resourcePath.Substring("assets/resources/".Length);

                    resourcePath = resourcePath.ToLowerInvariant();

                    allTextureEntries.Add(new BundleCompiler.TextureEntry
                    {
                        AssetName = assetName,
                        SourceFilePath = fullLocalPath,
                        ResourcePath = resourcePath,
                        CreateSprite = true // Create sprites by default for UI textures
                    });
                }
            }
            // Fallback: scan assets folder directly if Assets dictionary is empty
            else if (Directory.Exists(assetsDir))
            {
                var imageFiles = imageExtensions
                    .SelectMany(ext => Directory.GetFiles(assetsDir, $"*{ext}", SearchOption.AllDirectories))
                    .ToList();

                foreach (var imagePath in imageFiles)
                {
                    var assetName = Path.GetFileNameWithoutExtension(imagePath);
                    var relativePath = Path.GetRelativePath(assetsDir, imagePath).Replace('\\', '/');

                    // Build a resource path based on the modpack name and relative path
                    var resourcePath = $"assets/textures/{modpack.Name}/{Path.GetFileNameWithoutExtension(relativePath)}".ToLowerInvariant();

                    allTextureEntries.Add(new BundleCompiler.TextureEntry
                    {
                        AssetName = assetName,
                        SourceFilePath = imagePath,
                        ResourcePath = resourcePath,
                        CreateSprite = true
                    });
                }
            }

            // Collect GLB/GLTF model entries for native asset creation
            if (Directory.Exists(assetsDir))
            {
                var glbFiles = Directory.GetFiles(assetsDir, "*.glb", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(assetsDir, "*.gltf", SearchOption.AllDirectories));

                foreach (var glbPath in glbFiles)
                {
                    var assetName = Path.GetFileNameWithoutExtension(glbPath);

                    // Build resource path based on modpack assets mapping or default
                    var relativePath = Path.GetRelativePath(assetsDir, glbPath).Replace('\\', '/');
                    var resourcePath = $"assets/models/{modpack.Name}/{assetName}".ToLowerInvariant();

                    allModelEntries.Add(new BundleCompiler.ModelEntry
                    {
                        AssetName = assetName,
                        SourceFilePath = glbPath,
                        ResourcePath = resourcePath
                    });
                }
            }
        }

        if (allTextureEntries.Count > 0)
        {
            ModkitLog.Info($"[DeployManager] Collected {allTextureEntries.Count} texture(s) for native asset creation");
        }
        if (allModelEntries.Count > 0)
        {
            ModkitLog.Info($"[DeployManager] Collected {allModelEntries.Count} model(s) for native asset creation");
        }

        var merged = MergedPatchSet.MergePatchSets(orderedPatchSets);

        // Only skip compilation if there are no patches, no clones, no audio, AND no models
        // NOTE: Textures are now handled at runtime to avoid ColorSpace issues
        bool hasAudio = audioEntries.Count > 0;
        bool hasModels = allModelEntries.Count > 0;
        if (merged.Patches.Count == 0 && !mergedClones.HasClones && !hasAudio && !hasModels)
            return files;

        // Determine game data path and Unity version
        var gameInstallPath = _modpackManager.GetGameInstallPath();
        if (string.IsNullOrEmpty(gameInstallPath))
            return files;

        var unityVersion = DetectUnityVersion(gameInstallPath);
        var compiledDir = Path.Combine(modsBasePath, "compiled");
        var outputPath = Path.Combine(compiledDir, "templates.bundle");

        // Copy texture files to compiled/textures for runtime loading
        // (asset-file texture creation had ColorSpace issues, runtime loading works correctly)
        var texturesDir = Path.Combine(compiledDir, "textures");
        if (allTextureEntries.Count > 0)
        {
            Directory.CreateDirectory(texturesDir);
            ModkitLog.Info($"[DeployManager] Copying {allTextureEntries.Count} texture(s) for runtime loading...");
            foreach (var tex in allTextureEntries)
            {
                try
                {
                    var destPath = Path.Combine(texturesDir, $"{tex.AssetName}.png");
                    File.Copy(tex.SourceFilePath, destPath, overwrite: true);
                }
                catch (Exception ex)
                {
                    ModkitLog.Warn($"[DeployManager] Failed to copy texture '{tex.AssetName}': {ex.Message}");
                }
            }
        }

        try
        {
            // NOTE: Textures are handled at runtime via copied PNG files
            // Asset-file texture creation always resulted in washed-out colors
            ModkitLog.Info($"[DeployManager] Compiling bundle: {mergedClones.TotalCloneCount} clone(s), {merged.Patches.Count} patch type(s), {audioEntries.Count} audio file(s), {allModelEntries.Count} model(s), {allTextureEntries.Count} texture(s) (runtime)");
            var compiler = new BundleCompiler();
            var result = await compiler.CompileDataPatchBundleAsync(
                merged,
                mergedClones,
                audioEntries.Count > 0 ? audioEntries : null,
                null, // Textures handled at runtime - asset creation had ColorSpace issues
                allModelEntries.Count > 0 ? allModelEntries : null,
                gameInstallPath,
                unityVersion,
                outputPath,
                ct);

            if (result.Success && result.OutputPath != null)
            {
                ModkitLog.Info($"[DeployManager] Bundle compiled: {result.Message}");
                // Log any warnings even on success
                foreach (var warn in result.Warnings)
                    ModkitLog.Info($"[DeployManager]   - {warn}");
                // Track all files in the compiled directory
                foreach (var file in Directory.GetFiles(compiledDir, "*", SearchOption.AllDirectories))
                {
                    files.Add(Path.GetRelativePath(modsBasePath, file));
                }
            }
            else
            {
                ModkitLog.Warn($"[DeployManager] Bundle compilation failed: {result.Message}");
                foreach (var warn in result.Warnings)
                    ModkitLog.Warn($"[DeployManager]   - {warn}");
            }
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[DeployManager] Bundle compilation exception: {ex.Message}");
        }

        return files;
    }

    /// <summary>
    /// Try to detect the Unity version from the game install.
    /// Returns a fallback string if detection fails.
    /// </summary>
    private static string DetectUnityVersion(string gameInstallPath)
    {
        // Look for globalgamemanagers or data.unity3d to read version from
        var dataDirs = Directory.GetDirectories(gameInstallPath, "*_Data");
        foreach (var dataDir in dataDirs)
        {
            var ggm = Path.Combine(dataDir, "globalgamemanagers");
            if (File.Exists(ggm))
            {
                try
                {
                    // The Unity version is stored near the start of globalgamemanagers
                    using var fs = File.OpenRead(ggm);
                    using var reader = new BinaryReader(fs);
                    // Skip header bytes and read version string
                    // This is a simplified approach — the full detector in Core is more robust
                    fs.Seek(0x14, SeekOrigin.Begin);
                    var bytes = new List<byte>();
                    byte b;
                    while ((b = reader.ReadByte()) != 0 && bytes.Count < 30)
                        bytes.Add(b);
                    var version = Encoding.ASCII.GetString(bytes.ToArray());
                    if (version.Contains('.') && version.Length > 3)
                        return version;
                }
                catch { }
            }
        }

        return "2020.3.0f1"; // Fallback
    }

    private List<string> DeployModpack(ModpackManifest modpack, string modsBasePath)
    {
        // Guard against empty modpack names which would deploy to root Mods folder
        if (string.IsNullOrWhiteSpace(modpack.Name))
        {
            ModkitLog.Error($"[DeployManager] Cannot deploy modpack with empty name (path: {modpack.Path})");
            return new List<string>();
        }

        var deployDir = Path.Combine(modsBasePath, modpack.Name);
        var files = new List<string>();

        // Copy all modpack files to deploy directory
        CopyDirectory(modpack.Path, deployDir);

        // Deploy compiled DLLs from build/ directory
        DeployDlls(modpack, deployDir);

        // Track deployed files (relative to modsBasePath)
        foreach (var file in Directory.GetFiles(deployDir, "*", SearchOption.AllDirectories))
        {
            files.Add(Path.GetRelativePath(modsBasePath, file));
        }

        // Build runtime manifest
        BuildRuntimeManifest(modpack, deployDir);

        return files;
    }

    /// <summary>
    /// Copy compiled DLLs and prebuilt DLLs to the deploy directory.
    /// </summary>
    private void DeployDlls(ModpackManifest modpack, string deployDir)
    {
        var dllDir = Path.Combine(deployDir, "dlls");

        // Compiled DLLs from build/
        var buildDir = Path.Combine(modpack.Path, "build");
        if (Directory.Exists(buildDir))
        {
            foreach (var dll in Directory.GetFiles(buildDir, "*.dll"))
            {
                Directory.CreateDirectory(dllDir);
                File.Copy(dll, Path.Combine(dllDir, Path.GetFileName(dll)), true);
            }
        }

        // Prebuilt DLLs
        foreach (var prebuiltRelPath in modpack.Code.PrebuiltDlls)
        {
            var fullPath = Path.Combine(modpack.Path, prebuiltRelPath);
            if (File.Exists(fullPath))
            {
                Directory.CreateDirectory(dllDir);
                File.Copy(fullPath, Path.Combine(dllDir, Path.GetFileName(fullPath)), true);
            }
        }
    }

    private void BuildRuntimeManifest(ModpackManifest modpack, string deployPath)
    {
        var runtimeObj = new JsonObject
        {
            ["manifestVersion"] = 2,
            ["name"] = modpack.Name,
            ["version"] = modpack.Version,
            ["author"] = modpack.Author,
            ["loadOrder"] = modpack.LoadOrder
        };

        // Merge patches from manifest and stats/*.json files
        var patches = new JsonObject();
        var legacyTemplates = new JsonObject();

        // Stats files first
        var statsDir = Path.Combine(modpack.Path, "stats");
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

        // Manifest patches (stats files take priority)
        if (modpack.Patches.Count > 0)
        {
            var patchJson = JsonSerializer.Serialize(modpack.Patches);
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

        runtimeObj["patches"] = patches;
        runtimeObj["templates"] = legacyTemplates; // v1 backward compat

        // Clones from clones/*.json files
        var clones = new JsonObject();
        var clonesDir = Path.Combine(deployPath, "clones");
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
        if (clones.Count > 0)
            runtimeObj["clones"] = clones;

        // Assets: start from manifest entries, then scan for unregistered files
        var assetsObj = new JsonObject();
        if (modpack.Assets.Count > 0)
        {
            foreach (var kvp in modpack.Assets)
                assetsObj[kvp.Key] = kvp.Value;
        }

        // Fallback scan: pick up any files in assets/ not already in the manifest
        var assetsDir = Path.Combine(modpack.Path, "assets");
        if (Directory.Exists(assetsDir))
        {
            foreach (var file in Directory.GetFiles(assetsDir, "*", SearchOption.AllDirectories))
            {
                var relPath = Path.GetRelativePath(assetsDir, file);
                if (!assetsObj.ContainsKey(relPath))
                    assetsObj[relPath] = Path.Combine("assets", relPath);
            }
        }

        runtimeObj["assets"] = assetsObj;

        // Code
        if (modpack.Code.HasAnyCode)
        {
            var codeObj = new JsonObject
            {
                ["sources"] = new JsonArray(modpack.Code.Sources.Select(s => (JsonNode)JsonValue.Create(s)!).ToArray()),
                ["references"] = new JsonArray(modpack.Code.References.Select(r => (JsonNode)JsonValue.Create(r)!).ToArray()),
                ["prebuiltDlls"] = new JsonArray(modpack.Code.PrebuiltDlls.Select(d => (JsonNode)JsonValue.Create(d)!).ToArray())
            };
            runtimeObj["code"] = codeObj;
        }

        // Bundles
        if (modpack.Bundles.Count > 0)
        {
            runtimeObj["bundles"] = new JsonArray(modpack.Bundles.Select(b => (JsonNode)JsonValue.Create(b)!).ToArray());
        }

        runtimeObj["securityStatus"] = modpack.SecurityStatus.ToString();

        var manifestPath = Path.Combine(deployPath, "modpack.json");
        File.WriteAllText(manifestPath, runtimeObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Copy runtime DLLs from runtime/ into the game install.
    /// Menace.* mod DLLs go to Mods/, support libraries go to UserLibs/.
    /// Note: Core DLLs are NOT tracked in deploy state - they are infrastructure
    /// that should persist across undeploy/deploy cycles.
    /// </summary>
    private List<string> DeployRuntimeDlls(string modsBasePath)
    {
        // We intentionally return an empty list - core DLLs should not be tracked
        // in deploy state since they're infrastructure, not user content.
        // UndeployAll should not remove them.
        var runtimeDlls = _modpackManager.GetRuntimeDlls();

        ModkitLog.Info($"[DeployManager] DeployRuntimeDlls: Found {runtimeDlls.Count} runtime DLLs to deploy");

        if (runtimeDlls.Count == 0)
        {
            ModkitLog.Warn($"[DeployManager] DeployRuntimeDlls: No runtime DLLs found in {_modpackManager.RuntimeDllsPath}");
            return new List<string>();
        }

        var gameInstallPath = Path.GetDirectoryName(modsBasePath) ?? modsBasePath;
        var userLibsPath = Path.Combine(gameInstallPath, "UserLibs");
        Directory.CreateDirectory(userLibsPath);

        foreach (var (fileName, sourcePath) in runtimeDlls)
        {
            var isModDll = fileName.StartsWith("Menace.", StringComparison.OrdinalIgnoreCase);
            var destPath = Path.Combine(isModDll ? modsBasePath : userLibsPath, fileName);
            try
            {
                // Copy if destination doesn't exist or source is different size/newer
                bool needsCopy = !File.Exists(destPath);
                if (!needsCopy)
                {
                    var srcInfo = new FileInfo(sourcePath);
                    var destInfo = new FileInfo(destPath);
                    needsCopy = srcInfo.Length != destInfo.Length ||
                                srcInfo.LastWriteTimeUtc > destInfo.LastWriteTimeUtc;
                }

                if (needsCopy)
                {
                    File.Copy(sourcePath, destPath, true);
                    ModkitLog.Info($"[DeployManager] Deployed runtime DLL: {fileName} -> {(isModDll ? "Mods" : "UserLibs")}");
                }

                // Remove legacy support-library copies from Mods/ to avoid duplicate load contexts.
                if (!isModDll)
                {
                    var legacyModsPath = Path.Combine(modsBasePath, fileName);
                    if (File.Exists(legacyModsPath))
                    {
                        File.Delete(legacyModsPath);
                        ModkitLog.Info($"[DeployManager] Removed legacy dependency from Mods: {fileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                ModkitLog.Warn($"[DeployManager] Failed to deploy {fileName}: {ex.Message}");
            }
        }

        // Return empty list - core DLLs are not tracked for undeploy
        return new List<string>();
    }

    private void CleanPreviousDeployment(DeployState previousState, string modsBasePath)
    {
        foreach (var mp in previousState.DeployedModpacks)
        {
            // Skip empty/invalid names to avoid deleting Mods folder itself
            if (string.IsNullOrWhiteSpace(mp.Name))
            {
                ModkitLog.Warn($"[DeployManager] CleanPreviousDeployment: Skipping invalid modpack with empty name");
                continue;
            }

            var dir = Path.Combine(modsBasePath, mp.Name);

            // Safety: never delete the Mods folder itself
            if (Path.GetFullPath(dir) == Path.GetFullPath(modsBasePath))
            {
                ModkitLog.Warn($"[DeployManager] CleanPreviousDeployment: Skipping deletion of Mods folder itself");
                continue;
            }

            if (Directory.Exists(dir))
            {
                ModkitLog.Info($"[DeployManager] CleanPreviousDeployment: Removing {mp.Name}");
                Directory.Delete(dir, true);
            }
        }
    }

    private static string ComputeDirectoryHash(string directory)
    {
        if (!Directory.Exists(directory))
            return string.Empty;

        using var sha = SHA256.Create();
        var sb = new StringBuilder();

        foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories).OrderBy(f => f))
        {
            var relativePath = Path.GetRelativePath(directory, file);
            sb.Append(relativePath);
            sb.Append(new FileInfo(file).LastWriteTimeUtc.Ticks);
            sb.Append(new FileInfo(file).Length);
        }

        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Directories to exclude when copying modpacks to Mods/ folder.
    /// These are development artifacts that shouldn't be deployed.
    /// </summary>
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "src",      // Source code (compiled to build/)
        "build",    // Build output (DLLs deployed separately via DeployDlls)
        "obj",      // MSBuild intermediate files
        "bin",      // MSBuild output files
        "dll",      // Legacy DLL folder (use dlls/ instead)
        ".git",     // Git repository data
        ".vs",      // Visual Studio data
    };

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            // Skip excluded directories
            if (ExcludedDirectories.Contains(dirName))
                continue;
            CopyDirectory(dir, Path.Combine(destDir, dirName));
        }
    }

    /// <summary>
    /// Check if a modpack is a developer-only modpack that should be excluded
    /// from deployment unless EnableDeveloperTools is enabled.
    /// </summary>
    private static bool IsDevOnlyModpack(string modpackName)
    {
        // Modpacks starting with "Test" are developer tools
        if (modpackName.StartsWith("Test", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Deploy patched game data files (resources.assets, globalgamemanagers) to the game's data directory.
    /// Creates backups of originals if they don't exist.
    /// </summary>
    private void DeployPatchedGameData(string modsBasePath)
    {
        var gameInstallPath = _modpackManager.GetGameInstallPath();
        if (string.IsNullOrEmpty(gameInstallPath))
            return;

        var gameDataDir = Directory.GetDirectories(gameInstallPath, "*_Data").FirstOrDefault();
        if (string.IsNullOrEmpty(gameDataDir))
            return;

        var compiledDir = Path.Combine(modsBasePath, "compiled");
        if (!Directory.Exists(compiledDir))
            return;

        // Files to patch: resources.assets and globalgamemanagers
        var filesToPatch = new[]
        {
            ("resources.assets.patched", "resources.assets"),
            ("globalgamemanagers.patched", "globalgamemanagers")
        };

        foreach (var (patchedName, originalName) in filesToPatch)
        {
            var patchedPath = Path.Combine(compiledDir, patchedName);
            if (!File.Exists(patchedPath))
                continue;

            var originalPath = Path.Combine(gameDataDir, originalName);
            var backupPath = Path.Combine(gameDataDir, originalName + ".original");

            // Only create backup if one doesn't exist yet (preserve vanilla backup)
            // CRITICAL: Don't overwrite existing .original - it may be the only vanilla copy!
            // Previous bug: always creating "fresh backup" would copy a patched file to .original
            if (File.Exists(originalPath) && !File.Exists(backupPath))
            {
                ModkitLog.Info($"[DeployManager] Creating first-time backup: {originalName} -> {originalName}.original");
                File.Copy(originalPath, backupPath);
            }

            // Copy patched file over original
            ModkitLog.Info($"[DeployManager] Deploying patched: {patchedName} -> {originalName}");
            File.Copy(patchedPath, originalPath, overwrite: true);
        }
    }

    /// <summary>
    /// Restore original game data files from backups.
    /// </summary>
    private void RestoreOriginalGameData(string modsBasePath)
    {
        ModkitLog.Info("[DeployManager] RestoreOriginalGameData starting...");

        var gameInstallPath = _modpackManager.GetGameInstallPath();
        if (string.IsNullOrEmpty(gameInstallPath))
        {
            ModkitLog.Warn("[DeployManager] RestoreOriginalGameData: gameInstallPath is empty, cannot restore");
            return;
        }

        ModkitLog.Info($"[DeployManager] RestoreOriginalGameData: gameInstallPath = {gameInstallPath}");

        var gameDataDir = Directory.GetDirectories(gameInstallPath, "*_Data").FirstOrDefault();
        if (string.IsNullOrEmpty(gameDataDir))
        {
            ModkitLog.Warn($"[DeployManager] RestoreOriginalGameData: No *_Data directory found in {gameInstallPath}");
            return;
        }

        ModkitLog.Info($"[DeployManager] RestoreOriginalGameData: gameDataDir = {gameDataDir}");

        // Files to restore
        var filesToRestore = new[] { "resources.assets", "globalgamemanagers" };

        // Expected minimum sizes for validation (vanilla game files)
        var expectedMinSizes = new Dictionary<string, long>
        {
            { "resources.assets", 500 * 1024 * 1024 }, // ~518MB for vanilla
            { "globalgamemanagers", 5 * 1024 * 1024 }  // ~6MB for vanilla
        };

        foreach (var originalName in filesToRestore)
        {
            var originalPath = Path.Combine(gameDataDir, originalName);
            var backupPath = Path.Combine(gameDataDir, originalName + ".original");

            if (File.Exists(backupPath))
            {
                try
                {
                    var backupSize = new FileInfo(backupPath).Length;

                    // Validate backup isn't corrupted (too small)
                    if (expectedMinSizes.TryGetValue(originalName, out var minSize) && backupSize < minSize)
                    {
                        ModkitLog.Error($"[DeployManager] Backup {originalName}.original appears corrupted: {backupSize / 1024 / 1024}MB (expected >{minSize / 1024 / 1024}MB). Use Steam to verify game files, then use Clean Redeploy.");
                        continue;
                    }

                    ModkitLog.Info($"[DeployManager] Restoring original: {originalName}.original ({backupSize / 1024 / 1024}MB) -> {originalName}");
                    File.Copy(backupPath, originalPath, overwrite: true);
                    var restoredSize = new FileInfo(originalPath).Length;
                    ModkitLog.Info($"[DeployManager] Restored {originalName}: {restoredSize / 1024 / 1024}MB");
                }
                catch (Exception ex)
                {
                    ModkitLog.Error($"[DeployManager] Failed to restore {originalName}: {ex.Message}");
                }
            }
            else
            {
                ModkitLog.Warn($"[DeployManager] No backup found for {originalName} at {backupPath}");
            }
        }

        ModkitLog.Info("[DeployManager] RestoreOriginalGameData complete");
    }
}

public class DeployResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int DeployedCount { get; set; }
}
