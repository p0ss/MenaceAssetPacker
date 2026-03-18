using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Menace.SDK.CustomMaps;

/// <summary>
/// Configuration for a custom map. JSON-serializable for sharing and loading.
///
/// Custom maps work by overriding procedural generation parameters:
/// - Seed: Deterministic random seed for reproducible layouts
/// - MapSize: Override the default 42x42 map size
/// - Generators: Per-generator property overrides (density, prefabs, etc.)
///
/// Example JSON:
/// {
///   "name": "Desert Outpost",
///   "author": "PlayerName",
///   "seed": 424242,
///   "mapSize": 60,
///   "generators": {
///     "ChunkGenerator": { "spawnDensity": 0.2 }
///   }
/// }
/// </summary>
public class CustomMapConfig
{
    /// <summary>
    /// Unique identifier for this map configuration.
    /// Used for registry lookups and mission pool registration.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Display name for the map.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Author/creator of the map configuration.
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; set; }

    /// <summary>
    /// Description of the map and its gameplay characteristics.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; }

    /// <summary>
    /// Version string for tracking updates.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Random seed for deterministic map generation.
    /// Same seed + same parameters = same map layout.
    /// If null, uses the game's default seed selection.
    /// </summary>
    [JsonPropertyName("seed")]
    public int? Seed { get; set; }

    /// <summary>
    /// Map size in tiles (NxN square).
    /// Default game size is 42. Supported range: 20-80.
    /// Larger maps may have performance implications.
    /// </summary>
    [JsonPropertyName("mapSize")]
    public int? MapSize { get; set; }

    /// <summary>
    /// Per-generator configuration overrides.
    /// Key is the generator class name (e.g., "ChunkGenerator", "CoverGenerator").
    /// </summary>
    [JsonPropertyName("generators")]
    public Dictionary<string, GeneratorConfig> Generators { get; set; } = new();

    /// <summary>
    /// List of generator class names to disable entirely.
    /// e.g., ["CoverGenerator"] to create maps with no procedural cover.
    /// </summary>
    [JsonPropertyName("disabledGenerators")]
    public List<string> DisabledGenerators { get; set; } = new();

    /// <summary>
    /// Mission pool configuration - which difficulty layers this map appears in.
    /// Valid values: "easy", "medium", "hard", "extreme"
    /// </summary>
    [JsonPropertyName("layers")]
    public List<string> Layers { get; set; } = new() { "medium" };

    /// <summary>
    /// Weight for random selection in mission pools.
    /// Higher weight = more likely to be selected.
    /// Default is 10 (same as vanilla missions).
    /// </summary>
    [JsonPropertyName("weight")]
    public int Weight { get; set; } = 10;

    /// <summary>
    /// Optional Lua condition expression for context-aware placement.
    /// e.g., "planet.biome == 'desert'" to only appear on desert planets.
    /// </summary>
    [JsonPropertyName("condition")]
    public string Condition { get; set; }

    /// <summary>
    /// Optional terrain/biome texture overrides.
    /// </summary>
    [JsonPropertyName("terrain")]
    public TerrainConfig Terrain { get; set; }

    /// <summary>
    /// Tags for categorization and filtering.
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Map zones with custom generator settings.
    /// Zones define rectangular regions with priority-based generator overrides.
    /// </summary>
    [JsonPropertyName("zones")]
    public List<MapZone> Zones { get; set; } = new();

    /// <summary>
    /// Per-tile overrides for precise map control.
    /// Sparse collection - only contains tiles that differ from procedural generation.
    /// </summary>
    [JsonPropertyName("tiles")]
    public List<TileOverride> Tiles { get; set; } = new();

    /// <summary>
    /// Paths connecting waypoints (roads, rivers, etc.).
    /// These are hints for the road generator, not exact specifications.
    /// </summary>
    [JsonPropertyName("paths")]
    public List<MapPath> Paths { get; set; } = new();

    /// <summary>
    /// Specific chunk placements on the map.
    /// Allows placing game chunks at exact positions with rotation.
    /// </summary>
    [JsonPropertyName("chunks")]
    public List<ChunkPlacement> Chunks { get; set; } = new();

    /// <summary>
    /// Validate the configuration for common errors.
    /// Returns list of validation errors, empty if valid.
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Id))
            errors.Add("Id is required");

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name is required");

        if (MapSize.HasValue)
        {
            if (MapSize.Value < 20)
                errors.Add("MapSize must be at least 20");
            if (MapSize.Value > 80)
                errors.Add("MapSize must be at most 80 (performance limit)");
        }

        if (Weight < 1)
            errors.Add("Weight must be at least 1");

        foreach (var layer in Layers)
        {
            if (!IsValidLayer(layer))
                errors.Add($"Invalid layer '{layer}'. Valid: easy, medium, hard, extreme");
        }

        return errors;
    }

    /// <summary>
    /// Check if this is a valid difficulty layer name.
    /// </summary>
    public static bool IsValidLayer(string layer)
    {
        return layer?.ToLowerInvariant() switch
        {
            "easy" => true,
            "medium" => true,
            "hard" => true,
            "extreme" => true,
            _ => false
        };
    }

    /// <summary>
    /// Convert layer name to game's layer index.
    /// </summary>
    public static int LayerToIndex(string layer)
    {
        return layer?.ToLowerInvariant() switch
        {
            "easy" => 0,
            "medium" => 1,
            "hard" => 2,
            "extreme" => 3,
            _ => 1 // Default to medium
        };
    }
}

