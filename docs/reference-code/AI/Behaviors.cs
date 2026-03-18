// =============================================================================
// MENACE REFERENCE CODE - AI Behaviors & Criterions
// =============================================================================
// Behavior classes represent actions the AI can take.
// Criterion classes evaluate tile positions for scoring.
//
// WARNING: This is REFERENCE DOCUMENTATION for modders. The actual implementations
// are significantly more complex than typical game AI. This document reflects
// the actual binary code as decompiled from the game.
// =============================================================================

using System;
using System.Collections.Generic;

namespace Menace.Tactical.AI
{
    // =========================================================================
    // AI CONFIGURATION WEIGHTS (AIConfig/AIWeightsTemplate)
    // =========================================================================
    // These weights are loaded from AIWeightsTemplate and control AI behavior.
    // Constructor at: 0x18070e550
    // =========================================================================

    /// <summary>
    /// AI configuration weights template. Controls all AI decision-making multipliers.
    /// Loaded as a singleton from game data.
    /// </summary>
    public class AIWeightsTemplate
    {
        // Base Weights
        public float BaseWeight;                    // +0x18: 1.0
        public int ElementCount;                    // +0x1C: 4

        // Distance Scoring
        public float DistanceWeight;                // +0x40: 10.0 - Used in TileScore.GetScore
        public float DistanceWeightScaled;          // +0x44: Used in TileScore.GetScaledScore

        // Cover & Safety
        public float CoverAgainstOpponentsWeight;   // +0x70: 1.0 - Applied to cover score
        public float ThreatFromOpponentsWeight;     // +0x74: 1.0 - Applied to threat score
        public float UnknownThreatMult;             // +0x78: 0.1 - Multiplier for unknown opponents

        // Tile Effect Damage
        public float TileEffectDamageBase;          // +0x7C: 10.0
        public float TileEffectDamageMult;          // +0x80: 1.0

        // Suppression State Modifiers (threat reduction when opponent is suppressed)
        public float PanickedThreatMult;            // +0x90: 0.75 - Applied when opponent is panicked
        public float StunnedThreatMult;             // +0x94: 0.85 - Applied when opponent is stunned
        public float VehicleDamagedMult;            // +0x98: 0.25 - Applied when vehicle is damaged
        public float AlreadyDamagedMult;            // +0x9C: 0.75 - Applied when target already damaged
        public float LeaderMult;                    // +0xA0: 0.9 - Applied for leader targets
        public float MultipleAttackersMult;         // +0xA4: 0.5 - Applied when multiple attackers
        public float AdditionalCoverMult;           // +0xA8: Additional cover multiplier
        public float ThreatThreshold;               // +0xAC: 6.0 - Threshold for multiple attackers

        // Cover Penalties
        public float NoCoverPenalty;                // +0xD4: 0.1 - Penalty for no cover in direction
        public float InsideCoverWeight;             // +0xD8: Weight for inside cover template AI value
        public float InsideCoverProtectionWeight;   // +0xDC: Weight for inside cover protection
        public float NotVisibleMult;                // +0xE0: 0.25 - Multiplier when not visible

        // Damage Scoring
        public float DamageScaling;                 // +0xE4: 1.0 - Base damage scaling
        public float ArmorDamageMult;               // +0xE8: Armor damage multiplier
        public float MoraleImpactMult;              // +0xEC: 1.0 - Morale impact multiplier
        public float MultiHitMult;                  // +0xF0: Multi-hit skill multiplier
        public float ThreatScaling;                 // +0xF4: 0.25 - Threat scaling factor
        public float ThreatBonusMult;               // +0xF8: 0.5 - Threat bonus multiplier
        public float MinAttackValue;                // +0xFC: 0.1 - Minimum attack value threshold
        public float ScoreMultiplier;               // +0x104: 100.0 - Final score multiplier

        // Distance Penalties
        public float OverRangePenalty;              // +0x158: 1.0 - Penalty when beyond move range
    }

    // =========================================================================
    // TILE SCORE STRUCTURE
    // =========================================================================
    // Address: 0x1807382a0 (Reset), 0x1807381e0 (GetScore), 0x180738110 (GetScaledScore)
    // =========================================================================

    /// <summary>
    /// Score data for a tile position.
    /// Aggregates scores from multiple criterions.
    ///
    /// IMPORTANT: FinalScore is CALCULATED dynamically, not stored.
    /// The structure has scaled versions of utility/safety scores for different AI modes.
    /// </summary>
    public class TileScore
    {
        // Core tile references
        public Tile Tile;                           // +0x10: The tile being scored
        public object Unknown1;                     // +0x18: Cleared in Reset (purpose unclear)

        // Distance scoring
        public float DistanceToCurrentTile;         // +0x20: Distance from actor's current tile
        public float DistanceScore;                 // +0x24: Distance-based penalty score

        // Utility scoring (damage potential, etc)
        public float UtilityScore;                  // +0x28: Aggregated utility score (used in GetScore)
        public float ScaledUtilityScore;            // +0x2C: Scaled utility (used in GetScaledScore)

        // Safety scoring (cover, threat avoidance)
        public float SafetyScore;                   // +0x30: Aggregated safety score (used in GetScore)
        public float ScaledSafetyScore;             // +0x34: Scaled safety (used in GetScaledScore)

        // Additional fields
        public float Unknown2;                      // +0x38: Cleared in Reset
        public int Unknown3;                        // +0x40: Cleared in Reset
        public int Flags;                           // +0x44: Default 0x400

        // References (cleared in Reset)
        public object Reference1;                   // +0x48: Reference field
        public object Reference2;                   // +0x50: Reference field
        public object Reference3;                   // +0x58: Reference field

        // Visibility flags
        public byte VisibilityData;                 // +0x60: Visibility-related (short/byte)
        public byte VisibleToOpponents;             // +0x61: Whether tile is visible to any opponent
        public byte IsValid;                        // +0x62: Default true (1)

        /// <summary>
        /// Calculate the final score for standard AI evaluation.
        /// Address: 0x1807381e0
        ///
        /// Formula: (UtilityScore + SafetyScore) - (DistanceToCurrentTile + DistanceScore) * AIConfig.DistanceWeight[+0x40]
        /// </summary>
        public float GetScore()
        {
            var aiConfig = AIConfig.Instance;
            return (UtilityScore + SafetyScore) - (DistanceToCurrentTile + DistanceScore) * aiConfig.DistanceWeight;
        }

