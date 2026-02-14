# SDK Remediation Plan - Phase 5

## Overview

This phase addresses issues found in 11 SDK files from the latest audit round.
- **Critical Issues:** 7 files need significant fixes
- **Minor Issues:** 4 files need small improvements

**Total estimated issues: ~45**

---

## Critical Fixes (7 files)

### Task #1: Operation.cs (~8 issues) - COMPLETED

| Issue | Current | Fix To | Status |
|-------|---------|--------|--------|
| Singleton access | `GetProperty("s_Singleton")` | Use `StrategyState.Get()` static method | FIXED |
| Operations access | `GetProperty("Operations")` | Direct field access at offset `+0x58` | FIXED |
| GetTemplate | Method call | Direct field access at `+0x10` | FIXED |
| GetEnemyFaction | Method doesn't exist | Use `GetEnemyStoryFaction()` instead | FIXED |
| GetMissions | Method doesn't exist | Direct field access at `+0x50` | FIXED |
| m_CurrentMissionIdx | Property lookup | Direct field read at `+0x40` | FIXED |
| m_PassedTime | Property lookup | Direct field read at `+0x5c` | FIXED |
| m_MaxTimeUntilTimeout | Property lookup | Direct field read at `+0x58` | FIXED |

---

### Task #2: Mission.cs (~12 issues) - COMPLETED

| Issue | Current | Fix To | Status |
|-------|---------|--------|--------|
| TacticalManager singleton | `s_Singleton` property | Use `Get()` static method | FIXED |
| TacticalManager.m_Mission | Property doesn't exist | Access via StrategyState/Operation chain | FIXED |
| GetTemplate() | Method call | Direct field at `+0x10` | FIXED |
| GetStatus() | Method call | Direct field at `+0xB8` | FIXED |
| GetLayer() | Method call | Direct field at `+0x1C` | FIXED |
| GetSeed() | Method call | Direct field at `+0x24` | FIXED |
| GetBiome() | Method call | Direct field at `+0x70` | FIXED |
| GetWeatherTemplate() | Method call | Direct field at `+0x60` | FIXED |
| GetDifficulty() | Method call | Direct field at `+0x38` | FIXED |
| GetLightCondition() | Wrong name | Use `GetLightConditionTemplate()` | FIXED |
| Objectives property | Property | Direct field at `+0x40`, objectives at `+0x18` | FIXED |
| IsFailed() | Method doesn't exist | Check state field directly (state == 3) | FIXED |

---

### Task #3: Roster.cs (~6 issues) - COMPLETED

| Issue | Current | Fix To | Status |
|-------|---------|--------|--------|
| StrategyState singleton | `s_Singleton` property | Use `Get()` static method | FIXED |
| Roster property | `GetProperty("Roster")` | `GetField("m_Roster")` or offset `+0x70` | FIXED |
| HiredLeaders | `GetProperty("HiredLeaders")` | `GetField("m_HiredLeaders")` or offset `+0x10` | FIXED |
| Template on BaseUnitLeader | `GetProperty("Template")` | `GetField("m_Template")` or offset `+0x10` | FIXED |
| Perks | `GetProperty("Perks")` | `GetField("m_Perks")` or offset `+0x48` | FIXED |
| GetHirableLeaders() | Method doesn't exist | Iterate field at Roster `+0x18` | FIXED |

---

### Task #4: Inventory.cs (~8 issues) - COMPLETED

| Issue | Current | Fix To | Status |
|-------|---------|--------|--------|
| StrategyState singleton | `s_Singleton` property | Use `Get()` static method | FIXED |
| OwnedItems | `GetProperty("OwnedItems")` | Direct field access | FIXED |
| Actor.GetItemContainer() | Method doesn't exist | Field/interface access | FIXED |
| ItemTemplate.SlotType | Property | Field `m_SlotType` at `+0xe8` | FIXED |
| Item.Skills | Property | Field access | FIXED |
| ItemContainer.ModularVehicle | Property | Field at `+0x20` | FIXED |
| GetItemsWithTag | Returns List | Returns count (int) - use QueryTags | FIXED |
| AddItem/TryAddItem | Methods don't exist | Use `Place()` method | FIXED |

---

### Task #5: Conversation.cs (~5 issues) - COMPLETED

