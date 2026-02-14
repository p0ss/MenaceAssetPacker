using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for Line of Sight and visibility operations.
/// Provides safe access to LOS checks, detection, and visibility management.
///
/// Based on reverse engineering findings:
/// - LineOfSight.HasLineOfSight(from, to, flags) @ 0x18051df40
/// - Tile.HasLineOfSightTo(target, flags) @ 0x180681d70
/// - Actor.HasLineOfSightTo(entity, wasDetected, fromTile, toTile) @ 0x1805dfa10
/// - EntityProperties.GetVision() @ 0x18060c7b0
/// - EntityProperties.GetDetection() @ 0x18060bd90
/// </summary>
public static class LineOfSight
{
    // Cached types
    private static GameType _tileType;
    private static GameType _actorType;
    private static GameType _entityPropertiesType;
    private static GameType _tacticalManagerType;

    // Visibility states
    public const int VISIBILITY_UNKNOWN = 0;
    public const int VISIBILITY_VISIBLE = 1;
    public const int VISIBILITY_HIDDEN = 2;
    public const int VISIBILITY_DETECTED = 3;

    // LOS flags - matches LineOfSightFlags enum: Default=0, IgnoreLastTile=1, IgnoreHalfCover=4
    public const byte LOS_FLAG_DEFAULT = 0;
    public const byte LOS_FLAG_IGNORE_LAST_TILE = 1;
    public const byte LOS_FLAG_IGNORE_HALF_COVER = 4;

    // EntityProperties offsets
    private const uint OFFSET_BASE_VISION = 0xC4;
    private const uint OFFSET_VISION_MULT = 0xC8;
    private const uint OFFSET_BASE_DETECTION = 0xCC;
    private const uint OFFSET_DETECTION_MULT = 0xD0;
    private const uint OFFSET_BASE_CONCEALMENT = 0xD4;
    private const uint OFFSET_CONCEALMENT_MULT = 0xD8;

    // Actor visibility offset
    private const uint OFFSET_ACTOR_VISIBILITY_STATE = 0x90;
    private const uint OFFSET_ACTOR_FIRST_TIME_VISIBLE = 0x16D;
    private const uint OFFSET_ACTOR_IS_MARKED = 0x1AC;

    /// <summary>
    /// Check if there is clear line of sight between two tiles.
    /// </summary>
    public static bool HasLOS(int fromX, int fromY, int toX, int toY)
    {
        var fromTile = TileMap.GetTile(fromX, fromY);
        var toTile = TileMap.GetTile(toX, toY);
        return HasLOS(fromTile, toTile);
    }

