# DamageCalculation.cs Verification Report

**Date:** 2026-03-10
**File:** `/home/poss/Documents/Code/Menace/MenaceAssetPacker/docs/reference-code/Combat/DamageCalculation.cs`
**Status:** MULTIPLE DISCREPANCIES FOUND

---

## Summary

| Function | Address | Status | Severity |
|----------|---------|--------|----------|
| `GetDamage()` | 0x18060bd60 | MATCH | - |
| `GetDamageDropoff()` | 0x18060bcd0 | MATCH | - |
| `GetArmor()` | 0x18060bb00 | DISCREPANCY | Medium |
| `GetArmorValue()` | 0x18060bae0 | MATCH | - |
| `GetArmorPenetration()` | 0x18060bab0 | MATCH | - |
| `GetDamageToArmorDurability()` | 0x18060bd30 | MATCH | - |
| `ApplyDamage()` | 0x180702970 | MAJOR DISCREPANCY | Critical |

---

## Detailed Analysis

### 1. GetDamage() - Address: 0x18060bd60

**Status:** MATCH

**Decompiled:**
```c
float Menace_Tactical_EntityProperties__GetDamage(longlong param_1)
{
  float fVar1;
  float fVar2;

  fVar1 = *(float *)(param_1 + 0x118);
  fVar2 = (float)Menace_Tools_FloatExtensions__Clamped(*(undefined4 *)(param_1 + 0x11c),0);
  return fVar2 * fVar1;
}
```

**Reference Code:**
```csharp
public float GetDamage()
{
    float clampedMult = FloatExtensions.Clamped(DamageMult);
    return BaseDamage * clampedMult;
}
```

**Analysis:** The reference code correctly documents:
- `BaseDamage` at offset `+0x118`
- `DamageMult` at offset `+0x11C`
- Formula: `BaseDamage * Clamped(DamageMult)`

---

### 2. GetDamageDropoff() - Address: 0x18060bcd0

**Status:** MATCH

**Decompiled:**
```c
float Menace_Tactical_EntityProperties__GetDamageDropoff(longlong param_1)
{
  float fVar1;
  float fVar2;

  fVar1 = *(float *)(param_1 + 0x120);
  fVar2 = (float)Menace_Tools_FloatExtensions__Clamped(*(undefined4 *)(param_1 + 0x124),0);
  return fVar2 * fVar1;
}
```

**Reference Code:**
```csharp
public float GetDamageDropoff()
{
    float clampedMult = FloatExtensions.Clamped(DamageDropoffMult);
    return DamageDropoffBase * clampedMult;
}
```

**Analysis:** The reference code correctly documents:
- `DamageDropoffBase` at offset `+0x120`
- `DamageDropoffMult` at offset `+0x124`
- Formula: `DamageDropoffBase * Clamped(DamageDropoffMult)`

---

### 3. GetArmor() - Address: 0x18060bb00

**Status:** DISCREPANCY - Logic Order Incorrect

**Decompiled:**
```c
int Menace_Tactical_EntityProperties__GetArmor(longlong param_1)
{
  int iVar1;
  float fVar2;

  iVar1 = *(int *)(param_1 + 0x20);                    // ArmorFront
  if (*(int *)(param_1 + 0x20) <= *(int *)(param_1 + 0x24)) {  // if ArmorFront <= ArmorSide
    iVar1 = *(int *)(param_1 + 0x24);                  // use ArmorSide
  }
  if (iVar1 < *(int *)(param_1 + 0x1c)) {              // if max(Front,Side) < ArmorBase
    iVar1 = *(int *)(param_1 + 0x1c);                  // use ArmorBase
  }
  fVar2 = (float)Menace_Tools_FloatExtensions__Clamped(*(undefined4 *)(param_1 + 0x28),0);
  return (int)(fVar2 * (float)iVar1);
}
```

**Reference Code:**
```csharp
public int GetArmor()
{
    // Get maximum armor from all zones
    int armor = ArmorBase;
    if (ArmorFront > armor) armor = ArmorFront;
    if (ArmorSide > armor) armor = ArmorSide;

    float clampedMult = FloatExtensions.Clamped(ArmorMult);
    return (int)(armor * clampedMult);
}
```

**Discrepancy Details:**
The reference code starts with `ArmorBase` and compares against Front/Side, but the decompiled code:
1. Starts with `ArmorFront` (0x20)
2. Compares against `ArmorSide` (0x24), takes max
3. Then compares result against `ArmorBase` (0x1c), takes max

The **result is mathematically equivalent** (both find max of three values), but the **order of operations differs**. The reference implies ArmorBase is the "default" when actually ArmorFront is checked first.

