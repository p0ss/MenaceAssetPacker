using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Loads schema.json and provides fast lookup of field metadata by template type and field name.
/// Also loads embedded classes (non-template classes used as element types in collections).
/// </summary>
public class SchemaService
{
    // templateTypeName -> fieldName -> FieldMeta
    private readonly Dictionary<string, Dictionary<string, FieldMeta>> _fieldsByTemplate = new(StringComparer.Ordinal);
    // embeddedClassName -> fieldName -> FieldMeta (for classes like Army, ArmyEntry)
    private readonly Dictionary<string, Dictionary<string, FieldMeta>> _fieldsByEmbeddedClass = new(StringComparer.Ordinal);
    // templateTypeName -> full inheritance chain (base → derived)
    private readonly Dictionary<string, List<string>> _inheritanceChains = new(StringComparer.Ordinal);
    // enumTypeName -> { intValue -> name }
    private readonly Dictionary<string, Dictionary<int, string>> _enumsByType = new(StringComparer.Ordinal);
    private bool _isLoaded;

    public class FieldMeta
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Category { get; set; } = "";
        public string Offset { get; set; } = "";
        public string ElementType { get; set; } = "";
    }

    /// <summary>
    /// Load schema from the given path. Parses the "templates" section
    /// and indexes all fields by template type and field name.
    /// </summary>
    public void LoadSchema(string schemaJsonPath)
    {
        _fieldsByTemplate.Clear();
        _fieldsByEmbeddedClass.Clear();
        _inheritanceChains.Clear();
        _enumsByType.Clear();
        _isLoaded = false;

        if (!File.Exists(schemaJsonPath))
        {
            ModkitLog.Info($"[SchemaService] Schema not found at {schemaJsonPath}");
            return;
        }

        try
        {
            var json = File.ReadAllText(schemaJsonPath);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("templates", out var templates))
            {
                ModkitLog.Warn("[SchemaService] No 'templates' section in schema.json");
                return;
            }

            foreach (var templateProp in templates.EnumerateObject())
            {
                var templateName = templateProp.Name;
                var fieldDict = new Dictionary<string, FieldMeta>(StringComparer.Ordinal);

                if (templateProp.Value.TryGetProperty("fields", out var fields))
                {
                    foreach (var field in fields.EnumerateArray())
                    {
                        var name = field.GetProperty("name").GetString() ?? "";
                        var type = field.GetProperty("type").GetString() ?? "";
                        var offset = field.TryGetProperty("offset", out var o) ? o.GetString() ?? "" : "";
                        var category = field.TryGetProperty("category", out var c) ? c.GetString() ?? "" : "";
                        var elementType = field.TryGetProperty("element_type", out var et) ? et.GetString() ?? "" : "";

                        fieldDict[name] = new FieldMeta
                        {
                            Name = name,
                            Type = type,
                            Category = category,
                            Offset = offset,
                            ElementType = elementType
                        };
                    }
                }

                // Also walk base_class chain to inherit fields
                if (templateProp.Value.TryGetProperty("base_class", out var baseClassEl))
                {
                    var baseClassName = baseClassEl.GetString();
                    if (!string.IsNullOrEmpty(baseClassName) && baseClassName != "ScriptableObject")
                    {
                        // Store base class name for deferred resolution
                        // (base class may not be parsed yet)
                        _fieldsByTemplate[templateName] = fieldDict;
                        continue;
                    }
                }

                _fieldsByTemplate[templateName] = fieldDict;
            }

            // Resolve base class inheritance (merge parent fields into children)
            ResolveInheritance(templates);

            // Parse embedded classes (non-template classes used as element types)
            if (doc.RootElement.TryGetProperty("embedded_classes", out var embeddedClasses))
            {
                foreach (var classProp in embeddedClasses.EnumerateObject())
                {
                    var className = classProp.Name;
                    var fieldDict = new Dictionary<string, FieldMeta>(StringComparer.Ordinal);

                    if (classProp.Value.TryGetProperty("fields", out var fields))
                    {
                        foreach (var field in fields.EnumerateArray())
                        {
                            var name = field.GetProperty("name").GetString() ?? "";
                            var type = field.GetProperty("type").GetString() ?? "";
                            var offset = field.TryGetProperty("offset", out var o) ? o.GetString() ?? "" : "";
                            var category = field.TryGetProperty("category", out var c) ? c.GetString() ?? "" : "";
                            var elementType = field.TryGetProperty("element_type", out var et) ? et.GetString() ?? "" : "";

                            fieldDict[name] = new FieldMeta
                            {
                                Name = name,
                                Type = type,
                                Category = category,
                                Offset = offset,
                                ElementType = elementType
                            };
                        }
                    }

                    _fieldsByEmbeddedClass[className] = fieldDict;
                }
                ModkitLog.Info($"[SchemaService] Loaded {_fieldsByEmbeddedClass.Count} embedded classes");
            }

            // Parse inheritance chains
            if (doc.RootElement.TryGetProperty("inheritance", out var inheritance))
            {
                foreach (var prop in inheritance.EnumerateObject())
                {
                    var chain = new List<string>();
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        var val = item.GetString();
                        if (val != null) chain.Add(val);
                    }
                    _inheritanceChains[prop.Name] = chain;
                }
                ModkitLog.Info($"[SchemaService] Loaded {_inheritanceChains.Count} inheritance chains");
            }

            // Parse enum definitions
            if (doc.RootElement.TryGetProperty("enums", out var enums))
            {
                foreach (var enumProp in enums.EnumerateObject())
                {
                    if (enumProp.Value.TryGetProperty("values", out var values))
                    {
                        var valueToName = new Dictionary<int, string>();
                        foreach (var v in values.EnumerateObject())
                        {
                            if (v.Value.TryGetInt32(out var intVal))
                                valueToName[intVal] = v.Name;
                        }
                        _enumsByType[enumProp.Name] = valueToName;
                    }
                }
                ModkitLog.Info($"[SchemaService] Loaded {_enumsByType.Count} enum types");
            }

            _isLoaded = true;
            ModkitLog.Info($"[SchemaService] Loaded {_fieldsByTemplate.Count} template types");
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[SchemaService] Failed to load schema: {ex.Message}");
        }
    }

    private void ResolveInheritance(JsonElement templates)
    {
        // Build base_class mapping
        var baseClassMap = new Dictionary<string, string>();
        foreach (var templateProp in templates.EnumerateObject())
        {
            if (templateProp.Value.TryGetProperty("base_class", out var baseEl))
            {
                var baseName = baseEl.GetString();
                if (!string.IsNullOrEmpty(baseName) && baseName != "ScriptableObject")
                    baseClassMap[templateProp.Name] = baseName;
            }
        }

        // For each template, merge parent fields (parent fields first, child overrides)
        foreach (var kvp in baseClassMap)
        {
            var templateName = kvp.Key;
            var baseName = kvp.Value;

            if (!_fieldsByTemplate.ContainsKey(templateName))
                continue;

            // Walk the inheritance chain and collect parent fields
            var parentFields = new List<Dictionary<string, FieldMeta>>();
            var current = baseName;
            var visited = new HashSet<string> { templateName };

            while (!string.IsNullOrEmpty(current) && !visited.Contains(current))
            {
                visited.Add(current);
                if (_fieldsByTemplate.TryGetValue(current, out var parentFieldDict))
                    parentFields.Add(parentFieldDict);
                baseClassMap.TryGetValue(current, out current);
            }

            // Merge parent fields (only add fields not already defined on the child)
            var childFields = _fieldsByTemplate[templateName];
            for (int i = parentFields.Count - 1; i >= 0; i--)
            {
                foreach (var field in parentFields[i])
                {
                    childFields.TryAdd(field.Key, field.Value);
                }
            }
        }
    }

    /// <summary>
    /// Get metadata for a specific field on a template type.
    /// </summary>
    public FieldMeta? GetFieldMetadata(string templateTypeName, string fieldName)
    {
        if (!_isLoaded) return null;
        if (_fieldsByTemplate.TryGetValue(templateTypeName, out var fields))
        {
            if (fields.TryGetValue(fieldName, out var meta))
                return meta;
        }
        return null;
    }

    /// <summary>
    /// Get all fields for a template type.
    /// </summary>
    public List<FieldMeta> GetAllTemplateFields(string templateTypeName)
    {
        if (!_isLoaded) return new List<FieldMeta>();
        if (_fieldsByTemplate.TryGetValue(templateTypeName, out var fields))
        {
            return new List<FieldMeta>(fields.Values);
        }
        return new List<FieldMeta>();
    }

    /// <summary>
    /// Get all fields tagged as unity_asset for a template type.
    /// </summary>
    public List<FieldMeta> GetAssetFields(string templateTypeName)
    {
        var result = new List<FieldMeta>();
        if (!_isLoaded) return result;
        if (_fieldsByTemplate.TryGetValue(templateTypeName, out var fields))
        {
            foreach (var field in fields.Values)
            {
                if (field.Category == "unity_asset")
                    result.Add(field);
            }
        }
        return result;
    }

    /// <summary>
    /// Check if a specific field on a template is a unity_asset field.
    /// </summary>
    public bool IsAssetField(string templateTypeName, string fieldName)
    {
        var meta = GetFieldMetadata(templateTypeName, fieldName);
        return meta?.Category == "unity_asset";
    }

    /// <summary>
    /// Check if a field is a collection of template references (e.g. SkillTemplate[], TagTemplate[]).
    /// Returns true only if the element_type itself is a known template type with loadable instances.
    /// </summary>
    public bool IsTemplateRefCollection(string templateTypeName, string fieldName)
    {
        var meta = GetFieldMetadata(templateTypeName, fieldName);
        if (meta == null || meta.Category != "collection" || string.IsNullOrEmpty(meta.ElementType))
            return false;
        return _fieldsByTemplate.ContainsKey(meta.ElementType);
    }

    /// <summary>
    /// Resolve an enum integer value to its name, given the enum type name from the schema.
    /// Returns null if the enum type or value is not found.
    /// </summary>
    public string? ResolveEnumName(string enumTypeName, int value)
    {
        if (_enumsByType.TryGetValue(enumTypeName, out var values))
        {
            if (values.TryGetValue(value, out var name))
                return name;
        }
        return null;
    }

    public bool IsLoaded => _isLoaded;

    /// <summary>
    /// Get the full inheritance chain for a template type (base → derived).
    /// Returns a single-element list with the type name if no chain is found.
    /// </summary>
    public List<string> GetInheritanceChain(string templateTypeName)
    {
        if (_inheritanceChains.TryGetValue(templateTypeName, out var chain))
            return chain;
        return new List<string> { templateTypeName };
    }

    /// <summary>
    /// Get the inheritance depth (chain length) for a template type.
    /// </summary>
    public int GetInheritanceDepth(string templateTypeName)
    {
        if (_inheritanceChains.TryGetValue(templateTypeName, out var chain))
            return chain.Count;
        return 1;
    }

    /// <summary>
    /// Check if a class name is a known embedded class (non-template class used in collections).
    /// </summary>
    public bool IsEmbeddedClass(string className)
    {
        return _fieldsByEmbeddedClass.ContainsKey(className);
    }

    /// <summary>
    /// Get all field names for an embedded class.
    /// </summary>
    public List<string> GetEmbeddedClassFields(string className)
    {
        if (_fieldsByEmbeddedClass.TryGetValue(className, out var fields))
            return new List<string>(fields.Keys);
        return new List<string>();
    }

    /// <summary>
    /// Get metadata for a specific field on an embedded class.
    /// </summary>
    public FieldMeta? GetEmbeddedClassFieldMetadata(string className, string fieldName)
    {
        if (_fieldsByEmbeddedClass.TryGetValue(className, out var fields))
        {
            if (fields.TryGetValue(fieldName, out var meta))
                return meta;
        }
        return null;
    }

    /// <summary>
    /// Get all fields for an embedded class as a list.
    /// </summary>
    public List<FieldMeta> GetAllEmbeddedClassFields(string className)
    {
        if (_fieldsByEmbeddedClass.TryGetValue(className, out var fields))
            return new List<FieldMeta>(fields.Values);
        return new List<FieldMeta>();
    }

    /// <summary>
    /// Get all known embedded class names.
    /// </summary>
    public IEnumerable<string> GetAllEmbeddedClassNames()
    {
        return _fieldsByEmbeddedClass.Keys;
    }

    /// <summary>
    /// Check if a given element type (from a collection field) has a known schema.
    /// Returns true if it's a template, embedded class, or struct.
    /// </summary>
    public bool HasSchemaForElementType(string elementType)
    {
        return _fieldsByTemplate.ContainsKey(elementType) ||
               _fieldsByEmbeddedClass.ContainsKey(elementType);
    }
}
