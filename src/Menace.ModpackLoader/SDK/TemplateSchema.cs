using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Menace.SDK;

/// <summary>
/// Runtime schema loader for the SDK. Loads schema.json and provides fast offset lookups
/// for template field access without hardcoding offsets.
///
/// Usage:
///   TemplateSchema.Initialize("/path/to/schema.json");
///   var offset = TemplateSchema.GetOffset("SkillTemplate", "IsSilent");
///   bool isSilent = Marshal.ReadByte(templatePtr + offset) != 0;
/// </summary>
public static class TemplateSchema
{
    // templateTypeName -> fieldName -> offset (as int)
    private static readonly Dictionary<string, Dictionary<string, int>> _offsets = new(StringComparer.Ordinal);
    // templateTypeName -> fieldName -> type string
    private static readonly Dictionary<string, Dictionary<string, string>> _types = new(StringComparer.Ordinal);
    // enumTypeName -> { intValue -> name }
    private static readonly Dictionary<string, Dictionary<int, string>> _enums = new(StringComparer.Ordinal);
    // enumTypeName -> { name -> intValue }
    private static readonly Dictionary<string, Dictionary<string, int>> _enumsByName = new(StringComparer.Ordinal);

    private static bool _initialized;

    /// <summary>
    /// Initialize the schema from a JSON file. Call once at mod startup.
    /// </summary>
    public static void Initialize(string schemaJsonPath)
    {
        if (_initialized)
        {
            SdkLogger.Warning("[TemplateSchema] Already initialized, skipping reload");
            return;
        }

        _offsets.Clear();
        _types.Clear();
        _enums.Clear();
        _enumsByName.Clear();

        if (!File.Exists(schemaJsonPath))
        {
            SdkLogger.Warning($"[TemplateSchema] Schema not found at {schemaJsonPath}");
            return;
        }

        try
        {
            var json = File.ReadAllText(schemaJsonPath);
            using var doc = JsonDocument.Parse(json);

            // Parse templates
            if (doc.RootElement.TryGetProperty("templates", out var templates))
            {
                ParseTemplates(templates);
            }

            // Parse embedded_classes (same structure as templates)
            if (doc.RootElement.TryGetProperty("embedded_classes", out var embedded))
            {
                ParseTemplates(embedded);
            }

            // Parse effect_handlers (same structure as templates)
            if (doc.RootElement.TryGetProperty("effect_handlers", out var handlers))
            {
                ParseTemplates(handlers);
            }

            // Parse enums
            if (doc.RootElement.TryGetProperty("enums", out var enums))
            {
                foreach (var enumProp in enums.EnumerateObject())
                {
                    if (enumProp.Value.TryGetProperty("values", out var values))
                    {
                        var valueToName = new Dictionary<int, string>();
                        var nameToValue = new Dictionary<string, int>(StringComparer.Ordinal);

                        foreach (var v in values.EnumerateObject())
                        {
                            if (v.Value.TryGetInt32(out var intVal))
                            {
                                valueToName[intVal] = v.Name;
                                nameToValue[v.Name] = intVal;
                            }
                        }

                        _enums[enumProp.Name] = valueToName;
                        _enumsByName[enumProp.Name] = nameToValue;
                    }
                }
            }

            _initialized = true;
            SdkLogger.Msg($"[TemplateSchema] Loaded {_offsets.Count} types, {_enums.Count} enums");
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[TemplateSchema] Failed to load schema: {ex.Message}");
        }
    }

    private static void ParseTemplates(JsonElement templates)
    {
        foreach (var templateProp in templates.EnumerateObject())
        {
            var templateName = templateProp.Name;
            var offsetDict = new Dictionary<string, int>(StringComparer.Ordinal);
            var typeDict = new Dictionary<string, string>(StringComparer.Ordinal);

            if (templateProp.Value.TryGetProperty("fields", out var fields))
            {
                foreach (var field in fields.EnumerateArray())
                {
                    var name = field.GetProperty("name").GetString() ?? "";
                    var type = field.GetProperty("type").GetString() ?? "";
                    var offsetStr = field.TryGetProperty("offset", out var o) ? o.GetString() : null;

                    if (!string.IsNullOrEmpty(offsetStr))
                    {
                        // Parse hex offset like "0xF2"
                        if (offsetStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(offsetStr[2..], System.Globalization.NumberStyles.HexNumber, null, out var offset))
                            {
                                offsetDict[name] = offset;
                            }
                        }
                        else if (int.TryParse(offsetStr, out var decOffset))
                        {
                            offsetDict[name] = decOffset;
                        }
                    }

                    typeDict[name] = type;
                }
            }

            _offsets[templateName] = offsetDict;
            _types[templateName] = typeDict;
        }
    }

    /// <summary>
    /// Check if the schema has been loaded.
    /// </summary>
    public static bool IsInitialized => _initialized;

    /// <summary>
    /// Get the field offset for a template type. Returns -1 if not found.
    /// </summary>
    public static int GetOffset(string templateType, string fieldName)
    {
        if (_offsets.TryGetValue(templateType, out var fields))
        {
            if (fields.TryGetValue(fieldName, out var offset))
                return offset;
        }
        return -1;
    }

    /// <summary>
    /// Get the field offset, throwing if not found. Use when offset is required.
    /// </summary>
    public static int GetOffsetRequired(string templateType, string fieldName)
    {
        var offset = GetOffset(templateType, fieldName);
        if (offset < 0)
            throw new InvalidOperationException($"[TemplateSchema] Required offset not found: {templateType}.{fieldName}");
        return offset;
    }

    /// <summary>
    /// Try to get a field offset. Returns true if found.
    /// </summary>
    public static bool TryGetOffset(string templateType, string fieldName, out int offset)
    {
        offset = GetOffset(templateType, fieldName);
        return offset >= 0;
    }

    /// <summary>
    /// Get the type string for a field. Returns null if not found.
    /// </summary>
    public static string GetFieldType(string templateType, string fieldName)
    {
        if (_types.TryGetValue(templateType, out var fields))
        {
            if (fields.TryGetValue(fieldName, out var type))
                return type;
        }
        return null;
    }

    /// <summary>
    /// Check if a field exists on a template type.
    /// </summary>
    public static bool HasField(string templateType, string fieldName)
    {
        return _offsets.TryGetValue(templateType, out var fields) && fields.ContainsKey(fieldName);
    }

    /// <summary>
    /// Get all field names for a template type.
    /// </summary>
    public static IEnumerable<string> GetFieldNames(string templateType)
    {
        if (_offsets.TryGetValue(templateType, out var fields))
            return fields.Keys;
        return Array.Empty<string>();
    }

    /// <summary>
    /// Resolve an enum value to its name. Returns null if not found.
    /// </summary>
    public static string GetEnumName(string enumType, int value)
    {
        if (_enums.TryGetValue(enumType, out var values))
        {
            if (values.TryGetValue(value, out var name))
                return name;
        }
        return null;
    }

    /// <summary>
    /// Resolve an enum name to its value.
    /// </summary>
    public static int? GetEnumValue(string enumType, string name)
    {
        if (_enumsByName.TryGetValue(enumType, out var values))
        {
            if (values.TryGetValue(name, out var value))
                return value;
        }
        return null;
    }

    /// <summary>
    /// Get all values for an enum type. Returns null if not found.
    /// </summary>
    public static IReadOnlyDictionary<int, string> GetEnumValues(string enumType)
    {
        return _enums.TryGetValue(enumType, out var values) ? values : null;
    }
}
