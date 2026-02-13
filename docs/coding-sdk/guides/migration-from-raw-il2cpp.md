# Migration from Raw IL2CPP to Menace SDK

This guide shows how to replace common raw IL2CPP patterns with their Menace SDK equivalents. The SDK wraps IL2CPP runtime access behind safe, name-based APIs that handle offset resolution, caching, error recovery, and null safety automatically.

All SDK types live in the `Menace.SDK` namespace.

---

## Type Resolution

### Before: Manual IL2CPP class lookup

```csharp
var klass = IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "Menace.Strategy", "WeaponTemplate");
if (klass == IntPtr.Zero)
    throw new Exception("WeaponTemplate not found");

var nsPtr = IL2CPP.il2cpp_class_get_namespace(klass);
var namePtr = IL2CPP.il2cpp_class_get_name(klass);
var name = Marshal.PtrToStringAnsi(namePtr);
```

### After: GameType.Find

```csharp
var type = GameType.Find("WeaponTemplate");
if (!type.IsValid)
    MelonLogger.Warning("WeaponTemplate not found");

string name = type.FullName;
```

`GameType.Find` tries Assembly-CSharp first, then falls back to common assemblies (UnityEngine.CoreModule, mscorlib). Results are cached -- repeated calls for the same type name are free.

---

## Field Reads

### Before: Manual offset resolution and Marshal reads

```csharp
IntPtr klass = IL2CPP.il2cpp_object_get_class(ptr);
IntPtr field = IL2CPP.il2cpp_class_get_field_from_name(klass, "HitPoints");
if (field == IntPtr.Zero)
{
    // try _hitPoints, m_HitPoints, <HitPoints>k__BackingField ...
    field = IL2CPP.il2cpp_class_get_field_from_name(klass, "m_HitPoints");
}
uint offset = IL2CPP.il2cpp_field_get_offset(field);
int hp = Marshal.ReadInt32(ptr + (int)offset);
```

### After: GameObj.ReadInt

```csharp
var obj = new GameObj(ptr);
int hp = obj.ReadInt("HitPoints");
```

The SDK automatically:
- Resolves the field offset via `OffsetCache`, trying multiple naming conventions (exact, `_prefix`, `m_prefix`, lowercase, `<Name>k__BackingField`)
- Walks the class hierarchy if the field is defined on a base class
- Caches the resolved offset for subsequent reads
- Returns a safe default (0, 0f, false, null) on failure instead of throwing

Available read methods:

| Method | Return type | Default on failure |
|--------|------------|-------------------|
| `ReadInt(fieldName)` | `int` | `0` |
| `ReadFloat(fieldName)` | `float` | `0f` |
| `ReadBool(fieldName)` | `bool` | `false` |
| `ReadPtr(fieldName)` | `IntPtr` | `IntPtr.Zero` |
| `ReadString(fieldName)` | `string` | `null` |
| `ReadObj(fieldName)` | `GameObj` | `GameObj.Null` |

---

## Field Writes

### Before: Manual offset writes

```csharp
IntPtr klass = IL2CPP.il2cpp_object_get_class(ptr);
IntPtr field = IL2CPP.il2cpp_class_get_field_from_name(klass, "Speed");
uint offset = IL2CPP.il2cpp_field_get_offset(field);
int bits = BitConverter.SingleToInt32Bits(2.5f);
Marshal.WriteInt32(ptr + (int)offset, bits);
```

### After: GameObj.WriteFloat

```csharp
var obj = new GameObj(ptr);
bool success = obj.WriteFloat("Speed", 2.5f);
```

All write methods return `bool` -- `true` on success, `false` on failure. They never throw. Failures are reported to `ModError` automatically.

Available write methods:

| Method | Value type |
|--------|-----------|
| `WriteInt(fieldName, value)` | `int` |
| `WriteFloat(fieldName, value)` | `float` |
| `WritePtr(fieldName, value)` | `IntPtr` |

---

## Hardcoded Offsets

### Before: Hardcoded byte offsets

```csharp
// Fragile: offset 0x24 was correct in build 1.0, broke in 1.1
int turnsStunned = Marshal.ReadInt32(il2cppObj.Pointer + 0x24);
```

### After: Name-based access with automatic caching

```csharp
var obj = new GameObj(il2cppObj.Pointer);
int turnsStunned = obj.ReadInt("TurnsStunned");
```

If you need the raw offset for a hot path (e.g., a per-frame Harmony patch), resolve it once and use the offset overload:

```csharp
// Resolve once during initialization
var type = GameType.Find("SuppressionHandler");
uint offset = type.GetFieldOffset("TurnsStunned");

// Use in hot path
int value = obj.ReadInt(offset);
```

---

## Finding Game Objects

### Before: Manual FindObjectsOfTypeAll with IL2CPP type conversion

```csharp
var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
var templateType = gameAssembly.GetTypes()
    .FirstOrDefault(t => t.Name == "WeaponTemplate" && !t.IsAbstract);
var il2cppType = Il2CppType.From(templateType);
var objects = Resources.FindObjectsOfTypeAll(il2cppType);

foreach (var obj in objects)
{
    if (obj == null) continue;
    if (obj.name == "weapon.generic_assault_rifle_tier1_ARC_762")
    {
        // found it
    }
}
```

