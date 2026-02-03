using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Menace.SDK.Internal;

namespace Menace.SDK;

/// <summary>
/// Wrapper around an IL2CPP class pointer providing safe type system access.
/// Caches lookups to avoid repeated il2cpp_* FFI calls.
/// </summary>
public class GameType
{
    private static readonly Dictionary<string, GameType> _nameCache = new();
    private static readonly Dictionary<IntPtr, GameType> _ptrCache = new();

    public IntPtr ClassPointer { get; }
    public string FullName { get; }
    public bool IsValid => ClassPointer != IntPtr.Zero;

    private GameType _parent;
    private bool _parentResolved;
    private Type _managedType;
    private bool _managedTypeResolved;

    private GameType(IntPtr classPointer, string fullName)
    {
        ClassPointer = classPointer;
        FullName = fullName ?? "";
    }

    /// <summary>
    /// Find an IL2CPP type by full name (Namespace.TypeName).
    /// Tries Assembly-CSharp by default, then falls back to common assemblies.
    /// </summary>
    public static GameType Find(string fullTypeName, string assembly = "Assembly-CSharp")
    {
        if (string.IsNullOrEmpty(fullTypeName))
            return Invalid;

        var cacheKey = $"{assembly}:{fullTypeName}";
        if (_nameCache.TryGetValue(cacheKey, out var cached))
            return cached;

        // Split namespace and type name
        var lastDot = fullTypeName.LastIndexOf('.');
        var ns = lastDot > 0 ? fullTypeName[..lastDot] : "";
        var typeName = lastDot > 0 ? fullTypeName[(lastDot + 1)..] : fullTypeName;

        // Try with provided assembly
        var ptr = TryResolveClass(assembly, ns, typeName);

        // Try common assembly variants
        if (ptr == IntPtr.Zero && !assembly.EndsWith(".dll"))
            ptr = TryResolveClass(assembly + ".dll", ns, typeName);

        // Fallback assemblies
        if (ptr == IntPtr.Zero)
        {
            string[] fallbacks = {
                "Assembly-CSharp.dll", "Assembly-CSharp",
                "UnityEngine.CoreModule.dll", "UnityEngine.CoreModule",
                "mscorlib.dll", "Il2Cppmscorlib.dll"
            };
            foreach (var fb in fallbacks)
            {
                ptr = TryResolveClass(fb, ns, typeName);
                if (ptr != IntPtr.Zero) break;
            }
        }

        var result = ptr != IntPtr.Zero ? FromPointer(ptr) : Invalid;
        if (result.IsValid)
            result = new GameType(ptr, fullTypeName); // ensure we store the requested name

        _nameCache[cacheKey] = result;
        if (result.IsValid)
            _ptrCache[ptr] = result;

        return result;
    }

    /// <summary>
    /// Create a GameType from an existing IL2CPP class pointer.
    /// </summary>
    public static GameType FromPointer(IntPtr classPointer)
    {
        if (classPointer == IntPtr.Zero)
            return Invalid;

        if (_ptrCache.TryGetValue(classPointer, out var cached))
            return cached;

        string name;
        try
        {
            var nsPtr = IL2CPP.il2cpp_class_get_namespace(classPointer);
            var nPtr = IL2CPP.il2cpp_class_get_name(classPointer);
            var ns = nsPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(nsPtr) : "";
            var n = nPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(nPtr) : "?";
            name = string.IsNullOrEmpty(ns) ? n : $"{ns}.{n}";
        }
        catch
        {
            name = $"<unknown@0x{classPointer:X}>";
        }

        var gt = new GameType(classPointer, name);
        _ptrCache[classPointer] = gt;
        return gt;
    }

    public static GameType Invalid { get; } = new(IntPtr.Zero, "");

    /// <summary>
    /// Get the parent (base) type in the IL2CPP hierarchy.
    /// </summary>
    public GameType Parent
    {
        get
        {
            if (_parentResolved) return _parent;
            _parentResolved = true;

            if (!IsValid) return null;

            try
            {
                var parentPtr = IL2CPP.il2cpp_class_get_parent(ClassPointer);
                _parent = parentPtr != IntPtr.Zero ? FromPointer(parentPtr) : null;
            }
            catch (Exception ex)
            {
                ModError.ReportInternal("GameType.Parent", $"Failed for {FullName}", ex);
            }

            return _parent;
        }
    }

    /// <summary>
    /// Get the IL2CppInterop managed proxy Type, if available. May be null.
    /// </summary>
    public Type ManagedType
    {
        get
        {
            if (_managedTypeResolved) return _managedType;
            _managedTypeResolved = true;

            if (!IsValid) return null;

            try
            {
                // Try finding the proxy type in loaded assemblies
                // IL2CppInterop proxies are prefixed with "Il2Cpp" for some types
                var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (gameAssembly != null)
                {
                    var lastDot = FullName.LastIndexOf('.');
                    var typeName = lastDot > 0 ? FullName[(lastDot + 1)..] : FullName;

                    _managedType = gameAssembly.GetTypes()
                        .FirstOrDefault(t => t.Name == typeName && !t.IsAbstract);

                    if (_managedType == null)
                    {
                        // Try Il2Cpp-prefixed name
                        _managedType = gameAssembly.GetTypes()
                            .FirstOrDefault(t => t.Name == "Il2Cpp" + typeName && !t.IsAbstract);
                    }
                }
            }
            catch (Exception ex)
            {
                ModError.ReportInternal("GameType.ManagedType", $"Failed for {FullName}", ex);
            }

            return _managedType;
        }
    }

    /// <summary>
    /// Get the field offset for a given field name. Returns 0 if not found.
    /// </summary>
    public uint GetFieldOffset(string fieldName)
    {
        if (!IsValid) return 0;
        return OffsetCache.GetOrResolve(ClassPointer, fieldName);
    }

    /// <summary>
    /// Check if this type has a field with the given name.
    /// </summary>
    public bool HasField(string fieldName)
    {
        if (!IsValid) return false;
        return OffsetCache.FindField(ClassPointer, fieldName) != IntPtr.Zero;
    }

    /// <summary>
    /// Check if this type is assignable from another GameType.
    /// </summary>
    public bool IsAssignableFrom(GameType other)
    {
        if (!IsValid || other == null || !other.IsValid)
            return false;

        try
        {
            return IL2CPP.il2cpp_class_is_assignable_from(ClassPointer, other.ClassPointer);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if an IL2CPP object pointer is an instance of this type.
    /// </summary>
    public bool IsAssignableFrom(IntPtr objectPointer)
    {
        if (!IsValid || objectPointer == IntPtr.Zero)
            return false;

        try
        {
            var objClass = IL2CPP.il2cpp_object_get_class(objectPointer);
            if (objClass == IntPtr.Zero) return false;
            return IL2CPP.il2cpp_class_is_assignable_from(ClassPointer, objClass);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Find a method on this type by name via managed reflection.
    /// </summary>
    public MethodInfo FindMethod(string name, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
    {
        var managed = ManagedType;
        if (managed == null) return null;

        try
        {
            return managed.GetMethod(name, flags);
        }
        catch
        {
            return null;
        }
    }

    public override string ToString() => IsValid ? FullName : "<invalid GameType>";

    private static IntPtr TryResolveClass(string assembly, string ns, string typeName)
    {
        try
        {
            return IL2CPP.GetIl2CppClass(assembly, ns, typeName);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }
}
