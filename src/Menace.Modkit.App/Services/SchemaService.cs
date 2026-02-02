using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Loads schema.json and provides fast lookup of field metadata by template type and field name.
/// </summary>
public class SchemaService
{
    // templateTypeName -> fieldName -> FieldMeta
    private readonly Dictionary<string, Dictionary<string, FieldMeta>> _fieldsByTemplate = new(StringComparer.Ordinal);
    // templateTypeName -> full inheritance chain (base → derived)
    private readonly Dictionary<string, List<string>> _inheritanceChains = new(StringComparer.Ordinal);
    private bool _isLoaded;

    public class FieldMeta
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Category { get; set; } = "";
        public string Offset { get; set; } = "";
    }

    /// <summary>
    /// Load schema from the given path. Parses the "templates" section
    /// and indexes all fields by template type and field name.
    /// </summary>
    public void LoadSchema(string schemaJsonPath)
    {
        _fieldsByTemplate.Clear();
        _inheritanceChains.Clear();
        _isLoaded = false;

        if (!File.Exists(schemaJsonPath))
        {
            Console.WriteLine($"[SchemaService] Schema not found at {schemaJsonPath}");
            return;
        }

        try
        {
            var json = File.ReadAllText(schemaJsonPath);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("templates", out var templates))
            {
                Console.WriteLine("[SchemaService] No 'templates' section in schema.json");
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

                        fieldDict[name] = new FieldMeta
                        {
                            Name = name,
                            Type = type,
                            Category = category,
                            Offset = offset
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
                Console.WriteLine($"[SchemaService] Loaded {_inheritanceChains.Count} inheritance chains");
            }

            _isLoaded = true;
            Console.WriteLine($"[SchemaService] Loaded {_fieldsByTemplate.Count} template types");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SchemaService] Failed to load schema: {ex.Message}");
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
}
