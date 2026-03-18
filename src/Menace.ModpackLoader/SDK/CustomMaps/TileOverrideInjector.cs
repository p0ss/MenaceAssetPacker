using System;
using System.Collections.Generic;
using System.Linq;

namespace Menace.SDK.CustomMaps;

/// <summary>
/// Handles tile-level overrides for custom maps.
/// Provides O(1) lookup for tile overrides and zone containment checks.
/// </summary>
public static class TileOverrideInjector
{
    // Indexed tile overrides for O(1) lookup
    private static Dictionary<(int x, int y), TileOverride> _tileIndex = new();

    // Cached zones sorted by priority (descending)
    private static List<MapZone> _sortedZones = new();

    // Cached chunk placements
    private static List<ChunkPlacement> _chunkPlacements = new();

    // Currently active config
    private static CustomMapConfig _activeConfig;

    /// <summary>
    /// Initialize the injector with a custom map configuration.
    /// Call this before map generation starts.
    /// </summary>
    public static void Initialize(CustomMapConfig config)
    {
        _activeConfig = config;
        _tileIndex.Clear();
        _sortedZones.Clear();
        _chunkPlacements.Clear();

        if (config == null)
            return;

        // Index tile overrides by position
        foreach (var tile in config.Tiles)
        {
            _tileIndex[(tile.X, tile.Y)] = tile;
        }

        // Sort zones by priority (highest first)
        _sortedZones = config.Zones
            .OrderByDescending(z => z.Priority)
            .ToList();

        // Store chunk placements
        _chunkPlacements = config.Chunks?.ToList() ?? new List<ChunkPlacement>();

        SdkLogger.Msg($"[TileOverrideInjector] Initialized: {config.Tiles.Count} tiles, {config.Zones.Count} zones, " +
            $"{config.Paths.Count} paths, {_chunkPlacements.Count} chunks");
    }

    /// <summary>
    /// Clear the injector state.
    /// </summary>
    public static void Clear()
    {
        _activeConfig = null;
        _tileIndex.Clear();
        _sortedZones.Clear();
        _chunkPlacements.Clear();
    }

    /// <summary>
    /// Check if there's an active configuration.
    /// </summary>
    public static bool HasActiveConfig => _activeConfig != null;

    /// <summary>
    /// Get the tile override at the specified position, if any.
    /// O(1) lookup.
    /// </summary>
    public static TileOverride GetTileAt(int x, int y)
    {
        return _tileIndex.TryGetValue((x, y), out var tile) ? tile : null;
    }

    /// <summary>
    /// Check if there's a tile override at the specified position.
    /// </summary>
    public static bool HasTileAt(int x, int y)
    {
        return _tileIndex.ContainsKey((x, y));
    }

    /// <summary>
    /// Get the highest priority zone containing the specified position.
    /// Returns null if no zone contains the position.
    /// </summary>
    public static MapZone GetZoneAt(int x, int y)
    {
        // Zones are pre-sorted by priority (descending)
        foreach (var zone in _sortedZones)
        {
            if (zone.Contains(x, y))
                return zone;
        }
        return null;
    }

    /// <summary>
    /// Get all zones containing the specified position.
    /// </summary>
    public static IEnumerable<MapZone> GetAllZonesAt(int x, int y)
    {
        return _sortedZones.Where(z => z.Contains(x, y));
    }

