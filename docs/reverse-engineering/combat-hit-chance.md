# Combat Hit Chance System

## Overview

The hit chance calculation in Menace is handled primarily by `Skill$$GetHitchance` which combines accuracy, cover, dodge, and distance modifiers.

## Core Formula

```
FinalValue = clamp(BaseAccuracy * AccuracyMult * CoverMult * DefenseMult + AccuracyDropoff, MinHitChance, 100)
```

Where:
- All multipliers are clamped to minimum 0 via `FloatExtensions::Clamped`
- `DistancePenalty = |distance - idealRange| * AccuracyDropoff` (only applied when not at ideal range)
- `MinHitChance` is a floor value from EntityProperties

## Key Functions

### Skill$$GetHitchance (0x1806dba90)

Main entry point for hit chance calculation.

```c
// Pseudocode reconstruction
// Signature: GetHitchance(from, targetTile, properties, defenderProperties, includeDropoff, overrideTargetEntity, forImmediateUse)
HitChanceResult GetHitchance(Skill skill, Tile from, Tile targetTile,
                              EntityProperties properties, EntityProperties defenderProperties,
                              bool includeDropoff, Entity overrideTargetEntity, bool forImmediateUse) {

    HitChanceResult result = {};
    Entity target = overrideTargetEntity;

    // Check AlwaysHits flag
    if (skill.SkillTemplate.AlwaysHits) {
        result.FinalValue = 100;
        result.AlwaysHits = true;
        return result;
    }

    if (properties == null) {
        properties = BuildPropertiesForUse(skill);
    }
    float accuracy = properties.GetAccuracy();

    // Get defense modifier from defender (flipped so high defense = low hit)
    float defenseMult = 1.0;
    if (target != null) {
        defenseMult = FloatExtensions.Flipped(defenderProperties.DefenseMult);  // offset +0x44
    }

    float coverMult = GetCoverMult(skill, from, targetTile, target, defenderProperties);

    float hitChance;
    if (includeDropoff && from != null) {
        result.IncludeDropoff = true;
        int distance = from.GetDistanceTo(targetTile);
        int idealRange = skill.SkillTemplate.IdealRange;  // Skill+0xB8
        int rangeDiff = abs(distance - idealRange);

        float dropoff = properties.GetAccuracyDropoff();
        result.AccuracyDropoff = rangeDiff * dropoff;
        hitChance = accuracy * coverMult * defenseMult + result.AccuracyDropoff;
    } else {
        hitChance = accuracy * coverMult * defenseMult;
    }

    // Clamp to valid range
    hitChance = clamp(hitChance, 0, 100);

    // Apply minimum hit chance floor
    int minHitChance = properties.MinHitChance;  // EntityProperties+0x78
    if (hitChance < minHitChance) {
        hitChance = minHitChance;
    }

    result.FinalValue = hitChance;
    result.Accuracy = accuracy;
    result.CoverMult = coverMult;
    result.DefenseMult = defenseMult;
    return result;
}
```

### EntityProperties$$GetAccuracy (0x18060b9f0)

```c
float GetAccuracy(EntityProperties props) {
    float baseAccuracy = props.BaseAccuracy;      // +0x68
    float accuracyMult = Clamped(props.AccuracyMult);  // +0x6c
    return floor(baseAccuracy * accuracyMult);
}
```

### EntityProperties$$GetAccuracyDropoff (0x18060b9c0)

```c
float GetAccuracyDropoff(EntityProperties props) {
    float baseDropoff = props.AccuracyDropoffBase;  // +0x70
    float dropoffMult = Clamped(props.AccuracyDropoffMult);  // +0x74
    return floor(baseDropoff * dropoffMult);
}
```

### Skill$$GetCoverMult (0x1806d9bb0)

Calculates cover effectiveness between attacker and defender.

```c
float GetCoverMult(Skill skill, Tile sourceTile, Tile targetTile,
                   Entity target, EntityProperties defenseProps) {

    float coverMult = 1.0;

    // Check if skill ignores cover
    if (skill.SkillTemplate.IsIgnoringCoverInsideForTarget(target)) {
        return 1.0;
    }

    // Check if skill ignores cover at range (SkillTemplate+0x100)
    if (skill.SkillTemplate.IgnoresCoverAtRange) {
        return 1.0;
    }

    // Cover only applies at distance >= 2
    int distance = sourceTile.GetDistanceTo(targetTile);
    if (distance < 2) {
        return 1.0;
    }

    // Get direction from target to attacker
    int direction = targetTile.GetDirectionTo(sourceTile);

    // Get cover level (0-3) from tile in that direction
    int coverLevel = targetTile.GetCover(direction, target);

    // Look up cover multiplier from Config table
    float[] coverMultipliers = Config.CoverMultipliers;  // Config+0x88
    float coverValue = coverMultipliers[coverLevel];

    // Apply defender's cover usage stat
    float defenderCoverUsage = Clamped(defenseProps.CoverUsage);  // +0x88 area
    coverMult = min(coverValue / defenderCoverUsage, 1.0);

    return AddMult(1.0, coverMult);
}
```

