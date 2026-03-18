using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Menace.SDK.Entities;

/// <summary>
/// Object-oriented wrapper for an Actor/Entity instance.
///
/// Provides clean access to entity state, combat, skills, and the effect system.
/// Wraps existing static SDK modules (EntityCombat, EntitySkills, EntityState, etc.)
/// with an object-oriented API.
///
/// Usage:
///   var actor = new Actor(actorPtr);
///   actor.AddEffect("concealment", -3, 1);
///   var skills = actor.Skills;
///   actor.Attack(target);
///
/// The Actor class integrates with EffectSystem for temporary modifiers.
/// </summary>
public class Actor
{
    // Known offsets for Actor fields (fallback if schema not loaded)
    private const int OFFSET_FACTION_ID = 0xBC;
    private const int OFFSET_ENTITY_PROPERTIES = 0xC0;

    private readonly GameObj _gameObj;

    // Cached schema offsets
    private static int? _schemaOffsetFactionId;
    private static int? _schemaOffsetProperties;
    private static bool _schemaChecked;

    /// <summary>
    /// Create an Actor wrapper from a pointer.
    /// </summary>
    public Actor(IntPtr pointer)
    {
        _gameObj = new GameObj(pointer);
    }

    /// <summary>
    /// Create an Actor wrapper from a pointer value (for Lua interop).
    /// </summary>
    public Actor(long pointerValue) : this(new IntPtr(pointerValue))
    {
    }

    /// <summary>
    /// Create an Actor wrapper from a GameObj.
    /// </summary>
    public Actor(GameObj gameObj)
    {
        _gameObj = gameObj;
    }

    /// <summary>
    /// The raw pointer to the Actor instance.
    /// </summary>
    public IntPtr Pointer => _gameObj.Pointer;

    /// <summary>
    /// The GameObj wrapper for this actor.
    /// </summary>
    public GameObj GameObj => _gameObj;

    /// <summary>
    /// Check if this actor wrapper points to a valid actor.
    /// </summary>
    public bool IsValid => !_gameObj.IsNull;

    /// <summary>
    /// Get the actor's name via GameObj.
    /// </summary>
    public string Name
    {
        get
        {
            if (!IsValid) return "<invalid>";
            try
            {
                return _gameObj.GetName() ?? "<unnamed>";
            }
            catch
            {
                return "<error>";
            }
        }
    }

    /// <summary>
    /// Get the actor's faction ID.
    /// </summary>
    public int FactionId
    {
        get
        {
            if (!IsValid) return -1;
            try
            {
                var offset = GetOffsetFactionId();
                return Marshal.ReadInt32(Pointer + offset);
            }
            catch
            {
                return -1;
            }
        }
    }

    /// <summary>
    /// Get the actor's faction type.
    /// </summary>
    public FactionType Faction => (FactionType)FactionId;

    /// <summary>
    /// Get the actor's faction name.
    /// </summary>
    public string FactionName => TacticalController.GetFactionName(Faction);

    // ═══════════════════════════════════════════════════════════════════
    //  Effect System Integration
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Add a temporary effect to this actor.
    /// </summary>
    /// <param name="property">The property being modified (e.g., "concealment", "accuracy")</param>
    /// <param name="modifier">The modifier value (positive or negative)</param>
    /// <param name="rounds">How many rounds until expiry (1 = expires at end of current round)</param>
    /// <param name="source">Optional source identifier for debugging/tracking</param>
    public void AddEffect(string property, int modifier, int rounds, string source = "")
    {
        if (!IsValid) return;
        EffectSystem.AddEffect(Pointer, property, modifier, rounds, source);
    }

    /// <summary>
    /// Get the total modifier for a property from all active effects.
    /// </summary>
    public int GetEffectModifier(string property)
    {
        if (!IsValid) return 0;
        return EffectSystem.GetModifier(Pointer, property);
    }

    /// <summary>
    /// Check if this actor has any active effects on a property.
    /// </summary>
    public bool HasEffect(string property)
    {
        if (!IsValid) return false;
        return EffectSystem.HasEffect(Pointer, property);
    }

    /// <summary>
    /// Get all active effects on this actor.
    /// </summary>
    public List<EffectSystem.Effect> Effects
    {
        get
        {
            if (!IsValid) return new List<EffectSystem.Effect>();
            return EffectSystem.GetEffects(Pointer);
        }
    }

