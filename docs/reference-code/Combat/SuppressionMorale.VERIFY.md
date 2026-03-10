# Suppression & Morale System - Verification Report

**Generated:** 2026-03-10
**Reference File:** `/home/poss/Documents/Code/Menace/MenaceAssetPacker/docs/reference-code/Combat/SuppressionMorale.cs`

---

## Summary

| Function | Address | Status | Notes |
|----------|---------|--------|-------|
| GetSuppression() | 0x5DF7B0 | **INVALID** | No function at this address - likely inline accessor |
| GetSuppressionPct() | 0x1805DF710 | **MATCH** | Correct |
| GetSuppressionState(float) | 0x1805df730 | **MATCH** | Correct |
| SetSuppression(float) | 0x1805e76d0 | **MATCH** | Minor detail differences |
| ApplySuppression() | 0x1805ddda0 | **NEEDS CORRECTIONS** | Formula differences found |
| ChangeSuppressionAndUpdateAP() | 0x1805DE3B0 | **MATCH** | Correct with additional details |
| GetMorale() | 0x5DF5C0 | **INVALID** | No function at this address - likely inline accessor |
| GetMoralePct() | 0x1805df4a0 | **MATCH** | Correct |
| GetMoraleMax() | 0x1805df330 / 0x1805df3e0 | **NEEDS CORRECTIONS** | Different offsets and logic |
| GetMoraleState() | 0x1805df4d0 | **NEEDS CORRECTIONS** | Logic and offsets differ |
| EntityProperties.GetSuppression() | 0x18060c780 | **NEEDS CORRECTIONS** | Offsets are wrong |

---

## Detailed Analysis

### 1. GetSuppression() - Address 0x5DF7B0

**Status:** INVALID ADDRESS

The documented address `0x5DF7B0` does not contain a function in the binary. When searched with the full 64-bit address `0x1805DF7B0`, it points to `UnityEngine_EventSystems_PointerEventData__get_twist`, which is unrelated.

**Recommendation:** Remove this function from documentation or mark as "inlined/not exported". The suppression value is accessed directly from offset `+0x15C` throughout the codebase, suggesting this may be an inline accessor that was optimized away.

---

### 2. GetSuppressionPct() - Address 0x1805DF710

**Status:** MATCH

**Decompiled:**
```c
float Menace_Tactical_Actor__GetSuppressionPct(longlong param_1)
{
  return *(float *)(param_1 + 0x15c) * DAT_182d8fbd8;
}
```

**Analysis:**
- Suppression offset: `+0x15C` - **Correct**
- Multiplier: `DAT_182d8fbd8` (0.01) - **Correct**
- Formula: `suppression * 0.01` - **Correct**

---

### 3. GetSuppressionState(float additionalPct) - Address 0x1805df730

**Status:** MATCH

**Decompiled:**
```c
char Menace_Tactical_Actor__GetSuppressionState(longlong param_1, float param_2)
{
  param_2 = *(float *)(param_1 + 0x15c) * DAT_182d8fbd8 + param_2;
  cVar1 = '\0';
  if (DAT_182d8fe40 <= param_2) {
    cVar1 = (DAT_182d8fe48 <= param_2) + '\x01';
  }
  return cVar1;
}
```

**Analysis:**
- Suppression offset: `+0x15C` - **Correct**
- Threshold 1 (`DAT_182d8fe40`): 0.5 (50%) - **Correct**
- Threshold 2 (`DAT_182d8fe48`): 0.8 (80%) - **Correct**
- Return values: 0=None, 1=Suppressed, 2=PinnedDown - **Correct**
- Logic flow matches documentation exactly

---

### 4. SetSuppression(float value) - Address 0x1805e76d0

**Status:** MATCH (minor detail)

**Decompiled:**
```c
void Menace_Tactical_Actor__SetSuppression(longlong *param_1, float param_2)
{
  if (param_2 < 0.0) {
    param_2 = 0.0;
  }
  else if (DAT_182d8fd78 < param_2) {
    param_2 = DAT_182d8fd78;
  }
  *(float *)((longlong)param_1 + 0x15c) = param_2;
  if (param_1[0x1c] != 0) {
    Menace_UI_Tactical_UnitHUD__UpdateSuppression(param_1[0x1c], param_2 * DAT_182d8fbd8, 1000, 0);
  }
  lVar2 = (**(code **)(*param_1 + 1000))(param_1, ...);
  if (lVar2 != 0) {
    Menace_Tactical_Skills_SkillContainer__Update(lVar2, 0);
    return;
  }
  // ... null reference handling
}
```

