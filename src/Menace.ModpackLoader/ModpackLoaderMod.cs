using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime;
using MelonLoader;
using Menace.SDK;
using Menace.SDK.Internal;
using Menace.SDK.Repl;
using Menace.ModpackLoader.Mcp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

[assembly: MelonInfo(typeof(Menace.ModpackLoader.ModpackLoaderMod), "Menace Modpack Loader", Menace.ModkitVersion.MelonVersion, "Menace Modkit")]
[assembly: MelonGame(null, null)]
[assembly: MelonOptionalDependencies(
    "Microsoft.CodeAnalysis",
    "Microsoft.CodeAnalysis.CSharp",
    "System.Collections.Immutable",
    "System.Reflection.Metadata",
    "System.Text.Encoding.CodePages",
    "Newtonsoft.Json",
    "SharpGLTF.Core")]

namespace Menace.ModpackLoader;

public partial class ModpackLoaderMod : MelonMod
{
    private readonly Dictionary<string, Modpack> _loadedModpacks = new();
    private readonly HashSet<string> _registeredAssetPaths = new();
    private bool _templatesLoaded = false;

    // Tracks which modpack+templateType combos have been successfully patched,
    // so we don't double-apply when retrying on later scenes.
    private readonly HashSet<string> _appliedPatchKeys = new();

    public override void OnInitializeMelon()
    {
        SdkLogger.Msg($"{ModkitVersion.LoaderFull} initialized");

        // Initialize SDK subsystems
        OffsetCache.Initialize();
        DevConsole.Initialize();
        DevConsole.ApplyInputPatches(HarmonyInstance);
        SdkLogger.Initialize(LoggerInstance); // Set up dual logging to file and DevConsole
        ModSettings.Initialize();
        InitializeRepl();

        // Register SDK console commands
        RegisterSdkCommands();

        // Initialize MCP HTTP server for external tooling integration
        // Controlled via ModSettings - starts automatically if enabled
        GameMcpServer.Initialize(LoggerInstance);

        LoadModpacks();
        DllLoader.InitializeAllPlugins();

        // Patch bug reporter to include mod list in all reports
        BugReporterPatches.Initialize(LoggerInstance, HarmonyInstance);

        // Emit startup banner to Player.log for game dev triage
        PlayerLog("========================================");
        PlayerLog("THIS GAME SESSION IS RUNNING MODDED");
        PlayerLog(ModkitVersion.LoaderFull);
        PlayerLog($"Loaded {_loadedModpacks.Count} modpack(s):");
        foreach (var mp in _loadedModpacks.Values.OrderBy(m => m.LoadOrder))
            PlayerLog($"  - {mp.Name} v{mp.Version} by {mp.Author ?? "Unknown"} (order: {mp.LoadOrder})");
        PlayerLog($"Bundles: {BundleLoader.LoadedBundleCount} ({BundleLoader.LoadedAssetCount} assets)");
        PlayerLog($"Mod DLLs: {DllLoader.GetLoadedAssemblies().Count}");
        var pluginSummary = DllLoader.GetPluginSummary();
        if (pluginSummary != null)
            PlayerLog($"Modpack plugins: {pluginSummary}");
        PlayerLog($"Asset replacements registered: {AssetReplacer.RegisteredCount}");
        PlayerLog("========================================");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        // Save any pending settings changes before scene transition
        ModSettings.Save();

        DevConsole.IsVisible = false;
        GameState.NotifySceneLoaded(sceneName);
        GameQuery.ClearCache();
        DllLoader.NotifySceneLoaded(buildIndex, sceneName);

        // Retry template patches on every scene until all types are found.
        // Some builds (e.g. EA) load templates in later scenes, not the title screen.
        if (!_templatesLoaded)
        {
            SdkLogger.Msg($"Scene '{sceneName}' loaded, attempting template patches...");
            MelonCoroutines.Start(WaitForTemplatesAndApply(sceneName));
        }

        // Apply asset replacements after every scene load (assets get reloaded per scene)
        if (AssetReplacer.RegisteredCount > 0 || BundleLoader.LoadedAssetCount > 0)
        {
            MelonCoroutines.Start(ApplyAssetReplacementsDelayed(sceneName));
        }
    }

