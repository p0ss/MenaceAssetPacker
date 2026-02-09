# Tile & Map System

## Overview

The tile and map system forms the foundation of Menace's tactical gameplay. Maps are 2D grids of tiles, each tracking cover, elevation, occupants, effects, and visibility.

## Architecture

```
Map : BaseMap<Tile>
├── Tile[,] array (max 42x42)
├── MapMagicObject (terrain generator)
├── MapElevationTexture (shader data)
└── Dimensions (Width, Height)

Tile : BaseTile
├── Position & Elevation
├── Cover values (8 directions)
├── Half-cover flags (4 cardinal)
├── Occupant (Entity reference)
├── Effects (fire, smoke, etc.)
├── Visibility bitmask
└── AI TileData
```

## Direction System

Menace uses an 8-direction system, clockwise from North:

```
    0 (N)
  7   1
6 (W)   2 (E)
  5   3
    4 (S)
```

| Value | Direction | Vector |
|-------|-----------|--------|
| 0 | North | (0, +1) |
| 1 | NorthEast | (+1, +1) |
| 2 | East | (+1, 0) |
| 3 | SouthEast | (+1, -1) |
| 4 | South | (0, -1) |
| 5 | SouthWest | (-1, -1) |
| 6 | West | (-1, 0) |
| 7 | NorthWest | (-1, +1) |

- **Cardinal directions**: 0, 2, 4, 6 (even numbers)
- **Diagonal directions**: 1, 3, 5, 7 (odd numbers)
- **Half-cover**: Only applies to cardinal directions (indices 0-3 map to N/E/S/W)

## BaseTile Class

Base class for all tiles with core positioning and cover data.

### BaseTile Field Layout

```c
public class BaseTile {
    // Object header                    // +0x00 - 0x0F
    Vector2Int TilePos;                 // +0x10 (x at +0x10, y at +0x14)
    float Elevation;                    // +0x18
    uint Flags;                         // +0x1C (see TileFlags below)
    int EntityProvidedCover;            // +0x20 (cover from entity on tile)
    // padding                          // +0x24
    CoverType[] CoverValues;            // +0x28 (int[8] - cover per direction)
    bool[] HalfCoverFlags;              // +0x30 (bool[4] - cardinal directions only)
    byte[] MovementBlocked;             // +0x38 (byte[8] - blocked per direction)
}
```

### TileFlags (offset +0x1C)

```c
[Flags]
enum TileFlags : uint {
    Blocked = 0x01,           // Tile is completely blocked
    Isolated = 0x02,          // Tile is isolated from main map
    TemporarilyOccupied = 0x04, // Reserved for movement
    // Bit 12 (0x1000) - Has full cover structure
    // Bit 13 (0x2000) - Related to cover calculation
}
```

### CoverType Enum

```c
enum CoverType {
    None = 0,
    Half = 1,      // Half cover (sandbags, low walls)
    Full = 2,      // Full cover (walls, large objects)
    Elevated = 3,  // Elevation-based cover
}
```

### Key BaseTile Methods

```c
// Get tile grid position
Vector2Int GetTilePos();                    // @ 1805caa60

// Get world position (includes elevation)
Vector3 GetPos();                           // @ 1805caa10

// Get world position without elevation
Vector3 GetPosWithoutElevation();           // @ 1805ca9c0

// Set blocked state
void SetBlocked(bool blocked);              // @ 1805caf50

// Check if blocked
bool IsBlocked();                           // Flags & 0x01

// Check movement blocked in direction
bool IsMovementBlocked(int direction);      // @ 1805cae00

// Set/get half cover (cardinal directions only, 0-3)
void SetHalfCover(int dir, bool value, bool updateNeighbors);  // @ 1805cb000

// Calculate cover from all 8 neighbors
void CalculateSurroundingCover();           // @ 1805c9e30

// Check if any cover exists
bool HasCover();                            // @ 1805caa80

// Check half cover in direction
bool HasHalfCoverInDir(int dir);           // @ 1805caaf0

// Get neighbor tile
BaseTile GetNextBaseTile(int direction);    // @ 1805c9070

// Get direction to another tile
int GetDirectionTo(BaseTile other);         // @ 1805ca5d0

// Distance calculations
float GetDistanceTo(BaseTile other);        // @ 1805ca840
int GetManhattanDistanceTo(BaseTile other); // @ 1805ca8f0
```

## Tile Class

Runtime tile extending BaseTile with occupant tracking, effects, and visibility.

### Tile Field Layout