        /// <summary>
        /// Calculate the scaled score for scaled AI evaluation mode.
        /// Address: 0x180738110
        ///
        /// Formula: (ScaledUtilityScore + ScaledSafetyScore) - (DistanceToCurrentTile + DistanceScore) * AIConfig[+0x44]
        /// </summary>
        public float GetScaledScore()
        {
            var aiConfig = AIConfig.Instance;
            return (ScaledUtilityScore + ScaledSafetyScore) - (DistanceToCurrentTile + DistanceScore) * aiConfig.DistanceWeightScaled;
        }

        /// <summary>
        /// Reset this tile score for reuse from pool.
        /// Address: 0x1807382a0
        /// </summary>
        public void Reset(Tile tile)
        {
            Tile = tile;
            Unknown1 = null;
            DistanceToCurrentTile = 0;
            DistanceScore = 0;
            UtilityScore = 0;
            ScaledUtilityScore = 0;
            SafetyScore = 0;
            ScaledSafetyScore = 0;
            Unknown2 = 0;
            Unknown3 = 0;
            Flags = 0x400;      // Default flags
            Reference1 = null;
            Reference2 = null;
            Reference3 = null;
            VisibilityData = 0;
            VisibleToOpponents = 0;
            IsValid = 1;        // Default valid
        }
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

        /// <summary>
        /// Get utility threshold for criterion evaluation.
        /// Used by CoverAgainstOpponents for inside-cover scoring.
        /// </summary>
        protected virtual float GetUtilityThreshold(Entity entity) => 0f;
    }

    // =========================================================================
    // BUILT-IN CRITERIONS
    // =========================================================================

    /// <summary>
    /// Scores tiles based on cover from enemy positions.
    /// Controlled by RoleData.CoverAgainstOpponents (role flag at +0x3E).
    ///
    /// Address: 0x180754f50 (Evaluate), 0x180755e30 (IsValid), 0x180755e80 (Constructor)
    ///
    /// This is one of the most complex criterions, handling:
    /// - Container entity evaluation with InsideCoverTemplate
    /// - Multi-direction cover calculation (primary direction + 2 adjacent)
    /// - Distance-based threat falloff
    /// - Suppression state modifiers
    /// - Line of sight and range checks
    /// - No-cover penalty in all 8 directions
    /// </summary>
    public class CoverAgainstOpponents : Criterion
    {
        // Cover penalty array (initialized from static data in constructor)
        // Index 0 = no cover, 1 = light, 2 = medium, 3 = heavy
        private float[] CoverPenalties;  // +0x10: float[4]

        /// <summary>
        /// Constructor initializes the cover penalties array.
        /// Address: 0x180755e80
        /// </summary>
        public CoverAgainstOpponents()
        {
            // Initialized from static data - likely { 1.0f, 0.7f, 0.4f, 0.1f }
            CoverPenalties = new float[4];
        }

        /// <summary>
        /// Check if criterion is valid for this actor.
        /// Address: 0x180755e30
        ///
        /// Checks:
        /// 1. Entity.GetCoverUsage() != -2 (entity can use cover)
        /// 2. Role flag at +0x3E is set
        /// </summary>
        public override bool IsValid(Actor actor)
        {
            int coverUsage = actor.GetCoverUsage();
            if (coverUsage == -2)
                return false;

            var role = actor.Agent?.GetRole();
            if (role == null)
                return false;

            return role.CoverAgainstOpponents;  // +0x3E
        }