### Tile$$GetCover (0x180680aa0)

Determines cover level from environment and entities.

```c
int GetCover(Tile tile, int direction, Entity attacker) {
    int baseCover = tile.CoverValues[direction];  // Tile+0x28 array

    // Check adjacent tiles for entities providing cover
    Tile nextTile = tile.GetNextTile(direction);
    if (nextTile != null && nextTile.HasEntity) {
        Entity coverEntity = nextTile.GetActor();
        if (coverEntity.ProvidesCover) {
            int providedCover = coverEntity.GetProvidedCover(attacker);
            // Diagonal directions get -1 cover bonus
            if (direction % 2 == 1) {
                providedCover -= 1;
            }
            baseCover = max(baseCover, providedCover);
        }
    }

    // Check diagonal adjacent tiles for partial cover
    if (direction % 2 == 1) {  // Diagonal direction
        // Check both adjacent cardinal directions
        Tile leftTile = tile.GetNextTile((direction - 1) % 8);
        Tile rightTile = tile.GetNextTile((direction + 1) % 8);
        // Similar cover entity checks with -1 modifier
    }

    // Apply tile's inherent cover bonus
    baseCover = max(baseCover, tile.InherentCover);  // Tile+0x20

    // Apply entity's cover usage modifier
    if (attacker != null && baseCover > 0) {
        baseCover += attacker.GetCoverUsage();
    }

    // Clamp to 0-3 range
    return clamp(baseCover, 0, 3);
}
```

## Hitchance Effect (Skills)

The `Hitchance` skill effect modifies EntityProperties when applied:

### Hitchance$$ApplyToEntityProperties (0x180707500)

```c
void ApplyToEntityProperties(HitchanceEffect effect, EntityProperties props) {
    props.BaseAccuracy += effect.AccuracyBonus;      // effect+0x58 -> props+0x68
    AddMult(props.AccuracyMult, effect.AccuracyMult);  // effect+0x5c -> props+0x6c
    props.AccuracyDropoffBase += effect.DropoffBonus;  // effect+0x60 -> props+0x70
    AddMult(props.AccuracyDropoffMult, effect.DropoffMult);  // effect+0x64 -> props+0x74
}
```

## Helper Functions

### FloatExtensions$$AddMult (0x1805320a0)
```c
// Adds a multiplier to accumulator (1.0 is neutral)
void AddMult(float* accumulator, float mult) {
    *accumulator = *accumulator + (mult - 1.0);
}
```

### FloatExtensions$$Clamped (0x1805320c0)
```c
// Clamps to minimum 0
float Clamped(float value) {
    return max(0.0, value);
}
```

### FloatExtensions$$Flipped (0x1805320d0)
```c
// Inverts a multiplier around 1.0 (0.8 becomes 1.2, 1.3 becomes 0.7)
float Flipped(float value) {
    return 1.0 - (value - 1.0);  // = 2.0 - value
}
```

## Struct Offsets

### EntityProperties
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x68 | float | BaseAccuracy | Base accuracy value |
| +0x6c | float | AccuracyMult | Accuracy multiplier (1.0 = 100%) |
| +0x70 | float | AccuracyDropoffBase | Per-tile accuracy penalty base |
| +0x74 | float | AccuracyDropoffMult | Accuracy dropoff multiplier |
| +0x78 | int | MinHitChance | Minimum hit chance floor |

### Skill
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x10 | ptr | SkillTemplate | Reference to skill template |
| +0x18 | ptr | SkillContainer | Parent container |
| +0xB8 | int | IdealRange | Used in distance penalty calculation |

### SkillTemplate
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0xF3 | byte | AlwaysHits | If nonzero, returns 100% hit |
| +0x100 | byte | IgnoresCoverAtRange | If set, cover doesn't apply |

### HitchanceEffect
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x58 | float | AccuracyBonus | Added to BaseAccuracy |
| +0x5c | float | AccuracyMult | Multiplied with AccuracyMult |
| +0x60 | float | DropoffBonus | Added to AccuracyDropoffBase |
| +0x64 | float | DropoffMult | Multiplied with AccuracyDropoffMult |

### Tile
| Offset | Type | Field | Notes |
|--------|------|-------|-------|
| +0x20 | int | InherentCover | Tile's base cover value |
| +0x28 | int[] | CoverValues | Cover per direction (8 directions) |

## Cover Levels

Cover is represented as integers 0-3:
- 0 = No cover
- 1 = Light cover
- 2 = Medium cover
- 3 = Heavy cover

The actual hit chance multipliers are stored in `Config.CoverMultipliers` array at Config+0x88.

## Distance Penalty

The distance penalty system works as follows:
1. Calculate distance from source to target tile
2. Get ideal range from skill template (Skill+0xB8, comes from SkillTemplate)
3. Penalty = |distance - idealRange| * AccuracyDropoff
4. This penalty is ADDED to hit chance (typically negative dropoff values)

Note: Distance penalty is only calculated when the `includeDistance` parameter is true.
