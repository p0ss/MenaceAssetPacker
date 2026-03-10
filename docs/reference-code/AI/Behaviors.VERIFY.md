# Behaviors.cs Verification Report

This document verifies the reference code in `Behaviors.cs` against actual decompiled code from the game binary.

## Summary

| Component | Status | Notes |
|-----------|--------|-------|
| TileScore Structure | **MAJOR DISCREPANCIES** | Field offsets and additional fields incorrect |
| CoverAgainstOpponents | **SIGNIFICANT DISCREPANCIES** | Much more complex than documented |
| DistanceToCurrentTile | **DISCREPANCIES** | Uses different formula and AIConfig weights |
| ThreatFromOpponents | **MAJOR DISCREPANCIES** | Much more complex; has additional Score function |
| InflictDamage.GetTargetValue | **MAJOR DISCREPANCIES** | Wrapper that delegates to SkillBehavior; actual logic vastly different |
| Behavior Base Class | **MINOR DISCREPANCIES** | Field offsets need verification |
| Move Behavior | **DISCREPANCIES** | Much more complex than documented |

---

## Detailed Analysis

### 1. TileScore Structure

**Reference Code (Behaviors.cs lines 17-45):**
```csharp
public class TileScore
{
    public Tile Tile;                    // Offset: +0x10
    public Tile UltimateTile;            // Offset: +0x18
    public float DistanceToCurrentTile;  // Offset: +0x20
    public float DistanceScore;          // Offset: +0x24
    public float UtilityScore;           // Offset: +0x28
    public float SafetyScore;            // Offset: +0x2C
    public float FinalScore;             // Offset: +0x30
    public int CoverLevel;               // Offset: +0x34
    public bool VisibleToOpponents;      // Offset: +0x38
}
```

**Actual Structure (from decompilation at 0x1807382a0 - TileScore.Reset):**
```csharp
public class TileScore
{
    // +0x10: Tile (reference, set in Reset)
    // +0x18: Unknown (reference, cleared in Reset)
    // +0x20: float (distance-related, cleared in Reset)
    // +0x24: float (cleared in Reset)
    // +0x28: float (used in GetScore as UtilityScore)
    // +0x2C: float (used in GetScaledScore)
    // +0x30: float (used in GetScore as SafetyScore)
    // +0x34: float (used in GetScaledScore)
    // +0x38: float (cleared in Reset)
    // +0x40: int (cleared in Reset)
    // +0x44: int (set to 0x400 default)
    // +0x48: reference (cleared)
    // +0x50: reference (cleared)
    // +0x58: reference (cleared)
    // +0x60: short/byte (cleared)
    // +0x61: byte (visible flag? used in ThreatFromOpponents)
    // +0x62: byte (set to 1 default)
}
```

**GetScore function (0x1807381e0):**
```
FinalScore = (Utility + Safety) - (Distance + DistanceScore) * AIConfig.DistanceWeight[0x40]
```

**GetScaledScore function (0x180738110):**
```
ScaledScore = (ScaledUtility[+0x2C] + ScaledSafety[+0x34]) - (Distance + DistanceScore) * AIConfig[0x44]
```

**Discrepancies:**
- The structure has many more fields than documented
- FinalScore is calculated dynamically, not stored
- Additional scaled score fields exist at +0x2C and +0x34
- VisibleToOpponents appears to be at +0x60/+0x61, not +0x38
- The formula uses AIConfig weights for distance scaling

**Corrections Needed:**
```csharp
public class TileScore
{
    public Tile Tile;                     // +0x10
    public object Unknown1;               // +0x18 (purpose unclear)
    public float DistanceToCurrentTile;   // +0x20
    public float DistanceScore;           // +0x24
    public float UtilityScore;            // +0x28
    public float ScaledUtilityScore;      // +0x2C
    public float SafetyScore;             // +0x30
    public float ScaledSafetyScore;       // +0x34
    public float Unknown2;                // +0x38
    public int Unknown3;                  // +0x40
    public int Flags;                     // +0x44 (default: 0x400)
    public object Reference1;             // +0x48
    public object Reference2;             // +0x50
    public object Reference3;             // +0x58
    public byte VisibleToOpponents;       // +0x60 (or +0x61)
    public bool IsValid;                  // +0x62 (default: true)

    // FinalScore is CALCULATED, not stored:
    // = (UtilityScore + SafetyScore) - (DistanceToCurrentTile + DistanceScore) * AIConfig.DistanceWeight
}
```

---

### 2. CoverAgainstOpponents Criterion

**Reference Code (lines 96-155):**
- Simple constants: ADDITIONAL_RANGE = 3, NO_COVER_AT_ALL_PENALTY = 10f, NOT_VISIBLE_TO_OPPONENTS_MULT = 0.9f
- Simple COVER_PENALTIES array: {1.0f, 0.7f, 0.4f, 0.1f}
- Simple loop over opponents calculating cover penalties

**Actual Implementation (0x180754f50):**

The actual implementation is ~700 lines of decompiled code and is vastly more complex:

