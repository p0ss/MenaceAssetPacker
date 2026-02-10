// =============================================================================
// MENACE REFERENCE CODE - Skill Effects System
// =============================================================================
// How skill effects are applied to targets. Effects are the core building
// blocks of all abilities - damage, healing, buffs, debuffs, movement, etc.
// =============================================================================

using System;
using System.Collections.Generic;

namespace Menace.Tactical.Skills
{
    // =========================================================================
    // EFFECT TYPES
    // =========================================================================

    /// <summary>
    /// Categories of skill effects.
    /// </summary>
    public enum EffectType
    {
        /// <summary>Deals damage to target</summary>
        Damage = 0,

        /// <summary>Restores hitpoints</summary>
        Heal = 1,

        /// <summary>Applies suppression</summary>
        Suppress = 2,

        /// <summary>Applies a status effect (buff/debuff)</summary>
        ApplyStatus = 3,

        /// <summary>Removes a status effect</summary>
        RemoveStatus = 4,

        /// <summary>Moves the target</summary>
        Move = 5,

        /// <summary>Modifies a property temporarily</summary>
        ModifyProperty = 6,

        /// <summary>Spawns entities or objects</summary>
        Spawn = 7,

        /// <summary>Creates tile effects (fire, smoke, etc)</summary>
        TileEffect = 8,

        /// <summary>Special scripted effect</summary>
        Custom = 99
    }

    /// <summary>
    /// How the effect value is calculated.
    /// </summary>
    public enum EffectValueType
    {
        /// <summary>Fixed value (e.g., 10 damage)</summary>
        Flat = 0,

        /// <summary>Percentage of something (e.g., 50% of max HP)</summary>
        Percent = 1,

        /// <summary>Scales with a property</summary>
        PropertyScaled = 2
    }

    /// <summary>
    /// What the effect targets.
    /// </summary>
    public enum EffectTargetType
    {
        /// <summary>The skill's primary target</summary>
        Target = 0,

        /// <summary>The skill user</summary>
        Self = 1,

        /// <summary>All units in area</summary>
        Area = 2,

        /// <summary>All allies in area</summary>
        AlliesInArea = 3,

        /// <summary>All enemies in area</summary>
        EnemiesInArea = 4,

        /// <summary>The tile itself</summary>
        Tile = 5
    }

    // =========================================================================
    // EFFECT DATA
    // =========================================================================

    /// <summary>
    /// Base class for all skill effect definitions.
    /// Stored on SkillTemplate.Effects list.
    ///
    /// Instance size varies by effect type.
    /// </summary>
    [Serializable]
    public abstract class SkillEffect
    {
        /// <summary>Effect type identifier. Offset: +0x10</summary>
        public EffectType Type;

        /// <summary>What this effect targets. Offset: +0x14</summary>
        public EffectTargetType TargetType;

        /// <summary>Delay before effect applies (turns). Offset: +0x18</summary>
        public int Delay;

        /// <summary>Duration of effect (0 = instant). Offset: +0x1C</summary>
        public int Duration;

        /// <summary>
        /// Apply this effect to a target.
        /// Override in subclasses for specific behavior.
        /// </summary>
        public abstract void Apply(EffectContext context);
    }

    /// <summary>
    /// Context passed to effect application.
    /// Contains all information needed to resolve the effect.
    /// </summary>
    public class EffectContext
    {
        /// <summary>The skill being used. Offset: +0x10</summary>
        public Skill Skill;

        /// <summary>Entity using the skill. Offset: +0x18</summary>
        public Entity Source;

        /// <summary>Primary target entity (may be null). Offset: +0x20</summary>
        public Entity Target;

        /// <summary>Target tile. Offset: +0x28</summary>
        public Tile TargetTile;

        /// <summary>Source tile at time of use. Offset: +0x30</summary>
        public Tile SourceTile;

        /// <summary>Hit result from accuracy check. Offset: +0x38</summary>
        public HitResult HitResult;

        /// <summary>Random instance for deterministic results. Offset: +0x40</summary>
        public System.Random Random;

        /// <summary>All entities affected by AoE. Offset: +0x48</summary>
        public List<Entity> AffectedEntities;
    }

    /// <summary>
    /// Result of a hit check.
    /// </summary>
    public enum HitResult
    {
        Miss = 0,
        Graze = 1,  // Partial hit
        Hit = 2,
        Critical = 3
    }

    // =========================================================================
    // DAMAGE EFFECT
    // =========================================================================

