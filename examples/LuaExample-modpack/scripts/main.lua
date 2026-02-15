-- =============================================================================
-- Main Lua Script for LuaExample Modpack
-- =============================================================================
-- Demonstrates core Lua scripting capabilities including:
--   - Scene events
--   - Turn/round tracking
--   - Console commands
--   - Utility functions
-- =============================================================================

log("[LuaExample] Loading main.lua...")
log("[LuaExample] MOD_ID: " .. tostring(MOD_ID))
log("[LuaExample] SCRIPT_PATH: " .. tostring(SCRIPT_PATH))

-- =============================================================================
-- GLOBAL STATE
-- =============================================================================

-- Track current game state
local currentRound = 0
local currentTurn = 0
local currentScene = ""

-- =============================================================================
-- SCENE EVENTS
-- =============================================================================

on("scene_loaded", function(sceneName)
    currentScene = sceneName or ""
    log("[LuaExample] Scene loaded: " .. currentScene)
end)

-- =============================================================================
-- TURN AND ROUND EVENTS
-- =============================================================================

on("round_start", function(data)
    if data then
        currentRound = data.round or 0
        log("[LuaExample] === ROUND " .. currentRound .. " START ===")
    end
end)

on("round_end", function(data)
    if data then
        log("[LuaExample] === ROUND " .. (data.round or currentRound) .. " END ===")
    end
end)

on("turn_start", function(data)
    if data then
        currentTurn = data.turn or 0
        local faction = data.faction or -1
        local factionName = faction == 0 and "Player" or "Enemy " .. faction
        log("[LuaExample] Turn " .. currentTurn .. " - " .. factionName .. "'s turn")
    end
end)

on("turn_end", function(data)
    if data then
        log("[LuaExample] Turn ended (faction " .. tostring(data.faction) .. ")")
    end
end)

-- =============================================================================
-- UTILITY FUNCTIONS
-- =============================================================================

-- Helper to format position coordinates
function format_pos(x, y, z)
    if x and y then
        if z then
            return string.format("(%.1f, %.1f, %.1f)", x, y, z)
        else
            return string.format("(%.1f, %.1f)", x, y)
        end
    end
    return "(?, ?)"
end

-- Helper to safely convert value to string
function safe_tostring(val)
    if val == nil then return "nil" end
    local ok, result = pcall(tostring, val)
    if ok then return result else return "<error>" end
end

-- Helper to dump a table's contents for debugging
function dump_table(t, indent)
    indent = indent or ""
    if type(t) ~= "table" then
        log(indent .. safe_tostring(t))
        return
    end
    for k, v in pairs(t) do
        if type(v) == "table" then
            log(indent .. safe_tostring(k) .. ":")
            dump_table(v, indent .. "  ")
        else
            log(indent .. safe_tostring(k) .. " = " .. safe_tostring(v))
        end
    end
end

-- =============================================================================
-- CONSOLE COMMANDS
-- =============================================================================

-- Test command to verify Lua is working
cmd("lua_test", function(args)
    log("=== LUA TEST COMMAND ===")
    log("Arguments: " .. (args or "none"))
    log("Current scene: " .. currentScene)
    log("Current round: " .. currentRound)
    log("Current turn: " .. currentTurn)

    -- Check available API functions
    log("Available APIs:")
    log("  is_mission_running: " .. type(is_mission_running))
    log("  get_player_actors: " .. type(get_player_actors))
    log("  get_enemy_actors: " .. type(get_enemy_actors))
    log("  get_selected_actor: " .. type(get_selected_actor))

    return "Test complete - check log for details"
end)

-- Status command
cmd("lua_status", function()
    log("=== LUA MOD STATUS ===")
    log("MOD_ID: " .. tostring(MOD_ID))
    log("Scene: " .. currentScene)
    log("Round: " .. currentRound)
    log("Turn: " .. currentTurn)

    if type(is_mission_running) == "function" then
        log("Mission running: " .. tostring(is_mission_running()))
    end

    return "Status printed to log"
end)

-- List available events
cmd("lua_events", function()
    log("=== AVAILABLE EVENTS ===")
    log("")
    log("TACTICAL EVENTS:")
    log("  tactical_ready     - Tactical mission initialized")
    log("  turn_start         - Turn begins (data: faction, turn)")
    log("  turn_end           - Turn ends (data: faction, turn)")
    log("  round_start        - Round begins (data: round)")
    log("  round_end          - Round ends (data: round)")
    log("  actor_killed       - Unit killed (data: actor, killer)")
    log("  actor_spawned      - Unit spawned (data: actor)")
    log("  damage_received    - Damage dealt (data: actor, damage, source)")
    log("  attack_missed      - Attack missed (data: actor, target)")
    log("  move_start         - Movement begins (data: actor, from, to)")
    log("  move_complete      - Movement ends (data: actor, position)")
    log("  skill_used         - Skill activated (data: actor, skill)")
    log("  morale_changed     - Morale changed (data: actor, old, new)")
    log("  overwatch_triggered - Overwatch fires (data: actor, target)")
    log("")
    log("STRATEGY EVENTS:")
    log("  campaign_start     - New campaign started")
    log("  leader_hired       - Leader recruited (data: leader)")
    log("  leader_dismissed   - Leader dismissed (data: leader)")
    log("  leader_permadeath  - Leader died permanently (data: leader)")
    log("  leader_levelup     - Leader gained a perk (data: leader)")
    log("  faction_trust_changed - Trust changed (data: faction, delta)")
    log("  mission_ended      - Mission completed (data: success)")
    log("  squaddie_killed    - Squaddie died (data: id)")
    log("  squaddie_added     - Squaddie recruited (data: id)")
    log("")
    log("GENERAL EVENTS:")
    log("  scene_loaded       - Scene changed (data: sceneName)")

    return "Event list printed to log"
end)

log("[LuaExample] main.lua loaded successfully!")
