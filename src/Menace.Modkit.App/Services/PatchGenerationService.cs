using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Generates JSON patches for cloning operations.
/// Produces $append and $update patches for injecting clones into collections.
/// </summary>
public class PatchGenerationService
{
    private readonly SchemaService _schemaService;
    private readonly string _vanillaDataPath;

    public PatchGenerationService(SchemaService schemaService, string vanillaDataPath)
    {
        _schemaService = schemaService;
        _vanillaDataPath = vanillaDataPath;
    }

    /// <summary>
    /// Generate an $append patch for adding a clone to a collection.
    /// Creates a new entry with the clone's name and copies other fields from the source entry.
    /// </summary>
    /// <param name="reference">The collection reference to the source template</param>
    /// <param name="cloneName">The name of the new clone</param>
    /// <returns>Patch object for the collection field, or null if generation failed</returns>
    public JsonObject? GenerateAppendPatch(
        EnhancedReferenceEntry reference,
        string cloneName)
    {
        if (reference.Type != ReferenceType.CollectionEmbedded)
        {
            // For direct collection references, just append the name
            return GenerateDirectAppendPatch(reference.FieldName, cloneName);
        }

        // For embedded collections (like ArmyEntry), we need to construct a full object
        return GenerateEmbeddedAppendPatch(reference, cloneName);
    }

    /// <summary>
    /// Generate a patch for a simple collection of template references (string[]).
    /// </summary>
    private JsonObject? GenerateDirectAppendPatch(string fieldName, string cloneName)
    {
        // Result: { "FieldName": { "$append": ["cloneName"] } }
        var appendArray = new JsonArray { cloneName };
        var fieldPatch = new JsonObject { ["$append"] = appendArray };
        return new JsonObject { [fieldName] = fieldPatch };
    }

    /// <summary>
    /// Generate a patch for an embedded class collection (e.g., ArmyTemplate.Entries).
    /// Copies the source entry structure and replaces the template reference with the clone.
    /// </summary>
    private JsonObject? GenerateEmbeddedAppendPatch(
        EnhancedReferenceEntry reference,
        string cloneName)
    {
        var embeddedClassName = reference.EmbeddedClassName;
        var embeddedFieldName = reference.EmbeddedFieldName;

        if (string.IsNullOrEmpty(embeddedClassName) || string.IsNullOrEmpty(embeddedFieldName))
            return null;

        // Get the embedded class schema to know what fields to include
        var embeddedFields = _schemaService.GetAllEmbeddedClassFields(embeddedClassName);
        if (embeddedFields.Count == 0)
            return null;

        // Load the source entry from vanilla data to copy its values
        var sourceEntry = LoadSourceEntry(reference);

        // Build new entry object with clone template
        var newEntry = new JsonObject();

        foreach (var field in embeddedFields)
        {
            if (field.Name == embeddedFieldName)
            {
                // This is the template reference field - use the clone name
                newEntry[field.Name] = cloneName;
            }
            else if (sourceEntry != null && sourceEntry.TryGetPropertyValue(field.Name, out var sourceValue))
            {
                // Copy the value from source entry
                newEntry[field.Name] = sourceValue?.DeepClone();
            }
            else
            {
                // Use default value for this field type
                newEntry[field.Name] = GetDefaultValueNode(field);
            }
        }

        // Build the final patch structure
        // Result: { "Entries": { "$append": [{ "Template": "clone", "Amount": 1, ... }] } }
        var appendArray = new JsonArray { newEntry };
        var fieldPatch = new JsonObject { ["$append"] = appendArray };
        return new JsonObject { [reference.FieldName] = fieldPatch };
    }

    /// <summary>
    /// Generate a $update patch for replacing a source template with a clone at a specific index.
    /// </summary>
    public JsonObject? GenerateReplacePatch(
        EnhancedReferenceEntry reference,
        string cloneName)
    {
        if (reference.CollectionIndex < 0)
            return null;

        if (reference.Type == ReferenceType.CollectionEmbedded)
        {
            // Update the Template field on the embedded object at this index
            // Result: { "Entries": { "$update": { "3": { "Template": "clone_name" } } } }
            var fieldUpdate = new JsonObject { [reference.EmbeddedFieldName!] = cloneName };
            var indexUpdate = new JsonObject { [reference.CollectionIndex.ToString()] = fieldUpdate };
            var fieldPatch = new JsonObject { ["$update"] = indexUpdate };
            return new JsonObject { [reference.FieldName] = fieldPatch };
        }
        else
        {
            // For direct collections, update the element at this index
            // Result: { "Skills": { "$update": { "3": "clone_name" } } }
            var indexUpdate = new JsonObject { [reference.CollectionIndex.ToString()] = cloneName };
            var fieldPatch = new JsonObject { ["$update"] = indexUpdate };
            return new JsonObject { [reference.FieldName] = fieldPatch };
        }
    }

