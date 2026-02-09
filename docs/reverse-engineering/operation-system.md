# Operation System

## Overview

The Operation system manages campaign-level strategic gameplay, containing multiple missions, tracking overall progress, managing faction relationships, and coordinating strategic assets. Operations are the primary campaign progression unit.

## Architecture

```
Operation
├── OperationTemplate (definition)
├── List<Mission> (missions in operation)
├── OperationResult (outcome tracking)
├── OperationProperties (calculated stats)
├── Faction relationships
│   ├── StoryFactionTemplate (enemy)
│   └── FactionTemplate (friendly)
├── Strategic assets
│   ├── ActiveAssets
│   └── DeactivatedAssets
├── PseudoRandom (seeded random)
└── PlanetTemplate (location)
```

## Operation Class

### Operation Field Layout

```c
public class Operation {
    // Object header                          // +0x00 - 0x0F
    OperationTemplate Template;               // +0x10 (operation definition)
    StoryFactionTemplate EnemyFaction;        // +0x18 (enemy story faction)
    FactionTemplate FriendlyFaction;          // +0x20 (allied faction)
    OperationResult Result;                   // +0x28 (outcome tracking object)
    PlanetTemplate Planet;                    // +0x30 (planet location)
    OperationDurationTemplate Duration;       // +0x38 (time limits and pacing)
    int CurrentMissionIndex;                  // +0x40 (index into Missions, -1 = none)
    List<int> MissionIndices;                 // +0x48 (mission ordering/tracking)
    List<Mission> Missions;                   // +0x50 (all missions in operation)
    int TimeSpent;                            // +0x58 (time units elapsed)
    int TimeLimit;                            // +0x5C (max time allowed)
    int MissionCount;                         // +0x60 (total missions generated)
    PseudoRandom Random;                      // +0x68 (seeded random for operation)
    List<OperationAssetTemplate> ActiveAssets;     // +0x70 (currently active strategic assets)
    List<OperationAssetTemplate> DeactivatedAssets; // +0x78 (used/expired assets)
    OperationProperties Properties;           // +0x80 (calculated operation stats)
    // padding                                // +0x88 - 0x9B
    int IntroConversationId;                  // +0x9C (conversation tracking)
    bool HasCompletedOnce;                    // +0xA0 (version >= 28, completion flag)
}
```

## Key Operation Methods

```c
// Constructor
void Operation.ctor();                        // @ 180590220

// Operation lifecycle
void StartOperation(EventContext context);   // @ 18058fce0
void EndOperation();                          // @ 18058c6d0
void AdvanceTime(int amount);                 // @ 18058a810

// Mission management
void GenerateMissions(EventContext context); // @ 18058d090
Mission GenerateMission(...);                 // @ 18058cd80
Mission DrawRandomMission(...);               // @ 18058b1c0
Mission DrawStoryMissionOnLayer(...);         // @ 18058b410
void EndMission(MissionResult result);        // @ 18058b730
void OnMissionStarted(Mission mission);       // @ 18058f090
void OnMissionResultClosed();                 // @ 18058ee60

// Mission queries
Mission GetCurrentMission();                  // @ 18058e270
Mission GetPreviousMission();                 // @ 18058e530
void SetCurrentMission(Mission mission);      // @ 18058fca0
void ForEachPrevMission(Action callback);     // @ 18058cbe0

// Strategic assets
void ActivateOperationAsset(OperationAssetTemplate asset);   // @ 18058a700
void DeactivateOperationAsset(OperationAssetTemplate asset); // @ 18058b100
void ApplyMissionAssetReward(Mission mission);               // @ 18058aaf0
bool HasAnyMissionStrategicAsset();           // @ 18058ecf0

// Properties
void CalculateOperationProperties();          // @ 18058ad30
OperationType GetOperationType();             // @ 18058e410
int GetRemainingTime();                       // @ 18058e840
bool CanTimeOut();                            // @ 18058ae30

// Faction trust
void ChangeFactionTrust(StoryFactionTemplate faction, int amount);  // @ 18058ae40

// Queries
PlanetTemplate GetPlanet();                   // @ 18058e4c0
StoryFactionTemplate GetEnemyStoryFaction();  // @ 18058e340
FactionTemplate GetFriendlyFaction();         // @ 18058e3d0
MissionDifficultyTemplate GetDifficultyTemplate();  // @ 18058e2e0
List<MissionDifficultyTemplate> GetAvailableDifficulties();  // @ 18058e010
Vector2 GetRandomMissionPoiPos();             // @ 18058e5d0
TooltipData GetTooltipData();                 // @ 18058e9b0

// Results
ConversationVariant[] GetResultConversationVariants();  // @ 18058e8c0
EventTemplate GetResultEventTemplate();       // @ 18058e930
bool TryTriggerAfterOperationFinishedEvents();  // @ 180590100

// Persistence
void ProcessSaveState(SaveState state);       // @ 18058f9e0
void AutoSaveIfNeeded();                      // @ 18058acb0
void CreateScreenshot();                      // @ 18058b020
void RecordScreenshot();                      // @ 18058fc20

// Misc
void ResetIntroId();                          // @ 18058fc90
```