        /// <summary>
        /// Evaluate cover score for a tile.
        /// Address: 0x180754f50
        ///
        /// This is approximately 700 lines of decompiled code. Key operations:
        ///
        /// 1. CONTAINER/INSIDE COVER HANDLING:
        ///    - If tile has an entity that isn't the actor, check if actor can be contained
        ///    - Get InsideCoverTemplate AI value and protection value
        ///    - Subtract: UtilityScore -= insideCoverAIValue * AIConfig[+0xD8]
        ///    - Subtract: UtilityScore -= insideCoverProtection * AIConfig[+0xDC]
        ///    - Add utility threshold to SafetyScore
        ///
        /// 2. FOR EACH KNOWN OPPONENT:
        ///    a. Determine threat modifier based on suppression state:
        ///       - If vehicle is damaged (flag & 1): use AIConfig[+0x98] (VehicleDamagedMult = 0.25)
        ///       - If panicked (GetSuppressionState == 2): use AIConfig[+0x90] (0.75)
        ///       - If already damaged (GetDamageState == 1): use AIConfig[+0x9C] (0.75)
        ///       - If stunned (GetSuppressionState == 1): use AIConfig[+0x94] (0.85)
        ///       - If leader (flag at +0x164): use AIConfig[+0xA0] (0.9)
        ///       - Otherwise: base threat modifier (1.0)
        ///
        ///    b. Get opponent's damage range and check if tile is within range + margin
        ///
        ///    c. For each tile in opponent's attack range (iterating X and Y):
        ///       - Check line of sight if applicable
        ///       - Calculate distance to opponent
        ///       - Calculate direction-based cover in 3 directions:
        ///         * Primary direction (direct line to opponent)
        ///         * Adjacent direction +1 (clockwise)
        ///         * Adjacent direction -1 (counter-clockwise)
        ///       - Apply cover penalties from CoverPenalties array
        ///       - Apply distance falloff: score = (1.0 - distance / maxRange) * threatMod
        ///       - Track best cover score from all positions
        ///
        /// 3. POST-OPPONENT LOOP:
        ///    - Apply additional multiplier: totalScore += bestScore * AIConfig[+0xA8]
        ///    - Check cover in all 8 directions for no-cover penalty
        ///    - For each direction with no cover: subtract AIConfig[+0xD4] (NoCoverPenalty)
        ///    - If tile has no cover at all and actor uses cover: add fixed penalty
        ///    - If tile is not visible to opponents: multiply by role visibility modifier
        ///
        /// 4. FINAL CALCULATION:
        ///    - UtilityScore += totalScore * AIConfig[+0x70] (CoverAgainstOpponentsWeight)
        /// </summary>
        public override void Evaluate(Actor actor, TileScore tileScore)
        {
            var aiFaction = actor.Faction?.AIFaction;
            if (aiFaction == null || !aiFaction.HasKnownOpponent())
                return;

            Tile tile = tileScore.Tile;
            if (tile == null)
                return;

            // Handle container entities (vehicles, etc)
            if (!tile.IsEmpty())
            {
                Entity tileEntity = tile.GetEntity();
                if (tileEntity != actor)
                {
                    // Check if actor can be contained within this entity
                    if (aiFaction.HasKnownOpponent())
                        return;

                    if (tileEntity != actor.ContainerEntity)
                    {
                        if (!actor.IsContainableWithin(tileEntity))
                            return;
                    }

                    // Handle InsideCoverTemplate bonuses
                    var insideCover = tileEntity.GetActor()?.GetInsideCoverTemplate();
                    if (insideCover != null)
                    {
                        int aiValue = insideCover.GetAIValue();
                        int protection = insideCover.Protection;

                        var config = AIConfig.Instance;
                        tileScore.UtilityScore -= aiValue * config.InsideCoverWeight;      // +0xD8
                        tileScore.UtilityScore -= protection * config.InsideCoverProtectionWeight; // +0xDC
                    }

                    tileScore.SafetyScore += GetUtilityThreshold(actor);
                }
            }

            // Get map for tile lookups
            var map = TacticalManager.Instance?.Map;
            if (map == null)
                return;

            float bestScore = 0f;
            float totalScore = 0f;
            var opponents = aiFaction.GetKnownOpponents();

            foreach (var opponent in opponents)
            {
                Entity oppEntity = opponent.Entity;
                if (oppEntity == null)
                    continue;

                Tile oppTile = oppEntity.GetActor()?.CurrentTile;
                if (oppTile == null)
                    continue;

                // Determine threat modifier based on opponent state
                float threatMod = GetThreatModifier(oppEntity);

                // Get opponent's assessment data for range calculations
                var assessment = opponent.Assessment;
                if (assessment?.DamageRange == null)
                    continue;

                int minRange = assessment.DamageRange.Min;
                int maxRange = assessment.DamageRange.Max;

                // Iterate opponent's potential attack positions
                int oppX = oppTile.X;
                int oppY = oppTile.Y;
                int tileRange = 0; // Vehicle multi-tile range

                // Check if opponent is discovered and calculate vehicle range
                if (oppEntity.IsDiscovered(actor.FactionId))
                {
                    var oppActor = oppEntity.GetActor();
                    if (oppActor?.MovementTemplate != null)
                    {
                        int speed = oppActor.GetState()?.SpeedModifier ?? 0;
                        int moveRange = oppActor.MovementTemplate.MoveRange;
                        tileRange = (speed / moveRange) / 2;
                    }
                }

                // Calculate distance and direction to this tile
                int distance = oppTile.GetDistanceTo(tile);
                int direction = oppTile.GetDirectionTo(tile);
                int coverAtOpp = oppTile.GetCover(direction, oppEntity);
                int coverAtTile = tile.GetCover(DirectionExtensions.Flip(direction), actor);

                // Iterate potential attack positions (for multi-tile entities)
                for (int dx = -tileRange; dx <= tileRange; dx++)
                {
                    for (int dy = -tileRange; dy <= tileRange; dy++)
                    {
                        Tile attackTile = map.GetTile(oppX + dx, oppY + dy);
                        if (attackTile == null)
                            continue;

                        if (!attackTile.IsEmpty() && attackTile != oppTile)
                            continue;

                        int atkDistance = attackTile.GetDistanceTo(tile);

                        // Check if within damage range
                        if (aiFaction.HasKnownOpponent())
                        {
                            if (atkDistance > maxRange + 3 || atkDistance < minRange - 3)
                                continue;

                            // Check line of sight and range validity
                            if (!assessment.MeleeRange?.IsWithinDistance(atkDistance) ?? false)
                            {
                                if (!attackTile.HasLineOfSightTo(tile))
                                    continue;
                            }
                        }

                        // Calculate cover-based score
                        float posScore = CalculateCoverScore(
                            tile, attackTile, actor, oppEntity,
                            atkDistance, tileRange, threatMod,
                            assessment, coverAtOpp, coverAtTile);

                        if (posScore > bestScore)
                            bestScore = posScore;

                        if (attackTile == oppTile)
                            totalScore += posScore;
                    }
                }
            }

            // Apply additional multiplier for best score
            var aiConfig = AIConfig.Instance;
            totalScore += bestScore * aiConfig.AdditionalCoverMult; // +0xA8

            // Check for no cover penalty in all 8 directions
            if (!aiFaction.HasKnownOpponent())
            {
                for (int dir = 0; dir < 8; dir++)
                {
                    int cover = tile.GetCover(dir, actor);
                    if (cover == 0)
                    {
                        totalScore -= aiConfig.NoCoverPenalty; // +0xD4
                    }
                }
            }

            // Penalize tiles with no cover when actor uses cover
            if (totalScore != 0f)
            {
                int coverUsage = actor.GetCoverUsage();
                if (coverUsage != -2 && !tile.HasCover())
                {
                    totalScore += -10.0f; // Fixed no-cover penalty
                }
            }

            // Apply visibility modifier if tile is not visible to opponents
            if (tileScore.VisibleToOpponents == 0)
            {
                var actorData = actor.GetActor()?.RoleData;
                if (actorData?.HideFromOpponents == true)
                {
                    totalScore *= 0.5f; // Hiding bonus
                }
            }

            // Apply final weight
            tileScore.UtilityScore += totalScore * aiConfig.CoverAgainstOpponentsWeight; // +0x70
        }

        /// <summary>
        /// Calculate cover score for a specific attack position.
        /// Evaluates cover in primary direction and two adjacent directions.
        /// </summary>
        private float CalculateCoverScore(
            Tile targetTile, Tile attackTile, Actor actor, Entity opponent,
            int distance, int tileRange, float threatMod,
            Assessment assessment, int coverAtOpp, int coverAtTarget)
        {
            float distanceFalloff;
            if (distance < 2)
            {
                // Very close - use full penalty from cover array
                distanceFalloff = threatMod * 0.5f;
            }
            else
            {
                // Calculate distance-based falloff
                int rangeSpread = assessment.DamageRange.Max - assessment.DamageRange.Min;
                int effectiveRange = Math.Max(1, rangeSpread);
                distanceFalloff = 1.0f - (float)distance / effectiveRange;
                if (distanceFalloff < 0.25f)
                    distanceFalloff = 0.25f;
                distanceFalloff *= threatMod * 0.5f;
            }

            // Get direction and calculate cover in 3 directions
            int direction = targetTile.GetDirectionTo(attackTile);
            int dirPlus = (direction + 1) % 8;
            int dirMinus = direction - 1;
            if (dirMinus < 0) dirMinus = 7;

            float score = 0f;

            if (distance < 2)
            {
                // Very close - use first penalty value for all
                float penalty = CoverPenalties[0];
                score = distanceFalloff * penalty * 3;
            }
            else
            {
                // Get cover in each direction
                int coverMain = targetTile.GetCover(direction, actor);
                int coverPlus = targetTile.GetCover(dirPlus, actor);
                int coverMinus = targetTile.GetCover(dirMinus, actor);

                float penaltyMain = CoverPenalties[Math.Min(coverMain, 3)];
                float penaltyPlus = CoverPenalties[Math.Min(coverPlus, 3)] * 0.5f;
                float penaltyMinus = CoverPenalties[Math.Min(coverMinus, 3)] * 0.5f;

                score = distanceFalloff * (penaltyMain + penaltyPlus + penaltyMinus);
            }

            return score;
        }

