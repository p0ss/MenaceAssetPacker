# Menace Reverse Engineering Notes

This directory contains reverse-engineered documentation of Menace's game systems, extracted via Ghidra analysis of GameAssembly.dll and Il2CppDumper output.

## Analysis Status

| System | Status | File |
|--------|--------|------|
| Combat - Hit Chance | ✅ Complete | [combat-hit-chance.md](./combat-hit-chance.md) |
| Combat - Damage | ✅ Complete | [combat-damage.md](./combat-damage.md) |
| Combat - Cover | ✅ Complete | [combat-hit-chance.md](./combat-hit-chance.md) |
| Combat - Armor | ✅ Complete | [combat-damage.md](./combat-damage.md) |
| Suppression & Morale | ✅ Complete | [suppression-morale.md](./suppression-morale.md) |
| Skills & Effects | ✅ Complete | [skills-effects.md](./skills-effects.md) |
| Entity Properties | ✅ Complete | [entity-properties.md](./entity-properties.md) |
| Weapon Stats | ✅ Complete | [combat-damage.md](./combat-damage.md) |
| AI Decision Making | ✅ Complete | [ai-decisions.md](./ai-decisions.md) |
| AI Roles & Weights | ✅ Complete | [ai-decisions.md](./ai-decisions.md) |
| Template Loading | ✅ Complete | [template-loading.md](./template-loading.md) |
| Asset References | ✅ Complete | [asset-references.md](./asset-references.md) |
| Save/Load System | ✅ Complete | [save-system.md](./save-system.md) |
| Save Body Format | ✅ Complete | [save-system.md](./save-system.md) |
| Stat System & Multipliers | ✅ Complete | [stat-system.md](./stat-system.md) |
| UI System | ✅ Complete | [ui-system.md](./ui-system.md) |
| Offset Stability | ✅ Complete | [offset-stability.md](./offset-stability.md) |
| Terrain Generation | ✅ Complete | [terrain-generation.md](./terrain-generation.md) |
| Tile & Map System | ✅ Complete | [tile-map-system.md](./tile-map-system.md) |
| Pathfinding System | ✅ Complete | [pathfinding-system.md](./pathfinding-system.md) |
| Actor System | ✅ Complete | [actor-system.md](./actor-system.md) |
| Turn & Action System | ✅ Complete | [turn-action-system.md](./turn-action-system.md) |
| Mission System | ✅ Complete | [mission-system.md](./mission-system.md) |
| Operation System | ✅ Complete | [operation-system.md](./operation-system.md) |
| Army Generation | ✅ Complete | [army-generation.md](./army-generation.md) |
| Roster & Unit Management | ✅ Complete | [roster-system.md](./roster-system.md) |
| Supply Cost System | ✅ Complete | [supply-cost-system.md](./supply-cost-system.md) |
| Item System | ✅ Complete | [item-system.md](./item-system.md) |
| Vehicle System | ✅ Complete | [vehicle-system.md](./vehicle-system.md) |
| Conversation System | ✅ Complete | [conversation-system.md](./conversation-system.md) |
| Event System | ✅ Complete | [event-system.md](./event-system.md) |
| Emotional State System | ✅ Complete | [emotional-system.md](./emotional-system.md) |
| Tile Effects | ✅ Complete | [tile-effects.md](./tile-effects.md) |
| Line of Sight & Visibility | ✅ Complete | [line-of-sight.md](./line-of-sight.md) |
| Offmap Abilities | ✅ Complete | [offmap-abilities.md](./offmap-abilities.md) |
| BlackMarket | ✅ Complete | [blackmarket.md](./blackmarket.md) |
| Localization System | ✅ Complete | [localization-system.md](./localization-system.md) |
| Unity Asset Format | ✅ Complete | [unity-asset-format.md](./unity-asset-format.md) |
| IL2CPP Runtime | ✅ Complete | [../system-guide/il2cpp-runtime.md](../system-guide/il2cpp-runtime.md) |
| Unnamed Functions Plan | 📋 Reference | [unnamed-functions-plan.md](./unnamed-functions-plan.md) |

## SDK Coverage

Many reverse-engineered systems now have SDK wrappers with console commands:

