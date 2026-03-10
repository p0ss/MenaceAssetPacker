# SaveProcessors.cs Verification Report

## Summary

The reference code in `SaveProcessors.cs` contains **CRITICAL DISCREPANCIES** with the actual decompiled code. The addresses provided in the comments point to completely different functions, and the conceptual model of the save system is fundamentally different from the actual implementation.

## Critical Issues

### 1. Incorrect Addresses

All addresses in the reference code point to wrong functions:

| Reference Address | Reference Claim | Actual Function |
|------------------|-----------------|-----------------|
| `0x180718000` | EntityProcessor | `PlaySoundOnEnabledHandler::OnUpdate` - Sound playback handler |
| `0x18071a000` | TacticalMapProcessor | `RemoveDefectHandler::RemoveSkills` - Skill removal handler |
| `0x18071c000` | CampaignProcessor | `ShowHUDIcon::Create` - HUD icon factory method |

### 2. Architectural Discrepancy

The reference code describes a **processor-based system** with:
- `ISaveProcessor` interface
- `SaveContext` / `LoadContext` classes
- Separate EntityProcessor, TacticalMapProcessor, CampaignProcessor classes
- `BinaryWriter` / `BinaryReader` based serialization

The **actual implementation** uses a completely different architecture:
- `SaveState` class that handles both save/load modes
- `ProcessSaveState` methods on individual game objects
- Mode-based operation (`IsLoading()` check determines behavior)
- Process methods like `ProcessInt`, `ProcessString`, `ProcessDataTemplate<T>`, etc.

---

## Actual Save System Architecture

### SaveState Class

The actual save system uses `Menace.Strategy.SaveState` with these key characteristics:

**Key offsets:**
- `+0x18` - Mode indicator (1 = saving, other = loading)
- `+0x20` - Version number
- `+0x78` - Writer object pointer
- `+0x80` - Reader object pointer

**Core methods at their actual addresses:**
- `ProcessInt` @ `0x1805a6240`
- `ProcessString` @ `0x1805a6930`
- `ProcessBool` @ `0x1805a5460`
- `ProcessFloat` @ `0x1805a60c0`
- `ProcessDouble` @ `0x1805a5f30`
- `ProcessInts` @ `0x1805a62a0` / `0x1805a6500`
- `ProcessFloatArray` @ `0x1805a5fa0`
- `ProcessIntArray` @ `0x1805a6130`
- `ProcessDataTemplate<T>` @ `0x180b83940`
- `ProcessDataTemplates<T>` @ `0x180b83ac0` / `0x180b83d50`
- `ProcessObject<T>` @ `0x180b85560`
- `ProcessObjects<T>` @ `0x180b85660`
- `ProcessEnum<T>` @ `0x180b843e0` (ByteEnum), `0x180b84690` (Int32Enum)

### Actual ProcessSaveState Functions

| Function | Address | Description |
|----------|---------|-------------|
| `Squaddie::ProcessSaveState` | `0x1805c2130` | Saves squaddie data (ID, gender, skin, home planet, names) |
| `ItemContainer::ProcessSaveState` | `0x180824480` | Saves item container with item GUID references |
| `Operation::ProcessSaveState` | `0x18058f9e0` | Saves operation templates, results, missions |
| `Mission::ProcessSaveState` | `0x1805866b0` | Saves mission templates, difficulty, biome, objectives |
| `Vehicle::ProcessSaveState` | `0x1805c8e60` | Saves vehicle durability, skills |
| `Roster::ProcessSaveState` | `0x1805a4930` | Processes leader lists |
| `Menace.Strategy.ShipUpgrades::ProcessSaveState` | `0x1805ac430` | Saves ship upgrade templates and counts |
| `Menace.Strategy.Planet::ProcessSaveState` | `0x1805a0aa0` | Saves planet template and state |
| `Menace.Strategy.OperationsManager::ProcessSaveState` | `0x18059ac60` | Saves operation list and status |
| `Menace.Strategy.Squaddies::ProcessSaveState` | `0x1805c39e0` | Saves squaddie collections |
| `Menace.Strategy.StoryFactions::ProcessSaveState` | `0x180593780` | Saves faction dictionary |
| `Menace.Strategy.StoryFaction::ProcessSaveState` | `0x1805929c0` | Saves individual faction state |
| `Menace.Strategy.OwnedItems::ProcessSaveState` | `0x18059d4e0` | Saves vehicles and item collections |
| `Menace.Strategy.BaseUnitLeader::ProcessSaveState` | `0x1805b2300` | Saves unit leader attributes, perks, equipment |
| `Menace.States.StrategyState::ProcessSaveState` | `0x180641b30` | Main coroutine orchestrating save/load |