        /// <summary>
        /// Get threat modifier based on opponent's current state.
        /// Uses AIConfig multipliers for different suppression/damage states.
        /// </summary>
        private float GetThreatModifier(Entity opponent)
        {
            var config = AIConfig.Instance;
            var actor = opponent.GetActor();
            if (actor == null)
                return 1.0f;

            // Check if vehicle and damaged
            if (actor.IsLeader)
            {
                var state = actor.GetState();
                if (state != null && (state.Flags & 1) != 0)
                {
                    return config.VehicleDamagedMult; // +0x98: 0.25
                }
            }

            // Check suppression state
            int suppressionState = actor.GetSuppressionState();
            if (suppressionState == 2) // Panicked
            {
                return config.PanickedThreatMult; // +0x90: 0.75
            }

            var state2 = actor.GetState();
            if (state2 != null && (state2.Flags & 1) != 0)
            {
                return config.VehicleDamagedMult; // +0x98: 0.25
            }

            int damageState = actor.GetDamageState();
            if (damageState == 1) // Already damaged
            {
                return config.AlreadyDamagedMult; // +0x9C: 0.75
            }

            if (suppressionState == 1) // Stunned
            {
                return config.StunnedThreatMult; // +0x94: 0.85
            }

            // Check if leader
            if (actor.IsLeader)
            {
                return config.LeaderMult; // +0xA0: 0.9
            }

            return 1.0f;
        }
    }

    /// <summary>
    /// Penalizes tiles far from current position.
    /// Controlled by RoleData.DistanceToCurrentTile (role flag at +0x3F).
    ///
    /// Address: 0x1807581f0 (Evaluate), 0x1807583c0 (IsValid)
    ///
    /// The actual implementation considers:
    /// - Actor's movement template and move range
    /// - Current speed modifier from state
    /// - Over-range penalty multiplier from AIConfig
    /// - Role-based distance weight
    /// </summary>
    public class DistanceToCurrentTile : Criterion
    {
        /// <summary>
        /// Check if criterion is valid.
        /// Address: 0x1807583c0
        ///
        /// Checks:
        /// 1. Actor has movement template (+0x2B0)
        /// 2. TacticalManager.Instance[+0x60] != 0
        /// 3. Role flag at +0x3F is set
        /// </summary>
        public override bool IsValid(Actor actor)
        {
            var actorData = actor.GetActor();
            if (actorData?.MovementTemplate == null)
                return false;

            if (TacticalManager.Instance?.MapActive != true)
                return false;

            var role = actor.Agent?.GetRole();
            return role?.DistanceToCurrentTile ?? false; // +0x3F
        }

        /// <summary>
        /// Evaluate distance penalty for this tile.
        /// Address: 0x1807581f0
        ///
        /// Algorithm:
        /// 1. Get actor's action points remaining
        /// 2. Get move range from MovementTemplate (+0x2B0 -> +0x118)
        /// 3. Get speed modifier from current state (+0x3C)
        /// 4. Calculate effective range: moveRange + speedMod (minimum 1)
        /// 5. Calculate distance from tile to current position
        /// 6. Get role weight from role data (+0x20)
        /// 7. If distance > (actionPoints / effectiveRange):
        ///    - Apply over-range penalty from AIConfig[+0x158]
        /// 8. Final: DistanceScore += distance * roleWeight * penaltyMult
        /// </summary>
        public override void Evaluate(Actor actor, TileScore tileScore)
        {
            int actionPoints = actor.GetActionPoints();
            var actorData = actor.GetActor();

            if (actorData?.MovementTemplate == null)
                return;

            int moveRange = actorData.MovementTemplate.MoveRange; // +0x118
            var state = actorData.GetState();
            if (state == null)
                return;

            int speedMod = state.SpeedModifier; // +0x3C
            int effectiveRange = moveRange + speedMod;
            if (effectiveRange < 1)
                effectiveRange = 1;

            Tile targetTile = tileScore.Tile;
            Tile currentTile = actorData.CurrentTile;
            if (targetTile == null || currentTile == null)
                return;

            int distance = targetTile.GetDistanceTo(currentTile);

            var role = actor.Agent?.GetRole();
            if (role == null)
                return;

            float roleWeight = role.DistanceWeight; // +0x20

            float penaltyMult = 1.0f;
            if (distance > actionPoints / effectiveRange)
            {
                var config = AIConfig.Instance;
                penaltyMult = config.OverRangePenalty; // +0x158: 1.0
            }

            tileScore.DistanceScore += distance * roleWeight * penaltyMult;
        }
    }

    /// <summary>
    /// Scores tiles by threat from opponent attacks.
    /// Controlled by RoleData.ThreatFromOpponents (role flags at +0x41, agent +0x51).
    ///
    /// Address: 0x180764c40 (Evaluate), 0x1807656c0 (Score), 0x180764f20 (AI_ThreatFromOpponents_Score)
    ///
    /// This criterion has two main components:
    /// 1. Evaluate: Handles container entities and delegates to Score
    /// 2. Score: Complex multi-tile threat evaluation (~400 lines)
    /// 3. AI_ThreatFromOpponents_Score: Individual target scoring helper
    /// </summary>
    public class ThreatFromOpponents : Criterion
    {
        /// <summary>
        /// Check if criterion is valid.
        /// Address: 0x180764e90
        ///
        /// Checks:
        /// 1. TacticalManager.Instance[+0x60] != 0
        /// 2. Role flag at +0x41 is set
        /// 3. Agent[+0x51] == 0 (not skipping threat evaluation)
        /// </summary>
        public override bool IsValid(Actor actor)
        {
            if (TacticalManager.Instance?.MapActive != true)
                return false;

            var role = actor.Agent?.GetRole();
            if (role?.ThreatFromOpponents != true) // +0x41
                return false;

            // Check agent flag - skip if evaluating special mode
            return actor.Agent?.SkipThreatEval == false; // +0x51
        }

