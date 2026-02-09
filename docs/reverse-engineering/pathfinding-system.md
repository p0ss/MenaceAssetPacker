# Pathfinding System

## Overview

Menace uses an A* pathfinding algorithm with tactical game-specific extensions including surface-based movement costs, structure traversal penalties, direction change costs, and AI-specific path evaluation.

## Architecture

```
PathfindingManager (singleton pool)
├── PathfindingProcess[] (pooled pathfinders)
│   ├── PathfindingNode[64,64] (preallocated grid)
│   ├── FastPriorityQueue (open list)
│   └── ClosedSet bitmap (visited tiles)
│
└── Path Modifiers (post-processing)
    ├── FunnelModifier
    ├── SimplifyModifier
    ├── SplineModifier
    └── WalkThroughStructureModifier
```

## PathfindingProcess Class

Main pathfinding implementation using A* algorithm.

### PathfindingProcess Field Layout

```c
public class PathfindingProcess {
    // Object header                      // +0x00 - 0x0F
    bool IsAvailable;                     // +0x10 (for pooling)
    // padding                            // +0x11 - 0x17
    PathfindingNode[,] Nodes;             // +0x18 (64x64 preallocated grid)
    FastPriorityQueue<PathfindingNode> OpenList;  // +0x20 (capacity 0x800)
    byte* ClosedSet;                      // +0x28 (native memory, 64x64 bits = 0x1000 bytes)
    List<Vector3> ResultPath;             // +0x30 (final path waypoints)
    List<Vector3> TempPath;               // +0x38 (working path buffer)
    int MaxAP;                            // +0x40 (AP limit for path, 0 = unlimited)
    int BaseMovementCost;                 // +0x44 (extra cost per tile)
    int HeuristicMultiplier;              // +0x48 (A* heuristic weight)
    bool IgnoreAllies;                    // +0x4C (can path through allies)
    bool CanWalkThroughAllies;            // +0x4D (infantry can walk through)
    bool IsPlayerControlled;              // +0x4E (affects fog/visibility costs)
}
```

## PathfindingNode Class

Represents a single tile in the pathfinding grid.

### PathfindingNode Field Layout

```c
public class PathfindingNode {
    // Object header                    // +0x00 - 0x0F
    int X;                              // +0x10 (tile X coordinate)
    int Y;                              // +0x14 (tile Y coordinate)
    int F;                              // +0x18 (total cost = G + H, priority)
    int G;                              // +0x1C (cost from start)
    int MovementCost;                   // +0x20 (actual AP spent to reach)
    // padding                          // +0x24
    Tile Tile;                          // +0x28 (reference to actual tile)
    PathfindingNode Parent;             // +0x30 (for path reconstruction)
    int Direction;                      // +0x38 (direction arrived from, 0-7)
}
```

## FindPath Algorithm

### Method Signature

```c
bool FindPath(
    Tile start,                 // Starting tile
    Tile goal,                  // Destination tile
    Entity mover,               // Entity that will move
    List<Vector3> outPath,      // Output path waypoints
    int direction,              // Initial facing direction
    int maxAP,                  // AP limit (0 = unlimited)
    bool ignoreAllies           // Can path through allied units
);
```

### Algorithm Flow

```
1. Initialize
   ├── Clear open list and closed set
   ├── Get EntityTemplate.MovementCosts[] for surface costs
   ├── Calculate HeuristicMultiplier from template
   └── Create start node with G=0

2. Main Loop (while open list not empty)
   ├── Dequeue lowest F node
   ├── If node is goal → Reconstruct path, return true
   ├── Add to closed set
   │
   └── For each direction (0-7):
       ├── Get neighbor tile
       ├── Skip if in closed set
       ├── Check IsTraversable()
       │   ├── Blocked tiles → Skip
       │   ├── Surface type cost check
       │   ├── Entity collision check
       │   └── Diagonal corner cutting check
       │
       ├── Calculate movement cost:
       │   ├── Base: SurfaceTypeCost[tile.SurfaceType]
       │   ├── + BaseMovementCost
       │   ├── + EntityProperties.GetMovementCostModifier()
       │   ├── + StructureTraversalCost (if entering structure)
       │   ├── × 1.41 for diagonals
       │   ├── + DirectionChangePenalty
       │   └── × 8 if tile has flag 0x1000 (difficult terrain for player)
       │
       ├── AI-specific adjustments:
       │   ├── - TileScore.SafetyScore × SafetyPathfindingMult
       │   ├── - Bonus for not visible to opponents
       │   └── - Bonus for hidden from player
       │
       ├── Calculate F = G + H × HeuristicMultiplier
       │
       └── Add/update node in open list

3. Path Reconstruction
   └── Walk parent pointers from goal to start
```

