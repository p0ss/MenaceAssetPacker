using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;

[assembly: MelonInfo(typeof(Menace.BalanceMod.BalanceModMod), "Menace Balance Mod", "1.0.0", "Menace Modkit")]
[assembly: MelonGame(null, null)]

namespace Menace.BalanceMod;

public class BalanceModMod : MelonMod
{
    private static MelonLogger.Instance Log;
    private HarmonyLib.Harmony _harmony;
    private bool _patchesApplied;

    // Reflection cache for SuppressionHandler.m_TurnsStunned
    private static FieldInfo _turnsStunnedField;
    private static PropertyInfo _turnsStunnedProp;

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        Log.Msg("Menace Balance Mod v1.0.0");
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
        // Wait a few frames for types to be initialized
        for (int i = 0; i < 10; i++)
            yield return null;

        ApplyPatches();
    }

    private void ApplyPatches()
    {
        if (_patchesApplied) return;

        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (gameAssembly == null)
        {
            Log.Error("Assembly-CSharp not found");
            return;
        }

        _harmony = new HarmonyLib.Harmony("com.menacemodkit.balance");

        PatchCrawlEnabled(gameAssembly);
        PatchSuppressionStateChanged(gameAssembly);
        PatchSuppressionRoundStart(gameAssembly);

        _patchesApplied = true;
        Log.Msg("All balance patches applied");
    }

    private void PatchCrawlEnabled(Assembly gameAssembly)
    {
        var type = gameAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "CrawlHandler");

        if (type == null)
        {
            Log.Warning("CrawlHandler type not found");
            return;
        }

        var method = type.GetMethod("IsEnabled",
            BindingFlags.Public | BindingFlags.Instance);

        if (method == null)
        {
            Log.Warning("CrawlHandler.IsEnabled() not found");
            return;
        }

        try
        {
            _harmony.Patch(method,
                postfix: new HarmonyMethod(typeof(BalanceModMod),
                    nameof(CrawlIsEnabled_Postfix)));
            Log.Msg("Patched CrawlHandler.IsEnabled()");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to patch CrawlHandler.IsEnabled(): {ex.Message}");
        }
    }

    private void PatchSuppressionStateChanged(Assembly gameAssembly)
    {
        var type = gameAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "SuppressionHandler");

        if (type == null)
        {
            Log.Warning("SuppressionHandler type not found");
            return;
        }

        // Cache m_TurnsStunned accessor
        _turnsStunnedField = type.GetField("m_TurnsStunned",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        _turnsStunnedProp = type.GetProperty("m_TurnsStunned",
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

        if (_turnsStunnedField != null)
            Log.Msg("Found m_TurnsStunned as field");
        else if (_turnsStunnedProp != null)
            Log.Msg("Found m_TurnsStunned as property");
        else
        {
            Log.Warning("m_TurnsStunned not found via reflection, will use IL2CPP offset 0x24");
        }

        // Patch OnSuppressionStateChanged to cap initial stun at 1
        var stateChangedMethod = type.GetMethod("OnSuppressionStateChanged",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (stateChangedMethod != null)
        {
            try
            {
                _harmony.Patch(stateChangedMethod,
                    postfix: new HarmonyMethod(typeof(BalanceModMod),
                        nameof(OnSuppressionStateChanged_Postfix)));
                Log.Msg("Patched SuppressionHandler.OnSuppressionStateChanged()");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to patch OnSuppressionStateChanged: {ex.Message}");
            }
        }
        else
        {
            Log.Warning("SuppressionHandler.OnSuppressionStateChanged() not found");
        }
    }

    private void PatchSuppressionRoundStart(Assembly gameAssembly)
    {
        var type = gameAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "SuppressionHandler");

        if (type == null) return;

        var method = type.GetMethod("OnRoundStart",
            BindingFlags.Public | BindingFlags.Instance);

        if (method == null)
        {
            Log.Warning("SuppressionHandler.OnRoundStart() not found");
            return;
        }

        try
        {
            _harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(BalanceModMod),
                    nameof(OnRoundStart_Prefix)));
            Log.Msg("Patched SuppressionHandler.OnRoundStart()");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to patch OnRoundStart: {ex.Message}");
        }
    }

    // --- Harmony Patch Methods ---

    public static void CrawlIsEnabled_Postfix(ref bool __result)
    {
        if (!__result)
        {
            __result = true;
            Log?.Msg("[CrawlPatch] Crawl re-enabled for pinned unit");
        }
    }

    public static void OnSuppressionStateChanged_Postfix(object __instance)
    {
        try
        {
            int turnsStunned = GetTurnsStunned(__instance);
            if (turnsStunned > 1)
            {
                SetTurnsStunned(__instance, 1);
                Log?.Msg($"[SuppressionPatch] Capped initial m_TurnsStunned from {turnsStunned} to 1");
            }
        }
        catch (Exception ex)
        {
            Log?.Error($"[SuppressionPatch] OnStateChanged postfix failed: {ex.Message}");
        }
    }

    public static void OnRoundStart_Prefix(object __instance)
    {
        try
        {
            int turnsStunned = GetTurnsStunned(__instance);
            if (turnsStunned > 1)
            {
                SetTurnsStunned(__instance, 1);
                Log?.Msg($"[SuppressionPatch] Round start: capped m_TurnsStunned from {turnsStunned} to 1");
            }
        }
        catch (Exception ex)
        {
            Log?.Error($"[SuppressionPatch] OnRoundStart prefix failed: {ex.Message}");
        }
    }

    // --- Field access helpers ---

    private static int GetTurnsStunned(object instance)
    {
        if (_turnsStunnedField != null)
            return (int)_turnsStunnedField.GetValue(instance);
        if (_turnsStunnedProp != null)
            return (int)_turnsStunnedProp.GetValue(instance);

        // Fallback: IL2CPP native offset access
        if (instance is Il2CppObjectBase il2cppObj)
        {
            return Marshal.ReadInt32(il2cppObj.Pointer + 0x24);
        }

        return 0;
    }

    private static void SetTurnsStunned(object instance, int value)
    {
        if (_turnsStunnedField != null)
        {
            _turnsStunnedField.SetValue(instance, value);
            return;
        }
        if (_turnsStunnedProp != null)
        {
            _turnsStunnedProp.SetValue(instance, value);
            return;
        }

        // Fallback: IL2CPP native offset access
        if (instance is Il2CppObjectBase il2cppObj)
        {
            Marshal.WriteInt32(il2cppObj.Pointer + 0x24, value);
        }
    }
}
