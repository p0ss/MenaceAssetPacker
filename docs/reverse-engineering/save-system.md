# Save/Load System - Complete Binary Format

## Overview

Menace uses a binary serialization system based on `BinaryWriter`/`BinaryReader`. The save system is designed around the `ISaveStateProcessor` interface, which is implemented by any class that needs to persist state.

## Save File Location

```
{UserDataFolder}/Saves/*.save
```

Special files:
- `latest.save` - Most recent autosave/quicksave

## SaveState Class

```c
public class SaveState {
    // Version Info
    private const int SAVE_STATE_VERSION = 101;
    public const int OLDEST_SUPPORTED_SAVE_STATE_VERSION = 22;

    // Stream
    private readonly Stream m_Stream;         // +0x10
    private readonly SaveStateMode m_Mode;    // +0x18 (0=Load, 1=Save)

    // Header Data
    public readonly int Version;              // +0x20
    public readonly SaveStateType SaveStateType; // +0x24

    // I/O
    private BinaryWriter m_Writer;            // +0x78
    private BinaryReader m_Reader;            // +0x80
}
```

---

# HEADER FORMAT

**Important:** SaveStateType is int32 in ALL versions (not byte).

```
HEADER (read first):
  int32    Version (22-101 supported, current=101)
  int32    SaveStateType (see enum below)
  int64    DateTime.Ticks
  string   PlanetName (length-prefixed UTF-8)
  string   OperationName
  int32    CompletedMissions
  int32    OperationLength
  string   Difficulty (template ID)
  string   StrategyConfigName (if version > 27, else skip)
  double   PlayTimeSeconds
  string   SaveGameName
```

### Version Validation

From constructor at `0x1805a77e0`:
```c
// Valid versions: 22 through 101
if (version < 22 || version > 101) {
    IsValid = false;
    return;
}
```

---

# BODY FORMAT - StrategyState

From `StrategyState.ProcessSaveState` (0x18064C130):

```
STRATEGYSTATE BODY:
  1.  double    m_TotalPlayTimeInSec
  2.  bool      m_Ironman
  3.  string    m_IronmanSaveGameName
  4.  int32     m_Seed
  5.  bool      m_HasPickedInitialItemPack
  6.  bool      m_HasPickedInitialLeaders
  7.  template  m_GlobalDifficulty → GlobalDifficultyTemplate
  8.  int[]     m_Vars (see StrategyVars enum below)
  9.  int32     CheckCorruption = 42 (0x2A) ← magic number, versions >= 7
  10. → ShipUpgrades.ProcessSaveState()
  11. → OwnedItems.ProcessSaveState()
  12. → BlackMarket.ProcessSaveState()
  13. → StoryFactions.ProcessSaveState()
  14. → Squaddies.ProcessSaveState()
  15. → Roster.ProcessSaveState()
  16. → BattlePlan.ProcessSaveState()
  17. → PlanetManager.ProcessSaveState()
  18. → OperationsManager.ProcessSaveState()
  19. object    m_LastMissionResult → MissionResult.ProcessSaveState()
  20. dict      m_ConversationIntVars (count + key-value pairs)
  21. → ConversationsManager.ProcessSaveState()
  22. list      m_ConversationEffects → ConversationEffects[]
  23. → EventManager.ProcessSaveState()
  24. → BlackMarketBarksManager.ProcessSaveState()
  25. templates CurrentOffmapAbilities → OffmapAbilityTemplate[]
```

---

# STRATEGYVARS INDICES

The `m_Vars` array stores game resources. Each index represents:

```c
enum StrategyVars {
    Unused6 = 0,               // (unused)
    OciComponents = 1,         // Credits / currency for black market
    PromotionPoints = 2,       // Points for unit promotions
    PromotionPointsEarned = 3, // Total earned (lifetime)
    OperationsPlayed = 4,      // Operations attempted
    OperationsWon = 5,         // Operations completed successfully
    Unused1 = 6,
    Unused2 = 7,
    Unused3 = 8,
    Unused4 = 9,
    Unused5 = 10,
    DemoteRefundPercentage = 11,
    OperationsTimeoutBonus = 12,
    Intelligence = 13,         // Intel resource
    OciRefundPercentage = 14,
    Authority = 15,            // Authority resource
    OciComponentsEarned = 16,  // Total credits earned (lifetime)
    StoryCheckpoints = 17,
    Last = 17
}
```

