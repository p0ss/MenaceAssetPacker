using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.Mcp.Resources;

/// <summary>
/// MCP resource provider for template data.
/// Exposes template instances at modkit://templates/{type}/{instance}
/// </summary>
[McpServerResourceType]
public class TemplateResource
{
    private readonly ModpackManager _modpackManager;

    public TemplateResource(ModpackManager modpackManager)
    {
        _modpackManager = modpackManager;
    }

    [McpServerResource(UriTemplate = "modkit://templates/{templateType}")]
    [Description("List all instances of a template type")]
    public string ListInstances(string templateType)
    {
        var vanillaPath = _modpackManager.GetVanillaTemplatePath(templateType);
        if (vanillaPath == null)
        {
            return JsonSerializer.Serialize(new { error = $"Template type '{templateType}' not found" });
        }

        try
        {
            var json = File.ReadAllText(vanillaPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (data == null)
            {
                return JsonSerializer.Serialize(new { error = "Failed to parse template data" });
            }

            return JsonSerializer.Serialize(new
            {
                templateType,
                instances = data.Keys.OrderBy(k => k).ToList(),
                count = data.Count
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerResource(UriTemplate = "modkit://templates/{templateType}/{instance}")]
    [Description("Get the data for a specific template instance")]
    public string GetInstance(string templateType, string instance)
    {
        var vanillaPath = _modpackManager.GetVanillaTemplatePath(templateType);
        if (vanillaPath == null)
        {
            return JsonSerializer.Serialize(new { error = $"Template type '{templateType}' not found" });
        }

        try
        {
            var json = File.ReadAllText(vanillaPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (data == null || !data.TryGetValue(instance, out var instanceData))
            {
                return JsonSerializer.Serialize(new { error = $"Instance '{instance}' not found in '{templateType}'" });
            }

            return JsonSerializer.Serialize(new
            {
                templateType,
                instance,
                data = instanceData
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}
