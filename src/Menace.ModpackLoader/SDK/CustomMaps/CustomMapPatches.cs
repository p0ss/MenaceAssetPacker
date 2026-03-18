using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;

namespace Menace.SDK.CustomMaps;

/// <summary>
/// Harmony patches for custom map parameter injection.
///
/// Patches are applied at key points in the map generation pipeline:
/// 1. MissionTemplate.TryCreateMapLayout - Override seed and generator params
/// 2. Map.IsInBounds - Allow larger map sizes
/// 3. Map.ClampToBounds - Allow larger map sizes
/// 4. OperationTemplate.GetMissionsForDifficulties - Inject custom maps into pools
/// </summary>
public static class CustomMapPatches
{
    private static HarmonyLib.Harmony _harmony;
    private static bool _initialized;
    private static int _currentMapSize = 42; // Default game size

    /// <summary>
    /// Currently active map size override.
    /// </summary>
    public static int CurrentMapSize
    {
        get => _currentMapSize;
        set => _currentMapSize = Math.Clamp(value, 20, 80);
    }

    /// <summary>
    /// Initialize and apply all custom map patches.
    /// </summary>
    public static bool Initialize(HarmonyLib.Harmony harmony)
    {
        if (_initialized)
            return true;

        if (harmony == null)
        {
            ModError.ReportInternal("CustomMapPatches.Initialize", "Harmony instance is null");
            return false;
        }

        _harmony = harmony;

        try
        {
            // Apply patches
            ApplyMissionPatches();
            ApplyMapSizePatches();
            ApplyMissionPoolPatches();
            ApplyTileOverridePatches();

            _initialized = true;
            SdkLogger.Msg("[CustomMaps] Patches initialized");
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("CustomMapPatches.Initialize", "Failed to apply patches", ex);
            return false;
        }
    }

    /// <summary>
    /// Apply patches for mission/map generation parameter override.
    /// </summary>
    private static void ApplyMissionPatches()
    {
        // Patch MissionTemplate.TryCreateMapLayout to inject custom parameters
        var patchMethod = typeof(CustomMapPatches).GetMethod(nameof(TryCreateMapLayout_Prefix),
            BindingFlags.NonPublic | BindingFlags.Static);

        GamePatch.Prefix(_harmony, "Menace.Strategy.Missions.MissionTemplate",
            "TryCreateMapLayout", patchMethod);

        // Patch for generator iteration
        var generatorPatchMethod = typeof(CustomMapPatches).GetMethod(nameof(InitGenerators_Postfix),
            BindingFlags.NonPublic | BindingFlags.Static);

        // This will be the patch point after generators are created but before Init
        // Exact method name TBD from Ghidra analysis
    }

    /// <summary>
    /// Apply patches for map size validation override.
    /// </summary>
    private static void ApplyMapSizePatches()
    {
        // Patch Map.IsInBounds to use dynamic size
        var isInBoundsMethod = typeof(CustomMapPatches).GetMethod(nameof(IsInBounds_Prefix),
            BindingFlags.NonPublic | BindingFlags.Static);

        GamePatch.Prefix(_harmony, "Menace.Tactical.Map", "IsInBounds", isInBoundsMethod);

        // Patch Map.ClampToBounds to use dynamic size
        var clampMethod = typeof(CustomMapPatches).GetMethod(nameof(ClampToBounds_Prefix),
            BindingFlags.NonPublic | BindingFlags.Static);

        GamePatch.Prefix(_harmony, "Menace.Tactical.Map", "ClampToBounds", clampMethod);
    }

    /// <summary>
    /// Apply patches for mission pool injection.
    /// </summary>
    private static void ApplyMissionPoolPatches()
    {
        // Patch OperationTemplate.GetMissionsForDifficulties to add custom maps
        var poolPatchMethod = typeof(CustomMapPatches).GetMethod(nameof(GetMissionsForDifficulties_Postfix),
            BindingFlags.NonPublic | BindingFlags.Static);

        GamePatch.Postfix(_harmony, "Menace.Strategy.Operations.OperationTemplate",
            "GetMissionsForDifficulties", poolPatchMethod);
    }

