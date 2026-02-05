#nullable disable
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.TwitchSquaddies;

/// <summary>
/// Writes squaddie names/nicknames and intercepts the home planet display
/// to show arbitrary text (e.g. Twitch chat messages).
///
/// Name/Nickname writes use PropertyInfo.SetValue() as the primary strategy
/// (Il2CppInterop handles string marshaling). Falls back to direct IL2CPP
/// string pointer writes via ManagedStringToIl2Cpp + Marshal.WriteIntPtr.
///
/// Home planet interception uses a Harmony postfix on Squaddie.GetHomePlanetName().
/// </summary>
public class SquaddieReadWrite
{
    private readonly MelonLogger.Instance _log;
    private readonly SquaddieExplorer _explorer;
    private HarmonyLib.Harmony _harmony;

    // Twitch message overrides: squaddie ID -> message text
    private static readonly Dictionary<int, string> _twitchMessages = new();

    // Cached reflection for the Harmony patch to use
    private static MethodInfo _staticGetId;
    private bool _harmonyPatchApplied;

    public bool IsReady { get; private set; }
    public string LastOperationResult { get; private set; } = "";

    public SquaddieReadWrite(MelonLogger.Instance log, SquaddieExplorer explorer)
    {
        _log = log;
        _explorer = explorer;
    }

    /// <summary>
    /// Set up Harmony patches. Call after SquaddieExplorer.TrySetup() succeeds.
    /// </summary>
    public bool Setup(HarmonyLib.Harmony harmony)
    {
        _harmony = harmony;

        var getHomePlanetName = _explorer.GetHomePlanetNameMethod();
        _staticGetId = _explorer.GetIdMethod();

        if (getHomePlanetName == null)
        {
            _log.Warning("Squaddie.GetHomePlanetName() not found — home planet interception unavailable");
            // Still mark ready: name/nickname writes can work without the patch
            IsReady = true;
            return true;
        }

        if (_staticGetId == null)
        {
            _log.Warning("Squaddie.GetId() not found — cannot map squaddies for interception");
            IsReady = true;
            return true;
        }

        // Harmony patch deferred — will be applied on first SetTwitchMessage() call.
        // Applying the patch eagerly during setup causes a native crash, likely due to
        // IL2CPP calling convention issues with the postfix's ref string parameter.
        _log.Msg("SquaddieReadWrite ready (Harmony patch deferred until first message)");

        IsReady = true;
        return true;
    }

    /// <summary>
    /// Write a new Name to a squaddie proxy object.
    /// Primary: PropertyInfo.SetValue. Fallback: direct IL2CPP string write at offset 0x28.
    /// </summary>
    public bool WriteSquaddieName(object squaddieProxy, string newName)
    {
        var nameProp = _explorer.GetNameProperty();

        // Primary: PropertyInfo.SetValue (Il2CppInterop marshals the string)
        if (nameProp != null && nameProp.CanWrite)
        {
            try
            {
                nameProp.SetValue(squaddieProxy, newName);
                LastOperationResult = $"Name set to '{newName}' via property";
                _log.Msg(LastOperationResult);
                return true;
            }
            catch (Exception ex)
            {
                _log.Msg($"PropertyInfo.SetValue for Name failed: {ex.InnerException?.Message ?? ex.Message}, trying direct write");
            }
        }

        // Fallback: direct IL2CPP string write
        return DirectWriteString(squaddieProxy, 0x28, newName, "Name");
    }

    /// <summary>
    /// Write a new Nickname to a squaddie proxy object.
    /// Primary: PropertyInfo.SetValue. Fallback: direct IL2CPP string write at offset 0x30.
    /// </summary>
    public bool WriteSquaddieNickname(object squaddieProxy, string newNickname)
    {
        var nickProp = _explorer.GetNicknameProperty();

        // Primary: PropertyInfo.SetValue
        if (nickProp != null && nickProp.CanWrite)
        {
            try
            {
                nickProp.SetValue(squaddieProxy, newNickname);
                LastOperationResult = $"Nickname set to '{newNickname}' via property";
                _log.Msg(LastOperationResult);
                return true;
            }
            catch (Exception ex)
            {
                _log.Msg($"PropertyInfo.SetValue for Nickname failed: {ex.InnerException?.Message ?? ex.Message}, trying direct write");
            }
        }

        // Fallback: direct IL2CPP string write
        return DirectWriteString(squaddieProxy, 0x30, newNickname, "Nickname");
    }

