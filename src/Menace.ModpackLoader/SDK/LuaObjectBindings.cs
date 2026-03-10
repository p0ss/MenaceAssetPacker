using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using Menace.SDK.Entities;

namespace Menace.SDK;

/// <summary>
/// Lua object bindings for Phase 3 of the SDK Entity System.
///
/// Registers Actor, Skill, and Tile as MoonSharp UserData types,
/// allowing them to be passed to Lua scripts as objects with methods.
///
/// Usage in Lua:
///   on("skill_used", function(actor, skill)
///       if skill.is_attack and not skill.is_silent then
///           actor:add_effect("concealment", -3, 1)
///       end
///   end)
///
/// The bindings provide:
/// - Actor: add_effect(), get_effect(), clear_effects(), attack(), move_to(), etc.
/// - Skill: is_attack, is_silent, name, get_template_property()
/// - Tile: is_blocked, has_actor, get_cover(), get_occupant()
/// </summary>
public static class LuaObjectBindings
{
    private static bool _initialized;

    /// <summary>
    /// Initialize Lua object bindings. Must be called before using UserData types.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        // Register the wrapper types as UserData
        UserData.RegisterType<LuaActor>();
        UserData.RegisterType<LuaSkill>();
        UserData.RegisterType<LuaTile>();

        SdkLogger.Msg("[LuaObjectBindings] Registered Actor, Skill, Tile UserData types");
    }

    /// <summary>
    /// Register the object binding API functions on a script.
    /// </summary>
    public static void RegisterApi(Script script)
    {
        Initialize();

        // Factory functions for creating wrapped objects
        script.Globals["Actor"] = DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count == 0 || args[0].IsNil()) return DynValue.Nil;
            var ptr = (long)args[0].Number;
            return UserData.Create(new LuaActor(ptr));
        });

        script.Globals["Skill"] = DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count == 0 || args[0].IsNil()) return DynValue.Nil;
            var ptr = (long)args[0].Number;
            return UserData.Create(new LuaSkill(ptr));
        });

        script.Globals["Tile"] = DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count >= 2)
            {
                // Tile(x, y) - get by coordinates
                var x = (int)args[0].Number;
                var y = (int)args[1].Number;
                var tile = LuaTile.At(x, y);
                return tile != null ? UserData.Create(tile) : DynValue.Nil;
            }
            else if (args.Count == 1 && !args[0].IsNil())
            {
                // Tile(ptr) - wrap existing pointer
                var ptr = (long)args[0].Number;
                return UserData.Create(new LuaTile(ptr));
            }
            return DynValue.Nil;
        });

        // Expose effect system directly
        script.Globals["add_effect"] = DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 4) return DynValue.NewBoolean(false);
            var actorPtr = new IntPtr((long)args[0].Number);
            var property = args[1].String;
            var modifier = (int)args[2].Number;
            var rounds = (int)args[3].Number;
            var source = args.Count > 4 ? args[4].String : "";
            EffectSystem.AddEffect(actorPtr, property, modifier, rounds, source);
            return DynValue.NewBoolean(true);
        });

        script.Globals["get_effect"] = DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 2) return DynValue.NewNumber(0);
            var actorPtr = new IntPtr((long)args[0].Number);
            var property = args[1].String;
            return DynValue.NewNumber(EffectSystem.GetModifier(actorPtr, property));
        });

        script.Globals["has_effect"] = DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 2) return DynValue.NewBoolean(false);
            var actorPtr = new IntPtr((long)args[0].Number);
            var property = args[1].String;
            return DynValue.NewBoolean(EffectSystem.HasEffect(actorPtr, property));
        });

        script.Globals["clear_effects"] = DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count < 1) return DynValue.Nil;
            var actorPtr = new IntPtr((long)args[0].Number);
            EffectSystem.ClearEffects(actorPtr);
            return DynValue.Nil;
        });

        // Actor iteration - returns array of LuaActor objects
        script.Globals["actors"] = DynValue.NewCallback((ctx, args) =>
        {
            try
            {
                var gameActors = GameQuery.FindAll("Menace.Tactical.Actor");
                var table = new Table(script);
                int i = 1;
                foreach (var actor in gameActors)
                {
                    if (!actor.IsNull && actor.IsAlive)
                    {
                        table[i++] = UserData.Create(new LuaActor(actor.Pointer));
                    }
                }
                return DynValue.NewTable(table);
            }
            catch
            {
                return DynValue.NewTable(new Table(script));
            }
        });

        // Player actors only
        script.Globals["player_actors"] = DynValue.NewCallback((ctx, args) =>
        {
            try
            {
                var gameActors = GameQuery.FindAll("Menace.Tactical.Actor");
                var table = new Table(script);
                int i = 1;
                foreach (var actor in gameActors)
                {
                    if (actor.IsNull || !actor.IsAlive) continue;
                    var factionId = actor.ReadInt(0xBC);
                    if (factionId == 1 || factionId == 2) // Player factions
                    {
                        table[i++] = UserData.Create(new LuaActor(actor.Pointer));
                    }
                }
                return DynValue.NewTable(table);
            }
            catch
            {
                return DynValue.NewTable(new Table(script));
            }
        });

        // Enemy actors only
        script.Globals["enemy_actors"] = DynValue.NewCallback((ctx, args) =>
        {
            try
            {
                var gameActors = GameQuery.FindAll("Menace.Tactical.Actor");
                var table = new Table(script);
                int i = 1;
                foreach (var actor in gameActors)
                {
                    if (actor.IsNull || !actor.IsAlive) continue;
                    var factionId = actor.ReadInt(0xBC);
                    if (factionId > 3) // Enemy factions
                    {
                        table[i++] = UserData.Create(new LuaActor(actor.Pointer));
                    }
                }
                return DynValue.NewTable(table);
            }
            catch
            {
                return DynValue.NewTable(new Table(script));
            }
        });
    }

    /// <summary>
    /// Create a DynValue for a LuaActor from a pointer.
    /// </summary>
    public static DynValue CreateActor(long ptr)
    {
        if (ptr == 0) return DynValue.Nil;
        Initialize();
        return UserData.Create(new LuaActor(ptr));
    }

    /// <summary>
    /// Create a DynValue for a LuaSkill from a pointer.
    /// </summary>
    public static DynValue CreateSkill(long ptr)
    {
        if (ptr == 0) return DynValue.Nil;
        Initialize();
        return UserData.Create(new LuaSkill(ptr));
    }

    /// <summary>
    /// Create a DynValue for a LuaTile from coordinates.
    /// </summary>
    public static DynValue CreateTile(int x, int y)
    {
        Initialize();
        var tile = LuaTile.At(x, y);
        return tile != null ? UserData.Create(tile) : DynValue.Nil;
    }
}