**Common edits:**
- `m_Vars[1]` = OciComponents (credits)
- `m_Vars[13]` = Intelligence
- `m_Vars[15]` = Authority

---

# CHECKCORRUPTION MARKER

From `SaveState.CheckCorruption` (0x1805a50d0):

```c
void CheckCorruption(SaveState saveState) {
    if (version < 7) return;  // Skip for very old versions

    if (saving) {
        WriteInt32(42);  // Magic number 0x2A
    } else {
        int value = ReadInt32();
        if (value != 42) {
            Debug.LogError($"Corruption: got {value}, expected 42");
        }
    }
}
```

**The corruption marker is a single int32 = 42 (0x2A), written between m_Vars and ShipUpgrades.**

---

# ENUM VALUES

## SaveStateType (int32)

```c
None = 0
Manual = 1
Quick = 2
Auto = 3
Ironman = 4
```

## Gender (byte)

```c
Male = 0
Female = 1
```

## SkinColor (byte)

```c
White = 0
Brown = 1
Black = 2
```

## HomePlanetType (int32)

```c
// Meta values
Random = 0
RandomCoreWorld = 1
RandomWayback = 2

// Wayback planets (10-15)
Dice = 10
Songlurch = 11
Fira = 12
Mock = 13
Tolimen = 14
Backbone = 15

// Core worlds (50-55)
Earth = 50
TheMoon = 51
Mars = 52
Kentaurus = 53
Proxima = 54
Efio13 = 55
```

## LeaderHealthStatus (byte)

```c
Alive = 0
BleedingOut = 1
Stabilized = 2
PermanentlyDead = 3
```

## ActorType (int32) - for Roster leaders

```c
Infantry = 0   // → creates SquadLeader
Vehicle = 1    // → creates Pilot
Turret = 2     // (not used in Roster)
```

## OperationStatus (int32)

```c
Running = 0
Completed = 1
Failed = 2
Aborted = 3
```

## MissionStatus (int32)

```c
Playable = 0
Locked = 1
Played = 2
Unplayable = 3
```

## MissionLayer (int32)

```c
Invalid = 0
First = 1
Middle = 2
Final = 3
```

## BlackMarketStackType (byte)

```c
None = 0
Base = 1
Regular = 2
Tagged = 3
SpecialOffer = 4
```

## StoryFactionStatus (int32)

```c
Unknown = 0
Known = 1
```

---

# NESTED PROCESSOR FORMATS

## 1. ShipUpgrades (0x1805ac430)

```
SHIPUPGRADES:
  1.  template[]  m_SlotOverrides     → ShipUpgradeTemplate[] (fixed array)
  2.  templates   m_PermanentUpgrades → List<ShipUpgradeTemplate>
  3.  int[]       m_SlotLevels
  4.  dict        m_UpgradeAmounts:
        int32     count
        for each:
          template  ShipUpgradeTemplate
          int32     amount
```

---

## 2. OwnedItems (0x18059d4e0)

```
OWNEDITEMS:
  1.  int[]       m_PurchasedDossiers   (if version > 26)

  2.  --- Vehicles ---
      int32       vehicleCount
      for each vehicle:
        template    EntityTemplate
        string      vehicleGUID
        → Vehicle.ProcessSaveState()

  3.  --- Item Dictionary ---
      int32       templateCount
      for each template:
        template    BaseItemTemplate
        int32       itemCount
        for each item:
          string      itemGUID

  4.  templates   m_SeenItems → List<BaseItemTemplate>
```

### Vehicle (0x1805c8e60)

```
VEHICLE:
  1.  float       m_HealthPercent    (if version >= 24, else read int → set to 1.0)
  2.  float       m_ShieldPercent
  3.  templates   m_OverrideSkills → List<SkillTemplate>
```

---

## 3. BlackMarket (0x180569870)

```
BLACKMARKET:
  1.  objects     m_ItemStacks → List<BlackMarketItemStack>
```

### BlackMarketItemStack (0x1805677a0)

```
BLACKMARKETITEMSTACK:
  1.  template    m_Template → BaseItemTemplate
  2.  int32       m_Amount
  3.  enum        m_Type → BlackMarketStackType (byte)
  4.  --- Items list ---
      int32       itemCount
      for each item:
        template    BaseItemTemplate
        string      itemGUID
```

---

## 4. StoryFactions (0x1805937d0)

```
STORYFACTIONS:
  int32         count
  for each:
    → StoryFaction.ProcessSaveState()
```

