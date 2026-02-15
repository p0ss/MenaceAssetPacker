-- =============================================================================
-- Strategy Helper - Campaign and strategy layer event handling
-- =============================================================================
-- Demonstrates all strategy layer events:
--   - Campaign lifecycle events
--   - Leader management events
--   - Faction relationship events
--   - Squaddie events
--   - Economy and progression events
-- =============================================================================

log("[StrategyHelper] Loading...")

-- =============================================================================
-- CAMPAIGN STATE TRACKING
-- =============================================================================

local campaignActive = false
local leadersHired = 0
local leadersLost = 0
local missionsCompleted = 0
local missionsWon = 0

-- =============================================================================
-- CAMPAIGN EVENTS
-- =============================================================================

on("campaign_start", function()
    log("[StrategyHelper] === NEW CAMPAIGN STARTED ===")
    campaignActive = true
    leadersHired = 0
    leadersLost = 0
    missionsCompleted = 0
    missionsWon = 0
end)

-- =============================================================================
-- LEADER EVENTS
-- =============================================================================

on("leader_hired", function(data)
    if not data then return end

    leadersHired = leadersHired + 1

    local leader = data.leader or "unknown"
    log("[StrategyHelper] HIRED: Leader " .. tostring(leader))
    log("[StrategyHelper] Total leaders hired this campaign: " .. leadersHired)

    -- You could add hiring logic here:
    -- - Welcome message
    -- - Initial loadout setup
    -- - Tutorial tips for new players
end)

on("leader_dismissed", function(data)
    if not data then return end

    local leader = data.leader or "unknown"
    log("[StrategyHelper] DISMISSED: Leader " .. tostring(leader))

    -- You could add dismissal logic here:
    -- - Farewell message
    -- - Reputation effects
    -- - Stat tracking
end)

on("leader_permadeath", function(data)
    if not data then return end

    leadersLost = leadersLost + 1

    local leader = data.leader or "unknown"
    log("[StrategyHelper] === PERMADEATH ===")
    log("[StrategyHelper] Lost: " .. tostring(leader))
    log("[StrategyHelper] Total leaders lost: " .. leadersLost)

    -- You could add permadeath logic here:
    -- - Memorial message
    -- - Morale effects on other leaders
    -- - Achievement tracking
end)

on("leader_levelup", function(data)
    if not data then return end

    local leader = data.leader or "unknown"
    log("[StrategyHelper] LEVEL UP: " .. tostring(leader) .. " gained a perk!")

    -- You could add level up logic here:
    -- - Congratulations message
    -- - Perk recommendations
    -- - Stat tracking
end)

-- =============================================================================
-- FACTION EVENTS
-- =============================================================================

on("faction_trust_changed", function(data)
    if not data then return end

    local faction = data.faction or "unknown"
    local delta = data.delta or 0

    if delta > 0 then
        log("[StrategyHelper] TRUST UP: Faction " .. tostring(faction) .. " (+" .. delta .. ")")
    elseif delta < 0 then
        log("[StrategyHelper] TRUST DOWN: Faction " .. tostring(faction) .. " (" .. delta .. ")")
    end

    -- Check for relationship milestones
    -- You could trigger special events at certain trust levels
end)

on("faction_status_changed", function(data)
    if not data then return end

    local faction = data.faction or "unknown"
    local status = data.status or "unknown"

    log("[StrategyHelper] Faction " .. tostring(faction) .. " status: " .. tostring(status))

    -- Status changes might indicate:
    -- - Alliance formed
    -- - War declared
    -- - Neutrality established
end)

on("faction_upgrade_unlocked", function(data)
    if not data then return end

    local faction = data.faction or "unknown"
    local upgrade = data.upgrade or "unknown"

    log("[StrategyHelper] UPGRADE: " .. tostring(faction) .. " unlocked " .. tostring(upgrade))
end)

-- =============================================================================
-- SQUADDIE EVENTS
-- =============================================================================

on("squaddie_killed", function(data)
    if not data then return end

    local id = data.id or "unknown"
    log("[StrategyHelper] Squaddie lost: " .. tostring(id))

    -- Track squaddie losses for statistics
end)

on("squaddie_added", function(data)
    if not data then return end

    local id = data.id or "unknown"
    log("[StrategyHelper] New squaddie: " .. tostring(id))

    -- Welcome new recruits
end)

-- =============================================================================
-- MISSION EVENTS
-- =============================================================================

on("mission_ended", function(data)
    if not data then return end

    missionsCompleted = missionsCompleted + 1

    local success = data.success or false
    if success then
        missionsWon = missionsWon + 1
        log("[StrategyHelper] Mission VICTORY!")
    else
        log("[StrategyHelper] Mission DEFEAT")
    end

    local winRate = (missionsWon / missionsCompleted) * 100
    log("[StrategyHelper] Win rate: " .. string.format("%.1f%%", winRate) ..
        " (" .. missionsWon .. "/" .. missionsCompleted .. ")")
end)

on("operation_finished", function()
    log("[StrategyHelper] Operation completed")
end)

-- =============================================================================
-- ECONOMY EVENTS
-- =============================================================================

on("blackmarket_item_added", function(data)
    if not data then return end

    local item = data.item or "unknown"
    log("[StrategyHelper] New item in Black Market: " .. tostring(item))
end)

on("blackmarket_restocked", function()
    log("[StrategyHelper] Black Market restocked!")
end)

-- =============================================================================
-- CONSOLE COMMANDS
-- =============================================================================

cmd("campaign_stats", function()
    log("=== CAMPAIGN STATS ===")
    log("Campaign active: " .. tostring(campaignActive))
    log("Leaders hired: " .. leadersHired)
    log("Leaders lost: " .. leadersLost)
    log("Missions completed: " .. missionsCompleted)
    log("Missions won: " .. missionsWon)

    if missionsCompleted > 0 then
        local winRate = (missionsWon / missionsCompleted) * 100
        log("Win rate: " .. string.format("%.1f%%", winRate))
    end

    return "Stats printed to log"
end)

log("[StrategyHelper] Loaded successfully!")
