# GameObj

`Menace.SDK.GameObj` -- A `readonly struct` that wraps a raw IL2CPP object pointer with safe read/write operations.

All reads return default values on failure (0, 0f, false, null, `GameObj.Null`). All writes return `false` on failure. No method on `GameObj` ever throws an exception -- failures are routed to `ModError` internally.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Pointer` | `IntPtr` | The raw IL2CPP object pointer. |
| `IsNull` | `bool` | `true` when `Pointer == IntPtr.Zero`. |
| `IsAlive` | `bool` | `true` when the underlying Unity object has not been destroyed. Checks the `m_CachedPtr` field on `UnityEngine.Object`. Returns `true` if the offset cannot be resolved (assumes alive). Returns `false` if the pointer is zero or the read fails. |

## Static Members

### GameObj.Null

```csharp
public static GameObj Null => default;
```

The null/empty handle. `Pointer` is `IntPtr.Zero`, `IsNull` is `true`.

## Read Methods

Every read method has two overloads: one taking a **field name** (string) that resolves the offset at call time, and one taking a **pre-cached offset** (`uint`) for hot paths.

### By field name

```csharp
public int    ReadInt(string fieldName)
public float  ReadFloat(string fieldName)
public bool   ReadBool(string fieldName)
public IntPtr ReadPtr(string fieldName)
public string ReadString(string fieldName)
public GameObj ReadObj(string fieldName)
```

Field names are resolved through `OffsetCache`, which tries multiple naming conventions (exact, `_prefix`, `m_prefix`, lowercased, backing field pattern) and walks the class hierarchy.

`ReadString` reads a pointer-sized field, then converts the IL2CPP string to a managed `string` via `IL2CPP.Il2CppStringToManaged`.

`ReadObj` reads a pointer-sized field and wraps it in a new `GameObj`.

### By pre-cached offset

```csharp
public int    ReadInt(uint offset)
public float  ReadFloat(uint offset)
public IntPtr ReadPtr(uint offset)
```

Use these when you have already resolved the field offset (e.g., via `GameType.GetFieldOffset`) and want to avoid repeated name lookups. Returns the default value (0, 0f, `IntPtr.Zero`) if the pointer is null or the offset is 0.

## Write Methods

```csharp
public bool WriteInt(string fieldName, int value)
public bool WriteFloat(string fieldName, float value)
public bool WritePtr(string fieldName, IntPtr value)
```

Each returns `true` on success, `false` on failure. Failures are logged to `ModError`.

## Type Operations

### GetGameType

```csharp
public GameType GetGameType()
```

Read the IL2CPP class pointer from the object header and return a `GameType`. Returns `GameType.Invalid` if the pointer is null or the class cannot be read.

### Is

```csharp
public bool Is(GameType type)
```

Check if this object is an instance of the given `GameType` (or a derived type). Delegates to `GameType.IsAssignableFrom(IntPtr)`.

### GetTypeName

```csharp
public string GetTypeName()
```

Shorthand for `GetGameType().FullName`. Returns the fully qualified IL2CPP type name of this object.

### GetName

```csharp
public string GetName()
```

Read the Unity object name by resolving the `m_Name` field on `UnityEngine.Object`. Returns `null` if the object does not have a name field or the read fails.

## Equality

`GameObj` implements `IEquatable<GameObj>`. Two handles are equal if and only if their `Pointer` values are equal.

```csharp
public static bool operator ==(GameObj left, GameObj right)
public static bool operator !=(GameObj left, GameObj right)
```

`ToString()` returns a string like `TypeName 'ObjectName' @ 0x7F...` or `GameObj.Null`.

## Examples

### Safe field access pattern

```csharp
var agent = GameQuery.FindByName("Agent", "Player");
if (agent.IsNull || !agent.IsAlive)
    return;

int hp = agent.ReadInt("health");
float speed = agent.ReadFloat("moveSpeed");
string name = agent.ReadString("m_Name");
```

### Pre-cached offset for hot paths

```csharp
// Resolve once
var agentType = GameType.Find("Agent");
uint hpOffset = agentType.GetFieldOffset("health");

// Use in a loop
foreach (var agent in GameQuery.FindAll(agentType))
{
    int hp = agent.ReadInt(hpOffset);
    DevConsole.Log($"{agent.GetName()}: {hp} HP");
}
```

### Walking object references

```csharp
var unit = GameQuery.FindByName("UnitDef", "Assault");
GameObj weapon = unit.ReadObj("primaryWeapon");
if (!weapon.IsNull)
{
    int damage = weapon.ReadInt("damage");
    DevConsole.Log($"Weapon damage: {damage}");
}
```

### Writing fields

```csharp
var unit = GameQuery.FindByName("Agent", "Player");
if (!unit.IsNull)
{
    bool ok = unit.WriteInt("health", 999);
    if (!ok)
        ModError.Warn("MyMod", "Failed to write health");
}
```

### Type checking

```csharp
var unitDefType = GameType.Find("UnitDef");
var obj = GameQuery.FindByName("AgentDef", "Scout");
if (obj.Is(unitDefType))
    DevConsole.Log("Scout is a UnitDef");
```
