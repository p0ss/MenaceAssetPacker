// =============================================================================
// MENACE REFERENCE CODE - AI Behaviors & Criterions
// =============================================================================
// Behavior classes represent actions the AI can take.
// Criterion classes evaluate tile positions for scoring.
// =============================================================================

using System;
using System.Collections.Generic;

namespace Menace.Tactical.AI
{
    /// <summary>
    /// Score data for a tile position.
    /// Aggregates scores from multiple criterions.
    /// </summary>
    public class TileScore
    {
        /// <summary>The tile being scored. Offset: +0x10</summary>
        public Tile Tile;

        /// <summary>Final destination (may differ if pathing). Offset: +0x18</summary>
        public Tile UltimateTile;

        /// <summary>Distance from actor's current tile. Offset: +0x20</summary>
        public float DistanceToCurrentTile;

        /// <summary>Distance-based penalty score. Offset: +0x24</summary>
        public float DistanceScore;

        /// <summary>Aggregated utility score (damage potential, etc). Offset: +0x28</summary>
        public float UtilityScore;

        /// <summary>Aggregated safety score (cover, threat avoidance). Offset: +0x2C</summary>
        public float SafetyScore;

        /// <summary>Final combined score after role weighting. Offset: +0x30</summary>
        public float FinalScore;

        /// <summary>Cover level at this tile against primary threat. Offset: +0x34</summary>
        public int CoverLevel;

        /// <summary>Whether tile is visible to any opponent. Offset: +0x38</summary>
        public bool VisibleToOpponents;
    }

    // =========================================================================
    // CRITERION BASE CLASS
    // =========================================================================

    /// <summary>
    /// Base class for tile evaluation criterions.
    /// Criterions score tiles based on specific factors.
    /// </summary>
    public abstract class Criterion
    {
        /// <summary>
        /// Is this criterion relevant for this actor?
        /// Checked before evaluation begins.
        /// </summary>
        public abstract bool IsValid(Actor actor);

        /// <summary>
        /// Number of threads to use for parallel evaluation.
        /// Default: 1 (sequential).
        /// </summary>
        public virtual int GetThreads() => 1;

        /// <summary>
        /// Pre-evaluation data collection.
        /// Called once before tile evaluation loop.
        /// </summary>
        public virtual void Collect(Actor actor, Dictionary<Tile, TileScore> tiles) { }

        /// <summary>
        /// Score a single tile.
        /// Called for each tile, may run in parallel.
        /// </summary>
        public virtual void Evaluate(Actor actor, TileScore tile) { }

        /// <summary>
        /// Post-process all tiles after evaluation.
        /// Called once after all tiles scored.
        /// </summary>
        public virtual void PostProcess(Actor actor, Dictionary<Tile, TileScore> tiles) { }
    }

    // =========================================================================
    // BUILT-IN CRITERIONS
    // =========================================================================

    /// <summary>
    /// Scores tiles based on cover from enemy positions.
    /// Controlled by RoleData.CoverAgainstOpponents.
    /// </summary>
    public class CoverAgainstOpponents : Criterion
    {
        private const int ADDITIONAL_RANGE = 3;
        private const float NO_COVER_AT_ALL_PENALTY = 10f;
        private const float NOT_VISIBLE_TO_OPPONENTS_MULT = 0.9f;

        private float[] COVER_PENALTIES = { 1.0f, 0.7f, 0.4f, 0.1f };  // None, Light, Medium, Heavy

        private List<Opponent> m_Opponents;

        public override bool IsValid(Actor actor)
        {
            var role = actor.EntityTemplate?.AIRole;
            return role?.CoverAgainstOpponents == true;
        }

        public override void Collect(Actor actor, Dictionary<Tile, TileScore> tiles)
        {
            // Get known opponents from faction
            m_Opponents = actor.Faction?.AIFaction?.GetKnownOpponents();
        }

        public override void Evaluate(Actor actor, TileScore tileScore)
        {
            if (m_Opponents == null || m_Opponents.Count == 0)
                return;

            float totalCoverScore = 0f;

            foreach (var opponent in m_Opponents)
            {
                // Calculate cover at this tile against this opponent
                int direction = tileScore.Tile.GetDirectionTo(opponent.Actor.CurrentTile);
                int coverLevel = tileScore.Tile.GetCover(direction, actor);

                // Apply cover penalty (lower is better)
                float penalty = COVER_PENALTIES[Math.Min(coverLevel, 3)];

                // Reduce penalty if not visible
                if (!IsVisibleFrom(tileScore.Tile, opponent.Actor.CurrentTile))
                {
                    penalty *= NOT_VISIBLE_TO_OPPONENTS_MULT;
                }

                // Weight by opponent threat level
                penalty *= opponent.CurrentThreatPosedTotal;

                totalCoverScore += penalty;
            }

            // Apply to safety score (inverted - lower penalty = higher safety)
            tileScore.SafetyScore += (1f - totalCoverScore) * 10f;
        }