    public override void OnUpdate()
    {
        GameState.ProcessUpdate();
        DevConsole.Update();
        DllLoader.NotifyUpdate();
    }

    public override void OnGUI()
    {
        DevConsole.Draw();
        ErrorNotification.Draw();
        DllLoader.NotifyOnGUI();
    }

    public override void OnApplicationQuit()
    {
        // Ensure settings are saved before the game closes
        ModSettings.Save();

        // Stop MCP HTTP server
        GameMcpServer.Stop();

        // Cleanup save watcher
        SaveSystemPatches.Shutdown();
    }

    private System.Collections.IEnumerator WaitForTemplatesAndApply(string sceneName)
    {
        // Wait a few frames for the game to initialize templates
        for (int i = 0; i < 30; i++)
        {
            yield return null;
        }

        SdkLogger.Msg($"Applying modpack modifications (scene: {sceneName})...");

        // Initialize save system watcher (tries to find saves folder)
        SaveSystemPatches.TryInitialize();

        var allApplied = ApplyAllModpacks();

        if (allApplied)
        {
            SdkLogger.Msg("All template patches applied successfully.");
            _templatesLoaded = true;
            PlayerLog("All template patches applied successfully");
        }
        else
        {
            SdkLogger.Warning("Some template types not yet loaded — will retry on next scene.");
        }
    }

    private static void RegisterSdkCommands()
    {
        // Register console commands from SDK wrapper classes
        Inventory.RegisterConsoleCommands();
        Operation.RegisterConsoleCommands();
        ArmyGeneration.RegisterConsoleCommands();
        Vehicle.RegisterConsoleCommands();
        Conversation.RegisterConsoleCommands();
        Emotions.RegisterConsoleCommands();
        BlackMarket.RegisterConsoleCommands();
        Mission.RegisterConsoleCommands();
        Roster.RegisterConsoleCommands();
        Perks.RegisterConsoleCommands();
        TileMap.RegisterConsoleCommands();
        Pathfinding.RegisterConsoleCommands();
        LineOfSight.RegisterConsoleCommands();
        TileEffects.RegisterConsoleCommands();
    }

    private void LoadModpacks()
    {
        var modsPath = Path.Combine(Directory.GetCurrentDirectory(), "Mods");
        if (!Directory.Exists(modsPath))
        {
            SdkLogger.Warning($"Mods directory not found: {modsPath}");
            return;
        }

        SdkLogger.Msg($"Loading modpacks from: {modsPath}");

        var modpackFiles = Directory.GetFiles(modsPath, "modpack.json", SearchOption.AllDirectories);

        // Sort by load order (parse manifestVersion to determine format)
        var modpackEntries = new List<(string file, int order, int version)>();

        foreach (var modpackFile in modpackFiles)
        {
            try
            {
                var json = File.ReadAllText(modpackFile);
                var obj = JObject.Parse(json);
                var manifestVersion = obj.Value<int?>("manifestVersion") ?? 1;
                var loadOrder = obj.Value<int?>("loadOrder") ?? 100;
                modpackEntries.Add((modpackFile, loadOrder, manifestVersion));
            }
            catch
            {
                modpackEntries.Add((modpackFile, 100, 1));
            }
        }

        // Load in order
        foreach (var (modpackFile, _, manifestVersion) in modpackEntries.OrderBy(e => e.order))
        {
            try
            {
                var modpackDir = Path.GetDirectoryName(modpackFile);
                var json = File.ReadAllText(modpackFile);
                var modpack = JsonConvert.DeserializeObject<Modpack>(json);

                if (modpack != null)
                {
                    modpack.DirectoryPath = modpackDir;
                    modpack.ManifestVersion = manifestVersion;

                    // Load clones from clones/*.json files if not in manifest
                    if ((modpack.Clones == null || modpack.Clones.Count == 0) && !string.IsNullOrEmpty(modpackDir))
                    {
                        var clonesDir = Path.Combine(modpackDir, "clones");
                        if (Directory.Exists(clonesDir))
                        {
                            modpack.Clones = new Dictionary<string, Dictionary<string, string>>();
                            foreach (var file in Directory.GetFiles(clonesDir, "*.json"))
                            {
                                try
                                {
                                    var templateType = Path.GetFileNameWithoutExtension(file);
                                    var cloneJson = File.ReadAllText(file);
                                    var cloneMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(cloneJson);
                                    if (cloneMap != null && cloneMap.Count > 0)
                                        modpack.Clones[templateType] = cloneMap;
                                }
                                catch { }
                            }
                        }
                    }

                    _loadedModpacks[modpack.Name] = modpack;

                    // Register with ModRegistry for save system tracking
                    ModRegistry.RegisterModpack(modpack.Name, modpack.Version, modpack.Author);

                    var vLabel = manifestVersion >= 2 ? "v2" : "v1 (legacy)";
                    SdkLogger.Msg($"  Loaded [{vLabel}]: {modpack.Name} v{modpack.Version} (order: {modpack.LoadOrder})");

                    // V2: Load bundles, GLB models, and DLLs
                    if (manifestVersion >= 2 && !string.IsNullOrEmpty(modpackDir))
                    {
                        BundleLoader.LoadBundles(modpackDir, modpack.Name);
                        GlbLoader.LoadModpackModels(modpackDir);
                        DllLoader.LoadModDlls(modpackDir, modpack.Name, modpack.SecurityStatus ?? "Unreviewed");
                    }

                    // Load asset replacements (both V1 and V2)
                    if (modpack.Assets != null)
                    {
                        LoadModpackAssets(modpack);
                    }
                }
            }
            catch (Exception ex)
            {
                SdkLogger.Error($"Failed to load modpack from {modpackFile}: {ex.Message}");
            }
        }

        SdkLogger.Msg($"Loaded {_loadedModpacks.Count} modpack(s)");
    }