    /// <summary>
    /// Deals damage to target.
    ///
    /// Address: 0x1806a2340 (Apply)
    /// </summary>
    [Serializable]
    public class DamageEffect : SkillEffect
    {
        /// <summary>Base damage value. Offset: +0x20</summary>
        public float BaseDamage;

        /// <summary>How damage is calculated. Offset: +0x24</summary>
        public EffectValueType ValueType;

        /// <summary>Damage type for resistance checks. Offset: +0x28</summary>
        public DamageType DamageType;

        /// <summary>Property to scale with (if PropertyScaled). Offset: +0x30</summary>
        public string ScaleProperty;

        /// <summary>Scale multiplier. Offset: +0x38</summary>
        public float ScaleMultiplier = 1f;

        /// <summary>Can this effect crit? Offset: +0x3C</summary>
        public bool CanCrit = true;

        /// <summary>Armor penetration override (-1 = use weapon). Offset: +0x40</summary>
        public float ArmorPenetration = -1f;

        public override void Apply(EffectContext context)
        {
            if (context.Target == null || !context.Target.IsAlive)
                return;

            // Calculate base damage
            float damage = CalculateDamage(context);

            // Apply hit result modifiers
            switch (context.HitResult)
            {
                case HitResult.Miss:
                    return;  // No damage on miss

                case HitResult.Graze:
                    damage *= 0.5f;  // Half damage
                    break;

                case HitResult.Critical:
                    if (CanCrit)
                    {
                        float critMult = context.Source.GetEffectiveCritMult();
                        damage *= critMult;
                    }
                    break;
            }

            // Create damage info
            var damageInfo = new DamageInfo
            {
                Damage = damage,
                Type = DamageType,
                Source = context.Source,
                Skill = context.Skill,
                ArmorPenetration = ArmorPenetration >= 0
                    ? ArmorPenetration
                    : context.Source.GetEffectiveArmorPen(),
                IsCrit = context.HitResult == HitResult.Critical
            };

            // Apply through damage handler
            DamageHandler.ApplyDamage(context.Target, damageInfo);
        }

        private float CalculateDamage(EffectContext context)
        {
            switch (ValueType)
            {
                case EffectValueType.Flat:
                    return BaseDamage;

                case EffectValueType.Percent:
                    // Percentage of target's max HP
                    return context.Target.HitpointsMax * (BaseDamage / 100f);

                case EffectValueType.PropertyScaled:
                    // Scale with source property
                    float propValue = context.Source.Properties.GetProperty(ScaleProperty);
                    return BaseDamage + (propValue * ScaleMultiplier);

                default:
                    return BaseDamage;
            }
        }
    }

    /// <summary>
    /// Damage types for resistance calculation.
    /// </summary>
    public enum DamageType
    {
        Kinetic = 0,    // Bullets, melee
        Explosive = 1,  // Grenades, rockets
        Energy = 2,     // Lasers, plasma
        Fire = 3,       // Incendiary
        Poison = 4,     // Toxic, chemical
        Psychic = 5     // Mental attacks
    }

    // =========================================================================
    // HEAL EFFECT
    // =========================================================================

    /// <summary>
    /// Restores hitpoints to target.
    ///
    /// Address: 0x1806a2890 (Apply)
    /// </summary>
    [Serializable]
    public class HealEffect : SkillEffect
    {
        /// <summary>Base heal amount. Offset: +0x20</summary>
        public float BaseHeal;

        /// <summary>How heal is calculated. Offset: +0x24</summary>
        public EffectValueType ValueType;

        /// <summary>Can overheal above max HP? Offset: +0x28</summary>
        public bool CanOverheal;

        /// <summary>Max overheal percentage (1.2 = 120% max HP). Offset: +0x2C</summary>
        public float MaxOverhealPercent = 1f;

        public override void Apply(EffectContext context)
        {
            Entity target = TargetType == EffectTargetType.Self
                ? context.Source
                : context.Target;

            if (target == null || !target.IsAlive)
                return;

            float healAmount = CalculateHeal(context, target);

            // Apply heal
            target.Hitpoints += (int)healAmount;

            // Cap at max (or overheal cap)
            int maxHp = CanOverheal
                ? (int)(target.HitpointsMax * MaxOverhealPercent)
                : target.HitpointsMax;

            if (target.Hitpoints > maxHp)
                target.Hitpoints = maxHp;
        }

