using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// SDK wrapper for tile effects operations.
/// Provides safe access to spawning, querying, and removing tile effects (fire, smoke, ammo, etc.).
///
/// Based on reverse engineering findings:
/// - TileEffectHandler base class @ 0x10697fb0
/// - Tile.Effects list @ +0x68
/// - Types: ApplySkillTileEffectHandler, BleedOutTileEffectHandler, RefillAmmoTileEffectHandler, etc.
/// </summary>
public static class TileEffects
{
    // Cached types
    private static GameType _tileEffectHandlerType;
    private static GameType _tileEffectTemplateType;
    private static GameType _tileType;
    private static GameType _tacticalManagerType;

    /// <summary>
    /// Effect information structure.
    /// </summary>
    public class EffectInfo
    {
        public string TypeName { get; set; }
        public string TemplateName { get; set; }
        public int RoundsElapsed { get; set; }
        public int Duration { get; set; }
        public int RoundsRemaining { get; set; }
        public bool BlocksLOS { get; set; }
        public IntPtr Pointer { get; set; }
    }

    /// <summary>
    /// Get all effects on a tile.
    /// </summary>
    public static List<EffectInfo> GetEffects(int x, int y)
    {
        var tile = TileMap.GetTile(x, y);
        return GetEffects(tile);
    }

