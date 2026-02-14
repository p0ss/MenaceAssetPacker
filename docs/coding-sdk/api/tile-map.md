# TileMap

`Menace.SDK.TileMap` -- Static class for tile and map operations in tactical combat. Provides safe access to tile queries, cover checks, visibility, and map traversal.

## Coordinate System

**Important:** The game uses X/Z coordinates for horizontal tile positions, with Y representing elevation/height.

- `X` - Horizontal coordinate (left/right)
- `Z` - Horizontal coordinate (forward/back, depth)
- `Y` (Elevation) - Vertical height

All method parameters use `x` and `z` for horizontal tile coordinates.

## Constants

### Directions

Directions are numbered clockwise starting from North (0-7):

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

### Cover Types

```csharp
public const int COVER_NONE = 0;
public const int COVER_LIGHT = 1;
public const int COVER_MEDIUM = 2;
public const int COVER_HEAVY = 3;
```

### Map Constants

```csharp
public const int MAX_MAP_SIZE = 42;    // Maximum map dimension in tiles
public const float TILE_SIZE = 8.0f;   // World units per tile
```

## Methods

### GetMap

```csharp
public static GameObj GetMap()
```

Get the current tactical map object.

**Returns:** `GameObj` representing the map, or `GameObj.Null` if no map is loaded.

### GetMapInfo

```csharp
public static MapInfo GetMapInfo()
```

Get map dimensions and configuration.

**Returns:** `MapInfo` with width, height, and fog of war settings, or `null` if no map is loaded.

### GetTile

```csharp
public static GameObj GetTile(int x, int z)
```

Get a tile at specific grid coordinates.

**Parameters:**
- `x` - X coordinate (0 to Width-1)
- `z` - Z coordinate (0 to Height-1)

**Returns:** `GameObj` representing the tile, or `GameObj.Null` if out of bounds.

### GetTileAtWorldPos

```csharp
public static GameObj GetTileAtWorldPos(Vector3 worldPos)
```

Get the tile at a world position. Uses native `Map.GetTileAtPos` when available for accurate results.

**Parameters:**
- `worldPos` - World position (uses X and Z components)

**Returns:** `GameObj` representing the tile at that position.

### GetTileInfo

```csharp
public static TileInfo GetTileInfo(int x, int z)
public static TileInfo GetTileInfo(GameObj tile)
```

Get detailed information about a tile including elevation, blocking status, occupant, and visibility.

**Returns:** `TileInfo` with all tile properties, or `null` if tile is invalid.

### GetCover

```csharp
public static int GetCover(int x, int z, int direction)
public static int GetCover(GameObj tile, int direction)
```

Get the cover value in a specific direction (0-7).

**Parameters:**
- `x`, `z` - Tile coordinates, or `tile` - The tile GameObj
- `direction` - Direction index (use DIR_* constants)

**Returns:** Cover type (COVER_NONE, COVER_LIGHT, COVER_MEDIUM, or COVER_HEAVY).

### GetAllCover

```csharp
public static int[] GetAllCover(int x, int z)
public static int[] GetAllCover(GameObj tile)
```

Get cover values in all 8 directions.

**Returns:** Array of 8 cover values indexed by direction (None=0, Light=1, Medium=2, Heavy=3).

### IsVisibleToFaction

```csharp
public static bool IsVisibleToFaction(int x, int z, int factionId)
public static bool IsVisibleToFaction(GameObj tile, int factionId)
```

Check if a tile is visible to a specific faction. Uses native `Tile.IsVisibleToFaction` when available.

**Parameters:**
- `factionId` - Faction ID to check visibility for

**Returns:** `true` if the tile is visible to the faction.

### IsVisibleToPlayer

```csharp
public static bool IsVisibleToPlayer(int x, int z)
public static bool IsVisibleToPlayer(GameObj tile)
```

Check if a tile is visible to the player. Uses native `Tile.IsVisibleToPlayer` when available.

**Returns:** `true` if the tile is visible to player factions.

### IsBlocked

```csharp
public static bool IsBlocked(int x, int z)
public static bool IsBlocked(GameObj tile)
```

