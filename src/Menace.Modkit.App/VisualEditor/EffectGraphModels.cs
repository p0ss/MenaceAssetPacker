#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

namespace Menace.Modkit.App.VisualEditor;

/// <summary>
/// Real event types from TacticalEventHooks that can trigger effects.
/// </summary>
public enum TriggerEvent
{
    // Combat
    SkillUsed,
    DamageReceived,
    ActorKilled,
    AttackMissed,

    // Turn/Round
    TurnEnd,
    RoundStart,
    RoundEnd,

    // Movement
    MovementStarted,
    MovementFinished,

    // State
    HitpointsChanged,
    ActionPointsChanged,
    Suppressed
}

/// <summary>
/// Properties that can be checked in conditions.
/// </summary>
public enum ConditionProperty
{
    // Skill properties
    SkillIsAttack,
    SkillIsSilent,

    // Actor properties
    ActorFaction,
    ActorIsPlayer,
    ActorIsEnemy,
    ActorHasEffect,

    // Comparison
    DamageGreaterThan,
    DamageLessThan,
    HealthBelow,
    HealthAbove
}

/// <summary>
/// Actions that effects can perform.
/// </summary>
public enum EffectAction
{
    AddEffect,          // Add a timed property modifier
    RemoveEffect,       // Remove effects by source
    ApplyDamage,        // Deal damage
    Heal,               // Restore health
    ApplySuppression,   // Add suppression
    Log                 // Debug log
}

/// <summary>
/// Represents a complete effect definition that can generate C# code.
/// </summary>
public class EffectDefinition
{
    public string Name { get; set; } = "NewEffect";
    public string Description { get; set; } = "";

    /// <summary>
    /// The event that triggers this effect.
    /// </summary>
    public TriggerEvent Trigger { get; set; } = TriggerEvent.SkillUsed;

    /// <summary>
    /// Conditions that must be true for the effect to apply.
    /// All conditions are ANDed together.
    /// </summary>
    public List<ConditionNode> Conditions { get; } = new();

    /// <summary>
    /// Actions to perform when triggered and conditions pass.
    /// </summary>
    public List<ActionNode> Actions { get; } = new();

