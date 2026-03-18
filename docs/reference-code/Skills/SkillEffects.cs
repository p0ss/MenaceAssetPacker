// =============================================================================
// MENACE REFERENCE CODE - Skill Effects System
// =============================================================================
// How skill effects are applied to targets. Effects are the core building
// blocks of all abilities - damage, healing, buffs, debuffs, movement, etc.
//
// ARCHITECTURE: The actual implementation uses a Handler Pattern:
// 1. Effect Templates (ScriptableObjects) - Store configuration data
// 2. Effect Handlers (Runtime) - Execute the effect logic
// 3. Factory Pattern - Create() methods instantiate handlers
//
// Handler stores effect template reference at offset +0x18
// Handler stores skill reference at offset +0x10
// =============================================================================

using System;
using System.Collections.Generic;

namespace Menace.Tactical.Skills.Effects
{
    // =========================================================================
    // EFFECT CLASSES (Actual Implementation)
    // =========================================================================
    // The actual system does NOT use a simple EffectType enum. Instead, each
    // effect is a separate class. Here is a partial list of effect classes:
    //
    // AccuracyStacks, AddItemSlot, AddSkill, AddSkillAfterMovement,
    // AmmoPouch, ApplyAuthorityDisciplineMod, ApplySkillToSelf,
    // AttachObject, AttachTemporaryPrefab, Attack, AttackMorale,
    // Berserk, CameraShake, CauseDefect,
    // ChangeAPBasedOnHP, ChangeActionPointCost, ChangeActionPoints,
    // ChangeAttackCost, ChangeDropChance, ChangeGrowthPotential,
    // ChangeHeatCapacity, ChangeMalfunctionChance, ChangeMorale,
    // ChangeMovementCost, ChangeProperty, ChangePropertyAura,
    // ChangePropertyConditional, ChangePropertyConsecutive,
    // ChangePropertyTarget, ChangeRangesOfSkillsWithTags,
    // ChangeSkillUseAmount, ChangeStance, ChangeSupplyCosts,
    // ChangeSuppression, ChangeUsesPerSquaddie,
    // Charge, ChargeInfantry, ClearTileEffect, ClearTileEffectGroup,
    // ConsumeOnSkillUse, Cooldown, CounterAttack,
    // Damage, DamageArmorDurability, DamageOverTime, Deathrattle,
    // DelayTurn, DeployHeavyWeapon, DestroyProps, DisableByFlag,
    // DisableItem, DisableSkills, DisallowInvisible, DisplayText,
    // DivineIntervention, DropRecoverableObject, EjectEntity,
    // EmitAura, EnemiesDropPickupOnDeath, FilterByCondition,
    // FilterByMorale, FilterByOtherSkills, IgnoreDamage,
    // JetPack, Minions.CommandUseSkill,
    // RestoreArmorDurability, SpawnTileEffect, Suppression,
    // UseSkill, and many more...
    // =========================================================================

    // =========================================================================
    // BASE HANDLER CLASS
    // =========================================================================

    /// <summary>
    /// Base class for skill effect handlers.
    /// Each handler stores a reference to its effect template and the skill.
    ///
    /// SkillEventHandler base methods:
    ///   GetActor @ 0x1806eef20 - Returns the Actor using this skill
    ///   GetEntity @ 0x1806eef40 - Returns the target Entity
    ///   GetOwner @ 0x1806eef60 - Returns the owner Entity
    /// </summary>
    public abstract class SkillEventHandler
    {
        /// <summary>Reference to the Skill instance. Offset: +0x10</summary>
        public Skill Skill;

        /// <summary>Reference to the effect template (ScriptableObject). Offset: +0x18</summary>
        public object EffectData;

        /// <summary>Cached calculated value. Offset: +0x20</summary>
        public float CachedValue;

        /// <summary>Previous effect multiplier for change detection. Offset: +0x24</summary>
        public int PreviousEffectMult;

        public abstract void OnAdded();
        public abstract void OnRemoved();

        /// <summary>
        /// Gets the Actor from the skill reference.
        /// Address: 0x1806eef20
        /// </summary>
        public Actor GetActor()
        {
            // Delegates to BaseSkill.GetActor via skill at +0x10
            return Skill?.GetActor();
        }

        /// <summary>
        /// Gets the target Entity.
        /// Address: 0x1806eef40
        /// </summary>
        public Entity GetEntity(int targetIndex = 0)
        {
            return Skill?.GetEntity(targetIndex);
        }
    }

    // =========================================================================
    // DAMAGE EFFECT
    // =========================================================================