**Analysis:**
- Clamping: 0 to `DAT_182d8fd78` (100) - **Correct**
- Suppression offset: `+0x15C` - **Correct**
- HUD offset: `param_1[0x1c]` = `+0xE0` - **Correct**
- UpdateSuppression call with 1000 (duration) - **Correct**
- SkillContainer update via vtable - **Correct**

**Note:** The HUD call has a 4th parameter (0) not shown in reference code, but this is likely an optional method parameter.

---

### 5. ApplySuppression() - Address 0x1805ddda0

**Status:** NEEDS CORRECTIONS

**Decompiled (key excerpts):**
```c
void Actor_ApplySuppression_MainEntry(longlong *param_1, float param_2, char param_3,
                                       undefined8 param_4, undefined8 param_5)
{
  // Call base Entity::ApplySuppression
  Menace_Tactical_Entity__ApplySuppression(param_1, param_2, param_3, param_4, param_5, 0);

  // Get EntityProperties
  lVar6 = (**(code **)(*param_1 + 0x3d8))(param_1, ...);

  // Check ignore suppression flag: (EntityProps+0xEC >> 5) & 1
  if ((*(uint *)(lVar6 + 0xec) >> 5 & 1) == 0) {

    // For non-direct: also check (EntityProps+0xEC >> 6) & 1
    if (param_3 == '\0') {
      if ((*(uint *)(lVar6 + 0xec) >> 6 & 1) != 0) {
        return;
      }
    }

    // Apply cover resistance via AddMult
    if ((0.0 < param_2) && has_stylesheet_paths) {
      // Get cover from tile at (lVar6 + 0xe0)
      FloatExtensions_AddMult_StackPercentages(afStackX_18, *(undefined4 *)(lVar6 + 0x60), 0);
    }

    // Calculate final value
    if (0.0 < param_2) {
      // Get discipline
      iVar5 = Menace_Tactical_EntityProperties__GetDiscipline(lVar6, 0);

      // disciplineMult = max(0, 1.0 - discipline * 0.01)
      fVar10 = fVar10 - (float)iVar5 * DAT_182d8fbd8;
      fVar9 = 0.0;
      if (0.0 <= fVar10) {
        fVar9 = fVar10;
      }

      // Final: resistanceMult * value * disciplineMult * suppressionResist * DAT_182d8ff80
      fVar10 = fVar9 * fVar8 * DAT_182d8ff80;
    }
    fVar10 = fVar7 * param_2 * fVar10;

    // Log and apply
    DevCombatLog__ReportSuppression(param_5, param_1, fVar10, param_3, 0);
    Actor__SetSuppression(param_1, fVar10 + *(float *)((longlong)param_1 + 0x15c), 0);

    // Notify if increased
    if (fVar1 < *(float *)((longlong)param_1 + 0x15c)) {
      TacticalManager__InvokeOnSuppressionApplied(...);
    }
  }
}
```

**Discrepancies Found:**

1. **Cover SuppressionResistMult offset:** Reference says `tile.Cover.SuppressionResistMult`, actual code reads from `lVar6 + 0x60`. The cover object offset needs verification.

2. **Extra multiplier:** The decompiled code includes `DAT_182d8ff80` as an additional global multiplier in the formula. This is not documented in the reference code.

3. **Formula Order:** The reference code shows:
   ```
   finalValue = clampedResistance * value * disciplineMult * suppressionResist
   ```
   Actual code shows:
   ```
   finalValue = resistanceMult * value * disciplineMult * suppressionResist * DAT_182d8ff80
   ```

**Corrections Needed:**
```csharp
// Add this constant:
/// <summary>Global suppression multiplier. DAT_182d8ff80</summary>
public const float GlobalSuppressionMult = ???;  // Need to verify actual value

// Update formula in ApplySuppression:
finalValue = clampedResistance * value * disciplineMult * suppressionResist * GlobalSuppressionMult;
```

---