    /// <summary>
    /// Set a Twitch message override for a squaddie's home planet display.
    /// </summary>
    public void SetTwitchMessage(int squaddieId, string message)
    {
        if (!_harmonyPatchApplied)
            TryApplyHarmonyPatch();

        _twitchMessages[squaddieId] = message;
        LastOperationResult = $"Twitch message set for squaddie {squaddieId}: '{message}'";
        _log.Msg(LastOperationResult);
    }

    private void TryApplyHarmonyPatch()
    {
        var getHomePlanetName = _explorer.GetHomePlanetNameMethod();
        if (getHomePlanetName == null || _staticGetId == null)
        {
            _log.Warning("Cannot apply Harmony patch — methods not resolved");
            return;
        }

        try
        {
            var postfix = new HarmonyLib.HarmonyMethod(
                typeof(SquaddieReadWrite).GetMethod(nameof(GetHomePlanetName_Postfix),
                    BindingFlags.NonPublic | BindingFlags.Static));

            _harmony.Patch(getHomePlanetName, postfix: postfix);
            _harmonyPatchApplied = true;
            _log.Msg("Harmony postfix applied to Squaddie.GetHomePlanetName()");
        }
        catch (Exception ex)
        {
            _log.Warning($"Failed to patch GetHomePlanetName: {ex.Message}");
        }
    }

    /// <summary>
    /// Remove the Twitch message override, restoring the real planet name.
    /// </summary>
    public void ClearTwitchMessage(int squaddieId)
    {
        if (_twitchMessages.Remove(squaddieId))
        {
            LastOperationResult = $"Twitch message cleared for squaddie {squaddieId}";
            _log.Msg(LastOperationResult);
        }
    }

    /// <summary>
    /// Clear all Twitch message overrides.
    /// </summary>
    public void ClearAllTwitchMessages()
    {
        _twitchMessages.Clear();
        LastOperationResult = "All Twitch messages cleared";
        _log.Msg(LastOperationResult);
    }

    /// <summary>
    /// Check if a squaddie has a Twitch message override.
    /// </summary>
    public bool HasTwitchMessage(int squaddieId) => _twitchMessages.ContainsKey(squaddieId);

    /// <summary>
    /// Get the current Twitch message for a squaddie, or null if none.
    /// </summary>
    public string GetTwitchMessage(int squaddieId) =>
        _twitchMessages.TryGetValue(squaddieId, out var msg) ? msg : null;

    // --- Harmony postfix ---

    /// <summary>
    /// Harmony postfix on Squaddie.GetHomePlanetName().
    /// Replaces the return value with a Twitch message if one is set for this squaddie.
    /// </summary>
    private static void GetHomePlanetName_Postfix(object __instance, ref string __result)
    {
        if (_staticGetId == null || _twitchMessages.Count == 0) return;

        try
        {
            int id = (int)_staticGetId.Invoke(__instance, null);
            if (_twitchMessages.TryGetValue(id, out var msg))
                __result = msg;
        }
        catch
        {
            // Silently fail — don't break the game's planet name display
        }
    }

    // --- Direct IL2CPP string write (fallback) ---

    /// <summary>
    /// Write a managed string directly to an IL2CPP object field at the given offset.
    /// Pattern from TemplateCloning.cs:212-213.
    /// </summary>
    private bool DirectWriteString(object proxy, int offset, string value, string fieldLabel)
    {
        try
        {
            if (proxy is not Il2CppObjectBase il2cppObj)
            {
                LastOperationResult = $"Cannot direct-write {fieldLabel}: proxy is not Il2CppObjectBase";
                _log.Warning(LastOperationResult);
                return false;
            }

            IntPtr objectPointer = il2cppObj.Pointer;
            if (objectPointer == IntPtr.Zero)
            {
                LastOperationResult = $"Cannot direct-write {fieldLabel}: null pointer";
                _log.Warning(LastOperationResult);
                return false;
            }

            IntPtr il2cppString = IL2CPP.ManagedStringToIl2Cpp(value);
            Marshal.WriteIntPtr(objectPointer + offset, il2cppString);

            LastOperationResult = $"{fieldLabel} set to '{value}' via direct write (offset 0x{offset:X})";
            _log.Msg(LastOperationResult);
            return true;
        }
        catch (Exception ex)
        {
            LastOperationResult = $"Direct write failed for {fieldLabel}: {ex.Message}";
            _log.Warning(LastOperationResult);
            return false;
        }
    }
}