### StoryFaction (0x1805929c0)

```
STORYFACTION:
  1.  template    m_Template → StoryFactionTemplate
  2.  int32       m_Reputation
  3.  enum        m_Status → StoryFactionStatus (int32)
  4.  templates   m_UnlockedShipUpgrades → List<ShipUpgradeTemplate>
  5.  int[]       m_DiscoveredMissions (as HashSet<int>)
```

---

## 5. Squaddies (0x1805c39e0)

```
SQUADDIES:
  1.  int32       m_NextSquaddieID
  2.  objects     m_AllSquaddies → List<Squaddie>
  3.  objects     m_DeadSquaddies → List<Squaddie>
```

### Squaddie (0x1805c2130)

```
SQUADDIE:
  1.  int32       m_ID
  2.  enum        m_Gender → Gender (byte)
  3.  enum        m_SkinColor → SkinColor (byte)
  4.  enum        m_HomePlanetType → HomePlanetType (int32)
  5.  string      m_FirstName
  6.  string      m_LastName
  7.  int32       m_PortraitIndex
  8.  template    m_Template → EntityTemplate
```

---

## 6. Roster (0x1805a4930)

```
ROSTER:
  1.  → ProcessLeaderList(m_HiredLeaders)
  2.  → ProcessLeaderList(m_DismissedLeaders)
  3.  → ProcessLeaderList(m_UnburiedLeaders)
  4.  → ProcessLeaderList(m_BuriedLeaders)
  5.  templates   m_HirableLeaders → List<UnitLeaderTemplate>
```

### ProcessLeaderList (0x1805a4400)

```
LEADERLIST:
  int32         count
  for each:
    enum          ActorType (int32: 0=Infantry→SquadLeader, 1=Vehicle→Pilot)
    template      UnitLeaderTemplate
    → BaseUnitLeader.ProcessSaveState()
```

### BaseUnitLeader (0x1805b2300)

```
BASEUNITLEADER:
  1.  float[]     m_Attributes.Values   (if version < 26: read int[] ÷ 1000.0)
                                        (resize to 7 after load)
  2.  templates   m_Perks → List<PerkTemplate>
  3.  → ItemContainer.ProcessSaveState() (m_Equipment)
  4.  → UnitStatistics.ProcessSaveState() (m_Statistics)
  5.  → EmotionalStates.ProcessSaveState() (m_EmotionalStates)
  6.  → StrategicDuration.ProcessSaveState() (m_StrategicDuration)
  7.  template    m_ActiveConversation → ConversationTemplate
  8.  enum        m_HealthStatus → LeaderHealthStatus (byte, but read as int32 enum)
  9.  int[]       m_SortedSkillIndices
```

### ItemContainer (0x180824480)

```
ITEMCONTAINER:
  --- Saving ---
  int32         slotCount (always 11)
  for each slot:
    int32         slotIndex
    int32         itemCount
    for each item:
      string        item.GUID (or empty)

  --- Loading ---
  int32         slotCount
  for each slot:
    int32         slotIndex
    int32         itemCount
    for each item:
      string        itemGUID
      → look up via OwnedItems.GetItemByGuid()
      → place in container
```

### UnitStatistics (0x1805c7330)

```
UNITSTATISTICS:
  1.  int[]       m_Kills (resize to 32)
  2.  int[]       m_Deaths (resize to 32)
  3.  templates   m_KilledEntities → List<EntityTemplate>
  4.  int[]       m_MissionStats
  5.  int[]       m_DamageDealt
  6.  templates   m_KilledLeaders → List<UnitLeaderTemplate>
  7.  templates   m_AssistedKills → List<UnitLeaderTemplate>
  8.  template    m_Nemesis → UnitLeaderTemplate
  9.  dict        m_FoeEncounters → Dictionary<UnitLeaderTemplate, int>
  10. uint[]      m_AbilityUsages
```

### EmotionalStates (0x1805b4fd0)

```
EMOTIONALSTATES:
  1.  objects     m_States → List<EmotionalState>
  2.  int32       m_LastTriggeredIndex
  3.  int32       m_LastSkillTriggeredIndex (if version >= 101, else -1)
```

### EmotionalState (0x1805b4530)

```
EMOTIONALSTATE:
  1.  template    m_Template → EmotionalStateTemplate
  2.  enum        m_Trigger → EmotionalTrigger (int32)
  3.  template    m_CausedBy → UnitLeaderTemplate
  4.  int32       m_DurationRemaining
  5.  bool        m_IsActive
```

