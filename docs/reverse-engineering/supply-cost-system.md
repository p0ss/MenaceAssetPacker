# Supply Cost System

## Overview

The supply cost system in Menace determines how many supply points are required to deploy units in tactical missions. This document explains the different cost fields, where they're located, and how they interact.

## Key Insight

**Player units and enemy units use different cost systems:**

| Unit Type | Primary Cost Field | Location |
|-----------|-------------------|----------|
| Player Units | `FixedSupplyCost` | UnitLeaderTemplate |
| Enemy Units | `DeployCostsPerElement` + `DeployCosts` | EntityTemplate |

## Player Unit Costs (UnitLeaderTemplate)

Player units (squad leaders, pilots) are deployed via the roster system. Their supply cost comes from `UnitLeaderTemplate.FixedSupplyCost`.

### Field Location
```
UnitLeaderTemplate
├── FixedSupplyCost (int)     // Primary supply cost (4, 10, 20, 40, 80)
├── HiringCosts (int)         // Credits to hire (offset 0x90)
├── PromotionTax (int)        // XP cost for promotion
└── SquaddieTax (int)         // Cost per additional squaddie
```

### Example Values
| Unit | FixedSupplyCost |
|------|-----------------|
| squad_leader.carda | 4 |
| squad_leader.pike | 20 |
| squad_leader.darby | 40 |
| pilot.achilleas | 80 |

### Calculation (BaseUnitLeader.GetDeployCosts)

The game calls `GetDeployCosts()` on the unit leader, which:

1. Gets base cost from template fields
2. Multiplies by squaddie count
3. Applies `EntityProperties.DeployCostMult` (at offset 0xFC)
4. Applies operation-level multipliers via `GetDeployCostMult()`

```
Address: 0x1805B08A0 (Menace.Strategy.BaseUnitLeader$$GetDeployCosts)

FinalCost = (FixedSupplyCost + SquaddieTax × SquaddieCount)
          × EntityProperties.DeployCostMult
          × OperationDeployCostMult
```

## Enemy Unit Costs (EntityTemplate)

Enemy units use a different system for army generation.

### Field Locations
```
EntityTemplate
├── DeployCostsPerElement (int, offset 0xAC)  // Cost per squad element
├── DeployCosts (int, offset 0xB0)            // Flat additional cost
├── ArmyPointCost (int, offset 0xB4)          // Army budget points
└── Properties → DeployCostMult (float)       // Runtime multiplier
```

### Example Values
| Entity | DeployCostsPerElement | DeployCosts |
|--------|----------------------|-------------|
| player_squad.carda | 1 | 0 |
| enemy.pirate_grunt | 10 | 0 |
| enemy.alien_warrior | 120 | 6 |
| player_vehicle.modular_piratetruck | 150 | 0 |

### Enemy Cost Calculation
```
EnemyCost = (DeployCostsPerElement × ElementCount) + DeployCosts
```

## EntityProperties.DeployCostMult

**Important:** This field is **always 1.0** in templates.

### Location
- Type: `float`
- Offset: `0xFC` within EntityProperties
- Default: `1.0f` (set in constructor)

### Why It's Always 1.0

The `DeployCostMult` in templates is a baseline. Cost modifiers are applied **dynamically at runtime** through:

1. **Ship Upgrades** (e.g., "Automated Supply Compartments" - reduces vehicle costs)
2. **Strategic Assets** (e.g., "Supply Routes Secured" - reduces infantry costs)
3. **Difficulty Settings**

### Ghidra Analysis
```c
// EntityProperties constructor (0x18060D920)
*(float *)(this + 0xFC) = 1.0f;  // DeployCostMult default

// GetPropertyValue case 0x40 (DeployCostMult)
case 0x40:
    return *(float *)(this + 0xFC);

// UpdateMultProperty case 0x40
case 0x40:
    FloatExtensions.AddMult(this + 0xFC, value);
    return;
```

## Operation-Level Modifiers (OperationPropertyMultType)

These modifiers are applied at the operation/mission level and stack with template values.

### Enum Values
```c
enum OperationPropertyMultType {
    Unused = 0,
    DeployCostMult = 1,              // Global cost modifier
    PromotionPointsMult = 2,
    EnemyArmyPointsMult = 3,
    OperationRatingMult = 4,
    OCIPointsMult = 5,
    PlayerRankPointsMult = 6,
    DeployCostInfantryMult = 7,      // Infantry-specific
    DeployCostVehicleMult = 8,       // Vehicle-specific
    DeployCostInfantryAccessoryMult = 9,
    DeployCostVehicleAccessoryMult = 10
}
```

### GetDeployCostMult (SquadLeader)
```c
// Address: 0x1805C1490
float GetDeployCostMult(OperationProperties opProps) {
    float result = opProps.GetClamped(DeployCostMult);        // Global
    float infantry = opProps.GetClamped(DeployCostInfantryMult);
    FloatExtensions.AddMult(ref result, infantry);
    return result;
}
```

## Complete Cost Calculation Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                    PLAYER UNIT DEPLOYMENT                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  UnitLeaderTemplate                                             │
│  ├── FixedSupplyCost (e.g., 20)                                │
│  └── SquaddieTax × SquaddieCount                               │
│              ↓                                                  │
│  EntityProperties.DeployCostMult (1.0 default)                 │
│              ↓                                                  │
│  OperationProperties                                            │
│  ├── DeployCostMult (global, from ship upgrades)               │
│  └── DeployCostInfantryMult (from strategic assets)            │
│              ↓                                                  │
│  FINAL SUPPLY COST                                              │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## Modding Guide

### To Change a Player Unit's Supply Cost

Modify `UnitLeaderTemplate.FixedSupplyCost`:

```json
{
  "type": "UnitLeaderTemplate",
  "instance": "squad_leader.carda",
  "field": "FixedSupplyCost",
  "value": "10"
}
```

### To Change Enemy Unit Costs

Modify `EntityTemplate` fields:

```json
{
  "type": "EntityTemplate",
  "instance": "enemy.pirate_grunt",
  "fields": {
    "DeployCostsPerElement": 15,
    "DeployCosts": 5
  }
}
```

### To Apply a Global Modifier

Use skills/effects that call `UpdateMultProperty` with `DeployCostMult` (type 64/0x40):

```c
// This is done by ship upgrades and strategic assets
EntityProperties.UpdateMultProperty(DeployCostMult, 0.9f);  // -10% cost
```

## Related Files

- `entity-properties.md` - Full EntityProperties field layout
- `army-generation.md` - How enemy armies are generated
- `roster-system.md` - Player unit management

## Key Addresses (Ghidra)

| Function | Address |
|----------|---------|
| BaseUnitLeader.GetDeployCosts | 0x1805B08A0 |
| SquadLeader.GetDeployCostMult | 0x1805C1490 |
| Pilot.GetDeployCostMult | 0x1805C0890 |
| EntityProperties.GetPropertyValue | 0x18060BEF0 |
| EntityProperties.UpdateMultProperty | 0x18060CC80 |
| EntityProperties..ctor | 0x18060D920 |