```c
public class Tile : BaseTile {
    // BaseTile fields               // +0x00 - 0x3F
    List<HalfCover> HalfCoverInstances;  // +0x40 (visual half-cover objects)
    int EntityType;                      // +0x48 (default: 2)
    // padding                           // +0x4C
    OccupantHandle Occupant;             // +0x50 (reference with Entity at +0x10)
    ulong VisibilityMask;                // +0x58 (one bit per faction)
    // padding                           // +0x60
    List<TileEffectHandler> Effects;     // +0x68 (fire, smoke, supply, etc.)
    TileData AIData;                     // +0x70 (AI evaluation data)
}
```

### Visibility System

Visibility is tracked as a bitmask at offset +0x58:

```c
// Check if faction can see this tile
bool IsVisibleToFaction(byte factionIndex) {
    ulong mask = 1UL << factionIndex;
    return (VisibilityMask & mask) != 0;
}

// Faction indices typically:
// 0 = Player
// 1 = Enemy
// 2+ = Other factions
```

### Key Tile Methods

```c
// Occupant management
Entity GetEntity();                         // @ 180681ac0
Actor GetActor();                           // @ 1806809d0
bool HasActor();                            // @ 180681cd0
bool Occupy(OccupantHandle handle);         // @ 180682280
void Leave();                               // @ 180682170

// Cover calculation (includes entity-provided cover)
CoverType GetCover(int direction, Entity attacker, bool ignoreAllies);  // @ 180680b20
int GetCoverMask();                         // @ 180680a50

// Effects
void AddEffect(TileEffectHandler effect);   // @ 180680290
void RemoveEffect(TileEffectHandler effect); // @ 180682730
bool HasEffect();                           // @ 180681d10
TileEffectHandler GetEffect(Type type);     // @ 180681860

// Line of sight
bool HasLineOfSightTo(Tile target, byte flags);  // @ 180681d70
bool IsBlockingLineOfSight();               // @ 180681e50

// Visibility
bool IsVisibleToFaction(byte factionIndex); // @ 180682140
bool IsVisibleToPlayer();                   // @ 180682160
void AddVisibility(byte factionIndex);      // @ 1806805d0
void RemoveVisibility(byte factionIndex);   // @ 180682980
void ClearVisibility();                     // @ 1806809b0

// Movement validation
bool CanBeEntered();                        // @ 180680950
bool CanBeEnteredBy(Entity entity);         // @ 180680920
bool IsValidMovementDestination();          // @ 180682020

// Turn events
void OnTurnStart();                         // @ 1806824f0
void OnTurnEnd();                           // @ 180682440
void OnMovementFinished(Actor actor);       // @ 180682390

// Map access
Map GetMap();                               // @ 180681ad0 (via TacticalManager singleton)

// Neighbor access
Tile GetNextTile(int direction);            // @ 180681b20
```

## Map Class

Container for the tile grid with terrain integration.

### Map Field Layout

```c
public class Map : BaseMap<Tile> {
    // BaseMap<Tile> fields
    int Width;                          // +0x10
    int Height;                         // +0x14
    Tile[,] Tiles;                      // +0x18 (2D array)

    // Map-specific fields
    MapMagicObject MapMagic;            // +0x20 (terrain generator)
    // padding                          // +0x28
    MapElevationTexture ElevationTex;   // +0x30 (shader height data)
    bool UseFogOfWar;                   // +0x38 (default: true)
}
```

### Map Bounds

Maps have a maximum size of **42x42 tiles** (0x2A in hex), hardcoded in bounds checking.

### Key Map Methods

```c
// Tile access
Tile GetTileAtPos(Vector3 worldPos);        // @ 18062e160
Tile GetTile(int x, int y);                 // via Tiles array
bool IsInBounds(int x, int y);              // @ 18062e430

// Bounds
void ClampToBounds(ref Vector2Int pos);     // @ 18062ba60

// Elevation
float GetElevation(Vector3 pos);            // @ 18062d9c0
float SampleElevation(Vector3 pos);         // @ 18062e870
Vector3 GetSlopeEulerAngles(Vector3 pos);   // @ 18062dc40

// Surface types (from MapMagic)
SurfaceType GetSurfaceTypeAtPos(Vector3 pos); // @ 18062df00

// Updates
void UpdateElevation();                     // @ 18062f600
void UpdateBlockedTiles();                  // @ 18062f360
void UpdateWalkability();                   // @ 180630170
void UpdateVisibility();                    // @ 18062f9a0
void UpdateVisibilityForActor(Actor actor); // @ 18062f900

// Generation
IEnumerator GenerateMap();                  // @ 18062ca00 (12-state coroutine)
void Resize(int width, int height);         // @ 18062e7e0

// Queries
void QueryTilesInside(Bounds bounds, List<Tile> results);  // @ 18062e5c0
bool TryGetRandomDeploymentPos(out Vector2Int pos);        // @ 18062eff0

// Line of sight
void MakeMountainsBlockLineOfSight();       // @ 18062e4c0
```

## Cover Calculation

Cover is calculated per-direction, considering:

