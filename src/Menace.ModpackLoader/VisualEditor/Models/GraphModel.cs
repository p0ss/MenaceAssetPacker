#nullable disable

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Menace.ModpackLoader.VisualEditor.Models;

/// <summary>
/// JSON serialization options for mod graph files.
/// </summary>
public static class GraphJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

/// <summary>
/// Root object for a .modgraph.json file.
/// Contains metadata, graphs, and optional shared variables.
/// </summary>
public class ModGraphFile
{
    /// <summary>
    /// Schema version for compatibility checking.
    /// </summary>
    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; } = 1;

    /// <summary>
    /// Mod identification and metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public ModMetadata Metadata { get; set; } = new();

    /// <summary>
    /// List of graphs in this mod (one per hook point typically).
    /// </summary>
    [JsonPropertyName("graphs")]
    public List<ModGraph> Graphs { get; set; } = new();

    /// <summary>
    /// Optional shared variables that can be referenced by nodes.
    /// </summary>
    [JsonPropertyName("variables")]
    public Dictionary<string, JsonElement> Variables { get; set; } = new();
}

/// <summary>
/// Metadata about the mod for identification and display.
/// </summary>
public class ModMetadata
{
    /// <summary>
    /// Unique mod identifier (e.g., "beagles-concealment").
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    /// Human-readable mod name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// Mod author name.
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = "";

    /// <summary>
    /// Semantic version string (e.g., "1.0.0").
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Optional description of what the mod does.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; }

    /// <summary>
    /// Minimum compatible game version.
    /// </summary>
    [JsonPropertyName("gameVersion")]
    public string GameVersion { get; set; }

    /// <summary>
    /// IDs of required mods.
    /// </summary>
    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; }

    /// <summary>
    /// Categorization tags.
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; }
}

/// <summary>
/// A single graph within a mod file, attached to a specific hook point.
/// </summary>
public class ModGraph
{
    /// <summary>
    /// Unique identifier within the file.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    /// Display name for the graph.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// Game event hook point (e.g., "skill_used", "round_end").
    /// </summary>
    [JsonPropertyName("hookPoint")]
    public string HookPoint { get; set; } = "";

    /// <summary>
    /// Whether this graph is active.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Execution priority (lower = earlier).
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 0;

    /// <summary>
    /// All nodes in this graph.
    /// </summary>
    [JsonPropertyName("nodes")]
    public List<GraphNode> Nodes { get; set; } = new();

    /// <summary>
    /// All connections between nodes.
    /// </summary>
    [JsonPropertyName("connections")]
    public List<NodeConnection> Connections { get; set; } = new();

    /// <summary>
    /// Optional documentation for the graph.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; }
}

/// <summary>
/// A single node in the graph (event, condition, action, logic, or value).
/// </summary>
public class GraphNode
{
    /// <summary>
    /// Unique identifier within the graph.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    /// Node category: event, condition, action, logic, value.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    /// <summary>
    /// Specific node type (e.g., "skill_used", "property_check", "add_effect").
    /// </summary>
    [JsonPropertyName("subtype")]
    public string Subtype { get; set; } = "";

    /// <summary>
    /// X position in the editor.
    /// </summary>
    [JsonPropertyName("x")]
    public double X { get; set; }

    /// <summary>
    /// Y position in the editor.
    /// </summary>
    [JsonPropertyName("y")]
    public double Y { get; set; }

    /// <summary>
    /// Node-specific configuration (property values, etc.).
    /// </summary>
    [JsonPropertyName("config")]
    public Dictionary<string, JsonElement> Config { get; set; } = new();

    /// <summary>
    /// Optional custom display label.
    /// </summary>
    [JsonPropertyName("label")]
    public string Label { get; set; }

    /// <summary>
    /// Optional user comment (not compiled).
    /// </summary>
    [JsonPropertyName("comment")]
    public string Comment { get; set; }

    /// <summary>
    /// Editor display state (collapsed/expanded).
    /// </summary>
    [JsonPropertyName("collapsed")]
    public bool Collapsed { get; set; }

