-- =============================================================================
-- Tactical Helper - Comprehensive tactical event handling
-- =============================================================================
-- Demonstrates all tactical combat events:
--   - Combat events (damage, kills, misses)
--   - Movement events
--   - Skill and ability events
--   - Status effect events
--   - Mission lifecycle events
-- =============================================================================

log("[TacticalHelper] Loading...")

-- =============================================================================
-- MISSION TRACKING
-- =============================================================================

local missionActive = false
local killCount = 0
local damageDealt = 0
local turnCount = 0

on("tactical_ready", function()
    log("[TacticalHelper] === TACTICAL MISSION STARTED ===")
    missionActive = true
    killCount = 0
    damageDealt = 0
    turnCount = 0
end)

on("mission_ended", function(data)
    log("[TacticalHelper] === MISSION ENDED ===")
    if data then
        log("  Result: " .. (data.success and "VICTORY" or "DEFEAT"))
    end
    log("  Total kills: " .. killCount)
    log("  Total damage: " .. damageDealt)
    log("  Turns taken: " .. turnCount)
    missionActive = false
end)

-- =============================================================================
-- TURN EVENTS
-- =============================================================================

on("turn_start", function(data)
    if not data then return end

    local faction = data.faction or -1
    turnCount = turnCount + 1

    if faction == 0 then
        log("[TacticalHelper] === YOUR TURN ===")
        -- You could add turn start logic here:
        -- - Highlight available actions
        -- - Show tactical suggestions
        -- - Update UI elements
    else
        log("[TacticalHelper] Enemy faction " .. faction .. "'s turn")
    end
end)

on("turn_end", function(data)
    if not data then return end
    -- Turn cleanup logic here
end)

on("round_start", function(data)
    if data then
        log("[TacticalHelper] --- Round " .. (data.round or "?") .. " ---")
    end
end)

-- =============================================================================
-- COMBAT EVENTS
-- =============================================================================

on("actor_killed", function(data)
    if not data then return end

    killCount = killCount + 1

    local victim = data.actor or "unknown"
    local killer = data.killer or "unknown"

    log("[TacticalHelper] KILL: " .. tostring(victim) .. " killed by " .. tostring(killer))

    -- You could add kill tracking logic here:
    -- - Track kill streaks
    -- - Award bonuses
    -- - Update UI
end)

on("damage_received", function(data)
    if not data then return end

    local target = data.actor or "unknown"
    local amount = data.damage or 0
    local source = data.source or "unknown"
    local damageType = data.type or "normal"

    damageDealt = damageDealt + amount

    log("[TacticalHelper] DAMAGE: " .. tostring(target) ..
        " took " .. amount .. " " .. damageType .. " damage from " .. tostring(source))
end)

on("attack_missed", function(data)
    if not data then return end

    local attacker = data.actor or "unknown"
    local target = data.target or "unknown"

    log("[TacticalHelper] MISS: " .. tostring(attacker) .. " missed " .. tostring(target))
end)

on("critical_hit", function(data)
    if not data then return end
    log("[TacticalHelper] CRITICAL HIT!")
end)

-- =============================================================================
-- MOVEMENT EVENTS
-- =============================================================================

on("move_start", function(data)
    if not data then return end

    local actor = data.actor or "unknown"
    -- data.from and data.to contain position info

    log("[TacticalHelper] Moving: " .. tostring(actor))
end)

on("move_complete", function(data)
    if not data then return end

    local actor = data.actor or "unknown"
    log("[TacticalHelper] Move complete: " .. tostring(actor))
end)

-- =============================================================================
-- SKILL AND ABILITY EVENTS
-- =============================================================================

on("skill_used", function(data)
    if not data then return end

    local actor = data.actor or "unknown"
    local skill = data.skill or "unknown"

    log("[TacticalHelper] Skill used: " .. tostring(actor) .. " used " .. tostring(skill))
end)

on("overwatch_triggered", function(data)
    if not data then return end

    local actor = data.actor or "unknown"
    local target = data.target or "unknown"

    log("[TacticalHelper] OVERWATCH: " .. tostring(actor) .. " firing at " .. tostring(target))
end)

on("grenade_thrown", function(data)
    if not data then return end
    log("[TacticalHelper] Grenade thrown!")
end)

-- =============================================================================
-- STATUS EVENTS
-- =============================================================================

on("hp_changed", function(data)
    if not data then return end

    local actor = data.actor or "unknown"
    local oldHp = data.old or 0
    local newHp = data.new or 0
    local delta = newHp - oldHp

    if delta < 0 then
        -- Damage taken (already handled by damage_received usually)
    elseif delta > 0 then
        log("[TacticalHelper] HEALED: " .. tostring(actor) .. " +" .. delta .. " HP")
    end
end)

on("ap_changed", function(data)
    if not data then return end
    -- Action points changed - useful for tracking actions
end)

on("morale_changed", function(data)
    if not data then return end

    local actor = data.actor or "unknown"
    local oldMorale = data.old or 0
    local newMorale = data.new or 0
    local delta = newMorale - oldMorale

    if delta < -10 then
        log("[TacticalHelper] MORALE BROKEN: " .. tostring(actor) .. " is panicking!")
    elseif delta < 0 then
        log("[TacticalHelper] Morale dropped: " .. tostring(actor) .. " (" .. delta .. ")")
    elseif delta > 10 then
        log("[TacticalHelper] MORALE SURGE: " .. tostring(actor) .. " is inspired!")
    end
end)

-- =============================================================================
-- SPAWN EVENTS
-- =============================================================================

on("actor_spawned", function(data)
    if not data then return end

    local actor = data.actor or "unknown"
    log("[TacticalHelper] Unit spawned: " .. tostring(actor))
end)

on("entity_spawned", function(data)
    if not data then return end

    local entity = data.entity or "unknown"
    local entityType = data.type or "unknown"
    log("[TacticalHelper] Entity spawned: " .. tostring(entity) .. " (" .. entityType .. ")")
end)

on("reinforcements_spawned", function(data)
    log("[TacticalHelper] === REINFORCEMENTS INCOMING ===")
end)

-- =============================================================================
-- CONSOLE COMMANDS
-- =============================================================================

cmd("tactical_stats", function()
    log("=== TACTICAL STATS ===")
    log("Mission active: " .. tostring(missionActive))
    log("Kills: " .. killCount)
    log("Damage dealt: " .. damageDealt)
    log("Turns: " .. turnCount)

    if type(get_player_actors) == "function" then
        local actors = get_player_actors()
        if actors then
            log("Player units: " .. #actors)
        end
    end

    if type(get_enemy_actors) == "function" then
        local enemies = get_enemy_actors()
        if enemies then
            log("Enemy units: " .. #enemies)
        end
    end

    return "Stats printed to log"
end)

log("[TacticalHelper] Loaded successfully!")
