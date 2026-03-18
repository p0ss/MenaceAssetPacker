using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime;
using MelonLoader;
using Menace.SDK;
using Menace.SDK.CustomMaps;
using Menace.SDK.Internal;
using Menace.SDK.Repl;
using Menace.ModpackLoader.Mcp;
using Menace.ModpackLoader.Diagnostics;
using Menace.ModpackLoader.VisualEditor.Runtime;
using Menace.ModpackLoader.TemplateLoading;
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
        // Initialize SDK subsystems - order matters!
        // SdkLogger must be initialized FIRST so all subsequent logs go to both MelonLoader and DevConsole
        SdkLogger.Initialize(LoggerInstance);
        OffsetCache.Initialize();
        DevConsole.Initialize();
        DevConsole.ApplyInputPatches(HarmonyInstance);

        // Initialize TemplateSchema from schema.json (optional - provides offset lookups)
        InitializeTemplateSchema();

        SdkLogger.Msg($"{ModkitVersion.LoaderFull} initialized");
        ModSettings.Initialize();

        // Register Modpack Loader settings
        RegisterModpackLoaderSettings();

        InitializeRepl();

        // Initialize diagnostics (Harmony patches for template/scene loading)
        InitializeDiagnostics();

        // Register SDK console commands
        RegisterSdkCommands();

        // Initialize MCP HTTP server for external tooling integration
        // Controlled via ModSettings - starts automatically if enabled
        GameMcpServer.Initialize(LoggerInstance);

        // Initialize menu injection for mod settings UI
        MenuInjector.Initialize();

        // Initialize GLB loader (can be disabled via settings if it causes issues)
        GlbLoader.Initialize();

        LoadModpacks();
        DllLoader.InitializeAllPlugins();

        // Initialize early template injection (experimental - opt-in via settings)
        // This hooks game initialization to inject templates before pools are built
        EarlyTemplateInjection.Initialize(this, HarmonyInstance);

        // Initialize tactical event hooks for C# and Lua event subscriptions
        // Patches TacticalManager.InvokeOnX methods to fire SDK events
        TacticalEventHooks.Initialize(HarmonyInstance);

        // Wire EffectSystem to round end for automatic effect expiry
        TacticalEventHooks.OnRoundEnd += _ => EffectSystem.OnRoundEnd();

        // Initialize coroutine system (delays, repeats for mod effects)
        SDK.Coroutine.Initialize();

        // Initialize strategy event hooks for C# and Lua event subscriptions
        // Patches Roster, StoryFaction, Squaddies, Operation, BlackMarket methods
        StrategyEventHooks.Initialize(HarmonyInstance);

        // Cleanup coroutines, state machines, and once-trackers when mission ends
        StrategyEventHooks.OnMissionEnded += _ =>
        {
            SDK.Coroutine.Cleanup();
            StateMachine.Cleanup();
            OnceTracker.Cleanup();
            EffectSystem.ClearAll();
        };

        // Patch bug reporter to include mod list in all reports
        BugReporterPatches.Initialize(LoggerInstance, HarmonyInstance);

        // Initialize boot skip patches (splash/intro skipping in dev mode)
        BootSkip.Initialize(HarmonyInstance);

        // Initialize custom maps SDK (seed/size overrides, generator config, mission pool injection)
        CustomMaps.Initialize(HarmonyInstance);

        // Initialize Lua scripting engine
        try
        {
            LuaScriptEngine.Instance.Initialize(LoggerInstance);
            GameState.SceneLoaded += LuaScriptEngine.Instance.OnSceneLoaded;
            GameState.TacticalReady += LuaScriptEngine.Instance.OnTacticalReady;
            LoadLuaScripts();
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[LuaEngine] Failed to initialize: {ex.GetType().Name}: {ex.Message}");
            SdkLogger.Error($"[LuaEngine] Stack: {ex.StackTrace}");
        }

        // Load custom maps from modpacks
        LoadCustomMaps();

        // Initialize visual mod graph interpreter
        // This executes .modgraph.json files at runtime
        try
        {
            GraphInterpreter.Instance.Initialize();
            LoadVisualMods();
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[GraphInterpreter] Failed to initialize: {ex.GetType().Name}: {ex.Message}");
        }

        // Initialize multi-lingual localization system (loads all language CSVs)
        // This enables modders to view/edit translations for all languages
        try
        {
            SdkLogger.Msg("[Localization] Initializing multi-lingual system...");
            MultiLingualLocalization.Initialize();
            SdkLogger.Msg($"[Localization] Loaded {MultiLingualLocalization.GetAvailableLanguages().Length} languages");
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[Localization] Failed to initialize multi-lingual system: {ex.Message}");
            // Non-critical failure - game's localization system will still work
        }

        // Emit startup banner to Player.log for game dev triage
        PlayerLog("========================================");
        PlayerLog("THIS GAME SESSION IS RUNNING MODDED");
        PlayerLog(ModkitVersion.LoaderFull);
        PlayerLog($"Loaded {_loadedModpacks.Count} modpack(s):");
        foreach (var mp in _loadedModpacks.Values.OrderBy(m => m.LoadOrder))
            PlayerLog($"  - {mp.Name} v{mp.Version} by {mp.Author ?? "Unknown"} (order: {mp.LoadOrder})");
        PlayerLog($"Bundles: {BundleLoader.LoadedBundleCount} ({BundleLoader.LoadedAssetCount} assets)");
        PlayerLog($"Asset replacements registered: {AssetReplacer.RegisteredCount}");
        PlayerLog($"Custom sprites loaded: {AssetReplacer.CustomSpriteCount}");
        PlayerLog($"Compiled assets in manifest: {CompiledAssetLoader.ManifestAssetCount}");
        PlayerLog($"Mod DLLs: {DllLoader.GetLoadedAssemblies().Count}");
        var pluginSummary = DllLoader.GetPluginSummary();
        if (pluginSummary != null)
            PlayerLog($"Modpack plugins: {pluginSummary}");
        PlayerLog($"Lua scripts loaded: {LuaScriptEngine.Instance.LoadedScriptCount}");
        PlayerLog($"Visual mods loaded: {GraphInterpreter.Instance.LoadedMods.Count}");
        PlayerLog($"Custom maps registered: {CustomMapRegistry.Count}");
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
        MenuInjector.OnSceneLoaded(sceneName);

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
        MainThreadExecutor.ProcessQueue();
        GameState.ProcessUpdate();
        DevConsole.Update();
        MenuInjector.Update();
        DllLoader.NotifyUpdate();
    }

    public override void OnGUI()
    {
        DevConsole.Draw();
        ErrorNotification.Draw();
        MenuInjector.Draw();
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

        // If early injection is enabled and has already run, skip legacy injection
        if (EarlyTemplateInjection.IsEnabled && EarlyTemplateInjection.HasInjectedThisSession)
        {
            SdkLogger.Msg($"Early injection already applied, skipping legacy scene-load injection");
            _templatesLoaded = true;
            yield break;
        }

        SdkLogger.Msg($"Applying modpack modifications (scene: {sceneName})...");

        // Initialize save system watcher (tries to find saves folder)
        SaveSystemPatches.TryInitialize();

        // Load compiled assets now that Unity is ready
        // (manifest was loaded during init, actual Resources.Load happens here)
        CompiledAssetLoader.LoadAssets();

        // Load any pending custom sprites asynchronously to avoid stutter
        // This spreads the work across multiple frames (5 sprites per frame by default)
        var pendingCount = AssetReplacer.PendingSpriteCount;
        if (pendingCount > 0)
        {
            SdkLogger.Msg($"Loading {pendingCount} sprites asynchronously...");
            var spriteLoader = AssetReplacer.LoadPendingSpritesAsync(5);
            while (spriteLoader.MoveNext())
            {
                yield return spriteLoader.Current;
            }
        }

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

    /// <summary>
    /// Register settings for the Modpack Loader module.
    /// Settings appear in the in-game Settings menu.
    /// </summary>
    private static void RegisterModpackLoaderSettings()
    {
        ModSettings.Register("Modpack Loader", settings =>
        {
            settings.AddHeader("GLB Model Loading");
            settings.AddToggle("GlbLoader", "Enable GLB Loading", true);
        });
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
        BootSkip.RegisterConsoleCommands();
        SimpleAnimations.RegisterConsoleCommands();
        UIInspector.RegisterConsoleCommands();
        Modpacks.RegisterConsoleCommands();
        GraphInterpreter.RegisterConsoleCommands();

        // Register test harness commands for automated testing
        TestHarnessCommands.Register();

        // Register diagnostic commands
        DataTemplateLoaderDiagnostics.RegisterConsoleCommands();
        SceneLoadingDiagnostics.RegisterConsoleCommands();
        SdkSafetyTesting.RegisterConsoleCommands();
        TemplatePipelineValidator.RegisterConsoleCommands();
    }

    private void InitializeDiagnostics()
    {
        try
        {
            // Initialize diagnostic patches (for discovering issues)
            DataTemplateLoaderDiagnostics.Initialize(HarmonyInstance);
            SceneLoadingDiagnostics.Initialize(HarmonyInstance);

            // Initialize fixes (for known issues)
            TemplateLoadingFixes.Initialize(HarmonyInstance);
            SceneLoadingFixes.Initialize(HarmonyInstance);

            SdkLogger.Msg("Diagnostics and fixes initialized");
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"Failed to initialize diagnostics: {ex.Message}");
        }
    }

    private void InitializeTemplateSchema()
    {
        // Try to find schema.json in standard locations
        var baseDir = Directory.GetCurrentDirectory();
        var candidatePaths = new[]
        {
            Path.Combine(baseDir, "UserData", "Menace", "schema.json"),
            Path.Combine(baseDir, "Mods", "MenaceModpackLoader", "schema.json"),
            Path.Combine(baseDir, "schema.json"),
            Path.Combine(Path.GetDirectoryName(typeof(ModpackLoaderMod).Assembly.Location) ?? baseDir, "schema.json"),
        };

        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                TemplateSchema.Initialize(path);
                return;
            }
        }

        // Schema not found - this is OK, SDK still works without it
        SdkLogger.Warning("[TemplateSchema] schema.json not found - schema-driven offsets unavailable");
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

                    // V2: Load mod DLLs, bundles, and models
                    if (manifestVersion >= 2 && !string.IsNullOrEmpty(modpackDir))
                    {
                        BundleLoader.LoadBundles(modpackDir, modpack.Name);
                        GlbLoader.LoadModpackModels(modpackDir);
                        DllLoader.LoadModDlls(modpackDir, modpack.Name, modpack.SecurityStatus ?? "Unreviewed");
                    }

                    // Load asset replacements (both V1 and V2)
                    // Textures are NOT compiled into assets due to ColorSpace issues,
                    // so replacements are applied at runtime via ImageConversion.LoadImage
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

        // Load compiled asset manifest (actual asset loading deferred until Unity is ready)
        // Assets are embedded in resources.assets and registered with ResourceManager.
        var compiledDir = Path.Combine(modsPath, "compiled");
        if (Directory.Exists(compiledDir))
        {
            CompiledAssetLoader.LoadManifest(compiledDir);
        }
    }

    /// <summary>
    /// Load Lua scripts from all modpacks.
    /// Scripts are loaded from the scripts/ directory within each modpack.
    /// </summary>
    private void LoadLuaScripts()
    {
        int scriptCount = 0;

        foreach (var modpack in _loadedModpacks.Values.OrderBy(m => m.LoadOrder))
        {
            if (string.IsNullOrEmpty(modpack.DirectoryPath))
                continue;

            var scriptsDir = Path.Combine(modpack.DirectoryPath, "scripts");
            if (!Directory.Exists(scriptsDir))
                continue;

            var luaFiles = Directory.GetFiles(scriptsDir, "*.lua", SearchOption.AllDirectories);
            foreach (var luaFile in luaFiles)
            {
                if (LuaScriptEngine.Instance.LoadModpackScript(modpack.Name, luaFile))
                    scriptCount++;
            }
        }

        if (scriptCount > 0)
            SdkLogger.Msg($"Loaded {scriptCount} Lua script(s)");
    }

    /// <summary>
    /// Load custom map configurations from all modpacks.
    /// Maps are loaded from the custom_maps/ directory within each modpack.
    /// </summary>
    private void LoadCustomMaps()
    {
        int mapCount = 0;

        foreach (var modpack in _loadedModpacks.Values.OrderBy(m => m.LoadOrder))
        {
            if (string.IsNullOrEmpty(modpack.DirectoryPath))
                continue;

            var mapsDir = Path.Combine(modpack.DirectoryPath, "custom_maps");
            if (!Directory.Exists(mapsDir))
                continue;

            var count = CustomMapRegistry.LoadFromDirectory(mapsDir);
            if (count > 0)
            {
                SdkLogger.Msg($"  Loaded {count} custom map(s) from {modpack.Name}");
                mapCount += count;
            }
        }

        if (mapCount > 0)
            SdkLogger.Msg($"Loaded {mapCount} custom map(s) total");
    }

    /// <summary>
    /// Load visual mod graphs (.modgraph.json) from all modpacks.
    /// These are executed at runtime via the GraphInterpreter.
    /// </summary>
    private void LoadVisualMods()
    {
        int graphCount = 0;

        foreach (var modpack in _loadedModpacks.Values.OrderBy(m => m.LoadOrder))
        {
            if (string.IsNullOrEmpty(modpack.DirectoryPath))
                continue;

            // Look for .modgraph.json files in the visual_mods directory
            var modsDir = Path.Combine(modpack.DirectoryPath, "visual_mods");
            if (!Directory.Exists(modsDir))
                continue;

            var count = GraphInterpreter.Instance.LoadModsFromDirectory(modsDir);
            if (count > 0)
            {
                SdkLogger.Msg($"  Loaded {count} visual mod(s) from {modpack.Name}");
                graphCount += count;
            }
        }

        if (graphCount > 0)
            SdkLogger.Msg($"Loaded {graphCount} visual mod(s) total");
    }

    private void LoadModpackAssets(Modpack modpack)
    {
        if (modpack.Assets == null || string.IsNullOrEmpty(modpack.DirectoryPath))
            return;

        // Register preload assets first (these load immediately for loading screens, etc.)
        if (modpack.PreloadAssets != null)
        {
            foreach (var preloadName in modpack.PreloadAssets)
            {
                AssetReplacer.RegisterPreloadAsset(preloadName);
            }
            if (modpack.PreloadAssets.Count > 0)
                SdkLogger.Msg($"  Registered {modpack.PreloadAssets.Count} preload asset(s)");
        }

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
                    var ext = Path.GetExtension(assetPath).ToLowerInvariant();
                    var assetName = Path.GetFileNameWithoutExtension(assetPath);

                    // For texture files, also load as a custom Sprite so template patches
                    // can reference them by name (e.g., Icon fields on WeaponTemplate)
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" || ext == ".bmp")
                    {
                        AssetReplacer.RegisterAssetReplacement(assetPath, fullPath);
                        SdkLogger.Msg($"  Registered asset replacement: {assetPath}");

                        // LoadCustomSprite returns null for non-preload assets (they're queued)
                        // Preload assets return the sprite immediately
                        var sprite = AssetReplacer.LoadCustomSprite(fullPath, assetName);
                        if (sprite != null)
                            SdkLogger.Msg($"  Preloaded sprite ready: '{assetName}'");
                        // Non-preload sprites are queued - no warning needed
                    }
                    // For GLB/GLTF files, load as custom 3D model
                    else if (ext == ".glb" || ext == ".gltf")
                    {
                        var model = GlbLoader.LoadGlb(fullPath);
                        if (model != null)
                            SdkLogger.Msg($"  Custom model loaded: '{assetName}' ({model.Meshes.Count} meshes)");
                        else
                            SdkLogger.Warning($"  Failed to load custom model: '{assetName}'");
                    }
                    // For audio files, load as custom AudioClip
                    else if (ext == ".wav" || ext == ".ogg")
                    {
                        var clip = AssetReplacer.LoadCustomAudio(fullPath, assetName);
                        if (clip != null)
                            SdkLogger.Msg($"  Custom audio loaded: '{assetName}'");
                        else
                            SdkLogger.Warning($"  Failed to load custom audio: '{assetName}'");
                    }
                    else
                    {
                        // Other asset types - just register for replacement
                        AssetReplacer.RegisterAssetReplacement(assetPath, fullPath);
                        SdkLogger.Msg($"  Registered asset replacement: {assetPath}");
                    }
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

        // Log how many sprites are queued for async loading
        var pendingCount = AssetReplacer.PendingSpriteCount;
        if (pendingCount > 0)
            SdkLogger.Msg($"  {pendingCount} sprite(s) queued for async loading");
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
    internal bool ApplyAllModpacks()
    {
        if (_loadedModpacks.Count == 0)
        {
            SdkLogger.Msg("No modpacks to apply");
            return true;
        }

        // First, register any bundle-loaded clone templates with DataTemplateLoader
        // This handles clones that were compiled into the templates.bundle
        RegisterBundleClones();

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

                // Clear name lookup cache so patches can find the new clones
                InvalidateNameLookupCache();
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
                // Cache GetID method lookup for this template type
                var getIdMethod = templateType.GetMethod("GetID",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                // Debug: Log GetID availability and patch keys we're looking for
                var patchKeys = string.Join(", ", templateInstances.Keys.Take(5));
                SdkLogger.Msg($"  [Debug] {templateTypeName}: {objects.Length} instances, GetID={getIdMethod != null}, patches=[{patchKeys}]");

                foreach (var obj in objects)
                {
                    if (obj == null) continue;
                    var templateName = obj.name;

                    // First try matching by obj.name (m_Name)
                    if (templateInstances.ContainsKey(templateName))
                    {
                        SdkLogger.Msg($"    [Debug] obj.name matched: '{templateName}'");
                        var modifications = templateInstances[templateName];
                        ApplyTemplateModifications(obj, templateType, modifications);
                        appliedCount++;
                    }
                    // Fall back to matching by GetID() (m_ID) for cloned templates
                    // Cloned templates have m_Name unchanged but m_ID set to the new name
                    else if (getIdMethod != null)
                    {
                        try
                        {
                            // Need to cast to the proxy type first
                            var genericTryCast = TryCastMethod.MakeGenericMethod(templateType);
                            var castObj = genericTryCast.Invoke(obj, null);
                            if (castObj != null)
                            {
                                var templateId = getIdMethod.Invoke(castObj, null)?.ToString();
                                if (!string.IsNullOrEmpty(templateId) && templateInstances.ContainsKey(templateId))
                                {
                                    SdkLogger.Msg($"    [Debug] GetID matched: '{templateId}'");
                                    var modifications = templateInstances[templateId];
                                    ApplyTemplateModifications(obj, templateType, modifications);
                                    appliedCount++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            SdkLogger.Warning($"    [Debug] GetID failed for {templateName}: {ex.Message}");
                        }
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

    /// <summary>
    /// Assets that should load immediately (synchronously) at startup.
    /// Use for loading screen backgrounds, critical UI elements, etc.
    /// Other assets load asynchronously across frames to avoid stutter.
    /// </summary>
    [JsonProperty("preloadAssets")]
    public List<string> PreloadAssets { get; set; }

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
