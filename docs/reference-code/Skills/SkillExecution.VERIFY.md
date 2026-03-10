# SkillExecution.cs Verification Report

**Date:** 2026-03-10
**Status:** MAJOR DISCREPANCIES FOUND - Addresses are completely wrong

## Summary

The reference document `/home/poss/Documents/Code/Menace/MenaceAssetPacker/docs/reference-code/Skills/SkillExecution.cs` contains **fabricated or incorrect addresses**. All documented addresses point to completely unrelated functions (Objectives-related code, Shapes renderer, etc.) rather than skill execution functions.

The actual skill-related functions exist but at completely different addresses in the binary.

---

## Address Verification Results

### Functions Documented with WRONG Addresses

| Documented Function | Documented Address | Actual Function at Address | Correct Address |
|---------------------|-------------------|---------------------------|-----------------|
| `SkillContainer.GetSkillsOfType` | 0x1806b1240 | `Objective__add_OnProgressChanged` | NOT FOUND by this name |
| `SkillContainer.GetSkill` | 0x1806b1320 | `Objective__remove_OnCompleted` | `GetSkillByID` @ 0x1806e91b0 or `GetSkillByTemplate` @ 0x1806e9340 |
| `SkillContainer.UseSkill` | 0x1806b1500 | `ObjectiveManager_OnProgressEvent__BeginInvoke` | `Skill$$Use` @ 0x1806e2bd0 (different signature) |
| `SkillContainer.ValidateTarget` | 0x1806b1890 | `ShapeRenderer__get_DetailLevel` | Built into `Skill$$Use` logic |
| `SkillContainer.GatherTargets` | 0x1806b1c00 | `RecoverObject__Init` (Objectives) | Built into `Skill$$Use` + `QueryTargetTiles` |
| `SkillContainer.ExecuteOnTarget` | 0x1806b2000 | `RecoverObject__IsValidDropObjectSkill` | Built into `Skill$$Use` handler loop |
| `SkillContainer.ResolveHitRoll` | 0x1806b2340 | UNKNOWN | Not found as separate function |
| `SkillContainer.PaySkillCosts` | 0x1806b2580 | UNKNOWN | Built into `Skill$$Use` |
| `Skill.CanUse` | 0x1806b3200 | UNKNOWN | `Skill$$IsUsable` @ 0x1806deb10 |
| `Skill.GetHitchance` | 0x1806b3400 | UNKNOWN | 0x1806dba90 |
| `Skill.IsInRange` | 0x1806b3580 | UNKNOWN | 0x1806de4f0 / 0x1806de510 |
| `Skill.GetExpectedDamage` | 0x1806b3680 | UNKNOWN | 0x1806da4b0 |

---

## Actual Function Analysis

### 1. Skill$$GetHitchance @ 0x1806dba90

**Signature:** `float* GetHitchance(float* result, Tile from, Tile targetTile, EntityProperties attackerProps, EntityProperties defenderProps, bool includeDropoff, Entity overrideTarget, bool forImmediateUse)`

**Reference document says:**
```csharp
public HitChanceResult GetHitchance(Tile from, Tile targetTile, object properties,
    object defenderProperties, bool includeDropoff, Entity overrideTargetEntity, bool forImmediateUse)
```

**Actual logic (from decompiled code):**
- Returns result struct with fields at: +0x00 (FinalValue), +0x04 (Accuracy), +0x08 (CoverMult), +0x0C (DefenseMult), +0x10 (AlwaysHits flag), +0x14 (DistanceDropoff)
- If Template.AlwaysHits (+0xF3 on template), returns 100% with AlwaysHits flag
- Builds attacker properties via `SkillContainer.BuildPropertiesForUse` if not provided
- Builds defender properties via `SkillContainer.BuildPropertiesForDefense` if not provided
- Defense multiplier uses `Flipped(evasion) = 2.0 - evasion`
- Distance dropoff calculated as: `|distance - optimalRange| * GetAccuracyDropoff()`
- Final formula: `hitChance = accuracy * coverMult * defenseMult + distanceDropoff`
- Clamped to 0 and max (DAT_182d8fd78), then floored to MinAccuracy if skill has minimum

**Discrepancy:** Reference is MOSTLY CORRECT conceptually but missing:
- The Flipped() defense multiplier formula
- MinAccuracy floor
- Actual offset for optimal range: +0xB8 on Skill

### 2. Skill$$IsInRange @ 0x1806de4f0 (simple) and 0x1806de510 (complex)