### After: GameQuery.FindAll / FindByName

```csharp
// Find all instances
GameObj[] all = GameQuery.FindAll("WeaponTemplate");

// Find by name
GameObj rifle = GameQuery.FindByName("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");
if (!rifle.IsNull)
{
    float damage = rifle.ReadFloat("Damage");
}
```

For repeated lookups within the same scene, use the cached variant:

```csharp
GameObj[] cached = GameQuery.FindAllCached(type);
// Cache is cleared automatically on scene load
```

---

## Harmony Patching

### Before: Manual type resolution and patching with reflection

```csharp
var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

var controllerType = gameAssembly.GetTypes()
    .FirstOrDefault(t => t.Name == "ActorComponent");

if (controllerType == null)
{
    Log.Error("ActorComponent not found");
    return;
}

var method = controllerType.GetMethod("ApplyDamage",
    BindingFlags.Public | BindingFlags.NonPublic |
    BindingFlags.Instance | BindingFlags.Static);

if (method == null)
{
    Log.Error("ApplyDamage not found");
    return;
}

try
{
    harmony.Patch(method,
        postfix: new HarmonyMethod(typeof(MyMod), nameof(ApplyDamage_Postfix)));
}
catch (Exception ex)
{
    Log.Error($"Patch failed: {ex.Message}");
}
```

### After: GamePatch.Postfix

```csharp
bool ok = GamePatch.Postfix(harmony, "ActorComponent", "ApplyDamage",
    typeof(MyMod).GetMethod(nameof(ApplyDamage_Postfix)));

if (!ok)
    Log.Warning("Failed to patch ActorComponent.ApplyDamage");
```

`GamePatch` handles type resolution, method lookup (including hierarchy walk), and error reporting. It returns `false` on failure instead of throwing.

---

## Object Identity and Type Checking

### Before

```csharp
IntPtr klass = IL2CPP.il2cpp_object_get_class(ptr);
IntPtr parentKlass = IL2CPP.il2cpp_class_get_parent(klass);
var namePtr = IL2CPP.il2cpp_class_get_name(klass);
string typeName = Marshal.PtrToStringAnsi(namePtr);
```

### After

```csharp
var obj = new GameObj(ptr);
string typeName = obj.GetTypeName();
string objName = obj.GetName();
bool alive = obj.IsAlive;

// Type checking
var baseType = GameType.Find("DataTemplate");
bool isTemplate = obj.Is(baseType);

// Type hierarchy
GameType type = obj.GetGameType();
GameType parent = type.Parent;
```

---

## Managed Type Access

### Before: Scanning loaded assemblies manually

```csharp
var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
var type = gameAssembly?.GetTypes()
    .FirstOrDefault(t => t.Name == "SuppressionHandler");
```

### After: GameState.FindManagedType

```csharp
Type managedType = GameState.FindManagedType("SuppressionHandler");
```

Or via GameType:

```csharp
var gameType = GameType.Find("SuppressionHandler");
Type managedType = gameType.ManagedType;  // IL2CppInterop proxy type
MethodInfo method = gameType.FindMethod("OnRoundStart");
```

---

## Summary of Key Replacements

| Raw IL2CPP | SDK Equivalent |
|-----------|---------------|
| `IL2CPP.GetIl2CppClass(...)` | `GameType.Find(typeName)` |
| `IL2CPP.il2cpp_object_get_class(ptr)` | `obj.GetGameType()` |
| `IL2CPP.il2cpp_class_get_field_from_name(...)` | Automatic via `OffsetCache` |
| `IL2CPP.il2cpp_field_get_offset(...)` | `type.GetFieldOffset(fieldName)` |
| `Marshal.ReadInt32(ptr + offset)` | `obj.ReadInt(fieldName)` |
| `Marshal.WriteInt32(ptr + offset, val)` | `obj.WriteInt(fieldName, val)` |
| `Resources.FindObjectsOfTypeAll(il2cppType)` | `GameQuery.FindAll(typeName)` |
| Manual harmony patching boilerplate | `GamePatch.Prefix/Postfix(...)` |
| `AppDomain...GetAssemblies()...FirstOrDefault(...)` | `GameState.GameAssembly` |
| Hardcoded `ptr + 0x24` | `obj.ReadInt("TurnsStunned")` |

---

## Migration Checklist

1. Replace raw `IL2CPP.GetIl2CppClass` calls with `GameType.Find`
2. Replace `Marshal.Read*` / `Marshal.Write*` with `GameObj.Read*` / `GameObj.Write*`
3. Replace manual `FindObjectsOfTypeAll` boilerplate with `GameQuery.FindAll` / `FindByName`
4. Replace manual Harmony patching boilerplate with `GamePatch.Prefix` / `GamePatch.Postfix`
5. Replace hardcoded field offsets with name-based access
6. Replace `try/catch` error handling with `ModError.Report` and check return values
7. Remove assembly scanning boilerplate -- use `GameState.GameAssembly` or `GameState.FindManagedType`