    /// <summary>
    /// Damage effect template (ScriptableObject).
    /// Stored in handler at +0x18.
    ///
    /// Factory: Damage.Create @ 0x180703500
    /// </summary>
    [Serializable]
    public class Damage
    {
        // All offsets are relative to the effect template object

        /// <summary>
        /// If true, damage redirects to entity inside target (passenger).
        /// Checks if target actor has a contained entity at actor+0x68.
        /// Offset: +0x58
        /// </summary>
        public bool IsAppliedOnlyToPassengers;

        /// <summary>
        /// Base flat damage added to hit count calculation.
        /// Offset: +0x5c
        /// </summary>
        public int FlatDamageBase;

        /// <summary>
        /// Fraction 0.0-1.0 of target's elements to hit.
        /// Formula: ceil(elementCount * pct)
        /// Offset: +0x60
        /// </summary>
        public float ElementsHitPercentage;

        /// <summary>
        /// Flat HP damage added to the damage total.
        /// Offset: +0x64
        /// </summary>
        public float DamageFlatAmount;

        /// <summary>
        /// Percentage of target's CURRENT HP as damage (0.0-1.0 scale).
        /// E.g., 0.10 = 10% of current HP.
        /// Offset: +0x68
        /// </summary>
        public float DamagePctCurrentHitpoints;

        /// <summary>
        /// Minimum floor for current HP% damage.
        /// If (currentHP * pct) is below this minimum, use this value instead.
        /// Offset: +0x6c
        /// </summary>
        public float DamagePctCurrentHitpointsMin;

        /// <summary>
        /// Percentage of target's MAX HP as damage (0.0-1.0 scale).
        /// Offset: +0x70
        /// </summary>
        public float DamagePctMaxHitpoints;

        /// <summary>
        /// Minimum floor for max HP% damage.
        /// If (maxHP * pct) is below this minimum, use this value instead.
        /// Offset: +0x74
        /// </summary>
        public float DamagePctMaxHitpointsMin;

        /// <summary>
        /// Flat armor durability damage.
        /// Written to DamageInfo+0x34.
        /// Offset: +0x78
        /// </summary>
        public float DamageToArmor;

        /// <summary>
        /// Percentage of current armor durability to damage (0.0-1.0).
        /// Written to DamageInfo+0x38.
        /// Offset: +0x7c
        /// </summary>
        public float ArmorDmgPctCurrent;

        /// <summary>
        /// Death animation type enum.
        /// 0 = none/normal, higher values for special death animations.
        /// Written to DamageInfo+0x18.
        /// Offset: +0x80
        /// </summary>
        public int FatalityType;

        /// <summary>
        /// Reduces effective armor.
        /// Written to DamageInfo+0x40.
        /// Offset: +0x84
        /// </summary>
        public float ArmorPenetration;

        /// <summary>
        /// Armor damage scaled by number of elements hit.
        /// Written to DamageInfo+0x44.
        /// Offset: +0x88
        /// </summary>
        public float ArmorDmgFromElementCount;

        /// <summary>
        /// First element index to hit. Allows skipping first N elements.
        /// Written to DamageInfo+0x1c.
        /// Offset: +0x8c
        /// </summary>
        public int ElementHitMinIndex;

        /// <summary>
        /// Whether damage can critically strike.
        /// Written to DamageInfo+0x4e.
        /// Offset: +0x90
        /// </summary>
        public bool CanCrit;

        /// <summary>
        /// Factory method that creates a DamageHandler.
        /// Address: 0x180703500
        /// </summary>
        public static DamageHandler Create(Damage template)
        {
            var handler = new DamageHandler();
            handler.EffectData = template; // Stored at handler+0x18
            return handler;
        }
    }

