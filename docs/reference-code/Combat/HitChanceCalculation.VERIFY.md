# Hit Chance Calculation Verification Report

**Date:** 2026-03-10
**Reference File:** `/docs/reference-code/Combat/HitChanceCalculation.cs`
**Verification Method:** Ghidra MCP decompilation comparison

---

## Summary

| Function | Address | Status | Notes |
|----------|---------|--------|-------|
| `GetHitchance` | 0x1806dba90 | **MOSTLY CORRECT** | Minor struct offset issues |
| `GetCoverMult` | 0x1806d9bb0 | **PARTIALLY CORRECT** | Missing containment logic, formula differences |
| `GetAccuracy` | 0x18060b9f0 | **CORRECT** | Field offsets verified |
| `GetAccuracyDropoff` | 0x18060b9c0 | **CORRECT** | Field offsets verified |
| `Flipped` | 0x1805320d0 | **CORRECT** | Formula: `2.0 - value` |
| `Clamped` | 0x1805320c0 | **CORRECT** | Clamps to 0+ (not 0-1) |
| `AddMult` | 0x1805320a0 | **CORRECT** | Formula: `current += (mult - 1.0)` |

---

## Detailed Analysis

### 1. GetHitchance (0x1806dba90)

**Status:** MOSTLY CORRECT with minor discrepancies

#### Verified Correct:
- AlwaysHits check at `SkillTemplate+0xF3`
- SkillTemplate reference at `Skill+0x10`
- SkillContainer reference at `Skill+0x18`
- IdealRange at `Skill+0xB8`
- Formula: `accuracy * Clamped(coverMult) * Clamped(defenseMult) + distanceDropoff`
- Clamping to 0-100 range (DAT_182d8fd78 = 100.0f)
- MinHitChance floor at `EntityProperties+0x78`

#### HitChanceResult Struct Offsets - CORRECTIONS NEEDED:

**Reference code says:**
```csharp
public float FinalValue;            // offset 0x00
public float Accuracy;              // offset 0x04
public float CoverMult;             // offset 0x08
public float DefenseMult;           // offset 0x0C
public bool IncludeDropoff;         // offset 0x11
public float AccuracyDropoff;       // offset 0x14
public bool AlwaysHits;             // offset 0x15
```

**Actual decompiled code shows:**
```c
param_1[0] = 0.0;   // FinalValue at 0x00 - CORRECT
param_1[1] = 0.0;   // Accuracy at 0x04 - CORRECT
param_1[2] = 0.0;   // CoverMult at 0x08 - CORRECT
param_1[3] = 0.0;   // DefenseMult at 0x0C - CORRECT
param_1[4] = 0.0;   // Unknown at 0x10
param_1[5] = 0.0;   // AccuracyDropoff at 0x14

// IncludeDropoff written as:
*(undefined1 *)((longlong)param_1 + 0x11) = 1;  // offset 0x11 - CORRECT

// AlwaysHits written as:
*(undefined1 *)(param_1 + 4) = 1;  // This is at param_1+4*4 = 0x10, not 0x15
```

**CORRECTION NEEDED:**
```csharp
public struct HitChanceResult
{
    public float FinalValue;            // offset 0x00 - CORRECT
    public float Accuracy;              // offset 0x04 - CORRECT
    public float CoverMult;             // offset 0x08 - CORRECT
    public float DefenseMult;           // offset 0x0C - CORRECT
    public bool AlwaysHits;             // offset 0x10 - WAS 0x15, should be 0x10
    public bool IncludeDropoff;         // offset 0x11 - CORRECT
    public byte _padding1;              // offset 0x12 (alignment)
    public byte _padding2;              // offset 0x13 (alignment)
    public float AccuracyDropoff;       // offset 0x14 - CORRECT
}
```

#### Logic Flow - Minor Issue:

The reference code has `overrideTargetEntity` parameter but actual decompilation shows:
- If `param_8` (target entity) is null, it tries to get entity from targetTile
- This is more complex than the reference shows

**Actual logic:**
```c
if (param_8 == (longlong *)0x0) {
    if (param_4 == 0) goto LAB_1806dbe70;  // null check targetTile
    cVar3 = Menace_Tactical_Tile__IsEmpty(param_4,0);
    if (cVar3 == '\0') {
        param_8 = (longlong *)Menace_Tactical_Tile__GetEntity(param_4,0);
    }
}
```