        private bool IsVisibleFrom(Tile from, Tile to)
        {
            // Check line of sight
            return LineOfSight.HasLOS(from, to);
        }
    }

    /// <summary>
    /// Penalizes tiles far from current position.
    /// Controlled by RoleData.DistanceToCurrentTile.
    /// </summary>
    public class DistanceToCurrentTile : Criterion
    {
        public override bool IsValid(Actor actor)
        {
            var role = actor.EntityTemplate?.AIRole;
            return role?.DistanceToCurrentTile == true;
        }

        public override void Evaluate(Actor actor, TileScore tileScore)
        {
            // Distance is pre-calculated when tile is added
            tileScore.DistanceScore = tileScore.DistanceToCurrentTile * 0.5f;
        }
    }

    /// <summary>
    /// Scores tiles by threat from opponent attacks.
    /// Controlled by RoleData.ThreatFromOpponents.
    /// </summary>
    public class ThreatFromOpponents : Criterion
    {
        public override bool IsValid(Actor actor)
        {
            var role = actor.EntityTemplate?.AIRole;
            return role?.ThreatFromOpponents == true;
        }

        public override void Evaluate(Actor actor, TileScore tileScore)
        {
            var opponents = actor.Faction?.AIFaction?.GetKnownOpponents();
            if (opponents == null) return;

            float totalThreat = 0f;

            foreach (var opponent in opponents)
            {
                // Check if opponent can attack this tile
                float threat = CalculateThreatAtTile(tileScore.Tile, opponent);
                totalThreat += threat;
            }

            // Higher threat = lower safety
            tileScore.SafetyScore -= totalThreat;
        }

        private float CalculateThreatAtTile(Tile tile, Opponent opponent)
        {
            // Check opponent's attack range
            int distance = tile.GetDistanceTo(opponent.Actor.CurrentTile);

            if (distance > opponent.DamageRange.Max)
                return 0f;  // Out of range

            // Base threat
            float threat = opponent.CurrentThreatPosedTotal;

            // Reduce threat if opponent is suppressed/stunned
            if (opponent.Actor.GetSuppressionState() == SuppressionState.PinnedDown)
                threat *= AIWeightsTemplate.Instance.ThreatFromPinnedDownOpponents;
            else if (opponent.Actor.GetSuppressionState() == SuppressionState.Suppressed)
                threat *= AIWeightsTemplate.Instance.ThreatFromSuppressedOpponents;

            // Reduce threat if opponent already acted
            if (opponent.Actor.HasActedThisTurn)
                threat *= AIWeightsTemplate.Instance.ThreatFromOpponentsAlreadyActed;

            return threat;
        }
    }

    // =========================================================================
    // BEHAVIOR BASE CLASS
    // =========================================================================

    /// <summary>
    /// Base class for AI behaviors (actions).
    /// Each behavior represents something the AI can do.
    /// </summary>
    public abstract class Behavior
    {
        /// <summary>Current score for this behavior. Offset: +0x18</summary>
        public int Score { get; protected set; }

        /// <summary>Target tile for this behavior. Offset: +0x20</summary>
        public Tile TargetTile { get; protected set; }

        /// <summary>Target entity for this behavior. Offset: +0x28</summary>
        public Entity TargetEntity { get; protected set; }

        /// <summary>
        /// Resets behavior state for new evaluation.
        /// </summary>
        public virtual void Reset()
        {
            Score = 0;
            TargetTile = null;
            TargetEntity = null;
        }

        /// <summary>
        /// Collects potential targets/data for evaluation.
        /// </summary>
        public abstract bool Collect(Actor actor);

        /// <summary>
        /// Evaluates this behavior and sets Score.
        /// Returns true if behavior is usable.
        /// </summary>
        public abstract bool Evaluate(Actor actor, Dictionary<Tile, TileScore> tiles);

        /// <summary>
        /// Executes the selected behavior.
        /// </summary>
        public abstract void Execute(Agent agent);
    }

    // =========================================================================
    // COMBAT BEHAVIORS
    // =========================================================================

    /// <summary>
    /// Base class for attack behaviors.
    /// </summary>
    public abstract class Attack : Behavior
    {
        /// <summary>Skill type identifier for deduplication. Offset: +0x28</summary>
        public int SkillType { get; protected set; }

        /// <summary>The skill to use. Offset: +0x20</summary>
        protected Skill m_Skill;
    }

    /// <summary>
    /// Behavior for dealing damage to enemies.
    ///
    /// Address: 0x18072db70 (GetTargetValue)
    /// </summary>
    public class InflictDamage : Attack
    {
        public override bool Collect(Actor actor)
        {
            // Find damage-dealing skills
            var skills = actor.GetSkillContainer()?.GetSkillsOfType(SkillTag.Damage);
            if (skills == null || skills.Count == 0)
                return false;

            m_Skill = skills[0];  // Use first available
            return true;
        }

