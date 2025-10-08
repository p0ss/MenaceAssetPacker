using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Menace.Modkit.Typetrees;

/// <summary>
/// Serialized representation of the typetrees extracted from a Unity serialized file.
/// </summary>
public sealed class TypetreeCollection
{
  [JsonPropertyName("source")]
  public string Source { get; init; } = string.Empty;

  [JsonPropertyName("serializedFile")]
  public string SerializedFile { get; init; } = string.Empty;

  [JsonPropertyName("unityVersion")]
  public string UnityVersion { get; init; } = string.Empty;

  [JsonPropertyName("formatVersion")]
  public int FormatVersion { get; init; }

  [JsonPropertyName("types")]
  public IReadOnlyList<TypetreeDefinition> Types { get; init; } = new List<TypetreeDefinition>();

  [JsonPropertyName("referencedTypes")]
  public IReadOnlyList<TypetreeDefinition> ReferencedTypes { get; init; } = new List<TypetreeDefinition>();
}
