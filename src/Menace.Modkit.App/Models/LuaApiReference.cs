using System.Collections.Generic;

namespace Menace.Modkit.App.Models;

/// <summary>
/// Represents a Lua API function or event for the reference panel.
/// </summary>
public class LuaApiItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string InsertText { get; set; } = string.Empty;
    public string? DocLink { get; set; }
    public LuaApiItemType ItemType { get; set; }
    public List<LuaApiItem> Children { get; set; } = new();

    /// <summary>
    /// For tree expansion state.
    /// </summary>
    public bool IsExpanded { get; set; } = true;

    /// <summary>
    /// True if this is a category node (not insertable).
    /// </summary>
    public bool IsCategory => Children.Count > 0;
}

public enum LuaApiItemType
{
    Category,
    Function,
    Event
}

/// <summary>
/// Provides the Lua API reference data for the Code tab.
/// </summary>
public static class LuaApiReference
{
    public static List<LuaApiItem> GetApiTree()
    {
        return new List<LuaApiItem>
        {
            GetCoreFunctions(),
            GetActorQueryApi(),
            GetMovementApi(),
            GetCombatApi(),
            GetTacticalStateApi(),
            GetTileMapApi(),
            GetSpawnApi(),
            GetTileEffectsApi(),
            GetInventoryApi(),
            GetBlackMarketApi(),
            GetTacticalEvents(),
            GetStrategyEvents(),
            GetGeneralEvents()
        };
    }

    private static LuaApiItem GetCoreFunctions()
    {
        return new LuaApiItem
        {
            Name = "Core Functions",
            ItemType = LuaApiItemType.Category,
            Children = new List<LuaApiItem>
            {
                new()
                {
                    Name = "log",
                    Description = "Print a message to the game log",
                    Signature = "log(message)",
                    InsertText = "log(\"\")",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "on",
                    Description = "Register an event handler",
                    Signature = "on(eventName, callback)",
                    InsertText = "on(\"event_name\", function(data)\n    \nend)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "cmd",
                    Description = "Register a console command",
                    Signature = "cmd(name, callback)",
                    InsertText = "cmd(\"my_command\", function(args)\n    log(\"Command executed\")\n    return \"Done\"\nend)",
                    ItemType = LuaApiItemType.Function
                }
            }
        };
    }

    private static LuaApiItem GetActorQueryApi()
    {
        return new LuaApiItem
        {
            Name = "Actor Query",
            ItemType = LuaApiItemType.Category,
            IsExpanded = false,
            Children = new List<LuaApiItem>
            {
                new()
                {
                    Name = "get_actors",
                    Description = "Get all actors in the mission",
                    Signature = "get_actors() -> table",
                    InsertText = "local actors = get_actors()\nfor i, actor in ipairs(actors) do\n    log(actor.name)\nend",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_player_actors",
                    Description = "Get player-controlled actors",
                    Signature = "get_player_actors() -> table",
                    InsertText = "local players = get_player_actors()",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_enemy_actors",
                    Description = "Get enemy actors",
                    Signature = "get_enemy_actors() -> table",
                    InsertText = "local enemies = get_enemy_actors()",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "find_actor",
                    Description = "Find actor by name",
                    Signature = "find_actor(name) -> actor",
                    InsertText = "local actor = find_actor(\"ActorName\")",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_active_actor",
                    Description = "Get currently selected actor",
                    Signature = "get_active_actor() -> actor",
                    InsertText = "local actor = get_active_actor()",
                    ItemType = LuaApiItemType.Function
                }
            }
        };
    }

