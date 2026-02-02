using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Menace.Modkit.App.Models;

/// <summary>
/// Tracks what is currently deployed in the game's Mods/ folder.
/// Persisted as deploy-state.json alongside the staging area.
/// </summary>
public class DeployState
{
    public List<DeployedModpack> DeployedModpacks { get; set; } = new();
    public List<string> DeployedFiles { get; set; } = new();
    public DateTime LastDeployTimestamp { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static DeployState LoadFrom(string filePath)
    {
        if (!File.Exists(filePath))
            return new DeployState();

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<DeployState>(json, ReadOptions) ?? new DeployState();
        }
        catch
        {
            return new DeployState();
        }
    }

    public void SaveTo(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(filePath, JsonSerializer.Serialize(this, JsonOptions));
    }
}

public class DeployedModpack
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public int LoadOrder { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SecurityStatus SecurityStatus { get; set; }
}