| RE System | SDK Wrapper | Console Commands |
|-----------|-------------|------------------|
| [tile-map-system.md](./tile-map-system.md) | [TileMap](../coding-sdk/api/tile-map.md) | `tile`, `cover`, `mapinfo` |
| [pathfinding-system.md](./pathfinding-system.md) | [Pathfinding](../coding-sdk/api/pathfinding.md) | `path`, `movecost` |
| [line-of-sight.md](./line-of-sight.md) | [LineOfSight](../coding-sdk/api/line-of-sight.md) | `los`, `visible` |
| [tile-effects.md](./tile-effects.md) | [TileEffects](../coding-sdk/api/tile-effects.md) | `fire`, `smoke`, `effects` |
| [mission-system.md](./mission-system.md) | [Mission](../coding-sdk/api/mission.md) | `mission`, `objectives` |
| [operation-system.md](./operation-system.md) | [Operation](../coding-sdk/api/operation.md) | `operation`, `opmissions` |
| [roster-system.md](./roster-system.md) | [Roster](../coding-sdk/api/roster.md) | `roster`, `unit`, `available` |
| [item-system.md](./item-system.md) | [Inventory](../coding-sdk/api/inventory.md) | `inventory`, `weapons` |
| [army-generation.md](./army-generation.md) | [ArmyGeneration](../coding-sdk/api/army-generation.md) | `armytemplates`, `entitycost` |
| [vehicle-system.md](./vehicle-system.md) | [Vehicle](../coding-sdk/api/vehicle.md) | `vehicle`, `twinfire` |
| [conversation-system.md](./conversation-system.md) | [Conversation](../coding-sdk/api/conversation.md) | `conversations` |
| [emotional-system.md](./emotional-system.md) | [Emotions](../coding-sdk/api/emotions.md) | `emotions` |
| [blackmarket.md](./blackmarket.md) | [BlackMarket](../coding-sdk/api/black-market.md) | `blackmarket` |
| [combat-damage.md](./combat-damage.md) | [EntityCombat](../coding-sdk/api/entity-combat.md) | `damage`, `heal`, `skills` |
| [actor-system.md](./actor-system.md) | [EntitySpawner](../coding-sdk/api/entity-spawner.md) | `spawn`, `kill`, `actors` |
| [turn-action-system.md](./turn-action-system.md) | [TacticalController](../coding-sdk/api/tactical-controller.md) | `tactical`, `round`, `endturn` |

## Key Offsets Reference

See [offsets.md](./offsets.md) for a consolidated list of struct field offsets discovered during analysis.

## Documentation Index

### Combat System
- **[combat-hit-chance.md](./combat-hit-chance.md)** - Hit chance calculation, accuracy, cover multipliers, distance penalties
- **[combat-damage.md](./combat-damage.md)** - Damage calculation, armor penetration, weapon/armor template mappings

### Unit State
- **[suppression-morale.md](./suppression-morale.md)** - Suppression states, morale states, related methods
- **[entity-properties.md](./entity-properties.md)** - Complete EntityProperties field layout with all combat stats
- **[actor-system.md](./actor-system.md)** - Entity, Actor, UnitActor, Structure, Element class layouts and methods

### Game Systems
- **[skills-effects.md](./skills-effects.md)** - SkillTemplate structure, SkillEventHandler system, effect types
- **[ai-decisions.md](./ai-decisions.md)** - Agent class, behavior system, utility-based decision making
- **[template-loading.md](./template-loading.md)** - DataTemplateLoader, resource paths, template hierarchy
- **[turn-action-system.md](./turn-action-system.md)** - TacticalManager, TacticalState, rounds, turns, action classes

### Campaign/Strategy Layer
- **[mission-system.md](./mission-system.md)** - Mission class, army management, objectives, environmental settings, spawn flow
- **[operation-system.md](./operation-system.md)** - Operation lifecycle, faction trust, strategic assets, time management
- **[army-generation.md](./army-generation.md)** - Budget-based army generation, weighted selection, progress scaling
- **[roster-system.md](./roster-system.md)** - Roster, BaseUnitLeader, Squaddie, perks, deployment costs
- **[supply-cost-system.md](./supply-cost-system.md)** - Player vs enemy cost fields, FixedSupplyCost, DeployCostsPerElement, runtime modifiers

### Items & Equipment
- **[item-system.md](./item-system.md)** - Item, ItemContainer, BaseItemTemplate, 11 slot types, skill management
- **[vehicle-system.md](./vehicle-system.md)** - Vehicle, ItemsModularVehicle, modular slots, twin-fire detection
- **[blackmarket.md](./blackmarket.md)** - BlackMarket shop system, item generation, operation-based timeouts

### Dialogue & Events
- **[conversation-system.md](./conversation-system.md)** - BaseConversationManager, Role requirements, speaker assignment, conversation nodes
- **[event-system.md](./event-system.md)** - EventManager, ConversationInstance, strategic layer events
- **[emotional-system.md](./emotional-system.md)** - EmotionalStates, triggers, skill modifiers, mission lifecycle

