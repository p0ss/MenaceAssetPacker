# SaveSystem.cs Verification Report

## Summary

**CRITICAL FINDING: The addresses in the reference code are completely incorrect.**

All addresses documented in SaveSystem.cs (0x180712xxx, 0x180715xxx, 0x180716xxx range) do not correspond to save/load functionality. They actually point to unrelated code:
- AI Agent behavior initialization
- Tactical skill effect handlers
- Other unrelated systems

The actual save system is located in the **Menace.Strategy** namespace at addresses in the **0x1805axxxx** range.

---

## Detailed Function Verification

### 1. GetSaveDirectory() - Address: 0x180712100

**Reference Code Claim:**
```csharp
public static string GetSaveDirectory()
{
    return Path.Combine(
        UnityEngine.Application.persistentDataPath,
        "Saves");
}
```

**Actual Implementation:**
- **Correct Address:** `0x1805a8280` (`Menace_Strategy_SaveSystem__GetAndCreateSaveFileFolderPath`)
- **Actual Function Name:** `GetAndCreateSaveFileFolderPath`

The actual function calls `GetAndCreateUserDataFolderPath` with a string literal (likely "Saves" or similar subfolder name). The implementation is more complex as it ensures the directory is created if it doesn't exist.

**Discrepancy:**
- Wrong address (completely different function at documented address)
- Function name differs (includes "AndCreate" prefix)
- Uses a two-step path construction via `GetAndCreateUserDataFolderPath`

---

### 2. GetSavePath(string saveName) - Address: 0x180712180

**Reference Code Claim:**
```csharp
public static string GetSavePath(string saveName)
{
    return Path.Combine(GetSaveDirectory(), saveName + ".sav");
}
```

**Actual Implementation:**
- **Correct Address:** `0x1805a8950` (`Menace_Strategy_SaveSystem__GetSaveFilePath`)

The actual function:
1. Gets the save folder path via `GetAndCreateUserDataFolderPath`
2. Reads a character from static field at `SaveSystem_TypeInfo + 0xb8` (likely file separator)
3. Concatenates: folder + separator char + saveName + extension string literal

**Discrepancy:**
- Wrong address
- Extension is NOT ".sav" - uses a string literal reference (StringLiteral_5788)
- Uses a character separator from a static field rather than direct Path.Combine

---

### 3. SaveGame(string saveName, bool isAutoSave) - Address: 0x180712300

**Reference Code Claim:**
Documented as a simple save flow: create SaveState, serialize, write to file.

**Actual Implementation:**
- **Correct Address:** `0x1805a9170` (`Menace_Strategy_SaveSystem__Save`)
- **Actual Signature:** `Save(int saveType, string customPath, string operationName)`

The actual function is significantly more complex:
1. Takes an integer `saveType` parameter (not bool `isAutoSave`):
   - `1` = Manual save
   - `2` = Quick save (StringLiteral_14696)
   - `3` = Auto save (StringLiteral_13404)
   - `4` = Iron Man save (StringLiteral_5982)
2. Checks for Iron Man mode at offset +0x28 of StrategyState
3. Shows a notification via UIManager
4. Opens file with `System.IO.File.OpenWrite`
5. Creates `SaveState` object with BinaryWriter
6. Calls `StrategyState.ProcessSaveState` as a coroutine
7. Handles auto-save rotation (deletes old auto-saves based on player setting)
8. Saves a screenshot alongside the save file (different extension)

**Major Discrepancies:**
- Completely wrong address
- Different parameter signature (int saveType vs bool isAutoSave)
- No JSON/compression - uses direct binary serialization via BinaryWriter
- Has save type enumeration (manual, quick, auto, iron man)
- Includes auto-save rotation logic
- Saves screenshots with saves

---

### 4. LoadGame(string saveName) - Address: 0x180712600

**Reference Code Claim:**
Simple load flow: read file, deserialize, validate version, restore state.

