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

## Key Offsets Reference

See [offsets.md](./offsets.md) for a consolidated list of struct field offsets discovered during analysis.

## Documentation Index

### Combat System
- **[combat-hit-chance.md](./combat-hit-chance.md)** - Hit chance calculation, accuracy, cover multipliers, distance penalties
- **[combat-damage.md](./combat-damage.md)** - Damage calculation, armor penetration, weapon/armor template mappings

### Unit State
- **[suppression-morale.md](./suppression-morale.md)** - Suppression states, morale states, related methods
- **[entity-properties.md](./entity-properties.md)** - Complete EntityProperties field layout with all combat stats

### Game Systems
- **[skills-effects.md](./skills-effects.md)** - SkillTemplate structure, SkillEventHandler system, effect types
- **[ai-decisions.md](./ai-decisions.md)** - Agent class, behavior system, utility-based decision making
- **[template-loading.md](./template-loading.md)** - DataTemplateLoader, resource paths, template hierarchy

### Infrastructure
- **[save-system.md](./save-system.md)** - Complete save file format, StrategyState serialization order, all nested processors (Roster, OwnedItems, Squaddies, etc.), parser implementation guide
- **[stat-system.md](./stat-system.md)** - Multiplier system (AddMult), stat calculation flow, ammo/uses
- **[ui-system.md](./ui-system.md)** - UITacticalHUD, adding custom HUD elements, settings screens
- **[offset-stability.md](./offset-stability.md)** - API considerations, version compatibility, best practices
- **[asset-references.md](./asset-references.md)** - Template hierarchy, sprite/prefab references, finding asset usages
- **[terrain-generation.md](./terrain-generation.md)** - Map generation pipeline, ChunkGenerator, MapMagic integration, road/cover placement

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
