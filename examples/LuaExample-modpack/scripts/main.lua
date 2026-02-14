-- ============================================================================
-- Lua Scripting Example for Menace Modkit
-- ============================================================================
-- This script demonstrates the Lua scripting API. Place .lua files in your
-- modpack's scripts/ folder and they'll be loaded automatically.
--
-- Available API:
--   cmd("command args")     - Execute console command, returns {success, result, data}
--   log("message")          - Log to console
--   warn("message")         - Log warning
--   error("message")        - Log error
--   on("event", callback)   - Register event handler
--   off("event", callback)  - Unregister event handler
--   commands()              - Get list of all available commands
--   has_command("name")     - Check if command exists
--
-- Available events:
--   scene_loaded(sceneName)       - Fired when any scene loads
--   tactical_ready()              - Fired when tactical battle is ready
--   mission_start(info)           - Fired at mission start (info.name, info.biome, info.difficulty)
--   turn_start(info)              - Fired at turn start (info.faction, info.factionName)
--   turn_end(info)                - Fired at turn end (info.faction, info.factionName)
-- ============================================================================

log("LuaExample script loaded!")
log("Mod ID: " .. (MOD_ID or "unknown"))

-- ============================================================================
-- Basic command execution
-- ============================================================================

-- Execute a simple command
local result = cmd("roster")
if result.success then
    log("Roster command succeeded: " .. result.result)
else
    warn("Roster command failed: " .. result.result)
end

-- Check if we're in the right context for certain commands
if has_command("operation") then
    local op = cmd("operation hasactive")
    if op.success and op.result == "true" then
        log("Active operation detected!")
    end
end

-- ============================================================================
-- Event handlers
-- ============================================================================

-- Called when any scene loads
on("scene_loaded", function(sceneName)
    log("Scene loaded: " .. sceneName)

    -- Do something special for tactical scenes
    if string.find(sceneName, "Tactical") then
        log("Entering tactical battle!")
    end
end)

-- Called when tactical battle is fully ready
on("tactical_ready", function()
    log("Tactical battle ready - all systems go!")

    -- Example: Log the current roster
    local r = cmd("roster")
    if r.success then
        log("Current roster:\n" .. r.result)
    end
end)

-- Called at mission start
on("mission_start", function(info)
    log(string.format("Mission starting: %s in %s (difficulty %d)",
        info.name or "unknown",
        info.biome or "unknown",
        info.difficulty or 0))
end)

-- Called at turn start
on("turn_start", function(info)
    log(string.format("Turn start: faction %d (%s)",
        info.faction or -1,
        info.factionName or "unknown"))

    -- Example: If it's the player's turn, show some info
    if info.faction == 0 then
        cmd("status")  -- Show tactical status
    end
end)

-- ============================================================================
-- Utility functions you can define
-- ============================================================================

-- Helper to safely get a command result or default
function safe_cmd(command, default)
    local r = cmd(command)
    if r.success then
        return r.result
    else
        return default or ""
    end
end

-- Helper to apply emotion to all leaders
function buff_all_leaders(emotion)
    local roster = cmd("roster")
    if not roster.success then
        warn("Could not get roster")
        return
    end

    -- Note: This is a simplified example. In practice, you'd parse the roster output.
    log("Buffing all leaders with " .. emotion)
end

-- ============================================================================
-- Interactive console commands
-- ============================================================================
-- You can also type "lua <code>" in the console to execute Lua directly!
-- Examples:
--   lua log("Hello from console!")
--   lua cmd("roster")
--   lua for i, c in ipairs(commands()) do log(c) end
-- ============================================================================

log("LuaExample initialization complete!")
