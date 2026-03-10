# SkillEffects.cs Verification Report

**Date:** 2026-03-10
**Binary:** Ghidra Analysis
**Reference File:** `/home/poss/Documents/Code/Menace/MenaceAssetPacker/docs/reference-code/Skills/SkillEffects.cs`

---

## CRITICAL FINDING: Address Mismatch

**ALL addresses provided in the reference code are INCORRECT.**

The addresses listed in the reference code (0x1806a2340, 0x1806a2890, etc.) resolve to completely unrelated functions in the `Menace.Tactical.Mapgen.ChunkGenerator` namespace, NOT skill effect functions.

| Reference Address | Claimed Function | Actual Function |
|------------------|------------------|-----------------|
| 0x1806a2340 | DamageEffect.Apply | No function found |
| 0x1806a2890 | HealEffect.Apply | ChunkGenerator.OnLayoutPass |
| 0x1806a2b10 | SuppressEffect.Apply | ChunkGenerator.OnRoadGeneration |
| 0x1806a2d80 | ApplyStatusEffect.Apply | ChunkGenerator.OnSecondPass |
| 0x1806a3210 | MoveEffect.Apply | ChunkGenerator.PlaceBlockEntry |
| 0x1806a3680 | CreateTileEffect.Apply | ChunkGenerator.CreateBlueprint |
| 0x1806a1200 | EffectProcessor.ProcessEffects | ChunkGenerator.CreateBlueprint |

---

## Architecture Discrepancy: Effect System Design

### Reference Code Architecture (INCORRECT)

The reference code describes:
- Abstract `SkillEffect` base class with `Apply(EffectContext)` method
- Derived effect classes: `DamageEffect`, `HealEffect`, `SuppressEffect`, etc.
- `EffectProcessor.ProcessEffects()` to apply all effects
- `EffectContext` class with skill/source/target data

### Actual Architecture (CORRECT)

The actual implementation uses a **Handler Pattern**:

1. **Effect Templates** (ScriptableObjects) - Store configuration data
   - `Menace.Tactical.Skills.Effects.Damage` - Damage effect template
   - `Menace.Tactical.Skills.Effects.ChangeSuppression` - Suppression effect
   - `Menace.Tactical.Skills.Effects.ChangeProperty` - Stat modification
   - etc.

2. **Effect Handlers** (Runtime) - Execute the effect logic
   - `DamageHandler` @ 0x180702970 (ApplyDamage)
   - `ChangeSuppressionHandler` @ 0x180700600 (Apply)
   - `ChangePropertyHandler` @ 0x1806fee90 (Apply_ModifyEntityStat)
   - `SpawnTileEffectHandler` @ 0x18071dbe0 (Apply_WithProbability)
   - etc.

3. **Factory Pattern** - `Create()` methods instantiate handlers
   - `Damage.Create()` @ 0x180703500 creates `DamageHandler`
   - Handler stores effect template reference at offset +0x18

---

## Verified Actual Functions

### DamageHandler.ApplyDamage @ 0x180702970

**Actual Data Fields (at effectData = handler+0x18):**

| Offset | Field | Type | Description |
|--------|-------|------|-------------|
| +0x58 | IsAppliedOnlyToPassengers | bool | Redirects damage to passenger inside vehicle |
| +0x5c | FlatDamageBase | int | Base hit count for elements |
| +0x60 | ElementsHitPercentage | float | 0.0-1.0, fraction of elements to hit |
| +0x64 | DamageFlatAmount | float | Flat HP damage added |
| +0x68 | DamagePctCurrentHitpoints | float | % of current HP as damage |
| +0x6c | DamagePctCurrentHitpointsMin | float | Minimum floor for current HP% |
| +0x70 | DamagePctMaxHitpoints | float | % of max HP as damage |
| +0x74 | DamagePctMaxHitpointsMin | float | Minimum floor for max HP% |
| +0x78 | DamageToArmor | float | Flat armor durability damage |
| +0x7c | ArmorDmgPctCurrent | float | % of current armor as damage |
| +0x80 | FatalityType | int enum | Death animation type |
| +0x84 | ArmorPenetration | float | Reduces effective armor |
| +0x88 | ArmorDmgFromElementCount | float | Armor damage scaled by hits |
| +0x8c | ElementHitMinIndex | int | First element index to hit |
| +0x90 | CanCrit | bool | Whether damage can crit |

**Damage Formula (ACTUAL):**
```
hitCount = FlatDamageBase + ceil(elementCount * ElementsHitPercentage)  // minimum 1
hpDamage = max(currentHP * DamagePctCurrentHitpoints, DamagePctCurrentHitpointsMin)
         + DamageFlatAmount
         + max(maxHP * DamagePctMaxHitpoints, DamagePctMaxHitpointsMin)
```