    /// <summary>
    /// Runtime handler for instant damage effects.
    /// OnAdded immediately applies damage then removes itself.
    ///
    /// ApplyDamage @ 0x180702970
    /// OnAdded @ 0x180702d90
    /// </summary>
    public class DamageHandler : SkillEventHandler
    {
        /// <summary>
        /// Applies damage to the target entity.
        /// Address: 0x180702970
        ///
        /// DAMAGE FORMULA:
        /// hitCount = FlatDamageBase + ceil(elementCount * ElementsHitPercentage), minimum 1
        /// hpDamage = max(currentHP * DamagePctCurrentHitpoints, DamagePctCurrentHitpointsMin)
        ///          + DamageFlatAmount
        ///          + max(maxHP * DamagePctMaxHitpoints, DamagePctMaxHitpointsMin)
        /// </summary>
        public void ApplyDamage()
        {
            Entity target = GetEntity(0);
            if (target == null)
                return;

            Damage effectData = (Damage)EffectData;

            // Check for passenger redirection
            if (effectData.IsAppliedOnlyToPassengers)
            {
                Actor actor = GetActor();
                if (actor != null && actor.HasContainedEntity)
                {
                    // Redirect damage to passenger at actor+0x68
                    target = actor.ContainedEntity;
                    if (target == null)
                        return;
                }
            }

            // Calculate current HP % damage with minimum floor
            float currentHpDamage = target.CurrentHitpoints * effectData.DamagePctCurrentHitpoints;
            if (currentHpDamage < effectData.DamagePctCurrentHitpointsMin)
                currentHpDamage = effectData.DamagePctCurrentHitpointsMin;

            // Calculate max HP % damage with minimum floor
            float maxHpDamage = target.MaxHitpoints * effectData.DamagePctMaxHitpoints;
            if (maxHpDamage < effectData.DamagePctMaxHitpointsMin)
                maxHpDamage = effectData.DamagePctMaxHitpointsMin;

            // Total HP damage
            int totalDamage = (int)(currentHpDamage + effectData.DamageFlatAmount + maxHpDamage);

            // Calculate hit count from elements
            int elementCount = target.Elements.Count;
            int hitCount = effectData.FlatDamageBase + (int)Math.Ceiling(elementCount * effectData.ElementsHitPercentage);
            if (hitCount < 1)
                hitCount = 1;

            // Build DamageInfo structure
            var damageInfo = new DamageInfo
            {
                Damage = totalDamage,                           // +0x2c
                ArmorDmgFlat = (int)effectData.DamageToArmor,   // +0x34
                ArmorDmgPct = (int)effectData.ArmorDmgPctCurrent, // +0x38
                HitCount = hitCount,                            // +0x3c
                ArmorPenetration = effectData.ArmorPenetration, // +0x40
                ArmorDmgFromElements = effectData.ArmorDmgFromElementCount, // +0x44
                FatalityType = effectData.FatalityType,         // +0x18
                ElementHitMinIndex = effectData.ElementHitMinIndex, // +0x1c
                CanCrit = effectData.CanCrit                    // +0x4e
            };

            // Apply damage through entity virtual call
            target.TakeDamage(damageInfo, Skill);
        }

        public override void OnAdded()
        {
            ApplyDamage();
            // Handler removes itself after applying instant damage
        }

        public override void OnRemoved() { }
    }

    // =========================================================================
    // CHANGE SUPPRESSION EFFECT
    // =========================================================================

    /// <summary>
    /// ChangeSuppression effect template.
    /// NOTE: This is a simple flat suppression change. Distance-based suppression
    /// with cover mitigation is handled by Skill.ApplySuppression @ 0x1806d5930,
    /// which is a core Skill method, NOT this effect.
    ///
    /// Factory: ChangeSuppression.Create @ 0x1807007a0
    /// </summary>
    [Serializable]
    public class ChangeSuppression
    {
        /// <summary>
        /// When to apply the effect (0 = OnApply).
        /// Offset: +0x58
        /// </summary>
        public int EventType;

        /// <summary>
        /// Amount of suppression to add/remove.
        /// Offset: +0x5c
        /// </summary>
        public int SuppressionAmount;

        /// <summary>
        /// If true, apply to owner entity instead of target.
        /// Offset: +0x60
        /// </summary>
        public bool UseOwner;

        /// <summary>
        /// Factory method.
        /// Address: 0x1807007a0
        /// </summary>
        public static ChangeSuppressionHandler Create(ChangeSuppression template)
        {
            var handler = new ChangeSuppressionHandler();
            handler.EffectData = template;
            return handler;
        }
    }

    /// <summary>
    /// Runtime handler for suppression changes.
    ///
    /// OnApply @ 0x180700760
    /// Apply @ 0x180700600
    /// </summary>
    public class ChangeSuppressionHandler : SkillEventHandler
    {
        /// <summary>
        /// Applies suppression to target entity.
        /// If UseOwner is set and target has owner, applies to owner instead.
        /// Address: 0x180700600
        /// </summary>
        public void Apply(Entity target)
        {
            if (target == null)
                return;

            ChangeSuppression effectData = (ChangeSuppression)EffectData;
            Actor targetActor = target as Actor;

            // If UseOwner flag is set and target has an owner, redirect
            if (effectData.UseOwner && target.HasOwner)
            {
                Entity owner = target.ContainerEntity; // at entity+0x70
                targetActor = owner as Actor;
            }

            if (targetActor == null)
                return;

            // Apply suppression change
            targetActor.ChangeSuppressionAndUpdateAP(effectData.SuppressionAmount);

            // Notify skill that effect was applied
            Skill?.OnEffectApplied();
        }