**Simple version (0x1806de4f0):**
```c
bool IsInRange(Skill this, int distance, int bonus) {
    if (distance < *(int*)(this + 0xB4)) return false;  // MinRange
    return distance <= *(int*)(this + 0xBC) + bonus;     // MaxRange
}
```

**Complex version (0x1806de510):**
- Checks Template.IsTargeted (+0xE4)
- For shape type 1, calculates distance from entity to source position
- Uses `GetBestTileToAttackFrom` to find optimal tile
- Compares against stored min/max range at +0xB4/+0xBC

**Reference says:**
```csharp
public bool IsInRange(Tile targetTile)
{
    float distance = m_Owner.CurrentTile.GetDistanceTo(targetTile);
    return distance >= Template.Targeting.MinRange &&
           distance <= Template.Targeting.MaxRange;
}
```

**Discrepancy:**
- Reference shows float distance, actual uses int
- Reference accesses Template.Targeting.MinRange/MaxRange, actual stores range directly on Skill at +0xB4/+0xBC
- Reference is OVERSIMPLIFIED - missing the bonus parameter and complex shape handling

### 3. Skill$$IsUsable @ 0x1806deb10

**Actual logic:**
- Gets actor via `BaseSkill.GetActor()`
- Checks if actor has "stylesheet paths" (appears to be a state check)
- Checks limited uses if Template has limited uses (+0xB8)
- Calls `Template.IsDeploymentRequirementSatisfied()`
- Checks actor mobility state (+0x113 on template, +0x16F on actor)
- Checks actor action state (value 2 = special handling)
- Calls virtual `HasExecutingElements()` check
- For targeting types 1 or 3 with <2 elements, checks item at slot 1
- Iterates through skill handlers calling their IsUsable

**Reference (CanUse) says:**
```csharp
public bool CanUse()
{
    if (RemainingCooldown > 0) return false;
    if (m_Owner.CurrentAP < Template.Costs.ActionPoints) return false;
    // ... ammo check, resource check, stun check
}
```

**Major Discrepancy:**
- Reference shows cooldown check - actual has no obvious cooldown check in IsUsable (may be in handlers)
- Reference shows AP check - actual AP check is in `IsAffordable` (separate function)
- Actual has deployment requirements, mobility checks, element counts not in reference
- Reference "CanUse" appears to be a simplified/conceptual version, not the actual implementation

### 4. Skill$$IsAffordable @ 0x1806de0d0

**Actual logic:**
```c
bool IsAffordable(Skill this) {
    Actor* actor = this->ActorOverride ? this->ActorOverride : GetActor(this);
    if (actor && actor->CanAct()) {
        int cost = GetActionPointCost(this);
        int currentAP = actor->GetActionPoints();
        return cost <= currentAP;
    }
    return false;
}
```

**Not in reference** - Reference merges this into CanUse, but they are separate functions.

### 5. Skill$$GetActionPointCost @ 0x1806d8e80

**Actual logic:**
- Base cost at +0xA0
- If Template has cost modifier (+0xF2):
  - Gets cost modifier from actor via virtual call
  - Cost = `(baseCost + modifier.flatBonus) * modifier.multiplier`
- Floors to minimum at +0xA4

**Reference says nothing about cost modifiers or minimum costs.**

### 6. Skill$$Use @ 0x1806e2bd0

**Actual signature:** `Use(Skill this, Tile targetTile, int flags)`

**Flags decoded from code:**
- 0x01 = ForceAffordable (skip affordability check)
- 0x02 = ForceUsable (skip usability check)
- 0x04 = FromAllySkill
- 0x08 = Hidden
- 0x10 = CheckVisibility

**Actual flow:**
1. Create display class for closure
2. Check if FromAllySkill and Template.ShouldUseParentSkill - delegate to parent skill
3. Check IsAffordable (unless ForceAffordable)
4. Check IsUsable (unless ForceUsable)
5. Increment global skill use counter, store on skill
6. Get actor, store in closure
7. Get final target tile from handlers (loop through handlers calling their transform)
8. Get best tile to attack from
9. If not ForceUsable: validate targeting, range, LOS, call OnVerifyTarget
10. Set actor aiming state
11. If not FromAllySkill: call SkillContainer.OnSkillUsed
12. Deduct AP cost
13. Process handlers (loop calling OnApply/CanApply)
14. Consume uses if Template requires
15. Notify targets via OnBeingTargetedByAttack
16. Call TacticalManager.InvokeOnSkillUse
17. Update actor state
18. Add to busy skills
19. Schedule delayed completion callback

**Reference's UseSkill is VASTLY OVERSIMPLIFIED:**
- No flag parameter in reference
- No handler loop
- No tile transformation
- No busy skill tracking
- No delayed completion
- Validation, gathering, execution are shown as separate steps but are actually interleaved