1. **IsValid (0x180755e30):** Checks `Entity.GetCoverUsage()` returns != -2, then checks role flag at offset +0x3E

2. **Constructor (0x180755e80):** Initializes a float array of 4 elements from static data (the COVER_PENALTIES array)

3. **Evaluate (0x180754f50):**
   - Checks if tile has entity, handles container entities
   - Gets InsideCoverTemplate AI value
   - Uses AIConfig offsets: +0xD8, +0xDC, +0x70, +0xA8, +0xD4 for various multipliers
   - Iterates over all known opponents
   - For each opponent, calculates:
     - Direction-based cover in 3 directions (direct, +1, -1)
     - Distance-based falloff
     - Threat modifiers based on suppression state (AIConfig +0x90, +0x94, +0x98, +0x9c, +0xa0)
     - Line of sight checks
     - Range checks against opponent damage range
   - Applies penalty for no cover in all 8 directions
   - Modifies based on role settings

**Discrepancies:**
- Reference code is drastically oversimplified
- Missing InsideCoverTemplate handling
- Missing suppression state modifiers from AIConfig
- Missing multi-direction cover calculation
- Missing distance falloff formulas
- Missing many AIConfig weight references

**Key AIConfig Offsets Used:**
- +0x70: CoverAgainstOpponents base weight
- +0x90: PanickedThreatMult (0.75)
- +0x94: StunnedThreatMult (0.85)
- +0x98: VehicleDamagedMult (0.25)
- +0x9C: AlreadyDamagedMult (0.75)
- +0xA0: LeaderMult (0.9)
- +0xA8: Additional multiplier
- +0xD4: NoCoverPenalty
- +0xD8: InsideCoverWeight
- +0xDC: InsideCoverProtection weight

---

### 3. DistanceToCurrentTile Criterion

**Reference Code (lines 161-174):**
```csharp
public override void Evaluate(Actor actor, TileScore tileScore)
{
    tileScore.DistanceScore = tileScore.DistanceToCurrentTile * 0.5f;
}
```

**Actual Implementation (0x1807581f0):**
```csharp
// Gets move range from actor's movement template (+0x2b0 -> +0x118)
// Gets speed modifier from current state (+0x3c)
// Calculates: iVar8 = moveRange + speedModifier; if < 1, set to 1
// Gets distance from tile to current position
// Gets role weight from role data (+0x20)
// If distance > moveRange/speed:
//     Uses AIConfig +0x158 as penalty multiplier
// Final: DistanceScore = distance * roleWeight * penaltyMult + currentScore
```

**Discrepancies:**
- Missing move range calculation
- Missing speed modifier consideration
- Missing over-range penalty from AIConfig +0x158
- The 0.5f constant is not accurate; it uses role data weight at +0x20

**IsValid (0x1807583c0):**
- Checks actor has movement template (+0x2b0)
- Checks TacticalManager.Instance (+0x60) != 0
- Returns role flag at +0x3F

---

### 4. ThreatFromOpponents Criterion

**Reference Code (lines 180-228):**
- Simple loop calculating threat at tile
- Basic range check
- Simple threat modifiers for suppression

**Actual Implementation:**

**Evaluate (0x180764c40):**
- Checks actor state (GetActorState > 1)
- Handles container entities separately with HP-based multiplier
- Calls Score function for actual threat calculation
- Uses AIConfig +0x74 as threat weight

**Score (0x1807656c0):**
- ~400 lines of complex code
- Iterates all known opponents
- For each opponent:
  - Checks if opponent is discovered
  - Gets vehicle range if applicable
  - Calculates cover in both directions
  - Uses AIConfig multipliers: +0x78 (unknown threat), various others
  - Considers multi-tile range for vehicles
  - Applies modifiers based on:
    - Cover difference between positions
    - Flanking opportunities
    - Movement capabilities of threat

**Additional AI_ThreatFromOpponents_Score (0x180764f20):**
- Separate helper function for individual threat scoring

**Discrepancies:**
- Reference code missing vehicle/multi-tile handling
- Missing cover comparison logic
- Missing discovery state checks
- Missing AIConfig +0x78 unknown threat modifier
- The formula is much more complex than documented

---

### 5. InflictDamage.GetTargetValue

**Reference Code (lines 352-382):**
```csharp
protected float GetTargetValue(Entity target, Skill skill)
{
    var hitResult = skill.GetHitchance(...);
    float hitChance = hitResult.FinalValue / 100f;
    float damage = skill.GetExpectedDamage(target);
    float value = hitChance * damage;
    // Bonus for high-threat targets
    value *= 1f + (target.ThreatLevel * weights.TargetValueThreatScale);
    // Kill bonus
    if (damage >= target.Hitpoints) value *= 1.5f;
    else value *= 1f + (1f - healthRatio) * 0.3f;
    return value;
}
```

**Actual Implementation (0x18072db70):**
```csharp
// This is a WRAPPER function that:
// 1. Gets uses left this turn from skill container
// 2. Delegates to SkillBehavior.GetTargetValue (0x180733460)
```

