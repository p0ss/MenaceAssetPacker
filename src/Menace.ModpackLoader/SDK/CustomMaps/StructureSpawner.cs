using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;

namespace Menace.SDK.CustomMaps;

/// <summary>
/// Spawns structures (buildings, cover objects, static entities) on the tactical map.
///
/// Unlike EntitySpawner which handles TransientActors (units),
/// StructureSpawner handles EntityType=1 (Structure) entities which:
/// - Can occupy multiple tiles (via EntitySegments)
/// - Block line of sight
/// - Have cover properties
/// - Are static (don't move)
///
/// Based on RE findings:
/// - Structure.Create(EntityTemplate, Tile, int faction, int hitpoints)
/// - EntityType at EntityTemplate+0x88: 1=Structure, 2=TransientActor
/// - TacticalManager.InvokeOnEntitySpawned() for notification
/// </summary>
public static class StructureSpawner
{
    // Entity type constants from RE
    private const int ENTITY_TYPE_STRUCTURE = 1;
    private const int ENTITY_TYPE_TRANSIENT_ACTOR = 2;

    // Template offset for entity type
    private const int OFFSET_ENTITY_TYPE = 0x88;

    // Cached types
    private static Type _structureType;
    private static Type _entityTemplateType;
    private static Type _tileType;
    private static Type _tacticalManagerType;
    private static bool _typesLoaded;

    /// <summary>
    /// Result of a structure spawn operation.
    /// </summary>
    public class SpawnResult
    {
        public bool Success { get; set; }
        public GameObj Structure { get; set; }
        public string Error { get; set; }

        public static SpawnResult Failed(string error) => new() { Success = false, Error = error };
        public static SpawnResult Ok(GameObj structure) => new() { Success = true, Structure = structure };
    }

    /// <summary>
    /// Spawn a structure at the specified tile position.
    /// </summary>
    /// <param name="templateName">Name of the EntityTemplate for the structure</param>
    /// <param name="tileX">Tile X coordinate</param>
    /// <param name="tileY">Tile Y coordinate</param>
    /// <param name="faction">Faction ID (0=Neutral, 1=Enemy, 2=Player)</param>
    /// <param name="rotation">Rotation in degrees (0, 90, 180, 270)</param>
    /// <returns>SpawnResult with the spawned structure or error</returns>
    public static SpawnResult SpawnStructure(string templateName, int tileX, int tileY, int faction = 0, float rotation = 0)
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

            // Verify it's a structure type
            int entityType = template.ReadInt((uint)OFFSET_ENTITY_TYPE);
            if (entityType != ENTITY_TYPE_STRUCTURE)
            {
                // If it's an actor, delegate to EntitySpawner
                if (entityType == ENTITY_TYPE_TRANSIENT_ACTOR)
                {
                    SdkLogger.Msg($"[StructureSpawner] Template '{templateName}' is an actor, delegating to EntitySpawner");
                    var actorResult = EntitySpawner.SpawnUnit(templateName, tileX, tileY, faction);
                    return new SpawnResult
                    {
                        Success = actorResult.Success,
                        Structure = actorResult.Entity,
                        Error = actorResult.Error
                    };
                }
                return SpawnResult.Failed($"Template '{templateName}' has unknown entity type: {entityType}");
            }

            // Get the tile
            var tile = GetTileAt(tileX, tileY);
            if (tile.IsNull)
            {
                return SpawnResult.Failed($"Tile at ({tileX}, {tileY}) not found");
            }

            // Get managed proxies
            var templateProxy = GetManagedProxy(template, _entityTemplateType);
            var tileProxy = GetManagedProxy(tile, _tileType);

            if (templateProxy == null || tileProxy == null)
            {
                return SpawnResult.Failed("Failed to create managed proxies");
            }

            // Create Structure instance
            var structureCtor = _structureType.GetConstructor(Type.EmptyTypes);
            if (structureCtor == null)
            {
                return SpawnResult.Failed("Structure constructor not found");
            }

