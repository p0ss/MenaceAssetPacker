using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

/// <summary>
/// Harmony hooks for TacticalManager events that fire Lua callbacks.
///
/// Hooks all InvokeOnX methods in TacticalManager to provide a comprehensive
/// event system for both C# plugins and Lua scripts.
///
/// C# Usage:
///   TacticalEventHooks.OnActorKilled += (actor, killer, faction) => { ... };
///   TacticalEventHooks.OnDamageReceived += (target, attacker, skill) => { ... };
///
/// Lua Usage:
///   on("actor_killed", function(data) log(data.actor .. " killed by " .. data.killer) end)
///   on("damage_received", function(data) log(data.target .. " took damage") end)
/// </summary>
public static class TacticalEventHooks
{
    private static bool _initialized;
    private static Type _tacticalManagerType;
    private static Type _actorType;
    private static Type _entityType;
    private static Type _skillType;
    private static Type _tileType;

    // ═══════════════════════════════════════════════════════════════════
    //  C# Events - Subscribe from plugins
    // ═══════════════════════════════════════════════════════════════════

    // Combat Events
    public static event Action<IntPtr, IntPtr, int> OnActorKilled;           // actor, killer, faction
    public static event Action<IntPtr, IntPtr, IntPtr> OnDamageReceived;     // target, attacker, skill
    public static event Action<IntPtr, IntPtr> OnAttackMissed;               // attacker, target
    public static event Action<IntPtr, IntPtr> OnAttackTileStart;            // attacker, tile
    public static event Action<IntPtr> OnBleedingOut;                        // actor
    public static event Action<IntPtr> OnStabilized;                         // actor
    public static event Action<IntPtr> OnSuppressed;                         // actor
    public static event Action<IntPtr, IntPtr, float> OnSuppressionApplied;  // target, attacker, amount

    // Actor State Events
    public static event Action<IntPtr> OnActorStateChanged;                  // actor
    public static event Action<IntPtr, int> OnMoraleStateChanged;            // actor, newState
    public static event Action<IntPtr, int, int> OnHitpointsChanged;         // actor, oldHp, newHp
    public static event Action<IntPtr> OnArmorChanged;                       // actor
    public static event Action<IntPtr, int, int> OnActionPointsChanged;      // actor, oldAp, newAp

    // Visibility Events
    public static event Action<IntPtr, IntPtr> OnDiscovered;                 // discovered, discoverer
    public static event Action<IntPtr> OnVisibleToPlayer;                    // entity
    public static event Action<IntPtr> OnHiddenToPlayer;                     // entity

    // Movement Events
    public static event Action<IntPtr, IntPtr, IntPtr> OnMovementStarted;    // actor, fromTile, toTile
    public static event Action<IntPtr, IntPtr> OnMovementFinished;           // actor, tile

    // Skill Events
    public static event Action<IntPtr, IntPtr, IntPtr> OnSkillUsed;          // user, skill, target
    public static event Action<IntPtr> OnSkillCompleted;                     // skill
    public static event Action<IntPtr, IntPtr> OnSkillAdded;                 // actor, skill
    public static event Action<IntPtr> OnOffmapAbilityUsed;                  // ability
    public static event Action<IntPtr> OnOffmapAbilityCanceled;              // ability

    // Turn/Round Events
    public static event Action<IntPtr> OnTurnStart;                          // actor (fires when actor becomes active)
    public static event Action<IntPtr> OnTurnEnd;                            // actor
    public static event Action<int> OnRoundEnd;                              // roundNumber (fires before round increments)
    public static event Action<int> OnRoundStart;                            // roundNumber (fires after round increments)

    // Entity Events
    public static event Action<IntPtr> OnEntitySpawned;                      // entity
    public static event Action<IntPtr> OnElementDeath;                       // element
    public static event Action<IntPtr> OnElementMalfunction;                 // element

    // Mission Events
    public static event Action<IntPtr, int> OnObjectiveStateChanged;         // objective, newState

