# Actor System

## Overview

The Actor system is the core unit representation in Menace's tactical layer. It forms a class hierarchy: Entity (base) -> Actor -> UnitActor/TransientActor, with Structure as a parallel Entity subclass for buildings.

## Architecture

```
Entity (base)
├── Actor (tactical units)
│   ├── UnitActor (player-controlled squaddies)
│   └── TransientActor (AI units, temporary actors)
└── Structure (buildings, walls, destructibles)

Actor
├── EntityProperties (stats container)
├── List<Element> (visual models)
├── SkillContainer (abilities)
├── ItemContainer (equipment)
└── BaseFaction (team membership)
```

## Entity Class

Base class for all tactical objects with positioning, health, and tag systems.

### Entity Field Layout

```c
public class Entity : MonoBehaviour {
    // MonoBehaviour header             // +0x00 - 0x0F
    int EntityID;                       // +0x10 (unique instance ID)
    // padding                          // +0x14
    List<EntitySegment> Segments;       // +0x18 (multi-tile entities)
    List<Element> Elements;             // +0x20 (visual model components)
    int ElementCount;                   // +0x28 (number of elements)
    Vector3 Forward;                    // +0x2C (facing direction vector)
    HashSet<TagTemplate> Tags;          // +0x38 (tag templates)
    HashSet<TagType> TagTypes;          // +0x40 (tag type enum values)
    bool IsAlive;                       // +0x48 (alive flag, init: 1)
    // padding                          // +0x49 - 0x4B
    int FactionIndex;                   // +0x4C (0=Player, 1=Enemy, 2+=Others)
    int CurrentHitpoints;               // +0x50
    // padding                          // +0x54
    int MaxHitpoints;                   // +0x58
    int HealthPerElement;               // +0x5C (HP per model element)
    int TotalHealthPool;                // +0x60 (total HP capacity)
    // padding                          // +0x64
    Entity Container;                   // +0x70 (if inside vehicle/building)
    // ...additional fields
    string Name;                        // +0x88 (display name)
}
```

### Key Entity Methods

```c
// Creation
static Entity Create(Entity entity, EntityTemplate template, Tile tile, int hitpoints);

// Tile access
Tile GetTile();                         // @ 180611a90
bool HasTile();                         // @ 1806126a0

// Health management
float GetHitpointsPct();               // @ 180611720
void UpdateHitpoints();                // @ 180615050

// Tag system
bool HasTag(TagTemplate tag);          // @ 1806125e0
bool HasAnyOfTheseTags(List<TagTemplate> tags);  // @ 180612330

// Faction
bool IsPlayerControlled();             // @ 180612fe0
bool IsAlliedWith(Entity other);       // @ 180612840

// Container management
void ContainEntity(Entity entity);     // @ 18060f8d0
void EjectEntity(Tile exitTile);       // @ 180610c80

// Death
void Die(bool destroyImmediately);     // @ 180610aa0
void OnDeath();                        // @ 180613df0

// Element access
Element GetElement(int index);         // @ 1806115b0
Element GetFirstElementOrNull();       // @ 180611660
```

## Actor Class

Main tactical unit class extending Entity with suppression, morale, movement, and turn management.

### Actor Field Layout

