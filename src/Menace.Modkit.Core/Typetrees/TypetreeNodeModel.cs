using System.Text.Json.Serialization;

namespace Menace.Modkit.Typetrees;

/// <summary>
/// Simplified representation of a Unity type tree node.
/// </summary>
public sealed class TypetreeNodeModel
{
  [JsonPropertyName("level")]
  public byte Level { get; init; }

  [JsonPropertyName("typeFlags")]
  public int TypeFlags { get; init; }

  [JsonPropertyName("type")]
  public string Type { get; init; } = string.Empty;

  [JsonPropertyName("name")]
  public string Name { get; init; } = string.Empty;

  [JsonPropertyName("byteSize")]
  public int ByteSize { get; init; }

  [JsonPropertyName("index")]
  public int Index { get; init; }

  [JsonPropertyName("version")]
  public int Version { get; init; }

  [JsonPropertyName("metaFlag")]
  public uint MetaFlag { get; init; }

  [JsonPropertyName("typeOffset")]
  public uint TypeOffset { get; init; }

  [JsonPropertyName("nameOffset")]
  public uint NameOffset { get; init; }

  [JsonPropertyName("refTypeHash")]
  public ulong? RefTypeHash { get; init; }
}
