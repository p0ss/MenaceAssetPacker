// =============================================================================
// MENACE REFERENCE CODE - Hit Chance Calculation
// =============================================================================
// Reconstructed hit chance system showing how accuracy, cover, dodge, and
// distance combine to determine if an attack hits.
// =============================================================================

using Menace.Tools;

namespace Menace.Tactical.Skills
{
    /// <summary>
    /// Result structure returned by GetHitchance.
    /// Contains the final hit chance plus breakdown of contributing factors.
    /// </summary>
    public struct HitChanceResult
    {
        /// <summary>Final calculated hit chance (0-100)</summary>
        public float FinalHitChance;        // offset 0x00

        /// <summary>Base accuracy after multipliers</summary>
        public float Accuracy;              // offset 0x04

        /// <summary>Cover multiplier applied</summary>
        public float CoverMult;             // offset 0x08

        /// <summary>Dodge multiplier (flipped for defender)</summary>
        public float DodgeMult;             // offset 0x0C

        /// <summary>Whether distance calculation was applied</summary>
        public bool IncludesDistance;       // offset 0x11

        /// <summary>Distance penalty/bonus applied</summary>
        public float DistancePenalty;       // offset 0x14
    }

    public partial class Skill
    {
        // Field offsets
        // +0x10: SkillTemplate reference
        // +0x18: SkillContainer parent
        // +0xB8: IdealRange (int) from template

        /// <summary>
        /// Calculates hit chance for this skill against a target.
        ///
        /// Address: 0x1806dba90
        ///
        /// Formula:
        ///   HitChance = Accuracy * CoverMult * DodgeMult + DistancePenalty
        ///   Final = clamp(HitChance, MinHitChance, 100)
        /// </summary>
        /// <param name="sourceTile">Tile the attacker is on</param>
        /// <param name="targetTile">Tile being targeted</param>
        /// <param name="attackProps">Attacker's EntityProperties (or null to build)</param>
        /// <param name="defenseProps">Defender's EntityProperties (or null to build)</param>
        /// <param name="target">Target entity (can be null for empty tile)</param>
        /// <param name="includeDistance">Whether to apply distance penalty</param>
        /// <returns>HitChanceResult with breakdown</returns>
        public HitChanceResult GetHitchance(
            Tile sourceTile,
            Tile targetTile,
            EntityProperties attackProps,
            EntityProperties defenseProps,
            Entity target,
            bool includeDistance)
        {
            var result = new HitChanceResult();

            // Check for "always hits" flag on skill template (offset +0xF3)
            if (this.SkillTemplate.AlwaysHits)
            {
                result.FinalHitChance = 100f;
                return result;
            }

            // Build attack properties if not provided
            if (attackProps == null)
            {
                attackProps = this.SkillContainer.BuildPropertiesForUse(
                    this, sourceTile, targetTile, target);
            }

            // Build defense properties if target exists and props not provided
            float dodgeMult = 1.0f;
            if (target != null)
            {
                if (defenseProps == null)
                {
                    var targetSkillContainer = target.GetSkillContainer();
                    var attacker = this.GetActor();
                    defenseProps = targetSkillContainer.BuildPropertiesForDefense(
                        attacker, sourceTile, targetTile, this);
                }

                // Flip dodge mult: high dodge on defender = lower hit chance
                dodgeMult = FloatExtensions.Flipped(defenseProps.DodgeMult);
            }

            // Get base accuracy
            float accuracy = attackProps.GetAccuracy();
            result.Accuracy = accuracy;
            result.DodgeMult = dodgeMult;

            // Calculate cover multiplier
            bool isContained = CheckIfContained(targetTile, target);
            float coverMult = GetCoverMult(sourceTile, targetTile, target, defenseProps, isContained);
            result.CoverMult = coverMult;

            // Calculate final hit chance
            float hitChance;

            if (includeDistance && sourceTile != null)
            {
                result.IncludesDistance = true;

                // Calculate distance penalty
                int distance = sourceTile.GetDistanceTo(targetTile);
                int idealRange = this.IdealRange;  // Skill+0xB8
                int rangeDiff = Math.Abs(distance - idealRange);

                float dropoff = attackProps.GetAccuracyDropoff();
                float distancePenalty = rangeDiff * dropoff;

                result.DistancePenalty = distancePenalty;

                // Accuracy * CoverMult * DodgeMult + DistancePenalty
                hitChance = accuracy *
                            FloatExtensions.Clamped(coverMult) *
                            FloatExtensions.Clamped(dodgeMult) +
                            distancePenalty;
            }
            else
            {
                // No distance penalty
                hitChance = accuracy *
                            FloatExtensions.Clamped(coverMult) *
                            FloatExtensions.Clamped(dodgeMult);
            }

            // Clamp to 0-100 range
            const float MaxHitChance = 100f;  // DAT_182d8fd78
            hitChance = Math.Clamp(hitChance, 0f, MaxHitChance);

            // Apply minimum hit chance floor
            int minHitChance = attackProps.MinHitChance;
            if (hitChance < minHitChance)
            {
                hitChance = minHitChance;
            }

            result.FinalHitChance = hitChance;
            return result;
        }

        /// <summary>
        /// Calculates cover multiplier between attacker and defender.
        ///
        /// Address: 0x1806d9bb0
        /// </summary>
        public float GetCoverMult(
            Tile sourceTile,
            Tile targetTile,
            Entity target,
            EntityProperties defenseProps,
            bool isContained)
        {
            // Check if skill ignores cover
            if (this.SkillTemplate.IsIgnoringCoverInsideForTarget(target))
            {
                return 1.0f;
            }

            // Check if skill ignores cover at range (SkillTemplate+0x100)
            if (this.SkillTemplate.IgnoresCoverAtRange)
            {
                return 1.0f;
            }

            // Cover only applies at distance >= 2
            int distance = sourceTile.GetDistanceTo(targetTile);
            if (distance < 2)
            {
                return 1.0f;
            }

            // Get direction from target to attacker (for determining which side has cover)
            int direction = targetTile.GetDirectionTo(sourceTile);

            // Get cover level (0-3) from tile in that direction
            int coverLevel = targetTile.GetCover(direction, target);

            // Look up cover multiplier from Config table
            float[] coverMultipliers = Config.Instance.CoverMultipliers;  // Config+0x88
            float coverValue = coverMultipliers[coverLevel];

            // Apply defender's cover usage effectiveness
            float defenderCoverUsage = FloatExtensions.Clamped(defenseProps.CoverUsage);
            float coverMult = Math.Min(coverValue / defenderCoverUsage, 1.0f);

            // Convert to additive multiplier format
            return 1.0f.AddMult(coverMult);
        }

        private bool CheckIfContained(Tile targetTile, Entity target)
        {
            if (targetTile == null) return false;
            if (targetTile.IsEmpty()) return false;

            Entity tileEntity = targetTile.GetEntity();
            if (tileEntity == target) return false;

            return target?.IsContainableWithin(tileEntity) ?? false;
        }
    }
}
