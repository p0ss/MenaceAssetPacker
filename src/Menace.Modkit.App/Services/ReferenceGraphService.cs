using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Type of reference relationship between templates.
/// </summary>
public enum ReferenceType
{
    /// <summary>Single field reference (e.g., FactionTemplate.Leader → EntityTemplate)</summary>
    Direct,
    /// <summary>Array/List of templates (e.g., EntityTemplate.Skills[] → SkillTemplate)</summary>
    CollectionDirect,
    /// <summary>Array/List of embedded classes containing refs (e.g., Army.Entries[].Template)</summary>
    CollectionEmbedded
}

/// <summary>
/// Enhanced reference entry with detailed path information for collection references.
/// </summary>
public class EnhancedReferenceEntry : ReferenceEntry
{
    /// <summary>Type of reference relationship</summary>
    public ReferenceType Type { get; set; } = ReferenceType.Direct;

    /// <summary>For embedded collections: the class name (e.g., "ArmyEntry")</summary>
    public string? EmbeddedClassName { get; set; }

    /// <summary>For embedded collections: the field within the embedded class (e.g., "Template")</summary>
    public string? EmbeddedFieldName { get; set; }

    /// <summary>Index within the collection (-1 if not applicable)</summary>
    public int CollectionIndex { get; set; } = -1;

    /// <summary>The actual referenced value (template name)</summary>
    public string? ReferencedValue { get; set; }

    /// <summary>Full field path for display (e.g., "Entries[3].Template")</summary>
    public string FullFieldPath =>
        CollectionIndex >= 0 && !string.IsNullOrEmpty(EmbeddedFieldName)
            ? $"{FieldName}[{CollectionIndex}].{EmbeddedFieldName}"
            : FieldName;
}

/// <summary>
/// Builds and queries a reverse reference graph for templates and assets.
/// Enables "What Links Here" functionality by tracking which templates reference
/// other templates or assets.
/// </summary>
public class ReferenceGraphService
{
    private const string ReferenceFileName = "references.json";
    private const int CurrentVersion = 2; // Bumped for embedded collection support

    // Template backlinks: "TemplateType/InstanceName" → list of references
    private readonly Dictionary<string, List<ReferenceEntry>> _templateBacklinks = new(StringComparer.Ordinal);

    // Enhanced backlinks with full path info (for embedded collections)
    private readonly Dictionary<string, List<EnhancedReferenceEntry>> _enhancedBacklinks = new(StringComparer.Ordinal);

    // Asset backlinks: "assetName" → list of template references
    private readonly Dictionary<string, List<ReferenceEntry>> _assetBacklinks = new(StringComparer.OrdinalIgnoreCase);

    // Set of known template types for reference detection
    private readonly HashSet<string> _knownTemplateTypes = new(StringComparer.Ordinal);

    // Reference to schema service for embedded class lookups
    private SchemaService? _schemaService;

    // Schema-based relationship hints (when extracted data is incomplete)
    private readonly Dictionary<string, SchemaRelationshipHint> _schemaRelationshipHints = new(StringComparer.Ordinal);

    private bool _isLoaded;

    /// <summary>
    /// Represents a schema-based relationship between template types.
    /// Used when extracted data doesn't contain actual reference values.
    /// </summary>
    public class SchemaRelationshipHint
    {
        public required string SourceTemplateType { get; init; }
        public required string TargetTemplateType { get; init; }
        public required string Path { get; init; }
    }

    public bool IsLoaded => _isLoaded;

