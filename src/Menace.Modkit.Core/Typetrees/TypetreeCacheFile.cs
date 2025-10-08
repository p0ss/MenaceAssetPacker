using System.Text.Json.Serialization;

namespace Menace.Modkit.Typetrees;

/// <summary>
/// Manifest entry describing an exported typetree dataset for a serialized Unity file.
/// </summary>
public sealed class TypetreeCacheFile
{
  [JsonPropertyName("source")]
  public string Source { get; init; } = string.Empty;

  [JsonPropertyName("serializedFile")]
  public string SerializedFile { get; init; } = string.Empty;

  [JsonPropertyName("unityVersion")]
  public string? UnityVersion { get; init; }

  [JsonPropertyName("formatVersion")]
  public int FormatVersion { get; init; }

  [JsonPropertyName("typeCount")]
  public int TypeCount { get; init; }

  [JsonPropertyName("referenceTypeCount")]
  public int ReferenceTypeCount { get; init; }

  [JsonPropertyName("output")]
  public string Output { get; init; } = string.Empty;

  [JsonPropertyName("hash")]
  public string? Hash { get; init; }
}
