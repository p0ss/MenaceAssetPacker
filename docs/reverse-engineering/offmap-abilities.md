# Offmap Abilities System

## Overview

The Offmap Abilities system manages delayed tactical abilities like airstrikes, artillery barrages, and supply drops. These abilities are scheduled with a delay (in rounds) and execute automatically when the timer expires. The system integrates with the turn/round system and provides visual feedback during the countdown.

## Architecture

```
OffmapAbilityTemplate (ability definition)
├── DataTemplate base                            // +0x00 - 0x77
├── SkillTemplate Skill                          // +0x78 (skill to execute)
├── int DelayInRounds                            // +0x80 (base delay)
└── SoundID UseSound                             // +0x84 (activation sound)

DelayedOffmapAbilities (manager, per TacticalManager)
├── List<DelayedOffmapAbility> Scheduled         // +0x10 (scheduled abilities)
├── int GlobalDelayMod                           // +0x18 (modifier to all delays)
└── Event subscriptions (OnRoundStart, OnTurnEnd)

DelayedOffmapAbility (runtime scheduled ability)
├── OffmapAbilityTemplate Template               // +0x10
├── Skill Skill                                  // +0x18 (skill instance)
├── Tile TargetTile                              // +0x20
├── List<Tile> AffectedTiles                     // +0x28 (cached affected area)
├── DelayedAbilityHUD HUD                        // +0x30 (UI countdown element)
├── int RoundScheduled                           // +0x38 (round when scheduled)
└── float TurnProgressScheduled                  // +0x3C (turn % when scheduled)
```

## OffmapAbilityTemplate Class

### Field Layout

```c
public class OffmapAbilityTemplate : DataTemplate {
    // DataTemplate fields                       // +0x00 - 0x77
    SkillTemplate Skill;                         // +0x78 (skill to execute)
    int DelayInRounds;                           // +0x80 (rounds until execution)
    SoundID UseSound;                            // +0x84 (sound on use)
    // SkillTemplate.EventHandlers at +0x2C0 used for usability checks
}
```

### Key Methods

```c
// Tooltip display
void AppendTooltipData(TooltipData data);  // @ 18074b280

// Check if ability can be used
bool IsUsable();  // @ 18074b5f0

// Localization
List<LocalizedString> GetLocalizedStrings();  // @ 18074b3f0
TooltipData GetSimpleTooltipData();  // @ 18074b430
```

### IsUsable Flow

```c
// @ 18074b5f0
bool IsUsable() {
    SkillTemplate skill = this.Skill;  // +0x78
    if (skill == null) return false;

    // Check each event handler for usability
    List<SkillEventHandlerTemplate> handlers = skill.EventHandlers;  // +0x2C0

    foreach (SkillEventHandlerTemplate handler in handlers) {
        // Call virtual IsUsable on handler
        if (!handler.IsUsable()) {  // vtable +0x1D8
            return false;
        }
    }

    return true;
}
```

## DelayedOffmapAbilities Class

Manager for all scheduled offmap abilities.

### Field Layout

```c
public class DelayedOffmapAbilities {
    // Object header                              // +0x00 - 0x0F
    List<DelayedOffmapAbility> Scheduled;         // +0x10 (pending abilities)
    int GlobalDelayMod;                           // +0x18 (added to all delays)
}
```

### Key Methods

```c
// Constructor
void DelayedOffmapAbilities.ctor(TacticalManager manager);  // @ 18073e0a0

// Scheduling
void Schedule(OffmapAbilityTemplate template, Skill skill, Tile target);  // @ 18073dc50
int GetDelayInRounds(OffmapAbilityTemplate template);  // @ 18073db10

// Turn/Round events
void OnRoundStart();  // @ 18073db40
void OnTurnEnd(float turnProgress, int currentRound);  // @ 18073dc40

// Management
void UpdateAbilities();  // @ 18073deb0
void Dispose();  // @ 18073d9d0

// Modifiers
void AddGlobalDelayMod(int mod);  // @ 18073d870

// Display
void AppendTileTooltip(Tile tile, TooltipData data);  // @ 18073d880
```

