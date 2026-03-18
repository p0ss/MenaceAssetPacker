using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MoonSharp.Interpreter;
using CustomMaps = Menace.SDK.CustomMaps;

namespace Menace.SDK;

/// <summary>
/// Lua scripting engine that exposes console commands and tactical SDK to Lua scripts.
///
/// Core API:
///   cmd("command args")            - Execute a console command, returns {success, result}
///   log("message")                 - Log to console
///   warn("message")                - Log warning
///   error("message")               - Log error
///   on("event", function)          - Register event callback
///   off("event", function)         - Unregister event callback
///   emit("event", args...)         - Fire an event
///   commands()                     - Get list of all console commands
///   has_command("cmd")             - Check if a command exists
///
/// Black Market API:
///   blackmarket_stock("template")  - Add item to black market, returns {success, message}
///   blackmarket_has("template")    - Check if item exists in black market
///
/// Actor Query API:
///   get_actors()                   - Get all actors as table [{ptr, name, alive, x, y}, ...]
///   get_player_actors()            - Get player-controlled actors
///   get_enemy_actors()             - Get enemy actors
///   find_actor("name")             - Find actor by name
///   get_active_actor()             - Get currently selected actor
///
/// Movement API (actor = table with ptr field or number):
///   move_to(actor, x, y)           - Move actor to tile, returns {success, error}
///   teleport(actor, x, y)          - Teleport actor instantly
///   get_position(actor)            - Get actor position, returns {x, y}
///   get_ap(actor)                  - Get remaining action points
///   set_ap(actor, ap)              - Set action points
///   get_facing(actor)              - Get facing direction (0-7)
///   set_facing(actor, dir)         - Set facing direction
///   is_moving(actor)               - Check if actor is moving
///
/// Combat API:
///   attack(attacker, target)       - Attack target, returns {success, error, skill, damage}
///   use_ability(actor, skill, target?) - Use ability on target
///   get_skills(actor)              - Get actor skills as table
///   get_hp(actor)                  - Get HP, returns {current, max, percent}
///   set_hp(actor, hp)              - Set HP value
///   damage(actor, amount)          - Apply damage
///   heal(actor, amount)            - Heal actor
///   get_suppression(actor)         - Get suppression (0-100)
///   set_suppression(actor, value)  - Set suppression
///   get_morale(actor)              - Get morale
///   set_morale(actor, value)       - Set morale
///   set_stunned(actor, bool)       - Set stunned state
///   get_combat_info(actor)         - Get full combat info as table
///
/// Tactical State API:
///   get_round()                    - Get current round number
///   get_faction()                  - Get current faction ID
///   get_faction_name(id)           - Get faction name from ID
///   is_player_turn()               - Check if player's turn
///   is_paused()                    - Check if game paused
///   pause(bool?)                   - Pause game (default true)
///   unpause()                      - Unpause game
///   toggle_pause()                 - Toggle pause state
///   end_turn()                     - End current turn
///   next_round()                   - Advance to next round
///   next_faction()                 - Advance to next faction
///   get_time_scale()               - Get game speed
///   set_time_scale(scale)          - Set game speed (1.0 = normal)
///   get_tactical_state()           - Get full tactical state as table
///   is_mission_running()           - Check if mission is active
///
/// TileMap API:
///   get_tile_info(x, z)            - Get tile info, returns {x, z, elevation, blocked, ...}
///   get_cover(x, z, dir)           - Get cover value (0-3) in direction
///   get_all_cover(x, z)            - Get cover in all 8 directions
///   is_blocked(x, z)               - Check if tile is impassable
///   has_actor_at(x, z)             - Check if tile has actor
///   is_visible(x, z)               - Check if tile visible to player
///   get_map_info()                 - Get map dimensions, returns {width, height, fog_of_war}
///   get_actor_at(x, z)             - Get actor on tile
///   get_distance(x1, z1, x2, z2)   - Get distance between tiles
///
/// Spawn API (experimental - may crash):
///   spawn_unit(template, x, y, faction?) - Spawn unit at tile, returns {success, error, entity}
///   destroy_entity(actor, immediate?)    - Kill an entity
///   clear_enemies(immediate?)            - Clear all enemies, returns count
///   list_entities(faction?)              - List entities by faction (-1 for all)
///   get_entity_info(actor)               - Get entity info as table
///
/// Tile Effects API:
///   get_tile_effects(x, z)               - Get all effects on tile as table
///   has_effects(x, z)                    - Check if tile has any effects
///   is_on_fire(x, z)                     - Check if tile is on fire
///   has_smoke(x, z)                      - Check if tile has smoke
///   spawn_effect(x, z, template, delay?) - Spawn effect on tile
///   clear_tile_effects(x, z)             - Remove all effects, returns count
///   get_effect_templates()               - List available effect template names
///
/// Inventory/Item API:
///   give_item(actor?, template)          - Give item to actor (nil = active actor)
///   get_inventory(actor?)                - Get all items as table
///   get_equipped_weapons(actor?)         - Get equipped weapons
///   get_equipped_armor(actor?)           - Get equipped armor
///   get_item_templates(filter?)          - List item template names
///
/// Custom Maps API (maps.* table):
///   maps.list()                          - List all registered maps
///   maps.get(id)                         - Get map config by ID
///   maps.set_active(id_or_config)        - Set active map override
///   maps.clear_active()                  - Clear active override
///   maps.get_active()                    - Get current active map
///   maps.has_active()                    - Check if override is set
///   maps.load_directory(path)            - Load maps from directory
///   maps.count()                         - Get registered map count
///   maps.play_with_seed(seed)            - Quick play with seed
///   maps.play_with_size(size)            - Quick play with size
///   maps.play_with(seed, size)           - Quick play with seed+size
///   maps.create(id)                      - Create map builder (chainable)
///     :with_name(name)
///     :with_author(author)
///     :with_seed(seed)
///     :with_size(size)
///     :with_layers("easy", "medium", ...)
///     :with_tags("tag1", "tag2", ...)
///     :disable_generator("CoverGenerator")
///     :configure_generator("PropGenerator", {enabled=true, properties={count=100}})
///     :build()                           - Return config table
///     :register()                        - Build and register
///     :activate()                        - Build, register, and set active
///   maps.register(config_table)          - Register map from table
///
/// Object Bindings API (Phase 3):
///   Actor(ptr)                           - Create Actor object from pointer
///   Skill(ptr)                           - Create Skill object from pointer
///   Tile(x, y)                           - Get Tile object at coordinates
///   actors()                             - Get all alive actors as Actor objects
///   player_actors()                      - Get player faction actors
///   enemy_actors()                       - Get enemy faction actors
///
///   Actor object methods:
///     actor:add_effect(prop, mod, rounds) - Add temporary effect
///     actor:get_effect(prop)              - Get effect modifier
///     actor:has_effect(prop)              - Check if has effect
///     actor:clear_effects()               - Clear all effects
///     actor:attack(target)                - Attack another actor
///     actor:use_ability(name, target)     - Use ability by name
///     actor:damage(amount)                - Apply damage
///     actor:heal(amount)                  - Heal actor
///     actor:move_to(x, y)                 - Move to position
///     actor:teleport(x, y)                - Teleport to position
///     actor.name, actor.faction_id, actor.x, actor.y, etc.
///
///   Skill object properties:
///     skill.is_attack                     - Is this an attack skill?
///     skill.is_silent                     - Is this a silent skill?
///     skill.name, skill.template_name
///     skill:get_template_property(name)   - Get template property
///
///   Tile object methods:
///     tile:get_cover(direction)           - Get cover in direction
///     tile:get_occupant()                 - Get actor on tile
///     tile.is_blocked, tile.has_actor, tile.is_visible
///
///   Effect system (standalone functions):
///     add_effect(ptr, prop, mod, rounds)  - Add effect to actor
///     get_effect(ptr, prop)               - Get effect modifier
///     has_effect(ptr, prop)               - Check if has effect
///     clear_effects(ptr)                  - Clear all effects
///
/// Object-based Events (skill_used passes Actor and Skill objects):
///   on("skill_used", function(actor, skill)
///       if skill.is_attack and not skill.is_silent then
///           actor:add_effect("concealment", -3, 1)
///       end
///   end)
///
/// Tactical Events:
///   scene_loaded(sceneName)        - Fired when a scene loads
///   tactical_ready()               - Fired when tactical battle is ready
///   mission_start(missionInfo)     - Fired at mission start
///   turn_start(factionIndex)       - Fired at turn start
///   turn_end(factionIndex)         - Fired at turn end
///
/// Strategy Events:
///   campaign_start()               - Fired when a new campaign starts (before pools built)
///   campaign_loaded()              - Fired when a saved campaign is loaded
///   operation_end()                - Fired when an operation completes
///   blackmarket_refresh()          - Fired before black market restocks
/// </summary>
public class LuaScriptEngine
{
    private static LuaScriptEngine _instance;
    public static LuaScriptEngine Instance => _instance ??= new LuaScriptEngine();

    /// <summary>
    /// Number of Lua scripts currently loaded.
    /// </summary>
    public int LoadedScriptCount => _loadedScripts.Count;

    private readonly Script _lua;
    // Store handlers with their owning script to avoid cross-script resource errors
    private readonly Dictionary<string, List<(Script OwnerScript, DynValue Handler)>> _eventHandlers = new();
    private readonly List<(string ModId, string ScriptPath, Script Script)> _loadedScripts = new();
    // Lua interceptors that can modify values (separate from fire-and-forget events)
    // Keys are dynamically created when interceptors are registered - no fixed list needed
    private readonly Dictionary<string, List<(Script OwnerScript, DynValue Handler)>> _interceptors = new();

    // Supported events - see TacticalEventHooks.cs for event source
    private static readonly HashSet<string> ValidEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        // Scene/mission lifecycle
        "scene_loaded",
        "tactical_ready",
        "mission_start",

        // Combat events (from TacticalEventHooks)
        "actor_killed",
        "damage_received",
        "attack_missed",
        "attack_start",
        "critical_hit",
        "overwatch_triggered",
        "grenade_thrown",
        "bleeding_out",
        "stabilized",
        "suppressed",
        "suppression_applied",

        // Actor state events
        "actor_state_changed",
        "morale_changed",
        "hp_changed",
        "armor_changed",
        "ap_changed",

        // Visibility events
        "discovered",
        "visible_to_player",
        "hidden_from_player",

        // Movement events
        "move_start",
        "move_complete",

        // Skill events
        "skill_used",      // Phase 3: Passes Actor and Skill objects directly
        "skill_complete",
        "skill_added",
        "offmap_ability_used",
        "offmap_ability_canceled",

        // Turn/round events
        "turn_start",
        "turn_end",
        "round_start",
        "round_end",

        // Entity events
        "entity_spawned",
        "actor_spawned",  // alias for entity_spawned (actor subset)
        "reinforcements_spawned",
        "element_destroyed",
        "element_malfunction",

        // Mission events
        "objective_changed",

        // Strategy events (from EarlyTemplateInjection)
        "campaign_start",
        "campaign_loaded",
        "operation_end",
        "blackmarket_refresh",

        // Strategy events (from StrategyEventHooks)
        "leader_hired",
        "leader_dismissed",
        "leader_permadeath",
        "leader_levelup",
        "faction_trust_changed",
        "faction_status_changed",
        "faction_upgrade_unlocked",
        "squaddie_killed",
        "squaddie_added",
        "mission_ended",
        "operation_finished",
        "blackmarket_item_added",
        "blackmarket_restocked",

        // Legacy compatibility aliases
        "actor_damaged",  // alias for damage_received
        "ability_used",   // alias for skill_used

        // ═══════════════════════════════════════════════════════════════════
        //  INTERCEPTOR EVENTS (from Intercept.cs)
        // ═══════════════════════════════════════════════════════════════════

        // Property interceptors - EntityProperties
        "property_damage",
        "property_accuracy",
        "property_armor",
        "property_concealment",
        "property_detection",
        "property_vision",
        "property_damage_dropoff",
        "property_damage_to_armor_durability",
        "property_damage_to_armor_durability_dropoff",
        "property_accuracy_dropoff",
        "property_armor_penetration",
        "property_armor_penetration_dropoff",
        "property_suppression",
        "property_discipline",
        "property_hitpoints_per_element",
        "property_max_hitpoints",
        "property_action_points",
        "property_movement_cost_modifier",
        "property_value",

        // Skill interceptors
        "skill_hitchance",
        "skill_covermult",
        "skill_expected_damage",
        "skill_expected_suppression",
        "skill_ap_cost",
        "skill_ideal_range",
        "skill_max_range",
        "skill_min_range",
        "skill_is_in_range",
        "skill_is_in_range_shape",
        "skill_is_movement",

        // Actor interceptors
        "actor_los",

        // Entity state interceptors
        "entity_hitpoints_pct",
        "entity_armor_durability_pct",
        "entity_cover_usage",
        "entity_provided_cover",
        "entity_is_discovered",
        "entity_last_skill_used",
        "entity_scale_range",

        // Tile interceptors
        "tile_has_los",
        "tile_blocking_los",
        "tile_get_cover",
        "tile_get_cover_mask",
        "tile_entity_cover",
        "tile_can_enter",
        "tile_can_enter_by",

        // BaseTile interceptors
        "basetile_has_cover",
        "basetile_has_half_cover",
        "basetile_has_half_cover_dir",
        "basetile_movement_blocked",