### Infrastructure
- **[save-system.md](./save-system.md)** - Complete save file format, StrategyState serialization order, all nested processors (Roster, OwnedItems, Squaddies, etc.), parser implementation guide
- **[stat-system.md](./stat-system.md)** - Multiplier system (AddMult), stat calculation flow, ammo/uses
- **[ui-system.md](./ui-system.md)** - UITacticalHUD, adding custom HUD elements, settings screens
- **[offset-stability.md](./offset-stability.md)** - API considerations, version compatibility, best practices
- **[asset-references.md](./asset-references.md)** - Template hierarchy, sprite/prefab references, finding asset usages
- **[unity-asset-format.md](./unity-asset-format.md)** - Unity binary serialization format, MonoBehaviour vs ScriptableObject structures, string/PPtr encoding, texture formats
- **[terrain-generation.md](./terrain-generation.md)** - Map generation pipeline, ChunkGenerator, MapMagic integration, road/cover placement
- **[tile-map-system.md](./tile-map-system.md)** - Tile/Map classes, cover system, visibility, 8-direction system
- **[pathfinding-system.md](./pathfinding-system.md)** - A* pathfinding, movement costs, traversability, path modifiers
- **[tile-effects.md](./tile-effects.md)** - TileEffectHandler system, effect types (fire, smoke, bleed-out, ammo crates), lifecycle callbacks
- **[line-of-sight.md](./line-of-sight.md)** - LOS ray-tracing, visibility masks, fog of war, detection vs concealment
- **[offmap-abilities.md](./offmap-abilities.md)** - Delayed abilities (airstrikes, artillery), round-based timing, scheduling
- **[localization-system.md](./localization-system.md)** - LocalizedLine/LocalizedMultiLine wrapper objects, translation CSV format, why mods fail with string casts

### IL2CPP Runtime
- **[../system-guide/il2cpp-runtime.md](../system-guide/il2cpp-runtime.md)** - IL2CPP runtime function reference (GC, object system, strings, exceptions, method dispatch)

### Reference Code
- **[../reference-code/](../reference-code/)** - Reconstructed C# implementations for modder reference
  - Combat system (hit chance, damage, armor)
  - Multiplier math utilities
  - Ready-to-use Harmony patch targets

## Key Findings

### Combat Formula Summary

**Hit Chance:**
```
FinalHitChance = clamp(
    BaseAccuracy × AccuracyMult × CoverMult × DodgeMult + DistancePenalty,
    MinHitChance,
    100
)
```

**Damage:**
```
FinalDamage = (BaseDamage × DamageMult) - ArmorReduction
ArmorReduction = max(0, Armor - ArmorPenetration)
```

### Key Structs

- **EntityProperties**: Central stat container at ~0x1D8 bytes
- **Actor**: Player/AI units, extends Entity, contains suppression/morale
- **Skill**: Runtime skill instance with template reference
- **DamageInfo**: Damage packet passed to targets
- **SaveState**: Binary save file handler with version 101

### Save System

- Save files located in `{UserData}/Saves/*.save`
- Binary format using BinaryWriter/BinaryReader
- `ISaveStateProcessor` interface for all saveable classes
- Version 101, oldest supported: 22
- Complete body format documented with exact serialization order
- Key processors: StrategyState → ShipUpgrades → OwnedItems → BlackMarket → StoryFactions → Squaddies → Roster → BattlePlan → PlanetManager → OperationsManager

### Multiplier System (AddMult)

Multipliers stack **additively**, not multiplicatively:
```c
// FloatExtensions.AddMult @ 0x5320A0
void AddMult(float* acc, float mult) {
    *acc = *acc + (mult - 1.0f);
}
// Example: 1.0 + (1.2 - 1.0) + (1.3 - 1.0) = 1.5 (not 1.56)
```

### Template System

- Templates loaded via `DataTemplateLoader.GetBaseFolder()` → resource path
- Most templates in `Data/` folder structure
- Some templates (configs) are singletons without folder paths
- `ArmyTemplate` (not "ArmyListTemplate") defines army composition

## Analysis Tools

- **Ghidra + MCP**: Decompilation and symbol analysis
- **Il2CppDumper**: Extracted type definitions from global-metadata.dat
- **dump.cs**: C# type dump with field offsets and method signatures

## Generated

Analysis performed: 2025-02-08
Game version: Current Steam build
Tools: Ghidra + Il2CppDumper + MCP