### 7. SkillContainer$$GetSkillByID @ 0x1806e91b0

**Actual logic:**
- Iterates through skills at +0x18 (List)
- For each skill, checks if removed flag (+0x39 = false)
- Compares ID via virtual GetID() call
- Optional: checks skill source param at +0x28

**Reference GetSkill:**
```csharp
public Skill GetSkill(string templateName)
{
    foreach (var skill in m_Skills)
    {
        if (skill.Template.name == templateName)
            return skill;
    }
    return null;
}
```

**Discrepancy:**
- Reference compares Template.name, actual compares via GetID()
- Reference doesn't have source filter parameter

### 8. SkillContainer$$GetSkillByTemplate @ 0x1806e9340

Similar to GetSkillByID but compares Template reference directly using Unity's Object.op_Equality.

### 9. SkillContainer$$QueryTags @ 0x1806ed3f0

**Actual logic:**
- Iterates skills at +0x18
- For each Skill (checked via type comparison), gets Template.Tags at +0xA8
- For each tag, if tag.SomeValue (+0x8C) != 0, adds to output HashSet

**Reference GetSkillsOfType:**
```csharp
public List<Skill> GetSkillsOfType(SkillTag tag)
{
    foreach (var skill in m_Skills)
    {
        if (skill.Template.Tags.HasFlag(tag))
            result.Add(skill);
    }
}
```

**Discrepancy:**
- Reference returns List of Skill, actual adds TagTemplates to HashSet
- Reference filters by flag, actual iterates all tags and filters by internal value
- Completely different semantics - reference is filtering skills by tag, actual is collecting all tags

### 10. Skill$$GetExpectedDamage @ 0x1806da4b0

**Actual implementation is EXTREMELY COMPLEX (200+ lines decompiled)**

Key aspects not in reference:
- Returns an ExpectedDamage object with fields:
  - +0x10: base damage
  - +0x14: armor damage
  - +0x18: HP damage
  - +0x1C: overkill damage
  - +0x20: penetration chance
  - +0x24: can kill flag
  - +0x25: will kill flag
  - +0x28: defender properties
- Calculates:
  - Distance-based damage dropoff
  - Armor penetration vs armor rating
  - Armor durability effects
  - Cover damage reduction
  - Element count multiplier
  - Uses per turn factor
- Iterates handlers implementing IExpectedDamageContributor

**Reference:**
```csharp
public float GetExpectedDamage(Entity target)
{
    float totalDamage = 0f;
    foreach (var effect in Template.Effects)
    {
        if (effect is DamageEffect damageEffect)
            totalDamage += damageEffect.BaseDamage;
    }
    totalDamage *= m_Owner.Properties.GetDamageMult();
    totalDamage *= (1f - target.Properties.GetDamageReduction());
    return totalDamage;
}
```

**Major Discrepancy:**
- Reference returns single float, actual returns complex struct
- Reference doesn't account for armor, penetration, elements, distance
- Reference formula is drastically oversimplified

---

## Field Offset Corrections

### Skill Class
| Reference Field | Reference Offset | Actual Offset | Notes |
|-----------------|------------------|---------------|-------|
| Template | +0x10 | +0x10 | CORRECT |
| m_Owner | +0x18 | +0x18 | SkillContainer reference |
| RemainingCooldown | +0x20 | UNKNOWN | May be at +0xA8 (charges) or via handlers |
| MinRange (runtime) | - | +0xB4 | Not in reference |
| MaxRange (runtime) | - | +0xBC | Not in reference |
| OptimalRange | - | +0xB8 | Not in reference |
| APCost (runtime) | - | +0xA0 | Not in reference |
| MinAPCost | - | +0xA4 | Not in reference |
| Charges | - | +0xA8 | Not in reference |
| SkillItem | - | +0x28 | Not in reference |
| Handlers | - | +0x48 | List of skill handlers |
| ActorOverride | - | +0xD0 | Not in reference |
| UseId | - | +0xD8 | Not in reference |

### SkillTemplate (accessed via Template at +0x10)
| Field | Offset | Notes |
|-------|--------|-------|
| Tags | +0xA8 | List of TagTemplate |
| AlwaysHits | +0xF3 | bool |
| IsTargeted | +0xE4 | bool |
| RequiresLOS | +0xF1 | bool |
| HasCost | +0xF2 | bool |
| IgnoreCoverInside | +0x100 | bool |
| UseWeaponRange | +0x124 | bool |
| MinRange | +0x128 | int |
| MaxRange | +0x130 | int |
| AoEType | +0x178 | int (0=single, others=AoE) |