    /// <summary>
    /// Get all effects on a tile.
    /// </summary>
    public static List<EffectInfo> GetEffects(GameObj tile)
    {
        var result = new List<EffectInfo>();
        if (tile.IsNull) return result;

        try
        {
            EnsureTypesLoaded();

            var tileType = _tileType?.ManagedType;
            if (tileType == null) return result;

            var proxy = GetManagedProxy(tile, tileType);
            if (proxy == null) return result;

            // Get Effects property/field
            var effectsProp = tileType.GetProperty("Effects", BindingFlags.Public | BindingFlags.Instance);
            if (effectsProp == null)
                effectsProp = tileType.GetProperty("effects", BindingFlags.NonPublic | BindingFlags.Instance);

            var effects = effectsProp?.GetValue(proxy);
            if (effects == null) return result;

            // Iterate list
            var listType = effects.GetType();
            var countProp = listType.GetProperty("Count");
            var indexer = listType.GetMethod("get_Item");

            int count = (int)countProp.GetValue(effects);
            for (int i = 0; i < count; i++)
            {
                var effect = indexer.Invoke(effects, new object[] { i });
                if (effect == null) continue;

                var info = new EffectInfo
                {
                    TypeName = effect.GetType().Name,
                    Pointer = ((Il2CppObjectBase)effect).Pointer
                };

                // Get template info
                try
                {
                    var getTemplateMethod = effect.GetType().GetMethod("GetTemplate",
                        BindingFlags.Public | BindingFlags.Instance);
                    var template = getTemplateMethod?.Invoke(effect, null);
                    if (template != null)
                    {
                        var templateObj = new GameObj(((Il2CppObjectBase)template).Pointer);
                        info.TemplateName = templateObj.GetName();

                        var hasDurationProp = template.GetType().GetProperty("HasDuration");
                        var durationProp = template.GetType().GetProperty("Duration");
                        var blocksLosProp = template.GetType().GetProperty("BlocksLineOfSight");

                        if (hasDurationProp != null && (bool)hasDurationProp.GetValue(template))
                        {
                            info.Duration = (int)(durationProp?.GetValue(template) ?? 0);
                        }
                        if (blocksLosProp != null)
                        {
                            info.BlocksLOS = (bool)blocksLosProp.GetValue(template);
                        }
                    }
                }
                catch { }

                // Get rounds elapsed
                try
                {
                    var roundsElapsedField = effect.GetType().GetField("RoundsElapsed",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (roundsElapsedField != null)
                    {
                        info.RoundsElapsed = (int)roundsElapsedField.GetValue(effect);
                        info.RoundsRemaining = Math.Max(0, info.Duration - info.RoundsElapsed);
                    }
                }
                catch { }

                result.Add(info);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TileEffects.GetEffects", "Failed", ex);
            return result;
        }
    }

    /// <summary>
    /// Check if a tile has any effects.
    /// </summary>
    public static bool HasEffects(int x, int y)
    {
        var tile = TileMap.GetTile(x, y);
        return HasEffects(tile);
    }

    /// <summary>
    /// Check if a tile has any effects.
    /// </summary>
    public static bool HasEffects(GameObj tile)
    {
        if (tile.IsNull) return false;

        try
        {
            EnsureTypesLoaded();

            var tileType = _tileType?.ManagedType;
            if (tileType == null) return false;

            var proxy = GetManagedProxy(tile, tileType);
            if (proxy == null) return false;

            var hasEffectMethod = tileType.GetMethod("HasEffect", BindingFlags.Public | BindingFlags.Instance);
            if (hasEffectMethod != null)
            {
                return (bool)hasEffectMethod.Invoke(proxy, null);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if a tile has a specific effect type (by name pattern).
    /// </summary>
    public static bool HasEffectType(int x, int y, string typeNameContains)
    {
        var effects = GetEffects(x, y);
        foreach (var effect in effects)
        {
            if (effect.TypeName.Contains(typeNameContains, StringComparison.OrdinalIgnoreCase) ||
                (effect.TemplateName?.Contains(typeNameContains, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Check if tile is on fire.
    /// </summary>
    public static bool IsOnFire(int x, int y)
    {
        return HasEffectType(x, y, "Fire");
    }

    /// <summary>
    /// Check if tile has smoke.
    /// </summary>
    public static bool HasSmoke(int x, int y)
    {
        return HasEffectType(x, y, "Smoke");
    }

    /// <summary>
    /// Check if tile has ammo crate.
    /// </summary>
    public static bool HasAmmo(int x, int y)
    {
        return HasEffectType(x, y, "Ammo") || HasEffectType(x, y, "Refill");
    }

    /// <summary>
    /// Check if tile has a bleeding out unit.
    /// </summary>
    public static bool HasBleedingUnit(int x, int y)
    {
        return HasEffectType(x, y, "BleedOut");
    }

    /// <summary>
    /// Remove all effects from a tile.
    /// </summary>
    public static int ClearEffects(int x, int y)
    {
        var tile = TileMap.GetTile(x, y);
        return ClearEffects(tile);
    }

    /// <summary>
    /// Remove all effects from a tile.
    /// </summary>
    public static int ClearEffects(GameObj tile)
    {
        if (tile.IsNull) return 0;

        try
        {
            var effects = GetEffects(tile);
            int count = effects.Count;

            EnsureTypesLoaded();

            var tileType = _tileType?.ManagedType;
            if (tileType == null) return 0;

            var proxy = GetManagedProxy(tile, tileType);
            if (proxy == null) return 0;

            var removeMethod = tileType.GetMethod("RemoveEffect", BindingFlags.Public | BindingFlags.Instance);
            if (removeMethod == null) return 0;

            // Remove in reverse to avoid index issues
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var effectProxy = GetManagedProxy(new GameObj(effects[i].Pointer), _tileEffectHandlerType?.ManagedType);
                if (effectProxy != null)
                {
                    removeMethod.Invoke(proxy, new[] { effectProxy });
                }
            }

            return count;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TileEffects.ClearEffects", "Failed", ex);
            return 0;
        }
    }

    /// <summary>
    /// Spawn a tile effect by template name.
    /// </summary>
    public static bool SpawnEffect(int x, int y, string templateName, int delay = 0)
    {
        var tile = TileMap.GetTile(x, y);
        return SpawnEffect(tile, templateName, delay);
    }

    /// <summary>
    /// Spawn a tile effect by template name.
    /// </summary>
    public static bool SpawnEffect(GameObj tile, string templateName, int delay = 0)
    {
        if (tile.IsNull) return false;

        try
        {
            // Find template
            var template = GameQuery.FindByName("TileEffectTemplate", templateName);
            if (template.IsNull)
            {
                // Try with suffix
                template = GameQuery.FindByName("TileEffectTemplate", templateName + "TileEffectTemplate");
            }
            if (template.IsNull)
            {
                ModError.ReportInternal("TileEffects.SpawnEffect", $"Template '{templateName}' not found");
                return false;
            }

            EnsureTypesLoaded();

            var templateType = _tileEffectTemplateType?.ManagedType;
            if (templateType == null) return false;

            var templateProxy = GetManagedProxy(template, templateType);
            if (templateProxy == null) return false;

            // Create handler from template
            var createHandlerMethod = templateType.GetMethod("CreateHandler",
                BindingFlags.Public | BindingFlags.Instance);
            var handler = createHandlerMethod?.Invoke(templateProxy, new object[] { delay });
            if (handler == null) return false;

            // Add to tile
            var tileType = _tileType?.ManagedType;
            if (tileType == null) return false;

            var tileProxy = GetManagedProxy(tile, tileType);
            if (tileProxy == null) return false;

            var addEffectMethod = tileType.GetMethod("AddEffect", BindingFlags.Public | BindingFlags.Instance);
            addEffectMethod?.Invoke(tileProxy, new[] { handler });

            return true;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("TileEffects.SpawnEffect", "Failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Get all effect templates available in the game.
    /// </summary>
    public static string[] GetAvailableEffectTemplates()
    {
        var templates = GameQuery.FindAll("TileEffectTemplate");
        var result = new List<string>();
        foreach (var t in templates)
        {
            var name = t.GetName();
            if (!string.IsNullOrEmpty(name))
                result.Add(name);
        }
        result.Sort();
        return result.ToArray();
    }

    /// <summary>
    /// Register console commands for TileEffects SDK.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // effects <x> <y> - List effects on tile
        DevConsole.RegisterCommand("effects", "<x> <y>", "List effects on tile", args =>
        {
            if (args.Length < 2)
                return "Usage: effects <x> <y>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
                return "Invalid coordinates";

            var effects = GetEffects(x, y);
            if (effects.Count == 0)
                return $"No effects on tile ({x}, {y})";

            var lines = new List<string> { $"Effects on ({x}, {y}):" };
            foreach (var e in effects)
            {
                var duration = e.Duration > 0 ? $" ({e.RoundsRemaining} rounds left)" : "";
                lines.Add($"  {e.TypeName}: {e.TemplateName}{duration}");
            }
            return string.Join("\n", lines);
        });

        // hasfire <x> <y> - Check if tile is on fire
        DevConsole.RegisterCommand("hasfire", "<x> <y>", "Check if tile is on fire", args =>
        {
            if (args.Length < 2)
                return "Usage: hasfire <x> <y>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
                return "Invalid coordinates";

            return $"Tile ({x}, {y}) on fire: {IsOnFire(x, y)}";
        });

        // hassmoke <x> <y> - Check if tile has smoke
        DevConsole.RegisterCommand("hassmoke", "<x> <y>", "Check if tile has smoke", args =>
        {
            if (args.Length < 2)
                return "Usage: hassmoke <x> <y>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
                return "Invalid coordinates";

            return $"Tile ({x}, {y}) has smoke: {HasSmoke(x, y)}";
        });

        // cleareffects <x> <y> - Remove all effects from tile
        DevConsole.RegisterCommand("cleareffects", "<x> <y>", "Clear effects from tile", args =>
        {
            if (args.Length < 2)
                return "Usage: cleareffects <x> <y>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
                return "Invalid coordinates";

            var count = ClearEffects(x, y);
            return $"Cleared {count} effects from ({x}, {y})";
        });

        // spawneffect <x> <y> <template> - Spawn effect on tile
        DevConsole.RegisterCommand("spawneffect", "<x> <y> <template>", "Spawn effect on tile", args =>
        {
            if (args.Length < 3)
                return "Usage: spawneffect <x> <y> <template>";
            if (!int.TryParse(args[0], out int x) || !int.TryParse(args[1], out int y))
                return "Invalid coordinates";

            var template = string.Join(" ", args, 2, args.Length - 2);
            var success = SpawnEffect(x, y, template);
            return success ? $"Spawned '{template}' at ({x}, {y})" : $"Failed to spawn '{template}'";
        });

        // effecttypes - List available effect templates
        DevConsole.RegisterCommand("effecttypes", "", "List available effect templates", args =>
        {
            var templates = GetAvailableEffectTemplates();
            if (templates.Length == 0)
                return "No effect templates found";

            var lines = new List<string> { $"Effect templates ({templates.Length}):" };
            foreach (var t in templates.Take(30))
            {
                lines.Add($"  {t}");
            }
            if (templates.Length > 30)
                lines.Add($"  ... and {templates.Length - 30} more");
            return string.Join("\n", lines);
        });
    }

    // --- Internal helpers ---

    private static void EnsureTypesLoaded()
    {
        _tileEffectHandlerType ??= GameType.Find("Menace.Tactical.TileEffectHandler");
        _tileEffectTemplateType ??= GameType.Find("Menace.Tactical.TileEffectTemplate");
        _tileType ??= GameType.Find("Menace.Tactical.Tile");
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
