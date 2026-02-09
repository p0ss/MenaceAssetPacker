# Mission System

## Overview

The Mission system handles individual tactical missions within an Operation. Missions define the map generation parameters, army composition, objectives, and environmental conditions for tactical combat encounters.

## Architecture

```
Mission
├── MissionTemplate (definition)
├── ObjectiveManager (objectives)
├── Armies (Dictionary<FactionType, Army>)
├── Map Generators (terrain generation)
├── Environmental settings
│   ├── BiomeTemplate
│   ├── WeatherTemplate
│   └── LightConditionType
└── InitialAiUnits (pre-placed units)
```

## Mission Class

### Mission Field Layout

```c
public class Mission {
    // Object header                      // +0x00 - 0x0F
    MissionTemplate Template;             // +0x10 (mission definition)
    int SomeInt;                          // +0x18 (unknown purpose)
    MissionLayer Layer;                   // +0x1C (enum: surface, underground, etc.)
    int MapWidth;                         // +0x20 (map X dimension)
    int Seed;                             // +0x24 (random seed for generation)
    List<BaseMapGenerator> Generators;    // +0x28 (map generation chain)
    // padding                            // +0x30
    MissionDifficultyTemplate Difficulty; // +0x38 (difficulty settings)
    ObjectiveManager Objectives;          // +0x40 (mission objectives)
    Vector2 PoiPosition;                  // +0x48 (point of interest position)
    Vector2Int GridPosition;              // +0x50 (position on operation map)
    LightConditionType LightCondition;    // +0x58 (enum: day, night, etc.)
    WeatherTemplate Weather;              // +0x60 (weather effects)
    BiomeTemplate Biome;                  // +0x68 (terrain biome)
    Graph SelectedGraph;                  // +0x70 (selected MapMagic graph)
    OperationAssetTemplate Asset;         // +0x78 (strategic asset if applicable)
    FactionTemplate EnemyFaction;         // +0x80 (enemy faction type)
    Dictionary<FactionType,Army> Armies;  // +0x88 (armies by faction)
    List<InitialAiUnit> InitialUnits;     // +0x90 (pre-placed AI units)
    // padding                            // +0x98
    // padding                            // +0xA0
    OperationResources Resources;         // +0xA8 (struct, resource rewards)
    List<int> NextMissions;               // +0xB0 (follow-up mission IDs)
    MissionStatus Status;                 // +0xB8 (enum: pending, active, complete, failed)
    // padding                            // +0xBC - 0xCF
    RectInt Bounds;                       // +0xD0 (map bounds, 4 ints)
}
```

### MissionStatus Enum

```c
enum MissionStatus {
    Pending = 0,
    Active = 1,
    Complete = 2,
    Failed = 3
}
```

### MissionLayer Enum

```c
enum MissionLayer {
    Surface = 0,
    Underground = 1,
    Interior = 2,
    Space = 3,
    Random = 4  // Determined at init time
}
```

## Key Mission Methods

```c
// Constructor
void Mission.ctor();                      // @ 180588900

// Initialization
void Init();                              // @ 1805864f0
void AddMapgenTemplatesFromList(List templates);  // @ 180585a30

// Army management
Army GetArmy(FactionType faction);        // @ 180585e90
void SetArmy(FactionType faction, Army army);     // @ 180586c60
void ResetArmies();                       // @ 180586bd0

// Mission flow
void Start();                             // @ 180586dd0
void SetStatus(MissionStatus status);     // @ 180586db0

// Queries
RectInt GetBounds();                      // @ 180585f10
void SetBounds(RectInt bounds);           // @ 180586cf0
int GetEnemyArmyPoints();                 // @ 180586020
float GetEnemyArmyPointsMult();           // @ 180585f20
LightConditionTemplate GetLightConditionTemplate();  // @ 1805861e0
Vector2 GetPoiPosition();                 // @ 180586290
string GetAmbientSound();                 // @ 180585dd0
bool HasArmyWithEnemyFaction();           // @ 1805862d0

// Mission chaining
void AddNextMission(int missionId);       // @ 180585cf0

// Spawning
bool TryCreateInitialAiUnit(...);         // @ 180587d30

// Serialization
void ProcessSaveState(SaveState state);   // @ 1805866b0
```

## Mission Initialization Flow

```
1. Mission.Init() called
   │
   ├── Create PseudoRandom from Seed (+0x24)
   │
   ├── Select MapMagic Graph
   │   └── Random selection from Biome.Graphs[]
   │
   ├── Determine Light Condition
   │   └── If Layer == Random → Use Biome.LightConditionChances
   │
   ├── Select Weather
   │   └── Random weighted selection from Biome.WeatherEntries[]
   │
   ├── Clear ObjectiveManager
   │
   └── MissionTemplate.OnInit(mission, random)
       └── Sets up objectives, armies, spawn points
```

## Mission Start Flow