### Constructor Flow

```c
// @ 18073e0a0
void DelayedOffmapAbilities.ctor(TacticalManager manager) {
    this.Scheduled = new List<DelayedOffmapAbility>();  // +0x10

    // Subscribe to turn/round events
    manager.OnTurnEnd += this.OnTurnEnd;
    manager.OnRoundStart += this.OnRoundStart;
}
```

### Schedule Flow

```c
// @ 18073dc50
void Schedule(OffmapAbilityTemplate template, Skill skill, Tile target) {
    TacticalManager tm = TacticalManager.Instance;

    // Get current turn progress (0.0 - 1.0)
    float turnProgress = tm.GetUnitsFinishedTurnPct();

    // Create delayed ability instance
    DelayedOffmapAbility delayed = new DelayedOffmapAbility();
    delayed.Template = template;               // +0x10
    delayed.Skill = skill;                     // +0x18
    delayed.TargetTile = target;               // +0x20
    delayed.RoundScheduled = 0;                // +0x38 (starts at 0)
    delayed.TurnProgressScheduled = turnProgress;  // +0x3C

    // Cache affected tiles
    delayed.AffectedTiles = skill.GetAffectedTiles(target, target);  // +0x28

    // Add to list
    this.Scheduled.Add(delayed);  // +0x10

    // Create UI element
    TacticalState state = TacticalState.Instance;
    if (state.HUD != null) {
        DelayedAbilityHUD hud = state.HUD.AddAbility(delayed);  // +0xE0
        delayed.HUD = hud;  // +0x30
    }

    // Update tile highlighting
    state.TileHighlighter.UpdateDelayedOffmapAbilities(this.Scheduled);  // +0x98
}
```

### GetDelayInRounds Flow

```c
// @ 18073db10
int GetDelayInRounds(OffmapAbilityTemplate template) {
    // Base delay + global modifier
    int delay = this.GlobalDelayMod + template.DelayInRounds;  // +0x18, +0x80

    // Minimum delay is 0
    if (delay < 1) {
        return 0;
    }
    return delay;
}
```

### OnRoundStart Flow

```c
// @ 18073db40
void OnRoundStart() {
    // Increment round counter for all scheduled abilities
    foreach (DelayedOffmapAbility ability in this.Scheduled) {  // +0x10
        ability.RoundScheduled++;  // +0x38
    }
}
```

## DelayedOffmapAbility Class

Runtime instance of a scheduled ability.

### Field Layout

```c
public class DelayedOffmapAbility {
    // Object header                              // +0x00 - 0x0F
    OffmapAbilityTemplate Template;               // +0x10 (ability definition)
    Skill Skill;                                  // +0x18 (skill to execute)
    Tile TargetTile;                              // +0x20 (target location)
    List<Tile> AffectedTiles;                     // +0x28 (area of effect)
    DelayedAbilityHUD HUD;                        // +0x30 (UI countdown)
    int RoundScheduled;                           // +0x38 (rounds elapsed)
    float TurnProgressScheduled;                  // +0x3C (turn % when scheduled)
}
```

### Key Methods

```c
// Constructor
void DelayedOffmapAbility.ctor(OffmapAbilityTemplate template, Skill skill,
                                Tile target, float turnProgress);  // @ 18073e5c0

// Turn processing
bool OnTurnEnd(float currentTurnProgress, int delayRounds);  // @ 18073e420

// Display
void AppendTileTooltip(TooltipData data);  // @ 18073e2f0
void AppendDebugInfo(StringBuilder sb);  // @ 18073e1d0
Vector3 GetPos();  // @ 18073e3e0
```

### OnTurnEnd Flow