| Issue | Current | Fix To | Status |
|-------|---------|--------|--------|
| OFFSET_CT_TYPE | 0x18 | 0x28 | FIXED |
| ShowNextNode() | No params | Requires currentNode parameter | FIXED |
| TacticalManager.ConversationManager | Property | Doesn't exist - use TacticalBarksManager | FIXED |
| StrategyState singleton | `s_Singleton` | Use `Get()` method | FIXED |
| Trigger types | 16 defined | 52 exist (document or expand) | FIXED |

---

### Task #6: BlackMarket.cs (~5 issues) - COMPLETED

| Issue | Current | Fix To | Status |
|-------|---------|--------|--------|
| StackType enum | Type 1 = Reward | Type 1 = Permanent (no timeout) | FIXED |
| StackType enum | Missing Type 4 | Add SpecialOffer = 4 | FIXED |
| StrategyConfig offsets | Direct fields | Inside BlackMarketConfig sub-object at `+0x198` | FIXED |
| StrategyState.BlackMarket | Property | Field at `+0x88` | FIXED |
| s_Singleton | Property | Use `Get()` method | FIXED |

---

### Task #7: AICoordination.cs (~5 issues) - COMPLETED

| Issue | Current | Fix To | Status |
|-------|---------|--------|--------|
| m_Tile field | Direct field access | Use `Entity.GetTile()` method | FIXED |
| TargetTileX/TargetTileZ | Nullable int properties | Don't exist - use tile reference at `+0x58` | FIXED |
| TargetEntity | Direct field | Not found - verify access pattern | FIXED |
| IsThinking | Bool field read | Check `*(int*)(faction + 0x34) == 0 or 1` | FIXED |
| m_FactionIndex | Field name | Verify - offset `+0x14` confirmed | FIXED |

---

## Minor Fixes (4 files)

### Task #8: ModError.cs (~3 issues) - COMPLETED

| Issue | Fix | Status |
|-------|-----|--------|
| Missing Fatal() method | Add `Fatal(string modId, string message, Exception ex = null)` convenience method | FIXED |
| Missing exception param | Add optional `Exception` parameter to `Warn()` and `Info()` | FIXED |
| Missing InfoInternal | Add `InfoInternal(string source, string message)` for symmetry | FIXED |

---

### Task #9: ModSettings.cs (~2 issues) - COMPLETED

| Issue | Fix | Status |
|-------|-----|--------|
| Race condition | Add lock around file I/O operations | FIXED |
| Silent failure | Log warning when `Register()` receives invalid parameters | FIXED |

---

### Task #10: SdkLogger.cs (~2 issues) - COMPLETED

| Issue | Fix | Status |
|-------|-----|--------|
| Missing overloads | Add `Warning(string format, params object[] args)` and `Error(string format, params object[] args)` | FIXED |
| No exception handling | Wrap `string.Format` in try/catch with fallback | FIXED |

---

### Task #11: ErrorNotification.cs (~2 issues) - COMPLETED

| Issue | Fix | Status |
|-------|-----|--------|
| Texture hideFlags | Add `hideFlags = HideFlags.HideAndDontSave` to Texture2D | FIXED |
| No reinitialization | Add texture validity check like DevConsole has | FIXED |

---

## Common Patterns

1. **Singleton access:** Replace `GetProperty("s_Singleton")` with invoking `Get()` static method
2. **Property vs Field:** Many "properties" are actually direct fields with `m_` prefix
3. **Method existence:** Verify methods exist before calling - many getters don't exist
4. **Field offsets:** Use documented offsets for direct field access when methods don't exist

---

## Execution Plan - COMPLETED

1. ✅ Launch 11 fix agents in parallel (one per file)
2. ✅ Each agent applies fixes and verifies build
3. ✅ Manual review after completion
4. ✅ Full build verification - **Build succeeded with 0 warnings, 0 errors**
5. ✅ Documentation updated

---

## Completion Summary

**All 11 SDK files have been remediated:**
- Task #1: Operation.cs - 8 issues fixed
- Task #2: Mission.cs - 12 issues fixed
- Task #3: Roster.cs - 6 issues fixed
- Task #4: Inventory.cs - 8 issues fixed
- Task #5: Conversation.cs - 5 issues fixed
- Task #6: BlackMarket.cs - 5 issues fixed
- Task #7: AICoordination.cs - 5 issues fixed
- Task #8: ModError.cs - 3 issues fixed
- Task #9: ModSettings.cs - 2 issues fixed
- Task #10: SdkLogger.cs - 2 issues fixed
- Task #11: ErrorNotification.cs - 2 issues fixed

**Total: ~58 issues fixed across 11 SDK files**