The reference code should show this fallback behavior more explicitly.

---

### 2. GetCoverMult (0x1806d9bb0)

**Status:** PARTIALLY CORRECT - Significant logic missing

#### Verified Correct:
- IgnoresCoverAtRange at `SkillTemplate+0x100`
- Distance check `< 2` returns 1.0
- Cover multiplier lookup from `Config+0x88` array
- CoverUsage at `EntityProperties+0x88`
- AddMult stacking applied at the end

#### DISCREPANCIES:

**1. Missing AlwaysHits Check:**
The actual code checks `SkillTemplate+0xF3` (AlwaysHits) early and returns early:
```c
if (*(char *)(*(longlong *)(param_1 + 0x10) + 0xf3) != '\0') goto LAB_1806d9eec;
```
This is NOT in the reference code's GetCoverMult.

**2. Missing CoverUsage Zero Check:**
```c
fVar9 = (float)Menace_Tools_FloatExtensions__Clamped(*(undefined4 *)(param_5 + 0x88),0);
if (fVar9 <= 0.0) goto LAB_1806d9eec;  // Return 1.0 if CoverUsage <= 0
```
The reference code doesn't handle this case.

**3. Complex Containment Logic Missing:**

The actual decompilation shows significant containment-related logic that is absent from the reference:

```c
// Check if contained and handle differently
if (((cVar2 == '\0') &&
    ((param_6 != '\0' ||
    (cVar2 = UnityEngine_UIElements_VisualElementAsset__get_hasStylesheetPaths(param_4,0),
    cVar2 != '\0')))) &&
    (cVar2 = Menace_Tactical_Tile__IsEmpty(param_3,0), cVar2 == '\0')) &&
    (plVar8 = (longlong *)Menace_Tactical_Tile__GetEntity(param_3,0), param_4 != plVar8)) {
    // Special handling for contained entities - reads cover from container
    plVar8 = (longlong *)Menace_Tactical_Tile__GetEntity(param_3,0);
    // Gets TacticsCombat component at +0x398, reads interior cover at +0xE0, then +0x58
    ...
}
```

**Note:** `UnityEngine_UIElements_VisualElementAsset__get_hasStylesheetPaths` appears to be a misidentified function - likely actually checking some entity containment property.

**4. Cover Formula Difference:**

**Reference code:**
```csharp
float coverMult = Math.Min(coverValue / defenderCoverUsage, 1.0f);
return 1.0f.AddMult(coverMult);
```

**Actual code:**
```c
fVar10 = (float)Menace_Tools_FloatExtensions__Clamped(*(undefined4 *)(param_5 + 0x88),0);
if (fVar9 / fVar10 <= fVar12) {  // fVar12 = 1.0f
    fVar12 = fVar9 / fVar10;
}
FloatExtensions_AddMult_StackPercentages(afStackX_8,fVar12,0);
```

This is equivalent: `Min(coverValue / coverUsage, 1.0)` then AddMult. **CORRECT**

---

### 3. GetAccuracy (0x18060b9f0)

**Status:** CORRECT

**Decompiled:**
```c
fVar1 = *(float *)(param_1 + 0x68);  // AccuracyBonus
fVar2 = (float)Menace_Tools_FloatExtensions__Clamped(*(undefined4 *)(param_1 + 0x6c),0);  // AccuracyMult
floorf(fVar2 * fVar1);  // floor(AccuracyBonus * AccuracyMult)
```

**Field Offsets:**
- AccuracyBonus: `EntityProperties+0x68` - CORRECT (matches documentation)
- AccuracyMult: `EntityProperties+0x6C` - CORRECT

---

### 4. GetAccuracyDropoff (0x18060b9c0)

**Status:** CORRECT

**Decompiled:**
```c
fVar1 = *(float *)(param_1 + 0x70);  // AccuracyDropoff
fVar2 = (float)Menace_Tools_FloatExtensions__Clamped(*(undefined4 *)(param_1 + 0x74),0);  // AccuracyDropoffMult
floorf(fVar2 * fVar1);  // floor(AccuracyDropoff * AccuracyDropoffMult)
```