        public override void OnAdded() { }
        public override void OnRemoved() { }
    }

    // =========================================================================
    // CHANGE PROPERTY EFFECT
    // =========================================================================

    /// <summary>
    /// ChangeProperty effect template.
    /// Modifies entity stats using enum-based property types.
    ///
    /// Factory: ChangeProperty.Create @ 0x1806ff8e0
    /// </summary>
    [Serializable]
    public class ChangeProperty
    {
        /// <summary>
        /// EntityPropertyType enum value to modify.
        /// Offset: +0x5c
        /// </summary>
        public int PropertyType;

        /// <summary>
        /// Additive value for non-multiplicative properties.
        /// Offset: +0x60
        /// </summary>
        public int Value;

        /// <summary>
        /// Multiplicative value for mult-type properties.
        /// Offset: +0x64
        /// </summary>
        public float ValueMult;

        /// <summary>
        /// Dynamic value calculator interface.
        /// May be null for static values.
        /// Offset: +0x68
        /// </summary>
        public IValueProvider ValueProvider;

        /// <summary>
        /// Index for display string in skill string array.
        /// -1 means no display string.
        /// Offset: +0x70
        /// </summary>
        public int StringValueIndex;

        /// <summary>
        /// If true, flip positive/negative sign of value.
        /// Offset: +0x74
        /// </summary>
        public bool InvertSign;

        /// <summary>
        /// Factory method.
        /// Address: 0x1806ff8e0
        /// </summary>
        public static ChangePropertyHandler Create(ChangeProperty template)
        {
            var handler = new ChangePropertyHandler();
            handler.EffectData = template;
            return handler;
        }
    }

    /// <summary>
    /// Interface for dynamic value calculation.
    /// </summary>
    public interface IValueProvider
    {
        float GetValue(object context1, object context2, object context3);
    }

    /// <summary>
    /// Runtime handler for property modifications.
    ///
    /// Apply @ 0x1806fee90
    /// </summary>
    public class ChangePropertyHandler : SkillEventHandler
    {
        /// <summary>
        /// Applies property modification to entity.
        /// Uses UpdateProperty for additive types, UpdateMultProperty for multiplicative.
        /// Address: 0x1806fee90
        /// </summary>
        public void Apply(EntityProperties properties)
        {
            ChangeProperty effectData = (ChangeProperty)EffectData;

            // Check if this is a multiplicative property type
            bool isMultProperty = PropertyTypeExtensions.IsMultProperty(effectData.PropertyType);

            if (!isMultProperty)
            {
                // Additive property
                int value = effectData.Value;

                // Apply dynamic value provider if present
                if (effectData.ValueProvider != null)
                {
                    float multiplier = effectData.ValueProvider.GetValue(null, null, null);
                    value = (int)(value * multiplier);
                    CachedValue = value;
                }

                properties.UpdateProperty(effectData.PropertyType, value);
            }
            else
            {
                // Multiplicative property
                float value = effectData.ValueMult;

                // Apply dynamic value provider if present
                if (effectData.ValueProvider != null)
                {
                    float multiplier = effectData.ValueProvider.GetValue(null, null, null);
                    // Formula: (value - 1.0) * multiplier + 1.0, clamped
                    value = FloatExtensions.Clamped((value - 1.0f) * multiplier + 1.0f);
                    CachedValue = value;
                }

                properties.UpdateMultProperty(effectData.PropertyType, value);
            }

            // Update display string if configured
            if (effectData.StringValueIndex >= 0 && Skill?.StringValues != null)
            {
                string displayValue = isMultProperty
                    ? MultToString(CachedValue, effectData.InvertSign)
                    : AbsToString(CachedValue, effectData.InvertSign);
                Skill.StringValues[effectData.StringValueIndex] = displayValue;
            }

            // Schedule skill container update
            Skill?.SkillContainer?.ScheduleUpdate();
        }

        private static string AbsToString(float value, bool invertSign) => "";
        private static string MultToString(float value, bool invertSign) => "";

        public override void OnAdded() { }
        public override void OnRemoved() { }
    }

    // =========================================================================
    // SPAWN TILE EFFECT
    // =========================================================================

