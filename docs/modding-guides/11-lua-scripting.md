# Lua Scripting

Don't want to write C#? Lua scripting provides a simpler way to create mods using the familiar Lua language. Lua scripts can execute any console command and respond to game events.

## Why Lua?

- **Simpler syntax** - No compilation, types, or boilerplate
- **Quick iteration** - Edit scripts without recompiling
- **Full console access** - All 137+ console commands available
- **Event-driven** - React to game events like turn start, scene load, etc.
- **Safe sandbox** - No file system or network access

## Getting Started

Create a `scripts/` folder in your modpack and add `.lua` files:

```
MyMod-modpack/
  modpack.json
  scripts/
    main.lua
    tactical_helper.lua
```

All `.lua` files in `scripts/` are loaded automatically when your modpack loads.

## Lua API Reference

### Commands

```lua
-- Execute any console command
local result = cmd("roster")
if result.success then
    log("Roster: " .. result.result)
else
    warn("Failed: " .. result.result)
end

-- Check if a command exists
if has_command("operation") then
    cmd("operation hasactive")
end

-- Get list of all available commands
local all_commands = commands()
for i, name in ipairs(all_commands) do
    log(name)
end
```

The `cmd()` function returns a table with:
- `success` - boolean, whether the command succeeded
- `result` - string, the command output
- `data` - table, structured data (for supported commands)

### Logging

```lua
log("Normal message")      -- White text
warn("Warning message")    -- Yellow text
error("Error message")     -- Red text
```

All messages are prefixed with `[Lua]` in the console.

### Events

Register callbacks for game events:

```lua
-- Called when any scene loads
on("scene_loaded", function(sceneName)
    log("Scene loaded: " .. sceneName)
end)

-- Called when tactical battle is ready
on("tactical_ready", function()
    log("Battle started!")
    cmd("status")
end)

-- Called at mission start
on("mission_start", function(info)
    log("Mission: " .. info.name)
    log("Biome: " .. info.biome)
    log("Difficulty: " .. info.difficulty)
end)

-- Called at turn start
on("turn_start", function(info)
    if info.faction == 0 then
        log("Your turn! Faction: " .. info.factionName)
    end
end)

-- Called at turn end
on("turn_end", function(info)
    log("Turn ended for " .. info.factionName)
end)
```

Unregister handlers when needed:

```lua
local myHandler = function(sceneName)
    log("Scene: " .. sceneName)
end

on("scene_loaded", myHandler)   -- Register
off("scene_loaded", myHandler)  -- Unregister
```

### Global Variables

Each script receives context about its modpack:

```lua
log("Mod ID: " .. MOD_ID)           -- e.g., "MyMod"
log("Script: " .. SCRIPT_PATH)       -- Full path to this script
```

## Example: Tactical Helper

A practical example that shows battle information:

```lua
-- tactical_helper.lua
-- Shows helpful info during tactical battles

on("tactical_ready", function()
    log("=== TACTICAL BATTLE STARTED ===")

    -- Show mission info
    local mission = cmd("mission")
    if mission.success then
        log(mission.result)
    end

    -- Show objectives
    local objectives = cmd("objectives")
    if objectives.success then
        log("Objectives:\n" .. objectives.result)
    end
end)

on("turn_start", function(info)
    if info.faction ~= 0 then return end  -- Only player turn

    log("--- YOUR TURN ---")

    -- Show your actors
    local actors = cmd("actors 0")
    if actors.success then
        log(actors.result)
    end
end)

on("turn_end", function(info)
    if info.faction == 0 then
        log("--- TURN COMPLETE ---")
    end
end)

log("Tactical Helper loaded!")
```

## Example: Auto-Buff on Mission Start

```lua
-- auto_buff.lua
-- Apply emotion to all leaders at mission start

on("mission_start", function(info)
    log("Mission starting: " .. info.name)

    -- High difficulty? Buff the squad
    if info.difficulty >= 3 then
        log("High difficulty detected - buffing squad!")
        cmd("emotion apply Focused Leader1")
        cmd("emotion apply Focused Leader2")
        cmd("emotion apply Focused Leader3")
    end
end)
```

## Console Commands for Lua

The game adds these console commands for working with Lua:

| Command | Description |
|---------|-------------|
| `lua <code>` | Execute Lua code directly |
| `luafile <path>` | Execute a Lua file |
| `luaevents` | List registered event handlers |
| `luascripts` | List loaded Lua scripts |

Examples:
```
lua log("Hello from console!")
lua cmd("roster")
lua for i, c in ipairs(commands()) do log(c) end
luaevents
luascripts
```

## Available Console Commands

Lua scripts can call any console command via `cmd()`. Here are the categories:

### Roster & Leaders
- `roster` - Show current roster
- `roster count` - Count roster members
- `leader <name>` - Show leader details

### Operations & Missions
- `operation hasactive` - Check for active operation
- `operation info` - Show operation details
- `mission` - Current mission info
- `objectives` - Show objectives

### Tactical Combat
- `actors [faction]` - List actors (0=player, 1=enemy)
- `status` - Tactical status
- `turn` - Current turn info
- `spawn <template>` - Spawn unit
- `spawnlist` - Available spawn templates

### Emotions & Perks
- `emotion apply <emotion> <leader>` - Apply emotion
- `emotion list` - Available emotions
- `perks <leader>` - Show leader perks

### Debug
- `tilemap` - Show tilemap info
- `los <x1> <y1> <x2> <y2>` - Check line of sight
- `path <x1> <y1> <x2> <y2>` - Find path

Run `help` or `commands()` in Lua to see all available commands.

## Tips

### Conditional Logic Based on Scene

```lua
local in_tactical = false

on("scene_loaded", function(sceneName)
    in_tactical = string.find(sceneName, "Tactical") ~= nil

    if in_tactical then
        log("Entering tactical mode")
    end
end)
```

### Helper Functions

```lua
-- Safely get command result or default
function safe_cmd(command, default)
    local r = cmd(command)
    if r.success then
        return r.result
    else
        return default or ""
    end
end

-- Usage
local roster = safe_cmd("roster", "No roster available")
```

### Checking Command Availability

Some commands only work in certain contexts:

```lua
on("tactical_ready", function()
    -- These commands only work during tactical battles
    if has_command("actors") then
        cmd("actors 0")
    end
end)
```

## Limitations

- **No file access** - Scripts run in a sandbox without `io` or `os` libraries
- **No network** - Cannot make HTTP requests
- **Single-threaded** - Scripts run synchronously
- **No persistent state** - Variables reset when the game restarts (use `ModSettings` via C# for persistence)

## Lua vs C#

| Feature | Lua | C# |
|---------|-----|-----|
| Learning curve | Easy | Moderate |
| Compilation | None | Required |
| Console commands | All via `cmd()` | All via SDK |
| Direct game access | Via commands only | Full SDK access |
| Custom UI | No | Yes (IMGUI) |
| Performance | Good | Best |
| Persistence | No | Yes (ModSettings) |

**Use Lua when:** You want quick scripts, simple automation, or are new to modding.

**Use C# when:** You need direct game object access, custom UI, or complex logic.

## Debugging

1. Check the MelonLoader console for `[Lua]` messages
2. Use `luascripts` to verify your scripts loaded
3. Use `luaevents` to see registered handlers
4. Test commands manually with `lua cmd("yourcommand")`

---

**Previous:** [Audio](06-audio.md) | **Next:** [SDK Getting Started](../coding-sdk/getting-started.md)