        private float CalculateHeal(EffectContext context, Entity target)
        {
            switch (ValueType)
            {
                case EffectValueType.Flat:
                    return BaseHeal;

                case EffectValueType.Percent:
                    return target.HitpointsMax * (BaseHeal / 100f);

                default:
                    return BaseHeal;
            }
        }
    }

    // =========================================================================
    // SUPPRESS EFFECT
    // =========================================================================

    /// <summary>
    /// Applies suppression to target.
    ///
    /// Address: 0x1806a2b10 (Apply)
    /// </summary>
    [Serializable]
    public class SuppressEffect : SkillEffect
    {
        /// <summary>Base suppression amount. Offset: +0x20</summary>
        public float BaseSuppression;

        /// <summary>Scales with distance? Offset: +0x24</summary>
        public bool ScaleWithDistance;

        /// <summary>Distance falloff start. Offset: +0x28</summary>
        public float FalloffStart = 5f;

        /// <summary>Distance falloff end (0 suppression). Offset: +0x2C</summary>
        public float FalloffEnd = 15f;

        public override void Apply(EffectContext context)
        {
            if (context.Target == null || !context.Target.IsAlive)
                return;

            float suppression = BaseSuppression;

            // Apply distance falloff
            if (ScaleWithDistance)
            {
                float distance = context.SourceTile.GetDistanceTo(context.TargetTile);
                if (distance > FalloffStart)
                {
                    float falloffRange = FalloffEnd - FalloffStart;
                    float falloffProgress = (distance - FalloffStart) / falloffRange;
                    suppression *= 1f - Math.Min(falloffProgress, 1f);
                }
            }

            // Apply through suppression system
            context.Target.ApplySuppression(suppression, context.Source);
        }
    }

    // =========================================================================
    // STATUS EFFECT
    // =========================================================================

    /// <summary>
    /// Applies a status effect (buff/debuff) to target.
    ///
    /// Address: 0x1806a2d80 (Apply)
    /// </summary>
    [Serializable]
    public class ApplyStatusEffect : SkillEffect
    {
        /// <summary>Status to apply. Offset: +0x20</summary>
        public StatusTemplate Status;

        /// <summary>Override duration (-1 = use status default). Offset: +0x28</summary>
        public int DurationOverride = -1;

        /// <summary>Stack count to apply. Offset: +0x2C</summary>
        public int Stacks = 1;

        /// <summary>Chance to apply (0-100). Offset: +0x30</summary>
        public float ApplyChance = 100f;

        public override void Apply(EffectContext context)
        {
            Entity target = TargetType == EffectTargetType.Self
                ? context.Source
                : context.Target;

            if (target == null || !target.IsAlive)
                return;

            // Check apply chance
            if (ApplyChance < 100f)
            {
                float roll = (float)(context.Random.NextDouble() * 100);
                if (roll > ApplyChance)
                    return;  // Failed to apply
            }

            // Check target resistance
            float resistance = target.GetStatusResistance(Status.StatusType);
            if (resistance > 0)
            {
                float resistRoll = (float)(context.Random.NextDouble() * 100);
                if (resistRoll < resistance)
                    return;  // Resisted
            }

            // Apply status
            int duration = DurationOverride >= 0 ? DurationOverride : Status.DefaultDuration;
            target.StatusContainer.AddStatus(Status, duration, Stacks, context.Source);
        }
    }

    /// <summary>
    /// Status effect template (ScriptableObject).
    /// </summary>
    [Serializable]
    public class StatusTemplate
    {
        /// <summary>Unique identifier. Offset: +0x10</summary>
        public string StatusId;

        /// <summary>Display name. Offset: +0x18</summary>
        public string DisplayName;

        /// <summary>Status category. Offset: +0x20</summary>
        public StatusType StatusType;

        /// <summary>Default duration in turns. Offset: +0x24</summary>
        public int DefaultDuration;

        /// <summary>Maximum stacks. Offset: +0x28</summary>
        public int MaxStacks = 1;

        /// <summary>Property modifiers while active. Offset: +0x30</summary>
        public List<PropertyModifier> Modifiers;

        /// <summary>Effects applied each turn. Offset: +0x38</summary>
        public List<SkillEffect> TurnEffects;

        /// <summary>Is this a beneficial effect? Offset: +0x40</summary>
        public bool IsBuff;
    }

    public enum StatusType
    {
        Buff = 0,
        Debuff = 1,
        Poison = 2,
        Stun = 3,
        Root = 4,
        Bleed = 5,
        Burn = 6,
        Fear = 7
    }

