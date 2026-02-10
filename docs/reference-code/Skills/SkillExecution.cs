// =============================================================================
// MENACE REFERENCE CODE - Skill Execution Flow
// =============================================================================
// Documents how skills are executed from targeting through effect application.
// This is the main entry point for all skill usage.
// =============================================================================

using System;
using System.Collections.Generic;

namespace Menace.Tactical.Skills
{
    /// <summary>
    /// Skill usage result returned to UI/AI.
    /// </summary>
    public class SkillUseResult
    {
        /// <summary>Did the skill execute successfully? Offset: +0x10</summary>
        public bool Success;

        /// <summary>Error message if failed. Offset: +0x18</summary>
        public string ErrorMessage;

        /// <summary>Hit results for each target. Offset: +0x20</summary>
        public List<TargetHitResult> TargetResults;

        /// <summary>Total damage dealt. Offset: +0x28</summary>
        public float TotalDamage;

        /// <summary>Was any target killed? Offset: +0x2C</summary>
        public bool AnyKills;
    }

    /// <summary>
    /// Hit result for a single target.
    /// </summary>
    public class TargetHitResult
    {
        /// <summary>The target entity. Offset: +0x10</summary>
        public Entity Target;

        /// <summary>Hit result (miss/graze/hit/crit). Offset: +0x18</summary>
        public HitResult HitResult;

        /// <summary>Damage dealt to this target. Offset: +0x1C</summary>
        public float Damage;

        /// <summary>Was target killed? Offset: +0x20</summary>
        public bool Killed;

        /// <summary>Status effects applied. Offset: +0x28</summary>
        public List<StatusTemplate> AppliedStatuses;
    }

    /// <summary>
    /// Main skill controller attached to each entity.
    ///
    /// Handles:
    /// - Skill availability checks
    /// - Targeting validation
    /// - Execution flow
    ///
    /// Address: 0x1806b1000 (class start)
    /// </summary>
    public class SkillContainer
    {
        /// <summary>Owner entity. Offset: +0x10</summary>
        private Entity m_Owner;

        /// <summary>All skills this entity has. Offset: +0x18</summary>
        private List<Skill> m_Skills;

        /// <summary>Currently selected skill. Offset: +0x20</summary>
        private Skill m_SelectedSkill;

        // =====================================================================
        // SKILL ACCESS
        // =====================================================================

        /// <summary>
        /// Gets all skills of specified tag type.
        ///
        /// Address: 0x1806b1240
        /// </summary>
        public List<Skill> GetSkillsOfType(SkillTag tag)
        {
            var result = new List<Skill>();
            foreach (var skill in m_Skills)
            {
                if (skill.Template.Tags.HasFlag(tag))
                {
                    result.Add(skill);
                }
            }
            return result;
        }

        /// <summary>
        /// Gets skill by template name.
        ///
        /// Address: 0x1806b1320
        /// </summary>
        public Skill GetSkill(string templateName)
        {
            foreach (var skill in m_Skills)
            {
                if (skill.Template.name == templateName)
                    return skill;
            }
            return null;
        }

        // =====================================================================
        // SKILL USAGE
        // =====================================================================

        /// <summary>
        /// Main entry point for using a skill.
        ///
        /// Address: 0x1806b1500
        /// </summary>
        /// <param name="skill">Skill to use</param>
        /// <param name="targetTile">Target tile</param>
        /// <param name="targetEntity">Target entity (optional)</param>
        /// <returns>Result of skill execution</returns>
        public SkillUseResult UseSkill(Skill skill, Tile targetTile, Entity targetEntity = null)
        {
            var result = new SkillUseResult { TargetResults = new List<TargetHitResult>() };

            // =========================================================
            // STEP 1: Validation
            // =========================================================

            // Check if skill can be used
            if (!skill.CanUse())
            {
                result.Success = false;
                result.ErrorMessage = "Skill cannot be used";
                return result;
            }

            // Check targeting validity
            var targeting = skill.Template.Targeting;
            if (!ValidateTarget(skill, targetTile, targetEntity, out string error))
            {
                result.Success = false;
                result.ErrorMessage = error;
                return result;
            }

            // =========================================================
            // STEP 2: Gather targets
            // =========================================================

            var targets = GatherTargets(skill, targetTile, targetEntity);

            // =========================================================
            // STEP 3: Pay costs
            // =========================================================

            PaySkillCosts(skill);

            // =========================================================
            // STEP 4: Execute for each target
            // =========================================================

            // Create random for deterministic resolution
            var random = new Random(GetDeterministicSeed());

            foreach (var target in targets)
            {
                var targetResult = ExecuteOnTarget(skill, target, targetTile, random);
                result.TargetResults.Add(targetResult);

                result.TotalDamage += targetResult.Damage;
                if (targetResult.Killed)
                    result.AnyKills = true;
            }

            // =========================================================
            // STEP 5: Post-execution
            // =========================================================

            // Apply cooldown
            if (skill.Template.Cooldown > 0)
            {
                skill.RemainingCooldown = skill.Template.Cooldown;
            }

            // Trigger skill used event
            OnSkillUsed(skill, result);

            result.Success = true;
            return result;
        }

