using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for strategy-layer resource management.
/// Provides console commands to view and modify promotion points, credits, intel, and authority.
///
/// Based on reverse engineering findings:
/// - StrategyState.m_Vars is an IL2CPP int array indexed by StrategyVars enum
/// - Index 1 = Credits (OCI), 2 = Promotion Points, 3 = PP Earned, 13 = Intel, 15 = Authority
/// </summary>
public static class StrategyResources
{
    // StrategyVars indices (from save-system.md)
    private const int VAR_OCI_COMPONENTS = 1;
    private const int VAR_PROMOTION_POINTS = 2;
    private const int VAR_PROMOTION_POINTS_EARNED = 3;
    private const int VAR_INTELLIGENCE = 13;
    private const int VAR_AUTHORITY = 15;
    private const int VAR_LAST = 17;

    // Cached types
    private static GameType _strategyStateType;
    private static MethodInfo _strategyStateGet;
    private static bool _diagnosticsLogged;

    public static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("pp", "[value]", "Get/set promotion points", args =>
        {
            var vars = GetStrategyVars();
            if (vars == null)
                return "Not on strategy map";

            if (args.Length == 0)
            {
                var current = ReadVar(vars, VAR_PROMOTION_POINTS);
                var earned = ReadVar(vars, VAR_PROMOTION_POINTS_EARNED);
                return $"Promotion Points: {current} (lifetime earned: {earned})";
            }

            if (!int.TryParse(args[0], out int value))
                return "Invalid value";

            if (value < 0)
                return "Value cannot be negative";

            var prev = ReadVar(vars, VAR_PROMOTION_POINTS);
            WriteVar(vars, VAR_PROMOTION_POINTS, value);

            // Keep lifetime earned consistent
            var earned2 = ReadVar(vars, VAR_PROMOTION_POINTS_EARNED);
            if (value > earned2)
                WriteVar(vars, VAR_PROMOTION_POINTS_EARNED, value);

            return $"Promotion Points: {prev} -> {value}";
        });

        DevConsole.RegisterCommand("grantpp", "<amount>", "Grant promotion points (negative to subtract)", args =>
        {
            if (args.Length == 0)
                return "Usage: grantpp <amount>";

            if (!int.TryParse(args[0], out int amount))
                return "Invalid amount";

            if (amount == 0)
                return "Amount must be non-zero";

            var vars = GetStrategyVars();
            if (vars == null)
                return "Not on strategy map";

            var current = ReadVar(vars, VAR_PROMOTION_POINTS);
            var newValue = Math.Max(0, current + amount);
            WriteVar(vars, VAR_PROMOTION_POINTS, newValue);

            if (amount > 0)
            {
                var earned = ReadVar(vars, VAR_PROMOTION_POINTS_EARNED);
                WriteVar(vars, VAR_PROMOTION_POINTS_EARNED, earned + amount);
            }

            return $"Promotion Points: {current} -> {newValue}" +
                   (amount > 0 ? $" (+{amount})" : $" ({amount})");
        });

        DevConsole.RegisterCommand("credits", "[value]", "Get/set credits (OCI components)", args =>
        {
            return GetSetVar(args, VAR_OCI_COMPONENTS, "Credits");
        });

        DevConsole.RegisterCommand("intel", "[value]", "Get/set intelligence", args =>
        {
            return GetSetVar(args, VAR_INTELLIGENCE, "Intelligence");
        });

        DevConsole.RegisterCommand("authority", "[value]", "Get/set authority", args =>
        {
            return GetSetVar(args, VAR_AUTHORITY, "Authority");
        });

