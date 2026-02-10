# TileMap

`Menace.SDK.TileMap` -- Static class for tile and map operations in tactical combat. Provides safe access to tile queries, cover checks, visibility, and map traversal.

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
public const int COVER_HALF = 1;
public const int COVER_FULL = 2;
public const int COVER_ELEVATED = 3;
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
public static GameObj GetTile(int x, int y)
```

Get a tile at specific grid coordinates.

**Parameters:**
- `x` - X coordinate (0 to Width-1)
- `y` - Y coordinate (0 to Height-1)

**Returns:** `GameObj` representing the tile, or `GameObj.Null` if out of bounds.

### GetTileAtWorldPos

```csharp
public static GameObj GetTileAtWorldPos(Vector3 worldPos)
```

Get the tile at a world position.

**Parameters:**
- `worldPos` - World position (uses X and Z components)

**Returns:** `GameObj` representing the tile at that position.

### GetTileInfo

```csharp
public static TileInfo GetTileInfo(int x, int y)
public static TileInfo GetTileInfo(GameObj tile)
```

Get detailed information about a tile including elevation, blocking status, occupant, and visibility.

**Returns:** `TileInfo` with all tile properties, or `null` if tile is invalid.

### GetCover

```csharp
public static int GetCover(int x, int y, int direction)
public static int GetCover(GameObj tile, int direction)
```

Get the cover value in a specific direction (0-7).

**Parameters:**
- `x`, `y` - Tile coordinates, or `tile` - The tile GameObj
- `direction` - Direction index (use DIR_* constants)

**Returns:** Cover type (COVER_NONE, COVER_HALF, COVER_FULL, or COVER_ELEVATED).

### GetAllCover

```csharp
public static int[] GetAllCover(int x, int y)
public static int[] GetAllCover(GameObj tile)
```

Get cover values in all 8 directions.

**Returns:** Array of 8 cover values indexed by direction.

### IsVisibleToFaction

```csharp
public static bool IsVisibleToFaction(int x, int y, int factionId)
public static bool IsVisibleToFaction(GameObj tile, int factionId)
```

Check if a tile is visible to a specific faction.

**Parameters:**
- `factionId` - Faction ID to check visibility for

**Returns:** `true` if the tile is visible to the faction.

### IsVisibleToPlayer

```csharp
public static bool IsVisibleToPlayer(int x, int y)
```

Check if a tile is visible to the player (faction 1 or 2).

**Returns:** `true` if the tile is visible to player factions.

### IsBlocked

```csharp
public static bool IsBlocked(int x, int y)
public static bool IsBlocked(GameObj tile)
```

Check if a tile is blocked (impassable).

**Returns:** `true` if the tile cannot be traversed.

### HasActor

```csharp
public static bool HasActor(int x, int y)
public static bool HasActor(GameObj tile)
```

Check if a tile has an actor on it.

**Returns:** `true` if an actor occupies the tile.

### GetActorOnTile

```csharp
public static GameObj GetActorOnTile(int x, int y)
public static GameObj GetActorOnTile(GameObj tile)
```

Get the actor occupying a tile.

**Returns:** `GameObj` of the actor, or `GameObj.Null` if empty.

### GetNeighbor

```csharp
public static GameObj GetNeighbor(int x, int y, int direction)
public static GameObj GetNeighbor(GameObj tile, int direction)
```

Get the neighboring tile in a specific direction.

**Parameters:**
- `direction` - Direction index (0-7, use DIR_* constants)

**Returns:** `GameObj` of the neighbor tile, or `GameObj.Null` if out of bounds.

### GetAllNeighbors

```csharp
public static GameObj[] GetAllNeighbors(int x, int y)
public static GameObj[] GetAllNeighbors(GameObj tile)
```

Get all 8 neighboring tiles.

**Returns:** Array of 8 `GameObj` tiles indexed by direction.

### GetDirectionTo

```csharp
public static int GetDirectionTo(int fromX, int fromY, int toX, int toY)
public static int GetDirectionTo(GameObj fromTile, GameObj toTile)
```

Get the direction from one tile to another.

**Returns:** Direction index (0-7), or -1 if invalid.

### GetDistance

```csharp
public static float GetDistance(int x1, int y1, int x2, int y2)
public static float GetDistance(GameObj tile1, GameObj tile2)
```

Get the Euclidean distance between two tiles.

**Returns:** Distance in tiles, or -1 if invalid.

### GetManhattanDistance

```csharp
public static int GetManhattanDistance(int x1, int y1, int x2, int y2)
```

Get the Manhattan distance between two tiles.

**Returns:** Sum of absolute differences in X and Y coordinates.

### TileToWorld

```csharp
public static Vector3 TileToWorld(int x, int y, float elevation = 0f)
```

Convert tile coordinates to world position (center of tile).

**Parameters:**
- `x`, `y` - Tile coordinates
- `elevation` - Optional Y elevation (default 0)

**Returns:** `Vector3` world position at the center of the tile.

### WorldToTile

```csharp
public static (int x, int y) WorldToTile(Vector3 worldPos)
```

Convert world position to tile coordinates.

**Returns:** Tuple of (x, y) tile coordinates.

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

**Returns:** Cover name ("None", "Half", "Full", "Elevated") or "Unknown".

## Types

### TileInfo

```csharp
public class TileInfo
{
    public int X { get; set; }
    public int Y { get; set; }
    public float Elevation { get; set; }
    public bool IsBlocked { get; set; }
    public bool HasActor { get; set; }
    public string ActorName { get; set; }
    public int[] CoverValues { get; set; }       // Cover per direction (0-7)
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
    DevConsole.Log($"Tile ({info.X}, {info.Y})");
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
var (x, y) = TileMap.WorldToTile(actor.GetPosition());

// Get cover in all directions
var cover = TileMap.GetAllCover(x, y);
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
var (tileX, tileY) = TileMap.WorldToTile(worldPos);
DevConsole.Log($"Actor is on tile ({tileX}, {tileY})");

// Get world position from tile
var targetTile = TileMap.GetTile(20, 20);
var tileInfo = TileMap.GetTileInfo(targetTile);
var targetWorld = TileMap.TileToWorld(20, 20, tileInfo.Elevation);
DevConsole.Log($"Tile center is at {targetWorld}");
```

### Calculating distances and directions

```csharp
// Get distance and direction between two tiles
int fromX = 5, fromY = 5;
int toX = 10, toY = 8;

var distance = TileMap.GetDistance(fromX, fromY, toX, toY);
var manhattan = TileMap.GetManhattanDistance(fromX, fromY, toX, toY);
var direction = TileMap.GetDirectionTo(fromX, fromY, toX, toY);

DevConsole.Log($"Distance: {distance:F1} tiles");
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

- `tile <x> <y>` - Get detailed tile information (coordinates, elevation, blocked status, visibility, occupant, effects)
- `cover <x> <y>` - Get cover values for a tile in all 8 directions
- `mapinfo` - Get current map dimensions and fog of war status
- `blocked <x> <y>` - Check if a tile is blocked (impassable)
- `visible <x> <y>` - Check if a tile is visible to the player
- `dist <x1> <y1> <x2> <y2>` - Get distance, Manhattan distance, and direction between two tiles
- `whostile <x> <y>` - Show who occupies a tile