        /// <summary>
        /// Evaluate threat at this tile.
        /// Address: 0x180764c40
        ///
        /// Algorithm:
        /// 1. Check actor state (GetActorState must be > 1)
        /// 2. If tile has a container entity:
        ///    a. Calculate HP ratio of container
        ///    b. Call Score for container
        ///    c. Multiply by HP-based modifier: (2.0 - hpPct) * threatWeight * (entityCount/maxCount)
        /// 3. Call Score for actor itself
        /// 4. Apply AIConfig[+0x74] (ThreatFromOpponentsWeight)
        /// </summary>
        public override void Evaluate(Actor actor, TileScore tileScore)
        {
            // Check actor state
            int actorState = actor.GetActorState();
            if (actorState <= 1)
                return;

            Tile tile = tileScore.Tile;
            if (tile?.IsEmpty() == false)
            {
                Entity tileEntity = tile.GetEntity();
                if (tileEntity != actor)
                {
                    // Check if this is a container for entities
                    if (tileEntity.IsContainerForEntities())
                    {
                        int entityCount = actor.Hitpoints.Count;
                        int maxEntities = (int)actor.MaxEntities;

                        float containerScore = Score(actor, tileEntity, tileScore);
                        var config = AIConfig.Instance;
                        float threatWeight = config.ThreatFromOpponentsWeight; // +0x74

                        float hpPct = tileEntity.GetHitpointsPct();
                        float modifier = (2.0f - hpPct) * threatWeight * ((float)entityCount / maxEntities);

                        tileScore.UtilityScore += containerScore * modifier;
                    }
                }
            }

            // Score for the actor itself
            float actorScore = Score(actor, actor, tileScore);
            var aiConfig = AIConfig.Instance;
            tileScore.UtilityScore += actorScore * aiConfig.ThreatFromOpponentsWeight;
        }

        /// <summary>
        /// Calculate threat score from all known opponents.
        /// Address: 0x1807656c0
        ///
        /// This is approximately 400 lines of complex threat evaluation.
        ///
        /// For each opponent:
        /// 1. Check visibility and detection state
        /// 2. Handle opponent discovery status
        /// 3. Calculate vehicle multi-tile range if applicable
        /// 4. Get cover values in both directions
        /// 5. For each potential attack position:
        ///    a. Call AI_ThreatFromOpponents_Score for individual threat
        ///    b. Apply cover comparison modifiers:
        ///       - If target has no cover but attacker does: multiply by 1.3 (FlankBonus)
        ///       - If target has less cover than current: multiply by 0.5
        ///       - If target has more cover: multiply by 1.3
        ///    c. Check if opponent can flank (has movement and cover exists)
        ///    d. Apply flanking threat bonus if applicable: multiply by 2.0
        ///    e. Track best threat from all positions
        /// 6. Apply distance falloff based on tile range
        /// </summary>
        private float Score(Actor actor, Entity target, TileScore tileScore)
        {
            var aiFaction = actor.Faction?.AIFaction;
            if (aiFaction == null)
                return 0f;

            var opponents = aiFaction.GetKnownOpponents();
            if (opponents == null)
                return 0f;

            var map = TacticalManager.Instance?.Map;
            if (map == null)
                return 0f;

            float totalScore = 0f;

            foreach (var opponent in opponents)
            {
                Entity oppEntity = opponent.Entity;
                if (oppEntity == null)
                    continue;

                Tile oppTile = oppEntity.GetActor()?.CurrentTile;
                if (oppTile == null)
                    continue;

                // Update visibility flag
                if (tileScore.VisibleToOpponents == 0)
                {
                    bool detected = actor.IsDetectedByFaction(oppEntity.FactionId);
                    bool hasLOS = Actor_HasLineOfSightTo_WithStats(
                        oppEntity, actor, detected, oppTile, tileScore.Tile);
                    tileScore.VisibleToOpponents = (byte)(hasLOS ? 1 : 0);
                }
                else
                {
                    tileScore.VisibleToOpponents = 1;
                }

                // Check if opponent is known
                bool isKnown = opponent.IsKnown();
                if (!isKnown)
                {
                    var config = AIConfig.Instance;
                    if (config.UnknownThreatMult == 0f) // +0x78
                        continue;
                }

                // Calculate vehicle multi-tile range
                int tileRange = 0;
                if (oppEntity.IsDiscovered(actor.FactionId))
                {
                    var oppActor = oppEntity.GetActor();
                    if (oppActor != null && !oppActor.IsLeader)
                    {
                        var oppState = oppActor.GetState();
                        if (oppState != null && (oppState.Flags & 1) == 0)
                        {
                            if (oppActor.GetSuppressionState() != 2 && oppActor.GetDamageState() != 1)
                            {
                                var movement = oppActor.GetMovement();
                                if (movement != null)
                                {
                                    int speed = movement.Speed;
                                    int moveRange = oppActor.MovementTemplate?.MoveRange ?? 1;
                                    tileRange = (speed / moveRange) / 2;
                                }
                            }
                        }
                    }
                }

                // Calculate cover and direction
                int distance = oppTile.GetDistanceTo(tileScore.Tile);
                int direction = oppTile.GetDirectionTo(tileScore.Tile);
                int coverAtOpp = oppTile.GetCover(direction, oppEntity);
                int coverAtTarget = tileScore.Tile.GetCover(
                    DirectionExtensions.Flip(direction), actor);

                // Evaluate all potential attack positions
                float bestScore = 0f;
                int oppX = oppTile.X;
                int oppY = oppTile.Y;

                for (int dx = -tileRange; dx <= tileRange; dx++)
                {
                    for (int dy = -tileRange; dy <= tileRange; dy++)
                    {
                        Tile attackTile = map.GetTile(oppX + dx, oppY + dy);
                        if (attackTile == null)
                            continue;

                        // Check tile validity
                        if (!attackTile.IsEmpty())
                        {
                            if (attackTile != oppTile)
                            {
                                int atkDist = attackTile.GetDistanceTo(tileScore.Tile);
                                int atkDir = attackTile.GetDirectionTo(tileScore.Tile);
                                if (atkDist >= distance && atkDir != direction)
                                    continue;
                            }
                        }

                        // Calculate individual threat score
                        float posScore = AI_ThreatFromOpponents_Score(
                            actor, opponent, attackTile, target, tileScore.Tile);

                        if (posScore > 0f && attackTile != oppTile)
                        {
                            // Calculate cover at this attack position
                            int atkDir = attackTile.GetDirectionTo(tileScore.Tile);
                            int coverAtAtk = attackTile.GetCover(atkDir, oppEntity);
                            int targetCoverFromAtk = tileScore.Tile.GetCover(
                                DirectionExtensions.Flip(atkDir), actor);

                            // Apply cover comparison modifiers
                            if (targetCoverFromAtk == 0 && coverAtTarget > 0)
                            {
                                posScore *= 1.3f; // Flank bonus
                            }

                            if (coverAtAtk < coverAtOpp)
                            {
                                posScore *= 0.5f; // Worse cover for attacker
                            }
                            else if (coverAtOpp < coverAtAtk)
                            {
                                posScore *= 1.3f; // Better cover for attacker
                            }

                            // Check flanking potential
                            var oppActor = oppEntity.GetActor();
                            if (oppActor?.MovePoints >= 0 && oppTile.HasCover() && !attackTile.HasCover())
                            {
                                var assessment = opponent.Assessment?.Movement;
                                if (assessment != null && assessment.Count > 0 && assessment.MaxRange > 2)
                                {
                                    posScore *= 2.0f; // Flanking threat bonus
                                }
                            }

                            // Check if opponent can't move
                            if (oppActor?.MovePoints < 0)
                            {
                                posScore *= 1.3f;
                            }
                        }

                        // Apply distance falloff
                        if (attackTile != oppTile && tileRange > 0)
                        {
                            int tileDist = attackTile.GetDistanceTo(oppTile);
                            posScore *= (1.0f - (float)tileDist / (tileRange * 4.0f));
                        }

                        if (posScore > bestScore)
                            bestScore = posScore;
                    }
                }

                totalScore += bestScore;
            }

            return totalScore;
        }