1. **Static cover** from terrain/structures (stored in CoverValues[])
2. **Half-cover** objects (HalfCoverFlags[])
3. **Entity-provided cover** (actors can provide cover to adjacent allies)
4. **Elevation difference** (higher tiles get cover bonus)

### CalculateSurroundingCover Algorithm

```c
void CalculateSurroundingCover() {
    // Skip if tile is blocked
    if ((Flags & 0x01) != 0) return;

    // Clear existing cover values
    Array.Fill(CoverValues, CoverType.None);

    // For each of 8 directions:
    for (int dir = 0; dir < 8; dir++) {
        Tile neighbor = GetNextTile(dir);

        if (neighbor == null) continue;

        // Check if neighbor is blocked (provides full cover)
        if (neighbor.IsBlocked || neighbor.HasFullCoverStructure) {
            CoverValues[dir] = CoverType.Full;
        }
        // Check elevation difference
        else if (neighbor.Elevation >= this.Elevation + ELEVATION_THRESHOLD) {
            CoverValues[dir] = CoverType.Elevated;
        }
        // Check entity-provided cover
        else if (neighbor.HasActor() && neighbor.GetActor().ProvidesCover()) {
            CoverValues[dir] = neighbor.GetActor().GetProvidedCover() - 1;
        }

        // For cardinal directions, also apply to adjacent diagonals
        if ((dir & 1) == 0) {  // Cardinal direction
            // Spread cover to adjacent diagonal directions
            int prevDir = (dir - 1 + 8) % 8;
            int nextDir = (dir + 1) % 8;
            // Apply reduced cover to diagonals...
        }
    }

    // Second pass: apply half-cover bonuses
    for (int dir = 0; dir < 8; dir++) {
        if (CoverValues[dir] < CoverType.Full) {
            if ((dir & 1) == 0) {  // Cardinal
                if (HalfCoverFlags[dir / 2]) {
                    CoverValues[dir] = max(CoverValues[dir], CoverType.Half);
                }
            }
        }
    }
}
```

## TacticalManager Singleton

Map is accessed via `TacticalManager` singleton:

```c
// Get current map
Map map = TacticalManager.Instance.Map;  // Instance at TypeInfo+0xB8, Map at +0x28
```

## Tile Effects

Effects are managed through `TileEffectHandler` instances:

```c
public abstract class TileEffectHandler {
    // Virtual methods for effect behavior
    virtual void AssignToTile(Tile tile);
    virtual void RemoveFromTile();
    virtual void OnActorEnter(Actor actor);
    virtual void OnTurnStart();
    virtual void OnTurnEnd();
}
```

Common effect types:
- Fire (damages units, spreads)
- Smoke (blocks LOS)
- Supply (resupplies ammo)
- Poison/Gas

## Coordinate Conversion

```c
// World position to tile position
const float TILE_SIZE = 8.0f;
Vector2Int WorldToTile(Vector3 worldPos) {
    int x = (int)(worldPos.x / TILE_SIZE);
    int y = (int)(worldPos.z / TILE_SIZE);
    return new Vector2Int(x, y);
}

// Tile position to world position (tile center)
Vector3 TileToWorld(Vector2Int tilePos, float elevation) {
    return new Vector3(
        tilePos.x * TILE_SIZE + TILE_SIZE / 2,
        elevation,
        tilePos.y * TILE_SIZE + TILE_SIZE / 2
    );
}
```

## Modding Hooks

### Intercept Tile Access

```csharp
[HarmonyPatch(typeof(Tile), "GetCover")]
class CoverPatch {
    static void Postfix(Tile __instance, int direction, ref CoverType __result) {
        // Modify cover calculation
        Logger.Msg($"Tile {__instance.GetTilePos()} cover in dir {direction}: {__result}");
    }
}
```

### Modify Movement Validation

```csharp
[HarmonyPatch(typeof(Tile), "CanBeEnteredBy")]
class MovementPatch {
    static void Postfix(Tile __instance, Entity entity, ref bool __result) {
        // Allow movement to normally blocked tiles
        if (entity.HasTag("flying")) {
            __result = true;
        }
    }
}
```

### Custom Tile Effects

```csharp
[HarmonyPatch(typeof(Tile), "AddEffect")]
class EffectPatch {
    static void Prefix(Tile __instance, TileEffectHandler effect) {
        Logger.Msg($"Adding effect {effect.GetType().Name} to {__instance.GetTilePos()}");
    }
}
```

## Key Constants

```c
const float TILE_SIZE = 8.0f;           // World units per tile
const int MAX_MAP_SIZE = 42;            // Maximum map dimension
const float ELEVATION_THRESHOLD = 2.0f; // Height diff for elevation cover
const int DIRECTIONS = 8;               // Total direction count
const int CARDINAL_DIRS = 4;            // Cardinal direction count
```