### 6. ChangeSuppressionAndUpdateAP(float delta) - Address 0x1805DE3B0

**Status:** MATCH

**Decompiled:**
```c
void Actor_ChangeSuppressionAndUpdateAP_StateTransition(longlong *param_1, float param_2)
{
  // Get current state
  iVar2 = (**(code **)(*param_1 + 0x478))(param_1, ...);  // GetSuppressionState

  // Apply suppression change
  Actor__SetSuppression(param_1, param_2 + *(float *)((longlong)param_1 + 0x15c), 0);

  // Get new state
  iVar3 = (**(code **)(*param_1 + 0x478))(param_1, ...);

  if (iVar3 == iVar2) return;

  // State 0 -> 1: Reduce AP by 30
  if (iVar2 == 0 && iVar3 == 1) {
    Actor__SetActionPoints(param_1, currentAP + -0x1e, 1, 0);  // -30 AP
    return;
  }

  // State 2 -> 1: Restore to max - 30
  if (iVar2 == 2 && iVar3 == 1) {
    maxAP = EntityProperties__GetActionPoints(lVar5, 0);
    Actor__SetActionPoints(param_1, maxAP + -0x1e, 1, 0);  // max - 30
    Actor__SetTurnDone(param_1, 0, 0);  // Allow acting again
  }

  // State 2 -> 0: Restore to max
  if (iVar2 == 2 && iVar3 == 0) {
    maxAP = EntityProperties__GetActionPoints(lVar5, 0);
    Actor__SetActionPoints(param_1, maxAP, 0, 0);
    Actor__SetTurnDone(param_1, 0, 0);
  }

  // State 1 -> 0: Restore 30 AP
  if (iVar2 == 1 && iVar3 == 0) {
    Actor__SetActionPoints(param_1, currentAP + 0x1e, 0, 0);  // +30 AP
    return;
  }

  // Any -> State 2: Set AP to 0
  if (iVar3 == 2) {
    Actor__SetActionPoints(param_1, 0, 1, 0);
  }
}
```

**Analysis:**
- Logic matches reference code conceptually
- Suppressed state reduces AP by 30 (0x1e)
- PinnedDown state sets AP to 0
- Recovery restores appropriate AP amounts
- SetTurnDone(false) called when recovering from pinned - documented behavior

**Additional Detail:** The reference code is simplified. The actual implementation has more granular state transition handling.

---

### 7. GetMorale() - Address 0x5DF5C0

**Status:** INVALID ADDRESS

The documented address `0x5DF5C0` does not contain a function in the binary. When searched with full address `0x1805DF5C0`, it points to `Menace_Tactical_TacticalCamera__GetZoom`, which is unrelated.

**Recommendation:** Remove this function from documentation or mark as "inlined/not exported". Morale is accessed directly from offset `+0x160` throughout the codebase (confirmed as `param_1 + 0x2c` which equals `+0x160` for an 8-byte aligned struct).

**Note:** The morale field is at `param_1[0x2c]` = `param_1 + 0x160`, which matches the documented offset.

---

### 8. GetMoralePct() - Address 0x1805df4a0

**Status:** MATCH

**Decompiled:**
```c
float Menace_Tactical_Actor__GetMoralePct(longlong param_1)
{
  fVar1 = *(float *)(param_1 + 0x160);
  fVar2 = (float)Menace_Tactical_Actor__GetMoraleMax(param_1, 0);
  return fVar1 / fVar2;
}
```

**Analysis:**
- Morale offset: `+0x160` - **Correct**
- Formula: `morale / GetMoraleMax()` - **Correct**

---

### 9. GetMoraleMax() - Addresses 0x1805df330 / 0x1805df3e0

**Status:** NEEDS CORRECTIONS

**Decompiled (0x1805df330 - without health adjustment):**
```c
ulonglong Menace_Tactical_Actor__GetMoraleMax(longlong *param_1, float param_2)
{
  // Get EntityProperties via vtable
  lVar4 = (**(code **)(*param_1 + 0x3d8))(param_1, ...);

  fVar1 = *(float *)(lVar4 + 0xa0);   // BaseMorale
  fVar2 = *(float *)(lVar4 + 0xac);   // BonusMorale
  *(float *)(lVar4 + 0xb0);           // MoraleMult

  return (fVar2 + fVar1) * moraleMult * param_2;
}
```