    /// <summary>
    /// Build the reference graph from extracted template data.
    /// Scans all template JSON files and schema to find references.
    /// </summary>
    public void BuildFromExtractedData(string extractedDataPath, SchemaService schemaService)
    {
        _templateBacklinks.Clear();
        _enhancedBacklinks.Clear();
        _assetBacklinks.Clear();
        _knownTemplateTypes.Clear();
        _schemaRelationshipHints.Clear();
        _schemaService = schemaService;
        _isLoaded = false;

        if (!Directory.Exists(extractedDataPath))
        {
            ModkitLog.Warn($"[ReferenceGraphService] ExtractedData path not found: {extractedDataPath}");
            return;
        }

        // First pass: collect all template type names
        foreach (var file in Directory.GetFiles(extractedDataPath, "*.json"))
        {
            var templateType = Path.GetFileNameWithoutExtension(file);
            if (templateType != null && templateType != "AssetReferences" && templateType != "menu")
                _knownTemplateTypes.Add(templateType);
        }

        ModkitLog.Info($"[ReferenceGraphService] Found {_knownTemplateTypes.Count} template types");

        // Second pass: scan each template file for references
        foreach (var file in Directory.GetFiles(extractedDataPath, "*.json"))
        {
            var templateType = Path.GetFileNameWithoutExtension(file);
            if (templateType == null || templateType == "AssetReferences" || templateType == "menu")
                continue;

            try
            {
                ProcessTemplateFile(file, templateType, schemaService);
            }
            catch (Exception ex)
            {
                ModkitLog.Warn($"[ReferenceGraphService] Error processing {templateType}: {ex.Message}");
            }
        }

        _isLoaded = true;

        // Log summary of enhanced backlinks by type
        var embeddedCount = _enhancedBacklinks.Values.SelectMany(v => v).Count(e => e.Type == ReferenceType.CollectionEmbedded);
        var directCollCount = _enhancedBacklinks.Values.SelectMany(v => v).Count(e => e.Type == ReferenceType.CollectionDirect);
        var directCount = _enhancedBacklinks.Values.SelectMany(v => v).Count(e => e.Type == ReferenceType.Direct);

        ModkitLog.Info($"[ReferenceGraphService] Built graph with {_templateBacklinks.Count} template backlinks, {_assetBacklinks.Count} asset backlinks");
        ModkitLog.Info($"[ReferenceGraphService] Enhanced backlinks: {directCount} direct, {directCollCount} collection-direct, {embeddedCount} collection-embedded");

        // If we found very few backlinks, the extracted data may be incomplete
        // Build a schema-based relationship map as fallback
        if (_templateBacklinks.Count < 50)
        {
            ModkitLog.Info($"[ReferenceGraphService] Building schema-based reference hints (extracted data may be incomplete)...");
            BuildSchemaBasedRelationships(schemaService);
        }
    }

    /// <summary>
    /// Build relationship hints based on schema definitions rather than extracted data.
    /// This is a fallback when the extracted data doesn't include collection/reference fields.
    /// </summary>
    private void BuildSchemaBasedRelationships(SchemaService schemaService)
    {
        // Build a map of what template types reference other template types
        // e.g., "ArmyListTemplate" -> ["EntityTemplate"] via Compositions[].Entries[].Template
        foreach (var templateType in _knownTemplateTypes)
        {
            var referencedTypes = FindReferencedTypesInSchema(templateType, schemaService, new HashSet<string>());
            foreach (var refType in referencedTypes)
            {
                // Store as a schema hint (we don't have instance-level data)
                var hintKey = $"__schema__{templateType}_references_{refType}";
                if (!_schemaRelationshipHints.ContainsKey(hintKey))
                {
                    _schemaRelationshipHints[hintKey] = new SchemaRelationshipHint
                    {
                        SourceTemplateType = templateType,
                        TargetTemplateType = refType,
                        Path = GetReferencePath(templateType, refType, schemaService)
                    };
                }
            }
        }

        ModkitLog.Info($"[ReferenceGraphService] Found {_schemaRelationshipHints.Count} schema-based relationship hints");
    }

