#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Menace.ModpackLoader.VisualEditor.Schema;

/// <summary>
/// Complete catalog of all node types available in the visual mod editor.
/// Provides static definitions and integration with SchemaParser for dynamic dropdowns.
///
/// Node Categories:
/// - Event (~12): Entry points triggered by game hooks
/// - Condition: Property checks from schema (boolean/enum fields)
/// - Action (~15): Game state modifications
/// - Logic (3): Boolean combinators (AND, OR, NOT)
/// - Value (3): Constants, variables, and computed values
/// </summary>
public sealed class NodeCatalog
{
    private static NodeCatalog _instance;
    private static readonly object _lock = new();

    private readonly Dictionary<string, NodeTypeDefinition> _nodeTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<NodeCategory, List<NodeTypeDefinition>> _nodesByCategory = new();
    private bool _isInitialized;

    /// <summary>
    /// Gets the singleton instance of the node catalog.
    /// </summary>
    public static NodeCatalog Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new NodeCatalog();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Whether the catalog has been initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// All registered node type identifiers.
    /// </summary>
    public IEnumerable<string> NodeTypeIds => _nodeTypes.Keys;

    /// <summary>
    /// Total count of registered node types.
    /// </summary>
    public int NodeTypeCount => _nodeTypes.Count;

    private NodeCatalog() { }