**Decompiled (0x1805df3e0 - with health adjustment):**
```c
float Menace_Tactical_Actor__GetMoraleMax(longlong *param_1)
{
  fVar6 = *(float *)(lVar4 + 0xa0);   // BaseMorale
  fVar1 = *(float *)(lVar4 + 0xac);   // BonusMorale
  fVar2 = *(float *)(lVar4 + 0xb0);   // MoraleMult
  fVar5 = Entity__GetHitpointsPct(param_1, 0);

  return fVar5 * (fVar1 + fVar6) * fVar2;
}
```

**Discrepancies Found:**

1. **MoraleBase offset:** Reference says `+0xC4`, actual is `+0xA0`
2. **MoraleMult offset:** Reference says `+0xC8`, actual is `+0xB0`
3. **BonusMorale:** Reference doesn't mention this field at `+0xAC`
4. **Formula:** Reference shows `baseMorale * moraleMult`, actual is `(baseMorale + bonusMorale) * moraleMult`

**Corrections Needed for EntityProperties:**
```csharp
// CORRECTED OFFSETS:
/// <summary>Base morale value. Offset: +0xA0</summary>
public float MoraleBase;  // Note: float, not int

/// <summary>Bonus morale value. Offset: +0xAC</summary>
public float BonusMorale;  // NEW FIELD

/// <summary>Morale multiplier. Offset: +0xB0</summary>
public float MoraleMult = 1.0f;
```

**Corrections Needed for GetMoraleMax:**
```csharp
public float GetMoraleMax(bool adjustForHealth = false)
{
    EntityProperties props = this.GetEntityProperties();
    float baseMorale = props?.MoraleBase ?? 100f;
    float bonusMorale = props?.BonusMorale ?? 0f;  // NEW
    float moraleMult = props?.MoraleMult ?? 1f;    // Note: NOT clamped in actual code

    float maxMorale = (baseMorale + bonusMorale) * moraleMult;  // CORRECTED FORMULA

    if (adjustForHealth)
    {
        float hpPct = this.GetHitpointsPct();  // Uses Entity method
        maxMorale *= hpPct;
    }

    return maxMorale;
}
```

---

### 10. GetMoraleState() - Address 0x1805df4d0

**Status:** NEEDS CORRECTIONS

**Decompiled:**
```c
ulonglong Menace_Tactical_Actor__GetMoraleState(longlong *param_1)
{
  fVar1 = *(float *)(param_1 + 0x2c);  // Morale at +0x160
  fVar7 = Actor__GetMoraleMax(param_1, 0);
  iVar6 = 3;  // Neutral/Steady

  if (0.0 < fVar1 / fVar7) {
    // Morale > 0
    if (fVar1 / fVar7 <= DAT_182d8fe40) {  // 0.5 threshold
      iVar6 = 2;  // Wavering/Shaken
    }
  }
  else {
    // Morale <= 0
    // Check if this is faction type 1 (player) at offset +0x4C
    if (*(int *)((longlong)param_1 + 0x4c) == 1) {
      // Check if this is the selected actor
      if (TacticalManager.Instance.SelectedActor == param_1) {
        goto use_wavering_check;  // Don't flee if selected
      }
    }
    iVar6 = 1;  // Fleeing/Panicked
  }

  // Apply MoraleStateModifier from EntityProperties+0xC0
  lVar4 = GetEntityProperties(param_1);
  uVar3 = *(int *)(lVar4 + 0xc0) + iVar6;

  // Clamp to 1-3
  if (uVar3 < 1) return 1;
  if (uVar3 > 3) return 3;
  return uVar3;
}
```

**Discrepancies Found:**

1. **FactionType offset:** Checking `*(int *)(param_1 + 0x4c) == 1` for player faction. This matches documentation.

2. **Commander check:** Reference mentions "can't flee if selected", but actual code just checks if the actor is the selected actor (at TacticalManager offset +0x50), not specifically commanders. The condition `param_1 + 0x4c == 1` is the faction check.

3. **Morale state values:** The actual values are:
   - 1 = Panicked/Fleeing
   - 2 = Shaken/Wavering
   - 3 = Steady/Neutral

   This matches the documentation.