        // =====================================================================
        // VALIDATION
        // =====================================================================

        /// <summary>
        /// Validates targeting for a skill.
        ///
        /// Address: 0x1806b1890
        /// </summary>
        private bool ValidateTarget(Skill skill, Tile targetTile, Entity targetEntity, out string error)
        {
            error = null;
            var targeting = skill.Template.Targeting;

            // Check range
            float distance = m_Owner.CurrentTile.GetDistanceTo(targetTile);
            if (distance < targeting.MinRange)
            {
                error = "Target too close";
                return false;
            }
            if (distance > targeting.MaxRange)
            {
                error = "Target out of range";
                return false;
            }

            // Check line of sight
            if (targeting.RequiresLineOfSight)
            {
                if (!LineOfSight.HasLOS(m_Owner.CurrentTile, targetTile))
                {
                    error = "No line of sight";
                    return false;
                }
            }

            // Check target requirements
            if (targeting.RequiresTarget && targetEntity == null)
            {
                error = "Requires target";
                return false;
            }

            // Check faction targeting
            if (targetEntity != null)
            {
                bool isAlly = targetEntity.Faction == m_Owner.Faction;
                bool isEnemy = !isAlly;

                if (targeting.TargetType == TargetType.Enemy && !isEnemy)
                {
                    error = "Must target enemy";
                    return false;
                }
                if (targeting.TargetType == TargetType.Ally && !isAlly)
                {
                    error = "Must target ally";
                    return false;
                }
            }

            return true;
        }

        // =====================================================================
        // TARGET GATHERING
        // =====================================================================

        /// <summary>
        /// Gathers all entities affected by skill.
        ///
        /// Address: 0x1806b1c00
        /// </summary>
        private List<Entity> GatherTargets(Skill skill, Tile targetTile, Entity primaryTarget)
        {
            var targets = new List<Entity>();
            var targeting = skill.Template.Targeting;

            // Single target
            if (targeting.AreaRadius == 0)
            {
                if (primaryTarget != null)
                    targets.Add(primaryTarget);
                return targets;
            }

            // Area of effect
            var tilesInArea = GetTilesInRadius(targetTile, targeting.AreaRadius, targeting.AreaShape);

            foreach (var tile in tilesInArea)
            {
                var entity = tile.GetEntity();
                if (entity == null || !entity.IsAlive)
                    continue;

                // Check faction filter
                bool isAlly = entity.Faction == m_Owner.Faction;
                switch (targeting.AreaTargets)
                {
                    case AreaTargetType.All:
                        targets.Add(entity);
                        break;
                    case AreaTargetType.Enemies:
                        if (!isAlly) targets.Add(entity);
                        break;
                    case AreaTargetType.Allies:
                        if (isAlly) targets.Add(entity);
                        break;
                }
            }

            return targets;
        }

        // =====================================================================
        // EXECUTION
        // =====================================================================

        /// <summary>
        /// Executes skill on a single target.
        ///
        /// Address: 0x1806b2000
        /// </summary>
        private TargetHitResult ExecuteOnTarget(Skill skill, Entity target, Tile targetTile, Random random)
        {
            var result = new TargetHitResult
            {
                Target = target,
                AppliedStatuses = new List<StatusTemplate>()
            };

            // =========================================================
            // Roll to hit
            // =========================================================

            HitResult hitResult;
            if (skill.Template.AlwaysHits)
            {
                hitResult = HitResult.Hit;
            }
            else
            {
                // Get hit chance
                var hitChanceResult = skill.GetHitchance(
                    m_Owner.CurrentTile,
                    targetTile,
                    null, null,
                    target,
                    false);

                // Roll against hit chance
                int roll = random.Next(100);
                hitResult = ResolveHitRoll(roll, hitChanceResult);
            }

            result.HitResult = hitResult;

            // =========================================================
            // Apply effects
            // =========================================================

            var context = new EffectContext
            {
                Skill = skill,
                Source = m_Owner,
                Target = target,
                TargetTile = targetTile,
                SourceTile = m_Owner.CurrentTile,
                HitResult = hitResult,
                Random = random
            };

            // Track HP before for damage calculation
            int hpBefore = target.Hitpoints;

            // Process all effects
            EffectProcessor.ProcessEffects(skill, context);

            // Calculate damage dealt
            result.Damage = Math.Max(0, hpBefore - target.Hitpoints);
            result.Killed = !target.IsAlive;

            return result;
        }