### SkillContainer Class
| Reference Field | Reference Offset | Actual Offset | Notes |
|-----------------|------------------|---------------|-------|
| m_Owner | +0x10 | +0x10 | Owner entity |
| m_Skills | +0x18 | +0x18 | List of BaseSkill |
| m_SelectedSkill | +0x20 | UNKNOWN | - |

---

## Corrections Needed

### Critical (Addresses)
1. **ALL addresses in the document are wrong** - they need to be updated to the correct locations
2. The document class structure doesn't match the actual architecture:
   - `SkillContainer` doesn't have `UseSkill` - `Skill` has `Use`
   - Many "methods" are actually inline code or handler-based

### Critical (Logic)
1. **Hit chance formula** is mostly correct but missing DefenseMult=Flipped(evasion) detail
2. **IsInRange** uses int distance and has runtime range fields on Skill, not Template access
3. **GetExpectedDamage** is drastically oversimplified - actual has armor, penetration, elements
4. **CanUse vs IsUsable vs IsAffordable** - these are separate functions, reference merges them
5. **UseSkill/Use** - actual implementation uses handler pattern, not sequential steps

### Structural Issues
1. Reference shows `SkillTargeting`, `SkillCosts` as separate classes - actual has these as fields on SkillTemplate
2. Reference shows `SkillUseResult`, `TargetHitResult` - actual uses different result structures
3. Reference `EffectProcessor.ProcessEffects` - actual uses handler pattern with virtual calls

---

## Recommended Actions

1. **Remove all incorrect addresses** or mark them as "UNVERIFIED"
2. **Restructure document** to match actual class hierarchy:
   - `Skill` has `Use`, `IsUsable`, `IsAffordable`, `IsInRange`, `GetHitchance`, etc.
   - `SkillContainer` has `GetSkillByID`, `GetSkillByTemplate`, `QueryTags`, event handlers
3. **Add handler pattern documentation** - skills use `ISkillHandler` implementations
4. **Correct field offsets** using actual decompiled values
5. **Document the Use() flags** - 0x01=ForceAffordable, 0x02=ForceUsable, 0x04=FromAlly, 0x08=Hidden, 0x10=CheckVisibility
6. **Add missing functions**: `IsAffordable`, `GetActionPointCost`, handler interfaces

---

## Verified Correct Information

The following aspects of the reference are generally correct:
- Basic class names: `Skill`, `SkillContainer`, `SkillTemplate`
- Template at +0x10 on Skill
- SkillContainer at +0x18 on Skill
- Skills list at +0x18 on SkillContainer
- Conceptual flow: validate -> gather -> execute -> pay costs (though actual order differs)
- Hit chance involves accuracy, cover, defense/evasion, distance dropoff

---

## Correct Function Addresses Reference

For future documentation, here are the verified addresses:

| Function | Address | Notes |
|----------|---------|-------|
| `Skill$$Use` | 0x1806e2bd0 | Main skill execution |
| `Skill$$GetHitchance` | 0x1806dba90 | Hit chance calculation |
| `Skill$$IsUsable` | 0x1806deb10 | With TargetUsageParams |
| `Skill$$IsUsable` (simple) | 0x1806ded60 | No params, uses defaults |
| `Skill$$IsAffordable` | 0x1806de0d0 | AP cost check |
| `Skill$$IsInRange` (simple) | 0x1806de4f0 | distance, bonus params |
| `Skill$$IsInRange` (complex) | 0x1806de510 | Tile, from tile, flag params |
| `Skill$$GetExpectedDamage` | 0x1806da4b0 | Complex damage calc |
| `Skill$$GetActionPointCost` | 0x1806d8e80 | With modifiers |
| `Skill$$GetMinRangeBase` | 0x1806dc980 | From template or weapon |
| `Skill$$GetMaxRangeBase` | 0x1806dc8b0 | From template or weapon |
| `Skill$$GetCharges` | 0x1806d9b00 | Current charges |
| `SkillContainer$$GetSkillByID` | 0x1806e91b0 | By string ID |
| `SkillContainer$$GetSkillByTemplate` | 0x1806e9340 | By template reference |
| `SkillContainer$$QueryTags` | 0x1806ed3f0 | Collect all tags |
| `SkillContainer$$BuildPropertiesForUse` | 0x1806e81f0 | Build attacker props |
| `SkillContainer$$BuildPropertiesForDefense` | 0x1806e7fd0 | Build defender props |
| `SkillContainer$$OnSkillUsed` | 0x1806ebfa0 | Event handler |
