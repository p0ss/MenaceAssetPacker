# Combat Damage System

## Overview

The damage system calculates damage dealt from attacks, considering weapon stats, armor, armor penetration, and various modifiers.

## Core Damage Functions

### EntityProperties$$GetDamage (0x18060bd60)

```c
float GetDamage(EntityProperties props) {
    float baseDamage = props.BaseDamage;      // +0x118
    float damageMult = Clamped(props.DamageMult);  // +0x11c
    return baseDamage * damageMult;
}
```

### EntityProperties$$GetDamageDropoff (0x18060bcd0)

```c
float GetDamageDropoff(EntityProperties props) {
    float baseDropoff = props.DamageDropoffBase;  // +0x120
    float dropoffMult = Clamped(props.DamageDropoffMult);  // +0x124
    return baseDropoff * dropoffMult;
}
```

### EntityProperties$$GetArmor (0x18060bb00)

Returns effective armor value from multiple armor zones.

```c
int GetArmor(EntityProperties props) {
    // Get max of all armor zones
    int armor = props.ArmorFront;    // +0x20
    if (armor <= props.ArmorSide) {  // +0x24
        armor = props.ArmorSide;
    }
    if (armor < props.ArmorBase) {   // +0x1c
        armor = props.ArmorBase;
    }

    float armorMult = Clamped(props.ArmorMult);  // +0x28
    return (int)(armorMult * (float)armor);
}
```

### EntityProperties$$GetArmorValue (0x18060bae0)

Gets armor for a specific facing direction.

```c
int GetArmorValue(EntityProperties props, int direction) {
    switch (direction) {
        case 0:  return props.ArmorBase;   // +0x1c
        case 1:  return props.ArmorFront;  // +0x20
        case 2:  return props.ArmorSide;   // +0x24
        default: return props.ArmorBase;
    }
}
```

### EntityProperties$$GetArmorPenetration (0x18060bab0)

```c
float GetArmorPenetration(EntityProperties props) {
    float basePen = props.ArmorPenBase;      // +0x100
    float penMult = Clamped(props.ArmorPenMult);  // +0x104
    return basePen * penMult;
}
```

### EntityProperties$$GetDamageToArmorDurability (0x18060bd30)

Damage that degrades armor durability (anti-armor damage).

```c
float GetDamageToArmorDurability(EntityProperties props) {
    float baseDmg = props.ArmorDurabilityDmgBase;  // +0x12c (300 decimal)
    float dmgMult = Clamped(props.ArmorDurabilityDmgMult);  // +0x130
    return baseDmg * dmgMult;
}
```

### EntityProperties$$GetDamageToArmorDurabilityDropoff (0x18060bd00)

```c
float GetDamageToArmorDurabilityDropoff(EntityProperties props) {
    float baseDropoff = props.ArmorDurDmgDropoffBase;  // +0x134
    float dropoffMult = Clamped(props.ArmorDurDmgDropoffMult);  // +0x138
    return baseDropoff * dropoffMult;
}
```

## DamageHandler$$ApplyDamage (0x180702970)

Main function that applies damage from a skill to an entity.

Key operations:
1. Gets attack EntityProperties from skill
2. Calculates final damage values with range modifiers
3. Creates DamageInfo struct
4. Applies damage to target entity

```c
// Pseudocode from decompilation
void ApplyDamage(SkillEventHandler handler) {
    Entity target = handler.GetEntity();
    EntityProperties attackProps = handler.AttackProperties;  // +0x18

    // Calculate damage with range modifier
    int distance = target.DistanceToAttacker;  // +0x14
    float baseDmg = attackProps.BaseDamage * distance;  // +0x68
    float minDamage = attackProps.MinDamage;  // +0x6c
    if (minDamage <= baseDmg) {
        minDamage = baseDmg;
    }

    // Similar for armor durability damage
    float armorDmg = target.ArmorElements * attackProps.ArmorDurabilityDmg;  // +0x70
    float minArmorDmg = attackProps.MinArmorDmg;  // +0x74

    // Create DamageInfo
    DamageInfo dmgInfo = new DamageInfo();

    // Set shot count from attack props
    int shotCount = attackProps.ShotCount;  // +0x5c
    int targetElements = target.ElementCount;  // +0x18
    float shotsPerElement = attackProps.ShotsPerElement;  // +0x60
    int totalShots = ceil(targetElements * shotsPerElement) + shotCount;
    if (totalShots < 1) totalShots = 1;

    dmgInfo.TotalShots = totalShots;  // +0x1c
    dmgInfo.Damage = minDamage + attackProps.BaseDamageBonus + minArmorDmg;  // +0x64, +0xc
    dmgInfo.ArmorPen = attackProps.ArmorPenetration;  // +0x78 -> +0x14
    dmgInfo.ArmorDurDmg = attackProps.ArmorDurabilityDmg;  // +0x7c -> +0x18

    // Copy additional properties
    dmgInfo.Unknown1 = attackProps.Unknown1;  // +0x80 -> +0x18
    dmgInfo.Unknown2 = attackProps.Unknown2;  // +0x84 -> +0x20
    dmgInfo.Unknown3 = attackProps.Unknown3;  // +0x88 -> +0x24
    dmgInfo.Unknown4 = attackProps.Unknown4;  // +0x8c -> +0x1c
    dmgInfo.CanDismember = attackProps.CanDismember;  // +0x90 -> +0x2e

    // Apply damage to entity
    target.OnDamageReceived(handler.Skill, dmgInfo);
}
```

