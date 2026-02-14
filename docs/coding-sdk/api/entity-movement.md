# EntityMovement

`Menace.SDK.EntityMovement` -- Static class for controlling entity movement in tactical combat.

## Constants

### Direction Constants

```csharp
public const int DIR_NORTH = 0;
public const int DIR_NORTHEAST = 1;
public const int DIR_EAST = 2;
public const int DIR_SOUTHEAST = 3;
public const int DIR_SOUTH = 4;
public const int DIR_SOUTHWEST = 5;
public const int DIR_WEST = 6;
public const int DIR_NORTHWEST = 7;
```

### MovementFlags

```csharp
[Flags]
public enum MovementFlags
{
    None = 0,
    Force = 1,
    ForceTeleport = 2,
    AllowMoveThroughActors = 4,
    KeepContainerRotationPermanently = 8,
    Crawl = 16
}
```

## Methods

### MoveTo

```csharp
public static MoveResult MoveTo(GameObj actor, int destX, int destY, MovementFlags flags = MovementFlags.None)
```

Move an actor to the specified tile using pathfinding.

**Parameters:**
- `actor` - The actor to move
- `destX` - Destination tile X
- `destY` - Destination tile Y
- `flags` - Movement flags (optional)

**Returns:** `MoveResult` with success status.

### Teleport

```csharp
public static MoveResult Teleport(GameObj actor, int destX, int destY)
```

Teleport an actor instantly to the specified tile (no animation, no pathfinding).

### Stop

```csharp
public static bool Stop(GameObj actor)
```

Stop an actor's current movement.

### IsMoving

```csharp
public static bool IsMoving(GameObj actor)
```

Check if an actor is currently moving.

### GetMovementRange

```csharp
public static List<(int x, int y)> GetMovementRange(GameObj actor)
```

Get the tiles within movement range for an actor. This method blocks waiting for the async result.

### GetMovementRangeAsync

```csharp
public static async Task<List<(int x, int y)>> GetMovementRangeAsync(GameObj actor)
```

Get the tiles within movement range for an actor asynchronously.

### GetPath

```csharp
public static List<(int x, int y)> GetPath(GameObj actor, int destX, int destY)
```

Get the path from an actor's current position to a destination without moving.

### SetFacing / GetFacing

```csharp
public static bool SetFacing(GameObj actor, int direction)
public static int GetFacing(GameObj actor)
```

Set or get the facing direction of an actor (0-7).

### GetPosition

```csharp
public static (int x, int y)? GetPosition(GameObj actor)
```

Get the current tile position of an actor.

### GetRemainingAP / SetAP

```csharp
public static int GetRemainingAP(GameObj actor)
public static bool SetAP(GameObj actor, int ap)
```

Get or set action points for an actor.

### GetTilesMovedThisTurn

```csharp
public static int GetTilesMovedThisTurn(GameObj actor)
```

Get number of tiles moved this turn.

### GetMovementInfo

```csharp
public static MovementInfo GetMovementInfo(GameObj actor)
```

Get comprehensive movement state for an actor.

## Types

### MoveResult

```csharp
public class MoveResult
{
    public bool Success { get; set; }
    public string Error { get; set; }
    public int APCost { get; set; }
    public List<(int x, int y)> Path { get; set; }
}
```

### MovementInfo

```csharp
public class MovementInfo
{
    public (int x, int y)? Position { get; set; }
    public int Direction { get; set; }
    public string DirectionName { get; }  // "North", "Northeast", etc.
    public bool IsMoving { get; set; }
    public int CurrentAP { get; set; }
    public int APAtTurnStart { get; set; }
    public int TilesMovedThisTurn { get; set; }
    public int MovementMode { get; set; }
}
```

## Examples

### Moving an actor

```csharp
var actor = TacticalController.GetActiveActor();
if (!actor.IsNull)
{
    var result = EntityMovement.MoveTo(actor, 15, 20);
    if (result.Success)
        DevConsole.Log("Movement started");
    else
        DevConsole.Log($"Movement failed: {result.Error}");
}
```

### Teleporting an actor

```csharp
var actor = TacticalController.GetActiveActor();
EntityMovement.Teleport(actor, 10, 10);
```

### Getting movement range

```csharp
var actor = TacticalController.GetActiveActor();
var range = EntityMovement.GetMovementRange(actor);
DevConsole.Log($"Can move to {range.Count} tiles");
```

### Checking position and facing

```csharp
var actor = TacticalController.GetActiveActor();
var pos = EntityMovement.GetPosition(actor);
var facing = EntityMovement.GetFacing(actor);
if (pos.HasValue)
    DevConsole.Log($"At ({pos.Value.x}, {pos.Value.y}) facing {facing}");
```

### Setting facing direction

```csharp
var actor = TacticalController.GetActiveActor();
EntityMovement.SetFacing(actor, EntityMovement.DIR_NORTH);
```

## Console Commands

The following console commands are available:

- `move <x> <y>` - Move the selected actor to tile (x, y)
- `teleport <x> <y>` - Teleport the selected actor to tile (x, y)
- `facing [direction]` - Get or set the selected actor's facing (0-7 or N/NE/E/SE/S/SW/W/NW)
- `ap [value]` - Get or set action points for the selected actor
- `pos` - Show the selected actor's current position