    private static LuaApiItem GetMovementApi()
    {
        return new LuaApiItem
        {
            Name = "Movement",
            ItemType = LuaApiItemType.Category,
            IsExpanded = false,
            Children = new List<LuaApiItem>
            {
                new()
                {
                    Name = "move_to",
                    Description = "Move actor to tile position",
                    Signature = "move_to(actor, x, y) -> {success, error}",
                    InsertText = "local result = move_to(actor, x, y)\nif result.success then log(\"Moving\") end",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "teleport",
                    Description = "Teleport actor instantly",
                    Signature = "teleport(actor, x, y) -> {success, error}",
                    InsertText = "teleport(actor, x, y)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_position",
                    Description = "Get actor's tile position",
                    Signature = "get_position(actor) -> {x, y}",
                    InsertText = "local pos = get_position(actor)\nlog(\"Position: \" .. pos.x .. \", \" .. pos.y)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_ap",
                    Description = "Get remaining action points",
                    Signature = "get_ap(actor) -> number",
                    InsertText = "local ap = get_ap(actor)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "set_ap",
                    Description = "Set action points",
                    Signature = "set_ap(actor, ap) -> boolean",
                    InsertText = "set_ap(actor, 10)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_facing",
                    Description = "Get facing direction (0-7)",
                    Signature = "get_facing(actor) -> number",
                    InsertText = "local dir = get_facing(actor)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "set_facing",
                    Description = "Set facing direction (0-7)",
                    Signature = "set_facing(actor, dir) -> boolean",
                    InsertText = "set_facing(actor, 0)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "is_moving",
                    Description = "Check if actor is currently moving",
                    Signature = "is_moving(actor) -> boolean",
                    InsertText = "if not is_moving(actor) then\n    -- actor is stationary\nend",
                    ItemType = LuaApiItemType.Function
                }
            }
        };
    }

    private static LuaApiItem GetCombatApi()
    {
        return new LuaApiItem
        {
            Name = "Combat",
            ItemType = LuaApiItemType.Category,
            IsExpanded = false,
            Children = new List<LuaApiItem>
            {
                new()
                {
                    Name = "attack",
                    Description = "Attack a target",
                    Signature = "attack(attacker, target) -> {success, error, skill, damage}",
                    InsertText = "local result = attack(attacker, target)\nif result.success then\n    log(\"Dealt \" .. result.damage .. \" damage\")\nend",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "use_ability",
                    Description = "Use a skill/ability on target",
                    Signature = "use_ability(actor, skillName, target?) -> {success, error, skill}",
                    InsertText = "use_ability(actor, \"Overwatch\")",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_skills",
                    Description = "Get all actor skills",
                    Signature = "get_skills(actor) -> table",
                    InsertText = "local skills = get_skills(actor)\nfor i, skill in ipairs(skills) do\n    log(skill.name .. \" - AP: \" .. skill.ap_cost)\nend",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_hp",
                    Description = "Get actor HP info",
                    Signature = "get_hp(actor) -> {current, max, percent}",
                    InsertText = "local hp = get_hp(actor)\nlog(\"HP: \" .. hp.current .. \"/\" .. hp.max)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "set_hp",
                    Description = "Set actor HP value",
                    Signature = "set_hp(actor, hp) -> boolean",
                    InsertText = "set_hp(actor, 100)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "damage",
                    Description = "Apply damage to actor",
                    Signature = "damage(actor, amount) -> boolean",
                    InsertText = "damage(actor, 25)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "heal",
                    Description = "Heal actor",
                    Signature = "heal(actor, amount) -> boolean",
                    InsertText = "heal(actor, 50)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_suppression",
                    Description = "Get suppression level (0-100)",
                    Signature = "get_suppression(actor) -> number",
                    InsertText = "local supp = get_suppression(actor)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "set_suppression",
                    Description = "Set suppression level",
                    Signature = "set_suppression(actor, value) -> boolean",
                    InsertText = "set_suppression(actor, 0)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_morale",
                    Description = "Get actor morale",
                    Signature = "get_morale(actor) -> number",
                    InsertText = "local morale = get_morale(actor)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "set_morale",
                    Description = "Set actor morale",
                    Signature = "set_morale(actor, value) -> boolean",
                    InsertText = "set_morale(actor, 100)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "set_stunned",
                    Description = "Set stunned state",
                    Signature = "set_stunned(actor, bool) -> boolean",
                    InsertText = "set_stunned(actor, true)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_combat_info",
                    Description = "Get full combat state",
                    Signature = "get_combat_info(actor) -> table",
                    InsertText = "local info = get_combat_info(actor)\nlog(\"HP: \" .. info.hp .. \" Morale: \" .. info.morale)",
                    ItemType = LuaApiItemType.Function
                }
            }
        };
    }

