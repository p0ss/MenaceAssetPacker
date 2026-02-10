using System;
using System.Collections.Generic;
using System.Reflection;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for the AI decision-making system.
///
/// The Menace AI uses a utility-based decision system where Agent objects
/// evaluate Behavior options using a multi-criteria scoring system, then
/// execute the highest-utility action.
///
/// Based on reverse engineering in docs/reverse-engineering/ai-decisions.md
/// </summary>
public static class AI
{
    // Cached types
    private static GameType _agentType;
    private static GameType _aiFactionType;
    private static GameType _behaviorType;
    private static GameType _roleDataType;
    private static GameType _actorType;

    // Agent state enum values
    public const int STATE_NONE = 0;
    public const int STATE_EVALUATING_TILES = 1;
    public const int STATE_EVALUATING_BEHAVIORS = 2;
    public const int STATE_READY_TO_EXECUTE = 3;
    public const int STATE_EXECUTING = 4;

    /// <summary>
    /// AI Agent state info for a unit.
    /// </summary>
    public class AgentInfo
    {
        public bool HasAgent { get; set; }
        public int State { get; set; }
        public string StateName { get; set; }
        public string SelectedBehavior { get; set; }
        public int BehaviorScore { get; set; }
        public int TargetTileX { get; set; }
        public int TargetTileY { get; set; }
        public string TargetActorName { get; set; }
        public int EvaluatedTileCount { get; set; }
        public int AvailableBehaviorCount { get; set; }
    }

    /// <summary>
    /// RoleData defines per-unit AI configuration from the EntityTemplate.
    /// Controls how the AI values different actions and positions.
    /// </summary>
    public class RoleDataInfo
    {
        // Criterion weights
        public float UtilityScale { get; set; }
        public float SafetyScale { get; set; }
        public float DistanceScale { get; set; }
        public float FriendlyFirePenalty { get; set; }

        // Behavior weights
        public float MoveWeight { get; set; }
        public float InflictDamageWeight { get; set; }
        public float InflictSuppressionWeight { get; set; }
        public float StunWeight { get; set; }

        // Behavioral settings
        public bool IsAllowedToEvadeEnemies { get; set; }
        public bool AttemptToStayOutOfSight { get; set; }
        public bool PeekInAndOutOfCover { get; set; }

        // Criterion toggles
        public bool AvoidOpponents { get; set; }
        public bool CoverAgainstOpponents { get; set; }
        public bool ThreatFromOpponents { get; set; }
    }

    /// <summary>
    /// Tile score from AI evaluation.
    /// </summary>
    public class TileScoreInfo
    {
        public int X { get; set; }
        public int Y { get; set; }
        public float UtilityScore { get; set; }
        public float SafetyScore { get; set; }
        public float DistanceScore { get; set; }
        public float FinalScore { get; set; }
        public int CoverLevel { get; set; }
        public bool VisibleToOpponents { get; set; }
    }