    /// <summary>
    /// Initialize the catalog with all node type definitions.
    /// Must be called before using the catalog.
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized)
            return;

        // Initialize category lists
        foreach (NodeCategory category in Enum.GetValues(typeof(NodeCategory)))
        {
            _nodesByCategory[category] = new List<NodeTypeDefinition>();
        }

        // Register all node types
        RegisterEventNodes();
        RegisterConditionNodes();
        RegisterActionNodes();
        RegisterLogicNodes();
        RegisterValueNodes();

        _isInitialized = true;
    }

    /// <summary>
    /// Reset the catalog to allow reinitialization.
    /// </summary>
    public void Reset()
    {
        _nodeTypes.Clear();
        _nodesByCategory.Clear();
        _isInitialized = false;
    }

    #region Node Access

    /// <summary>
    /// Get a node type definition by its identifier.
    /// </summary>
    /// <param name="typeId">The node type identifier (e.g., "skill_used", "add_effect")</param>
    /// <returns>The node definition, or null if not found</returns>
    public NodeTypeDefinition GetNodeType(string typeId)
    {
        return _nodeTypes.TryGetValue(typeId, out var def) ? def : null;
    }

    /// <summary>
    /// Check if a node type exists.
    /// </summary>
    public bool HasNodeType(string typeId) => _nodeTypes.ContainsKey(typeId);

    /// <summary>
    /// Get all node types in a category.
    /// </summary>
    public IReadOnlyList<NodeTypeDefinition> GetNodesByCategory(NodeCategory category)
    {
        return _nodesByCategory.TryGetValue(category, out var list)
            ? list
            : Array.Empty<NodeTypeDefinition>();
    }

    /// <summary>
    /// Get all event nodes.
    /// </summary>
    public IReadOnlyList<NodeTypeDefinition> GetEventNodes() => GetNodesByCategory(NodeCategory.Event);

    /// <summary>
    /// Get all condition nodes.
    /// </summary>
    public IReadOnlyList<NodeTypeDefinition> GetConditionNodes() => GetNodesByCategory(NodeCategory.Condition);

    /// <summary>
    /// Get all action nodes.
    /// </summary>
    public IReadOnlyList<NodeTypeDefinition> GetActionNodes() => GetNodesByCategory(NodeCategory.Action);

    /// <summary>
    /// Get all logic nodes.
    /// </summary>
    public IReadOnlyList<NodeTypeDefinition> GetLogicNodes() => GetNodesByCategory(NodeCategory.Logic);

    /// <summary>
    /// Get all value nodes.
    /// </summary>
    public IReadOnlyList<NodeTypeDefinition> GetValueNodes() => GetNodesByCategory(NodeCategory.Value);

    /// <summary>
    /// Get all node types as a flat list.
    /// </summary>
    public IReadOnlyList<NodeTypeDefinition> GetAllNodes() => _nodeTypes.Values.ToList();

    /// <summary>
    /// Search for nodes by name or description.
    /// </summary>
    public IEnumerable<NodeTypeDefinition> SearchNodes(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return GetAllNodes();

        return _nodeTypes.Values.Where(n =>
            n.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            n.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            n.TypeId.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Schema Integration

    /// <summary>
    /// Get dropdown options for a node property from the schema.
    /// </summary>
    /// <param name="propertyId">The property identifier</param>
    /// <returns>List of option values</returns>
    public IReadOnlyList<DropdownOption> GetPropertyDropdownOptions(string propertyId)
    {
        var schema = SchemaParser.Instance;
        if (!schema.IsLoaded)
            return Array.Empty<DropdownOption>();

        // Check if it's an enum type
        if (schema.HasEnum(propertyId))
        {
            return schema.GetEnumValues(propertyId)
                .Select(v => new DropdownOption(v.Name, v.Name, v.IntValue.ToString()))
                .ToList();
        }

        // Handle special property mappings
        return propertyId switch
        {
            "entity_property" => GetEntityPropertyOptions(),
            "faction" => GetFactionOptions(),
            "skill_type" => GetSkillTypeOptions(),
            "entity_flag" => GetEntityFlagOptions(),
            "cover_type" => GetCoverTypeOptions(),
            "morale_state" => GetMoraleStateOptions(),
            "damage_type" => GetDamageTypeOptions(),
            _ => Array.Empty<DropdownOption>()
        };
    }

    /// <summary>
    /// Get entity property options from schema.
    /// </summary>
    public IReadOnlyList<DropdownOption> GetEntityPropertyOptions()
    {
        var schema = SchemaParser.Instance;
        if (!schema.IsLoaded || !schema.HasEnum("EntityPropertyType"))
            return GetDefaultEntityPropertyOptions();

        return schema.GetEnumValues("EntityPropertyType")
            .Select(v => new DropdownOption(v.Name, FormatDisplayName(v.Name), v.IntValue.ToString()))
            .ToList();
    }

    /// <summary>
    /// Get faction options from schema.
    /// </summary>
    public IReadOnlyList<DropdownOption> GetFactionOptions()
    {
        var schema = SchemaParser.Instance;
        if (!schema.IsLoaded || !schema.HasEnum("FactionType"))
            return GetDefaultFactionOptions();

        return schema.GetEnumValues("FactionType")
            .Select(v => new DropdownOption(v.Name, FormatDisplayName(v.Name), v.IntValue.ToString()))
            .ToList();
    }

    /// <summary>
    /// Get skill type options from schema.
    /// </summary>
    public IReadOnlyList<DropdownOption> GetSkillTypeOptions()
    {
        var schema = SchemaParser.Instance;
        if (!schema.IsLoaded || !schema.HasEnum("SkillType"))
            return GetDefaultSkillTypeOptions();

        return schema.GetEnumValues("SkillType")
            .Select(v => new DropdownOption(v.Name, FormatDisplayName(v.Name), v.IntValue.ToString()))
            .ToList();
    }

    /// <summary>
    /// Get entity flag options from schema.
    /// </summary>
    public IReadOnlyList<DropdownOption> GetEntityFlagOptions()
    {
        var schema = SchemaParser.Instance;
        if (!schema.IsLoaded || !schema.HasEnum("EntityFlags"))
            return GetDefaultEntityFlagOptions();

        return schema.GetEnumValues("EntityFlags")
            .Select(v => new DropdownOption(v.Name, FormatDisplayName(v.Name), v.IntValue.ToString()))
            .ToList();
    }

    /// <summary>
    /// Get cover type options from schema.
    /// </summary>
    public IReadOnlyList<DropdownOption> GetCoverTypeOptions()
    {
        var schema = SchemaParser.Instance;
        if (!schema.IsLoaded || !schema.HasEnum("CoverType"))
            return GetDefaultCoverTypeOptions();

        return schema.GetEnumValues("CoverType")
            .Select(v => new DropdownOption(v.Name, FormatDisplayName(v.Name), v.IntValue.ToString()))
            .ToList();
    }

    /// <summary>
    /// Get morale state options from schema.
    /// </summary>
    public IReadOnlyList<DropdownOption> GetMoraleStateOptions()
    {
        var schema = SchemaParser.Instance;
        if (!schema.IsLoaded || !schema.HasEnum("MoraleState"))
            return GetDefaultMoraleStateOptions();

        return schema.GetEnumValues("MoraleState")
            .Select(v => new DropdownOption(v.Name, FormatDisplayName(v.Name), v.IntValue.ToString()))
            .ToList();
    }

    /// <summary>
    /// Get damage type options from schema.
    /// </summary>
    public IReadOnlyList<DropdownOption> GetDamageTypeOptions()
    {
        var schema = SchemaParser.Instance;
        if (!schema.IsLoaded || !schema.HasEnum("DamageType"))
            return GetDefaultDamageTypeOptions();

        return schema.GetEnumValues("DamageType")
            .Select(v => new DropdownOption(v.Name, FormatDisplayName(v.Name), v.IntValue.ToString()))
            .ToList();
    }

    /// <summary>
    /// Get boolean fields from SkillTemplate for skill condition nodes.
    /// </summary>
    public IReadOnlyList<DropdownOption> GetSkillBooleanFields()
    {
        var schema = SchemaParser.Instance;
        if (!schema.IsLoaded || !schema.HasTemplate("SkillTemplate"))
            return GetDefaultSkillBooleanFields();

        return schema.GetBooleanFields("SkillTemplate")
            .Select(f => new DropdownOption(f.Name, FormatDisplayName(f.Name)))
            .ToList();
    }

    /// <summary>
    /// Get enum fields from SkillTemplate for skill condition nodes.
    /// </summary>
    public IReadOnlyList<DropdownOption> GetSkillEnumFields()
    {
        var schema = SchemaParser.Instance;
        if (!schema.IsLoaded || !schema.HasTemplate("SkillTemplate"))
            return GetDefaultSkillEnumFields();

        return schema.GetEnumFields("SkillTemplate")
            .Select(f => new DropdownOption(f.Name, FormatDisplayName(f.Name), f.Type))
            .ToList();
    }

    #endregion

    #region Node Registration - Events

    private void RegisterEventNodes()
    {
        // Tactical (Combat) Events
        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "skill_used",
            DisplayName = "Skill Used",
            Description = "Triggered when any skill or ability is activated",
            Category = NodeCategory.Event,
            HookPoint = "skill_used",
            Color = "#4CAF50",
            OutputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "The entity using the skill"),
                new PortDefinition("skill", "Skill", PortDataType.Skill, "The skill being used"),
                new PortDefinition("target", "Target", PortDataType.Actor, "The target of the skill (if any)")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "damage_received",
            DisplayName = "Damage Received",
            Description = "Triggered when an entity takes damage",
            Category = NodeCategory.Event,
            HookPoint = "damage_received",
            Color = "#F44336",
            OutputPorts = new[]
            {
                new PortDefinition("target", "Target", PortDataType.Actor, "The entity receiving damage"),
                new PortDefinition("attacker", "Attacker", PortDataType.Actor, "The entity dealing damage"),
                new PortDefinition("skill", "Skill", PortDataType.Skill, "The skill that caused the damage"),
                new PortDefinition("amount", "Amount", PortDataType.Number, "The damage amount")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "actor_killed",
            DisplayName = "Actor Killed",
            Description = "Triggered when an entity is killed",
            Category = NodeCategory.Event,
            HookPoint = "actor_killed",
            Color = "#9C27B0",
            OutputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "The entity that was killed"),
                new PortDefinition("killer", "Killer", PortDataType.Actor, "The entity that dealt the killing blow"),
                new PortDefinition("skill", "Skill", PortDataType.Skill, "The skill that caused the death")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "attack_missed",
            DisplayName = "Attack Missed",
            Description = "Triggered when an attack fails to hit",
            Category = NodeCategory.Event,
            HookPoint = "attack_missed",
            Color = "#FF9800",
            OutputPorts = new[]
            {
                new PortDefinition("attacker", "Attacker", PortDataType.Actor, "The entity that missed"),
                new PortDefinition("target", "Target", PortDataType.Actor, "The intended target"),
                new PortDefinition("skill", "Skill", PortDataType.Skill, "The skill that missed")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "round_start",
            DisplayName = "Round Start",
            Description = "Triggered at the beginning of a combat round",
            Category = NodeCategory.Event,
            HookPoint = "round_start",
            Color = "#2196F3",
            OutputPorts = new[]
            {
                new PortDefinition("round_number", "Round", PortDataType.Number, "The current round number")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "round_end",
            DisplayName = "Round End",
            Description = "Triggered at the end of a combat round",
            Category = NodeCategory.Event,
            HookPoint = "round_end",
            Color = "#2196F3",
            OutputPorts = new[]
            {
                new PortDefinition("round_number", "Round", PortDataType.Number, "The current round number")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "turn_start",
            DisplayName = "Turn Start",
            Description = "Triggered when an entity's turn begins",
            Category = NodeCategory.Event,
            HookPoint = "turn_start",
            Color = "#00BCD4",
            OutputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "The entity whose turn is starting")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "turn_end",
            DisplayName = "Turn End",
            Description = "Triggered when an entity's turn ends",
            Category = NodeCategory.Event,
            HookPoint = "turn_end",
            Color = "#00BCD4",
            OutputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "The entity whose turn is ending")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "move_start",
            DisplayName = "Move Start",
            Description = "Triggered when an entity begins movement",
            Category = NodeCategory.Event,
            HookPoint = "move_start",
            Color = "#8BC34A",
            OutputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "The moving entity"),
                new PortDefinition("from_tile", "From", PortDataType.Any, "Starting position"),
                new PortDefinition("to_tile", "To", PortDataType.Any, "Destination position")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "move_complete",
            DisplayName = "Move Complete",
            Description = "Triggered when an entity completes movement",
            Category = NodeCategory.Event,
            HookPoint = "move_complete",
            Color = "#8BC34A",
            OutputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "The moving entity"),
                new PortDefinition("from_tile", "From", PortDataType.Any, "Starting position"),
                new PortDefinition("to_tile", "To", PortDataType.Any, "Final position")
            }
        });

        // Strategic Events
        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "leader_hired",
            DisplayName = "Leader Hired",
            Description = "Triggered when a leader joins the roster",
            Category = NodeCategory.Event,
            HookPoint = "leader_hired",
            Color = "#673AB7",
            OutputPorts = new[]
            {
                new PortDefinition("leader", "Leader", PortDataType.Actor, "The hired leader"),
                new PortDefinition("template", "Template", PortDataType.Any, "The leader template")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "mission_ended",
            DisplayName = "Mission Ended",
            Description = "Triggered when a mission completes",
            Category = NodeCategory.Event,
            HookPoint = "mission_ended",
            Color = "#607D8B",
            OutputPorts = new[]
            {
                new PortDefinition("mission", "Mission", PortDataType.Any, "The completed mission"),
                new PortDefinition("status", "Status", PortDataType.String, "Success, failure, or aborted")
            }
        });
    }

    #endregion

    #region Node Registration - Conditions

    private void RegisterConditionNodes()
    {
        // Generic property check condition
        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "property_check",
            DisplayName = "Property Check",
            Description = "Check any property value with comparison operators",
            Category = NodeCategory.Condition,
            Color = "#FFC107",
            InputPorts = new[]
            {
                new PortDefinition("input", "Input", PortDataType.Any, "Value to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Pass", PortDataType.Flow, "Condition is true"),
                new PortDefinition("fail", "Fail", PortDataType.Flow, "Condition is false")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("property", "Property", ConfigPropertyType.Dropdown, "Property to check")
                {
                    DropdownSource = "dynamic:property"
                },
                new ConfigPropertyDefinition("operator", "Operator", ConfigPropertyType.Dropdown, "Comparison operator")
                {
                    DefaultValue = "==",
                    Options = new[] { "==", "!=", "<", ">", "<=", ">=", "contains" }
                },
                new ConfigPropertyDefinition("value", "Value", ConfigPropertyType.Text, "Value to compare against")
            }
        });

        // Skill boolean checks (from schema)
        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "skill_is_attack",
            DisplayName = "Is Attack?",
            Description = "Check if the skill is an attack skill",
            Category = NodeCategory.Condition,
            Color = "#FF5722",
            InputPorts = new[]
            {
                new PortDefinition("skill", "Skill", PortDataType.Skill, "Skill to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Yes", PortDataType.Flow, "Skill is an attack"),
                new PortDefinition("fail", "No", PortDataType.Flow, "Skill is not an attack")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "skill_is_silent",
            DisplayName = "Is Silent?",
            Description = "Check if the skill is silent (doesn't break concealment)",
            Category = NodeCategory.Condition,
            Color = "#795548",
            InputPorts = new[]
            {
                new PortDefinition("skill", "Skill", PortDataType.Skill, "Skill to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Yes", PortDataType.Flow, "Skill is silent"),
                new PortDefinition("fail", "No", PortDataType.Flow, "Skill is not silent")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "skill_is_active",
            DisplayName = "Is Active?",
            Description = "Check if the skill requires activation (not passive)",
            Category = NodeCategory.Condition,
            Color = "#E91E63",
            InputPorts = new[]
            {
                new PortDefinition("skill", "Skill", PortDataType.Skill, "Skill to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Yes", PortDataType.Flow, "Skill is active"),
                new PortDefinition("fail", "No", PortDataType.Flow, "Skill is passive")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "skill_is_targeted",
            DisplayName = "Is Targeted?",
            Description = "Check if the skill requires a target",
            Category = NodeCategory.Condition,
            Color = "#3F51B5",
            InputPorts = new[]
            {
                new PortDefinition("skill", "Skill", PortDataType.Skill, "Skill to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Yes", PortDataType.Flow, "Skill is targeted"),
                new PortDefinition("fail", "No", PortDataType.Flow, "Skill is not targeted")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "skill_ignores_cover",
            DisplayName = "Ignores Cover?",
            Description = "Check if the skill ignores cover bonuses",
            Category = NodeCategory.Condition,
            Color = "#009688",
            InputPorts = new[]
            {
                new PortDefinition("skill", "Skill", PortDataType.Skill, "Skill to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Yes", PortDataType.Flow, "Skill ignores cover"),
                new PortDefinition("fail", "No", PortDataType.Flow, "Skill respects cover")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "skill_costs_zero_ap",
            DisplayName = "Costs 0 AP?",
            Description = "Check if the skill costs zero action points",
            Category = NodeCategory.Condition,
            Color = "#CDDC39",
            InputPorts = new[]
            {
                new PortDefinition("skill", "Skill", PortDataType.Skill, "Skill to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Yes", PortDataType.Flow, "Free action"),
                new PortDefinition("fail", "No", PortDataType.Flow, "Costs AP")
            }
        });

        // Actor faction checks
        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "actor_faction_check",
            DisplayName = "Faction Check",
            Description = "Check the actor's faction",
            Category = NodeCategory.Condition,
            Color = "#9E9E9E",
            InputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Pass", PortDataType.Flow, "Faction matches"),
                new PortDefinition("fail", "Fail", PortDataType.Flow, "Faction doesn't match")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("faction", "Faction", ConfigPropertyType.Dropdown, "Faction to check for")
                {
                    DropdownSource = "faction"
                }
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "actor_is_player",
            DisplayName = "Is Player?",
            Description = "Check if the actor belongs to the player faction",
            Category = NodeCategory.Condition,
            Color = "#4CAF50",
            InputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Yes", PortDataType.Flow, "Actor is player"),
                new PortDefinition("fail", "No", PortDataType.Flow, "Actor is not player")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "actor_is_enemy",
            DisplayName = "Is Enemy?",
            Description = "Check if the actor is hostile to the player",
            Category = NodeCategory.Condition,
            Color = "#F44336",
            InputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Yes", PortDataType.Flow, "Actor is enemy"),
                new PortDefinition("fail", "No", PortDataType.Flow, "Actor is not enemy")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "actor_is_ally",
            DisplayName = "Is Ally?",
            Description = "Check if the actor is allied with the player",
            Category = NodeCategory.Condition,
            Color = "#2196F3",
            InputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Yes", PortDataType.Flow, "Actor is ally"),
                new PortDefinition("fail", "No", PortDataType.Flow, "Actor is not ally")
            }
        });

        // Actor state checks
        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "actor_has_flag",
            DisplayName = "Has Flag?",
            Description = "Check if the actor has a specific flag set",
            Category = NodeCategory.Condition,
            Color = "#FF9800",
            InputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Yes", PortDataType.Flow, "Flag is set"),
                new PortDefinition("fail", "No", PortDataType.Flow, "Flag is not set")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("flag", "Flag", ConfigPropertyType.Dropdown, "Flag to check")
                {
                    DropdownSource = "entity_flag"
                }
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "actor_is_stunned",
            DisplayName = "Is Stunned?",
            Description = "Check if the actor is currently stunned",
            Category = NodeCategory.Condition,
            Color = "#9C27B0",
            InputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Yes", PortDataType.Flow, "Actor is stunned"),
                new PortDefinition("fail", "No", PortDataType.Flow, "Actor is not stunned")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "actor_is_rooted",
            DisplayName = "Is Rooted?",
            Description = "Check if the actor is currently rooted (cannot move)",
            Category = NodeCategory.Condition,
            Color = "#795548",
            InputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Yes", PortDataType.Flow, "Actor is rooted"),
                new PortDefinition("fail", "No", PortDataType.Flow, "Actor is not rooted")
            }
        });

        // Cover checks
        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "actor_in_cover",
            DisplayName = "In Cover?",
            Description = "Check if the actor is in any cover",
            Category = NodeCategory.Condition,
            Color = "#607D8B",
            InputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Yes", PortDataType.Flow, "Actor is in cover"),
                new PortDefinition("fail", "No", PortDataType.Flow, "Actor is not in cover")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "actor_cover_type",
            DisplayName = "Cover Type?",
            Description = "Check the actor's cover type",
            Category = NodeCategory.Condition,
            Color = "#455A64",
            InputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Pass", PortDataType.Flow, "Cover type matches"),
                new PortDefinition("fail", "Fail", PortDataType.Flow, "Cover type doesn't match")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("cover_type", "Cover Type", ConfigPropertyType.Dropdown, "Cover type to check for")
                {
                    DropdownSource = "cover_type"
                }
            }
        });

        // Morale checks
        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "actor_morale_state",
            DisplayName = "Morale State?",
            Description = "Check the actor's morale state",
            Category = NodeCategory.Condition,
            Color = "#E91E63",
            InputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Pass", PortDataType.Flow, "Morale state matches"),
                new PortDefinition("fail", "Fail", PortDataType.Flow, "Morale state doesn't match")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("morale_state", "State", ConfigPropertyType.Dropdown, "Morale state to check for")
                {
                    DropdownSource = "morale_state"
                }
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "actor_is_wavering",
            DisplayName = "Is Wavering?",
            Description = "Check if the actor's morale is wavering",
            Category = NodeCategory.Condition,
            Color = "#FFC107",
            InputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Yes", PortDataType.Flow, "Actor is wavering"),
                new PortDefinition("fail", "No", PortDataType.Flow, "Actor is not wavering")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "actor_is_panicked",
            DisplayName = "Is Panicked?",
            Description = "Check if the actor is panicked/fleeing",
            Category = NodeCategory.Condition,
            Color = "#FF5722",
            InputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Yes", PortDataType.Flow, "Actor is panicked"),
                new PortDefinition("fail", "No", PortDataType.Flow, "Actor is not panicked")
            }
        });

        // Numeric comparisons
        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "compare_numbers",
            DisplayName = "Compare Numbers",
            Description = "Compare two numeric values",
            Category = NodeCategory.Condition,
            Color = "#00BCD4",
            InputPorts = new[]
            {
                new PortDefinition("a", "A", PortDataType.Number, "First number"),
                new PortDefinition("b", "B", PortDataType.Number, "Second number")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Pass", PortDataType.Flow, "Comparison is true"),
                new PortDefinition("fail", "Fail", PortDataType.Flow, "Comparison is false")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("operator", "Operator", ConfigPropertyType.Dropdown, "Comparison operator")
                {
                    DefaultValue = "==",
                    Options = new[] { "==", "!=", "<", ">", "<=", ">=" }
                }
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "health_threshold",
            DisplayName = "Health Threshold",
            Description = "Check if actor health is above/below a threshold",
            Category = NodeCategory.Condition,
            Color = "#4CAF50",
            InputPorts = new[]
            {
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to check")
            },
            OutputPorts = new[]
            {
                new PortDefinition("pass", "Pass", PortDataType.Flow, "Health meets threshold"),
                new PortDefinition("fail", "Fail", PortDataType.Flow, "Health doesn't meet threshold")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("operator", "Operator", ConfigPropertyType.Dropdown, "Comparison")
                {
                    DefaultValue = "<",
                    Options = new[] { "<", "<=", ">", ">=", "==" }
                },
                new ConfigPropertyDefinition("threshold", "Threshold %", ConfigPropertyType.Number, "Health percentage (0-100)")
                {
                    DefaultValue = "50",
                    Min = 0,
                    Max = 100
                }
            }
        });
    }

    #endregion

    #region Node Registration - Actions

    private void RegisterActionNodes()
    {
        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "add_effect",
            DisplayName = "Add Effect",
            Description = "Add a stat modifier effect to an actor",
            Category = NodeCategory.Action,
            Color = "#9C27B0",
            InputPorts = new[]
            {
                new PortDefinition("flow_in", "Flow", PortDataType.Flow, "Execution flow"),
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to modify")
            },
            OutputPorts = new[]
            {
                new PortDefinition("flow_out", "Flow", PortDataType.Flow, "Continue execution")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("property", "Property", ConfigPropertyType.Dropdown, "Property to modify")
                {
                    DropdownSource = "entity_property"
                },
                new ConfigPropertyDefinition("modifier", "Modifier", ConfigPropertyType.Number, "Amount to add/subtract")
                {
                    DefaultValue = "0"
                },
                new ConfigPropertyDefinition("duration", "Duration", ConfigPropertyType.Number, "Duration in rounds (0 = permanent)")
                {
                    DefaultValue = "0",
                    Min = 0
                }
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "damage",
            DisplayName = "Deal Damage",
            Description = "Deal damage to an actor",
            Category = NodeCategory.Action,
            Color = "#F44336",
            InputPorts = new[]
            {
                new PortDefinition("flow_in", "Flow", PortDataType.Flow, "Execution flow"),
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to damage")
            },
            OutputPorts = new[]
            {
                new PortDefinition("flow_out", "Flow", PortDataType.Flow, "Continue execution")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("amount", "Amount", ConfigPropertyType.Number, "Damage amount")
                {
                    DefaultValue = "10",
                    Min = 1
                },
                new ConfigPropertyDefinition("damage_type", "Type", ConfigPropertyType.Dropdown, "Damage type")
                {
                    DropdownSource = "damage_type"
                },
                new ConfigPropertyDefinition("bypass_armor", "Bypass Armor", ConfigPropertyType.Boolean, "Ignore armor reduction")
                {
                    DefaultValue = "false"
                }
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "heal",
            DisplayName = "Heal",
            Description = "Restore health to an actor",
            Category = NodeCategory.Action,
            Color = "#4CAF50",
            InputPorts = new[]
            {
                new PortDefinition("flow_in", "Flow", PortDataType.Flow, "Execution flow"),
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to heal")
            },
            OutputPorts = new[]
            {
                new PortDefinition("flow_out", "Flow", PortDataType.Flow, "Continue execution")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("amount", "Amount", ConfigPropertyType.Number, "Heal amount")
                {
                    DefaultValue = "10",
                    Min = 1
                },
                new ConfigPropertyDefinition("can_overheal", "Can Overheal", ConfigPropertyType.Boolean, "Allow healing above max HP")
                {
                    DefaultValue = "false"
                }
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "set_flag",
            DisplayName = "Set Flag",
            Description = "Set or clear an entity flag",
            Category = NodeCategory.Action,
            Color = "#FF9800",
            InputPorts = new[]
            {
                new PortDefinition("flow_in", "Flow", PortDataType.Flow, "Execution flow"),
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to modify")
            },
            OutputPorts = new[]
            {
                new PortDefinition("flow_out", "Flow", PortDataType.Flow, "Continue execution")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("flag", "Flag", ConfigPropertyType.Dropdown, "Flag to set")
                {
                    DropdownSource = "entity_flag"
                },
                new ConfigPropertyDefinition("value", "Value", ConfigPropertyType.Boolean, "True to set, false to clear")
                {
                    DefaultValue = "true"
                }
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "stun",
            DisplayName = "Stun",
            Description = "Stun an actor for a number of rounds",
            Category = NodeCategory.Action,
            Color = "#9C27B0",
            InputPorts = new[]
            {
                new PortDefinition("flow_in", "Flow", PortDataType.Flow, "Execution flow"),
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to stun")
            },
            OutputPorts = new[]
            {
                new PortDefinition("flow_out", "Flow", PortDataType.Flow, "Continue execution")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("duration", "Duration", ConfigPropertyType.Number, "Stun duration in rounds")
                {
                    DefaultValue = "1",
                    Min = 1
                }
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "root",
            DisplayName = "Root",
            Description = "Root an actor in place for a number of rounds",
            Category = NodeCategory.Action,
            Color = "#795548",
            InputPorts = new[]
            {
                new PortDefinition("flow_in", "Flow", PortDataType.Flow, "Execution flow"),
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to root")
            },
            OutputPorts = new[]
            {
                new PortDefinition("flow_out", "Flow", PortDataType.Flow, "Continue execution")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("duration", "Duration", ConfigPropertyType.Number, "Root duration in rounds")
                {
                    DefaultValue = "1",
                    Min = 1
                }
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "suppress",
            DisplayName = "Suppress",
            Description = "Apply suppression to an actor",
            Category = NodeCategory.Action,
            Color = "#607D8B",
            InputPorts = new[]
            {
                new PortDefinition("flow_in", "Flow", PortDataType.Flow, "Execution flow"),
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to suppress")
            },
            OutputPorts = new[]
            {
                new PortDefinition("flow_out", "Flow", PortDataType.Flow, "Continue execution")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("amount", "Amount", ConfigPropertyType.Number, "Suppression amount")
                {
                    DefaultValue = "50",
                    Min = 1,
                    Max = 100
                }
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "modify_morale",
            DisplayName = "Modify Morale",
            Description = "Modify an actor's morale",
            Category = NodeCategory.Action,
            Color = "#E91E63",
            InputPorts = new[]
            {
                new PortDefinition("flow_in", "Flow", PortDataType.Flow, "Execution flow"),
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to modify")
            },
            OutputPorts = new[]
            {
                new PortDefinition("flow_out", "Flow", PortDataType.Flow, "Continue execution")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("delta", "Delta", ConfigPropertyType.Number, "Morale change (positive or negative)")
                {
                    DefaultValue = "10"
                }
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "grant_action_points",
            DisplayName = "Grant Action Points",
            Description = "Grant additional action points to an actor",
            Category = NodeCategory.Action,
            Color = "#2196F3",
            InputPorts = new[]
            {
                new PortDefinition("flow_in", "Flow", PortDataType.Flow, "Execution flow"),
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to grant AP")
            },
            OutputPorts = new[]
            {
                new PortDefinition("flow_out", "Flow", PortDataType.Flow, "Continue execution")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("amount", "Amount", ConfigPropertyType.Number, "Action points to grant")
                {
                    DefaultValue = "1",
                    Min = 1
                }
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "teleport",
            DisplayName = "Teleport",
            Description = "Teleport an actor to a location",
            Category = NodeCategory.Action,
            Color = "#00BCD4",
            InputPorts = new[]
            {
                new PortDefinition("flow_in", "Flow", PortDataType.Flow, "Execution flow"),
                new PortDefinition("actor", "Actor", PortDataType.Actor, "Actor to teleport"),
                new PortDefinition("destination", "Destination", PortDataType.Any, "Target location")
            },
            OutputPorts = new[]
            {
                new PortDefinition("flow_out", "Flow", PortDataType.Flow, "Continue execution")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "spawn_unit",
            DisplayName = "Spawn Unit",
            Description = "Spawn a new unit at a location",
            Category = NodeCategory.Action,
            Color = "#8BC34A",
            InputPorts = new[]
            {
                new PortDefinition("flow_in", "Flow", PortDataType.Flow, "Execution flow"),
                new PortDefinition("location", "Location", PortDataType.Any, "Spawn location")
            },
            OutputPorts = new[]
            {
                new PortDefinition("flow_out", "Flow", PortDataType.Flow, "Continue execution"),
                new PortDefinition("spawned", "Spawned", PortDataType.Actor, "The spawned unit")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("template", "Template", ConfigPropertyType.Text, "Unit template ID"),
                new ConfigPropertyDefinition("faction", "Faction", ConfigPropertyType.Dropdown, "Unit faction")
                {
                    DropdownSource = "faction"
                }
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "change_faction_trust",
            DisplayName = "Change Faction Trust",
            Description = "Modify trust with a faction",
            Category = NodeCategory.Action,
            Color = "#673AB7",
            InputPorts = new[]
            {
                new PortDefinition("flow_in", "Flow", PortDataType.Flow, "Execution flow")
            },
            OutputPorts = new[]
            {
                new PortDefinition("flow_out", "Flow", PortDataType.Flow, "Continue execution")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("faction", "Faction", ConfigPropertyType.Dropdown, "Target faction")
                {
                    DropdownSource = "faction"
                },
                new ConfigPropertyDefinition("delta", "Delta", ConfigPropertyType.Number, "Trust change amount")
                {
                    DefaultValue = "10"
                }
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "add_item",
            DisplayName = "Add Item",
            Description = "Add an item to the player inventory",
            Category = NodeCategory.Action,
            Color = "#FFEB3B",
            InputPorts = new[]
            {
                new PortDefinition("flow_in", "Flow", PortDataType.Flow, "Execution flow")
            },
            OutputPorts = new[]
            {
                new PortDefinition("flow_out", "Flow", PortDataType.Flow, "Continue execution")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("item_id", "Item", ConfigPropertyType.Text, "Item template ID"),
                new ConfigPropertyDefinition("quantity", "Quantity", ConfigPropertyType.Number, "Number of items")
                {
                    DefaultValue = "1",
                    Min = 1
                }
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "log",
            DisplayName = "Log Message",
            Description = "Output a debug message to the console",
            Category = NodeCategory.Action,
            Color = "#9E9E9E",
            InputPorts = new[]
            {
                new PortDefinition("flow_in", "Flow", PortDataType.Flow, "Execution flow")
            },
            OutputPorts = new[]
            {
                new PortDefinition("flow_out", "Flow", PortDataType.Flow, "Continue execution")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("message", "Message", ConfigPropertyType.Text, "Message to log"),
                new ConfigPropertyDefinition("level", "Level", ConfigPropertyType.Dropdown, "Log level")
                {
                    DefaultValue = "Info",
                    Options = new[] { "Debug", "Info", "Warning", "Error" }
                }
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "play_sound",
            DisplayName = "Play Sound",
            Description = "Play a sound effect",
            Category = NodeCategory.Action,
            Color = "#00BCD4",
            InputPorts = new[]
            {
                new PortDefinition("flow_in", "Flow", PortDataType.Flow, "Execution flow"),
                new PortDefinition("location", "Location", PortDataType.Any, "Sound location (optional)")
            },
            OutputPorts = new[]
            {
                new PortDefinition("flow_out", "Flow", PortDataType.Flow, "Continue execution")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("sound_id", "Sound", ConfigPropertyType.Text, "Sound effect ID")
            }
        });
    }

    #endregion

    #region Node Registration - Logic

    private void RegisterLogicNodes()
    {
        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "and",
            DisplayName = "AND",
            Description = "True only if all inputs are true",
            Category = NodeCategory.Logic,
            Color = "#3F51B5",
            InputPorts = new[]
            {
                new PortDefinition("a", "A", PortDataType.Bool, "First condition"),
                new PortDefinition("b", "B", PortDataType.Bool, "Second condition")
            },
            OutputPorts = new[]
            {
                new PortDefinition("result", "Result", PortDataType.Bool, "All inputs true")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "or",
            DisplayName = "OR",
            Description = "True if any input is true",
            Category = NodeCategory.Logic,
            Color = "#3F51B5",
            InputPorts = new[]
            {
                new PortDefinition("a", "A", PortDataType.Bool, "First condition"),
                new PortDefinition("b", "B", PortDataType.Bool, "Second condition")
            },
            OutputPorts = new[]
            {
                new PortDefinition("result", "Result", PortDataType.Bool, "Any input true")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "not",
            DisplayName = "NOT",
            Description = "Inverts the input boolean",
            Category = NodeCategory.Logic,
            Color = "#3F51B5",
            InputPorts = new[]
            {
                new PortDefinition("input", "Input", PortDataType.Bool, "Value to invert")
            },
            OutputPorts = new[]
            {
                new PortDefinition("result", "Result", PortDataType.Bool, "Inverted value")
            }
        });
    }

    #endregion

    #region Node Registration - Values

    private void RegisterValueNodes()
    {
        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "constant",
            DisplayName = "Constant",
            Description = "A fixed value",
            Category = NodeCategory.Value,
            Color = "#607D8B",
            OutputPorts = new[]
            {
                new PortDefinition("value", "Value", PortDataType.Any, "The constant value")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("type", "Type", ConfigPropertyType.Dropdown, "Value type")
                {
                    DefaultValue = "number",
                    Options = new[] { "number", "string", "boolean" }
                },
                new ConfigPropertyDefinition("value", "Value", ConfigPropertyType.Text, "The value")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "variable",
            DisplayName = "Variable",
            Description = "Reference to a mod variable",
            Category = NodeCategory.Value,
            Color = "#795548",
            OutputPorts = new[]
            {
                new PortDefinition("value", "Value", PortDataType.Any, "The variable value")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("variable_id", "Variable", ConfigPropertyType.Text, "Variable ID")
            }
        });

        RegisterNode(new NodeTypeDefinition
        {
            TypeId = "random",
            DisplayName = "Random Number",
            Description = "Generate a random number in range",
            Category = NodeCategory.Value,
            Color = "#FF5722",
            OutputPorts = new[]
            {
                new PortDefinition("value", "Value", PortDataType.Number, "Random value")
            },
            ConfigProperties = new[]
            {
                new ConfigPropertyDefinition("min", "Min", ConfigPropertyType.Number, "Minimum value (inclusive)")
                {
                    DefaultValue = "0"
                },
                new ConfigPropertyDefinition("max", "Max", ConfigPropertyType.Number, "Maximum value (inclusive)")
                {
                    DefaultValue = "100"
                },
                new ConfigPropertyDefinition("integer", "Integer Only", ConfigPropertyType.Boolean, "Return whole numbers only")
                {
                    DefaultValue = "true"
                }
            }
        });
    }

    #endregion

    #region Helper Methods

    private void RegisterNode(NodeTypeDefinition node)
    {
        _nodeTypes[node.TypeId] = node;
        if (_nodesByCategory.TryGetValue(node.Category, out var list))
        {
            list.Add(node);
        }
    }

    private static string FormatDisplayName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Convert PascalCase/camelCase to spaced words
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
            {
                result.Append(' ');
            }
            result.Append(c);
        }
        return result.ToString();
    }

    #endregion

    #region Default Options (Fallbacks when schema not loaded)

    private static IReadOnlyList<DropdownOption> GetDefaultEntityPropertyOptions() => new[]
    {
        new DropdownOption("Concealment", "Concealment"),
        new DropdownOption("Accuracy", "Accuracy"),
        new DropdownOption("Defense", "Defense"),
        new DropdownOption("Mobility", "Mobility"),
        new DropdownOption("Armor", "Armor"),
        new DropdownOption("CritChance", "Crit Chance"),
        new DropdownOption("Damage", "Damage"),
        new DropdownOption("Will", "Will"),
        new DropdownOption("Dodge", "Dodge"),
        new DropdownOption("HackDefense", "Hack Defense")
    };

    private static IReadOnlyList<DropdownOption> GetDefaultFactionOptions() => new[]
    {
        new DropdownOption("Player", "Player"),
        new DropdownOption("Ally", "Ally"),
        new DropdownOption("Enemy", "Enemy"),
        new DropdownOption("Neutral", "Neutral"),
        new DropdownOption("Wildlife", "Wildlife")
    };

    private static IReadOnlyList<DropdownOption> GetDefaultSkillTypeOptions() => new[]
    {
        new DropdownOption("Attack", "Attack"),
        new DropdownOption("Support", "Support"),
        new DropdownOption("Movement", "Movement"),
        new DropdownOption("Defensive", "Defensive"),
        new DropdownOption("Utility", "Utility")
    };

    private static IReadOnlyList<DropdownOption> GetDefaultEntityFlagOptions() => new[]
    {
        new DropdownOption("Stunned", "Stunned"),
        new DropdownOption("Rooted", "Rooted"),
        new DropdownOption("Immune", "Immune"),
        new DropdownOption("Invisible", "Invisible"),
        new DropdownOption("Overwatch", "Overwatch"),
        new DropdownOption("Suppressed", "Suppressed"),
        new DropdownOption("Flanked", "Flanked"),
        new DropdownOption("Burning", "Burning"),
        new DropdownOption("Poisoned", "Poisoned"),
        new DropdownOption("Bleeding", "Bleeding")
    };

    private static IReadOnlyList<DropdownOption> GetDefaultCoverTypeOptions() => new[]
    {
        new DropdownOption("None", "None"),
        new DropdownOption("Light", "Light"),
        new DropdownOption("Medium", "Medium"),
        new DropdownOption("Heavy", "Heavy"),
        new DropdownOption("Full", "Full")
    };

    private static IReadOnlyList<DropdownOption> GetDefaultMoraleStateOptions() => new[]
    {
        new DropdownOption("Steady", "Steady"),
        new DropdownOption("Wavering", "Wavering"),
        new DropdownOption("Shaken", "Shaken"),
        new DropdownOption("Panicked", "Panicked"),
        new DropdownOption("Broken", "Broken")
    };

    private static IReadOnlyList<DropdownOption> GetDefaultDamageTypeOptions() => new[]
    {
        new DropdownOption("Physical", "Physical"),
        new DropdownOption("Fire", "Fire"),
        new DropdownOption("Explosive", "Explosive"),
        new DropdownOption("Energy", "Energy"),
        new DropdownOption("Psychic", "Psychic"),
        new DropdownOption("Poison", "Poison"),
        new DropdownOption("True", "True")
    };

    private static IReadOnlyList<DropdownOption> GetDefaultSkillBooleanFields() => new[]
    {
        new DropdownOption("IsAttack", "Is Attack"),
        new DropdownOption("IsSilent", "Is Silent"),
        new DropdownOption("IsActive", "Is Active"),
        new DropdownOption("IsTargeted", "Is Targeted"),
        new DropdownOption("IgnoresCover", "Ignores Cover"),
        new DropdownOption("CostsZeroAP", "Costs 0 AP"),
        new DropdownOption("EndsMove", "Ends Move"),
        new DropdownOption("EndsTurn", "Ends Turn")
    };

    private static IReadOnlyList<DropdownOption> GetDefaultSkillEnumFields() => new[]
    {
        new DropdownOption("SkillType", "Skill Type", "SkillType"),
        new DropdownOption("DamageType", "Damage Type", "DamageType"),
        new DropdownOption("TargetType", "Target Type", "SkillTarget")
    };

    #endregion
}

#region Data Models

/// <summary>
/// Categories of nodes in the visual editor.
/// </summary>
public enum NodeCategory
{
    /// <summary>Event nodes - entry points triggered by game hooks</summary>
    Event,
    /// <summary>Condition nodes - filter/branch the flow</summary>
    Condition,
    /// <summary>Action nodes - produce effects/modify game state</summary>
    Action,
    /// <summary>Logic nodes - boolean combinators</summary>
    Logic,
    /// <summary>Value nodes - constants and computed values</summary>
    Value
}

/// <summary>
/// Data types for node ports.
/// </summary>
public enum PortDataType
{
    /// <summary>Execution flow (no data)</summary>
    Flow,
    /// <summary>Entity/actor reference</summary>
    Actor,
    /// <summary>Skill/ability reference</summary>
    Skill,
    /// <summary>Numeric value</summary>
    Number,
    /// <summary>Boolean value</summary>
    Bool,
    /// <summary>Text value</summary>
    String,
    /// <summary>Any compatible type</summary>
    Any
}

/// <summary>
/// Types of configuration properties on nodes.
/// </summary>
public enum ConfigPropertyType
{
    /// <summary>Text input field</summary>
    Text,
    /// <summary>Numeric input field</summary>
    Number,
    /// <summary>Checkbox/toggle</summary>
    Boolean,
    /// <summary>Dropdown selection</summary>
    Dropdown,
    /// <summary>Color picker</summary>
    Color
}

/// <summary>
/// Definition of a node type in the visual editor.
/// </summary>
public sealed class NodeTypeDefinition
{
    /// <summary>Unique type identifier (e.g., "skill_used", "add_effect")</summary>
    public string TypeId { get; set; }

    /// <summary>Human-readable name for display</summary>
    public string DisplayName { get; set; }

    /// <summary>Description of what the node does</summary>
    public string Description { get; set; }

    /// <summary>Node category (event, condition, action, logic, value)</summary>
    public NodeCategory Category { get; set; }

    /// <summary>For event nodes, the hook point this node responds to</summary>
    public string HookPoint { get; set; }

    /// <summary>Color for the node header (hex format)</summary>
    public string Color { get; set; }

    /// <summary>Input ports on this node</summary>
    public PortDefinition[] InputPorts { get; set; } = Array.Empty<PortDefinition>();

    /// <summary>Output ports on this node</summary>
    public PortDefinition[] OutputPorts { get; set; } = Array.Empty<PortDefinition>();

    /// <summary>Configuration properties editable in the node inspector</summary>
    public ConfigPropertyDefinition[] ConfigProperties { get; set; } = Array.Empty<ConfigPropertyDefinition>();

    public override string ToString() => $"{Category}/{TypeId}: {DisplayName}";
}

/// <summary>
/// Definition of a port (input or output) on a node.
/// </summary>
public sealed class PortDefinition
{
    /// <summary>Port identifier (unique within the node)</summary>
    public string Id { get; set; }

    /// <summary>Display name for the port</summary>
    public string DisplayName { get; set; }

    /// <summary>Data type carried by this port</summary>
    public PortDataType DataType { get; set; }

    /// <summary>Description/tooltip for the port</summary>
    public string Description { get; set; }

    /// <summary>Whether this port accepts multiple connections (inputs only)</summary>
    public bool AllowMultiple { get; set; }

    public PortDefinition() { }

    public PortDefinition(string id, string displayName, PortDataType dataType, string description = null)
    {
        Id = id;
        DisplayName = displayName;
        DataType = dataType;
        Description = description;
    }

    public override string ToString() => $"{Id} ({DataType})";
}

/// <summary>
/// Definition of a configurable property on a node.
/// </summary>
public sealed class ConfigPropertyDefinition
{
    /// <summary>Property identifier</summary>
    public string Id { get; set; }

    /// <summary>Display label</summary>
    public string DisplayName { get; set; }

    /// <summary>Property type (text, number, dropdown, etc.)</summary>
    public ConfigPropertyType PropertyType { get; set; }

    /// <summary>Description/tooltip</summary>
    public string Description { get; set; }

    /// <summary>Default value as string</summary>
    public string DefaultValue { get; set; }

    /// <summary>For dropdowns: static options array</summary>
    public string[] Options { get; set; }

    /// <summary>For dropdowns: dynamic source identifier (e.g., "faction", "entity_property")</summary>
    public string DropdownSource { get; set; }

    /// <summary>For numbers: minimum value</summary>
    public double? Min { get; set; }

    /// <summary>For numbers: maximum value</summary>
    public double? Max { get; set; }

    /// <summary>For numbers: step increment</summary>
    public double? Step { get; set; }

    public ConfigPropertyDefinition() { }

    public ConfigPropertyDefinition(string id, string displayName, ConfigPropertyType propertyType, string description = null)
    {
        Id = id;
        DisplayName = displayName;
        PropertyType = propertyType;
        Description = description;
    }

    public override string ToString() => $"{Id}: {PropertyType}";
}

/// <summary>
/// Represents an option in a dropdown menu.
/// </summary>
public sealed class DropdownOption
{
    /// <summary>The value stored when this option is selected</summary>
    public string Value { get; set; }

    /// <summary>The display text shown to the user</summary>
    public string DisplayText { get; set; }

    /// <summary>Optional additional data (e.g., enum type name)</summary>
    public string Data { get; set; }

    public DropdownOption() { }

    public DropdownOption(string value, string displayText, string data = null)
    {
        Value = value;
        DisplayText = displayText;
        Data = data;
    }

    public override string ToString() => DisplayText ?? Value;
}

#endregion