## WeaponTemplate$$ApplyToEntityProperties (0x180563080)

How weapons modify EntityProperties for attacks:

```c
void ApplyToEntityProperties(WeaponTemplate weapon, EntityProperties props) {
    // Accuracy
    props.BaseAccuracy += weapon.AccuracyBonus;           // +0x14c -> +0x68
    props.AccuracyDropoffBase += weapon.AccuracyDropoff;  // +0x150 -> +0x70

    // Damage
    props.BaseDamage += weapon.DamageBonus;               // +0x154 -> +0x118
    props.DamageDropoffBase += weapon.DamageDropoff;      // +0x158 -> +0x120

    // Suppression/Other
    props.Unknown1 += weapon.Unknown1;                    // +0x15c -> +0x144
    props.Unknown2 += weapon.Unknown2;                    // +0x160 -> +0x148
    props.Unknown3 += weapon.Unknown3;                    // +0x164 -> +0x14c
    props.Unknown4 += weapon.Unknown4;                    // +0x168 -> +0x150

    // Armor penetration
    props.ArmorPenBase += weapon.ArmorPenBonus;          // +0x16c -> +0x100
    props.ArmorPenDropoff += weapon.ArmorPenDropoff;     // +0x170 -> +0x108

    // Armor durability damage
    props.ArmorDurDmgBase += weapon.ArmorDurDmgBonus;    // +0x174 -> +0x12c
    AddMult(props.ArmorDurDmgMult, weapon.ArmorDurDmgMult);  // +0x178 -> +0x130
    props.ArmorDurDmgDropoff += weapon.ArmorDurDmgDropoffBonus;  // +0x17c -> +0x134
    AddMult(props.ArmorDurDmgDropoffMult, weapon.ArmorDurDmgDropoffMult);  // +0x180 -> +0x138

    // Other modifiers
    props.Unknown5 += weapon.Unknown5;                   // +0x184 -> +0xe0
}
```

## ArmorTemplate$$ApplyToEntityProperties (0x1805488f0)

How armor modifies defensive EntityProperties:

```c
void ApplyToEntityProperties(ArmorTemplate armor, EntityProperties props) {
    // Base armor values (all zones get same bonus)
    props.ArmorBase += armor.ArmorBonus;   // +0x190 -> +0x1c
    props.ArmorFront += armor.ArmorBonus;  // +0x190 -> +0x20
    props.ArmorSide += armor.ArmorBonus;   // +0x190 -> +0x24

    // Armor durability
    props.ArmorDurability += armor.ArmorDurabilityBonus;  // +0x194 -> +0x2c

    // Dodge (inverted - high dodge penalty = low dodge)
    float dodgePenalty = Clamped(1.0 - armor.DodgePenalty);  // +0x198
    AddMult(props.DodgeMult, dodgePenalty);  // -> +0x8c

    // Health
    props.Health += armor.HealthBonus;  // +0x19c -> +0x14
    AddMult(props.HealthMult, armor.HealthMult);  // +0x1a0 -> +0x18

    // Accuracy (armor can affect aim)
    props.BaseAccuracy += armor.AccuracyBonus;  // +0x1a4 -> +0x68
    AddMult(props.AccuracyMult, armor.AccuracyMult);  // +0x1a8 -> +0x6c

    // Evasion
    AddMult(props.EvasionMult, armor.EvasionMult);  // +0x1ac -> +0x84

    // Various resistances
    props.ResistBase += armor.ResistBonus;  // +0x1b0 -> +0xa0
    AddMult(props.ResistMult, armor.ResistMult);  // +0x1b4 -> +0xa4

    // Morale
    props.MoraleBase += armor.MoraleBonus;  // +0x1b8 -> +0xc4
    AddMult(props.MoraleMult, armor.MoraleMult);  // +0x1bc -> +0xc8

    // Suppression resistance
    props.SuppressionResist += armor.SuppressionResistBonus;  // +0x1c0 -> +0xcc
    AddMult(props.SuppressionResistMult, armor.SuppressionResistMult);  // +0x1c4 -> +0xd0

    // Other
    props.Unknown += armor.Unknown;  // +0x1c8 -> +0xd4
    AddMult(props.UnknownMult, armor.UnknownMult);  // +0x1cc -> +0xd8
    AddMult(props.MoveSpeedMult, armor.MoveSpeedMult);  // +0x1d0 -> +0xdc

    // Additional bonuses
    props.SightRange += armor.SightRangeBonus;  // +0x1d4 -> +0x154
    AddMult(props.SightRangeMult, armor.SightRangeMult);  // +0x1d8 -> +0x158

    props.ActionPoints += armor.ActionPointBonus;  // +0x1dc -> +0x34
    props.MovementPoints += armor.MovementBonus;   // +0x1e4 -> +0x3c
    AddMult(props.MovementMult, armor.MovementMult);  // +0x1e0 -> +0x38
}
```