        DevConsole.RegisterCommand("resources", "", "Show all strategy resources", args =>
        {
            var vars = GetStrategyVars();
            if (vars == null)
                return "Not on strategy map";

            var lines = new List<string> { "Strategy Resources:" };
            lines.Add($"  Credits:          {ReadVar(vars, VAR_OCI_COMPONENTS)}");
            lines.Add($"  Promotion Points: {ReadVar(vars, VAR_PROMOTION_POINTS)} (earned: {ReadVar(vars, VAR_PROMOTION_POINTS_EARNED)})");
            lines.Add($"  Intelligence:     {ReadVar(vars, VAR_INTELLIGENCE)}");
            lines.Add($"  Authority:        {ReadVar(vars, VAR_AUTHORITY)}");
            return string.Join("\n", lines);
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static string GetSetVar(string[] args, int index, string label)
    {
        var vars = GetStrategyVars();
        if (vars == null)
            return "Not on strategy map";

        if (args.Length == 0)
            return $"{label}: {ReadVar(vars, index)}";

        if (!int.TryParse(args[0], out int value))
            return "Invalid value";

        if (value < 0)
            return "Value cannot be negative";

        var prev = ReadVar(vars, index);
        WriteVar(vars, index, value);
        return $"{label}: {prev} -> {value}";
    }

    // ═══════════════════════════════════════════════════════════════════
    //  StrategyState access
    // ═══════════════════════════════════════════════════════════════════

    private static object GetStrategyVars()
    {
        try
        {
            EnsureTypesLoaded();
            if (_strategyStateGet == null) return null;

            var ss = _strategyStateGet.Invoke(null, null);
            if (ss == null) return null;

            var lookupType = _strategyStateType.ManagedType;

            var prop = lookupType.GetProperty("m_Vars",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null)
                return prop.GetValue(ss);

            var field = lookupType.GetField("m_Vars",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                return field.GetValue(ss);

            // Try runtime type if IL2CPP interop generates differently
            var runtimeType = ss.GetType();
            if (runtimeType != lookupType)
            {
                prop = runtimeType.GetProperty("m_Vars",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null)
                    return prop.GetValue(ss);

                field = runtimeType.GetField("m_Vars",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                    return field.GetValue(ss);
            }

            if (!_diagnosticsLogged)
            {
                _diagnosticsLogged = true;
                SdkLogger.Warning($"[StrategyResources.GetStrategyVars] Could not find m_Vars on StrategyState");
                var props = runtimeType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var fields = runtimeType.GetFields(BindingFlags.Public | BindingFlags.Instance);
                SdkLogger.Msg($"[StrategyResources] Props: {string.Join(", ", props.Select(p => p.Name).Take(20))}");
                SdkLogger.Msg($"[StrategyResources] Fields: {string.Join(", ", fields.Select(f => f.Name).Take(20))}");
            }

            return null;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("StrategyResources.GetStrategyVars", "Failed to access m_Vars", ex);
            return null;
        }
    }

    private static int ReadVar(object vars, int index)
    {
        try
        {
            var varsType = vars.GetType();

            // IL2CPP indexed access (get_Item)
            var indexer = varsType.GetMethod("get_Item");
            if (indexer != null)
                return Convert.ToInt32(indexer.Invoke(vars, new object[] { index }));

            // Managed array
            if (vars is Array arr)
                return Convert.ToInt32(arr.GetValue(index));

            // Raw IL2CPP pointer (64-bit layout)
            if (vars is Il2CppObjectBase il2cppObj)
            {
                var ptr = il2cppObj.Pointer;
                if (ptr != IntPtr.Zero)
                {
                    var maxLength = Marshal.ReadInt64(ptr + 0x18);
                    if (index < 0 || index >= maxLength)
                        return 0;
                    return Marshal.ReadInt32(ptr + 0x20 + index * 4);
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("StrategyResources.ReadVar", $"Failed to read index {index}", ex);
            return 0;
        }
    }

    private static bool WriteVar(object vars, int index, int value)
    {
        try
        {
            var varsType = vars.GetType();

            var setter = varsType.GetMethod("set_Item");
            if (setter != null)
            {
                setter.Invoke(vars, new object[] { index, value });
                return true;
            }

            if (vars is Array arr)
            {
                arr.SetValue(value, index);
                return true;
            }

            if (vars is Il2CppObjectBase il2cppObj)
            {
                var ptr = il2cppObj.Pointer;
                if (ptr != IntPtr.Zero)
                {
                    var maxLength = Marshal.ReadInt64(ptr + 0x18);
                    if (index < 0 || index >= maxLength)
                        return false;
                    Marshal.WriteInt32(ptr + 0x20 + index * 4, value);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("StrategyResources.WriteVar", $"Failed to write index {index}", ex);
            return false;
        }
    }

    private static void EnsureTypesLoaded()
    {
        if (_strategyStateGet != null) return;

        _strategyStateType = GameType.Find("Menace.States.StrategyState");
        if (_strategyStateType == null)
            return;

        var managedType = _strategyStateType.ManagedType;

        _strategyStateGet = managedType.GetMethod("Get",
            BindingFlags.Public | BindingFlags.Static,
            null, Type.EmptyTypes, null);

        if (_strategyStateGet == null)
        {
            try
            {
                _strategyStateGet = managedType.GetMethod("Get",
                    BindingFlags.Public | BindingFlags.Static);
            }
            catch (AmbiguousMatchException)
            {
                var methods = managedType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "Get");
                _strategyStateGet = methods.FirstOrDefault(m => m.GetParameters().Length == 0)
                                 ?? methods.FirstOrDefault();
            }
        }
    }
}
