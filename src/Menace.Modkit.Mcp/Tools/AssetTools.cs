using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.Mcp.Tools;

/// <summary>
/// MCP tools for managing modpack assets (textures, audio, etc.).
/// </summary>
[McpServerToolType]
public static class AssetTools
{
    [McpServerTool(Name = "asset_list", ReadOnly = true)]
    [Description("List all asset files in a modpack. Assets are files like textures, audio, or other game resources that will be included in the modpack.")]
    public static string AssetList(
        ModpackManager modpackManager,
        [Description("The name of the modpack")] string modpack,
        [Description("Filter assets by path pattern (optional)")] string? filter = null)
    {
        var modpacks = modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(modpack, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{modpack}' not found" }, JsonOptions);
        }

        var assets = modpackManager.GetStagingAssetPaths(modpack);

        if (!string.IsNullOrEmpty(filter))
        {
            assets = assets
                .Where(a => a.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Group by directory and extension
        var byExtension = assets
            .GroupBy(a => Path.GetExtension(a).ToLowerInvariant())
            .ToDictionary(g => string.IsNullOrEmpty(g.Key) ? "(no extension)" : g.Key, g => g.Count());

        var byDirectory = assets
            .GroupBy(a => Path.GetDirectoryName(a) ?? "(root)")
            .ToDictionary(g => g.Key, g => g.Count());

        return JsonSerializer.Serialize(new
        {
            modpack,
            assets,
            count = assets.Count,
            byExtension,
            byDirectory
        }, JsonOptions);
    }

    [McpServerTool(Name = "asset_info", ReadOnly = true)]
    [Description("Get information about a specific asset file.")]
    public static string AssetInfo(
        ModpackManager modpackManager,
        [Description("The name of the modpack")] string modpack,
        [Description("The relative path to the asset")] string path)
    {
        var modpacks = modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(modpack, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{modpack}' not found" }, JsonOptions);
        }

        var assetPath = modpackManager.GetStagingAssetPath(modpack, path);
        if (assetPath == null)
        {
            return JsonSerializer.Serialize(new { error = $"Asset '{path}' not found" }, JsonOptions);
        }

        var fileInfo = new FileInfo(assetPath);
        var extension = fileInfo.Extension.ToLowerInvariant();

        // Determine asset type
        string assetType = extension switch
        {
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tga" => "texture",
            ".wav" or ".mp3" or ".ogg" => "audio",
            ".fbx" or ".obj" or ".gltf" or ".glb" => "model",
            ".json" => "data",
            ".txt" or ".md" => "text",
            _ => "unknown"
        };

        return JsonSerializer.Serialize(new
        {
            modpack,
            path,
            fullPath = assetPath,
            exists = true,
            assetType,
            extension,
            sizeBytes = fileInfo.Length,
            sizeHuman = FormatFileSize(fileInfo.Length),
            lastModified = fileInfo.LastWriteTime
        }, JsonOptions);
    }

    [McpServerTool(Name = "asset_delete", Destructive = true)]
    [Description("Delete an asset file from a modpack.")]
    public static string AssetDelete(
        ModpackManager modpackManager,
        [Description("The name of the modpack")] string modpack,
        [Description("The relative path to the asset to delete")] string path)
    {
        var modpacks = modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(modpack, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{modpack}' not found" }, JsonOptions);
        }

        // Security: prevent path traversal
        if (path.Contains("..") || Path.IsPathRooted(path))
        {
            return JsonSerializer.Serialize(new { error = "Invalid path: path traversal not allowed" }, JsonOptions);
        }

        var assetPath = modpackManager.GetStagingAssetPath(modpack, path);
        if (assetPath == null)
        {
            return JsonSerializer.Serialize(new { error = $"Asset '{path}' not found" }, JsonOptions);
        }

        try
        {
            modpackManager.RemoveStagingAsset(modpack, path);

            return JsonSerializer.Serialize(new
            {
                success = true,
                modpack,
                path,
                message = $"Deleted asset '{path}'"
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "extraction_status", ReadOnly = true)]
    [Description("Check the status of game data and asset extraction. Shows whether vanilla data is available and where extracted assets are located.")]
    public static string ExtractionStatus(ModpackManager modpackManager)
    {
        var hasVanillaData = modpackManager.HasVanillaData();
        var vanillaPath = modpackManager.VanillaDataPath;
        var gameInstallPath = modpackManager.GetGameInstallPath();

        var vanillaTemplateTypes = new List<string>();
        if (hasVanillaData && Directory.Exists(vanillaPath))
        {
            vanillaTemplateTypes = Directory.GetFiles(vanillaPath, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => n != null)
                .Cast<string>()
                .OrderBy(n => n)
                .ToList();
        }

        return JsonSerializer.Serialize(new
        {
            hasVanillaData,
            vanillaDataPath = vanillaPath,
            vanillaTemplateTypes,
            vanillaTemplateCount = vanillaTemplateTypes.Count,
            gameInstallPath,
            modsPath = modpackManager.ModsBasePath,
            stagingPath = modpackManager.StagingBasePath
        }, JsonOptions);
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