## Operation Lifecycle

### StartOperation Flow

```
StartOperation(context)
│
├── Set as current in OperationsManager
│
├── Process OperationDurationTemplate.OnStartEffects[]
│   └── Apply strategy variable changes
│
├── Apply initial faction trust changes
│   ├── EnemyFaction trust from Duration.EnemyTrustOnStart
│   └── FriendlyFaction trust from Duration.FriendlyTrustOnStart
│
├── GenerateMissions(context)
│   └── Create mission pool based on template
│
├── ForEachActiveGameEffect (type=0: OnOperationStart)
│
├── ForEachActiveGameEffect (type=1: ApplyToOperation)
│
├── ForEachActiveGameEffect (type=4: Custom)
│
├── CalculateOperationProperties()
│
└── EventManager.GenerateMissionSelectEvents()
```

### EndOperation Flow

```
EndOperation()
│
├── Get OperationResult star rating
│
├── Process OperationDurationTemplate.OnEndEffects[]
│   └── Apply strategy variable changes based on result
│
├── Apply final faction trust changes
│   ├── EnemyFaction trust from Duration.EnemyTrustOnEnd
│   └── FriendlyFaction trust from Duration.FriendlyTrustOnEnd
│
├── ForEachActiveGameEffect (type=4: OnOperationEnd)
│
├── ForEachActiveGameEffect (type=1: RemoveFromOperation)
│
├── Clear ActiveAssets list
├── Clear DeactivatedAssets list
│
├── ForEachActiveGameEffect (type=2: Cleanup)
│
└── CalculateOperationProperties()
```

## Save State Serialization Order

```
ProcessSaveState order:
1. Template                    @ +0x10 (OperationTemplate)
2. EnemyFaction                @ +0x18 (StoryFactionTemplate)
3. FriendlyFaction             @ +0x20 (FactionTemplate)
4. Result                      @ +0x28 (OperationResult object)
5. Planet                      @ +0x30 (PlanetTemplate)
6. Duration                    @ +0x38 (OperationDurationTemplate)
7. TimeSpent                   @ +0x58 (int)
8. TimeLimit                   @ +0x5C (int)
9. CurrentMissionIndex         @ +0x40 (int)
10. MissionIndices             @ +0x48 (List<int>)
11. MissionCount               @ +0x60 (int)
12. Random                     @ +0x68 (PseudoRandom object)
13. IntroConversationId        @ +0x9C (int)
14. ActiveAssets               @ +0x70 (List<OperationAssetTemplate>)
15. DeactivatedAssets          @ +0x78 (List<OperationAssetTemplate>)
16. HasCompletedOnce           @ +0xA0 (bool, version >= 28)
17. Missions                   @ +0x50 (List<Mission> via ProcessObjects)

On Load:
- Create new OperationProperties at +0x80
- Call CalculateOperationProperties()
- Process each Mission's save state
```

## OperationResult Class

Tracks mission outcomes and calculates star rating.

```c
public class OperationResult {
    // Tracks individual mission results
    // Calculates total star rating
    int GetRoundedFilledStars();
    int ResultType;                           // +0x18 (outcome enum)
}
```

