using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Builds and queries a reverse reference graph for templates and assets.
/// Enables "What Links Here" functionality by tracking which templates reference
/// other templates or assets.
/// </summary>
public class ReferenceGraphService
{
    private const string ReferenceFileName = "references.json";
    private const int CurrentVersion = 1;

    // Template backlinks: "TemplateType/InstanceName" → list of references
    private readonly Dictionary<string, List<ReferenceEntry>> _templateBacklinks = new(StringComparer.Ordinal);

    // Asset backlinks: "assetName" → list of template references
    private readonly Dictionary<string, List<ReferenceEntry>> _assetBacklinks = new(StringComparer.OrdinalIgnoreCase);

    // Set of known template types for reference detection
    private readonly HashSet<string> _knownTemplateTypes = new(StringComparer.Ordinal);

    private bool _isLoaded;

    public bool IsLoaded => _isLoaded;

    /// <summary>
    /// Build the reference graph from extracted template data.
    /// Scans all template JSON files and schema to find references.
    /// </summary>
    public void BuildFromExtractedData(string extractedDataPath, SchemaService schemaService)
    {
        _templateBacklinks.Clear();
        _assetBacklinks.Clear();
        _knownTemplateTypes.Clear();
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
        ModkitLog.Info($"[ReferenceGraphService] Built graph with {_templateBacklinks.Count} template backlinks, {_assetBacklinks.Count} asset backlinks");
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
                }
            }
        }
        // Handle collection of template references
        else if (fieldMeta?.Category == "collection" && !string.IsNullOrEmpty(fieldMeta.ElementType))
        {
            if (_knownTemplateTypes.Contains(fieldMeta.ElementType) && value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                {
                    var refValue = ExtractStringValue(item);
                    if (!string.IsNullOrEmpty(refValue))
                    {
                        AddTemplateBacklink(fieldMeta.ElementType, refValue, templateType, instanceName, fieldName);
                    }
                }
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
                foreach (var item in value.EnumerateArray())
                {
                    var refValue = ExtractStringValue(item);
                    if (!string.IsNullOrEmpty(refValue))
                    {
                        AddTemplateBacklink(possibleElementType, refValue, templateType, instanceName, fieldName);
                    }
                }
            }
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

        var data = new ReferenceGraphData
        {
            Version = CurrentVersion,
            BuildDate = DateTime.UtcNow.ToString("O"),
            TemplateBacklinks = _templateBacklinks,
            AssetBacklinks = _assetBacklinks
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
            _assetBacklinks.Clear();

            foreach (var kvp in data.TemplateBacklinks)
                _templateBacklinks[kvp.Key] = kvp.Value;

            foreach (var kvp in data.AssetBacklinks)
                _assetBacklinks[kvp.Key] = kvp.Value;

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