**Correction Needed:**
```csharp
public int GetArmor()
{
    // Get maximum armor from all zones
    // Actual order: start with Front, compare to Side, then compare to Base
    int armor = ArmorFront;
    if (ArmorSide > armor) armor = ArmorSide;
    if (ArmorBase > armor) armor = ArmorBase;

    float clampedMult = FloatExtensions.Clamped(ArmorMult);
    return (int)(armor * clampedMult);
}
```

---

### 4. GetArmorValue() - Address: 0x18060bae0

**Status:** MATCH

**Decompiled:**
```c
undefined4 Menace_Tactical_EntityProperties__GetArmorValue(longlong param_1, int param_2)
{
  if (param_2 != 0) {
    if (param_2 == 1) {
      return *(undefined4 *)(param_1 + 0x20);  // ArmorFront
    }
    if (param_2 == 2) {
      return *(undefined4 *)(param_1 + 0x24);  // ArmorSide
    }
  }
  return *(undefined4 *)(param_1 + 0x1c);      // ArmorBase (default)
}
```

**Reference Code:**
```csharp
public int GetArmorValue(int direction)
{
    return direction switch
    {
        1 => ArmorFront,
        2 => ArmorSide,
        _ => ArmorBase
    };
}
```

**Analysis:** The reference code correctly documents:
- Direction 0 or default: `ArmorBase` at `+0x1C`
- Direction 1: `ArmorFront` at `+0x20`
- Direction 2: `ArmorSide` at `+0x24`

---

### 5. GetArmorPenetration() - Address: 0x18060bab0

**Status:** MATCH

**Decompiled:**
```c
float Menace_Tactical_EntityProperties__GetArmorPenetration(longlong param_1)
{
  float fVar1;
  float fVar2;

  fVar1 = *(float *)(param_1 + 0x100);
  fVar2 = (float)Menace_Tools_FloatExtensions__Clamped(*(undefined4 *)(param_1 + 0x104),0);
  return fVar2 * fVar1;
}
```

**Reference Code:**
```csharp
public float GetArmorPenetration()
{
    float clampedMult = FloatExtensions.Clamped(ArmorPenMult);
    return ArmorPenBase * clampedMult;
}
```

**Analysis:** The reference code correctly documents:
- `ArmorPenBase` at offset `+0x100`
- `ArmorPenMult` at offset `+0x104`
- Formula: `ArmorPenBase * Clamped(ArmorPenMult)`

---

### 6. GetDamageToArmorDurability() - Address: 0x18060bd30

**Status:** MATCH

**Decompiled:**
```c
float Menace_Tactical_EntityProperties__GetDamageToArmorDurability(longlong param_1)
{
  float fVar1;
  float fVar2;

  fVar1 = *(float *)(param_1 + 300);   // 300 decimal = 0x12C
  fVar2 = (float)Menace_Tools_FloatExtensions__Clamped(*(undefined4 *)(param_1 + 0x130),0);
  return fVar2 * fVar1;
}
```

**Reference Code:**
```csharp
public float GetDamageToArmorDurability()
{
    float clampedMult = FloatExtensions.Clamped(ArmorDurDmgMult);
    return ArmorDurDmgBase * clampedMult;
}
```

**Analysis:** The reference code correctly documents:
- `ArmorDurDmgBase` at offset `+0x12C` (300 decimal)
- `ArmorDurDmgMult` at offset `+0x130`
- Formula: `ArmorDurDmgBase * Clamped(ArmorDurDmgMult)`

---

### 7. ApplyDamage() - Address: 0x180702970

**Status:** MAJOR DISCREPANCY - Significant Errors in Reference

**Decompiled Analysis:**

The actual damage calculation system is substantially different from the reference code. Here are the key findings:

#### Effect Data Offsets (handler+0x18 -> effectData)

| Offset | Actual Field | Reference Field | Status |
|--------|--------------|-----------------|--------|
| +0x58 | `IsAppliedOnlyToPassengers` (bool) | `DamageParent` | WRONG |
| +0x5C | `FlatDamageBase` (int) | `ShotCount` | WRONG |
| +0x60 | `ElementsHitPercentage` (float) | `ShotsPerElement` | PARTIALLY CORRECT |
| +0x64 | `DamageFlatAmount` (float) | `DamageBonus` | PARTIALLY CORRECT |
| +0x68 | `DamagePctCurrentHitpoints` (float) | `BaseDamage` | WRONG |
| +0x6C | `DamagePctCurrentHitpointsMin` (float) | `MinDamage` | PARTIALLY CORRECT |
| +0x70 | `DamagePctMaxHitpoints` (float) | `ArmorDurDmgBase` | WRONG |
| +0x74 | `DamagePctMaxHitpointsMin` (float) | `MinArmorDmg` | WRONG |
| +0x78 | `DamageToArmor` (float) | `ArmorPenBase` | WRONG |
| +0x7C | `ArmorDmgPctCurrent` (float) | `ArmorDurDmg` | WRONG |
| +0x80 | `FatalityType` (int) | Not documented | MISSING |
| +0x84 | `ArmorPenetration` (float) | Not documented | MISSING |
| +0x88 | `ArmorDmgFromElementCount` (float) | Not documented | MISSING |
| +0x8C | `ElementHitMinIndex` (int) | Not documented | MISSING |
| +0x90 | `CanCrit` (bool) | `CanDismember` | WRONG |