    /// <summary>
    /// Clear all effects from this actor.
    /// </summary>
    public void ClearEffects()
    {
        if (!IsValid) return;
        EffectSystem.ClearEffects(Pointer);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Combat (wraps EntityCombat)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Make this actor attack the target.
    /// </summary>
    public EntityCombat.CombatResult Attack(Actor target)
    {
        if (!IsValid || target == null || !target.IsValid)
            return EntityCombat.CombatResult.Failed("Invalid actor or target");
        return EntityCombat.Attack(_gameObj, target._gameObj);
    }

    /// <summary>
    /// Use a skill/ability by name on an optional target.
    /// </summary>
    public EntityCombat.CombatResult UseAbility(string skillName, Actor target = null)
    {
        if (!IsValid)
            return EntityCombat.CombatResult.Failed("Invalid actor");
        return EntityCombat.UseAbility(_gameObj, skillName, target?._gameObj ?? GameObj.Null);
    }

    /// <summary>
    /// Apply damage to this actor.
    /// </summary>
    public bool ApplyDamage(int damage)
    {
        if (!IsValid) return false;
        return EntityCombat.ApplyDamage(_gameObj, damage);
    }

    /// <summary>
    /// Heal this actor.
    /// </summary>
    public bool Heal(int amount)
    {
        if (!IsValid) return false;
        return EntityCombat.Heal(_gameObj, amount);
    }

    /// <summary>
    /// Apply suppression to this actor.
    /// </summary>
    public bool ApplySuppression(float amount)
    {
        if (!IsValid) return false;
        return EntityCombat.ApplySuppression(_gameObj, amount);
    }

    /// <summary>
    /// Get current suppression level.
    /// </summary>
    public float Suppression => IsValid ? EntityCombat.GetSuppression(_gameObj) : 0f;

    /// <summary>
    /// Get current morale.
    /// </summary>
    public float Morale => IsValid ? EntityCombat.GetMorale(_gameObj) : 0f;

    /// <summary>
    /// Get combat info for this actor.
    /// </summary>
    public EntityCombat.CombatInfo CombatInfo => IsValid ? EntityCombat.GetCombatInfo(_gameObj) : default;

    /// <summary>
    /// Get all skills available to this actor as SkillInfo.
    /// </summary>
    public List<EntityCombat.SkillInfo> SkillInfos => IsValid ? EntityCombat.GetSkills(_gameObj) : new List<EntityCombat.SkillInfo>();

    // ═══════════════════════════════════════════════════════════════════
    //  State (wraps EntityState)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get state flags (heavy weapon deployed, detection mask, etc.).
    /// </summary>
    public EntityState.StateFlags StateFlags => IsValid ? EntityState.GetStateFlags(_gameObj) : default;

    // ═══════════════════════════════════════════════════════════════════
    //  Movement (wraps EntityMovement)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Move this actor to coordinates.
    /// </summary>
    public EntityMovement.MoveResult MoveTo(int x, int y)
    {
        if (!IsValid) return EntityMovement.MoveResult.Failed("Invalid actor");
        return EntityMovement.MoveTo(_gameObj, x, y);
    }

    /// <summary>
    /// Teleport this actor to coordinates.
    /// </summary>
    public EntityMovement.MoveResult Teleport(int x, int y)
    {
        if (!IsValid) return EntityMovement.MoveResult.Failed("Invalid actor");
        return EntityMovement.Teleport(_gameObj, x, y);
    }

    /// <summary>
    /// Get the actor's current position.
    /// </summary>
    public (int x, int y)? Position => IsValid ? EntityMovement.GetPosition(_gameObj) : null;

    /// <summary>
    /// Check if the actor is currently moving.
    /// </summary>
    public bool IsMoving => IsValid && EntityMovement.IsMoving(_gameObj);

    /// <summary>
    /// Get current action points.
    /// </summary>
    public int ActionPoints => IsValid ? EntityMovement.GetRemainingAP(_gameObj) : 0;

    /// <summary>
    /// Get current hitpoints.
    /// </summary>
    public int Hitpoints => CombatInfo.CurrentHP;

    /// <summary>
    /// Get maximum hitpoints.
    /// </summary>
    public int MaxHitpoints => CombatInfo.MaxHP;

    /// <summary>
    /// Kill this actor (set HP to 0).
    /// </summary>
    public bool Kill()
    {
        if (!IsValid) return false;
        return EntityCombat.Kill(_gameObj);
    }

    /// <summary>
    /// Get the movement range for this actor.
    /// </summary>
    public List<(int x, int y)> GetMovementRange() => IsValid ? EntityMovement.GetMovementRange(_gameObj) : new List<(int, int)>();

    // ═══════════════════════════════════════════════════════════════════
    //  Skills (wraps EntitySkills)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all skill IDs for this actor.
    /// </summary>
    public List<string> SkillIDs => IsValid ? EntitySkills.GetSkillIDs(_gameObj) : new List<string>();

    /// <summary>
    /// Check if this actor has a specific skill.
    /// </summary>
    public bool HasSkill(string skillID) => IsValid && EntitySkills.HasSkill(_gameObj, skillID);

    /// <summary>
    /// Add a skill to this actor.
    /// </summary>
    public bool AddSkill(string skillTemplateID) => IsValid && EntitySkills.AddSkill(_gameObj, skillTemplateID);

    /// <summary>
    /// Remove a skill from this actor.
    /// </summary>
    public bool RemoveSkill(string skillID) => IsValid && EntitySkills.RemoveSkill(_gameObj, skillID);

    /// <summary>
    /// Get the state of a specific skill.
    /// </summary>
    public EntitySkills.SkillStateInfo GetSkillState(string skillID) => IsValid ? EntitySkills.GetSkillState(_gameObj, skillID) : default;

    // ═══════════════════════════════════════════════════════════════════
    //  Offset Resolution (schema-first with fallback)
    // ═══════════════════════════════════════════════════════════════════

    private static void EnsureSchemaChecked()
    {
        if (_schemaChecked) return;
        _schemaChecked = true;

        if (!TemplateSchema.IsInitialized) return;

        // Try to get offsets from schema
        if (TemplateSchema.TryGetOffset("Actor", "FactionId", out var fOff))
            _schemaOffsetFactionId = fOff;
        else if (TemplateSchema.TryGetOffset("Entity", "FactionId", out var eFOff))
            _schemaOffsetFactionId = eFOff;

        if (TemplateSchema.TryGetOffset("Actor", "EntityProperties", out var pOff))
            _schemaOffsetProperties = pOff;
        else if (TemplateSchema.TryGetOffset("Entity", "EntityProperties", out var ePOff))
            _schemaOffsetProperties = ePOff;
    }

    private static int GetOffsetFactionId()
    {
        EnsureSchemaChecked();
        return _schemaOffsetFactionId ?? OFFSET_FACTION_ID;
    }

    private static int GetOffsetProperties()
    {
        EnsureSchemaChecked();
        return _schemaOffsetProperties ?? OFFSET_ENTITY_PROPERTIES;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Static Factory / Cache
    // ═══════════════════════════════════════════════════════════════════

    // Cache of Actor wrappers by pointer for reuse
    private static readonly Dictionary<long, WeakReference<Actor>> _cache = new();

    /// <summary>
    /// Get or create an Actor wrapper for the given pointer.
    /// Reuses existing wrappers when possible.
    /// </summary>
    public static Actor Get(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero) return null;

        var key = pointer.ToInt64();

        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var weakRef))
            {
                if (weakRef.TryGetTarget(out var existing))
                    return existing;
                _cache.Remove(key);
            }

            var actor = new Actor(pointer);
            _cache[key] = new WeakReference<Actor>(actor);
            return actor;
        }
    }

    /// <summary>
    /// Get or create an Actor wrapper from a pointer value.
    /// </summary>
    public static Actor Get(long pointerValue)
    {
        return Get(new IntPtr(pointerValue));
    }

    public override string ToString()
    {
        return $"Actor({Name}, Faction={FactionName})";
    }

    public override bool Equals(object obj)
    {
        return obj is Actor other && other.Pointer == Pointer;
    }

    public override int GetHashCode()
    {
        return Pointer.GetHashCode();
    }

    public static bool operator ==(Actor a, Actor b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return a.Pointer == b.Pointer;
    }

    public static bool operator !=(Actor a, Actor b) => !(a == b);
}
