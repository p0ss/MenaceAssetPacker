using System;
using HarmonyLib;

namespace Menace.SDK.CustomMaps;

/// <summary>
/// Main entry point for the Custom Maps SDK.
/// Provides initialization and high-level API for custom map creation.
///
/// Usage:
/// 1. Call CustomMaps.Initialize(harmony) during mod startup
/// 2. Use CustomMaps.Create() to build map configurations
/// 3. Load maps from JSON via CustomMaps.LoadMapsFromDirectory()
/// </summary>
public static class CustomMaps
{
    private static bool _initialized;

    /// <summary>
    /// Initialize the custom maps system.
    /// Call this during mod startup with your Harmony instance.
    /// </summary>
    public static bool Initialize(HarmonyLib.Harmony harmony)
    {
        if (_initialized)
            return true;

        try
        {
            // Apply patches
            if (!CustomMapPatches.Initialize(harmony))
            {
                ModError.ReportInternal("CustomMaps.Initialize", "Failed to apply patches");
                return false;
            }

            // Register console commands
            CustomMapRegistry.RegisterConsoleCommands();
            AssetResolver.RegisterConsoleCommands();
            TemplateCatalog.RegisterConsoleCommands();
            StructureSpawner.RegisterConsoleCommands();
            ChunkBrowser.RegisterConsoleCommands();

            _initialized = true;
            SdkLogger.Msg("[CustomMaps] SDK initialized");
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("CustomMaps.Initialize", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if custom maps system is initialized.
    /// </summary>
    public static bool IsInitialized => _initialized;

    /// <summary>
    /// Create a new map configuration builder.
    /// </summary>
    public static MapBuilder Create(string id)
    {
        return new MapBuilder(id);
    }

    /// <summary>
    /// Create a sparse/open map preset.
    /// </summary>
    public static MapBuilder CreateSparse(string id)
    {
        return MapBuilder.Sparse(id);
    }

    /// <summary>
    /// Create a dense/urban map preset.
    /// </summary>
    public static MapBuilder CreateDense(string id)
    {
        return MapBuilder.Dense(id);
    }

    /// <summary>
    /// Create a large map preset.
    /// </summary>
    public static MapBuilder CreateLarge(string id, int size = 60)
    {
        return MapBuilder.Large(id, size);
    }

    /// <summary>
    /// Create an arena (no cover) map preset.
    /// </summary>
    public static MapBuilder CreateArena(string id)
    {
        return MapBuilder.Arena(id);
    }

    /// <summary>
    /// Register a map configuration.
    /// </summary>
    public static bool Register(CustomMapConfig config)
    {
        return CustomMapRegistry.Register(config);
    }

    /// <summary>
    /// Get a registered map by ID.
    /// </summary>
    public static CustomMapConfig Get(string id)
    {
        return CustomMapRegistry.Get(id);
    }

    /// <summary>
    /// Load maps from a directory.
    /// </summary>
    public static int LoadMapsFromDirectory(string path)
    {
        return CustomMapRegistry.LoadFromDirectory(path);
    }

    /// <summary>
    /// Set a map as the active override (will be used for next mission).
    /// </summary>
    public static void SetActive(string mapId)
    {
        CustomMapRegistry.SetActiveOverride(mapId);
    }

    /// <summary>
    /// Set a map config as the active override.
    /// </summary>
    public static void SetActive(CustomMapConfig config)
    {
        CustomMapRegistry.SetActiveOverride(config);
    }

    /// <summary>
    /// Clear the active map override.
    /// </summary>
    public static void ClearActive()
    {
        CustomMapRegistry.ClearActiveOverride();
    }

    /// <summary>
    /// Get the currently active map override.
    /// </summary>
    public static CustomMapConfig GetActive()
    {
        return CustomMapRegistry.GetActiveOverride();
    }

    /// <summary>
    /// Quick method to create and activate a map with specific seed.
    /// </summary>
    public static void PlayWithSeed(int seed)
    {
        var config = Create($"seed_{seed}")
            .WithName($"Seed {seed}")
            .WithSeed(seed)
            .Build();

        SetActive(config);
    }

    /// <summary>
    /// Quick method to play with a specific map size.
    /// </summary>
    public static void PlayWithSize(int size)
    {
        var config = Create($"size_{size}")
            .WithName($"{size}x{size} Map")
            .WithMapSize(size)
            .Build();

        SetActive(config);
    }

    /// <summary>
    /// Quick method to play with specific seed and size.
    /// </summary>
    public static void PlayWith(int seed, int size)
    {
        var config = Create($"custom_{seed}_{size}")
            .WithName($"Custom Map")
            .WithSeed(seed)
            .WithMapSize(size)
            .Build();

        SetActive(config);
    }
}