#### Actual Damage Formula (from decompiled code)

```
// Hit count calculation
hitCount = FlatDamageBase + ceil(elementCount * ElementsHitPercentage)
hitCount = max(1, hitCount)

// HP Damage calculation
currentHpDamage = max(currentHP * DamagePctCurrentHitpoints, DamagePctCurrentHitpointsMin)
maxHpDamage = max(maxHP * DamagePctMaxHitpoints, DamagePctMaxHitpointsMin)
totalDamage = currentHpDamage + DamageFlatAmount + maxHpDamage
```

#### DamageInfo Field Mapping

| DamageInfo Offset | Actual Source | Reference Claim |
|-------------------|---------------|-----------------|
| +0x2C (Damage) | `currentHpDmg + flatAmount + maxHpDmg` | `damage + damageBonus + armorDmg` |
| +0x34 | `effectData+0x78` (DamageToArmor) | `ArmorPenBase` |
| +0x38 | `effectData+0x7C` (ArmorDmgPctCurrent) | `ArmorDurDmg` |
| +0x3C (TotalShots) | `hitCount` | CORRECT |
| +0x18 | `effectData+0x80` (FatalityType) | Not documented |
| +0x40 | `effectData+0x84` (ArmorPenetration) | Not documented |
| +0x44 | `effectData+0x88` (ArmorDmgFromElementCount) | Not documented |
| +0x1C | `effectData+0x8C` (ElementHitMinIndex) | Not documented |
| +0x4E | `effectData+0x90` (CanCrit) | `CanDismember` |

#### Entity Offsets Used

| Entity Offset | Actual Field | Reference Claim |
|---------------|--------------|-----------------|
| +0x54 | Current Hitpoints | `DistanceToAttacker` |
| entity[0xB] / +0x58 | Max Hitpoints (via list at entity+0x20, count at +0x18) | `ElementCount` |
| entity[4]+0x18 | Element list count | Not documented |
| entity[0xD] / +0x68 | Parent/Passenger container | Not documented |

---

## Corrected ApplyDamage() Implementation

