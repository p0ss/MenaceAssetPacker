using System;
using System.Collections.Generic;

namespace Menace.SDK.CustomMaps;

/// <summary>
/// Fluent builder for creating CustomMapConfig objects.
///
/// Example usage:
/// var config = new MapBuilder("desert_combat")
///     .WithName("Desert Combat")
///     .WithAuthor("PlayerName")
///     .WithSeed(424242)
///     .WithMapSize(60)
///     .WithLayers("medium", "hard")
///     .ConfigureGenerator("ChunkGenerator", g => g
///         .WithProperty("spawnDensity", 0.2f)
///         .WithProperty("minDistance", 10))
///     .DisableGenerator("CoverGenerator")
///     .Build();
/// </summary>
public class MapBuilder
{
    private readonly CustomMapConfig _config;

    /// <summary>
    /// Create a new map builder with the given ID.
    /// </summary>
    public MapBuilder(string id)
    {
        _config = new CustomMapConfig
        {
            Id = id,
            Name = id // Default name to ID
        };
    }

    /// <summary>
    /// Set the display name.
    /// </summary>
    public MapBuilder WithName(string name)
    {
        _config.Name = name;
        return this;
    }

    /// <summary>
    /// Set the author.
    /// </summary>
    public MapBuilder WithAuthor(string author)
    {
        _config.Author = author;
        return this;
    }

    /// <summary>
    /// Set the description.
    /// </summary>
    public MapBuilder WithDescription(string description)
    {
        _config.Description = description;
        return this;
    }

    /// <summary>
    /// Set the version.
    /// </summary>
    public MapBuilder WithVersion(string version)
    {
        _config.Version = version;
        return this;
    }

    /// <summary>
    /// Set a specific seed for deterministic generation.
    /// </summary>
    public MapBuilder WithSeed(int seed)
    {
        _config.Seed = seed;
        return this;
    }

    /// <summary>
    /// Use a random seed (clears any set seed).
    /// </summary>
    public MapBuilder WithRandomSeed()
    {
        _config.Seed = null;
        return this;
    }

    /// <summary>
    /// Set the map size (default is 42).
    /// </summary>
    public MapBuilder WithMapSize(int size)
    {
        _config.MapSize = size;
        return this;
    }

    /// <summary>
    /// Set the difficulty layers this map appears in.
    /// </summary>
    public MapBuilder WithLayers(params string[] layers)
    {
        _config.Layers = new List<string>(layers);
        return this;
    }

    /// <summary>
    /// Set the selection weight in mission pools.
    /// </summary>
    public MapBuilder WithWeight(int weight)
    {
        _config.Weight = weight;
        return this;
    }

    /// <summary>
    /// Set the placement condition expression.
    /// </summary>
    public MapBuilder WithCondition(string condition)
    {
        _config.Condition = condition;
        return this;
    }

    /// <summary>
    /// Add tags.
    /// </summary>
    public MapBuilder WithTags(params string[] tags)
    {
        _config.Tags.AddRange(tags);
        return this;
    }

    /// <summary>
    /// Configure a specific generator.
    /// </summary>
    public MapBuilder ConfigureGenerator(string generatorName, Action<GeneratorConfigBuilder> configure)
    {
        var builder = new GeneratorConfigBuilder();
        configure(builder);

        _config.Generators[generatorName] = builder.Build();
        return this;
    }

    /// <summary>
    /// Disable a generator entirely.
    /// </summary>
    public MapBuilder DisableGenerator(string generatorName)
    {
        _config.DisabledGenerators.Add(generatorName);
        return this;
    }

    /// <summary>
    /// Configure terrain settings.
    /// </summary>
    public MapBuilder WithTerrain(Action<TerrainConfigBuilder> configure)
    {
        var builder = new TerrainConfigBuilder();
        configure(builder);
        _config.Terrain = builder.Build();
        return this;
    }

    /// <summary>
    /// Build the final configuration.
    /// </summary>
    public CustomMapConfig Build()
    {
        return _config;
    }

    /// <summary>
    /// Build and register the configuration.
    /// </summary>
    public CustomMapConfig BuildAndRegister()
    {
        var config = Build();
        CustomMapRegistry.Register(config);
        return config;
    }

    // ==================== Preset Builders ====================

