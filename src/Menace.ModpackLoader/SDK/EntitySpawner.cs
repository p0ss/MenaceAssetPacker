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
            var template = Templates.Find("Menace.Tactical.EntityTemplate", templateName);
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

            // Get managed proxies for template and tile
            var templateProxy = GetManagedProxy(template, _entityTemplateType.ManagedType);
            var tileProxy = GetManagedProxy(tile, _tileType.ManagedType);

            if (templateProxy == null || tileProxy == null)
            {
                return SpawnResult.Failed("Failed to create managed proxies");
            }

            // Use DevMode's approach: create TransientActor directly, initialize it, then finalize
            // This matches how SpawnEntityAction.HandleLeftClickOnTile works
            var transientActorType = GameType.Find("Menace.Tactical.TransientActor")?.ManagedType;
            if (transientActorType == null)
            {
                return SpawnResult.Failed("TransientActor type not found");
            }

            // Create new TransientActor instance
            var ctor = transientActorType.GetConstructor(Type.EmptyTypes);
            if (ctor == null)
            {
                return SpawnResult.Failed("TransientActor constructor not found");
            }
            var actor = ctor.Invoke(null);
            if (actor == null)
            {
                return SpawnResult.Failed("Failed to create TransientActor instance");
            }

            // Call Create(EntityTemplate, Tile, int faction, int totalHitpoints)
            // DevMode uses the int version with faction=1 (enemy) and totalHitpoints=0
            var createMethod = transientActorType.GetMethod("Create", BindingFlags.Public | BindingFlags.Instance,
                null, new[] { _entityTemplateType.ManagedType, _tileType.ManagedType, typeof(int), typeof(int) }, null);
            if (createMethod == null)
            {
                return SpawnResult.Failed("TransientActor.Create method not found");
            }

            // Create expects (EntityTemplate, Tile, int faction, int totalHitpoints=0)
            createMethod.Invoke(actor, new object[] { templateProxy, tileProxy, factionIndex, 0 });

            // Call FinishCreate() - virtual method that completes setup
            var finishCreateMethod = transientActorType.GetMethod("FinishCreate", BindingFlags.Public | BindingFlags.Instance);
            finishCreateMethod?.Invoke(actor, null);

            // Get TacticalManager for remaining calls
            var tmType = _tacticalManagerType?.ManagedType;
            var getMethod = tmType?.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            var tm = getMethod?.Invoke(null, null);

            if (tm != null)
            {
                // Call OnRoundStart and OnTurnStart like DevMode does
                var onRoundStart = transientActorType.GetMethod("OnRoundStart", BindingFlags.Public | BindingFlags.Instance);
                onRoundStart?.Invoke(actor, null);

                var onTurnStart = transientActorType.GetMethod("OnTurnStart", BindingFlags.Public | BindingFlags.Instance);
                onTurnStart?.Invoke(actor, null);

                // Notify TacticalManager of new entity
                var invokeSpawnedMethod = tmType.GetMethod("InvokeOnEntitySpawned", BindingFlags.Public | BindingFlags.Instance);
                invokeSpawnedMethod?.Invoke(tm, new object[] { actor });
            }

            var actorObj = new GameObj(((Il2CppObjectBase)actor).Pointer);

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

            // Actors are stored in TacticalManager.m_Factions[].m_Actors, not as Unity Resources
            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return Array.Empty<GameObj>();

            var getMethod = tmType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            var tm = getMethod?.Invoke(null, null);
            if (tm == null) return Array.Empty<GameObj>();

            // Get m_Factions property
            var factionsProperty = tmType.GetProperty("m_Factions", BindingFlags.Public | BindingFlags.Instance);
            if (factionsProperty == null) return Array.Empty<GameObj>();

            var factions = factionsProperty.GetValue(tm);
            if (factions == null) return Array.Empty<GameObj>();

            var result = new List<GameObj>();
            var factionList = (System.Collections.IEnumerable)factions;

            int factionIdx = 0;
            foreach (var faction in factionList)
            {
                if (faction == null)
                {
                    factionIdx++;
                    continue;
                }

                // Skip if filtering by faction
                if (factionFilter >= 0 && factionIdx != factionFilter)
                {
                    factionIdx++;
                    continue;
                }

                // Get m_Actors from this faction
                var factionType = faction.GetType();
                var actorsProp = factionType.GetProperty("m_Actors", BindingFlags.Public | BindingFlags.Instance);
                if (actorsProp == null)
                {
                    factionIdx++;
                    continue;
                }

                var actors = actorsProp.GetValue(faction);
                if (actors == null)
                {
                    factionIdx++;
                    continue;
                }

                // Il2CppSystem.Collections.Generic.List cannot be cast to System.Collections.IEnumerable
                // Use Count property and indexer via reflection instead
                var actorsType = actors.GetType();
                var countProp = actorsType.GetProperty("Count");
                var itemProp = actorsType.GetProperty("Item");
                if (countProp == null || itemProp == null)
                {
                    factionIdx++;
                    continue;
                }

                var count = (int)countProp.GetValue(actors);
                for (int i = 0; i < count; i++)
                {
                    var actor = itemProp.GetValue(actors, new object[] { i });
                    if (actor != null)
                    {
                        var ptr = ((Il2CppObjectBase)actor).Pointer;
                        if (ptr != IntPtr.Zero)
                            result.Add(new GameObj(ptr));
                    }
                }

                factionIdx++;
            }

            return result.ToArray();
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
            // Get TacticalManager singleton via Get() method
            var tmType = _tacticalManagerType?.ManagedType;
            if (tmType == null) return GameObj.Null;

            var getMethod = tmType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            if (getMethod == null) return GameObj.Null;

            var tm = getMethod.Invoke(null, null);
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
