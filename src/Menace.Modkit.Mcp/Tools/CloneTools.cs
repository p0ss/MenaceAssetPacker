using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.Mcp.Tools;

/// <summary>
/// MCP tools for template cloning wizard operations.
/// These tools provide programmatic access to the cloning wizard functionality.
/// </summary>
[McpServerToolType]
public static class CloneTools
{
    // Fields that should not be copied when using CopyAllProperties
    private static readonly HashSet<string> NonCopyableFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "m_ID", "m_IsGarbage", "m_IsInitialized", "m_CachedPtr",
        "DisplayTitle", "DisplayShortName", "DisplayDescription",
        "HasIcon", "IconAssetName", // Computed from Icon property
        "Pointer", "ObjectClass", "WasCollected", "hideFlags", "serializationData"
    };

    // Schema field -> extracted field name mappings for asset fields
    private static readonly Dictionary<string, string> AssetFieldMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Icon", "IconAssetName" }
    };

    [McpServerTool(Name = "clone_analyze", ReadOnly = true)]
    [Description("Analyze a template instance to discover asset dependencies before cloning. Returns asset fields and their current values.")]
    public static string CloneAnalyze(
        ModpackManager modpackManager,
        SchemaService schemaService,
        [Description("The template type (e.g., 'WeaponTemplate')")] string type,
        [Description("The source instance to analyze (e.g., 'weapon.laser_rifle')")] string source)
    {
        var vanillaPath = modpackManager.GetVanillaTemplatePath(type);
        if (vanillaPath == null)
        {
            return JsonSerializer.Serialize(new { error = $"Template type '{type}' not found" }, JsonOptions);
        }

        try
        {
            // Load vanilla data
            var json = File.ReadAllText(vanillaPath);
            using var doc = JsonDocument.Parse(json);

            JsonElement? sourceTemplate = null;

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("name", out var nameProp) &&
                        nameProp.GetString()?.Equals(source, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        sourceTemplate = element;
                        break;
                    }
                }
            }

            if (sourceTemplate == null)
            {
                return JsonSerializer.Serialize(new { error = $"Instance '{source}' not found in '{type}'" }, JsonOptions);
            }

            // Find asset fields using schema
            var assetFields = schemaService.GetAssetFields(type);
            var assetDependencies = new List<object>();

            foreach (var field in assetFields)
            {
                string? assetValue = null;
                string extractedFieldName = field.Name;

                // Try schema field name first
                if (sourceTemplate.Value.TryGetProperty(field.Name, out var schemaValue) &&
                    schemaValue.ValueKind == JsonValueKind.String)
                {
                    assetValue = schemaValue.GetString();
                }
                // Try extracted field name (e.g., IconAssetName for Icon)
                else if (AssetFieldMappings.TryGetValue(field.Name, out var mappedName))
                {
                    if (sourceTemplate.Value.TryGetProperty(mappedName, out var mappedValue) &&
                        mappedValue.ValueKind == JsonValueKind.String)
                    {
                        assetValue = mappedValue.GetString();
                        extractedFieldName = mappedName;
                    }
                }

                if (!string.IsNullOrEmpty(assetValue) &&
                    !assetValue.StartsWith("(") && !assetValue.EndsWith(")"))
                {
                    var category = InferAssetCategory(field.Type);
                    assetDependencies.Add(new
                    {
                        schemaField = field.Name,
                        extractedField = extractedFieldName,
                        category,
                        currentValue = assetValue,
                        fieldType = field.Type
                    });
                }
            }

            // Count total properties
            int propertyCount = 0;
            foreach (var _ in sourceTemplate.Value.EnumerateObject())
                propertyCount++;

            return JsonSerializer.Serialize(new
            {
                type,
                source,
                propertyCount,
                assetDependencies,
                assetCount = assetDependencies.Count,
                suggestedCloneName = GenerateSuggestedName(source)
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "clone_create", Destructive = false)]
    [Description("Create a clone of a template instance with full control over property copying and asset handling.")]
    public static string CloneCreate(
        ModpackManager modpackManager,
        SchemaService schemaService,
        [Description("The modpack to add the clone to")] string modpack,
        [Description("The template type (e.g., 'WeaponTemplate')")] string type,
        [Description("The source instance to clone")] string source,
        [Description("The name for the new clone")] string cloneName,
        [Description("If true, copy all property values from source (default true)")] bool copyAllProperties = true,
        [Description("JSON object mapping asset schema field names to new asset names, e.g., {\"Icon\": \"my_custom_icon\"}")] string? assetOverrides = null)
    {
        // Verify modpack exists
        var modpacks = modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(modpack, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{modpack}' not found" }, JsonOptions);
        }

        // Verify source exists
        var vanillaPath = modpackManager.GetVanillaTemplatePath(type);
        if (vanillaPath == null)
        {
            return JsonSerializer.Serialize(new { error = $"Template type '{type}' not found" }, JsonOptions);
        }

        try
        {
            // Load vanilla data
            var json = File.ReadAllText(vanillaPath);
            using var doc = JsonDocument.Parse(json);

            JsonElement? sourceTemplate = null;

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("name", out var nameProp) &&
                        nameProp.GetString()?.Equals(source, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        sourceTemplate = element;
                        break;
                    }
                }
            }

            if (sourceTemplate == null)
            {
                return JsonSerializer.Serialize(new { error = $"Instance '{source}' not found" }, JsonOptions);
            }

            // Parse asset overrides
            Dictionary<string, string>? assetOverridesDict = null;
            if (!string.IsNullOrEmpty(assetOverrides))
            {
                assetOverridesDict = JsonSerializer.Deserialize<Dictionary<string, string>>(assetOverrides);
            }

            // 1. Create clone definition
            var clones = modpackManager.LoadStagingClones(modpack);
            if (!clones.TryGetValue(type, out var typeClones))
            {
                typeClones = new Dictionary<string, string>();
                clones[type] = typeClones;
            }
            typeClones[cloneName] = source;
            var clonesJson = JsonSerializer.Serialize(typeClones, JsonOptions);
            modpackManager.SaveStagingClones(modpack, type, clonesJson);

            // 2. Copy properties if requested
            int propertiesCopied = 0;
            var patchedFields = new List<string>();

            if (copyAllProperties)
            {
                var stagingPath = modpackManager.GetStagingTemplatePath(modpack, type);
                var existingData = new Dictionary<string, Dictionary<string, JsonElement>>();

                if (stagingPath != null)
                {
                    var existingJson = File.ReadAllText(stagingPath);
                    existingData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(existingJson)
                        ?? new Dictionary<string, Dictionary<string, JsonElement>>();
                }

                var instancePatches = new Dictionary<string, JsonElement>();

                foreach (var prop in sourceTemplate.Value.EnumerateObject())
                {
                    // Skip non-copyable fields
                    if (NonCopyableFields.Contains(prop.Name))
                        continue;

                    instancePatches[prop.Name] = prop.Value.Clone();
                    patchedFields.Add(prop.Name);
                    propertiesCopied++;
                }

                existingData[cloneName] = instancePatches;

                var outputJson = JsonSerializer.Serialize(existingData, JsonOptions);
                modpackManager.SaveStagingTemplate(modpack, type, outputJson);
            }

            // 3. Apply asset overrides
            int assetsOverridden = 0;
            if (assetOverridesDict != null && assetOverridesDict.Count > 0)
            {
                var stagingPath = modpackManager.GetStagingTemplatePath(modpack, type);
                var existingData = new Dictionary<string, Dictionary<string, JsonElement>>();

                if (stagingPath != null)
                {
                    var existingJson = File.ReadAllText(stagingPath);
                    existingData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(existingJson)
                        ?? new Dictionary<string, Dictionary<string, JsonElement>>();
                }

                if (!existingData.TryGetValue(cloneName, out var instancePatches))
                {
                    instancePatches = new Dictionary<string, JsonElement>();
                    existingData[cloneName] = instancePatches;
                }

                foreach (var (schemaField, newAssetName) in assetOverridesDict)
                {
                    // Set the schema field (e.g., "Icon") with the new asset name
                    // The game's template injection will resolve this by name
                    instancePatches[schemaField] = JsonSerializer.SerializeToElement(newAssetName);
                    assetsOverridden++;

                    if (!patchedFields.Contains(schemaField))
                        patchedFields.Add(schemaField);
                }

                var outputJson = JsonSerializer.Serialize(existingData, JsonOptions);
                modpackManager.SaveStagingTemplate(modpack, type, outputJson);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                modpack,
                type,
                clone = new
                {
                    name = cloneName,
                    source
                },
                propertiesCopied,
                assetsOverridden,
                patchedFields
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "clone_set_asset", Destructive = false)]
    [Description("Set an asset field on a clone to use a custom asset. The asset must be in the modpack's assets folder.")]
    public static string CloneSetAsset(
        ModpackManager modpackManager,
        [Description("The modpack containing the clone")] string modpack,
        [Description("The template type")] string type,
        [Description("The clone instance name")] string instance,
        [Description("The schema field name for the asset (e.g., 'Icon', 'IconEquipment')")] string field,
        [Description("The asset name to set (without extension)")] string assetName)
    {
        // Verify modpack exists
        var modpacks = modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(modpack, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{modpack}' not found" }, JsonOptions);
        }

        try
        {
            // Load existing staging data
            var stagingPath = modpackManager.GetStagingTemplatePath(modpack, type);
            var existingData = new Dictionary<string, Dictionary<string, JsonElement>>();

            if (stagingPath != null)
            {
                var existingJson = File.ReadAllText(stagingPath);
                existingData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(existingJson)
                    ?? new Dictionary<string, Dictionary<string, JsonElement>>();
            }

            if (!existingData.TryGetValue(instance, out var instancePatches))
            {
                instancePatches = new Dictionary<string, JsonElement>();
                existingData[instance] = instancePatches;
            }

            // Set the asset field
            instancePatches[field] = JsonSerializer.SerializeToElement(assetName);

            // Save
            var outputJson = JsonSerializer.Serialize(existingData, JsonOptions);
            modpackManager.SaveStagingTemplate(modpack, type, outputJson);

            return JsonSerializer.Serialize(new
            {
                success = true,
                modpack,
                type,
                instance,
                field,
                assetName,
                message = $"Set {field} = {assetName}"
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "clone_list", ReadOnly = true)]
    [Description("List all clone definitions in a modpack.")]
    public static string CloneList(
        ModpackManager modpackManager,
        [Description("The modpack name")] string modpack,
        [Description("Optional template type filter")] string? type = null)
    {
        var modpacks = modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(modpack, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{modpack}' not found" }, JsonOptions);
        }

        try
        {
            var clones = modpackManager.LoadStagingClones(modpack);

            if (!string.IsNullOrEmpty(type))
            {
                if (clones.TryGetValue(type, out var typeClones))
                {
                    return JsonSerializer.Serialize(new
                    {
                        modpack,
                        type,
                        clones = typeClones.Select(kvp => new { name = kvp.Key, source = kvp.Value }),
                        count = typeClones.Count
                    }, JsonOptions);
                }
                else
                {
                    return JsonSerializer.Serialize(new
                    {
                        modpack,
                        type,
                        clones = Array.Empty<object>(),
                        count = 0
                    }, JsonOptions);
                }
            }

            // Return all clones grouped by type
            var allClones = clones.SelectMany(kvp =>
                kvp.Value.Select(c => new { type = kvp.Key, name = c.Key, source = c.Value }));

            return JsonSerializer.Serialize(new
            {
                modpack,
                clones = allClones,
                count = allClones.Count(),
                byType = clones.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count)
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "clone_delete", Destructive = true)]
    [Description("Delete a clone definition and optionally its patches.")]
    public static string CloneDelete(
        ModpackManager modpackManager,
        [Description("The modpack name")] string modpack,
        [Description("The template type")] string type,
        [Description("The clone name to delete")] string cloneName,
        [Description("If true, also delete any patches for this clone (default true)")] bool deletePatches = true)
    {
        var modpacks = modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(modpack, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{modpack}' not found" }, JsonOptions);
        }

        try
        {
            // Remove clone definition
            var clones = modpackManager.LoadStagingClones(modpack);
            bool cloneRemoved = false;

            if (clones.TryGetValue(type, out var typeClones) && typeClones.ContainsKey(cloneName))
            {
                typeClones.Remove(cloneName);
                cloneRemoved = true;

                if (typeClones.Count == 0)
                {
                    modpackManager.DeleteStagingClones(modpack, type);
                }
                else
                {
                    var clonesJson = JsonSerializer.Serialize(typeClones, JsonOptions);
                    modpackManager.SaveStagingClones(modpack, type, clonesJson);
                }
            }

            // Remove patches if requested
            bool patchesRemoved = false;
            if (deletePatches)
            {
                var stagingPath = modpackManager.GetStagingTemplatePath(modpack, type);
                if (stagingPath != null)
                {
                    var json = File.ReadAllText(stagingPath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(json);

                    if (data != null && data.ContainsKey(cloneName))
                    {
                        data.Remove(cloneName);
                        patchesRemoved = true;

                        if (data.Count == 0)
                        {
                            modpackManager.DeleteStagingTemplate(modpack, type);
                        }
                        else
                        {
                            var outputJson = JsonSerializer.Serialize(data, JsonOptions);
                            modpackManager.SaveStagingTemplate(modpack, type, outputJson);
                        }
                    }
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = cloneRemoved || patchesRemoved,
                cloneRemoved,
                patchesRemoved,
                message = cloneRemoved
                    ? $"Deleted clone '{cloneName}'" + (patchesRemoved ? " and its patches" : "")
                    : $"Clone '{cloneName}' not found"
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    private static string GenerateSuggestedName(string sourceName)
    {
        var lastDot = sourceName.LastIndexOf('.');
        if (lastDot >= 0)
        {
            var prefix = sourceName[..lastDot];
            var suffix = sourceName[(lastDot + 1)..];
            return $"{prefix}.{suffix}_clone";
        }
        return $"{sourceName}_clone";
    }

    private static string InferAssetCategory(string fieldType)
    {
        var lower = fieldType.ToLowerInvariant();
        if (lower.Contains("sprite")) return "sprite";
        if (lower.Contains("texture")) return "texture";
        if (lower.Contains("mesh") || lower.Contains("model") || lower.Contains("skinned")) return "mesh";
        if (lower.Contains("prefab") || lower.Contains("gameobject")) return "prefab";
        if (lower.Contains("audio") || lower.Contains("sound")) return "audio";
        if (lower.Contains("material")) return "material";
        return "asset";
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