        /// <summary>
        /// Calculate individual threat score from an opponent at a specific position.
        /// Address: 0x180764f20
        ///
        /// Evaluates:
        /// 1. All opponent skills that can target this position
        /// 2. Checks skill usability and applicability
        /// 3. Calculates base threat using Criterion.Score
        /// 4. Applies state modifiers:
        ///    - Unknown opponent: multiply by AIConfig[+0x78] (0.1)
        ///    - Leader with damaged vehicle: multiply by AIConfig[+0x98] (0.25)
        ///    - Panicked: multiply by AIConfig[+0x90] (0.75)
        ///    - Already damaged: multiply by AIConfig[+0x9C] (0.75)
        ///    - Stunned: multiply by AIConfig[+0x94] (0.85)
        ///    - Is leader: multiply by AIConfig[+0xA0] (0.9)
        /// 5. Checks multiple attacker bonus:
        ///    - If threat contribution below threshold (AIConfig[+0xAC] = 6.0)
        ///    - If 2+ attackers targeting same position
        ///    - Apply multiple attackers modifier: AIConfig[+0xA4] (0.5)
        /// </summary>
        private float AI_ThreatFromOpponents_Score(
            Actor actor, Opponent opponent, Tile attackTile,
            Entity target, Tile targetTile)
        {
            var assessment = opponent.Assessment;
            if (assessment?.Skills == null)
                return 0f;

            float bestScore = 0f;

            foreach (var skill in assessment.Skills)
            {
                if (skill?.Template == null)
                    continue;

                var targetParams = TargetUsageParams.Default;
                if (!skill.IsUsableOn(targetTile, attackTile, targetParams))
                    continue;

                if (!skill.IsApplicableTo(targetTile, target))
                    continue;

                float skillScore = Criterion.Score(target, skill, attackTile, targetTile);
                if (skillScore > bestScore)
                    bestScore = skillScore;
            }

            if (bestScore == 0f)
                return 0f;

            // Apply known/unknown modifier
            if (!opponent.IsKnown())
            {
                var config = AIConfig.Instance;
                bestScore *= config.UnknownThreatMult; // +0x78: 0.1
            }

            // Apply suppression/damage state modifiers
            Entity oppEntity = opponent.Entity;
            var oppActor = oppEntity?.GetActor();
            if (oppActor != null)
            {
                var config = AIConfig.Instance;

                if (oppActor.IsLeader)
                {
                    var state = oppActor.GetState();
                    if (state != null && (state.Flags & 1) != 0)
                    {
                        bestScore *= config.VehicleDamagedMult; // +0x98
                        goto ApplyMultipleAttackers;
                    }
                }

                int suppState = oppActor.GetSuppressionState();
                if (suppState == 2) // Panicked
                {
                    bestScore *= config.PanickedThreatMult; // +0x90
                }
                else
                {
                    var state = oppActor.GetState();
                    if (state != null && (state.Flags & 1) != 0)
                    {
                        bestScore *= config.VehicleDamagedMult; // +0x98
                    }
                    else
                    {
                        int dmgState = oppActor.GetDamageState();
                        if (dmgState == 1)
                        {
                            bestScore *= config.AlreadyDamagedMult; // +0x9C
                        }
                        else if (suppState == 1)
                        {
                            bestScore *= config.StunnedThreatMult; // +0x94
                        }
                        else if (oppActor.IsLeader)
                        {
                            bestScore *= config.LeaderMult; // +0xA0
                        }
                    }
                }
            }

            ApplyMultipleAttackers:
            // Check for multiple attackers bonus
            if (bestScore > 0f)
            {
                var aiFaction = TacticalManager.Instance?.GetActiveAIFaction();
                if (aiFaction != null && !aiFaction.IsThinking())
                {
                    var assessment2 = opponent.Assessment;
                    if (assessment2?.ThreatContributions != null)
                    {
                        float contribution = assessment2.ThreatContributions.GetValue(actor);
                        var config = AIConfig.Instance;

                        if (contribution < config.ThreatThreshold) // +0xAC: 6.0
                        {
                            float contribution2 = assessment2.ThreatContributions2?.GetValue(actor) ?? 0f;
                            if (contribution2 < config.ThreatThreshold)
                            {
                                int attackCount1 = assessment2.AttackCounts?[1] ?? 0;
                                int attackCount2 = assessment2.AttackCounts?[2] ?? 0;

                                if (attackCount1 + attackCount2 > 1)
                                {
                                    bestScore *= config.MultipleAttackersMult; // +0xA4: 0.5
                                }
                            }
                        }
                    }
                }
            }

            return bestScore;
        }

        private bool Actor_HasLineOfSightTo_WithStats(
            Entity entity, Actor actor, bool detected, Tile from, Tile to)
        {
            // Stub - actual implementation checks line of sight with stat modifiers
            return from.HasLineOfSightTo(to);
        }
    }

    // =========================================================================
    // BEHAVIOR BASE CLASS
    // =========================================================================

    /// <summary>
    /// Base class for AI behaviors (actions).
    /// Each behavior represents something the AI can do.
    ///
    /// Note: The actual structure has many more fields than documented here.
    /// Field discovery from InflictDamage constructor (0x18072dda0):
    /// - +0x60: int field (initialized to 0)
    /// - +0x90: string ID field
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
    /// Address: 0x18072db70 (GetTargetValue - wrapper)
    ///
    /// IMPORTANT: GetTargetValue at 0x18072db70 is a WRAPPER function that:
    /// 1. Gets uses left this turn from skill container
    /// 2. Delegates to SkillBehavior.GetTargetValue (0x180733460)
    ///
    /// The actual target value calculation is in SkillBehavior.GetTargetValue,
    /// which is approximately 800 lines of comprehensive AI target evaluation.
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
                Score = (int)(bestValue * AIWeightsTemplate.Instance.ScoreMultiplier +
                             AIWeightsTemplate.Instance.MinAttackValue);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Wrapper function that delegates to SkillBehavior.GetTargetValue.
        ///
        /// Address: 0x18072db70
        ///
        /// This wrapper:
        /// 1. Gets skill container from behavior data (+0x10 -> +0x18)
        /// 2. Gets action points from actor
        /// 3. Gets uses left this turn from skill
        /// 4. Delegates to SkillBehavior.GetTargetValue with all parameters
        /// </summary>
        protected float GetTargetValue(Entity target, Skill skill)
        {
            // Get uses left this turn
            var skillContainer = GetSkillContainer();
            int actionPoints = skillContainer?.Actor?.GetActionPoints() ?? 0;
            int usesLeft = skill.GetUsesLeftThisTurn(actionPoints);

            // Delegate to SkillBehavior.GetTargetValue
            return SkillBehavior_GetTargetValue(
                this, true, usesLeft, target.CurrentTile, 0,
                skill, target, null);
        }

