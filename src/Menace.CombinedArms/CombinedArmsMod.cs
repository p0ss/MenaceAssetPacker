using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(Menace.CombinedArms.CombinedArmsMod), "Menace Combined Arms", "1.0.0", "Menace Modkit")]
[assembly: MelonGame(null, null)]

namespace Menace.CombinedArms;

public class CombinedArmsMod : MelonMod
{
    private static MelonLogger.Instance Log;
    private HarmonyLib.Harmony _harmony;
    private bool _patchesApplied;

    // Cached managed reflection
    private static MethodInfo _getRoleMethod;
    private static Type _attackType;
    private static IntPtr _cachedAttackKlass = IntPtr.Zero;
    private static IntPtr _cachedAIFactionKlass = IntPtr.Zero;

    // First-invocation diagnostic flags (hook fired)
    private static bool _loggedFirstTurnStart;
    private static bool _loggedFirstScoreMult;
    private static bool _loggedFirstExecute;
    private static bool _loggedFirstTileScores;

    // First-effect diagnostic flags (hook actually modified a value)
    private static bool _loggedFirstSequencingEffect;
    private static bool _loggedFirstFocusFireEffect;
    private static bool _loggedFirstCofEffect;
    private static bool _loggedFirstDepthEffect;
    private static bool _loggedFirstExecuteTrack;

    // ═══════════════════════════════════════════════════════
    //  Dynamic IL2CPP offset cache
    //
    //  Resolved once at startup via il2cpp_class_get_field_from_name
    //  + il2cpp_field_get_offset. Value of 0 means "not found" and
    //  the hook that needs it will gracefully no-op.
    // ═══════════════════════════════════════════════════════

    // BaseFaction
    private static uint _off_BaseFaction_m_FactionIndex;

    // Agent
    private static uint _off_Agent_m_Faction;
    private static uint _off_Agent_m_Actor;
    private static uint _off_Agent_m_Behaviors;
    private static uint _off_Agent_m_ActiveBehavior;
    private static uint _off_Agent_m_Tiles;

    // AIFaction
    private static uint _off_AIFaction_m_Opponents;

    // Opponent
    private static uint _off_Opponent_Actor;

    // RoleData
    private static uint _off_RoleData_Move;
    private static uint _off_RoleData_SafetyScale;
    private static uint _off_RoleData_InflictDamage;
    private static uint _off_RoleData_InflictSuppression;

    // SkillBehavior
    private static uint _off_SkillBehavior_m_TargetTile;

    // Attack
    private static uint _off_Attack_m_Goal;

    // TileScore
    private static uint _off_TileScore_UtilityScore;

    // Il2Cpp List<T> internals (resolved from Il2CppSystem metadata)
    private static uint _off_List_items;
    private static uint _off_List_size;

    // Il2Cpp Dictionary<K,V> internals
    private static uint _off_Dict_entries;
    private static uint _off_Dict_count;
    private static int _dictEntrySize; // Actual inline entry stride from IL2CPP metadata

