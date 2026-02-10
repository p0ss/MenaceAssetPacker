using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// SDK extension for entity combat actions including attacks, abilities, and AI control.
///
/// Based on reverse engineering findings:
/// - Actor.AimAt(Vector3 target, bool playSound, Entity targetEntity) @ 0x1805db6b0
/// - Actor.ApplySuppression(float amount, ...) @ 0x1805ddda0
/// - Actor.ApplyMorale(MoraleEventType, float) @ 0x1805dd240
/// - Actor.SetSuppression(float) @ 0x1805e76d0
/// - Actor.SetMorale(float) @ 0x1805e6d90
/// - Skill.Use(target) for abilities
/// - TacticalState.TrySelectSkill(Skill) @ 0x18064b3d0
/// </summary>
public static class EntityCombat
{
    // Cached types
    private static GameType _actorType;
    private static GameType _skillType;
    private static GameType _skillContainerType;
    private static GameType _tacticalStateType;
    private static GameType _tacticalManagerType;

    // Field offsets from actor-system.md
    private const uint OFFSET_ACTOR_SUPPRESSION = 0x15C;
    private const uint OFFSET_ACTOR_MORALE = 0x160;
    private const uint OFFSET_ACTOR_IS_TURN_DONE = 0x164;
    private const uint OFFSET_ACTOR_IS_STUNNED = 0x16C;
    private const uint OFFSET_ACTOR_HAS_ACTED = 0x171;
    private const uint OFFSET_ACTOR_TIMES_ATTACKED = 0x140;
    private const uint OFFSET_ACTOR_CURRENT_AP = 0x148;

    // Entity offsets
    private const uint OFFSET_ENTITY_CURRENT_HP = 0x50;
    private const uint OFFSET_ENTITY_MAX_HP = 0x58;
    private const uint OFFSET_ENTITY_IS_ALIVE = 0x48;

    // Suppression state thresholds
    public const float SUPPRESSION_THRESHOLD = 0.33f;
    public const float PINNED_THRESHOLD = 0.66f;
    public const float MAX_SUPPRESSION = 100.0f;

    /// <summary>
    /// Combat action result.
    /// </summary>
    public class CombatResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public int Damage { get; set; }
        public string SkillUsed { get; set; }

