using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Menace.SDK.CustomMaps;

/// <summary>
/// Configuration overrides for a specific map generator.
///
/// Generators are the procedural systems that populate the map:
/// - ChunkGenerator: Buildings, structures
/// - EnvironmentFeatureGenerator: Rocks, vegetation, terrain features
/// - EnvironmentPropGenerator: Props, debris, decorations
/// - CoverGenerator: Tactical cover objects
///
/// Properties are applied via reflection at runtime.
/// </summary>
public class GeneratorConfig
{
    /// <summary>
    /// Whether this generator is enabled.
    /// Set to false to completely disable a generator type.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; set; }

    /// <summary>
    /// Property overrides for this generator.
    /// Keys are property names, values are the new values.
    ///
    /// Common properties (discovered via Ghidra - actual names may vary):
    ///
    /// ChunkGenerator:
    /// - spawnDensity (float, 0.0-1.0): Building spawn probability
    /// - minDistance (int): Minimum spacing between buildings
    /// - allowRotation (bool): Random building rotation
    ///
    /// EnvironmentFeatureGenerator:
    /// - density (float, 0.0-1.0): Feature spawn density
    /// - clusterSize (int): Feature grouping amount
    ///
    /// CoverGenerator:
    /// - spawnChance (float, 0.0-1.0): Cover object spawn probability
    ///
    /// Actual property names will be documented after Ghidra analysis.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, object> Properties { get; set; } = new();

    /// <summary>
    /// Asset references for prefab arrays.
    /// Key is the array field name, value is list of asset paths.
    ///
    /// Example:
    /// "prefabs": {
    ///   "chunkTemplates": ["Buildings/SmallOutpost", "Buildings/Bunker"],
    ///   "featureTemplates": ["Props/Rock_Large", "Props/Cactus"]
    /// }
    /// </summary>
    [JsonPropertyName("prefabs")]
    public Dictionary<string, List<string>> Prefabs { get; set; } = new();

    /// <summary>
    /// Helper to set a property value.
    /// </summary>
    public GeneratorConfig WithProperty(string name, object value)
    {
        Properties[name] = value;
        return this;
    }

    /// <summary>
    /// Helper to set prefab array.
    /// </summary>
    public GeneratorConfig WithPrefabs(string fieldName, params string[] assetPaths)
    {
        Prefabs[fieldName] = new List<string>(assetPaths);
        return this;
    }

    /// <summary>
    /// Create a config that disables the generator.
    /// </summary>
    public static GeneratorConfig Disabled()
    {
        return new GeneratorConfig { Enabled = false };
    }
}

/// <summary>
/// Catalog of known generator properties with metadata.
/// Populated from Ghidra analysis of game binary.
///
/// Generator Class Hierarchy (from Ghidra):
/// BaseMapGenerator (base class)
///   +-- ChunkGenerator        (buildings/structures)
///   +-- PropGenerator         (decorative props)
///   +-- EnvironmentFeatureGenerator (grass/foliage details)
///   +-- EnvironmentPropGenerator    (environment props around structures)
///   +-- LakeGenerator         (water bodies)
///
/// Standalone generators (not BaseMapGenerator subclasses):
///   - CoverGenerator          (cover objects)
///   - RoadGenerator           (road system)
///   - LampGenerator           (lighting)
///   - CableGenerator          (power cables)
/// </summary>
public static class GeneratorProperties
{
    /// <summary>
    /// Property metadata for UI and validation.
    /// </summary>
    public class PropertyInfo
    {
        public string Name { get; }
        public string Type { get; }
        public int Offset { get; }
        public object MinValue { get; }
        public object MaxValue { get; }
        public object DefaultValue { get; }
        public string Description { get; }
        public string Category { get; }

        public PropertyInfo(string name, string type, int offset, object minValue, object maxValue,
            object defaultValue, string description, string category = null)
        {
            Name = name;
            Type = type;
            Offset = offset;
            MinValue = minValue;
            MaxValue = maxValue;
            DefaultValue = defaultValue;
            Description = description;
            Category = category ?? "General";
        }

        public bool IsFloat => Type == "float" || Type == "Single";
        public bool IsInt => Type == "int" || Type == "Int32";
        public bool IsBool => Type == "bool" || Type == "Boolean";
        public bool IsPrefabArray => Type == "GameObject[]" || Type == "List<EntityTemplate>";
    }

    // ==================== ChunkGenerator (Buildings/Structures) ====================
    // Config at generator+0x38, uses ChunkConfig ScriptableObject

