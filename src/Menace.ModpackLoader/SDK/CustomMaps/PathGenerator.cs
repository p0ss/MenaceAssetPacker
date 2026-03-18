using System;
using System.Collections.Generic;

namespace Menace.SDK.CustomMaps;

/// <summary>
/// Generates paths between waypoints using line drawing algorithms.
/// Supports roads, rivers, trails, and trenches.
/// </summary>
public static class PathGenerator
{
    /// <summary>
    /// Surface type mapping for path types.
    /// </summary>
    private static readonly Dictionary<PathType, string> PathSurfaces = new()
    {
        { PathType.Road, "Road" },
        { PathType.River, "Water" },
        { PathType.Trail, "Dirt" },
        { PathType.Trench, "Trench" }
    };

    /// <summary>
    /// Apply a path to the map.
    /// </summary>
    public static void ApplyPath(object mapInstance, MapPath path)
    {
        if (path == null || path.Waypoints == null || path.Waypoints.Count < 2)
            return;

        var tiles = GeneratePathTiles(path);
        var surface = PathSurfaces.TryGetValue(path.Type, out var s) ? s : "Road";

        foreach (var (x, y) in tiles)
        {
            ApplyPathTile(x, y, path.Type, surface);
        }

        SdkLogger.Msg($"[PathGenerator] Applied path '{path.Id}': {tiles.Count} tiles");
    }

    /// <summary>
    /// Generate the list of tiles that make up a path.
    /// </summary>
    public static List<(int x, int y)> GeneratePathTiles(MapPath path)
    {
        var tiles = new HashSet<(int x, int y)>();

        if (path.Waypoints.Count < 2)
            return new List<(int, int)>(tiles);

        // Connect each pair of consecutive waypoints
        for (int i = 0; i < path.Waypoints.Count - 1; i++)
        {
            var start = path.Waypoints[i];
            var end = path.Waypoints[i + 1];

            // Get centerline tiles using Bresenham
            var centerline = BresenhamLine(start.X, start.Y, end.X, end.Y);

            // Expand to path width
            foreach (var (cx, cy) in centerline)
            {
                var expanded = ExpandPathWidth(cx, cy, path.Width);
                foreach (var tile in expanded)
                {
                    tiles.Add(tile);
                }
            }
        }

        return new List<(int, int)>(tiles);
    }

    /// <summary>
    /// Bresenham line algorithm for drawing lines between two points.
    /// Returns all tiles along the line.
    /// </summary>
    public static List<(int x, int y)> BresenhamLine(int x0, int y0, int x1, int y1)
    {
        var tiles = new List<(int x, int y)>();

        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        int x = x0;
        int y = y0;

        while (true)
        {
            tiles.Add((x, y));

            if (x == x1 && y == y1)
                break;

            int e2 = 2 * err;

            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
        }

        return tiles;
    }

