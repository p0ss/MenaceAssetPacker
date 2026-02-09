# Turn & Action System

## Overview

The turn and action system manages the tactical gameplay flow through rounds, faction turns, and individual unit actions. It coordinates between TacticalManager (game state), TacticalState (input/UI state machine), and various Action classes.

## Architecture

```
TacticalManager (singleton)
├── Round management
├── Faction turn order
├── Active actor tracking
├── Event dispatching
└── Objective tracking

TacticalState (state machine)
├── CurrentAction (input handler)
├── Skill selection
├── UI coordination
└── Camera control

Actions (input handlers)
├── NoneAction (default selection)
├── SkillAction (skill targeting)
├── TravelPathAction (movement)
├── TravelAndEnterAction (vehicle entry)
└── SelectAoETilesAction (area targeting)
```

## Turn Flow

```
Mission Start
│
├── Round 1 starts (NextRound)
│   ├── OnRoundStart for all actors
│   ├── OnRoundStart for all structures
│   └── Reset faction tracking
│
├── Faction Turn (NextFaction)
│   ├── Select faction by turn order
│   ├── Find first undone actor
│   ├── SetActiveActor
│   │   ├── OnTurnEnd for previous actor
│   │   ├── OnTurnStart for new actor
│   │   └── Invoke OnActiveActorChanged
│   └── Process AI if AI faction
│
├── Actor Turn
│   ├── Player selects skill/movement
│   ├── Execute action
│   ├── Deduct AP
│   └── Mark turn done (or continue)
│
├── End Turn (EndTurn)
│   ├── Check if actor is moving/busy
│   ├── SetActiveActor(null)
│   └── TryCheckRound
│
├── Check Round (CheckRound)
│   ├── IsRoundDone? → NextRound
│   └── NextFaction
│
└── Mission End (when objectives complete)
```

## TacticalManager Class

Singleton managing all tactical game state.

### TacticalManager Field Layout

```c
public class TacticalManager {
    // Object header                    // +0x00 - 0x0F
    TacticalCamera Camera;              // +0x10 (main tactical camera)
    Volume VolumeProfile;               // +0x18 (post-processing volume)
    VolumeProfile BackupProfile;        // +0x20 (backup for actor overrides)
    Map Map;                            // +0x28 (current tactical map)
    // padding                          // +0x30
    MissionContext MissionContext;      // +0x40 (mission manager)
    // padding                          // +0x48
    Actor ActiveActor;                  // +0x50 (currently selected actor)
    List<Actor> AllActors;              // +0x58 (all actors on map)
    int RoundNumber;                    // +0x60 (current round, 1-indexed)
    // padding                          // +0x64
    // padding                          // +0x68
    // padding                          // +0x70
    List<Structure> AllStructures;      // +0x78 (all structures on map)
    HashSet<Structure> ProcessedStructures;  // +0x80 (structures processed this round)
    int StructureProcessBatchSize;      // +0x88 (batch size for structure updates)
    // padding                          // +0x8C
    // padding                          // +0x90
    // padding                          // +0x98
    // padding                          // +0xA0
    List<BaseFaction> Factions;         // +0xA8 (all factions in mission)
    int CurrentFactionIndex;            // +0xB0 (current faction's index)
    int PreviousFactionIndex;           // +0xB4 (previous faction's index)
    // padding                          // +0xB8
    // padding                          // +0xC0
    // padding                          // +0xC8
    // padding                          // +0xD0
    bool RoundCheckPending;             // +0xD8 (flag for deferred round check)
    // ...events at various offsets
    Action<Actor> OnActiveActorChanged; // +0x120
    Action<Actor> OnTurnStart;          // +0x128
    Action<int> OnRoundStart;           // +0x138
    Action OnPlayerTurn;                // +0x140
    Action<int> OnAITurn;               // +0x148
}
```

### Key TacticalManager Methods

