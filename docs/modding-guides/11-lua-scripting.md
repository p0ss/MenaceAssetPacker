# Lua Scripting

Coding can seem complex, but Lua scripting provides a simpler way to create mods.  

There is a reason Lua ends up as the universal modding script, its dead easy. 

Lua scripts can execute any console command and respond to game events.



## Getting Started

Create a `scripts/` folder in your modpack and add `.lua` files:

```
MyMod-modpack/
  modpack.json
  scripts/
    some_name.lua
```

All `.lua` files in `scripts/` are loaded automatically when your modpack loads.

## What you can do in Lua

### Commands

```lua
-- Execute any console command

cmd("roster")

-- store that as a "result" variable for later

local result = cmd("roster")

-- check what we got back from it

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

## Tactical SDK API

The Lua engine exposes the full tactical SDK, giving you direct access to actors, movement, combat, and map data without going through console commands.

### Actor Query

```lua
-- Get all actors in the 

local actors = get_actors()
for i, actor in ipairs(actors) do
    log(actor.name .. " at (" .. actor.x .. ", " .. actor.y .. ")")
end

-- Get only player-controlled actors

local squad = get_player_actors()
for i, actor in ipairs(squad) do
    log("Squad member: " .. actor.name)
end

-- Get enemy actors

local enemies = get_enemy_actors()
log("Enemy count: " .. #enemies)

-- Find a specific actor by name
local leader = find_actor("Leader1")
if leader then
    log("Found " .. leader.name)
end

-- Get the currently selected actor
local active = get_active_actor()
if active then
    log("Selected: " .. active.name)
end
```

Actor tables contain:
- `ptr` - Internal pointer (used to reference the actor in other functions)
- `name` - Actor's display name
- `alive` - Boolean, whether actor is alive
- `x`, `y` - Tile coordinates

### Movement

```lua
local actor = get_active_actor()

-- Get current position
local pos = get_position(actor)
log("Position: " .. pos.x .. ", " .. pos.y)

-- Move to a tile (uses pathfinding)
local result = move_to(actor, 10, 15)
if result.success then
    log("Moving!")
else
    warn("Can't move: " .. result.error)
end

-- Teleport instantly (no animation)
teleport(actor, 10, 15)

-- Action points
local ap = get_ap(actor)
log("AP remaining: " .. ap)
set_ap(actor, 100)  -- Set AP to 100

-- Facing direction (0-7: N, NE, E, SE, S, SW, W, NW)
local facing = get_facing(actor)
set_facing(actor, 4)  -- Face south

-- Check if moving
if is_moving(actor) then
    log("Actor is moving")
end
```

### Combat

```lua
local attacker = find_actor("Leader1")
local target = get_enemy_actors()[1]

-- Attack a target
local result = attack(attacker, target)
if result.success then
    log("Attack with " .. result.skill)
else
    warn("Attack failed: " .. result.error)
end

-- Use a specific ability
local result = use_ability(attacker, "Overwatch", target)

-- Get actor's skills
local skills = get_skills(attacker)
for i, skill in ipairs(skills) do
    log(skill.name .. " - AP: " .. skill.ap_cost .. " Range: " .. skill.range)
    if skill.can_use then
        log("  (ready)")
    end
end

-- Health
local hp = get_hp(attacker)
log("HP: " .. hp.current .. "/" .. hp.max .. " (" .. math.floor(hp.percent * 100) .. "%)")

set_hp(attacker, 50)      -- Set HP to 50
damage(attacker, 10)      -- Apply 10 damage
heal(attacker, 20)        -- Heal 20 HP

-- Suppression (0-100)
local supp = get_suppression(target)
set_suppression(target, 50)  -- Set to 50%

-- Morale
local morale = get_morale(attacker)
set_morale(attacker, 100)

-- Stun
set_stunned(target, true)   -- Stun the target

-- Full combat info
local info = get_combat_info(attacker)
log("HP: " .. info.hp .. "/" .. info.max_hp)
log("Suppression: " .. info.suppression .. " (" .. info.suppression_state .. ")")
log("AP: " .. info.ap)
log("Stunned: " .. tostring(info.stunned))
```

Skill tables contain:
- `name` - Skill ID
- `display_name` - Localized name
- `can_use` - Boolean, whether skill can be used now
- `ap_cost` - Action point cost
- `range` - Maximum range
- `cooldown` - Base cooldown
- `current_cooldown` - Remaining cooldown
- `is_attack` - Boolean, whether it's an attack skill
- `is_passive` - Boolean, whether it's passive

### Tactical State

```lua
-- Round and faction info
local round = get_round()
local faction = get_faction()
local faction_name = get_faction_name(faction)
log("Round " .. round .. " - " .. faction_name .. "'s turn")

-- Check whose turn it is
if is_player_turn() then
    log("Your turn!")
end

-- Pause control
if is_paused() then
    unpause()
else
    pause()
end
toggle_pause()  -- Toggle pause state

-- Turn/round control
end_turn()      -- End current turn
next_round()    -- Skip to next round
next_faction()  -- Skip to next faction

-- Time scale (game speed)
local speed = get_time_scale()
set_time_scale(2.0)  -- 2x speed
set_time_scale(0.5)  -- Half speed

-- Mission status
if is_mission_running() then
    log("Mission active")
end

-- Full tactical state
local state = get_tactical_state()
log("Round: " .. state.round)
log("Faction: " .. state.faction_name)
log("Player turn: " .. tostring(state.is_player_turn))
log("Enemies: " .. state.alive_enemies .. "/" .. state.total_enemies)
log("Active actor: " .. state.active_actor)
```

Tactical state table contains:
- `round` - Current round number
- `faction` - Current faction ID
- `faction_name` - Faction display name
- `is_player_turn` - Boolean
- `is_paused` - Boolean
- `time_scale` - Current game speed
- `mission_running` - Boolean
- `active_actor` - Name of selected actor
- `any_player_alive` - Boolean
- `any_enemy_alive` - Boolean
- `total_enemies`, `dead_enemies`, `alive_enemies` - Enemy counts

### TileMap

```lua
-- Get tile info
local tile = get_tile_info(10, 15)
if tile then
    log("Tile (" .. tile.x .. ", " .. tile.z .. ")")
    log("Elevation: " .. tile.elevation)
    log("Blocked: " .. tostring(tile.blocked))
    log("Visible: " .. tostring(tile.visible))
    if tile.has_actor then
        log("Occupied by: " .. tile.actor_name)
    end
end

-- Cover values (0=None, 1=Light, 2=Medium, 3=Heavy)
local cover = get_cover(10, 15, 0)  -- Cover from north (direction 0)
log("Cover from north: " .. cover)

-- Get cover in all directions
local all_cover = get_all_cover(10, 15)
log("North: " .. all_cover.north)
log("East: " .. all_cover.east)
-- Also accessible by index: all_cover[0] through all_cover[7]

-- Tile queries
if is_blocked(10, 15) then
    log("Tile is blocked")
end

if has_actor_at(10, 15) then
    local actor = get_actor_at(10, 15)
    log("Found: " .. actor.name)
end

if is_visible(10, 15) then
    log("Tile visible to player")
end

-- Map info
local map = get_map_info()
log("Map size: " .. map.width .. "x" .. map.height)
log("Fog of war: " .. tostring(map.fog_of_war))

-- Distance between tiles
local dist = get_distance(0, 0, 10, 10)
log("Distance: " .. dist .. " tiles")
```

Direction constants for cover/facing:
- 0 = North
- 1 = Northeast
- 2 = East
- 3 = Southeast
- 4 = South
- 5 = Southwest
- 6 = West
- 7 = Northwest

### Spawn API (Experimental)

> **Warning:** The spawn API is experimental and may crash the game in some situations. Use with caution.

```lua
-- Spawn a unit at a tile
-- faction: 0=Neutral, 1=Player, 2=PlayerAI, 3=Civilian, 4=AlliedLocalForces,
--          5=EnemyLocalForces, 6=Pirates, 7=Wildlife, 8=Constructs, 9=RogueArmy
local result = spawn_unit("Grunt", 10, 15, 5)  -- Spawn enemy at (10, 15)
if result.success then
    log("Spawned: " .. result.entity.name)
else
    warn("Spawn failed: " .. result.error)
end

-- Spawn player-faction unit
spawn_unit("Grunt", 5, 5, 1)

-- Destroy an entity
local enemy = get_enemy_actors()[1]
if enemy then
    destroy_entity(enemy)          -- With death animation
    destroy_entity(enemy, true)    -- Instant removal
end

-- Clear all enemies
local count = clear_enemies()      -- With animation
local count = clear_enemies(true)  -- Instant
log("Cleared " .. count .. " enemies")

-- List entities by faction
local all = list_entities()        -- All factions
local enemies = list_entities(5)   -- EnemyLocalForces only
local players = list_entities(1)   -- Player faction

for i, entity in ipairs(all) do
    log(entity.name .. " (faction " .. entity.faction .. ")")
end

-- Get detailed entity info
local info = get_entity_info(enemy)
if info then
    log("Entity ID: " .. info.entity_id)
    log("Type: " .. info.type_name)
    log("Faction: " .. info.faction)
    log("Alive: " .. tostring(info.alive))
end
```

Faction constants:
- 0 = Neutral
- 1 = Player
- 2 = PlayerAI
- 3 = Civilian
- 4 = AlliedLocalForces
- 5 = EnemyLocalForces
- 6 = Pirates
- 7 = Wildlife
- 8 = Constructs
- 9 = RogueArmy

### Tile Effects

```lua
-- Get all effects on a tile
local effects = get_tile_effects(10, 15)
for i, effect in ipairs(effects) do
    log(effect.type .. ": " .. effect.template)
    log("  Rounds left: " .. effect.rounds_left)
    log("  Blocks LOS: " .. tostring(effect.blocks_los))
end

-- Quick checks
if is_on_fire(10, 15) then
    log("Tile is burning!")
end

if has_smoke(10, 15) then
    log("Tile has smoke cover")
end

if has_effects(10, 15) then
    log("Tile has some effects")
end

-- Spawn effects
spawn_effect(10, 15, "FireTileEffectTemplate")
spawn_effect(10, 15, "SmokeTileEffectTemplate", 0.5)  -- 0.5 second delay

-- Clear all effects from a tile
local count = clear_tile_effects(10, 15)
log("Cleared " .. count .. " effects")

-- List available effect templates
local templates = get_effect_templates()
for i, name in ipairs(templates) do
    log(name)
end
```

### Inventory & Items

```lua
-- Give item to selected actor
local result = give_item(nil, "weapon.laser_smg")  -- nil = active actor
if result.success then
    log(result.message)
else
    warn("Failed: " .. result.message)
end

-- Give item to specific actor
local actor = find_actor("Leader1")
give_item(actor, "armor.heavy_vest")

-- Get actor's inventory
local items = get_inventory(actor)
for i, item in ipairs(items) do
    log(item.name .. " [" .. item.slot .. "]")
    log("  Value: $" .. item.value)
    log("  Rarity: " .. item.rarity)
    log("  Skills: " .. item.skills)
end

-- Get equipped weapons
local weapons = get_equipped_weapons(actor)
for i, weapon in ipairs(weapons) do
    log("Weapon: " .. weapon.name .. " (" .. weapon.rarity .. ")")
end

-- Get equipped armor
local armor = get_equipped_armor(actor)
if armor then
    log("Armor: " .. armor.name .. " - $" .. armor.value)
end

-- Search for item templates
local templates = get_item_templates("laser")  -- Filter by "laser"
for i, name in ipairs(templates) do
    log(name)
end

-- Get all templates
local all = get_item_templates()
log("Total item templates: " .. #all)
```

Slot types:
- Weapon1, Weapon2
- Armor
- Accessory1, Accessory2
- Consumable1, Consumable2
- Grenade
- VehicleWeapon, VehicleArmor, VehicleAccessory

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

## Example: Advanced Tactical AI Helper

Using the SDK API for more sophisticated automation:

```lua
-- ai_helper.lua
-- Provides tactical analysis and assistance using SDK

-- Analyze the battlefield on each player turn
on("turn_start", function(info)
    if not is_player_turn() then return end

    log("=== TACTICAL ANALYSIS ===")

    local state = get_tactical_state()
    log("Round " .. state.round .. " - Enemies remaining: " .. state.alive_enemies)

    -- Analyze each squad member
    local squad = get_player_actors()
    for i, actor in ipairs(squad) do
        local combat = get_combat_info(actor)
        local pos = get_position(actor)

        log("")
        log(actor.name .. ":")
        log("  HP: " .. combat.hp .. "/" .. combat.max_hp)
        log("  AP: " .. combat.ap)
        log("  Position: (" .. pos.x .. ", " .. pos.y .. ")")

        -- Check cover at current position
        local cover = get_all_cover(pos.x, pos.y)
        local best_cover = math.max(cover[0], cover[1], cover[2], cover[3],
                                     cover[4], cover[5], cover[6], cover[7])
        if best_cover == 0 then
            warn("  WARNING: No cover!")
        elseif best_cover == 3 then
            log("  Cover: Heavy (good)")
        end

        -- Show usable skills
        local skills = get_skills(actor)
        for j, skill in ipairs(skills) do
            if skill.can_use and not skill.is_passive then
                log("  Ready: " .. skill.display_name .. " (AP: " .. skill.ap_cost .. ")")
            end
        end

        -- Warn if suppressed
        if combat.suppression > 33 then
            warn("  Suppression: " .. math.floor(combat.suppression) .. "%")
        end
    end

    -- Find nearby enemies
    local enemies = get_enemy_actors()
    log("")
    log("Visible threats:")
    for i, enemy in ipairs(enemies) do
        local epos = get_position(enemy)
        if epos and is_visible(epos.x, epos.y) then
            -- Find distance to closest squad member
            local min_dist = 999
            for j, ally in ipairs(squad) do
                local apos = get_position(ally)
                if apos then
                    local dist = get_distance(apos.x, apos.y, epos.x, epos.y)
                    if dist < min_dist then
                        min_dist = dist
                    end
                end
            end
            log("  " .. enemy.name .. " at (" .. epos.x .. ", " .. epos.y .. ") - " .. min_dist .. " tiles away")
        end
    end
end)

log("AI Helper loaded!")
```

## Example: Quick Actions

Bind common actions to simple function calls:

```lua
-- quick_actions.lua
-- Helper functions for common tactical actions

-- Heal the most damaged squad member
function heal_weakest()
    local squad = get_player_actors()
    local weakest = nil
    local lowest_hp = 1.0

    for i, actor in ipairs(squad) do
        local hp = get_hp(actor)
        if hp and hp.percent < lowest_hp then
            lowest_hp = hp.percent
            weakest = actor
        end
    end

    if weakest and lowest_hp < 0.5 then
        heal(weakest, 50)
        log("Healed " .. weakest.name .. " (was at " .. math.floor(lowest_hp * 100) .. "%)")
        return true
    else
        log("No one needs healing")
        return false
    end
end

-- Give everyone max AP
function refill_ap()
    local squad = get_player_actors()
    for i, actor in ipairs(squad) do
        set_ap(actor, 100)
    end
    log("Refilled AP for " .. #squad .. " actors")
end

-- Remove all suppression from squad
function clear_suppression()
    local squad = get_player_actors()
    for i, actor in ipairs(squad) do
        set_suppression(actor, 0)
    end
    log("Cleared suppression")
end

-- Speed up the game
function fast_mode()
    set_time_scale(3.0)
    log("Fast mode enabled (3x)")
end

function normal_speed()
    set_time_scale(1.0)
    log("Normal speed")
end

log("Quick actions loaded! Use heal_weakest(), refill_ap(), clear_suppression(), fast_mode()")
```

Then in the console:
```
lua heal_weakest()
lua refill_ap()
lua fast_mode()
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
| Direct game access | Full tactical SDK | Full SDK access |
| Custom UI | No | Yes (IMGUI) |
| Performance | Good | Best |
| Persistence | No | Yes (ModSettings) |
| Actor control | Yes | Yes |
| Combat control | Yes | Yes |
| Tile/map access | Yes | Yes |

**Use Lua when:** You want quick scripts, tactical automation, event-driven logic, or are new to modding.

**Use C# when:** You need custom UI, complex patching, performance-critical code, or persistent settings.

## Debugging

1. Check the MelonLoader console for `[Lua]` messages
2. Use `luascripts` to verify your scripts loaded
3. Use `luaevents` to see registered handlers
4. Test commands manually with `lua cmd("yourcommand")`
5. Test SDK functions: `lua log(get_round())` or `lua for i,a in ipairs(get_actors()) do log(a.name) end`

## Complete API Reference

### Core Functions

| Function | Returns | Description |
|----------|---------|-------------|
| `cmd(command)` | `{success, result, data}` | Execute console command |
| `log(message)` | - | Log info message |
| `warn(message)` | - | Log warning |
| `error(message)` | - | Log error |
| `on(event, callback)` | - | Register event handler |
| `off(event, callback)` | - | Unregister event handler |
| `emit(event, args...)` | - | Fire custom event |
| `commands()` | `table` | Get all command names |
| `has_command(name)` | `boolean` | Check if command exists |

### Actor Query

| Function | Returns | Description |
|----------|---------|-------------|
| `get_actors()` | `[actor, ...]` | Get all actors |
| `get_player_actors()` | `[actor, ...]` | Get player-controlled actors |
| `get_enemy_actors()` | `[actor, ...]` | Get enemy actors |
| `find_actor(name)` | `actor` or `nil` | Find actor by name |
| `get_active_actor()` | `actor` or `nil` | Get selected actor |

### Movement

| Function | Returns | Description |
|----------|---------|-------------|
| `move_to(actor, x, y)` | `{success, error}` | Move actor to tile |
| `teleport(actor, x, y)` | `{success, error}` | Teleport instantly |
| `get_position(actor)` | `{x, y}` or `nil` | Get actor position |
| `get_ap(actor)` | `number` | Get action points |
| `set_ap(actor, ap)` | `boolean` | Set action points |
| `get_facing(actor)` | `number` (0-7) | Get facing direction |
| `set_facing(actor, dir)` | `boolean` | Set facing direction |
| `is_moving(actor)` | `boolean` | Check if moving |

### Combat

| Function | Returns | Description |
|----------|---------|-------------|
| `attack(attacker, target)` | `{success, error, skill, damage}` | Attack target |
| `use_ability(actor, skill, target?)` | `{success, error, skill}` | Use ability |
| `get_skills(actor)` | `[skill, ...]` | Get actor's skills |
| `get_hp(actor)` | `{current, max, percent}` | Get HP info |
| `set_hp(actor, hp)` | `boolean` | Set HP value |
| `damage(actor, amount)` | `boolean` | Apply damage |
| `heal(actor, amount)` | `boolean` | Heal actor |
| `get_suppression(actor)` | `number` (0-100) | Get suppression |
| `set_suppression(actor, value)` | `boolean` | Set suppression |
| `get_morale(actor)` | `number` | Get morale |
| `set_morale(actor, value)` | `boolean` | Set morale |
| `set_stunned(actor, bool)` | `boolean` | Set stunned state |
| `get_combat_info(actor)` | `table` | Get full combat info |

### Tactical State

| Function | Returns | Description |
|----------|---------|-------------|
| `get_round()` | `number` | Get current round |
| `get_faction()` | `number` | Get current faction ID |
| `get_faction_name(id)` | `string` | Get faction name |
| `is_player_turn()` | `boolean` | Check if player's turn |
| `is_paused()` | `boolean` | Check if paused |
| `pause(bool?)` | `boolean` | Pause (default: true) |
| `unpause()` | `boolean` | Unpause |
| `toggle_pause()` | `boolean` | Toggle pause |
| `end_turn()` | `boolean` | End current turn |
| `next_round()` | `boolean` | Advance to next round |
| `next_faction()` | `boolean` | Advance to next faction |
| `get_time_scale()` | `number` | Get game speed |
| `set_time_scale(scale)` | `boolean` | Set game speed |
| `get_tactical_state()` | `table` | Get full tactical state |
| `is_mission_running()` | `boolean` | Check if mission active |

### TileMap

| Function | Returns | Description |
|----------|---------|-------------|
| `get_tile_info(x, z)` | `table` or `nil` | Get tile info |
| `get_cover(x, z, dir)` | `number` (0-3) | Get cover in direction |
| `get_all_cover(x, z)` | `table` | Get cover all directions |
| `is_blocked(x, z)` | `boolean` | Check if impassable |
| `has_actor_at(x, z)` | `boolean` | Check for actor |
| `is_visible(x, z)` | `boolean` | Check player visibility |
| `get_map_info()` | `{width, height, fog_of_war}` | Get map info |
| `get_actor_at(x, z)` | `actor` or `nil` | Get actor on tile |
| `get_distance(x1, z1, x2, z2)` | `number` | Get tile distance |

### Spawn (Experimental)

| Function | Returns | Description |
|----------|---------|-------------|
| `spawn_unit(template, x, y, faction?)` | `{success, error, entity}` | Spawn unit at tile |
| `destroy_entity(actor, immediate?)` | `boolean` | Kill an entity |
| `clear_enemies(immediate?)` | `number` | Clear all enemies |
| `list_entities(faction?)` | `[actor, ...]` | List entities by faction |
| `get_entity_info(actor)` | `table` or `nil` | Get entity info |

### Tile Effects

| Function | Returns | Description |
|----------|---------|-------------|
| `get_tile_effects(x, z)` | `[effect, ...]` | Get effects on tile |
| `has_effects(x, z)` | `boolean` | Check for any effects |
| `is_on_fire(x, z)` | `boolean` | Check if tile burning |
| `has_smoke(x, z)` | `boolean` | Check for smoke |
| `spawn_effect(x, z, template, delay?)` | `boolean` | Spawn effect on tile |
| `clear_tile_effects(x, z)` | `number` | Remove all effects |
| `get_effect_templates()` | `[string, ...]` | List effect templates |

### Inventory

| Function | Returns | Description |
|----------|---------|-------------|
| `give_item(actor?, template)` | `{success, message}` | Give item to actor |
| `get_inventory(actor?)` | `[item, ...]` | Get all items |
| `get_equipped_weapons(actor?)` | `[weapon, ...]` | Get equipped weapons |
| `get_equipped_armor(actor?)` | `table` or `nil` | Get equipped armor |
| `get_item_templates(filter?)` | `[string, ...]` | List item templates |

### Black Market

| Function | Returns | Description |
|----------|---------|-------------|
| `blackmarket_stock(template)` | `{success, message}` | Add item to black market |
| `blackmarket_has(template)` | `boolean` | Check if item in stock |

### Events

#### Lifecycle Events

| Event | Callback Args | Description |
|-------|---------------|-------------|
| `scene_loaded` | `sceneName` | Scene loaded |
| `tactical_ready` | - | Battle ready |
| `mission_start` | `{name, biome, difficulty}` | Mission started |

#### Combat Events

| Event | Callback Args | Description |
|-------|---------------|-------------|
| `actor_killed` | `{actor, actor_ptr, killer, killer_ptr, faction}` | Actor dies |
| `damage_received` | `{target, target_ptr, attacker, attacker_ptr, skill, skill_ptr}` | Actor takes damage |
| `attack_missed` | `{attacker, attacker_ptr, target, target_ptr}` | Attack misses |
| `attack_start` | `{attacker, attacker_ptr, tile_ptr}` | Attack begins |
| `bleeding_out` | `{actor, actor_ptr}` | Actor starts bleeding out |
| `stabilized` | `{actor, actor_ptr}` | Actor stabilized |
| `suppressed` | `{actor, actor_ptr}` | Actor fully suppressed |
| `suppression_applied` | `{target, target_ptr, attacker, attacker_ptr, amount}` | Suppression applied |

#### Actor State Events

| Event | Callback Args | Description |
|-------|---------------|-------------|
| `actor_state_changed` | `{actor, actor_ptr}` | Actor state changes |
| `morale_changed` | `{actor, actor_ptr, state}` | Morale state changes |
| `hp_changed` | `{actor, actor_ptr, old_hp, new_hp, delta}` | HP changes |
| `armor_changed` | `{actor, actor_ptr}` | Armor changes |
| `ap_changed` | `{actor, actor_ptr, old_ap, new_ap, delta}` | Action points change |

#### Visibility Events

| Event | Callback Args | Description |
|-------|---------------|-------------|
| `discovered` | `{discovered, discovered_ptr, discoverer, discoverer_ptr}` | Entity discovered |
| `visible_to_player` | `{entity, entity_ptr}` | Entity becomes visible |
| `hidden_from_player` | `{entity, entity_ptr}` | Entity becomes hidden |

#### Movement Events

| Event | Callback Args | Description |
|-------|---------------|-------------|
| `move_start` | `{actor, actor_ptr, from_tile_ptr, to_tile_ptr, action}` | Movement begins |
| `move_complete` | `{actor, actor_ptr, tile_ptr}` | Movement finishes |

#### Skill Events

| Event | Callback Args | Description |
|-------|---------------|-------------|
| `skill_used` | `{user, user_ptr, skill, skill_ptr}` | Skill is used |
| `skill_complete` | `{skill, skill_ptr}` | Skill execution finishes |
| `skill_added` | `{actor, actor_ptr, skill, skill_ptr}` | Skill added to actor |
| `offmap_ability_used` | `{ability, ability_ptr}` | Offmap ability used |
| `offmap_ability_canceled` | `{ability, ability_ptr}` | Offmap ability canceled |

#### Turn/Round Events

| Event | Callback Args | Description |
|-------|---------------|-------------|
| `turn_start` | `{faction, factionName}` | Turn started |
| `turn_end` | `{faction, factionName}` | Turn ended |
| `round_start` | `{round}` | New round begins |

#### Entity Events

| Event | Callback Args | Description |
|-------|---------------|-------------|
| `entity_spawned` | `{entity, entity_ptr}` | Entity spawns |
| `element_destroyed` | `{element, element_ptr}` | Element destroyed (vehicles, objects) |
| `element_malfunction` | `{element, element_ptr}` | Element malfunctions |

#### Mission Events

| Event | Callback Args | Description |
|-------|---------------|-------------|
| `objective_changed` | `{objective, objective_ptr, state}` | Objective state changes |

#### Strategy Events (Lifecycle)

| Event | Callback Args | Description |
|-------|---------------|-------------|
| `campaign_start` | - | New campaign started |
| `campaign_loaded` | - | Saved campaign loaded |
| `operation_end` | - | Operation completed |
| `operation_finished` | - | Operation fully completed |
| `mission_ended` | `{mission_ptr, status}` | Mission ended |

#### Roster Events

| Event | Callback Args | Description |
|-------|---------------|-------------|
| `leader_hired` | `{leader, leader_ptr, template}` | Leader joins roster |
| `leader_dismissed` | `{leader, leader_ptr}` | Leader dismissed |
| `leader_permadeath` | `{leader, leader_ptr}` | Leader dies permanently |
| `leader_levelup` | `{leader, leader_ptr, perk}` | Leader gains a perk |
| `squaddie_killed` | `{squaddie_id}` | Squaddie dies |
| `squaddie_added` | `{squaddie, alive_count}` | Squaddie added |

#### Faction Events

| Event | Callback Args | Description |
|-------|---------------|-------------|
| `faction_trust_changed` | `{faction, faction_ptr, delta}` | Faction trust changes |
| `faction_status_changed` | `{faction, faction_ptr, status}` | Faction status changes |
| `faction_upgrade_unlocked` | `{faction, faction_ptr, upgrade, upgrade_ptr}` | Faction upgrade unlocked |

#### Black Market Events

| Event | Callback Args | Description |
|-------|---------------|-------------|
| `blackmarket_refresh` | - | Black market restocking (pre) |
| `blackmarket_restocked` | - | Black market restocked (post) |
| `blackmarket_item_added` | `{item, item_ptr}` | Item added to market |

### Example: Reacting to Combat Events

```lua
-- Log all kills
on("actor_killed", function(data)
    log(data.actor .. " was killed by " .. data.killer)
end)

-- Track damage
on("damage_received", function(data)
    log(data.target .. " took damage from " .. data.attacker .. " using " .. data.skill)
end)

-- React to HP changes
on("hp_changed", function(data)
    if data.delta < 0 then
        log(data.actor .. " lost " .. (-data.delta) .. " HP")
        if data.new_hp < 20 then
            warn(data.actor .. " is critically wounded!")
        end
    else
        log(data.actor .. " healed " .. data.delta .. " HP")
    end
end)

-- Track movement
on("move_start", function(data)
    log(data.actor .. " is moving")
end)

on("move_complete", function(data)
    log(data.actor .. " finished moving")
end)

-- Track rounds
on("round_start", function(data)
    log("=== ROUND " .. data.round .. " ===")
end)
```

### Example: Reacting to Strategy Events

```lua
-- Track roster changes
on("leader_hired", function(data)
    log("Welcome to the team: " .. data.leader)
end)

on("leader_permadeath", function(data)
    warn("We lost " .. data.leader .. " forever...")
end)

on("leader_levelup", function(data)
    log(data.leader .. " gained perk: " .. data.perk)
end)

-- Track faction relations
on("faction_trust_changed", function(data)
    if data.delta > 0 then
        log(data.faction .. " trust increased by " .. data.delta)
    else
        warn(data.faction .. " trust decreased by " .. (-data.delta))
    end
end)

on("faction_upgrade_unlocked", function(data)
    log("Unlocked " .. data.upgrade .. " from " .. data.faction)
end)

-- Track black market
on("blackmarket_restocked", function()
    log("Black market has new items!")
end)

-- Track operations
on("mission_ended", function(data)
    log("Mission complete! Status: " .. data.status)
end)
```

### Logging

```lua
log("Normal message")      -- White text
warn("Warning message")    -- Yellow text
error("Error message")     -- Red text
```

All messages are prefixed with `[Lua]` in the console.

---

**Previous:** [Audio](06-audio.md) | **Next:** [SDK Getting Started](../coding-sdk/getting-started.md)