**Reference Code Discrepancies:**
- Reference shows simple `BaseDamage` field - actual has multiple damage components
- Reference shows `DamageType` enum - actual uses a more complex system
- Reference claims offset +0x20 for BaseDamage - actual is +0x64 for DamageFlatAmount
- No element hit percentage logic in reference code
- No passenger redirection logic in reference code

---

### ChangeSuppressionHandler.Apply @ 0x180700600

**Actual Data Fields:**

| Offset | Field | Type | Description |
|--------|-------|------|-------------|
| +0x58 | EventType | int enum | When to apply (0=OnApply) |
| +0x5c | SuppressionAmount | int | Amount of suppression change |
| +0x60 | UseOwner | bool | Apply to owner instead of target |

**Reference Code Discrepancies:**
- Reference code names it `SuppressEffect` - actual is `ChangeSuppression`
- Reference shows `BaseSuppression`, `ScaleWithDistance`, `FalloffStart`, `FalloffEnd` fields
- Actual implementation is much simpler - just applies flat suppression amount
- Distance falloff is NOT part of this effect - it's in `Skill.ApplySuppression()` @ 0x1806d5930

---

### Skill.ApplySuppression @ 0x1806d5930

This function handles the complex suppression with cover/distance:
- Checks cover direction between source and target
- Applies cover mitigation to suppression
- Handles adjacent tile suppression spread
- Uses `Config` values for suppression ratios

**Note:** This is NOT an "effect" but a core Skill method.

---

### SpawnTileEffectHandler @ 0x18071dbe0

**Actual Data Fields:**

| Offset | Field | Type | Description |
|--------|-------|------|-------------|
| +0x58 | EventType | int enum | When to spawn |
| +0x60 | EffectToSpawn | TileEffectTemplate | Template for tile effect |
| +0x68 | ChanceAtCenter | int | % chance at center (0-100) |
| +0x6c | ChancePerTileFromCenter | int | % modifier per tile distance |
| +0x70 | DelayWithDistance | float | Spawn delay multiplier |

**Reference Code Discrepancies:**
- Reference has `TileEffectTemplate`, `Radius`, `EffectDuration` fields
- Actual has probability-based spawning with distance modifiers
- Refreshes existing effects instead of stacking

---

### ChangePropertyHandler @ 0x1806fee90

**Actual Data Fields:**

| Offset | Field | Type | Description |
|--------|-------|------|-------------|
| +0x5c | PropertyType | int enum | EntityPropertyType to modify |
| +0x60 | Value | int | Additive value |
| +0x64 | ValueMult | float | Multiplicative value |
| +0x68 | IValueProvider | interface | Dynamic value calculator |
| +0x70 | StringValueIndex | int | Index for display string |
| +0x74 | InvertSign | bool | Flip positive/negative |

**Reference Code Discrepancies:**
- Reference has `PropertyModifier` class with `PropertyName`, `Type`, `ValuePerStack`
- Actual uses enum-based property types, not string names
- Actual supports dynamic value providers through interface

---

### RestoreArmorDurabilityHandler @ 0x18071aff0

**Actual Data Fields:**

| Offset | Field | Type | Description |
|--------|-------|------|-------------|
| +0x58 | RestorePercent | int | 0-100, % of max to restore |

**Restoration Formula:**
```
restoredAmount = ceil(RestorePercent / 100.0 * MaxDurability)
newDurability = clamp(currentDurability + restoredAmount, 0, MaxDurability)
```

**Note:** There is no "HealEffect" in the reference code matching this pattern. The reference's `HealEffect` doesn't match any actual function.

---

### ChargeHandler.OnUse @ 0x180700f40

Movement effect (closest to reference `MoveEffect`):
- Gets direction to target tile
- Finds destination (tile past target)
- Calls `MoveToWithSkill` with movement type flag
- Applies skill to target tile if configured

**Reference Code Discrepancies:**
- Reference has abstract `MoveEffect` with `MoveType`, `Distance`, collision handling
- Actual implementation is specific to "Charge" ability
- Push/Pull effects are separate implementations

---

### Entity Field Offsets (Verified)

From `Entity.UpdateHitpoints` @ 0x180615050:

| Offset | Field | Type |
|--------|-------|------|
| +0x20 | Elements | List<Element> |
| +0x48 | IsDead | bool |
| +0x4c | FactionID | int |
| +0x54 | CurrentHitpoints | int |
| +0x58 | MaxHitpoints | int |
| +0x5c | CurrentArmorDurability | int |
| +0x60 | MaxArmorDurability | int |
| +0x68 | ContainedEntity | Entity* |
| +0x70 | ContainerEntity | Entity* |

Element HP at: element+0x114 (current), element+0x118 (max)

---

## Effect Type Enumeration (Actual)

The actual system doesn't use a simple `EffectType` enum. Instead, each effect is a separate class:

