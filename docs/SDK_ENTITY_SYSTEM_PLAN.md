# SDK Entity System Plan

## Overview

Add an **object-oriented wrapper layer** and **effect system** on top of the existing SDK infrastructure.

```
┌─────────────────────────────────────────────────────────────┐
│  NEW: OO Wrapper Layer (Actor, Skill, Tile classes)         │
│  NEW: EffectSystem (temporary modifiers with expiry)        │
│  NEW: Lua object bindings (pass objects, not pointers)      │
├─────────────────────────────────────────────────────────────┤
│  EXISTING: Static SDK modules                               │
│  Roster, BlackMarket, Mission, EntityCombat, TileMap, etc.  │
└─────────────────────────────────────────────────────────────┘
```

---

## Existing SDK Coverage

### Strategic Layer (EXPANDED)
| Module | File | Status |
|--------|------|--------|
| Squad Management | `Roster.cs` | ✅ Hiring, dismissal, perks, squaddies, healing |
| Black Market | `BlackMarket.cs` | ✅ Items, stacks, expiration |
| Missions | `Mission.cs` | ✅ Metadata, objectives |
| Conversations | `Conversation.cs` | ✅ Dialogue, speakers, dilemmas |
| Operations/Planets | `Operation.cs` | ✅ Multi-op, factions, missions, time |
| Inventory | `Inventory.cs` | ✅ Items, equipment slots |
| Perks | `Perks.cs` | ✅ Perk trees, manipulation |
| Strategy Events | `StrategyEventHooks.cs` | ✅ Hiring, trust, market |
| **Factions** | `Faction.cs` | ✅ **Trust, relations, upgrades** |
| **Ship Upgrades** | `OCI.cs` | ✅ **OCI points, slots, installs** |
| **Content Creation** | `StrategicContent.cs` | ✅ **Missions, operations, dilemmas** |

### Tactical Layer (ALREADY BUILT)
| Module | File | Status |
|--------|------|--------|
| Combat | `EntityCombat.cs` | ✅ Attack, damage, suppression |
| Skills | `EntitySkills.cs` | ✅ Cooldowns, ranges, AP costs |
| State | `EntityState.cs` | ✅ Flags, detection masks |
| Visibility | `EntityVisibility.cs` | ✅ Faction detection |
| Movement | `EntityMovement.cs` | ✅ Pathfinding-based |
| Spawning | `EntitySpawner.cs` | ✅ Spawn/destroy |
| Vehicles | `Vehicle.cs` | ✅ Health, armor, slots |
| Tiles | `TileMap.cs` | ✅ Cover, occupancy, LOS |
| Tile Manipulation | `TileManipulation.cs` | ✅ Traversability, cover |
| Tile Effects | `TileEffects.cs` | ✅ Fire, smoke, etc. |
| Pathfinding | `Pathfinding.cs` | ✅ A*, movement range |
| Line of Sight | `LineOfSight.cs` | ✅ LOS checks, detection |
| Turn Control | `TacticalController.cs` | ✅ Round/turn, factions |
| Tactical Events | `TacticalEventHooks.cs` | ✅ 20+ event hooks |
| Property Intercepts | `Intercept.cs` | ✅ Hit chance, damage, etc. |

### Shared (ALREADY BUILT)
| Module | File | Status |
|--------|------|--------|
| Templates | `Templates.cs` | ✅ Field access, cloning |
| Base Wrapper | `GameObj.cs` | ✅ Pointer management |
| AI | `AICoordination.cs` | ✅ Faction AI state |
| Custom Maps | `CustomMaps/` | ✅ Map creation |
| Lua Engine | `LuaScriptEngine.cs` | ✅ Script execution |

---

## What's Missing (TO BUILD)

### 1. EffectSystem (NEW)
Temporary modifiers that auto-expire. Not currently in SDK.

```csharp
public static class EffectSystem
{
    public static void AddEffect(IntPtr entity, string property, int modifier, int rounds);
    public static int GetModifier(IntPtr entity, string property);
    internal static void OnRoundEnd();  // decrements, removes expired
}
```

### 2. OO Wrapper Classes (NEW)
Clean object wrappers over existing static methods.