    /// <summary>
    /// Apply patches for tile override injection during map generation.
    /// </summary>
    private static void ApplyTileOverridePatches()
    {
        // Patch OnSecondPass (State 7) to apply tile-level overrides after generation
        var secondPassPostfixMethod = typeof(CustomMapPatches).GetMethod(nameof(OnSecondPass_Postfix),
            BindingFlags.NonPublic | BindingFlags.Static);

        // The map generator's OnSecondPass is called after initial layout is complete
        // This is the ideal hook point for applying tile overrides
        GamePatch.Postfix(_harmony, "Menace.Tactical.MapGeneration.MapGenerator",
            "OnSecondPass", secondPassPostfixMethod);

        // Also patch OnLayoutPass (State 3) for zone-aware generator parameter overrides
        var layoutPassPrefixMethod = typeof(CustomMapPatches).GetMethod(nameof(OnLayoutPass_Prefix),
            BindingFlags.NonPublic | BindingFlags.Static);

        GamePatch.Prefix(_harmony, "Menace.Tactical.MapGeneration.MapGenerator",
            "OnLayoutPass", layoutPassPrefixMethod);
    }

    // ==================== Patch Methods ====================

    /// <summary>
    /// Prefix patch for MissionTemplate.TryCreateMapLayout
    /// Injects custom seed and parameters before map generation.
    /// </summary>
    private static void TryCreateMapLayout_Prefix(object __instance, object mission)
    {
        try
        {
            var config = CustomMapRegistry.GetActiveOverride();
            if (config == null)
            {
                TileOverrideInjector.Clear();
                return;
            }

            SdkLogger.Msg($"[CustomMaps] Applying config: {config.Name}");

            // Initialize tile override injector with zones/tiles/paths
            TileOverrideInjector.Initialize(config);

            // Override seed if specified
            if (config.Seed.HasValue)
            {
                ApplySeedOverride(mission, config.Seed.Value);
            }

            // Override map size if specified
            if (config.MapSize.HasValue)
            {
                CurrentMapSize = config.MapSize.Value;
                ApplyMapSizeOverride(mission, config.MapSize.Value);
            }
            else
            {
                CurrentMapSize = 42; // Reset to default
            }
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("CustomMapPatches.TryCreateMapLayout_Prefix",
                "Failed to apply config", ex);
        }
    }

    /// <summary>
    /// Postfix patch for generator initialization.
    /// Applies per-generator parameter overrides.
    /// </summary>
    private static void InitGenerators_Postfix(object mission)
    {
        try
        {
            var config = CustomMapRegistry.GetActiveOverride();
            if (config == null)
                return;

            ApplyGeneratorOverrides(mission, config);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("CustomMapPatches.InitGenerators_Postfix",
                "Failed to apply generator overrides", ex);
        }
    }

    /// <summary>
    /// Prefix patch for Map.IsInBounds
    /// Uses dynamic map size instead of hardcoded 42.
    /// </summary>
    private static bool IsInBounds_Prefix(int x, int y, ref bool __result)
    {
        __result = x >= 0 && y >= 0 && x < CurrentMapSize && y < CurrentMapSize;
        return false; // Skip original method
    }

    /// <summary>
    /// Prefix patch for Map.ClampToBounds
    /// Uses dynamic map size instead of hardcoded 42.
    /// </summary>
    private static bool ClampToBounds_Prefix(ref UnityEngine.Vector2 pos)
    {
        pos.x = UnityEngine.Mathf.Clamp(pos.x, 0, CurrentMapSize);
        pos.y = UnityEngine.Mathf.Clamp(pos.y, 0, CurrentMapSize);
        return false; // Skip original method
    }

