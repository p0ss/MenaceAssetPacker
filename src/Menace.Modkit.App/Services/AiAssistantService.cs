using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Detected AI client with its configuration status.
/// </summary>
public record AiClientStatus(
    string Name,
    string Description,
    bool IsInstalled,
    bool IsConfigured,
    string? ConfigPath,
    string? SetupDocsUrl
);

/// <summary>
/// Service for detecting and configuring AI assistant clients (Claude, OpenCode, etc.)
/// that can connect to the modkit's MCP server.
/// </summary>
public class AiAssistantService
{
    private static AiAssistantService? _instance;
    public static AiAssistantService Instance => _instance ??= new AiAssistantService();

    private readonly string _modkitRoot;
    private readonly string _mcpProjectPath;

    private AiAssistantService()
    {
        _modkitRoot = AppContext.BaseDirectory;
        _mcpProjectPath = Path.Combine(_modkitRoot, "src", "Menace.Modkit.Mcp");

        // If running from build output, adjust path
        if (!Directory.Exists(_mcpProjectPath))
        {
            // Try finding it relative to working directory (development)
            var devPath = Path.Combine(Environment.CurrentDirectory, "src", "Menace.Modkit.Mcp");
            if (Directory.Exists(devPath))
                _mcpProjectPath = devPath;
        }
    }

    /// <summary>
    /// Detect all supported AI clients and their configuration status.
    /// </summary>
    public async Task<List<AiClientStatus>> DetectClientsAsync()
    {
        var clients = new List<AiClientStatus>();

        // Check Claude Code
        clients.Add(await DetectClaudeCodeAsync());

        // Check Claude Desktop
        clients.Add(await DetectClaudeDesktopAsync());

        // Check OpenCode
        clients.Add(await DetectOpenCodeAsync());

        return clients;
    }

    /// <summary>
    /// Check if any AI client is installed.
    /// </summary>
    public async Task<bool> HasAnyClientInstalledAsync()
    {
        var clients = await DetectClientsAsync();
        return clients.Exists(c => c.IsInstalled);
    }

    /// <summary>
    /// Configure MCP for a specific client.
    /// </summary>
    public async Task<bool> ConfigureClientAsync(string clientName)
    {
        return clientName.ToLowerInvariant() switch
        {
            "claude code" => await ConfigureClaudeCodeAsync(),
            "claude desktop" => await ConfigureClaudeDesktopAsync(),
            "opencode" => await ConfigureOpenCodeAsync(),
            _ => false
        };
    }

    private async Task<AiClientStatus> DetectClaudeCodeAsync()
    {
        var isInstalled = await IsCommandInPathAsync("claude");
        var isConfigured = false;
        string? configPath = null;

        // Claude Code reads .mcp.json from the working directory
        // Check if the modkit's .mcp.json exists
        var mcpJsonPath = Path.Combine(_modkitRoot, ".mcp.json");
        var projectPath = _modkitRoot;

        if (!File.Exists(mcpJsonPath))
        {
            // Try development path
            mcpJsonPath = Path.Combine(Environment.CurrentDirectory, ".mcp.json");
            projectPath = Environment.CurrentDirectory;
        }

        if (File.Exists(mcpJsonPath))
        {
            configPath = mcpJsonPath;

            // Check Claude's global config to see if the server is enabled for this project
            isConfigured = IsClaudeCodeServerEnabled(projectPath, "menace-modkit");
        }

        return new AiClientStatus(
            "Claude Code",
            "Terminal-based AI assistant from Anthropic",
            isInstalled,
            isConfigured,
            configPath,
            "https://docs.anthropic.com/en/docs/claude-code"
        );
    }

    /// <summary>
    /// Check if a specific MCP server is enabled in Claude Code's global config for a project.
    /// </summary>
    private bool IsClaudeCodeServerEnabled(string projectPath, string serverName)
    {
        try
        {
            // Claude Code stores config in ~/.claude.json
            var claudeConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude.json"
            );

            if (!File.Exists(claudeConfigPath))
                return false;

            var json = File.ReadAllText(claudeConfigPath);
            var doc = JsonNode.Parse(json);
            if (doc == null) return false;

            // Find the project settings (key is the project path)
            var projects = doc["projects"]?.AsObject();
            if (projects == null) return false;

            // Normalize paths for comparison
            projectPath = Path.GetFullPath(projectPath).TrimEnd(Path.DirectorySeparatorChar);

            foreach (var (path, settings) in projects)
            {
                var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
                if (string.Equals(normalizedPath, projectPath, StringComparison.OrdinalIgnoreCase))
                {
                    // Check enabledMcpjsonServers array for the server
                    var enabledServers = settings?["enabledMcpjsonServers"]?.AsArray();
                    if (enabledServers != null)
                    {
                        foreach (var server in enabledServers)
                        {
                            if (server?.GetValue<string>() == serverName)
                                return true;
                        }
                    }

                    // Also check mcpServers object (for manually added servers)
                    var mcpServers = settings?["mcpServers"]?.AsObject();
                    if (mcpServers != null && mcpServers.ContainsKey(serverName))
                        return true;

                    break;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[AiAssistant] Failed to check Claude Code config: {ex.Message}");
            return false;
        }
    }

    private Task<AiClientStatus> DetectClaudeDesktopAsync()
    {
        var configPath = GetClaudeDesktopConfigPath();
        var isInstalled = configPath != null && (
            File.Exists(configPath) ||
            Directory.Exists(Path.GetDirectoryName(configPath))
        );

        var isConfigured = false;
        if (isInstalled && File.Exists(configPath))
        {
            isConfigured = CheckMcpConfigured(configPath!, "menace-modkit");
        }

        return Task.FromResult(new AiClientStatus(
            "Claude Desktop",
            "Desktop app from Anthropic with MCP support",
            isInstalled,
            isConfigured,
            configPath,
            "https://claude.ai/download"
        ));
    }

    private async Task<AiClientStatus> DetectOpenCodeAsync()
    {
        var isInstalled = await IsCommandInPathAsync("opencode");

        // Also check for config directory as fallback
        if (!isInstalled)
        {
            var configDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".opencode"
            );
            isInstalled = Directory.Exists(configDir);
        }

        var configPath = GetOpenCodeConfigPath();
        var isConfigured = false;
        if (configPath != null && File.Exists(configPath))
        {
            isConfigured = CheckMcpConfigured(configPath, "menace-modkit");
        }

        return new AiClientStatus(
            "OpenCode",
            "Open-source AI assistant (supports Ollama)",
            isInstalled,
            isConfigured,
            configPath,
            "https://github.com/opencode-ai/opencode"
        );
    }