```c
// @ 18073e420
bool OnTurnEnd(float currentTurnProgress, int totalDelayRounds) {
    // Check if ability should trigger
    // Triggers when: roundsElapsed >= totalDelay AND turnProgress >= scheduledProgress

    int roundsElapsed = this.RoundScheduled;  // +0x38
    float scheduledProgress = this.TurnProgressScheduled;  // +0x3C

    if (roundsElapsed < totalDelayRounds) {
        // Not enough rounds passed
        UpdateProgressBar(roundsElapsed, currentTurnProgress, totalDelayRounds, scheduledProgress);
        return false;  // Not triggered
    }

    if (roundsElapsed == totalDelayRounds && currentTurnProgress < scheduledProgress) {
        // Same round but not enough turn progress
        UpdateProgressBar(roundsElapsed, currentTurnProgress, totalDelayRounds, scheduledProgress);
        return false;
    }

    // Time to execute!
    this.Skill.Use(this.TargetTile, SkillUseType.Offmap);  // +0x18, +0x20, type=3

    // Cleanup
    this.AffectedTiles.Clear();  // +0x28
    this.AffectedTiles = null;
    this.HUD = null;  // +0x30
    this.TargetTile = null;  // +0x20

    // Remove from HUD
    TacticalState state = TacticalState.Instance;
    if (state.HUD != null) {
        state.HUD.RemoveAbility(this);  // +0xE0
    }

    return true;  // Triggered
}

void UpdateProgressBar(int rounds, float progress, int totalRounds, float totalProgress) {
    if (this.HUD != null) {
        float pct = (rounds + progress) / (totalRounds + totalProgress);
        this.HUD.SetProgressPct(pct);
    }
}
```

## OffmapAbilityAction Class

State machine action for targeting offmap abilities.

### Field Layout

```c
public class OffmapAbilityAction : BaseAction {
    // BaseAction fields...
    OffmapAbilityTemplate Template;               // +0x10 (selected ability)
    Skill Skill;                                  // +0x18 (skill instance)
    // Static: bool UseDelaySystem                // +0x01 in static field
}
```

### Key Methods

```c
// Constructor
void OffmapAbilityAction.ctor();  // @ 18063bd40

// Input handling
void HandleLeftClickOnTile(Tile tile);  // @ 18063b540
void HandleMouseMoveOnTile(Tile tile);  // @ 18063b790
void HandleRightClick();  // @ 18063b9a0

// Execution
void UseOrSchedule(Tile target);  // @ 18063bab0
void OnTilesSelected(List<Tile> tiles);  // @ 18063ba30
void Cancel();  // @ 18063b460
```

### UseOrSchedule Flow

```c
// @ 18063bab0
void UseOrSchedule(Tile target) {
    TacticalManager tm = TacticalManager.Instance;
    DelayedOffmapAbilities delayed = tm.DelayedOffmapAbilities;  // +0xA0

    // Calculate actual delay
    int delay = delayed.GetDelayInRounds(this.Template);  // +0x10

    // Check if delay system is enabled and delay > 0
    bool useDelay = OffmapAbilityAction.UseDelaySystem;  // static +0x01

    if (!useDelay || delay < 1) {
        // Execute immediately
        this.Skill.Use(target, SkillUseType.Offmap);  // +0x18, type=3
    } else {
        // Schedule for later
        delayed.Schedule(this.Template, this.Skill, target);
    }

    // Notify listeners
    tm.InvokeOnOffmapAbilityUsed(this.Template, target);

    // Play sound
    TacticalState state = TacticalState.Instance;
    state.TileHighlighter.HideSkill();  // +0x98
    state.CancelCurrentAction();

    // Play use sound
    OffmapAbilityTemplate template = this.Template;
    if (template != null && template.UseSound.IsValid()) {  // +0x84
        template.UseSound.Play();
    }
}
```

## TacticalManager Integration

### Relevant Fields

