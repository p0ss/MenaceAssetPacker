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
var agents = GameQuery.FindAll("Agent");
DevConsole.Log($"Found {agents.Length} agents");

foreach (var agent in agents)
{
    string name = agent.GetName();
    int hp = agent.ReadInt("health");
    DevConsole.Log($"  {name}: {hp} HP");
}
```

### Finding a specific named object

```csharp
var player = GameQuery.FindByName("Agent", "Player");
if (!player.IsNull)
{
    DevConsole.Log($"Player HP: {player.ReadInt("health")}");
}
```

### Using the generic overload

```csharp
// When you have the IL2CppInterop proxy type available
var all = GameQuery.FindAll<Il2Cpp.WeaponDef>();
```

### Cached queries in per-frame code

```csharp
GameState.TacticalReady += () =>
{
    var unitType = GameType.Find("UnitDef");

    // This will only perform the query once per scene
    var units = GameQuery.FindAllCached(unitType);
    foreach (var unit in units)
    {
        // ... modify unit stats
    }
};
```