            var structure = structureCtor.Invoke(null);
            if (structure == null)
            {
                return SpawnResult.Failed("Failed to create Structure instance");
            }

            // Set rotation if supported
            if (rotation != 0)
            {
                SetRotation(structure, rotation);
            }

            // Call Structure.Create(EntityTemplate, Tile, int faction, int hitpoints)
            var createMethod = _structureType.GetMethod("Create", BindingFlags.Public | BindingFlags.Instance,
                null, new[] { _entityTemplateType, _tileType, typeof(int), typeof(int) }, null);

            if (createMethod == null)
            {
                return SpawnResult.Failed("Structure.Create method not found");
            }

            createMethod.Invoke(structure, new object[] { templateProxy, tileProxy, faction, 0 });

            // Notify TacticalManager
            NotifyEntitySpawned(structure);

            var structureObj = new GameObj(((Il2CppObjectBase)structure).Pointer);

            SdkLogger.Msg($"[StructureSpawner] Spawned structure '{templateName}' at ({tileX}, {tileY})");
            return SpawnResult.Ok(structureObj);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("StructureSpawner.SpawnStructure", $"Failed to spawn {templateName}", ex);
            return SpawnResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Spawn a structure or actor based on the template's entity type.
    /// Automatically determines whether to use Structure.Create or TransientActor.Create.
    /// </summary>
    public static SpawnResult SpawnEntity(string templateName, int tileX, int tileY, int faction = 0, float rotation = 0)
    {
        try
        {
            EnsureTypesLoaded();

            // Find the template and check its type
            var template = Templates.Find("Menace.Tactical.EntityTemplate", templateName);
            if (template.IsNull)
            {
                return SpawnResult.Failed($"Template '{templateName}' not found");
            }

            int entityType = template.ReadInt((uint)OFFSET_ENTITY_TYPE);

            if (entityType == ENTITY_TYPE_STRUCTURE)
            {
                return SpawnStructure(templateName, tileX, tileY, faction, rotation);
            }
            else if (entityType == ENTITY_TYPE_TRANSIENT_ACTOR)
            {
                var result = EntitySpawner.SpawnUnit(templateName, tileX, tileY, faction);
                return new SpawnResult
                {
                    Success = result.Success,
                    Structure = result.Entity,
                    Error = result.Error
                };
            }
            else
            {
                return SpawnResult.Failed($"Unknown entity type: {entityType}");
            }
        }
        catch (Exception ex)
        {
            return SpawnResult.Failed($"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if a template is a structure (EntityType=1).
    /// </summary>
    public static bool IsStructure(string templateName)
    {
        var template = Templates.Find("Menace.Tactical.EntityTemplate", templateName);
        if (template.IsNull) return false;
        return template.ReadInt((uint)OFFSET_ENTITY_TYPE) == ENTITY_TYPE_STRUCTURE;
    }

    /// <summary>
    /// Check if a template is an actor (EntityType=2).
    /// </summary>
    public static bool IsActor(string templateName)
    {
        var template = Templates.Find("Menace.Tactical.EntityTemplate", templateName);
        if (template.IsNull) return false;
        return template.ReadInt((uint)OFFSET_ENTITY_TYPE) == ENTITY_TYPE_TRANSIENT_ACTOR;
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        if (_typesLoaded) return;

        var gameType = GameType.Find("Menace.Tactical.Structure");
        _structureType = gameType?.ManagedType;

        gameType = GameType.Find("Menace.Tactical.EntityTemplate");
        _entityTemplateType = gameType?.ManagedType;

        gameType = GameType.Find("Menace.Tactical.Tile");
        _tileType = gameType?.ManagedType;

        gameType = GameType.Find("Menace.Tactical.TacticalManager");
        _tacticalManagerType = gameType?.ManagedType;

        _typesLoaded = true;
    }

    private static GameObj GetTileAt(int x, int y)
    {
        try
        {
            var tmType = _tacticalManagerType;
            if (tmType == null) return GameObj.Null;

            var getMethod = tmType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            if (getMethod == null) return GameObj.Null;

            var tm = getMethod.Invoke(null, null);
            if (tm == null) return GameObj.Null;

            var getMapMethod = tmType.GetMethod("GetMap", BindingFlags.Public | BindingFlags.Instance);
            if (getMapMethod == null) return GameObj.Null;

            var map = getMapMethod.Invoke(tm, null);
            if (map == null) return GameObj.Null;

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
            ModError.ReportInternal("StructureSpawner.GetTileAt", $"Failed for ({x}, {y})", ex);
            return GameObj.Null;
        }
    }

    private static object GetManagedProxy(GameObj obj, Type managedType)
        => Il2CppUtils.GetManagedProxy(obj, managedType);

    private static void SetRotation(object structure, float rotation)
    {
        try
        {
            // Try to set rotation direction property
            // Direction enum: 0=N, 2=E, 4=S, 6=W (each 90 degrees)
            int direction = ((int)(rotation / 45)) % 8;

            var rotationProp = _structureType.GetProperty("Rotation", BindingFlags.Public | BindingFlags.Instance);
            if (rotationProp != null && rotationProp.CanWrite)
            {
                rotationProp.SetValue(structure, direction);
            }
        }
        catch { /* Rotation not supported, continue without it */ }
    }

    private static void NotifyEntitySpawned(object entity)
    {
        try
        {
            if (_tacticalManagerType == null) return;

            var getMethod = _tacticalManagerType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
            var tm = getMethod?.Invoke(null, null);
            if (tm == null) return;

            var invokeMethod = _tacticalManagerType.GetMethod("InvokeOnEntitySpawned",
                BindingFlags.Public | BindingFlags.Instance);

            invokeMethod?.Invoke(tm, new object[] { entity });
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("StructureSpawner.NotifyEntitySpawned", ex.Message);
        }
    }

    /// <summary>
    /// Register console commands for structure spawning.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // spawnstructure <template> <x> <y> [faction] [rotation]
        DevConsole.RegisterCommand("spawnstructure", "<template> <x> <y> [faction] [rotation]",
            "Spawn a structure at a tile position", args =>
        {
            if (args.Length < 3)
                return "Usage: spawnstructure <template> <x> <y> [faction=0] [rotation=0]";

            var template = args[0];
            if (!int.TryParse(args[1], out var x) || !int.TryParse(args[2], out var y))
                return "Invalid coordinates";

            int faction = args.Length > 3 && int.TryParse(args[3], out var f) ? f : 0;
            float rotation = args.Length > 4 && float.TryParse(args[4], out var r) ? r : 0;

            var result = SpawnStructure(template, x, y, faction, rotation);

            return result.Success
                ? $"Spawned structure '{template}' at ({x}, {y})"
                : $"Failed: {result.Error}";
        });

        // spawnentity <template> <x> <y> [faction] [rotation] - auto-detects type
        DevConsole.RegisterCommand("spawnentity", "<template> <x> <y> [faction] [rotation]",
            "Spawn an entity (structure or actor) at a tile position", args =>
        {
            if (args.Length < 3)
                return "Usage: spawnentity <template> <x> <y> [faction=0] [rotation=0]";

            var template = args[0];
            if (!int.TryParse(args[1], out var x) || !int.TryParse(args[2], out var y))
                return "Invalid coordinates";

            int faction = args.Length > 3 && int.TryParse(args[3], out var f) ? f : 0;
            float rotation = args.Length > 4 && float.TryParse(args[4], out var r) ? r : 0;

            var result = SpawnEntity(template, x, y, faction, rotation);

            return result.Success
                ? $"Spawned entity '{template}' at ({x}, {y})"
                : $"Failed: {result.Error}";
        });
    }
}