```c
// Singleton access
static TacticalManager Instance;        // TypeInfo+0xB8

// Round management
void CheckRound();                      // @ 18066eb80
void NextRound();                       // @ 1806736b0
void NextFaction();                     // @ 1806730f0
void TryCheckRound();                   // @ 180675510
bool IsRoundDone();                     // @ 180672cb0

// Actor management
void SetActiveActor(Actor actor, bool invokeEvents);  // @ 180674d30
void MarkActorMoving(Actor actor);      // @ 180673090
void UnmarkActorMoving(Actor actor);    // @ 180675b60
bool IsAnyActorMoving();                // @ 180672910
void UpdateAllActorStates();            // @ 180675d30

// Faction queries
BaseFaction GetFaction(int index);      // @ 18066f6a0
BaseFaction GetPlayerFaction();         // @ 18066f6e0
BaseFaction GetActiveFaction();         // @ 18066f2c0
int GetActiveFactionID();               // @ 18066f240
bool IsPlayerTurn();                    // @ 180672ca0

// Mission state
bool IsMissionRunning();                // @ 180672c20
bool IsFinished();                      // @ 180672c10
bool IsPaused();                        // @ 180672c90
void SetPaused(bool paused);            // @ 1806753c0
void Finish();                          // @ 18066ecc0

// Actor queries
int GetActorCount(int factionIndex);    // @ 18066f360
int GetDeadCount(int factionIndex);     // @ 18066f500
int GetDeadEnemyCount();                // @ 18066f560
int GetTotalEnemyCount();               // @ 18066f8f0
bool IsAnyPlayerUnitAlive();            // @ 180672ba0
bool IsAnyAIUnitAlive();                // @ 180672880

// Events
void InvokeOnTurnEnd(Actor actor);      // @ 1806726d0
void InvokeOnMovement(...);             // @ 180671990
void InvokeOnMovementFinished(...);     // @ 1806717c0
void InvokeOnSkillUse(...);             // @ 180672290
void InvokeOnAfterSkillUse(...);        // @ 1806703c0
void InvokeOnDamageReceived(...);       // @ 180670aa0
void InvokeOnDeath(...);                // @ 180670bc0
void InvokeOnActorStateChanged(...);    // @ 1806702b0
void InvokeOnMoraleStateChanged(...);   // @ 1806716b0
void InvokeOnSuppressionApplied(...);   // @ 1806725b0

// AI
void ProcessAI();                       // @ 1806742b0
void SetAIPaused(bool paused);          // @ 180674d20
bool IsAIDryrun();                      // @ 180672870
```

## TacticalState Class

State machine handling player input and UI during tactical combat.

### TacticalState Field Layout

```c
public class TacticalState : BaseState {
    // BaseState header                 // +0x00 - 0x0F
    bool IsActive;                      // +0x10 (state is active)
    // padding                          // +0x11 - 0x17
    string Name;                        // +0x18 ("TacticalState")
    UITactical UI;                      // +0x20 (tactical HUD reference)
    float TimeScale;                    // +0x28 (game speed multiplier)
    // padding                          // +0x2C
    // padding                          // +0x30
    BaseAction CurrentAction;           // +0x38 (current input handler)
    // padding                          // +0x40
    // ...
    HoveredTileHighlighter HoverHighlighter;  // +0x78
    TacticalTooltipController Tooltips; // +0x80
    // padding                          // +0x88
    // padding                          // +0x90
    TileHighlighter SkillRangeHighlighter;  // +0x98
    // padding                          // +0xA0
    // ...
    bool SomeFlag;                      // +0xB8 (init: 1)
    // padding                          // +0xB9 - 0xBF
    // ...
    Skill QueuedSkill;                  // +0xC8 (skill queued during movement)
}
```

### TacticalState Singleton Access

```c
// Access via TypeInfo+0xB8
TacticalState state = TacticalState.Instance;
```

### Key TacticalState Methods