/// <summary>
/// Configuration for terrain/biome visual overrides.
/// </summary>
public class TerrainConfig
{
    /// <summary>
    /// Ground texture asset reference.
    /// </summary>
    [JsonPropertyName("groundTexture")]
    public string GroundTexture { get; set; }

    /// <summary>
    /// Detail/secondary texture asset reference.
    /// </summary>
    [JsonPropertyName("detailTexture")]
    public string DetailTexture { get; set; }

    /// <summary>
    /// Terrain height range multiplier.
    /// </summary>
    [JsonPropertyName("heightScale")]
    public float? HeightScale { get; set; }

    /// <summary>
    /// Terrain roughness/noise multiplier.
    /// </summary>
    [JsonPropertyName("roughness")]
    public float? Roughness { get; set; }
}

/// <summary>
/// Zone types matching the game's MissionAreaType enum.
/// These define deployment/area zones on the map.
/// </summary>
public enum ZoneType
{
    /// <summary>Base deployment zone (default player spawn area).</summary>
    Base = 0,
    /// <summary>Chunk-based zone - positions relative to a chunk.</summary>
    Chunk = 1,
    /// <summary>South border of the map.</summary>
    SouthMapBorder = 2,
    /// <summary>East border of the map.</summary>
    EastMapBorder = 3,
    /// <summary>West border of the map.</summary>
    WestMapBorder = 4,
    /// <summary>North border of the map.</summary>
    NorthMapBorder = 5,
    /// <summary>Generic rectangle area.</summary>
    Rect = 6,
    /// <summary>Northeast corner of the map.</summary>
    NorthEastMapBorder = 7,
    /// <summary>Southeast corner of the map.</summary>
    SouthEastMapBorder = 8,
    /// <summary>Southwest corner of the map.</summary>
    SouthWestMapBorder = 9,
    /// <summary>Northwest corner of the map.</summary>
    NorthWestMapBorder = 10,
    /// <summary>Custom zone type for modding purposes.</summary>
    Custom = 100
}

/// <summary>
/// Rectangular zone for gameplay purposes (spawn points, objectives, etc.).
/// Zones can overlap - higher priority zones take precedence.
/// </summary>
public class MapZone
{
    /// <summary>
    /// Unique identifier for this zone.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Display name for the zone.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Type of zone (PlayerSpawn, EnemySpawn, Entry, Extraction, Objective, etc.).
    /// </summary>
    [JsonPropertyName("type")]
    public ZoneType Type { get; set; } = ZoneType.Custom;

    /// <summary>
    /// X coordinate of zone origin (top-left).
    /// </summary>
    [JsonPropertyName("x")]
    public int X { get; set; }

    /// <summary>
    /// Y coordinate of zone origin (top-left).
    /// </summary>
    [JsonPropertyName("y")]
    public int Y { get; set; }