## IsTraversable Check

Determines if a tile can be entered from a given direction.

```c
bool IsTraversable(
    Tile tile,                  // Target tile
    PathfindingNode fromNode,   // Coming from this node
    int direction,              // Direction of travel (0-7)
    Entity mover,               // Entity attempting to move
    out bool markClosed         // Output: should mark as closed
);
```

### Traversability Rules

1. **Blocked Tiles**: `(Flags & 0x01) != 0` → Not traversable
2. **Surface Type**: Must have valid movement cost in EntityTemplate
3. **Movement Blocked**: Check `MovementBlocked[direction]` array
4. **Occupied Tiles**:
   - If allied infantry and `CanWalkThroughAllies` → OK
   - If entity `IsPassable()` → OK
   - Otherwise → Not traversable
5. **Diagonal Movement**: Both adjacent cardinal tiles must be clear

### Diagonal Corner Cutting

For diagonal moves (directions 1, 3, 5, 7), both adjacent tiles must be passable:

```
Moving NE (direction 1):
  Must check N (0) and E (2) are passable

    +---+---+
    |   | N |  ← Must be passable
    +---+---+
    | E | X |  ← X = destination
    +---+---+
        ↑ Must be passable
```

## Movement Cost Calculation

### Base Cost Formula

```c
int CalculateMovementCost(Tile tile, Entity mover, int direction) {
    EntityTemplate template = mover.GetTemplate();

    // Base surface cost
    int cost = template.MovementCosts[tile.SurfaceType];

    // Entity-specific modifier
    cost += mover.EntityProperties.GetMovementCostModifier(tile.SurfaceType);

    // Process-level base cost
    cost += BaseMovementCost;

    // Structure traversal
    if (tile.HasStructure()) {
        cost += template.StructureTraversalCost;
    }

    // Diagonal multiplier (√2 ≈ 1.41)
    if ((direction & 1) != 0) {
        cost = (int)(cost * 1.41f);
    }

    // Direction change penalty
    if (DirectionChangeCost != 0) {
        int dirDiff = Abs(direction - previousDirection);
        if (dirDiff > 4) dirDiff = 8 - dirDiff;
        cost += dirDiff * DirectionChangeCost;
    }

    // Difficult terrain flag (0x1000)
    if ((tile.Flags & 0x1000) != 0 && mover.IsPlayerControlled) {
        cost *= 8;
    }

    // Occupied tile penalty
    if (tile.HasActor()) {
        cost += 2;
    }

    return cost;
}
```

### AI Safety Adjustments

For AI units, pathfinding considers tactical positioning:

```c
// From AIConfig
float SafetyPathfindingMult;     // +0x140 - How much safety affects path
int HiddenFromOpponentBonus;     // +0x148 - Bonus for fog of war tiles

// Applied to G cost:
if (!mover.IsPlayerControlled && TileScores.TryGetValue(tile, out score)) {
    cost -= (int)(score.SafetyScore * SafetyPathfindingMult);

    if (!score.IsVisibleToOpponentsHere) {
        cost -= HiddenFromOpponentBonus;

        if (mover.IsHiddenToPlayer()) {
            cost -= HiddenFromOpponentBonus;  // Double bonus
        }
    }
}
```

## Heuristic Function

