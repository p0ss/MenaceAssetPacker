using System.Text.Json.Serialization;

namespace Menace.Modkit.App.Models;

public class DependencyVersions
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("components")]
    public Dictionary<string, ComponentInfo> Components { get; set; } = new();
}

public class ComponentInfo
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

// Keep for backwards compatibility if needed
public class DependencyInfo
{
    public string Version { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string BundledPath { get; set; } = string.Empty;
}