    private static LuaApiItem GetTacticalStateApi()
    {
        return new LuaApiItem
        {
            Name = "Tactical State",
            ItemType = LuaApiItemType.Category,
            IsExpanded = false,
            Children = new List<LuaApiItem>
            {
                new()
                {
                    Name = "is_mission_running",
                    Description = "Check if mission is active",
                    Signature = "is_mission_running() -> boolean",
                    InsertText = "if is_mission_running() then\n    -- in tactical\nend",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_round",
                    Description = "Get current round number",
                    Signature = "get_round() -> number",
                    InsertText = "local round = get_round()",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_faction",
                    Description = "Get current faction ID",
                    Signature = "get_faction() -> number",
                    InsertText = "local faction = get_faction()",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_faction_name",
                    Description = "Get faction name from ID",
                    Signature = "get_faction_name(id) -> string",
                    InsertText = "local name = get_faction_name(faction)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "is_player_turn",
                    Description = "Check if it's the player's turn",
                    Signature = "is_player_turn() -> boolean",
                    InsertText = "if is_player_turn() then\n    log(\"Your turn!\")\nend",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "is_paused",
                    Description = "Check if game is paused",
                    Signature = "is_paused() -> boolean",
                    InsertText = "if is_paused() then log(\"Game paused\") end",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "pause",
                    Description = "Pause the game",
                    Signature = "pause(bool?) -> boolean",
                    InsertText = "pause()",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "unpause",
                    Description = "Unpause the game",
                    Signature = "unpause() -> boolean",
                    InsertText = "unpause()",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "toggle_pause",
                    Description = "Toggle pause state",
                    Signature = "toggle_pause() -> boolean",
                    InsertText = "toggle_pause()",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "end_turn",
                    Description = "End current turn",
                    Signature = "end_turn() -> boolean",
                    InsertText = "end_turn()",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_time_scale",
                    Description = "Get game speed multiplier",
                    Signature = "get_time_scale() -> number",
                    InsertText = "local speed = get_time_scale()",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "set_time_scale",
                    Description = "Set game speed (1.0 = normal)",
                    Signature = "set_time_scale(scale) -> boolean",
                    InsertText = "set_time_scale(2.0)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_tactical_state",
                    Description = "Get full tactical state table",
                    Signature = "get_tactical_state() -> table",
                    InsertText = "local state = get_tactical_state()\nlog(\"Round: \" .. state.round .. \" Enemies: \" .. state.alive_enemies)",
                    ItemType = LuaApiItemType.Function
                }
            }
        };
    }

    private static LuaApiItem GetTileMapApi()
    {
        return new LuaApiItem
        {
            Name = "TileMap",
            ItemType = LuaApiItemType.Category,
            IsExpanded = false,
            Children = new List<LuaApiItem>
            {
                new()
                {
                    Name = "get_tile_info",
                    Description = "Get info about a tile",
                    Signature = "get_tile_info(x, z) -> table",
                    InsertText = "local tile = get_tile_info(x, z)\nlog(\"Elevation: \" .. tile.elevation)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_cover",
                    Description = "Get cover value in direction (0-3)",
                    Signature = "get_cover(x, z, dir) -> number",
                    InsertText = "local cover = get_cover(x, z, 0)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_all_cover",
                    Description = "Get cover in all 8 directions",
                    Signature = "get_all_cover(x, z) -> table",
                    InsertText = "local cover = get_all_cover(x, z)\nlog(\"North cover: \" .. cover.north)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "is_blocked",
                    Description = "Check if tile is impassable",
                    Signature = "is_blocked(x, z) -> boolean",
                    InsertText = "if not is_blocked(x, z) then\n    -- can move here\nend",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "has_actor_at",
                    Description = "Check if tile has an actor",
                    Signature = "has_actor_at(x, z) -> boolean",
                    InsertText = "if has_actor_at(x, z) then\n    local actor = get_actor_at(x, z)\nend",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "is_visible",
                    Description = "Check if tile is visible to player",
                    Signature = "is_visible(x, z) -> boolean",
                    InsertText = "if is_visible(x, z) then\n    -- tile is revealed\nend",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_map_info",
                    Description = "Get map dimensions",
                    Signature = "get_map_info() -> {width, height, fog_of_war}",
                    InsertText = "local map = get_map_info()\nlog(\"Map size: \" .. map.width .. \"x\" .. map.height)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_actor_at",
                    Description = "Get actor on a tile",
                    Signature = "get_actor_at(x, z) -> actor",
                    InsertText = "local actor = get_actor_at(x, z)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_distance",
                    Description = "Get distance between tiles",
                    Signature = "get_distance(x1, z1, x2, z2) -> number",
                    InsertText = "local dist = get_distance(x1, z1, x2, z2)",
                    ItemType = LuaApiItemType.Function
                }
            }
        };
    }