        public static CombatResult Failed(string error) => new() { Success = false, Error = error };
        public static CombatResult Ok(string skill = null, int damage = 0) =>
            new() { Success = true, SkillUsed = skill, Damage = damage };
    }

    /// <summary>
    /// Have an actor attack a target using their primary weapon/skill.
    /// </summary>
    public static CombatResult Attack(GameObj attacker, GameObj target)
    {
        if (attacker.IsNull || target.IsNull)
            return CombatResult.Failed("Invalid attacker or target");

        if (!attacker.IsAlive)
            return CombatResult.Failed("Attacker is dead");

        if (!target.IsAlive)
            return CombatResult.Failed("Target is dead");

        try
        {
            EnsureTypesLoaded();

            // Get skills and find primary attack skill
            var skills = GetSkills(attacker);
            var attackSkill = skills.Find(s => s.IsAttack);

            if (attackSkill == null)
                return CombatResult.Failed("No attack skill found");

            return UseAbility(attacker, attackSkill.Name, target);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityCombat.Attack", "Failed", ex);
            return CombatResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Use a specific ability/skill on a target.
    /// </summary>
    public static CombatResult UseAbility(GameObj actor, string skillName, GameObj target = default)
    {
        if (actor.IsNull)
            return CombatResult.Failed("Invalid actor");

        try
        {
            EnsureTypesLoaded();

            var actorType = _actorType?.ManagedType;
            if (actorType == null)
                return CombatResult.Failed("Actor type not available");

            var actorProxy = GetManagedProxy(actor, actorType);
            if (actorProxy == null)
                return CombatResult.Failed("Failed to get actor proxy");

            // Get SkillContainer from actor
            var skillContainerProp = actorType.GetProperty("SkillContainer",
                BindingFlags.Public | BindingFlags.Instance);
            if (skillContainerProp == null)
                return CombatResult.Failed("SkillContainer not found");

            var skillContainer = skillContainerProp.GetValue(actorProxy);
            if (skillContainer == null)
                return CombatResult.Failed("Actor has no SkillContainer");

            // Find the skill by name
            var getSkillMethod = skillContainer.GetType().GetMethod("GetSkill",
                BindingFlags.Public | BindingFlags.Instance);

            object skill = null;
            if (getSkillMethod != null)
            {
                skill = getSkillMethod.Invoke(skillContainer, new object[] { skillName });
            }
            else
            {
                // Try finding via Skills list
                var skillsProp = skillContainer.GetType().GetProperty("Skills",
                    BindingFlags.Public | BindingFlags.Instance);
                if (skillsProp != null)
                {
                    var skillsList = skillsProp.GetValue(skillContainer);
                    if (skillsList != null)
                    {
                        var enumerator = skillsList.GetType().GetMethod("GetEnumerator")?.Invoke(skillsList, null);
                        if (enumerator != null)
                        {
                            var moveNext = enumerator.GetType().GetMethod("MoveNext");
                            var current = enumerator.GetType().GetProperty("Current");

                            while ((bool)moveNext.Invoke(enumerator, null))
                            {
                                var s = current.GetValue(enumerator);
                                if (s != null)
                                {
                                    var nameProp = s.GetType().GetProperty("Name",
                                        BindingFlags.Public | BindingFlags.Instance);
                                    var name = nameProp?.GetValue(s)?.ToString();
                                    if (name == skillName)
                                    {
                                        skill = s;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (skill == null)
                return CombatResult.Failed($"Skill '{skillName}' not found");

            // Check if skill can be used
            var canUseMethod = skill.GetType().GetMethod("CanUse", BindingFlags.Public | BindingFlags.Instance);
            if (canUseMethod != null)
            {
                var canUse = (bool)canUseMethod.Invoke(skill, null);
                if (!canUse)
                    return CombatResult.Failed($"Skill '{skillName}' cannot be used (on cooldown or no AP)");
            }

            // Get target tile if target is provided
            object targetTile = null;
            if (!target.IsNull)
            {
                var tileType = GameType.Find("Menace.Tactical.Tile")?.ManagedType;
                var targetActorProxy = GetManagedProxy(target, actorType);
                if (targetActorProxy != null)
                {
                    var getTileMethod = actorType.GetMethod("GetTile", BindingFlags.Public | BindingFlags.Instance);
                    targetTile = getTileMethod?.Invoke(targetActorProxy, null);
                }
            }

            // Use the skill
            var useMethod = skill.GetType().GetMethod("Use", BindingFlags.Public | BindingFlags.Instance);
            if (useMethod == null)
                return CombatResult.Failed("Skill.Use method not found");

            useMethod.Invoke(skill, new[] { targetTile });

            ModError.Info("Menace.SDK", $"Used skill '{skillName}'");
            return CombatResult.Ok(skillName);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityCombat.UseAbility", $"Failed to use {skillName}", ex);
            return CombatResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all abilities/skills for an actor.
    /// </summary>
    public static List<SkillInfo> GetSkills(GameObj actor)
    {
        var result = new List<SkillInfo>();

        if (actor.IsNull)
            return result;

        try
        {
            EnsureTypesLoaded();

            var actorType = _actorType?.ManagedType;
            if (actorType == null)
                return result;

            var actorProxy = GetManagedProxy(actor, actorType);
            if (actorProxy == null)
                return result;

            var skillContainerProp = actorType.GetProperty("SkillContainer",
                BindingFlags.Public | BindingFlags.Instance);
            if (skillContainerProp == null)
                return result;

            var skillContainer = skillContainerProp.GetValue(actorProxy);
            if (skillContainer == null)
                return result;

            var skillsProp = skillContainer.GetType().GetProperty("Skills",
                BindingFlags.Public | BindingFlags.Instance);
            if (skillsProp == null)
                return result;

            var skillsList = skillsProp.GetValue(skillContainer);
            if (skillsList == null)
                return result;

            var enumerator = skillsList.GetType().GetMethod("GetEnumerator")?.Invoke(skillsList, null);
            if (enumerator == null)
                return result;

            var moveNext = enumerator.GetType().GetMethod("MoveNext");
            var current = enumerator.GetType().GetProperty("Current");

            while ((bool)moveNext.Invoke(enumerator, null))
            {
                var skill = current.GetValue(enumerator);
                if (skill != null)
                {
                    var info = ExtractSkillInfo(skill);
                    if (info != null)
                        result.Add(info);
                }
            }
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityCombat.GetSkills", "Failed", ex);
        }

        return result;
    }

    /// <summary>
    /// Check if an actor can use a specific skill.
    /// </summary>
    public static bool CanUseAbility(GameObj actor, string skillName)
    {
        var skills = GetSkills(actor);
        var skill = skills.Find(s => s.Name == skillName);
        return skill?.CanUse ?? false;
    }

    /// <summary>
    /// Get the attack range for an actor's primary weapon.
    /// </summary>
    public static int GetAttackRange(GameObj actor)
    {
        var skills = GetSkills(actor);
        var attackSkill = skills.Find(s => s.IsAttack);
        return attackSkill?.Range ?? 0;
    }

    /// <summary>
    /// Apply suppression to an actor.
    /// </summary>
    public static bool ApplySuppression(GameObj actor, float amount)
    {
        if (actor.IsNull || !actor.IsAlive)
            return false;

        try
        {
            var current = GetSuppression(actor);
            var newValue = Math.Clamp(current + amount, 0f, MAX_SUPPRESSION);
            return SetSuppression(actor, newValue);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityCombat.ApplySuppression", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Set suppression value directly.
    /// </summary>
    public static bool SetSuppression(GameObj actor, float value)
    {
        if (actor.IsNull)
            return false;

        try
        {
            var clamped = Math.Clamp(value, 0f, MAX_SUPPRESSION);
            var bits = BitConverter.SingleToInt32Bits(clamped);
            Marshal.WriteInt32(actor.Pointer + (int)OFFSET_ACTOR_SUPPRESSION, bits);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityCombat.SetSuppression", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get current suppression value.
    /// </summary>
    public static float GetSuppression(GameObj actor)
    {
        if (actor.IsNull)
            return 0f;

        return actor.ReadFloat(OFFSET_ACTOR_SUPPRESSION);
    }

    /// <summary>
    /// Set morale value.
    /// </summary>
    public static bool SetMorale(GameObj actor, float value)
    {
        if (actor.IsNull)
            return false;

        try
        {
            var clamped = Math.Max(0f, value);
            var bits = BitConverter.SingleToInt32Bits(clamped);
            Marshal.WriteInt32(actor.Pointer + (int)OFFSET_ACTOR_MORALE, bits);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityCombat.SetMorale", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get current morale value.
    /// </summary>
    public static float GetMorale(GameObj actor)
    {
        if (actor.IsNull)
            return 0f;

        return actor.ReadFloat(OFFSET_ACTOR_MORALE);
    }

    /// <summary>
    /// Apply damage to an entity.
    /// </summary>
    public static bool ApplyDamage(GameObj entity, int damage)
    {
        if (entity.IsNull || !entity.IsAlive)
            return false;

        try
        {
            var currentHP = entity.ReadInt(OFFSET_ENTITY_CURRENT_HP);
            var newHP = Math.Max(0, currentHP - damage);
            Marshal.WriteInt32(entity.Pointer + (int)OFFSET_ENTITY_CURRENT_HP, newHP);

            // If HP reaches 0, entity should die - but we let the game handle that
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityCombat.ApplyDamage", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Heal an entity.
    /// </summary>
    public static bool Heal(GameObj entity, int amount)
    {
        if (entity.IsNull || !entity.IsAlive)
            return false;

        try
        {
            var currentHP = entity.ReadInt(OFFSET_ENTITY_CURRENT_HP);
            var maxHP = entity.ReadInt(OFFSET_ENTITY_MAX_HP);
            var newHP = Math.Min(maxHP, currentHP + amount);
            Marshal.WriteInt32(entity.Pointer + (int)OFFSET_ENTITY_CURRENT_HP, newHP);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityCombat.Heal", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Set whether an actor's turn is done.
    /// </summary>
    public static bool SetTurnDone(GameObj actor, bool done)
    {
        if (actor.IsNull)
            return false;

        try
        {
            Marshal.WriteByte(actor.Pointer + (int)OFFSET_ACTOR_IS_TURN_DONE, done ? (byte)1 : (byte)0);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityCombat.SetTurnDone", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Stun an actor.
    /// </summary>
    public static bool SetStunned(GameObj actor, bool stunned)
    {
        if (actor.IsNull)
            return false;

        try
        {
            Marshal.WriteByte(actor.Pointer + (int)OFFSET_ACTOR_IS_STUNNED, stunned ? (byte)1 : (byte)0);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityCombat.SetStunned", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get combat status info for an actor.
    /// </summary>
    public static CombatInfo GetCombatInfo(GameObj actor)
    {
        if (actor.IsNull)
            return null;

        return new CombatInfo
        {
            CurrentHP = actor.ReadInt(OFFSET_ENTITY_CURRENT_HP),
            MaxHP = actor.ReadInt(OFFSET_ENTITY_MAX_HP),
            IsAlive = ReadBoolAtOffset(actor, OFFSET_ENTITY_IS_ALIVE),
            Suppression = GetSuppression(actor),
            SuppressionState = GetSuppressionState(actor),
            Morale = GetMorale(actor),
            IsTurnDone = ReadBoolAtOffset(actor, OFFSET_ACTOR_IS_TURN_DONE),
            IsStunned = ReadBoolAtOffset(actor, OFFSET_ACTOR_IS_STUNNED),
            HasActed = ReadBoolAtOffset(actor, OFFSET_ACTOR_HAS_ACTED),
            TimesAttackedThisTurn = actor.ReadInt(OFFSET_ACTOR_TIMES_ATTACKED),
            CurrentAP = actor.ReadInt(OFFSET_ACTOR_CURRENT_AP)
        };
    }

    public class CombatInfo
    {
        public int CurrentHP { get; set; }
        public int MaxHP { get; set; }
        public float HPPercent => MaxHP > 0 ? (float)CurrentHP / MaxHP : 0f;
        public bool IsAlive { get; set; }
        public float Suppression { get; set; }
        public string SuppressionState { get; set; }
        public float Morale { get; set; }
        public bool IsTurnDone { get; set; }
        public bool IsStunned { get; set; }
        public bool HasActed { get; set; }
        public int TimesAttackedThisTurn { get; set; }
        public int CurrentAP { get; set; }
    }

    public class SkillInfo
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public bool CanUse { get; set; }
        public int APCost { get; set; }
        public int Range { get; set; }
        public int Cooldown { get; set; }
        public int CurrentCooldown { get; set; }
        public bool IsAttack { get; set; }
        public bool IsPassive { get; set; }
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _actorType ??= GameType.Find("Menace.Tactical.Actor");
        _skillType ??= GameType.Find("Menace.Tactical.Skills.BaseSkill");
        _skillContainerType ??= GameType.Find("Menace.Tactical.Skills.SkillContainer");
        _tacticalStateType ??= GameType.Find("Menace.States.TacticalState");
        _tacticalManagerType ??= GameType.Find("Menace.Tactical.TacticalManager");
    }

    private static string GetSuppressionState(GameObj actor)
    {
        var suppression = GetSuppression(actor);
        var pct = suppression / MAX_SUPPRESSION;

        if (pct >= PINNED_THRESHOLD) return "Pinned";
        if (pct >= SUPPRESSION_THRESHOLD) return "Suppressed";
        return "None";
    }

    private static SkillInfo ExtractSkillInfo(object skill)
    {
        if (skill == null)
            return null;

        try
        {
            var type = skill.GetType();
            var info = new SkillInfo();

            // Name
            var nameProp = type.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
            info.Name = nameProp?.GetValue(skill)?.ToString() ?? "Unknown";

            // Display name (from template if available)
            var templateProp = type.GetProperty("Template", BindingFlags.Public | BindingFlags.Instance);
            if (templateProp != null)
            {
                var template = templateProp.GetValue(skill);
                if (template != null)
                {
                    var displayNameProp = template.GetType().GetProperty("DisplayName",
                        BindingFlags.Public | BindingFlags.Instance);
                    info.DisplayName = displayNameProp?.GetValue(template)?.ToString() ?? info.Name;

                    var rangeProp = template.GetType().GetProperty("Range",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (rangeProp != null)
                        info.Range = Convert.ToInt32(rangeProp.GetValue(template) ?? 0);

                    var apCostProp = template.GetType().GetProperty("APCost",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (apCostProp != null)
                        info.APCost = Convert.ToInt32(apCostProp.GetValue(template) ?? 0);

                    var isPassiveProp = template.GetType().GetProperty("IsPassive",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (isPassiveProp != null)
                        info.IsPassive = (bool)(isPassiveProp.GetValue(template) ?? false);
                }
            }

            // CanUse
            var canUseMethod = type.GetMethod("CanUse", BindingFlags.Public | BindingFlags.Instance);
            if (canUseMethod != null)
                info.CanUse = (bool)canUseMethod.Invoke(skill, null);

            // Check if it's an attack skill (has damage, is weapon skill, etc.)
            info.IsAttack = info.Name.Contains("Attack") ||
                           info.Name.Contains("Shoot") ||
                           info.Name.Contains("Fire") ||
                           (info.Range > 0 && !info.IsPassive);

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityCombat.ExtractSkillInfo", "Failed", ex);
            return null;
        }
    }

    private static object GetManagedProxy(GameObj obj, Type managedType)
    {
        if (obj.IsNull || managedType == null) return null;

        try
        {
            var ptrCtor = managedType.GetConstructor(new[] { typeof(IntPtr) });
            return ptrCtor?.Invoke(new object[] { obj.Pointer });
        }
        catch
        {
            return null;
        }
    }

    private static bool ReadBoolAtOffset(GameObj obj, uint offset)
    {
        if (obj.IsNull || offset == 0) return false;

        try
        {
            return Marshal.ReadByte(obj.Pointer + (int)offset) != 0;
        }
        catch
        {
            return false;
        }
    }
}