```csharp
// Wraps existing EntityCombat, EntitySkills, etc.
public class Actor
{
    public void AddEffect(string property, int modifier, int rounds)
        => EffectSystem.AddEffect(Pointer, property, modifier, rounds);

    public void Attack(Actor target)
        => EntityCombat.Attack(Pointer, target.Pointer);

    public IReadOnlyList<Skill> Skills
        => EntitySkills.GetSkills(Pointer).Select(s => new Skill(s)).ToList();
}
```

### 3. Lua Object Bindings (ENHANCE)
Pass objects instead of pointers to Lua.

```lua
-- Current (pointers):
on("skill_used", function(data)
    -- data.user_ptr is a number (pointer)
end)

-- New (objects):
on("skill_used", function(actor, skill)
    -- actor is an object with methods
    actor:add_effect("concealment", -3, 1)
end)
```

### 4. Round-End Hook for Effects (WIRE UP)
Wire EffectSystem.OnRoundEnd() to TacticalEventHooks.OnRoundEnd.

### 5. Skill.IsSilent Property (FOUND)
From `generated_injection_code.cs` and schema:
- **SkillTemplate+0x110** = IsSilent (bool)
- **SkillTemplate+0xF2** = IsAttack (bool)

Both properties exist and should be exposed on the Skill wrapper class.

# SHARED SYSTEMS

## X1. Template/Schema System

All properties come from templates defined in schema. Update schema when game updates.

```csharp
public static class TemplateSchema
{
    // Load schema from JSON extracted from game data
    public static void LoadSchema(string path);

    // Query any template property
    public static T GetProperty<T>(string templateId, string propertyName);
    public static bool HasProperty(string templateId, string propertyName);

    // Template discovery
    public static IEnumerable<string> GetTemplateIds(string category);
}
```

---

## X2. Lua Bindings

Lua wraps the C# objects with identical API:

```lua
-- Strategic layer
local squad = Squad.instance
for _, op in ipairs(squad.operatives) do
    log(op.name .. " has " .. #op.perks .. " perks")
end

-- Intercept map generation
on("map_generating", function(mission, params)
    params.weather = "rain"
    params.time_of_day = "night"
end)

-- Tactical layer
on("skill_used", function(actor, skill)
    if skill.is_attack then
        actor:add_effect("concealment", -3, 1)
    end
end)

-- Access properties via template
local damage = skill:get_template_property("BaseDamage")
```

---

# IMPLEMENTATION PLAN (Minimal New Work)

## Phase 1: EffectSystem (NEW) ✅ COMPLETE
1. ✅ `TemplateSchema.cs` - schema-driven offset lookups
2. ✅ `EffectSystem.cs` - modifier tracking with round expiry
3. ✅ Wire into `Intercept.cs` - apply modifiers in GetConcealment_Postfix
4. ✅ Wire into `TacticalEventHooks.OnRoundEnd` - decrement/expire effects
5. ✅ Add `is_silent` to skill_used event using schema offsets

## Phase 2: OO Wrappers (NEW) ✅ COMPLETE
6. ✅ `SDK/Entities/Actor.cs` - wraps EntityCombat, EntitySkills, EntityMovement, EffectSystem
7. ✅ `SDK/Entities/Skill.cs` - wraps skill pointer with IsSilent, IsAttack from template
8. ✅ `SDK/Entities/Tile.cs` - wraps TileMap queries
9. ✅ Entity cache in Actor.Get() - lookup by pointer

## Phase 3: Lua Object Bindings (NEW) ✅ COMPLETE
10. ✅ `SDK/LuaObjectBindings.cs` - MoonSharp UserData types for Actor, Skill, Tile
11. ✅ `LuaActor` class with `add_effect()`, `attack()`, `move_to()` etc. methods
12. ✅ `LuaSkill` class with `is_attack`, `is_silent` properties
13. ✅ `LuaTile` class with `get_cover()`, `get_occupant()` methods
14. ✅ `skill_used` event migrated to pass Actor and Skill objects directly
15. ✅ Effect system standalone functions: `add_effect()`, `get_effect()`, `has_effect()`
16. ✅ Factory functions: `Actor(ptr)`, `Skill(ptr)`, `Tile(x, y)`
17. ✅ Actor iteration: `actors()`, `player_actors()`, `enemy_actors()`

## Current Status
- **Phase 1**: Complete (~250 lines) - EffectSystem, schema-driven offsets
- **Phase 2**: Complete (~500 lines) - Actor, Skill, Tile wrappers
- **Phase 3**: Complete (~400 lines) - Lua object bindings with UserData
- **Phase 4**: Complete (~1500 lines) - Strategic layer expansion