    private static LuaApiItem GetSpawnApi()
    {
        return new LuaApiItem
        {
            Name = "Spawn (Experimental)",
            ItemType = LuaApiItemType.Category,
            IsExpanded = false,
            Children = new List<LuaApiItem>
            {
                new()
                {
                    Name = "spawn_unit",
                    Description = "Spawn a unit at position",
                    Signature = "spawn_unit(template, x, y, faction?) -> {success, error, entity}",
                    InsertText = "local result = spawn_unit(\"EnemySoldier\", x, y, 4)\nif result.success then\n    log(\"Spawned: \" .. result.entity.name)\nend",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "destroy_entity",
                    Description = "Kill an entity",
                    Signature = "destroy_entity(actor, immediate?) -> boolean",
                    InsertText = "destroy_entity(actor, true)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "clear_enemies",
                    Description = "Remove all enemies",
                    Signature = "clear_enemies(immediate?) -> number",
                    InsertText = "local count = clear_enemies()\nlog(\"Cleared \" .. count .. \" enemies\")",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "list_entities",
                    Description = "List entities by faction (-1 for all)",
                    Signature = "list_entities(faction?) -> table",
                    InsertText = "local entities = list_entities(-1)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_entity_info",
                    Description = "Get entity details",
                    Signature = "get_entity_info(actor) -> table",
                    InsertText = "local info = get_entity_info(actor)\nlog(\"Type: \" .. info.type_name)",
                    ItemType = LuaApiItemType.Function
                }
            }
        };
    }

    private static LuaApiItem GetTileEffectsApi()
    {
        return new LuaApiItem
        {
            Name = "Tile Effects",
            ItemType = LuaApiItemType.Category,
            IsExpanded = false,
            Children = new List<LuaApiItem>
            {
                new()
                {
                    Name = "get_tile_effects",
                    Description = "Get all effects on tile",
                    Signature = "get_tile_effects(x, z) -> table",
                    InsertText = "local effects = get_tile_effects(x, z)\nfor i, effect in ipairs(effects) do\n    log(effect.template)\nend",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "has_effects",
                    Description = "Check if tile has any effects",
                    Signature = "has_effects(x, z) -> boolean",
                    InsertText = "if has_effects(x, z) then\n    -- tile has effects\nend",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "is_on_fire",
                    Description = "Check if tile is burning",
                    Signature = "is_on_fire(x, z) -> boolean",
                    InsertText = "if is_on_fire(x, z) then\n    log(\"Tile is on fire!\")\nend",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "has_smoke",
                    Description = "Check if tile has smoke",
                    Signature = "has_smoke(x, z) -> boolean",
                    InsertText = "if has_smoke(x, z) then\n    log(\"Smoke cover available\")\nend",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "spawn_effect",
                    Description = "Spawn effect on tile",
                    Signature = "spawn_effect(x, z, template, delay?) -> boolean",
                    InsertText = "spawn_effect(x, z, \"FireEffect\", 0)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "clear_tile_effects",
                    Description = "Remove all effects from tile",
                    Signature = "clear_tile_effects(x, z) -> number",
                    InsertText = "local cleared = clear_tile_effects(x, z)",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_effect_templates",
                    Description = "List available effect templates",
                    Signature = "get_effect_templates() -> table",
                    InsertText = "local templates = get_effect_templates()\nfor i, name in ipairs(templates) do\n    log(name)\nend",
                    ItemType = LuaApiItemType.Function
                }
            }
        };
    }

