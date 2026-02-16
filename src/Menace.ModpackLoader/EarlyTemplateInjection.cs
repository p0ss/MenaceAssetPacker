using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
using Menace.SDK;
using UnityEngine;

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

    // Settings - must match GameMcpServer.SETTINGS_NAME where the setting is registered
    private const string SETTINGS_NAME = "MCP Server";
    private const string SETTING_KEY_EARLY_INJECTION = "EarlyInjection";
    private static bool _useEarlyInjection = false;
    private static bool _initialized = false;
    private static bool _hasInjectedThisSession = false;

    // Reference to the main mod for accessing modpack data
    private static ModpackLoaderMod _modInstance;

    // Cached types for black market pool injection
    private static Type _baseItemTemplateType;
    private static Type _strategyStateType;
    private static Type _strategyConfigType;
    private static bool _hasInjectedBlackMarketPool = false;

    // Field offset for BlackMarketMaxQuantity on BaseItemTemplate
    private const int OFFSET_BLACKMARKET_MAX_QUANTITY = 0xAC;

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

        // Hook BlackMarket.FillUp for blackmarket_refresh event
        var blackMarketType = gameAssembly.GetType("Menace.Strategy.BlackMarket");
        if (blackMarketType != null)
        {
            var fillUpMethod = blackMarketType.GetMethod("FillUp",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (fillUpMethod != null)
            {
                var bmPrefix = typeof(EarlyTemplateInjection).GetMethod(nameof(BlackMarketFillUp_Prefix),
                    BindingFlags.Static | BindingFlags.NonPublic);
                harmony.Patch(fillUpMethod, prefix: new HarmonyMethod(bmPrefix));
                _log.Msg("Patched BlackMarket.FillUp");
            }
            else
            {
                _log.Warning("BlackMarket.FillUp method not found");
            }
        }
        else
        {
            _log.Warning("BlackMarket type not found");
        }
    }

    /// <summary>
    /// Prefix for CreateNewGame - injects templates before campaign initialization.
    /// Fires campaign_start Lua event for modders to hook into.
    /// </summary>
    private static void CreateNewGame_Prefix()
    {
        InjectTemplatesNow("CreateNewGame");

        // Fire Lua event so modders can inject into pools
        try
        {
            LuaScriptEngine.Instance.OnCampaignStart();
        }
        catch (Exception ex)
        {
            _log.Warning($"Error firing campaign_start Lua event: {ex.Message}");
        }
    }

    /// <summary>
    /// Prefix for OnOperationFinished - ensures templates exist before black market refresh.
    /// Fires operation_end Lua event for modders to hook into.
    /// </summary>
    private static void OnOperationFinished_Prefix()
    {
        // Only inject if we haven't already this session
        // (templates should persist, but just in case)
        if (!_hasInjectedThisSession)
        {
            InjectTemplatesNow("OnOperationFinished");
        }

        // Always fire operation_end event for Lua scripts
        try
        {
            LuaScriptEngine.Instance.OnOperationEnd();
        }
        catch (Exception ex)
        {
            _log.Warning($"Error firing operation_end Lua event: {ex.Message}");
        }
    }

    /// <summary>
    /// Prefix for LoadGame - injects templates before loading saved campaign.
    /// Fires campaign_loaded Lua event for modders to hook into.
    /// </summary>
    private static void LoadGame_Prefix()
    {
        InjectTemplatesNow("LoadGame");

        // Fire Lua event so modders can react to save load
        try
        {
            LuaScriptEngine.Instance.OnCampaignLoaded();
        }
        catch (Exception ex)
        {
            _log.Warning($"Error firing campaign_loaded Lua event: {ex.Message}");
        }
    }

    /// <summary>
    /// Prefix for BlackMarket.FillUp - injects custom items into the pool and fires
    /// blackmarket_refresh event before restock.
    /// </summary>
    private static void BlackMarketFillUp_Prefix()
    {
        // Inject custom items into the black market pool
        // This ensures items with BlackMarketMaxQuantity > 0 are in the pool
        if (!_hasInjectedBlackMarketPool)
        {
            InjectItemsIntoBlackMarketPool();
        }

        _log.Msg("[BlackMarket.FillUp] Firing blackmarket_refresh Lua event");

        try
        {
            LuaScriptEngine.Instance.OnBlackMarketRefresh();
        }
        catch (Exception ex)
        {
            _log.Warning($"Error firing blackmarket_refresh Lua event: {ex.Message}");
        }
    }

    /// <summary>
    /// Injects all item templates with BlackMarketMaxQuantity > 0 into the
    /// StrategyConfig.BlackMarketItems pool, enabling custom items to appear
    /// in the black market without requiring Lua scripting.
    /// </summary>
    private static void InjectItemsIntoBlackMarketPool()
    {
        try
        {
            _log.Msg("[BlackMarket] Scanning for custom items to inject into pool...");

            // Ensure types are loaded
            EnsureBlackMarketTypesLoaded();

            if (_baseItemTemplateType == null || _strategyStateType == null || _strategyConfigType == null)
            {
                _log.Warning("[BlackMarket] Required types not found, cannot inject items");
                return;
            }

            // Get StrategyState.Get() to access the current strategy state
            var getMethod = _strategyStateType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            var strategyState = getMethod?.Invoke(null, null);
            if (strategyState == null)
            {
                _log.Warning("[BlackMarket] StrategyState.Get() returned null");
                return;
            }

            // Get Config property
            var configProp = _strategyStateType.GetProperty("Config", BindingFlags.Public | BindingFlags.Instance);
            var config = configProp?.GetValue(strategyState);
            if (config == null)
            {
                _log.Warning("[BlackMarket] StrategyConfig is null");
                return;
            }

            // Get BlackMarketItems property (the pool)
            var itemsProp = _strategyConfigType.GetProperty("BlackMarketItems", BindingFlags.Public | BindingFlags.Instance);
            var itemPool = itemsProp?.GetValue(config);
            if (itemPool == null)
            {
                _log.Warning("[BlackMarket] BlackMarketItems pool is null");
                return;
            }

            // Get the Add method on the list
            var poolType = itemPool.GetType();
            var addMethod = poolType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
            var containsMethod = poolType.GetMethod("Contains", BindingFlags.Public | BindingFlags.Instance);
            var countProp = poolType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);

            if (addMethod == null)
            {
                _log.Warning("[BlackMarket] Could not find Add method on item pool");
                return;
            }

            int originalCount = (int)(countProp?.GetValue(itemPool) ?? 0);

            // Find all BaseItemTemplate objects in the game
            var il2cppType = Il2CppType.From(_baseItemTemplateType);
            var allTemplates = Resources.FindObjectsOfTypeAll(il2cppType);

            if (allTemplates == null || allTemplates.Length == 0)
            {
                _log.Msg("[BlackMarket] No item templates found");
                _hasInjectedBlackMarketPool = true;
                return;
            }

            int injectedCount = 0;

            foreach (var templateObj in allTemplates)
            {
                if (templateObj == null) continue;

                try
                {
                    // Read BlackMarketMaxQuantity at offset 0xAC
                    var templatePtr = ((Il2CppObjectBase)templateObj).Pointer;
                    if (templatePtr == IntPtr.Zero) continue;

                    int maxQuantity = System.Runtime.InteropServices.Marshal.ReadInt32(templatePtr + OFFSET_BLACKMARKET_MAX_QUANTITY);

                    if (maxQuantity > 0)
                    {
                        // Check if already in pool
                        bool alreadyInPool = false;
                        if (containsMethod != null)
                        {
                            try
                            {
                                alreadyInPool = (bool)containsMethod.Invoke(itemPool, new object[] { templateObj });
                            }
                            catch
                            {
                                // Contains might fail for various reasons, proceed anyway
                            }
                        }

                        if (!alreadyInPool)
                        {
                            addMethod.Invoke(itemPool, new object[] { templateObj });
                            injectedCount++;
                            _log.Msg($"[BlackMarket] Injected '{templateObj.name}' into pool (MaxQty: {maxQuantity})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.Warning($"[BlackMarket] Error processing template: {ex.Message}");
                }
            }

            int finalCount = (int)(countProp?.GetValue(itemPool) ?? 0);
            _log.Msg($"[BlackMarket] Pool injection complete: {injectedCount} items added ({originalCount} -> {finalCount})");
            _hasInjectedBlackMarketPool = true;
        }
        catch (Exception ex)
        {
            _log.Error($"[BlackMarket] Failed to inject items into pool: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads types needed for black market pool injection.
    /// </summary>
    private static void EnsureBlackMarketTypesLoaded()
    {
        if (_baseItemTemplateType != null && _strategyStateType != null && _strategyConfigType != null)
            return;

        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (gameAssembly == null)
        {
            _log.Warning("[BlackMarket] Assembly-CSharp not found");
            return;
        }

        _baseItemTemplateType ??= gameAssembly.GetType("Menace.Items.BaseItemTemplate");
        _strategyStateType ??= gameAssembly.GetType("Menace.States.StrategyState");
        _strategyConfigType ??= gameAssembly.GetType("Menace.Strategy.StrategyConfig");

        if (_baseItemTemplateType == null)
            _log.Warning("[BlackMarket] BaseItemTemplate type not found");
        if (_strategyStateType == null)
            _log.Warning("[BlackMarket] StrategyState type not found");
        if (_strategyConfigType == null)
            _log.Warning("[BlackMarket] StrategyConfig type not found");
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
        _hasInjectedBlackMarketPool = false;
    }
}