Check if a tile is blocked (impassable).

**Returns:** `true` if the tile cannot be traversed.

### HasActor

```csharp
public static bool HasActor(int x, int z)
public static bool HasActor(GameObj tile)
```

Check if a tile has an actor on it.

**Returns:** `true` if an actor occupies the tile.

### GetActorOnTile

```csharp
public static GameObj GetActorOnTile(int x, int z)
public static GameObj GetActorOnTile(GameObj tile)
```

Get the actor occupying a tile.

**Returns:** `GameObj` of the actor, or `GameObj.Null` if empty.

### GetNeighbor

```csharp
public static GameObj GetNeighbor(int x, int z, int direction)
public static GameObj GetNeighbor(GameObj tile, int direction)
```

Get the neighboring tile in a specific direction.

**Parameters:**
- `direction` - Direction index (0-7, use DIR_* constants)

**Returns:** `GameObj` of the neighbor tile, or `GameObj.Null` if out of bounds.

### GetAllNeighbors

```csharp
public static GameObj[] GetAllNeighbors(int x, int z)
public static GameObj[] GetAllNeighbors(GameObj tile)
```

Get all 8 neighboring tiles.

**Returns:** Array of 8 `GameObj` tiles indexed by direction.

### GetDirectionTo

```csharp
public static int GetDirectionTo(int fromX, int fromZ, int toX, int toZ)
public static int GetDirectionTo(GameObj fromTile, GameObj toTile)
```

Get the direction from one tile to another.

**Returns:** Direction index (0-7), or -1 if invalid.

### GetDistance

```csharp
public static int GetDistance(int x1, int z1, int x2, int z2)
public static int GetDistance(GameObj tile1, GameObj tile2)
```

Get the distance between two tiles in tile units.

**Note:** The game's `GetDistanceTo` method returns `Int32`, not float.

**Returns:** Distance in tiles, or -1 if invalid.

### GetManhattanDistance

```csharp
public static int GetManhattanDistance(int x1, int z1, int x2, int z2)
```

Get the Manhattan distance between two tiles.

**Returns:** Sum of absolute differences in X and Z coordinates.

### TileToWorld

```csharp
public static Vector3 TileToWorld(int x, int z, float elevation = 0f)
```

Convert tile coordinates to world position (center of tile).

**Parameters:**
- `x`, `z` - Tile coordinates
- `elevation` - Optional Y elevation (default 0)

**Returns:** `Vector3` world position at the center of the tile.

### WorldToTile

```csharp
public static (int x, int z) WorldToTile(Vector3 worldPos)
```

Convert world position to tile coordinates.

**Returns:** Tuple of (x, z) tile coordinates.

### GetDirectionName

```csharp
public static string GetDirectionName(int direction)
```

Get a human-readable name for a direction index.

**Returns:** Direction name ("North", "Northeast", etc.) or "Unknown".

### GetCoverName

```csharp
public static string GetCoverName(int coverType)
```

Get a human-readable name for a cover type.

**Returns:** Cover name ("None", "Light", "Medium", "Heavy") or "Unknown".

## Types

### TileInfo

```csharp
public class TileInfo
{
    public int X { get; set; }           // Game's X coordinate (horizontal)
    public int Z { get; set; }           // Game's Z coordinate (horizontal depth)
    public float Elevation { get; set; } // Tile elevation (game's Y axis)
    public bool IsBlocked { get; set; }
    public bool HasActor { get; set; }
    public string ActorName { get; set; }
    public int[] CoverValues { get; set; }       // Cover per direction (0-7): None=0, Light=1, Medium=2, Heavy=3
    public bool[] HalfCoverFlags { get; set; }   // Half cover (4 cardinal)
    public bool IsVisibleToPlayer { get; set; }
    public bool BlocksLOS { get; set; }
    public bool HasEffects { get; set; }
    public IntPtr Pointer { get; set; }
}
```

### MapInfo

```csharp
public class MapInfo
{
    public int Width { get; set; }
    public int Height { get; set; }
    public bool UseFogOfWar { get; set; }
    public IntPtr Pointer { get; set; }
}
```

## Examples

### Querying tile information

