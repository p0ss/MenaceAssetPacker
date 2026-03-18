using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Menace.SDK.CustomMaps;

/// <summary>
/// Central registry for custom map configurations.
/// Handles loading, registration, and lookup of custom maps.
/// </summary>
public static class CustomMapRegistry
{
    private static readonly Dictionary<string, CustomMapConfig> _maps = new();
    private static readonly Dictionary<int, List<CustomMapConfig>> _layerMaps = new();
    private static CustomMapConfig _activeOverride;
    private static readonly object _lock = new();

    /// <summary>
    /// Event fired when a new map is registered.
    /// </summary>
    public static event Action<CustomMapConfig> OnMapRegistered;

    /// <summary>
    /// Event fired when maps are loaded from a directory.
    /// </summary>
    public static event Action<int> OnMapsLoaded;

    /// <summary>
    /// Register a custom map configuration.
    /// </summary>
    public static bool Register(CustomMapConfig config)
    {
        if (config == null)
        {
            ModError.WarnInternal("CustomMapRegistry.Register", "Config is null");
            return false;
        }

        var errors = config.Validate();
        if (errors.Count > 0)
        {
            ModError.WarnInternal("CustomMapRegistry.Register",
                $"Invalid config '{config.Name}': {string.Join(", ", errors)}");
            return false;
        }

        lock (_lock)
        {
            _maps[config.Id] = config;

            // Index by layer for mission pool injection
            foreach (var layer in config.Layers)
            {
                var layerIndex = CustomMapConfig.LayerToIndex(layer);
                if (!_layerMaps.TryGetValue(layerIndex, out var list))
                {
                    list = new List<CustomMapConfig>();
                    _layerMaps[layerIndex] = list;
                }

                // Remove existing entry with same ID
                list.RemoveAll(m => m.Id == config.Id);
                list.Add(config);
            }
        }

        SdkLogger.Msg($"[CustomMaps] Registered: {config.Name} (id={config.Id})");
        OnMapRegistered?.Invoke(config);
        return true;
    }

    /// <summary>
    /// Unregister a custom map by ID.
    /// </summary>
    public static bool Unregister(string id)
    {
        lock (_lock)
        {
            if (!_maps.Remove(id))
                return false;

            foreach (var list in _layerMaps.Values)
                list.RemoveAll(m => m.Id == id);

            return true;
        }
    }

    /// <summary>
    /// Get a registered map by ID.
    /// </summary>
    public static CustomMapConfig Get(string id)
    {
        lock (_lock)
        {
            return _maps.TryGetValue(id, out var config) ? config : null;
        }
    }

    /// <summary>
    /// Get all registered maps.
    /// </summary>
    public static List<CustomMapConfig> GetAll()
    {
        lock (_lock)
        {
            return _maps.Values.ToList();
        }
    }

    /// <summary>
    /// Get all maps registered for a specific difficulty layer.
    /// </summary>
    public static List<CustomMapConfig> GetByLayer(int layerIndex)
    {
        lock (_lock)
        {
            return _layerMaps.TryGetValue(layerIndex, out var list)
                ? list.ToList()
                : new List<CustomMapConfig>();
        }
    }

    /// <summary>
    /// Get maps filtered by layer name.
    /// </summary>
    public static List<CustomMapConfig> GetByLayer(string layerName)
    {
        return GetByLayer(CustomMapConfig.LayerToIndex(layerName));
    }

