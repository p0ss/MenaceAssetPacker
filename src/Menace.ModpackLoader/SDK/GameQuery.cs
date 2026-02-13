using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// Safe wrappers around FindObjectsOfTypeAll for discovering game objects
/// by IL2CPP type. Includes a per-scene cache that is cleared on scene load.
/// </summary>
public static class GameQuery
{
    private static readonly Dictionary<IntPtr, GameObj[]> _cache = new();

    /// <summary>
    /// Find all objects of a given type name.
    /// </summary>
    public static GameObj[] FindAll(string typeName, string assembly = "Assembly-CSharp")
    {
        var type = GameType.Find(typeName, assembly);
        return FindAll(type);
    }

    /// <summary>
    /// Find all objects of a given type name and return them as managed IL2CPP proxy objects.
    /// This is the preferred method when you need to pass objects to reflection or IL2CPP APIs.
    /// </summary>
    public static object[] FindAllManaged(string typeName, string assembly = "Assembly-CSharp")
    {
        var type = GameType.Find(typeName, assembly);
        return FindAllManaged(type);
    }

    /// <summary>
    /// Find all objects of a given GameType and return as managed IL2CPP proxy objects.
    /// </summary>
    public static object[] FindAllManaged(GameType type)
    {
        if (type == null || !type.IsValid)
            return Array.Empty<object>();

        try
        {
            var managedType = type.ManagedType;
            if (managedType == null)
            {
                ModError.WarnInternal("GameQuery.FindAllManaged", $"No managed proxy for {type.FullName}");
                return Array.Empty<object>();
            }

            var il2cppType = Il2CppType.From(managedType);
            var objects = Resources.FindObjectsOfTypeAll(il2cppType);

            if (objects == null || objects.Length == 0)
                return Array.Empty<object>();

            // Get the IntPtr constructor for the target type to create properly-typed instances
            var ptrCtor = managedType.GetConstructor(new[] { typeof(IntPtr) });
            if (ptrCtor == null)
            {
                ModError.WarnInternal("GameQuery.FindAllManaged", $"No IntPtr constructor on {managedType.Name}");
                return Array.Empty<object>();
            }

            // Create properly-typed managed proxy instances
            var results = new List<object>(objects.Length);
            for (int i = 0; i < objects.Length; i++)
            {
                var obj = objects[i];
                if (obj == null) continue;

                try
                {
                    // Get the pointer and construct the correct type
                    var ptr = obj.Pointer;
                    if (ptr != IntPtr.Zero)
                    {
                        var typedObj = ptrCtor.Invoke(new object[] { ptr });
                        results.Add(typedObj);
                    }
                }
                catch
                {
                    // Skip objects that fail to convert
                }
            }

            return results.ToArray();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameQuery.FindAllManaged", $"Failed for {type.FullName}", ex);
            return Array.Empty<object>();
        }
    }

    /// <summary>
    /// Find all objects of a given GameType.
    /// </summary>
    public static GameObj[] FindAll(GameType type)
    {
        if (type == null || !type.IsValid)
            return Array.Empty<GameObj>();

        try
        {
            var managedType = type.ManagedType;
            if (managedType == null)
            {
                ModError.WarnInternal("GameQuery.FindAll", $"No managed proxy for {type.FullName}");
                return Array.Empty<GameObj>();
            }

            var il2cppType = Il2CppType.From(managedType);
            var objects = Resources.FindObjectsOfTypeAll(il2cppType);

            if (objects == null || objects.Length == 0)
                return Array.Empty<GameObj>();

            var results = new GameObj[objects.Length];
            for (int i = 0; i < objects.Length; i++)
            {
                var obj = objects[i];
                results[i] = obj != null ? new GameObj(obj.Pointer) : GameObj.Null;
            }

            return results;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameQuery.FindAll", $"Failed for {type.FullName}", ex);
            return Array.Empty<GameObj>();
        }
    }

    /// <summary>
    /// Find all objects of a given IL2CppInterop proxy type.
    /// </summary>
    public static GameObj[] FindAll<T>() where T : Il2CppObjectBase
    {
        try
        {
            var il2cppType = Il2CppType.From(typeof(T));
            var objects = Resources.FindObjectsOfTypeAll(il2cppType);

            if (objects == null || objects.Length == 0)
                return Array.Empty<GameObj>();

            var results = new GameObj[objects.Length];
            for (int i = 0; i < objects.Length; i++)
            {
                var obj = objects[i];
                results[i] = obj != null ? new GameObj(obj.Pointer) : GameObj.Null;
            }

            return results;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameQuery.FindAll<T>", $"Failed for {typeof(T).Name}", ex);
            return Array.Empty<GameObj>();
        }
    }

    /// <summary>
    /// Find an object by type name and Unity object name.
    /// </summary>
    public static GameObj FindByName(string typeName, string name)
    {
        var type = GameType.Find(typeName);
        return FindByName(type, name);
    }

    /// <summary>
    /// Find an object by GameType and Unity object name.
    /// </summary>
    public static GameObj FindByName(GameType type, string name)
    {
        if (string.IsNullOrEmpty(name))
            return GameObj.Null;

        var all = FindAll(type);
        foreach (var obj in all)
        {
            var objName = obj.GetName();
            if (objName == name)
                return obj;
        }
        return GameObj.Null;
    }

    /// <summary>
    /// Cached variant of FindAll — results are cached until ClearCache is called
    /// (typically on scene load).
    /// </summary>
    public static GameObj[] FindAllCached(GameType type)
    {
        if (type == null || !type.IsValid)
            return Array.Empty<GameObj>();

        if (_cache.TryGetValue(type.ClassPointer, out var cached))
            return cached;

        var results = FindAll(type);
        _cache[type.ClassPointer] = results;
        return results;
    }

    /// <summary>
    /// Clear the per-scene query cache. Called from ModpackLoaderMod.OnSceneWasLoaded.
    /// </summary>
    internal static void ClearCache()
    {
        _cache.Clear();
    }
}