        /// <summary>
        /// Resolves hit roll to HitResult.
        ///
        /// Address: 0x1806b2340
        /// </summary>
        private HitResult ResolveHitRoll(int roll, HitChanceResult hitChance)
        {
            // Check for miss
            if (roll >= hitChance.FinalHitChance)
            {
                return HitResult.Miss;
            }

            // Check for graze (partial hit)
            // Graze window is (hitChance * 0.15) at the top of the hit range
            float grazeThreshold = hitChance.FinalHitChance * 0.85f;
            if (roll >= grazeThreshold && roll < hitChance.FinalHitChance)
            {
                return HitResult.Graze;
            }

            // Check for critical
            // Crit range is at the bottom of the roll range
            if (roll < hitChance.CritChance)
            {
                return HitResult.Critical;
            }

            return HitResult.Hit;
        }

        // =====================================================================
        // COSTS
        // =====================================================================

        /// <summary>
        /// Pays all costs for using a skill.
        ///
        /// Address: 0x1806b2580
        /// </summary>
        private void PaySkillCosts(Skill skill)
        {
            var costs = skill.Template.Costs;

            // Action points
            if (costs.ActionPoints > 0)
            {
                m_Owner.CurrentAP -= costs.ActionPoints;
            }

            // Ammo
            if (costs.Ammo > 0)
            {
                var weapon = m_Owner.GetEquippedWeapon();
                if (weapon != null)
                {
                    weapon.CurrentAmmo -= costs.Ammo;
                }
            }

            // Consumable item
            if (costs.ConsumableItem != null)
            {
                m_Owner.Inventory.RemoveItem(costs.ConsumableItem, 1);
            }

            // Special resources
            foreach (var resourceCost in costs.SpecialResources)
            {
                m_Owner.SpendResource(resourceCost.ResourceType, resourceCost.Amount);
            }
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        private int GetDeterministicSeed()
        {
            // Creates seed from game state for replay consistency
            return TurnManager.CurrentTurn * 1000 + m_Owner.GetInstanceID();
        }

        private List<Tile> GetTilesInRadius(Tile center, int radius, AreaShape shape)
        {
            // Implementation varies by shape
            return new List<Tile>();
        }

        private void OnSkillUsed(Skill skill, SkillUseResult result)
        {
            // Triggers events for UI, achievements, AI reaction
        }
    }

    // =========================================================================
    // SKILL CLASS
    // =========================================================================

    /// <summary>
    /// Runtime skill instance.
    /// </summary>
    public class Skill
    {
        /// <summary>Template definition. Offset: +0x10</summary>
        public SkillTemplate Template;

        /// <summary>Owner entity. Offset: +0x18</summary>
        private Entity m_Owner;

        /// <summary>Remaining cooldown turns. Offset: +0x20</summary>
        public int RemainingCooldown;

        /// <summary>
        /// Can this skill be used right now?
        ///
        /// Address: 0x1806b3200
        /// </summary>
        public bool CanUse()
        {
            // Check cooldown
            if (RemainingCooldown > 0)
                return false;

            // Check action points
            if (m_Owner.CurrentAP < Template.Costs.ActionPoints)
                return false;

            // Check ammo
            if (Template.Costs.Ammo > 0)
            {
                var weapon = m_Owner.GetEquippedWeapon();
                if (weapon == null || weapon.CurrentAmmo < Template.Costs.Ammo)
                    return false;
            }

            // Check other costs
            foreach (var resourceCost in Template.Costs.SpecialResources)
            {
                if (!m_Owner.HasResource(resourceCost.ResourceType, resourceCost.Amount))
                    return false;
            }

            // Check status effects blocking skill use
            if (m_Owner.HasStatus(StatusType.Stun))
                return false;

            return true;
        }

        /// <summary>
        /// Gets hit chance for this skill against a target.
        ///
        /// Address: 0x1806b3400 (delegates to HitChanceCalculation)
        /// </summary>
        public HitChanceResult GetHitchance(
            Tile sourceTile,
            Tile targetTile,
            object unused1,
            object unused2,
            Entity target,
            bool isPreview)
        {
            return HitChanceCalculation.Calculate(
                this, m_Owner, target,
                sourceTile, targetTile,
                isPreview);
        }