```c
public class Actor : Entity {
    // Entity fields                    // +0x00 - 0x8F

    // Movement & Position
    Tile StartTile;                     // +0xA0 (tile at move start)
    Tile CurrentTile;                   // +0xA8 (current tile reference)
    int Direction;                      // +0xB0 (facing, 0-7 clockwise from N)
    // padding                          // +0xB4

    // Formation data
    FormationData Formation;            // +0xD8 (element formation)

    // Morale state
    int MoraleState;                    // +0xD4 (1=Panicked, 2=Shaken, 3=Steady)
    int[] DirectionChangeCounts;        // +0xD8 (int[9] array)

    // UI References
    UnitHUD HUD;                        // +0xE0 (health/suppression bars)
    WorldSpaceIcon DetectionIcon;       // +0xE8 (detection indicator)

    // Aim timing
    float LastAimTime;                  // +0xF8 (cooldown for aim sounds)

    // Tile tracking sets
    HashSet<Tile> MovementTilesVisited; // +0x100 (tiles crossed this move)
    HashSet<Tile> MovementTilesTarget;  // +0x108 (target path tiles)
    HashSet<Tile> VisibleTiles;         // +0x110 (currently visible tiles)

    // Combat tracking
    int TimesAttackedSinceLastTurn;     // +0x140
    int APThreshold;                    // +0x144 (movement-related)
    int CurrentAP;                      // +0x148 (current action points)
    int APAtTurnStart;                  // +0x14C (AP at turn beginning)
    int TilesMovedThisTurn;             // +0x154 (movement counter)
    int CombatCounter;                  // +0x158

    // Suppression & Morale
    float Suppression;                  // +0x15C (0.0 - max)
    float Morale;                       // +0x160 (0.0 - max)

    // Flags (packed booleans)
    bool IsTurnDone;                    // +0x164
    // byte padding                     // +0x165
    // byte padding                     // +0x166
    bool IsMoving;                      // +0x167
    bool MovementTrackingFlag;          // +0x168
    bool IsVisibleToPlayerDuringMove;   // +0x169
    bool MoraleImmune;                  // +0x16A
    // byte padding                     // +0x16B
    bool IsStunned;                     // +0x16C
    // byte padding                     // +0x16D
    bool MovementModeFlag;              // +0x16E
    bool IsHeavyWeaponDeployed;         // +0x16F
    // byte padding                     // +0x170
    bool HasActed;                      // +0x171

    // Movement mode
    int MovementMode;                   // +0x174 (0=normal, 1=walk, 2=teleport)

    // Movement path
    List<Tile> ClippedPath;             // +0x180 (path to first enemy sighted)

    // Skill references
    IMovementSkill ActiveMovementSkill; // +0x198

    // Actor state
    int ActorState;                     // +0x1A0 (see ActorState enum)
    int SomeCounter;                    // +0x1A4

    // Visibility
    int VisibilityState;                // +0x1A8 (visibility enum)
    bool Revealed;                      // +0x1AC

    // Minion tracking
    int MinionId;                       // +0x1B0 (-1 if not minion)

    // Unit leader reference
    BaseUnitLeader UnitLeader;          // +0x1B8 (for UnitActors)

    // Entity entering
    Entity EnteringEntity;              // +0x17C
}
```

### ActorState Enum

```c
enum ActorState {
    Dead = 0,
    Available = 1,      // Can act
    Shaken = 2,         // Morale debuff
    Ready = 3,          // Has AP
    TurnDone = 4,       // Finished turn
    Panicked = 5,       // Morale broken
    BleedingOut = 6,    // Incapacitated
    PermanentlyDead = 7,
    Unknown8 = 8
}
```

### MoraleState Enum

```c
enum MoraleState {
    Panicked = 1,   // Morale <= 0, faction switch possible
    Shaken = 2,     // Low morale
    Steady = 3      // Normal state
}
```

### SuppressionState Enum

```c
enum SuppressionState {
    None = 0,
    Suppressed = 1,
    Pinned = 2
}
```

### Key Actor Methods