**SkillBehavior.GetTargetValue (0x180733460):**
- ~800 lines of decompiled code
- Comprehensive AI target evaluation including:
  - Hit chance calculation
  - Expected damage calculation
  - Expected suppression calculation
  - Morale impact calculation (both immediate and projected)
  - Armor damage consideration
  - Kill potential with tiered multipliers
  - Cover consideration at target position
  - Discipline/morale state modifiers
  - Friendly fire considerations
  - Tile effect considerations (fire, acid, etc.)
  - Strategy tag values
  - Multi-use skill considerations

**Key AIConfig Offsets Used:**
- +0x7C: TileEffectDamageBase (10.0)
- +0x80: TileEffectDamageMult (1.0)
- +0xE4: DamageScaling
- +0xE8: ArmorDamageMult
- +0xEC: MoraleImpactMult
- +0xF0: MultiHitMult
- +0xF8: ThreatBonusMult

**Discrepancies:**
- The reference code documents the WRAPPER, not the actual logic
- Missing suppression value calculation
- Missing morale impact calculation
- Missing armor damage consideration
- Missing multi-use skill handling
- Missing tile effect damage consideration
- Missing strategy tag evaluation
- The kill bonus logic is much more sophisticated with HP threshold tiers

---

### 6. Behavior Base Class

**Reference Code:**
```csharp
public abstract class Behavior
{
    public int Score { get; protected set; }      // Offset: +0x18
    public Tile TargetTile { get; protected set; }  // Offset: +0x20
    public Entity TargetEntity { get; protected set; }  // Offset: +0x28
}
```

**From InflictDamage constructor (0x18072dda0):**
- +0x60: int field (set to 0)
- +0x90: string ID field
- Inherits from Attack which inherits from Behavior

The exact Behavior structure needs more investigation, but appears to have many more fields than documented.

---

### 7. Move Behavior

**Reference Code (lines 454-499):**
- Simple tile iteration
- Basic score comparison
- Direct MoveTo call

**Actual Implementation (0x18072eeb0 OnEvaluate, 0x180731cb0 OnExecute):**

OnExecute alone is ~300 lines including:
- Skill list management (+0x70, +0x78 for skill iteration)
- Container tracking (+0x98)
- Timed execution with delays (+0x90 time tracking)
- Path-based movement with intermediate skills
- Hidden actor detection
- Agent sleep calls
- Turn completion handling

**Discrepancies:**
- Reference code missing skill usage during movement
- Missing container handling
- Missing timing/delay system
- Missing hidden detection

---

## AIWeightsTemplate Key Offsets

From constructor at 0x18070e550, key float values (hex IEEE754 -> decimal):

| Offset | Hex Value | Float Value | Likely Purpose |
|--------|-----------|-------------|----------------|
| +0x18 | 0x3f800000 | 1.0 | Base weight |
| +0x1c | 4 | 4 | Integer count |
| +0x40 | 0x41200000 | 10.0 | Distance weight |
| +0x70 | 0x3f800000 | 1.0 | Cover weight |
| +0x74 | 0x3f800000 | 1.0 | Threat weight |
| +0x78 | 0x3dcccccd | 0.1 | Unknown threat mult |
| +0x90 | 0x3f400000 | 0.75 | Panicked threat mult |
| +0x94 | 0x3f59999a | 0.85 | Stunned threat mult |
| +0x98 | 0x3e800000 | 0.25 | Vehicle damaged mult |
| +0x9c | 0x3f400000 | 0.75 | Already damaged mult |
| +0xa0 | 0x3f666666 | 0.9 | Leader mult |
| +0xa4 | 0x3f000000 | 0.5 | Multiple attackers |
| +0xac | 0x40c00000 | 6.0 | Threat threshold |
| +0xd4 | 0x3dcccccd | 0.1 | No cover penalty |
| +0xe0 | 0x3e800000 | 0.25 | Not visible mult |
| +0xe4 | 0x3f800000 | 1.0 | Damage scaling |
| +0xec | 0x3f800000 | 1.0 | Morale impact |
| +0xf4 | 0x3e800000 | 0.25 | Threat scaling |
| +0xf8 | 0x3f000000 | 0.5 | Threat bonus |
| +0xfc | 0x3dcccccd | 0.1 | Min attack value |
| +0x104 | 0x42c80000 | 100.0 | Score multiplier |
| +0x158 | 0x3f800000 | 1.0 | Over-range penalty |

---

## Recommendations

1. **TileScore:** Completely rewrite with correct field layout including scaled scores

2. **Criterions:** Add note that these are heavily simplified. The actual implementations consider:
   - Suppression states with multipliers
   - Container entities
   - Vehicle multi-tile considerations
   - Direction-based cover in multiple directions
   - Complex AIConfig weight references

3. **InflictDamage.GetTargetValue:** Note that this is a wrapper; document the actual SkillBehavior.GetTargetValue which handles:
   - Suppression value
   - Morale impact
   - Armor damage
   - Multi-use skills
   - Tile effects
   - Strategy tags

4. **AIConfig/AIWeightsTemplate:** Create separate reference document with all known offsets and their purposes

5. **Move Behavior:** Add documentation for skill usage during movement and timing system