### StrategicDuration (0x1805acf10)

```
STRATEGICDURATION:
  1.  int32       m_RemainingMissions
  2.  int32       m_TotalMissions
```

---

## 7. BattlePlan (0x180566c10)

**Note:** Only entityType=0 (Infantry/SquadLeader) is supported. Type 1+ throws NotImplementedException.

```
BATTLEPLAN:
  int32         count
  for each deployed entity:
    int32         entityType (MUST be 0!)
    template      UnitLeaderTemplate
    int32         gridX
    int32         gridY
```

---

## 8. PlanetManager (0x18059f2f0)

```
PLANETMANAGER:
  1.  → PseudoRandom.ProcessSaveState() (m_Random)
  2.  objects     m_Planets → List<Planet>
```

### Planet (0x1805a0aa0)

```
PLANET:
  1.  template    m_Template → PlanetTemplate
  2.  int32       m_Control (0-100)
  3.  int32       m_ControlChange
```

### PseudoRandom (0x18053d690)

```
PSEUDORANDOM:
  1.  uint32      state0
  2.  uint32      state1
  3.  uint32      state2
  4.  uint32      state3
```

---

## 9. OperationsManager (0x18059ac60)

```
OPERATIONSMANAGER:
  1.  templates   m_NotAvailableOperations → List<OperationTemplate>
  2.  int32       m_Seed
  3.  → Operation.ProcessSaveState() (m_CurrentOperation, nullable)
  4.  objects     m_AvailableOperations → List<Operation>
  5.  enums       m_OperationStatuses → List<OperationStatus> (int32 each)
```

### Operation (0x18058f9e0)

```
OPERATION:
  1.  template    m_Template → OperationTemplate
  2.  template    m_StoryFaction → StoryFactionTemplate
  3.  template    m_Faction → FactionTemplate
  4.  → OperationResult.ProcessSaveState() (m_OperationResult)
  5.  template    m_Planet → PlanetTemplate
  6.  template    m_Duration → OperationDurationTemplate
  7.  int32       m_IntelligenceLevel
  8.  int32       m_AvailableDrops
  9.  int32       m_CurrentMission
  10. int[]       m_MissionLayers
  11. int32       m_Deployments
  12. → PseudoRandom.ProcessSaveState() (m_Random)
  13. int32       m_TotalEnemyValueGained
  14. templates   m_MissionAssets → List<OperationAssetTemplate>
  15. templates   m_OperationAssets → List<OperationAssetTemplate>
  16. bool        m_IsPressured (if version > 28)
  17. objects     m_Missions → List<Mission>
```

### OperationResult (0x180597400)

```
OPERATIONRESULT:
  1.  template    m_Duration → OperationDurationTemplate
  2.  enum        m_Status → OperationStatus (int32)
  3.  objects     m_MissionResults → List<MissionResult>
```

### Mission (0x1805866b0)

```
MISSION:
  1.  template    m_Template → MissionTemplate
  2.  int32       m_Index
  3.  enum        m_Layer → MissionLayer (int32)
  4.  int32       m_NextMissionsCount
  5.  int32       m_NextMissionsOffset
  6.  template    m_Difficulty → MissionDifficultyTemplate
  7.  template    m_Faction → FactionTemplate
  8.  Vector2     m_MapSize (2 floats)
  9.  Vector2Int  m_MapSizeInt (2 int32s)
  10. enum        m_LightCondition → LightConditionType (int32)
  11. template    m_Weather → WeatherTemplate
  12. template    m_Biome → BiomeTemplate
  13. template    m_Asset → OperationAssetTemplate
  14. → OperationResources.ProcessSaveState() (m_Resources)
  15. enum        m_Status → MissionStatus (int32)
  16. RectInt     m_Bounds (4 int32s)
  17. int[]       m_NextMissions (on save) / init from template (on load)
  18. enums       ObjectiveStates → List<ObjectiveState>
```

### OperationResources (0x180596ce0)

```
OPERATIONRESOURCES (struct):
  1.  int32       value (single int representing resources)
```

---

## 10. MissionResult (0x180584cd0)