    /// <summary>
    /// Expand a center tile to fill the path width.
    /// Uses a simple diamond/circle pattern based on width.
    /// </summary>
    public static List<(int x, int y)> ExpandPathWidth(int cx, int cy, int width)
    {
        var tiles = new List<(int x, int y)>();
        int radius = (width - 1) / 2;

        // For width=1, just the center
        if (radius <= 0)
        {
            tiles.Add((cx, cy));
            return tiles;
        }

        // Use diamond pattern for odd widths, expanded for even
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                // Diamond pattern: Manhattan distance <= radius
                if (Math.Abs(dx) + Math.Abs(dy) <= radius)
                {
                    tiles.Add((cx + dx, cy + dy));
                }
            }
        }

        return tiles;
    }

    /// <summary>
    /// Apply path properties to a single tile.
    /// </summary>
    private static void ApplyPathTile(int x, int y, PathType type, string surface)
    {
        // Set surface type
        TileManipulation.SetSurface(x, y, surface);

        // Path-specific modifications
        switch (type)
        {
            case PathType.Road:
                // Roads are flat and clear
                TileManipulation.SetBlocked(x, y, false);
                // Clear cover on roads
                for (int dir = 0; dir < 8; dir++)
                {
                    TileManipulation.SetCover(x, y, dir, 0);
                }
                break;

            case PathType.River:
                // Rivers block movement
                TileManipulation.SetBlocked(x, y, true);
                break;

            case PathType.Trail:
                // Trails are clear but don't remove cover
                TileManipulation.SetBlocked(x, y, false);
                break;

            case PathType.Trench:
                // Trenches provide cover
                TileManipulation.SetBlocked(x, y, false);
                // Add light cover in all directions
                for (int dir = 0; dir < 8; dir++)
                {
                    var currentCover = TileManipulation.GetCover(x, y, dir);
                    if (currentCover < 1)
                        TileManipulation.SetCover(x, y, dir, 1);
                }
                break;
        }
    }

    /// <summary>
    /// Create a path from an array of coordinate pairs.
    /// Convenience method for Lua API.
    /// </summary>
    public static MapPath CreatePath(string id, PathType type, int width, int[,] waypoints)
    {
        var path = new MapPath
        {
            Id = id,
            Type = type,
            Width = width,
            Waypoints = new List<PathWaypoint>()
        };

        int count = waypoints.GetLength(0);
        for (int i = 0; i < count; i++)
        {
            path.Waypoints.Add(new PathWaypoint(waypoints[i, 0], waypoints[i, 1]));
        }

        return path;
    }

    /// <summary>
    /// Calculate the total length of a path in tiles.
    /// </summary>
    public static int CalculatePathLength(MapPath path)
    {
        if (path.Waypoints.Count < 2)
            return 0;

        int totalLength = 0;
        for (int i = 0; i < path.Waypoints.Count - 1; i++)
        {
            var start = path.Waypoints[i];
            var end = path.Waypoints[i + 1];
            totalLength += Math.Max(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
        }

        return totalLength;
    }

    /// <summary>
    /// Get a smooth spline path through waypoints.
    /// Uses Catmull-Rom interpolation for smoother curves.
    /// </summary>
    public static List<(int x, int y)> GenerateSmoothPath(MapPath path, int resolution = 4)
    {
        var result = new HashSet<(int x, int y)>();

        if (path.Waypoints.Count < 2)
            return new List<(int, int)>(result);

        if (path.Waypoints.Count == 2)
        {
            // Just use Bresenham for two points
            return GeneratePathTiles(path);
        }

        // Convert waypoints to float for interpolation
        var points = new List<(float x, float y)>();
        foreach (var wp in path.Waypoints)
        {
            points.Add((wp.X, wp.Y));
        }

        // Add virtual points at start and end for Catmull-Rom
        points.Insert(0, (2 * points[0].x - points[1].x, 2 * points[0].y - points[1].y));
        points.Add((2 * points[^1].x - points[^2].x, 2 * points[^1].y - points[^2].y));

        // Interpolate between each pair of points
        for (int i = 1; i < points.Count - 2; i++)
        {
            var p0 = points[i - 1];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = points[i + 2];

            int segments = Math.Max(1, (int)(Distance(p1.x, p1.y, p2.x, p2.y) * resolution / 4));

            for (int s = 0; s <= segments; s++)
            {
                float t = (float)s / segments;
                var (x, y) = CatmullRom(p0, p1, p2, p3, t);

                int tileX = (int)Math.Round(x);
                int tileY = (int)Math.Round(y);

                // Expand to path width
                var expanded = ExpandPathWidth(tileX, tileY, path.Width);
                foreach (var tile in expanded)
                {
                    result.Add(tile);
                }
            }
        }

        return new List<(int, int)>(result);
    }

    /// <summary>
    /// Catmull-Rom spline interpolation.
    /// </summary>
    private static (float x, float y) CatmullRom(
        (float x, float y) p0,
        (float x, float y) p1,
        (float x, float y) p2,
        (float x, float y) p3,
        float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        float x = 0.5f * (
            2 * p1.x +
            (-p0.x + p2.x) * t +
            (2 * p0.x - 5 * p1.x + 4 * p2.x - p3.x) * t2 +
            (-p0.x + 3 * p1.x - 3 * p2.x + p3.x) * t3
        );

        float y = 0.5f * (
            2 * p1.y +
            (-p0.y + p2.y) * t +
            (2 * p0.y - 5 * p1.y + 4 * p2.y - p3.y) * t2 +
            (-p0.y + 3 * p1.y - 3 * p2.y + p3.y) * t3
        );

        return (x, y);
    }

    /// <summary>
    /// Calculate distance between two points.
    /// </summary>
    private static float Distance(float x1, float y1, float x2, float y2)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }
}
