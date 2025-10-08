using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Menace.Modkit.Typetrees;

/// <summary>
/// Represents a single Unity type tree definition.
/// </summary>
public sealed class TypetreeDefinition
{
  [JsonPropertyName("typeId")]
  public int TypeId { get; init; }

  [JsonPropertyName("originalTypeId")]
  public int OriginalTypeId { get; init; }

  [JsonPropertyName("scriptTypeIndex")]
  public short ScriptTypeIndex { get; init; }

  [JsonPropertyName("isStripped")]
  public bool IsStripped { get; init; }

  [JsonPropertyName("scriptId")]
  public string? ScriptId { get; init; }

  [JsonPropertyName("typeHash")]
  public string? TypeHash { get; init; }

  [JsonPropertyName("nodes")]
  public IReadOnlyList<TypetreeNodeModel> Nodes { get; init; } = new List<TypetreeNodeModel>();
}