        /// <summary>
        /// The actual target value calculation.
        /// Address: 0x180733460
        ///
        /// This is approximately 800 lines of comprehensive AI target evaluation.
        ///
        /// Parameters:
        /// - param_2 (bool): Attack mode (true) vs preview mode (false)
        /// - param_3 (int): Uses left this turn
        /// - param_4 (Tile): Target tile
        /// - param_5 (uint): Mode flags
        /// - param_6 (Skill): The skill being evaluated
        /// - param_7 (Tile): Override target tile (optional)
        /// - param_8 (Tile): Override attacker tile (optional)
        /// - param_9 (bool): Use container entity
        ///
        /// EVALUATION STEPS:
        ///
        /// 1. TILE EFFECT DAMAGE:
        ///    - Iterates tile effect handlers
        ///    - For ApplySkillTileEffectHandler with damage flag:
        ///      score += Criterion.Score * AIConfig[+0x80] * AIConfig[+0x7C]
        ///
        /// 2. HIT CHANCE AND EXPECTED DAMAGE:
        ///    - Builds attack and defense properties
        ///    - Calls Skill.GetHitchance (normalize to 0-1 range)
        ///    - Calls Skill.GetExpectedDamage
        ///    - expectedDamage = hitChance * rawDamage
        ///
        /// 3. SUPPRESSION VALUE:
        ///    - Calculates expected suppression from skill
        ///    - Uses expected damage to estimate morale impact
        ///
        /// 4. ARMOR DAMAGE:
        ///    - If damage > 0 and target has armor:
        ///      a. Get armor value and durability
        ///      b. Calculate remaining HP after damage
        ///      c. armorScore = (armorDurability * armor - (remainingHP / maxHP) * armor)
        ///                      * AIConfig[+0xE8] * min(armor, 100) * 0.02
        ///
        /// 5. DAMAGE SCALING:
        ///    - damageRatio = (hitChance * expectedDamage) / targetMaxHP
        ///    - Clamp to max 2.0
        ///    - Apply AIConfig[+0xE4] (DamageScaling)
        ///
        /// 6. KILL POTENTIAL (HP Threshold Tiers):
        ///    - Uses Config.HPThresholds array for tiered scoring
        ///    - For each threshold tier that damage crosses:
        ///      a. Get bonus multiplier from Config.HPBonuses[tier]
        ///      b. Apply: score *= (1 + tier * 0.01 * bonus * 0.01)
        ///    - If expectedDamage >= targetHP: apply kill bonus (1.5x)
        ///    - If overkill (> maxHP): apply overkill multiplier
        ///
        /// 7. ACCURACY MODIFIERS:
        ///    - If hit chance < 0.4 or > 4.0: multiply by 0.5
        ///    - If hit chance between 2.0 and some threshold: multiply by 1.5 (sweet spot)
        ///
        /// 8. MORALE IMPACT (Attack mode only):
        ///    - Get target's current morale percentage
        ///    - Calculate projected morale after attack
        ///    - If morale would drop below 0:
        ///      moraleBonus = abs(projectedMorale) * AIConfig[+0xEC]
        ///      Clamp to max 1.0, add 2x multiplier
        ///    - If morale would cross panic threshold (0.4):
        ///      Add AIConfig[+0xEC] * 0.5 bonus
        ///    - Check AttackMoraleHandler for additional morale damage
        ///
        /// 9. SUPPRESSION SCORING:
        ///    - If suppression value > 0:
        ///      a. Scale by AIConfig[+0xEC] based on expected suppression
        ///      b. Apply cover modifier if target has no cover
        ///      c. Check if suppression would change enemy state
        ///      d. If state change: apply state change multiplier
        ///    - Apply target health modifier
        ///
        /// 10. THREAT BONUS:
        ///     - If value > threshold and not thinking:
        ///       a. Get opponent assessment
        ///       b. Check actor's contribution to total threat
        ///       c. If below threshold: add threat bonus * AIConfig[+0xF8]
        ///
        /// 11. SPECIAL STATE MODIFIERS:
        ///     - If target is panicked/stunned/damaged: multiply by 0.25
        ///     - If target is not leader: multiply by 1.5
        ///
        /// 12. MULTI-USE SCALING:
        ///     - If usesLeft > 1: Apply diminishing returns formula
        ///       score = score/usesLeft + (usesLeft-1) * score * 0.1
        ///
        /// 13. PREVIEW MODE ADJUSTMENTS:
        ///     - Check distance to target
        ///     - If within skill range + 2: multiply by 0.5
        ///
        /// 14. STRATEGY TAG VALUES:
        ///     - Get skill's tag value against this target
        ///     - Apply strategy data modifiers
        /// </summary>
        private float SkillBehavior_GetTargetValue(
            Behavior behavior, bool attackMode, int usesLeft,
            Tile targetTile, uint modeFlags, Skill skill,
            Entity target, Tile overrideTile)
        {
            // This is a simplified representation of the actual 800-line function
            // See decompiled code at 0x180733460 for full implementation

            if (targetTile == null || targetTile.IsEmpty())
                return 0f;

            Entity targetEntity = target ?? targetTile.GetEntity();
            if (targetEntity == null)
                return 0f;

            Actor targetActor = targetEntity.IsActor() ? targetEntity.ToActor() : null;

            // Build properties
            var attackProps = skill.GetActor()?.GetSkillContainer()
                ?.BuildPropertiesForUse(skill, skill.GetActor().CurrentTile, targetTile, targetEntity);
            var defenseProps = targetEntity.GetSkillContainer()
                ?.BuildPropertiesForDefense(skill.GetActor(), skill.GetActor().CurrentTile, targetTile, skill);

            // Calculate hit chance and damage
            var hitResult = skill.GetHitchance(
                skill.GetActor().CurrentTile, targetTile,
                attackProps, defenseProps, true, targetEntity, true);
            float hitChance = hitResult.FinalValue * 0.01f;

            var expectedDamage = skill.GetExpectedDamage(
                skill.GetActor().CurrentTile, targetTile, targetTile,
                targetEntity, attackProps, defenseProps, usesLeft);

            if (targetEntity.Hitpoints == null || expectedDamage == null)
                return 0f;

            // Base value from expected damage
            float expectedPhysical = hitChance * expectedDamage.Physical;
            int targetMaxHP = targetEntity.HitpointsMax;
            float damageRatio = expectedPhysical / targetMaxHP;
            if (damageRatio > 2.0f) damageRatio = 2.0f;

            var config = AIConfig.Instance;
            float value = config.DamageScaling * damageRatio; // +0xE4

            // Armor damage bonus
            if (hitChance * expectedDamage.ArmorDamage > 0f)
            {
                var state = targetActor?.GetState();
                int armor = state?.GetArmor(3) ?? 0;
                float armorDurability = targetEntity.GetArmorDurabilityPct();
                float remainingHP = Math.Max(0f, targetEntity.Hitpoints.Current - hitChance * expectedDamage.ArmorDamage);
                float maxHP = targetEntity.HitpointsMax;
                float armorScore = (armorDurability * armor - (remainingHP / maxHP) * armor)
                    * config.ArmorDamageMult * Math.Min(armor, 100f) * 0.02f;
                value += armorScore;
            }

            // Kill potential
            int targetHP = targetEntity.Hitpoints.Current;
            if (hitChance * expectedDamage.Total >= targetHP)
            {
                value *= 1.5f; // Kill bonus
            }
            else if (expectedDamage.Overkill && hitChance > 0f)
            {
                value *= 1.5f;
            }

            // Apply hit chance sweet spot
            float accuracy = expectedDamage.Accuracy;
            if (accuracy < 0.4f || accuracy > 4.0f)
            {
                value *= 0.5f;
            }
            else if (accuracy >= 2.0f && accuracy <= 4.0f)
            {
                value *= 1.5f;
            }

            // Morale impact (attack mode)
            if (attackMode && targetActor != null)
            {
                var actorState = targetActor.GetState();
                if (actorState != null && (actorState.Flags & 0x80) == 0 && targetActor.Morale > 0f)
                {
                    float moralePct = targetActor.GetMoralePct();
                    // Calculate projected morale...
                    // (complex calculation involving damage, suppression, and morale handlers)
                }
            }

            // Suppression value
            float expectedSuppression = hitChance * expectedDamage.Suppression;
            if (expectedSuppression > 0f)
            {
                float suppScore = expectedSuppression * config.MoraleImpactMult;
                if (suppScore > 2.0f) suppScore = config.MoraleImpactMult * 2f;

                int coverMask = targetTile.GetCoverMask();
                if (coverMask == 0)
                {
                    int coverUsage = targetEntity.GetCoverUsage();
                    suppScore *= (coverUsage * 0.25f + 1.5f);
                }

                value += suppScore;
            }

            // Multi-use scaling
            if (attackMode && usesLeft > 1)
            {
                if (usesLeft > 10) usesLeft = 10;
                value = value / usesLeft + (usesLeft - 1) * value * 0.1f;
            }

            return value;
        }

