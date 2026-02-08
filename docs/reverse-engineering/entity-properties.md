# Entity Properties System

## Overview

`EntityProperties` is the central stat container in Menace, holding all combat-relevant attributes for entities. Properties can be base values (from templates) or modified values (after applying equipment, skills, effects).

## IEntityProperties Interface

Entities implement this interface to provide property access:

```c
public interface IEntityProperties {
    bool HasTile();
    EntityTemplate GetTemplate();
    EntityProperties GetCurrentProperties();  // After all modifiers
    EntityProperties GetBaseProperties();     // Template baseline
    SkillContainer GetSkills();
    ItemContainer GetItems();
    bool IsActor();
    bool IsBuilding();
    bool IsVegetation();
    bool IsInfantry();
    bool IsVehicle();
    bool IsTurret();
    bool HasTag(TagType tag);
    void SetCurrentProperties(EntityProperties p);
}
```

## EntityProperties Full Field Layout

### Core Stats
| Offset | Type | Field | Range | Notes |
|--------|------|-------|-------|-------|
| +0x10 | int | MaxElements | - | Max squad size |
| +0x14 | int | HitpointsPerElement | 1-100000 | HP per element |
| +0x18 | float | HitpointsPerElementMult | - | HP multiplier |

### Armor
| Offset | Type | Field | Range | Notes |
|--------|------|-------|-------|-------|
| +0x1C | int | Armor | 0-9000 | Front/base armor |
| +0x20 | int | ArmorSide | 0-9000 | Side armor |
| +0x24 | int | ArmorBack | 0-9000 | Rear armor |
| +0x28 | float | ArmorMult | - | Armor multiplier |
| +0x2C | float | ArmorDurabilityPerElement | 0-9000 | Armor durability |
| +0x30 | float | ArmorDurabilityPerElementMult | - | Durability mult |

### Action Economy
| Offset | Type | Field | Range | Notes |
|--------|------|-------|-------|-------|
| +0x34 | int | ActionPoints | 0-1000 | Base action points |
| +0x38 | float | ActionPointsMult | - | AP multiplier |
| +0x3C | int | AdditionalMovementCost | 0-100 | Extra move cost |
| +0x40 | int | AdditionalTurningCost | -100-100 | Extra turn cost |
| +0x44 | int | AdditionalAttackCost | 0-100 | Extra attack cost |
| +0x48 | float | AttackCostMult | 0-10 | Attack cost mult |
| +0x4C | int | AdditionalMalfunctionChance | 0-100 | Weapon jam chance |
| +0x50 | int | APEnterCost | - | Cost to enter container |
| +0x54 | int | APLeaveCost | - | Cost to leave container |
| +0x58 | int | LowestModifiedMovementCosts | - | Cached min move cost |
| +0x5C | float | BackwardsMovementMult | - | Backwards move penalty |
| +0x60 | int[] | ModifiedMovementCosts | - | Surface movement costs |

### Accuracy & Hit Chance
| Offset | Type | Field | Range | Notes |
|--------|------|-------|-------|-------|
| +0x68 | float | Accuracy | -200-200 | Base accuracy |
| +0x6C | float | AccuracyMult | - | Accuracy multiplier |
| +0x70 | float | AccuracyDropoff | -100-100 | Per-tile penalty |
| +0x74 | float | AccuracyDropoffMult | -10-10 | Dropoff multiplier |
| +0x78 | int | HitchanceMin | 0-100 | Minimum hit chance |
| +0x7C | int | CriticalChance | - | Critical hit chance |
| +0x80 | float | CriticalDamageMult | - | Critical damage mult |

### Defense
| Offset | Type | Field | Range | Notes |
|--------|------|-------|-------|-------|
| +0x84 | float | DefenseMult | 0-10 | Defense multiplier |
| +0x88 | float | CoverEffectivenessMult | 0-10 | Cover effectiveness |
| +0x8C | float | DamageSustainedMult | 0-10 | Damage taken mult |
| +0x90 | float | DamageSustainedSquadLeaderMult | 0-10 | Leader damage mult |
| +0x94 | int | ProvidedCoverBonus | - | Cover provided to allies |
| +0x98 | int | CoverTypeOffset | - | Cover type modifier |
| +0x9C | int | CoverGainedByVehicleOffset | - | Vehicle cover bonus |

