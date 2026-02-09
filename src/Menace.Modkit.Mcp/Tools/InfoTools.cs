using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.Mcp.Tools;

/// <summary>
/// MCP tools for getting general information about the modkit environment.
/// </summary>
[McpServerToolType]
public static class InfoTools
{
    [McpServerTool(Name = "modkit_info", ReadOnly = true)]
    [Description("Get information about the Menace Modkit environment, including paths, version, and capabilities.")]
    public static string ModkitInfo(ModpackManager modpackManager)
    {
        var hasVanillaData = modpackManager.HasVanillaData();
        var gameInstallPath = modpackManager.GetGameInstallPath();
        var stagingModpacks = modpackManager.GetStagingModpacks();
        var activeMods = modpackManager.GetActiveMods();

        return JsonSerializer.Serialize(new
        {
            version = Menace.ModkitVersion.AppFull,
            buildNumber = Menace.ModkitVersion.BuildNumber,
            paths = new
            {
                staging = modpackManager.StagingBasePath,
                vanillaData = modpackManager.VanillaDataPath,
                mods = modpackManager.ModsBasePath,
                runtimeDlls = modpackManager.RuntimeDllsPath,
                gameInstall = gameInstallPath
            },
            status = new
            {
                hasGamePath = !string.IsNullOrEmpty(gameInstallPath),
                hasVanillaData,
                stagingModpackCount = stagingModpacks.Count,
                activeModCount = activeMods.Count
            },
            capabilities = new[]
            {
                "modpack_create", "modpack_list", "modpack_get", "modpack_delete", "modpack_update",
                "template_types", "template_list", "template_get", "template_patch", "template_reset", "template_clone",
                "compile_modpack", "security_scan",
                "deploy_modpack", "deploy_all", "undeploy_all", "deploy_status",
                "source_list", "source_read", "source_write", "source_add", "source_delete",
                "asset_list", "asset_info", "asset_delete", "extraction_status"
            }
        }, JsonOptions);
    }

    [McpServerTool(Name = "game_status", ReadOnly = true)]
    [Description("Check the game installation status and what data is available for modding.")]
    public static string GameStatus(ModpackManager modpackManager)
    {
        var gameInstallPath = modpackManager.GetGameInstallPath();
        var hasVanillaData = modpackManager.HasVanillaData();
        var vanillaPath = modpackManager.VanillaDataPath;

        var templateTypes = new List<string>();
        if (hasVanillaData && Directory.Exists(vanillaPath))
        {
            templateTypes = Directory.GetFiles(vanillaPath, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => n != null)
                .Cast<string>()
                .OrderBy(n => n)
                .ToList();
        }

        var modsPath = modpackManager.ModsBasePath;
        var hasModsFolder = !string.IsNullOrEmpty(modsPath) && Directory.Exists(modsPath);

        return JsonSerializer.Serialize(new
        {
            gameInstallPath,
            hasGamePath = !string.IsNullOrEmpty(gameInstallPath),
            hasVanillaData,
            vanillaDataPath = vanillaPath,
            templateTypeCount = templateTypes.Count,
            templateTypes = templateTypes.Take(20).ToList(), // First 20 for summary
            hasMoreTypes = templateTypes.Count > 20,
            hasModsFolder,
            modsPath,
            readyForModding = hasVanillaData && hasModsFolder
        }, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
