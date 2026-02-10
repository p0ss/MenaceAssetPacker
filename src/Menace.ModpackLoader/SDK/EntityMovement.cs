using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// SDK extension for controlling entity movement in tactical combat.
///
/// Based on reverse engineering findings:
/// - Actor.MoveTo(Tile, MovementFlags, MovementAction) @ 0x1805e0a60
/// - Actor.CalculateTilesInMovementRange() @ 0x1805de100
/// - Actor.GetTilesMovedThisTurn() @ 0x1805df7c0
/// - Actor.IsMoving() @ 0x1805e0810
/// - PathfindingProcess.FindPath() for pathfinding
/// - Direction system: 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW
/// </summary>
public static class EntityMovement
{
    // Cached types
    private static GameType _actorType;
    private static GameType _tileType;
    private static GameType _tacticalManagerType;
    private static GameType _pathfindingProcessType;

    // Field offsets from actor-system.md
    private const uint OFFSET_ACTOR_START_TILE = 0xA0;
    private const uint OFFSET_ACTOR_CURRENT_TILE = 0xA8;
    private const uint OFFSET_ACTOR_DIRECTION = 0xB0;
    private const uint OFFSET_ACTOR_IS_MOVING = 0x167;
    private const uint OFFSET_ACTOR_CURRENT_AP = 0x148;
    private const uint OFFSET_ACTOR_AP_AT_TURN_START = 0x14C;
    private const uint OFFSET_ACTOR_TILES_MOVED = 0x154;
    private const uint OFFSET_ACTOR_MOVEMENT_MODE = 0x174;

    // Movement flags (from actor-system.md)
    [Flags]
    public enum MovementFlags
    {
        None = 0,
        EnterVehicle = 1,
        ExitVehicle = 2,
        Backward = 4,
        Instant = 8,        // Teleport (no animation)
        Sprint = 16
    }

    // Direction constants
    public const int DIR_NORTH = 0;
    public const int DIR_NORTHEAST = 1;
    public const int DIR_EAST = 2;
    public const int DIR_SOUTHEAST = 3;
    public const int DIR_SOUTH = 4;
    public const int DIR_SOUTHWEST = 5;
    public const int DIR_WEST = 6;
    public const int DIR_NORTHWEST = 7;

    /// <summary>
    /// Movement result with success status and details.
    /// </summary>
    public class MoveResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public int APCost { get; set; }
        public List<(int x, int y)> Path { get; set; }

