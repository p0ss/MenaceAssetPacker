# Collections

Safe wrappers for IL2CPP collection types.

## Overview

The Collections module provides three `readonly struct` wrappers for IL2CPP collections:

- **GameList** - Wraps `List<T>` with count, indexing, and enumeration
- **GameDict** - Wraps `Dictionary<K,V>` with key-value pair enumeration
- **GameArray** - Wraps native arrays with indexing and primitive value reads

All wrappers read directly from the IL2CPP memory layout without allocating managed objects. They're designed for safe, zero-throw access: out-of-bounds reads return defaults, null pointers are handled gracefully.

Use these when you encounter collection-typed fields on game objects (e.g., `unit.ReadPtr("abilities")` returns a List pointer that you can wrap with `new GameList(...)`).

---

## GameList

`Menace.SDK.GameList` -- Wraps an IL2CPP `List<T>` object.

Reads the internal `_items` array and `_size` field via cached offsets. Elements are returned as `GameObj` handles (pointer-sized reference type elements).

### Construction

```csharp
public GameList(IntPtr listPointer)
public GameList(GameObj listObj)
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsValid` | `bool` | `true` when the underlying pointer is non-zero. |
| `Count` | `int` | Number of elements. Reads the `_size` field. Returns 0 on failure. |

### Indexer

```csharp
public GameObj this[int index] { get; }
```

Returns the element at the given index. Validates against the backing array's `max_length`. Returns `GameObj.Null` for out-of-bounds or null pointer access.

### Enumeration

`GameList` supports `foreach` via a custom `Enumerator` struct (no allocations).

```csharp
var list = new GameList(someObj.ReadPtr("unitList"));
foreach (var unit in list)
{
    DevConsole.Log(unit.GetName());
}
```

### Example: indexed access

```csharp
var list = new GameList(obj.ReadPtr("items"));
for (int i = 0; i < list.Count; i++)
{
    var item = list[i];
    if (!item.IsNull)
        DevConsole.Log($"[{i}] {item.GetTypeName()} - {item.GetName()}");
}
```

---

## GameDict

`Menace.SDK.GameDict` -- Wraps an IL2CPP `Dictionary<K,V>` object.

Iterates the internal `_entries` array, skipping tombstoned entries (entries where `hashCode < 0`). The entry stride is resolved from the element class size, with a fallback assumption of `int hash + int next + IntPtr key + IntPtr value`.

### Construction

```csharp
public GameDict(IntPtr dictPointer)
public GameDict(GameObj dictObj)
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsValid` | `bool` | `true` when the underlying pointer is non-zero. |
| `Count` | `int` | Number of entries. Reads the `_count` field (tries `_count` then `count`). Returns 0 on failure. |

### Enumeration

`GameDict` supports `foreach` with `(GameObj Key, GameObj Value)` tuples.

```csharp
var dict = new GameDict(obj.ReadPtr("weaponMap"));
foreach (var (key, value) in dict)
{
    string keyName = key.GetName() ?? key.GetTypeName();
    string valName = value.GetName() ?? value.GetTypeName();
    DevConsole.Log($"  {keyName} -> {valName}");
}
```

**Note:** Both keys and values are returned as `GameObj`. For dictionaries with value-type keys (int, enum), the `GameObj.Pointer` will contain the boxed value pointer, not a direct integer. Use the REPL or managed reflection (via `Templates.ReadField`) for value-type dictionary access.

---

## GameArray

`Menace.SDK.GameArray` -- Wraps an IL2CPP native array.

Reads elements directly from the array's data region, which starts after the IL2CPP array header (`klass | monitor | bounds | max_length | data...`).

### Construction

```csharp
public GameArray(IntPtr arrayPointer)
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsValid` | `bool` | `true` when the underlying pointer is non-zero. |
| `Length` | `int` | Number of elements. Reads the `max_length` field in the array header. Returns 0 on failure. |

### Indexer (reference types)

```csharp
public GameObj this[int index] { get; }
```

Reads a pointer-sized element at the given index. Returns `GameObj.Null` for out-of-bounds access.

### Value type reads

```csharp
public int ReadInt(int index)
public float ReadFloat(int index)
```

Read primitive `int` or `float` elements from the data region, using 4-byte element stride. These are for arrays of value types (`int[]`, `float[]`), not arrays of reference types.

### Enumeration

`GameArray` supports `foreach` via a custom `Enumerator` struct. Each element is yielded as a `GameObj`.

```csharp
var arr = new GameArray(obj.ReadPtr("components"));
foreach (var component in arr)
{
    DevConsole.Log(component.GetTypeName());
}
```

### Example: reading a float array

```csharp
var floatArray = new GameArray(obj.ReadPtr("damageMultipliers"));
for (int i = 0; i < floatArray.Length; i++)
{
    float mult = floatArray.ReadFloat(i);
    DevConsole.Log($"[{i}] = {mult}");
}
```