    private async Task<bool> ConfigureClaudeCodeAsync()
    {
        // Claude Code uses .mcp.json in the project directory
        // This should already exist in the modkit repo
        var mcpJsonPath = Path.Combine(_modkitRoot, ".mcp.json");
        if (!File.Exists(mcpJsonPath))
        {
            mcpJsonPath = Path.Combine(Environment.CurrentDirectory, ".mcp.json");
        }

        if (!File.Exists(mcpJsonPath))
        {
            // Create it
            return await WriteMcpConfigAsync(mcpJsonPath);
        }

        return true; // Already configured
    }

    private async Task<bool> ConfigureClaudeDesktopAsync()
    {
        var configPath = GetClaudeDesktopConfigPath();
        if (configPath == null) return false;

        return await MergeMcpServerIntoConfigAsync(configPath);
    }

    private async Task<bool> ConfigureOpenCodeAsync()
    {
        var configPath = GetOpenCodeConfigPath();
        if (configPath == null) return false;

        // Ensure directory exists
        var dir = Path.GetDirectoryName(configPath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return await MergeMcpServerIntoConfigAsync(configPath);
    }

    private string? GetClaudeDesktopConfigPath()
    {
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "Claude", "claude_desktop_config.json"
            );
        }
        else if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Claude", "claude_desktop_config.json"
            );
        }
        else // Linux
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "claude", "claude_desktop_config.json"
            );
        }
    }

    private string? GetOpenCodeConfigPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".opencode", "mcp.json"
        );
    }

    private bool CheckMcpConfigured(string configPath, string serverName)
    {
        try
        {
            if (!File.Exists(configPath)) return false;

            var json = File.ReadAllText(configPath);
            var doc = JsonNode.Parse(json);
            if (doc == null) return false;

            var servers = doc["mcpServers"];
            if (servers == null) return false;

            return servers[serverName] != null;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> WriteMcpConfigAsync(string configPath)
    {
        try
        {
            var config = new JsonObject
            {
                ["mcpServers"] = new JsonObject
                {
                    ["menace-modkit"] = CreateMcpServerEntry()
                }
            };

            var json = config.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(configPath, json);

            ModkitLog.Info($"[AiAssistant] Created MCP config at {configPath}");
            return true;
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[AiAssistant] Failed to write MCP config: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> MergeMcpServerIntoConfigAsync(string configPath)
    {
        try
        {
            JsonObject config;

            if (File.Exists(configPath))
            {
                var existingJson = await File.ReadAllTextAsync(configPath);
                config = JsonNode.Parse(existingJson)?.AsObject() ?? new JsonObject();
            }
            else
            {
                config = new JsonObject();
            }

            // Ensure mcpServers exists
            if (config["mcpServers"] == null)
            {
                config["mcpServers"] = new JsonObject();
            }

            var servers = config["mcpServers"]!.AsObject();

            // Add or update our server
            servers["menace-modkit"] = CreateMcpServerEntry();

            // Ensure directory exists
            var dir = Path.GetDirectoryName(configPath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = config.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(configPath, json);

            ModkitLog.Info($"[AiAssistant] Updated MCP config at {configPath}");
            return true;
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[AiAssistant] Failed to merge MCP config: {ex.Message}");
            return false;
        }
    }

    private JsonObject CreateMcpServerEntry()
    {
        // Use the MCP project path for development, or bundled path for release
        var projectPath = _mcpProjectPath;

        // For bundled releases, we might use a different approach
        // For now, assume dotnet run --project works
        return new JsonObject
        {
            ["command"] = "dotnet",
            ["args"] = new JsonArray
            {
                "run",
                "--project",
                projectPath
            }
        };
    }

    private async Task<bool> IsCommandInPathAsync(string command)
    {
        try
        {
            var whichCommand = OperatingSystem.IsWindows() ? "where" : "which";
            var psi = new ProcessStartInfo
            {
                FileName = whichCommand,
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