    /// <summary>
    /// Behavior info from AI evaluation.
    /// </summary>
    public class BehaviorInfo
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public int Score { get; set; }
        public int TargetTileX { get; set; }
        public int TargetTileY { get; set; }
        public string TargetActorName { get; set; }
        public bool IsSelected { get; set; }
    }

    /// <summary>
    /// AIFaction info for a faction.
    /// </summary>
    public class AIFactionInfo
    {
        public int FactionIndex { get; set; }
        public int AgentCount { get; set; }
        public int OpponentCount { get; set; }
        public bool IsEvaluating { get; set; }
    }

    /// <summary>
    /// Get the AI Agent for an actor.
    /// Returns null GameObj if the actor has no AI agent (e.g., player units).
    /// </summary>
    public static GameObj GetAgent(GameObj actor)
    {
        if (actor.IsNull)
            return GameObj.Null;

        try
        {
            EnsureTypesLoaded();

            // Agent is typically stored on Actor or accessible via AIFaction
            // Try Actor.Agent property first
            var agentObj = actor.ReadObj("Agent");
            if (!agentObj.IsNull)
                return agentObj;

            // Try via m_Agent field
            agentObj = actor.ReadObj("m_Agent");
            if (!agentObj.IsNull)
                return agentObj;

            return GameObj.Null;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.GetAgent", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get AI agent state for an actor.
    /// </summary>
    public static AgentInfo GetAgentInfo(GameObj actor)
    {
        var info = new AgentInfo { HasAgent = false };

        if (actor.IsNull)
            return info;

        try
        {
            var agent = GetAgent(actor);
            if (agent.IsNull)
                return info;

            info.HasAgent = true;

            // Read state
            int state = agent.ReadInt("m_State");
            info.State = state;
            info.StateName = GetStateName(state);

            // Read evaluated tile count
            var tilesDict = agent.ReadObj("m_Tiles");
            if (!tilesDict.IsNull)
            {
                info.EvaluatedTileCount = tilesDict.ReadInt("_count");
            }

            // Read behaviors
            var behaviors = agent.ReadObj("m_Behaviors");
            if (!behaviors.IsNull)
            {
                info.AvailableBehaviorCount = behaviors.ReadInt("_size");
            }

            // Get selected behavior if ready to execute
            if (state >= STATE_READY_TO_EXECUTE)
            {
                var selectedBehavior = agent.ReadObj("m_SelectedBehavior");
                if (!selectedBehavior.IsNull)
                {
                    info.SelectedBehavior = selectedBehavior.GetType()?.Name ?? "Unknown";
                    info.BehaviorScore = selectedBehavior.ReadInt("Score");

                    // Get target tile
                    var targetTile = selectedBehavior.ReadObj("TargetTile");
                    if (!targetTile.IsNull)
                    {
                        info.TargetTileX = targetTile.ReadInt("X");
                        info.TargetTileY = targetTile.ReadInt("Y");
                    }

                    // Get target entity
                    var targetEntity = selectedBehavior.ReadObj("TargetEntity");
                    if (!targetEntity.IsNull)
                    {
                        info.TargetActorName = targetEntity.GetName();
                    }
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.GetAgentInfo", "Failed", ex);
            return info;
        }
    }

    /// <summary>
    /// Get the RoleData (AI configuration) for an actor.
    /// RoleData is defined on the EntityTemplate at offset +0x310.
    /// </summary>
    public static RoleDataInfo GetRoleData(GameObj actor)
    {
        var info = new RoleDataInfo();

        if (actor.IsNull)
            return info;

        try
        {
            // Get EntityTemplate from actor
            var template = actor.ReadObj("m_Template");
            if (template.IsNull)
                template = actor.ReadObj("Template");
            if (template.IsNull)
                return info;

            // Get AIRole/RoleData from template
            var roleData = template.ReadObj("AIRole");
            if (roleData.IsNull)
                roleData = template.ReadObj("m_AIRole");
            if (roleData.IsNull)
                return info;

            // Read criterion weights
            info.UtilityScale = roleData.ReadFloat("UtilityScale");
            info.SafetyScale = roleData.ReadFloat("SafetyScale");
            info.DistanceScale = roleData.ReadFloat("DistanceScale");
            info.FriendlyFirePenalty = roleData.ReadFloat("FriendlyFirePenalty");

            // Read behavior weights
            info.MoveWeight = roleData.ReadFloat("Move");
            info.InflictDamageWeight = roleData.ReadFloat("InflictDamage");
            info.InflictSuppressionWeight = roleData.ReadFloat("InflictSuppression");
            info.StunWeight = roleData.ReadFloat("Stun");

            // Read behavioral settings
            info.IsAllowedToEvadeEnemies = roleData.ReadBool("IsAllowedToEvadeEnemies");
            info.AttemptToStayOutOfSight = roleData.ReadBool("AttemptToStayOutOfSight");
            info.PeekInAndOutOfCover = roleData.ReadBool("PeekInAndOutOfCover");

            // Read criterion toggles
            info.AvoidOpponents = roleData.ReadBool("AvoidOpponents");
            info.CoverAgainstOpponents = roleData.ReadBool("CoverAgainstOpponents");
            info.ThreatFromOpponents = roleData.ReadBool("ThreatFromOpponents");

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.GetRoleData", "Failed", ex);
            return info;
        }
    }

    /// <summary>
    /// Get all behaviors available to an actor's AI agent.
    /// </summary>
    public static List<BehaviorInfo> GetBehaviors(GameObj actor)
    {
        var result = new List<BehaviorInfo>();

        if (actor.IsNull)
            return result;

        try
        {
            var agent = GetAgent(actor);
            if (agent.IsNull)
                return result;

            var behaviors = agent.ReadObj("m_Behaviors");
            if (behaviors.IsNull)
                return result;

            var selectedBehavior = agent.ReadObj("m_SelectedBehavior");

            // Iterate behaviors list
            int count = behaviors.ReadInt("_size");
            var itemsPtr = behaviors.ReadPtr("_items");
            if (itemsPtr == IntPtr.Zero)
                return result;

            var items = new GameArray(itemsPtr);
            for (int i = 0; i < count; i++)
            {
                var behavior = items[i];
                if (behavior.IsNull)
                    continue;

                var info = new BehaviorInfo
                {
                    TypeName = behavior.GetType()?.Name ?? "Unknown",
                    Score = behavior.ReadInt("Score"),
                    IsSelected = !selectedBehavior.IsNull && behavior.Pointer == selectedBehavior.Pointer
                };

                // Try to get name from type
                info.Name = info.TypeName;

                // Get target info
                var targetTile = behavior.ReadObj("TargetTile");
                if (!targetTile.IsNull)
                {
                    info.TargetTileX = targetTile.ReadInt("X");
                    info.TargetTileY = targetTile.ReadInt("Y");
                }

                var targetEntity = behavior.ReadObj("TargetEntity");
                if (!targetEntity.IsNull)
                {
                    info.TargetActorName = targetEntity.GetName();
                }

                result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.GetBehaviors", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get tile scores from an actor's AI evaluation.
    /// Returns the top N tiles by score.
    /// </summary>
    public static List<TileScoreInfo> GetTileScores(GameObj actor, int maxTiles = 10)
    {
        var result = new List<TileScoreInfo>();

        if (actor.IsNull)
            return result;

        try
        {
            var agent = GetAgent(actor);
            if (agent.IsNull)
                return result;

            var tilesDict = agent.ReadObj("m_Tiles");
            if (tilesDict.IsNull)
                return result;

            // Iterate dictionary using GameDict wrapper
            var dict = new GameDict(tilesDict);
            var allScores = new List<TileScoreInfo>();

            foreach (var (tileKey, tileScore) in dict)
            {
                if (tileKey.IsNull || tileScore.IsNull)
                    continue;

                var info = new TileScoreInfo
                {
                    X = tileKey.ReadInt("X"),
                    Y = tileKey.ReadInt("Y"),
                    UtilityScore = tileScore.ReadFloat("UtilityScore"),
                    SafetyScore = tileScore.ReadFloat("SafetyScore"),
                    DistanceScore = tileScore.ReadFloat("DistanceScore"),
                    FinalScore = tileScore.ReadFloat("FinalScore"),
                    CoverLevel = tileScore.ReadInt("CoverLevel"),
                    VisibleToOpponents = tileScore.ReadBool("VisibleToOpponents")
                };

                allScores.Add(info);

                // Limit iterations for safety
                if (allScores.Count >= 1000)
                    break;
            }

            // Sort by FinalScore descending and take top N
            allScores.Sort((a, b) => b.FinalScore.CompareTo(a.FinalScore));
            for (int i = 0; i < Math.Min(maxTiles, allScores.Count); i++)
            {
                result.Add(allScores[i]);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.GetTileScores", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Get AIFaction info for a faction index.
    /// </summary>
    public static AIFactionInfo GetAIFactionInfo(int factionIndex)
    {
        var info = new AIFactionInfo { FactionIndex = factionIndex };

        try
        {
            EnsureTypesLoaded();

            // Find AIFaction for this faction
            var aiFactions = GameQuery.FindAll("AIFaction");
            foreach (var aiFaction in aiFactions)
            {
                int index = aiFaction.ReadInt("FactionIndex");
                if (index == factionIndex)
                {
                    var agents = aiFaction.ReadObj("m_Agents");
                    if (!agents.IsNull)
                    {
                        info.AgentCount = agents.ReadInt("_size");
                    }

                    var opponents = aiFaction.ReadObj("m_Opponents");
                    if (!opponents.IsNull)
                    {
                        info.OpponentCount = opponents.ReadInt("_size");
                    }

                    info.IsEvaluating = aiFaction.ReadBool("m_IsEvaluating");
                    break;
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.GetAIFactionInfo", "Failed", ex);
            return info;
        }
    }

    /// <summary>
    /// Get what the AI is planning for an actor.
    /// Convenience method that returns a summary of the AI's current intent.
    /// </summary>
    public static string GetAIIntent(GameObj actor)
    {
        if (actor.IsNull)
            return "No actor";

        var info = GetAgentInfo(actor);
        if (!info.HasAgent)
            return "No AI agent (player unit?)";

        if (info.State < STATE_READY_TO_EXECUTE)
            return $"Evaluating ({info.StateName})";

        if (string.IsNullOrEmpty(info.SelectedBehavior))
            return "No behavior selected";

        string intent = $"{info.SelectedBehavior} (score: {info.BehaviorScore})";

        if (!string.IsNullOrEmpty(info.TargetActorName))
            intent += $" -> {info.TargetActorName}";
        else if (info.TargetTileX > 0 || info.TargetTileY > 0)
            intent += $" -> ({info.TargetTileX}, {info.TargetTileY})";

        return intent;
    }

    /// <summary>
    /// Register console commands for AI inspection.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("ai", "[actor_name]", "Show AI agent info for actor", args =>
        {
            GameObj actor;
            if (args.Length > 0)
            {
                var name = string.Join(" ", args);
                actor = GameQuery.FindByName("Actor", name);
                if (actor.IsNull)
                    return $"Actor '{name}' not found";
            }
            else
            {
                actor = TacticalController.GetActiveActor();
                if (actor.IsNull)
                    return "No active actor";
            }

            var info = GetAgentInfo(actor);
            if (!info.HasAgent)
                return $"{actor.GetName()}: No AI agent (player unit?)";

            return $"{actor.GetName()}:\n" +
                   $"  State: {info.StateName}\n" +
                   $"  Tiles evaluated: {info.EvaluatedTileCount}\n" +
                   $"  Behaviors: {info.AvailableBehaviorCount}\n" +
                   $"  Selected: {info.SelectedBehavior ?? "none"} (score: {info.BehaviorScore})\n" +
                   $"  Target: {info.TargetActorName ?? $"({info.TargetTileX}, {info.TargetTileY})"}";
        });

        DevConsole.RegisterCommand("airole", "[actor_name]", "Show AI RoleData for actor", args =>
        {
            GameObj actor;
            if (args.Length > 0)
            {
                var name = string.Join(" ", args);
                actor = GameQuery.FindByName("Actor", name);
                if (actor.IsNull)
                    return $"Actor '{name}' not found";
            }
            else
            {
                actor = TacticalController.GetActiveActor();
                if (actor.IsNull)
                    return "No active actor";
            }

            var role = GetRoleData(actor);
            return $"{actor.GetName()} RoleData:\n" +
                   $"  Utility: {role.UtilityScale:F1}, Safety: {role.SafetyScale:F1}, Distance: {role.DistanceScale:F1}\n" +
                   $"  Move: {role.MoveWeight:F1}, Damage: {role.InflictDamageWeight:F1}, Suppress: {role.InflictSuppressionWeight:F1}\n" +
                   $"  Evade: {role.IsAllowedToEvadeEnemies}, StayHidden: {role.AttemptToStayOutOfSight}, Peek: {role.PeekInAndOutOfCover}";
        });

        DevConsole.RegisterCommand("aibehaviors", "[actor_name]", "List AI behaviors for actor", args =>
        {
            GameObj actor;
            if (args.Length > 0)
            {
                var name = string.Join(" ", args);
                actor = GameQuery.FindByName("Actor", name);
                if (actor.IsNull)
                    return $"Actor '{name}' not found";
            }
            else
            {
                actor = TacticalController.GetActiveActor();
                if (actor.IsNull)
                    return "No active actor";
            }

            var behaviors = GetBehaviors(actor);
            if (behaviors.Count == 0)
                return $"{actor.GetName()}: No behaviors";

            var lines = new List<string> { $"{actor.GetName()} behaviors:" };
            foreach (var b in behaviors)
            {
                string marker = b.IsSelected ? " [SELECTED]" : "";
                string target = !string.IsNullOrEmpty(b.TargetActorName)
                    ? $" -> {b.TargetActorName}"
                    : b.TargetTileX > 0 ? $" -> ({b.TargetTileX}, {b.TargetTileY})" : "";
                lines.Add($"  {b.TypeName}: {b.Score}{target}{marker}");
            }
            return string.Join("\n", lines);
        });

        DevConsole.RegisterCommand("aitiles", "[actor_name] [count]", "Show top tile scores for actor", args =>
        {
            GameObj actor;
            int count = 5;

            if (args.Length > 0 && int.TryParse(args[^1], out int n))
            {
                count = n;
                args = args[..^1];
            }

            if (args.Length > 0)
            {
                var name = string.Join(" ", args);
                actor = GameQuery.FindByName("Actor", name);
                if (actor.IsNull)
                    return $"Actor '{name}' not found";
            }
            else
            {
                actor = TacticalController.GetActiveActor();
                if (actor.IsNull)
                    return "No active actor";
            }

            var tiles = GetTileScores(actor, count);
            if (tiles.Count == 0)
                return $"{actor.GetName()}: No tile scores";

            var lines = new List<string> { $"{actor.GetName()} top {count} tiles:" };
            foreach (var t in tiles)
            {
                lines.Add($"  ({t.X}, {t.Y}): score={t.FinalScore:F1} (util={t.UtilityScore:F1}, safe={t.SafetyScore:F1})");
            }
            return string.Join("\n", lines);
        });

        DevConsole.RegisterCommand("aiintent", "[actor_name]", "Show what the AI is planning", args =>
        {
            GameObj actor;
            if (args.Length > 0)
            {
                var name = string.Join(" ", args);
                actor = GameQuery.FindByName("Actor", name);
                if (actor.IsNull)
                    return $"Actor '{name}' not found";
            }
            else
            {
                actor = TacticalController.GetActiveActor();
                if (actor.IsNull)
                    return "No active actor";
            }

            return $"{actor.GetName()}: {GetAIIntent(actor)}";
        });
    }

    // ==========================================================================
    // WRITE METHODS
    // ==========================================================================
    // THREADING WARNING: These methods modify AI state. They are ONLY safe to call:
    //   1. Before the faction's turn begins (e.g., in OnTurnStart hook)
    //   2. After the faction's turn ends
    //   3. When IsAnyFactionEvaluating() returns false
    //
    // Calling these during parallel evaluation WILL cause race conditions and crashes.
    // ==========================================================================

    /// <summary>
    /// Check if any AI faction is currently evaluating (parallel tile/behavior scoring).
    /// When this returns true, it is NOT safe to write to AI state.
    /// </summary>
    public static bool IsAnyFactionEvaluating()
    {
        try
        {
            var aiFactions = GameQuery.FindAll("AIFaction");
            foreach (var aiFaction in aiFactions)
            {
                if (aiFaction.ReadBool("m_IsEvaluating"))
                    return true;
            }
            return false;
        }
        catch
        {
            return true; // Assume unsafe if we can't check
        }
    }

    /// <summary>
    /// Get the RoleData object for an actor, for direct field modification.
    /// Returns GameObj.Null if actor has no RoleData.
    /// </summary>
    public static GameObj GetRoleDataObject(GameObj actor)
    {
        if (actor.IsNull)
            return GameObj.Null;

        try
        {
            var template = actor.ReadObj("m_Template");
            if (template.IsNull)
                template = actor.ReadObj("Template");
            if (template.IsNull)
                return GameObj.Null;

            var roleData = template.ReadObj("AIRole");
            if (roleData.IsNull)
                roleData = template.ReadObj("m_AIRole");

            return roleData;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AI.GetRoleDataObject", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Set a float field on an actor's RoleData.
    /// Returns true if successful, false if write failed or actor has no RoleData.
    ///
    /// Common fields: UtilityScale, SafetyScale, DistanceScale, FriendlyFirePenalty,
    ///                Move, InflictDamage, InflictSuppression, Stun
    /// </summary>
    public static bool SetRoleDataFloat(GameObj actor, string fieldName, float value)
    {
        if (IsAnyFactionEvaluating())
        {
            ModError.ReportInternal("AI.SetRoleDataFloat",
                $"Cannot write during AI evaluation - will cause race condition. Field: {fieldName}");
            return false;
        }

        var roleData = GetRoleDataObject(actor);
        if (roleData.IsNull)
        {
            ModError.ReportInternal("AI.SetRoleDataFloat", $"Actor has no RoleData");
            return false;
        }

        return roleData.WriteFloat(fieldName, value);
    }

    /// <summary>
    /// Set a bool field on an actor's RoleData.
    /// Returns true if successful.
    ///
    /// Common fields: IsAllowedToEvadeEnemies, AttemptToStayOutOfSight, PeekInAndOutOfCover,
    ///                AvoidOpponents, CoverAgainstOpponents, ThreatFromOpponents
    /// </summary>
    public static bool SetRoleDataBool(GameObj actor, string fieldName, bool value)
    {
        if (IsAnyFactionEvaluating())
        {
            ModError.ReportInternal("AI.SetRoleDataBool",
                $"Cannot write during AI evaluation - will cause race condition. Field: {fieldName}");
            return false;
        }

        var roleData = GetRoleDataObject(actor);
        if (roleData.IsNull)
        {
            ModError.ReportInternal("AI.SetRoleDataBool", $"Actor has no RoleData");
            return false;
        }

        // Write bool as int (0 or 1)
        return roleData.WriteInt(fieldName, value ? 1 : 0);
    }

    /// <summary>
    /// Apply a complete RoleData configuration to an actor.
    /// Only the fields that differ from the current values will be written.
    /// </summary>
    public static bool ApplyRoleData(GameObj actor, RoleDataInfo newRole)
    {
        if (IsAnyFactionEvaluating())
        {
            ModError.ReportInternal("AI.ApplyRoleData",
                "Cannot write during AI evaluation - will cause race condition");
            return false;
        }

        var roleData = GetRoleDataObject(actor);
        if (roleData.IsNull)
        {
            ModError.ReportInternal("AI.ApplyRoleData", "Actor has no RoleData");
            return false;
        }

        bool success = true;

        // Write criterion weights
        success &= roleData.WriteFloat("UtilityScale", newRole.UtilityScale);
        success &= roleData.WriteFloat("SafetyScale", newRole.SafetyScale);
        success &= roleData.WriteFloat("DistanceScale", newRole.DistanceScale);
        success &= roleData.WriteFloat("FriendlyFirePenalty", newRole.FriendlyFirePenalty);

        // Write behavior weights
        success &= roleData.WriteFloat("Move", newRole.MoveWeight);
        success &= roleData.WriteFloat("InflictDamage", newRole.InflictDamageWeight);
        success &= roleData.WriteFloat("InflictSuppression", newRole.InflictSuppressionWeight);
        success &= roleData.WriteFloat("Stun", newRole.StunWeight);

        // Write behavioral settings (as ints)
        success &= roleData.WriteInt("IsAllowedToEvadeEnemies", newRole.IsAllowedToEvadeEnemies ? 1 : 0);
        success &= roleData.WriteInt("AttemptToStayOutOfSight", newRole.AttemptToStayOutOfSight ? 1 : 0);
        success &= roleData.WriteInt("PeekInAndOutOfCover", newRole.PeekInAndOutOfCover ? 1 : 0);

        // Write criterion toggles
        success &= roleData.WriteInt("AvoidOpponents", newRole.AvoidOpponents ? 1 : 0);
        success &= roleData.WriteInt("CoverAgainstOpponents", newRole.CoverAgainstOpponents ? 1 : 0);
        success &= roleData.WriteInt("ThreatFromOpponents", newRole.ThreatFromOpponents ? 1 : 0);

        return success;
    }

    /// <summary>
    /// Force-set a behavior's score. Use with extreme caution.
    /// This can override AI decisions but may cause unexpected behavior.
    /// </summary>
    public static bool SetBehaviorScore(GameObj actor, string behaviorTypeName, int score)
    {
        if (IsAnyFactionEvaluating())
        {
            ModError.ReportInternal("AI.SetBehaviorScore",
                "Cannot write during AI evaluation - will cause race condition");
            return false;
        }

        var agent = GetAgent(actor);
        if (agent.IsNull)
            return false;

        var behaviors = agent.ReadObj("m_Behaviors");
        if (behaviors.IsNull)
            return false;

        int count = behaviors.ReadInt("_size");
        var itemsPtr = behaviors.ReadPtr("_items");
        if (itemsPtr == IntPtr.Zero)
            return false;

        var items = new GameArray(itemsPtr);
        for (int i = 0; i < count; i++)
        {
            var behavior = items[i];
            if (behavior.IsNull)
                continue;

            var typeName = behavior.GetType()?.Name;
            if (typeName == behaviorTypeName)
            {
                return behavior.WriteInt("Score", score);
            }
        }

        return false; // Behavior not found
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _agentType ??= GameType.Find("Menace.Tactical.AI.Agent");
        _aiFactionType ??= GameType.Find("Menace.Tactical.AI.AIFaction");
        _behaviorType ??= GameType.Find("Menace.Tactical.AI.Behavior");
        _roleDataType ??= GameType.Find("Menace.Tactical.AI.Data.RoleData");
        _actorType ??= GameType.Find("Menace.Tactical.Actor");
    }

    private static string GetStateName(int state)
    {
        return state switch
        {
            STATE_NONE => "None",
            STATE_EVALUATING_TILES => "EvaluatingTiles",
            STATE_EVALUATING_BEHAVIORS => "EvaluatingBehaviors",
            STATE_READY_TO_EXECUTE => "ReadyToExecute",
            STATE_EXECUTING => "Executing",
            _ => $"Unknown({state})"
        };
    }
}
