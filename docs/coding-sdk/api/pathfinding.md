# Pathfinding

`Menace.SDK.Pathfinding` -- Static class for pathfinding operations in tactical combat. Provides safe access to path finding, movement cost calculation, and traversability checks.

## Overview

The Pathfinding SDK wraps the game's internal A* pathfinding system. It provides:
- Path finding between tiles with obstacle avoidance
- Movement cost calculations based on surface types
- Traversability checks for entities
- Reachable tile enumeration within AP limits

The underlying system uses a 64x64 max grid with costs determined by surface types, structure penalties, and direction changes.

## Enums

### SurfaceType

```csharp
public enum SurfaceType
{
    Concrete = 0,
    Metal = 1,
    Sand = 2,
    Earth = 3,
    Snow = 4,
    Water = 5,
    Ruins = 6,
    SandStone = 7,
    Mud = 8,
    Grass = 9,
    Glass = 10,
    Forest = 11,
    Rock = 12,
    DirtRoad = 13,
    COUNT = 14
}
```

### CoverType

```csharp
public enum CoverType
{
    None = 0,
    Light = 1,
    Medium = 2,
    Heavy = 3
}
```

## Constants

### Movement Cost Multipliers

```csharp
public const float DIAGONAL_COST_MULT = 1.41421356f;  // sqrt(2) for diagonal movement
```

## Methods

### FindPath (with mover)

```csharp
public static PathResult FindPath(GameObj mover, int startX, int startY, int goalX, int goalY, int maxAP = 0)
```

Find a path from start to goal tile for a specific entity.

**Parameters:**
- `mover` - The entity that will traverse the path
- `startX`, `startY` - Starting tile coordinates
- `goalX`, `goalY` - Destination tile coordinates
- `maxAP` - Maximum AP to spend (0 = unlimited)

**Returns:** `PathResult` with success status, waypoints, and cost information.

### FindPath (active actor)

```csharp
public static PathResult FindPath(int goalX, int goalY, int maxAP = 0)
```

Find a path from the active actor's current position to a destination.

**Parameters:**
- `goalX`, `goalY` - Destination tile coordinates
- `maxAP` - Maximum AP to spend (0 = unlimited)

**Returns:** `PathResult` with success status, waypoints, and cost information.

### CanEnter

```csharp
public static bool CanEnter(GameObj mover, int x, int y, int fromDirection = -1)
```

Check if a tile can be entered by an entity from a given direction.

**Parameters:**
- `mover` - The entity attempting to enter
- `x`, `y` - Tile coordinates to check
- `fromDirection` - Direction of approach (-1 = any direction)

**Returns:** `true` if the tile can be entered, `false` otherwise.

### GetMovementCost

```csharp
public static MovementCostInfo GetMovementCost(GameObj mover, int x, int y)
```

Get detailed movement cost information for a tile.

**Parameters:**
- `mover` - The entity for which to calculate costs
- `x`, `y` - Tile coordinates

**Returns:** `MovementCostInfo` with cost breakdown.

### GetSurfaceType

```csharp
public static SurfaceType GetSurfaceType(int x, int y)
```

Get the surface type at a tile position.

**Returns:** A `SurfaceType` enum value.

### GetSurfaceTypeName

```csharp
public static string GetSurfaceTypeName(SurfaceType surfaceType)
```

Get a human-readable name for a surface type.

**Returns:** Surface name string (e.g., "Concrete", "Water", "DirtRoad").

### GetReachableTiles

```csharp
public static List<(int x, int y, int cost)> GetReachableTiles(GameObj mover, int maxAP)
```

Get all tiles reachable by an entity within a given AP budget.

**Parameters:**
- `mover` - The entity to check reachability for
- `maxAP` - Maximum AP to spend

**Returns:** List of tuples containing (x, y, cost) for each reachable tile.

### EstimateCost

```csharp
public static int EstimateCost(int fromX, int fromY, int toX, int toY, int baseCost = 10)
```

Calculate an estimated movement cost between two tiles (ignoring obstacles). Uses diagonal distance calculation.

**Parameters:**
- `fromX`, `fromY` - Starting tile coordinates
- `toX`, `toY` - Destination tile coordinates
- `baseCost` - Base cost per tile (default: 10)

**Returns:** Estimated AP cost.

## Types

### PathResult

```csharp
public class PathResult
{
    public bool Success { get; set; }
    public string Error { get; set; }
    public List<Vector3> Waypoints { get; set; }
    public int TotalCost { get; set; }
    public int TileCount { get; set; }
}
```

Result of a pathfinding operation.

| Property | Description |
|----------|-------------|
| `Success` | Whether a valid path was found |
| `Error` | Error message if path finding failed |
| `Waypoints` | List of world-space positions along the path |
| `TotalCost` | Total AP cost to traverse the path |
| `TileCount` | Number of tiles in the path |

### MovementCostInfo

```csharp
public class MovementCostInfo
{
    public int X { get; set; }
    public int Y { get; set; }
    public int BaseCost { get; set; }
    public SurfaceType Surface { get; set; }
    public string SurfaceTypeName { get; set; }
    public bool IsBlocked { get; set; }
    public bool HasActor { get; set; }
    public int TotalCost { get; set; }
}
```

Detailed movement cost information for a tile.

| Property | Description |
|----------|-------------|
| `X`, `Y` | Tile coordinates |
| `BaseCost` | Base movement cost for this surface type |
| `Surface` | SurfaceType enum value |
| `SurfaceTypeName` | Human-readable surface name |
| `IsBlocked` | Whether the tile is impassable |
| `HasActor` | Whether an actor occupies the tile |
| `TotalCost` | Final calculated movement cost |