```csharp
/// <summary>
/// Applies damage from this skill to the target entity.
///
/// Address: 0x180702970
///
/// EFFECT DATA FIELDS (at handler+0x18):
/// +0x58: IsAppliedOnlyToPassengers (bool) - Redirect damage to passenger
/// +0x5C: FlatDamageBase (int) - Base hit count
/// +0x60: ElementsHitPercentage (float) - Fraction of elements to hit
/// +0x64: DamageFlatAmount (float) - Flat HP damage
/// +0x68: DamagePctCurrentHitpoints (float) - % of current HP as damage
/// +0x6C: DamagePctCurrentHitpointsMin (float) - Minimum for current HP%
/// +0x70: DamagePctMaxHitpoints (float) - % of max HP as damage
/// +0x74: DamagePctMaxHitpointsMin (float) - Minimum for max HP%
/// +0x78: DamageToArmor (float) - Flat armor durability damage
/// +0x7C: ArmorDmgPctCurrent (float) - % of current armor as damage
/// +0x80: FatalityType (int) - Death animation type
/// +0x84: ArmorPenetration (float) - AP value
/// +0x88: ArmorDmgFromElementCount (float) - Armor damage per element
/// +0x8C: ElementHitMinIndex (int) - First element index to hit
/// +0x90: CanCrit (bool) - Whether damage can critically strike
/// </summary>
public void ApplyDamage()
{
    Entity target = this.GetEntity();
    if (target == null) return;

    EffectData effectData = this.EffectData;  // +0x18

    // Calculate current HP percentage damage
    int currentHP = target.CurrentHitpoints;  // +0x54
    float currentHpDmg = currentHP * effectData.DamagePctCurrentHitpoints;  // +0x68
    float minCurrentDmg = effectData.DamagePctCurrentHitpointsMin;  // +0x6C
    if (minCurrentDmg > currentHpDmg) currentHpDmg = minCurrentDmg;

    // Calculate max HP percentage damage
    int maxHP = target.MaxHitpoints;  // via element list
    float maxHpDmg = maxHP * effectData.DamagePctMaxHitpoints;  // +0x70
    float minMaxDmg = effectData.DamagePctMaxHitpointsMin;  // +0x74
    if (minMaxDmg > maxHpDmg) maxHpDmg = minMaxDmg;

    // Create DamageInfo
    var dmgInfo = new DamageInfo();

    // Calculate hit count
    int flatBase = effectData.FlatDamageBase;  // +0x5C
    int elementCount = target.ElementCount;  // element list count
    float elemPct = effectData.ElementsHitPercentage;  // +0x60
    int hitCount = flatBase + (int)Math.Ceiling(elementCount * elemPct);
    hitCount = Math.Max(1, hitCount);
    dmgInfo.TotalShots = hitCount;  // +0x3C

    // Calculate total damage
    float flatAmount = effectData.DamageFlatAmount;  // +0x64
    dmgInfo.Damage = (int)(currentHpDmg + flatAmount + maxHpDmg);  // +0x2C

    // Set armor-related values
    dmgInfo.ArmorDmgFlat = (int)effectData.DamageToArmor;  // +0x34 from +0x78
    dmgInfo.ArmorDmgPct = (int)effectData.ArmorDmgPctCurrent;  // +0x38 from +0x7C
    dmgInfo.FatalityType = effectData.FatalityType;  // +0x18 from +0x80
    dmgInfo.ArmorPenetration = effectData.ArmorPenetration;  // +0x40 from +0x84
    dmgInfo.ArmorDmgFromElements = effectData.ArmorDmgFromElementCount;  // +0x44 from +0x88
    dmgInfo.ElementHitMinIndex = effectData.ElementHitMinIndex;  // +0x1C from +0x8C
    dmgInfo.CanCrit = effectData.CanCrit;  // +0x4E from +0x90

    // Apply to target or passenger
    if (effectData.IsAppliedOnlyToPassengers)  // +0x58
    {
        Actor actor = this.GetActor();
        if (actor?.HasDefaultAttribute == true)
        {
            target.PassengerContainer?.OnDamageReceived(this.Skill, dmgInfo);
            return;
        }
    }

    target.OnDamageReceived(this.Skill, dmgInfo);

    // Report to combat log
    int healthLost = /* calculated from before/after HP */;
    DevCombatLog.ReportHit(this.GetActor(), this.Skill, healthLost, target, dmgInfo);
}
```

---

## DamageInfo Class Corrections

```csharp
/// <summary>
/// Damage packet passed to targets when they receive damage.
/// Created by DamageHandler and consumed by target's OnDamageReceived.
/// </summary>
public class DamageInfo
{
    /// <summary>Fatality/death animation type. Offset: +0x18</summary>
    public int FatalityType;

    /// <summary>Minimum element index to hit. Offset: +0x1C</summary>
    public int ElementHitMinIndex;

    /// <summary>Final damage value after all calculations. Offset: +0x2C</summary>
    public int Damage;

    /// <summary>Flat armor durability damage. Offset: +0x34</summary>
    public int ArmorDmgFlat;

    /// <summary>Percentage of current armor as durability damage. Offset: +0x38</summary>
    public int ArmorDmgPct;

    /// <summary>Number of hits in this attack. Offset: +0x3C</summary>
    public int TotalShots;

    /// <summary>Armor penetration value. Offset: +0x40</summary>
    public float ArmorPenetration;

    /// <summary>Armor damage scaled by elements hit. Offset: +0x44</summary>
    public float ArmorDmgFromElements;

    /// <summary>Whether damage was absorbed by armor. Offset: +0x4D</summary>
    public bool AbsorbedByArmor;

    /// <summary>Whether this attack can critically strike. Offset: +0x4E</summary>
    public bool CanCrit;
}
```

---

## Recommendations

1. **CRITICAL:** The `ApplyDamage()` function needs complete rewrite. The damage formula is fundamentally different:
   - Reference claims distance-based damage; actual uses HP percentage damage
   - Reference claims wrong field purposes for nearly every offset

2. **HIGH:** Update `DamageInfo` class with correct field names and offsets

3. **MEDIUM:** Fix `GetArmor()` comparison order for accuracy (though result is equivalent)

4. **LOW:** Document the `IsAppliedOnlyToPassengers` mechanic which redirects damage to contained entities

---

## Verified Correct Items

The following are confirmed accurate:
- `GetDamage()` - offsets 0x118, 0x11C and formula
- `GetDamageDropoff()` - offsets 0x120, 0x124 and formula
- `GetArmorValue()` - direction switch and offsets 0x1C, 0x20, 0x24
- `GetArmorPenetration()` - offsets 0x100, 0x104 and formula
- `GetDamageToArmorDurability()` - offsets 0x12C, 0x130 and formula
- Armor offsets: ArmorBase +0x1C, ArmorFront +0x20, ArmorSide +0x24, ArmorMult +0x28