    /// <summary>
    /// SpawnTileEffect template.
    /// Creates tile effects (fire, smoke, etc.) with probability-based spawning.
    ///
    /// Factory: SpawnTileEffect.Create @ 0x18071e280
    /// </summary>
    [Serializable]
    public class SpawnTileEffect
    {
        /// <summary>
        /// When to spawn the effect (event enum).
        /// Offset: +0x58
        /// </summary>
        public int EventType;

        /// <summary>
        /// Template for the tile effect to spawn.
        /// Offset: +0x60
        /// </summary>
        public TileEffectTemplate EffectToSpawn;

        /// <summary>
        /// Base percentage chance at center tile (0-100).
        /// Offset: +0x68
        /// </summary>
        public int ChanceAtCenter;

        /// <summary>
        /// Percentage modifier per tile distance from center.
        /// Can be negative to reduce chance with distance.
        /// Offset: +0x6c
        /// </summary>
        public int ChancePerTileFromCenter;

        /// <summary>
        /// Spawn delay multiplier based on distance.
        /// Offset: +0x70
        /// </summary>
        public float DelayWithDistance;

        /// <summary>
        /// Factory method.
        /// Address: 0x18071e280
        /// </summary>
        public static SpawnTileEffectHandler Create(SpawnTileEffect template)
        {
            var handler = new SpawnTileEffectHandler();
            handler.EffectData = template;
            return handler;
        }
    }

    /// <summary>
    /// Runtime handler for spawning tile effects.
    /// Refreshes existing effects instead of stacking.
    ///
    /// OnApply @ 0x18071de80
    /// Apply_WithProbability @ 0x18071dbe0
    /// </summary>
    public class SpawnTileEffectHandler : SkillEventHandler
    {
        /// <summary>
        /// Spawns tile effect with probability calculation.
        /// Address: 0x18071dbe0
        ///
        /// Probability formula:
        /// chance = ChanceAtCenter + (distance * ChancePerTileFromCenter)
        /// If chance >= 100, always spawns
        /// If chance < 100, uses PseudoRandom.NextTry for roll
        /// </summary>
        public void Apply_WithProbability(Tile targetTile, Tile centerTile)
        {
            if (targetTile == null)
                return;

            // Check if tile blocks effects
            if (targetTile.BlocksEffects)
                return;

            // Check if entity on tile allows effects
            Entity entity = targetTile.GetEntity();
            if (entity != null && !entity.AllowsTileEffects())
                return;

            SpawnTileEffect effectData = (SpawnTileEffect)EffectData;

            // Calculate spawn chance based on distance
            int distance = targetTile.GetDiagonalManhattanDistanceTo(centerTile);
            int chance = effectData.ChanceAtCenter + (distance * effectData.ChancePerTileFromCenter);

            // Roll for spawn if chance < 100
            if (chance < 100)
            {
                if (!TacticalManager.Instance.Random.NextTry(chance))
                    return; // Failed probability check
            }

            // Check if effect already exists on tile
            TileEffectHandler existingEffect = targetTile.GetEffect(effectData.EffectToSpawn);
            if (existingEffect != null)
            {
                // Refresh existing effect instead of stacking
                existingEffect.Refresh();
            }
            else
            {
                // Calculate spawn delay
                int tileDistance = targetTile.GetDistanceTo(centerTile);
                float delay = tileDistance * effectData.DelayWithDistance;

                // Create new tile effect
                var newEffect = effectData.EffectToSpawn.CreateHandler(delay);
                targetTile.AddEffect(newEffect);
            }

            Skill?.OnEffectApplied();
        }

        public override void OnAdded() { }
        public override void OnRemoved() { }
    }

    // =========================================================================
    // RESTORE ARMOR DURABILITY EFFECT
    // =========================================================================

    /// <summary>
    /// RestoreArmorDurability effect template.
    /// Restores a percentage of max armor durability.
    ///
    /// Factory: RestoreArmorDurability.Create @ 0x18071b140
    /// </summary>
    [Serializable]
    public class RestoreArmorDurability
    {
        /// <summary>
        /// Percentage of max durability to restore (0-100).
        /// Offset: +0x58
        /// </summary>
        public int RestorePercent;

        /// <summary>
        /// Factory method.
        /// Address: 0x18071b140
        /// </summary>
        public static RestoreArmorDurabilityHandler Create(RestoreArmorDurability template)
        {
            var handler = new RestoreArmorDurabilityHandler();
            handler.EffectData = template;
            return handler;
        }
    }

