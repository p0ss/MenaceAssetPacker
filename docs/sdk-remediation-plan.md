# SDK Remediation Plan - Phase 4

## Overview

This document captures audit findings from comparing SDK files against the live game REPL.
All 8 files have been audited and issues identified.

**Total Issues: ~54 across 8 files (+1 dependency fix)**

---

## Task #1: ArmyGeneration.cs (7 issues) - COMPLETED

| Line | Current | Fix To | Status |
|------|---------|--------|--------|
| 85 | `"TotalBudget"` | `"m_Budget"` | FIXED |
| 90 | `"Entries"` | `"m_Entries"` | FIXED |
| 145 | `ArmyEntry.Template` | `EntityTemplate` | FIXED |
| 153, 223, 449 | `EntityTemplate.Cost` | `ArmyPointCost` | FIXED |
| 159 | `ArmyEntry.Count` | `m_Amount` | FIXED |
| 421 | `ArmyTemplate.Entries` | `PossibleUnits` | FIXED |
| 459-460 | `ArmyTemplateEntry.Count/Amount` | Uses `Weight` for selection probability | FIXED |

**Also:** Added `_armyTemplateEntryType` to type cache.

---

## Task #2: CombatSimulation.cs (6 issues) - COMPLETED

| Line | Current | Fix To | Status |
|------|---------|--------|--------|
| 210 | `ModError.ReportInternal(...)` | `ModError.Report(modId, msg, ex, severity)` | FIXED |
| 110-116 | `Actor.SkillContainer` property | `Actor.GetSkills()` method | FIXED |
| 120-123 | `SkillContainer.Skills` | `m_Skills` | FIXED |
| 167-175 | `GetHitchance` 6 params, wrong order | 7 params: (from, target, props, defProps, includeDropoff, entity, forImmediate) | FIXED |
| 186-204 | Field names wrong | `FinalHitChance`→`FinalValue`, `DodgeMult`→`DefenseMult`, `DistancePenalty`→`AccuracyDropoff`, `IncludesDistance`→`IncludeDropoff` | FIXED |
| 27-37 | `HitChanceResult` class | Update field names to match, add `AlwaysHits` | FIXED |

**Also fixed:** Updated console commands output, documentation header comments, and reference documentation files.

---

## Task #3: TileEffects.cs (7 issues) - COMPLETED

| Line | Current | Fix To | Status |
|------|---------|--------|--------|
| 460-461 | `Menace.Tactical.TileEffectHandler` | `Menace.Tactical.TileEffects.TileEffectHandler` | FIXED |
| 460-461 | `Menace.Tactical.TileEffectTemplate` | `Menace.Tactical.TileEffects.TileEffectTemplate` | FIXED |
| 68-72 | `GetProperty("Effects")` | `GetMethod("GetEffects")` | FIXED |
| 324 | `CreateHandler` method | `Create` method | FIXED |
| 287, 296 | `int delay = 0` | `float delay = 0f` | FIXED |
| 103-105 | `HasDuration`, `Duration`, `BlocksLineOfSight` | `HasTimeLimit`, `RemoveAfterRounds`, `BlockLineOfSight` | FIXED |
| 122 | `RoundsElapsed` field | `GetLifetime()` method | FIXED |

**Also:** Renamed Y→Z in parameter names throughout.

---

## Task #4: Vehicle.cs (10 issues) - COMPLETED

| Line | Current | Fix To | Status |
|------|---------|--------|--------|
| 82 | `Template` | `EntityTemplate` | FIXED |
| 91 | `HitpointsPct` | `m_HitpointsPct` | FIXED |
| 95 | `ArmorDurabilityPct` | `m_ArmorDurabilityPct` | FIXED |
| 160 | `ModularVehicle` | `m_ModularVehicle` | FIXED |
| 171 | `HasTwinFire` | `IsTwinFire` | FIXED |
| 176 | `GetEquippedCount()` no params | Requires `ModularVehicleWeaponTemplate` param | FIXED |
| 364 | `ItemsModularVehicle.Slot` type | Use nested type or `ModularVehicleSlot` | FIXED |
| 224-226 | `IsEnabled` on Slot | Doesn't exist - remove | FIXED |
| 229-239 | `Slot.Template` | `Slot.Data` (returns `ModularVehicleSlot`) | FIXED |
| 243-250 | `EquippedItem` | `MountedWeapon` | FIXED |

---

## Task #5: Perks.cs (3 issues + 1 dependency) - COMPLETED

| Line | Current | Fix To | Status |
|------|---------|--------|--------|
| 74 | `Perks` property | `m_Perks` | FIXED |
| 183, 226 | `is Array` check | `is IEnumerable` (Il2CppReferenceArray is not System.Array) | FIXED |

**DEPENDENCY FIX in Roster.cs:**
| Line | Current | Fix To | Status |
|------|---------|--------|--------|
| 663-666 | `GetField("LeaderTemplate")` | `GetProperty("LeaderTemplate")` | FIXED |

---

## Task #6: Emotions.cs (14 issues - MAJOR REWORK) - COMPLETED

### Enum Values Completely Wrong - FIXED