### Morale & Discipline
| Offset | Type | Field | Range | Notes |
|--------|------|-------|-------|-------|
| +0xA0 | float | Discipline | 0-1000 | Base discipline |
| +0xA4 | float | DisciplineMult | - | Discipline mult |
| +0xA8 | MoraleEvent | MoraleEvents | - | Morale event flags |
| +0xAC | float | MoraleBonus | 0-1000 | Morale bonus |
| +0xB0 | float | MoraleMult | - | Morale multiplier |
| +0xB4 | float | MoraleRecoveryMult | 0-10 | Recovery speed |
| +0xB8 | float | DamageToMoraleMult | - | Damage->morale impact |
| +0xBC | float | MoraleImpactMult | 0-10 | Morale impact mult |
| +0xC0 | int | MoraleStateOffset | -2-2 | State threshold offset |

### Vision & Detection
| Offset | Type | Field | Range | Notes |
|--------|------|-------|-------|-------|
| +0xC4 | int | Vision | 0-99 | Sight range |
| +0xC8 | float | VisionMult | - | Vision multiplier |
| +0xCC | int | Detection | -99-99 | Detection ability |
| +0xD0 | float | DetectionMult | - | Detection mult |
| +0xD4 | int | Concealment | -99-99 | Stealth value |
| +0xD8 | float | ConcealmentMult | - | Concealment mult |

### Suppression
| Offset | Type | Field | Range | Notes |
|--------|------|-------|-------|-------|
| +0xDC | float | SuppressionImpactMult | 0-10 | Suppression resist |
| +0xE0 | float | SuppressionDealt | - | Suppression damage |
| +0xE4 | float | SuppressionDealtMult | 0-10 | Suppression mult |
| +0xE8 | float | SuppressionDealtDropoffAOE | - | AOE dropoff |

### Flags & Modifiers
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0xEC | EntityFlags | Flags | Entity capability flags |
| +0xF0 | float | PromotionCostMult | Promotion cost modifier |
| +0xF4 | float | DeploymentZoneMult | Deployment zone size |
| +0xF8 | int | DeploymentZoneMinExtend | Min deployment extension |
| +0xFC | float | DeployCostMult | Deployment cost modifier |

### Armor Penetration
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x100 | float | ArmorPenetration | Base armor pen |
| +0x104 | float | ArmorPenetrationMult | AP multiplier |
| +0x108 | float | ArmorPenetrationDropoff | Per-tile AP loss |
| +0x10C | float | ArmorPenetrationDropoffMult | Dropoff mult |
| +0x110 | float | ArmorPenetrationDropoffAOE | AOE AP dropoff |
| +0x114 | float | IgnoreCoverMult | Cover ignore % |

### Damage
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x118 | float | Damage | Base damage |
| +0x11C | float | DamageMult | Damage multiplier |
| +0x120 | float | DamageDropoff | Per-tile damage loss |
| +0x124 | float | DamageDropoffMult | Dropoff mult |
| +0x128 | float | DamageDropoffAOE | AOE damage dropoff |
| +0x12C | float | DamageToArmorDurability | Anti-armor damage |
| +0x130 | float | DamageToArmorDurabilityMult | Anti-armor mult |
| +0x134 | float | DamageToArmorDurabilityDropoff | Anti-armor dropoff |
| +0x138 | float | DamageToArmorDurabilityDropoffMult | Dropoff mult |
| +0x13C | float | DamageToArmorDurabilityDropoffAOE | AOE dropoff |
| +0x140 | float | TotalDamageMult | Final damage mult |
| +0x144 | float | DamagePctCurrentHitpoints | % current HP damage |
| +0x148 | float | DamagePctCurrentHitpointsMin | Min % HP damage |
| +0x14C | float | DamagePctMaxHitpoints | % max HP damage |
| +0x150 | float | DamagePctMaxHitpointsMin | Min % max HP damage |

