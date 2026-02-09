using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.Mcp.Tools;

/// <summary>
/// MCP tools for template (game data) operations.
/// </summary>
[McpServerToolType]
public static class TemplateTools
{
    [McpServerTool(Name = "template_types", ReadOnly = true)]
    [Description("List all available template types from the vanilla game data. Template types represent game entities like weapons, units, buildings, etc.")]
    public static string TemplateTypes(ModpackManager modpackManager)
    {
        var vanillaPath = modpackManager.VanillaDataPath;
        if (string.IsNullOrEmpty(vanillaPath) || !Directory.Exists(vanillaPath))
        {
            return JsonSerializer.Serialize(new
            {
                error = "Vanilla data not found. Extract game data first.",
                hint = "Use the Menace Modkit app to extract game data, or set the game install path."
            }, JsonOptions);
        }

        var types = Directory.GetFiles(vanillaPath, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(t => t)
            .ToList();

        return JsonSerializer.Serialize(new
        {
            templateTypes = types,
            count = types.Count,
            vanillaDataPath = vanillaPath
        }, JsonOptions);
    }

    [McpServerTool(Name = "template_list", ReadOnly = true)]
    [Description("List all instances (entries) of a specific template type. For example, listing all weapons or all units.")]
    public static string TemplateList(
        ModpackManager modpackManager,
        [Description("The template type to list (e.g., 'WeaponTemplate', 'UnitTemplate')")] string type,
        [Description("Filter instances by name pattern (optional)")] string? filter = null)
    {
        var vanillaPath = modpackManager.GetVanillaTemplatePath(type);
        if (vanillaPath == null)
        {
            return JsonSerializer.Serialize(new { error = $"Template type '{type}' not found" }, JsonOptions);
        }

        try
        {
            var json = File.ReadAllText(vanillaPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (data == null)
            {
                return JsonSerializer.Serialize(new { error = "Failed to parse template data" }, JsonOptions);
            }

            var instances = data.Keys.AsEnumerable();
            if (!string.IsNullOrEmpty(filter))
            {
                instances = instances.Where(k =>
                    k.Contains(filter, StringComparison.OrdinalIgnoreCase));
            }

            var list = instances.OrderBy(k => k).ToList();

            return JsonSerializer.Serialize(new
            {
                type,
                instances = list,
                count = list.Count,
                totalCount = data.Count
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "template_get", ReadOnly = true)]
    [Description("Get the data for a specific template instance. Returns vanilla data merged with any modpack overrides if a modpack is specified.")]
    public static string TemplateGet(
        ModpackManager modpackManager,
        [Description("The template type (e.g., 'WeaponTemplate')")] string type,
        [Description("The instance name (e.g., 'mod_weapon.heavy.cannon_long')")] string instance,
        [Description("Modpack name to get modded version (optional, returns vanilla if not specified)")] string? modpack = null)
    {
        // Load vanilla data
        var vanillaPath = modpackManager.GetVanillaTemplatePath(type);
        if (vanillaPath == null)
        {
            return JsonSerializer.Serialize(new { error = $"Template type '{type}' not found" }, JsonOptions);
        }

        try
        {
            var vanillaJson = File.ReadAllText(vanillaPath);
            var vanillaData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(vanillaJson);
            if (vanillaData == null || !vanillaData.TryGetValue(instance, out var vanillaInstance))
            {
                return JsonSerializer.Serialize(new { error = $"Instance '{instance}' not found in '{type}'" }, JsonOptions);
            }

            // If no modpack specified, return vanilla
            if (string.IsNullOrEmpty(modpack))
            {
                return JsonSerializer.Serialize(new
                {
                    type,
                    instance,
                    source = "vanilla",
                    data = vanillaInstance
                }, JsonOptions);
            }

            // Try to load modpack overrides
            var stagingPath = modpackManager.GetStagingTemplatePath(modpack, type);
            if (stagingPath == null)
            {
                return JsonSerializer.Serialize(new
                {
                    type,
                    instance,
                    modpack,
                    source = "vanilla",
                    hasOverride = false,
                    data = vanillaInstance
                }, JsonOptions);
            }

            var stagingJson = File.ReadAllText(stagingPath);
            var stagingData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(stagingJson);

            if (stagingData == null || !stagingData.TryGetValue(instance, out var overrides))
            {
                return JsonSerializer.Serialize(new
                {
                    type,
                    instance,
                    modpack,
                    source = "vanilla",
                    hasOverride = false,
                    data = vanillaInstance
                }, JsonOptions);
            }

            // Merge vanilla with overrides
            var merged = MergeJsonElements(vanillaInstance, overrides);

            return JsonSerializer.Serialize(new
            {
                type,
                instance,
                modpack,
                source = "merged",
                hasOverride = true,
                overriddenFields = overrides.Keys.ToList(),
                data = merged
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "template_set_field", Destructive = false)]
    [Description("Set a single field value on a template instance. This is the primary way to modify game data.")]
    public static string TemplateSetField(
        ModpackManager modpackManager,
        [Description("The modpack to modify")] string modpack,
        [Description("The template type (e.g., 'WeaponTemplate')")] string type,
        [Description("The instance name (e.g., 'mod_weapon.heavy.cannon_long')")] string instance,
        [Description("The field name to set (e.g., 'Damage', 'Range', 'Properties.Armor')")] string field,
        [Description("The value to set (number, string, boolean, or JSON for complex values)")] string value)
    {
        // Verify modpack exists
        var modpacks = modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(modpack, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{modpack}' not found" }, JsonOptions);
        }

        // Verify template type exists
        var vanillaPath = modpackManager.GetVanillaTemplatePath(type);
        if (vanillaPath == null)
        {
            return JsonSerializer.Serialize(new { error = $"Template type '{type}' not found" }, JsonOptions);
        }

        // Verify instance exists
        var vanillaJson = File.ReadAllText(vanillaPath);
        var vanillaData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(vanillaJson);
        if (vanillaData == null || !vanillaData.ContainsKey(instance))
        {
            return JsonSerializer.Serialize(new { error = $"Instance '{instance}' not found in '{type}'" }, JsonOptions);
        }

        try
        {
            // Parse the value - try as JSON first, then as primitives
            JsonElement parsedValue;
            try
            {
                parsedValue = JsonSerializer.Deserialize<JsonElement>(value);
            }
            catch
            {
                // If not valid JSON, treat as string
                parsedValue = JsonSerializer.Deserialize<JsonElement>($"\"{value.Replace("\"", "\\\"")}\"");
            }

            // Load existing staging data
            var stagingPath = modpackManager.GetStagingTemplatePath(modpack, type);
            var existingData = new Dictionary<string, Dictionary<string, JsonElement>>();

            if (stagingPath != null)
            {
                var existingJson = File.ReadAllText(stagingPath);
                existingData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(existingJson)
                    ?? new Dictionary<string, Dictionary<string, JsonElement>>();
            }

            // Set the field
            if (!existingData.TryGetValue(instance, out var instancePatches))
            {
                instancePatches = new Dictionary<string, JsonElement>();
                existingData[instance] = instancePatches;
            }

            instancePatches[field] = parsedValue;

            // Save
            var outputJson = JsonSerializer.Serialize(existingData, JsonOptions);
            modpackManager.SaveStagingTemplate(modpack, type, outputJson);

            // Get the vanilla value for comparison
            object? vanillaValue = null;
            if (vanillaData.TryGetValue(instance, out var vanillaInstance))
            {
                var vanillaObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(vanillaInstance.GetRawText());
                if (vanillaObj != null && vanillaObj.TryGetValue(field, out var vv))
                {
                    vanillaValue = vv.ValueKind switch
                    {
                        JsonValueKind.Number => vv.TryGetInt32(out var i) ? i : vv.GetDouble(),
                        JsonValueKind.String => vv.GetString(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => vv.ToString()
                    };
                }
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                modpack,
                type,
                instance,
                field,
                newValue = parsedValue,
                vanillaValue,
                message = $"Set {field} = {value}"
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "template_set_fields", Destructive = false)]
    [Description("Set multiple field values on a template instance at once.")]
    public static string TemplateSetFields(
        ModpackManager modpackManager,
        [Description("The modpack to modify")] string modpack,
        [Description("The template type")] string type,
        [Description("The instance name")] string instance,
        [Description("Field names to set")] string[] fields,
        [Description("Values to set (same order as fields)")] string[] values)
    {
        if (fields.Length != values.Length)
        {
            return JsonSerializer.Serialize(new { error = "fields and values arrays must have the same length" }, JsonOptions);
        }

        var results = new List<JsonElement>();
        bool allSuccess = true;
        for (int i = 0; i < fields.Length; i++)
        {
            var result = TemplateSetField(modpackManager, modpack, type, instance, fields[i], values[i]);
            var parsed = JsonSerializer.Deserialize<JsonElement>(result);
            results.Add(parsed);
            if (!parsed.TryGetProperty("success", out var s) || !s.GetBoolean())
                allSuccess = false;
        }

        return JsonSerializer.Serialize(new
        {
            success = allSuccess,
            modpack,
            type,
            instance,
            fieldsSet = fields,
            results
        }, JsonOptions);
    }

    [McpServerTool(Name = "template_patch", Destructive = false)]
    [Description("Apply multiple field overrides to a template instance using a JSON object. Use template_set_field for single fields.")]
    public static string TemplatePatch(
        ModpackManager modpackManager,
        [Description("The modpack to modify")] string modpack,
        [Description("The template type (e.g., 'WeaponTemplate')")] string type,
        [Description("The instance name to patch")] string instance,
        [Description("JSON object containing the fields to override (e.g., {\"Damage\": 100, \"Range\": 5})")] string patches)
    {
        // Verify modpack exists
        var modpacks = modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(modpack, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{modpack}' not found" }, JsonOptions);
        }

        // Verify template type exists
        var vanillaPath = modpackManager.GetVanillaTemplatePath(type);
        if (vanillaPath == null)
        {
            return JsonSerializer.Serialize(new { error = $"Template type '{type}' not found" }, JsonOptions);
        }

        // Verify instance exists in vanilla
        var vanillaJson = File.ReadAllText(vanillaPath);
        var vanillaData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(vanillaJson);
        if (vanillaData == null || !vanillaData.ContainsKey(instance))
        {
            return JsonSerializer.Serialize(new { error = $"Instance '{instance}' not found in '{type}'" }, JsonOptions);
        }

        try
        {
            // Parse the patches
            var patchData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(patches);
            if (patchData == null || patchData.Count == 0)
            {
                return JsonSerializer.Serialize(new { error = "No valid patches provided" }, JsonOptions);
            }

            // Load existing staging data
            var stagingPath = modpackManager.GetStagingTemplatePath(modpack, type);
            var existingData = new Dictionary<string, Dictionary<string, JsonElement>>();

            if (stagingPath != null)
            {
                var existingJson = File.ReadAllText(stagingPath);
                existingData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(existingJson)
                    ?? new Dictionary<string, Dictionary<string, JsonElement>>();
            }

            // Merge patches
            if (!existingData.TryGetValue(instance, out var instancePatches))
            {
                instancePatches = new Dictionary<string, JsonElement>();
                existingData[instance] = instancePatches;
            }

            foreach (var (key, value) in patchData)
            {
                instancePatches[key] = value;
            }

            // Save
            var outputJson = JsonSerializer.Serialize(existingData, JsonOptions);
            modpackManager.SaveStagingTemplate(modpack, type, outputJson);

            return JsonSerializer.Serialize(new
            {
                success = true,
                modpack,
                type,
                instance,
                patchedFields = patchData.Keys.ToList(),
                totalOverriddenFields = instancePatches.Keys.ToList()
            }, JsonOptions);
        }
        catch (JsonException ex)
        {
            return JsonSerializer.Serialize(new { error = $"Invalid JSON in patches: {ex.Message}" }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "template_reset", Destructive = true)]
    [Description("Remove all overrides for a template instance in a modpack, reverting it to vanilla values.")]
    public static string TemplateReset(
        ModpackManager modpackManager,
        [Description("The modpack to modify")] string modpack,
        [Description("The template type")] string type,
        [Description("The instance name to reset")] string instance)
    {
        var stagingPath = modpackManager.GetStagingTemplatePath(modpack, type);
        if (stagingPath == null)
        {
            return JsonSerializer.Serialize(new
            {
                success = true,
                message = "No overrides exist for this template type"
            }, JsonOptions);
        }

        try
        {
            var json = File.ReadAllText(stagingPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(json);

            if (data == null || !data.ContainsKey(instance))
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = $"No overrides exist for instance '{instance}'"
                }, JsonOptions);
            }

            data.Remove(instance);

            if (data.Count == 0)
            {
                // Delete the file if no more overrides
                modpackManager.DeleteStagingTemplate(modpack, type);
            }
            else
            {
                var outputJson = JsonSerializer.Serialize(data, JsonOptions);
                modpackManager.SaveStagingTemplate(modpack, type, outputJson);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"Reset '{instance}' to vanilla values"
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "template_clone", Destructive = false)]
    [Description("Create a clone definition for a template instance. The clone will be a copy of the source with a new name, which can then be patched independently.")]
    public static string TemplateClone(
        ModpackManager modpackManager,
        [Description("The modpack to add the clone to")] string modpack,
        [Description("The template type")] string type,
        [Description("The source instance to clone")] string source,
        [Description("The name for the new cloned instance")] string newName)
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

        var vanillaJson = File.ReadAllText(vanillaPath);
        var vanillaData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(vanillaJson);
        if (vanillaData == null || !vanillaData.ContainsKey(source))
        {
            return JsonSerializer.Serialize(new { error = $"Source instance '{source}' not found" }, JsonOptions);
        }

        try
        {
            // Load existing clones
            var clones = modpackManager.LoadStagingClones(modpack);
            if (!clones.TryGetValue(type, out var typeClones))
            {
                typeClones = new Dictionary<string, string>();
                clones[type] = typeClones;
            }

            // Add the new clone
            typeClones[newName] = source;

            // Save
            var clonesJson = JsonSerializer.Serialize(typeClones, JsonOptions);
            modpackManager.SaveStagingClones(modpack, type, clonesJson);

            return JsonSerializer.Serialize(new
            {
                success = true,
                modpack,
                type,
                clone = new { name = newName, source }
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    private static JsonElement MergeJsonElements(JsonElement vanilla, Dictionary<string, JsonElement> overrides)
    {
        var vanillaObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(vanilla.GetRawText())
            ?? new Dictionary<string, JsonElement>();

        foreach (var (key, value) in overrides)
        {
            vanillaObj[key] = value;
        }

        var merged = JsonSerializer.SerializeToElement(vanillaObj);
        return merged;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