## Examples

### Finding a path to a destination

```csharp
// Find path for the active actor
var result = Pathfinding.FindPath(10, 15);
if (result.Success)
{
    DevConsole.Log($"Path found with {result.TileCount} waypoints");
    DevConsole.Log($"Total AP cost: {result.TotalCost}");

    foreach (var waypoint in result.Waypoints)
    {
        DevConsole.Log($"  -> ({waypoint.x}, {waypoint.y})");
    }
}
else
{
    DevConsole.Log($"No path: {result.Error}");
}
```

### Finding a path for a specific entity

```csharp
var actor = TacticalController.GetActiveActor();
var result = Pathfinding.FindPath(actor, 5, 5, 20, 20, maxAP: 100);

if (result.Success)
{
    DevConsole.Log($"Path costs {result.TotalCost} AP");
}
```

### Checking tile accessibility

```csharp
var actor = TacticalController.GetActiveActor();

// Check if actor can enter a tile
if (Pathfinding.CanEnter(actor, 10, 10))
{
    DevConsole.Log("Tile is accessible");
}
else
{
    DevConsole.Log("Tile is blocked or occupied");
}
```

### Getting movement costs

```csharp
var actor = TacticalController.GetActiveActor();
var cost = Pathfinding.GetMovementCost(actor, 10, 10);

if (cost.IsBlocked)
{
    DevConsole.Log("Tile is impassable");
}
else
{
    DevConsole.Log($"Surface: {cost.SurfaceTypeName}");
    DevConsole.Log($"Base cost: {cost.BaseCost}");
    DevConsole.Log($"Total cost: {cost.TotalCost}");
    if (cost.HasActor)
        DevConsole.Log("Warning: Tile is occupied");
}
```

### Finding all reachable tiles

```csharp
var actor = TacticalController.GetActiveActor();
int availableAP = 50;

var reachable = Pathfinding.GetReachableTiles(actor, availableAP);
DevConsole.Log($"Can reach {reachable.Count} tiles with {availableAP} AP");

// Find the furthest reachable tile
var furthest = reachable.OrderByDescending(t => t.cost).FirstOrDefault();
if (furthest != default)
{
    DevConsole.Log($"Furthest tile: ({furthest.x}, {furthest.y}) costs {furthest.cost} AP");
}
```

### Estimating path costs

```csharp
// Quick estimate without actual pathfinding
int fromX = 5, fromY = 5;
int toX = 15, toY = 20;

var estimate = Pathfinding.EstimateCost(fromX, fromY, toX, toY);
DevConsole.Log($"Estimated cost: {estimate} AP");

// Compare with actual path
var actual = Pathfinding.FindPath(toX, toY);
if (actual.Success)
{
    DevConsole.Log($"Actual cost: {actual.TotalCost} AP");
}
```

### Working with surface types

```csharp
// Check surface at cursor position
int x = 10, y = 10;
var surfaceType = Pathfinding.GetSurfaceType(x, y);
string surfaceName = Pathfinding.GetSurfaceTypeName(surfaceType);

DevConsole.Log($"Surface at ({x}, {y}): {surfaceName}");

// Check for specific surface types
if (surfaceType == Pathfinding.SurfaceType.Water)
{
    DevConsole.Log("This is a water tile - higher movement cost");
}
else if (surfaceType == Pathfinding.SurfaceType.DirtRoad)
{
    DevConsole.Log("This is a dirt road tile - faster movement");
}
```

## Console Commands

The following console commands are registered by `RegisterConsoleCommands()`:

| Command | Arguments | Description |
|---------|-----------|-------------|
| `path` | `<x> <y>` | Find path to destination for selected actor |
| `canenter` | `<x> <y>` | Check if selected actor can enter tile |
| `movecost` | `<x> <y>` | Get movement cost breakdown for tile |
| `surface` | `<x> <y>` | Get surface type at tile |
| `reachable` | `<ap>` | Count tiles reachable within AP budget |
| `estimate` | `<x1> <y1> <x2> <y2>` | Estimate movement cost between tiles |

### Console Command Examples

```
> path 15 20
Path found: 12 waypoints
Total cost: 95 AP

> canenter 10 10
Can enter (10, 10): True

> movecost 10 10
Movement cost for (10, 10):
  Surface: Road
  Base cost: 8
  Has actor: False
  Total: 8

> surface 10 10
Surface at (10, 10): Road (1)

> reachable 50
Tiles reachable within 50 AP: 47

> estimate 5 5 15 20
Estimated cost from (5,5) to (15,20):
  Manhattan distance: 25
  Estimated AP: 250
```

## Technical Notes

- The pathfinding system uses A* algorithm with an internal PathfindingManager singleton
- Use `PathfindingManager.RequestProcess()` to get a process (NOT `Get()`)
- PathfindingProcess objects are pooled for performance
- Maximum grid size is 64x64 tiles
- Diagonal movement costs approximately 1.41x the base cost
- Occupied tiles add a +2 penalty to movement cost
- Direction must be passed as the game's Direction enum type, not as int
- FindPath requires `Il2CppSystem.Collections.Generic.List<Vector3>`, not `System.Collections.Generic.List<Vector3>`
- Default costs by surface type:
  - Concrete, Metal, Grass: 10
  - Earth, SandStone, Glass: 12
  - Rock: 14
  - Sand, Snow, Forest: 15
  - Ruins: 18
  - Mud: 20
  - Water: 25
  - DirtRoad: 8 (fastest)