```c
// TacticalManager
DelayedOffmapAbilities DelayedOffmapAbilities;    // +0xA0

// Events
event OnOffmapAbilityUsed;                        // invoked when ability used/scheduled
event OnOffmapAbilityCanceled;                    // invoked when targeting canceled
event OnOffmapAbilityUpdateUsability;             // invoked to refresh UI
```

### Key Methods

```c
void InvokeOnOffmapAbilityUsed(OffmapAbilityTemplate template, Tile target);  // @ 180671ec0
void InvokeOnOffmapAbilityCanceled(OffmapAbilityTemplate template);  // @ 180671dd0
void InvokeOnOffmapAbilityRefreshUsability();  // @ 180671ea0
```

## Timing System

The offmap ability timing uses a combination of rounds and turn progress:

```
Round 0, Turn 50%: Ability scheduled (RoundScheduled=0, TurnProgressScheduled=0.5)
Round 1 starts:    RoundScheduled incremented to 1
Round 1, Turn 50%: If delay=1, ability triggers (1 >= 1 AND 0.5 >= 0.5)

Progress calculation:
  progress = (roundsElapsed + currentTurnProgress) / (delayRounds + scheduledTurnProgress)
```

This ensures abilities trigger at approximately the same point in the round they were scheduled, creating predictable timing.

## Modding Hooks

### Modify Delay

```csharp
[HarmonyPatch(typeof(DelayedOffmapAbilities), "GetDelayInRounds")]
class DelayPatch {
    static void Postfix(ref int __result, OffmapAbilityTemplate template) {
        // Reduce all delays by 1 (min 0)
        __result = Math.Max(0, __result - 1);
    }
}
```

### Intercept Ability Use

```csharp
[HarmonyPatch(typeof(OffmapAbilityAction), "UseOrSchedule")]
class AbilityUsePatch {
    static void Prefix(OffmapAbilityAction __instance, Tile target) {
        Logger.Msg($"Offmap ability used: {__instance.Template.name} at {target.Position}");
    }
}
```

### Skip Delay System

```csharp
[HarmonyPatch(typeof(OffmapAbilityAction), "UseOrSchedule")]
class InstantAbilityPatch {
    static void Prefix(OffmapAbilityAction __instance) {
        // Force immediate execution
        var field = AccessTools.Field(typeof(OffmapAbilityAction), "UseDelaySystem");
        field.SetValue(null, false);
    }
}
```

### Add Custom Ability

```csharp
[HarmonyPatch(typeof(UITactical), "AddOffmapAbility")]
class CustomAbilityPatch {
    static void Postfix(UITactical __instance) {
        // Add custom offmap ability button
        var template = GetCustomAbilityTemplate();
        __instance.AddOffmapAbility(template);
    }
}
```

### Modify Global Delay

```csharp
// Use the existing method
DelayedOffmapAbilities delayed = TacticalManager.Instance.DelayedOffmapAbilities;
delayed.AddGlobalDelayMod(-1);  // Reduce all delays by 1
```

## Key Constants

```c
// Skill use type for offmap abilities
const int SKILL_USE_TYPE_OFFMAP = 3;

// TacticalManager offsets
const int OFFSET_DELAYED_OFFMAP = 0xA0;  // DelayedOffmapAbilities

// TacticalState offsets
const int OFFSET_HUD = 0x20;             // UITacticalHUD
const int OFFSET_TILE_HIGHLIGHTER = 0x98; // TileHighlighter

// UITacticalHUD offsets
const int OFFSET_HUD_ABILITIES = 0xE0;   // Ability HUD container
```

## Related Classes

- **TacticalManager**: Holds DelayedOffmapAbilities at +0xA0, events
- **TacticalState**: Access to HUD and TileHighlighter
- **Skill**: Execute the actual effect
- **SkillTemplate**: Defines the skill behavior
- **TileHighlighter**: Shows affected area during countdown
- **DelayedAbilityHUD**: UI countdown display
- **ChangeOffmapAbilityDelayEffect**: Strategy effect that modifies delays