        public override void Execute(Agent agent)
        {
            var actor = agent.GetActor();
            m_Skill.Use(TargetTile, TargetEntity);
        }

        private List<Entity> GetValidTargets(Actor actor)
        {
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

        private SkillContainer GetSkillContainer()
        {
            // Stub - returns skill container from behavior data
            return null;
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
            // Uses SkillBehavior.GetTargetValue with suppression mode
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
    ///
    /// Address: 0x18072eeb0 (OnEvaluate), 0x180731cb0 (OnExecute)
    ///
    /// OnExecute is approximately 300 lines and handles:
    /// - Skill list management (+0x70, +0x78 for skill iteration)
    /// - Container tracking (+0x98)
    /// - Timed execution with delays (+0x90 time tracking)
    /// - Path-based movement with intermediate skills
    /// - Hidden actor detection
    /// - Agent sleep calls
    /// - Turn completion handling
    /// </summary>
    public class Move : Behavior
    {
        // Internal skill tracking
        private List<Skill> m_Skills;       // +0x70
        private int m_SkillIndex;           // +0x78
        private object m_Container;         // +0x98
        private float m_ExecutionTime;      // +0x90

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

                float score = kvp.Value.GetScore(); // Use calculated score
                if (score > bestScore)
                {
                    bestScore = score;
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

        /// <summary>
        /// Execute movement behavior.
        /// Address: 0x180731cb0
        ///
        /// This is approximately 300 lines handling:
        /// 1. Initialize skill list from actor
        /// 2. Track execution timing
        /// 3. For each path segment:
        ///    a. Check for hidden actors
        ///    b. Execute movement skill
        ///    c. Sleep agent for animation
        ///    d. Process intermediate skills (overwatch, etc)
        /// 4. Update container tracking
        /// 5. Handle turn completion
        /// </summary>
        public override void Execute(Agent agent)
        {
            var actor = agent.GetActor();

            // Initialize skill tracking
            m_Skills = new List<Skill>();
            m_SkillIndex = 0;
            m_ExecutionTime = 0f;

            // Get path to target
            var path = actor.GetPathTo(TargetTile);
            if (path == null)
                return;

            // Execute movement with skill handling
            foreach (var pathTile in path)
            {
                // Check for hidden actors
                if (!pathTile.IsEmpty())
                {
                    var tileEntity = pathTile.GetEntity();
                    if (tileEntity != actor && tileEntity.IsHidden)
                    {
                        // Detected hidden enemy - stop movement
                        break;
                    }
                }

                // Move to this tile
                actor.MoveTo(pathTile);

                // Sleep for animation
                agent.Sleep(0.1f);

                // Process any triggered skills (reactions, overwatch)
                ProcessIntermediateSkills(agent);
            }

            // Update container if entering vehicle/building
            UpdateContainerTracking(actor);
        }

        private void ProcessIntermediateSkills(Agent agent)
        {
            // Process skills triggered during movement
            while (m_SkillIndex < m_Skills.Count)
            {
                var skill = m_Skills[m_SkillIndex++];
                // Execute reaction skills...
            }
        }

        private void UpdateContainerTracking(Actor actor)
        {
            // Update container reference if actor entered a container
            var tile = actor.CurrentTile;
            if (!tile.IsEmpty())
            {
                var entity = tile.GetEntity();
                if (entity != actor && entity.IsContainerForEntities())
                {
                    m_Container = entity;
                }
            }
        }
    }
}