    private void LoadModpackAssets(Modpack modpack)
    {
        if (modpack.Assets == null || string.IsNullOrEmpty(modpack.DirectoryPath))
            return;

        foreach (var (assetPath, replacementFile) in modpack.Assets)
        {
            try
            {
                // Validate path stays within modpack directory to prevent traversal attacks
                var fullPath = ValidatePathWithinModpack(modpack.DirectoryPath, replacementFile);
                if (fullPath == null)
                {
                    SdkLogger.Warning($"  Path traversal blocked for asset: {replacementFile}");
                    continue;
                }

                if (File.Exists(fullPath))
                {
                    _registeredAssetPaths.Add(assetPath);
                    AssetReplacer.RegisterAssetReplacement(assetPath, fullPath);
                    SdkLogger.Msg($"  Registered asset replacement: {assetPath}");
                }
                else
                {
                    SdkLogger.Warning($"  Asset file not found: {fullPath}");
                }
            }
            catch (Exception ex)
            {
                SdkLogger.Error($"  Failed to load asset {assetPath}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Validates that a relative path stays within the modpack directory.
    /// Returns the full path if valid, null if path traversal was attempted.
    /// </summary>
    private static string ValidatePathWithinModpack(string modpackDir, string relativePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(modpackDir, relativePath));
            var baseFullPath = Path.GetFullPath(modpackDir);

            // Ensure base path ends with directory separator for proper prefix matching
            if (!baseFullPath.EndsWith(Path.DirectorySeparatorChar))
                baseFullPath += Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(baseFullPath, StringComparison.Ordinal) &&
                !fullPath.Equals(baseFullPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal))
            {
                return null;
            }

            return fullPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Apply all modpack template patches. Returns true if all template types were found
    /// and patched, false if some types had no instances (need to retry on later scene).
    /// </summary>
    private bool ApplyAllModpacks()
    {
        if (_loadedModpacks.Count == 0)
        {
            SdkLogger.Msg("No modpacks to apply");
            return true;
        }

        var allSucceeded = true;

        foreach (var modpack in _loadedModpacks.Values.OrderBy(m => m.LoadOrder))
        {
            var hasClones = modpack.Clones != null && modpack.Clones.Count > 0;
            // Use "patches" for V2, "templates" for V1
            var hasPatches = modpack.Patches != null && modpack.Patches.Count > 0;
            var hasTemplates = modpack.Templates != null && modpack.Templates.Count > 0;

            if (!hasClones && !hasPatches && !hasTemplates)
                continue;

            SdkLogger.Msg($"Applying modpack: {modpack.Name}");

            // Apply clones BEFORE patches so cloned templates exist when patches run
            if (hasClones)
            {
                if (!ApplyClones(modpack))
                    allSucceeded = false;
            }

            bool success;
            if (hasPatches && modpack.ManifestVersion >= 2)
            {
                success = ApplyModpackPatches(modpack);
            }
            else if (hasTemplates)
            {
                success = ApplyModpackTemplates(modpack);
            }
            else
            {
                success = true;
            }

            if (!success)
                allSucceeded = false;
        }

        if (_appliedPatchKeys.Count > 0)
        {
            var patchedTypes = _appliedPatchKeys.Select(k => k.Split(':').Last()).Distinct();
            PlayerLog($"Template types patched: {string.Join(", ", patchedTypes)}");
        }

        return allSucceeded;
    }

    /// <summary>
    /// Apply template modifications from a dictionary of templateType → instances → fields.
    /// Tracks success per modpack+type via _appliedPatchKeys to avoid double-patching on retry.
    /// Returns true if all template types had instances, false if some were missing.
    /// </summary>
    private bool ApplyTemplateData(
        Modpack modpack,
        Dictionary<string, Dictionary<string, Dictionary<string, object>>> data,
        string label)
    {
        if (data == null) return true;

        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (gameAssembly == null)
        {
            SdkLogger.Error($"Assembly-CSharp not found, cannot apply {label}");
            return false;
        }

        var allFound = true;

        foreach (var (templateTypeName, templateInstances) in data)
        {
            var patchKey = $"{modpack.Name}:{templateTypeName}";

            // Skip types we've already successfully patched
            if (_appliedPatchKeys.Contains(patchKey))
                continue;

            try
            {
                var templateType = gameAssembly.GetTypes()
                    .FirstOrDefault(t => t.Name == templateTypeName && !t.IsAbstract);

                if (templateType == null)
                {
                    SdkLogger.Warning($"  Template type '{templateTypeName}' not found in Assembly-CSharp");
                    allFound = false;
                    continue;
                }

                // Force-load templates via DataTemplateLoader before FindObjectsOfTypeAll
                // This ensures templates are in memory even if the game hasn't loaded them yet
                EnsureTemplatesLoaded(gameAssembly, templateType);

                var il2cppType = Il2CppType.From(templateType);
                var objects = Resources.FindObjectsOfTypeAll(il2cppType);

                if (objects == null || objects.Length == 0)
                {
                    SdkLogger.Warning($"  No {templateTypeName} instances found — will retry on next scene");
                    allFound = false;
                    continue;
                }

                int appliedCount = 0;
                foreach (var obj in objects)
                {
                    if (obj == null) continue;
                    var templateName = obj.name;
                    if (templateInstances.ContainsKey(templateName))
                    {
                        var modifications = templateInstances[templateName];
                        ApplyTemplateModifications(obj, templateType, modifications);
                        appliedCount++;
                    }
                }

                if (appliedCount > 0)
                {
                    SdkLogger.Msg($"  Applied {label} to {appliedCount} {templateTypeName} instance(s)");
                    _appliedPatchKeys.Add(patchKey);
                }
            }
            catch (Exception ex)
            {
                SdkLogger.Error($"  Failed to apply {label} to {templateTypeName}: {ex.Message}");
            }
        }

        return allFound;
    }

    /// <summary>
    /// Apply V2-format patches. Returns true if all template types were found.
    /// </summary>
    private bool ApplyModpackPatches(Modpack modpack)
    {
        return ApplyTemplateData(modpack, modpack.Patches, "patches");
    }

    /// <summary>
    /// Apply V1-format template modifications. Returns true if all template types were found.
    /// </summary>
    private bool ApplyModpackTemplates(Modpack modpack)
    {
        return ApplyTemplateData(modpack, modpack.Templates, "modifications");
    }

    private System.Collections.IEnumerator ApplyAssetReplacementsDelayed(string sceneName)
    {
        SdkLogger.Msg($"Asset replacement queued for scene: {sceneName} ({AssetReplacer.RegisteredCount} disk, {BundleLoader.LoadedAssetCount} bundle)");

        // Wait frames for textures to finish loading
        for (int i = 0; i < 15; i++)
            yield return null;

        SdkLogger.Msg($"Applying asset replacements for scene: {sceneName}");
        AssetReplacer.ApplyAllReplacements();
        PlayerLog($"Asset replacements applied for scene: {sceneName}");
    }

    // ApplyTemplateModifications is implemented in TemplateInjection.cs (partial class)

    private void InitializeRepl()
    {
        try
        {
            // Roslyn types live in a separate method to prevent the JIT from resolving
            // Microsoft.CodeAnalysis when compiling THIS method. Without this split,
            // the FileNotFoundException fires during JIT (before the try block runs)
            // and escapes the catch, crashing the entire OnInitializeMelon.
            InitializeReplCore();
        }
        catch (System.IO.FileNotFoundException ex)
        {
            SdkLogger.Warning($"REPL initialization failed - missing assembly: {ex.FileName}");
            SdkLogger.Warning($"  Message: {ex.Message}");
            SdkLogger.Warning($"  FusionLog: {ex.FusionLog ?? "(none)"}");
        }
        catch (System.IO.FileLoadException ex)
        {
            SdkLogger.Warning($"REPL initialization failed - assembly load error: {ex.FileName}");
            SdkLogger.Warning($"  Message: {ex.Message}");
            SdkLogger.Warning($"  FusionLog: {ex.FusionLog ?? "(none)"}");
        }
        catch (System.TypeLoadException ex)
        {
            SdkLogger.Warning($"REPL initialization failed - type load error: {ex.TypeName}");
            SdkLogger.Warning($"  Message: {ex.Message}");
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"REPL initialization failed: {ex.GetType().Name}");
            SdkLogger.Warning($"  Message: {ex.Message}");
            if (ex.InnerException != null)
                SdkLogger.Warning($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void InitializeReplCore()
    {
        var resolver = new RuntimeReferenceResolver();
        var refs = resolver.ResolveAll();
        var compiler = new RuntimeCompiler(refs);
        var evaluator = new ConsoleEvaluator(compiler);
        ReplPanel.Initialize(evaluator);
        DevConsole.SetReplEvaluator(evaluator);
        SdkLogger.Msg($"REPL initialized with {refs.Count} references");
    }

    private static void PlayerLog(string message)
    {
        UnityEngine.Debug.Log($"[MODDED] {message}");
    }
}

public class Modpack
{
    [JsonProperty("manifestVersion")]
    public int ManifestVersion { get; set; } = 1;

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("version")]
    public string Version { get; set; }

    [JsonProperty("author")]
    public string Author { get; set; }

    [JsonProperty("loadOrder")]
    public int LoadOrder { get; set; } = 100;

    /// <summary>
    /// V1 format: template modifications
    /// </summary>
    [JsonProperty("templates")]
    public Dictionary<string, Dictionary<string, Dictionary<string, object>>> Templates { get; set; }

    /// <summary>
    /// V2 format: data patches (same structure as templates, preferred in V2)
    /// </summary>
    [JsonProperty("patches")]
    public Dictionary<string, Dictionary<string, Dictionary<string, object>>> Patches { get; set; }

    [JsonProperty("assets")]
    public Dictionary<string, string> Assets { get; set; }

    [JsonProperty("bundles")]
    public List<string> Bundles { get; set; }

    [JsonProperty("securityStatus")]
    public string SecurityStatus { get; set; }

    /// <summary>
    /// Clone definitions: templateType → { newName → sourceName }
    /// </summary>
    [JsonProperty("clones")]
    public Dictionary<string, Dictionary<string, string>> Clones { get; set; }

    // -- Repository / Updates --
    /// <summary>
    /// Repository type for update checking (github, nexus, gamebanana, etc.)
    /// </summary>
    [JsonProperty("repositoryType")]
    public string RepositoryType { get; set; }

    /// <summary>
    /// Repository URL for update checking and mod homepage.
    /// </summary>
    [JsonProperty("repositoryUrl")]
    public string RepositoryUrl { get; set; }

    [JsonIgnore]
    public string DirectoryPath { get; set; }
}