    /// <summary>
    /// Runtime handler for armor restoration.
    ///
    /// ApplyToEntity @ 0x18071aff0
    /// </summary>
    public class RestoreArmorDurabilityHandler : SkillEventHandler
    {
        /// <summary>
        /// Restores armor durability by percentage of max.
        /// Address: 0x18071aff0
        ///
        /// Formula:
        /// restoredAmount = ceil(RestorePercent / 100.0 * MaxDurability)
        /// newDurability = clamp(currentDurability + restoredAmount, 0, MaxDurability)
        /// </summary>
        public void ApplyToEntity(Entity target)
        {
            if (target == null)
                return;

            RestoreArmorDurability effectData = (RestoreArmorDurability)EffectData;

            int currentDurability = target.CurrentArmorDurability; // entity+0x5c
            int maxDurability = target.MaxArmorDurability;         // entity+0x60

            // Calculate restoration amount
            float percent = effectData.RestorePercent / 100.0f;
            int restoredAmount = (int)Math.Ceiling(percent * maxDurability);

            // Calculate new durability with clamping
            int newDurability = currentDurability + restoredAmount;
            if (newDurability < 0)
                newDurability = 0;
            else if (newDurability > maxDurability)
                newDurability = maxDurability;

            // Skip if no change
            if (newDurability == currentDurability)
                return;

            // Apply armor change
            target.SetArmorDurability(newDurability);

            // Trigger OnArmorChanged event
            float durabilityPct = target.GetArmorDurabilityPct();
            int armorValue = target.Properties?.GetArmor() ?? 0;
            TacticalManager.Instance?.InvokeOnArmorChanged(target, durabilityPct, armorValue, 1000);
        }

        public override void OnAdded() { }
        public override void OnRemoved() { }
    }

    // =========================================================================
    // CHARGE EFFECT (Movement)
    // =========================================================================

    /// <summary>
    /// Charge effect - movement ability that moves through target.
    /// NOTE: This is a specific movement implementation. Push/Pull effects
    /// are separate implementations.
    ///
    /// Factory: Charge.Create @ 0x180701ce0
    /// </summary>
    [Serializable]
    public class Charge
    {
        // Charge effect uses handler fields directly

        /// <summary>
        /// Factory method.
        /// Address: 0x180701ce0
        /// </summary>
        public static ChargeHandler Create(Charge template)
        {
            var handler = new ChargeHandler();
            handler.EffectData = template;
            return handler;
        }
    }

    /// <summary>
    /// Runtime handler for charge movement.
    ///
    /// OnUse @ 0x180700f40
    /// </summary>
    public class ChargeHandler : SkillEventHandler
    {
        /// <summary>
        /// Executes charge movement.
        /// Address: 0x180700f40
        ///
        /// 1. Gets direction from actor to target tile
        /// 2. Finds destination (tile past target via GetNextTile)
        /// 3. Calls MoveToWithSkill with flag 5 (charge movement type)
        /// 4. If move fails for player, shows notification
        /// 5. Applies skill to target tile if flag 8 is set
        /// </summary>
        public void OnUse(Actor actor, Tile targetTile)
        {
            if (actor == null)
                return;

            Tile actorTile = actor.GetCurrentTile();
            if (actorTile == null)
                return;

            // Get direction from actor to target
            int direction = actorTile.GetDirectionTo(targetTile);

            // Get destination (tile past the target)
            Tile destination = targetTile.GetNextTile(direction);

            // Get actor's movement component
            var movement = actor.Movement;
            if (movement == null || movement.PathFinder == null)
                return;

            // Build movement flags
            int moveFlags = 0;
            int actorFacing = actor.GetFacing();
            int flippedDirection = DirectionExtensions.Flip(direction);
            if (actorFacing == flippedDirection)
            {
                moveFlags |= 4; // Facing opposite direction flag
            }

            // Execute charge movement (type 5)
            bool success = actor.MoveToWithSkill(destination, ref moveFlags, this, 5);

            if (!success)
            {
                // Show notification for player-controlled units
                if (actor.IsPlayerControlled)
                {
                    string message = Loca.TranslateNotification("CHARGE_BLOCKED");
                    UIManager.Instance?.ShowNotification(message);
                }
            }

            // Apply skill to target tile if flag 8 is set
            if ((moveFlags & 8) != 0)
            {
                Skill?.ApplyToTile(targetTile, false);
            }
        }

        public override void OnAdded() { }
        public override void OnRemoved() { }
    }

    // =========================================================================
    // USE SKILL EFFECT
    // =========================================================================

    /// <summary>
    /// UseSkill effect - triggers another skill.
    ///
    /// Factory: UseSkill.Create @ 0x1807217c0
    /// </summary>
    [Serializable]
    public class UseSkill
    {
        // UseSkill template configuration