    /// <summary>
    /// Generate C# code for this effect.
    /// </summary>
    public string GenerateCode()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"// Effect: {Name}");
        if (!string.IsNullOrEmpty(Description))
            sb.AppendLine($"// {Description}");
        sb.AppendLine();

        // Generate event subscription
        var (eventName, parameters, setup) = GetEventSignature(Trigger);

        sb.AppendLine($"TacticalEventHooks.{eventName} += ({parameters}) =>");
        sb.AppendLine("{");

        // Setup variables
        foreach (var line in setup)
            sb.AppendLine($"    {line}");

        if (setup.Count > 0)
            sb.AppendLine();

        // Generate conditions
        if (Conditions.Count > 0)
        {
            var conditionCode = GenerateConditions();
            sb.AppendLine($"    if ({conditionCode})");
            sb.AppendLine("    {");

            // Generate actions
            foreach (var action in Actions)
            {
                sb.AppendLine($"        {GenerateAction(action)}");
            }

            sb.AppendLine("    }");
        }
        else
        {
            // No conditions, just run actions
            foreach (var action in Actions)
            {
                sb.AppendLine($"    {GenerateAction(action)}");
            }
        }

        sb.AppendLine("};");

        return sb.ToString();
    }

    private (string eventName, string parameters, List<string> setup) GetEventSignature(TriggerEvent trigger)
    {
        return trigger switch
        {
            TriggerEvent.SkillUsed => ("OnSkillUsed", "userPtr, skillPtr, targetPtr", new List<string>
            {
                "var actor = Actor.Get(userPtr);",
                "var skill = new Skill(skillPtr);"
            }),
            TriggerEvent.DamageReceived => ("OnDamageReceived", "targetPtr, attackerPtr, skillPtr", new List<string>
            {
                "var target = Actor.Get(targetPtr);",
                "var attacker = Actor.Get(attackerPtr);",
                "var skill = new Skill(skillPtr);"
            }),
            TriggerEvent.ActorKilled => ("OnActorKilled", "actorPtr, killerPtr, factionId", new List<string>
            {
                "var actor = Actor.Get(actorPtr);",
                "var killer = Actor.Get(killerPtr);"
            }),
            TriggerEvent.AttackMissed => ("OnAttackMissed", "attackerPtr, targetPtr", new List<string>
            {
                "var attacker = Actor.Get(attackerPtr);",
                "var target = Actor.Get(targetPtr);"
            }),
            TriggerEvent.TurnEnd => ("OnTurnEnd", "actorPtr", new List<string>
            {
                "var actor = Actor.Get(actorPtr);"
            }),
            TriggerEvent.RoundStart => ("OnRoundStart", "roundNumber", new List<string>()),
            TriggerEvent.RoundEnd => ("OnRoundEnd", "roundNumber", new List<string>()),
            TriggerEvent.MovementStarted => ("OnMovementStarted", "actorPtr, fromTilePtr, toTilePtr", new List<string>
            {
                "var actor = Actor.Get(actorPtr);"
            }),
            TriggerEvent.MovementFinished => ("OnMovementFinished", "actorPtr, tilePtr", new List<string>
            {
                "var actor = Actor.Get(actorPtr);"
            }),
            TriggerEvent.HitpointsChanged => ("OnHitpointsChanged", "actorPtr, oldHp, newHp", new List<string>
            {
                "var actor = Actor.Get(actorPtr);",
                "var damage = oldHp - newHp;"
            }),
            TriggerEvent.ActionPointsChanged => ("OnActionPointsChanged", "actorPtr, oldAp, newAp", new List<string>
            {
                "var actor = Actor.Get(actorPtr);"
            }),
            TriggerEvent.Suppressed => ("OnSuppressed", "actorPtr", new List<string>
            {
                "var actor = Actor.Get(actorPtr);"
            }),
            _ => ("OnSkillUsed", "userPtr, skillPtr, targetPtr", new List<string>())
        };
    }

    private string GenerateConditions()
    {
        if (Conditions.Count == 0) return "true";

        var parts = new List<string>();
        foreach (var cond in Conditions)
        {
            parts.Add(GenerateCondition(cond));
        }

        return string.Join(" && ", parts);
    }

    private string GenerateCondition(ConditionNode cond)
    {
        return cond.Property switch
        {
            ConditionProperty.SkillIsAttack => cond.Negate ? "!skill.IsAttack" : "skill.IsAttack",
            ConditionProperty.SkillIsSilent => cond.Negate ? "!skill.IsSilent" : "skill.IsSilent",
            ConditionProperty.ActorIsPlayer => cond.Negate ? "actor.Faction != FactionType.Player" : "actor.Faction == FactionType.Player",
            ConditionProperty.ActorIsEnemy => cond.Negate ? "actor.Faction == FactionType.Player" : "actor.Faction != FactionType.Player",
            ConditionProperty.ActorHasEffect => cond.Negate
                ? $"!actor.HasEffect(\"{cond.StringValue}\")"
                : $"actor.HasEffect(\"{cond.StringValue}\")",
            ConditionProperty.DamageGreaterThan => $"damage > {cond.IntValue}",
            ConditionProperty.DamageLessThan => $"damage < {cond.IntValue}",
            ConditionProperty.HealthBelow => $"actor.CombatInfo.CurrentHp < {cond.IntValue}",
            ConditionProperty.HealthAbove => $"actor.CombatInfo.CurrentHp > {cond.IntValue}",
            _ => "true"
        };
    }

    private string GenerateAction(ActionNode action)
    {
        return action.Action switch
        {
            EffectAction.AddEffect => $"actor.AddEffect(\"{action.Property}\", {action.Modifier}, {action.Rounds}, \"{Name}\");",
            EffectAction.RemoveEffect => $"EffectSystem.ClearEffectsBySource(\"{action.Source}\");",
            EffectAction.ApplyDamage => $"actor.ApplyDamage({action.Modifier});",
            EffectAction.Heal => $"actor.Heal({action.Modifier});",
            EffectAction.ApplySuppression => $"actor.ApplySuppression({action.Modifier}f);",
            EffectAction.Log => $"SdkLogger.Msg(\"[{Name}] {action.Message}\");",
            _ => "// unknown action"
        };
    }
}

/// <summary>
/// A condition check in the effect graph.
/// </summary>
public class ConditionNode
{
    public ConditionProperty Property { get; set; }
    public bool Negate { get; set; }
    public int IntValue { get; set; }
    public string StringValue { get; set; } = "";
}

/// <summary>
/// An action to perform in the effect graph.
/// </summary>
public class ActionNode
{
    public EffectAction Action { get; set; }

    // For AddEffect
    public string Property { get; set; } = "concealment";
    public int Modifier { get; set; } = -3;
    public int Rounds { get; set; } = 1;

    // For RemoveEffect
    public string Source { get; set; } = "";

    // For Log
    public string Message { get; set; } = "";
}