    private static LuaApiItem GetInventoryApi()
    {
        return new LuaApiItem
        {
            Name = "Inventory",
            ItemType = LuaApiItemType.Category,
            IsExpanded = false,
            Children = new List<LuaApiItem>
            {
                new()
                {
                    Name = "give_item",
                    Description = "Give item to actor (nil = active actor)",
                    Signature = "give_item(actor?, template) -> {success, message}",
                    InsertText = "give_item(nil, \"WeaponAssaultRifle\")",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_inventory",
                    Description = "Get all items in actor's inventory",
                    Signature = "get_inventory(actor?) -> table",
                    InsertText = "local items = get_inventory()\nfor i, item in ipairs(items) do\n    log(item.name .. \" (\" .. item.rarity .. \")\")\nend",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_equipped_weapons",
                    Description = "Get equipped weapons",
                    Signature = "get_equipped_weapons(actor?) -> table",
                    InsertText = "local weapons = get_equipped_weapons()\nfor i, w in ipairs(weapons) do\n    log(\"Weapon: \" .. w.name)\nend",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_equipped_armor",
                    Description = "Get equipped armor",
                    Signature = "get_equipped_armor(actor?) -> table",
                    InsertText = "local armor = get_equipped_armor()\nif armor then log(\"Armor: \" .. armor.name) end",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "get_item_templates",
                    Description = "List item template names",
                    Signature = "get_item_templates(filter?) -> table",
                    InsertText = "local items = get_item_templates(\"Weapon\")\nfor i, name in ipairs(items) do\n    log(name)\nend",
                    ItemType = LuaApiItemType.Function
                }
            }
        };
    }

    private static LuaApiItem GetBlackMarketApi()
    {
        return new LuaApiItem
        {
            Name = "Black Market",
            ItemType = LuaApiItemType.Category,
            IsExpanded = false,
            Children = new List<LuaApiItem>
            {
                new()
                {
                    Name = "blackmarket_stock",
                    Description = "Add item to black market",
                    Signature = "blackmarket_stock(template) -> {success, message}",
                    InsertText = "local result = blackmarket_stock(\"WeaponSniper\")\nif result.success then\n    log(\"Added to black market!\")\nend",
                    ItemType = LuaApiItemType.Function
                },
                new()
                {
                    Name = "blackmarket_has",
                    Description = "Check if item exists in black market",
                    Signature = "blackmarket_has(template) -> boolean",
                    InsertText = "if blackmarket_has(\"WeaponSniper\") then\n    log(\"Item available\")\nend",
                    ItemType = LuaApiItemType.Function
                }
            }
        };
    }

