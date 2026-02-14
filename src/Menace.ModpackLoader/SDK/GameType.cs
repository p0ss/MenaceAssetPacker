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
    private static readonly string[] FallbackAssemblies =
    {
        "Assembly-CSharp.dll",
        "Assembly-CSharp",
        "UnityEngine.CoreModule.dll",
        "UnityEngine.CoreModule",
        "UnityEngine.UIModule.dll",
        "UnityEngine.UIModule",
        "UnityEngine.UI.dll",
        "UnityEngine.UI",
        "Unity.TextMeshPro.dll",
        "Unity.TextMeshPro",
        "mscorlib.dll",
        "Il2Cppmscorlib.dll"
    };

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

        assembly = string.IsNullOrWhiteSpace(assembly) ? "Assembly-CSharp" : assembly;
        var cacheKey = $"{assembly}:{fullTypeName}";
        if (_nameCache.TryGetValue(cacheKey, out var cached))
            return cached;

        // Split namespace and type name
        var lastDot = fullTypeName.LastIndexOf('.');
        var ns = lastDot > 0 ? fullTypeName[..lastDot] : "";
        var typeName = lastDot > 0 ? fullTypeName[(lastDot + 1)..] : fullTypeName;

        var ptr = IntPtr.Zero;
        foreach (var probeAssembly in BuildProbeAssemblies(assembly))
        {
            ptr = TryResolveClass(probeAssembly, ns, typeName);
            if (ptr != IntPtr.Zero)
                break;
        }

        // Fallback: if no namespace was provided and we didn't find it,
        // search managed types by short name to discover the full namespace
        if (ptr == IntPtr.Zero && string.IsNullOrEmpty(ns))
        {
            ptr = TryResolveByShortName(typeName);
        }

        var result = ptr != IntPtr.Zero ? FromPointer(ptr) : Invalid;
        if (result.IsValid)
            result = new GameType(ptr, fullTypeName); // ensure we store the requested name

        // Only cache valid results - invalid lookups may succeed later
        // (e.g., short name lookups before templates are loaded)
        if (result.IsValid)
        {
            _nameCache[cacheKey] = result;
            _ptrCache[ptr] = result;
        }

        return result;
    }

    /// <summary>
    /// Search for a type by short name (no namespace) by scanning managed assemblies.
    /// Returns the IL2CPP class pointer if found.
    /// </summary>
    private static IntPtr TryResolveByShortName(string shortName)
    {
        try
        {
            // Search Assembly-CSharp managed proxy for the type
            var gameAssembly = GameState.GameAssembly;
            if (gameAssembly == null)
                return IntPtr.Zero;

            // Don't filter by IsAbstract - template base classes like WeaponTemplate are abstract
            // but we still need to resolve them for FindObjectsOfTypeAll queries
            var managedType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == shortName);

            if (managedType == null)
                return IntPtr.Zero;

            // Extract namespace from the managed type and resolve via IL2CPP
            // The managed proxy has "Il2Cpp" prefix we need to strip for IL2CPP lookup
            var fullName = managedType.FullName ?? "";
            var originalFullName = fullName;
            if (fullName.StartsWith("Il2Cpp"))
                fullName = fullName.Substring(6);

            var dotIdx = fullName.LastIndexOf('.');
            var realNs = dotIdx > 0 ? fullName[..dotIdx] : "";
            var realName = dotIdx > 0 ? fullName[(dotIdx + 1)..] : fullName;

            // Try both with and without .dll extension
            var ptr = TryResolveClass("Assembly-CSharp.dll", realNs, realName);
            if (ptr == IntPtr.Zero)
                ptr = TryResolveClass("Assembly-CSharp", realNs, realName);
            return ptr;
        }
        catch
        {
            return IntPtr.Zero;
        }
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
    /// Only returns types that inherit from Il2CppObjectBase (actual IL2CPP proxies).
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
                var lastDot = FullName.LastIndexOf('.');
                var ns = lastDot > 0 ? FullName[..lastDot] : "";
                var typeName = lastDot > 0 ? FullName[(lastDot + 1)..] : FullName;

                // IL2CppInterop prefixes namespaces with "Il2Cpp"
                var il2cppFullName = string.IsNullOrEmpty(ns)
                    ? typeName
                    : $"Il2Cpp{ns}.{typeName}";

                // Search all loaded assemblies for the type
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        // First try exact full name match with Il2Cpp prefix
                        var candidate = asm.GetType(il2cppFullName);
                        if (IsValidIl2CppProxy(candidate))
                        {
                            _managedType = candidate;
                            return _managedType;
                        }

                        // Try original full name (some types aren't prefixed but are still proxies)
                        candidate = asm.GetType(FullName);
                        if (IsValidIl2CppProxy(candidate))
                        {
                            _managedType = candidate;
                            return _managedType;
                        }
                    }
                    catch
                    {
                        // Skip assemblies that throw on GetType
                    }
                }

                // Fallback: search by type name only in Assembly-CSharp
                var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (gameAssembly != null)
                {
                    _managedType = gameAssembly.GetTypes()
                        .FirstOrDefault(t => t.Name == typeName && IsValidIl2CppProxy(t));
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
    /// Check if a type is a valid IL2CPP proxy type (inherits from Il2CppObjectBase or is an enum).
    /// Abstract types are allowed - they're still valid for type queries like FindObjectsOfTypeAll.
    /// </summary>
    private static bool IsValidIl2CppProxy(Type type)
    {
        if (type == null)
            return false;

        // Enums are valid IL2CPP types too
        if (type.IsEnum)
            return true;

        // Must inherit from Il2CppObjectBase to be an actual IL2CPP proxy
        // Abstract types are valid - we need them for type queries
        return typeof(Il2CppObjectBase).IsAssignableFrom(type);
    }

    /// <summary>
    /// Get the Il2CppSystem.Type for use with Unity APIs like FindObjectsOfType.
    /// Tries multiple strategies to get a working type reference.
    /// </summary>
    public Il2CppSystem.Type GetIl2CppType()
    {
        if (!IsValid) return null;

        try
        {
            // Strategy 1: Try to get from managed proxy type (most reliable when available)
            var managed = ManagedType;
            if (managed != null)
            {
                try
                {
                    var il2cppType = Il2CppType.From(managed);
                    // Convert Il2CppType to Il2CppSystem.Type
                    return Il2CppSystem.Type.internal_from_handle(il2cppType.Pointer);
                }
                catch
                {
                    // Fall through to other strategies
                }
            }

            // Strategy 2: Use Il2CppSystem.Type.GetType with assembly-qualified name
            try
            {
                // Try with assembly qualifier
                var assemblyQualifiedName = $"{FullName}, {GetAssemblyName()}";
                var type = Il2CppSystem.Type.GetType(assemblyQualifiedName);
                if (type != null) return type;
            }
            catch
            {
                // Fall through
            }

            // Strategy 3: Use the type pointer directly from class
            var typePtr = IL2CPP.il2cpp_class_get_type(ClassPointer);
            if (typePtr != IntPtr.Zero)
            {
                return Il2CppSystem.Type.internal_from_handle(typePtr);
            }

            return null;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("GameType.GetIl2CppType", $"Failed for {FullName}", ex);
            return null;
        }
    }

    /// <summary>
    /// Get the assembly name for this type by querying the IL2CPP class.
    /// </summary>
    private string GetAssemblyName()
    {
        if (!IsValid) return "";

        try
        {
            var imagePtr = IL2CPP.il2cpp_class_get_image(ClassPointer);
            if (imagePtr == IntPtr.Zero) return "";

            var namePtr = IL2CPP.il2cpp_image_get_name(imagePtr);
            if (namePtr == IntPtr.Zero) return "";

            return Marshal.PtrToStringAnsi(namePtr) ?? "";
        }
        catch
        {
            return "";
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

    private static List<string> BuildProbeAssemblies(string assembly)
    {
        var probes = new List<string>(8);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        AddProbe(probes, seen, assembly);

        if (assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            AddProbe(probes, seen, assembly[..^4]);
        else
            AddProbe(probes, seen, assembly + ".dll");

        foreach (var fallback in FallbackAssemblies)
            AddProbe(probes, seen, fallback);

        return probes;
    }

    private static void AddProbe(List<string> probes, HashSet<string> seen, string assembly)
    {
        if (string.IsNullOrWhiteSpace(assembly))
            return;

        if (seen.Add(assembly))
            probes.Add(assembly);
    }
}
