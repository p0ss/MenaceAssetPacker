using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for pathfinding operations in tactical combat.
/// Provides safe access to path finding, movement cost calculation, and traversability checks.
///
/// Based on reverse engineering findings:
/// - PathfindingManager (singleton pool) with PathfindingProcess objects
/// - PathfindingManager.RequestProcess() to get a process (NOT Get())
/// - A* algorithm with surface costs, structure penalties, direction change costs
/// - PathfindingNode grid 64x64 max size
/// - Direction passed as enum type, not int
/// </summary>
public static class Pathfinding
{
    // Cached types
    private static GameType _pathfindingManagerType;
    private static GameType _pathfindingProcessType;
    private static GameType _actorType;
    private static GameType _tileType;
    private static GameType _directionType;

    /// <summary>
    /// Surface types in the game (SurfaceType enum).
    /// </summary>
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

    /// <summary>
    /// Cover types in the game (CoverType enum).
    /// </summary>
    public enum CoverType
    {
        None = 0,
        Light = 1,
        Medium = 2,
        Heavy = 3
    }

    // Diagonal cost multiplier
    public const float DIAGONAL_COST_MULT = 1.41421356f;

    /// <summary>
    /// Path result from FindPath operation.
    /// </summary>
    public class PathResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public List<Vector3> Waypoints { get; set; } = new();
        public int TotalCost { get; set; }
        public int TileCount { get; set; }
    }

    /// <summary>
    /// Movement cost information for a tile.
    /// </summary>
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

    /// <summary>
    /// Find a path from start to goal tile for an entity.
    /// </summary>
    public static PathResult FindPath(GameObj mover, int startX, int startY, int goalX, int goalY, int maxAP = 0)
    {
        var result = new PathResult();

        if (mover.IsNull)
        {
            result.Error = "No mover entity";
            return result;
        }

        try
        {
            EnsureTypesLoaded();

            var startTile = TileMap.GetTile(startX, startY);
            var goalTile = TileMap.GetTile(goalX, goalY);

            if (startTile.IsNull || goalTile.IsNull)
            {
                result.Error = "Invalid start or goal tile";
                return result;
            }

            // Get pathfinding manager
            var pmType = _pathfindingManagerType?.ManagedType;
            if (pmType == null)
            {
                result.Error = "PathfindingManager type not found";
                return result;
            }

            var instanceProp = pmType.GetProperty("s_Singleton", BindingFlags.Public | BindingFlags.Static);
            var pm = instanceProp?.GetValue(null);
            if (pm == null)
            {
                result.Error = "PathfindingManager instance not found";
                return result;
            }

            // Get a process from pool - use RequestProcess() not Get()
            var requestProcessMethod = pmType.GetMethod("RequestProcess", BindingFlags.Public | BindingFlags.Instance);
            var process = requestProcessMethod?.Invoke(pm, null);
            if (process == null)
            {
                result.Error = "Could not get pathfinding process";
                return result;
            }

            try
            {
                // Create output list - must use Il2CppSystem.Collections.Generic.List<Vector3>
                var moverType = _actorType?.ManagedType;
                var moverProxy = moverType != null ? GetManagedProxy(mover, moverType) : null;
                var startProxy = GetManagedProxy(startTile, _tileType?.ManagedType);
                var goalProxy = GetManagedProxy(goalTile, _tileType?.ManagedType);

                if (moverProxy == null || startProxy == null || goalProxy == null)
                {
                    result.Error = "Could not create managed proxies";
                    return result;
                }

                // Get current facing direction as int, then convert to Direction enum
                int directionInt = EntityMovement.GetFacing(mover);
                var directionEnum = ConvertToDirectionEnum(directionInt);
                if (directionEnum == null)
                {
                    result.Error = "Could not convert direction to enum";
                    return result;
                }

                // Call FindPath
                var findPathMethod = process.GetType().GetMethod("FindPath", BindingFlags.Public | BindingFlags.Instance);
                if (findPathMethod == null)
                {
                    result.Error = "FindPath method not found";
                    return result;
                }

                // Create Il2Cpp list for output - FindPath expects Il2CppSystem.Collections.Generic.List<Vector3>
                var il2cppListType = typeof(Il2CppSystem.Collections.Generic.List<Vector3>);
                var pathList = Activator.CreateInstance(il2cppListType);

                var success = findPathMethod.Invoke(process, new object[]
                {
                    startProxy, goalProxy, moverProxy, pathList, directionEnum, maxAP, false
                });

                result.Success = (bool)success;

                if (result.Success)
                {
                    // Extract waypoints from the Il2Cpp list
                    var countProp = il2cppListType.GetProperty("Count");
                    var indexer = il2cppListType.GetProperty("Item");
                    int count = (int)countProp.GetValue(pathList);

                    for (int i = 0; i < count; i++)
                    {
                        var wp = (Vector3)indexer.GetValue(pathList, new object[] { i });
                        result.Waypoints.Add(wp);
                    }
                    result.TileCount = count;
                }
                else
                {
                    result.Error = "No path found";
                }
            }
            finally
            {
                // Return process to pool
                var returnMethod = pmType.GetMethod("ReturnProcess", BindingFlags.Public | BindingFlags.Instance);
                returnMethod?.Invoke(pm, new[] { process });
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            ModError.ReportInternal("Pathfinding.FindPath", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Find a path for the active actor to a destination.
    /// </summary>
    public static PathResult FindPath(int goalX, int goalY, int maxAP = 0)
    {
        var actor = TacticalController.GetActiveActor();
        if (actor.IsNull)
            return new PathResult { Error = "No active actor" };

        var pos = EntityMovement.GetPosition(actor);
        if (!pos.HasValue)
            return new PathResult { Error = "Could not get actor position" };

        return FindPath(actor, pos.Value.x, pos.Value.y, goalX, goalY, maxAP);
    }

    /// <summary>
    /// Check if a tile can be entered by an entity from a given direction.
    /// </summary>
    public static bool CanEnter(GameObj mover, int x, int y, int fromDirection = -1)
    {
        if (mover.IsNull) return false;

        try
        {
            var tile = TileMap.GetTile(x, y);
            if (tile.IsNull) return false;

            // Basic checks
            if (TileMap.IsBlocked(tile)) return false;
            if (TileMap.HasActor(tile)) return false;

            // Check traversability via reflection if available
            EnsureTypesLoaded();

            var tileType = _tileType?.ManagedType;
            if (tileType != null)
            {
                var proxy = GetManagedProxy(tile, tileType);
                var moverProxy = GetManagedProxy(mover, _actorType?.ManagedType);

                var canEnterMethod = tileType.GetMethod("CanBeEnteredBy", BindingFlags.Public | BindingFlags.Instance);
                if (canEnterMethod != null && moverProxy != null)
                {
                    return (bool)canEnterMethod.Invoke(proxy, new[] { moverProxy });
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Pathfinding.CanEnter", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get movement cost for a tile for an entity.
    /// </summary>
    public static MovementCostInfo GetMovementCost(GameObj mover, int x, int y)
    {
        var result = new MovementCostInfo { X = x, Y = y };

        try
        {
            var tile = TileMap.GetTile(x, y);
            if (tile.IsNull)
            {
                result.IsBlocked = true;
                return result;
            }

            result.IsBlocked = TileMap.IsBlocked(tile);
            result.HasActor = TileMap.HasActor(tile);

            if (result.IsBlocked)
            {
                result.TotalCost = int.MaxValue;
                return result;
            }

            // Get surface type
            result.Surface = GetSurfaceType(x, y);
            result.SurfaceTypeName = GetSurfaceTypeName(result.Surface);

            // Get base movement cost - default costs per surface type
            result.BaseCost = GetBaseCostForSurface(result.Surface);
            result.TotalCost = result.BaseCost;

            // Add penalty for occupied tile
            if (result.HasActor)
                result.TotalCost += 2;

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("Pathfinding.GetMovementCost", "Failed", ex);
            result.TotalCost = int.MaxValue;
            return result;
        }
    }

    /// <summary>
    /// Get the surface type at a tile position.
    /// </summary>
    public static SurfaceType GetSurfaceType(int x, int y)
    {
        try
        {
            var mapObj = TileMap.GetMap();
            if (mapObj.IsNull) return SurfaceType.Concrete;

            var mapType = GameType.Find("Menace.Tactical.Map")?.ManagedType;
            if (mapType == null) return SurfaceType.Concrete;

            var proxy = GetManagedProxy(mapObj, mapType);
            if (proxy == null) return SurfaceType.Concrete;

            var getSurfaceMethod = mapType.GetMethod("GetSurfaceTypeAtPos", BindingFlags.Public | BindingFlags.Instance);
            if (getSurfaceMethod != null)
            {
                var worldPos = TileMap.TileToWorld(x, y, 0);
                var result = getSurfaceMethod.Invoke(proxy, new object[] { worldPos });
                var intValue = Convert.ToInt32(result);
                if (Enum.IsDefined(typeof(SurfaceType), intValue))
                    return (SurfaceType)intValue;
            }

            return SurfaceType.Concrete;
        }
        catch
        {
            return SurfaceType.Concrete;
        }
    }

    /// <summary>
    /// Get surface type name.
    /// </summary>
    public static string GetSurfaceTypeName(SurfaceType surfaceType)
    {
        return surfaceType.ToString();
    }

    /// <summary>
    /// Get base movement cost for a surface type.
    /// Note: EntityTemplate.MovementCosts property does not exist in the game.
    /// This uses reasonable default costs per surface type.
    /// </summary>
    private static int GetBaseCostForSurface(SurfaceType surfaceType)
    {
        // Default movement costs per surface type
        return surfaceType switch
        {
            SurfaceType.Concrete => 10,
            SurfaceType.Metal => 10,
            SurfaceType.Sand => 15,
            SurfaceType.Earth => 12,
            SurfaceType.Snow => 15,
            SurfaceType.Water => 25,
            SurfaceType.Ruins => 18,
            SurfaceType.SandStone => 12,
            SurfaceType.Mud => 20,
            SurfaceType.Grass => 10,
            SurfaceType.Glass => 12,
            SurfaceType.Forest => 15,
            SurfaceType.Rock => 14,
            SurfaceType.DirtRoad => 8,
            _ => 10
        };
    }

    /// <summary>
    /// Get all tiles reachable within a given AP cost.
    /// </summary>
    public static List<(int x, int y, int cost)> GetReachableTiles(GameObj mover, int maxAP)
    {
        var result = new List<(int x, int y, int cost)>();

        if (mover.IsNull) return result;

        var pos = EntityMovement.GetPosition(mover);
        if (!pos.HasValue) return result;

        var mapInfo = TileMap.GetMapInfo();
        if (mapInfo == null) return result;

        int startX = pos.Value.x;
        int startY = pos.Value.y;

        // Simple flood fill with cost tracking
        var visited = new Dictionary<(int, int), int>();
        var queue = new Queue<(int x, int y, int cost)>();
        queue.Enqueue((startX, startY, 0));
        visited[(startX, startY)] = 0;

        while (queue.Count > 0)
        {
            var (x, y, cost) = queue.Dequeue();

            // Check all 8 directions
            for (int dir = 0; dir < 8; dir++)
            {
                var (dx, dy) = GetDirectionOffset(dir);
                int nx = x + dx;
                int ny = y + dy;

                // Bounds check
                if (nx < 0 || ny < 0 || nx >= mapInfo.Width || ny >= mapInfo.Height)
                    continue;

                // Already visited with lower cost
                if (visited.TryGetValue((nx, ny), out int prevCost) && prevCost <= cost)
                    continue;

                // Can we enter this tile?
                if (!CanEnter(mover, nx, ny, (dir + 4) % 8))
                    continue;

                // Calculate movement cost
                var moveCost = GetMovementCost(mover, nx, ny);
                int tileCost = moveCost.TotalCost;

                // Diagonal movement costs more
                if (dir % 2 == 1)
                    tileCost = (int)(tileCost * DIAGONAL_COST_MULT);

                int newCost = cost + tileCost;
                if (newCost > maxAP)
                    continue;

                visited[(nx, ny)] = newCost;
                result.Add((nx, ny, newCost));
                queue.Enqueue((nx, ny, newCost));
            }
        }

        return result;
    }

    /// <summary>
    /// Get direction offset for a direction index.
    /// </summary>
    private static (int dx, int dy) GetDirectionOffset(int direction)
    {
        return direction switch
        {
            0 => (0, 1),    // North
            1 => (1, 1),    // Northeast
            2 => (1, 0),    // East
            3 => (1, -1),   // Southeast
            4 => (0, -1),   // South
            5 => (-1, -1),  // Southwest
            6 => (-1, 0),   // West
            7 => (-1, 1),   // Northwest
            _ => (0, 0)
        };
    }

    /// <summary>
    /// Calculate simple Manhattan distance cost (no obstacles).
    /// </summary>
    public static int EstimateCost(int fromX, int fromY, int toX, int toY, int baseCost = 10)
    {
        int dx = Math.Abs(toX - fromX);
        int dy = Math.Abs(toY - fromY);
        int diagonal = Math.Min(dx, dy);
        int straight = Math.Abs(dx - dy);
        return diagonal * (int)(baseCost * DIAGONAL_COST_MULT) + straight * baseCost;
    }

    /// <summary>
    /// Register console commands for Pathfinding SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // path <x> <y> - Find path to destination
        DevConsole.RegisterCommand("path", "<x> <y>", "Find path to destination for selected actor", args =>
        {
            if (args.Length < 2)
                return "Usage: path <x> <y>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
                return "Invalid coordinates";

            var result = FindPath(x, y);
            if (!result.Success)
                return $"No path found: {result.Error}";

            return $"Path found: {result.TileCount} waypoints\n" +
                   $"Total cost: {result.TotalCost} AP";
        });

        // canenter <x> <y> - Check if tile can be entered
        DevConsole.RegisterCommand("canenter", "<x> <y>", "Check if selected actor can enter tile", args =>
        {
            if (args.Length < 2)
                return "Usage: canenter <x> <y>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
                return "Invalid coordinates";

            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            var canEnter = CanEnter(actor, x, y);
            return $"Can enter ({x}, {y}): {canEnter}";
        });

        // movecost <x> <y> - Get movement cost for tile
        DevConsole.RegisterCommand("movecost", "<x> <y>", "Get movement cost for tile", args =>
        {
            if (args.Length < 2)
                return "Usage: movecost <x> <y>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
                return "Invalid coordinates";

            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            var cost = GetMovementCost(actor, x, y);
            if (cost.IsBlocked)
                return $"Tile ({x}, {y}) is blocked";

            return $"Movement cost for ({x}, {y}):\n" +
                   $"  Surface: {cost.SurfaceTypeName}\n" +
                   $"  Base cost: {cost.BaseCost}\n" +
                   $"  Has actor: {cost.HasActor}\n" +
                   $"  Total: {cost.TotalCost}";
        });

        // surface <x> <y> - Get surface type
        DevConsole.RegisterCommand("surface", "<x> <y>", "Get surface type at tile", args =>
        {
            if (args.Length < 2)
                return "Usage: surface <x> <y>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
                return "Invalid coordinates";

            var type = GetSurfaceType(x, y);
            return $"Surface at ({x}, {y}): {type} ({(int)type})";
        });

        // reachable <ap> - Show reachable tiles count
        DevConsole.RegisterCommand("reachable", "<ap>", "Count tiles reachable within AP", args =>
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            int maxAP = 50;
            if (args.Length > 0 && int.TryParse(args[0], out int ap))
                maxAP = ap;

            var tiles = GetReachableTiles(actor, maxAP);
            return $"Tiles reachable within {maxAP} AP: {tiles.Count}";
        });

        // estimate <x1> <y1> <x2> <y2> - Estimate path cost
        DevConsole.RegisterCommand("estimate", "<x1> <y1> <x2> <y2>", "Estimate movement cost between tiles", args =>
        {
            if (args.Length < 4)
                return "Usage: estimate <x1> <y1> <x2> <y2>";
            if (!int.TryParse(args[0], out int x1) || !int.TryParse(args[1], out int y1) ||
                !int.TryParse(args[2], out int x2) || !int.TryParse(args[3], out int y2))
                return "Invalid coordinates";

            var cost = EstimateCost(x1, y1, x2, y2);
            var dist = TileMap.GetManhattanDistance(x1, y1, x2, y2);
            return $"Estimated cost from ({x1},{y1}) to ({x2},{y2}):\n" +
                   $"  Manhattan distance: {dist}\n" +
                   $"  Estimated AP: {cost}";
        });
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _pathfindingManagerType ??= GameType.Find("Menace.Tactical.PathfindingManager");
        _pathfindingProcessType ??= GameType.Find("Menace.Tactical.PathfindingProcess");
        _actorType ??= GameType.Find("Menace.Tactical.Actor");
        _tileType ??= GameType.Find("Menace.Tactical.Tile");
        _directionType ??= GameType.Find("Menace.Tactical.Direction");
    }

    /// <summary>
    /// Convert an integer direction (0-7) to the game's Direction enum type.
    /// </summary>
    private static object ConvertToDirectionEnum(int directionInt)
    {
        try
        {
            EnsureTypesLoaded();
            var dirType = _directionType?.ManagedType;
            if (dirType == null || !dirType.IsEnum) return null;
            return Enum.ToObject(dirType, directionInt);
        }
        catch
        {
            return null;
        }
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