    /// <summary>
    /// Create a sparse/open map preset.
    /// </summary>
    public static MapBuilder Sparse(string id)
    {
        return new MapBuilder(id)
            .WithDescription("Sparse open map with minimal cover")
            .ConfigureGenerator("ChunkGenerator", g => g
                .WithProperty("spawnDensity", 0.15f)
                .WithProperty("minDistance", 12))
            .ConfigureGenerator("EnvironmentFeatureGenerator", g => g
                .WithProperty("density", 0.1f))
            .ConfigureGenerator("CoverGenerator", g => g
                .WithProperty("spawnChance", 0.2f))
            .WithTags("sparse", "open");
    }

    /// <summary>
    /// Create a dense/urban map preset.
    /// </summary>
    public static MapBuilder Dense(string id)
    {
        return new MapBuilder(id)
            .WithDescription("Dense urban combat with heavy cover")
            .ConfigureGenerator("ChunkGenerator", g => g
                .WithProperty("spawnDensity", 0.7f)
                .WithProperty("minDistance", 3))
            .ConfigureGenerator("EnvironmentFeatureGenerator", g => g
                .WithProperty("density", 0.05f))
            .ConfigureGenerator("CoverGenerator", g => g
                .WithProperty("spawnChance", 0.8f))
            .WithTags("dense", "urban");
    }

    /// <summary>
    /// Create a balanced map preset.
    /// </summary>
    public static MapBuilder Balanced(string id)
    {
        return new MapBuilder(id)
            .WithDescription("Balanced map with moderate cover and structures")
            .WithTags("balanced");
    }

    /// <summary>
    /// Create a large map preset.
    /// </summary>
    public static MapBuilder Large(string id, int size = 60)
    {
        return new MapBuilder(id)
            .WithMapSize(size)
            .WithDescription($"Large {size}x{size} map")
            .WithTags("large");
    }

    /// <summary>
    /// Create a no-cover arena preset.
    /// </summary>
    public static MapBuilder Arena(string id)
    {
        return new MapBuilder(id)
            .WithDescription("Open arena with no procedural cover")
            .DisableGenerator("CoverGenerator")
            .ConfigureGenerator("ChunkGenerator", g => g
                .WithProperty("spawnDensity", 0.05f))
            .WithTags("arena", "no-cover");
    }
}

/// <summary>
/// Fluent builder for generator configuration.
/// </summary>
public class GeneratorConfigBuilder
{
    private readonly GeneratorConfig _config = new();

    /// <summary>
    /// Set whether generator is enabled.
    /// </summary>
    public GeneratorConfigBuilder Enabled(bool enabled)
    {
        _config.Enabled = enabled;
        return this;
    }

    /// <summary>
    /// Disable the generator.
    /// </summary>
    public GeneratorConfigBuilder Disable()
    {
        _config.Enabled = false;
        return this;
    }

    /// <summary>
    /// Set a property value.
    /// </summary>
    public GeneratorConfigBuilder WithProperty(string name, object value)
    {
        _config.Properties[name] = value;
        return this;
    }

    /// <summary>
    /// Set prefab array.
    /// </summary>
    public GeneratorConfigBuilder WithPrefabs(string fieldName, params string[] assetPaths)
    {
        _config.Prefabs[fieldName] = new List<string>(assetPaths);
        return this;
    }

    /// <summary>
    /// Build the generator config.
    /// </summary>
    public GeneratorConfig Build()
    {
        return _config;
    }
}

/// <summary>
/// Fluent builder for terrain configuration.
/// </summary>
public class TerrainConfigBuilder
{
    private readonly TerrainConfig _config = new();

    /// <summary>
    /// Set ground texture.
    /// </summary>
    public TerrainConfigBuilder WithGroundTexture(string texture)
    {
        _config.GroundTexture = texture;
        return this;
    }

    /// <summary>
    /// Set detail texture.
    /// </summary>
    public TerrainConfigBuilder WithDetailTexture(string texture)
    {
        _config.DetailTexture = texture;
        return this;
    }

    /// <summary>
    /// Set height scale.
    /// </summary>
    public TerrainConfigBuilder WithHeightScale(float scale)
    {
        _config.HeightScale = scale;
        return this;
    }

    /// <summary>
    /// Set roughness.
    /// </summary>
    public TerrainConfigBuilder WithRoughness(float roughness)
    {
        _config.Roughness = roughness;
        return this;
    }

    /// <summary>
    /// Build the terrain config.
    /// </summary>
    public TerrainConfig Build()
    {
        return _config;
    }
}