    /// <summary>
    /// Zone width in tiles.
    /// </summary>
    [JsonPropertyName("width")]
    public int Width { get; set; }

    /// <summary>
    /// Zone height in tiles.
    /// </summary>
    [JsonPropertyName("height")]
    public int Height { get; set; }

    /// <summary>
    /// Zone priority for overlapping zones.
    /// Higher values take precedence.
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Per-generator configuration overrides within this zone.
    /// </summary>
    [JsonPropertyName("generators")]
    public Dictionary<string, GeneratorConfig> Generators { get; set; } = new();

    /// <summary>
    /// Generators to disable within this zone.
    /// </summary>
    [JsonPropertyName("disabledGenerators")]
    public List<string> DisabledGenerators { get; set; } = new();

    /// <summary>
    /// Check if the given tile coordinates are within this zone.
    /// </summary>
    public bool Contains(int x, int y)
    {
        return x >= X && x < X + Width && y >= Y && y < Y + Height;
    }
}

/// <summary>
/// A chunk placement on the map - placing a specific game chunk at a position with rotation.
/// </summary>
public class ChunkPlacement
{
    /// <summary>
    /// X coordinate of chunk placement.
    /// </summary>
    [JsonPropertyName("x")]
    public int X { get; set; }

    /// <summary>
    /// Y coordinate of chunk placement.
    /// </summary>
    [JsonPropertyName("y")]
    public int Y { get; set; }

    /// <summary>
    /// Name of the chunk template to place.
    /// </summary>
    [JsonPropertyName("template")]
    public string ChunkTemplate { get; set; }

    /// <summary>
    /// Rotation in degrees (0, 90, 180, 270).
    /// </summary>
    [JsonPropertyName("rotation")]
    public int Rotation { get; set; } = 0;
}

/// <summary>
/// Terrain types for painting.
/// </summary>
public enum TerrainType
{
    Default,
    Trees,
    Water,
    HighGround,
    Road,
    Sand,
    Concrete
}

/// <summary>
/// Override for a specific tile position.
/// Used for terrain painting (trees, water, high ground, etc.).
/// </summary>
public class TileOverride
{
    /// <summary>
    /// X coordinate of the tile.
    /// </summary>
    [JsonPropertyName("x")]
    public int X { get; set; }

    /// <summary>
    /// Y coordinate of the tile.
    /// </summary>
    [JsonPropertyName("y")]
    public int Y { get; set; }

    /// <summary>
    /// Terrain type (Trees, Water, HighGround, Road, Sand, Concrete).
    /// </summary>
    [JsonPropertyName("terrain")]
    public string Terrain { get; set; }

    /// <summary>
    /// If set, override the height of the tile.
    /// </summary>
    [JsonPropertyName("height")]
    public float? Height { get; set; }
}

/// <summary>
/// Path type enumeration.
/// </summary>
public enum PathType
{
    Road,
    River,
    Trail,
    Trench
}

/// <summary>
/// A path connecting waypoints.
/// </summary>
public class MapPath
{
    /// <summary>
    /// Unique identifier for this path.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Type of path (affects rendering and gameplay).
    /// </summary>
    [JsonPropertyName("type")]
    public PathType Type { get; set; } = PathType.Road;

    /// <summary>
    /// Width of the path in tiles.
    /// </summary>
    [JsonPropertyName("width")]
    public int Width { get; set; } = 3;

    /// <summary>
    /// Ordered list of waypoints defining the path.
    /// </summary>
    [JsonPropertyName("waypoints")]
    public List<PathWaypoint> Waypoints { get; set; } = new();
}

/// <summary>
/// A waypoint along a path.
/// </summary>
public class PathWaypoint
{
    /// <summary>
    /// X coordinate of the waypoint.
    /// </summary>
    [JsonPropertyName("x")]
    public int X { get; set; }

    /// <summary>
    /// Y coordinate of the waypoint.
    /// </summary>
    [JsonPropertyName("y")]
    public int Y { get; set; }

    public PathWaypoint() { }

    public PathWaypoint(int x, int y)
    {
        X = x;
        Y = y;
    }
}
