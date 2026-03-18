#nullable enable

using System;

namespace Menace.Modkit.App.VisualEditor;

/// <summary>
/// Factory for creating properly configured timeline nodes.
/// </summary>
public static class NodeFactory
{
    /// <summary>
    /// Create a node of the specified type with default ports configured.
    /// </summary>
    public static TimelineNode Create(TimelineNodeType type, double x = 0, double y = 0)
    {
        var node = new TimelineNode
        {
            NodeType = type,
            X = x,
            Y = y,
            Title = GetDefaultTitle(type)
        };

        ConfigurePorts(node);
        return node;
    }

    private static string GetDefaultTitle(TimelineNodeType type) => type switch
    {
        // Triggers
        TimelineNodeType.TriggerSkillUsed => "On Skill Used",
        TimelineNodeType.TriggerDamageReceived => "On Damage",
        TimelineNodeType.TriggerActorKilled => "On Kill",
        TimelineNodeType.TriggerMovementStarted => "On Move Start",
        TimelineNodeType.TriggerMovementFinished => "On Move End",
        TimelineNodeType.TriggerTurnStart => "On Turn Start",
        TimelineNodeType.TriggerTurnEnd => "On Turn End",
        TimelineNodeType.TriggerRoundStart => "On Round Start",
        TimelineNodeType.TriggerRoundEnd => "On Round End",
        TimelineNodeType.TriggerHitpointsChanged => "On HP Change",
        TimelineNodeType.TriggerSuppressed => "On Suppressed",

        // Data
        TimelineNodeType.Actor => "Actor",
        TimelineNodeType.Skill => "Skill",
        TimelineNodeType.Tile => "Tile",

        // Template selectors
        TimelineNodeType.TemplateRefActor => "Actor Template",
        TimelineNodeType.TemplateRefSkill => "Skill Template",
        TimelineNodeType.TemplateRefItem => "Item Template",

        // Logic
        TimelineNodeType.Condition => "If",
        TimelineNodeType.Compare => "Compare",
        TimelineNodeType.MathOp => "Math",
        TimelineNodeType.Not => "NOT",
        TimelineNodeType.And => "AND",
        TimelineNodeType.Or => "OR",

        // Variables
        TimelineNodeType.SetVariable => "Set Var",
        TimelineNodeType.GetVariable => "Get Var",

        // Loops
        TimelineNodeType.ForEachActor => "Each Actor",
        TimelineNodeType.ForEachTileInRadius => "Tiles In Radius",
        TimelineNodeType.ForEachSkill => "Each Skill",

        // Timing/Coroutines
        TimelineNodeType.Delay => "Delay",
        TimelineNodeType.Repeat => "Repeat",
        TimelineNodeType.Once => "Once",

        // State Machine
        TimelineNodeType.State => "State",
        TimelineNodeType.Transition => "Transition",
        TimelineNodeType.GetState => "Get State",
        TimelineNodeType.SetState => "Set State",

        // Effects
        TimelineNodeType.Effect => "Effect",

        // Actions
        TimelineNodeType.ActionMoveTo => "Move To",
        TimelineNodeType.ActionApplyDamage => "Damage",
        TimelineNodeType.ActionHeal => "Heal",
        TimelineNodeType.ActionApplySuppression => "Suppress",
        TimelineNodeType.ActionAddSkill => "Add Skill",
        TimelineNodeType.ActionRemoveSkill => "Remove Skill",
        TimelineNodeType.ActionSpawn => "Spawn",
        TimelineNodeType.ActionKill => "Kill",
        TimelineNodeType.ActionLog => "Log",

        _ => "Node"
    };