```csharp
// Get info for a specific tile
var info = TileMap.GetTileInfo(10, 15);
if (info != null)
{
    DevConsole.Log($"Tile ({info.X}, {info.Z})");
    DevConsole.Log($"  Elevation: {info.Elevation}");
    DevConsole.Log($"  Blocked: {info.IsBlocked}");
    DevConsole.Log($"  Visible: {info.IsVisibleToPlayer}");
    if (info.HasActor)
        DevConsole.Log($"  Occupant: {info.ActorName}");
}
```

### Checking cover around a position

```csharp
var actor = TacticalController.GetActiveActor();
var (x, z) = TileMap.WorldToTile(actor.GetPosition());

// Get cover in all directions
var cover = TileMap.GetAllCover(x, z);
for (int dir = 0; dir < 8; dir++)
{
    var name = TileMap.GetDirectionName(dir);
    var type = TileMap.GetCoverName(cover[dir]);
    DevConsole.Log($"  {name}: {type}");
}
```

### Finding nearby actors

```csharp
// Check all neighbors for actors
var tile = TileMap.GetTile(10, 10);
var neighbors = TileMap.GetAllNeighbors(tile);

for (int dir = 0; dir < 8; dir++)
{
    if (TileMap.HasActor(neighbors[dir]))
    {
        var actor = TileMap.GetActorOnTile(neighbors[dir]);
        var dirName = TileMap.GetDirectionName(dir);
        DevConsole.Log($"Actor {actor.GetName()} to the {dirName}");
    }
}
```

### Getting map dimensions

```csharp
var mapInfo = TileMap.GetMapInfo();
if (mapInfo != null)
{
    DevConsole.Log($"Map size: {mapInfo.Width}x{mapInfo.Height}");
    DevConsole.Log($"Fog of War: {mapInfo.UseFogOfWar}");
}
```

### Converting between world and tile coordinates

```csharp
// Get tile from actor position
var actor = TacticalController.GetActiveActor();
var worldPos = actor.GetPosition();
var (tileX, tileZ) = TileMap.WorldToTile(worldPos);
DevConsole.Log($"Actor is on tile ({tileX}, {tileZ})");

// Get world position from tile
var targetTile = TileMap.GetTile(20, 20);
var tileInfo = TileMap.GetTileInfo(targetTile);
var targetWorld = TileMap.TileToWorld(20, 20, tileInfo.Elevation);
DevConsole.Log($"Tile center is at {targetWorld}");
```

### Calculating distances and directions

```csharp
// Get distance and direction between two tiles
int fromX = 5, fromZ = 5;
int toX = 10, toZ = 8;

var distance = TileMap.GetDistance(fromX, fromZ, toX, toZ);
var manhattan = TileMap.GetManhattanDistance(fromX, fromZ, toX, toZ);
var direction = TileMap.GetDirectionTo(fromX, fromZ, toX, toZ);

DevConsole.Log($"Distance: {distance} tiles");
DevConsole.Log($"Manhattan: {manhattan} tiles");
DevConsole.Log($"Direction: {TileMap.GetDirectionName(direction)}");
```

### Checking line of sight and visibility

```csharp
// Check if tile is visible to player
if (TileMap.IsVisibleToPlayer(15, 20))
{
    DevConsole.Log("Tile is in player's line of sight");
}

// Check visibility for specific faction
int enemyFactionId = 1;
if (TileMap.IsVisibleToFaction(15, 20, enemyFactionId))
{
    DevConsole.Log("Tile is visible to enemy faction");
}
```

## Console Commands

The following console commands are available:

- `tile <x> <z>` - Get detailed tile information (coordinates, elevation, blocked status, visibility, occupant, effects)
- `cover <x> <z>` - Get cover values for a tile in all 8 directions
- `mapinfo` - Get current map dimensions and fog of war status
- `blocked <x> <z>` - Check if a tile is blocked (impassable)
- `visible <x> <z>` - Check if a tile is visible to the player
- `dist <x1> <z1> <x2> <z2>` - Get distance, Manhattan distance, and direction between two tiles
- `whostile <x> <z>` - Show who occupies a tile
