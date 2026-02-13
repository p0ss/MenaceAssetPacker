# GameQuery

`Menace.SDK.GameQuery` -- Static class providing safe wrappers around Unity's `FindObjectsOfTypeAll` for discovering live game objects by IL2CPP type.

Includes a per-scene cache that is automatically cleared on scene load.

## Methods

### FindAll (by type name)

```csharp
public static GameObj[] FindAll(string typeName, string assembly = "Assembly-CSharp")
```

Find all objects of the given type name. Resolves the type via `GameType.Find`, then locates the managed proxy and calls `Resources.FindObjectsOfTypeAll`. Returns an empty array if the type is not found, has no managed proxy, or the query fails.

### FindAll (by GameType)

```csharp
public static GameObj[] FindAll(GameType type)
```

Find all objects of the given `GameType`. Requires a valid `ManagedType` on the `GameType` to perform the query. Returns an empty array on failure, and logs a warning to `ModError` if the managed proxy is missing.

### FindAll&lt;T&gt;

```csharp
public static GameObj[] FindAll<T>() where T : Il2CppObjectBase
```

Find all objects of a given IL2CppInterop proxy type. This is the most direct overload -- it bypasses `GameType` entirely and goes straight through the IL2CPP type system.

### FindAllManaged (by type name)

```csharp
public static object[] FindAllManaged(string typeName, string assembly = "Assembly-CSharp")
```

Find all objects of the given type and return them as properly-typed IL2CPP proxy objects. Unlike `FindAll` which returns `GameObj[]`, this method constructs actual managed proxy instances using the IntPtr constructor pattern.

Use this when you need to pass objects to game APIs that expect typed instances, or when working with templates via reflection.

### FindAllManaged (by GameType)

```csharp
public static object[] FindAllManaged(GameType type)
```

Find all objects of the given `GameType` and return them as properly-typed managed proxy objects. The method:

1. Resolves the `ManagedType` from the `GameType`
2. Gets the IL2CPP type for the query
3. Calls `Resources.FindObjectsOfTypeAll`
4. Constructs properly-typed instances via the `IntPtr` constructor

Returns an empty array if the type has no managed proxy or the query fails.

### FindByName (by type name)

```csharp
public static GameObj FindByName(string typeName, string name)
```

Find a single object matching both the type name and the Unity object name. Calls `FindAll(typeName)` and returns the first object whose `GetName()` equals `name`. Returns `GameObj.Null` if not found.

### FindByName (by GameType)

```csharp
public static GameObj FindByName(GameType type, string name)
```

Same as above, but takes a pre-resolved `GameType`.

### FindAllCached

```csharp
public static GameObj[] FindAllCached(GameType type)
```

Cached variant of `FindAll`. Results are stored by `ClassPointer` and returned on subsequent calls until the cache is cleared. Use this in per-frame code where object composition does not change within a scene.

The cache is automatically cleared on scene load (called internally from the modpack loader's `OnSceneWasLoaded`).

### ClearCache

```csharp
internal static void ClearCache()
```

Clear the per-scene query cache. This is called automatically on scene transitions. Mod code does not normally need to call this directly.

## Examples

### Finding all instances of a type

```csharp
var weapons = GameQuery.FindAll("WeaponTemplate");
DevConsole.Log($"Found {weapons.Length} weapon templates");

foreach (var weapon in weapons)
{
    string name = weapon.GetName();
    float damage = weapon.ReadFloat("Damage");
    DevConsole.Log($"  {name}: {damage} damage");
}
```

### Finding a specific named object

```csharp
var rifle = GameQuery.FindByName("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");
if (!rifle.IsNull)
{
    DevConsole.Log($"ARC-762 damage: {rifle.ReadFloat("Damage")}");
}
```

### Using the generic overload

```csharp
// When you have the IL2CppInterop proxy type available
var all = GameQuery.FindAll<Il2CppMenace.Strategy.WeaponTemplate>();
```

### Cached queries in per-frame code

```csharp
GameState.TacticalReady += () =>
{
    var entityType = GameType.Find("EntityTemplate");

    // This will only perform the query once per scene
    var entities = GameQuery.FindAllCached(entityType);
    foreach (var entity in entities)
    {
        // ... process entities
    }
};
```

### Getting typed objects for game API interaction

```csharp
// FindAllManaged returns actual IL2CPP proxy instances
var allLeaders = GameQuery.FindAllManaged("Menace.Strategy.UnitLeaderTemplate");
foreach (var leader in allLeaders)
{
    // leader is a real Il2CppMenace.Strategy.UnitLeaderTemplate instance
    // Can pass directly to game APIs that expect this type
    var titleProp = leader.GetType().GetProperty("UnitTitle");
    DevConsole.Log($"Found leader: {titleProp?.GetValue(leader)}");
}
```

### Using FindAllManaged with GameType

```csharp
var leaderType = GameType.Find("Menace.Strategy.UnitLeaderTemplate");
var leaders = GameQuery.FindAllManaged(leaderType);

// All objects in the array are properly typed
foreach (var leader in leaders)
{
    DevConsole.Log($"Type: {leader.GetType().Name}");
}
```