**Actual Implementation:**
- **Correct Address:** `0x1805a8fa0` (`Menace_Strategy_SaveSystem__Load`)
- **Execution Address:** `0x1805a8080` (`Menace_Strategy_SaveSystem__ExecLoad`)

The actual function:
1. `Load` creates a display class and checks if game is in Iron Man mode
2. If not in Iron Man, shows a confirmation dialog (yes/no) before loading
3. `ExecLoad` actually performs the load:
   - Checks file exists
   - Closes all UI screens
   - Shows loading overlay
   - Creates a coroutine (`LoadSaveGameCoroutine`) to handle async loading
   - Uses `StrategyScheduler.Execute` for the coroutine

**LoadSaveGameCoroutine** (at `0x1805aeca0`):
1. Creates MemoryStream and copies file content
2. Removes TacticalState if present (finishes tactical mission)
3. Removes existing StrategyState
4. Creates new SaveState with BinaryReader mode
5. Loads StrategyConfig based on save's config ID
6. Creates new StrategyState and calls ProcessSaveState coroutine

**Major Discrepancies:**
- Wrong address
- Takes SaveState object, not string
- Has confirmation dialog for non-Iron Man saves
- Uses coroutine-based async loading
- No JSON deserialization - uses BinaryReader
- Has complex state management (removes tactical/strategy states)

---

### 5. SaveSerializer.Serialize() - Address: 0x180715100

**Reference Code Claim:**
```csharp
// Magic number "MSAV"
// Uses JSON + LZ4 compression
```

**Actual Implementation:**
The game does NOT use a SaveSerializer class or JSON format.

**Actual format:**
- Uses `System.IO.BinaryWriter` directly in `SaveState` constructor
- Header written first (version, save type, timestamp, metadata)
- Version check: `0x16` (22) to `0x65` (101) is valid range
- No JSON serialization
- No compression
- No "MSAV" magic number

**SaveState Header (writing mode - address 0x1805a77e0):**
1. Write version int (0x65 = 101 for current)
2. Write save type int
3. Write timestamp as DateTime.Ticks (long)
4. Write planet name string
5. Write operation name string
6. Write completed missions count int
7. Write total missions count int
8. Write campaign template ID string
9. Write strategy config ID string (version > 0x1b)
10. Write play time double
11. Write custom save name string

**Major Discrepancies:**
- SaveSerializer class doesn't exist
- No magic number
- No JSON
- No compression
- Direct binary serialization
- Version is integer (currently 101), not struct

---

### 6. SaveSerializer.Deserialize() - Address: 0x180715300

**Reference Code Claim:**
Validates magic number, decompresses, parses JSON.

**Actual Implementation:**
Uses `System.IO.BinaryReader` in `SaveState` constructor (read mode).

**SaveState Header (reading mode):**
1. Read version int
2. Validate version in range [0x16, 0x65] (22-101)
3. Read save type int
4. Read timestamp as ticks, construct DateTime
5. Read planet name string
6. Read operation name string
7. Read completed missions int
8. Read total missions int
9. Read campaign template ID string
10. Conditionally read strategy config ID (if version > 0x1b)
11. Read play time double
12. Read custom save name string

**Discrepancies:**
- No magic number validation
- No decompression
- No JSON parsing
- Version validation uses numeric range check

---

### 7. TemplateRedirector.GetRedirect() - Address: 0x180716100

**Reference Code Claim:**
Returns redirected template names for save compatibility.

**Actual Implementation:**
No function found at this address related to template redirection.

The address `0x180716100` contains `Menace_Tactical_Skills_Effects_EnemiesDropPickupOnDeathHandler__OnMissionStarted` which is completely unrelated.

**Discrepancy:**
- Function may not exist or is named differently
- No evidence of TemplateRedirector class in analyzed code

---

## SaveState Class Field Offsets