/// <summary>
/// Lua-friendly wrapper for Actor that exposes methods to MoonSharp.
/// Uses snake_case naming to match Lua conventions.
/// </summary>
[MoonSharpUserData]
public class LuaActor
{
    private readonly Actor _actor;

    public LuaActor(long ptr)
    {
        _actor = Actor.Get(new IntPtr(ptr));
    }

    public LuaActor(IntPtr ptr)
    {
        _actor = Actor.Get(ptr);
    }

    public LuaActor(Actor actor)
    {
        _actor = actor;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Properties (exposed as lowercase for Lua)
    // ═══════════════════════════════════════════════════════════════════

    public long ptr => _actor?.Pointer.ToInt64() ?? 0;
    public bool is_valid => _actor?.IsValid ?? false;
    public string name => _actor?.Name ?? "<invalid>";
    public int faction_id => _actor?.FactionId ?? -1;
    public string faction_name => _actor?.FactionName ?? "Unknown";

    // Position
    public int x => _actor?.Position?.x ?? -1;
    public int y => _actor?.Position?.y ?? -1;

    // Combat stats
    public int action_points => _actor?.ActionPoints ?? 0;
    public float suppression => _actor?.Suppression ?? 0f;
    public float morale => _actor?.Morale ?? 0f;
    public bool is_moving => _actor?.IsMoving ?? false;

    // ═══════════════════════════════════════════════════════════════════
    //  Effect System Methods
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Add a temporary effect to this actor.
    /// actor:add_effect("concealment", -3, 1)
    /// </summary>
    public void add_effect(string property, int modifier, int rounds, string source = "lua")
    {
        _actor?.AddEffect(property, modifier, rounds, source);
    }

    /// <summary>
    /// Get the total modifier for a property from active effects.
    /// local mod = actor:get_effect("concealment")
    /// </summary>
    public int get_effect(string property)
    {
        return _actor?.GetEffectModifier(property) ?? 0;
    }

    /// <summary>
    /// Check if actor has any effects on a property.
    /// if actor:has_effect("concealment") then ...
    /// </summary>
    public bool has_effect(string property)
    {
        return _actor?.HasEffect(property) ?? false;
    }

    /// <summary>
    /// Clear all effects from this actor.
    /// actor:clear_effects()
    /// </summary>
    public void clear_effects()
    {
        _actor?.ClearEffects();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Combat Methods
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attack another actor.
    /// local result = actor:attack(target)
    /// </summary>
    public Table attack(LuaActor target)
    {
        var result = _actor?.Attack(target?._actor);
        var table = new Table(null);
        table["success"] = result?.Success ?? false;
        table["error"] = result?.Error ?? "Invalid actor";
        return table;
    }

    /// <summary>
    /// Use an ability by name.
    /// actor:use_ability("Overwatch", target)
    /// </summary>
    public Table use_ability(string skillName, LuaActor target = null)
    {
        var result = _actor?.UseAbility(skillName, target?._actor);
        var table = new Table(null);
        table["success"] = result?.Success ?? false;
        table["error"] = result?.Error ?? "Invalid actor";
        return table;
    }

    /// <summary>
    /// Apply damage to this actor.
    /// actor:damage(10)
    /// </summary>
    public bool damage(int amount)
    {
        return _actor?.ApplyDamage(amount) ?? false;
    }

    /// <summary>
    /// Heal this actor.
    /// actor:heal(5)
    /// </summary>
    public bool heal(int amount)
    {
        return _actor?.Heal(amount) ?? false;
    }

    /// <summary>
    /// Apply suppression.
    /// actor:suppress(50)
    /// </summary>
    public bool suppress(float amount)
    {
        return _actor?.ApplySuppression(amount) ?? false;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Movement Methods
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Move to coordinates using pathfinding.
    /// actor:move_to(5, 10)
    /// </summary>
    public Table move_to(int x, int y)
    {
        var result = _actor?.MoveTo(x, y);
        var table = new Table(null);
        table["success"] = result?.Success ?? false;
        table["error"] = result?.Error ?? "Invalid actor";
        return table;
    }

    /// <summary>
    /// Teleport instantly to coordinates.
    /// actor:teleport(5, 10)
    /// </summary>
    public Table teleport(int x, int y)
    {
        var result = _actor?.Teleport(x, y);
        var table = new Table(null);
        table["success"] = result?.Success ?? false;
        table["error"] = result?.Error ?? "Invalid actor";
        return table;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Skills
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get list of skill IDs.
    /// for _, id in ipairs(actor:get_skills()) do ...
    /// </summary>
    public List<string> get_skills()
    {
        return _actor?.SkillIDs ?? new List<string>();
    }

    /// <summary>
    /// Check if actor has a skill.
    /// if actor:has_skill("Overwatch") then ...
    /// </summary>
    public bool has_skill(string skillId)
    {
        return _actor?.HasSkill(skillId) ?? false;
    }

    public override string ToString()
    {
        return $"Actor({name}, Faction={faction_name})";
    }
}

/// <summary>
/// Lua-friendly wrapper for Skill that exposes properties to MoonSharp.
/// Uses snake_case naming to match Lua conventions.
/// </summary>
[MoonSharpUserData]
public class LuaSkill
{
    private readonly Skill _skill;

    public LuaSkill(long ptr)
    {
        _skill = new Skill(new IntPtr(ptr));
    }

    public LuaSkill(IntPtr ptr)
    {
        _skill = new Skill(ptr);
    }

    public LuaSkill(Skill skill)
    {
        _skill = skill;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Properties
    // ═══════════════════════════════════════════════════════════════════

    public long ptr => _skill?.Pointer.ToInt64() ?? 0;
    public bool is_valid => _skill?.IsValid ?? false;
    public string name => _skill?.Name ?? "<invalid>";
    public string template_name => _skill?.TemplateName ?? "<no template>";

    /// <summary>
    /// Whether this skill is an attack skill.
    /// if skill.is_attack then ...
    /// </summary>
    public bool is_attack => _skill?.IsAttack ?? false;

    /// <summary>
    /// Whether this skill is silent (doesn't reveal position).
    /// if not skill.is_silent then ...
    /// </summary>
    public bool is_silent => _skill?.IsSilent ?? false;

    // ═══════════════════════════════════════════════════════════════════
    //  Methods
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get a template property by name.
    /// local damage = skill:get_template_property("BaseDamage")
    /// </summary>
    public object get_template_property(string propertyName)
    {
        return _skill?.GetTemplateProperty(propertyName);
    }

    public override string ToString()
    {
        return $"Skill({name}, IsAttack={is_attack}, IsSilent={is_silent})";
    }
}

/// <summary>
/// Lua-friendly wrapper for Tile that exposes properties to MoonSharp.
/// Uses snake_case naming to match Lua conventions.
/// </summary>
[MoonSharpUserData]
public class LuaTile
{
    private readonly Tile _tile;

    public LuaTile(long ptr)
    {
        // Note: Tile needs coordinates, try to look up from pointer
        _tile = new Tile(new IntPtr(ptr));
    }

    public LuaTile(IntPtr ptr)
    {
        _tile = new Tile(ptr);
    }

    public LuaTile(Tile tile)
    {
        _tile = tile;
    }

    private LuaTile(int x, int y)
    {
        _tile = Tile.At(x, y);
    }

    /// <summary>
    /// Create a tile at coordinates.
    /// </summary>
    public static LuaTile At(int x, int y)
    {
        var tile = Tile.At(x, y);
        return tile != null ? new LuaTile(tile) : null;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Properties
    // ═══════════════════════════════════════════════════════════════════

    public long ptr => _tile?.Pointer.ToInt64() ?? 0;
    public bool is_valid => _tile?.IsValid ?? false;
    public int x => _tile?.X ?? -1;
    public int y => _tile?.Y ?? -1;

    /// <summary>
    /// Whether this tile is blocked (impassable).
    /// if not tile.is_blocked then ...
    /// </summary>
    public bool is_blocked => _tile?.IsBlocked ?? true;

    /// <summary>
    /// Whether this tile has an actor on it.
    /// if tile.has_actor then ...
    /// </summary>
    public bool has_actor => _tile?.HasActor ?? false;

    /// <summary>
    /// Whether this tile is visible to the player.
    /// </summary>
    public bool is_visible => _tile?.IsVisibleToPlayer ?? false;

    // ═══════════════════════════════════════════════════════════════════
    //  Methods
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get cover value in a direction (0-7).
    /// local cover = tile:get_cover(0)  -- north
    /// </summary>
    public int get_cover(int direction)
    {
        return _tile?.GetCover(direction) ?? 0;
    }

    /// <summary>
    /// Get cover values in all directions.
    /// local covers = tile:get_all_cover()
    /// </summary>
    public int[] get_all_cover()
    {
        return _tile?.GetAllCover() ?? Array.Empty<int>();
    }

    /// <summary>
    /// Get the actor on this tile (if any).
    /// local actor = tile:get_occupant()
    /// </summary>
    public LuaActor get_occupant()
    {
        var actor = _tile?.GetOccupant();
        return actor != null ? new LuaActor(actor) : null;
    }

    /// <summary>
    /// Get neighbor tile in a direction.
    /// local neighbor = tile:get_neighbor(0)  -- north
    /// </summary>
    public LuaTile get_neighbor(int direction)
    {
        var neighbor = _tile?.GetNeighbor(direction);
        return neighbor != null ? new LuaTile(neighbor) : null;
    }

    /// <summary>
    /// Get distance to another tile.
    /// local dist = tile:distance_to(other_tile)
    /// </summary>
    public int distance_to(LuaTile other)
    {
        if (other == null || other._tile == null) return int.MaxValue;
        return _tile?.DistanceTo(other._tile) ?? int.MaxValue;
    }

    public override string ToString()
    {
        return $"Tile({x}, {y}, Blocked={is_blocked}, HasActor={has_actor})";
    }
}