    /// <summary>
    /// Check if there is clear line of sight between two tiles.
    /// </summary>
    public static bool HasLOS(GameObj fromTile, GameObj toTile, byte flags = 0)
    {
        if (fromTile.IsNull || toTile.IsNull) return false;

        // Same tile always has LOS
        if (fromTile.Pointer == toTile.Pointer) return true;

        try
        {
            EnsureTypesLoaded();

            var tileType = _tileType?.ManagedType;
            if (tileType == null) return false;

            var fromProxy = GetManagedProxy(fromTile, tileType);
            if (fromProxy == null) return false;

            var toProxy = GetManagedProxy(toTile, tileType);
            if (toProxy == null) return false;

            var hasLosMethod = tileType.GetMethod("HasLineOfSightTo", BindingFlags.Public | BindingFlags.Instance);
            if (hasLosMethod != null)
            {
                var result = hasLosMethod.Invoke(fromProxy, new object[] { toProxy, flags });
                return (bool)result;
            }

            return false;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("LineOfSight.HasLOS", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Check if an actor can see a target entity (includes detection vs concealment).
    /// </summary>
    public static bool CanActorSee(GameObj actor, GameObj target)
    {
        if (actor.IsNull || target.IsNull) return false;

        try
        {
            EnsureTypesLoaded();

            var actorType = _actorType?.ManagedType;
            if (actorType == null) return false;

            var actorProxy = GetManagedProxy(actor, actorType);
            if (actorProxy == null) return false;

            var targetProxy = GetManagedProxy(target, actorType);
            if (targetProxy == null) return false;

            var hasLosMethod = actorType.GetMethod("HasLineOfSightTo",
                BindingFlags.Public | BindingFlags.Instance);

            if (hasLosMethod != null)
            {
                var result = hasLosMethod.Invoke(actorProxy, new object[] { targetProxy, false, null, null });
                return (bool)result;
            }

            return false;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("LineOfSight.CanActorSee", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get the visibility state of an actor (Unknown, Visible, Hidden, Detected).
    /// </summary>
    public static int GetVisibilityState(GameObj actor)
    {
        if (actor.IsNull) return VISIBILITY_UNKNOWN;

        try
        {
            return actor.ReadInt(OFFSET_ACTOR_VISIBILITY_STATE);
        }
        catch
        {
            return VISIBILITY_UNKNOWN;
        }
    }

    /// <summary>
    /// Get visibility state name.
    /// </summary>
    public static string GetVisibilityStateName(int state)
    {
        return state switch
        {
            0 => "Unknown",
            1 => "Visible",
            2 => "Hidden",
            3 => "Detected",
            _ => $"State {state}"
        };
    }

    /// <summary>
    /// Check if an actor is currently visible to the player.
    /// </summary>
    public static bool IsVisibleToPlayer(GameObj actor)
    {
        var state = GetVisibilityState(actor);
        return state == VISIBILITY_VISIBLE;
    }

    /// <summary>
    /// Check if an actor is marked/painted (always visible when in range).
    /// </summary>
    public static bool IsMarked(GameObj actor)
    {
        if (actor.IsNull) return false;

        try
        {
            return Marshal.ReadByte(actor.Pointer + (int)OFFSET_ACTOR_IS_MARKED) != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get vision range for an entity.
    /// </summary>
    public static int GetVision(GameObj entity)
    {
        if (entity.IsNull) return 0;

        try
        {
            EnsureTypesLoaded();

            var actorType = _actorType?.ManagedType;
            if (actorType == null) return 0;

            var proxy = GetManagedProxy(entity, actorType);
            if (proxy == null) return 0;

            // Get EntityProperties
            var getPropsMethod = actorType.GetMethod("GetCurrentProperties", BindingFlags.Public | BindingFlags.Instance);
            var props = getPropsMethod?.Invoke(proxy, null);
            if (props == null) return 0;

            // Call GetVision
            var getVisionMethod = props.GetType().GetMethod("GetVision", BindingFlags.Public | BindingFlags.Instance);
            if (getVisionMethod != null)
            {
                return (int)getVisionMethod.Invoke(props, null);
            }

            return 0;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("LineOfSight.GetVision", "Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Get detection stat for an entity.
    /// </summary>
    public static int GetDetection(GameObj entity)
    {
        if (entity.IsNull) return 0;

        try
        {
            EnsureTypesLoaded();

            var actorType = _actorType?.ManagedType;
            if (actorType == null) return 0;

            var proxy = GetManagedProxy(entity, actorType);
            if (proxy == null) return 0;

            var getPropsMethod = actorType.GetMethod("GetCurrentProperties", BindingFlags.Public | BindingFlags.Instance);
            var props = getPropsMethod?.Invoke(proxy, null);
            if (props == null) return 0;

            var getDetectionMethod = props.GetType().GetMethod("GetDetection", BindingFlags.Public | BindingFlags.Instance);
            if (getDetectionMethod != null)
            {
                return (int)getDetectionMethod.Invoke(props, null);
            }

            return 0;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("LineOfSight.GetDetection", "Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Get concealment stat for an entity.
    /// </summary>
    public static int GetConcealment(GameObj entity)
    {
        if (entity.IsNull) return 0;

        try
        {
            EnsureTypesLoaded();

            var actorType = _actorType?.ManagedType;
            if (actorType == null) return 0;

            var proxy = GetManagedProxy(entity, actorType);
            if (proxy == null) return 0;

            var getPropsMethod = actorType.GetMethod("GetCurrentProperties", BindingFlags.Public | BindingFlags.Instance);
            var props = getPropsMethod?.Invoke(proxy, null);
            if (props == null) return 0;

            var getConcealMethod = props.GetType().GetMethod("GetConcealment", BindingFlags.Public | BindingFlags.Instance);
            if (getConcealMethod != null)
            {
                return (int)getConcealMethod.Invoke(props, null);
            }

            return 0;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("LineOfSight.GetConcealment", "Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Get all tiles visible from a position within a given range.
    /// </summary>
    public static List<(int x, int y)> GetVisibleTiles(int centerX, int centerY, int range)
    {
        var result = new List<(int x, int y)>();
        var centerTile = TileMap.GetTile(centerX, centerY);
        if (centerTile.IsNull) return result;

        var mapInfo = TileMap.GetMapInfo();
        if (mapInfo == null) return result;

        // Check all tiles in range
        for (int x = Math.Max(0, centerX - range); x <= Math.Min(mapInfo.Width - 1, centerX + range); x++)
        {
            for (int y = Math.Max(0, centerY - range); y <= Math.Min(mapInfo.Height - 1, centerY + range); y++)
            {
                // Skip if too far (circular check)
                int dx = x - centerX;
                int dy = y - centerY;
                if (dx * dx + dy * dy > range * range) continue;

                // Check LOS
                if (HasLOS(centerX, centerY, x, y))
                {
                    result.Add((x, y));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Get visibility info for an actor.
    /// </summary>
    public static VisibilityInfo GetVisibilityInfo(GameObj actor)
    {
        if (actor.IsNull) return null;

        return new VisibilityInfo
        {
            State = GetVisibilityState(actor),
            StateName = GetVisibilityStateName(GetVisibilityState(actor)),
            IsVisible = IsVisibleToPlayer(actor),
            IsMarked = IsMarked(actor),
            Vision = GetVision(actor),
            Detection = GetDetection(actor),
            Concealment = GetConcealment(actor)
        };
    }

    /// <summary>
    /// Visibility information structure.
    /// </summary>
    public class VisibilityInfo
    {
        public int State { get; set; }
        public string StateName { get; set; }
        public bool IsVisible { get; set; }
        public bool IsMarked { get; set; }
        public int Vision { get; set; }
        public int Detection { get; set; }
        public int Concealment { get; set; }
    }

    /// <summary>
    /// Register console commands for LineOfSight SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // los <x1> <y1> <x2> <y2> - Check LOS between tiles
        DevConsole.RegisterCommand("los", "<x1> <y1> <x2> <y2>", "Check line of sight between tiles", args =>
        {
            if (args.Length < 4)
                return "Usage: los <x1> <y1> <x2> <y2>";
            if (!int.TryParse(args[0], out int x1) || !int.TryParse(args[1], out int y1) ||
                !int.TryParse(args[2], out int x2) || !int.TryParse(args[3], out int y2))
                return "Invalid coordinates";

            var hasLos = HasLOS(x1, y1, x2, y2);
            var dist = TileMap.GetDistance(x1, y1, x2, y2);

            return $"LOS from ({x1},{y1}) to ({x2},{y2}): {(hasLos ? "Clear" : "Blocked")}\n" +
                   $"Distance: {dist:F1}";
        });

        // visibility - Show visibility info for selected actor
        DevConsole.RegisterCommand("visibility", "", "Show visibility info for selected actor", args =>
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            var info = GetVisibilityInfo(actor);
            if (info == null) return "Could not get visibility info";

            return $"Visibility State: {info.StateName}\n" +
                   $"Is Visible: {info.IsVisible}, Marked: {info.IsMarked}\n" +
                   $"Vision: {info.Vision}, Detection: {info.Detection}\n" +
                   $"Concealment: {info.Concealment}";
        });

        // vision [actor] - Get vision range
        DevConsole.RegisterCommand("vision", "", "Get vision range for selected actor", args =>
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            return $"Vision: {GetVision(actor)}, Detection: {GetDetection(actor)}, Concealment: {GetConcealment(actor)}";
        });

        // cansee <target_name> - Check if selected can see target
        DevConsole.RegisterCommand("cansee", "<target_name>", "Check if selected actor can see target", args =>
        {
            if (args.Length == 0)
                return "Usage: cansee <target_name>";

            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            var targetName = string.Join(" ", args);
            var target = GameQuery.FindByName("Actor", targetName);
            if (target.IsNull)
                return $"Target '{targetName}' not found";

            var canSee = CanActorSee(actor, target);
            return $"Can see '{targetName}': {canSee}";
        });

        // visibletiles <range> - List visible tiles from selected actor
        DevConsole.RegisterCommand("visibletiles", "<range>", "Count visible tiles from selected actor", args =>
        {
            var actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return "No actor selected";

            int range = 10;
            if (args.Length > 0 && int.TryParse(args[0], out int r))
                range = r;

            var pos = EntityMovement.GetPosition(actor);
            if (!pos.HasValue)
                return "Could not get actor position";

            var visibleTiles = GetVisibleTiles(pos.Value.x, pos.Value.y, range);
            return $"Visible tiles within range {range}: {visibleTiles.Count}";
        });
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _tileType ??= GameType.Find("Menace.Tactical.Tile");
        _actorType ??= GameType.Find("Menace.Tactical.Actor");
        _entityPropertiesType ??= GameType.Find("Menace.Tactical.EntityProperties");
        _tacticalManagerType ??= GameType.Find("Menace.Tactical.TacticalManager");
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