        /// <summary>
        /// Factory method.
        /// Address: 0x1807217c0
        /// </summary>
        public static UseSkillHandler Create(UseSkill template)
        {
            var handler = new UseSkillHandler();
            handler.EffectData = template;
            return handler;
        }
    }

    /// <summary>
    /// Runtime handler for triggering skills.
    ///
    /// ApplySkill @ 0x180721510
    /// </summary>
    public class UseSkillHandler : SkillEventHandler
    {
        public override void OnAdded() { }
        public override void OnRemoved() { }
    }

    // =========================================================================
    // ENTITY FIELD OFFSETS (Verified)
    // =========================================================================
    // From Entity.UpdateHitpoints @ 0x180615050:
    //
    // +0x20: Elements (List<Element>) - List of element components
    // +0x48: IsDead (bool) - Entity death flag
    // +0x4c: FactionID (int) - Faction this entity belongs to
    // +0x54: CurrentHitpoints (int) - Sum of all element HPs
    // +0x58: MaxHitpoints (int) - Maximum possible HP
    // +0x5c: CurrentArmorDurability (int) - Current armor points
    // +0x60: MaxArmorDurability (int) - Maximum armor points
    // +0x68: ContainedEntity (Entity*) - Passenger inside vehicle
    // +0x70: ContainerEntity (Entity*) - Vehicle containing this entity
    //
    // Element HP: element+0x114 (current), element+0x118 (max)
    // =========================================================================

    // =========================================================================
    // SKILL.APPLYSUPPRESSION (Complex Suppression with Cover)
    // =========================================================================
    // Address: 0x1806d5930
    //
    // This is NOT an "effect" but a core Skill method that handles:
    // - Checking cover direction between source and target
    // - Applying cover mitigation to suppression amount
    // - Handling adjacent tile suppression spread
    // - Uses Config values for suppression ratios
    //
    // This is separate from the ChangeSuppression effect which is simpler.
    // =========================================================================

    // =========================================================================
    // PLACEHOLDER TYPES
    // =========================================================================

    public class Skill
    {
        /// <summary>Offset: +0x10</summary>
        public SkillTemplate Template;

        /// <summary>Offset: +0x18</summary>
        public SkillContainer SkillContainer;

        /// <summary>Calculated effect multiplier. Offset: +0xe0</summary>
        public int EffectMult;

        /// <summary>Display string array. Offset: +0xf8</summary>
        public string[] StringValues;

        public Actor GetActor() => null;
        public Entity GetEntity(int index) => null;
        public void OnEffectApplied() { }
        public void ApplyToTile(Tile tile, bool flag) { }
    }

    public class SkillTemplate { }
    public class SkillContainer
    {
        public void ScheduleUpdate() { }
    }

    public class Entity
    {
        /// <summary>Offset: +0x20</summary>
        public List<Element> Elements;

        /// <summary>Offset: +0x48</summary>
        public bool IsDead;

        /// <summary>Offset: +0x4c</summary>
        public int FactionID;

        /// <summary>Offset: +0x54</summary>
        public int CurrentHitpoints;

        /// <summary>Offset: +0x58</summary>
        public int MaxHitpoints;

        /// <summary>Offset: +0x5c</summary>
        public int CurrentArmorDurability;

        /// <summary>Offset: +0x60</summary>
        public int MaxArmorDurability;

        /// <summary>Offset: +0x68</summary>
        public Entity ContainedEntity;

        /// <summary>Offset: +0x70</summary>
        public Entity ContainerEntity;

        public bool HasOwner => ContainerEntity != null;
        public EntityProperties Properties;

        public void TakeDamage(DamageInfo info, Skill skill) { }
        public void SetArmorDurability(int value) { }
        public float GetArmorDurabilityPct() => 0f;
        public bool AllowsTileEffects() => true;
    }

    public class Element
    {
        /// <summary>Offset: +0x114</summary>
        public int CurrentHP;

        /// <summary>Offset: +0x118</summary>
        public int MaxHP;
    }

    public class Actor : Entity
    {
        public bool HasContainedEntity => ContainedEntity != null;
        public Movement Movement;

        public Tile GetCurrentTile() => null;
        public int GetFacing() => 0;
        public bool MoveToWithSkill(Tile dest, ref int flags, SkillEventHandler handler, int moveType) => false;
        public void ChangeSuppressionAndUpdateAP(int amount) { }
        public bool IsPlayerControlled => false;
    }