```
MISSIONRESULT:
  1.  enum        m_Layer → MissionLayer (int32)
  2.  template    m_Difficulty → MissionDifficultyTemplate
  3.  bool        m_Victory
  4.  int32       m_TurnsPlayed
  5.  int32       m_EnemyValueKilled
  6.  float       m_AlivePercentage
  7.  → OperationResources.ProcessSaveState() (m_ResourcesEarned)
  8.  → OperationResources.ProcessSaveState() (m_ResourcesSpent)
  9.  templates   m_DeadLeaders → List<UnitLeaderTemplate>
  10. bool        m_Abandoned
  11. → MissionResultStats.ProcessSaveState() (m_Stats)
  12. templates   m_Loot → List<BaseItemTemplate>
```

### MissionResultStats (0x180584700)

```
MISSIONRESULTSTATS (struct):
  1.  int32       field0
  2.  float       field1
  3.  float       field2
  4.  float       field3
  5.  (float      obsolete - if version < 23, read and discard)
  6.  int32       field4
  7.  int32       field5
```

---

## 11. ConversationsManager / BaseConversationManager (0x180549280)

```
BASECONVERSATIONMANAGER:
  1.  → PseudoRandom.ProcessSaveState() (m_Random)
  2.  dict        m_RepeatedConversations → Dictionary<ConversationTemplate, int>
  3.  enums       m_TriggeredTypes → List<ConversationTriggerType> (int32)
```

---

## 12. ConversationEffects (0x18054ff20)

```
CONVERSATIONEFFECTS:
  1.  template    m_Template → ConversationEffectsTemplate
  2.  template    m_Conversation → ConversationTemplate
```

---

## 13. EventManager (0x18056d5d0)

```
EVENTMANAGER:
  1.  → BaseConversationManager.ProcessSaveState() (base class)
  2.  bool        m_HasTriggeredEvent
  3.  objects     m_Available → List<ConversationInstance>
  4.  objects     m_Upcoming → List<ConversationInstance>
  5.  objects     m_Failed → List<ConversationInstance>
  6.  templates   m_Triggered → List<ConversationTemplate>
```

### ConversationInstance (0x180550290)

```
CONVERSATIONINSTANCE:
  1.  template    m_Template → ConversationTemplate
  2.  bool        m_IsRepeatable
  3.  enum        m_TriggerType → ConversationTriggerType (int32)
  4.  dict        m_Speakers → Dictionary<int, SpeakerTemplate>:
        int32       count
        for each:
          int32       speakerIndex
          template    SpeakerTemplate
```

---

## 14. BlackMarketBarksManager / BarksManager (0x180564970)

```
BARKSMANAGER:
  1.  → BaseConversationManager.ProcessSaveState() (base class)
  2.  float       m_LastBarkTime
```

---

# PRIMITIVE TYPE FORMATS

## Template Serialization

```
ProcessDataTemplate<T>:
  --- Saving ---
  string        template.GetID() (or empty if null)

  --- Loading ---
  string        id = ReadString()
  if id != "":
    return DataTemplateLoader.Get<T>(id)
  return null
```

## List Serialization

```
ProcessObjects<T>:
  int32         count
  for each:
    → T.ProcessSaveState()

ProcessDataTemplates<T>:
  int32         count
  for each:
    template      T
```

## Array Serialization

```
ProcessIntArray:
  int32         length
  for each:
    int32         value

ProcessFloatArray:
  int32         length
  for each:
    float         value
```

## Enum Serialization

```
ProcessEnum<ByteEnum>:
  byte          value

ProcessEnum<Int32Enum>:
  int32         value
```

---

# VERSION COMPATIBILITY

| Version | Changes |
|---------|---------|
| < 7 | No CheckCorruption marker |
| < 22 | Not supported (rejected on load) |
| < 23 | MissionResultStats has extra float field |
| < 24 | Vehicle health read as int, set to 1.0 |
| < 26 | UnitLeaderAttributes as int[] ÷ 1000 |
| < 27 | No m_PurchasedDossiers in OwnedItems |
| < 28 | No StrategyConfigName in header |
| < 29 | No m_IsPressured in Operation |
| < 101 | EmotionalStates.m_LastSkillTriggeredIndex = -1 |
| 101 | Current version |

Always check `saveState.Version` before parsing version-dependent fields.

---

# C# PARSER EXAMPLE