```c
// Creation
static Actor Create(EntityTemplate template, Tile tile, ...);  // @ 1805de5a0

// Movement
bool MoveTo(Tile destination, MovementFlags flags, MovementAction action);  // @ 1805e0a60
IEnumerable<Tile> CalculateTilesInMovementRange();  // @ 1805de100
int GetTilesMovedThisTurn();           // @ 1805df7c0
bool IsMoving();                       // @ 1805e0810

// Combat
void AimAt(Vector3 target, bool playSound, Entity targetEntity);  // @ 1805db6b0
int GetTimesAttackedSinceLastTurn();   // @ 1805df7d0
void IncrementAttackedThisRound();     // @ 1805dfe00

// Suppression
void ApplySuppression(float amount, bool isFriendlyFire, ...);  // @ 1805ddda0
void SetSuppression(float value);      // @ 1805e76d0
float GetSuppressionPct();             // @ 1805df710
SuppressionState GetSuppressionState(float modifier);  // @ 1805df730

// Morale
void ApplyMorale(MoraleEventType type, float amount);  // @ 1805dd240
void SetMorale(float value);           // @ 1805e6d90
float GetMoralePct();                  // @ 1805df4a0
MoraleState GetMoraleState();          // @ 1805df4d0
float GetMoraleMax();                  // @ 1805df330

// Turn management
void SetTurnDone(bool done);           // @ 1805e7c60
bool IsTurnDone();                     // @ 1805e08a0
bool HasActed();                       // @ 1805df9f0
void MarkAsHasActed();                 // @ 1805e0950
void SkipTurn();                       // @ 1805e8140
int GetActionPointsAtTurnStart();      // @ 1805df0c0

// State queries
bool IsActive();                       // @ 1805e0110
bool IsInfantry();                     // @ 1805e0780
bool IsVehicle();                      // @ 1805e0900
bool IsTurret();                       // @ 1805e08b0
bool IsMinion();                       // @ 1805e07f0
bool IsStunned();                      // @ 1805e0860
bool IsHeavyWeaponDeployed();          // @ 1805e0190
bool IsDying();                        // @ 1805e0180
bool IsLeavingMap();                   // @ 1805e07e0

// Visibility
bool IsHiddenToPlayer();               // @ 1805e0750
bool IsHiddenToAI();                   // @ 1805e01a0
bool IsHiddenToPlayerAtTile(Tile tile);  // @ 1805e0430
bool Is3DModelVisibleToPlayer();       // @ 1805dfe10
bool HasLineOfSightTo(Tile target);    // @ 1805dfa10
bool IsDetectedByFaction(byte factionIndex);  // @ 1805e0160

// State update
void UpdateActorState();               // @ 1805e8b60
ActorState GetActorState();            // @ 1805df0f0

// Death
void Die(bool immediate);              // @ 1805dee10
```

## UnitActor Class

Player-controlled unit actor with persistent squaddie data.

### UnitActor Field Layout

```c
public class UnitActor : Actor {
    // Actor fields                     // +0x00 - 0x1BF
    int RankIndex;                      // +0x1CC (initialized to -1)
}
```

### Key UnitActor Methods

```c
// Data access
BaseUnitLeader GetBaseProperties();    // @ 180638330
EntityProperties GetCurrentProperties();  // @ 180638350
List<Skill> GetSkills();               // @ 180638430
ItemContainer GetItems();              // @ 1806383e0
EmotionalStates GetEmotionalStates();  // @ 1806383b0
UnitStatistics GetStatistics();        // @ 180638470

// Creation
static UnitActor Create(Squaddie squaddie, Tile tile);  // @ 1806380c0

// Mission events
void OnMissionStarted();               // @ 180638870
void OnMissionFinished();              // @ 180638810
void OnDeath();                        // @ 1806384c0
```

## TransientActor Class

Temporary actors for AI units, spawned enemies, etc.

### Key TransientActor Methods

```c
// Creation
static TransientActor Create(EntityTemplate template, Tile tile);  // @ 1806357d0

// Properties
void SetCurrentProperties(EntityProperties props);  // @ 180635a70
```

## Structure Class

Buildings, walls, and destructible objects.

### Structure Field Layout

```c
public class Structure : Entity {
    // Entity fields                    // +0x00 - 0x8F
    // ...
    List<GameObject> Cables;            // +0xC8 (connected cables)
    int TileWidth;                      // +0x?? (multi-tile width)
    int TileHeight;                     // +0x?? (multi-tile height)
}
```

### Key Structure Methods