All phases complete! The SDK provides both:
- Legacy table-based events (backwards compatible)
- Object-based events with full method access (Phase 3)

---

## Phase 4: Strategic Layer Expansion (NEW) ✅ COMPLETE

### 4.1 Faction.cs (NEW) ✅
Full faction management:
- `GetAllFactions()` - list all story factions
- `GetFactionInfo()` - trust, status, upgrades, active operations
- `GetFactionsWithOperations()` - factions with active ops
- `ChangeTrust()`, `SetStatus()` - manipulation
- `GetUpgrades()`, `UnlockUpgrade()` - faction upgrades

### 4.2 Operation.cs Enhancement ✅
Multi-operation support:
- `GetAllOperations()` - all active operations
- `GetAllOperationInfo()` - info for all operations
- `FindByFaction()` - get operation by faction name
- `FindByPlanet()` - get operation by planet
- `GetCompletedOperationTypes()` - operation completion history

### 4.3 OCI.cs (NEW) ✅
Orbital Command Interface (Ship Upgrades):
- `GetOciPoints()`, `GetMaxOciPoints()` - OCI points
- `GetAllUpgradeTemplates()` - all available upgrades
- `GetInstalledUpgrades()`, `GetAvailableUpgrades()` - current state
- `GetSlots()`, `GetSlotInfo()` - slot management
- `InstallUpgrade()`, `UninstallUpgrade()` - modification

### 4.4 Roster.cs Enhancement ✅
Strategic layer squad modification:
- `GetSquaddies()` - get squaddies for a leader
- `AddSquaddie()`, `RemoveSquaddie()` - squaddie management
- `AddPerk()`, `RemovePerk()` - perk manipulation
- `HealLeader()` - restore health
- `SetLeaderAvailable()` - availability control

### 4.5 StrategicContent.cs (NEW) ✅
Runtime content creation/modification:
- `AddMissionToOperation()` - inject missions
- `StartOperation()`, `EndCurrentOperation()` - operation control
- `TriggerDilemma()` - strategic conversations
- `GetAllPlanets()`, `GetAllBiomes()` - world info
- `FireStrategicEvent()` - event triggering

---

# EXAMPLES

## Example 1: Beagle's Concealment Mod

### C# (using new OO wrappers)
```csharp
using Menace.SDK.Entities;

TacticalEventHooks.OnSkillUsed += (userPtr, skillPtr, targetPtr) =>
{
    var actor = Actor.Get(userPtr);
    var skill = new Skill(skillPtr);

    if (skill.IsAttack && !skill.IsSilent)
        actor.AddEffect("concealment", -3, rounds: 1);
};
```

### Lua (Phase 3 - object bindings) ✅ CURRENT API
```lua
-- skill_used passes Actor and Skill objects directly
on("skill_used", function(actor, skill)
    if skill.is_attack and not skill.is_silent then
        actor:add_effect("concealment", -3, 1)
    end
end)
```

### Lua (Iterating all actors)
```lua
-- Apply effect to all enemies
for _, enemy in ipairs(enemy_actors()) do
    enemy:add_effect("accuracy", -2, 1)
end

-- Check all player actors
for _, ally in ipairs(player_actors()) do
    if ally:has_effect("concealment") then
        log(ally.name .. " is concealed")
    end
end
```

---

## Example 2: Night Mission Modifier

### C#
```csharp
MissionEvents.OnMapGenerating += (mission, mapParams) =>
{
    if (mission.Planet.Id == "shadow_world")
    {
        mapParams.TimeOfDay = TimeOfDay.Night;
        mapParams.Weather = WeatherType.Fog;
    }
};
```

### Lua
```lua
on("map_generating", function(mission, params)
    if mission.planet.id == "shadow_world" then
        params.time_of_day = "night"
        params.weather = "fog"
    end
end)
```

---

## Example 3: Faction Reputation Effects

### C#
```csharp
FactionEvents.OnRelationChanged += (faction, oldVal, newVal) =>
{
    if (newVal >= 50 && oldVal < 50)
    {
        BlackMarket.Instance.UnlockFactionItems(faction);
    }
};
```

### Lua
```lua
on("faction_relation_changed", function(faction, old_val, new_val)
    if new_val >= 50 and old_val < 50 then
        black_market:unlock_faction_items(faction)
    end
end)
```