**Found Effect Classes (partial list):**
- AccuracyStacks, AddItemSlot, AddSkill, AddSkillAfterMovement
- AmmoPouch, ApplyAuthorityDisciplineMod, ApplySkillToSelf
- AttachObject, AttachTemporaryPrefab, Attack, AttackMorale
- Berserk, CameraShake, CauseDefect
- ChangeAPBasedOnHP, ChangeActionPointCost, ChangeActionPoints
- ChangeAttackCost, ChangeDropChance, ChangeGrowthPotential
- ChangeHeatCapacity, ChangeMalfunctionChance, ChangeMorale
- ChangeMovementCost, ChangeProperty, ChangePropertyAura
- ChangePropertyConditional, ChangePropertyConsecutive
- ChangePropertyTarget, ChangeRangesOfSkillsWithTags
- ChangeSkillUseAmount, ChangeStance, ChangeSupplyCosts
- ChangeSuppression, ChangeUsesPerSquaddie
- Charge, ChargeInfantry, ClearTileEffect, ClearTileEffectGroup
- ConsumeOnSkillUse, Cooldown, CounterAttack
- Damage, DamageArmorDurability, DamageOverTime, Deathrattle
- DelayTurn, DeployHeavyWeapon, DestroyProps, DisableByFlag
- DisableItem, DisableSkills, DisallowInvisible, DisplayText
- DivineIntervention, DropRecoverableObject, EjectEntity
- EmitAura, EnemiesDropPickupOnDeath, FilterByCondition
- FilterByMorale, FilterByOtherSkills, IgnoreDamage
- JetPack, Minions.CommandUseSkill
- RestoreArmorDurability, SpawnTileEffect, Suppression
- UseSkill, and many more...

---

## Corrections Required

### 1. Remove All Address Comments
All address comments in the reference code are wrong and should be removed.

### 2. Rewrite Architecture Description
The reference code's class hierarchy is incorrect. Document:
- Effect Template + Handler pattern
- Factory method pattern for handler creation
- Handler stores template at +0x18 offset
- `SkillEventHandler` base class methods

### 3. Fix Field Offsets
All field offsets are incorrect. Use verified offsets from decompilation.

### 4. Update Effect Type List
Replace the `EffectType` enum with documentation of actual effect classes.

### 5. Document Actual Damage Formula
The damage system is much more complex than shown:
- Multiple damage components (flat, %current, %max, armor)
- Element hit calculations
- Passenger redirection
- Hit count based on element percentage

### 6. Remove Fictional Classes
These classes don't exist as shown:
- `HealEffect` (should be stat modification or special skills)
- `SuppressEffect` (is `ChangeSuppression`)
- `MoveEffect` (is specific movement effects like `Charge`)
- `EffectProcessor` (processing is handled differently)
- `EffectContext` (handler has direct skill reference)

---

## Summary

| Aspect | Reference Code | Actual Implementation | Match |
|--------|---------------|----------------------|-------|
| Addresses | All wrong (mapgen namespace) | Different addresses | **NO** |
| Architecture | Inheritance-based effects | Handler pattern | **NO** |
| Field Offsets | Guessed | Verified via decompilation | **NO** |
| Effect Types | Simple enum | Separate classes | **NO** |
| Damage Formula | Simplified | Complex multi-component | **PARTIAL** |
| Suppression | Distance falloff | In Skill, not effect | **NO** |
| Movement | Generic MoveEffect | Specific handlers | **NO** |
| Status Effects | ApplyStatusEffect class | TileEffect system | **NO** |

**Recommendation:** This reference file should be rewritten from scratch based on actual decompiled code. The current version is not reliable for modding or reverse engineering purposes.

---

## Correct Function Addresses Reference

For future reference, here are the actual addresses for skill effect functions:

| Function | Address |
|----------|---------|
| Damage.Create | 0x180703500 |
| DamageHandler.ApplyDamage | 0x180702970 |
| DamageHandler.OnAdded | 0x180702d90 |
| ChangeSuppression.Create | 0x1807007a0 |
| ChangeSuppressionHandler.OnApply | 0x180700760 |
| ChangeSuppressionHandler.Apply | 0x180700600 |
| ChangeProperty.Create | 0x1806ff8e0 |
| ChangePropertyHandler.Apply | 0x1806fee90 |
| SpawnTileEffect.Create | 0x18071e280 |
| SpawnTileEffectHandler.OnApply | 0x18071de80 |
| SpawnTileEffectHandler.Apply_WithProbability | 0x18071dbe0 |
| RestoreArmorDurability.Create | 0x18071b140 |
| RestoreArmorDurabilityHandler.ApplyToEntity | 0x18071aff0 |
| Charge.Create | 0x180701ce0 |
| ChargeHandler.OnUse | 0x180700f40 |
| UseSkill.Create | 0x1807217c0 |
| UseSkillHandler.ApplySkill | 0x180721510 |
| Entity.UpdateHitpoints | 0x180615050 |
| Entity.ApplySuppression | 0x18060f8a0 |
| Skill.ApplySuppression | 0x1806d5930 |
| Element.TeleportTo | 0x1805ffec0 |