4. **MoraleStateModifier offset:** `+0xC0` - **Correct**

**Minor Correction:**
The reference code incorrectly describes the selected actor check. It's not "Commander check" but a general selected actor protection:

```csharp
// CORRECTED:
if (moralePct <= 0f)
{
    // Zero morale - check if this is the selected unit (can't flee if selected)
    if (this.FactionType == FactionType.Player &&
        TacticalManager.Instance?.SelectedActor == this)
    {
        baseState = (int)MoraleState.Neutral;  // Don't flee if player-controlled
    }
    else
    {
        baseState = (int)MoraleState.Fleeing;
    }
}
```

---

### 11. EntityProperties.GetSuppression() - Address 0x18060c780

**Status:** NEEDS CORRECTIONS

**Decompiled:**
```c
float Menace_Tactical_EntityProperties__GetSuppression(longlong param_1)
{
  fVar1 = *(float *)(param_1 + 0xe0);  // SuppressionBase
  fVar2 = FloatExtensions__Clamped(*(undefined4 *)(param_1 + 0xe4), 0);  // SuppressionMult
  return fVar2 * fVar1;
}
```

**Discrepancies Found:**

1. **SuppressionResist offset:** Reference says `+0xCC`, actual is `+0xE0`
2. **SuppressionResistMult offset:** Reference says `+0xD0`, actual is `+0xE4`

**Corrections Needed:**
```csharp
// CORRECTED OFFSETS:
/// <summary>Base suppression resistance. Offset: +0xE0</summary>
public float SuppressionResist;  // Note: likely float, not int

/// <summary>Suppression resistance multiplier. Offset: +0xE4</summary>
public float SuppressionResistMult = 1.0f;
```

---

## EntityProperties Field Offset Summary

Based on the decompilation analysis, here are the corrected offsets:

| Field | Reference Offset | Actual Offset | Status |
|-------|-----------------|---------------|--------|
| MoraleBase | +0xC4 | +0xA0 | **WRONG** |
| BonusMorale | (not documented) | +0xAC | **MISSING** |
| MoraleMult | +0xC8 | +0xB0 | **WRONG** |
| MoraleStateModifier | +0xC0 | +0xC0 | Correct |
| SuppressionResist | +0xCC | +0xE0 | **WRONG** |
| SuppressionResistMult | +0xD0 | +0xE4 | **WRONG** |
| Flags (ignores suppression) | +0xEC bit 5 | +0xEC bit 5 | Correct |
| Flags (ignores indirect) | +0xEC bit 6 | +0xEC bit 6 | Correct |
| Flags (morale immune) | (not documented) | +0xEC bit 7 | **MISSING** |
| Morale types mask | (not documented) | +0xA8 | **MISSING** |
| Morale multiplier | (not documented) | +0xBC | **MISSING** |

---

## Actor Field Offset Summary

| Field | Reference Offset | Actual Offset | Status |
|-------|-----------------|---------------|--------|
| m_Suppression | +0x15C | +0x15C | Correct |
| m_Morale | +0x160 | +0x160 (0x2c * 8) | Correct |
| m_LastMoraleState | +0xD4 | +0xD4 | Correct |
| m_HUD | +0xE0 | +0xE0 (0x1c * 8) | Correct |
| FactionType | +0x4C | +0x4C | Correct |
| Invulnerable flag | (not documented) | +0x16A | **MISSING** |
| Alive flag | (not documented) | param_1[9] | **MISSING** |

---

## Recommendations

1. **Remove invalid addresses:** GetSuppression() at 0x5DF7B0 and GetMorale() at 0x5DF5C0 should be marked as inline accessors or removed.

2. **Update EntityProperties offsets:** Multiple field offsets are incorrect and need updating.

3. **Add missing fields:**
   - `BonusMorale` at +0xAC
   - `MoraleTypeMask` at +0xA8
   - `MoraleApplyMult` at +0xBC
   - Morale immunity flag at +0xEC bit 7

4. **Update GetMoraleMax formula:** Add BonusMorale to the calculation.

5. **Add global suppression multiplier:** Document `DAT_182d8ff80` constant used in ApplySuppression.

6. **Clarify selected actor check:** The morale fleeing protection is for the currently selected actor, not specifically commanders.