```c
// Creation
static Structure Create(EntityTemplate template, Tile tile, int rotation);  // @ 180633ef0

// Properties
bool IsBuilding();                     // @ 180634ff0
bool IsVegetation();                   // @ 1806350a0

// Line of sight
void BlockLineOfSight();               // @ 180633c30
void UnblockLineOfSight();             // @ 180635520
bool IsTileLoSBlockedByThisStructure(Tile tile);  // @ 180635030

// Death
void Die(bool destroyImmediate);       // @ 180634960
void OnDeath();                        // @ 180635310
```

## Element Class

Visual model component attached to Entity. Actors can have multiple Elements (e.g., squad members).

### Element Field Layout

```c
public class Element : Transform {
    // Transform header                 // +0x00 - 0xC7
    int ModelIndex;                     // +0xC8 (initialized to -1)
    bool SomeFlag;                      // +0xCC (initialized to 1)
    // padding                          // +0xCD - 0xCF
    List<SharedMaterialsRenderers> MaterialRenderers;  // +0xD0
    // ...
    bool VisibleToPlayer;               // +0x124 (initialized to 1)
    // ...
    float SomeFloat;                    // +0x12C (initialized to -1.0f)
}
```

### Key Element Methods

```c
// Creation
static Element Create(Entity owner, ElementTemplate template);  // @ 1805f6a20

// Movement
void MoveTo(Tile destination, List<Vector3> path, MovementFlags flags);  // @ 1805fa7c0
bool IsMoving();                       // @ 1805fa7b0

// Aiming
float AimAt(Vector3 target, bool playSound, Entity targetEntity);  // @ 1805f5f80
bool IsAiming();                       // @ 1805fa790

// Visibility
void ChangeVisibilityToPlayer(bool visible);  // @ 1805f6530

// Health
float GetHitpointsPct();               // @ 1805f9210

// Events
void OnHit(DamageInfo damage);         // @ 1805fd880
void OnDeath();                        // @ 1805fb590
void OnDeathComplete();                // @ 1805fb0e0
void OnMovementStarted();              // @ 1805feb00
void OnMovementStep(Tile tile);        // @ 1805feb90

// Container
void OnEnteredContainer(Entity container);  // @ 1805fd560
void OnLeftContainer();                // @ 1805fe7c0

// Effects
void AddDamageToShader(float damage);  // @ 1805f5cf0
void DisableAnimationsAndEffects();    // @ 1805f8660
```

## Suppression System

Suppression reduces unit effectiveness and can pin units.

### Suppression Flow

```
ApplySuppression(amount, isFriendlyFire, ...)
├── Entity.ApplySuppression() - base handling
├── Check template flags for suppression immunity
├── Get suppression multiplier from EntityProperties
├── Apply discipline reduction: mult -= Discipline * 0.01
├── Calculate final: current + (amount * mult)
├── Clamp to [0, MaxSuppression]
├── Update HUD display
└── Invoke OnSuppressionApplied event
```

### Suppression Thresholds

From constants in memory:
- Suppressed threshold: ~0.33 (33%)
- Pinned threshold: ~0.66 (66%)
- Max suppression: 100.0

## Morale System

Morale affects unit behavior and can cause panic or faction defection.

### Morale Flow

```
ApplyMorale(eventType, amount)
├── Check if alive and not morale-immune
├── Check template morale immunity flag
├── Check if event type affects this unit (bitmask)
├── Apply morale multiplier from EntityProperties
├── Notify SkillContainer.OnMoraleEvent()
├── Clamp to [0, MoraleMax]
├── Update morale state
│   ├── Morale <= 0 → Panicked (may switch faction)
│   ├── Morale <= threshold → Shaken
│   └── Otherwise → Steady
├── Handle faction switching for panic
├── Spread panic to nearby allies (if panicked)
└── Update actor state
```

### Morale State Thresholds

- Panicked: Morale <= 0 (or equal to 0 with special conditions)
- Shaken: Morale/MoraleMax <= ~0.33
- Steady: Above shaken threshold