    /// <summary>
    /// Postfix patch for OperationTemplate.GetMissionsForDifficulties
    /// Adds custom maps to the mission pool.
    /// </summary>
    private static void GetMissionsForDifficulties_Postfix(ref object __result, int layer)
    {
        try
        {
            var customMaps = CustomMapRegistry.GetByLayer(layer);
            if (customMaps.Count == 0)
                return;

            // __result is List<MissionConfig> - we need to add our custom maps
            // This requires creating MissionConfig objects with our custom templates
            // Implementation depends on exact MissionConfig structure from Ghidra

            SdkLogger.Msg($"[CustomMaps] Injecting {customMaps.Count} maps into layer {layer} pool");

            // TODO: Create MissionConfig objects and add to __result
            // InjectMapsIntoPool(__result, customMaps);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("CustomMapPatches.GetMissionsForDifficulties_Postfix",
                "Failed to inject custom maps", ex);
        }
    }

    /// <summary>
    /// Prefix patch for MapGenerator.OnLayoutPass (State 3).
    /// Applies zone-aware generator parameter overrides before layout generation.
    /// </summary>
    private static void OnLayoutPass_Prefix(object __instance)
    {
        try
        {
            if (!TileOverrideInjector.HasActiveConfig)
                return;

            // Zone overrides are checked dynamically during generation via TileOverrideInjector
            // Generators can query ShouldGeneratorRunAt() and GetGeneratorConfigAt()
            SdkLogger.Msg("[CustomMaps] OnLayoutPass - zone overrides active");
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("CustomMapPatches.OnLayoutPass_Prefix", ex.Message);
        }
    }

    /// <summary>
    /// Postfix patch for MapGenerator.OnSecondPass (State 7).
    /// Applies tile-level overrides after generation is complete.
    /// </summary>
    private static void OnSecondPass_Postfix(object __instance)
    {
        try
        {
            if (!TileOverrideInjector.HasActiveConfig)
                return;

            SdkLogger.Msg("[CustomMaps] OnSecondPass - applying tile overrides");

            // Get the Map instance from TacticalManager singleton
            // From RE: TacticalManager singleton at TypeInfo + 0xB8, Map at +0x28
            IntPtr mapPointer = GetMapFromTacticalManager();

            if (mapPointer != IntPtr.Zero)
            {
                SdkLogger.Msg($"[CustomMaps] Got Map pointer: 0x{mapPointer:X}");
                TileOverrideInjector.ApplyTileOverrides(mapPointer);
            }
            else
            {
                // Fallback: try using the generator instance (may have map reference)
                SdkLogger.Warning("[CustomMaps] Could not get Map from TacticalManager, using generator instance");
                TileOverrideInjector.ApplyTileOverrides(__instance);
            }
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("CustomMapPatches.OnSecondPass_Postfix",
                "Failed to apply tile overrides", ex);
        }
    }

    /// <summary>
    /// Get the Map pointer from TacticalManager singleton.
    /// From RE: TacticalManager singleton at TypeInfo + 0xB8, Map reference at +0x28
    /// </summary>
    private static IntPtr GetMapFromTacticalManager()
    {
        try
        {
            // Get TacticalManager TypeInfo
            var tacticalManagerType = Type.GetType("Menace.Tactical.TacticalManager, Menace");
            if (tacticalManagerType == null)
            {
                // Try with assembly-qualified name search
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    tacticalManagerType = asm.GetType("Menace.Tactical.TacticalManager");
                    if (tacticalManagerType != null)
                        break;
                }
            }

            if (tacticalManagerType == null)
            {
                SdkLogger.Warning("[CustomMaps] TacticalManager type not found");
                return IntPtr.Zero;
            }

            // Get the singleton instance via Instance property
            var instanceProp = tacticalManagerType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);

            if (instanceProp != null)
            {
                var instance = instanceProp.GetValue(null);
                if (instance is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase il2cppObj)
                {
                    // Map is at offset 0x28
                    IntPtr mapPtr = Marshal.ReadIntPtr(il2cppObj.Pointer + 0x28);
                    return mapPtr;
                }
            }

            // Fallback: try getting from static field
            var currentMapField = tacticalManagerType.GetField("CurrentMap",
                BindingFlags.Public | BindingFlags.Static);

            if (currentMapField != null)
            {
                var map = currentMapField.GetValue(null);
                if (map is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase il2cppMap)
                {
                    return il2cppMap.Pointer;
                }
            }

            return IntPtr.Zero;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[CustomMaps] GetMapFromTacticalManager failed: {ex.Message}");
            return IntPtr.Zero;
        }
    }

    // ==================== Helper Methods ====================

    /// <summary>
    /// Apply seed override to mission object.
    /// From Ghidra analysis: Mission.Seed is at offset +0x24
    /// </summary>
    private static void ApplySeedOverride(object mission, int seed)
    {
        try
        {
            // Mission.Seed is at offset +0x24 (confirmed via Ghidra MISSION_CLASS.md)
            if (mission is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase il2cppObj)
            {
                Marshal.WriteInt32(il2cppObj.Pointer + 0x24, seed);
                SdkLogger.Msg($"[CustomMaps] Seed override: {seed}");
            }
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("CustomMapPatches.ApplySeedOverride", ex.Message);
        }
    }

    /// <summary>
    /// Apply map size override to mission object.
    /// From Ghidra analysis: Mission.Bounds is RectInt at offsets +0xD0 to +0xDC
    ///   - Bounds.X at +0xD0
    ///   - Bounds.Y at +0xD4
    ///   - Bounds.Width at +0xD8
    ///   - Bounds.Height at +0xDC
    /// </summary>
    private static void ApplyMapSizeOverride(object mission, int size)
    {
        try
        {
            if (mission is Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase il2cppObj)
            {
                // Set Bounds.Width and Bounds.Height
                Marshal.WriteInt32(il2cppObj.Pointer + 0xD8, size);  // Width
                Marshal.WriteInt32(il2cppObj.Pointer + 0xDC, size);  // Height
                SdkLogger.Msg($"[CustomMaps] Map size override: {size}x{size}");
            }
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("CustomMapPatches.ApplyMapSizeOverride", ex.Message);
        }
    }

    /// <summary>
    /// Apply generator configuration overrides.
    /// </summary>
    private static void ApplyGeneratorOverrides(object mission, CustomMapConfig config)
    {
        try
        {
            // Get generators array from mission
            // Based on documentation: mission.Generators is BaseMapGenerator[]

            var missionType = mission.GetType();
            var generatorsProp = missionType.GetProperty("Generators",
                BindingFlags.Public | BindingFlags.Instance);

            if (generatorsProp == null)
            {
                ModError.WarnInternal("CustomMapPatches.ApplyGeneratorOverrides",
                    "Generators property not found");
                return;
            }

            var generators = generatorsProp.GetValue(mission);
            if (generators == null)
                return;

            // Iterate generators and apply overrides
            var enumerable = generators as System.Collections.IEnumerable;
            if (enumerable == null)
                return;

            var toRemove = new List<object>();

            foreach (var generator in enumerable)
            {
                if (generator == null)
                    continue;

                var genType = generator.GetType();
                var genTypeName = genType.Name;

                // Check if generator should be disabled
                if (config.DisabledGenerators.Contains(genTypeName))
                {
                    toRemove.Add(generator);
                    SdkLogger.Msg($"[CustomMaps] Disabling generator: {genTypeName}");
                    continue;
                }

                // Check for generator config
                if (!config.Generators.TryGetValue(genTypeName, out var genConfig))
                    continue;

                // Check if explicitly disabled in config
                if (genConfig.Enabled == false)
                {
                    toRemove.Add(generator);
                    SdkLogger.Msg($"[CustomMaps] Disabling generator: {genTypeName}");
                    continue;
                }

                // Apply property overrides
                foreach (var (propName, value) in genConfig.Properties)
                {
                    ApplyPropertyOverride(generator, genType, propName, value);
                }

                // Apply prefab overrides
                foreach (var (fieldName, assetPaths) in genConfig.Prefabs)
                {
                    ApplyPrefabOverride(generator, genType, fieldName, assetPaths);
                }
            }

            // Remove disabled generators
            // Note: Actual removal depends on collection type (List vs Array)
            // May need to set to null or use different approach
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("CustomMapPatches.ApplyGeneratorOverrides",
                "Failed to apply overrides", ex);
        }
    }

    /// <summary>
    /// Apply a single property override to a generator.
    /// </summary>
    private static void ApplyPropertyOverride(object generator, Type genType, string propName, object value)
    {
        try
        {
            var prop = genType.GetProperty(propName,
                BindingFlags.Public | BindingFlags.Instance);

            if (prop == null)
            {
                // Try field instead
                var field = genType.GetField(propName,
                    BindingFlags.Public | BindingFlags.Instance);

                if (field != null)
                {
                    var converted = ConvertValue(value, field.FieldType);
                    field.SetValue(generator, converted);
                    SdkLogger.Msg($"[CustomMaps] Set {genType.Name}.{propName} = {value}");
                }
                return;
            }

            if (prop.CanWrite)
            {
                var converted = ConvertValue(value, prop.PropertyType);
                prop.SetValue(generator, converted);
                SdkLogger.Msg($"[CustomMaps] Set {genType.Name}.{propName} = {value}");
            }
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("CustomMapPatches.ApplyPropertyOverride",
                $"Failed to set {propName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Apply prefab array override to a generator.
    /// </summary>
    private static void ApplyPrefabOverride(object generator, Type genType, string fieldName, List<string> assetPaths)
    {
        try
        {
            // Resolve asset paths to GameObjects
            var prefabs = AssetResolver.ResolvePrefabArray(assetPaths);
            if (prefabs == null || prefabs.Length == 0)
            {
                ModError.WarnInternal("CustomMapPatches.ApplyPrefabOverride",
                    $"No prefabs resolved for {fieldName}");
                return;
            }

            var field = genType.GetField(fieldName,
                BindingFlags.Public | BindingFlags.Instance);

            if (field != null && field.FieldType == typeof(UnityEngine.GameObject[]))
            {
                field.SetValue(generator, prefabs);
                SdkLogger.Msg($"[CustomMaps] Set {genType.Name}.{fieldName} = [{prefabs.Length} prefabs]");
            }
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("CustomMapPatches.ApplyPrefabOverride",
                $"Failed to set {fieldName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Convert a value to the target type.
    /// </summary>
    private static object ConvertValue(object value, Type targetType)
    {
        if (value == null)
            return null;
        if (targetType.IsInstanceOfType(value))
            return value;

        // Handle JSON element types
        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Number when targetType == typeof(float) => jsonElement.GetSingle(),
                System.Text.Json.JsonValueKind.Number when targetType == typeof(int) => jsonElement.GetInt32(),
                System.Text.Json.JsonValueKind.Number when targetType == typeof(double) => jsonElement.GetDouble(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                System.Text.Json.JsonValueKind.String => jsonElement.GetString(),
                _ => Convert.ChangeType(jsonElement.ToString(), targetType)
            };
        }

        if (targetType == typeof(float))
            return Convert.ToSingle(value);
        if (targetType == typeof(int))
            return Convert.ToInt32(value);
        if (targetType == typeof(double))
            return Convert.ToDouble(value);
        if (targetType == typeof(bool))
            return Convert.ToBoolean(value);
        if (targetType == typeof(string))
            return value.ToString();

        return Convert.ChangeType(value, targetType);
    }
}
