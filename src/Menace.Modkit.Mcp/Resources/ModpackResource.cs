using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.Mcp.Resources;

/// <summary>
/// MCP resource provider for modpack data.
/// Exposes modpack information at modkit://modpack/{name}/*
/// </summary>
[McpServerResourceType]
public class ModpackResource
{
    private readonly ModpackManager _modpackManager;

    public ModpackResource(ModpackManager modpackManager)
    {
        _modpackManager = modpackManager;
    }

    [McpServerResource(UriTemplate = "modkit://modpacks")]
    [Description("List all staging modpacks")]
    public string ListModpacks()
    {
        var modpacks = _modpackManager.GetStagingModpacks();

        return JsonSerializer.Serialize(new
        {
            modpacks = modpacks.Select(m => new
            {
                name = m.Name,
                uri = $"modkit://modpack/{m.Name}/manifest"
            }).ToList(),
            count = modpacks.Count
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerResource(UriTemplate = "modkit://modpack/{name}/manifest")]
    [Description("Get a modpack's manifest")]
    public string GetManifest(string name)
    {
        var modpacks = _modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{name}' not found" });
        }

        return JsonSerializer.Serialize(new
        {
            name = manifest.Name,
            version = manifest.Version,
            author = manifest.Author,
            description = manifest.Description,
            loadOrder = manifest.LoadOrder,
            createdDate = manifest.CreatedDate,
            modifiedDate = manifest.ModifiedDate,
            securityStatus = manifest.SecurityStatus.ToString(),
            code = manifest.Code,
            dependencies = manifest.Dependencies,
            path = manifest.Path
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerResource(UriTemplate = "modkit://modpack/{name}/patches/{templateType}")]
    [Description("Get a modpack's patches for a specific template type")]
    public string GetPatches(string name, string templateType)
    {
        var stagingPath = _modpackManager.GetStagingTemplatePath(name, templateType);
        if (stagingPath == null)
        {
            return JsonSerializer.Serialize(new
            {
                modpack = name,
                templateType,
                hasPatches = false,
                patches = new Dictionary<string, object>()
            });
        }

        try
        {
            var json = File.ReadAllText(stagingPath);
            var patches = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(json);

            return JsonSerializer.Serialize(new
            {
                modpack = name,
                templateType,
                hasPatches = true,
                instanceCount = patches?.Count ?? 0,
                patches
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerResource(UriTemplate = "modkit://modpack/{name}/sources")]
    [Description("List a modpack's source files")]
    public string GetSources(string name)
    {
        var modpacks = _modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{name}' not found" });
        }

        var sources = _modpackManager.GetStagingSources(name);

        return JsonSerializer.Serialize(new
        {
            modpack = name,
            sources,
            count = sources.Count
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerResource(UriTemplate = "modkit://modpack/{name}/assets")]
    [Description("List a modpack's asset files")]
    public string GetAssets(string name)
    {
        var modpacks = _modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{name}' not found" });
        }

        var assets = _modpackManager.GetStagingAssetPaths(name);

        return JsonSerializer.Serialize(new
        {
            modpack = name,
            assets,
            count = assets.Count
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
