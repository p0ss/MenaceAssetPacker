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
/// - A* algorithm with surface costs, structure penalties, direction change costs
/// - PathfindingNode grid 64x64 max size
/// - Movement costs from EntityTemplate.MovementCosts[]
/// </summary>
public static class Pathfinding
{
    // Cached types
    private static GameType _pathfindingManagerType;
    private static GameType _pathfindingProcessType;
    private static GameType _entityTemplateType;
    private static GameType _actorType;
    private static GameType _tileType;

    // Surface types
    public const int SURFACE_DEFAULT = 0;
    public const int SURFACE_ROAD = 1;
    public const int SURFACE_ROUGH = 2;
    public const int SURFACE_WATER = 3;
    public const int SURFACE_IMPASSABLE = 4;

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
        public int SurfaceType { get; set; }
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

            var instanceProp = pmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var pm = instanceProp?.GetValue(null);
            if (pm == null)
            {
                result.Error = "PathfindingManager instance not found";
                return result;
            }

            // Get a process from pool
            var getMethod = pmType.GetMethod("Get", BindingFlags.Public | BindingFlags.Instance);
            var process = getMethod?.Invoke(pm, null);
            if (process == null)
            {
                result.Error = "Could not get pathfinding process";
                return result;
            }

            try
            {
                // Create output list (we need to pass an IL2CPP list)
                var moverType = _actorType?.ManagedType;
                var moverProxy = moverType != null ? GetManagedProxy(mover, moverType) : null;
                var startProxy = GetManagedProxy(startTile, _tileType?.ManagedType);
                var goalProxy = GetManagedProxy(goalTile, _tileType?.ManagedType);

                if (moverProxy == null || startProxy == null || goalProxy == null)
                {
                    result.Error = "Could not create managed proxies";
                    return result;
                }

                // Get current facing direction
                int direction = EntityMovement.GetFacing(mover);

                // Call FindPath
                var findPathMethod = process.GetType().GetMethod("FindPath", BindingFlags.Public | BindingFlags.Instance);
                if (findPathMethod == null)
                {
                    result.Error = "FindPath method not found";
                    return result;
                }

                // Create empty list for output
                var pathListType = typeof(System.Collections.Generic.List<Vector3>);
                var pathList = Activator.CreateInstance(pathListType);

                var success = findPathMethod.Invoke(process, new object[]
                {
                    startProxy, goalProxy, moverProxy, pathList, direction, maxAP, false
                });

                result.Success = (bool)success;

                if (result.Success)
                {
                    // Extract waypoints from the list
                    var countProp = pathListType.GetProperty("Count");
                    var indexer = pathListType.GetMethod("get_Item");
                    int count = (int)countProp.GetValue(pathList);

                    for (int i = 0; i < count; i++)
                    {
                        var wp = (Vector3)indexer.Invoke(pathList, new object[] { i });
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
            result.SurfaceType = GetSurfaceType(x, y);
            result.SurfaceTypeName = GetSurfaceTypeName(result.SurfaceType);

            // Get base movement cost from entity template
            result.BaseCost = GetBaseCostForSurface(mover, result.SurfaceType);
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
    public static int GetSurfaceType(int x, int y)
    {
        try
        {
            var mapObj = TileMap.GetMap();
            if (mapObj.IsNull) return SURFACE_DEFAULT;

            var mapType = GameType.Find("Menace.Tactical.Map")?.ManagedType;
            if (mapType == null) return SURFACE_DEFAULT;

            var proxy = GetManagedProxy(mapObj, mapType);
            if (proxy == null) return SURFACE_DEFAULT;

            var getSurfaceMethod = mapType.GetMethod("GetSurfaceTypeAtPos", BindingFlags.Public | BindingFlags.Instance);
            if (getSurfaceMethod != null)
            {
                var worldPos = TileMap.TileToWorld(x, y, 0);
                var result = getSurfaceMethod.Invoke(proxy, new object[] { worldPos });
                return Convert.ToInt32(result);
            }

            return SURFACE_DEFAULT;
        }
        catch
        {
            return SURFACE_DEFAULT;
        }
    }

    /// <summary>
    /// Get surface type name.
    /// </summary>
    public static string GetSurfaceTypeName(int surfaceType)
    {
        return surfaceType switch
        {
            0 => "Default",
            1 => "Road",
            2 => "Rough",
            3 => "Water",
            4 => "Impassable",
            _ => $"Surface {surfaceType}"
        };
    }

    /// <summary>
    /// Get base movement cost for a surface type from entity template.
    /// </summary>
    private static int GetBaseCostForSurface(GameObj mover, int surfaceType)
    {
        // Default costs if we can't read from template
        var defaultCosts = new[] { 10, 8, 15, 20, int.MaxValue };
        if (surfaceType < 0 || surfaceType >= defaultCosts.Length)
            return 10;

        if (mover.IsNull)
            return defaultCosts[surfaceType];

        try
        {
            EnsureTypesLoaded();

            var actorType = _actorType?.ManagedType;
            if (actorType == null) return defaultCosts[surfaceType];

            var proxy = GetManagedProxy(mover, actorType);
            if (proxy == null) return defaultCosts[surfaceType];

            // Get template
            var getTemplateMethod = actorType.GetMethod("GetTemplate", BindingFlags.Public | BindingFlags.Instance);
            var template = getTemplateMethod?.Invoke(proxy, null);
            if (template == null) return defaultCosts[surfaceType];

            // Get MovementCosts array
            var movementCostsProp = template.GetType().GetProperty("MovementCosts", BindingFlags.Public | BindingFlags.Instance);
            var costs = movementCostsProp?.GetValue(template) as int[];
            if (costs == null || surfaceType >= costs.Length)
                return defaultCosts[surfaceType];

            return costs[surfaceType];
        }
        catch
        {
            return defaultCosts[surfaceType];
        }
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
            return $"Surface at ({x}, {y}): {GetSurfaceTypeName(type)} ({type})";
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
        _entityTemplateType ??= GameType.Find("Menace.Tactical.EntityTemplate");
        _actorType ??= GameType.Find("Menace.Tactical.Actor");
        _tileType ??= GameType.Find("Menace.Tactical.Tile");
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
