#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Menace.ModpackLoader.VisualEditor.Schema;

/// <summary>
/// Parses schema.json to extract node definitions for the visual mod editor.
/// Provides cached access to enums and template fields for populating dropdowns
/// and defining node types.
///
/// Usage:
///   var parser = SchemaParser.Instance;
///   parser.Load("/path/to/schema.json");
///
///   // Get enum values for dropdowns
///   var factions = parser.GetEnumValues("FactionType");
///
///   // Get template fields
///   var skillFields = parser.GetTemplateFields("SkillTemplate");
///
///   // Get boolean fields for filter blocks
///   var boolFields = parser.GetBooleanFields("SkillTemplate");
///   // Returns: IsAttack, IsSilent, IsActive, IsTargeted, etc.
/// </summary>
public sealed class SchemaParser
{
    private static SchemaParser _instance;
    private static readonly object _lock = new();

    private readonly Dictionary<string, EnumDefinition> _enums = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TemplateDefinition> _templates = new(StringComparer.Ordinal);
    private bool _isLoaded;
    private string _schemaVersion;

    /// <summary>
    /// Gets the singleton instance of the schema parser.
    /// </summary>
    public static SchemaParser Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new SchemaParser();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Whether the schema has been loaded successfully.
    /// </summary>
    public bool IsLoaded => _isLoaded;

    /// <summary>
    /// The schema version string from schema.json.
    /// </summary>
    public string SchemaVersion => _schemaVersion;

    /// <summary>
    /// All available enum type names.
    /// </summary>
    public IEnumerable<string> EnumNames => _enums.Keys;

    /// <summary>
    /// All available template type names.
    /// </summary>
    public IEnumerable<string> TemplateNames => _templates.Keys;

    private SchemaParser() { }

