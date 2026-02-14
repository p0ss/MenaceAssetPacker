-- ============================================================================
-- Tactical Helper - Auto-helper for combat
-- ============================================================================
-- This script provides helpful information during tactical battles.
-- ============================================================================

local tactical_entered = false

-- Track when we enter/exit tactical mode
on("scene_loaded", function(sceneName)
    if string.find(sceneName, "Tactical") then
        tactical_entered = true
    else
        tactical_entered = false
    end
end)

-- When tactical battle is ready, show a summary
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

    log("================================")
end)

-- At the start of player turn, show helpful info
on("turn_start", function(info)
    if info.faction ~= 0 then return end  -- Only for player turn

    log("--- YOUR TURN ---")

    -- Show actor count
    local actors = cmd("actors 0")  -- Faction 0 = player
    if actors.success then
        log(actors.result)
    end
end)

-- At end of player turn, show a summary
on("turn_end", function(info)
    if info.faction ~= 0 then return end

    log("--- TURN COMPLETE ---")
end)

log("Tactical Helper loaded!")
