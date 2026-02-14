using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for tile and map operations in tactical combat.
/// Provides safe access to tile queries, cover checks, visibility, and map traversal.
///
/// Based on reverse engineering findings:
/// - Map : BaseMap&lt;Tile&gt; with max 42x42 tiles
/// - Tile.GetCover(Direction, Entity, EntityProperties, bool realCover) @ 0x180680b20
/// - Tile.HasActor() @ 0x180681cd0
/// - Tile.HasLineOfSightTo(other, flags) @ 0x180681d70
/// - Tile.IsVisibleToFaction(factionId) @ 0x180682140
/// - Tile.IsVisibleToPlayer() for player visibility check
/// - Map.GetTile(x, z) via Tiles array
/// - Map.GetTileAtPos(Vector3) for world position lookup
/// - TacticalManager.Instance.Map @ +0x28
///
/// COORDINATE SYSTEM NOTE:
/// The game uses X/Z coordinates for tiles (Y is elevation/height).
/// TileInfo.X = game's X coordinate
/// TileInfo.Z = game's Z coordinate (formerly named Y in SDK)
/// </summary>
public static class TileMap
{
    // Cached types
    private static GameType _tileType;
    private static GameType _mapType;
    private static GameType _tacticalManagerType;
    private static GameType _directionType;

    // Direction constants (clockwise from North)
    public const int DIR_NORTH = 0;
    public const int DIR_NORTHEAST = 1;
    public const int DIR_EAST = 2;
    public const int DIR_SOUTHEAST = 3;
    public const int DIR_SOUTH = 4;
    public const int DIR_SOUTHWEST = 5;
    public const int DIR_WEST = 6;
    public const int DIR_NORTHWEST = 7;

    // Cover types (matches game's CoverType enum)
    public const int COVER_NONE = 0;
    public const int COVER_LIGHT = 1;
    public const int COVER_MEDIUM = 2;
    public const int COVER_HEAVY = 3;

    // Tile field offsets from tile-map-system.md
    private const uint OFFSET_TILE_POS_X = 0x10;
    private const uint OFFSET_TILE_POS_Y = 0x14;
    private const uint OFFSET_TILE_ELEVATION = 0x18;
    private const uint OFFSET_TILE_FLAGS = 0x1C;
    private const uint OFFSET_TILE_ENTITY_PROVIDED_COVER = 0x20;
    private const uint OFFSET_TILE_COVER_VALUES = 0x28;  // int[8]
    private const uint OFFSET_TILE_HALF_COVER_FLAGS = 0x30;  // bool[4]
    private const uint OFFSET_TILE_OCCUPANT = 0x50;
    private const uint OFFSET_TILE_VISIBILITY_MASK = 0x58;
    private const uint OFFSET_TILE_BLOCKS_LOS = 0x60;
    private const uint OFFSET_TILE_EFFECTS = 0x68;

    // Tile flags
    private const uint FLAG_BLOCKED = 0x01;
    private const uint FLAG_ISOLATED = 0x02;
    private const uint FLAG_TEMP_OCCUPIED = 0x04;
    private const uint FLAG_HAS_LOS_BLOCKER = 0x800;

    // Map constants
    public const int MAX_MAP_SIZE = 42;
    public const float TILE_SIZE = 8.0f;