```
Mission.Start()
│
├── Get TacticalManager.Map
│
├── For each InitialAiUnit:
│   ├── Get tile at unit position
│   │
│   ├── If tile empty and not blocked:
│   │   └── Spawn directly
│   │
│   ├── If tile occupied:
│   │   ├── If unit is Building (Template.Type == 2):
│   │   │   └── Log and find free tile nearby
│   │   └── Otherwise:
│   │       └── Warn and find free tile
│   │
│   └── TacticalManager.TrySpawnUnit(faction, template, tile)
│
├── MissionTemplate.OnMissionStarted()
│
└── ObjectiveManager.OnMissionStarted()
```

## InitialAiUnit Structure

Pre-placed units for mission setup.

```c
struct InitialAiUnit {
    EntityTemplate Template;          // +0x00
    int Faction;                      // +0x08
    Vector2Int Position;              // +0x0C
    // ... additional spawn data
}
```

## Save State Serialization Order

```
ProcessSaveState order:
1. Template                    @ +0x10 (MissionTemplate)
2. SomeInt                     @ +0x18 (int)
3. Layer                       @ +0x1C (MissionLayer enum)
4. MapWidth                    @ +0x20 (int)
5. Seed                        @ +0x24 (int)
6. Difficulty                  @ +0x38 (MissionDifficultyTemplate)
7. EnemyFaction                @ +0x80 (FactionTemplate)
8. PoiPosition                 @ +0x48 (Vector2)
9. GridPosition                @ +0x50 (Vector2Int)
10. LightCondition             @ +0x58 (LightConditionType enum)
11. Weather                    @ +0x60 (WeatherTemplate)
12. Biome                      @ +0x68 (BiomeTemplate)
13. Asset                      @ +0x78 (OperationAssetTemplate)
14. Resources                  @ +0xA8 (OperationResources struct)
15. Status                     @ +0xB8 (MissionStatus enum)
16. Bounds                     @ +0xD0 (RectInt)
17. NextMissions               @ +0xB0 (List<int>, as int array)
18. ObjectiveStates            (from ObjectiveManager)

On Load:
- Call Init() to regenerate graphs, weather, etc.
- Process NextMissions list
- Restore objective states
```

## Army Retrieval

```c
// GetArmy @ 180585e90
Army GetArmy(FactionType faction) {
    Army result;
    if (Armies != null) {  // +0x88
        Armies.TryGetValue(faction, out result);
        return result;
    }
    throw NullReferenceException();
}
```

## MissionTemplate Integration

The MissionTemplate provides:
- Base objectives configuration
- Army generation parameters
- Special spawn rules
- Victory/defeat conditions

```c
// Template virtual calls
Template.OnInit(mission, random);           // +0x208 vtable
Template.OnMissionStarted(mission);         // Called in Mission.Start()
```

## Modding Hooks

### Intercept Mission Start

```csharp
[HarmonyPatch(typeof(Mission), "Start")]
class MissionStartPatch {
    static void Prefix(Mission __instance) {
        var template = __instance.Template;  // +0x10
        Logger.Msg($"Mission starting: {template?.Name}");
    }
}
```

### Modify Army Points

```csharp
[HarmonyPatch(typeof(Mission), "GetEnemyArmyPoints")]
class ArmyPointsPatch {
    static void Postfix(ref int __result) {
        // Double enemy army points
        __result *= 2;
    }
}
```

### Custom Mission Initialization

```csharp
[HarmonyPatch(typeof(Mission), "Init")]
class MissionInitPatch {
    static void Postfix(Mission __instance) {
        // Force night missions
        var lightConditionField = AccessTools.Field(typeof(Mission), "_lightCondition");
        lightConditionField.SetValue(__instance, LightConditionType.Night);
    }
}
```

### Add Custom Initial Units

```csharp
[HarmonyPatch(typeof(Mission), "Start")]
class AddUnitsOnStartPatch {
    static void Prefix(Mission __instance) {
        // Access InitialUnits list at +0x90
        // Add custom units before spawn loop
    }
}
```

## Key Constants

```c
// Mission status values
const int MISSION_PENDING = 0;
const int MISSION_ACTIVE = 1;
const int MISSION_COMPLETE = 2;
const int MISSION_FAILED = 3;

// Mission layers
const int LAYER_SURFACE = 0;
const int LAYER_UNDERGROUND = 1;
const int LAYER_INTERIOR = 2;
const int LAYER_SPACE = 3;
const int LAYER_RANDOM = 4;

// Building template type (for spawn logic)
const int TEMPLATE_TYPE_BUILDING = 2;
```

## Related Classes

- **MissionTemplate**: Definition of mission parameters and objectives
- **ObjectiveManager**: Manages mission objectives and completion tracking
- **Army**: Generated enemy/allied forces for the mission
- **BiomeTemplate**: Terrain generation and environmental settings
- **OperationResources**: Resource rewards on mission completion