        public static MoveResult Failed(string error) => new() { Success = false, Error = error };
        public static MoveResult Ok(int apCost = 0) => new() { Success = true, APCost = apCost };
    }

    /// <summary>
    /// Move an actor to the specified tile using pathfinding.
    /// </summary>
    /// <param name="actor">The actor to move</param>
    /// <param name="destX">Destination tile X</param>
    /// <param name="destY">Destination tile Y</param>
    /// <param name="flags">Movement flags (optional)</param>
    /// <returns>MoveResult with success status</returns>
    public static MoveResult MoveTo(GameObj actor, int destX, int destY, MovementFlags flags = MovementFlags.None)
    {
        if (actor.IsNull || !actor.IsAlive)
            return MoveResult.Failed("Invalid or dead actor");

        try
        {
            EnsureTypesLoaded();

            // Check if already moving
            if (IsMoving(actor))
                return MoveResult.Failed("Actor is already moving");

            // Get destination tile
            var destTile = GetTileAt(destX, destY);
            if (destTile.IsNull)
                return MoveResult.Failed($"Tile at ({destX}, {destY}) not found");

            // Get managed proxy for actor
            var actorType = _actorType?.ManagedType;
            if (actorType == null)
                return MoveResult.Failed("Actor type not available");

            var actorProxy = GetManagedProxy(actor, actorType);
            if (actorProxy == null)
                return MoveResult.Failed("Failed to get actor proxy");

            var tileProxy = GetManagedProxy(destTile, _tileType.ManagedType);
            if (tileProxy == null)
                return MoveResult.Failed("Failed to get tile proxy");

            // Find MoveTo method
            var moveToMethod = actorType.GetMethod("MoveTo", BindingFlags.Public | BindingFlags.Instance);
            if (moveToMethod == null)
                return MoveResult.Failed("MoveTo method not found");

            // Invoke MoveTo
            var result = moveToMethod.Invoke(actorProxy, new object[] { tileProxy, (int)flags, null });

            // MoveTo returns bool
            if (result is bool success && success)
            {
                ModError.Info("Menace.SDK", $"Actor moving to ({destX}, {destY})");
                return MoveResult.Ok();
            }

            return MoveResult.Failed("MoveTo returned false (path blocked or no AP)");
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityMovement.MoveTo", $"Failed to move to ({destX}, {destY})", ex);
            return MoveResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Teleport an actor instantly to the specified tile (no animation, no pathfinding).
    /// </summary>
    public static MoveResult Teleport(GameObj actor, int destX, int destY)
    {
        return MoveTo(actor, destX, destY, MovementFlags.Instant);
    }

    /// <summary>
    /// Stop an actor's current movement.
    /// </summary>
    public static bool Stop(GameObj actor)
    {
        if (actor.IsNull)
            return false;

        try
        {
            // Set IsMoving to false
            Marshal.WriteByte(actor.Pointer + (int)OFFSET_ACTOR_IS_MOVING, 0);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityMovement.Stop", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if an actor is currently moving.
    /// </summary>
    public static bool IsMoving(GameObj actor)
    {
        if (actor.IsNull)
            return false;

        try
        {
            return Marshal.ReadByte(actor.Pointer + (int)OFFSET_ACTOR_IS_MOVING) != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get the tiles within movement range for an actor.
    /// </summary>
    public static List<(int x, int y)> GetMovementRange(GameObj actor)
    {
        var result = new List<(int x, int y)>();

        if (actor.IsNull || !actor.IsAlive)
            return result;

        try
        {
            EnsureTypesLoaded();

            var actorType = _actorType?.ManagedType;
            if (actorType == null)
                return result;

            var actorProxy = GetManagedProxy(actor, actorType);
            if (actorProxy == null)
                return result;

            // Find CalculateTilesInMovementRange method
            var calcMethod = actorType.GetMethod("CalculateTilesInMovementRange",
                BindingFlags.Public | BindingFlags.Instance);

            if (calcMethod == null)
                return result;

            var tiles = calcMethod.Invoke(actorProxy, null);
            if (tiles == null)
                return result;

            // Iterate the result (IEnumerable<Tile>)
            var enumerator = tiles.GetType().GetMethod("GetEnumerator")?.Invoke(tiles, null);
            if (enumerator == null)
                return result;

            var moveNextMethod = enumerator.GetType().GetMethod("MoveNext");
            var currentProp = enumerator.GetType().GetProperty("Current");

            while ((bool)moveNextMethod.Invoke(enumerator, null))
            {
                var tile = currentProp.GetValue(enumerator);
                if (tile != null)
                {
                    var tilePos = GetTilePosition(new GameObj(((Il2CppObjectBase)tile).Pointer));
                    if (tilePos.HasValue)
                        result.Add(tilePos.Value);
                }
            }
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityMovement.GetMovementRange", "Failed", ex);
        }

        return result;
    }

    /// <summary>
    /// Get the path from an actor's current position to a destination.
    /// </summary>
    public static List<(int x, int y)> GetPath(GameObj actor, int destX, int destY)
    {
        var result = new List<(int x, int y)>();

        if (actor.IsNull)
            return result;

        try
        {
            EnsureTypesLoaded();

            // Get current and destination tiles
            var currentTile = GetActorTile(actor);
            var destTile = GetTileAt(destX, destY);

            if (currentTile.IsNull || destTile.IsNull)
                return result;

            // Use PathfindingManager to get path
            var pmType = GameType.Find("Menace.Tactical.PathfindingManager");
            var pmManaged = pmType?.ManagedType;
            if (pmManaged == null)
                return result;

            var instanceProp = pmManaged.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProp == null)
                return result;

            var pm = instanceProp.GetValue(null);
            if (pm == null)
                return result;

            // Get a pathfinding process
            var getMethod = pmManaged.GetMethod("Get", BindingFlags.Public | BindingFlags.Instance);
            if (getMethod == null)
                return result;

            var process = getMethod.Invoke(pm, null);
            if (process == null)
                return result;

            // Call FindPath
            var processType = process.GetType();
            var findPathMethod = processType.GetMethod("FindPath", BindingFlags.Public | BindingFlags.Instance);
            if (findPathMethod == null)
                return result;

            var actorProxy = GetManagedProxy(actor, _actorType.ManagedType);
            var startProxy = GetManagedProxy(currentTile, _tileType.ManagedType);
            var endProxy = GetManagedProxy(destTile, _tileType.ManagedType);

            var pathList = new List<Vector3>();
            var direction = actor.ReadInt(OFFSET_ACTOR_DIRECTION);
            var ap = actor.ReadInt(OFFSET_ACTOR_CURRENT_AP);

            var success = (bool)findPathMethod.Invoke(process, new object[]
            {
                startProxy, endProxy, actorProxy, pathList, direction, ap, false
            });

            if (success)
            {
                // Convert Vector3 path to tile coordinates
                foreach (var pos in pathList)
                {
                    var tileCoords = WorldToTile(pos);
                    if (tileCoords.HasValue)
                        result.Add(tileCoords.Value);
                }
            }

            // Return process to pool
            var returnMethod = pmManaged.GetMethod("ReturnProcess", BindingFlags.Public | BindingFlags.Instance);
            returnMethod?.Invoke(pm, new[] { process });
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityMovement.GetPath", "Failed", ex);
        }

        return result;
    }

    /// <summary>
    /// Set the facing direction of an actor.
    /// </summary>
    /// <param name="actor">The actor</param>
    /// <param name="direction">Direction (0-7, see DIR_* constants)</param>
    public static bool SetFacing(GameObj actor, int direction)
    {
        if (actor.IsNull || direction < 0 || direction > 7)
            return false;

        try
        {
            Marshal.WriteInt32(actor.Pointer + (int)OFFSET_ACTOR_DIRECTION, direction);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityMovement.SetFacing", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get the current facing direction of an actor.
    /// </summary>
    public static int GetFacing(GameObj actor)
    {
        if (actor.IsNull)
            return -1;

        return actor.ReadInt(OFFSET_ACTOR_DIRECTION);
    }

    /// <summary>
    /// Get the current tile position of an actor.
    /// </summary>
    public static (int x, int y)? GetPosition(GameObj actor)
    {
        if (actor.IsNull)
            return null;

        var tile = GetActorTile(actor);
        return GetTilePosition(tile);
    }

    /// <summary>
    /// Get remaining action points for an actor.
    /// </summary>
    public static int GetRemainingAP(GameObj actor)
    {
        if (actor.IsNull)
            return 0;

        return actor.ReadInt(OFFSET_ACTOR_CURRENT_AP);
    }

    /// <summary>
    /// Set action points for an actor.
    /// </summary>
    public static bool SetAP(GameObj actor, int ap)
    {
        if (actor.IsNull)
            return false;

        try
        {
            Marshal.WriteInt32(actor.Pointer + (int)OFFSET_ACTOR_CURRENT_AP, ap);
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntityMovement.SetAP", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get number of tiles moved this turn.
    /// </summary>
    public static int GetTilesMovedThisTurn(GameObj actor)
    {
        if (actor.IsNull)
            return 0;

        return actor.ReadInt(OFFSET_ACTOR_TILES_MOVED);
    }

    /// <summary>
    /// Get movement info for an actor.
    /// </summary>
    public static MovementInfo GetMovementInfo(GameObj actor)
    {
        if (actor.IsNull)
            return null;

        var position = GetPosition(actor);

        return new MovementInfo
        {
            Position = position,
            Direction = GetFacing(actor),
            IsMoving = IsMoving(actor),
            CurrentAP = GetRemainingAP(actor),
            APAtTurnStart = actor.ReadInt(OFFSET_ACTOR_AP_AT_TURN_START),
            TilesMovedThisTurn = GetTilesMovedThisTurn(actor),
            MovementMode = actor.ReadInt(OFFSET_ACTOR_MOVEMENT_MODE)
        };
    }

    public class MovementInfo
    {
        public (int x, int y)? Position { get; set; }
        public int Direction { get; set; }
        public string DirectionName => Direction switch
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
        public bool IsMoving { get; set; }
        public int CurrentAP { get; set; }
        public int APAtTurnStart { get; set; }
        public int TilesMovedThisTurn { get; set; }
        public int MovementMode { get; set; }
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _actorType ??= GameType.Find("Menace.Tactical.Actor");
        _tileType ??= GameType.Find("Menace.Tactical.Tile");
        _tacticalManagerType ??= GameType.Find("Menace.Tactical.TacticalManager");
        _pathfindingProcessType ??= GameType.Find("Menace.Tactical.PathfindingProcess");
    }

    private static GameObj GetTileAt(int x, int y)
    {
        try
        {
            EnsureTypesLoaded();

            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return GameObj.Null;

            var instanceProp = tmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var tm = instanceProp?.GetValue(null);
            if (tm == null) return GameObj.Null;

            var mapProp = tmType.GetProperty("Map", BindingFlags.Public | BindingFlags.Instance);
            var map = mapProp?.GetValue(tm);
            if (map == null) return GameObj.Null;

            var getTileMethod = map.GetType().GetMethod("GetTile",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(int), typeof(int) }, null);

            var tile = getTileMethod?.Invoke(map, new object[] { x, y });
            if (tile == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)tile).Pointer);
        }
        catch
        {
            return GameObj.Null;
        }
    }

    private static GameObj GetActorTile(GameObj actor)
    {
        if (actor.IsNull)
            return GameObj.Null;

        var tilePtr = actor.ReadPtr(OFFSET_ACTOR_CURRENT_TILE);
        return new GameObj(tilePtr);
    }

    private static (int x, int y)? GetTilePosition(GameObj tile)
    {
        if (tile.IsNull)
            return null;

        try
        {
            var tileType = _tileType?.ManagedType;
            if (tileType == null) return null;

            var proxy = GetManagedProxy(tile, tileType);
            if (proxy == null) return null;

            var xProp = tileType.GetProperty("X", BindingFlags.Public | BindingFlags.Instance);
            var yProp = tileType.GetProperty("Y", BindingFlags.Public | BindingFlags.Instance);

            if (xProp == null || yProp == null) return null;

            var x = (int)xProp.GetValue(proxy);
            var y = (int)yProp.GetValue(proxy);

            return (x, y);
        }
        catch
        {
            return null;
        }
    }

    private static (int x, int y)? WorldToTile(Vector3 worldPos)
    {
        // Approximate conversion - tiles are typically 1 unit apart
        // This may need adjustment based on actual game scale
        return ((int)Math.Round(worldPos.x), (int)Math.Round(worldPos.z));
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
}