    /// <summary>
    /// Tile information structure.
    /// Note: X and Z map to game's X and Z coordinates respectively.
    /// The game uses Y for elevation/height, not horizontal position.
    /// </summary>
    public class TileInfo
    {
        /// <summary>Game's X coordinate (horizontal).</summary>
        public int X { get; set; }
        /// <summary>Game's Z coordinate (horizontal depth). Note: Game uses X/Z for horizontal, Y for elevation.</summary>
        public int Z { get; set; }
        /// <summary>Tile elevation (game's Y axis).</summary>
        public float Elevation { get; set; }
        public bool IsBlocked { get; set; }
        public bool HasActor { get; set; }
        public string ActorName { get; set; }
        public int[] CoverValues { get; set; }  // Cover per direction (0-7): None=0, Light=1, Medium=2, Heavy=3
        public bool[] HalfCoverFlags { get; set; }  // Half cover (4 cardinal)
        public bool IsVisibleToPlayer { get; set; }
        public bool BlocksLOS { get; set; }
        public bool HasEffects { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Map information structure.
    /// </summary>
    public class MapInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public bool UseFogOfWar { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Get the current tactical map.
    /// </summary>
    public static GameObj GetMap()
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return GameObj.Null;

            var instanceProp = tmType.GetProperty("s_Singleton", BindingFlags.Public | BindingFlags.Static);
            var tm = instanceProp?.GetValue(null);
            if (tm == null) return GameObj.Null;

            var getMapMethod = tmType.GetMethod("GetMap", BindingFlags.Public | BindingFlags.Instance);
            var map = getMapMethod?.Invoke(tm, null);
            if (map == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)map).Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TileMap.GetMap", "Failed", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get map dimensions and info.
    /// </summary>
    public static MapInfo GetMapInfo()
    {
        try
        {
            EnsureTypesLoaded();

            var mapObj = GetMap();
            if (mapObj.IsNull) return null;

            var mapType = _mapType?.ManagedType;
            if (mapType == null) return null;

            var proxy = GetManagedProxy(mapObj, mapType);
            if (proxy == null) return null;

            var info = new MapInfo { Pointer = mapObj.Pointer };

            var getSizeXMethod = mapType.GetMethod("GetSizeX", BindingFlags.Public | BindingFlags.Instance);
            var getSizeZMethod = mapType.GetMethod("GetSizeZ", BindingFlags.Public | BindingFlags.Instance);
            var isUsingFogMethod = mapType.GetMethod("IsUsingFogOfWar", BindingFlags.Public | BindingFlags.Instance);

            if (getSizeXMethod != null) info.Width = (int)getSizeXMethod.Invoke(proxy, null);
            if (getSizeZMethod != null) info.Height = (int)getSizeZMethod.Invoke(proxy, null);
            if (isUsingFogMethod != null) info.UseFogOfWar = (bool)isUsingFogMethod.Invoke(proxy, null);

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TileMap.GetMapInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get a tile at specific coordinates.
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    public static GameObj GetTile(int x, int z)
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return GameObj.Null;

            var instanceProp = tmType.GetProperty("s_Singleton", BindingFlags.Public | BindingFlags.Static);
            var tm = instanceProp?.GetValue(null);
            if (tm == null) return GameObj.Null;

            var getMapMethod = tmType.GetMethod("GetMap", BindingFlags.Public | BindingFlags.Instance);
            var map = getMapMethod?.Invoke(tm, null);
            if (map == null) return GameObj.Null;

            var getTileMethod = map.GetType().GetMethod("GetTile",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(int), typeof(int) }, null);

            var tile = getTileMethod?.Invoke(map, new object[] { x, z });
            if (tile == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)tile).Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TileMap.GetTile", $"Failed for ({x}, {z})", ex);
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get tile at a world position.
    /// Uses native Map.GetTileAtPos when available for accurate results.
    /// </summary>
    public static GameObj GetTileAtWorldPos(Vector3 worldPos)
    {
        try
        {
            EnsureTypesLoaded();

            var mapObj = GetMap();
            if (mapObj.IsNull) goto fallback;

            var mapType = _mapType?.ManagedType;
            if (mapType == null) goto fallback;

            var proxy = GetManagedProxy(mapObj, mapType);
            if (proxy == null) goto fallback;

            // Try native GetTileAtPos(Vector3)
            var getTileAtPosMethod = mapType.GetMethod("GetTileAtPos",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(Vector3) }, null);

            if (getTileAtPosMethod != null)
            {
                var tile = getTileAtPosMethod.Invoke(proxy, new object[] { worldPos });
                if (tile != null)
                    return new GameObj(((Il2CppObjectBase)tile).Pointer);
            }
        }
        catch
        {
            // Fall through to manual calculation
        }

        fallback:
        int x = (int)(worldPos.x / TILE_SIZE);
        int z = (int)(worldPos.z / TILE_SIZE);
        return GetTile(x, z);
    }

    /// <summary>
    /// Get detailed information about a tile.
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    public static TileInfo GetTileInfo(int x, int z)
    {
        var tile = GetTile(x, z);
        return GetTileInfo(tile);
    }