    /// <summary>
    /// Load and parse the schema from a JSON file.
    /// </summary>
    /// <param name="schemaJsonPath">Path to schema.json</param>
    /// <returns>True if loaded successfully</returns>
    public bool Load(string schemaJsonPath)
    {
        if (_isLoaded)
            return true;

        if (!File.Exists(schemaJsonPath))
            return false;

        try
        {
            var json = File.ReadAllText(schemaJsonPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Parse version
            if (root.TryGetProperty("version", out var version))
                _schemaVersion = version.GetString();

            // Parse enums
            if (root.TryGetProperty("enums", out var enums))
                ParseEnums(enums);

            // Parse templates
            if (root.TryGetProperty("templates", out var templates))
                ParseTemplates(templates);

            // Parse embedded_classes (same structure as templates)
            if (root.TryGetProperty("embedded_classes", out var embedded))
                ParseTemplates(embedded);

            // Parse effect_handlers (same structure as templates)
            if (root.TryGetProperty("effect_handlers", out var handlers))
                ParseTemplates(handlers);

            _isLoaded = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reset the parser to allow reloading.
    /// </summary>
    public void Reset()
    {
        _enums.Clear();
        _templates.Clear();
        _isLoaded = false;
        _schemaVersion = null;
    }

    #region Enum Access

    /// <summary>
    /// Get all values for an enum type.
    /// </summary>
    /// <param name="enumName">The enum type name (e.g., "FactionType")</param>
    /// <returns>List of enum values, or empty if not found</returns>
    public IReadOnlyList<EnumValue> GetEnumValues(string enumName)
    {
        return _enums.TryGetValue(enumName, out var def)
            ? def.Values
            : Array.Empty<EnumValue>();
    }

    /// <summary>
    /// Get the enum definition for a type.
    /// </summary>
    /// <param name="enumName">The enum type name</param>
    /// <returns>Enum definition, or null if not found</returns>
    public EnumDefinition GetEnum(string enumName)
    {
        return _enums.TryGetValue(enumName, out var def) ? def : null;
    }

    /// <summary>
    /// Check if an enum type exists.
    /// </summary>
    public bool HasEnum(string enumName) => _enums.ContainsKey(enumName);

    /// <summary>
    /// Get enum value names as strings (for dropdown population).
    /// </summary>
    /// <param name="enumName">The enum type name</param>
    /// <returns>List of value names</returns>
    public IReadOnlyList<string> GetEnumValueNames(string enumName)
    {
        if (!_enums.TryGetValue(enumName, out var def))
            return Array.Empty<string>();

        return def.Values.Select(v => v.Name).ToList();
    }

    /// <summary>
    /// Resolve an enum name to its integer value.
    /// </summary>
    public int? GetEnumIntValue(string enumName, string valueName)
    {
        if (!_enums.TryGetValue(enumName, out var def))
            return null;

        var val = def.Values.FirstOrDefault(v => v.Name == valueName);
        return val != null ? val.IntValue : null;
    }

    /// <summary>
    /// Resolve an enum integer value to its name.
    /// </summary>
    public string GetEnumValueName(string enumName, int intValue)
    {
        if (!_enums.TryGetValue(enumName, out var def))
            return null;

        return def.Values.FirstOrDefault(v => v.IntValue == intValue)?.Name;
    }

    /// <summary>
    /// Get all enums relevant to the visual editor.
    /// Filters out debug/rendering enums that are not useful for modding.
    /// </summary>
    public IEnumerable<EnumDefinition> GetModdingEnums()
    {
        // Relevant enum prefixes/names for modding
        var relevantNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "FactionType", "SkillType", "EntityPropertyType", "EntityFlags",
            "MoraleState", "CoverType", "SkillTarget", "SkillOrder",
            "AnimationType", "AimingType", "RangeShape", "ExecutingElementType",
            "DamageType", "ArmorType", "StatusEffectType", "WeaponType",
            "ItemType", "ItemRarity", "ResourceType", "TerrainType"
        };

        foreach (var kvp in _enums)
        {
            // Include if explicitly relevant
            if (relevantNames.Contains(kvp.Key))
            {
                yield return kvp.Value;
                continue;
            }

            // Exclude debug/rendering/internal enums
            if (kvp.Key.Contains("Debug", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("Rendering", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("Resolution", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("Layer", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("Cache", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.StartsWith("HD", StringComparison.Ordinal) ||
                kvp.Key.StartsWith("RTAS", StringComparison.Ordinal) ||
                kvp.Key.Contains("Fog", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("Shadow", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("Light", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("Cookie", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("Probe", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("Reflection", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("Cluster", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("Mip", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("Exposure", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("HDR", StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains("Decal", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return kvp.Value;
        }
    }

    #endregion

    #region Template Access

    /// <summary>
    /// Get all fields for a template type.
    /// </summary>
    /// <param name="templateName">The template type name (e.g., "SkillTemplate")</param>
    /// <returns>List of field definitions, or empty if not found</returns>
    public IReadOnlyList<FieldDefinition> GetTemplateFields(string templateName)
    {
        return _templates.TryGetValue(templateName, out var def)
            ? def.Fields
            : Array.Empty<FieldDefinition>();
    }

    /// <summary>
    /// Get the template definition for a type.
    /// </summary>
    /// <param name="templateName">The template type name</param>
    /// <returns>Template definition, or null if not found</returns>
    public TemplateDefinition GetTemplate(string templateName)
    {
        return _templates.TryGetValue(templateName, out var def) ? def : null;
    }

    /// <summary>
    /// Check if a template type exists.
    /// </summary>
    public bool HasTemplate(string templateName) => _templates.ContainsKey(templateName);

    /// <summary>
    /// Get field names as strings (for dropdown population).
    /// </summary>
    /// <param name="templateName">The template type name</param>
    /// <returns>List of field names</returns>
    public IReadOnlyList<string> GetTemplateFieldNames(string templateName)
    {
        if (!_templates.TryGetValue(templateName, out var def))
            return Array.Empty<string>();

        return def.Fields.Select(f => f.Name).ToList();
    }

    /// <summary>
    /// Get a specific field from a template.
    /// </summary>
    public FieldDefinition GetField(string templateName, string fieldName)
    {
        if (!_templates.TryGetValue(templateName, out var def))
            return null;

        return def.Fields.FirstOrDefault(f => f.Name == fieldName);
    }

    /// <summary>
    /// Get all boolean fields from a template (useful for filter blocks).
    /// </summary>
    /// <param name="templateName">The template type name</param>
    /// <returns>List of boolean field definitions</returns>
    public IReadOnlyList<FieldDefinition> GetBooleanFields(string templateName)
    {
        if (!_templates.TryGetValue(templateName, out var def))
            return Array.Empty<FieldDefinition>();

        return def.Fields
            .Where(f => f.Type.Equals("bool", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Get all enum fields from a template.
    /// </summary>
    /// <param name="templateName">The template type name</param>
    /// <returns>List of enum field definitions</returns>
    public IReadOnlyList<FieldDefinition> GetEnumFields(string templateName)
    {
        if (!_templates.TryGetValue(templateName, out var def))
            return Array.Empty<FieldDefinition>();

        return def.Fields
            .Where(f => f.Category == FieldCategory.Enum)
            .ToList();
    }

    /// <summary>
    /// Get all numeric fields from a template (int, float).
    /// </summary>
    /// <param name="templateName">The template type name</param>
    /// <returns>List of numeric field definitions</returns>
    public IReadOnlyList<FieldDefinition> GetNumericFields(string templateName)
    {
        if (!_templates.TryGetValue(templateName, out var def))
            return Array.Empty<FieldDefinition>();

        return def.Fields
            .Where(f => f.Type.Equals("int", StringComparison.OrdinalIgnoreCase) ||
                       f.Type.Equals("float", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Get templates relevant for modding (filters out internal types).
    /// </summary>
    public IEnumerable<TemplateDefinition> GetModdingTemplates()
    {
        var relevantNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "SkillTemplate", "WeaponTemplate", "ArmorTemplate", "ItemTemplate",
            "LeaderTemplate", "EntityTemplate", "VehicleTemplate", "UnitTemplate",
            "FactionTemplate", "MissionTemplate", "EffectTemplate", "PerkTemplate",
            "StatusEffectTemplate", "EquipmentTemplate"
        };

        foreach (var kvp in _templates)
        {
            if (relevantNames.Contains(kvp.Key) || kvp.Key.EndsWith("Template"))
            {
                yield return kvp.Value;
            }
        }
    }

    #endregion

    #region Parsing

    private void ParseEnums(JsonElement enums)
    {
        foreach (var enumProp in enums.EnumerateObject())
        {
            var def = new EnumDefinition
            {
                Name = enumProp.Name
            };

            if (enumProp.Value.TryGetProperty("underlying_type", out var underlyingType))
                def.UnderlyingType = underlyingType.GetString();

            if (enumProp.Value.TryGetProperty("values", out var values))
            {
                var valueList = new List<EnumValue>();
                foreach (var valueProp in values.EnumerateObject())
                {
                    if (valueProp.Value.TryGetInt32(out var intVal))
                    {
                        valueList.Add(new EnumValue
                        {
                            Name = valueProp.Name,
                            IntValue = intVal
                        });
                    }
                }
                def.Values = valueList;
            }
            else
            {
                def.Values = Array.Empty<EnumValue>();
            }

            _enums[enumProp.Name] = def;
        }
    }

    private void ParseTemplates(JsonElement templates)
    {
        foreach (var templateProp in templates.EnumerateObject())
        {
            var def = new TemplateDefinition
            {
                Name = templateProp.Name
            };

            if (templateProp.Value.TryGetProperty("base_class", out var baseClass))
                def.BaseClass = baseClass.GetString();

            if (templateProp.Value.TryGetProperty("is_abstract", out var isAbstract))
                def.IsAbstract = isAbstract.GetBoolean();

            if (templateProp.Value.TryGetProperty("fields", out var fields))
            {
                var fieldList = new List<FieldDefinition>();
                foreach (var fieldElement in fields.EnumerateArray())
                {
                    var field = new FieldDefinition();

                    if (fieldElement.TryGetProperty("name", out var name))
                        field.Name = name.GetString();

                    if (fieldElement.TryGetProperty("type", out var type))
                        field.Type = type.GetString();

                    if (fieldElement.TryGetProperty("offset", out var offset))
                        field.Offset = ParseOffset(offset.GetString());

                    if (fieldElement.TryGetProperty("category", out var category))
                        field.Category = ParseFieldCategory(category.GetString());

                    if (fieldElement.TryGetProperty("element_type", out var elementType))
                        field.ElementType = elementType.GetString();

                    fieldList.Add(field);
                }
                def.Fields = fieldList;
            }
            else
            {
                def.Fields = Array.Empty<FieldDefinition>();
            }

            _templates[templateProp.Name] = def;
        }
    }

    private static int ParseOffset(string offsetStr)
    {
        if (string.IsNullOrEmpty(offsetStr))
            return -1;

        if (offsetStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(offsetStr[2..], System.Globalization.NumberStyles.HexNumber, null, out var offset))
                return offset;
        }
        else if (int.TryParse(offsetStr, out var decOffset))
        {
            return decOffset;
        }

        return -1;
    }

    private static FieldCategory ParseFieldCategory(string category)
    {
        return category?.ToLowerInvariant() switch
        {
            "primitive" => FieldCategory.Primitive,
            "enum" => FieldCategory.Enum,
            "string" => FieldCategory.String,
            "reference" => FieldCategory.Reference,
            "collection" => FieldCategory.Collection,
            "localization" => FieldCategory.Localization,
            "unity_asset" => FieldCategory.UnityAsset,
            _ => FieldCategory.Unknown
        };
    }

    #endregion
}

#region Data Models

/// <summary>
/// Definition of an enum type from the schema.
/// </summary>
public sealed class EnumDefinition
{
    public string Name { get; set; }
    public string UnderlyingType { get; set; }
    public IReadOnlyList<EnumValue> Values { get; set; }

    public override string ToString() => $"{Name} ({Values?.Count ?? 0} values)";
}

/// <summary>
/// A single value within an enum.
/// </summary>
public sealed class EnumValue
{
    public string Name { get; set; }
    public int IntValue { get; set; }

    public override string ToString() => $"{Name} = {IntValue}";
}

/// <summary>
/// Definition of a template class from the schema.
/// </summary>
public sealed class TemplateDefinition
{
    public string Name { get; set; }
    public string BaseClass { get; set; }
    public bool IsAbstract { get; set; }
    public IReadOnlyList<FieldDefinition> Fields { get; set; }

    public override string ToString() => $"{Name} ({Fields?.Count ?? 0} fields)";
}

/// <summary>
/// Definition of a field within a template.
/// </summary>
public sealed class FieldDefinition
{
    public string Name { get; set; }
    public string Type { get; set; }
    public int Offset { get; set; } = -1;
    public FieldCategory Category { get; set; }
    public string ElementType { get; set; }

    /// <summary>
    /// Whether this field is a boolean (useful for filter blocks).
    /// </summary>
    public bool IsBool => Type?.Equals("bool", StringComparison.OrdinalIgnoreCase) ?? false;

    /// <summary>
    /// Whether this field is an integer.
    /// </summary>
    public bool IsInt => Type?.Equals("int", StringComparison.OrdinalIgnoreCase) ?? false;

    /// <summary>
    /// Whether this field is a float.
    /// </summary>
    public bool IsFloat => Type?.Equals("float", StringComparison.OrdinalIgnoreCase) ?? false;

    /// <summary>
    /// Whether this field is a numeric type (int or float).
    /// </summary>
    public bool IsNumeric => IsInt || IsFloat;

    public override string ToString() => $"{Name}: {Type}";
}

/// <summary>
/// Categories of fields in the schema.
/// </summary>
public enum FieldCategory
{
    Unknown,
    Primitive,
    Enum,
    String,
    Reference,
    Collection,
    Localization,
    UnityAsset
}

#endregion