**EmotionalStateType (lines 71-99):**
- Old: None, Angry, Confident, Targeted, Grief, Fear, Inspired, Vengeful, Traumatized
- New: None, AnimosityTowards, Determined, Weary, Disheartened, Eager, Frustrated, Exhausted, GoodwillTowards, Hesitant, Overconfident, Injured, Bruised, Euphoric, Miserable

**EmotionalTrigger (lines 104-132):**
- Old: None, KilledEnemy, WasWounded, AllyKilled, AllyWounded, MissionSuccess, MissionFailure, OperationStart, OperationEnd
- New: StabilizedBy, StabilizedOthers, ReceivedFriendlyFireFrom, DeployedXTimesWithOther, KilledXEnemyEntities, KilledXEnemyMiniBosses, DeployedInTheXMissionsBeforeCurrent, NotDeployedInTheXMissionsBeforeCurrent, KilledXCivElements, SuccessOnFavPlanet, FailedOnFavPlanet, LostOverXPercentHitpoints, GameEffect, Event, Cheat, OtherLeaderKilledCivElementOnFavPlanet, Fled, NearDeathExperience, LostAllSquaddies, Last

### Property/Method Fixes - ALL FIXED

| Line | Current | Fix To | Status |
|------|---------|--------|--------|
| 758 | `States` | `m_States` | FIXED |
| 456 | `Template` on leader | `GetTemplate()` method | FIXED |
| 777 | `GetProperty("Template")` | `GetMethod("GetTemplate")` | FIXED |
| 782-784 | `Type` | `StateType` | FIXED |
| 463-468 | `TriggerEmotion` 2 params | 4 params: (trigger, target, random, mission) | FIXED |
| 540-546 | `TryApplyEmotionalState` 4 params | 5 params: add `_showAsReward` bool | FIXED |
| 334, 600, 787 | Enum passed as `(int)type` | Pass enum type directly | FIXED |
| 164, 257 | `IsNegative` | `IsPositive` (inverted) | FIXED |
| 167, 258 | `IsStackable` | Doesn't exist - use `IsSuperState` | FIXED |
| 56 | `Replacement` | `SuperState` | FIXED |
| 52, 261-266 | `Skill` | `Effect` | FIXED |
| 1144-1172 | `PseudoRandom.Instance` or `Get()` | Must instantiate - no singleton | FIXED |
| 39-40 | `LastMissionParticipation` fields | Different semantics - `m_NextDeployedXTimesWithOther` | FIXED |

**Also fixed:** Updated helper methods, documentation files, and console command outputs.

---

## Task #7: BattleLog.cs (5 issues) - COMPLETED

| Line | Current | Fix To | Status |
|------|---------|--------|--------|
| 511-512 | `ReadInt("Damage")`, `ReadInt("ArmorDamage")` | Use property getters via reflection (not fields) | FIXED |
| 372 | `ReadString("DebugName")` | Use property getter via `ReadPropertyString()` helper | FIXED |
| 458 | `ReadObj("HitChance")` for embedded struct | Use reflection on proxy object to access embedded struct | FIXED |
| 480-491 | Hardcoded offsets 0x10, 0x14 | Use `ResolveEmbeddedStructSize()` for dynamic resolution; fallback to 0x18 (5 floats + 2 bools) | FIXED |
| 499 | Comment says "struct" | Updated comment: DamageInfo is a class | FIXED |

---

## Task #8: UIInspector.cs (1 issue) - COMPLETED

| Line | Current | Fix To | Status |
|------|---------|--------|--------|
| 416-418 | `GetMethod("InvokeClicked")` | `clickable.clicked?.Invoke()` - use Action delegate | FIXED |

---

## Common Patterns Across All Files

1. **Property naming:** Game uses `m_` prefix for backing fields
2. **Methods vs Properties:** Game often uses `GetX()` methods instead of `X` properties
3. **Coordinates:** Game uses X/Z (not X/Y) - Z is horizontal depth
4. **Enums:** Pass actual enum types, not int casts
5. **Il2Cpp arrays:** `Il2CppReferenceArray<T>` is not `System.Array`, use `IEnumerable`
6. **Singletons:** Use `s_Singleton` not `Instance`

---

## Execution Plan - COMPLETED

1. ✅ Launch 8 fix agents in parallel (one per file)
2. ✅ Each agent applies fixes and updates docs
3. ✅ Manual review after completion
4. ✅ Build verification - **Build succeeded with 0 warnings, 0 errors**
5. ⏳ Integration testing with live game REPL (pending)

---

## Completion Summary

**All 8 SDK files have been remediated:**
- Task #1: ArmyGeneration.cs - 7 issues fixed
- Task #2: CombatSimulation.cs - 6 issues fixed + GameMcpServer.cs updated
- Task #3: TileEffects.cs - 7 issues fixed
- Task #4: Vehicle.cs - 10 issues fixed
- Task #5: Perks.cs - 3 issues fixed + Roster.cs dependency fix
- Task #6: Emotions.cs - 14 issues fixed (major rework)
- Task #7: BattleLog.cs - 5 issues fixed
- Task #8: UIInspector.cs - 1 issue fixed

**Total: ~54 issues fixed across 8 SDK files**

**Documentation updated:**
- All API documentation files
- All reverse engineering documentation files
- Reference code examples