    /// <summary>
    /// Get detailed information about a tile.
    /// </summary>
    public static TileInfo GetTileInfo(GameObj tile)
    {
        if (tile.IsNull) return null;

        try
        {
            EnsureTypesLoaded();

            var info = new TileInfo
            {
                Pointer = tile.Pointer,
                X = tile.ReadInt(OFFSET_TILE_POS_X),
                Z = tile.ReadInt(OFFSET_TILE_POS_Y),  // Game stores Z in POS_Y field
                Elevation = tile.ReadFloat(OFFSET_TILE_ELEVATION)
            };

            var flags = (uint)tile.ReadInt(OFFSET_TILE_FLAGS);
            info.IsBlocked = (flags & FLAG_BLOCKED) != 0;
            info.BlocksLOS = (flags & FLAG_HAS_LOS_BLOCKER) != 0;

            // Check for actor via reflection
            var tileType = _tileType?.ManagedType;
            if (tileType != null)
            {
                var proxy = GetManagedProxy(tile, tileType);
                if (proxy != null)
                {
                    var hasActorMethod = tileType.GetMethod("HasActor", BindingFlags.Public | BindingFlags.Instance);
                    if (hasActorMethod != null)
                        info.HasActor = (bool)hasActorMethod.Invoke(proxy, null);

                    if (info.HasActor)
                    {
                        var getActorMethod = tileType.GetMethod("GetActor", BindingFlags.Public | BindingFlags.Instance);
                        var actor = getActorMethod?.Invoke(proxy, null);
                        if (actor != null)
                        {
                            var actorObj = new GameObj(((Il2CppObjectBase)actor).Pointer);
                            info.ActorName = actorObj.GetName();
                        }
                    }

                    var hasEffectMethod = tileType.GetMethod("HasEffect", BindingFlags.Public | BindingFlags.Instance);
                    if (hasEffectMethod != null)
                        info.HasEffects = (bool)hasEffectMethod.Invoke(proxy, null);
                }
            }

            // Read visibility mask
            var visibilityMask = Marshal.ReadInt64(tile.Pointer + (int)OFFSET_TILE_VISIBILITY_MASK);
            info.IsVisibleToPlayer = (visibilityMask & 0x6) != 0;  // Bits 1 and 2 are player factions

            return info;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TileMap.GetTileInfo", "Failed", ex);
            return null;
        }
    }

    /// <summary>
    /// Get cover value in a specific direction (0-7).
    /// Returns: None=0, Light=1, Medium=2, Heavy=3
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    /// <param name="direction">Direction index (0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW)</param>
    public static int GetCover(int x, int z, int direction)
    {
        var tile = GetTile(x, z);
        return GetCover(tile, direction);
    }

