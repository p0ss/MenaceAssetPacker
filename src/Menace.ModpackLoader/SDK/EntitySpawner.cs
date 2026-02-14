using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// SDK extension for spawning and destroying entities in tactical combat.
/// Uses IL2CPP interop to call game spawning methods.
///
/// Based on reverse engineering findings:
/// - TacticalManager.TrySpawnUnit(FactionType, EntityTemplate, Tile, out Actor) @ TacticalManager.s_Singleton
/// - Entity.Die(bool destroyImmediately) @ 0x180610aa0
/// </summary>
public static class EntitySpawner
{
    // Cached types
    private static GameType _actorType;
    private static GameType _entityTemplateType;
    private static GameType _tileType;
    private static GameType _tacticalManagerType;
    private static GameType _factionType;

    // Field offsets from actor-system.md
    private const uint OFFSET_ENTITY_ID = 0x10;
    private const uint OFFSET_ENTITY_FACTION_INDEX = 0x4C;
    private const uint OFFSET_ENTITY_IS_ALIVE = 0x48;
    private const uint OFFSET_ENTITY_NAME = 0x88;
    private const uint OFFSET_ACTOR_CURRENT_TILE = 0xA8;

    // TacticalManager offsets from turn-action-system.md
    private const uint OFFSET_TM_ALL_ACTORS = 0x58;
    private const uint OFFSET_TM_MAP = 0x28;

    /// <summary>
    /// Spawn result containing the spawned entity or error info.
    /// </summary>
    public class SpawnResult
    {
        public bool Success { get; set; }
        public GameObj Entity { get; set; }
        public string Error { get; set; }

        public static SpawnResult Failed(string error) => new() { Success = false, Error = error };
        public static SpawnResult Ok(GameObj entity) => new() { Success = true, Entity = entity };
    }