```c
// Singleton
static TacticalState Get();             // @ 180645790

// Action management
void SetCurrentAction(BaseAction action);  // @ 18064ac80
void ClearCurrentAction();              // @ 180644a10
void CancelCurrentAction();             // @ 180644990

// Skill selection
bool TrySelectSkill(Skill skill);       // @ 18064b3d0
bool TrySelectSkillInternal(Skill skill);  // @ 18064b110
Skill GetSelectedSkill();               // @ 180645660

// Turn management
void EndTurn();                         // @ 180645200
void OnTurnStart(Actor actor);          // @ 180649390
void OnRoundStart();                    // @ 180649340

// Movement
void ComputeActorPath(Tile destination);   // @ 180644ad0
void CancelActorPath();                 // @ 180644880
void ExecuteActorTravel();              // @ 1806452f0
void SpeedUpCurrentActorMovement();     // @ 18064ae40

// Input handling
void HandleKeyInput();                  // @ 1806457e0
void HandleMouseInput();                // @ 180645b60

// Events
void OnActiveActorChanged(Actor actor); // @ 180646240
void OnMovement(...);                   // @ 180648c10
void OnMovementFinished(...);           // @ 180648b90
void OnAfterSkillUse(...);              // @ 180646630
void OnFinished();                      // @ 1806466b0

// UI
void RefreshSkillRangeOverlay();        // @ 180649d50
void RepositionInCover(Actor actor);    // @ 180649de0
```

## Action Classes

Actions handle specific input modes during tactical gameplay.

### BaseAction (Abstract)

```c
public abstract class BaseAction {
    // Virtual methods
    virtual void Update();
    virtual void HandleLeftClickOnTile(Tile tile);
    virtual void HandleMouseMoveOnTile(Tile tile);
    virtual void HandleRightClick();
    virtual void Cancel();
}
```

### NoneAction

Default action when no skill is selected. Handles actor selection and basic movement.

```c
public class NoneAction : BaseAction {
    // Methods
    void ChangeActiveActor(Actor actor);     // @ 18063a800
    void HandleLeftClickOnTile(Tile tile);   // @ 18063a9d0
    void HandleMouseMoveOnTile(Tile tile);   // @ 18063ad50
    void HandleRightClick();                 // @ 18063b080
    bool IsTileSelectable(Tile tile);        // @ 18063b120
    void Update();                           // @ 18063b340
}
```

### SkillAction

Action for targeting and using skills.

#### SkillAction Field Layout

```c
public class SkillAction : BaseAction {
    // BaseAction header                // +0x00 - 0x0F
    Skill Skill;                        // +0x10 (skill being used)
    SkillTemplate Template;             // +0x18 (skill's template)
}
```

#### Key SkillAction Methods

```c
// Constructor
SkillAction(Skill skill);               // @ 18063e460

// Input handling
void HandleLeftClickOnTile(Tile tile);  // @ 18063db50
void HandleMouseMoveOnTile(Tile tile);  // @ 18063dff0
void HandleRightClick();                // @ 18063e3d0

// Cancellation
void Cancel();                          // @ 18063da40
void CancelWithoutCancelAim();          // @ 18063d8e0

// Targeting
ushort GetTargetUsageParams();          // @ 18063db10
```

### TravelPathAction

Action for movement along a path.

```c
public class TravelPathAction : BaseAction {
    // Movement path handling
}
```

### TravelAndEnterAction

Action for moving to and entering a vehicle.

```c
public class TravelAndEnterAction : BaseAction {
    // Combined movement and vehicle entry
}
```

### SelectAoETilesAction

Action for selecting area-of-effect targets.

```c
public class SelectAoETilesAction : BaseAction {
    // AoE tile selection
}
```

## BaseFaction Class

Manages actors belonging to a faction.

### Key BaseFaction Methods

```c
// Actor management
void AddActor(Actor actor);             // @ 180713040
void RemoveActor(Actor actor);          // @ 180713ad0
int GetActorCount();                    // @ 180713250
int GetAmountOfActorsLeftToAct();       // @ 180713530
float GetDeadActorFractionCount();      // @ 180713650
bool HasActors();                       // @ 180713820

// Events
void OnActorDeath(Actor actor);         // @ 180713a20
void OnTurnStart();                     // +0x1B8 vtable
void OnTurnEnd();                       // +0x1C8 vtable
void OnRoundStart();                    // +0x1C8 vtable
```

## Round vs Turn

- **Round**: One complete cycle where all factions take their turns
- **Turn**: A single faction's opportunity to act with all their units
- **Actor Turn**: Individual unit's action phase within a faction turn