    public static readonly PropertyInfo[] ChunkGenerator = new[]
    {
        // ChunkConfig fields (accessed via _config at +0x38)
        new PropertyInfo("chunkTemplates", "GameObject[]", 0x58, null, null, null,
            "Building/structure prefabs to spawn", "Prefabs"),
        new PropertyInfo("_chunkSize", "Vector2Int", 0x58, null, null, null,
            "Base chunk size dimensions", "Layout"),
        new PropertyInfo("_roads", "bool", 0x78, null, null, true,
            "Enable road generation between chunks", "Roads"),
        new PropertyInfo("lampConfig", "LampConfig", 0x68, null, null, null,
            "Configuration for lamp placement around chunks", "Lighting"),
    };

    // ==================== PropGenerator (Decorative Props) ====================
    // Config at generator+0x38, uses PropConfig ScriptableObject

    public static readonly PropertyInfo[] PropGenerator = new[]
    {
        new PropertyInfo("_sizeMin", "int", 0x58, 1, 10, 1,
            "Minimum prop size", "Size"),
        new PropertyInfo("_sizeMax", "int", 0x5c, 1, 10, 1,
            "Maximum prop size", "Size"),
        new PropertyInfo("_spacingMin", "int", 0x60, 1, 10, 1,
            "Minimum tiles between props", "Spacing"),
        new PropertyInfo("_spacingMax", "int", 0x64, 1, 10, 2,
            "Maximum tiles between props", "Spacing"),
        new PropertyInfo("_count", "int", 0x68, 0, 200, 50,
            "Target number of props to spawn", "Spawning"),
        new PropertyInfo("_scatterCount", "int", 0x6c, 1, 50, 12,
            "Number of scatter positions", "Spawning"),
        new PropertyInfo("_margin", "int", 0x70, 0, 10, 2,
            "Edge margin from map border", "Placement"),
        new PropertyInfo("_scaleMin", "float", 0x84, 0.1f, 1.0f, 0.66f,
            "Minimum scale multiplier", "Scale"),
        new PropertyInfo("_scaleMax", "float", 0x88, 0.5f, 2.0f, 1.0f,
            "Maximum scale multiplier", "Scale"),
        new PropertyInfo("_templates", "List<EntityTemplate>", 0x90, null, null, null,
            "Small prop templates", "Prefabs"),
        new PropertyInfo("_largeTemplates", "List<EntityTemplate>", 0x98, null, null, null,
            "Large prop templates", "Prefabs"),
    };

    // ==================== EnvironmentFeatureGenerator (Grass/Foliage) ====================
    // Config at generator+0x38, uses EnvironmentFeatureConfig ScriptableObject

    public static readonly PropertyInfo[] EnvironmentFeatureGenerator = new[]
    {
        new PropertyInfo("_densityPerBiome", "float[]", 0x58, null, null, null,
            "Density values per biome type (14 elements, 0-100%)", "Density"),
        new PropertyInfo("_edgeDensityBonus", "float", 0x60, 0.0f, 50.0f, 10.0f,
            "Extra density near structure edges", "Density"),
    };

    // ==================== EnvironmentPropGenerator (Props Around Structures) ====================
    // Config at generator+0x38, uses EnvironmentPropConfig ScriptableObject

    public static readonly PropertyInfo[] EnvironmentPropGenerator = new[]
    {
        new PropertyInfo("_density", "float", 0x58, 0.0f, 2.0f, 1.0f,
            "Base spawn density", "Spawning"),
        new PropertyInfo("_edgeDensityBonus", "float", 0x5c, 0.0f, 50.0f, 10.0f,
            "Extra density at structure edges", "Spawning"),
        new PropertyInfo("_maxPerTile", "int", 0x64, 1, 10, 2,
            "Maximum props per tile", "Spawning"),
        new PropertyInfo("_scaleMin", "float", 0x68, 0.1f, 1.0f, 0.4f,
            "Minimum scale multiplier", "Scale"),
        new PropertyInfo("_scaleMax", "float", 0x6c, 0.5f, 2.0f, 0.9f,
            "Maximum scale multiplier", "Scale"),
        new PropertyInfo("_blockMovement", "bool", 0x74, null, null, false,
            "Whether props block movement", "Navigation"),
        new PropertyInfo("_smallTemplates", "List<EntityTemplate>", 0x78, null, null, null,
            "Small prop templates", "Prefabs"),
        new PropertyInfo("_mediumTemplates", "List<EntityTemplate>", 0x80, null, null, null,
            "Medium prop templates", "Prefabs"),
        new PropertyInfo("_largeTemplates", "List<EntityTemplate>", 0x88, null, null, null,
            "Large prop templates", "Prefabs"),
    };

    // ==================== LakeGenerator (Water Bodies) ====================