    /// <summary>
    /// Spawn a transient actor (AI enemy or temporary unit) at the specified tile.
    /// Uses TacticalManager.TrySpawnUnit() which handles actor registration internally.
    /// </summary>
    /// <param name="templateName">EntityTemplate name (e.g., "Grunt", "HeavyTrooper")</param>
    /// <param name="tileX">Tile X coordinate</param>
    /// <param name="tileY">Tile Y coordinate</param>
    /// <param name="factionIndex">Faction index (0=Player, 1=Enemy, 2+=Others)</param>
    /// <returns>SpawnResult with the spawned entity or error</returns>
    public static SpawnResult SpawnUnit(string templateName, int tileX, int tileY, int factionIndex = 1)
    {
        try
        {
            EnsureTypesLoaded();

            // Find the template
            var template = Templates.Find("EntityTemplate", templateName);
            if (template.IsNull)
            {
                return SpawnResult.Failed($"Template '{templateName}' not found");
            }

            // Get the tile
            var tile = GetTileAt(tileX, tileY);
            if (tile.IsNull)
            {
                return SpawnResult.Failed($"Tile at ({tileX}, {tileY}) not found");
            }

            // Check tile is valid for spawning
            if (IsTileOccupied(tile))
            {
                return SpawnResult.Failed($"Tile at ({tileX}, {tileY}) is occupied");
            }

            // Get TacticalManager singleton
            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null)
            {
                return SpawnResult.Failed("TacticalManager managed type not available");
            }

            var instanceProp = tmType.GetProperty("s_Singleton", BindingFlags.Public | BindingFlags.Static);
            if (instanceProp == null)
            {
                return SpawnResult.Failed("TacticalManager.s_Singleton property not found");
            }

            var tm = instanceProp.GetValue(null);
            if (tm == null)
            {
                return SpawnResult.Failed("TacticalManager.s_Singleton is null");
            }

            // Check FactionType enum
            if (_factionType == null)
            {
                return SpawnResult.Failed("FactionType enum not found");
            }

            // Get TrySpawnUnit method: TrySpawnUnit(FactionType _faction, EntityTemplate _template, Tile _tile, out Actor _unit)
            var trySpawnMethod = tmType.GetMethod("TrySpawnUnit", BindingFlags.Public | BindingFlags.Instance);
            if (trySpawnMethod == null)
            {
                return SpawnResult.Failed("TacticalManager.TrySpawnUnit method not found");
            }

            // Get managed proxies for template and tile
            var templateProxy = GetManagedProxy(template, _entityTemplateType.ManagedType);
            var tileProxy = GetManagedProxy(tile, _tileType.ManagedType);

            if (templateProxy == null || tileProxy == null)
            {
                return SpawnResult.Failed("Failed to create managed proxies");
            }

            // Convert factionIndex to FactionType enum value
            var factionEnumValue = Enum.ToObject(_factionType.ManagedType, factionIndex);

            // Invoke TrySpawnUnit with out parameter
            var parameters = new object[] { factionEnumValue, templateProxy, tileProxy, null };
            var success = (bool)trySpawnMethod.Invoke(tm, parameters);

            if (!success || parameters[3] == null)
            {
                return SpawnResult.Failed("TacticalManager.TrySpawnUnit returned false");
            }

            var actorObj = new GameObj(((Il2CppObjectBase)parameters[3]).Pointer);

            ModError.Info("Menace.SDK", $"Spawned {templateName} at ({tileX}, {tileY}) faction {factionIndex}");
            return SpawnResult.Ok(actorObj);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySpawner.SpawnUnit", $"Failed to spawn {templateName}", ex);
            return SpawnResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Spawn multiple units at once.
    /// </summary>
    /// <param name="templateName">EntityTemplate name</param>
    /// <param name="positions">List of (x, y) tile coordinates</param>
    /// <param name="factionIndex">Faction index for all spawned units</param>
    /// <returns>List of spawn results</returns>
    public static List<SpawnResult> SpawnGroup(string templateName, List<(int x, int y)> positions, int factionIndex = 1)
    {
        var results = new List<SpawnResult>();

        foreach (var (x, y) in positions)
        {
            results.Add(SpawnUnit(templateName, x, y, factionIndex));
        }

        return results;
    }

    /// <summary>
    /// Get all actors currently on the tactical map.
    /// </summary>
    /// <param name="factionFilter">Optional faction index to filter by (-1 for all)</param>
    /// <returns>Array of actor GameObjs</returns>
    public static GameObj[] ListEntities(int factionFilter = -1)
    {
        try
        {
            EnsureTypesLoaded();

            var actors = GameQuery.FindAll(_actorType);
            if (factionFilter < 0)
                return actors;

            var filtered = new List<GameObj>();
            foreach (var actor in actors)
            {
                if (!actor.IsNull && actor.IsAlive)
                {
                    var faction = actor.ReadInt(OFFSET_ENTITY_FACTION_INDEX);
                    if (faction == factionFilter)
                        filtered.Add(actor);
                }
            }

            return filtered.ToArray();
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySpawner.ListEntities", "Failed to list entities", ex);
            return Array.Empty<GameObj>();
        }
    }

    /// <summary>
    /// Destroy/kill an entity.
    /// </summary>
    /// <param name="entity">The entity to destroy</param>
    /// <param name="immediate">If true, skip death animation</param>
    /// <returns>True if successful</returns>
    public static bool DestroyEntity(GameObj entity, bool immediate = false)
    {
        if (entity.IsNull || !entity.IsAlive)
            return false;

        try
        {
            EnsureTypesLoaded();

            var managedType = _actorType?.ManagedType;
            if (managedType == null)
            {
                ModError.WarnInternal("EntitySpawner.DestroyEntity", "Actor managed type not available");
                return false;
            }

            var dieMethod = managedType.GetMethod("Die", BindingFlags.Public | BindingFlags.Instance);
            if (dieMethod == null)
            {
                ModError.WarnInternal("EntitySpawner.DestroyEntity", "Die method not found");
                return false;
            }

            var proxy = GetManagedProxy(entity, managedType);
            if (proxy == null)
                return false;

            dieMethod.Invoke(proxy, new object[] { immediate });
            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySpawner.DestroyEntity", "Failed to destroy entity", ex);
            return false;
        }
    }

    /// <summary>
    /// Clear all enemies from the map.
    /// </summary>
    /// <param name="immediate">If true, skip death animations</param>
    /// <returns>Number of enemies cleared</returns>
    public static int ClearEnemies(bool immediate = true)
    {
        var enemies = ListEntities(factionFilter: 1);
        int count = 0;

        foreach (var enemy in enemies)
        {
            if (DestroyEntity(enemy, immediate))
                count++;
        }

        return count;
    }

    /// <summary>
    /// Get entity information as a summary object.
    /// </summary>
    public static EntityInfo GetEntityInfo(GameObj entity)
    {
        if (entity.IsNull)
            return null;

        try
        {
            return new EntityInfo
            {
                EntityId = entity.ReadInt(OFFSET_ENTITY_ID),
                Name = entity.GetName() ?? entity.ReadString("Name"),
                TypeName = entity.GetTypeName(),
                FactionIndex = entity.ReadInt(OFFSET_ENTITY_FACTION_INDEX),
                IsAlive = ReadBoolAtOffset(entity, OFFSET_ENTITY_IS_ALIVE),
                Pointer = entity.Pointer
            };
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySpawner.GetEntityInfo", "Failed", ex);
            return null;
        }
    }

    public class EntityInfo
    {
        public int EntityId { get; set; }
        public string Name { get; set; }
        public string TypeName { get; set; }
        public int FactionIndex { get; set; }
        public bool IsAlive { get; set; }
        public IntPtr Pointer { get; set; }
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _actorType ??= GameType.Find("Menace.Tactical.Actor");
        _entityTemplateType ??= GameType.Find("Menace.Tactical.EntityTemplate");
        _tileType ??= GameType.Find("Menace.Tactical.Tile");
        _tacticalManagerType ??= GameType.Find("Menace.Tactical.TacticalManager");
        _factionType ??= GameType.Find("Menace.Tactical.FactionType");
    }

    private static GameObj GetTileAt(int x, int y)
    {
        try
        {
            // Get TacticalManager singleton
            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return GameObj.Null;

            var instanceProp = tmType.GetProperty("s_Singleton", BindingFlags.Public | BindingFlags.Static);
            if (instanceProp == null) return GameObj.Null;

            var tm = instanceProp.GetValue(null);
            if (tm == null) return GameObj.Null;

            // Get Map from TacticalManager via GetMap() method
            var getMapMethod = tmType.GetMethod("GetMap", BindingFlags.Public | BindingFlags.Instance);
            if (getMapMethod == null) return GameObj.Null;

            var map = getMapMethod.Invoke(tm, null);
            if (map == null) return GameObj.Null;

            // Call Map.GetTile(x, y)
            var getTileMethod = map.GetType().GetMethod("GetTile",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(int), typeof(int) }, null);

            if (getTileMethod == null) return GameObj.Null;

            var tile = getTileMethod.Invoke(map, new object[] { x, y });
            if (tile == null) return GameObj.Null;

            return new GameObj(((Il2CppObjectBase)tile).Pointer);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("EntitySpawner.GetTileAt", $"Failed for ({x}, {y})", ex);
            return GameObj.Null;
        }
    }

    private static bool IsTileOccupied(GameObj tile)
    {
        if (tile.IsNull) return true;

        try
        {
            var tileType = _tileType?.ManagedType;
            if (tileType == null) return false;

            var proxy = GetManagedProxy(tile, tileType);
            if (proxy == null) return false;

            var hasActorMethod = tileType.GetMethod("HasActor", BindingFlags.Public | BindingFlags.Instance);
            if (hasActorMethod != null)
            {
                return (bool)hasActorMethod.Invoke(proxy, null);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static object GetManagedProxy(GameObj obj, Type managedType)
    {
        if (obj.IsNull || managedType == null) return null;

        try
        {
            var ptrCtor = managedType.GetConstructor(new[] { typeof(IntPtr) });
            if (ptrCtor != null)
                return ptrCtor.Invoke(new object[] { obj.Pointer });

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool ReadBoolAtOffset(GameObj obj, uint offset)
    {
        if (obj.IsNull || offset == 0) return false;

        try
        {
            return Marshal.ReadByte(obj.Pointer + (int)offset) != 0;
        }
        catch
        {
            return false;
        }
    }
}
