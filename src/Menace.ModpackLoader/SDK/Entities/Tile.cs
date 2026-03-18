using System;
using System.Collections.Generic;

namespace Menace.SDK.Entities;

/// <summary>
/// Object-oriented wrapper for a Tile instance.
///
/// Provides clean access to tile properties like cover, occupancy, and visibility.
/// Wraps existing TileMap static methods with an object-oriented API.
///
/// Usage:
///   var tile = Tile.At(5, 10);  // Get tile by coordinates
///   if (!tile.IsBlocked)
///       actor.MoveTo(tile.X, tile.Y);
///   var occupant = tile.GetOccupant();
/// </summary>
public class Tile
{
    private readonly GameObj _gameObj;
    private readonly int _x;
    private readonly int _y;

    /// <summary>
    /// Create a Tile wrapper from a GameObj.
    /// </summary>
    public Tile(GameObj gameObj, int x = -1, int y = -1)
    {
        _gameObj = gameObj;
        _x = x;
        _y = y;
    }

    /// <summary>
    /// Create a Tile wrapper from a pointer.
    /// </summary>
    public Tile(IntPtr pointer, int x = -1, int y = -1)
        : this(new GameObj(pointer), x, y)
    {
    }

    /// <summary>
    /// The raw pointer to the Tile instance.
    /// </summary>
    public IntPtr Pointer => _gameObj.Pointer;

    /// <summary>
    /// The GameObj wrapper for this tile.
    /// </summary>
    public GameObj GameObj => _gameObj;

    /// <summary>
    /// Check if this tile wrapper points to a valid tile.
    /// </summary>
    public bool IsValid => !_gameObj.IsNull;

    /// <summary>
    /// Get the tile's grid X coordinate.
    /// </summary>
    public int X => _x;

    /// <summary>
    /// Get the tile's grid Y/Z coordinate.
    /// </summary>
    public int Y => _y;

    // ═══════════════════════════════════════════════════════════════════
    //  Tile Properties (wraps TileMap)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get tile info from the TileMap.
    /// </summary>
    public TileMap.TileInfo Info => IsValid ? TileMap.GetTileInfo(_gameObj) : default;

    /// <summary>
    /// Check if this tile is blocked (impassable).
    /// </summary>
    public bool IsBlocked => IsValid && TileMap.IsBlocked(_gameObj);

    /// <summary>
    /// Check if this tile has an actor on it.
    /// </summary>
    public bool HasActor => IsValid && TileMap.HasActor(_gameObj);

    /// <summary>
    /// Get the actor occupying this tile (if any).
    /// Property accessor for generated code compatibility.
    /// </summary>
    public Actor Occupant => GetOccupant();

    /// <summary>
    /// Get the actor occupying this tile (if any).
    /// </summary>
    public Actor GetOccupant()
    {
        if (!IsValid) return null;
        var actorObj = TileMap.GetActorOnTile(_gameObj);
        return actorObj.IsNull ? null : Actor.Get(actorObj.Pointer);
    }

    /// <summary>
    /// Check if this tile is visible to the player.
    /// </summary>
    public bool IsVisibleToPlayer => IsValid && TileMap.IsVisibleToPlayer(_gameObj);

    /// <summary>
    /// Check if this tile is visible to a specific faction.
    /// </summary>
    public bool IsVisibleToFaction(int factionId) => IsValid && TileMap.IsVisibleToFaction(_gameObj, factionId);

    // ═══════════════════════════════════════════════════════════════════
    //  Cover
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get cover value in a specific direction (0-7).
    /// </summary>
    public int GetCover(int direction) => IsValid ? TileMap.GetCover(_gameObj, direction) : 0;

    /// <summary>
    /// Get cover values in all directions.
    /// </summary>
    public int[] GetAllCover() => IsValid ? TileMap.GetAllCover(_gameObj) : Array.Empty<int>();

    // ═══════════════════════════════════════════════════════════════════
    //  Neighbors
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get neighbor tile in a specific direction (0-7).
    /// </summary>
    public Tile GetNeighbor(int direction)
    {
        if (!IsValid) return null;
        var neighborObj = TileMap.GetNeighbor(_gameObj, direction);
        if (neighborObj.IsNull) return null;
        // We don't know the neighbor's coords, so pass -1
        return new Tile(neighborObj);
    }

    /// <summary>
    /// Get all neighbor tiles (up to 8).
    /// </summary>
    public List<Tile> GetAllNeighbors()
    {
        if (!IsValid) return new List<Tile>();
        var neighbors = TileMap.GetAllNeighbors(_gameObj);
        var result = new List<Tile>(neighbors.Length);
        foreach (var n in neighbors)
        {
            if (!n.IsNull)
                result.Add(new Tile(n));
        }
        return result;
    }

    /// <summary>
    /// Get the distance to another tile.
    /// </summary>
    public int DistanceTo(Tile other)
    {
        if (!IsValid || other == null || !other.IsValid) return int.MaxValue;
        return TileMap.GetDistance(_gameObj, other._gameObj);
    }

    /// <summary>
    /// Get the direction from this tile to another.
    /// </summary>
    public int DirectionTo(Tile other)
    {
        if (!IsValid || other == null || !other.IsValid) return -1;
        return TileMap.GetDirectionTo(_gameObj, other._gameObj);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Static Factory
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get a tile at the given grid coordinates.
    /// </summary>
    public static Tile At(int x, int z)
    {
        var tileObj = TileMap.GetTile(x, z);
        return tileObj.IsNull ? null : new Tile(tileObj, x, z);
    }

    /// <summary>
    /// Get map info.
    /// </summary>
    public static TileMap.MapInfo GetMapInfo() => TileMap.GetMapInfo();

    public override string ToString()
    {
        return $"Tile({X}, {Y}, Blocked={IsBlocked}, HasActor={HasActor})";
    }

    public override bool Equals(object obj)
    {
        return obj is Tile other && other.Pointer == Pointer;
    }

    public override int GetHashCode()
    {
        return Pointer.GetHashCode();
    }

    public static bool operator ==(Tile a, Tile b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return a.Pointer == b.Pointer;
    }

    public static bool operator !=(Tile a, Tile b) => !(a == b);
}
