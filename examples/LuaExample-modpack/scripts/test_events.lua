-- =============================================================================
-- Event Testing Script
-- =============================================================================
-- Provides testing functions to verify the Lua API is working correctly.
-- Use these functions via the REPL or console commands to test functionality.
-- =============================================================================

log("[EventTest] Loading test functions...")

-- =============================================================================
-- API AVAILABILITY TESTS
-- =============================================================================

-- Test what API functions are available
function test_api()
    log("=== API AVAILABILITY TEST ===")
    log("")

    -- Core functions (should always exist)
    log("CORE FUNCTIONS:")
    log("  log: " .. type(log))
    log("  on: " .. type(on))
    log("  cmd: " .. type(cmd))
    log("")

    -- Tactical API functions
    log("TACTICAL API:")
    log("  is_mission_running: " .. type(is_mission_running))
    log("  get_player_actors: " .. type(get_player_actors))
    log("  get_enemy_actors: " .. type(get_enemy_actors))
    log("  get_all_actors: " .. type(get_all_actors))
    log("  get_selected_actor: " .. type(get_selected_actor))
    log("  get_actor_hp: " .. type(get_actor_hp))
    log("  get_actor_ap: " .. type(get_actor_ap))
    log("  get_actor_position: " .. type(get_actor_position))
    log("")

    -- Strategy API functions
    log("STRATEGY API:")
    log("  get_current_leaders: " .. type(get_current_leaders))
    log("  get_faction_trust: " .. type(get_faction_trust))
    log("  get_resources: " .. type(get_resources))
    log("")

    return "API test complete - check log"
end

-- =============================================================================
-- TACTICAL TESTS
-- =============================================================================

-- Test tactical mission functions (run during a mission)
function test_tactical()
    log("=== TACTICAL TEST ===")
    log("")

    -- Check if mission is running
    if type(is_mission_running) ~= "function" then
        log("ERROR: is_mission_running not available")
        return "Test failed - function not available"
    end

    local running = is_mission_running()
    log("Mission running: " .. tostring(running))

    if not running then
        log("NOTE: Run this test during a tactical mission")
        return "Not in mission"
    end

    -- Get player actors
    if type(get_player_actors) == "function" then
        local players = get_player_actors()
        if players then
            log("Player actors: " .. #players)
            for i, actor in ipairs(players) do
                log("  [" .. i .. "] " .. tostring(actor))

                -- Try to get actor stats
                if type(get_actor_hp) == "function" then
                    local hp = get_actor_hp(actor)
                    log("      HP: " .. tostring(hp))
                end

                if type(get_actor_ap) == "function" then
                    local ap = get_actor_ap(actor)
                    log("      AP: " .. tostring(ap))
                end
            end
        else
            log("Player actors: nil")
        end
    end

    -- Get enemy actors
    if type(get_enemy_actors) == "function" then
        local enemies = get_enemy_actors()
        if enemies then
            log("Enemy actors: " .. #enemies)
        else
            log("Enemy actors: nil")
        end
    end

    -- Get selected actor
    if type(get_selected_actor) == "function" then
        local selected = get_selected_actor()
        log("Selected actor: " .. tostring(selected))
    end

    return "Tactical test complete"
end

-- =============================================================================
-- EVENT REGISTRATION TEST
-- =============================================================================

-- Track which events have fired
local eventsFired = {}

-- Register test handlers for all events
function test_register_all()
    log("=== REGISTERING TEST HANDLERS ===")

    local events = {
        -- Tactical events
        "tactical_ready", "turn_start", "turn_end", "round_start", "round_end",
        "actor_killed", "actor_spawned", "damage_received", "attack_missed",
        "move_start", "move_complete", "skill_used", "hp_changed", "ap_changed",
        "morale_changed", "overwatch_triggered", "critical_hit", "grenade_thrown",
        "entity_spawned", "reinforcements_spawned",

        -- Strategy events
        "campaign_start", "leader_hired", "leader_dismissed", "leader_permadeath",
        "leader_levelup", "faction_trust_changed", "faction_status_changed",
        "faction_upgrade_unlocked", "squaddie_killed", "squaddie_added",
        "mission_ended", "operation_finished", "blackmarket_item_added",
        "blackmarket_restocked",

        -- General events
        "scene_loaded"
    }

    for _, eventName in ipairs(events) do
        on(eventName, function(data)
            eventsFired[eventName] = (eventsFired[eventName] or 0) + 1
            log("[TEST] Event fired: " .. eventName .. " (count: " .. eventsFired[eventName] .. ")")
        end)
    end

    log("Registered " .. #events .. " test handlers")
    return "Test handlers registered"
end

-- Show which events have fired
function test_show_fired()
    log("=== EVENTS FIRED ===")

    local count = 0
    for event, times in pairs(eventsFired) do
        log("  " .. event .. ": " .. times .. " time(s)")
        count = count + 1
    end

    if count == 0 then
        log("  (no events fired yet)")
    end

    return count .. " unique events fired"
end

-- Reset event tracking
function test_reset()
    eventsFired = {}
    log("Event tracking reset")
    return "Reset complete"
end

-- =============================================================================
-- CONSOLE COMMANDS
-- =============================================================================

cmd("test_api", function()
    return test_api()
end)

cmd("test_tactical", function()
    return test_tactical()
end)

cmd("test_events", function()
    return test_show_fired()
end)

cmd("test_register", function()
    return test_register_all()
end)

cmd("test_reset", function()
    return test_reset()
end)

-- =============================================================================
-- AUTO-REGISTER TEST HANDLERS
-- =============================================================================

-- Automatically register test handlers on load
test_register_all()

log("[EventTest] Test functions loaded!")
log("[EventTest] Available test commands:")
log("  test_api      - Check API function availability")
log("  test_tactical - Test tactical mission functions")
log("  test_events   - Show which events have fired")
log("  test_register - Re-register all test handlers")
log("  test_reset    - Reset event tracking")