Uses diagonal Manhattan distance:

```c
int GetDiagonalManhattanDistanceTo(Tile from, Tile to) {
    int dx = Abs(to.X - from.X);
    int dy = Abs(to.Y - from.Y);

    // Diagonal distance estimation
    int diagonal = Min(dx, dy);
    int straight = Abs(dx - dy);

    return diagonal * 14 + straight * 10;  // 14 ≈ 10 × √2
}
```

## PathfindingManager

Pool manager for pathfinding processes.

```c
public class PathfindingManager {
    // Singleton access
    static PathfindingManager Instance;

    // Get a pathfinding process from pool
    PathfindingProcess Get();

    // Return process to pool
    void ReturnProcess(PathfindingProcess process);
}
```

## Path Modifiers

Post-processing modifiers smooth and optimize paths.

### FunnelModifier

Applies funnel algorithm for smoother paths through portals/doors.

```c
// Creates portals at tile boundaries and funnels path
void Apply(List<Vector3> path);
```

### SimplifyModifier

Removes unnecessary waypoints using line-of-sight checks.

```c
// Removes intermediate points if direct path exists
void Apply(List<Vector3> path);
```

### SplineModifier

Converts path to smooth spline curve.

```c
// Interpolates path with Catmull-Rom splines
void Apply(List<Vector3> path);
```

### WalkThroughStructureModifier

Adjusts path when entering/exiting structures.

```c
// Aligns path to structure entry points
void Apply(List<Vector3> path);
```

## Surface Types

Movement costs vary by surface type (from EntityTemplate.MovementCosts[]):

| Surface Type | Index | Typical Cost |
|--------------|-------|--------------|
| Default | 0 | 10 |
| Road | 1 | 8 |
| Rough | 2 | 15 |
| Water/Shallow | 3 | 20+ |
| Impassable | 4 | int.MaxValue |

## Constants

```c
const int GRID_SIZE = 64;           // Max pathfinding grid dimension
const int OPEN_LIST_CAPACITY = 2048; // Priority queue capacity (0x800)
const float DIAGONAL_COST_MULT = 1.41421356f;  // √2
const int OCCUPIED_TILE_PENALTY = 2; // Extra cost for tiles with units
```

## Usage Example

```c
// Get pathfinder from pool
PathfindingProcess pathfinder = PathfindingManager.Instance.Get();

// Find path
List<Vector3> path = new List<Vector3>();
bool success = pathfinder.FindPath(
    startTile,
    goalTile,
    moverEntity,
    path,
    currentDirection,
    remainingAP,      // 0 for unlimited
    ignoreAllies
);

if (success) {
    // Apply modifiers
    FunnelModifier.Apply(path);
    SimplifyModifier.Apply(path);

    // Use path...
}

// Return to pool
PathfindingManager.Instance.ReturnProcess(pathfinder);
```

## Modding Hooks

### Modify Movement Costs

```csharp
[HarmonyPatch(typeof(EntityProperties), "GetMovementCostModifier")]
class MovementCostPatch {
    static void Postfix(EntityProperties __instance, int surfaceType, ref int __result) {
        // Reduce all movement costs by 2
        __result = Math.Max(0, __result - 2);
    }
}
```

### Custom Traversability

```csharp
[HarmonyPatch(typeof(PathfindingProcess), "IsTraversable")]
class TraversablePatch {
    static void Postfix(Tile tile, Entity mover, ref bool __result) {
        // Allow flying units to pass through blocked tiles
        if (mover.HasTag("flying") && !tile.HasStructure()) {
            __result = true;
        }
    }
}
```

### Override AI Path Costs

```csharp
[HarmonyPatch(typeof(PathfindingProcess), "ProcessNode")]
class AIPathPatch {
    static void Prefix(PathfindingProcess __instance, Tile tile, Entity mover) {
        // Log AI pathfinding decisions
        if (!mover.IsPlayerControlled) {
            Logger.Msg($"AI evaluating tile {tile.GetTilePos()}");
        }
    }
}
```