        // LineOfSight interceptors
        "los_raytrace",
        "los_near_corner",

        // Movement interceptors
        "movement_max_speed",
        "movement_path_cost",
        "movement_turn_speed",
        "movement_slowdown_distance",
        "movement_max_angle_turn_slowdown",
        "movement_clip_path",

        // Strategy layer interceptors
        "strategy_action_points",
        "strategy_hitpoints_per_element",
        "strategy_damage_sustained_mult",
        "strategy_hitpoints_pct",
        "strategy_can_be_promoted",
        "strategy_can_be_demoted",
        "strategy_entity_property",
        "strategy_vehicle_armor",

        // AI behavior interceptors
        "ai_attack_score",
        "ai_threat_value",
        "ai_action_priority",
        "ai_should_flee"
    };

    private LuaScriptEngine()
    {
        // Create Lua state with limited permissions (no OS/IO access)
        _lua = new Script(CoreModules.Preset_SoftSandbox);

        // Initialize object bindings (registers UserData types)
        LuaObjectBindings.Initialize();

        // Register API functions
        RegisterApiCallbacks(_lua);

        // Initialize event handler lists
        foreach (var evt in ValidEvents)
            _eventHandlers[evt] = new List<(Script, DynValue)>();
    }

    /// <summary>
    /// Register all API callbacks on a script instance.
    /// Uses explicit DynValue.NewCallback for reliable delegate binding.
    /// </summary>
    private void RegisterApiCallbacks(Script script)
    {
        script.Globals["cmd"] = DynValue.NewCallback((ctx, args) => LuaCmd(args[0].String));
        script.Globals["log"] = DynValue.NewCallback((ctx, args) => { LuaLog(args[0].String); return DynValue.NewString($"logged: {args[0].String}"); });
        script.Globals["warn"] = DynValue.NewCallback((ctx, args) => { LuaWarn(args[0].String); return DynValue.Nil; });
        script.Globals["error"] = DynValue.NewCallback((ctx, args) => { LuaError(args[0].String); return DynValue.Nil; });
        script.Globals["on"] = DynValue.NewCallback((ctx, args) => { LuaOn(args[0].String, args[1]); return DynValue.Nil; });
        script.Globals["off"] = DynValue.NewCallback((ctx, args) => { LuaOff(args[0].String, args[1]); return DynValue.Nil; });
        script.Globals["intercept"] = DynValue.NewCallback((ctx, args) => { LuaRegisterInterceptor(args[0].String, args[1]); return DynValue.Nil; });
        script.Globals["unintercept"] = DynValue.NewCallback((ctx, args) => { LuaUnregisterInterceptor(args[0].String, args[1]); return DynValue.Nil; });
        script.Globals["emit"] = DynValue.NewCallback((ctx, args) => {
            var eventArgs = new DynValue[args.Count - 1];
            for (int i = 1; i < args.Count; i++) eventArgs[i - 1] = args[i];
            LuaEmit(args[0].String, eventArgs);
            return DynValue.Nil;
        });
        script.Globals["sleep"] = DynValue.NewCallback((ctx, args) => { LuaSleep((int)args[0].Number); return DynValue.Nil; });
        script.Globals["commands"] = DynValue.NewCallback((ctx, args) => DynValue.NewTable(LuaGetCommands()));
        script.Globals["has_command"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(DevConsole.HasCommand(args[0].String)));

        // Black market API
        script.Globals["blackmarket_stock"] = DynValue.NewCallback((ctx, args) => LuaBlackMarketStock(args[0].String));
        script.Globals["blackmarket_has"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(LuaBlackMarketHas(args[0].String)));

        // ═══════════════════════════════════════════════════════════════════
        //  Tactical SDK API
        // ═══════════════════════════════════════════════════════════════════

        // --- Actor Query API ---
        script.Globals["get_actors"] = DynValue.NewCallback((ctx, args) => LuaGetActors());
        script.Globals["get_player_actors"] = DynValue.NewCallback((ctx, args) => LuaGetPlayerActors());
        script.Globals["get_enemy_actors"] = DynValue.NewCallback((ctx, args) => LuaGetEnemyActors());
        script.Globals["find_actor"] = DynValue.NewCallback((ctx, args) => LuaFindActor(args[0].String));
        script.Globals["get_active_actor"] = DynValue.NewCallback((ctx, args) => LuaGetActiveActor());

        // --- Movement API ---
        script.Globals["move_to"] = DynValue.NewCallback((ctx, args) => LuaMoveTo(args[0], (int)args[1].Number, (int)args[2].Number));
        script.Globals["teleport"] = DynValue.NewCallback((ctx, args) => LuaTeleport(args[0], (int)args[1].Number, (int)args[2].Number));
        script.Globals["get_position"] = DynValue.NewCallback((ctx, args) => LuaGetPosition(args[0]));
        script.Globals["get_ap"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(LuaGetAP(args[0])));
        script.Globals["set_ap"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(LuaSetAP(args[0], (int)args[1].Number)));
        script.Globals["get_facing"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(LuaGetFacing(args[0])));
        script.Globals["set_facing"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(LuaSetFacing(args[0], (int)args[1].Number)));
        script.Globals["is_moving"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(LuaIsMoving(args[0])));

        // --- Combat API ---
        script.Globals["attack"] = DynValue.NewCallback((ctx, args) => LuaAttack(args[0], args[1]));
        script.Globals["use_ability"] = DynValue.NewCallback((ctx, args) => LuaUseAbility(args[0], args[1].String, args.Count > 2 ? args[2] : DynValue.Nil));
        script.Globals["get_skills"] = DynValue.NewCallback((ctx, args) => LuaGetSkills(args[0]));
        script.Globals["get_hp"] = DynValue.NewCallback((ctx, args) => LuaGetHP(args[0]));
        script.Globals["set_hp"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(LuaSetHP(args[0], (int)args[1].Number)));
        script.Globals["damage"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(LuaDamage(args[0], (int)args[1].Number)));
        script.Globals["heal"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(LuaHeal(args[0], (int)args[1].Number)));
        script.Globals["get_suppression"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(LuaGetSuppression(args[0])));
        script.Globals["set_suppression"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(LuaSetSuppression(args[0], (float)args[1].Number)));
        script.Globals["get_morale"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(LuaGetMorale(args[0])));
        script.Globals["set_morale"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(LuaSetMorale(args[0], (float)args[1].Number)));
        script.Globals["set_stunned"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(LuaSetStunned(args[0], args[1].Boolean)));
        script.Globals["get_combat_info"] = DynValue.NewCallback((ctx, args) => LuaGetCombatInfo(args[0]));

        // --- Tactical State API ---
        script.Globals["get_round"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(TacticalController.GetCurrentRound()));
        script.Globals["get_faction"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(TacticalController.GetCurrentFaction()));
        script.Globals["get_faction_name"] = DynValue.NewCallback((ctx, args) => DynValue.NewString(TacticalController.GetFactionName((FactionType)(int)args[0].Number)));
        script.Globals["is_player_turn"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(TacticalController.IsPlayerTurn()));
        script.Globals["is_paused"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(TacticalController.IsPaused()));
        script.Globals["pause"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(TacticalController.SetPaused(args.Count > 0 ? args[0].Boolean : true)));
        script.Globals["unpause"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(TacticalController.SetPaused(false)));
        script.Globals["toggle_pause"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(TacticalController.TogglePause()));
        script.Globals["end_turn"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(TacticalController.EndTurn()));
        script.Globals["next_round"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(TacticalController.NextRound()));
        script.Globals["next_faction"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(TacticalController.NextFaction()));
        script.Globals["get_time_scale"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(TacticalController.GetTimeScale()));
        script.Globals["set_time_scale"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(TacticalController.SetTimeScale((float)args[0].Number)));
        script.Globals["get_tactical_state"] = DynValue.NewCallback((ctx, args) => LuaGetTacticalState());
        script.Globals["is_mission_running"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(TacticalController.IsMissionRunning()));

        // --- TileMap API ---
        script.Globals["get_tile_info"] = DynValue.NewCallback((ctx, args) => LuaGetTileInfo((int)args[0].Number, (int)args[1].Number));
        script.Globals["get_cover"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(TileMap.GetCover((int)args[0].Number, (int)args[1].Number, (int)args[2].Number)));
        script.Globals["get_all_cover"] = DynValue.NewCallback((ctx, args) => LuaGetAllCover((int)args[0].Number, (int)args[1].Number));
        script.Globals["is_blocked"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(TileMap.IsBlocked((int)args[0].Number, (int)args[1].Number)));
        script.Globals["has_actor_at"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(TileMap.HasActor((int)args[0].Number, (int)args[1].Number)));
        script.Globals["is_visible"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(TileMap.IsVisibleToPlayer((int)args[0].Number, (int)args[1].Number)));
        script.Globals["get_map_info"] = DynValue.NewCallback((ctx, args) => LuaGetMapInfo());
        script.Globals["get_actor_at"] = DynValue.NewCallback((ctx, args) => LuaGetActorAt((int)args[0].Number, (int)args[1].Number));
        script.Globals["get_distance"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(TileMap.GetDistance((int)args[0].Number, (int)args[1].Number, (int)args[2].Number, (int)args[3].Number)));

        // --- Spawn API (experimental - may crash) ---
        script.Globals["spawn_unit"] = DynValue.NewCallback((ctx, args) => LuaSpawnUnit(args[0].String, (int)args[1].Number, (int)args[2].Number, args.Count > 3 ? (int)args[3].Number : 1));
        script.Globals["destroy_entity"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(LuaDestroyEntity(args[0], args.Count > 1 && args[1].Boolean)));
        script.Globals["clear_enemies"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(EntitySpawner.ClearEnemies(args.Count == 0 || args[0].Boolean)));
        script.Globals["list_entities"] = DynValue.NewCallback((ctx, args) => LuaListEntities(args.Count > 0 ? (int)args[0].Number : -1));
        script.Globals["get_entity_info"] = DynValue.NewCallback((ctx, args) => LuaGetEntityInfo(args[0]));

        // --- Tile Effects API ---
        script.Globals["get_tile_effects"] = DynValue.NewCallback((ctx, args) => LuaGetTileEffects((int)args[0].Number, (int)args[1].Number));
        script.Globals["has_effects"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(TileEffects.HasEffects((int)args[0].Number, (int)args[1].Number)));
        script.Globals["is_on_fire"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(TileEffects.IsOnFire((int)args[0].Number, (int)args[1].Number)));
        script.Globals["has_smoke"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(TileEffects.HasSmoke((int)args[0].Number, (int)args[1].Number)));
        script.Globals["spawn_effect"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(TileEffects.SpawnEffect((int)args[0].Number, (int)args[1].Number, args[2].String, args.Count > 3 ? (float)args[3].Number : 0f)));
        script.Globals["clear_tile_effects"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(TileEffects.ClearEffects((int)args[0].Number, (int)args[1].Number)));
        script.Globals["get_effect_templates"] = DynValue.NewCallback((ctx, args) => LuaGetEffectTemplates());

        // --- Inventory/Item API ---
        script.Globals["give_item"] = DynValue.NewCallback((ctx, args) => LuaGiveItem(args[0], args[1].String));
        script.Globals["get_inventory"] = DynValue.NewCallback((ctx, args) => LuaGetInventory(args[0]));
        script.Globals["get_equipped_weapons"] = DynValue.NewCallback((ctx, args) => LuaGetEquippedWeapons(args[0]));
        script.Globals["get_equipped_armor"] = DynValue.NewCallback((ctx, args) => LuaGetEquippedArmor(args[0]));
        script.Globals["get_item_templates"] = DynValue.NewCallback((ctx, args) => LuaGetItemTemplates(args.Count > 0 ? args[0].String : null));

        // --- Animation API ---
        SimpleAnimations.RegisterLuaHelpers(script);

        // --- Object Bindings API (Phase 3) ---
        // Registers Actor, Skill, Tile factory functions and effect system helpers
        LuaObjectBindings.RegisterApi(script);

        // --- Custom Maps API ---
        RegisterCustomMapsApi(script);
    }

    /// <summary>
    /// Register Custom Maps API as a 'maps' table with methods.
    /// </summary>
    private void RegisterCustomMapsApi(Script script)
    {
        var maps = new Table(script);

        // maps.list() - List all registered custom maps
        maps["list"] = DynValue.NewCallback((ctx, args) => LuaMapsList(script));

        // maps.get(id) - Get a map config by ID
        maps["get"] = DynValue.NewCallback((ctx, args) => LuaMapsGet(script, args[0].String));

        // maps.set_active(id_or_config) - Set active map override
        maps["set_active"] = DynValue.NewCallback((ctx, args) => LuaMapsSetActive(args[0]));

        // maps.clear_active() - Clear active override
        maps["clear_active"] = DynValue.NewCallback((ctx, args) => { LuaMapsClearActive(); return DynValue.Nil; });

        // maps.get_active() - Get current active map
        maps["get_active"] = DynValue.NewCallback((ctx, args) => LuaMapsGetActive(script));

        // maps.has_active() - Check if there's an active override
        maps["has_active"] = DynValue.NewCallback((ctx, args) => DynValue.NewBoolean(CustomMaps.CustomMapRegistry.HasActiveOverride()));

        // maps.load_directory(path) - Load maps from directory
        maps["load_directory"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(CustomMaps.CustomMapRegistry.LoadFromDirectory(args[0].String)));

        // maps.count() - Get count of registered maps
        maps["count"] = DynValue.NewCallback((ctx, args) => DynValue.NewNumber(CustomMaps.CustomMapRegistry.Count));

        // maps.create(id) - Create a new map builder
        maps["create"] = DynValue.NewCallback((ctx, args) => LuaMapsCreate(script, args[0].String));

        // maps.register(config_table) - Register a map from Lua table
        maps["register"] = DynValue.NewCallback((ctx, args) => LuaMapsRegister(args[0].Table));

        // maps.play_with_seed(seed) - Quick play with specific seed
        maps["play_with_seed"] = DynValue.NewCallback((ctx, args) => { CustomMaps.CustomMaps.PlayWithSeed((int)args[0].Number); return DynValue.Nil; });

        // maps.play_with_size(size) - Quick play with specific size
        maps["play_with_size"] = DynValue.NewCallback((ctx, args) => { CustomMaps.CustomMaps.PlayWithSize((int)args[0].Number); return DynValue.Nil; });

        // maps.play_with(seed, size) - Quick play with seed and size
        maps["play_with"] = DynValue.NewCallback((ctx, args) => { CustomMaps.CustomMaps.PlayWith((int)args[0].Number, (int)args[1].Number); return DynValue.Nil; });

        // === Zone Management ===

        // maps.add_zone({ id, name, type, x, y, width, height, priority })
        maps["add_zone"] = DynValue.NewCallback((ctx, args) => LuaMapsAddZone(args[0].Table));

        // maps.configure_zone(id, { generators = {...}, disabled = {...} })
        maps["configure_zone"] = DynValue.NewCallback((ctx, args) => LuaMapsConfigureZone(args[0].String, args[1].Table));

        // maps.remove_zone(id)
        maps["remove_zone"] = DynValue.NewCallback((ctx, args) => LuaMapsRemoveZone(args[0].String));

        // maps.get_zone_at(x, y)
        maps["get_zone_at"] = DynValue.NewCallback((ctx, args) => LuaMapsGetZoneAt(script, (int)args[0].Number, (int)args[1].Number));

        // === Tile/Terrain Painting ===

        // maps.set_tile(x, y, { terrain, height })
        maps["set_tile"] = DynValue.NewCallback((ctx, args) => LuaMapsSetTile((int)args[0].Number, (int)args[1].Number, args[2].Table));

        // maps.clear_tile(x, y)
        maps["clear_tile"] = DynValue.NewCallback((ctx, args) => LuaMapsClearTile((int)args[0].Number, (int)args[1].Number));

        // maps.get_tile(x, y)
        maps["get_tile"] = DynValue.NewCallback((ctx, args) => LuaMapsGetTile(script, (int)args[0].Number, (int)args[1].Number));

        // === Chunk Placement ===

        // maps.add_chunk({ x, y, template, rotation })
        maps["add_chunk"] = DynValue.NewCallback((ctx, args) => LuaMapsAddChunk(args[0].Table));

        // maps.remove_chunk(x, y)
        maps["remove_chunk"] = DynValue.NewCallback((ctx, args) => LuaMapsRemoveChunk((int)args[0].Number, (int)args[1].Number));

        // === Path Drawing ===

        // maps.add_path({ id, type, width, waypoints = {{x,y}, ...} })
        maps["add_path"] = DynValue.NewCallback((ctx, args) => LuaMapsAddPath(args[0].Table));

        // maps.remove_path(id)
        maps["remove_path"] = DynValue.NewCallback((ctx, args) => LuaMapsRemovePath(args[0].String));

        // === Utility ===

        // maps.stats() - Get tile/zone/path/chunk counts
        maps["stats"] = DynValue.NewCallback((ctx, args) =>
        {
            var activeConfig = CustomMaps.CustomMapRegistry.GetActiveOverride();
            var table = new Table(script);
            table["tiles"] = activeConfig?.Tiles.Count ?? 0;
            table["zones"] = activeConfig?.Zones.Count ?? 0;
            table["paths"] = activeConfig?.Paths.Count ?? 0;
            table["chunks"] = activeConfig?.Chunks.Count ?? 0;
            return DynValue.NewTable(table);
        });

        script.Globals["maps"] = maps;

        // --- Chunks API (for chunk template browsing) ---
        RegisterChunksApi(script);

        // --- Assets API (for prefab browsing) ---
        RegisterAssetsApi(script);
    }

    /// <summary>
    /// Register Chunks API as a 'chunks' table for browsing chunk templates.
    /// </summary>
    private void RegisterChunksApi(Script script)
    {
        var chunks = new Table(script);

        // chunks.list([query]) - List available chunk templates
        chunks["list"] = DynValue.NewCallback((ctx, args) =>
        {
            var table = new Table(script);
            var chunkList = args.Count > 0 && !args[0].IsNil()
                ? CustomMaps.ChunkBrowser.Search(args[0].String)
                : CustomMaps.ChunkBrowser.GetAll();

            for (int i = 0; i < chunkList.Count; i++)
            {
                var info = chunkList[i];
                var chunkTable = new Table(script);
                chunkTable["name"] = info.Name;
                chunkTable["width"] = info.Width;
                chunkTable["height"] = info.Height;
                chunkTable["type"] = info.TypeName;
                table[i + 1] = DynValue.NewTable(chunkTable);
            }
            return DynValue.NewTable(table);
        });

        // chunks.get(name) - Get detailed chunk info
        chunks["get"] = DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count == 0 || args[0].IsNil())
                return DynValue.Nil;

            var info = CustomMaps.ChunkBrowser.Get(args[0].String);
            if (info == null)
                return DynValue.Nil;

            var table = new Table(script);
            table["name"] = info.Name;
            table["width"] = info.Width;
            table["height"] = info.Height;
            table["type"] = info.TypeName;
            table["type_id"] = info.Type;
            table["spawn_mode"] = info.SpawnMode == 0 ? "block" : "scatter";
            table["max_spawns"] = info.MaxSpawns;
            table["fixed_children"] = info.FixedChildCount;
            table["random_children"] = info.RandomChildCount;
            table["fixed_prefabs"] = info.FixedPrefabCount;
            return DynValue.NewTable(table);
        });

        // chunks.exists(name) - Check if chunk exists
        chunks["exists"] = DynValue.NewCallback((ctx, args) =>
        {
            if (args.Count == 0 || args[0].IsNil())
                return DynValue.NewBoolean(false);
            return DynValue.NewBoolean(CustomMaps.ChunkBrowser.Exists(args[0].String));
        });

        // chunks.count() - Get total chunk count
        chunks["count"] = DynValue.NewCallback((ctx, args) =>
        {
            return DynValue.NewNumber(CustomMaps.ChunkBrowser.Count);
        });

        // chunks.refresh() - Refresh chunk cache
        chunks["refresh"] = DynValue.NewCallback((ctx, args) =>
        {
            CustomMaps.ChunkBrowser.RefreshCache();
            return DynValue.NewNumber(CustomMaps.ChunkBrowser.Count);
        });

        // chunks.by_size(min_width, min_height) - Filter by minimum size
        chunks["by_size"] = DynValue.NewCallback((ctx, args) =>
        {
            int minW = args.Count > 0 ? (int)args[0].Number : 1;
            int minH = args.Count > 1 ? (int)args[1].Number : 1;

            var table = new Table(script);
            var chunkList = CustomMaps.ChunkBrowser.GetByMinSize(minW, minH);

            for (int i = 0; i < chunkList.Count; i++)
            {
                var info = chunkList[i];
                var chunkTable = new Table(script);
                chunkTable["name"] = info.Name;
                chunkTable["width"] = info.Width;
                chunkTable["height"] = info.Height;
                chunkTable["type"] = info.TypeName;
                table[i + 1] = DynValue.NewTable(chunkTable);
            }
            return DynValue.NewTable(table);
        });

        script.Globals["chunks"] = chunks;
    }

    /// <summary>
    /// Register Assets API as an 'assets' table for browsing prefabs.
    /// </summary>
    private void RegisterAssetsApi(Script script)
    {
        var assets = new Table(script);

        // assets.categories() - List asset categories
        assets["categories"] = DynValue.NewCallback((ctx, args) => {
            var table = new Table(script);
            var cats = CustomMaps.AssetResolver.GetCategories();
            for (int i = 0; i < cats.Length; i++)
                table[i + 1] = cats[i];
            return DynValue.NewTable(table);
        });

        // assets.list(category?) - List prefabs in category (or all)
        assets["list"] = DynValue.NewCallback((ctx, args) => {
            var table = new Table(script);
            string[] prefabs;

            if (args.Count > 0 && !args[0].IsNil())
            {
                prefabs = CustomMaps.AssetResolver.GetPrefabsInCategory(args[0].String);
            }
            else
            {
                // Get all prefabs
                var all = new List<string>();
                foreach (var cat in CustomMaps.AssetResolver.GetCategories())
                    all.AddRange(CustomMaps.AssetResolver.GetPrefabsInCategory(cat));
                prefabs = all.ToArray();
            }

            for (int i = 0; i < prefabs.Length; i++)
                table[i + 1] = prefabs[i];
            return DynValue.NewTable(table);
        });

        // assets.search(pattern) - Search prefabs by name pattern
        assets["search"] = DynValue.NewCallback((ctx, args) => {
            var pattern = args.Count > 0 ? args[0].String : "";
            var results = CustomMaps.AssetResolver.SearchPrefabs(pattern);
            var table = new Table(script);
            for (int i = 0; i < results.Length; i++)
                table[i + 1] = results[i];
            return DynValue.NewTable(table);
        });

        // assets.rebuild() - Rebuild the asset catalog
        assets["rebuild"] = DynValue.NewCallback((ctx, args) => {
            CustomMaps.AssetResolver.ClearCache();
            CustomMaps.AssetResolver.BuildAssetCatalog();
            return DynValue.Nil;
        });

        // assets.count(category?) - Count prefabs
        assets["count"] = DynValue.NewCallback((ctx, args) => {
            if (args.Count > 0 && !args[0].IsNil())
            {
                return DynValue.NewNumber(CustomMaps.AssetResolver.GetPrefabsInCategory(args[0].String).Length);
            }
            else
            {
                int total = 0;
                foreach (var cat in CustomMaps.AssetResolver.GetCategories())
                    total += CustomMaps.AssetResolver.GetPrefabsInCategory(cat).Length;
                return DynValue.NewNumber(total);
            }
        });

        script.Globals["assets"] = assets;

        // ============================================================
        // visuals.* - Character visual override system
        // ============================================================
        var visuals = new Table(script);

        // visuals.override_glb(prefab_name, glb_asset) - Register override from GLB
        visuals["override_glb"] = DynValue.NewCallback((ctx, args) => {
            if (args.Count < 2)
                return DynValue.NewString("Usage: visuals.override_glb(prefab_name, glb_asset)");

            var prefabName = args[0].String;
            var glbAsset = args[1].String;

            CharacterVisuals.RegisterOverrideFromGlb(prefabName, glbAsset);
            return DynValue.NewString($"Registered visual override for '{prefabName}'");
        });

        // visuals.override(prefab_name, mesh_mappings, material_mappings) - Register override with mappings
        visuals["override"] = DynValue.NewCallback((ctx, args) => {
            if (args.Count < 1)
                return DynValue.NewString("Usage: visuals.override(prefab_name, {mesh_mappings}, {material_mappings})");

            var prefabName = args[0].String;
            var meshMappings = new Dictionary<string, UnityEngine.Mesh>();
            var matMappings = new Dictionary<string, UnityEngine.Material>();

            // Parse mesh mappings from table
            if (args.Count > 1 && args[1].Type == DataType.Table)
            {
                foreach (var pair in args[1].Table.Pairs)
                {
                    var childName = pair.Key.String;
                    var meshName = pair.Value.String;
                    var mesh = Menace.ModpackLoader.BundleLoader.GetAsset<UnityEngine.Mesh>(meshName);
                    if (mesh != null)
                        meshMappings[childName] = mesh;
                }
            }

            // Parse material mappings from table
            if (args.Count > 2 && args[2].Type == DataType.Table)
            {
                foreach (var pair in args[2].Table.Pairs)
                {
                    var matName = pair.Key.String;
                    var replacementName = pair.Value.String;
                    var mat = Menace.ModpackLoader.BundleLoader.GetAsset<UnityEngine.Material>(replacementName);
                    if (mat != null)
                        matMappings[matName] = mat;
                }
            }

            CharacterVisuals.RegisterOverride(prefabName, meshMappings, matMappings);
            return DynValue.NewString($"Registered visual override for '{prefabName}'");
        });

        // visuals.apply_existing() - Apply overrides to already-spawned entities
        visuals["apply_existing"] = DynValue.NewCallback((ctx, args) => {
            var count = CharacterVisuals.ApplyToExistingEntities();
            return DynValue.NewString($"Applied overrides to {count} entities");
        });

        // visuals.clear() - Clear all registered overrides
        visuals["clear"] = DynValue.NewCallback((ctx, args) => {
            CharacterVisuals.ClearOverrides();
            return DynValue.NewString("Cleared all visual overrides");
        });

        // visuals.list() - List registered overrides
        visuals["list"] = DynValue.NewCallback((ctx, args) => {
            var overrides = CharacterVisuals.GetRegisteredOverrides();
            var table = new Table(script);
            for (int i = 0; i < overrides.Length; i++)
                table[i + 1] = overrides[i];
            return DynValue.NewTable(table);
        });

        script.Globals["visuals"] = visuals;
    }

    /// <summary>
    /// Initialize the Lua engine with a logger.
    /// </summary>
    public void Initialize(MelonLogger.Instance logger)
    {
        // Logger parameter kept for API compatibility but we use SdkLogger for dual output
        SdkLogger.Msg("[LuaEngine] Initialized");

        // Register console commands for Lua
        DevConsole.RegisterCommand("lua", "<code>", "Execute Lua code", args =>
        {
            if (args.Length == 0) return "Usage: lua <code>";
            var code = string.Join(" ", args);
            return ExecuteString(code);
        });

        DevConsole.RegisterCommand("luafile", "<path>", "Execute Lua file", args =>
        {
            if (args.Length == 0) return "Usage: luafile <path>";
            return ExecuteFile(args[0]);
        });

        DevConsole.RegisterCommand("luaevents", "", "List registered Lua event handlers", _ =>
        {
            var lines = new List<string> { "Lua Event Handlers:" };
            foreach (var kvp in _eventHandlers.Where(k => k.Value.Count > 0))
            {
                lines.Add($"  {kvp.Key}: {kvp.Value.Count} handler(s)");
            }
            return lines.Count > 1 ? string.Join("\n", lines) : "No event handlers registered";
        });

        DevConsole.RegisterCommand("luascripts", "", "List loaded Lua scripts", _ =>
        {
            if (_loadedScripts.Count == 0) return "No Lua scripts loaded";
            var lines = new List<string> { "Loaded Lua Scripts:" };
            foreach (var (modId, path, _) in _loadedScripts)
            {
                lines.Add($"  [{modId}] {Path.GetFileName(path)}");
            }
            return string.Join("\n", lines);
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Lua API Functions
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// cmd("command args") - Execute console command from Lua.
    /// Returns a table with { success = bool, result = string }
    /// </summary>
    private DynValue LuaCmd(string input)
    {
        var (success, result) = DevConsole.ExecuteCommandWithResult(input);

        // Return as Lua table for easy access
        var table = new Table(_lua);
        table["success"] = success;
        table["result"] = result;

        // Also try to parse structured data for certain commands
        table["data"] = TryParseCommandResult(input, result);

        return DynValue.NewTable(table);
    }

    /// <summary>
    /// Attempt to parse command results into structured Lua tables.
    /// </summary>
    private DynValue TryParseCommandResult(string command, string result)
    {
        // For now, return nil - can be enhanced to parse specific command outputs
        // into Lua tables (e.g., roster command → table of leaders)
        return DynValue.Nil;
    }

    private void LuaLog(string message)
    {
        SdkLogger.Msg($"[Lua] {message}");
    }

    private void LuaWarn(string message)
    {
        SdkLogger.Warning($"[Lua] {message}");
    }

    private void LuaError(string message)
    {
        SdkLogger.Error($"[Lua] {message}");
    }

    /// <summary>
    /// on("event", callback) - Register event handler.
    /// </summary>
    private void LuaOn(string eventName, DynValue callback)
    {
        if (!ValidEvents.Contains(eventName))
        {
            LuaWarn($"Unknown event: {eventName}. Valid events: {string.Join(", ", ValidEvents)}");
            return;
        }

        if (callback.Type != DataType.Function)
        {
            LuaError($"on() requires a function callback, got {callback.Type}");
            return;
        }

        // Store with owning script to call from correct context
        var ownerScript = callback.Function?.OwnerScript ?? _lua;
        _eventHandlers[eventName].Add((ownerScript, callback));
    }

    /// <summary>
    /// off("event", callback) - Unregister event handler.
    /// </summary>
    private void LuaOff(string eventName, DynValue callback)
    {
        if (_eventHandlers.TryGetValue(eventName, out var handlers))
        {
            handlers.RemoveAll(h => h.Handler.Equals(callback));
        }
    }

    /// <summary>
    /// intercept("property", callback) - Register interceptor that can modify values.
    /// Callback receives (owner_ptr, value) and should return modified value.
    /// Valid names match C# Intercept events (e.g., "concealment", "damage", "accuracy").
    /// </summary>
    private void LuaRegisterInterceptor(string name, DynValue callback)
    {
        if (string.IsNullOrEmpty(name))
        {
            LuaError("intercept() requires an interceptor name");
            return;
        }

        if (callback.Type != DataType.Function)
        {
            LuaError($"intercept() requires a function callback, got {callback.Type}");
            return;
        }

        // Create list if it doesn't exist - allows any interceptor name
        if (!_interceptors.ContainsKey(name))
            _interceptors[name] = new List<(Script, DynValue)>();

        var ownerScript = callback.Function?.OwnerScript ?? _lua;
        _interceptors[name].Add((ownerScript, callback));
        SdkLogger.Msg($"[Lua] Registered '{name}' interceptor");
    }

    /// <summary>
    /// unintercept("property", callback) - Unregister interceptor.
    /// </summary>
    private void LuaUnregisterInterceptor(string name, DynValue callback)
    {
        if (_interceptors.TryGetValue(name, out var handlers))
        {
            handlers.RemoveAll(h => h.Handler.Equals(callback));
        }
    }

    /// <summary>
    /// Invoke Lua interceptors for integer properties. Called from Intercept.cs.
    /// Returns the final modified value after all interceptors run.
    /// </summary>
    public int InvokeLuaInterceptors(string name, long ownerPtr, int value)
    {
        if (!_interceptors.TryGetValue(name, out var handlers) || handlers.Count == 0)
            return value;

        var result = value;
        foreach (var (ownerScript, handler) in handlers.ToList())
        {
            try
            {
                var luaResult = ownerScript.Call(handler, DynValue.NewNumber(ownerPtr), DynValue.NewNumber(result));
                if (luaResult.Type == DataType.Number)
                {
                    result = (int)luaResult.Number;
                }
            }
            catch (Exception ex)
            {
                ModError.WarnInternal("LuaScriptEngine", $"Interceptor '{name}' failed: {ex.Message}");
            }
        }
        return result;
    }

    /// <summary>
    /// emit("event", args...) - Fire an event (for testing/custom events).
    /// </summary>
    private void LuaEmit(string eventName, params DynValue[] args)
    {
        FireEvent(eventName, args);
    }

    /// <summary>
    /// sleep(frames) - Yield for N frames (only works in coroutines).
    /// </summary>
    private void LuaSleep(int frames)
    {
        // Note: This is a placeholder. True async sleep would require coroutine support.
        SdkLogger.Msg($"[Lua] sleep({frames}) called - async sleep not yet implemented");
    }

    /// <summary>
    /// commands() - Get list of all available console commands.
    /// </summary>
    private Table LuaGetCommands()
    {
        var table = new Table(_lua);
        int i = 1;
        foreach (var name in DevConsole.GetCommandNames().OrderBy(n => n))
        {
            table[i++] = name;
        }
        return table;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Black Market API Functions
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// blackmarket_stock(templateName) - Add an item to the black market.
    /// Returns { success = bool, message = string }
    /// </summary>
    private DynValue LuaBlackMarketStock(string templateName)
    {
        var table = new Table(_lua);
        try
        {
            var result = BlackMarket.StockItemInBlackMarket(templateName);
            var success = result.StartsWith("Stocked");
            table["success"] = success;
            table["message"] = result;
            if (success)
                SdkLogger.Msg($"[Lua] Added {templateName} to black market");
            else
                SdkLogger.Warning($"[Lua] Failed to add {templateName} to black market: {result}");
        }
        catch (Exception ex)
        {
            table["success"] = false;
            table["message"] = ex.Message;
        }
        return DynValue.NewTable(table);
    }

    /// <summary>
    /// blackmarket_has(templateName) - Check if an item exists in the black market.
    /// </summary>
    private bool LuaBlackMarketHas(string templateName)
    {
        try
        {
            return BlackMarket.HasTemplate(templateName);
        }
        catch
        {
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Tactical SDK API Implementations
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Convert a Lua value (number/table with ptr) to GameObj.
    /// </summary>
    private GameObj LuaToGameObj(DynValue val)
    {
        if (val.IsNil()) return GameObj.Null;

        // Handle number (pointer as int64)
        if (val.Type == DataType.Number)
        {
            return new GameObj(new IntPtr((long)val.Number));
        }

        // Handle table with ptr field
        if (val.Type == DataType.Table)
        {
            var ptrVal = val.Table.Get("ptr");
            if (!ptrVal.IsNil() && ptrVal.Type == DataType.Number)
                return new GameObj(new IntPtr((long)ptrVal.Number));
        }

        return GameObj.Null;
    }

    /// <summary>
    /// Convert GameObj to Lua actor table with useful info.
    /// </summary>
    private DynValue GameObjToLuaActor(GameObj obj)
    {
        if (obj.IsNull) return DynValue.Nil;

        var table = new Table(_lua);
        table["ptr"] = obj.Pointer.ToInt64();
        table["name"] = obj.GetName() ?? "<unnamed>";
        table["alive"] = obj.IsAlive;

        var pos = EntityMovement.GetPosition(obj);
        if (pos.HasValue)
        {
            table["x"] = pos.Value.x;
            table["y"] = pos.Value.y;
        }

        return DynValue.NewTable(table);
    }

    // --- Actor Query ---

    private DynValue LuaGetActors()
    {
        try
        {
            var actors = GameQuery.FindAll("Menace.Tactical.Actor");
            var table = new Table(_lua);
            int i = 1;
            foreach (var actor in actors)
            {
                if (!actor.IsNull)
                    table[i++] = GameObjToLuaActor(actor);
            }
            return DynValue.NewTable(table);
        }
        catch (Exception ex)
        {
            LuaError($"get_actors failed: {ex.Message}");
            return DynValue.NewTable(new Table(_lua));
        }
    }

    private DynValue LuaGetPlayerActors()
    {
        try
        {
            var actors = GameQuery.FindAll("Menace.Tactical.Actor");
            var table = new Table(_lua);
            int i = 1;
            foreach (var actor in actors)
            {
                if (actor.IsNull || !actor.IsAlive) continue;
                // Check if player faction (1 or 2)
                var factionId = actor.ReadInt(0xBC); // OFFSET_ACTOR_FACTION_ID
                if (factionId == 1 || factionId == 2)
                    table[i++] = GameObjToLuaActor(actor);
            }
            return DynValue.NewTable(table);
        }
        catch (Exception ex)
        {
            LuaError($"get_player_actors failed: {ex.Message}");
            return DynValue.NewTable(new Table(_lua));
        }
    }

    private DynValue LuaGetEnemyActors()
    {
        try
        {
            var actors = GameQuery.FindAll("Menace.Tactical.Actor");
            var table = new Table(_lua);
            int i = 1;
            foreach (var actor in actors)
            {
                if (actor.IsNull || !actor.IsAlive) continue;
                // Check if enemy faction (not 0, 1, 2, 3)
                var factionId = actor.ReadInt(0xBC);
                if (factionId > 3) // EnemyLocalForces, Pirates, etc.
                    table[i++] = GameObjToLuaActor(actor);
            }
            return DynValue.NewTable(table);
        }
        catch (Exception ex)
        {
            LuaError($"get_enemy_actors failed: {ex.Message}");
            return DynValue.NewTable(new Table(_lua));
        }
    }

    private DynValue LuaFindActor(string name)
    {
        try
        {
            var actor = GameQuery.FindByName("Menace.Tactical.Actor", name);
            return GameObjToLuaActor(actor);
        }
        catch (Exception ex)
        {
            LuaError($"find_actor failed: {ex.Message}");
            return DynValue.Nil;
        }
    }

    private DynValue LuaGetActiveActor()
    {
        try
        {
            var actor = TacticalController.GetActiveActor();
            return GameObjToLuaActor(actor);
        }
        catch (Exception ex)
        {
            LuaError($"get_active_actor failed: {ex.Message}");
            return DynValue.Nil;
        }
    }

    // --- Movement ---

    private DynValue LuaMoveTo(DynValue actorVal, int x, int y)
    {
        var actor = LuaToGameObj(actorVal);
        var result = EntityMovement.MoveTo(actor, x, y);

        var table = new Table(_lua);
        table["success"] = result.Success;
        table["error"] = result.Error ?? "";
        return DynValue.NewTable(table);
    }

    private DynValue LuaTeleport(DynValue actorVal, int x, int y)
    {
        var actor = LuaToGameObj(actorVal);
        var result = EntityMovement.Teleport(actor, x, y);

        var table = new Table(_lua);
        table["success"] = result.Success;
        table["error"] = result.Error ?? "";
        return DynValue.NewTable(table);
    }

    private DynValue LuaGetPosition(DynValue actorVal)
    {
        var actor = LuaToGameObj(actorVal);
        var pos = EntityMovement.GetPosition(actor);

        if (!pos.HasValue) return DynValue.Nil;

        var table = new Table(_lua);
        table["x"] = pos.Value.x;
        table["y"] = pos.Value.y;
        return DynValue.NewTable(table);
    }

    private int LuaGetAP(DynValue actorVal)
    {
        var actor = LuaToGameObj(actorVal);
        return EntityMovement.GetRemainingAP(actor);
    }

    private bool LuaSetAP(DynValue actorVal, int ap)
    {
        var actor = LuaToGameObj(actorVal);
        return EntityMovement.SetAP(actor, ap);
    }

    private int LuaGetFacing(DynValue actorVal)
    {
        var actor = LuaToGameObj(actorVal);
        return EntityMovement.GetFacing(actor);
    }

    private bool LuaSetFacing(DynValue actorVal, int direction)
    {
        var actor = LuaToGameObj(actorVal);
        return EntityMovement.SetFacing(actor, direction);
    }

    private bool LuaIsMoving(DynValue actorVal)
    {
        var actor = LuaToGameObj(actorVal);
        return EntityMovement.IsMoving(actor);
    }

    // --- Combat ---

    private DynValue LuaAttack(DynValue attackerVal, DynValue targetVal)
    {
        var attacker = LuaToGameObj(attackerVal);
        var target = LuaToGameObj(targetVal);
        var result = EntityCombat.Attack(attacker, target);

        var table = new Table(_lua);
        table["success"] = result.Success;
        table["error"] = result.Error ?? "";
        table["skill"] = result.SkillUsed ?? "";
        table["damage"] = result.Damage;
        return DynValue.NewTable(table);
    }

    private DynValue LuaUseAbility(DynValue actorVal, string skillName, DynValue targetVal)
    {
        var actor = LuaToGameObj(actorVal);
        var target = targetVal.IsNil() ? GameObj.Null : LuaToGameObj(targetVal);
        var result = EntityCombat.UseAbility(actor, skillName, target);

        var table = new Table(_lua);
        table["success"] = result.Success;
        table["error"] = result.Error ?? "";
        table["skill"] = result.SkillUsed ?? "";
        return DynValue.NewTable(table);
    }

    private DynValue LuaGetSkills(DynValue actorVal)
    {
        try
        {
            var actor = LuaToGameObj(actorVal);
            var skills = EntityCombat.GetSkills(actor);

            var table = new Table(_lua);
            int i = 1;
            foreach (var skill in skills)
            {
                var skillTable = new Table(_lua);
                skillTable["name"] = skill.Name ?? "";
                skillTable["display_name"] = skill.DisplayName ?? skill.Name ?? "";
                skillTable["can_use"] = skill.CanUse;
                skillTable["ap_cost"] = skill.APCost;
                skillTable["range"] = skill.Range;
                skillTable["cooldown"] = skill.Cooldown;
                skillTable["current_cooldown"] = skill.CurrentCooldown;
                skillTable["is_attack"] = skill.IsAttack;
                skillTable["is_passive"] = skill.IsPassive;
                table[i++] = skillTable;
            }
            return DynValue.NewTable(table);
        }
        catch (Exception ex)
        {
            LuaError($"get_skills failed: {ex.Message}");
            return DynValue.NewTable(new Table(_lua));
        }
    }

    private DynValue LuaGetHP(DynValue actorVal)
    {
        var actor = LuaToGameObj(actorVal);
        if (actor.IsNull) return DynValue.Nil;

        var info = EntityCombat.GetCombatInfo(actor);
        if (info == null) return DynValue.Nil;

        var table = new Table(_lua);
        table["current"] = info.CurrentHP;
        table["max"] = info.MaxHP;
        table["percent"] = info.HPPercent;
        return DynValue.NewTable(table);
    }

    private bool LuaSetHP(DynValue actorVal, int hp)
    {
        var actor = LuaToGameObj(actorVal);
        if (actor.IsNull) return false;

        // Get max HP to ensure we don't exceed it
        var info = EntityCombat.GetCombatInfo(actor);
        if (info == null) return false;

        var clampedHP = Math.Clamp(hp, 0, info.MaxHP);
        var diff = clampedHP - info.CurrentHP;
        if (diff > 0)
            return EntityCombat.Heal(actor, diff);
        else if (diff < 0)
            return EntityCombat.ApplyDamage(actor, -diff);
        return true;
    }

    private bool LuaDamage(DynValue actorVal, int amount)
    {
        var actor = LuaToGameObj(actorVal);
        return EntityCombat.ApplyDamage(actor, amount);
    }

    private bool LuaHeal(DynValue actorVal, int amount)
    {
        var actor = LuaToGameObj(actorVal);
        return EntityCombat.Heal(actor, amount);
    }

    private float LuaGetSuppression(DynValue actorVal)
    {
        var actor = LuaToGameObj(actorVal);
        return EntityCombat.GetSuppression(actor);
    }

    private bool LuaSetSuppression(DynValue actorVal, float value)
    {
        var actor = LuaToGameObj(actorVal);
        return EntityCombat.SetSuppression(actor, value);
    }

    private float LuaGetMorale(DynValue actorVal)
    {
        var actor = LuaToGameObj(actorVal);
        return EntityCombat.GetMorale(actor);
    }

    private bool LuaSetMorale(DynValue actorVal, float value)
    {
        var actor = LuaToGameObj(actorVal);
        return EntityCombat.SetMorale(actor, value);
    }

    private bool LuaSetStunned(DynValue actorVal, bool stunned)
    {
        var actor = LuaToGameObj(actorVal);
        return EntityCombat.SetStunned(actor, stunned);
    }

    private DynValue LuaGetCombatInfo(DynValue actorVal)
    {
        var actor = LuaToGameObj(actorVal);
        var info = EntityCombat.GetCombatInfo(actor);
        if (info == null) return DynValue.Nil;

        var table = new Table(_lua);
        table["hp"] = info.CurrentHP;
        table["max_hp"] = info.MaxHP;
        table["hp_percent"] = info.HPPercent;
        table["alive"] = info.IsAlive;
        table["suppression"] = info.Suppression;
        table["suppression_state"] = info.SuppressionState;
        table["morale"] = info.Morale;
        table["turn_done"] = info.IsTurnDone;
        table["stunned"] = info.IsStunned;
        table["has_acted"] = info.HasActed;
        table["times_attacked"] = info.TimesAttackedThisTurn;
        table["ap"] = info.CurrentAP;
        return DynValue.NewTable(table);
    }

    // --- Tactical State ---

    private DynValue LuaGetTacticalState()
    {
        var state = TacticalController.GetTacticalState();
        if (state == null) return DynValue.Nil;

        var table = new Table(_lua);
        table["round"] = state.RoundNumber;
        table["faction"] = state.CurrentFaction;
        table["faction_name"] = state.CurrentFactionName;
        table["is_player_turn"] = state.IsPlayerTurn;
        table["is_paused"] = state.IsPaused;
        table["time_scale"] = state.TimeScale;
        table["mission_running"] = state.IsMissionRunning;
        table["active_actor"] = state.ActiveActorName ?? "";
        table["any_player_alive"] = state.IsAnyPlayerAlive;
        table["any_enemy_alive"] = state.IsAnyEnemyAlive;
        table["total_enemies"] = state.TotalEnemyCount;
        table["dead_enemies"] = state.DeadEnemyCount;
        table["alive_enemies"] = state.AliveEnemyCount;
        return DynValue.NewTable(table);
    }

    // --- TileMap ---

    private DynValue LuaGetTileInfo(int x, int z)
    {
        var info = TileMap.GetTileInfo(x, z);
        if (info == null) return DynValue.Nil;

        var table = new Table(_lua);
        table["x"] = info.X;
        table["z"] = info.Z;
        table["elevation"] = info.Elevation;
        table["blocked"] = info.IsBlocked;
        table["has_actor"] = info.HasActor;
        table["actor_name"] = info.ActorName ?? "";
        table["visible"] = info.IsVisibleToPlayer;
        table["blocks_los"] = info.BlocksLOS;
        table["has_effects"] = info.HasEffects;
        return DynValue.NewTable(table);
    }

    private DynValue LuaGetAllCover(int x, int z)
    {
        var cover = TileMap.GetAllCover(x, z);
        var table = new Table(_lua);
        for (int i = 0; i < 8; i++)
        {
            table[i] = cover[i];
        }
        // Also add named directions
        table["north"] = cover[0];
        table["northeast"] = cover[1];
        table["east"] = cover[2];
        table["southeast"] = cover[3];
        table["south"] = cover[4];
        table["southwest"] = cover[5];
        table["west"] = cover[6];
        table["northwest"] = cover[7];
        return DynValue.NewTable(table);
    }

    private DynValue LuaGetMapInfo()
    {
        var info = TileMap.GetMapInfo();
        if (info == null) return DynValue.Nil;

        var table = new Table(_lua);
        table["width"] = info.Width;
        table["height"] = info.Height;
        table["fog_of_war"] = info.UseFogOfWar;
        return DynValue.NewTable(table);
    }

    private DynValue LuaGetActorAt(int x, int z)
    {
        var actor = TileMap.GetActorOnTile(x, z);
        return GameObjToLuaActor(actor);
    }

    // --- Spawn (experimental) ---

    private DynValue LuaSpawnUnit(string templateName, int x, int y, int faction)
    {
        try
        {
            var result = EntitySpawner.SpawnUnit(templateName, x, y, faction);

            var table = new Table(_lua);
            table["success"] = result.Success;
            table["error"] = result.Error ?? "";
            if (result.Success && !result.Entity.IsNull)
            {
                table["entity"] = GameObjToLuaActor(result.Entity);
            }
            return DynValue.NewTable(table);
        }
        catch (Exception ex)
        {
            LuaError($"spawn_unit failed: {ex.Message}");
            var table = new Table(_lua);
            table["success"] = false;
            table["error"] = ex.Message;
            return DynValue.NewTable(table);
        }
    }

    private bool LuaDestroyEntity(DynValue actorVal, bool immediate)
    {
        var actor = LuaToGameObj(actorVal);
        return EntitySpawner.DestroyEntity(actor, immediate);
    }

    private DynValue LuaListEntities(int factionFilter)
    {
        try
        {
            var entities = EntitySpawner.ListEntities(factionFilter);
            var table = new Table(_lua);
            int i = 1;
            foreach (var entity in entities)
            {
                if (!entity.IsNull)
                    table[i++] = GameObjToLuaActor(entity);
            }
            return DynValue.NewTable(table);
        }
        catch (Exception ex)
        {
            LuaError($"list_entities failed: {ex.Message}");
            return DynValue.NewTable(new Table(_lua));
        }
    }

    private DynValue LuaGetEntityInfo(DynValue actorVal)
    {
        var actor = LuaToGameObj(actorVal);
        var info = EntitySpawner.GetEntityInfo(actor);
        if (info == null) return DynValue.Nil;

        var table = new Table(_lua);
        table["entity_id"] = info.EntityId;
        table["name"] = info.Name ?? "";
        table["type_name"] = info.TypeName ?? "";
        table["faction"] = info.FactionIndex;
        table["alive"] = info.IsAlive;
        table["ptr"] = info.Pointer.ToInt64();
        return DynValue.NewTable(table);
    }

    // --- Tile Effects ---

    private DynValue LuaGetTileEffects(int x, int z)
    {
        try
        {
            var effects = TileEffects.GetEffects(x, z);
            var table = new Table(_lua);
            int i = 1;
            foreach (var effect in effects)
            {
                var effectTable = new Table(_lua);
                effectTable["type"] = effect.TypeName ?? "";
                effectTable["template"] = effect.TemplateName ?? "";
                effectTable["duration"] = effect.Duration;
                effectTable["rounds_left"] = effect.RoundsRemaining;
                effectTable["blocks_los"] = effect.BlocksLOS;
                table[i++] = effectTable;
            }
            return DynValue.NewTable(table);
        }
        catch (Exception ex)
        {
            LuaError($"get_tile_effects failed: {ex.Message}");
            return DynValue.NewTable(new Table(_lua));
        }
    }

    private DynValue LuaGetEffectTemplates()
    {
        try
        {
            var templates = TileEffects.GetAvailableEffectTemplates();
            var table = new Table(_lua);
            int i = 1;
            foreach (var name in templates)
            {
                table[i++] = name;
            }
            return DynValue.NewTable(table);
        }
        catch (Exception ex)
        {
            LuaError($"get_effect_templates failed: {ex.Message}");
            return DynValue.NewTable(new Table(_lua));
        }
    }

    // --- Inventory ---

    private DynValue LuaGiveItem(DynValue actorVal, string templateName)
    {
        try
        {
            // If no actor specified, use active actor
            GameObj actor;
            if (actorVal.IsNil())
            {
                actor = TacticalController.GetActiveActor();
            }
            else
            {
                actor = LuaToGameObj(actorVal);
            }

            if (actor.IsNull)
            {
                var table = new Table(_lua);
                table["success"] = false;
                table["error"] = "No actor selected";
                return DynValue.NewTable(table);
            }

            var result = Inventory.GiveItemToActor(templateName);
            var resultTable = new Table(_lua);
            resultTable["success"] = !result.StartsWith("Error") && !result.StartsWith("No") && !result.StartsWith("Template") && !result.StartsWith("Failed") && !result.StartsWith("Could not");
            resultTable["message"] = result;
            return DynValue.NewTable(resultTable);
        }
        catch (Exception ex)
        {
            LuaError($"give_item failed: {ex.Message}");
            var table = new Table(_lua);
            table["success"] = false;
            table["error"] = ex.Message;
            return DynValue.NewTable(table);
        }
    }

    private DynValue LuaGetInventory(DynValue actorVal)
    {
        try
        {
            var actor = LuaToGameObj(actorVal);
            if (actor.IsNull)
            {
                actor = TacticalController.GetActiveActor();
            }
            if (actor.IsNull) return DynValue.Nil;

            var container = Inventory.GetContainer(actor);
            if (container.IsNull) return DynValue.Nil;

            var items = Inventory.GetAllItems(container);
            var table = new Table(_lua);
            int i = 1;
            foreach (var item in items)
            {
                var itemTable = new Table(_lua);
                itemTable["name"] = item.TemplateName ?? "";
                itemTable["slot"] = item.SlotTypeName ?? "";
                itemTable["slot_id"] = item.SlotType;
                itemTable["value"] = item.TradeValue;
                itemTable["rarity"] = item.Rarity ?? "Common";
                itemTable["skills"] = item.SkillCount;
                itemTable["temporary"] = item.IsTemporary;
                table[i++] = itemTable;
            }
            return DynValue.NewTable(table);
        }
        catch (Exception ex)
        {
            LuaError($"get_inventory failed: {ex.Message}");
            return DynValue.NewTable(new Table(_lua));
        }
    }

    private DynValue LuaGetEquippedWeapons(DynValue actorVal)
    {
        try
        {
            var actor = LuaToGameObj(actorVal);
            if (actor.IsNull)
            {
                actor = TacticalController.GetActiveActor();
            }
            if (actor.IsNull) return DynValue.Nil;

            var weapons = Inventory.GetEquippedWeapons(actor);
            var table = new Table(_lua);
            int i = 1;
            foreach (var weapon in weapons)
            {
                var weaponTable = new Table(_lua);
                weaponTable["name"] = weapon.TemplateName ?? "";
                weaponTable["slot"] = weapon.SlotTypeName ?? "";
                weaponTable["value"] = weapon.TradeValue;
                weaponTable["rarity"] = weapon.Rarity ?? "Common";
                weaponTable["skills"] = weapon.SkillCount;
                table[i++] = weaponTable;
            }
            return DynValue.NewTable(table);
        }
        catch (Exception ex)
        {
            LuaError($"get_equipped_weapons failed: {ex.Message}");
            return DynValue.NewTable(new Table(_lua));
        }
    }

    private DynValue LuaGetEquippedArmor(DynValue actorVal)
    {
        try
        {
            var actor = LuaToGameObj(actorVal);
            if (actor.IsNull)
            {
                actor = TacticalController.GetActiveActor();
            }
            if (actor.IsNull) return DynValue.Nil;

            var armor = Inventory.GetEquippedArmor(actor);
            if (armor == null) return DynValue.Nil;

            var table = new Table(_lua);
            table["name"] = armor.TemplateName ?? "";
            table["value"] = armor.TradeValue;
            table["rarity"] = armor.Rarity ?? "Common";
            table["skills"] = armor.SkillCount;
            return DynValue.NewTable(table);
        }
        catch (Exception ex)
        {
            LuaError($"get_equipped_armor failed: {ex.Message}");
            return DynValue.Nil;
        }
    }

    private DynValue LuaGetItemTemplates(string filter)
    {
        try
        {
            var templates = Inventory.GetItemTemplates(filter);
            var table = new Table(_lua);
            int i = 1;
            foreach (var name in templates)
            {
                table[i++] = name;
            }
            return DynValue.NewTable(table);
        }
        catch (Exception ex)
        {
            LuaError($"get_item_templates failed: {ex.Message}");
            return DynValue.NewTable(new Table(_lua));
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Custom Maps API Implementations
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// maps.list() - List all registered custom maps.
    /// Returns array of {id, name, author, seed, size, layers, active}
    /// </summary>
    private DynValue LuaMapsList(Script script)
    {
        try
        {
            var maps = CustomMaps.CustomMapRegistry.GetAll();
            var activeId = CustomMaps.CustomMapRegistry.GetActiveOverride()?.Id;
            var table = new Table(script);
            int i = 1;

            foreach (var map in maps)
            {
                var mapTable = new Table(script);
                mapTable["id"] = map.Id ?? "";
                mapTable["name"] = map.Name ?? "";
                mapTable["author"] = map.Author ?? "";
                mapTable["seed"] = map.Seed.HasValue ? DynValue.NewNumber(map.Seed.Value) : DynValue.Nil;
                mapTable["size"] = map.MapSize.HasValue ? DynValue.NewNumber(map.MapSize.Value) : DynValue.Nil;
                mapTable["weight"] = map.Weight;
                mapTable["active"] = map.Id == activeId;

                var layersTable = new Table(script);
                int j = 1;
                foreach (var layer in map.Layers)
                    layersTable[j++] = layer;
                mapTable["layers"] = layersTable;

                table[i++] = mapTable;
            }
            return DynValue.NewTable(table);
        }
        catch (Exception ex)
        {
            LuaError($"maps.list failed: {ex.Message}");
            return DynValue.NewTable(new Table(script));
        }
    }

    /// <summary>
    /// maps.get(id) - Get a specific map config.
    /// </summary>
    private DynValue LuaMapsGet(Script script, string id)
    {
        try
        {
            var map = CustomMaps.CustomMapRegistry.Get(id);
            if (map == null) return DynValue.Nil;

            return MapConfigToLuaTable(script, map);
        }
        catch (Exception ex)
        {
            LuaError($"maps.get failed: {ex.Message}");
            return DynValue.Nil;
        }
    }

    /// <summary>
    /// Convert CustomMapConfig to Lua table.
    /// </summary>
    private DynValue MapConfigToLuaTable(Script script, CustomMaps.CustomMapConfig map)
    {
        var table = new Table(script);
        table["id"] = map.Id ?? "";
        table["name"] = map.Name ?? "";
        table["author"] = map.Author ?? "";
        table["description"] = map.Description ?? "";
        table["version"] = map.Version ?? "1.0";
        table["seed"] = map.Seed.HasValue ? DynValue.NewNumber(map.Seed.Value) : DynValue.Nil;
        table["size"] = map.MapSize.HasValue ? DynValue.NewNumber(map.MapSize.Value) : DynValue.Nil;
        table["weight"] = map.Weight;
        table["condition"] = map.Condition ?? "";

        // Layers
        var layersTable = new Table(script);
        int i = 1;
        foreach (var layer in map.Layers)
            layersTable[i++] = layer;
        table["layers"] = layersTable;

        // Tags
        var tagsTable = new Table(script);
        i = 1;
        foreach (var tag in map.Tags)
            tagsTable[i++] = tag;
        table["tags"] = tagsTable;

        // Disabled generators
        var disabledTable = new Table(script);
        i = 1;
        foreach (var gen in map.DisabledGenerators)
            disabledTable[i++] = gen;
        table["disabled_generators"] = disabledTable;

        // Generator configs (simplified)
        var gensTable = new Table(script);
        foreach (var (genName, genConfig) in map.Generators)
        {
            var genTable = new Table(script);
            genTable["enabled"] = genConfig.Enabled ?? true;

            var propsTable = new Table(script);
            foreach (var (propName, propValue) in genConfig.Properties)
            {
                propsTable[propName] = DynValue.FromObject(script, propValue);
            }
            genTable["properties"] = propsTable;
            gensTable[genName] = genTable;
        }
        table["generators"] = gensTable;

        return DynValue.NewTable(table);
    }

    /// <summary>
    /// maps.set_active(id_or_config) - Set active map.
    /// </summary>
    private DynValue LuaMapsSetActive(DynValue arg)
    {
        try
        {
            if (arg.Type == DataType.String)
            {
                var success = CustomMaps.CustomMapRegistry.SetActiveOverride(arg.String);
                return DynValue.NewBoolean(success);
            }
            else if (arg.Type == DataType.Table)
            {
                // Convert Lua table to config and set
                var config = LuaTableToMapConfig(arg.Table);
                if (config != null)
                {
                    CustomMaps.CustomMapRegistry.SetActiveOverride(config);
                    return DynValue.NewBoolean(true);
                }
                return DynValue.NewBoolean(false);
            }
            return DynValue.NewBoolean(false);
        }
        catch (Exception ex)
        {
            LuaError($"maps.set_active failed: {ex.Message}");
            return DynValue.NewBoolean(false);
        }
    }

    /// <summary>
    /// maps.clear_active() - Clear the active override.
    /// </summary>
    private void LuaMapsClearActive()
    {
        CustomMaps.CustomMapRegistry.ClearActiveOverride();
    }

    /// <summary>
    /// maps.get_active() - Get the currently active map.
    /// </summary>
    private DynValue LuaMapsGetActive(Script script)
    {
        try
        {
            var map = CustomMaps.CustomMapRegistry.GetActiveOverride();
            if (map == null) return DynValue.Nil;
            return MapConfigToLuaTable(script, map);
        }
        catch (Exception ex)
        {
            LuaError($"maps.get_active failed: {ex.Message}");
            return DynValue.Nil;
        }
    }

    /// <summary>
    /// maps.create(id) - Create a map builder table.
    /// Returns a table with chainable methods.
    /// </summary>
    private DynValue LuaMapsCreate(Script script, string id)
    {
        var builder = new Table(script);

        // Store config data in the table
        builder["_id"] = id;
        builder["_name"] = id;
        builder["_author"] = "";
        builder["_description"] = "";
        builder["_version"] = "1.0";
        builder["_seed"] = DynValue.Nil;
        builder["_size"] = DynValue.Nil;
        builder["_weight"] = 10;
        builder["_layers"] = DynValue.NewTable(new Table(script) { [1] = "medium" });
        builder["_tags"] = DynValue.NewTable(new Table(script));
        builder["_disabled"] = DynValue.NewTable(new Table(script));
        builder["_generators"] = DynValue.NewTable(new Table(script));
        builder["_condition"] = "";

        // Chainable methods
        builder["with_name"] = DynValue.NewCallback((ctx, args) => {
            builder["_name"] = args[0].String;
            return DynValue.NewTable(builder);
        });

        builder["with_author"] = DynValue.NewCallback((ctx, args) => {
            builder["_author"] = args[0].String;
            return DynValue.NewTable(builder);
        });

        builder["with_description"] = DynValue.NewCallback((ctx, args) => {
            builder["_description"] = args[0].String;
            return DynValue.NewTable(builder);
        });

        builder["with_version"] = DynValue.NewCallback((ctx, args) => {
            builder["_version"] = args[0].String;
            return DynValue.NewTable(builder);
        });

        builder["with_seed"] = DynValue.NewCallback((ctx, args) => {
            builder["_seed"] = args[0];
            return DynValue.NewTable(builder);
        });

        builder["with_size"] = DynValue.NewCallback((ctx, args) => {
            builder["_size"] = args[0];
            return DynValue.NewTable(builder);
        });

        builder["with_weight"] = DynValue.NewCallback((ctx, args) => {
            builder["_weight"] = args[0].Number;
            return DynValue.NewTable(builder);
        });

        builder["with_layers"] = DynValue.NewCallback((ctx, args) => {
            var layers = new Table(script);
            for (int i = 0; i < args.Count; i++)
                layers[i + 1] = args[i].String;
            builder["_layers"] = DynValue.NewTable(layers);
            return DynValue.NewTable(builder);
        });

        builder["with_tags"] = DynValue.NewCallback((ctx, args) => {
            var tags = new Table(script);
            for (int i = 0; i < args.Count; i++)
                tags[i + 1] = args[i].String;
            builder["_tags"] = DynValue.NewTable(tags);
            return DynValue.NewTable(builder);
        });

        builder["with_condition"] = DynValue.NewCallback((ctx, args) => {
            builder["_condition"] = args[0].String;
            return DynValue.NewTable(builder);
        });

        builder["disable_generator"] = DynValue.NewCallback((ctx, args) => {
            var disabled = builder.Get("_disabled").Table;
            disabled[disabled.Length + 1] = args[0].String;
            return DynValue.NewTable(builder);
        });

        builder["configure_generator"] = DynValue.NewCallback((ctx, args) => {
            var genName = args[0].String;
            var genConfig = args[1].Table;
            var gens = builder.Get("_generators").Table;
            gens[genName] = genConfig;
            return DynValue.NewTable(builder);
        });

        // Build and return config table
        builder["build"] = DynValue.NewCallback((ctx, args) => {
            return LuaBuilderToConfig(script, builder);
        });

        // Build and register
        builder["register"] = DynValue.NewCallback((ctx, args) => {
            var configTable = LuaBuilderToConfig(script, builder);
            return LuaMapsRegister(configTable.Table);
        });

        // Build, register, and set active
        builder["activate"] = DynValue.NewCallback((ctx, args) => {
            var configTable = LuaBuilderToConfig(script, builder);
            var success = LuaMapsRegister(configTable.Table);
            if (success.Boolean)
            {
                CustomMaps.CustomMapRegistry.SetActiveOverride(builder.Get("_id").String);
            }
            return success;
        });

        return DynValue.NewTable(builder);
    }

    /// <summary>
    /// Convert builder table to config table.
    /// </summary>
    private DynValue LuaBuilderToConfig(Script script, Table builder)
    {
        var config = new Table(script);
        config["id"] = builder.Get("_id");
        config["name"] = builder.Get("_name");
        config["author"] = builder.Get("_author");
        config["description"] = builder.Get("_description");
        config["version"] = builder.Get("_version");
        config["seed"] = builder.Get("_seed");
        config["size"] = builder.Get("_size");
        config["weight"] = builder.Get("_weight");
        config["layers"] = builder.Get("_layers");
        config["tags"] = builder.Get("_tags");
        config["disabled_generators"] = builder.Get("_disabled");
        config["generators"] = builder.Get("_generators");
        config["condition"] = builder.Get("_condition");
        return DynValue.NewTable(config);
    }

    /// <summary>
    /// maps.register(config_table) - Register a map from Lua table.
    /// </summary>
    private DynValue LuaMapsRegister(Table configTable)
    {
        try
        {
            var config = LuaTableToMapConfig(configTable);
            if (config == null)
            {
                LuaError("maps.register: Invalid config table");
                return DynValue.NewBoolean(false);
            }

            var success = CustomMaps.CustomMapRegistry.Register(config);
            return DynValue.NewBoolean(success);
        }
        catch (Exception ex)
        {
            LuaError($"maps.register failed: {ex.Message}");
            return DynValue.NewBoolean(false);
        }
    }

    /// <summary>
    /// Convert Lua table to CustomMapConfig.
    /// </summary>
    private CustomMaps.CustomMapConfig LuaTableToMapConfig(Table table)
    {
        var config = new CustomMaps.CustomMapConfig
        {
            Id = table.Get("id").String ?? table.Get("_id").String,
            Name = table.Get("name").String ?? table.Get("_name").String,
            Author = table.Get("author").String ?? table.Get("_author").String ?? "",
            Description = table.Get("description").String ?? table.Get("_description").String ?? "",
            Version = table.Get("version").String ?? table.Get("_version").String ?? "1.0",
            Condition = table.Get("condition").String ?? table.Get("_condition").String ?? ""
        };

        // Seed
        var seedVal = table.Get("seed");
        if (seedVal.IsNil()) seedVal = table.Get("_seed");
        if (!seedVal.IsNil() && seedVal.Type == DataType.Number)
            config.Seed = (int)seedVal.Number;

        // Size
        var sizeVal = table.Get("size");
        if (sizeVal.IsNil()) sizeVal = table.Get("_size");
        if (!sizeVal.IsNil() && sizeVal.Type == DataType.Number)
            config.MapSize = (int)sizeVal.Number;

        // Weight
        var weightVal = table.Get("weight");
        if (weightVal.IsNil()) weightVal = table.Get("_weight");
        if (!weightVal.IsNil() && weightVal.Type == DataType.Number)
            config.Weight = (int)weightVal.Number;

        // Layers
        var layersVal = table.Get("layers");
        if (layersVal.IsNil()) layersVal = table.Get("_layers");
        if (!layersVal.IsNil() && layersVal.Type == DataType.Table)
        {
            config.Layers.Clear();
            foreach (var pair in layersVal.Table.Pairs)
            {
                if (pair.Value.Type == DataType.String)
                    config.Layers.Add(pair.Value.String);
            }
        }

        // Tags
        var tagsVal = table.Get("tags");
        if (tagsVal.IsNil()) tagsVal = table.Get("_tags");
        if (!tagsVal.IsNil() && tagsVal.Type == DataType.Table)
        {
            foreach (var pair in tagsVal.Table.Pairs)
            {
                if (pair.Value.Type == DataType.String)
                    config.Tags.Add(pair.Value.String);
            }
        }

        // Disabled generators
        var disabledVal = table.Get("disabled_generators");
        if (disabledVal.IsNil()) disabledVal = table.Get("_disabled");
        if (!disabledVal.IsNil() && disabledVal.Type == DataType.Table)
        {
            foreach (var pair in disabledVal.Table.Pairs)
            {
                if (pair.Value.Type == DataType.String)
                    config.DisabledGenerators.Add(pair.Value.String);
            }
        }

        // Generator configs
        var gensVal = table.Get("generators");
        if (gensVal.IsNil()) gensVal = table.Get("_generators");
        if (!gensVal.IsNil() && gensVal.Type == DataType.Table)
        {
            foreach (var pair in gensVal.Table.Pairs)
            {
                if (pair.Key.Type != DataType.String || pair.Value.Type != DataType.Table)
                    continue;

                var genName = pair.Key.String;
                var genTable = pair.Value.Table;
                var genConfig = new CustomMaps.GeneratorConfig();

                var enabledVal = genTable.Get("enabled");
                if (!enabledVal.IsNil() && enabledVal.Type == DataType.Boolean)
                    genConfig.Enabled = enabledVal.Boolean;

                var propsVal = genTable.Get("properties");
                if (!propsVal.IsNil() && propsVal.Type == DataType.Table)
                {
                    foreach (var propPair in propsVal.Table.Pairs)
                    {
                        if (propPair.Key.Type == DataType.String)
                        {
                            var propName = propPair.Key.String;
                            object propValue = propPair.Value.Type switch
                            {
                                DataType.Number => propPair.Value.Number,
                                DataType.Boolean => propPair.Value.Boolean,
                                DataType.String => propPair.Value.String,
                                _ => null
                            };
                            if (propValue != null)
                                genConfig.Properties[propName] = propValue;
                        }
                    }
                }

                config.Generators[genName] = genConfig;
            }
        }

        return config;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Maps API: Zone/Tile/Path Management
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// maps.add_zone({ id, name, type, x, y, width, height, priority })
    /// </summary>
    private DynValue LuaMapsAddZone(Table table)
    {
        try
        {
            var activeConfig = CustomMaps.CustomMapRegistry.GetActiveOverride();
            if (activeConfig == null)
            {
                LuaError("maps.add_zone: No active map override");
                return DynValue.NewBoolean(false);
            }

            var zone = new CustomMaps.MapZone
            {
                Id = table.Get("id").String ?? $"zone_{DateTime.Now.Ticks}",
                Name = table.Get("name").String ?? "Unnamed Zone",
                X = (int)(table.Get("x").Number),
                Y = (int)(table.Get("y").Number),
                Width = (int)(table.Get("width").Number),
                Height = (int)(table.Get("height").Number),
                Priority = table.Get("priority").IsNil() ? 0 : (int)(table.Get("priority").Number)
            };

            // Parse zone type (matches game's MissionAreaType enum)
            var typeVal = table.Get("type");
            if (!typeVal.IsNil())
            {
                if (typeVal.Type == DataType.Number)
                {
                    // Allow numeric type values
                    zone.Type = (CustomMaps.ZoneType)(int)typeVal.Number;
                }
                else if (typeVal.Type == DataType.String)
                {
                    zone.Type = typeVal.String.ToLowerInvariant() switch
                    {
                        "base" => CustomMaps.ZoneType.Base,
                        "chunk" => CustomMaps.ZoneType.Chunk,
                        "southmapborder" or "south" => CustomMaps.ZoneType.SouthMapBorder,
                        "eastmapborder" or "east" => CustomMaps.ZoneType.EastMapBorder,
                        "westmapborder" or "west" => CustomMaps.ZoneType.WestMapBorder,
                        "northmapborder" or "north" => CustomMaps.ZoneType.NorthMapBorder,
                        "rect" => CustomMaps.ZoneType.Rect,
                        "northeastmapborder" or "northeast" => CustomMaps.ZoneType.NorthEastMapBorder,
                        "southeastmapborder" or "southeast" => CustomMaps.ZoneType.SouthEastMapBorder,
                        "southwestmapborder" or "southwest" => CustomMaps.ZoneType.SouthWestMapBorder,
                        "northwestmapborder" or "northwest" => CustomMaps.ZoneType.NorthWestMapBorder,
                        _ => CustomMaps.ZoneType.Custom
                    };
                }
            }

            // Remove existing zone with same ID
            activeConfig.Zones.RemoveAll(z => z.Id == zone.Id);
            activeConfig.Zones.Add(zone);

            return DynValue.NewBoolean(true);
        }
        catch (Exception ex)
        {
            LuaError($"maps.add_zone failed: {ex.Message}");
            return DynValue.NewBoolean(false);
        }
    }

    /// <summary>
    /// maps.remove_zone(id)
    /// </summary>
    private DynValue LuaMapsRemoveZone(string id)
    {
        try
        {
            var activeConfig = CustomMaps.CustomMapRegistry.GetActiveOverride();
            if (activeConfig == null)
                return DynValue.NewBoolean(false);

            var removed = activeConfig.Zones.RemoveAll(z => z.Id == id);
            return DynValue.NewBoolean(removed > 0);
        }
        catch (Exception ex)
        {
            LuaError($"maps.remove_zone failed: {ex.Message}");
            return DynValue.NewBoolean(false);
        }
    }

    /// <summary>
    /// maps.configure_zone(id, { generators = {...}, disabled = {...} })
    /// </summary>
    private DynValue LuaMapsConfigureZone(string id, Table config)
    {
        try
        {
            // Get active config
            var activeConfig = CustomMaps.CustomMapRegistry.GetActiveOverride();
            if (activeConfig == null)
            {
                LuaError("maps.configure_zone: No active map override");
                return DynValue.NewBoolean(false);
            }

            var zone = activeConfig.Zones.FirstOrDefault(z => z.Id == id);
            if (zone == null)
            {
                LuaError($"maps.configure_zone: Zone '{id}' not found");
                return DynValue.NewBoolean(false);
            }

            // Apply generator configs
            var gensVal = config.Get("generators");
            if (!gensVal.IsNil() && gensVal.Type == DataType.Table)
            {
                foreach (var pair in gensVal.Table.Pairs)
                {
                    if (pair.Key.Type != DataType.String || pair.Value.Type != DataType.Table)
                        continue;

                    var genName = pair.Key.String;
                    var genTable = pair.Value.Table;
                    var genConfig = new CustomMaps.GeneratorConfig();

                    var enabledVal = genTable.Get("enabled");
                    if (!enabledVal.IsNil() && enabledVal.Type == DataType.Boolean)
                        genConfig.Enabled = enabledVal.Boolean;

                    var propsVal = genTable.Get("properties");
                    if (!propsVal.IsNil() && propsVal.Type == DataType.Table)
                    {
                        foreach (var propPair in propsVal.Table.Pairs)
                        {
                            if (propPair.Key.Type == DataType.String)
                            {
                                var propName = propPair.Key.String;
                                object propValue = propPair.Value.Type switch
                                {
                                    DataType.Number => propPair.Value.Number,
                                    DataType.Boolean => propPair.Value.Boolean,
                                    DataType.String => propPair.Value.String,
                                    _ => null
                                };
                                if (propValue != null)
                                    genConfig.Properties[propName] = propValue;
                            }
                        }
                    }

                    zone.Generators[genName] = genConfig;
                }
            }

            // Apply disabled generators
            var disabledVal = config.Get("disabled");
            if (!disabledVal.IsNil() && disabledVal.Type == DataType.Table)
            {
                zone.DisabledGenerators.Clear();
                foreach (var pair in disabledVal.Table.Pairs)
                {
                    if (pair.Value.Type == DataType.String)
                        zone.DisabledGenerators.Add(pair.Value.String);
                }
            }

            return DynValue.NewBoolean(true);
        }
        catch (Exception ex)
        {
            LuaError($"maps.configure_zone failed: {ex.Message}");
            return DynValue.NewBoolean(false);
        }
    }

    /// <summary>
    /// maps.get_zone_at(x, y)
    /// </summary>
    private DynValue LuaMapsGetZoneAt(Script script, int x, int y)
    {
        var zone = CustomMaps.TileOverrideInjector.GetZoneAt(x, y);
        if (zone == null)
            return DynValue.Nil;

        var table = new Table(script);
        table["id"] = zone.Id;
        table["name"] = zone.Name;
        table["type"] = zone.Type.ToString().ToLowerInvariant();
        table["x"] = zone.X;
        table["y"] = zone.Y;
        table["width"] = zone.Width;
        table["height"] = zone.Height;
        table["priority"] = zone.Priority;
        return DynValue.NewTable(table);
    }

    /// <summary>
    /// maps.set_tile(x, y, { terrain, height })
    /// </summary>
    private DynValue LuaMapsSetTile(int x, int y, Table config)
    {
        try
        {
            var activeConfig = CustomMaps.CustomMapRegistry.GetActiveOverride();
            if (activeConfig == null)
            {
                LuaError("maps.set_tile: No active map override");
                return DynValue.NewBoolean(false);
            }

            var tile = new CustomMaps.TileOverride
            {
                X = x,
                Y = y
            };

            // terrain type (Trees, Water, HighGround, Road, Sand, Concrete)
            var terrainVal = config.Get("terrain");
            if (!terrainVal.IsNil() && terrainVal.Type == DataType.String)
                tile.Terrain = terrainVal.String;

            // height
            var heightVal = config.Get("height");
            if (!heightVal.IsNil() && heightVal.Type == DataType.Number)
                tile.Height = (float)heightVal.Number;

            // Remove existing tile at this position and add new one
            activeConfig.Tiles.RemoveAll(t => t.X == x && t.Y == y);
            activeConfig.Tiles.Add(tile);

            return DynValue.NewBoolean(true);
        }
        catch (Exception ex)
        {
            LuaError($"maps.set_tile failed: {ex.Message}");
            return DynValue.NewBoolean(false);
        }
    }

    /// <summary>
    /// maps.clear_tile(x, y)
    /// </summary>
    private DynValue LuaMapsClearTile(int x, int y)
    {
        try
        {
            var activeConfig = CustomMaps.CustomMapRegistry.GetActiveOverride();
            if (activeConfig == null)
                return DynValue.NewBoolean(false);

            var removed = activeConfig.Tiles.RemoveAll(t => t.X == x && t.Y == y);
            return DynValue.NewBoolean(removed > 0);
        }
        catch (Exception ex)
        {
            LuaError($"maps.clear_tile failed: {ex.Message}");
            return DynValue.NewBoolean(false);
        }
    }

    /// <summary>
    /// maps.get_tile(x, y)
    /// </summary>
    private DynValue LuaMapsGetTile(Script script, int x, int y)
    {
        var tile = CustomMaps.TileOverrideInjector.GetTileAt(x, y);
        if (tile == null)
            return DynValue.Nil;

        var table = new Table(script);
        table["x"] = tile.X;
        table["y"] = tile.Y;
        if (!string.IsNullOrEmpty(tile.Terrain)) table["terrain"] = tile.Terrain;
        if (tile.Height.HasValue) table["height"] = tile.Height.Value;
        return DynValue.NewTable(table);
    }

    /// <summary>
    /// maps.add_chunk({ x, y, template, rotation })
    /// </summary>
    private DynValue LuaMapsAddChunk(Table config)
    {
        try
        {
            var activeConfig = CustomMaps.CustomMapRegistry.GetActiveOverride();
            if (activeConfig == null)
            {
                LuaError("maps.add_chunk: No active map override");
                return DynValue.NewBoolean(false);
            }

            var chunk = new CustomMaps.ChunkPlacement
            {
                X = (int)config.Get("x").Number,
                Y = (int)config.Get("y").Number,
                ChunkTemplate = config.Get("template").String ?? ""
            };

            var rotationVal = config.Get("rotation");
            if (!rotationVal.IsNil() && rotationVal.Type == DataType.Number)
                chunk.Rotation = (int)rotationVal.Number;

            // Remove existing chunk at same position and add new one
            activeConfig.Chunks.RemoveAll(c => c.X == chunk.X && c.Y == chunk.Y);
            activeConfig.Chunks.Add(chunk);

            return DynValue.NewBoolean(true);
        }
        catch (Exception ex)
        {
            LuaError($"maps.add_chunk failed: {ex.Message}");
            return DynValue.NewBoolean(false);
        }
    }

    /// <summary>
    /// maps.remove_chunk(x, y)
    /// </summary>
    private DynValue LuaMapsRemoveChunk(int x, int y)
    {
        try
        {
            var activeConfig = CustomMaps.CustomMapRegistry.GetActiveOverride();
            if (activeConfig == null)
                return DynValue.NewBoolean(false);

            var removed = activeConfig.Chunks.RemoveAll(c => c.X == x && c.Y == y);
            return DynValue.NewBoolean(removed > 0);
        }
        catch (Exception ex)
        {
            LuaError($"maps.remove_chunk failed: {ex.Message}");
            return DynValue.NewBoolean(false);
        }
    }

    /// <summary>
    /// maps.add_path({ id, type, width, waypoints = {{x,y}, ...} })
    /// </summary>
    private DynValue LuaMapsAddPath(Table config)
    {
        try
        {
            var path = new CustomMaps.MapPath
            {
                Id = config.Get("id").String ?? $"path_{DateTime.Now.Ticks}",
                Width = config.Get("width").IsNil() ? 3 : (int)config.Get("width").Number
            };

            // Parse type
            var typeVal = config.Get("type");
            if (!typeVal.IsNil() && typeVal.Type == DataType.String)
            {
                path.Type = typeVal.String.ToLowerInvariant() switch
                {
                    "road" => CustomMaps.PathType.Road,
                    "river" => CustomMaps.PathType.River,
                    "trail" => CustomMaps.PathType.Trail,
                    "trench" => CustomMaps.PathType.Trench,
                    _ => CustomMaps.PathType.Road
                };
            }

            // Parse waypoints
            var waypointsVal = config.Get("waypoints");
            if (!waypointsVal.IsNil() && waypointsVal.Type == DataType.Table)
            {
                foreach (var pair in waypointsVal.Table.Pairs)
                {
                    if (pair.Value.Type == DataType.Table)
                    {
                        var wpTable = pair.Value.Table;
                        var x = (int)wpTable.Get("x").Number;
                        var y = (int)wpTable.Get("y").Number;
                        path.Waypoints.Add(new CustomMaps.PathWaypoint(x, y));
                    }
                }
            }

            // Add to active config
            var activeConfig = CustomMaps.CustomMapRegistry.GetActiveOverride();
            if (activeConfig != null)
            {
                activeConfig.Paths.RemoveAll(p => p.Id == path.Id);
                activeConfig.Paths.Add(path);
            }

            return DynValue.NewBoolean(true);
        }
        catch (Exception ex)
        {
            LuaError($"maps.add_path failed: {ex.Message}");
            return DynValue.NewBoolean(false);
        }
    }

    /// <summary>
    /// maps.remove_path(id)
    /// </summary>
    private DynValue LuaMapsRemovePath(string id)
    {
        var activeConfig = CustomMaps.CustomMapRegistry.GetActiveOverride();
        if (activeConfig == null)
            return DynValue.NewBoolean(false);

        var removed = activeConfig.Paths.RemoveAll(p => p.Id == id) > 0;
        return DynValue.NewBoolean(removed);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Script Execution
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Execute Lua code string.
    /// </summary>
    public string ExecuteString(string code, string chunkName = "console")
    {
        try
        {
            SdkLogger.Msg($"[LuaEngine] Executing: {code}");
            var result = _lua.DoString(code, null, chunkName);
            SdkLogger.Msg($"[LuaEngine] Result: {(result.IsNil() || result.IsVoid() ? "(ok)" : result.ToPrintString())}");
            if (result.IsNil() || result.IsVoid())
                return "(ok)";
            return result.ToPrintString();
        }
        catch (ScriptRuntimeException ex)
        {
            return $"Lua error: {ex.DecoratedMessage}";
        }
        catch (SyntaxErrorException ex)
        {
            return $"Lua syntax error: {ex.DecoratedMessage}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Execute Lua file.
    /// </summary>
    public string ExecuteFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return $"File not found: {path}";

            var code = File.ReadAllText(path);
            return ExecuteString(code, Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            return $"Error loading file: {ex.Message}";
        }
    }

    /// <summary>
    /// Load and execute a Lua script from a modpack.
    /// </summary>
    public bool LoadModpackScript(string modId, string scriptPath)
    {
        try
        {
            if (!File.Exists(scriptPath))
            {
                SdkLogger.Warning($"[LuaEngine] Script not found: {scriptPath}");
                return false;
            }

            var code = File.ReadAllText(scriptPath);

            // Create a new script instance for isolation (optional - could share state)
            var script = new Script(CoreModules.Preset_SoftSandbox);

            // Copy API functions to new script using explicit callbacks
            RegisterApiCallbacks(script);

            // Set mod context
            script.Globals["MOD_ID"] = modId;
            script.Globals["SCRIPT_PATH"] = scriptPath;

            // Execute the script
            script.DoString(code, null, Path.GetFileName(scriptPath));

            _loadedScripts.Add((modId, scriptPath, script));
            SdkLogger.Msg($"[LuaEngine] Loaded script: {Path.GetFileName(scriptPath)} from {modId}");

            return true;
        }
        catch (ScriptRuntimeException ex)
        {
            SdkLogger.Error($"[LuaEngine] Runtime error in {scriptPath}: {ex.DecoratedMessage}");
            return false;
        }
        catch (SyntaxErrorException ex)
        {
            SdkLogger.Error($"[LuaEngine] Syntax error in {scriptPath}: {ex.DecoratedMessage}");
            return false;
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[LuaEngine] Error loading {scriptPath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Unload all scripts from a modpack.
    /// </summary>
    public void UnloadModpackScripts(string modId)
    {
        _loadedScripts.RemoveAll(s => s.ModId == modId);
        SdkLogger.Msg($"[LuaEngine] Unloaded scripts for {modId}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Event System
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Fire an event to all registered Lua handlers.
    /// </summary>
    public void FireEvent(string eventName, params DynValue[] args)
    {
        if (!_eventHandlers.TryGetValue(eventName, out var handlers) || handlers.Count == 0)
            return;

        foreach (var (ownerScript, handler) in handlers.ToList()) // ToList to allow modification during iteration
        {
            try
            {
                if (handler == null || handler.IsNil())
                {
                    SdkLogger.Warning($"[LuaEngine] Skipping nil {eventName} handler");
                    continue;
                }
                // Call handler from its owning script to avoid cross-script resource errors
                ownerScript.Call(handler, args);
            }
            catch (ScriptRuntimeException ex)
            {
                var msg = ex.DecoratedMessage;
                if (string.IsNullOrWhiteSpace(msg))
                    msg = ex.Message;
                if (string.IsNullOrWhiteSpace(msg))
                    msg = $"{ex.GetType().Name} (no message)";
                SdkLogger.Warning($"[LuaEngine] Error in {eventName} handler: {msg}");
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (string.IsNullOrWhiteSpace(msg))
                    msg = $"{ex.GetType().Name} (no message)";
                SdkLogger.Warning($"[LuaEngine] Error in {eventName} handler: {msg}");
            }
        }
    }

    /// <summary>
    /// Fire event with simple string argument.
    /// </summary>
    public void FireEvent(string eventName, string arg)
    {
        FireEvent(eventName, DynValue.NewString(arg));
    }

    /// <summary>
    /// Fire event with table argument.
    /// </summary>
    public void FireEventWithTable(string eventName, Dictionary<string, object> data)
    {
        var table = new Table(_lua);
        foreach (var kvp in data)
        {
            table[kvp.Key] = DynValue.FromObject(_lua, kvp.Value);
        }
        FireEvent(eventName, DynValue.NewTable(table));
    }

    /// <summary>
    /// Fire event with actor and skill objects (Phase 3 object bindings).
    /// Passes LuaActor and LuaSkill UserData objects to handlers.
    ///
    /// Example Lua handler:
    ///   on("skill_used_obj", function(actor, skill)
    ///       if skill.is_attack and not skill.is_silent then
    ///           actor:add_effect("concealment", -3, 1)
    ///       end
    ///   end)
    /// </summary>
    public void FireEventWithActorAndSkill(string eventName, long actorPtr, long skillPtr)
    {
        var actorObj = LuaObjectBindings.CreateActor(actorPtr);
        var skillObj = LuaObjectBindings.CreateSkill(skillPtr);
        FireEvent(eventName, actorObj, skillObj);
    }

    /// <summary>
    /// Fire event with actor object only.
    /// </summary>
    public void FireEventWithActor(string eventName, long actorPtr)
    {
        var actorObj = LuaObjectBindings.CreateActor(actorPtr);
        FireEvent(eventName, actorObj);
    }

    /// <summary>
    /// Fire event with actor object and additional data table.
    /// </summary>
    public void FireEventWithActorAndData(string eventName, long actorPtr, Dictionary<string, object> data)
    {
        var actorObj = LuaObjectBindings.CreateActor(actorPtr);
        var table = new Table(_lua);
        foreach (var kvp in data)
        {
            table[kvp.Key] = DynValue.FromObject(_lua, kvp.Value);
        }
        FireEvent(eventName, actorObj, DynValue.NewTable(table));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Event Triggers (called from game hooks)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called when a scene is loaded.
    /// </summary>
    public void OnSceneLoaded(string sceneName)
    {
        FireEvent("scene_loaded", sceneName);
    }

    /// <summary>
    /// Called when tactical battle is ready.
    /// </summary>
    public void OnTacticalReady()
    {
        FireEvent("tactical_ready");
    }

    /// <summary>
    /// Called at mission start.
    /// </summary>
    public void OnMissionStart(string missionName, string biome, int difficulty)
    {
        FireEventWithTable("mission_start", new Dictionary<string, object>
        {
            ["name"] = missionName,
            ["biome"] = biome,
            ["difficulty"] = difficulty
        });
    }

    /// <summary>
    /// Called at turn start.
    /// </summary>
    public void OnTurnStart(int factionIndex, string factionName)
    {
        FireEventWithTable("turn_start", new Dictionary<string, object>
        {
            ["faction"] = factionIndex,
            ["factionName"] = factionName
        });
    }

    /// <summary>
    /// Called at turn end.
    /// </summary>
    public void OnTurnEnd(int factionIndex, string factionName)
    {
        FireEventWithTable("turn_end", new Dictionary<string, object>
        {
            ["faction"] = factionIndex,
            ["factionName"] = factionName
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Strategy Event Triggers (for pool injection hooks)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called when a new campaign is started (before pools are built).
    /// Use this to inject content into black market, spawn pools, etc.
    /// </summary>
    public void OnCampaignStart()
    {
        SdkLogger.Msg("[LuaEngine] Firing campaign_start event");
        FireEvent("campaign_start");
    }

    /// <summary>
    /// Called when a saved campaign is loaded.
    /// </summary>
    public void OnCampaignLoaded()
    {
        SdkLogger.Msg("[LuaEngine] Firing campaign_loaded event");
        FireEvent("campaign_loaded");
    }

    /// <summary>
    /// Called when an operation completes and player returns to strategy layer.
    /// </summary>
    public void OnOperationEnd()
    {
        SdkLogger.Msg("[LuaEngine] Firing operation_end event");
        FireEvent("operation_end");
    }

    /// <summary>
    /// Called before black market restocks its inventory.
    /// Use this to inject items into the black market pool.
    /// </summary>
    public void OnBlackMarketRefresh()
    {
        SdkLogger.Msg("[LuaEngine] Firing blackmarket_refresh event");
        FireEvent("blackmarket_refresh");
    }
}
