# EntitySpawner

`Menace.SDK.EntitySpawner` -- Static class for spawning and destroying entities in tactical combat.

## Methods

### SpawnUnit

```csharp
public static SpawnResult SpawnUnit(string templateName, int tileX, int tileY, int factionIndex = 1)
```

Spawn a transient actor (AI enemy or temporary unit) at the specified tile. Uses `TacticalManager.TrySpawnUnit()` internally, which handles actor registration automatically.

**Parameters:**
- `templateName` - EntityTemplate name (e.g., `enemy.pirate_boarding_commandos`, `enemy.construct_soldier_tier1`)
- `tileX` - Tile X coordinate
- `tileY` - Tile Y coordinate
- `factionIndex` - Faction index (0=Player, 1=Enemy, 2+=Others). Defaults to 1 (Enemy).

**Returns:** `SpawnResult` with the spawned entity or error information.

### SpawnGroup

```csharp
public static List<SpawnResult> SpawnGroup(string templateName, List<(int x, int y)> positions, int factionIndex = 1)
```

Spawn multiple units at once.

**Parameters:**
- `templateName` - EntityTemplate name
- `positions` - List of (x, y) tile coordinates
- `factionIndex` - Faction index for all spawned units

**Returns:** List of `SpawnResult` for each position.

### ListEntities

```csharp
public static GameObj[] ListEntities(int factionFilter = -1)
```

Get all actors currently on the tactical map.

**Parameters:**
- `factionFilter` - Optional faction index to filter by (-1 for all)

**Returns:** Array of actor `GameObj` instances.

### DestroyEntity

```csharp
public static bool DestroyEntity(GameObj entity, bool immediate = false)
```

Destroy/kill an entity.

**Parameters:**
- `entity` - The entity to destroy
- `immediate` - If true, skip death animation

**Returns:** `true` if successful.

### ClearEnemies

```csharp
public static int ClearEnemies(bool immediate = true)
```

Clear all enemies from the map.

**Parameters:**
- `immediate` - If true, skip death animations

**Returns:** Number of enemies cleared.

### GetEntityInfo

```csharp
public static EntityInfo GetEntityInfo(GameObj entity)
```

Get entity information as a summary object.

**Returns:** `EntityInfo` object or null if entity is invalid.

## Types

### SpawnResult

```csharp
public class SpawnResult
{
    public bool Success { get; set; }
    public GameObj Entity { get; set; }
    public string Error { get; set; }
}
```

### EntityInfo

```csharp
public class EntityInfo
{
    public int EntityId { get; set; }
    public string Name { get; set; }
    public string TypeName { get; set; }
    public int FactionIndex { get; set; }
    public bool IsAlive { get; set; }
    public IntPtr Pointer { get; set; }
}
```

## Examples

### Spawning a single enemy

```csharp
var result = EntitySpawner.SpawnUnit("enemy.pirate_boarding_commandos", 10, 15, factionIndex: 1);
if (result.Success)
{
    DevConsole.Log($"Spawned enemy at entity ID {result.Entity.ReadInt(0x10)}");
}
else
{
    DevConsole.Log($"Failed to spawn: {result.Error}");
}
```

### Spawning a group of enemies

```csharp
var positions = new List<(int, int)>
{
    (10, 15),
    (11, 15),
    (12, 15)
};

var results = EntitySpawner.SpawnGroup("enemy.construct_soldier_tier1", positions, factionIndex: 1);
var spawned = results.Count(r => r.Success);
DevConsole.Log($"Spawned {spawned}/{results.Count} enemies");
```

### Listing all enemies

```csharp
var enemies = EntitySpawner.ListEntities(factionFilter: 1);
foreach (var enemy in enemies)
{
    var info = EntitySpawner.GetEntityInfo(enemy);
    DevConsole.Log($"Enemy: {info.Name} at ID {info.EntityId}");
}
```

### Clearing all enemies

```csharp
var cleared = EntitySpawner.ClearEnemies(immediate: true);
DevConsole.Log($"Cleared {cleared} enemies");
```

## Console Commands

The following console commands are available:

- `spawn <template> <x> <y> [faction]` - Spawn a unit at the specified tile
- `kill` - Kill the currently selected actor
- `enemies` - List all enemy actors
- `actors [faction]` - List all actors (optionally filtered by faction)
- `clearwave` - Clear all enemies from the map