Based on decompiled `SaveState` constructor, actual field layout:

| Offset | Type | Field Description |
|--------|------|-------------------|
| +0x10 | Stream | File stream reference |
| +0x18 | int | Mode (1=write, 0=read) |
| +0x1c | bool | Is valid flag |
| +0x20 | int | Version |
| +0x24 | int | Save type |
| +0x28 | DateTime | Timestamp |
| +0x30 | string | Planet name |
| +0x38 | string | Operation name |
| +0x40 | int | Completed missions |
| +0x44 | int | Total missions |
| +0x48 | string | Campaign template ID |
| +0x50 | double | Play time |
| +0x58 | string | Custom save name |
| +0x60 | string | Strategy config ID |
| +0x68 | object | Unknown (possibly operation ref) |
| +0x70 | string | File path |
| +0x78 | BinaryWriter | Writer reference |
| +0x80 | BinaryReader | Reader reference |

**Reference code offsets are INCORRECT.**

---

## StrategyState ProcessSaveState Flow

The actual save state processing (at `0x18064c130`) saves/loads:
1. Play time double (+0x20)
2. Iron Man bool (+0x28)
3. Save name string (+0x30)
4. Current day/turn int (+0x38)
5. Additional bools (+0x3c, +0x3d)
6. Difficulty template (+0x48)
7. Int array (+0xc0)
8. Ship upgrades (+0xa0)
9. Owned items (+0x80)
10. Black market (+0x88)
11. Story factions (+0xb8)
12. Squaddies (+0x68)
13. Roster (+0x70)
14. Battle plan (+0x78)
15. Planet manager (+0x50)
16. Operations manager (+0x58)
17. Mission result (+0x60)
18. Conversation variables (+0xc8)
19. Barks manager (+0xa8)
20. Event manager (+0xb0)
21. Unknown manager (+0x90)
22. Offmap abilities (+0x98)
23. Conversation effects (+0xd0)

---

## Correct Function Addresses

| Function | Reference Address | Actual Address |
|----------|------------------|----------------|
| GetSaveDirectory | 0x180712100 | 0x1805a8280 |
| GetSavePath | 0x180712180 | 0x1805a8950 |
| SaveGame | 0x180712300 | 0x1805a9170 |
| LoadGame | 0x180712600 | 0x1805a8fa0 |
| ExecLoad | N/A | 0x1805a8080 |
| SaveState.ctor | N/A | 0x1805a77e0 |
| SaveState.Close | N/A | 0x1805a51e0 |
| CheckCorruption | N/A | 0x1805a50d0 |
| Delete | N/A | 0x1805a7f10 |
| ProcessSaveState | N/A | 0x18064c130 |

---

## Corrections Needed

1. **All addresses must be corrected** - Every single address is wrong
2. **SaveSerializer class should be removed** - No such class exists
3. **Save format documentation is completely wrong**:
   - No magic number
   - No JSON
   - No compression
   - Direct binary format using BinaryWriter/BinaryReader
4. **SaveState structure needs complete rewrite** with correct field offsets
5. **Save type enumeration should be added** (Manual=1, Quick=2, Auto=3, IronMan=4)
6. **TemplateRedirector may not exist** - needs investigation
7. **Version is integer (currently 101)**, not struct with Major.Minor.Patch
8. **Actual namespace is Menace.Strategy**, not Menace.SaveLoad

---

## Recommendations

The reference code file appears to be **entirely speculative/fabricated** and does not reflect the actual game implementation. It should be:

1. **Completely rewritten** based on actual decompiled code
2. **All addresses updated** to correct 0x1805axxxx range
3. **Save format documentation corrected** to reflect binary format
4. **Class structure updated** to match actual SaveState implementation
5. **SaveSerializer removed** and replaced with actual BinaryWriter/BinaryReader usage

The current reference code provides misleading information that could cause significant issues for anyone trying to understand or modify save/load functionality.