    private static void ConfigurePorts(TimelineNode node)
    {
        switch (node.NodeType)
        {
            // Triggers - outputs only
            case TimelineNodeType.TriggerSkillUsed:
                AddOutput(node, "actor", PortDataType.Actor);
                AddOutput(node, "skill", PortDataType.Skill);
                AddOutput(node, "target", PortDataType.Actor);
                break;

            case TimelineNodeType.TriggerDamageReceived:
                AddOutput(node, "target", PortDataType.Actor);
                AddOutput(node, "attacker", PortDataType.Actor);
                AddOutput(node, "skill", PortDataType.Skill);
                // Note: SDK doesn't provide damage amount in this event
                break;

            case TimelineNodeType.TriggerActorKilled:
                AddOutput(node, "actor", PortDataType.Actor);
                AddOutput(node, "killer", PortDataType.Actor);
                break;

            case TimelineNodeType.TriggerMovementStarted:
                AddOutput(node, "actor", PortDataType.Actor);
                AddOutput(node, "from", PortDataType.Tile);
                AddOutput(node, "to", PortDataType.Tile);
                break;

            case TimelineNodeType.TriggerMovementFinished:
                AddOutput(node, "actor", PortDataType.Actor);
                AddOutput(node, "tile", PortDataType.Tile);
                break;

            case TimelineNodeType.TriggerTurnStart:
            case TimelineNodeType.TriggerTurnEnd:
                AddOutput(node, "actor", PortDataType.Actor);
                break;

            case TimelineNodeType.TriggerRoundStart:
            case TimelineNodeType.TriggerRoundEnd:
                AddOutput(node, "round", PortDataType.Number);
                break;

            case TimelineNodeType.TriggerHitpointsChanged:
                AddOutput(node, "actor", PortDataType.Actor);
                AddOutput(node, "oldHp", PortDataType.Number);
                AddOutput(node, "newHp", PortDataType.Number);
                AddOutput(node, "delta", PortDataType.Number);
                break;

            case TimelineNodeType.TriggerSuppressed:
                AddOutput(node, "actor", PortDataType.Actor);
                break;

            // Data nodes - pass through with property access
            case TimelineNodeType.Actor:
                AddInput(node, "in", PortDataType.Actor);
                AddOutput(node, "out", PortDataType.Actor);
                AddOutput(node, "name", PortDataType.String);
                AddOutput(node, "faction", PortDataType.Number);
                AddOutput(node, "hp", PortDataType.Number);
                AddOutput(node, "ap", PortDataType.Number);
                break;

            case TimelineNodeType.Skill:
                AddInput(node, "in", PortDataType.Skill);
                AddOutput(node, "out", PortDataType.Skill);
                AddOutput(node, "isAttack", PortDataType.Boolean);
                AddOutput(node, "isSilent", PortDataType.Boolean);
                AddOutput(node, "name", PortDataType.String);
                break;

            case TimelineNodeType.Tile:
                AddInput(node, "in", PortDataType.Tile);
                AddOutput(node, "out", PortDataType.Tile);
                AddOutput(node, "x", PortDataType.Number);
                AddOutput(node, "y", PortDataType.Number);
                AddOutput(node, "occupant", PortDataType.Actor);
                break;

            // Template selector nodes - output a specific template reference
            case TimelineNodeType.TemplateRefActor:
                AddOutput(node, "actor", PortDataType.Actor);
                node.Properties["templateId"] = "";
                node.Properties["templateType"] = "ActorTemplate";
                break;

            case TimelineNodeType.TemplateRefSkill:
                AddOutput(node, "skill", PortDataType.Skill);
                node.Properties["templateId"] = "";
                node.Properties["templateType"] = "SkillTemplate";
                break;

            case TimelineNodeType.TemplateRefItem:
                AddOutput(node, "item", PortDataType.Any);  // No dedicated Item port type yet
                node.Properties["templateId"] = "";
                node.Properties["templateType"] = "ItemTemplate";
                break;

            // Logic
            case TimelineNodeType.Condition:
                AddInput(node, "value", PortDataType.Boolean);
                AddOutput(node, "pass", PortDataType.Any);
                AddOutput(node, "fail", PortDataType.Any);
                break;

            case TimelineNodeType.Compare:
                AddInput(node, "a", PortDataType.Any);
                AddInput(node, "b", PortDataType.Any);
                AddOutput(node, "result", PortDataType.Boolean);
                node.Properties["operator"] = "==";  // ==, !=, >, <, >=, <=
                break;

            case TimelineNodeType.MathOp:
                AddInput(node, "a", PortDataType.Number);
                AddInput(node, "b", PortDataType.Number);
                AddOutput(node, "result", PortDataType.Number);
                node.Properties["operator"] = "+";  // +, -, *, /
                break;

            case TimelineNodeType.Not:
                AddInput(node, "in", PortDataType.Boolean);
                AddOutput(node, "out", PortDataType.Boolean);
                break;

            case TimelineNodeType.And:
            case TimelineNodeType.Or:
                AddInput(node, "a", PortDataType.Boolean);
                AddInput(node, "b", PortDataType.Boolean);
                AddOutput(node, "result", PortDataType.Boolean);
                break;

            // Variables
            case TimelineNodeType.SetVariable:
                AddInput(node, "value", PortDataType.Any);
                node.Properties["name"] = "myVar";
                break;

            case TimelineNodeType.GetVariable:
                AddOutput(node, "value", PortDataType.Any);
                node.Properties["name"] = "myVar";
                break;

            // Loops
            case TimelineNodeType.ForEachActor:
                AddInput(node, "trigger", PortDataType.Any);
                AddOutput(node, "actor", PortDataType.Actor);
                AddOutput(node, "done", PortDataType.Any);
                node.Properties["filter"] = "all";  // all, player, enemy
                break;

            case TimelineNodeType.ForEachTileInRadius:
                AddInput(node, "center", PortDataType.Tile);
                AddInput(node, "radius", PortDataType.Number);
                AddOutput(node, "tile", PortDataType.Tile);
                AddOutput(node, "done", PortDataType.Any);
                break;

            case TimelineNodeType.ForEachSkill:
                AddInput(node, "actor", PortDataType.Actor);
                AddOutput(node, "skill", PortDataType.Skill);
                AddOutput(node, "done", PortDataType.Any);
                break;

            // Timing/Coroutines
            case TimelineNodeType.Delay:
                AddInput(node, "trigger", PortDataType.Any);
                AddInput(node, "actor", PortDataType.Actor);  // Context for the delay (which entity to track)
                AddOutput(node, "execute", PortDataType.Any);
                node.Properties["rounds"] = 1;  // Delay in rounds
                break;

            case TimelineNodeType.Repeat:
                AddInput(node, "trigger", PortDataType.Any);
                AddInput(node, "actor", PortDataType.Actor);  // Context
                AddOutput(node, "execute", PortDataType.Any);  // Fires each time
                AddOutput(node, "done", PortDataType.Any);     // Fires when all iterations complete
                node.Properties["interval"] = 1;  // Every N rounds
                node.Properties["count"] = 3;     // Total iterations
                break;

            case TimelineNodeType.Once:
                AddInput(node, "trigger", PortDataType.Any);
                AddInput(node, "actor", PortDataType.Actor);  // Tracks per-entity or use combat ID
                AddOutput(node, "execute", PortDataType.Any);
                node.Properties["scope"] = "entity";  // "entity" or "combat"
                break;

            // State Machine
            case TimelineNodeType.State:
                AddInput(node, "enter", PortDataType.Any);     // Enter this state
                AddOutput(node, "onEnter", PortDataType.Any);  // Fires when entering
                AddOutput(node, "onExit", PortDataType.Any);   // Fires when exiting
                AddOutput(node, "active", PortDataType.Boolean);  // Whether currently in this state
                node.Properties["name"] = "idle";  // State name
                node.Properties["machine"] = "default";  // State machine name
                break;

            case TimelineNodeType.Transition:
                AddInput(node, "condition", PortDataType.Boolean);  // When true, transition
                AddInput(node, "actor", PortDataType.Actor);         // Entity context
                AddOutput(node, "done", PortDataType.Any);
                node.Properties["from"] = "idle";    // Source state
                node.Properties["to"] = "active";    // Target state
                node.Properties["machine"] = "default";
                break;

            case TimelineNodeType.GetState:
                AddInput(node, "actor", PortDataType.Actor);
                AddOutput(node, "state", PortDataType.String);
                AddOutput(node, "isState", PortDataType.Boolean);  // True if matches checkState
                node.Properties["machine"] = "default";
                node.Properties["checkState"] = "";  // If set, isState outputs whether current == checkState
                break;

            case TimelineNodeType.SetState:
                AddInput(node, "trigger", PortDataType.Any);
                AddInput(node, "actor", PortDataType.Actor);
                AddOutput(node, "done", PortDataType.Any);
                node.Properties["machine"] = "default";
                node.Properties["state"] = "idle";
                break;

            // Effects
            case TimelineNodeType.Effect:
                AddInput(node, "actor", PortDataType.Actor);
                AddInput(node, "trigger", PortDataType.Any);
                node.Properties["property"] = "concealment";
                node.Properties["modifier"] = -3;
                node.Properties["duration"] = 1;  // rounds
                break;

            // Actions
            case TimelineNodeType.ActionMoveTo:
                AddInput(node, "actor", PortDataType.Actor);
                AddInput(node, "tile", PortDataType.Tile);
                AddOutput(node, "done", PortDataType.Any);
                break;

            case TimelineNodeType.ActionApplyDamage:
                AddInput(node, "actor", PortDataType.Actor);
                AddInput(node, "amount", PortDataType.Number);
                AddOutput(node, "done", PortDataType.Any);
                node.Properties["amount"] = 10;
                break;

            case TimelineNodeType.ActionHeal:
                AddInput(node, "actor", PortDataType.Actor);
                AddInput(node, "amount", PortDataType.Number);
                AddOutput(node, "done", PortDataType.Any);
                node.Properties["amount"] = 10;
                break;

            case TimelineNodeType.ActionApplySuppression:
                AddInput(node, "actor", PortDataType.Actor);
                AddInput(node, "amount", PortDataType.Number);
                AddOutput(node, "done", PortDataType.Any);
                node.Properties["amount"] = 50;
                break;

            case TimelineNodeType.ActionAddSkill:
                AddInput(node, "actor", PortDataType.Actor);
                AddOutput(node, "done", PortDataType.Any);
                node.Properties["skillTemplate"] = "";
                break;

            case TimelineNodeType.ActionRemoveSkill:
                AddInput(node, "actor", PortDataType.Actor);
                AddInput(node, "skill", PortDataType.Skill);
                AddOutput(node, "done", PortDataType.Any);
                break;

            case TimelineNodeType.ActionSpawn:
                AddInput(node, "tile", PortDataType.Tile);
                AddOutput(node, "actor", PortDataType.Actor);
                node.Properties["template"] = "";
                break;

            case TimelineNodeType.ActionKill:
                AddInput(node, "actor", PortDataType.Actor);
                AddOutput(node, "done", PortDataType.Any);
                break;

            case TimelineNodeType.ActionLog:
                AddInput(node, "trigger", PortDataType.Any);
                AddInput(node, "message", PortDataType.String);
                node.Properties["message"] = "Debug message";
                break;
        }
    }

    private static void AddInput(TimelineNode node, string name, PortDataType type)
    {
        node.Inputs.Add(new NodePort
        {
            Name = name,
            DataType = type,
            IsOutput = false,
            Node = node
        });
    }

    private static void AddOutput(TimelineNode node, string name, PortDataType type)
    {
        node.Outputs.Add(new NodePort
        {
            Name = name,
            DataType = type,
            IsOutput = true,
            Node = node
        });
    }
}