    /// <summary>
    /// Modifies a property while status is active.
    /// </summary>
    [Serializable]
    public class PropertyModifier
    {
        /// <summary>Property to modify. Offset: +0x10</summary>
        public string PropertyName;

        /// <summary>Modification type. Offset: +0x18</summary>
        public ModifierType Type;

        /// <summary>Value per stack. Offset: +0x1C</summary>
        public float ValuePerStack;
    }

    public enum ModifierType
    {
        Flat = 0,       // +10
        Percent = 1,    // +10%
        Multiply = 2    // *1.1
    }

    // =========================================================================
    // MOVEMENT EFFECT
    // =========================================================================

    /// <summary>
    /// Moves the target to a new position.
    ///
    /// Address: 0x1806a3210 (Apply)
    /// </summary>
    [Serializable]
    public class MoveEffect : SkillEffect
    {
        /// <summary>Movement type. Offset: +0x20</summary>
        public MoveType MoveType;

        /// <summary>Distance to move. Offset: +0x24</summary>
        public int Distance;

        /// <summary>Ignores collision? Offset: +0x28</summary>
        public bool IgnoreCollision;

        /// <summary>Deals damage on collision? Offset: +0x29</summary>
        public bool CollisionDamage;

        /// <summary>Collision damage amount. Offset: +0x2C</summary>
        public float CollisionDamageAmount;

        public override void Apply(EffectContext context)
        {
            Entity target = TargetType == EffectTargetType.Self
                ? context.Source
                : context.Target;

            if (target == null || !target.IsAlive)
                return;

            Tile destination = CalculateDestination(context, target);
            if (destination == null)
                return;

            // Check for collision
            if (!IgnoreCollision)
            {
                var collisionResult = CheckCollision(target.CurrentTile, destination);
                if (collisionResult.Collided)
                {
                    destination = collisionResult.StopTile;
                    if (CollisionDamage)
                    {
                        ApplyCollisionDamage(target, collisionResult);
                    }
                }
            }

            // Move target
            target.TeleportTo(destination);
        }

        private Tile CalculateDestination(EffectContext context, Entity target)
        {
            switch (MoveType)
            {
                case MoveType.Push:
                    // Away from source
                    int direction = context.SourceTile.GetDirectionTo(target.CurrentTile);
                    return target.CurrentTile.GetTileInDirection(direction, Distance);

                case MoveType.Pull:
                    // Toward source
                    int pullDir = target.CurrentTile.GetDirectionTo(context.SourceTile);
                    return target.CurrentTile.GetTileInDirection(pullDir, Distance);

                case MoveType.Teleport:
                    // To target tile
                    return context.TargetTile;

                default:
                    return null;
            }
        }

        private CollisionResult CheckCollision(Tile from, Tile to)
        {
            // Check each tile along path for obstacles
            // Returns first blocking tile
            return new CollisionResult { Collided = false, StopTile = to };
        }

        private void ApplyCollisionDamage(Entity target, CollisionResult collision)
        {
            var damageInfo = new DamageInfo
            {
                Damage = CollisionDamageAmount,
                Type = DamageType.Kinetic
            };
            DamageHandler.ApplyDamage(target, damageInfo);
        }
    }

    public enum MoveType
    {
        Push = 0,
        Pull = 1,
        Teleport = 2
    }

    public class CollisionResult
    {
        public bool Collided;
        public Tile StopTile;
        public Entity CollidedWith;
    }

    // =========================================================================
    // TILE EFFECT
    // =========================================================================

    /// <summary>
    /// Creates a tile effect at target location.
    ///
    /// Address: 0x1806a3680 (Apply)
    /// </summary>
    [Serializable]
    public class CreateTileEffect : SkillEffect
    {
        /// <summary>Tile effect to create. Offset: +0x20</summary>
        public TileEffectTemplate TileEffectTemplate;

        /// <summary>Radius of effect. Offset: +0x28</summary>
        public int Radius;

        /// <summary>Duration in turns. Offset: +0x2C</summary>
        public int EffectDuration;

        public override void Apply(EffectContext context)
        {
            var tiles = GetTilesInRadius(context.TargetTile, Radius);

            foreach (var tile in tiles)
            {
                tile.AddTileEffect(TileEffectTemplate, EffectDuration, context.Source);
            }
        }

        private List<Tile> GetTilesInRadius(Tile center, int radius)
        {
            // Returns all tiles within radius
            var result = new List<Tile>();
            // Implementation...
            return result;
        }
    }

    // =========================================================================
    // EFFECT PROCESSOR
    // =========================================================================