### Dismemberment
| Offset | Type | Field | Range | Notes |
|--------|------|-------|-------|-------|
| +0x154 | int | GetDismemberedChanceBonus | 0-100 | Dismember chance |
| +0x158 | float | GetDismemberedChanceMult | 0-3 | Dismember mult |
| +0x15C | int | GetDismemberedMinParts | 1-10 | Min parts |
| +0x160 | int | GetDismemberedMaxParts | 1-10 | Max parts |
| +0x164 | int | DismemberChance | - | Final chance |
| +0x168 | RagdollHitArea | DismemberArea | - | Hit area |

### Elements & Targeting
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x16C | int | ElementsHit | Elements hit per attack |
| +0x170 | float | ElementsHitPct | % of elements hit |
| +0x174 | int | ReduceElementsHit | Reduce elements targeted |
| +0x178 | int | DefectThresholdOffset | Defection threshold |
| +0x17C | FatalityType | FatalityType | Death animation type |
| +0x180 | DamageVisualizationType | DamageVisualizationType | Damage visual |
| +0x184 | float | AIPriorityMult | AI targeting priority |

### Sounds
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x188 | ID | SoundOnMovementStart | Movement start sound |
| +0x190 | ID | SoundOnMovementStop | Movement stop sound |
| +0x198 | ID | SoundOnMovementStep | Footstep sound |
| +0x1A0 | ID | SoundOnMovementSymbolic | Symbolic move sound |
| +0x1A8 | SurfaceSoundsTemplate | SoundOnMovementStepOverrides2 | Surface-specific sounds |
| +0x1B0 | ID | SoundOnArmorHit | Armor hit sound |
| +0x1B8 | ID | SoundOnHitpointsHit | HP damage sound |
| +0x1C0 | ID | SoundOnHitpointsHitFemale | Female damage sound |
| +0x1C8 | ID | SoundOnDeath | Death sound |
| +0x1D0 | ID | SoundOnDeathFemale | Female death sound |

## Key Methods

### GetAccuracy (0x60B9F0)
```c
float GetAccuracy() {
    float mult = Clamped(AccuracyMult);  // +0x6C
    return floor(Accuracy * mult);        // +0x68
}
```

### GetAccuracyDropoff (0x60B9C0)
```c
float GetAccuracyDropoff() {
    float mult = Clamped(AccuracyDropoffMult);  // +0x74
    return floor(AccuracyDropoff * mult);        // +0x70
}
```

### GetArmor (0x60BB00)
```c
int GetArmor() {
    int armor = max(Armor, ArmorSide, ArmorBack);
    float mult = Clamped(ArmorMult);
    return (int)(armor * mult);
}
```

### GetArmorValue (0x60BAE0)
```c
int GetArmorValue(int direction) {
    switch (direction) {
        case 0: return Armor;      // Front
        case 1: return ArmorSide;
        case 2: return ArmorBack;
        default: return Armor;
    }
}
```

### GetDamage (0x60BD60)
```c
float GetDamage() {
    float mult = Clamped(DamageMult);
    return Damage * mult;
}
```

### GetDamageDropoff (0x60BCD0)
```c
float GetDamageDropoff() {
    float mult = Clamped(DamageDropoffMult);
    return DamageDropoff * mult;
}
```

### GetArmorPenetration (0x60BAB0)
```c
float GetArmorPenetration() {
    float mult = Clamped(ArmorPenetrationMult);
    return ArmorPenetration * mult;
}
```

## Property Modification Flow

1. **Base Properties**: From EntityTemplate
2. **Equipment**: Weapon/Armor `ApplyToEntityProperties()`
3. **Skills**: Passive skill effects via `OnUpdate()`
4. **Buffs/Debuffs**: Active status effects
5. **Combat Modifiers**: Range, cover, stance

## Multiplier System

The game uses `AddMult` for multiplicative stacking:
```c
// Neutral = 1.0, values stack additively
void AddMult(ref float mult, float value) {
    mult = mult + (value - 1.0);
}

// Example: 1.0 + (1.2 - 1.0) + (0.8 - 1.0) = 1.0
// Two mults of +20% and -20% cancel out
```

## EntityFlags

Capability flags that modify entity behavior:
- Movement restrictions
- Special abilities
- Damage type immunities
- etc.