    /// <summary>
    /// Load the source entry from vanilla data to copy its structure.
    /// </summary>
    private JsonObject? LoadSourceEntry(EnhancedReferenceEntry reference)
    {
        if (string.IsNullOrEmpty(_vanillaDataPath))
            return null;

        var templateFile = Path.Combine(_vanillaDataPath, $"{reference.SourceTemplateType}.json");
        if (!File.Exists(templateFile))
            return null;

        try
        {
            var json = File.ReadAllText(templateFile);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            // Find the source template
            foreach (var template in doc.RootElement.EnumerateArray())
            {
                if (template.ValueKind != JsonValueKind.Object)
                    continue;

                if (template.TryGetProperty("name", out var nameProp) &&
                    nameProp.GetString() == reference.SourceInstanceName)
                {
                    // Found the source template - now find the collection entry
                    if (template.TryGetProperty(reference.FieldName, out var collection) &&
                        collection.ValueKind == JsonValueKind.Array &&
                        reference.CollectionIndex >= 0 &&
                        reference.CollectionIndex < collection.GetArrayLength())
                    {
                        var entry = collection[reference.CollectionIndex];
                        if (entry.ValueKind == JsonValueKind.Object)
                        {
                            return JsonNode.Parse(entry.GetRawText())?.AsObject();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[PatchGenerationService] Failed to load source entry: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Get a default JSON value node for a schema field.
    /// </summary>
    private static JsonNode? GetDefaultValueNode(SchemaService.FieldMeta field)
    {
        return field.Category switch
        {
            "primitive" => field.Type.ToLowerInvariant() switch
            {
                "int" or "int32" => JsonValue.Create(0),
                "float" or "single" => JsonValue.Create(0.0f),
                "bool" or "boolean" => JsonValue.Create(false),
                "string" => JsonValue.Create(""),
                _ => null
            },
            "string" => JsonValue.Create(""),
            "enum" => JsonValue.Create(0),
            "reference" => JsonValue.Create(""),
            "collection" => new JsonArray(),
            _ => null
        };
    }

    /// <summary>
    /// Merge multiple patch objects into a single patch for one template instance.
    /// </summary>
    public static JsonObject MergePatches(IEnumerable<JsonObject> patches)
    {
        var result = new JsonObject();

        foreach (var patch in patches)
        {
            foreach (var kvp in patch)
            {
                var fieldName = kvp.Key;
                var fieldValue = kvp.Value;

                if (!result.ContainsKey(fieldName))
                {
                    result[fieldName] = fieldValue?.DeepClone();
                }
                else
                {
                    // Need to merge field patches (e.g., combine $append arrays)
                    MergeFieldPatch(result[fieldName]!.AsObject(), fieldValue?.AsObject());
                }
            }
        }

        return result;
    }

    private static void MergeFieldPatch(JsonObject existing, JsonObject? addition)
    {
        if (addition == null)
            return;

        // Merge $append arrays
        if (addition.TryGetPropertyValue("$append", out var appendAdd) &&
            appendAdd is JsonArray appendAddArray)
        {
            if (existing.TryGetPropertyValue("$append", out var appendExisting) &&
                appendExisting is JsonArray appendExistingArray)
            {
                foreach (var item in appendAddArray)
                    appendExistingArray.Add(item?.DeepClone());
            }
            else
            {
                existing["$append"] = appendAdd.DeepClone();
            }
        }

        // Merge $update objects
        if (addition.TryGetPropertyValue("$update", out var updateAdd) &&
            updateAdd is JsonObject updateAddObj)
        {
            if (existing.TryGetPropertyValue("$update", out var updateExisting) &&
                updateExisting is JsonObject updateExistingObj)
            {
                foreach (var kvp in updateAddObj)
                    updateExistingObj[kvp.Key] = kvp.Value?.DeepClone();
            }
            else
            {
                existing["$update"] = updateAdd.DeepClone();
            }
        }

        // Merge $remove arrays
        if (addition.TryGetPropertyValue("$remove", out var removeAdd) &&
            removeAdd is JsonArray removeAddArray)
        {
            if (existing.TryGetPropertyValue("$remove", out var removeExisting) &&
                removeExisting is JsonArray removeExistingArray)
            {
                foreach (var item in removeAddArray)
                    removeExistingArray.Add(item?.DeepClone());
            }
            else
            {
                existing["$remove"] = removeAdd.DeepClone();
            }
        }
    }

    /// <summary>
    /// Format a patch as JSON string for storage.
    /// </summary>
    public static string FormatPatch(JsonObject patch)
    {
        return patch.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
