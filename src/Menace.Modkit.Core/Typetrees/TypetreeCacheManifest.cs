using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Menace.Modkit.Typetrees;

/// <summary>
/// Metadata persisted alongside exported typetrees so other tools can validate compatibility.
/// </summary>
public sealed class TypetreeCacheManifest
{
  [JsonPropertyName("gameVersion")]
  public string? GameVersion { get; init; }

  [JsonPropertyName("unityVersion")]
  public string? UnityVersion { get; init; }

  [JsonPropertyName("sourcePath")]
  public string SourcePath { get; init; } = string.Empty;

  [JsonPropertyName("sourceFingerprint")]
  public string SourceFingerprint { get; init; } = string.Empty;

  [JsonPropertyName("createdAtUtc")]
  public DateTimeOffset CreatedAtUtc { get; init; }

  [JsonPropertyName("toolVersion")]
  public string ToolVersion { get; init; } = "0.1.0-dev";

  [JsonPropertyName("files")]
  public List<TypetreeCacheFile> Files { get; set; } = new();
}