**Field Offsets:**
- AccuracyDropoff: `EntityProperties+0x70` - CORRECT
- AccuracyDropoffMult: `EntityProperties+0x74` - CORRECT

---

### 5. Flipped (0x1805320d0)

**Status:** CORRECT

**Decompiled:**
```c
return DAT_182d8fb9c - (param_1 - DAT_182d8fb9c);
// Where DAT_182d8fb9c = 1.0f
// = 1.0 - (value - 1.0)
// = 1.0 - value + 1.0
// = 2.0 - value
```

The reference documentation states `defenseMult = 2.0 - evasion` which is **CORRECT**.

---

### 6. Clamped (0x1805320c0)

**Status:** CORRECT but documentation could be clearer

**Decompiled:**
```c
float Menace_Tools_FloatExtensions__Clamped(float param_1) {
    float fVar1 = 0.0;
    if (0.0 <= param_1) {
        fVar1 = param_1;
    }
    return fVar1;
}
```

This clamps to **0+** (minimum 0, no maximum), NOT 0-1 as might be assumed. The reference code uses `FloatExtensions.Clamped()` correctly but should clarify this only sets a floor of 0.

---

### 7. AddMult (0x1805320a0)

**Status:** CORRECT

**Decompiled:**
```c
void FloatExtensions_AddMult_StackPercentages(float *param_1, float param_2) {
    *param_1 = (param_2 - DAT_182d8fb9c) + *param_1;  // current += (mult - 1.0)
}
```

The AddMult stacking formula `current += (mult - 1.0)` is **CORRECT** as documented.

---

## Global Data Constants

| Address | Value | Usage |
|---------|-------|-------|
| DAT_182d8fb9c | 1.0f | Base multiplier, used in Flipped/AddMult |
| DAT_182d8fd78 | 100.0f | Max hit chance cap |

---

## Corrections Required

### HitChanceResult.cs - Fix struct layout:

```csharp
public struct HitChanceResult
{
    public float FinalValue;            // offset 0x00
    public float Accuracy;              // offset 0x04
    public float CoverMult;             // offset 0x08
    public float DefenseMult;           // offset 0x0C
    public bool AlwaysHits;             // offset 0x10  <-- MOVED from 0x15
    public bool IncludeDropoff;         // offset 0x11
    // 2 bytes padding (0x12-0x13)
    public float AccuracyDropoff;       // offset 0x14
}
```

### GetCoverMult - Add missing checks:

```csharp
public float GetCoverMult(...)
{
    // ADD: Early return if AlwaysHits
    if (this.SkillTemplate.AlwaysHits)
    {
        return 1.0f;
    }

    // ADD: CoverUsage zero check
    float coverUsage = FloatExtensions.Clamped(defenseProps.CoverUsage);
    if (coverUsage <= 0.0f)
    {
        return 1.0f;
    }

    // ... rest of logic
}
```

### GetHitchance - Clarify target resolution:

```csharp
// If no override target, try to get from tile
if (overrideTargetEntity == null && targetTile != null && !targetTile.IsEmpty())
{
    target = targetTile.GetEntity();
}
```

---

## EntityProperties Field Offset Summary

| Field | Offset | Type |
|-------|--------|------|
| AccuracyBonus | 0x68 | float |
| AccuracyMult | 0x6C | float |
| AccuracyDropoff | 0x70 | float |
| AccuracyDropoffMult | 0x74 | float |
| MinHitChance | 0x78 | int |
| CoverUsage | 0x88 | float |

---

## SkillTemplate Field Offset Summary

| Field | Offset | Type |
|-------|--------|------|
| AlwaysHits | 0xF3 | bool |
| IgnoresCoverAtRange | 0x100 | bool |

---

## Conclusion

The reference code is **largely accurate** for the core hit chance formula and is suitable for understanding the game mechanics. The main discrepancies are:

1. **Minor:** HitChanceResult struct has AlwaysHits at wrong offset (0x10 not 0x15)
2. **Moderate:** GetCoverMult missing AlwaysHits and CoverUsage <= 0 early returns
3. **Minor:** Target entity resolution from tile not explicitly shown

The mathematical formulas (accuracy calculation, dropoff, cover mult, flipped, addmult stacking) are all **correct** and match the decompiled code exactly.
