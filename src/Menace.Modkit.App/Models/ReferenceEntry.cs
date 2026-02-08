using System.Text.Json.Serialization;

namespace Menace.Modkit.App.Models;

/// <summary>
/// Represents a single reverse reference (backlink) from a source template to a target.
/// Used by the ReferenceGraphService to track "what links here" relationships.
/// </summary>
public class ReferenceEntry
{
    /// <summary>
    /// The template type of the source (e.g., "SquadTemplate", "EntityTemplate")
    /// </summary>
    [JsonPropertyName("sourceType")]
    public string SourceTemplateType { get; set; } = string.Empty;

    /// <summary>
    /// The instance name of the source template (e.g., "squad.pirates")
    /// </summary>
    [JsonPropertyName("sourceInstance")]
    public string SourceInstanceName { get; set; } = string.Empty;

    /// <summary>
    /// The field name that contains the reference (e.g., "Members", "Portrait")
    /// </summary>
    [JsonPropertyName("field")]
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// User-friendly display name showing the reference path
    /// </summary>
    [JsonIgnore]
    public string DisplayName => $"{SourceInstanceName} → {FieldName}";

    /// <summary>
    /// Composite key for the source template (TemplateType/instanceName)
    /// </summary>
    [JsonIgnore]
    public string SourceKey => $"{SourceTemplateType}/{SourceInstanceName}";
}

/// <summary>
/// Container for the serialized reference graph file (references.json)
/// </summary>
public class ReferenceGraphData
{
    /// <summary>
    /// Schema version for forward compatibility
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    /// <summary>
    /// When the graph was built
    /// </summary>
    [JsonPropertyName("buildDate")]
    public string BuildDate { get; set; } = string.Empty;

    /// <summary>
    /// Template backlinks: "TemplateType/InstanceName" → list of references
    /// </summary>
    [JsonPropertyName("templateBacklinks")]
    public Dictionary<string, List<ReferenceEntry>> TemplateBacklinks { get; set; } = new();

    /// <summary>
    /// Asset backlinks: "path/to/asset.png" → list of template references
    /// </summary>
    [JsonPropertyName("assetBacklinks")]
    public Dictionary<string, List<ReferenceEntry>> AssetBacklinks { get; set; } = new();
}