    /// <summary>
    /// Find all template types referenced by a given template type (directly or through embedded classes).
    /// </summary>
    private HashSet<string> FindReferencedTypesInSchema(string templateType, SchemaService schemaService, HashSet<string> visited)
    {
        var result = new HashSet<string>();
        if (visited.Contains(templateType))
            return result;
        visited.Add(templateType);

        // Get fields for this template type
        var fields = GetAllFieldsForType(templateType, schemaService);

        foreach (var field in fields)
        {
            if (field.Category == "reference" && _knownTemplateTypes.Contains(field.Type))
            {
                result.Add(field.Type);
            }
            else if (field.Category == "collection" && !string.IsNullOrEmpty(field.ElementType))
            {
                if (_knownTemplateTypes.Contains(field.ElementType))
                {
                    result.Add(field.ElementType);
                }
                else if (schemaService.IsEmbeddedClass(field.ElementType))
                {
                    // Recursively find references in embedded class
                    var embeddedRefs = FindReferencedTypesInEmbeddedClass(field.ElementType, schemaService, visited);
                    foreach (var r in embeddedRefs)
                        result.Add(r);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Find template types referenced by an embedded class.
    /// </summary>
    private HashSet<string> FindReferencedTypesInEmbeddedClass(string className, SchemaService schemaService, HashSet<string> visited)
    {
        var result = new HashSet<string>();
        if (visited.Contains(className))
            return result;
        visited.Add(className);

        var fields = schemaService.GetAllEmbeddedClassFields(className);
        foreach (var field in fields)
        {
            if (field.Category == "reference" && _knownTemplateTypes.Contains(field.Type))
            {
                result.Add(field.Type);
            }
            else if (field.Category == "collection" && !string.IsNullOrEmpty(field.ElementType))
            {
                if (_knownTemplateTypes.Contains(field.ElementType))
                {
                    result.Add(field.ElementType);
                }
                else if (schemaService.IsEmbeddedClass(field.ElementType))
                {
                    var nested = FindReferencedTypesInEmbeddedClass(field.ElementType, schemaService, visited);
                    foreach (var r in nested)
                        result.Add(r);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Get all fields for a template type (from schema service).
    /// </summary>
    private List<SchemaService.FieldMeta> GetAllFieldsForType(string templateType, SchemaService schemaService)
    {
        return schemaService.GetAllTemplateFields(templateType);
    }

    /// <summary>
    /// Get the path from source to target template type via schema fields.
    /// </summary>
    private string GetReferencePath(string sourceType, string targetType, SchemaService schemaService)
    {
        // Build a human-readable path like "Compositions[].Entries[].Template"
        return FindPathToType(sourceType, targetType, schemaService, "", new HashSet<string>()) ?? targetType;
    }

    private string? FindPathToType(string currentType, string targetType, SchemaService schemaService, string currentPath, HashSet<string> visited)
    {
        if (visited.Contains(currentType))
            return null;
        visited.Add(currentType);

        var fields = GetFieldsForPathSearch(currentType, schemaService);

        foreach (var field in fields)
        {
            var fieldPath = string.IsNullOrEmpty(currentPath) ? field.Name : $"{currentPath}.{field.Name}";

            if (field.Category == "reference" && field.Type == targetType)
            {
                return fieldPath;
            }
            else if (field.Category == "collection" && !string.IsNullOrEmpty(field.ElementType))
            {
                var collPath = $"{fieldPath}[]";

                if (field.ElementType == targetType)
                {
                    return collPath;
                }
                else if (schemaService.IsEmbeddedClass(field.ElementType))
                {
                    var nestedPath = FindPathToType(field.ElementType, targetType, schemaService, collPath, visited);
                    if (nestedPath != null)
                        return nestedPath;
                }
            }
        }

        return null;
    }

    private List<SchemaService.FieldMeta> GetFieldsForPathSearch(string typeName, SchemaService schemaService)
    {
        if (schemaService.IsEmbeddedClass(typeName))
        {
            return schemaService.GetAllEmbeddedClassFields(typeName);
        }

        // For template types, get all fields from schema
        return schemaService.GetAllTemplateFields(typeName);
    }

    private void ProcessTemplateFile(string filePath, string templateType, SchemaService schemaService)
    {
        var json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return;

        foreach (var templateElement in doc.RootElement.EnumerateArray())
        {
            if (templateElement.ValueKind != JsonValueKind.Object)
                continue;

            var instanceName = templateElement.TryGetProperty("name", out var nameProp)
                ? nameProp.GetString() ?? ""
                : "";

            if (string.IsNullOrEmpty(instanceName))
                continue;

            // Scan all properties of this template
            foreach (var prop in templateElement.EnumerateObject())
            {
                ProcessField(templateType, instanceName, prop.Name, prop.Value, schemaService);
            }
        }
    }

    private void ProcessField(string templateType, string instanceName, string fieldName, JsonElement value, SchemaService schemaService)
    {
        var fieldMeta = schemaService.GetFieldMetadata(templateType, fieldName);

        // Handle explicit reference category
        if (fieldMeta?.Category == "reference")
        {
            // Check if the type is a known template type
            if (_knownTemplateTypes.Contains(fieldMeta.Type))
            {
                var refValue = ExtractStringValue(value);
                if (!string.IsNullOrEmpty(refValue))
                {
                    AddTemplateBacklink(fieldMeta.Type, refValue, templateType, instanceName, fieldName);
                    AddEnhancedBacklink(fieldMeta.Type, refValue, templateType, instanceName, fieldName,
                        ReferenceType.Direct, null, null, -1);
                }
            }
        }
        // Handle collection of template references
        else if (fieldMeta?.Category == "collection" && !string.IsNullOrEmpty(fieldMeta.ElementType))
        {
            // Check if element type is a known template (direct template collection)
            if (_knownTemplateTypes.Contains(fieldMeta.ElementType) && value.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    var refValue = ExtractStringValue(item);
                    if (!string.IsNullOrEmpty(refValue))
                    {
                        AddTemplateBacklink(fieldMeta.ElementType, refValue, templateType, instanceName, fieldName);
                        AddEnhancedBacklink(fieldMeta.ElementType, refValue, templateType, instanceName, fieldName,
                            ReferenceType.CollectionDirect, null, null, index);
                    }
                    index++;
                }
            }
            // Check if element type is an embedded class (embedded collection)
            else if (schemaService.IsEmbeddedClass(fieldMeta.ElementType) && value.ValueKind == JsonValueKind.Array)
            {
                ProcessEmbeddedCollection(templateType, instanceName, fieldName, fieldMeta.ElementType, value, schemaService);
            }
        }
        // Handle unity_asset references
        else if (fieldMeta?.Category == "unity_asset")
        {
            var assetName = ExtractStringValue(value);
            if (!string.IsNullOrEmpty(assetName) && !assetName.StartsWith("(") && !assetName.EndsWith(")"))
            {
                AddAssetBacklink(assetName, templateType, instanceName, fieldName);
            }
        }
        // Handle fields that might be template references based on naming convention
        // (fields ending in "Template" that reference another template)
        else if (fieldMeta == null && fieldName.EndsWith("Template") && value.ValueKind == JsonValueKind.String)
        {
            var refValue = value.GetString();
            if (!string.IsNullOrEmpty(refValue))
            {
                // Try to infer the template type from the field name
                var inferredType = fieldName; // e.g., "SpeakerTemplate" → look for SpeakerTemplate.json
                if (_knownTemplateTypes.Contains(inferredType))
                {
                    AddTemplateBacklink(inferredType, refValue, templateType, instanceName, fieldName);
                    AddEnhancedBacklink(inferredType, refValue, templateType, instanceName, fieldName,
                        ReferenceType.Direct, null, null, -1);
                }
            }
        }
        // Also check for string arrays that might be template references (Tags, Skills, etc.)
        else if (value.ValueKind == JsonValueKind.Array)
        {
            // Check common template reference patterns
            var possibleElementType = InferElementType(fieldName);
            if (possibleElementType != null && _knownTemplateTypes.Contains(possibleElementType))
            {
                int index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    var refValue = ExtractStringValue(item);
                    if (!string.IsNullOrEmpty(refValue))
                    {
                        AddTemplateBacklink(possibleElementType, refValue, templateType, instanceName, fieldName);
                        AddEnhancedBacklink(possibleElementType, refValue, templateType, instanceName, fieldName,
                            ReferenceType.CollectionDirect, null, null, index);
                    }
                    index++;
                }
            }
        }
    }

    /// <summary>
    /// Process a collection of embedded class objects, scanning each for template references.
    /// E.g., ArmyTemplate.Entries (List<ArmyEntry>) where ArmyEntry.Template → EntityTemplate
    /// Also handles nested embedded collections recursively.
    /// </summary>
    private void ProcessEmbeddedCollection(
        string templateType,
        string instanceName,
        string fieldName,
        string embeddedClassName,
        JsonElement arrayValue,
        SchemaService schemaService)
    {
        ProcessEmbeddedCollectionRecursive(
            templateType, instanceName, fieldName, embeddedClassName,
            arrayValue, schemaService, fieldName, 0);
    }

    /// <summary>
    /// Recursive helper for processing embedded collections at any depth.
    /// </summary>
    private void ProcessEmbeddedCollectionRecursive(
        string templateType,
        string instanceName,
        string rootFieldName,
        string embeddedClassName,
        JsonElement arrayValue,
        SchemaService schemaService,
        string currentPath,
        int depth)
    {
        // Prevent infinite recursion
        if (depth > 5)
            return;

        var embeddedFields = schemaService.GetAllEmbeddedClassFields(embeddedClassName);
        if (embeddedFields.Count == 0)
            return;

        int index = 0;
        foreach (var item in arrayValue.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                index++;
                continue;
            }

            var itemPath = $"{currentPath}[{index}]";

            foreach (var field in embeddedFields)
            {
                if (!item.TryGetProperty(field.Name, out var fieldValue))
                    continue;

                // Handle direct reference fields
                if (field.Category == "reference" && _knownTemplateTypes.Contains(field.Type))
                {
                    var refName = ExtractStringValue(fieldValue);
                    if (!string.IsNullOrEmpty(refName))
                    {
                        var fullPath = $"{itemPath}.{field.Name}";
                        AddTemplateBacklink(field.Type, refName, templateType, instanceName, fullPath);
                        AddEnhancedBacklink(field.Type, refName, templateType, instanceName, rootFieldName,
                            ReferenceType.CollectionEmbedded, embeddedClassName, field.Name, index);
                    }
                }
                // Handle nested collections of templates
                else if (field.Category == "collection" && !string.IsNullOrEmpty(field.ElementType))
                {
                    if (fieldValue.ValueKind == JsonValueKind.Array)
                    {
                        // Direct template collection within embedded class
                        if (_knownTemplateTypes.Contains(field.ElementType))
                        {
                            int nestedIndex = 0;
                            foreach (var nestedItem in fieldValue.EnumerateArray())
                            {
                                var refName = ExtractStringValue(nestedItem);
                                if (!string.IsNullOrEmpty(refName))
                                {
                                    var fullPath = $"{itemPath}.{field.Name}[{nestedIndex}]";
                                    AddTemplateBacklink(field.ElementType, refName, templateType, instanceName, fullPath);
                                    AddEnhancedBacklink(field.ElementType, refName, templateType, instanceName, rootFieldName,
                                        ReferenceType.CollectionEmbedded, embeddedClassName, $"{field.Name}[{nestedIndex}]", index);
                                }
                                nestedIndex++;
                            }
                        }
                        // Nested embedded class collection - recurse!
                        else if (schemaService.IsEmbeddedClass(field.ElementType))
                        {
                            var nestedPath = $"{itemPath}.{field.Name}";
                            ProcessEmbeddedCollectionRecursive(
                                templateType, instanceName, rootFieldName, field.ElementType,
                                fieldValue, schemaService, nestedPath, depth + 1);
                        }
                    }
                }
            }
            index++;
        }
    }

    private static string? InferElementType(string fieldName)
    {
        // Common patterns: "Tags" → "TagTemplate", "Skills" → "SkillTemplate"
        if (fieldName.EndsWith("s"))
        {
            var singular = fieldName[..^1]; // Remove trailing 's'
            return singular + "Template";
        }
        return null;
    }

    private static string? ExtractStringValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Null => null,
            _ => null
        };
    }

    private void AddTemplateBacklink(string targetType, string targetInstance, string sourceType, string sourceInstance, string fieldName)
    {
        var targetKey = $"{targetType}/{targetInstance}";

        if (!_templateBacklinks.TryGetValue(targetKey, out var entries))
        {
            entries = new List<ReferenceEntry>();
            _templateBacklinks[targetKey] = entries;
        }

        // Avoid duplicates
        if (!entries.Any(e => e.SourceTemplateType == sourceType && e.SourceInstanceName == sourceInstance && e.FieldName == fieldName))
        {
            entries.Add(new ReferenceEntry
            {
                SourceTemplateType = sourceType,
                SourceInstanceName = sourceInstance,
                FieldName = fieldName
            });
        }
    }

    private void AddEnhancedBacklink(
        string targetType,
        string targetInstance,
        string sourceType,
        string sourceInstance,
        string fieldName,
        ReferenceType refType,
        string? embeddedClassName,
        string? embeddedFieldName,
        int collectionIndex)
    {
        var targetKey = $"{targetType}/{targetInstance}";

        if (!_enhancedBacklinks.TryGetValue(targetKey, out var entries))
        {
            entries = new List<EnhancedReferenceEntry>();
            _enhancedBacklinks[targetKey] = entries;
        }

        // Avoid duplicates (check full path)
        var exists = entries.Any(e =>
            e.SourceTemplateType == sourceType &&
            e.SourceInstanceName == sourceInstance &&
            e.FieldName == fieldName &&
            e.CollectionIndex == collectionIndex &&
            e.EmbeddedFieldName == embeddedFieldName);

        if (!exists)
        {
            entries.Add(new EnhancedReferenceEntry
            {
                SourceTemplateType = sourceType,
                SourceInstanceName = sourceInstance,
                FieldName = fieldName,
                Type = refType,
                EmbeddedClassName = embeddedClassName,
                EmbeddedFieldName = embeddedFieldName,
                CollectionIndex = collectionIndex,
                ReferencedValue = targetInstance
            });
        }
    }

    private void AddAssetBacklink(string assetName, string sourceType, string sourceInstance, string fieldName)
    {
        if (!_assetBacklinks.TryGetValue(assetName, out var entries))
        {
            entries = new List<ReferenceEntry>();
            _assetBacklinks[assetName] = entries;
        }

        // Avoid duplicates
        if (!entries.Any(e => e.SourceTemplateType == sourceType && e.SourceInstanceName == sourceInstance && e.FieldName == fieldName))
        {
            entries.Add(new ReferenceEntry
            {
                SourceTemplateType = sourceType,
                SourceInstanceName = sourceInstance,
                FieldName = fieldName
            });
        }
    }

    /// <summary>
    /// Get all templates that reference the specified template.
    /// </summary>
    public List<ReferenceEntry> GetTemplateBacklinks(string templateType, string instanceName)
    {
        var key = $"{templateType}/{instanceName}";
        return _templateBacklinks.TryGetValue(key, out var entries)
            ? entries.ToList()
            : new List<ReferenceEntry>();
    }

    /// <summary>
    /// Get enhanced backlinks with full path information for the specified template.
    /// Useful for the cloning wizard to understand exactly where in collections a template is referenced.
    /// </summary>
    public List<EnhancedReferenceEntry> GetEnhancedBacklinks(string templateType, string instanceName)
    {
        var key = $"{templateType}/{instanceName}";
        return _enhancedBacklinks.TryGetValue(key, out var entries)
            ? entries.ToList()
            : new List<EnhancedReferenceEntry>();
    }

    /// <summary>
    /// Get only collection-based backlinks (embedded or direct) for the specified template.
    /// These are the references that might need patches when cloning.
    /// </summary>
    public List<EnhancedReferenceEntry> GetCollectionBacklinks(string templateType, string instanceName)
    {
        var key = $"{templateType}/{instanceName}";
        if (!_enhancedBacklinks.TryGetValue(key, out var entries))
            return new List<EnhancedReferenceEntry>();

        return entries
            .Where(e => e.Type == ReferenceType.CollectionDirect || e.Type == ReferenceType.CollectionEmbedded)
            .ToList();
    }

    /// <summary>
    /// Get all enhanced backlinks grouped by source template.
    /// Returns a dictionary where key is "SourceType/SourceInstance" and value is list of references.
    /// </summary>
    public Dictionary<string, List<EnhancedReferenceEntry>> GetCollectionBacklinksGrouped(string templateType, string instanceName)
    {
        var backlinks = GetCollectionBacklinks(templateType, instanceName);
        return backlinks
            .GroupBy(e => e.SourceKey)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// Get template types that COULD reference this template type based on schema analysis.
    /// Used when extracted data is incomplete and we need to show potential relationships.
    /// </summary>
    public List<SchemaRelationshipHint> GetSchemaRelationshipHints(string targetTemplateType)
    {
        return _schemaRelationshipHints.Values
            .Where(h => h.TargetTemplateType == targetTemplateType)
            .ToList();
    }

    /// <summary>
    /// Get all template instances of a given type that exist in the extracted data.
    /// </summary>
    public List<string> GetAllInstancesOfType(string templateType)
    {
        // Look through backlinks to find all instances of this template type that have been referenced
        var instances = new HashSet<string>();

        foreach (var kvp in _templateBacklinks)
        {
            var key = kvp.Key;
            var slashIdx = key.IndexOf('/');
            if (slashIdx > 0)
            {
                var type = key[..slashIdx];
                var name = key[(slashIdx + 1)..];
                if (type == templateType)
                    instances.Add(name);
            }

            // Also check source templates
            foreach (var entry in kvp.Value)
            {
                if (entry.SourceTemplateType == templateType)
                    instances.Add(entry.SourceInstanceName);
            }
        }

        return instances.ToList();
    }

    /// <summary>
    /// Check if the reference graph has complete data or is using schema-based hints.
    /// </summary>
    public bool HasCompleteData => _templateBacklinks.Count >= 50;

    /// <summary>
    /// Get a summary of why data might be incomplete.
    /// </summary>
    public string GetDataStatusMessage()
    {
        if (HasCompleteData)
            return "Reference data loaded successfully.";

        if (_schemaRelationshipHints.Count > 0)
            return $"Extracted data is incomplete. Using {_schemaRelationshipHints.Count} schema-based relationship hints. " +
                   "Consider re-running data extraction (F11 in-game) to get full reference data.";

        return "No reference data available. Run data extraction in-game (F11) first.";
    }

    /// <summary>
    /// Get all templates that reference the specified asset.
    /// </summary>
    public List<ReferenceEntry> GetAssetBacklinks(string assetName)
    {
        // Try exact match first
        if (_assetBacklinks.TryGetValue(assetName, out var entries))
            return entries.ToList();

        // Try without extension
        var nameWithoutExt = Path.GetFileNameWithoutExtension(assetName);
        if (_assetBacklinks.TryGetValue(nameWithoutExt, out entries))
            return entries.ToList();

        return new List<ReferenceEntry>();
    }

    /// <summary>
    /// Save the reference graph to disk for caching.
    /// </summary>
    public void SaveToFile(string extractedDataPath)
    {
        var filePath = Path.Combine(extractedDataPath, ReferenceFileName);

        // Convert enhanced backlinks to serializable format
        var enhancedData = new Dictionary<string, List<EnhancedReferenceEntryData>>();
        foreach (var kvp in _enhancedBacklinks)
        {
            enhancedData[kvp.Key] = kvp.Value.Select(e => new EnhancedReferenceEntryData
            {
                SourceTemplateType = e.SourceTemplateType,
                SourceInstanceName = e.SourceInstanceName,
                FieldName = e.FieldName,
                Type = (int)e.Type,
                EmbeddedClassName = e.EmbeddedClassName,
                EmbeddedFieldName = e.EmbeddedFieldName,
                CollectionIndex = e.CollectionIndex,
                ReferencedValue = e.ReferencedValue
            }).ToList();
        }

        var data = new ReferenceGraphData
        {
            Version = CurrentVersion,
            BuildDate = DateTime.UtcNow.ToString("O"),
            TemplateBacklinks = _templateBacklinks,
            AssetBacklinks = _assetBacklinks,
            EnhancedBacklinks = enhancedData
        };

        try
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            ModkitLog.Info($"[ReferenceGraphService] Saved reference graph to {filePath}");
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[ReferenceGraphService] Failed to save reference graph: {ex.Message}");
        }
    }

    /// <summary>
    /// Load the reference graph from disk cache.
    /// Returns true if loaded successfully, false if cache is missing or stale.
    /// </summary>
    public bool LoadFromFile(string extractedDataPath)
    {
        var filePath = Path.Combine(extractedDataPath, ReferenceFileName);

        if (!File.Exists(filePath))
            return false;

        try
        {
            // Check if cache is stale (older than any template file)
            var cacheTime = File.GetLastWriteTimeUtc(filePath);
            foreach (var templateFile in Directory.GetFiles(extractedDataPath, "*.json"))
            {
                if (Path.GetFileName(templateFile) == ReferenceFileName)
                    continue;

                if (File.GetLastWriteTimeUtc(templateFile) > cacheTime)
                {
                    ModkitLog.Info($"[ReferenceGraphService] Cache is stale, will rebuild");
                    return false;
                }
            }

            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<ReferenceGraphData>(json);

            if (data == null || data.Version < CurrentVersion)
            {
                ModkitLog.Info($"[ReferenceGraphService] Cache version mismatch, will rebuild");
                return false;
            }

            _templateBacklinks.Clear();
            _enhancedBacklinks.Clear();
            _assetBacklinks.Clear();

            foreach (var kvp in data.TemplateBacklinks)
                _templateBacklinks[kvp.Key] = kvp.Value;

            foreach (var kvp in data.AssetBacklinks)
                _assetBacklinks[kvp.Key] = kvp.Value;

            // Load enhanced backlinks if present (v2+)
            if (data.EnhancedBacklinks != null)
            {
                foreach (var kvp in data.EnhancedBacklinks)
                {
                    _enhancedBacklinks[kvp.Key] = kvp.Value.Select(e => new EnhancedReferenceEntry
                    {
                        SourceTemplateType = e.SourceTemplateType,
                        SourceInstanceName = e.SourceInstanceName,
                        FieldName = e.FieldName,
                        Type = (ReferenceType)e.Type,
                        EmbeddedClassName = e.EmbeddedClassName,
                        EmbeddedFieldName = e.EmbeddedFieldName,
                        CollectionIndex = e.CollectionIndex,
                        ReferencedValue = e.ReferencedValue
                    }).ToList();
                }
            }

            _isLoaded = true;
            ModkitLog.Info($"[ReferenceGraphService] Loaded reference graph from cache");
            return true;
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[ReferenceGraphService] Failed to load reference graph: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Load from cache if available, otherwise build from extracted data.
    /// </summary>
    public void LoadOrBuild(string extractedDataPath, SchemaService schemaService)
    {
        if (!LoadFromFile(extractedDataPath))
        {
            BuildFromExtractedData(extractedDataPath, schemaService);
            SaveToFile(extractedDataPath);
        }
    }
}