    private static LuaApiItem GetTacticalEvents()
    {
        return new LuaApiItem
        {
            Name = "Tactical Events",
            ItemType = LuaApiItemType.Category,
            IsExpanded = false,
            Children = new List<LuaApiItem>
            {
                // Mission lifecycle
                new() { Name = "tactical_ready", Description = "Fired when tactical mission is initialized", InsertText = "on(\"tactical_ready\", function()\n    log(\"Mission ready\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "mission_start", Description = "Fired at mission start with mission info", InsertText = "on(\"mission_start\", function(data)\n    log(\"Mission: \" .. tostring(data.name))\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "mission_ended", Description = "Fired when mission ends. Data: { success }", InsertText = "on(\"mission_ended\", function(data)\n    if data.success then log(\"Victory!\") end\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "objective_changed", Description = "Fired when objectives update", InsertText = "on(\"objective_changed\", function(data)\n    log(\"Objective updated\")\nend)", ItemType = LuaApiItemType.Event },

                // Turn/round events
                new() { Name = "turn_start", Description = "Fired when a turn begins. Data: { faction, factionName }", InsertText = "on(\"turn_start\", function(data)\n    log(data.factionName .. \" turn\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "turn_end", Description = "Fired when a turn ends. Data: { faction, factionName }", InsertText = "on(\"turn_end\", function(data)\n    log(\"Turn ended\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "round_start", Description = "Fired when a round begins", InsertText = "on(\"round_start\", function(data)\n    log(\"New round\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "round_end", Description = "Fired when a round ends", InsertText = "on(\"round_end\", function(data)\n    log(\"Round ended\")\nend)", ItemType = LuaApiItemType.Event },

                // Combat events
                new() { Name = "actor_killed", Description = "Fired when an actor dies", InsertText = "on(\"actor_killed\", function(data)\n    log(\"Actor killed\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "damage_received", Description = "Fired when damage is dealt", InsertText = "on(\"damage_received\", function(data)\n    log(\"Damage received\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "attack_start", Description = "Fired when an attack begins", InsertText = "on(\"attack_start\", function(data)\n    log(\"Attack started\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "attack_missed", Description = "Fired when an attack misses", InsertText = "on(\"attack_missed\", function(data)\n    log(\"Missed!\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "critical_hit", Description = "Fired on a critical hit", InsertText = "on(\"critical_hit\", function(data)\n    log(\"Critical hit!\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "overwatch_triggered", Description = "Fired when overwatch activates", InsertText = "on(\"overwatch_triggered\", function(data)\n    log(\"Overwatch!\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "grenade_thrown", Description = "Fired when a grenade is thrown", InsertText = "on(\"grenade_thrown\", function(data)\n    log(\"Grenade!\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "bleeding_out", Description = "Fired when actor starts bleeding out", InsertText = "on(\"bleeding_out\", function(data)\n    log(\"Bleeding out!\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "stabilized", Description = "Fired when actor is stabilized", InsertText = "on(\"stabilized\", function(data)\n    log(\"Stabilized\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "suppressed", Description = "Fired when actor becomes suppressed", InsertText = "on(\"suppressed\", function(data)\n    log(\"Suppressed!\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "suppression_applied", Description = "Fired when suppression is applied", InsertText = "on(\"suppression_applied\", function(data)\n    log(\"Suppression applied\")\nend)", ItemType = LuaApiItemType.Event },

                // State changes
                new() { Name = "actor_state_changed", Description = "Fired when actor state changes", InsertText = "on(\"actor_state_changed\", function(data)\n    log(\"State changed\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "hp_changed", Description = "Fired when HP changes", InsertText = "on(\"hp_changed\", function(data)\n    log(\"HP changed\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "ap_changed", Description = "Fired when AP changes", InsertText = "on(\"ap_changed\", function(data)\n    log(\"AP changed\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "armor_changed", Description = "Fired when armor changes", InsertText = "on(\"armor_changed\", function(data)\n    log(\"Armor changed\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "morale_changed", Description = "Fired when morale changes", InsertText = "on(\"morale_changed\", function(data)\n    log(\"Morale changed\")\nend)", ItemType = LuaApiItemType.Event },

                // Visibility events
                new() { Name = "discovered", Description = "Fired when actor is discovered", InsertText = "on(\"discovered\", function(data)\n    log(\"Enemy discovered!\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "visible_to_player", Description = "Fired when actor becomes visible", InsertText = "on(\"visible_to_player\", function(data)\n    log(\"Now visible\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "hidden_from_player", Description = "Fired when actor becomes hidden", InsertText = "on(\"hidden_from_player\", function(data)\n    log(\"Now hidden\")\nend)", ItemType = LuaApiItemType.Event },

                // Movement
                new() { Name = "move_start", Description = "Fired when movement begins", InsertText = "on(\"move_start\", function(data)\n    log(\"Moving...\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "move_complete", Description = "Fired when movement ends", InsertText = "on(\"move_complete\", function(data)\n    log(\"Move complete\")\nend)", ItemType = LuaApiItemType.Event },

                // Skills
                new() { Name = "skill_used", Description = "Fired when a skill is activated", InsertText = "on(\"skill_used\", function(data)\n    log(\"Skill used\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "skill_complete", Description = "Fired when a skill finishes", InsertText = "on(\"skill_complete\", function(data)\n    log(\"Skill complete\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "skill_added", Description = "Fired when a skill is added", InsertText = "on(\"skill_added\", function(data)\n    log(\"Skill added\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "offmap_ability_used", Description = "Fired when offmap ability is used", InsertText = "on(\"offmap_ability_used\", function(data)\n    log(\"Offmap ability!\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "offmap_ability_canceled", Description = "Fired when offmap ability is canceled", InsertText = "on(\"offmap_ability_canceled\", function(data)\n    log(\"Ability canceled\")\nend)", ItemType = LuaApiItemType.Event },

                // Entity events
                new() { Name = "entity_spawned", Description = "Fired when an entity spawns", InsertText = "on(\"entity_spawned\", function(data)\n    log(\"Entity spawned\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "element_destroyed", Description = "Fired when a map element is destroyed", InsertText = "on(\"element_destroyed\", function(data)\n    log(\"Element destroyed\")\nend)", ItemType = LuaApiItemType.Event },
                new() { Name = "element_malfunction", Description = "Fired when equipment malfunctions", InsertText = "on(\"element_malfunction\", function(data)\n    log(\"Malfunction!\")\nend)", ItemType = LuaApiItemType.Event }
            }
        };
    }

    private static LuaApiItem GetStrategyEvents()
    {
        return new LuaApiItem
        {
            Name = "Strategy Events",
            ItemType = LuaApiItemType.Category,
            IsExpanded = false,
            Children = new List<LuaApiItem>
            {
                // Campaign
                new()
                {
                    Name = "campaign_start",
                    Description = "Fired when a new campaign starts",
                    Signature = "on(\"campaign_start\", function() ... end)",
                    InsertText = "on(\"campaign_start\", function()\n    log(\"Campaign started!\")\nend)",
                    ItemType = LuaApiItemType.Event
                },
                // Leaders
                new()
                {
                    Name = "leader_hired",
                    Description = "Fired when a leader is recruited. Data: { leader }",
                    Signature = "on(\"leader_hired\", function(data) ... end)",
                    InsertText = "on(\"leader_hired\", function(data)\n    log(\"Leader hired: \" .. tostring(data.leader))\nend)",
                    ItemType = LuaApiItemType.Event
                },
                new()
                {
                    Name = "leader_dismissed",
                    Description = "Fired when a leader is dismissed. Data: { leader }",
                    Signature = "on(\"leader_dismissed\", function(data) ... end)",
                    InsertText = "on(\"leader_dismissed\", function(data)\n    log(\"Leader dismissed\")\nend)",
                    ItemType = LuaApiItemType.Event
                },
                new()
                {
                    Name = "leader_permadeath",
                    Description = "Fired when a leader dies permanently. Data: { leader }",
                    Signature = "on(\"leader_permadeath\", function(data) ... end)",
                    InsertText = "on(\"leader_permadeath\", function(data)\n    log(\"Leader lost forever: \" .. tostring(data.leader))\nend)",
                    ItemType = LuaApiItemType.Event
                },
                new()
                {
                    Name = "leader_levelup",
                    Description = "Fired when a leader gains a perk. Data: { leader }",
                    Signature = "on(\"leader_levelup\", function(data) ... end)",
                    InsertText = "on(\"leader_levelup\", function(data)\n    log(\"Leader leveled up!\")\nend)",
                    ItemType = LuaApiItemType.Event
                },
                // Factions
                new()
                {
                    Name = "faction_trust_changed",
                    Description = "Fired when faction trust changes. Data: { faction, delta }",
                    Signature = "on(\"faction_trust_changed\", function(data) ... end)",
                    InsertText = "on(\"faction_trust_changed\", function(data)\n    if data.delta > 0 then\n        log(\"Trust increased with \" .. tostring(data.faction))\n    else\n        log(\"Trust decreased with \" .. tostring(data.faction))\n    end\nend)",
                    ItemType = LuaApiItemType.Event
                },
                new()
                {
                    Name = "faction_status_changed",
                    Description = "Fired when faction status changes. Data: { faction, status }",
                    Signature = "on(\"faction_status_changed\", function(data) ... end)",
                    InsertText = "on(\"faction_status_changed\", function(data)\n    log(\"Faction status: \" .. tostring(data.status))\nend)",
                    ItemType = LuaApiItemType.Event
                },
                new()
                {
                    Name = "faction_upgrade_unlocked",
                    Description = "Fired when a faction upgrade unlocks. Data: { faction, upgrade }",
                    Signature = "on(\"faction_upgrade_unlocked\", function(data) ... end)",
                    InsertText = "on(\"faction_upgrade_unlocked\", function(data)\n    log(\"Upgrade unlocked: \" .. tostring(data.upgrade))\nend)",
                    ItemType = LuaApiItemType.Event
                },
                // Squaddies
                new()
                {
                    Name = "squaddie_killed",
                    Description = "Fired when a squaddie dies. Data: { id }",
                    Signature = "on(\"squaddie_killed\", function(data) ... end)",
                    InsertText = "on(\"squaddie_killed\", function(data)\n    log(\"Squaddie lost\")\nend)",
                    ItemType = LuaApiItemType.Event
                },
                new()
                {
                    Name = "squaddie_added",
                    Description = "Fired when a squaddie is recruited. Data: { id }",
                    Signature = "on(\"squaddie_added\", function(data) ... end)",
                    InsertText = "on(\"squaddie_added\", function(data)\n    log(\"New squaddie recruited\")\nend)",
                    ItemType = LuaApiItemType.Event
                },
                // Economy
                new()
                {
                    Name = "blackmarket_item_added",
                    Description = "Fired when an item is added to the black market. Data: { item }",
                    Signature = "on(\"blackmarket_item_added\", function(data) ... end)",
                    InsertText = "on(\"blackmarket_item_added\", function(data)\n    log(\"New black market item\")\nend)",
                    ItemType = LuaApiItemType.Event
                },
                new()
                {
                    Name = "blackmarket_restocked",
                    Description = "Fired when the black market restocks",
                    Signature = "on(\"blackmarket_restocked\", function() ... end)",
                    InsertText = "on(\"blackmarket_restocked\", function()\n    log(\"Black market restocked!\")\nend)",
                    ItemType = LuaApiItemType.Event
                },
                new()
                {
                    Name = "operation_finished",
                    Description = "Fired when an operation completes",
                    Signature = "on(\"operation_finished\", function() ... end)",
                    InsertText = "on(\"operation_finished\", function()\n    log(\"Operation complete\")\nend)",
                    ItemType = LuaApiItemType.Event
                }
            }
        };
    }

    private static LuaApiItem GetGeneralEvents()
    {
        return new LuaApiItem
        {
            Name = "General Events",
            ItemType = LuaApiItemType.Category,
            IsExpanded = false,
            Children = new List<LuaApiItem>
            {
                new()
                {
                    Name = "scene_loaded",
                    Description = "Fired when a scene loads. Data: sceneName (string)",
                    Signature = "on(\"scene_loaded\", function(sceneName) ... end)",
                    InsertText = "on(\"scene_loaded\", function(sceneName)\n    log(\"Scene loaded: \" .. tostring(sceneName))\nend)",
                    ItemType = LuaApiItemType.Event
                }
            }
        };
    }

    /// <summary>
    /// Get script templates for new file creation.
    /// </summary>
    public static List<(string Name, string Description, string Content)> GetScriptTemplates()
    {
        return new List<(string, string, string)>
        {
            ("Basic Script", "Simple script with logging and scene event",
@"-- Basic Lua Script
log(""[MyMod] Loading..."")

on(""scene_loaded"", function(sceneName)
    log(""[MyMod] Scene: "" .. tostring(sceneName))
end)

log(""[MyMod] Loaded!"")
"),
            ("Tactical Helper", "Track combat events in tactical missions",
@"-- Tactical Helper Script
log(""[TacticalMod] Loading..."")

local killCount = 0

on(""tactical_ready"", function()
    log(""[TacticalMod] Mission started"")
    killCount = 0
end)

on(""turn_start"", function(data)
    if data and data.faction == 0 then
        log(""[TacticalMod] Your turn!"")
    end
end)

on(""actor_killed"", function(data)
    killCount = killCount + 1
    log(""[TacticalMod] Kill #"" .. killCount)
end)

log(""[TacticalMod] Loaded!"")
"),
            ("Strategy Helper", "Track campaign events in strategy layer",
@"-- Strategy Helper Script
log(""[StrategyMod] Loading..."")

on(""campaign_start"", function()
    log(""[StrategyMod] Campaign started!"")
end)

on(""leader_hired"", function(data)
    log(""[StrategyMod] New leader: "" .. tostring(data.leader))
end)

on(""faction_trust_changed"", function(data)
    local direction = data.delta > 0 and ""up"" or ""down""
    log(""[StrategyMod] Trust "" .. direction .. "" with "" .. tostring(data.faction))
end)

log(""[StrategyMod] Loaded!"")
"),
            ("Console Commands", "Add custom console commands",
@"-- Console Commands Script
log(""[Commands] Loading..."")

cmd(""hello"", function(args)
    log(""Hello, "" .. (args or ""world"") .. ""!"")
    return ""Greeting sent""
end)

cmd(""status"", function()
    log(""=== STATUS ==="")
    if type(is_mission_running) == ""function"" then
        log(""Mission running: "" .. tostring(is_mission_running()))
    end
    return ""Status printed""
end)

log(""[Commands] Loaded!"")
log(""[Commands] Available: /hello, /status"")
")
        };
    }
}
