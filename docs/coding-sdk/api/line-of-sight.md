# LineOfSight

`Menace.SDK.LineOfSight` -- Static class for line of sight checks, visibility queries, and detection management.

## Overview

The LineOfSight SDK provides safe access to the game's visibility system, allowing modders to:
- Check line of sight between tiles
- Query visibility states of actors
- Get vision, detection, and concealment stats
- Find all visible tiles from a position

## Constants

### Visibility States

```csharp
public const int VISIBILITY_UNKNOWN = 0;   // Unknown visibility
public const int VISIBILITY_VISIBLE = 1;   // Fully visible
public const int VISIBILITY_HIDDEN = 2;    // Hidden from view
public const int VISIBILITY_DETECTED = 3;  // Detected but not fully visible
```

### LOS Flags

```csharp
public const byte LOS_FLAG_IGNORE_TARGET_BLOCKER = 1;   // Ignore blocking at target tile
public const byte LOS_FLAG_IGNORE_STRUCTURE_PARTS = 4;  // Ignore structure parts blocking LOS
```

## Methods

### HasLOS (Coordinates)

```csharp
public static bool HasLOS(int fromX, int fromY, int toX, int toY)
```

Check if there is clear line of sight between two tile coordinates.

**Parameters:**
- `fromX`, `fromY` - Source tile coordinates
- `toX`, `toY` - Target tile coordinates

**Returns:** `true` if line of sight is clear, `false` otherwise.

### HasLOS (GameObj)

```csharp
public static bool HasLOS(GameObj fromTile, GameObj toTile, byte flags = 0)
```

Check if there is clear line of sight between two tile objects.

**Parameters:**
- `fromTile` - Source tile object
- `toTile` - Target tile object
- `flags` - Optional LOS flags (see LOS Flags constants)

**Returns:** `true` if line of sight is clear, `false` otherwise.

### CanActorSee

```csharp
public static bool CanActorSee(GameObj actor, GameObj target)
```

Check if an actor can see a target entity. This includes detection vs concealment calculations.

**Parameters:**
- `actor` - The observing actor
- `target` - The target entity to check visibility of

**Returns:** `true` if the actor can see the target.

### GetVisibilityState

```csharp
public static int GetVisibilityState(GameObj actor)
```

Get the visibility state of an actor.

**Returns:** One of the `VISIBILITY_*` constants.

### GetVisibilityStateName

```csharp
public static string GetVisibilityStateName(int state)
```

Get a human-readable name for a visibility state.

**Returns:** `"Unknown"`, `"Visible"`, `"Hidden"`, `"Detected"`, or `"State N"` for unknown values.

### IsVisibleToPlayer

```csharp
public static bool IsVisibleToPlayer(GameObj actor)
```

Check if an actor is currently visible to the player.

**Returns:** `true` if the actor's visibility state is `VISIBILITY_VISIBLE`.

### IsMarked

```csharp
public static bool IsMarked(GameObj actor)
```

Check if an actor is marked/painted (always visible when in range).

**Returns:** `true` if the actor is marked.

### GetVision

```csharp
public static int GetVision(GameObj entity)
```

Get the vision range for an entity.

**Returns:** The entity's vision range value.

### GetDetection

```csharp
public static int GetDetection(GameObj entity)
```

Get the detection stat for an entity. Higher detection allows spotting concealed enemies.

**Returns:** The entity's detection value.

### GetConcealment

```csharp
public static int GetConcealment(GameObj entity)
```

Get the concealment stat for an entity. Higher concealment makes the entity harder to detect.

**Returns:** The entity's concealment value.

### GetVisibleTiles

```csharp
public static List<(int x, int y)> GetVisibleTiles(int centerX, int centerY, int range)
```

Get all tiles visible from a position within a given range.

**Parameters:**
- `centerX`, `centerY` - Center position coordinates
- `range` - Maximum range to check (circular)

**Returns:** List of (x, y) coordinate tuples for all visible tiles.

### GetVisibilityInfo