    /// <summary>
    /// Get cover value in a specific direction (0-7).
    /// Returns: None=0, Light=1, Medium=2, Heavy=3
    /// </summary>
    /// <param name="tile">The tile to check</param>
    /// <param name="direction">Direction index (0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW)</param>
    public static int GetCover(GameObj tile, int direction)
    {
        if (tile.IsNull || direction < 0 || direction > 7) return 0;

        try
        {
            EnsureTypesLoaded();

            var tileType = _tileType?.ManagedType;
            if (tileType == null) return 0;

            var proxy = GetManagedProxy(tile, tileType);
            if (proxy == null) return 0;

            // GetCover(Direction _dir, Entity _specificToEntity, EntityProperties _entityProperties, Boolean _realCover)
            var getCoverMethod = tileType.GetMethod("GetCover", BindingFlags.Public | BindingFlags.Instance);
            if (getCoverMethod != null)
            {
                // Convert direction int to Direction enum
                object directionEnum = direction;
                if (_directionType?.ManagedType != null)
                {
                    directionEnum = Enum.ToObject(_directionType.ManagedType, direction);
                }

                // Invoke with 4 parameters: Direction, Entity (null), EntityProperties (null), bool realCover (true)
                var result = getCoverMethod.Invoke(proxy, new object[] { directionEnum, null, null, true });
                return Convert.ToInt32(result);
            }

            return 0;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TileMap.GetCover", "Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Get cover in all 8 directions.
    /// Returns array of cover values: None=0, Light=1, Medium=2, Heavy=3
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    public static int[] GetAllCover(int x, int z)
    {
        var tile = GetTile(x, z);
        return GetAllCover(tile);
    }

    /// <summary>
    /// Get cover in all 8 directions.
    /// </summary>
    public static int[] GetAllCover(GameObj tile)
    {
        var result = new int[8];
        if (tile.IsNull) return result;

        for (int dir = 0; dir < 8; dir++)
        {
            result[dir] = GetCover(tile, dir);
        }
        return result;
    }

    /// <summary>
    /// Check if a tile is visible to a specific faction.
    /// Uses native Tile.IsVisibleToFaction when available.
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    /// <param name="factionId">Faction ID to check visibility for</param>
    public static bool IsVisibleToFaction(int x, int z, int factionId)
    {
        var tile = GetTile(x, z);
        return IsVisibleToFaction(tile, factionId);
    }

    /// <summary>
    /// Check if a tile is visible to a specific faction.
    /// Uses native Tile.IsVisibleToFaction when available.
    /// </summary>
    public static bool IsVisibleToFaction(GameObj tile, int factionId)
    {
        if (tile.IsNull) return false;

        try
        {
            EnsureTypesLoaded();

            var tileType = _tileType?.ManagedType;
            if (tileType != null)
            {
                var proxy = GetManagedProxy(tile, tileType);
                if (proxy != null)
                {
                    // Try native IsVisibleToFaction(int factionId)
                    var isVisibleMethod = tileType.GetMethod("IsVisibleToFaction",
                        BindingFlags.Public | BindingFlags.Instance,
                        null, new[] { typeof(int) }, null);

                    if (isVisibleMethod != null)
                    {
                        return (bool)isVisibleMethod.Invoke(proxy, new object[] { factionId });
                    }
                }
            }

            // Fallback to bitmask
            var visibilityMask = Marshal.ReadInt64(tile.Pointer + (int)OFFSET_TILE_VISIBILITY_MASK);
            ulong bit = 1UL << factionId;
            return (visibilityMask & (long)bit) != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if a tile is visible to the player.
    /// Uses native Tile.IsVisibleToPlayer when available.
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    public static bool IsVisibleToPlayer(int x, int z)
    {
        var tile = GetTile(x, z);
        return IsVisibleToPlayer(tile);
    }

    /// <summary>
    /// Check if a tile is visible to the player.
    /// Uses native Tile.IsVisibleToPlayer when available.
    /// </summary>
    public static bool IsVisibleToPlayer(GameObj tile)
    {
        if (tile.IsNull) return false;

        try
        {
            EnsureTypesLoaded();

            var tileType = _tileType?.ManagedType;
            if (tileType != null)
            {
                var proxy = GetManagedProxy(tile, tileType);
                if (proxy != null)
                {
                    // Try native IsVisibleToPlayer method
                    var isVisibleMethod = tileType.GetMethod("IsVisibleToPlayer", BindingFlags.Public | BindingFlags.Instance);
                    if (isVisibleMethod != null)
                    {
                        return (bool)isVisibleMethod.Invoke(proxy, null);
                    }
                }
            }
        }
        catch
        {
            // Fall through to faction check
        }

        // Fallback to faction checks
        return IsVisibleToFaction(tile, 1) || IsVisibleToFaction(tile, 2);
    }

    /// <summary>
    /// Check if a tile is blocked (impassable).
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    public static bool IsBlocked(int x, int z)
    {
        var tile = GetTile(x, z);
        return IsBlocked(tile);
    }

    /// <summary>
    /// Check if a tile is blocked (impassable).
    /// </summary>
    public static bool IsBlocked(GameObj tile)
    {
        if (tile.IsNull) return true;

        try
        {
            var flags = (uint)tile.ReadInt(OFFSET_TILE_FLAGS);
            return (flags & FLAG_BLOCKED) != 0;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Check if a tile has an actor on it.
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    public static bool HasActor(int x, int z)
    {
        var tile = GetTile(x, z);
        return HasActor(tile);
    }

    /// <summary>
    /// Check if a tile has an actor on it.
    /// </summary>
    public static bool HasActor(GameObj tile)
    {
        if (tile.IsNull) return false;

        try
        {
            EnsureTypesLoaded();

            var tileType = _tileType?.ManagedType;
            if (tileType == null) return false;

            var proxy = GetManagedProxy(tile, tileType);
            if (proxy == null) return false;

            var hasActorMethod = tileType.GetMethod("HasActor", BindingFlags.Public | BindingFlags.Instance);
            if (hasActorMethod != null)
                return (bool)hasActorMethod.Invoke(proxy, null);

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get the actor on a tile.
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    public static GameObj GetActorOnTile(int x, int z)
    {
        var tile = GetTile(x, z);
        return GetActorOnTile(tile);
    }

    /// <summary>
    /// Get the actor on a tile.
    /// </summary>
    public static GameObj GetActorOnTile(GameObj tile)
    {
        if (tile.IsNull) return GameObj.Null;

        try
        {
            EnsureTypesLoaded();

            var tileType = _tileType?.ManagedType;
            if (tileType == null) return GameObj.Null;

            var proxy = GetManagedProxy(tile, tileType);
            if (proxy == null) return GameObj.Null;

            var getActorMethod = tileType.GetMethod("GetActor", BindingFlags.Public | BindingFlags.Instance);
            var actor = getActorMethod?.Invoke(proxy, null);
            if (actor == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)actor).Pointer);
        }
        catch
        {
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get the neighbor tile in a direction.
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    /// <param name="direction">Direction index (0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW)</param>
    public static GameObj GetNeighbor(int x, int z, int direction)
    {
        var tile = GetTile(x, z);
        return GetNeighbor(tile, direction);
    }

    /// <summary>
    /// Get the neighbor tile in a direction.
    /// </summary>
    public static GameObj GetNeighbor(GameObj tile, int direction)
    {
        if (tile.IsNull || direction < 0 || direction > 7) return GameObj.Null;

        try
        {
            EnsureTypesLoaded();

            var tileType = _tileType?.ManagedType;
            if (tileType == null) return GameObj.Null;

            var proxy = GetManagedProxy(tile, tileType);
            if (proxy == null) return GameObj.Null;

            var getNextMethod = tileType.GetMethod("GetNextTile", BindingFlags.Public | BindingFlags.Instance);
            var neighbor = getNextMethod?.Invoke(proxy, new object[] { direction });
            if (neighbor == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)neighbor).Pointer);
        }
        catch
        {
            return GameObj.Null;
        }
    }

    /// <summary>
    /// Get all 8 neighbors of a tile.
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate (horizontal depth)</param>
    public static GameObj[] GetAllNeighbors(int x, int z)
    {
        var tile = GetTile(x, z);
        return GetAllNeighbors(tile);
    }

    /// <summary>
    /// Get all 8 neighbors of a tile.
    /// </summary>
    public static GameObj[] GetAllNeighbors(GameObj tile)
    {
        var result = new GameObj[8];
        for (int dir = 0; dir < 8; dir++)
        {
            result[dir] = GetNeighbor(tile, dir);
        }
        return result;
    }

    /// <summary>
    /// Get the direction from one tile to another.
    /// </summary>
    /// <param name="fromX">Source tile X coordinate</param>
    /// <param name="fromZ">Source tile Z coordinate</param>
    /// <param name="toX">Target tile X coordinate</param>
    /// <param name="toZ">Target tile Z coordinate</param>
    public static int GetDirectionTo(int fromX, int fromZ, int toX, int toZ)
    {
        var fromTile = GetTile(fromX, fromZ);
        var toTile = GetTile(toX, toZ);
        return GetDirectionTo(fromTile, toTile);
    }

    /// <summary>
    /// Get the direction from one tile to another.
    /// </summary>
    public static int GetDirectionTo(GameObj fromTile, GameObj toTile)
    {
        if (fromTile.IsNull || toTile.IsNull) return -1;

        try
        {
            EnsureTypesLoaded();

            var tileType = _tileType?.ManagedType;
            if (tileType == null) return -1;

            var fromProxy = GetManagedProxy(fromTile, tileType);
            if (fromProxy == null) return -1;

            var toProxy = GetManagedProxy(toTile, tileType);
            if (toProxy == null) return -1;

            var getDirectionMethod = tileType.GetMethod("GetDirectionTo", BindingFlags.Public | BindingFlags.Instance);
            if (getDirectionMethod != null)
            {
                var result = getDirectionMethod.Invoke(fromProxy, new[] { toProxy });
                return Convert.ToInt32(result);
            }

            return -1;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Get the distance between two tiles (in tile units).
    /// Note: Game's GetDistanceTo returns Int32, not float.
    /// </summary>
    /// <param name="x1">First tile X coordinate</param>
    /// <param name="z1">First tile Z coordinate</param>
    /// <param name="x2">Second tile X coordinate</param>
    /// <param name="z2">Second tile Z coordinate</param>
    public static int GetDistance(int x1, int z1, int x2, int z2)
    {
        var tile1 = GetTile(x1, z1);
        var tile2 = GetTile(x2, z2);
        return GetDistance(tile1, tile2);
    }

    /// <summary>
    /// Get the distance between two tiles (in tile units).
    /// Note: Game's GetDistanceTo returns Int32, not float.
    /// </summary>
    public static int GetDistance(GameObj tile1, GameObj tile2)
    {
        if (tile1.IsNull || tile2.IsNull) return -1;

        try
        {
            EnsureTypesLoaded();

            var tileType = _tileType?.ManagedType;
            if (tileType == null) return -1;

            var proxy1 = GetManagedProxy(tile1, tileType);
            if (proxy1 == null) return -1;

            var proxy2 = GetManagedProxy(tile2, tileType);
            if (proxy2 == null) return -1;

            var getDistanceMethod = tileType.GetMethod("GetDistanceTo", BindingFlags.Public | BindingFlags.Instance);
            if (getDistanceMethod != null)
            {
                var result = getDistanceMethod.Invoke(proxy1, new[] { proxy2 });
                return Convert.ToInt32(result);
            }

            return -1;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Get Manhattan distance between two tiles.
    /// </summary>
    /// <param name="x1">First tile X coordinate</param>
    /// <param name="z1">First tile Z coordinate</param>
    /// <param name="x2">Second tile X coordinate</param>
    /// <param name="z2">Second tile Z coordinate</param>
    public static int GetManhattanDistance(int x1, int z1, int x2, int z2)
    {
        return Math.Abs(x2 - x1) + Math.Abs(z2 - z1);
    }

    /// <summary>
    /// Convert tile coordinates to world position.
    /// </summary>
    /// <param name="x">Game's X coordinate</param>
    /// <param name="z">Game's Z coordinate</param>
    /// <param name="elevation">Elevation (game's Y axis)</param>
    public static Vector3 TileToWorld(int x, int z, float elevation = 0f)
    {
        return new Vector3(
            x * TILE_SIZE + TILE_SIZE / 2f,
            elevation,
            z * TILE_SIZE + TILE_SIZE / 2f
        );
    }

    /// <summary>
    /// Convert world position to tile coordinates.
    /// </summary>
    /// <returns>Tuple of (x, z) tile coordinates</returns>
    public static (int x, int z) WorldToTile(Vector3 worldPos)
    {
        int x = (int)(worldPos.x / TILE_SIZE);
        int z = (int)(worldPos.z / TILE_SIZE);
        return (x, z);
    }

    /// <summary>
    /// Get direction name from direction index.
    /// </summary>
    public static string GetDirectionName(int direction)
    {
        return direction switch
        {
            0 => "North",
            1 => "Northeast",
            2 => "East",
            3 => "Southeast",
            4 => "South",
            5 => "Southwest",
            6 => "West",
            7 => "Northwest",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Get cover type name.
    /// </summary>
    public static string GetCoverName(int coverType)
    {
        return coverType switch
        {
            COVER_NONE => "None",
            COVER_LIGHT => "Light",
            COVER_MEDIUM => "Medium",
            COVER_HEAVY => "Heavy",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Register console commands for TileMap SDK.
    /// Called by DevConsole during initialization.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // tile <x> <y> - Get tile info
        DevConsole.RegisterCommand("tile", "<x> <y>", "Get tile information", args =>
        {
            if (args.Length < 2)
                return "Usage: tile <x> <y>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
                return "Invalid coordinates";

            var info = GetTileInfo(x, y);
            if (info == null)
                return $"Tile at ({x}, {y}) not found";

            var lines = new List<string>
            {
                $"Tile ({info.X}, {info.Z}) - Elevation: {info.Elevation:F1}",
                $"Blocked: {info.IsBlocked}, Visible: {info.IsVisibleToPlayer}",
                $"HasActor: {info.HasActor}" + (info.HasActor ? $" ({info.ActorName})" : ""),
                $"Effects: {info.HasEffects}, Blocks LOS: {info.BlocksLOS}"
            };
            return string.Join("\n", lines);
        });

        // cover <x> <y> - Get cover values
        DevConsole.RegisterCommand("cover", "<x> <y>", "Get cover values for a tile", args =>
        {
            if (args.Length < 2)
                return "Usage: cover <x> <y>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
                return "Invalid coordinates";

            var cover = GetAllCover(x, y);
            var lines = new List<string> { $"Cover at ({x}, {y}):" };
            for (int dir = 0; dir < 8; dir++)
            {
                lines.Add($"  {GetDirectionName(dir)}: {GetCoverName(cover[dir])}");
            }
            return string.Join("\n", lines);
        });

        // mapinfo - Get map information
        DevConsole.RegisterCommand("mapinfo", "", "Get current map information", args =>
        {
            var info = GetMapInfo();
            if (info == null)
                return "No map available";

            return $"Map: {info.Width}x{info.Height} tiles\n" +
                   $"Fog of War: {info.UseFogOfWar}";
        });

        // blocked <x> <y> - Check if tile is blocked
        DevConsole.RegisterCommand("blocked", "<x> <y>", "Check if tile is blocked", args =>
        {
            if (args.Length < 2)
                return "Usage: blocked <x> <y>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
                return "Invalid coordinates";

            return $"Tile ({x}, {y}) blocked: {IsBlocked(x, y)}";
        });

        // visible <x> <y> - Check tile visibility
        DevConsole.RegisterCommand("visible", "<x> <y>", "Check if tile is visible to player", args =>
        {
            if (args.Length < 2)
                return "Usage: visible <x> <y>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
                return "Invalid coordinates";

            return $"Tile ({x}, {y}) visible: {IsVisibleToPlayer(x, y)}";
        });

        // dist <x1> <z1> <x2> <z2> - Get distance between tiles
        DevConsole.RegisterCommand("dist", "<x1> <z1> <x2> <z2>", "Get distance between tiles", args =>
        {
            if (args.Length < 4)
                return "Usage: dist <x1> <z1> <x2> <z2>";
            if (!int.TryParse(args[0], out int x1) || !int.TryParse(args[1], out int z1) ||
                !int.TryParse(args[2], out int x2) || !int.TryParse(args[3], out int z2))
                return "Invalid coordinates";

            var distance = GetDistance(x1, z1, x2, z2);
            var manhattan = GetManhattanDistance(x1, z1, x2, z2);
            var direction = GetDirectionTo(x1, z1, x2, z2);

            return $"Distance from ({x1},{z1}) to ({x2},{z2}):\n" +
                   $"  Distance: {distance}\n" +
                   $"  Manhattan: {manhattan}\n" +
                   $"  Direction: {GetDirectionName(direction)}";
        });

        // whostile <x> <y> - Show who is on a tile
        DevConsole.RegisterCommand("whostile", "<x> <y>", "Show who occupies a tile", args =>
        {
            if (args.Length < 2)
                return "Usage: whostile <x> <y>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
                return "Invalid coordinates";

            if (!HasActor(x, y))
                return $"Tile ({x}, {y}) is empty";

            var actor = GetActorOnTile(x, y);
            if (actor.IsNull)
                return $"Tile ({x}, {y}) has no actor";

            var name = actor.GetName() ?? "<unnamed>";
            return $"Tile ({x}, {y}) occupied by: {name}";
        });
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _tileType ??= GameType.Find("Menace.Tactical.Tile");
        _mapType ??= GameType.Find("Menace.Tactical.Map");
        _tacticalManagerType ??= GameType.Find("Menace.Tactical.TacticalManager");
        _directionType ??= GameType.Find("Menace.Tactical.Direction");
    }

    private static object GetManagedProxy(GameObj obj, Type managedType)
    {
        if (obj.IsNull || managedType == null) return null;

        try
        {
            var ptrCtor = managedType.GetConstructor(new[] { typeof(IntPtr) });
            return ptrCtor?.Invoke(new object[] { obj.Pointer });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Try to use native Tile.IsVisibleToPlayer() method.
    /// Returns null if method not available.
    /// </summary>
    private static bool? IsVisibleToPlayerNative(GameObj tile, Type tileType, object proxy)
    {
        if (tile.IsNull || tileType == null || proxy == null)
            return null;

        try
        {
            var isVisibleMethod = tileType.GetMethod("IsVisibleToPlayer",
                BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);

            if (isVisibleMethod != null)
            {
                return (bool)isVisibleMethod.Invoke(proxy, null);
            }
        }
        catch
        {
            // Fall through
        }

        return null;
    }
}