    /// <summary>
    /// Load all .json map configs from a directory.
    /// </summary>
    public static int LoadFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            ModError.WarnInternal("CustomMapRegistry.LoadFromDirectory",
                $"Directory not found: {directoryPath}");
            return 0;
        }

        int count = 0;
        var files = Directory.GetFiles(directoryPath, "*.json", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            try
            {
                var config = LoadFromFile(file);
                if (config != null && Register(config))
                    count++;
            }
            catch (Exception ex)
            {
                ModError.WarnInternal("CustomMapRegistry.LoadFromDirectory",
                    $"Failed to load {file}: {ex.Message}");
            }
        }

        SdkLogger.Msg($"[CustomMaps] Loaded {count} maps from {directoryPath}");
        OnMapsLoaded?.Invoke(count);
        return count;
    }

    /// <summary>
    /// Load a single map config from a JSON file.
    /// </summary>
    public static CustomMapConfig LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            ModError.WarnInternal("CustomMapRegistry.LoadFromFile",
                $"File not found: {filePath}");
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            var config = JsonSerializer.Deserialize<CustomMapConfig>(json, options);

            // Use filename as ID if not specified
            if (string.IsNullOrEmpty(config.Id))
                config.Id = Path.GetFileNameWithoutExtension(filePath);

            return config;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("CustomMapRegistry.LoadFromFile",
                $"Failed to parse {filePath}", ex);
            return null;
        }
    }

    /// <summary>
    /// Save a map config to a JSON file.
    /// </summary>
    public static bool SaveToFile(CustomMapConfig config, string filePath)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(config, options);

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(filePath, json);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("CustomMapRegistry.SaveToFile",
                $"Failed to save to {filePath}", ex);
            return false;
        }
    }

    /// <summary>
    /// Set the active map override. This map's parameters will be applied
    /// to the next mission generation.
    /// </summary>
    public static void SetActiveOverride(CustomMapConfig config)
    {
        lock (_lock)
        {
            _activeOverride = config;
        }

        if (config != null)
            SdkLogger.Msg($"[CustomMaps] Active override set: {config.Name}");
        else
            SdkLogger.Msg("[CustomMaps] Active override cleared");
    }

    /// <summary>
    /// Set the active map override by ID.
    /// </summary>
    public static bool SetActiveOverride(string id)
    {
        var config = Get(id);
        if (config == null)
        {
            ModError.WarnInternal("CustomMapRegistry.SetActiveOverride",
                $"Map not found: {id}");
            return false;
        }

        SetActiveOverride(config);
        return true;
    }

    /// <summary>
    /// Clear the active map override.
    /// </summary>
    public static void ClearActiveOverride()
    {
        SetActiveOverride((CustomMapConfig)null);
    }

    /// <summary>
    /// Get the currently active map override, if any.
    /// </summary>
    public static CustomMapConfig GetActiveOverride()
    {
        lock (_lock)
        {
            return _activeOverride;
        }
    }

    /// <summary>
    /// Check if there's an active map override.
    /// </summary>
    public static bool HasActiveOverride()
    {
        lock (_lock)
        {
            return _activeOverride != null;
        }
    }

    /// <summary>
    /// Get the total count of registered maps.
    /// </summary>
    public static int Count
    {
        get
        {
            lock (_lock)
            {
                return _maps.Count;
            }
        }
    }

    /// <summary>
    /// Clear all registered maps.
    /// </summary>
    public static void Clear()
    {
        lock (_lock)
        {
            _maps.Clear();
            _layerMaps.Clear();
            _activeOverride = null;
        }

        SdkLogger.Msg("[CustomMaps] Registry cleared");
    }

    /// <summary>
    /// Register console commands for custom maps.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // listmaps - Show all registered custom maps
        DevConsole.RegisterCommand("custommaps", "", "List all registered custom maps", args =>
        {
            var maps = GetAll();
            if (maps.Count == 0)
                return "No custom maps registered";

            var lines = new List<string> { $"Custom Maps ({maps.Count}):" };
            foreach (var map in maps)
            {
                var layers = string.Join(",", map.Layers);
                var active = GetActiveOverride()?.Id == map.Id ? " [ACTIVE]" : "";
                lines.Add($"  {map.Id}: {map.Name} ({layers}){active}");
            }
            return string.Join("\n", lines);
        });

        // setmap <id> - Set active map override
        DevConsole.RegisterCommand("setmap", "<id>", "Set active custom map override", args =>
        {
            if (args.Length == 0)
                return "Usage: setmap <id>";

            return SetActiveOverride(args[0])
                ? $"Active map set to: {args[0]}"
                : $"Map not found: {args[0]}";
        });

        // clearmap - Clear active map override
        DevConsole.RegisterCommand("clearmap", "", "Clear active custom map override", args =>
        {
            ClearActiveOverride();
            return "Custom map override cleared";
        });

        // loadmaps <path> - Load maps from directory
        DevConsole.RegisterCommand("loadmaps", "<path>", "Load custom maps from directory", args =>
        {
            if (args.Length == 0)
                return "Usage: loadmaps <path>";

            var count = LoadFromDirectory(args[0]);
            return $"Loaded {count} custom maps";
        });

        // mapinfo <id> - Show detailed map info
        DevConsole.RegisterCommand("mapinfo", "<id>", "Show custom map details", args =>
        {
            if (args.Length == 0)
                return "Usage: mapinfo <id>";

            var map = Get(args[0]);
            if (map == null)
                return $"Map not found: {args[0]}";

            var lines = new List<string>
            {
                $"Name: {map.Name}",
                $"ID: {map.Id}",
                $"Author: {map.Author ?? "Unknown"}",
                $"Version: {map.Version}",
                $"Description: {map.Description ?? "N/A"}",
                $"Seed: {map.Seed?.ToString() ?? "Random"}",
                $"Map Size: {map.MapSize?.ToString() ?? "Default (42)"}",
                $"Layers: {string.Join(", ", map.Layers)}",
                $"Weight: {map.Weight}",
                $"Generators: {map.Generators.Count} overrides",
                $"Disabled: {string.Join(", ", map.DisabledGenerators)}"
            };

            if (map.Generators.Count > 0)
            {
                lines.Add("Generator Overrides:");
                foreach (var (name, config) in map.Generators)
                {
                    var enabled = config.Enabled == false ? " (DISABLED)" : "";
                    lines.Add($"  {name}{enabled}: {config.Properties.Count} properties");
                }
            }

            return string.Join("\n", lines);
        });

        // mapzone <x> <y> - Show zone at position
        DevConsole.RegisterCommand("mapzone", "<x> <y>", "Show zone info at tile position", args =>
        {
            if (args.Length < 2)
                return "Usage: mapzone <x> <y>";

            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
                return "Invalid coordinates";

            return TileOverrideInjector.GetZoneInfo(x, y);
        });

        // maptile <x> <y> - Show tile override at position
        DevConsole.RegisterCommand("maptile", "<x> <y>", "Show tile override info at position", args =>
        {
            if (args.Length < 2)
                return "Usage: maptile <x> <y>";

            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
                return "Invalid coordinates";

            return TileOverrideInjector.GetTileInfo(x, y);
        });

        // testmap - Create a test map with zones/tiles/paths
        DevConsole.RegisterCommand("testmap", "", "Create and activate a test map with zones, tiles, and paths", args =>
        {
            var testConfig = new CustomMapConfig
            {
                Id = "test_map",
                Name = "Test Map",
                Author = "SDK",
                Seed = 424242,
                MapSize = 50
            };

            // Add deployment/area zones (matches game's MissionAreaType)
            testConfig.Zones.Add(new MapZone
            {
                Id = "base_deploy",
                Name = "Base Deployment",
                Type = ZoneType.Base,  // Player deployment area
                X = 5,
                Y = 5,
                Width = 8,
                Height = 8,
                Priority = 1
            });

            testConfig.Zones.Add(new MapZone
            {
                Id = "north_border",
                Name = "North Border",
                Type = ZoneType.NorthMapBorder,
                X = 0,
                Y = 40,
                Width = 50,
                Height = 10,
                Priority = 1
            });

            testConfig.Zones.Add(new MapZone
            {
                Id = "objective_rect",
                Name = "Objective Area",
                Type = ZoneType.Rect,  // Generic rectangle for objectives
                X = 22,
                Y = 22,
                Width = 6,
                Height = 6,
                Priority = 2
            });

            // Add terrain painting
            testConfig.Tiles.Add(new TileOverride { X = 25, Y = 25, Terrain = "Water" });
            testConfig.Tiles.Add(new TileOverride { X = 26, Y = 25, Terrain = "Water" });
            testConfig.Tiles.Add(new TileOverride { X = 20, Y = 20, Terrain = "Trees" });
            testConfig.Tiles.Add(new TileOverride { X = 30, Y = 30, Terrain = "HighGround", Height = 3.0f });

            // Add a test path (hint for road generator)
            testConfig.Paths.Add(new MapPath
            {
                Id = "main_road",
                Type = PathType.Road,
                Width = 3,
                Waypoints = new List<PathWaypoint>
                {
                    new(0, 25),
                    new(50, 25)
                }
            });

            // Add a chunk placement
            testConfig.Chunks.Add(new ChunkPlacement
            {
                X = 15,
                Y = 15,
                ChunkTemplate = "Bunker",
                Rotation = 90
            });

            // Register and activate
            Register(testConfig);
            SetActiveOverride(testConfig);

            return $"Test map created and activated:\n  3 gameplay zones\n  4 terrain tiles\n  1 path\n  1 chunk placement";
        });
    }
}