    // ═══════════════════════════════════════════════════════════════════
    //  Initialization
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Initialize tactical event hooks. Call from ModpackLoaderMod after game assembly is loaded.
    /// </summary>
    public static void Initialize(HarmonyLib.Harmony harmony)
    {
        if (_initialized) return;

        try
        {
            var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            if (gameAssembly == null)
            {
                SdkLogger.Warning("[TacticalEventHooks] Assembly-CSharp not found");
                return;
            }

            // Cache types
            _tacticalManagerType = gameAssembly.GetType("Menace.Tactical.TacticalManager");
            _actorType = gameAssembly.GetType("Menace.Tactical.Actor");
            _entityType = gameAssembly.GetType("Menace.Tactical.Entity");
            _skillType = gameAssembly.GetType("Menace.Tactical.Skills.Skill");
            _tileType = gameAssembly.GetType("Menace.Tactical.Tile");

            if (_tacticalManagerType == null)
            {
                SdkLogger.Warning("[TacticalEventHooks] TacticalManager type not found");
                return;
            }

            // Apply all patches
            int patchCount = 0;
            patchCount += PatchMethod(harmony, "InvokeOnDeath", nameof(OnDeath_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnDamageReceived", nameof(OnDamageReceived_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnAttackMissed", nameof(OnAttackMissed_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnAttackTileStart", nameof(OnAttackTileStart_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnBleedingOut", nameof(OnBleedingOut_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnStabilized", nameof(OnStabilized_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnSuppressed", nameof(OnSuppressed_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnSuppressionApplied", nameof(OnSuppressionApplied_Postfix));

            patchCount += PatchMethod(harmony, "InvokeOnActorStateChanged", nameof(OnActorStateChanged_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnMoraleStateChanged", nameof(OnMoraleStateChanged_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnHitpointsChanged", nameof(OnHitpointsChanged_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnArmorChanged", nameof(OnArmorChanged_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnActionPointsChanged", nameof(OnActionPointsChanged_Postfix));

            patchCount += PatchMethod(harmony, "InvokeOnDiscovered", nameof(OnDiscovered_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnVisibleToPlayer", nameof(OnVisibleToPlayer_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnHiddenToPlayer", nameof(OnHiddenToPlayer_Postfix));

            patchCount += PatchMethod(harmony, "InvokeOnMovement", nameof(OnMovement_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnMovementFinished", nameof(OnMovementFinished_Postfix));

            patchCount += PatchMethod(harmony, "InvokeOnSkillUse", nameof(OnSkillUse_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnAfterSkillUse", nameof(OnAfterSkillUse_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnSkillAdded", nameof(OnSkillAdded_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnOffmapAbilityUsed", nameof(OnOffmapAbilityUsed_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnOffmapAbilityCanceled", nameof(OnOffmapAbilityCanceled_Postfix));

            patchCount += PatchMethod(harmony, "InvokeOnTurnEnd", nameof(OnTurnEnd_Postfix));

            // OnTurnStart fires when SetActiveActor is called with a new actor
            patchCount += PatchMethod(harmony, "SetActiveActor", nameof(OnSetActiveActor_Postfix));

            patchCount += PatchMethod(harmony, "InvokeOnEntitySpawned", nameof(OnEntitySpawned_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnElementDeath", nameof(OnElementDeath_Postfix));
            patchCount += PatchMethod(harmony, "InvokeOnElementMalfunction", nameof(OnElementMalfunction_Postfix));

            patchCount += PatchMethod(harmony, "InvokeOnObjectiveStateChanged", nameof(OnObjectiveStateChanged_Postfix));

            // Additional hooks for turn/round that don't have InvokeOn methods
            patchCount += PatchMethodPrefixPostfix(harmony, "NextRound", nameof(OnNextRound_Prefix), nameof(OnNextRound_Postfix));

            _initialized = true;
            SdkLogger.Msg($"[TacticalEventHooks] Initialized with {patchCount} event hooks");
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[TacticalEventHooks] Failed to initialize: {ex.Message}");
        }
    }

    private static int PatchMethod(HarmonyLib.Harmony harmony, string methodName, string patchMethodName)
    {
        try
        {
            var targetMethod = _tacticalManagerType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[TacticalEventHooks] Method not found: {methodName}");
                return 0;
            }

            var patchMethod = typeof(TacticalEventHooks).GetMethod(patchMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            if (patchMethod == null)
            {
                SdkLogger.Warning($"[TacticalEventHooks] Patch method not found: {patchMethodName}");
                return 0;
            }

            harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[TacticalEventHooks] Failed to patch {methodName}: {ex.Message}");
            return 0;
        }
    }

    private static int PatchMethodPrefixPostfix(HarmonyLib.Harmony harmony, string methodName, string prefixMethodName, string postfixMethodName)
    {
        try
        {
            var targetMethod = _tacticalManagerType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (targetMethod == null)
            {
                SdkLogger.Warning($"[TacticalEventHooks] Method not found: {methodName}");
                return 0;
            }

            var prefixMethod = typeof(TacticalEventHooks).GetMethod(prefixMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            var postfixMethod = typeof(TacticalEventHooks).GetMethod(postfixMethodName,
                BindingFlags.Static | BindingFlags.NonPublic);

            harmony.Patch(targetMethod,
                prefix: prefixMethod != null ? new HarmonyMethod(prefixMethod) : null,
                postfix: postfixMethod != null ? new HarmonyMethod(postfixMethod) : null);
            return 1;
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[TacticalEventHooks] Failed to patch {methodName}: {ex.Message}");
            return 0;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Helper Methods
    // ═══════════════════════════════════════════════════════════════════

    private static IntPtr GetPointer(object obj)
    {
        if (obj == null) return IntPtr.Zero;
        if (obj is Il2CppObjectBase il2cppObj)
            return il2cppObj.Pointer;
        return IntPtr.Zero;
    }

    private static string GetName(object obj)
    {
        if (obj == null) return "<null>";
        try
        {
            var gameObj = new GameObj(GetPointer(obj));
            return gameObj.GetName() ?? "<unnamed>";
        }
        catch
        {
            return "<unknown>";
        }
    }

    // Skill field offsets - fallbacks if schema not loaded
    private const int FALLBACK_OFFSET_SKILL_TEMPLATE = 0x10;
    private const int FALLBACK_OFFSET_TEMPLATE_IS_ATTACK = 0xF2;
    private const int FALLBACK_OFFSET_TEMPLATE_IS_SILENT = 0x110;

    // Cached schema offsets
    private static int? _schemaOffsetTemplate;
    private static int? _schemaOffsetIsAttack;
    private static int? _schemaOffsetIsSilent;
    private static bool _schemaChecked;

    private static void EnsureSchemaOffsetsLoaded()
    {
        if (_schemaChecked) return;
        _schemaChecked = true;

        if (!TemplateSchema.IsInitialized) return;

        if (TemplateSchema.TryGetOffset("Skill", "Template", out var tOff))
            _schemaOffsetTemplate = tOff;
        if (TemplateSchema.TryGetOffset("SkillTemplate", "IsAttack", out var aOff))
            _schemaOffsetIsAttack = aOff;
        if (TemplateSchema.TryGetOffset("SkillTemplate", "IsSilent", out var sOff))
            _schemaOffsetIsSilent = sOff;
    }

    private static int GetOffsetSkillTemplate()
    {
        EnsureSchemaOffsetsLoaded();
        return _schemaOffsetTemplate ?? FALLBACK_OFFSET_SKILL_TEMPLATE;
    }

    private static int GetOffsetIsAttack()
    {
        EnsureSchemaOffsetsLoaded();
        return _schemaOffsetIsAttack ?? FALLBACK_OFFSET_TEMPLATE_IS_ATTACK;
    }

    private static int GetOffsetIsSilent()
    {
        EnsureSchemaOffsetsLoaded();
        return _schemaOffsetIsSilent ?? FALLBACK_OFFSET_TEMPLATE_IS_SILENT;
    }

    /// <summary>
    /// Extract skill information from a skill object for Lua events.
    /// Returns (isAttack, isSilent, skillName) tuple.
    /// </summary>
    private static (bool isAttack, bool isSilent, string name) GetSkillInfo(object skill)
    {
        if (skill == null) return (false, false, "<null>");

        try
        {
            var skillPtr = GetPointer(skill);
            if (skillPtr == IntPtr.Zero) return (false, false, "<null>");

            var skillObj = new GameObj(skillPtr);
            var name = skillObj.GetName() ?? "<unnamed>";

            // Read template pointer from skill
            var templatePtr = Marshal.ReadIntPtr(skillPtr + GetOffsetSkillTemplate());
            if (templatePtr == IntPtr.Zero) return (false, false, name);

            // Read IsAttack bool from template
            var isAttack = Marshal.ReadByte(templatePtr + GetOffsetIsAttack()) != 0;

            // Read IsSilent bool from template
            var isSilent = Marshal.ReadByte(templatePtr + GetOffsetIsSilent()) != 0;

            return (isAttack, isSilent, name);
        }
        catch
        {
            return (false, false, "<unknown>");
        }
    }

    private static void FireLuaEvent(string eventName, Dictionary<string, object> data)
    {
        try
        {
            LuaScriptEngine.Instance?.FireEventWithTable(eventName, data);
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("TacticalEventHooks", $"Lua event '{eventName}' failed: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Harmony Postfix Patches
    // ═══════════════════════════════════════════════════════════════════

    // --- Combat Events ---

    private static void OnDeath_Postfix(object __instance, object entity, object killer, int factionId)
    {
        var entityPtr = GetPointer(entity);
        var killerPtr = GetPointer(killer);

        OnActorKilled?.Invoke(entityPtr, killerPtr, factionId);

        FireLuaEvent("actor_killed", new Dictionary<string, object>
        {
            ["actor"] = GetName(entity),
            ["actor_ptr"] = entityPtr.ToInt64(),
            ["killer"] = GetName(killer),
            ["killer_ptr"] = killerPtr.ToInt64(),
            ["faction"] = factionId
        });
    }

    private static void OnDamageReceived_Postfix(object __instance, object target, object attacker, object skill)
    {
        var targetPtr = GetPointer(target);
        var attackerPtr = GetPointer(attacker);
        var skillPtr = GetPointer(skill);

        OnDamageReceived?.Invoke(targetPtr, attackerPtr, skillPtr);

        FireLuaEvent("damage_received", new Dictionary<string, object>
        {
            ["target"] = GetName(target),
            ["target_ptr"] = targetPtr.ToInt64(),
            ["attacker"] = GetName(attacker),
            ["attacker_ptr"] = attackerPtr.ToInt64(),
            ["skill"] = GetName(skill),
            ["skill_ptr"] = skillPtr.ToInt64()
        });
    }

    private static void OnAttackMissed_Postfix(object __instance, object attacker, object target)
    {
        var attackerPtr = GetPointer(attacker);
        var targetPtr = GetPointer(target);

        OnAttackMissed?.Invoke(attackerPtr, targetPtr);

        FireLuaEvent("attack_missed", new Dictionary<string, object>
        {
            ["attacker"] = GetName(attacker),
            ["attacker_ptr"] = attackerPtr.ToInt64(),
            ["target"] = GetName(target),
            ["target_ptr"] = targetPtr.ToInt64()
        });
    }

    private static void OnAttackTileStart_Postfix(object __instance, object attacker, object tile)
    {
        var attackerPtr = GetPointer(attacker);
        var tilePtr = GetPointer(tile);

        OnAttackTileStart?.Invoke(attackerPtr, tilePtr);

        FireLuaEvent("attack_start", new Dictionary<string, object>
        {
            ["attacker"] = GetName(attacker),
            ["attacker_ptr"] = attackerPtr.ToInt64(),
            ["tile_ptr"] = tilePtr.ToInt64()
        });
    }

    private static void OnBleedingOut_Postfix(object __instance, object actor)
    {
        var actorPtr = GetPointer(actor);

        OnBleedingOut?.Invoke(actorPtr);

        FireLuaEvent("bleeding_out", new Dictionary<string, object>
        {
            ["actor"] = GetName(actor),
            ["actor_ptr"] = actorPtr.ToInt64()
        });
    }

    private static void OnStabilized_Postfix(object __instance, object actor)
    {
        var actorPtr = GetPointer(actor);

        OnStabilized?.Invoke(actorPtr);

        FireLuaEvent("stabilized", new Dictionary<string, object>
        {
            ["actor"] = GetName(actor),
            ["actor_ptr"] = actorPtr.ToInt64()
        });
    }

    private static void OnSuppressed_Postfix(object __instance, object actor)
    {
        var actorPtr = GetPointer(actor);

        OnSuppressed?.Invoke(actorPtr);

        FireLuaEvent("suppressed", new Dictionary<string, object>
        {
            ["actor"] = GetName(actor),
            ["actor_ptr"] = actorPtr.ToInt64()
        });
    }

    private static void OnSuppressionApplied_Postfix(object __instance, object target, object attacker, float amount)
    {
        var targetPtr = GetPointer(target);
        var attackerPtr = GetPointer(attacker);

        OnSuppressionApplied?.Invoke(targetPtr, attackerPtr, amount);

        FireLuaEvent("suppression_applied", new Dictionary<string, object>
        {
            ["target"] = GetName(target),
            ["target_ptr"] = targetPtr.ToInt64(),
            ["attacker"] = GetName(attacker),
            ["attacker_ptr"] = attackerPtr.ToInt64(),
            ["amount"] = amount
        });
    }

    // --- Actor State Events ---

    private static void OnActorStateChanged_Postfix(object __instance, object actor)
    {
        var actorPtr = GetPointer(actor);

        OnActorStateChanged?.Invoke(actorPtr);

        FireLuaEvent("actor_state_changed", new Dictionary<string, object>
        {
            ["actor"] = GetName(actor),
            ["actor_ptr"] = actorPtr.ToInt64()
        });
    }

    private static void OnMoraleStateChanged_Postfix(object __instance, object actor, int newState)
    {
        var actorPtr = GetPointer(actor);

        OnMoraleStateChanged?.Invoke(actorPtr, newState);

        FireLuaEvent("morale_changed", new Dictionary<string, object>
        {
            ["actor"] = GetName(actor),
            ["actor_ptr"] = actorPtr.ToInt64(),
            ["state"] = newState
        });
    }

    private static void OnHitpointsChanged_Postfix(object __instance, object actor, int oldHp, int newHp)
    {
        var actorPtr = GetPointer(actor);

        OnHitpointsChanged?.Invoke(actorPtr, oldHp, newHp);

        FireLuaEvent("hp_changed", new Dictionary<string, object>
        {
            ["actor"] = GetName(actor),
            ["actor_ptr"] = actorPtr.ToInt64(),
            ["old_hp"] = oldHp,
            ["new_hp"] = newHp,
            ["delta"] = newHp - oldHp
        });
    }

    private static void OnArmorChanged_Postfix(object __instance, object actor)
    {
        var actorPtr = GetPointer(actor);

        OnArmorChanged?.Invoke(actorPtr);

        FireLuaEvent("armor_changed", new Dictionary<string, object>
        {
            ["actor"] = GetName(actor),
            ["actor_ptr"] = actorPtr.ToInt64()
        });
    }

    private static void OnActionPointsChanged_Postfix(object __instance, object actor, int oldAp, int newAp)
    {
        var actorPtr = GetPointer(actor);

        OnActionPointsChanged?.Invoke(actorPtr, oldAp, newAp);

        FireLuaEvent("ap_changed", new Dictionary<string, object>
        {
            ["actor"] = GetName(actor),
            ["actor_ptr"] = actorPtr.ToInt64(),
            ["old_ap"] = oldAp,
            ["new_ap"] = newAp,
            ["delta"] = newAp - oldAp
        });
    }

    // --- Visibility Events ---

    private static void OnDiscovered_Postfix(object __instance, object discovered, object discoverer)
    {
        var discoveredPtr = GetPointer(discovered);
        var discovererPtr = GetPointer(discoverer);

        OnDiscovered?.Invoke(discoveredPtr, discovererPtr);

        FireLuaEvent("discovered", new Dictionary<string, object>
        {
            ["discovered"] = GetName(discovered),
            ["discovered_ptr"] = discoveredPtr.ToInt64(),
            ["discoverer"] = GetName(discoverer),
            ["discoverer_ptr"] = discovererPtr.ToInt64()
        });
    }

    private static void OnVisibleToPlayer_Postfix(object __instance, object entity)
    {
        var entityPtr = GetPointer(entity);

        OnVisibleToPlayer?.Invoke(entityPtr);

        FireLuaEvent("visible_to_player", new Dictionary<string, object>
        {
            ["entity"] = GetName(entity),
            ["entity_ptr"] = entityPtr.ToInt64()
        });
    }

    private static void OnHiddenToPlayer_Postfix(object __instance, object entity)
    {
        var entityPtr = GetPointer(entity);

        OnHiddenToPlayer?.Invoke(entityPtr);

        FireLuaEvent("hidden_from_player", new Dictionary<string, object>
        {
            ["entity"] = GetName(entity),
            ["entity_ptr"] = entityPtr.ToInt64()
        });
    }

    // --- Movement Events ---

    private static void OnMovement_Postfix(object __instance, object actor, object fromTile, object toTile,
        int action, object container)
    {
        var actorPtr = GetPointer(actor);
        var fromPtr = GetPointer(fromTile);
        var toPtr = GetPointer(toTile);

        OnMovementStarted?.Invoke(actorPtr, fromPtr, toPtr);

        FireLuaEvent("move_start", new Dictionary<string, object>
        {
            ["actor"] = GetName(actor),
            ["actor_ptr"] = actorPtr.ToInt64(),
            ["from_tile_ptr"] = fromPtr.ToInt64(),
            ["to_tile_ptr"] = toPtr.ToInt64(),
            ["action"] = action
        });
    }

    private static void OnMovementFinished_Postfix(object __instance, object actor, object tile)
    {
        var actorPtr = GetPointer(actor);
        var tilePtr = GetPointer(tile);

        OnMovementFinished?.Invoke(actorPtr, tilePtr);

        FireLuaEvent("move_complete", new Dictionary<string, object>
        {
            ["actor"] = GetName(actor),
            ["actor_ptr"] = actorPtr.ToInt64(),
            ["tile_ptr"] = tilePtr.ToInt64()
        });
    }

    // --- Skill Events ---

    private static void OnSkillUse_Postfix(object __instance, object user, object skill, object targetParams)
    {
        var userPtr = GetPointer(user);
        var skillPtr = GetPointer(skill);
        var targetPtr = GetPointer(targetParams);

        OnSkillUsed?.Invoke(userPtr, skillPtr, targetPtr);

        // Fire Lua event with Actor and Skill objects directly
        // Usage: on("skill_used", function(actor, skill)
        //            if skill.is_attack and not skill.is_silent then
        //                actor:add_effect("concealment", -3, 1)
        //            end
        //        end)
        try
        {
            LuaScriptEngine.Instance?.FireEventWithActorAndSkill("skill_used", userPtr.ToInt64(), skillPtr.ToInt64());
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("TacticalEventHooks", $"skill_used event failed: {ex.Message}");
        }
    }

    private static void OnAfterSkillUse_Postfix(object __instance, object skill)
    {
        var skillPtr = GetPointer(skill);

        OnSkillCompleted?.Invoke(skillPtr);

        FireLuaEvent("skill_complete", new Dictionary<string, object>
        {
            ["skill"] = GetName(skill),
            ["skill_ptr"] = skillPtr.ToInt64()
        });
    }

    private static void OnSkillAdded_Postfix(object __instance, object actor, object skill)
    {
        var actorPtr = GetPointer(actor);
        var skillPtr = GetPointer(skill);

        OnSkillAdded?.Invoke(actorPtr, skillPtr);

        FireLuaEvent("skill_added", new Dictionary<string, object>
        {
            ["actor"] = GetName(actor),
            ["actor_ptr"] = actorPtr.ToInt64(),
            ["skill"] = GetName(skill),
            ["skill_ptr"] = skillPtr.ToInt64()
        });
    }

    private static void OnOffmapAbilityUsed_Postfix(object __instance, object ability)
    {
        var abilityPtr = GetPointer(ability);

        OnOffmapAbilityUsed?.Invoke(abilityPtr);

        FireLuaEvent("offmap_ability_used", new Dictionary<string, object>
        {
            ["ability"] = GetName(ability),
            ["ability_ptr"] = abilityPtr.ToInt64()
        });
    }

    private static void OnOffmapAbilityCanceled_Postfix(object __instance, object ability)
    {
        var abilityPtr = GetPointer(ability);

        OnOffmapAbilityCanceled?.Invoke(abilityPtr);

        FireLuaEvent("offmap_ability_canceled", new Dictionary<string, object>
        {
            ["ability"] = GetName(ability),
            ["ability_ptr"] = abilityPtr.ToInt64()
        });
    }

    // --- Turn/Round Events ---

    private static void OnTurnEnd_Postfix(object __instance, object actor)
    {
        var actorPtr = GetPointer(actor);

        OnTurnEnd?.Invoke(actorPtr);

        // Get faction info
        int faction = 0;
        string factionName = "";
        try
        {
            var gameObj = new GameObj(actorPtr);
            faction = gameObj.ReadInt(0xBC); // OFFSET_ACTOR_FACTION_ID
            factionName = TacticalController.GetFactionName((FactionType)faction);
        }
        catch { }

        // Fire existing turn_end event for compatibility
        LuaScriptEngine.Instance?.OnTurnEnd(faction, factionName);
    }

    private static void OnNextRound_Prefix(object __instance)
    {
        // Fire round_end BEFORE the round number increments
        int roundNumber = TacticalController.GetCurrentRound();

        OnRoundEnd?.Invoke(roundNumber);

        FireLuaEvent("round_end", new Dictionary<string, object>
        {
            ["round"] = roundNumber
        });
    }

    private static void OnNextRound_Postfix(object __instance)
    {
        // Fire round_start AFTER the round number has incremented
        int roundNumber = TacticalController.GetCurrentRound();

        OnRoundStart?.Invoke(roundNumber);

        FireLuaEvent("round_start", new Dictionary<string, object>
        {
            ["round"] = roundNumber
        });
    }

    private static void OnSetActiveActor_Postfix(object __instance, object actor, bool isNewTurn)
    {
        // Only fire turn_start if this is actually a new turn (not just selecting an actor)
        if (!isNewTurn || actor == null) return;

        var actorPtr = GetPointer(actor);
        if (actorPtr == IntPtr.Zero) return;

        OnTurnStart?.Invoke(actorPtr);

        // Get faction info for Lua
        int faction = 0;
        string factionName = "";
        try
        {
            var gameObj = new GameObj(actorPtr);
            faction = gameObj.ReadInt(0xBC); // OFFSET_ACTOR_FACTION_ID
            factionName = TacticalController.GetFactionName((FactionType)faction);
        }
        catch { }

        FireLuaEvent("turn_start", new Dictionary<string, object>
        {
            ["actor"] = GetName(actor),
            ["actor_ptr"] = actorPtr.ToInt64(),
            ["faction"] = faction,
            ["faction_name"] = factionName
        });
    }

    // --- Entity Events ---

    private static void OnEntitySpawned_Postfix(object __instance, object entity)
    {
        var entityPtr = GetPointer(entity);

        OnEntitySpawned?.Invoke(entityPtr);

        FireLuaEvent("entity_spawned", new Dictionary<string, object>
        {
            ["entity"] = GetName(entity),
            ["entity_ptr"] = entityPtr.ToInt64()
        });
    }

    private static void OnElementDeath_Postfix(object __instance, object element)
    {
        var elementPtr = GetPointer(element);

        OnElementDeath?.Invoke(elementPtr);

        FireLuaEvent("element_destroyed", new Dictionary<string, object>
        {
            ["element"] = GetName(element),
            ["element_ptr"] = elementPtr.ToInt64()
        });
    }

    private static void OnElementMalfunction_Postfix(object __instance, object element)
    {
        var elementPtr = GetPointer(element);

        OnElementMalfunction?.Invoke(elementPtr);

        FireLuaEvent("element_malfunction", new Dictionary<string, object>
        {
            ["element"] = GetName(element),
            ["element_ptr"] = elementPtr.ToInt64()
        });
    }

    // --- Mission Events ---

    private static void OnObjectiveStateChanged_Postfix(object __instance, object objective, int newState)
    {
        var objectivePtr = GetPointer(objective);

        OnObjectiveStateChanged?.Invoke(objectivePtr, newState);

        FireLuaEvent("objective_changed", new Dictionary<string, object>
        {
            ["objective"] = GetName(objective),
            ["objective_ptr"] = objectivePtr.ToInt64(),
            ["state"] = newState
        });
    }
}
