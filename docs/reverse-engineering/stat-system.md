# Stat System & Multipliers

## Overview

Menace uses a layered stat system where base values from templates are modified by multipliers from equipment, skills, and effects. Understanding this system is key to predicting in-game values.

## The AddMult System

The core of Menace's multiplier system is `FloatExtensions.AddMult`:

```c
// Decompiled from FloatExtensions.AddMult (0x5320A0)
void AddMult(float* accumulator, float mult) {
    *accumulator = *accumulator + (mult - 1.0f);
}
```

This means multipliers are **additive** when stacked:
- Neutral value: 1.0 (100%)
- +20% bonus: 1.2
- Two +20% bonuses: 1.0 + (1.2 - 1.0) + (1.2 - 1.0) = 1.4 (not 1.44)
- +20% and -20%: 1.0 + 0.2 + (-0.2) = 1.0 (cancel out)

## Stat Calculation Flow

```
1. Base Value (from EntityTemplate or WeaponTemplate)
2. + Flat Bonuses (from equipment, skills)
3. × Final Multiplier (accumulated via AddMult)
4. = Final Value
```

### Example: Accuracy

```c
// EntityProperties.GetAccuracy (0x60B9F0)
float GetAccuracy() {
    float mult = Clamped(AccuracyMult);  // +0x6C, clamped to valid range
    return floor(Accuracy * mult);        // +0x68 * mult
}
```

Where:
- `Accuracy` (+0x68): Base accuracy from template + flat bonuses
- `AccuracyMult` (+0x6C): Accumulated multiplier from AddMult

## EntityProperties Modification Sources

### 1. EntityTemplate (Base)

The template provides initial values loaded into EntityProperties.

### 2. WeaponTemplate.ApplyToEntityProperties

Weapons add their stats to EntityProperties:

| WeaponTemplate Field | EntityProperties Field |
|---------------------|----------------------|
| +0x14C AccuracyBonus | +0x68 Accuracy |
| +0x154 DamageBonus | +0x118 Damage |
| +0x16C ArmorPenBonus | +0x100 ArmorPenetration |

### 3. ArmorTemplate.ApplyToEntityProperties

Armor adds defensive stats:

| ArmorTemplate Field | EntityProperties Field |
|---------------------|----------------------|
| +0x190 ArmorBonus | +0x1C, +0x20, +0x24 (all armor zones) |
| +0x194 ArmorDurabilityBonus | +0x2C ArmorDurability |
| +0x1A4 AccuracyBonus | +0x68 Accuracy |
| +0x1A8 AccuracyMult | +0x6C AccuracyMult (via AddMult) |

### 4. SkillEventHandler.OnUpdate

Active skills can modify properties each update:

```c
// Example: Hitchance effect (from skills-effects.md)
void ApplyToEntityProperties(EntityProperties props) {
    props.BaseAccuracy += AccuracyBonus;        // +0x68
    AddMult(props.AccuracyMult, AccuracyMult);  // +0x6C
}
```

### 5. Combat Modifiers

Applied during combat calculations:
- Cover bonus/penalty
- Range penalty
- Stance modifiers
- Suppression effects

## Ammo System (Uses)

### Why Ammo Gets "Maxed Out"

You asked why setting ammo on a weapon results in maxed ammo in-game. Here's how it works:

**Skill Class Fields:**
```c
public class Skill : BaseSkill {
    private int m_Uses;        // +0xA8 - Current uses remaining
    private int m_UsesMax;     // +0xAC - Maximum uses
    private int m_UsesConsumed; // +0xB0 - Uses consumed this mission
}
```

**SkillTemplate Fields:**
```c
public class SkillTemplate : DataTemplate {
    public bool IsLimitedUses;  // +0xB8 - Whether skill has limited uses
    public int Uses;            // +0xBC - Base uses from template
}
```

**Initialization Flow:**
1. Skill created from SkillTemplate
2. `m_UsesMax` set from template's `Uses` field
3. `m_Uses` set to `m_UsesMax` (fully loaded)

**Refill Logic (Actor.RefillAmmo @ 0x5E51E0):**
```c
void RefillAmmo(float refillFactor, int minAmount, ISkillFilter filter) {
    foreach (Skill skill in skills) {
        if (!skill.HasLimitedUses()) continue;
        if (filter != null && !filter.Matches(skill)) continue;

        int refillAmount = max(skill.m_UsesMax * refillFactor, minAmount);
        int newUses = skill.m_Uses + refillAmount;

        // Clamp to max
        if (newUses > skill.m_UsesMax) {
            newUses = skill.m_UsesMax;
        }

        skill.m_Uses = newUses;
    }
}
```

**Why It Appears Maxed:**
- At mission start or after resupply, `RefillAmmo(1.0, 0)` is called
- This sets `m_Uses = m_UsesMax`
- The template's `Uses` value becomes the ceiling

**To Modify In-Game Ammo:**
- Change `SkillTemplate.Uses` to affect max capacity
- Hook `Skill.SetMaxUses` or the constructor to modify at runtime
- For one-time modifications, hook `Actor.RefillAmmo`

## Common Stat Relationships

### Damage Calculation

```
FinalDamage = (BaseDamage × DamageMult) - ArmorReduction

Where:
- BaseDamage = EntityProperties.Damage (+0x118)
- DamageMult = EntityProperties.DamageMult (+0x11C)
- ArmorReduction = max(0, TargetArmor - ArmorPenetration)
```

### Hit Chance

```
FinalHitChance = clamp(
    (BaseAccuracy × AccuracyMult) × CoverMult × DodgeMult + DistancePenalty,
    MinHitChance,
    100
)
```

### Distance Penalty

```c
float GetDistancePenalty(int distance, int idealRange) {
    int diff = abs(distance - idealRange);
    float dropoff = GetAccuracyDropoff();  // Per-tile penalty
    return -diff * dropoff;
}
```

### Action Points

```
EffectiveAP = (BaseAP + APBonuses) × APMult

Movement cost = BaseCost + AdditionalMovementCost
Attack cost = (BaseAttackCost + AdditionalAttackCost) × AttackCostMult
```

## Multiplier Clamping

Most multipliers are clamped to prevent extreme values:

```c
float Clamped(float mult) {
    // Implementation varies, but typically:
    return clamp(mult, 0.0f, 10.0f);  // Common range
}
```

Some specific clamp ranges from EntityProperties:
- AccuracyMult: no explicit clamp in getter
- ArmorMult: clamped before calculation
- DamageMult: clamped before calculation

## Debugging Stats

To understand why a stat has a particular value:

1. **Get Base Value**: Check EntityTemplate in asset files
2. **Add Equipment**: Sum flat bonuses from weapon/armor templates
3. **Apply Skill Effects**: Check active skills' OnUpdate contributions
4. **Calculate Multiplier**: Sum all (mult - 1.0) values, add 1.0
5. **Apply Final Mult**: base × finalMult

### Runtime Inspection

Using MelonLoader, you can hook `EntityProperties.GetX()` methods to log the calculation.