```csharp
public class MenaceSaveParser {
    private BinaryReader _reader;
    private int _version;

    public SaveData Parse(string path) {
        using var stream = File.OpenRead(path);
        _reader = new BinaryReader(stream, Encoding.UTF8);

        // Header
        _version = _reader.ReadInt32();
        if (_version < 22 || _version > 101)
            throw new Exception($"Unsupported version: {_version}");

        var saveType = _reader.ReadInt32();  // NOT byte!
        var time = new DateTime(_reader.ReadInt64());
        var planet = _reader.ReadString();
        var operation = _reader.ReadString();
        var completed = _reader.ReadInt32();
        var length = _reader.ReadInt32();
        var difficulty = _reader.ReadString();

        string configName = "";
        if (_version > 27) {
            configName = _reader.ReadString();
        }

        var playTime = _reader.ReadDouble();
        var saveName = _reader.ReadString();

        // Body - StrategyState
        var data = new SaveData();
        data.TotalPlayTime = _reader.ReadDouble();
        data.Ironman = _reader.ReadBoolean();
        data.IronmanName = _reader.ReadString();
        data.Seed = _reader.ReadInt32();
        data.HasPickedItemPack = _reader.ReadBoolean();
        data.HasPickedLeaders = _reader.ReadBoolean();
        data.DifficultyTemplate = _reader.ReadString();
        data.Vars = ReadIntArray();

        // CheckCorruption marker
        if (_version >= 7) {
            int marker = _reader.ReadInt32();
            if (marker != 42)
                throw new Exception($"Corruption check failed: {marker} != 42");
        }

        // Nested processors in order
        data.ShipUpgrades = ParseShipUpgrades();
        data.OwnedItems = ParseOwnedItems();
        data.BlackMarket = ParseBlackMarket();
        data.StoryFactions = ParseStoryFactions();
        data.Squaddies = ParseSquaddies();
        data.Roster = ParseRoster();
        data.BattlePlan = ParseBattlePlan();
        data.PlanetManager = ParsePlanetManager();
        data.OperationsManager = ParseOperationsManager();
        data.LastMissionResult = ParseMissionResult();
        data.ConversationIntVars = ParseConversationIntVars();
        data.ConversationsManager = ParseConversationsManager();
        data.ConversationEffects = ParseConversationEffects();
        data.EventManager = ParseEventManager();
        data.BarksManager = ParseBarksManager();
        data.OffmapAbilities = ReadTemplateList();

        return data;
    }

    private int[] ReadIntArray() {
        int count = _reader.ReadInt32();
        var arr = new int[count];
        for (int i = 0; i < count; i++)
            arr[i] = _reader.ReadInt32();
        return arr;
    }

    private string ReadTemplate() => _reader.ReadString();

    private List<string> ReadTemplateList() {
        int count = _reader.ReadInt32();
        var list = new List<string>(count);
        for (int i = 0; i < count; i++)
            list.Add(_reader.ReadString());
        return list;
    }

    private PseudoRandomState ParsePseudoRandom() {
        return new PseudoRandomState {
            State0 = _reader.ReadUInt32(),
            State1 = _reader.ReadUInt32(),
            State2 = _reader.ReadUInt32(),
            State3 = _reader.ReadUInt32()
        };
    }

    // ... implement each Parse* method following the formats above
}
```

---

# EQUIPMENT SLOT INDICES

| Index | Slot |
|-------|------|
| 0 | Primary Weapon |
| 1 | Secondary Weapon |
| 2 | Armor |
| 3 | Accessory 1 |
| 4 | Accessory 2 |
| 5 | Consumable 1 |
| 6 | Consumable 2 |
| 7-10 | Additional |

---

# COMMON EDITS

| Goal | Location | Type |
|------|----------|------|
| Add credits | m_Vars[1] (OciComponents) | int |
| Add intel | m_Vars[13] (Intelligence) | int |
| Add authority | m_Vars[15] (Authority) | int |
| Toggle ironman | m_Ironman in StrategyState | bool |
| Change planet control | Planet.m_Control | int (0-100) |
| Modify unit stats | UnitStatistics arrays | int[] |
| Add/remove items | OwnedItems structure | Complex |
| Change leader health | BaseUnitLeader.m_HealthStatus | enum |
| Modify RNG state | PseudoRandom fields | uint32[4] |

---

# RUNTIME EDITING (RECOMMENDED)

```csharp
[HarmonyPatch(typeof(StrategyState), "ProcessSaveState")]
class SaveStatePatch {
    static void Postfix(SaveState _saveState, StrategyState __instance) {
        if (_saveState.IsLoading()) {
            // Modify after load
            __instance.Roster.GetHiredLeaders().ForEach(leader => {
                // Modify leaders...
            });
        }
    }
}
```