```
Round 1
├── Player Turn (faction 0)
│   ├── Actor A turn → Actor B turn → ... → End Turn
├── Enemy Turn (faction 1)
│   ├── AI processes all enemy actors
└── (other factions if present)

Round 2
├── Player Turn
└── Enemy Turn
...
```

## Faction Turn Order

Turn order is defined in `Config.FactionOrder` (List<int> at Config+0x58):

```c
// Default order: [0, 1] = Player, then Enemy
// Index 0 = Player faction
// Index 1 = Enemy faction
// Index 2+ = Other factions (neutral, etc.)
```

## Skill Execution Flow

```
TrySelectSkill(skill)
│
├── Check if actor is moving → Queue skill
├── Check if current action blocks selection
├── Check if SkillContainer is busy
└── TrySelectSkillInternal
    │
    └── Create SkillAction
        │
        └── HandleLeftClickOnTile(tile)
            │
            ├── Validate skill usability
            ├── Validate affordability (AP/ammo)
            ├── Validate target
            ├── Play aim animation if needed
            ├── If AoE skill → SelectAoETilesAction
            └── Skill.Use(target)
                │
                ├── Deduct costs
                ├── Execute effects
                ├── Update UI
                └── Clear action (or keep for repeat)
```

## Events

TacticalManager dispatches events for game state changes:

```c
// Actor events
OnActiveActorChanged(Actor actor);
OnActorStateChanged(Actor actor, int oldState, int newState);
OnTurnEnd(Actor actor);

// Combat events
OnSkillUse(Skill skill, Tile target);
OnAfterSkillUse(Skill skill);
OnDamageReceived(Entity target, DamageInfo damage);
OnDeath(Entity entity);
OnAttackMissed(Actor attacker, Entity target);

// Movement events
OnMovement(Actor actor, Tile from, Tile to, MovementFlags flags, Entity entering);
OnMovementFinished(Actor actor, Tile destination);

// State events
OnMoraleStateChanged(Actor actor, int newState);
OnSuppressionApplied(Actor actor, float amount, Entity source);
OnHitpointsChanged(Entity entity, int oldHP, int newHP);
OnArmorChanged(Entity entity);

// Round events
OnRoundStart(int roundNumber);
OnPlayerTurn();
OnAITurn(int factionIndex);

// Objective events
OnObjectiveStateChanged(Objective objective);
```

## Modding Hooks

### Intercept Turn End

```csharp
[HarmonyPatch(typeof(TacticalState), "EndTurn")]
class EndTurnPatch {
    static bool Prefix(TacticalState __instance) {
        Logger.Msg("Player ending turn");
        return true; // Allow original
    }
}
```

### Modify Skill Selection

```csharp
[HarmonyPatch(typeof(TacticalState), "TrySelectSkill")]
class SkillSelectPatch {
    static void Prefix(TacticalState __instance, ref Skill skill) {
        Logger.Msg($"Selecting skill: {skill?.GetTemplate()?.Name}");
    }
}
```

### Custom Round Start Logic

```csharp
[HarmonyPatch(typeof(TacticalManager), "NextRound")]
class RoundStartPatch {
    static void Postfix(TacticalManager __instance) {
        int round = __instance.RoundNumber; // +0x60
        Logger.Msg($"Round {round} started");
    }
}
```

### Intercept Active Actor Change

```csharp
[HarmonyPatch(typeof(TacticalManager), "SetActiveActor")]
class ActiveActorPatch {
    static void Postfix(TacticalManager __instance, Actor actor) {
        if (actor != null) {
            Logger.Msg($"Active actor: {actor.Name}");
        }
    }
}
```

### Custom Action

```csharp
public class CustomAction : BaseAction {
    public override void HandleLeftClickOnTile(Tile tile) {
        // Custom tile click handling
    }

    public override void HandleRightClick() {
        // Cancel and return to NoneAction
        TacticalState.Instance.ClearCurrentAction();
    }
}

// To use:
TacticalState.Instance.SetCurrentAction(new CustomAction());
```

## Key Constants

```c
// Faction indices
const int FACTION_PLAYER = 0;
const int FACTION_ENEMY = 1;

// Default turn order
int[] DEFAULT_TURN_ORDER = { 0, 1 };  // Player, then Enemy
```