    public class Movement
    {
        public object PathFinder;
    }

    public class Tile
    {
        public bool BlocksEffects;

        public Entity GetEntity() => null;
        public int GetDirectionTo(Tile other) => 0;
        public Tile GetNextTile(int direction) => null;
        public int GetDistanceTo(Tile other) => 0;
        public int GetDiagonalManhattanDistanceTo(Tile other) => 0;
        public TileEffectHandler GetEffect(TileEffectTemplate template) => null;
        public void AddEffect(TileEffectHandler effect) { }
    }

    public class TileEffectTemplate
    {
        public TileEffectHandler CreateHandler(float delay) => null;
    }

    public class TileEffectHandler
    {
        public void Refresh() { }
    }

    public class EntityProperties
    {
        public void UpdateProperty(int propertyType, int value) { }
        public void UpdateMultProperty(int propertyType, float value) { }
        public int GetArmor() => 0;
    }

    public class DamageInfo
    {
        /// <summary>Offset: +0x18</summary>
        public int FatalityType;

        /// <summary>Offset: +0x1c</summary>
        public int ElementHitMinIndex;

        /// <summary>Offset: +0x2c</summary>
        public int Damage;

        /// <summary>Offset: +0x34</summary>
        public int ArmorDmgFlat;

        /// <summary>Offset: +0x38</summary>
        public int ArmorDmgPct;

        /// <summary>Offset: +0x3c</summary>
        public int HitCount;

        /// <summary>Offset: +0x40</summary>
        public float ArmorPenetration;

        /// <summary>Offset: +0x44</summary>
        public float ArmorDmgFromElements;

        /// <summary>Offset: +0x4d</summary>
        public bool WasBlocked;

        /// <summary>Offset: +0x4e</summary>
        public bool CanCrit;
    }

    public static class PropertyTypeExtensions
    {
        public static bool IsMultProperty(int propertyType) => false;
    }

    public static class FloatExtensions
    {
        public static float Clamped(float value) => Math.Max(0, Math.Min(1, value));
    }

    public static class DirectionExtensions
    {
        public static int Flip(int direction) => (direction + 4) % 8;
    }

    public static class Loca
    {
        public static string TranslateNotification(string key) => key;
    }

    public class TacticalManager
    {
        public static TacticalManager Instance;
        public PseudoRandom Random;

        public void InvokeOnArmorChanged(Entity entity, float pct, int armor, int param) { }
    }

    public class PseudoRandom
    {
        public bool NextTry(int chance) => chance >= 100;
    }

    public class UIManager
    {
        public static UIManager Instance;
        public void ShowNotification(string message) { }
    }
}

// =============================================================================
// CORRECT FUNCTION ADDRESSES REFERENCE
// =============================================================================
//
// | Function                                    | Address      |
// |---------------------------------------------|--------------|
// | Damage.Create                               | 0x180703500  |
// | DamageHandler.ApplyDamage                   | 0x180702970  |
// | DamageHandler.OnAdded                       | 0x180702d90  |
// | ChangeSuppression.Create                    | 0x1807007a0  |
// | ChangeSuppressionHandler.OnApply            | 0x180700760  |
// | ChangeSuppressionHandler.Apply              | 0x180700600  |
// | ChangeProperty.Create                       | 0x1806ff8e0  |
// | ChangePropertyHandler.Apply                 | 0x1806fee90  |
// | SpawnTileEffect.Create                      | 0x18071e280  |
// | SpawnTileEffectHandler.OnApply              | 0x18071de80  |
// | SpawnTileEffectHandler.Apply_WithProbability| 0x18071dbe0  |
// | RestoreArmorDurability.Create               | 0x18071b140  |
// | RestoreArmorDurabilityHandler.ApplyToEntity | 0x18071aff0  |
// | Charge.Create                               | 0x180701ce0  |
// | ChargeHandler.OnUse                         | 0x180700f40  |
// | UseSkill.Create                             | 0x1807217c0  |
// | UseSkillHandler.ApplySkill                  | 0x180721510  |
// | SkillEventHandler.GetActor                  | 0x1806eef20  |
// | SkillEventHandler.GetEntity                 | 0x1806eef40  |
// | SkillEventHandler.GetOwner                  | 0x1806eef60  |
// | Entity.UpdateHitpoints                      | 0x180615050  |
// | Entity.ApplySuppression                     | 0x18060f8a0  |
// | Skill.ApplySuppression                      | 0x1806d5930  |
// | Element.TeleportTo                          | 0x1805ffec0  |
// =============================================================================
