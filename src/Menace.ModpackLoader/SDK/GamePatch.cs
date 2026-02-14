using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Menace.SDK;

/// <summary>
/// Simplified Harmony patching helpers that resolve types at runtime and
/// route failures to ModError instead of throwing.
/// </summary>
public static class GamePatch
{
    /// <summary>
    /// Apply a Harmony Postfix patch by type name and method name.
    /// Returns false and logs to ModError on failure (never throws).
    /// </summary>
    public static bool Postfix(HarmonyLib.Harmony harmony, string typeName,
        string methodName, MethodInfo patchMethod)
    {
        var type = ResolveType(typeName);
        if (type == null) return false;
        return PatchInternal(harmony, type, methodName, null, patchMethod);
    }

    /// <summary>
    /// Apply a Harmony Prefix patch by type name and method name.
    /// </summary>
    public static bool Prefix(HarmonyLib.Harmony harmony, string typeName,
        string methodName, MethodInfo patchMethod)
    {
        var type = ResolveType(typeName);
        if (type == null) return false;
        return PatchInternal(harmony, type, methodName, patchMethod, null);
    }

    /// <summary>
    /// Apply a Harmony Postfix patch using a GameType.
    /// </summary>
    public static bool Postfix(HarmonyLib.Harmony harmony, GameType type,
        string methodName, MethodInfo patchMethod)
    {
        if (type == null || !type.IsValid)
        {
            ModError.ReportInternal("GamePatch.Postfix", "Invalid GameType");
            return false;
        }

        var managed = type.ManagedType;
        if (managed == null)
        {
            ModError.ReportInternal("GamePatch.Postfix",
                $"No managed proxy type for {type.FullName}");
            return false;
        }

        return PatchInternal(harmony, managed, methodName, null, patchMethod);
    }

    /// <summary>
    /// Apply a Harmony Prefix patch using a GameType.
    /// </summary>
    public static bool Prefix(HarmonyLib.Harmony harmony, GameType type,
        string methodName, MethodInfo patchMethod)
    {
        if (type == null || !type.IsValid)
        {
            ModError.ReportInternal("GamePatch.Prefix", "Invalid GameType");
            return false;
        }

        var managed = type.ManagedType;
        if (managed == null)
        {
            ModError.ReportInternal("GamePatch.Prefix",
                $"No managed proxy type for {type.FullName}");
            return false;
        }

        return PatchInternal(harmony, managed, methodName, patchMethod, null);
    }

    // --- Overload-aware variants ---

    /// <summary>
    /// Apply a Harmony Postfix patch to a specific method overload.
    /// Use this when the method has multiple overloads.
    /// </summary>
    public static bool Postfix(HarmonyLib.Harmony harmony, string typeName,
        string methodName, Type[] parameterTypes, MethodInfo patchMethod)
    {
        var type = ResolveType(typeName);
        if (type == null) return false;
        return PatchInternalWithParams(harmony, type, methodName, parameterTypes, null, patchMethod);
    }

    /// <summary>
    /// Apply a Harmony Prefix patch to a specific method overload.
    /// Use this when the method has multiple overloads.
    /// </summary>
    public static bool Prefix(HarmonyLib.Harmony harmony, string typeName,
        string methodName, Type[] parameterTypes, MethodInfo patchMethod)
    {
        var type = ResolveType(typeName);
        if (type == null) return false;
        return PatchInternalWithParams(harmony, type, methodName, parameterTypes, patchMethod, null);
    }

    private static bool PatchInternalWithParams(HarmonyLib.Harmony harmony, Type targetType,
        string methodName, Type[] parameterTypes, MethodInfo prefix, MethodInfo postfix)
    {
        if (harmony == null)
        {
            ModError.ReportInternal("GamePatch", "Harmony instance is null");
            return false;
        }

        try
        {
            var method = targetType.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static,
                null, parameterTypes, null);

            if (method == null)
            {
                ModError.ReportInternal("GamePatch",
                    $"Method '{methodName}' with specified parameters not found on {targetType.Name}");
                return false;
            }

            var prefixHm = prefix != null ? new HarmonyMethod(prefix) : null;
            var postfixHm = postfix != null ? new HarmonyMethod(postfix) : null;

            harmony.Patch(method, prefix: prefixHm, postfix: postfixHm);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GamePatch",
                $"Failed to patch {targetType.Name}.{methodName}", ex);
            return false;
        }
    }

    private static bool PatchInternal(HarmonyLib.Harmony harmony, Type targetType,
        string methodName, MethodInfo prefix, MethodInfo postfix)
    {
        if (harmony == null)
        {
            ModError.ReportInternal("GamePatch", "Harmony instance is null");
            return false;
        }

        try
        {
            var method = targetType.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static);

            if (method == null)
            {
                // Try by declared-only in the type hierarchy
                var current = targetType;
                while (current != null && method == null)
                {
                    method = current.GetMethod(methodName,
                        BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance | BindingFlags.Static |
                        BindingFlags.DeclaredOnly);
                    current = current.BaseType;
                }
            }

            if (method == null)
            {
                ModError.ReportInternal("GamePatch",
                    $"Method '{methodName}' not found on {targetType.Name}");
                return false;
            }

            var prefixHm = prefix != null ? new HarmonyMethod(prefix) : null;
            var postfixHm = postfix != null ? new HarmonyMethod(postfix) : null;

            harmony.Patch(method, prefix: prefixHm, postfix: postfixHm);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GamePatch",
                $"Failed to patch {targetType.Name}.{methodName}", ex);
            return false;
        }
    }

    private static Type ResolveType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            ModError.ReportInternal("GamePatch", "Type name is null or empty");
            return null;
        }

        try
        {
            var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            if (gameAssembly == null)
            {
                ModError.ReportInternal("GamePatch", "Assembly-CSharp not loaded");
                return null;
            }

            // Try exact match first
            var type = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);

            if (type == null)
            {
                ModError.ReportInternal("GamePatch",
                    $"Type '{typeName}' not found in Assembly-CSharp");
                return null;
            }

            return type;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GamePatch", $"Failed to resolve type '{typeName}'", ex);
            return null;
        }
    }
}
