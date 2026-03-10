using System;
using System.Collections.Generic;
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
