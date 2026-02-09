using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.Mcp.Tools;

/// <summary>
/// MCP tools for managing modpack source code files.
/// </summary>
[McpServerToolType]
public static class SourceTools
{
    [McpServerTool(Name = "source_list", ReadOnly = true)]
    [Description("List all C# source files in a modpack. Returns paths relative to the modpack directory.")]
    public static string SourceList(
        ModpackManager modpackManager,
        [Description("The name of the modpack")] string modpack)
    {
        var modpacks = modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(modpack, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{modpack}' not found" }, JsonOptions);
        }

        var sources = modpackManager.GetStagingSources(modpack);

        return JsonSerializer.Serialize(new
        {
            modpack,
            sources,
            count = sources.Count,
            hasAnySources = sources.Count > 0
        }, JsonOptions);
    }

    [McpServerTool(Name = "source_read", ReadOnly = true)]
    [Description("Read the content of a source file from a modpack.")]
    public static string SourceRead(
        ModpackManager modpackManager,
        [Description("The name of the modpack")] string modpack,
        [Description("The relative path to the source file (e.g., 'src/MyPlugin.cs')")] string path)
    {
        var modpacks = modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(modpack, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{modpack}' not found" }, JsonOptions);
        }

        var content = modpackManager.ReadStagingSource(modpack, path);
        if (content == null)
        {
            return JsonSerializer.Serialize(new { error = $"Source file '{path}' not found" }, JsonOptions);
        }

        return JsonSerializer.Serialize(new
        {
            modpack,
            path,
            content,
            lines = content.Split('\n').Length
        }, JsonOptions);
    }

    [McpServerTool(Name = "source_write", Destructive = false)]
    [Description("Write or update a source file in a modpack. Creates parent directories if needed.")]
    public static string SourceWrite(
        ModpackManager modpackManager,
        [Description("The name of the modpack")] string modpack,
        [Description("The relative path for the source file (e.g., 'src/MyPlugin.cs')")] string path,
        [Description("The C# source code content")] string content)
    {
        var modpacks = modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(modpack, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{modpack}' not found" }, JsonOptions);
        }

        // Validate path
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new { error = "Source files must have .cs extension" }, JsonOptions);
        }

        // Security: prevent path traversal
        if (path.Contains("..") || Path.IsPathRooted(path))
        {
            return JsonSerializer.Serialize(new { error = "Invalid path: path traversal not allowed" }, JsonOptions);
        }

        try
        {
            modpackManager.SaveStagingSource(modpack, path, content);

            return JsonSerializer.Serialize(new
            {
                success = true,
                modpack,
                path,
                lines = content.Split('\n').Length,
                bytes = content.Length
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "source_add", Destructive = false)]
    [Description("Add a new source file with a template to a modpack. Creates a basic class structure with the correct namespace.")]
    public static string SourceAdd(
        ModpackManager modpackManager,
        [Description("The name of the modpack")] string modpack,
        [Description("The relative path for the new source file (e.g., 'src/MyFeature.cs')")] string path)
    {
        var modpacks = modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m =>
            m.Name.Equals(modpack, StringComparison.OrdinalIgnoreCase));

        if (manifest == null)
        {
            return JsonSerializer.Serialize(new { error = $"Modpack '{modpack}' not found" }, JsonOptions);
        }

        // Validate path
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new { error = "Source files must have .cs extension" }, JsonOptions);
        }

        // Security: prevent path traversal
        if (path.Contains("..") || Path.IsPathRooted(path))
        {
            return JsonSerializer.Serialize(new { error = "Invalid path: path traversal not allowed" }, JsonOptions);
        }

        // Check if file already exists
        var existing = modpackManager.ReadStagingSource(modpack, path);
        if (existing != null)
        {
            return JsonSerializer.Serialize(new { error = $"Source file '{path}' already exists" }, JsonOptions);
        }

        try
        {
            modpackManager.AddStagingSource(modpack, path);
            var content = modpackManager.ReadStagingSource(modpack, path);

            return JsonSerializer.Serialize(new
            {
                success = true,
                modpack,
                path,
                content,
                message = "Created new source file with template"
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [McpServerTool(Name = "source_delete", Destructive = true)]
    [Description("Delete a source file from a modpack.")]
    public static string SourceDelete(
        ModpackManager modpackManager,
        [Description("The name of the modpack")] string modpack,
        [Description("The relative path to the source file to delete")] string path)
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

        var existing = modpackManager.ReadStagingSource(modpack, path);
        if (existing == null)
        {
            return JsonSerializer.Serialize(new { error = $"Source file '{path}' not found" }, JsonOptions);
        }

        try
        {
            modpackManager.RemoveStagingSource(modpack, path);

            return JsonSerializer.Serialize(new
            {
                success = true,
                modpack,
                path,
                message = $"Deleted source file '{path}'"
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
