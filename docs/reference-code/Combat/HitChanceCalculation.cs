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
        public float FinalValue;            // offset 0x00

        /// <summary>Base accuracy after multipliers</summary>
        public float Accuracy;              // offset 0x04

        /// <summary>Cover multiplier applied</summary>
        public float CoverMult;             // offset 0x08

        /// <summary>Defense multiplier (flipped for defender)</summary>
        public float DefenseMult;           // offset 0x0C

        /// <summary>Whether distance dropoff was applied</summary>
        public bool IncludeDropoff;         // offset 0x11

        /// <summary>Distance accuracy dropoff applied</summary>
        public float AccuracyDropoff;       // offset 0x14

        /// <summary>Whether the skill always hits (100% hit chance)</summary>
        public bool AlwaysHits;             // offset 0x15
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
        ///   HitChance = Accuracy * CoverMult * DefenseMult + AccuracyDropoff
        ///   Final = clamp(HitChance, MinHitChance, 100)
        /// </summary>
        /// <param name="from">Tile the attacker is on</param>
        /// <param name="targetTile">Tile being targeted</param>
        /// <param name="properties">Attacker's EntityProperties (or null to build)</param>
        /// <param name="defenderProperties">Defender's EntityProperties (or null to build)</param>
        /// <param name="includeDropoff">Whether to apply distance dropoff</param>
        /// <param name="overrideTargetEntity">Target entity override (can be null for empty tile)</param>
        /// <param name="forImmediateUse">Whether this is for immediate use</param>
        /// <returns>HitChanceResult with breakdown</returns>
        public HitChanceResult GetHitchance(
            Tile from,
            Tile targetTile,
            EntityProperties properties,
            EntityProperties defenderProperties,
            bool includeDropoff,
            Entity overrideTargetEntity,
            bool forImmediateUse)
        {
            var result = new HitChanceResult();

            var target = overrideTargetEntity;

            // Check for "always hits" flag on skill template (offset +0xF3)
            if (this.SkillTemplate.AlwaysHits)
            {
                result.FinalValue = 100f;
                result.AlwaysHits = true;
                return result;
            }

            // Build attack properties if not provided
            if (properties == null)
            {
                properties = this.SkillContainer.BuildPropertiesForUse(
                    this, from, targetTile, target);
            }

            // Build defense properties if target exists and props not provided
            float defenseMult = 1.0f;
            if (target != null)
            {
                if (defenderProperties == null)
                {
                    var targetSkillContainer = target.GetSkillContainer();
                    var attacker = this.GetActor();
                    defenderProperties = targetSkillContainer.BuildPropertiesForDefense(
                        attacker, from, targetTile, this);
                }

                // Flip defense mult: high defense on defender = lower hit chance
                defenseMult = FloatExtensions.Flipped(defenderProperties.DefenseMult);
            }

            // Get base accuracy
            float accuracy = properties.GetAccuracy();
            result.Accuracy = accuracy;
            result.DefenseMult = defenseMult;

            // Calculate cover multiplier
            bool isContained = CheckIfContained(targetTile, target);
            float coverMult = GetCoverMult(from, targetTile, target, defenderProperties, isContained);
            result.CoverMult = coverMult;

            // Calculate final hit chance
            float hitChance;

            if (includeDropoff && from != null)
            {
                result.IncludeDropoff = true;

                // Calculate distance dropoff
                int distance = from.GetDistanceTo(targetTile);
                int idealRange = this.IdealRange;  // Skill+0xB8
                int rangeDiff = Math.Abs(distance - idealRange);

                float dropoff = properties.GetAccuracyDropoff();
                float accuracyDropoff = rangeDiff * dropoff;

                result.AccuracyDropoff = accuracyDropoff;

                // Accuracy * CoverMult * DefenseMult + AccuracyDropoff
                hitChance = accuracy *
                            FloatExtensions.Clamped(coverMult) *
                            FloatExtensions.Clamped(defenseMult) +
                            accuracyDropoff;
            }
            else
            {
                // No distance dropoff
                hitChance = accuracy *
                            FloatExtensions.Clamped(coverMult) *
                            FloatExtensions.Clamped(defenseMult);
            }

            // Clamp to 0-100 range
            const float MaxHitChance = 100f;  // DAT_182d8fd78
            hitChance = Math.Clamp(hitChance, 0f, MaxHitChance);

            // Apply minimum hit chance floor
            int minHitChance = properties.MinHitChance;
            if (hitChance < minHitChance)
            {
                hitChance = minHitChance;
            }

            result.FinalValue = hitChance;
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