---

## Detailed Function Analysis

### Squaddie::ProcessSaveState @ 0x1805c2130

**Actual field offsets:**
- `+0x10`: Int (likely ID)
- `+0x14`: Enum `Gender` (ByteEnum)
- `+0x15`: Enum `SkinColor` (ByteEnum)
- `+0x18`: Enum `HomePlanetType` (Int32Enum)
- `+0x28`: String (name?)
- `+0x30`: String (callsign?)
- `+0x38`: Int
- `+0x20`: DataTemplate `EntityTemplate`

**Reference code discrepancy:** The reference EntityProcessor describes saving hitpoints, AP, position, suppression, morale, statuses, equipment, and skills - but the actual Squaddie save is much simpler, focused on identity and template data.

### Operation::ProcessSaveState @ 0x18058f9e0

**Actual field offsets:**
- `+0x10`: DataTemplate `OperationTemplate`
- `+0x18`: DataTemplate `StoryFactionTemplate`
- `+0x20`: DataTemplate `FactionTemplate`
- `+0x28`: Object `OperationResult`
- `+0x30`: DataTemplate `PlanetTemplate`
- `+0x38`: DataTemplate `OperationDurationTemplate`
- `+0x40`: Int
- `+0x48`: Ints (array pointer)
- `+0x50`: Objects `Mission` list
- `+0x58`: Int
- `+0x5c`: Int
- `+0x60`: Int
- `+0x68`: Object `PseudoRandom`
- `+0x70`: DataTemplates `OperationAssetTemplate`
- `+0x78`: DataTemplates `OperationAssetTemplate`
- `+0x80`: OperationProperties (calculated, not saved)
- `+0x9c`: Int
- `+0xa0`: Bool (version > 0x1c)

**Reference code discrepancy:** The reference CampaignProcessor describes saving CurrentDay, CompletedOperations list, ActiveOperations, Faction relationships, ShipUpgrades, and Resources. The actual Operation save is per-operation and contains different data.

### Mission::ProcessSaveState @ 0x1805866b0

**Actual field offsets:**
- `+0x10`: DataTemplate `MissionTemplate`
- `+0x18`: Int
- `+0x1c`: Enum `MissionLayer`
- `+0x20`: Int
- `+0x24`: Int
- `+0x38`: DataTemplate `MissionDifficultyTemplate`
- `+0x40`: ObjectiveManager pointer (used for objectives, not directly saved)
- `+0x48`: Vector2
- `+0x50`: Vector2Int
- `+0x58`: Enum `LightConditionType`
- `+0x60`: DataTemplate `WeatherTemplate`
- `+0x68`: DataTemplate `BiomeTemplate`
- `+0x78`: DataTemplate `OperationAssetTemplate`
- `+0x80`: DataTemplate `FactionTemplate`
- `+0xa8`: Struct `OperationResources`
- `+0xb0`: Ints (next mission indices)
- `+0xb8`: Enum `MissionStatus`
- `+0xd0`: RectInt

### Vehicle::ProcessSaveState @ 0x1805c8e60

**Actual field offsets:**
- `+0x20`: Float (durability, with version < 0x18 migration from int)
- `+0x24`: Float
- `+0x28`: DataTemplates `SkillTemplate`

### StoryFaction::ProcessSaveState @ 0x1805929c0

**Actual field offsets:**
- `+0x10`: DataTemplate `StoryFactionTemplate`
- `+0x18`: Int (relationship value?)
- `+0x1c`: Enum `StoryFactionStatus`
- `+0x20`: DataTemplates `ShipUpgradeTemplate`
- `+0x28`: Ints (HashSet of ints)

### BaseUnitLeader::ProcessSaveState @ 0x1805b2300

**Actual field offsets (via param_1 array indices):**
- `[2]` / `+0x10`: Template pointer
- `[3]` / `+0x18`: Enum `LeaderHealthStatus` (ByteEnum)
- `[6]` / `+0x30`: Attributes object with:
  - `+0x10`: Float array (version >= 0x1a uses floats, older uses ints converted)