    /// <summary>
    /// Processes skill effects in correct order.
    ///
    /// Address: 0x1806a1200 (ProcessEffects)
    /// </summary>
    public static class EffectProcessor
    {
        /// <summary>
        /// Applies all effects from a skill.
        ///
        /// Address: 0x1806a1200
        /// </summary>
        public static void ProcessEffects(Skill skill, EffectContext context)
        {
            var effects = skill.Template.Effects;
            if (effects == null || effects.Count == 0)
                return;

            // Group effects by delay
            var immediateEffects = new List<SkillEffect>();
            var delayedEffects = new List<SkillEffect>();

            foreach (var effect in effects)
            {
                if (effect.Delay == 0)
                    immediateEffects.Add(effect);
                else
                    delayedEffects.Add(effect);
            }

            // Apply immediate effects in order
            foreach (var effect in immediateEffects)
            {
                ApplyEffect(effect, context);
            }

            // Schedule delayed effects
            foreach (var effect in delayedEffects)
            {
                ScheduleDelayedEffect(effect, context, effect.Delay);
            }
        }

        private static void ApplyEffect(SkillEffect effect, EffectContext context)
        {
            // Determine actual targets based on TargetType
            switch (effect.TargetType)
            {
                case EffectTargetType.Target:
                case EffectTargetType.Self:
                    effect.Apply(context);
                    break;

                case EffectTargetType.Area:
                case EffectTargetType.AlliesInArea:
                case EffectTargetType.EnemiesInArea:
                    ApplyToArea(effect, context);
                    break;

                case EffectTargetType.Tile:
                    effect.Apply(context);
                    break;
            }
        }

        private static void ApplyToArea(SkillEffect effect, EffectContext context)
        {
            if (context.AffectedEntities == null)
                return;

            foreach (var entity in context.AffectedEntities)
            {
                // Filter by effect target type
                bool shouldApply = effect.TargetType switch
                {
                    EffectTargetType.Area => true,
                    EffectTargetType.AlliesInArea => entity.Faction == context.Source.Faction,
                    EffectTargetType.EnemiesInArea => entity.Faction != context.Source.Faction,
                    _ => false
                };

                if (shouldApply)
                {
                    // Create per-target context
                    var targetContext = new EffectContext
                    {
                        Skill = context.Skill,
                        Source = context.Source,
                        Target = entity,
                        TargetTile = entity.CurrentTile,
                        SourceTile = context.SourceTile,
                        HitResult = context.HitResult,
                        Random = context.Random
                    };

                    effect.Apply(targetContext);
                }
            }
        }

        private static void ScheduleDelayedEffect(SkillEffect effect, EffectContext context, int delay)
        {
            // Add to turn-based scheduler
            TurnScheduler.Instance.Schedule(delay, () =>
            {
                ApplyEffect(effect, context);
            });
        }
    }

    // =========================================================================
    // PLACEHOLDER TYPES
    // =========================================================================

    // These are referenced but defined elsewhere

    public class Skill
    {
        public SkillTemplate Template;
        public Entity GetActor() => null;
    }

    public class Entity
    {
        public int Hitpoints;
        public int HitpointsMax;
        public bool IsAlive => Hitpoints > 0;
        public Tile CurrentTile;
        public Faction Faction;
        public EntityProperties Properties;
        public StatusContainer StatusContainer;

        public float GetEffectiveCritMult() => 1.5f;
        public float GetEffectiveArmorPen() => 0f;
        public float GetStatusResistance(StatusType type) => 0f;
        public void ApplySuppression(float amount, Entity source) { }
        public void TeleportTo(Tile tile) { }
    }

    public class Tile
    {
        public float GetDistanceTo(Tile other) => 0f;
        public int GetDirectionTo(Tile other) => 0;
        public Tile GetTileInDirection(int direction, int distance) => null;
        public void AddTileEffect(TileEffectTemplate template, int duration, Entity source) { }
    }

    public class Faction { }

    public class StatusContainer
    {
        public void AddStatus(StatusTemplate template, int duration, int stacks, Entity source) { }
    }

    public class TileEffectTemplate { }

    public class TurnScheduler
    {
        public static TurnScheduler Instance;
        public void Schedule(int delay, Action action) { }
    }

    public class DamageInfo
    {
        public float Damage;
        public DamageType Type;
        public Entity Source;
        public Skill Skill;
        public float ArmorPenetration;
        public bool IsCrit;
    }

    public static class DamageHandler
    {
        public static void ApplyDamage(Entity target, DamageInfo info) { }
    }
}
