// =============================================================================
// MENACE REFERENCE CODE - Hit Chance Calculation
// =============================================================================
// Reconstructed hit chance system showing how accuracy, cover, dodge, and
// distance combine to determine if an attack hits.
//
// VERIFIED ACCURACY FORMULA (from Ghidra analysis):
// =============================================================================
//
// STEP 1: Calculate base accuracy
//   accuracy = floor(AccuracyBonus * AccuracyMult)
//   - AccuracyBonus: float, 0-100 scale, ADDITIVE from all sources
//   - AccuracyMult: float, 1.0 centered, uses AddMult stacking
//
// STEP 2: Calculate accuracy dropoff (if includeDropoff=true)
//   dropoff = floor(AccuracyDropoff * AccuracyDropoffMult)
//   distance_penalty = |distance - idealRange| * dropoff
//   - AccuracyDropoff: float, per-tile penalty (negative reduces accuracy)
//   - AccuracyDropoffMult: float, 1.0 centered, uses AddMult stacking
//
// STEP 3: Apply cover and defense
//   coverMult = GetCoverMult() - 0.0 to 1.0 based on cover level
//   defenseMult = 2.0 - evasion (Flipped function)
//
// STEP 4: Final formula
//   hitChance = accuracy * Clamped(coverMult) * Clamped(defenseMult) + distance_penalty
//   hitChance = clamp(hitChance, 0, 100)
//   hitChance = max(hitChance, MinHitChance)
//
// ADDMULT STACKING FORMULA:
//   Multipliers stack additively: result = 1.0 + (mult1-1) + (mult2-1) + ...
//   Example: Two 1.5x multipliers = 1.0 + 0.5 + 0.5 = 2.0x total (not 2.25x)
//   This prevents exponential scaling from stacking many multipliers.
//
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

        /// <summary>Whether the skill always hits (100% hit chance)</summary>
        public bool AlwaysHits;             // offset 0x10

        /// <summary>Whether distance dropoff was applied</summary>
        public bool IncludeDropoff;         // offset 0x11

        // 2 bytes padding for alignment (0x12-0x13)

        /// <summary>Distance accuracy dropoff applied</summary>
        public float AccuracyDropoff;       // offset 0x14
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
        /// <param name="overrideTargetEntity">Target entity override (can be null to auto-resolve from tile)</param>
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

            // If no override target provided, try to get entity from tile
            if (target == null && targetTile != null && !targetTile.IsEmpty())
            {
                target = targetTile.GetEntity();
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
        ///
        /// Early returns 1.0f (no cover penalty) when:
        /// - AlwaysHits flag is set on skill template (SkillTemplate+0xF3)
        /// - CoverUsage on defender is <= 0.0
        /// - Skill ignores cover for the target (IsIgnoringCoverInsideForTarget)
        /// - Skill ignores cover at range (SkillTemplate+0x100)
        /// - Distance is less than 2 tiles
        /// </summary>
        public float GetCoverMult(
            Tile sourceTile,
            Tile targetTile,
            Entity target,
            EntityProperties defenseProps,
            bool isContained)
        {
            // Early return if AlwaysHits flag is set (SkillTemplate+0xF3)
            if (this.SkillTemplate.AlwaysHits)
            {
                return 1.0f;
            }

            // If no target provided, try to resolve from tile
            if (target == null && targetTile != null && !targetTile.IsEmpty())
            {
                target = targetTile.GetEntity();
            }

            // Build defense properties if not provided and target exists
            if (defenseProps == null && target != null)
            {
                var targetSkillContainer = target.GetSkillContainer();
                var attacker = this.GetActor();
                defenseProps = targetSkillContainer.BuildPropertiesForDefense(
                    attacker, sourceTile, targetTile, this);
            }

            // Early return if no defense properties available or no target
            if (defenseProps == null || target == null)
            {
                return 1.0f;
            }

            // CoverUsage zero check - if defender has no cover usage, no penalty
            float coverUsage = FloatExtensions.Clamped(defenseProps.CoverUsage);  // EntityProperties+0x88
            if (coverUsage <= 0.0f)
            {
                return 1.0f;
            }

            // Get tile entity for containment checks
            Entity tileEntity = targetTile.GetEntity();

            // Check if skill ignores cover for this target
            if (this.SkillTemplate.IsIgnoringCoverInsideForTarget(tileEntity))
            {
                return 1.0f;
            }

            // Complex containment logic: if target is contained, may use interior cover
            // from the container entity's TacticsCombat component (+0x398 -> +0xE0 -> +0x58)
            if ((isContained || target.HasContainmentProperty) && !targetTile.IsEmpty())
            {
                Entity containerEntity = targetTile.GetEntity();
                if (target != containerEntity)
                {
                    // Get interior cover from container's TacticsCombat component
                    var tacticsCombat = containerEntity.GetTacticsCombat();  // +0x398
                    if (tacticsCombat?.InteriorCover != null)  // +0xE0
                    {
                        float interiorCover = tacticsCombat.InteriorCover.CoverValue;  // +0x58
                        float coverMult = Math.Min(interiorCover / coverUsage, 1.0f);
                        return 1.0f.AddMult(coverMult);
                    }
                }
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
            // Note: GetCover takes additional params for defenseProps and whether target == tileEntity
            int coverLevel = targetTile.GetCover(direction, target, defenseProps, target == tileEntity);

            // Look up cover multiplier from Config table
            float[] coverMultipliers = Config.Instance.CoverMultipliers;  // Config+0x88
            float coverValue = coverMultipliers[coverLevel];

            // Apply defender's cover usage effectiveness
            float coverMult = Math.Min(coverValue / coverUsage, 1.0f);

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
