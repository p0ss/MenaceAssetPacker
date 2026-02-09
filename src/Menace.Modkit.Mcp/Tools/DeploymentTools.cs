using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.Mcp.Tools;

/// <summary>
/// MCP tools for deploying modpacks to the game.
/// </summary>
[McpServerToolType]
public static class DeploymentTools
{
    [McpServerTool(Name = "deploy_modpack", Destructive = true)]
    [Description("Deploy a single modpack to the game's Mods folder. Compiles source code if present and copies all assets and patches.")]
    public static async Task<string> DeployModpack(
        ModpackManager modpackManager,
        DeployManager deployManager,
        [Description("The name of the modpack to deploy")] string name,
        CancellationToken cancellationToken = default)
    {
        var modpacks = modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{name}' not found" }, JsonOptions);
        }

        var progress = new List<string>();
        var progressHandler = new Progress<string>(msg => progress.Add(msg));

        try
        {
            var result = await deployManager.DeploySingleAsync(manifest, progressHandler, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = result.Success,
                message = result.Message,
                deployedCount = result.DeployedCount,
                progressLog = progress
            }, JsonOptions);
        }
        catch (OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { error = "Deployment cancelled" }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "deploy_all", Destructive = true)]
    [Description("Deploy all staging modpacks to the game's Mods folder. Compiles sources, merges patches in load order, and creates asset bundles.")]
    public static async Task<string> DeployAll(
        DeployManager deployManager,
        CancellationToken cancellationToken = default)
    {
        var progress = new List<string>();
        var progressHandler = new Progress<string>(msg => progress.Add(msg));

        try
        {
            var result = await deployManager.DeployAllAsync(progressHandler, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = result.Success,
                message = result.Message,
                deployedCount = result.DeployedCount,
                progressLog = progress
            }, JsonOptions);
        }
        catch (OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { error = "Deployment cancelled" }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "undeploy_all", Destructive = true)]
    [Description("Remove all deployed mods from the game's Mods folder. This does not affect staging modpacks.")]
    public static async Task<string> UndeployAll(
        DeployManager deployManager,
        CancellationToken cancellationToken = default)
    {
        var progress = new List<string>();
        var progressHandler = new Progress<string>(msg => progress.Add(msg));

        try
        {
            var result = await deployManager.UndeployAllAsync(progressHandler, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = result.Success,
                message = result.Message,
                progressLog = progress
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "deploy_status", ReadOnly = true)]
    [Description("Get the current deployment status, including what's deployed vs what's in staging, and whether any changes need to be redeployed.")]
    public static string DeployStatus(
        ModpackManager modpackManager,
        DeployManager deployManager)
    {
        var state = deployManager.GetDeployState();
        var stagingModpacks = modpackManager.GetStagingModpacks();
        var hasChanges = deployManager.HasChangedSinceDeploy();

        var deployedNames = state.DeployedModpacks.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stagingNames = stagingModpacks.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var onlyInStaging = stagingNames.Except(deployedNames, StringComparer.OrdinalIgnoreCase).ToList();
        var onlyInDeployed = deployedNames.Except(stagingNames, StringComparer.OrdinalIgnoreCase).ToList();

        return JsonSerializer.Serialize(new
        {
            hasChanges,
            lastDeployTimestamp = state.LastDeployTimestamp,
            deployedModpacks = state.DeployedModpacks.Select(m => new
            {
                name = m.Name,
                version = m.Version,
                loadOrder = m.LoadOrder,
                securityStatus = m.SecurityStatus.ToString()
            }),
            stagingModpackCount = stagingModpacks.Count,
            deployedModpackCount = state.DeployedModpacks.Count,
            newInStaging = onlyInStaging,
            removedFromStaging = onlyInDeployed,
            gameInstallPath = modpackManager.GetGameInstallPath(),
            modsPath = modpackManager.ModsBasePath
        }, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
