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

        var modpacks = _modpackManager.GetStagingModpacks()
            .OrderBy(m => m.LoadOrder)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
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

            // Step 5: Save deploy state
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
                    var dir = Path.Combine(modsBasePath, mp.Name);
                    if (Directory.Exists(dir))
                        Directory.Delete(dir, true);
                }

                // Also remove any tracked loose files
                foreach (var file in state.DeployedFiles)
                {
                    var fullPath = Path.Combine(modsBasePath, file);
                    if (File.Exists(fullPath))
                        File.Delete(fullPath);
                }
            }, ct);

            // Clear deploy state
            var emptyState = new DeployState();
            emptyState.SaveTo(DeployStateFilePath);

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
    /// Merge all modpack patches and attempt to compile them into an asset bundle.
    /// Returns list of deployed files (relative to modsBasePath). Falls back silently
    /// if compilation fails — the runtime JSON loader will handle the patches instead.
    /// </summary>
    private async Task<List<string>> TryCompileBundleAsync(
        List<ModpackManifest> modpacks, string modsBasePath, CancellationToken ct)
    {
        var files = new List<string>();

        // Collect ordered patch sets from all modpacks
        var orderedPatchSets = new List<Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>>();

        foreach (var modpack in modpacks)
        {
            // Patches from stats/*.json files
            var statsDir = Path.Combine(modpack.Path, "stats");
            if (Directory.Exists(statsDir))
            {
                var statsPatches = new Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>();
                foreach (var statsFile in Directory.GetFiles(statsDir, "*.json"))
                {
                    var templateType = System.IO.Path.GetFileNameWithoutExtension(statsFile);
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

        // Collect and deploy texture entries from all modpacks
        foreach (var modpack in modpacks)
        {
            var assetsDir = Path.Combine(modpack.Path, "assets");
            var textureEntries = TextureBundler.CollectTextureEntries(assetsDir);
            if (textureEntries.Count > 0)
            {
                var textureOutputDir = Path.Combine(modsBasePath, "compiled");
                var textureOutputPath = Path.Combine(textureOutputDir, "textures.json");
                var unityVer = DetectUnityVersion(_modpackManager.GetGameInstallPath() ?? "");
                if (TextureBundler.CreateTextureBundle(textureEntries, textureOutputPath, unityVer))
                {
                    foreach (var file in Directory.GetFiles(textureOutputDir, "*", SearchOption.AllDirectories))
                    {
                        var rel = Path.GetRelativePath(modsBasePath, file);
                        if (!files.Contains(rel))
                            files.Add(rel);
                    }
                }
            }
        }

        if (orderedPatchSets.Count == 0)
            return files;

        var merged = MergedPatchSet.MergePatchSets(orderedPatchSets);
        if (merged.Patches.Count == 0)
            return files;

        // Determine game data path and Unity version
        var gameInstallPath = _modpackManager.GetGameInstallPath();
        if (string.IsNullOrEmpty(gameInstallPath))
            return files;

        var unityVersion = DetectUnityVersion(gameInstallPath);
        var compiledDir = Path.Combine(modsBasePath, "compiled");
        var outputPath = Path.Combine(compiledDir, "templates.bundle");

        try
        {
            var compiler = new BundleCompiler();
            var result = await compiler.CompileDataPatchBundleAsync(
                merged, gameInstallPath, unityVersion, outputPath, ct);

            if (result.Success && result.OutputPath != null)
            {
                // Track all files in the compiled directory
                foreach (var file in Directory.GetFiles(compiledDir, "*", SearchOption.AllDirectories))
                {
                    files.Add(Path.GetRelativePath(modsBasePath, file));
                }
            }
            // If compilation failed, the JSON patches in each modpack's modpack.json
            // will be used by the runtime loader instead — no action needed.
        }
        catch
        {
            // Bundle compilation is best-effort; JSON fallback handles patches.
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
    /// Copy runtime DLLs (ModpackLoader, DataExtractor, etc.) from the runtime/
    /// directory to the game's Mods/ root. Compares file sizes to avoid unnecessary copies.
    /// Returns list of deployed files (relative to modsBasePath).
    /// </summary>
    private List<string> DeployRuntimeDlls(string modsBasePath)
    {
        var files = new List<string>();
        var runtimeDlls = _modpackManager.GetRuntimeDlls();

        if (runtimeDlls.Count == 0)
            return files;

        foreach (var (fileName, sourcePath) in runtimeDlls)
        {
            var destPath = Path.Combine(modsBasePath, fileName);
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
                    Console.WriteLine($"[DeployManager] Deployed runtime DLL: {fileName}");
                }
                else
                {
                    Console.WriteLine($"[DeployManager] Runtime DLL up to date: {fileName}");
                }

                files.Add(fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeployManager] Failed to deploy {fileName}: {ex.Message}");
            }
        }

        return files;
    }

    private void CleanPreviousDeployment(DeployState previousState, string modsBasePath)
    {
        foreach (var mp in previousState.DeployedModpacks)
        {
            var dir = Path.Combine(modsBasePath, mp.Name);
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
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

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }
    }
}

public class DeployResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int DeployedCount { get; set; }
}