    /// <summary>
    /// Check if a generator should run at the specified position.
    /// Returns false if the generator is disabled in the active zone.
    /// </summary>
    public static bool ShouldGeneratorRunAt(string generatorName, int x, int y)
    {
        var zone = GetZoneAt(x, y);
        if (zone == null)
            return true;

        // Check if explicitly disabled
        if (zone.DisabledGenerators.Contains(generatorName))
            return false;

        // Check if generator config has enabled=false
        if (zone.Generators.TryGetValue(generatorName, out var config))
        {
            if (config.Enabled == false)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Get generator config for a zone at the specified position.
    /// Returns null if no zone or no config for that generator.
    /// </summary>
    public static GeneratorConfig GetGeneratorConfigAt(string generatorName, int x, int y)
    {
        var zone = GetZoneAt(x, y);
        if (zone == null)
            return null;

        return zone.Generators.TryGetValue(generatorName, out var config) ? config : null;
    }

    /// <summary>
    /// Apply tile overrides to the map after generation.
    /// Called during post-processing phase.
    /// </summary>
    /// <param name="mapInstance">The Map object pointer or wrapper</param>
    public static void ApplyTileOverrides(object mapInstance)
    {
        if (_activeConfig == null || mapInstance == null)
            return;

        // Initialize TileManipulation with the map instance
        int mapSize = _activeConfig.MapSize.GetValueOrDefault(42);

        if (mapInstance is IntPtr ptr && ptr != IntPtr.Zero)
        {
            TileManipulation.SetMapInstance(ptr, mapSize);
        }
        else if (mapInstance is GameObj gameObj && !gameObj.IsNull)
        {
            TileManipulation.SetMapInstance(gameObj.Pointer, mapSize);
        }
        else
        {
            TileManipulation.SetMapInstance(mapInstance, mapSize);
        }

        // Check if map was initialized successfully
        if (!TileManipulation.IsMapInitialized)
        {
            SdkLogger.Warning("[TileOverrideInjector] Map not initialized - tile overrides cannot be applied to tile memory");
        }

        var appliedCount = 0;
        var failedCount = 0;

        // Apply terrain/tile overrides
        foreach (var tile in _activeConfig.Tiles)
        {
            try
            {
                ApplySingleTileOverride(mapInstance, tile);
                appliedCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                ModError.WarnInternal("TileOverrideInjector.ApplyTileOverrides",
                    $"Failed to apply tile at ({tile.X}, {tile.Y}): {ex.Message}");
            }
        }

        // Apply paths (hints for road generator)
        var pathCount = 0;
        foreach (var path in _activeConfig.Paths)
        {
            try
            {
                PathGenerator.ApplyPath(mapInstance, path);
                pathCount++;
            }
            catch (Exception ex)
            {
                ModError.WarnInternal("TileOverrideInjector.ApplyTileOverrides",
                    $"Failed to apply path '{path.Id}': {ex.Message}");
            }
        }

        // Apply chunk placements
        var chunkCount = 0;
        foreach (var chunk in _chunkPlacements)
        {
            try
            {
                ApplyChunkPlacement(mapInstance, chunk);
                chunkCount++;
            }
            catch (Exception ex)
            {
                ModError.WarnInternal("TileOverrideInjector.ApplyTileOverrides",
                    $"Failed to apply chunk '{chunk.ChunkTemplate}' at ({chunk.X}, {chunk.Y}): {ex.Message}");
            }
        }

        SdkLogger.Msg($"[TileOverrideInjector] Applied {appliedCount} tile overrides, {pathCount} paths, {chunkCount} chunks" +
            (failedCount > 0 ? $" ({failedCount} failed)" : ""));
    }

    /// <summary>
    /// Map terrain type to game surface type.
    /// </summary>
    private static string TerrainToSurface(string terrain)
    {
        return terrain switch
        {
            "Trees" => "Grass",      // Trees are on grass terrain
            "Water" => "Water",
            "HighGround" => "Rock",  // High ground is rocky
            "Road" => "Road",
            "Sand" => "Sand",
            "Concrete" => "Concrete",
            _ => null
        };
    }

    /// <summary>
    /// Apply a single tile override.
    /// </summary>
    private static void ApplySingleTileOverride(object mapInstance, TileOverride tile)
    {
        // Get tile reference using TileManipulation API
        var baseTile = TileManipulation.GetBaseTile(tile.X, tile.Y);
        bool hasTileAccess = !baseTile.IsNull;

        // Apply tile properties only if we have access to the tile memory
        if (hasTileAccess)
        {
            // Apply height
            if (tile.Height.HasValue)
            {
                TileManipulation.SetHeight(tile.X, tile.Y, tile.Height.Value);
            }

            // Apply terrain type as surface
            if (!string.IsNullOrEmpty(tile.Terrain))
            {
                var surfaceType = TerrainToSurface(tile.Terrain);
                if (surfaceType != null)
                {
                    TileManipulation.SetSurface(tile.X, tile.Y, surfaceType);
                }

                // For special terrain types, apply additional effects
                switch (tile.Terrain)
                {
                    case "Trees":
                        // Could spawn tree prop at this location
                        // For now just set the surface type
                        break;

                    case "Water":
                        // Water blocks movement
                        TileManipulation.SetBlocked(tile.X, tile.Y, true);
                        break;

                    case "HighGround":
                        // Increase height for high ground
                        var currentHeight = tile.Height ?? 0;
                        TileManipulation.SetHeight(tile.X, tile.Y, currentHeight + 2.0f);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Apply a chunk placement.
    /// </summary>
    private static void ApplyChunkPlacement(object mapInstance, ChunkPlacement chunk)
    {
        // Use StructureSpawner to spawn the chunk template
        // The chunk template name should match an EntityTemplate for a building/structure
        var result = StructureSpawner.SpawnEntity(
            chunk.ChunkTemplate,
            chunk.X,
            chunk.Y,
            0, // Neutral faction
            chunk.Rotation
        );

        if (result.Success)
        {
            SdkLogger.Msg($"[TileOverrideInjector] Spawned chunk '{chunk.ChunkTemplate}' at ({chunk.X}, {chunk.Y}) R{chunk.Rotation}°");
        }
        else
        {
            ModError.WarnInternal("TileOverrideInjector.ApplyChunkPlacement",
                $"Failed to spawn chunk '{chunk.ChunkTemplate}': {result.Error}");
        }
    }

    /// <summary>
    /// Get info about a tile for debugging.
    /// </summary>
    public static string GetTileInfo(int x, int y)
    {
        var lines = new List<string>
        {
            $"Tile ({x}, {y}):"
        };

        var tile = GetTileAt(x, y);
        if (tile != null)
        {
            lines.Add($"  Override: yes");
            if (!string.IsNullOrEmpty(tile.Terrain))
                lines.Add($"  Terrain: {tile.Terrain}");
            if (tile.Height.HasValue)
                lines.Add($"  Height: {tile.Height.Value}");
        }
        else
        {
            lines.Add($"  Override: no");
        }

        var zone = GetZoneAt(x, y);
        if (zone != null)
        {
            lines.Add($"  Zone: {zone.Name} (id={zone.Id}, type={zone.Type}, priority={zone.Priority})");
        }
        else
        {
            lines.Add($"  Zone: none");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Get info about a zone for debugging.
    /// </summary>
    public static string GetZoneInfo(int x, int y)
    {
        var zone = GetZoneAt(x, y);
        if (zone == null)
            return $"No zone at ({x}, {y})";

        var lines = new List<string>
        {
            $"Zone: {zone.Name} (id={zone.Id})",
            $"  Type: {zone.Type}",
            $"  Bounds: ({zone.X}, {zone.Y}) to ({zone.X + zone.Width - 1}, {zone.Y + zone.Height - 1})",
            $"  Size: {zone.Width}x{zone.Height}",
            $"  Priority: {zone.Priority}",
            $"  Disabled Generators: {(zone.DisabledGenerators.Count > 0 ? string.Join(", ", zone.DisabledGenerators) : "none")}"
        };

        if (zone.Generators.Count > 0)
        {
            lines.Add($"  Generator Overrides:");
            foreach (var (name, config) in zone.Generators)
            {
                lines.Add($"    {name}: {(config.Enabled == false ? "disabled" : "enabled")}");
            }
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Register console commands for debugging.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("maptile", "<x> <y>",
            "Show tile override info at position", args =>
        {
            if (args.Length < 2)
                return "Usage: maptile <x> <y>";

            if (!int.TryParse(args[0], out var x) || !int.TryParse(args[1], out var y))
                return "Invalid coordinates";

            return GetTileInfo(x, y);
        });

        DevConsole.RegisterCommand("mapzone", "<x> <y>",
            "Show zone info at position", args =>
        {
            if (args.Length < 2)
                return "Usage: mapzone <x> <y>";

            if (!int.TryParse(args[0], out var x) || !int.TryParse(args[1], out var y))
                return "Invalid coordinates";

            return GetZoneInfo(x, y);
        });

        DevConsole.RegisterCommand("mapstats", "",
            "Show current map override statistics", _ =>
        {
            if (_activeConfig == null)
                return "No active custom map config";

            var lines = new List<string>
            {
                $"Custom Map: {_activeConfig.Name} (id={_activeConfig.Id})",
                $"  Size: {_activeConfig.MapSize ?? 42}",
                $"  Seed: {_activeConfig.Seed?.ToString() ?? "default"}",
                $"  Tile Overrides: {_activeConfig.Tiles.Count}",
                $"  Zones: {_activeConfig.Zones.Count}",
                $"  Paths: {_activeConfig.Paths.Count}",
                $"  Chunks: {_activeConfig.Chunks?.Count ?? 0}"
            };

            if (_sortedZones.Count > 0)
            {
                lines.Add("  Zone Types:");
                var typeCounts = _sortedZones.GroupBy(z => z.Type).ToDictionary(g => g.Key, g => g.Count());
                foreach (var (type, count) in typeCounts)
                {
                    lines.Add($"    {type}: {count}");
                }
            }

            return string.Join("\n", lines);
        });
    }
}