```csharp
public static VisibilityInfo GetVisibilityInfo(GameObj actor)
```

Get comprehensive visibility information for an actor.

**Returns:** A `VisibilityInfo` object with all visibility data, or `null` if the actor is invalid.

## Types

### VisibilityInfo

```csharp
public class VisibilityInfo
{
    public int State { get; set; }           // Visibility state constant
    public string StateName { get; set; }    // Human-readable state name
    public bool IsVisible { get; set; }      // True if visible to player
    public bool IsMarked { get; set; }       // True if marked/painted
    public int Vision { get; set; }          // Vision range
    public int Detection { get; set; }       // Detection stat
    public int Concealment { get; set; }     // Concealment stat
}
```

## Examples

### Checking line of sight between tiles

```csharp
// Check LOS using coordinates
bool hasLos = LineOfSight.HasLOS(5, 10, 12, 8);
if (hasLos)
    DevConsole.Log("Clear line of sight!");
else
    DevConsole.Log("Line of sight blocked");
```

### Checking if actor can see a target

```csharp
var actor = TacticalController.GetActiveActor();
var enemies = EntitySpawner.ListEntities(factionFilter: 1);

foreach (var enemy in enemies)
{
    if (LineOfSight.CanActorSee(actor, enemy))
    {
        DevConsole.Log($"Can see enemy at position");
    }
}
```

### Getting visibility info for an actor

```csharp
var actor = TacticalController.GetActiveActor();
var info = LineOfSight.GetVisibilityInfo(actor);

if (info != null)
{
    DevConsole.Log($"Visibility: {info.StateName}");
    DevConsole.Log($"Vision: {info.Vision}, Detection: {info.Detection}");
    DevConsole.Log($"Concealment: {info.Concealment}");
    DevConsole.Log($"Marked: {info.IsMarked}");
}
```

### Finding all visible tiles from a position

```csharp
var actor = TacticalController.GetActiveActor();
var pos = EntityMovement.GetPosition(actor);

if (pos.HasValue)
{
    int range = 12;
    var visibleTiles = LineOfSight.GetVisibleTiles(pos.Value.x, pos.Value.y, range);
    DevConsole.Log($"Can see {visibleTiles.Count} tiles within range {range}");

    // Check if a specific tile is visible
    var targetTile = (x: 10, y: 15);
    if (visibleTiles.Contains(targetTile))
        DevConsole.Log("Target tile is visible!");
}
```

### Checking visibility states

```csharp
var enemies = EntitySpawner.ListEntities(factionFilter: 1);

foreach (var enemy in enemies)
{
    var state = LineOfSight.GetVisibilityState(enemy);
    var stateName = LineOfSight.GetVisibilityStateName(state);

    if (state == LineOfSight.VISIBILITY_VISIBLE)
        DevConsole.Log("Enemy is fully visible");
    else if (state == LineOfSight.VISIBILITY_DETECTED)
        DevConsole.Log("Enemy detected but not fully visible");
    else if (state == LineOfSight.VISIBILITY_HIDDEN)
        DevConsole.Log("Enemy is hidden");
}
```

### Using LOS flags

```csharp
var fromTile = TileMap.GetTile(5, 10);
var toTile = TileMap.GetTile(12, 8);

// Check LOS ignoring structure parts
byte flags = LineOfSight.LOS_FLAG_IGNORE_STRUCTURE_PARTS;
bool hasLos = LineOfSight.HasLOS(fromTile, toTile, flags);
```

## Console Commands

The following console commands are registered by `RegisterConsoleCommands()`:

- `los <x1> <y1> <x2> <y2>` - Check line of sight between two tile coordinates. Returns whether LOS is clear or blocked, plus distance.

- `visibility` - Show visibility info for the currently selected actor. Displays state, vision, detection, and concealment stats.

- `vision` - Get vision, detection, and concealment stats for the selected actor.

- `cansee <target_name>` - Check if the selected actor can see a target by name.

- `visibletiles <range>` - Count visible tiles from the selected actor's position within the specified range. Default range is 10.