    public static readonly PropertyInfo[] LakeGenerator = new[]
    {
        new PropertyInfo("_spawnChance", "int", 0x10, 0, 100, 100,
            "Percentage chance to spawn lake (0-100)", "Spawning"),
        new PropertyInfo("_padding", "int", 0x14, 0, 10, 0,
            "Edge padding from boundaries", "Placement"),
        new PropertyInfo("_minSize", "float", 0x24, 1.0f, 10.0f, 2.0f,
            "Minimum lake radius", "Size"),
        new PropertyInfo("_maxSize", "float", 0x28, 2.0f, 20.0f, 4.0f,
            "Maximum lake radius", "Size"),
    };

    // ==================== CoverGenerator (Tactical Cover) ====================
    // Note: Not a BaseMapGenerator subclass

    public static readonly PropertyInfo[] CoverGenerator = new[]
    {
        new PropertyInfo("_coverPrefabs", "GameObject[]", 0x58, null, null, null,
            "Cover object prefab variants", "Prefabs"),
    };

    // ==================== RoadGenerator (Road Networks) ====================
    // Note: Not a BaseMapGenerator subclass

    public static readonly PropertyInfo[] RoadGenerator = new[]
    {
        new PropertyInfo("_surfaceType", "int", 0x58, 0, 10, 3,
            "Road surface material type", "Material"),
        new PropertyInfo("_decorationChance", "int", 0x60, 0, 100, 50,
            "Chance for road decorations (%)", "Decoration"),
        new PropertyInfo("_roadWidth", "int", 0x68, 1, 5, 3,
            "Width of roads in tiles", "Layout"),
        new PropertyInfo("_scale", "float", 0x6c, 0.5f, 2.0f, 1.0f,
            "Road decal scale", "Appearance"),
    };

    // ==================== LampGenerator (Street Lighting) ====================
    // Note: Not a BaseMapGenerator subclass

    public static readonly PropertyInfo[] LampGenerator = new[]
    {
        new PropertyInfo("_minDistance", "int", 0x58, 1, 20, 5,
            "Minimum tiles between lamps", "Spacing"),
        new PropertyInfo("_maxDistance", "int", 0x5c, 5, 30, 10,
            "Maximum tiles between lamps", "Spacing"),
        new PropertyInfo("_edgeDistance", "int", 0x60, 0, 5, 1,
            "Distance from structure edges", "Placement"),
        new PropertyInfo("_maxPerChunk", "int", 0x64, 1, 100, 99,
            "Maximum lamps per chunk", "Limits"),
        new PropertyInfo("_radius", "int", 0x68, 1, 20, 8,
            "Light radius", "Lighting"),
        new PropertyInfo("_prefabs", "GameObject[]", 0x70, null, null, null,
            "Lamp prefab variants", "Prefabs"),
    };

    // ==================== CableGenerator (Power Cables) ====================
    // Note: Not a BaseMapGenerator subclass

    public static readonly PropertyInfo[] CableGenerator = new[]
    {
        new PropertyInfo("_spawnChance", "int", 0x58, 0, 100, 55,
            "Percentage chance for cables (%)", "Spawning"),
        new PropertyInfo("_maxCablesPerStructure", "int", 0x5c, 0, 10, 3,
            "Max cable connections per building", "Limits"),
    };

    /// <summary>
    /// Get properties for a generator type by name.
    /// </summary>
    public static PropertyInfo[] GetProperties(string generatorTypeName)
    {
        return generatorTypeName switch
        {
            "ChunkGenerator" => ChunkGenerator,
            "PropGenerator" => PropGenerator,
            "EnvironmentFeatureGenerator" => EnvironmentFeatureGenerator,
            "EnvironmentPropGenerator" => EnvironmentPropGenerator,
            "LakeGenerator" => LakeGenerator,
            "CoverGenerator" => CoverGenerator,
            "RoadGenerator" => RoadGenerator,
            "LampGenerator" => LampGenerator,
            "CableGenerator" => CableGenerator,
            _ => System.Array.Empty<PropertyInfo>()
        };
    }

    /// <summary>
    /// Get all known generator type names.
    /// </summary>
    public static string[] GetGeneratorTypes()
    {
        return new[]
        {
            // BaseMapGenerator subclasses
            "ChunkGenerator",
            "PropGenerator",
            "EnvironmentFeatureGenerator",
            "EnvironmentPropGenerator",
            "LakeGenerator",
            // Standalone generators
            "CoverGenerator",
            "RoadGenerator",
            "LampGenerator",
            "CableGenerator"
        };
    }

    /// <summary>
    /// Get BaseMapGenerator subclass names only.
    /// </summary>
    public static string[] GetBaseMapGeneratorTypes()
    {
        return new[]
        {
            "ChunkGenerator",
            "PropGenerator",
            "EnvironmentFeatureGenerator",
            "EnvironmentPropGenerator",
            "LakeGenerator"
        };
    }
}