- `[7]` / `+0x38`: SkillContainer
- `[8]` / `+0x40`: ItemContainer (Equipment)
- `[9]` / `+0x48`: DataTemplates `PerkTemplate`
- `[10]` / `+0x50`: Object `UnitStatistics`
- `[11]` / `+0x58`: Object `EmotionalStates`
- `[12]` / `+0x60`: Ints
- `[13]` / `+0x68`: Struct `StrategicDuration`
- `[14]` / `+0x70`: ConversationTemplate

---

## StrategyState::ProcessSaveState Orchestration @ 0x180641b30

This is the main save/load coordinator. It's implemented as a coroutine (IEnumerator pattern) and processes these systems in order:

1. Basic state: Double, Bool, String, Int, Bool, Bool, GlobalDifficultyTemplate, IntArray, corruption check
2. ShipUpgrades
3. OwnedItems
4. BlackMarket
5. StoryFactions
6. Squaddies
7. Roster
8. BattlePlan
9. PlanetManager
10. OperationsManager
11. MissionResult
12. ConversationIntVars (dictionary with hash keys)
13. ConversationManager (virtual call)
14. ConversationEffects
15. EventManager (virtual call)
16. BarksManager (virtual call)
17. OffmapAbilityTemplates

Each step has a corruption check and yields for UI progress updates when loading.

---

## Corrections Needed

### Complete Rewrite Required

The reference code needs to be completely rewritten to match the actual architecture:

1. **Remove ISaveProcessor interface** - The game doesn't use this pattern
2. **Remove SaveContext/LoadContext** - Replace with SaveState's mode-based approach
3. **Fix all addresses** - Update to actual function addresses
4. **Fix class structure** - Match actual ProcessSaveState method signatures
5. **Fix field offsets** - Document actual offsets from decompilation
6. **Document version handling** - Several fields have version checks for migration

### Suggested Correct Structure

```csharp
namespace Menace.Strategy
{
    /// <summary>
    /// Save state manager - handles both saving and loading.
    /// Mode is determined by field at +0x18 (1 = saving).
    ///
    /// Address: 0x1805a77e0 (.ctor)
    /// </summary>
    public class SaveState
    {
        // +0x18: Mode (1 = save, other = load)
        // +0x20: Version number
        // +0x78: Writer
        // +0x80: Reader

        public void ProcessInt(ref int value) { }      // 0x1805a6240
        public void ProcessString(ref string value) { } // 0x1805a6930
        public void ProcessBool(ref bool value) { }    // 0x1805a5460
        public void ProcessFloat(ref float value) { }  // 0x1805a60c0
        // etc.
    }

    public partial class Squaddie
    {
        // Address: 0x1805c2130
        public void ProcessSaveState(SaveState state)
        {
            state.ProcessInt(/* +0x10 */);
            state.ProcessEnum<Gender>(/* +0x14 */);
            state.ProcessEnum<SkinColor>(/* +0x15 */);
            state.ProcessEnum<HomePlanetType>(/* +0x18 */);
            state.ProcessString(/* +0x28 */);
            state.ProcessString(/* +0x30 */);
            state.ProcessInt(/* +0x38 */);
            state.ProcessDataTemplate<EntityTemplate>(/* +0x20 */);
        }
    }

    // Similar patterns for other classes...
}
```

---

## Verification Status

| Component | Status | Notes |
|-----------|--------|-------|
| ISaveProcessor interface | **INCORRECT** | Does not exist in game |
| SaveContext class | **INCORRECT** | Does not exist in game |
| LoadContext class | **INCORRECT** | Does not exist in game |
| EntityProcessor | **INCORRECT** | Wrong address, wrong architecture |
| TacticalMapProcessor | **INCORRECT** | Wrong address, wrong architecture |
| CampaignProcessor | **INCORRECT** | Wrong address, wrong architecture |
| Save field patterns | **INCORRECT** | Wrong field types and offsets |
| BinaryWriter/BinaryReader usage | **PARTIALLY CORRECT** | Uses similar concept via ProcessX methods |

---

## Recommendations

1. **Do not use this reference code** for modding or reverse engineering
2. **Start fresh** with the actual decompiled ProcessSaveState functions
3. **Use actual addresses** from this verification report
4. **Follow the SaveState pattern** - single mode-based class, not processor interface
5. **Check version numbers** - Many fields have version-based migration logic

---

*Generated by automated verification against Ghidra decompilation*
*Verification Date: 2026-03-10*