## Struct Offsets

### EntityProperties - Damage Related
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x14 | int | Health | Current/max health |
| +0x18 | float | HealthMult | Health multiplier |
| +0x1c | int | ArmorBase | Base armor value |
| +0x20 | int | ArmorFront | Frontal armor |
| +0x24 | int | ArmorSide | Side armor |
| +0x28 | float | ArmorMult | Armor multiplier |
| +0x2c | float | ArmorDurability | Current armor durability |
| +0x100 | float | ArmorPenBase | Armor penetration base |
| +0x104 | float | ArmorPenMult | Armor penetration multiplier |
| +0x108 | float | ArmorPenDropoff | Armor pen dropoff per tile |
| +0x118 | float | BaseDamage | Base damage value |
| +0x11c | float | DamageMult | Damage multiplier |
| +0x120 | float | DamageDropoffBase | Damage dropoff base |
| +0x124 | float | DamageDropoffMult | Damage dropoff multiplier |
| +0x12c | float | ArmorDurDmgBase | Anti-armor damage base |
| +0x130 | float | ArmorDurDmgMult | Anti-armor damage mult |
| +0x134 | float | ArmorDurDmgDropoff | Anti-armor dropoff base |
| +0x138 | float | ArmorDurDmgDropoffMult | Anti-armor dropoff mult |

### WeaponTemplate
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x13C | int | MinRange | From schema |
| +0x140 | int | IdealRange | From schema |
| +0x144 | int | MaxRange | From schema |
| +0x14c | float | AccuracyBonus | Base accuracy bonus |
| +0x150 | float | AccuracyDropoff | Accuracy drop per tile |
| +0x154 | float | DamageBonus | Base damage bonus |
| +0x158 | float | DamageDropoff | Damage drop per tile |
| +0x16c | float | ArmorPenBonus | Armor penetration bonus |
| +0x170 | float | ArmorPenDropoff | AP drop per tile |
| +0x174 | float | ArmorDurDmgBonus | Anti-armor damage |
| +0x178 | float | ArmorDurDmgMult | Anti-armor multiplier |
| +0x17c | float | ArmorDurDmgDropoff | Anti-armor dropoff |
| +0x180 | float | ArmorDurDmgDropoffMult | Anti-armor dropoff mult |
| +0x184 | float | Unknown | Applied to +0xe0 |

### ArmorTemplate
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x190 | int | ArmorBonus | Added to all armor zones |
| +0x194 | int | ArmorDurabilityBonus | Added to durability |
| +0x198 | float | DodgePenalty | Reduces dodge (inverted) |
| +0x19c | int | HealthBonus | Added to health |
| +0x1a0 | float | HealthMult | Health multiplier |
| +0x1a4 | int | AccuracyBonus | Added to accuracy |
| +0x1a8 | float | AccuracyMult | Accuracy multiplier |

### DamageInfo Struct
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0xc | int | Damage | Final damage value |
| +0x14 | int | ArmorPenetration | AP value |
| +0x18 | int | ArmorDurabilityDamage | Anti-armor damage |
| +0x1c | int | TotalShots | Number of shots/hits |
| +0x2e | byte | CanDismember | Whether can cause dismemberment |

## Damage Flow

1. **Skill activation** triggers `DamageHandler$$ApplyDamage`
2. **EntityProperties** are built from:
   - Entity base stats
   - Equipped weapon stats
   - Active skill effects
   - Buffs/debuffs
3. **Damage calculation** considers:
   - Base damage × damage mult
   - Distance penalty (if applicable)
   - Shot count and elements
4. **Armor resolution** (in target's OnDamageReceived):
   - Compare armor penetration vs armor value
   - Reduce damage based on armor effectiveness
   - Apply armor durability damage
5. **Final damage** applied to target health
