using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime;
using MelonLoader;
using Menace.SDK;

namespace Menace.ModpackLoader;

/// <summary>
/// Early template injection system that patches templates before game systems
/// build their pools (black market, army lists, spawn pools, etc.).
///
/// Hooks StrategyState.CreateNewGame to inject templates before the campaign
/// initializes its pools. This ensures modded content is visible everywhere.
///
/// Can be toggled via ModSettings. When disabled, falls back to the legacy
/// scene-load based injection.
/// </summary>
public static class EarlyTemplateInjection
{
    private static readonly MelonLogger.Instance _log = new("EarlyTemplateInjection");

    // Settings
    private const string SETTINGS_NAME = "Modpack Loader";
    private const string SETTING_KEY_EARLY_INJECTION = "EarlyInjection";
    private static bool _useEarlyInjection = false;
    private static bool _initialized = false;
    private static bool _hasInjectedThisSession = false;

    // Reference to the main mod for accessing modpack data
    private static ModpackLoaderMod _modInstance;

    /// <summary>
    /// Whether early injection is enabled.
    /// </summary>
    public static bool IsEnabled => _useEarlyInjection;

    /// <summary>
    /// Whether early injection has been initialized and patches applied.
    /// </summary>
    public static bool IsInitialized => _initialized;

    /// <summary>
    /// Whether templates have been injected this session.
    /// Used to skip legacy injection if early injection already ran.
    /// </summary>
    public static bool HasInjectedThisSession => _hasInjectedThisSession;

    /// <summary>
    /// Initialize the early injection system.
    /// Call this from ModpackLoaderMod.OnInitializeMelon after modpacks are loaded.
    /// </summary>
    public static void Initialize(ModpackLoaderMod modInstance, HarmonyLib.Harmony harmony)
    {
        _modInstance = modInstance;

        // Read setting value (registered by GameMcpServer under "Modpack Loader")
        _useEarlyInjection = ModSettings.Get<bool>(SETTINGS_NAME, SETTING_KEY_EARLY_INJECTION);

        if (!_useEarlyInjection)
        {
            _log.Msg("Early injection disabled, using legacy scene-load injection");
            return;
        }

        // Apply Harmony patches
        try
        {
            ApplyPatches(harmony);
            _initialized = true;
            _log.Msg("Early template injection initialized - will inject before CreateNewGame");
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to initialize early injection: {ex.Message}");
            _log.Error("Falling back to legacy scene-load injection");
            _useEarlyInjection = false;
        }
    }

    private static void ApplyPatches(HarmonyLib.Harmony harmony)
    {
        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (gameAssembly == null)
        {
            throw new Exception("Assembly-CSharp not found");
        }

        // Hook StrategyState.CreateNewGame - this is called when starting a new campaign
        var strategyStateType = gameAssembly.GetType("Menace.States.StrategyState");
        if (strategyStateType == null)
        {
            throw new Exception("StrategyState type not found");
        }

        var createNewGameMethod = strategyStateType.GetMethod("CreateNewGame",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (createNewGameMethod == null)
        {
            throw new Exception("CreateNewGame method not found");
        }

        // Apply prefix patch
        var prefix = typeof(EarlyTemplateInjection).GetMethod(nameof(CreateNewGame_Prefix),
            BindingFlags.Static | BindingFlags.NonPublic);

        harmony.Patch(createNewGameMethod, prefix: new HarmonyMethod(prefix));
        _log.Msg("Patched StrategyState.CreateNewGame");

        // Also hook OnOperationFinished for black market refresh
        var onOpFinishedMethod = strategyStateType.GetMethod("OnOperationFinished",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (onOpFinishedMethod != null)
        {
            var opPrefix = typeof(EarlyTemplateInjection).GetMethod(nameof(OnOperationFinished_Prefix),
                BindingFlags.Static | BindingFlags.NonPublic);
            harmony.Patch(onOpFinishedMethod, prefix: new HarmonyMethod(opPrefix));
            _log.Msg("Patched StrategyState.OnOperationFinished");
        }

        // Hook loading a saved game as well
        var loadGameMethod = strategyStateType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name.Contains("LoadGame") || m.Name.Contains("LoadSave"));

        if (loadGameMethod != null)
        {
            var loadPrefix = typeof(EarlyTemplateInjection).GetMethod(nameof(LoadGame_Prefix),
                BindingFlags.Static | BindingFlags.NonPublic);
            harmony.Patch(loadGameMethod, prefix: new HarmonyMethod(loadPrefix));
            _log.Msg($"Patched {loadGameMethod.Name}");
        }
    }

    /// <summary>
    /// Prefix for CreateNewGame - injects templates before campaign initialization.
    /// </summary>
    private static void CreateNewGame_Prefix()
    {
        InjectTemplatesNow("CreateNewGame");
    }

    /// <summary>
    /// Prefix for OnOperationFinished - ensures templates exist before black market refresh.
    /// </summary>
    private static void OnOperationFinished_Prefix()
    {
        // Only inject if we haven't already this session
        // (templates should persist, but just in case)
        if (!_hasInjectedThisSession)
        {
            InjectTemplatesNow("OnOperationFinished");
        }
    }

    /// <summary>
    /// Prefix for LoadGame - injects templates before loading saved campaign.
    /// </summary>
    private static void LoadGame_Prefix()
    {
        InjectTemplatesNow("LoadGame");
    }

    /// <summary>
    /// Actually inject all templates now.
    /// </summary>
    private static void InjectTemplatesNow(string trigger)
    {
        if (_modInstance == null)
        {
            _log.Warning($"[{trigger}] ModInstance is null, cannot inject");
            return;
        }

        if (_hasInjectedThisSession)
        {
            _log.Msg($"[{trigger}] Templates already injected this session, skipping");
            return;
        }

        _log.Msg($"[{trigger}] Early injecting templates before pools are built...");

        try
        {
            // Force load sprites first
            AssetReplacer.LoadPendingSprites();

            // Apply all modpack patches
            var success = _modInstance.ApplyAllModpacks();

            if (success)
            {
                _hasInjectedThisSession = true;
                _log.Msg($"[{trigger}] Early injection complete");
            }
            else
            {
                _log.Warning($"[{trigger}] Early injection partial - some types may not be loaded yet");
            }
        }
        catch (Exception ex)
        {
            _log.Error($"[{trigger}] Early injection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Reset injection state. Call when returning to main menu or similar.
    /// </summary>
    public static void Reset()
    {
        _hasInjectedThisSession = false;
    }
}