    // Feature availability flags — set false if critical offsets are missing
    private static bool _sequencingAvailable;
    private static bool _focusFireAvailable;
    private static bool _executionTrackingAvailable;
    private static bool _cofAvailable;
    private static bool _formationDepthAvailable;
    private static bool _turnResetAvailable;

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        Log.Msg("Menace Combined Arms v1.0.0");
        CoordinationState.LoadConfig();
        Log.Msg($"Config loaded — Sequencing={CoordinationState.Config.EnableAgentSequencing} " +
                $"FocusFire={CoordinationState.Config.EnableFocusFire} " +
                $"CoF={CoordinationState.Config.EnableCenterOfForces} " +
                $"Depth={CoordinationState.Config.EnableFormationDepth}");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (!_patchesApplied && sceneName == "Tactical")
        {
            MelonCoroutines.Start(DelayedPatchApply());
        }
    }

    private System.Collections.IEnumerator DelayedPatchApply()
    {
        for (int i = 0; i < 10; i++)
            yield return null;

        ApplyPatches();
    }

    public override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.F8))
        {
            CoordinationState.Enabled = !CoordinationState.Enabled;
            Log.Msg($"Combined Arms {(CoordinationState.Enabled ? "ENABLED" : "DISABLED")}");
        }

        if (Input.GetKeyDown(KeyCode.F10))
        {
            CoordinationState.LoadConfig();
            Log.Msg("Config reloaded");
        }
    }

    // ═══════════════════════════════════════════════════════
    //  IL2CPP field resolution helpers
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Find a native IL2CPP field by name, walking parent classes.
    /// Tries the exact name first, then common C# backing-field conventions.
    /// </summary>
    private static IntPtr FindNativeField(IntPtr klass, string fieldName)
    {
        string[] namesToTry =
        {
            fieldName,
            fieldName.Length > 0 && char.IsUpper(fieldName[0])
                ? char.ToLower(fieldName[0]) + fieldName.Substring(1) : null,
            "_" + fieldName,
            "m_" + fieldName,
            $"<{fieldName}>k__BackingField"
        };

        IntPtr searchKlass = klass;
        while (searchKlass != IntPtr.Zero)
        {
            foreach (var name in namesToTry)
            {
                if (name == null) continue;
                IntPtr field = IL2CPP.il2cpp_class_get_field_from_name(searchKlass, name);
                if (field != IntPtr.Zero) return field;
            }
            searchKlass = IL2CPP.il2cpp_class_get_parent(searchKlass);
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Resolve a field's offset. Returns 0 on failure (which is never a valid
    /// instance field offset — offset 0 is the method table pointer).
    /// </summary>
    private static uint ResolveOffset(IntPtr klass, string fieldName, string context)
    {
        IntPtr field = FindNativeField(klass, fieldName);
        if (field == IntPtr.Zero)
        {
            Log?.Warning($"[CombinedArms] Field '{fieldName}' not found on {context}");
            return 0;
        }
        uint offset = IL2CPP.il2cpp_field_get_offset(field);
        if (offset == 0)
            Log?.Warning($"[CombinedArms] Field '{fieldName}' on {context} has offset 0 (unexpected)");
        else
            Log?.Msg($"[CombinedArms]   {context}.{fieldName} → 0x{offset:X}");
        return offset;
    }

    // Cached game assembly for type lookups
    private static Assembly _gameAssembly;

    /// <summary>
    /// Find a managed type by full name, with Il2Cpp-prefixed and short name fallbacks.
    /// Il2CppInterop prefixes all proxy type namespaces with "Il2Cpp".
    /// </summary>
    private static Type FindManagedType(string fullTypeName, string shortName)
    {
        // Try exact FullName
        var type = _gameAssembly?.GetTypes().FirstOrDefault(t => t.FullName == fullTypeName);

        // Try with Il2Cpp prefix (Il2CppInterop proxy naming convention)
        if (type == null)
        {
            string il2cppName = "Il2Cpp" + fullTypeName;
            type = _gameAssembly?.GetTypes().FirstOrDefault(t => t.FullName == il2cppName);
            if (type != null)
                Log?.Msg($"[CombinedArms] '{fullTypeName}' found as '{type.FullName}'");
        }

        // Fall back to short Name match (may be ambiguous)
        if (type == null)
        {
            type = _gameAssembly?.GetTypes().FirstOrDefault(t => t.Name == shortName);
            if (type != null)
                Log?.Msg($"[CombinedArms] '{fullTypeName}' matched by short name as '{type.FullName}'");
            else
                Log?.Warning($"[CombinedArms] Type '{fullTypeName}' (short: '{shortName}') not found");
        }
        return type;
    }

    /// <summary>
    /// Resolve an IL2CPP native class pointer directly from IL2CPP metadata.
    /// Uses IL2CPP.GetIl2CppClass which queries the runtime by assembly/namespace/name,
    /// bypassing managed proxy type resolution entirely.
    /// </summary>
    private static IntPtr GetIl2CppClass(string fullTypeName)
    {
        try
        {
            // Split "Menace.Tactical.AI.Data.RoleData" into namespace + class name
            int lastDot = fullTypeName.LastIndexOf('.');
            string namespaceName = lastDot >= 0 ? fullTypeName.Substring(0, lastDot) : "";
            string className = lastDot >= 0 ? fullTypeName.Substring(lastDot + 1) : fullTypeName;

            // Query IL2CPP metadata directly — no managed type resolution needed
            IntPtr klass = IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", namespaceName, className);
            if (klass != IntPtr.Zero)
            {
                Log?.Msg($"[CombinedArms] Resolved {namespaceName}.{className} → 0x{klass.ToInt64():X}");
                return klass;
            }

            // Try without .dll suffix
            klass = IL2CPP.GetIl2CppClass("Assembly-CSharp", namespaceName, className);
            if (klass != IntPtr.Zero)
            {
                Log?.Msg($"[CombinedArms] Resolved {namespaceName}.{className} → 0x{klass.ToInt64():X}");
                return klass;
            }

            Log?.Warning($"[CombinedArms] IL2CPP class '{fullTypeName}' not found in Assembly-CSharp");
        }
        catch (Exception ex)
        {
            Log?.Warning($"[CombinedArms] GetIl2CppClass({fullTypeName}) error: {ex.Message}");
        }

        return IntPtr.Zero;
    }



    // ═══════════════════════════════════════════════════════
    //  Patch Setup & Offset Resolution
    // ═══════════════════════════════════════════════════════

    private void ApplyPatches()
    {
        if (_patchesApplied) return;

        _gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (_gameAssembly == null)
        {
            Log.Error("Assembly-CSharp not found");
            return;
        }

        _harmony = new HarmonyLib.Harmony("com.menacemodkit.combinedarms");

        // ── Resolve managed types ──
        _attackType = FindManagedType("Menace.Tactical.AI.Behaviors.Attack", "Attack");

        var agentType = FindManagedType("Menace.Tactical.AI.Agent", "Agent");

        if (agentType != null)
        {
            _getRoleMethod = agentType.GetMethod("GetRole",
                BindingFlags.Public | BindingFlags.Instance);
        }

        // ── Resolve IL2CPP offsets dynamically ──
        Log.Msg("Resolving IL2CPP field offsets...");
        ResolveAllOffsets();

        // ── Apply Harmony patches ──
        if (_turnResetAvailable)
            PatchOnTurnStart(_gameAssembly);
        else
            Log.Warning("Turn reset hook disabled — missing BaseFaction.m_FactionIndex");

        if (_sequencingAvailable || _focusFireAvailable)
            PatchGetScoreMult(_gameAssembly);
        else
            Log.Warning("GetScoreMult hook disabled — missing required offsets");

        if (_executionTrackingAvailable)
            PatchExecute(_gameAssembly);
        else
            Log.Warning("Execute tracking hook disabled — missing required offsets");

        if (_cofAvailable || _formationDepthAvailable)
            PatchPostProcessTileScores(_gameAssembly);
        else
            Log.Warning("PostProcessTileScores hook disabled — missing required offsets");

        _patchesApplied = true;
        Log.Msg($"Combined Arms patches applied — " +
                $"Sequencing={_sequencingAvailable} FocusFire={_focusFireAvailable} " +
                $"Tracking={_executionTrackingAvailable} CoF={_cofAvailable} " +
                $"Depth={_formationDepthAvailable}");
    }

    private void ResolveAllOffsets()
    {
        // ── BaseFaction ──
        IntPtr baseFactionClass = GetIl2CppClass("Menace.Tactical.AI.BaseFaction");
        if (baseFactionClass != IntPtr.Zero)
            _off_BaseFaction_m_FactionIndex = ResolveOffset(baseFactionClass, "m_FactionIndex", "BaseFaction");

        // ── Agent ──
        IntPtr agentClass = GetIl2CppClass("Menace.Tactical.AI.Agent");
        if (agentClass != IntPtr.Zero)
        {
            _off_Agent_m_Faction = ResolveOffset(agentClass, "m_Faction", "Agent");
            _off_Agent_m_Actor = ResolveOffset(agentClass, "m_Actor", "Agent");
            _off_Agent_m_Behaviors = ResolveOffset(agentClass, "m_Behaviors", "Agent");
            _off_Agent_m_ActiveBehavior = ResolveOffset(agentClass, "m_ActiveBehavior", "Agent");
            _off_Agent_m_Tiles = ResolveOffset(agentClass, "m_Tiles", "Agent");
        }

        // ── AIFaction ──
        IntPtr aiFactionClass = GetIl2CppClass("Menace.Tactical.AI.AIFaction");
        if (aiFactionClass != IntPtr.Zero)
        {
            _off_AIFaction_m_Opponents = ResolveOffset(aiFactionClass, "m_Opponents", "AIFaction");
            _cachedAIFactionKlass = aiFactionClass;
        }

        // ── Opponent ──
        IntPtr opponentClass = GetIl2CppClass("Menace.Tactical.AI.Opponent");
        if (opponentClass != IntPtr.Zero)
            _off_Opponent_Actor = ResolveOffset(opponentClass, "Actor", "Opponent");

        // ── RoleData ──
        IntPtr roleDataClass = GetIl2CppClass("Menace.Tactical.AI.Data.RoleData");
        if (roleDataClass != IntPtr.Zero)
        {
            _off_RoleData_Move = ResolveOffset(roleDataClass, "Move", "RoleData");
            _off_RoleData_SafetyScale = ResolveOffset(roleDataClass, "SafetyScale", "RoleData");
            _off_RoleData_InflictDamage = ResolveOffset(roleDataClass, "InflictDamage", "RoleData");
            _off_RoleData_InflictSuppression = ResolveOffset(roleDataClass, "InflictSuppression", "RoleData");
        }

        // ── SkillBehavior ──
        IntPtr skillBehaviorClass = GetIl2CppClass("Menace.Tactical.AI.SkillBehavior");
        if (skillBehaviorClass != IntPtr.Zero)
            _off_SkillBehavior_m_TargetTile = ResolveOffset(skillBehaviorClass, "m_TargetTile", "SkillBehavior");

        // ── Attack ──
        IntPtr attackClass = GetIl2CppClass("Menace.Tactical.AI.Behaviors.Attack");
        if (attackClass != IntPtr.Zero)
        {
            _off_Attack_m_Goal = ResolveOffset(attackClass, "m_Goal", "Attack");
            _cachedAttackKlass = attackClass; // Cache for IsAttackBehavior
        }

        // ── TileScore ──
        IntPtr tileScoreClass = GetIl2CppClass("Menace.Tactical.AI.Data.TileScore");
        if (tileScoreClass != IntPtr.Zero)
            _off_TileScore_UtilityScore = ResolveOffset(tileScoreClass, "UtilityScore", "TileScore");

        // ── Il2Cpp collection internals (resolve from concrete generic fields) ──
        // Open generic types (List`1, Dictionary`2) have offset 0 for all fields.
        // Instead, get the concrete generic class via the field type of a known field.
        if (agentClass != IntPtr.Zero)
        {
            // List<T> from Agent.m_Behaviors (which is List<Behavior>)
            IntPtr behaviorsField = IL2CPP.il2cpp_class_get_field_from_name(agentClass, "m_Behaviors");
            if (behaviorsField != IntPtr.Zero)
            {
                IntPtr fieldType = IL2CPP.il2cpp_field_get_type(behaviorsField);
                if (fieldType != IntPtr.Zero)
                {
                    IntPtr concreteListClass = IL2CPP.il2cpp_class_from_il2cpp_type(fieldType);
                    if (concreteListClass != IntPtr.Zero)
                    {
                        Log?.Msg($"[CombinedArms] Resolved concrete List<Behavior> → 0x{concreteListClass.ToInt64():X}");
                        _off_List_items = ResolveOffset(concreteListClass, "_items", "List<Behavior>");
                        _off_List_size = ResolveOffset(concreteListClass, "_size", "List<Behavior>");
                    }
                }
            }

            // Dictionary<K,V> from Agent.m_Tiles (which is Dictionary<Tile, TileScore>)
            IntPtr tilesField = IL2CPP.il2cpp_class_get_field_from_name(agentClass, "m_Tiles");
            if (tilesField != IntPtr.Zero)
            {
                IntPtr fieldType = IL2CPP.il2cpp_field_get_type(tilesField);
                if (fieldType != IntPtr.Zero)
                {
                    IntPtr concreteDictClass = IL2CPP.il2cpp_class_from_il2cpp_type(fieldType);
                    if (concreteDictClass != IntPtr.Zero)
                    {
                        Log?.Msg($"[CombinedArms] Resolved concrete Dictionary<Tile,TileScore> → 0x{concreteDictClass.ToInt64():X}");
                        _off_Dict_entries = ResolveOffset(concreteDictClass, "_entries", "Dict<Tile,TileScore>");
                        _off_Dict_count = ResolveOffset(concreteDictClass, "_count", "Dict<Tile,TileScore>");

                        // Resolve actual dictionary entry struct size from IL2CPP metadata
                        try
                        {
                            IntPtr entriesField2 = IL2CPP.il2cpp_class_get_field_from_name(concreteDictClass, "_entries");
                            if (entriesField2 != IntPtr.Zero)
                            {
                                IntPtr entriesFieldType2 = IL2CPP.il2cpp_field_get_type(entriesField2);
                                IntPtr entriesArrayClass = IL2CPP.il2cpp_class_from_il2cpp_type(entriesFieldType2);
                                if (entriesArrayClass != IntPtr.Zero)
                                {
                                    IntPtr entryClass = IL2CPP.il2cpp_class_get_element_class(entriesArrayClass);
                                    if (entryClass != IntPtr.Zero)
                                    {
                                        int entryInstanceSize = (int)IL2CPP.il2cpp_class_instance_size(entryClass);
                                        // Value types inline in arrays don't have the object header (2*IntPtr)
                                        _dictEntrySize = entryInstanceSize - IntPtr.Size * 2;
                                        int expectedInline = 4 + 4 + IntPtr.Size + IntPtr.Size;
                                        Log?.Msg($"[CombinedArms] Dict Entry: resolved stride={_dictEntrySize} " +
                                                 $"expected={expectedInline} " +
                                                 $"(instanceSize={entryInstanceSize})");
                                        if (_dictEntrySize < expectedInline)
                                        {
                                            Log?.Error($"[CombinedArms] Dict entry size too small ({_dictEntrySize} < {expectedInline}) " +
                                                       $"— disabling dictionary iteration to prevent crash");
                                            _dictEntrySize = 0;
                                        }
                                        else if (_dictEntrySize != expectedInline)
                                        {
                                            Log?.Warning($"[CombinedArms] Dict entry has padding: " +
                                                         $"stride={_dictEntrySize} vs expected={expectedInline}. " +
                                                         $"Using resolved stride.");
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log?.Warning($"[CombinedArms] Dict entry size resolution failed: {ex.Message}");
                        }
                    }
                }
            }
        }

        // ── Determine feature availability ──
        _turnResetAvailable =
            _off_BaseFaction_m_FactionIndex != 0;

        _sequencingAvailable =
            _off_Agent_m_Faction != 0 &&
            _off_BaseFaction_m_FactionIndex != 0 &&
            _off_RoleData_InflictDamage != 0 &&
            _off_RoleData_InflictSuppression != 0;

        _focusFireAvailable =
            _off_Agent_m_Faction != 0 &&
            _off_BaseFaction_m_FactionIndex != 0 &&
            _off_Agent_m_Behaviors != 0 &&
            _off_SkillBehavior_m_TargetTile != 0 &&
            _off_List_items != 0 &&
            _off_List_size != 0;

        _executionTrackingAvailable =
            _off_Agent_m_Faction != 0 &&
            _off_BaseFaction_m_FactionIndex != 0 &&
            _off_Agent_m_Actor != 0 &&
            _off_Agent_m_ActiveBehavior != 0 &&
            _off_SkillBehavior_m_TargetTile != 0 &&
            _off_Attack_m_Goal != 0;

        _cofAvailable =
            _off_Agent_m_Faction != 0 &&
            _off_BaseFaction_m_FactionIndex != 0 &&
            _off_Agent_m_Tiles != 0 &&
            _off_TileScore_UtilityScore != 0 &&
            _off_Dict_entries != 0 &&
            _off_Dict_count != 0 &&
            _dictEntrySize > 0;

        _formationDepthAvailable =
            _off_Agent_m_Faction != 0 &&
            _off_BaseFaction_m_FactionIndex != 0 &&
            _off_Agent_m_Tiles != 0 &&
            _off_TileScore_UtilityScore != 0 &&
            _off_Dict_entries != 0 &&
            _off_Dict_count != 0 &&
            _dictEntrySize > 0 &&
            _off_AIFaction_m_Opponents != 0 &&
            _off_Opponent_Actor != 0 &&
            _off_List_items != 0 &&
            _off_List_size != 0 &&
            _off_RoleData_Move != 0 &&
            _off_RoleData_SafetyScale != 0 &&
            _off_RoleData_InflictDamage != 0 &&
            _off_RoleData_InflictSuppression != 0;
    }

    // ═══════════════════════════════════════════════════════
    //  Harmony patch registration
    // ═══════════════════════════════════════════════════════

    private void PatchOnTurnStart(Assembly asm)
    {
        var type = FindManagedType("Menace.Tactical.AI.AIFaction", "AIFaction");

        if (type == null) { Log.Warning("AIFaction not found"); return; }

        var method = type.GetMethod("OnTurnStart",
            BindingFlags.Public | BindingFlags.Instance);

        if (method == null) { Log.Warning("AIFaction.OnTurnStart() not found"); return; }

        try
        {
            _harmony.Patch(method,
                postfix: new HarmonyMethod(typeof(CombinedArmsMod),
                    nameof(OnTurnStart_Postfix)));
            Log.Msg("Patched AIFaction.OnTurnStart()");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to patch AIFaction.OnTurnStart(): {ex.Message}");
        }
    }

    private void PatchGetScoreMult(Assembly asm)
    {
        var type = FindManagedType("Menace.Tactical.AI.Agent", "Agent");

        if (type == null) { Log.Warning("Agent not found"); return; }

        var method = type.GetMethod("GetScoreMultForPickingThisAgent",
            BindingFlags.Public | BindingFlags.Instance);

        if (method == null) { Log.Warning("Agent.GetScoreMultForPickingThisAgent() not found"); return; }

        try
        {
            _harmony.Patch(method,
                postfix: new HarmonyMethod(typeof(CombinedArmsMod),
                    nameof(GetScoreMult_Postfix)));
            Log.Msg("Patched Agent.GetScoreMultForPickingThisAgent()");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to patch GetScoreMultForPickingThisAgent: {ex.Message}");
        }
    }

    private void PatchExecute(Assembly asm)
    {
        var type = FindManagedType("Menace.Tactical.AI.Agent", "Agent");

        if (type == null) return;

        var method = type.GetMethod("Execute",
            BindingFlags.Public | BindingFlags.Instance);

        if (method == null) { Log.Warning("Agent.Execute() not found"); return; }

        try
        {
            _harmony.Patch(method,
                postfix: new HarmonyMethod(typeof(CombinedArmsMod),
                    nameof(Execute_Postfix)));
            Log.Msg("Patched Agent.Execute()");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to patch Agent.Execute(): {ex.Message}");
        }
    }

    private void PatchPostProcessTileScores(Assembly asm)
    {
        var type = FindManagedType("Menace.Tactical.AI.Agent", "Agent");

        if (type == null) return;

        // Il2CppInterop exposes private methods as public on the proxy type
        var method = type.GetMethod("PostProcessTileScores",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (method == null)
        {
            // Enumerate methods to find partial match
            var candidates = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name.Contains("PostProcess") || m.Name.Contains("TileScore"))
                .Select(m => m.Name)
                .ToList();
            Log.Warning($"Agent.PostProcessTileScores() not found. Candidates: {string.Join(", ", candidates)}");
            return;
        }

        try
        {
            _harmony.Patch(method,
                postfix: new HarmonyMethod(typeof(CombinedArmsMod),
                    nameof(PostProcessTileScores_Postfix)));
            Log.Msg("Patched Agent.PostProcessTileScores()");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to patch PostProcessTileScores: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Hook 1 + 5: Turn State Reset & Position Archiving
    // ═══════════════════════════════════════════════════════

    public static void OnTurnStart_Postfix(object __instance)
    {
        if (!CoordinationState.Enabled) return;

        try
        {
            if (!_loggedFirstTurnStart)
            {
                _loggedFirstTurnStart = true;
                Log?.Msg("[CombinedArms] OnTurnStart_Postfix first invocation");
            }

            if (__instance is not Il2CppObjectBase il2cppObj) return;

            int factionIndex = Marshal.ReadInt32(il2cppObj.Pointer + (int)_off_BaseFaction_m_FactionIndex);

            var state = CoordinationState.GetOrCreate(factionIndex);

            // Hook 5: Archive positions before reset
            state.ArchivePositions();
            state.Reset();
        }
        catch (Exception ex)
        {
            Log?.Error($"[CombinedArms] OnTurnStart error: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Hook 2: Agent Sequencing + Focus Fire Priority
    // ═══════════════════════════════════════════════════════

    public static void GetScoreMult_Postfix(object __instance, ref float __result)
    {
        if (!CoordinationState.Enabled) return;

        try
        {
            if (!_loggedFirstScoreMult)
            {
                _loggedFirstScoreMult = true;
                Log?.Msg("[CombinedArms] GetScoreMult_Postfix first invocation");
            }

            if (__instance is not Il2CppObjectBase agentObj) return;

            var config = CoordinationState.Config;
            IntPtr agentPtr = agentObj.Pointer;

            IntPtr factionPtr = Marshal.ReadIntPtr(agentPtr + (int)_off_Agent_m_Faction);
            if (factionPtr == IntPtr.Zero) return;
            int factionIndex = Marshal.ReadInt32(factionPtr + (int)_off_BaseFaction_m_FactionIndex);

            var state = CoordinationState.GetOrCreate(factionIndex);

            // ── Agent Sequencing ──
            if (config.EnableAgentSequencing && _sequencingAvailable)
            {
                float suppressionWeight = 0f;
                float damageWeight = 0f;

                if (_getRoleMethod != null)
                {
                    try
                    {
                        var roleObj = _getRoleMethod.Invoke(__instance, null);
                        if (roleObj is Il2CppObjectBase roleIl2Cpp && roleIl2Cpp.Pointer != IntPtr.Zero)
                        {
                            damageWeight = ReadFloat(roleIl2Cpp.Pointer + (int)_off_RoleData_InflictDamage);
                            suppressionWeight = ReadFloat(roleIl2Cpp.Pointer + (int)_off_RoleData_InflictSuppression);
                        }
                    }
                    catch
                    {
                        // Role method failed, skip sequencing for this agent
                    }
                }

                bool isSuppressor = suppressionWeight > damageWeight && suppressionWeight > 0f;
                bool isDamageDealer = damageWeight > suppressionWeight && damageWeight > 0f;

                if (!state.HasSuppressorActed)
                {
                    if (isSuppressor)
                    {
                        __result *= config.SuppressorPriorityBoost;
                        if (!_loggedFirstSequencingEffect)
                        {
                            _loggedFirstSequencingEffect = true;
                            Log?.Msg($"[CombinedArms] Sequencing ACTIVE — suppressor boost {config.SuppressorPriorityBoost}x applied");
                        }
                    }
                    else if (isDamageDealer)
                    {
                        __result *= config.DamageDealerPenalty;
                        if (!_loggedFirstSequencingEffect)
                        {
                            _loggedFirstSequencingEffect = true;
                            Log?.Msg($"[CombinedArms] Sequencing ACTIVE — damage dealer penalty {config.DamageDealerPenalty}x applied");
                        }
                    }
                }
            }

            // ── Focus Fire ──
            if (config.EnableFocusFire && _focusFireAvailable && state.CompletedActions.Count > 0)
            {
                IntPtr behaviorsListPtr = Marshal.ReadIntPtr(agentPtr + (int)_off_Agent_m_Behaviors);
                if (behaviorsListPtr != IntPtr.Zero)
                {
                    bool targetsEngagedTile = CheckTargetsEngagedTile(behaviorsListPtr, state);
                    if (targetsEngagedTile)
                    {
                        __result *= config.FocusFirePickingBoost;
                        if (!_loggedFirstFocusFireEffect)
                        {
                            _loggedFirstFocusFireEffect = true;
                            Log?.Msg($"[CombinedArms] Focus Fire ACTIVE — picking boost {config.FocusFirePickingBoost}x applied");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log?.Error($"[CombinedArms] GetScoreMult error: {ex.Message}");
        }
    }

    private static bool CheckTargetsEngagedTile(IntPtr behaviorsListPtr, FactionTurnState state)
    {
        try
        {
            IntPtr itemsArrayPtr = Marshal.ReadIntPtr(behaviorsListPtr + (int)_off_List_items);
            int count = Marshal.ReadInt32(behaviorsListPtr + (int)_off_List_size);

            if (itemsArrayPtr == IntPtr.Zero || count <= 0) return false;
            if (count > 20) count = 20;

            // Validate against IL2CPP array max_length
            long arrayMaxLength = Marshal.ReadIntPtr(itemsArrayPtr + IntPtr.Size * 3).ToInt64();
            if (count > arrayMaxLength)
                count = (int)Math.Min(arrayMaxLength, 20);
            if (count <= 0) return false;

            // Il2CppArray element data starts after header:
            // 2*IntPtr (obj header) + IntPtr (bounds) + IntPtr (length padded)
            int elementsOffset = IntPtr.Size * 4;

            for (int i = 0; i < count; i++)
            {
                IntPtr behaviorPtr = Marshal.ReadIntPtr(itemsArrayPtr + elementsOffset + i * IntPtr.Size);
                if (behaviorPtr == IntPtr.Zero) continue;

                if (!IsAttackBehavior(behaviorPtr)) continue;

                IntPtr targetTilePtr = Marshal.ReadIntPtr(behaviorPtr + (int)_off_SkillBehavior_m_TargetTile);
                if (targetTilePtr == IntPtr.Zero) continue;

                if (state.TargetedTileCount.ContainsKey(targetTilePtr))
                    return true;
            }
        }
        catch
        {
            // Silently fail
        }

        return false;
    }

    private static bool IsAttackBehavior(IntPtr behaviorPtr)
    {
        if (_cachedAttackKlass == IntPtr.Zero) return false;

        try
        {
            IntPtr klass = IL2CPP.il2cpp_object_get_class(behaviorPtr);
            if (klass == IntPtr.Zero) return false;

            return IL2CPP.il2cpp_class_is_assignable_from(_cachedAttackKlass, klass);
        }
        catch
        {
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Hook 3: Execution Tracking
    // ═══════════════════════════════════════════════════════

    public static void Execute_Postfix(object __instance, bool __result)
    {
        if (!CoordinationState.Enabled) return;

        try
        {
            if (!_loggedFirstExecute)
            {
                _loggedFirstExecute = true;
                Log?.Msg("[CombinedArms] Execute_Postfix first invocation");
            }

            if (__instance is not Il2CppObjectBase agentObj) return;

            IntPtr agentPtr = agentObj.Pointer;

            IntPtr factionPtr = Marshal.ReadIntPtr(agentPtr + (int)_off_Agent_m_Faction);
            if (factionPtr == IntPtr.Zero) return;
            int factionIndex = Marshal.ReadInt32(factionPtr + (int)_off_BaseFaction_m_FactionIndex);

            var state = CoordinationState.GetOrCreate(factionIndex);

            IntPtr actorPtr = Marshal.ReadIntPtr(agentPtr + (int)_off_Agent_m_Actor);
            IntPtr activeBehaviorPtr = Marshal.ReadIntPtr(agentPtr + (int)_off_Agent_m_ActiveBehavior);

            var action = new AgentAction
            {
                ActorPtr = actorPtr,
                BehaviorGoal = -1
            };

            if (activeBehaviorPtr != IntPtr.Zero && IsAttackBehavior(activeBehaviorPtr))
            {
                action.BehaviorGoal = Marshal.ReadInt32(activeBehaviorPtr + (int)_off_Attack_m_Goal);

                IntPtr targetTilePtr = Marshal.ReadIntPtr(activeBehaviorPtr + (int)_off_SkillBehavior_m_TargetTile);
                action.TargetTilePtr = targetTilePtr;

                if (targetTilePtr != IntPtr.Zero)
                {
                    if (state.TargetedTileCount.ContainsKey(targetTilePtr))
                        state.TargetedTileCount[targetTilePtr]++;
                    else
                        state.TargetedTileCount[targetTilePtr] = 1;
                }

                // Goal 1 = Suppression
                if (action.BehaviorGoal == 1)
                    state.HasSuppressorActed = true;
            }

            if (actorPtr != IntPtr.Zero)
            {
                try
                {
                    var tilePos = GetActorTilePosition(actorPtr);
                    action.TileX = tilePos.x;
                    action.TileZ = tilePos.z;
                    state.ActedAllyPositions[actorPtr] = (tilePos.x, tilePos.z);
                }
                catch { }
            }

            state.CompletedActions.Add(action);

            if (!_loggedFirstExecuteTrack)
            {
                _loggedFirstExecuteTrack = true;
                Log?.Msg($"[CombinedArms] Execution Tracking ACTIVE — recording agent actions (faction {factionIndex})");
            }
        }
        catch (Exception ex)
        {
            Log?.Error($"[CombinedArms] Execute tracking error: {ex.Message}");
        }
    }

    private static (int x, int z) GetActorTilePosition(IntPtr actorPtr)
    {
        try
        {
            if (actorPtr == IntPtr.Zero) return (0, 0);

            // Validate pointer is a valid IL2CPP object before creating Component wrapper
            IntPtr klass = IL2CPP.il2cpp_object_get_class(actorPtr);
            if (klass == IntPtr.Zero) return (0, 0);

            // Actor extends MonoBehaviour → Component — use Unity interop for transform
            var actorComponent = new UnityEngine.Component(actorPtr);
            var transform = actorComponent.transform;
            if (transform != null)
            {
                var pos = transform.position;
                return ((int)Math.Round(pos.x), (int)Math.Round(pos.z));
            }
        }
        catch { }

        return (0, 0);
    }

    // ═══════════════════════════════════════════════════════
    //  Hook 4: Center of Forces
    // ═══════════════════════════════════════════════════════

    public static void PostProcessTileScores_Postfix(object __instance)
    {
        if (!CoordinationState.Enabled) return;

        var config = CoordinationState.Config;
        bool wantCoF = config.EnableCenterOfForces && _cofAvailable;
        bool wantDepth = config.EnableFormationDepth && _formationDepthAvailable;
        if (!wantCoF && !wantDepth) return;

        try
        {
            if (!_loggedFirstTileScores)
            {
                _loggedFirstTileScores = true;
                Log?.Msg("[CombinedArms] PostProcessTileScores_Postfix first invocation");
            }

            if (__instance is not Il2CppObjectBase agentObj) return;

            IntPtr agentPtr = agentObj.Pointer;

            IntPtr factionPtr = Marshal.ReadIntPtr(agentPtr + (int)_off_Agent_m_Faction);
            if (factionPtr == IntPtr.Zero) return;
            int factionIndex = Marshal.ReadInt32(factionPtr + (int)_off_BaseFaction_m_FactionIndex);

            var state = CoordinationState.GetOrCreate(factionIndex);

            IntPtr tilesPtr = Marshal.ReadIntPtr(agentPtr + (int)_off_Agent_m_Tiles);
            if (tilesPtr == IntPtr.Zero) return;

            // ── Center of Forces ──
            if (wantCoF && state.PreviousTurnAllyPositions.Count >= config.CenterOfForcesMinAllies)
            {
                float centroidX = 0f, centroidZ = 0f;
                foreach (var pos in state.PreviousTurnAllyPositions.Values)
                {
                    centroidX += pos.x;
                    centroidZ += pos.z;
                }
                int allyCount = state.PreviousTurnAllyPositions.Count;
                centroidX /= allyCount;
                centroidZ /= allyCount;

                IterateTileScoresAndApplyCoF(tilesPtr, centroidX, centroidZ, config);

                if (!_loggedFirstCofEffect)
                {
                    _loggedFirstCofEffect = true;
                    Log?.Msg($"[CombinedArms] Center of Forces ACTIVE — centroid=({centroidX:F1},{centroidZ:F1}) allies={allyCount}");
                }
            }

            // ── Formation Depth ──
            if (wantDepth)
            {
                EnsureDepthCache(state, factionPtr, config);

                if (state.DepthCache.EnemyCentroids.Count > 0)
                {
                    FormationBand band = ClassifyAgentBand(__instance);

                    IterateTileScoresAndApplyFormationDepth(
                        tilesPtr, state.DepthCache, band, config);

                    if (!_loggedFirstDepthEffect)
                    {
                        _loggedFirstDepthEffect = true;
                        var c = state.DepthCache.EnemyCentroids[0];
                        Log?.Msg($"[CombinedArms] Formation Depth ACTIVE — band={band} enemyCentroid=({c.x:F1},{c.z:F1})");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log?.Error($"[CombinedArms] PostProcessTileScores error: {ex.Message}");
        }
    }

    private static void IterateTileScoresAndApplyCoF(
        IntPtr dictPtr, float centroidX, float centroidZ, CombinedArmsConfig config)
    {
        try
        {
            IntPtr entriesArrayPtr = Marshal.ReadIntPtr(dictPtr + (int)_off_Dict_entries);
            int count = Marshal.ReadInt32(dictPtr + (int)_off_Dict_count);

            if (entriesArrayPtr == IntPtr.Zero || count <= 0 || _dictEntrySize <= 0) return;
            if (count > 200) count = 200;

            // Validate against IL2CPP array max_length (at offset 3*IntPtr in array header)
            long arrayMaxLength = Marshal.ReadIntPtr(entriesArrayPtr + IntPtr.Size * 3).ToInt64();
            if (count > arrayMaxLength)
                count = (int)Math.Min(arrayMaxLength, 200);
            if (count <= 0) return;

            // Il2CppArray header: 2*IntPtr (obj header) + IntPtr (bounds) + IntPtr (length padded)
            int arrayHeaderSize = IntPtr.Size * 4;

            // Entry stride resolved from IL2CPP metadata at startup
            int entrySize = _dictEntrySize;

            for (int i = 0; i < count; i++)
            {
                IntPtr entryAddr = entriesArrayPtr + arrayHeaderSize + i * entrySize;

                int hashCode = Marshal.ReadInt32(entryAddr);
                if (hashCode < 0) continue; // unused slot

                IntPtr tilePtr = Marshal.ReadIntPtr(entryAddr + 8);
                IntPtr tileScorePtr = Marshal.ReadIntPtr(entryAddr + 8 + IntPtr.Size);

                if (tilePtr == IntPtr.Zero || tileScorePtr == IntPtr.Zero) continue;

                // Validate tile pointer is a valid IL2CPP object before creating Component wrapper
                IntPtr tileKlass = IL2CPP.il2cpp_object_get_class(tilePtr);
                if (tileKlass == IntPtr.Zero) continue;

                float tileX, tileZ;
                try
                {
                    var tileComponent = new UnityEngine.Component(tilePtr);
                    var transform = tileComponent.transform;
                    if (transform == null) continue;
                    var pos = transform.position;
                    tileX = pos.x;
                    tileZ = pos.z;
                }
                catch { continue; }

                float dx = tileX - centroidX;
                float dz = tileZ - centroidZ;
                float dist = (float)Math.Sqrt(dx * dx + dz * dz);

                if (dist >= config.CenterOfForcesMaxRange) continue;

                float normalizedProximity = 1.0f - (dist / config.CenterOfForcesMaxRange);
                float bonus = config.CenterOfForcesWeight * normalizedProximity;

                float currentUtility = ReadFloat(tileScorePtr + (int)_off_TileScore_UtilityScore);
                float newUtility = currentUtility + bonus;
                WriteFloat(tileScorePtr + (int)_off_TileScore_UtilityScore, newUtility);
            }
        }
        catch (Exception ex)
        {
            Log?.Error($"[CombinedArms] Tile iteration error: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Formation Depth
    // ═══════════════════════════════════════════════════════

    private static void EnsureDepthCache(FactionTurnState state, IntPtr factionPtr, CombinedArmsConfig config)
    {
        if (state.DepthCache.IsValid) return;
        state.DepthCache.IsValid = true;

        TryComputeEnemyCentroids(state.DepthCache, factionPtr, config);
        ComputeBandEdges(state.DepthCache, config);
    }

    private static void TryComputeEnemyCentroids(FormationDepthCache cache, IntPtr factionPtr, CombinedArmsConfig config)
    {
        try
        {
            // Verify the faction is actually an AIFaction before reading AIFaction-specific fields.
            // Agent.m_Faction is typed as BaseFaction — if the runtime object is a PlayerFaction
            // or other subclass, reading m_Opponents at the AIFaction offset would be invalid.
            if (_cachedAIFactionKlass != IntPtr.Zero)
            {
                IntPtr factionClass = IL2CPP.il2cpp_object_get_class(factionPtr);
                if (factionClass == IntPtr.Zero ||
                    !IL2CPP.il2cpp_class_is_assignable_from(_cachedAIFactionKlass, factionClass))
                {
                    return;
                }
            }

            IntPtr opponentsListPtr = Marshal.ReadIntPtr(factionPtr + (int)_off_AIFaction_m_Opponents);
            if (opponentsListPtr == IntPtr.Zero) return;

            IntPtr itemsArrayPtr = Marshal.ReadIntPtr(opponentsListPtr + (int)_off_List_items);
            int count = Marshal.ReadInt32(opponentsListPtr + (int)_off_List_size);

            if (itemsArrayPtr == IntPtr.Zero || count < config.FormationDepthMinOpponents) return;
            if (count > 100) count = 100;

            // Validate against IL2CPP array max_length
            long arrayMaxLength = Marshal.ReadIntPtr(itemsArrayPtr + IntPtr.Size * 3).ToInt64();
            if (count > arrayMaxLength)
                count = (int)Math.Min(arrayMaxLength, 100);
            if (count < config.FormationDepthMinOpponents) return;

            int elementsOffset = IntPtr.Size * 4;

            float sumX = 0f, sumZ = 0f;
            int validCount = 0;

            for (int i = 0; i < count; i++)
            {
                IntPtr opponentPtr = Marshal.ReadIntPtr(itemsArrayPtr + elementsOffset + i * IntPtr.Size);
                if (opponentPtr == IntPtr.Zero) continue;

                IntPtr actorPtr = Marshal.ReadIntPtr(opponentPtr + (int)_off_Opponent_Actor);
                if (actorPtr == IntPtr.Zero) continue;

                // Validate actor is a valid IL2CPP object before creating Component wrapper
                IntPtr actorKlass = IL2CPP.il2cpp_object_get_class(actorPtr);
                if (actorKlass == IntPtr.Zero) continue;

                try
                {
                    var actorComponent = new UnityEngine.Component(actorPtr);
                    var transform = actorComponent.transform;
                    if (transform == null) continue;
                    var pos = transform.position;
                    sumX += pos.x;
                    sumZ += pos.z;
                    validCount++;
                }
                catch { continue; }
            }

            if (validCount >= config.FormationDepthMinOpponents)
            {
                cache.EnemyCentroids.Add((sumX / validCount, sumZ / validCount));
            }
        }
        catch (Exception ex)
        {
            Log?.Error($"[CombinedArms] TryComputeEnemyCentroids error: {ex.Message}");
        }
    }

    private static void ComputeBandEdges(FormationDepthCache cache, CombinedArmsConfig config)
    {
        float maxRange = config.FormationDepthMaxRange;
        cache.BandEdges[0] = 0f;
        cache.BandEdges[1] = maxRange * config.FrontlineFraction;
        cache.BandEdges[2] = maxRange * (config.FrontlineFraction + config.MidlineFraction);
        cache.BandEdges[3] = maxRange;
    }

    private static FormationBand ClassifyAgentBand(object agentInstance)
    {
        if (_getRoleMethod == null) return FormationBand.Midline;

        try
        {
            var roleObj = _getRoleMethod.Invoke(agentInstance, null);
            if (roleObj is not Il2CppObjectBase roleIl2Cpp || roleIl2Cpp.Pointer == IntPtr.Zero)
                return FormationBand.Midline;

            float move = ReadFloat(roleIl2Cpp.Pointer + (int)_off_RoleData_Move);
            float damage = ReadFloat(roleIl2Cpp.Pointer + (int)_off_RoleData_InflictDamage);
            float suppression = ReadFloat(roleIl2Cpp.Pointer + (int)_off_RoleData_InflictSuppression);
            float safety = ReadFloat(roleIl2Cpp.Pointer + (int)_off_RoleData_SafetyScale);

            float frontlineScore = move * 0.4f + damage * 0.6f;
            float midlineScore = suppression;
            float backlineScore = safety;

            if (frontlineScore >= midlineScore && frontlineScore >= backlineScore)
                return FormationBand.Frontline;
            if (midlineScore >= backlineScore)
                return FormationBand.Midline;
            return FormationBand.Backline;
        }
        catch
        {
            return FormationBand.Midline;
        }
    }

    private static float ComputeDepthScore(float distance, FormationBand band, FormationDepthCache cache, CombinedArmsConfig config)
    {
        float maxRange = config.FormationDepthMaxRange;
        float d = Math.Max(0f, Math.Min(distance, maxRange));
        int bandIndex = (int)band;
        float bandCenter = (cache.BandEdges[bandIndex] + cache.BandEdges[bandIndex + 1]) / 2f;
        return 1.0f - 2.0f * Math.Abs(d - bandCenter) / maxRange;
    }

    private static void IterateTileScoresAndApplyFormationDepth(
        IntPtr dictPtr, FormationDepthCache cache, FormationBand band, CombinedArmsConfig config)
    {
        try
        {
            IntPtr entriesArrayPtr = Marshal.ReadIntPtr(dictPtr + (int)_off_Dict_entries);
            int count = Marshal.ReadInt32(dictPtr + (int)_off_Dict_count);

            if (entriesArrayPtr == IntPtr.Zero || count <= 0 || _dictEntrySize <= 0) return;
            if (count > 200) count = 200;

            // Validate against IL2CPP array max_length
            long arrayMaxLength = Marshal.ReadIntPtr(entriesArrayPtr + IntPtr.Size * 3).ToInt64();
            if (count > arrayMaxLength)
                count = (int)Math.Min(arrayMaxLength, 200);
            if (count <= 0) return;

            int arrayHeaderSize = IntPtr.Size * 4;
            int entrySize = _dictEntrySize;

            for (int i = 0; i < count; i++)
            {
                IntPtr entryAddr = entriesArrayPtr + arrayHeaderSize + i * entrySize;

                int hashCode = Marshal.ReadInt32(entryAddr);
                if (hashCode < 0) continue;

                IntPtr tilePtr = Marshal.ReadIntPtr(entryAddr + 8);
                IntPtr tileScorePtr = Marshal.ReadIntPtr(entryAddr + 8 + IntPtr.Size);

                if (tilePtr == IntPtr.Zero || tileScorePtr == IntPtr.Zero) continue;

                // Validate tile pointer is a valid IL2CPP object
                IntPtr tileKlass = IL2CPP.il2cpp_object_get_class(tilePtr);
                if (tileKlass == IntPtr.Zero) continue;

                float tileX, tileZ;
                try
                {
                    var tileComponent = new UnityEngine.Component(tilePtr);
                    var transform = tileComponent.transform;
                    if (transform == null) continue;
                    var pos = transform.position;
                    tileX = pos.x;
                    tileZ = pos.z;
                }
                catch { continue; }

                // Find distance to nearest enemy centroid
                float minDist = float.MaxValue;
                foreach (var centroid in cache.EnemyCentroids)
                {
                    float dx = tileX - centroid.x;
                    float dz = tileZ - centroid.z;
                    float dist = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (dist < minDist) minDist = dist;
                }

                float score = ComputeDepthScore(minDist, band, cache, config);
                float delta = score * config.FormationDepthWeight;

                float currentUtility = ReadFloat(tileScorePtr + (int)_off_TileScore_UtilityScore);
                WriteFloat(tileScorePtr + (int)_off_TileScore_UtilityScore, currentUtility + delta);
            }
        }
        catch (Exception ex)
        {
            Log?.Error($"[CombinedArms] Formation depth tile iteration error: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════

    private static float ReadFloat(IntPtr addr)
    {
        int raw = Marshal.ReadInt32(addr);
        return BitConverter.ToSingle(BitConverter.GetBytes(raw), 0);
    }

    private static void WriteFloat(IntPtr addr, float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        int raw = BitConverter.ToInt32(bytes, 0);
        Marshal.WriteInt32(addr, raw);
    }
}
