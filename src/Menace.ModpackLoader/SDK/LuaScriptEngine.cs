using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MoonSharp.Interpreter;

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
    private readonly Dictionary<string, List<DynValue>> _eventHandlers = new();
    private readonly List<(string ModId, string ScriptPath, Script Script)> _loadedScripts = new();

    // Supported events
    private static readonly HashSet<string> ValidEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        // Tactical events
        "scene_loaded",
        "tactical_ready",
        "mission_start",
        "turn_start",
        "turn_end",
        "actor_killed",
        "actor_damaged",
        "ability_used",
        // Strategy events (for pool injection hooks)
        "campaign_start",
        "campaign_loaded",
        "operation_end",
        "blackmarket_refresh"
    };

    private LuaScriptEngine()
    {
        // Create Lua state with limited permissions (no OS/IO access)
        _lua = new Script(CoreModules.Preset_SoftSandbox);

        // Register API functions
        RegisterApiCallbacks(_lua);

        // Initialize event handler lists
        foreach (var evt in ValidEvents)
            _eventHandlers[evt] = new List<DynValue>();
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

        _eventHandlers[eventName].Add(callback);
    }

    /// <summary>
    /// off("event", callback) - Unregister event handler.
    /// </summary>
    private void LuaOff(string eventName, DynValue callback)
    {
        if (_eventHandlers.TryGetValue(eventName, out var handlers))
        {
            handlers.RemoveAll(h => h.Equals(callback));
        }
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

        foreach (var handler in handlers.ToList()) // ToList to allow modification during iteration
        {
            try
            {
                _lua.Call(handler, args);
            }
            catch (ScriptRuntimeException ex)
            {
                SdkLogger.Warning($"[LuaEngine] Error in {eventName} handler: {ex.DecoratedMessage}");
            }
            catch (Exception ex)
            {
                SdkLogger.Warning($"[LuaEngine] Error in {eventName} handler: {ex.Message}");
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