    /// <summary>
    /// Helper to get a config value as a specific type.
    /// </summary>
    public T GetConfig<T>(string key, T defaultValue = default)
    {
        if (Config == null || !Config.TryGetValue(key, out var element))
            return defaultValue;

        try
        {
            return element.Deserialize<T>(GraphJsonOptions.Default);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Helper to get a config value as string.
    /// </summary>
    public string GetConfigString(string key, string defaultValue = "")
    {
        if (Config == null || !Config.TryGetValue(key, out var element))
            return defaultValue;

        return element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? defaultValue
            : element.ToString();
    }

    /// <summary>
    /// Helper to get a config value as int.
    /// </summary>
    public int GetConfigInt(string key, int defaultValue = 0)
    {
        if (Config == null || !Config.TryGetValue(key, out var element))
            return defaultValue;

        return element.ValueKind == JsonValueKind.Number
            ? element.GetInt32()
            : defaultValue;
    }

    /// <summary>
    /// Helper to get a config value as bool.
    /// </summary>
    public bool GetConfigBool(string key, bool defaultValue = false)
    {
        if (Config == null || !Config.TryGetValue(key, out var element))
            return defaultValue;

        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => defaultValue
        };
    }
}

/// <summary>
/// A connection between two node ports.
/// </summary>
public class NodeConnection
{
    /// <summary>
    /// Unique identifier for this connection.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>
    /// Source node ID.
    /// </summary>
    [JsonPropertyName("sourceNodeId")]
    public string SourceNodeId { get; set; } = "";

    /// <summary>
    /// Source port name.
    /// </summary>
    [JsonPropertyName("sourcePort")]
    public string SourcePort { get; set; } = "";

    /// <summary>
    /// Target node ID.
    /// </summary>
    [JsonPropertyName("targetNodeId")]
    public string TargetNodeId { get; set; } = "";

    /// <summary>
    /// Target port name.
    /// </summary>
    [JsonPropertyName("targetPort")]
    public string TargetPort { get; set; } = "";
}

/// <summary>
/// Known hook point types.
/// </summary>
public static class HookPoints
{
    // Tactical hooks
    public const string SkillUsed = "skill_used";
    public const string DamageReceived = "damage_received";
    public const string ActorKilled = "actor_killed";
    public const string AttackMissed = "attack_missed";
    public const string RoundStart = "round_start";
    public const string RoundEnd = "round_end";
    public const string TurnStart = "turn_start";
    public const string TurnEnd = "turn_end";
    public const string MoveStart = "move_start";
    public const string MoveComplete = "move_complete";

    // Strategy hooks
    public const string LeaderHired = "leader_hired";
    public const string LeaderDismissed = "leader_dismissed";
    public const string LeaderLevelUp = "leader_levelup";
    public const string FactionTrustChanged = "faction_trust_changed";
    public const string MissionEnded = "mission_ended";
    public const string BlackmarketRestocked = "blackmarket_restocked";
}

/// <summary>
/// Known node types.
/// </summary>
public static class NodeTypes
{
    public const string Event = "event";
    public const string Condition = "condition";
    public const string Action = "action";
    public const string Logic = "logic";
    public const string Value = "value";
}

/// <summary>
/// Known node subtypes.
/// </summary>
public static class NodeSubtypes
{
    // Event subtypes (match hook points)
    public const string SkillUsed = "skill_used";
    public const string DamageReceived = "damage_received";
    public const string ActorKilled = "actor_killed";
    public const string RoundStart = "round_start";
    public const string RoundEnd = "round_end";
    public const string TurnEnd = "turn_end";

    // Condition subtypes
    public const string PropertyCheck = "property_check";

    // Logic subtypes
    public const string And = "and";
    public const string Or = "or";
    public const string Not = "not";

    // Action subtypes
    public const string AddEffect = "add_effect";
    public const string Damage = "damage";
    public const string Heal = "heal";
    public const string SetFlag = "set_flag";
    public const string Log = "log";

    // Value subtypes
    public const string Constant = "constant";
    public const string Variable = "variable";
    public const string Random = "random";
}