## OperationProperties

Calculated statistics for the operation.

```c
public class OperationProperties {
    // Aggregated stats from all missions
    // Enemy army totals
    // Resource projections
    // Difficulty modifiers
}
```

## Faction Trust System

Trust changes occur at operation start and end:

```c
void ChangeFactionTrust(StoryFactionTemplate faction, int amount) {
    // Modifies global faction relationship
    // Affects available operations, rewards, etc.
}

// Trust calculation at start/end
struct OperationTrustChange {
    // Base trust change
    // Modifiers based on result
    // Star rating multipliers
}
```

## Strategic Assets

Operations can have strategic assets that provide bonuses:

```c
void ActivateOperationAsset(OperationAssetTemplate asset) {
    // Add to ActiveAssets list (+0x70)
    // Apply asset effects
}

void DeactivateOperationAsset(OperationAssetTemplate asset) {
    // Move to DeactivatedAssets (+0x78)
    // Remove asset effects
}
```

## Mission Generation

```c
void GenerateMissions(EventContext context) {
    // Uses OperationTemplate to determine:
    // - Number of missions
    // - Mission types
    // - Difficulty scaling
    // - Story mission placement
}

Mission DrawRandomMission(...) {
    // Select random mission type
    // Apply difficulty settings
    // Generate army compositions
}

Mission DrawStoryMissionOnLayer(MissionLayer layer) {
    // Draw from story mission pool
    // Filter by mission layer type
}
```

## Time Management

```c
void AdvanceTime(int amount) {
    TimeSpent += amount;  // +0x58
    // Check for timeout
    // Trigger time-based events
}

int GetRemainingTime() {
    return TimeLimit - TimeSpent;  // +0x5C - +0x58
}

bool CanTimeOut() {
    return TimeLimit > 0;
}
```

## Modding Hooks

### Intercept Operation Start

```csharp
[HarmonyPatch(typeof(Operation), "StartOperation")]
class OperationStartPatch {
    static void Postfix(Operation __instance) {
        var template = __instance.Template;  // +0x10
        Logger.Msg($"Operation started: {template?.Name}");
    }
}
```

### Modify Time Limits

```csharp
[HarmonyPatch(typeof(Operation), "AdvanceTime")]
class TimePatch {
    static void Prefix(Operation __instance, ref int amount) {
        // Halve time passage
        amount /= 2;
    }
}
```

### Custom Mission Generation

```csharp
[HarmonyPatch(typeof(Operation), "GenerateMissions")]
class MissionGenPatch {
    static void Postfix(Operation __instance) {
        var missions = __instance.Missions;  // +0x50
        Logger.Msg($"Generated {missions.Count} missions");
    }
}
```

### Modify Faction Trust

```csharp
[HarmonyPatch(typeof(Operation), "ChangeFactionTrust")]
class TrustPatch {
    static void Prefix(ref int amount) {
        // Double trust gains/losses
        amount *= 2;
    }
}
```

### Custom Strategic Asset Logic

```csharp
[HarmonyPatch(typeof(Operation), "ActivateOperationAsset")]
class AssetPatch {
    static void Postfix(Operation __instance, OperationAssetTemplate asset) {
        Logger.Msg($"Asset activated: {asset?.Name}");
        // Additional asset effects
    }
}
```

## Integration with OperationsManager

```c
// OperationsManager singleton
OperationsManager.SetCurrentOperation(operation);
OperationsManager.GetCurrentOperation();
OperationsManager.EndCurrentOperation();
```

## Key Constants

```c
// Default current mission index (no mission selected)
const int NO_CURRENT_MISSION = -1;

// Save version for HasCompletedOnce field
const int VERSION_HAS_COMPLETED = 28;
```

## Related Classes

- **OperationTemplate**: Defines operation structure, missions, and rewards
- **OperationDurationTemplate**: Time limits, trust changes, pacing
- **OperationsManager**: Singleton managing active operation
- **Mission**: Individual tactical encounters within operation
- **StoryFactionTemplate**: Enemy faction with trust/reputation system
- **OperationAssetTemplate**: Strategic bonuses and unlocks