## Movement System

### Movement Flow

```
MoveTo(destination, flags, action)
├── Get path via MovementSystem.GetPath()
├── Check path validity
├── Handle "stop on enemy sighted" setting
│   └── ClipPathToFirstEnemySighted()
├── Calculate movement cost
├── Check AP availability
├── Apply formation to elements
├── Store start/current tiles
├── Update tile tracking sets
├── Mark actor as moving
├── Set movement speed/timescale
├── Start element movement animations
├── Deduct AP cost
└── Invoke OnMovement event
```

### Movement Flags

```c
[Flags]
enum MovementFlags {
    None = 0,
    EnterVehicle = 1,     // Entering a vehicle
    ExitVehicle = 2,      // Exiting a vehicle
    Backward = 4,         // Moving backward
    Instant = 8,          // Teleport (no animation)
    Sprint = 16,          // Sprint movement mode
}
```

## TacticalManager Integration

Actors are managed through the TacticalManager singleton:

```c
// Access current active actor
Actor activeActor = TacticalManager.Instance.ActiveActor;  // +0x50

// Get faction
BaseFaction faction = TacticalManager.Instance.GetFaction(factionIndex);

// Events
TacticalManager.Instance.InvokeOnMovement(...);
TacticalManager.Instance.InvokeOnSuppressionApplied(...);
TacticalManager.Instance.InvokeOnMoraleStateChanged(...);
TacticalManager.Instance.InvokeOnActorStateChanged(...);
TacticalManager.Instance.InvokeOnTurnEnd(...);
```

## Modding Hooks

### Override Movement

```csharp
[HarmonyPatch(typeof(Actor), "MoveTo")]
class MovementPatch {
    static void Prefix(Actor __instance, ref Tile destination, ref MovementFlags flags) {
        // Modify movement parameters
        Logger.Msg($"Actor {__instance.Name} moving to {destination.GetTilePos()}");
    }
}
```

### Modify Suppression

```csharp
[HarmonyPatch(typeof(Actor), "ApplySuppression")]
class SuppressionPatch {
    static void Prefix(Actor __instance, ref float amount) {
        // Reduce all suppression by 50%
        amount *= 0.5f;
    }
}
```

### Custom Morale Events

```csharp
[HarmonyPatch(typeof(Actor), "ApplyMorale")]
class MoralePatch {
    static void Postfix(Actor __instance, uint eventType, float amount) {
        Logger.Msg($"Morale event {eventType} applied to {__instance.Name}: {amount}");
    }
}
```

### Intercept Actor State Changes

```csharp
[HarmonyPatch(typeof(Actor), "UpdateActorState")]
class StatePatch {
    static void Postfix(Actor __instance) {
        var state = __instance.GetActorState();
        Logger.Msg($"Actor {__instance.Name} state: {state}");
    }
}
```

### Modify Death Behavior

```csharp
[HarmonyPatch(typeof(Actor), "Die")]
class DeathPatch {
    static bool Prefix(Actor __instance, bool immediate) {
        // Prevent death for testing
        if (__instance.IsPlayerControlled()) {
            __instance.SetSuppression(0);
            __instance.SetMorale(__instance.GetMoraleMax());
            return false; // Skip original
        }
        return true;
    }
}
```

## Key Constants

```c
// Faction indices
const int FACTION_PLAYER = 0;
const int FACTION_ENEMY = 1;
const int FACTION_NEUTRAL = 2;

// Direction system (same as tiles)
const int DIR_NORTH = 0;
const int DIR_NORTHEAST = 1;
const int DIR_EAST = 2;
const int DIR_SOUTHEAST = 3;
const int DIR_SOUTH = 4;
const int DIR_SOUTHWEST = 5;
const int DIR_WEST = 6;
const int DIR_NORTHWEST = 7;

// Default values
const int DEFAULT_MINION_ID = -1;
const int DEFAULT_RANK_INDEX = -1;
```