        public override bool Evaluate(Actor actor, Dictionary<Tile, TileScore> tiles)
        {
            if (m_Skill == null || !m_Skill.CanUse())
                return false;

            var role = actor.EntityTemplate?.AIRole;
            float damageWeight = role?.InflictDamage ?? 1f;

            // Evaluate potential targets
            float bestValue = 0f;
            Entity bestTarget = null;

            var targets = GetValidTargets(actor);
            foreach (var target in targets)
            {
                float value = GetTargetValue(target, m_Skill);
                value *= damageWeight;

                if (value > bestValue)
                {
                    bestValue = value;
                    bestTarget = target;
                }
            }

            if (bestTarget != null)
            {
                TargetEntity = bestTarget;
                TargetTile = bestTarget.CurrentTile;
                Score = (int)(bestValue * AIWeightsTemplate.Instance.DamageScoreMult +
                             AIWeightsTemplate.Instance.DamageBaseScore);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Calculates target value for damage behavior.
        ///
        /// Address: 0x18072db70
        /// </summary>
        protected float GetTargetValue(Entity target, Skill skill)
        {
            // Get expected damage
            var hitResult = skill.GetHitchance(
                skill.GetActor().CurrentTile,
                target.CurrentTile,
                null, null, target, true);

            float hitChance = hitResult.FinalHitChance / 100f;
            float damage = skill.GetExpectedDamage(target);

            // Base value = expected damage
            float value = hitChance * damage;

            // Apply target priority modifiers
            var weights = AIWeightsTemplate.Instance;

            // Bonus for high-threat targets
            value *= 1f + (target.ThreatLevel * weights.TargetValueThreatScale);

            // Bonus for low-health targets (can kill)
            float healthRatio = (float)target.Hitpoints / target.HitpointsMax;
            if (damage >= target.Hitpoints)
                value *= 1.5f;  // Kill bonus
            else
                value *= 1f + (1f - healthRatio) * 0.3f;

            return value;
        }

        public override void Execute(Agent agent)
        {
            var actor = agent.GetActor();
            m_Skill.Use(TargetTile, TargetEntity);
        }

        private List<Entity> GetValidTargets(Actor actor)
        {
            // Get enemies in range
            var enemies = new List<Entity>();
            var opponents = actor.Faction?.AIFaction?.GetKnownOpponents();

            if (opponents != null)
            {
                foreach (var opp in opponents)
                {
                    if (opp.Actor.IsAlive && m_Skill.IsInRange(opp.Actor.CurrentTile))
                    {
                        enemies.Add(opp.Actor);
                    }
                }
            }

            return enemies;
        }
    }

    /// <summary>
    /// Behavior for suppressing enemies.
    /// </summary>
    public class InflictSuppression : Attack
    {
        public override bool Collect(Actor actor)
        {
            var skills = actor.GetSkillContainer()?.GetSkillsOfType(SkillTag.Suppression);
            if (skills == null || skills.Count == 0)
                return false;

            m_Skill = skills[0];
            return true;
        }

        public override bool Evaluate(Actor actor, Dictionary<Tile, TileScore> tiles)
        {
            if (m_Skill == null || !m_Skill.CanUse())
                return false;

            var role = actor.EntityTemplate?.AIRole;
            float suppressWeight = role?.InflictSuppression ?? 0.5f;

            // Target selection similar to InflictDamage
            // Prefers targets that aren't already suppressed
            // ...

            return false;  // Simplified
        }

        public override void Execute(Agent agent)
        {
            m_Skill.Use(TargetTile, TargetEntity);
        }
    }

    // =========================================================================
    // MOVEMENT BEHAVIORS
    // =========================================================================

    /// <summary>
    /// Behavior for standard movement.
    /// </summary>
    public class Move : Behavior
    {
        public override bool Collect(Actor actor)
        {
            return actor.CanMove();
        }

        public override bool Evaluate(Actor actor, Dictionary<Tile, TileScore> tiles)
        {
            var role = actor.EntityTemplate?.AIRole;
            float moveWeight = role?.Move ?? 1f;

            // Find best tile to move to
            TileScore bestTile = null;
            float bestScore = float.MinValue;

            foreach (var kvp in tiles)
            {
                if (kvp.Key == actor.CurrentTile)
                    continue;  // Skip current position

                if (!actor.CanReach(kvp.Key))
                    continue;

                if (kvp.Value.FinalScore > bestScore)
                {
                    bestScore = kvp.Value.FinalScore;
                    bestTile = kvp.Value;
                }
            }

            if (bestTile != null && bestScore > 0)
            {
                TargetTile = bestTile.Tile;
                Score = (int)(bestScore * moveWeight);
                return true;
            }

            return false;
        }

        public override void Execute(Agent agent)
        {
            agent.GetActor().MoveTo(TargetTile);
        }
    }
}