        /// <summary>
        /// Checks if target tile is in range.
        ///
        /// Address: 0x1806b3580
        /// </summary>
        public bool IsInRange(Tile targetTile)
        {
            float distance = m_Owner.CurrentTile.GetDistanceTo(targetTile);
            return distance >= Template.Targeting.MinRange &&
                   distance <= Template.Targeting.MaxRange;
        }

        /// <summary>
        /// Gets expected damage for preview.
        ///
        /// Address: 0x1806b3680
        /// </summary>
        public float GetExpectedDamage(Entity target)
        {
            float totalDamage = 0f;

            foreach (var effect in Template.Effects)
            {
                if (effect is DamageEffect damageEffect)
                {
                    totalDamage += damageEffect.BaseDamage;
                }
            }

            // Apply damage multipliers
            totalDamage *= m_Owner.Properties.GetDamageMult();

            // Apply target's damage reduction
            float damageReduction = target.Properties.GetDamageReduction();
            totalDamage *= (1f - damageReduction);

            return totalDamage;
        }

        public Entity GetActor() => m_Owner;
    }

    // =========================================================================
    // SKILL TEMPLATE
    // =========================================================================

    /// <summary>
    /// Skill template (ScriptableObject).
    /// Defines a skill's behavior.
    /// </summary>
    public class SkillTemplate
    {
        public string name;
        public SkillTag Tags;
        public SkillTargeting Targeting;
        public SkillCosts Costs;
        public List<SkillEffect> Effects;
        public int Cooldown;
        public bool AlwaysHits;
    }

    /// <summary>
    /// Skill targeting configuration.
    /// </summary>
    public class SkillTargeting
    {
        public float MinRange;
        public float MaxRange;
        public bool RequiresLineOfSight;
        public bool RequiresTarget;
        public TargetType TargetType;
        public int AreaRadius;
        public AreaShape AreaShape;
        public AreaTargetType AreaTargets;
    }

    /// <summary>
    /// Skill costs.
    /// </summary>
    public class SkillCosts
    {
        public int ActionPoints;
        public int Ammo;
        public object ConsumableItem;
        public List<ResourceCost> SpecialResources = new List<ResourceCost>();
    }

    public class ResourceCost
    {
        public string ResourceType;
        public int Amount;
    }

    public enum TargetType
    {
        Any = 0,
        Enemy = 1,
        Ally = 2,
        Self = 3,
        Tile = 4
    }

    public enum AreaShape
    {
        Circle = 0,
        Line = 1,
        Cone = 2,
        Cross = 3
    }

    public enum AreaTargetType
    {
        All = 0,
        Enemies = 1,
        Allies = 2
    }

    [Flags]
    public enum SkillTag
    {
        None = 0,
        Damage = 1,
        Heal = 2,
        Suppression = 4,
        Movement = 8,
        Buff = 16,
        Debuff = 32,
        Summon = 64,
        Utility = 128
    }

    // =========================================================================
    // HIT CHANCE (referenced from Skills)
    // =========================================================================

    public class HitChanceResult
    {
        public float FinalHitChance;
        public float CritChance;
    }

    public static class HitChanceCalculation
    {
        public static HitChanceResult Calculate(
            Skill skill, Entity source, Entity target,
            Tile sourceTile, Tile targetTile, bool isPreview)
        {
            // Full implementation in Combat/HitChanceCalculation.cs
            return new HitChanceResult { FinalHitChance = 75, CritChance = 5 };
        }
    }

    public static class LineOfSight
    {
        public static bool HasLOS(Tile from, Tile to) => true;
    }

    public static class TurnManager
    {
        public static int CurrentTurn = 0;
    }

    // =========================================================================
    // ENTITY EXTENSIONS (referenced types)
    // =========================================================================

    public partial class Entity
    {
        public int CurrentAP;
        public Inventory Inventory;

        public Weapon GetEquippedWeapon() => null;
        public bool HasStatus(StatusType type) => false;
        public bool HasResource(string type, int amount) => true;
        public void SpendResource(string type, int amount) { }
        public int GetInstanceID() => 0;
    }

    public class Weapon
    {
        public int CurrentAmmo;
    }

    public class Inventory
    {
        public void RemoveItem(object item, int count) { }
    }

    public partial class Tile
    {
        public Entity GetEntity() => null;
    }
}
