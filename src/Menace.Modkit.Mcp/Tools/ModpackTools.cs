using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.Mcp.Tools;

/// <summary>
/// MCP tools for modpack CRUD operations.
/// </summary>
[McpServerToolType]
public static class ModpackTools
{
    [McpServerTool(Name = "modpack_list", ReadOnly = true)]
    [Description("List all modpacks in the staging directory. Returns an array of modpack summaries with name, author, version, and content flags.")]
    public static string ModpackList(
        ModpackManager modpackManager,
        [Description("If true, also include deployed modpacks from the game's Mods folder")] bool includeDeployed = false)
    {
        var modpacks = modpackManager.GetStagingModpacks();

        if (includeDeployed)
        {
            var deployed = modpackManager.GetActiveMods();
            var stagingNames = modpacks.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var d in deployed.Where(m => !stagingNames.Contains(m.Name)))
            {
                modpacks.Add(d);
            }
        }

        var result = modpacks.Select(m => new
        {
            name = m.Name,
            author = m.Author,
            version = m.Version,
            description = m.Description,
            loadOrder = m.LoadOrder,
            hasCode = m.Code.HasAnySources,
            hasAssets = m.Assets.Count > 0,
            hasPatches = m.Patches.Count > 0,
            securityStatus = m.SecurityStatus.ToString(),
            path = m.Path
        });

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "modpack_create", Destructive = false)]
    [Description("Create a new modpack in the staging directory. Returns the created modpack manifest.")]
    public static string ModpackCreate(
        ModpackManager modpackManager,
        [Description("The name for the new modpack (alphanumeric, spaces, underscores, hyphens allowed)")] string name,
        [Description("The author of the modpack")] string? author = null,
        [Description("A description of what the modpack does")] string? description = null)
    {
        var manifest = modpackManager.CreateModpack(name, author ?? "Unknown", description ?? "");

        return JsonSerializer.Serialize(new
        {
            success = true,
            modpack = new
            {
                name = manifest.Name,
                author = manifest.Author,
                version = manifest.Version,
                description = manifest.Description,
                path = manifest.Path
            }
        }, JsonOptions);
    }

    [McpServerTool(Name = "modpack_get", ReadOnly = true)]
    [Description("Get detailed information about a specific modpack, including its manifest, patches, sources, and assets.")]
    public static string ModpackGet(
        ModpackManager modpackManager,
        [Description("The name of the modpack to retrieve")] string name)
    {
        var modpacks = modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{name}' not found" }, JsonOptions);
        }

        // Get template types that have overrides
        var statsDir = Path.Combine(manifest.Path, "stats");
        var patchedTypes = Directory.Exists(statsDir)
            ? Directory.GetFiles(statsDir, "*.json").Select(Path.GetFileNameWithoutExtension).ToList()
            : new List<string?>();

        return JsonSerializer.Serialize(new
        {
            name = manifest.Name,
            author = manifest.Author,
            version = manifest.Version,
            description = manifest.Description,
            loadOrder = manifest.LoadOrder,
            createdDate = manifest.CreatedDate,
            modifiedDate = manifest.ModifiedDate,
            securityStatus = manifest.SecurityStatus.ToString(),
            code = new
            {
                sources = manifest.Code.Sources,
                references = manifest.Code.References,
                prebuiltDlls = manifest.Code.PrebuiltDlls,
                hasAnySources = manifest.Code.HasAnySources
            },
            patchedTemplateTypes = patchedTypes,
            assets = modpackManager.GetStagingAssetPaths(name),
            dependencies = manifest.Dependencies,
            path = manifest.Path
        }, JsonOptions);
    }

    [McpServerTool(Name = "modpack_delete", Destructive = true)]
    [Description("Delete a modpack from the staging directory. This is irreversible.")]
    public static string ModpackDelete(
        ModpackManager modpackManager,
        [Description("The name of the modpack to delete")] string name)
    {
        var success = modpackManager.DeleteStagingModpack(name);

        return JsonSerializer.Serialize(new
        {
            success,
            message = success ? $"Deleted modpack '{name}'" : $"Modpack '{name}' not found"
        }, JsonOptions);
    }

    [McpServerTool(Name = "modpack_update", Destructive = false)]
    [Description("Update a modpack's metadata (name, author, version, description, load order).")]
    public static string ModpackUpdate(
        ModpackManager modpackManager,
        [Description("The name of the modpack to update")] string name,
        [Description("New author (optional)")] string? author = null,
        [Description("New version (optional)")] string? version = null,
        [Description("New description (optional)")] string? description = null,
        [Description("New load order (optional, lower values load first)")] int? loadOrder = null)
    {
        var modpacks = modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{name}' not found" }, JsonOptions);
        }

        if (author != null) manifest.Author = author;
        if (version != null) manifest.Version = version;
        if (description != null) manifest.Description = description;
        if (loadOrder.HasValue) manifest.LoadOrder = loadOrder.Value;

        modpackManager.UpdateModpackMetadata(manifest);

        return JsonSerializer.Serialize(new
        {
            success = true,
            modpack = new
            {
                name = manifest.Name,
                author = manifest.Author,
                version = manifest.Version,
                description = manifest.Description,
                loadOrder = manifest.LoadOrder
            }
        }, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
