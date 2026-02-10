# GameType

`Menace.SDK.GameType` -- Wrapper around an IL2CPP class pointer providing safe type system access.

## Overview

GameType represents an IL2CPP class and provides type system operations:

- **Type Lookup** - Find types by name across game assemblies
- **Hierarchy Navigation** - Walk parent types, check inheritance relationships
- **Field Discovery** - Get field offsets for direct memory access
- **Managed Bridge** - Access the IL2CppInterop proxy type for reflection

Use `GameType.Find()` to locate types, then use the GameType to check compatibility with `GameObj` instances or to pre-cache field offsets for performance-critical code.

All lookups are cached internally to avoid repeated `il2cpp_*` FFI calls. The cache is keyed both by name (`assembly:fullTypeName`) and by raw `IntPtr` class pointer.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `ClassPointer` | `IntPtr` | Raw IL2CPP class pointer. `IntPtr.Zero` for invalid types. |
| `FullName` | `string` | Fully qualified type name (`Namespace.TypeName`). Empty string for invalid types. |
| `IsValid` | `bool` | `true` when `ClassPointer != IntPtr.Zero`. |
| `Parent` | `GameType` | Parent (base) type in the IL2CPP hierarchy. Resolved lazily and cached. Returns `null` at the root of the hierarchy or if resolution fails. |
| `ManagedType` | `Type` | The IL2CppInterop managed proxy `System.Type`, if one exists. Resolved lazily from `Assembly-CSharp`. May be `null` if no proxy type is found. |

## Static Members

### GameType.Invalid

```csharp
public static GameType Invalid { get; }
```

Singleton representing an invalid type (`ClassPointer == IntPtr.Zero`, empty `FullName`). Returned by `Find` and `FromPointer` on failure.

### GameType.Find

```csharp
public static GameType Find(string fullTypeName, string assembly = "Assembly-CSharp")
```

Find an IL2CPP type by its full name (e.g., `"Namespace.TypeName"`).

**Resolution order:**

1. Check the name cache for a previous lookup with the same `assembly:fullTypeName` key.
2. Split `fullTypeName` into namespace and type name at the last `.`.
3. Try the provided assembly (with and without `.dll` suffix).
4. Fall back through: `Assembly-CSharp.dll`, `Assembly-CSharp`, `UnityEngine.CoreModule.dll`, `UnityEngine.CoreModule`, `mscorlib.dll`, `Il2Cppmscorlib.dll`.

Returns `GameType.Invalid` if the type cannot be found. Never throws.

### GameType.FromPointer

```csharp
public static GameType FromPointer(IntPtr classPointer)
```

Create a `GameType` from an existing IL2CPP class pointer. The name is read from the runtime via `il2cpp_class_get_namespace` and `il2cpp_class_get_name`. Results are cached by pointer.

Returns `GameType.Invalid` if `classPointer` is `IntPtr.Zero`.

## Instance Methods

### GetFieldOffset

```csharp
public uint GetFieldOffset(string fieldName)
```

Get the byte offset of a field on this type. The offset is resolved through `OffsetCache`, which tries multiple naming conventions: exact, `_prefix`, `m_prefix`, lowercased, and `<Name>k__BackingField`. Walks the class hierarchy.

Returns `0` if the type is invalid or the field is not found.

### HasField

```csharp
public bool HasField(string fieldName)
```

Check whether a field with the given name exists on this type or any of its parents. Uses the same multi-convention lookup as `GetFieldOffset`.

### IsAssignableFrom (GameType)

```csharp
public bool IsAssignableFrom(GameType other)
```

Check if `other` is the same type as, or derives from, this type. Wraps `il2cpp_class_is_assignable_from`. Returns `false` on any failure.

### IsAssignableFrom (IntPtr)

```csharp
public bool IsAssignableFrom(IntPtr objectPointer)
```

Check if a live IL2CPP object (by pointer) is an instance of this type. Reads the object's class pointer via `il2cpp_object_get_class`, then delegates to the class-level assignability check.

### FindMethod

```csharp
public MethodInfo FindMethod(string name, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
```

Find a method on this type's managed proxy via .NET reflection. Returns `null` if `ManagedType` is `null` or the method is not found.

## Examples

### Finding a type and checking validity

```csharp
var agentType = GameType.Find("Agent");
if (!agentType.IsValid)
{
    ModError.Warn("MyMod", "Agent type not found");
    return;
}
DevConsole.Log($"Found: {agentType.FullName}");
```

### Specifying a non-default assembly

```csharp
var transformType = GameType.Find("UnityEngine.Transform", "UnityEngine.CoreModule");
```

### Checking if a type has a field

```csharp
var type = GameType.Find("WeaponDef");
if (type.HasField("damage"))
{
    uint offset = type.GetFieldOffset("damage");
    DevConsole.Log($"damage offset: {offset}");
}
```

### Walking the type hierarchy

```csharp
var type = GameType.Find("SpecialAgent");
var current = type;
while (current != null)
{
    DevConsole.Log($"  {current.FullName}");
    current = current.Parent;
}
```

### Type compatibility checks

```csharp
var baseType = GameType.Find("UnitDef");
var derivedType = GameType.Find("AgentDef");

if (baseType.IsAssignableFrom(derivedType))
    DevConsole.Log("AgentDef derives from UnitDef");
```
