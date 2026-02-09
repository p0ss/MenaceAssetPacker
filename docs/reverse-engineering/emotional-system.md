# Emotional State System

## Overview

The Emotional State system manages temporary morale and psychological effects on unit leaders. Emotions are triggered by in-game events (kills, injuries, deaths of allies) and apply skill modifiers that affect combat performance. Emotions have duration and can stack, replace, or cancel each other.

## Architecture

```
EmotionalStates (collection per BaseUnitLeader)
├── BaseUnitLeader Owner                         // +0x10 (owning unit)
├── List<EmotionalState> States                  // +0x18 (active emotions)
├── int LastMissionParticipation                 // +0x20 (mission counter, init: -1)
└── int LastOperationParticipation               // +0x24 (operation counter, init: -1)

EmotionalState (single active emotion)
├── EmotionalStateTemplate Template              // +0x10 (emotion definition)
├── EmotionalTrigger Trigger                     // +0x18 (what caused it)
├── UnitLeaderTemplate TargetLeader              // +0x20 (optional target)
├── int RemainingDuration                        // +0x28 (missions remaining)
└── bool IsFirstMission                          // +0x2C (true if just applied)

EmotionalStateTemplate (emotion definition)
├── EmotionalStateType Type                      // +0x78 (emotion type enum)
├── SkillTemplate Skill                          // +0x98 (skill modifier)
├── bool IsNegative                              // +0xCC (negative emotion)
├── bool IsStackable                             // +0xCD (can stack)
├── IntRange Duration                            // +0xC0 (duration range)
└── EmotionalStateResponse Response              // (replacement behavior)
```

## EmotionalStateType Enum

```c
enum EmotionalStateType {
    None = 0,
    Angry = 1,
    Confident = 2,
    Targeted = 3,        // Requires target (e.g., "hates" specific enemy)
    Grief = 4,
    Fear = 5,
    Inspired = 6,
    Vengeful = 7,
    Traumatized = 8
    // ... additional types
}
```

## EmotionalTrigger Enum

```c
enum EmotionalTrigger {
    None = 0,
    KilledEnemy = 1,
    WasWounded = 2,
    AllyKilled = 3,
    AllyWounded = 4,
    MissionSuccess = 5,
    MissionFailure = 6,
    OperationStart = 7,
    OperationEnd = 8,
    // ... additional triggers
}
```

## EmotionalStates Class

### Field Layout

```c
public class EmotionalStates : ISaveStateProcessor, IEnumerable<EmotionalState> {
    // Object header                              // +0x00 - 0x0F
    BaseUnitLeader Owner;                         // +0x10 (owning unit leader)
    List<EmotionalState> States;                  // +0x18 (list of active emotions)
    int LastMissionParticipation;                 // +0x20 (init: -1)
    int LastOperationParticipation;               // +0x24 (init: -1, version >= 101)
}
```

### Key Methods

```c
// Constructor
void EmotionalStates.ctor();  // @ 1805b8f30

// State management
EmotionalState AddEmotionalState(EmotionalStateTemplate template, EmotionalTrigger trigger, UnitLeaderTemplate target, PseudoRandom random);  // @ 1805b4940
void RemoveState(int index);  // @ 1805b5060
bool TryApplyEmotionalState(EmotionalStateTemplate template, EmotionalTrigger trigger, UnitLeaderTemplate target, PseudoRandom random);  // @ 1805b5920
bool TryRemoveRandomEmotionalStates(int count, PseudoRandom random);  // @ 1805b6360

// Queries
int GetStateIdx(EmotionalStateType type);  // @ 1805b4b10
HashSet<EmotionalStateType> GetStateSet();  // @ 1805b4bd0
bool HasState(EmotionalStateType type);  // @ 1805b4d00
bool HasStates(EmotionalStateType[] types);  // @ 1805b4d20

// Iteration
IEnumerator<EmotionalState> GetEnumerator();  // @ 1805b4a90

// Events
void TriggerEmotion(EmotionalTrigger trigger, UnitLeaderTemplate target);  // @ 1805b51b0

// Mission lifecycle
void OnMissionFinished(bool isVictory);  // @ 1805b4da0
void UpdateParticipantOnMissionStart();  // @ 1805b8a10
void UpdateParticipantAfterMission(MissionResult result);  // @ 1805b6630

// Persistence
void ProcessSaveState(SaveState state);  // @ 1805b4fd0
```

### TryApplyEmotionalState Flow

```c
// @ 1805b5920
bool TryApplyEmotionalState(EmotionalStateTemplate newTemplate, EmotionalTrigger trigger, UnitLeaderTemplate target, PseudoRandom random) {
    if (Owner == null) return false;

    EmotionalStateType newType = newTemplate.Type;  // +0x78
    EmotionalStatesConfig config = StrategyConfig.Instance.EmotionalConfig;  // +0x1A8

    // Check for existing emotion of same type
    foreach (EmotionalState existing in States) {  // +0x18
        EmotionalStateTemplate existingTemplate = existing.Template;  // +0x10

        if (existingTemplate.Type != newType) continue;

        // Same type found - handle based on polarity
        if (existingTemplate.IsNegative == newTemplate.IsNegative) {  // +0xCC
            // Same polarity
            if (existingTemplate.IsStackable) {  // +0xCD
                // Stackable: increase duration
                existing.RemainingDuration++;  // +0x28
                ShowNotification("emotion_extended", ...);
                return true;
            } else {
                // Not stackable: replace
                RemoveState(i);
                EmotionalState newState = AddEmotionalState(newTemplate, trigger, target, random);
                ShowNotification("emotion_replaced", ...);
                return true;
            }
        } else {
            // Opposite polarity - they cancel
            if (!existingTemplate.IsStackable) {
                RemoveState(i);
                EmotionalState newState = AddEmotionalState(newTemplate, trigger, target, random);
                ShowNotification("emotion_replaced", ...);
            } else {
                // Reduce stack
                existing.RemainingDuration--;  // +0x28
                if (existing.RemainingDuration < 1) {
                    RemoveState(i);
                }
                ShowNotification("emotion_reduced", ...);
            }
            return true;
        }
    }

    // No existing emotion of this type - add new
    EmotionalState state = AddEmotionalState(newTemplate, trigger, target, random);
    ShowNotification("emotion_added", ...);
    return true;
}
```

### OnMissionFinished Flow

```c
// @ 1805b4da0
void OnMissionFinished(bool isVictory) {
    Roster roster = StrategyState.Instance.Roster;  // +0x70

    // Remove emotions targeting dead leaders
    for (int i = States.Count - 1; i >= 0; i--) {
        EmotionalState state = States[i];
        EmotionalStateTemplate template = state.Template;  // +0x10

        if (template.Type == EmotionalStateType.Targeted) {  // 3
            UnitLeaderTemplate target = state.TargetLeader;  // +0x20
            if (target != null) {
                LeaderStatus status;
                roster.GetLeaderByTemplate(target, out status);
                if (status.IsPermanentlyDead()) {
                    RemoveState(i);
                }
            }
        }
    }

    // Reduce duration of all emotions
    for (int i = States.Count - 1; i >= 0; i--) {
        EmotionalState state = States[i];
        state.IsFirstMission = false;  // +0x2C

        // Check for "permanent until victory" type
        EmotionalStateTemplate template = state.Template;
        if (template.Type == 0x40 && isVictory) {  // Special flag
            continue;  // Don't reduce
        }

        state.RemainingDuration--;  // +0x28
        if (state.RemainingDuration < 1) {
            RemoveState(i);
        }
    }
}
```

## EmotionalState Class

### Field Layout

```c
public class EmotionalState : ISaveStateProcessor {
    // Object header                              // +0x00 - 0x0F
    EmotionalStateTemplate Template;              // +0x10 (emotion definition)
    EmotionalTrigger Trigger;                     // +0x18 (what caused this emotion)
    UnitLeaderTemplate TargetLeader;              // +0x20 (for "targeted" emotions)
    int RemainingDuration;                        // +0x28 (missions until expiry)
    bool IsFirstMission;                          // +0x2C (true = just applied)
}
```

### Key Methods

```c
// Constructor
void EmotionalState.ctor(EmotionalStateTemplate template, EmotionalTrigger trigger, UnitLeaderTemplate target, int duration);  // @ 1805b48d0

// Skill management
void AddSkill(BaseUnitLeader owner);  // @ 1805b3550
void RemoveSkill(BaseUnitLeader owner);  // @ 1805b45f0

// Duration
void ExtendDuration(int amount);  // @ 1805b3d30
void ReduceRemainingDuration();  // @ 1805b45e0

// Display
TooltipData GetTooltipData();  // @ 1805b3d40
string GetTranslatedTooltipTitle(BaseUnitLeader owner);  // @ 1805b4420
string ReplacePlaceholders(string text);  // @ 1805b46c0

// Persistence
void ProcessSaveState(SaveState state);  // @ 1805b4530
```

### AddSkill Flow

```c
// @ 1805b3550
void AddSkill(BaseUnitLeader owner) {
    EmotionalStateTemplate template = Template;  // +0x10
    if (template == null) return;

    SkillTemplate skillTemplate = template.Skill;  // +0x98
    if (skillTemplate == null) {
        // No skill to add - log debug message
        return;
    }

    // Targeted emotions (type 3) don't add skills if no target
    if (template.Type != EmotionalStateType.Targeted) {
        SkillContainer skills = owner.Skills;  // +0x38
        Skill skill = skillTemplate.CreateSkill();
        skills.Add(skill);

        // Warn about deprecated flags
        if (skillTemplate.HasDeprecatedFlag1) {  // +0x117
            Debug.LogWarning("Skill has deprecated flag 1");
        }
        if (skillTemplate.HasDeprecatedFlag2) {  // +0x118
            Debug.LogWarning("Skill has deprecated flag 2");
        }
    }

    // Log debug message
    Debug.Log($"Added emotion skill {skillTemplate.ID} for {template.Type} triggered by {Trigger}");
}
```

## EmotionalStateTemplate Class

### Key Fields

```c
public class EmotionalStateTemplate : DataTemplate {
    // DataTemplate fields...
    EmotionalStateType Type;                      // +0x78 (emotion type)
    Sprite Icon;                                  // +0x80
    SkillTemplate Skill;                          // +0x98 (applied skill modifier)
    IntRange Duration;                            // +0xC0 (random duration range)
    bool IsNegative;                              // +0xCC (negative emotion)
    bool IsStackable;                             // +0xCD (can stack same type)
    EmotionalStateTemplate ReplacementTemplate;   // +0xD0 (optional upgrade)
}
```

### Key Methods

```c
// Constructor
void EmotionalStateTemplate.ctor();  // @ 1805b3380

// Queries
static EmotionalStateTemplate GetByType(EmotionalStateType type);  // @ 1805b2f30
bool NeedsTarget();  // @ 1805b3370 (true for Type == Targeted)
List<LocalizedString> GetLocalizedStrings();  // @ 1805b31f0
```

## Save State Serialization

### EmotionalStates Save Order

```
ProcessSaveState order:
1. States list               @ +0x18 (via ProcessObjects<EmotionalState>)
2. LastMissionParticipation  @ +0x20 (int)
3. LastOperationParticipation @ +0x24 (int, version >= 101)
```

### EmotionalState Save Order

```
ProcessSaveState order:
1. Template                  @ +0x10 (EmotionalStateTemplate)
2. Trigger                   @ +0x18 (EmotionalTrigger enum)
3. TargetLeader              @ +0x20 (UnitLeaderTemplate)
4. RemainingDuration         @ +0x28 (int)
5. IsFirstMission            @ +0x2C (bool)
```

## Trigger Flow

### MissionResult.TryExecuteEmotionalTriggers

```c
// @ 180584e30
void TryExecuteEmotionalTriggers() {
    // After mission completion, trigger emotions based on results
    foreach (BaseUnitLeader leader in participants) {
        EmotionalStates emotions = leader.Emotions;  // +0x58

        // Check for kills
        if (leader.Statistics.Kills > 0) {
            emotions.TriggerEmotion(EmotionalTrigger.KilledEnemy, null);
        }

        // Check for wounds
        if (leader.WasWoundedThisMission) {
            emotions.TriggerEmotion(EmotionalTrigger.WasWounded, null);
        }

        // Check for ally deaths
        foreach (BaseUnitLeader dead in deadAllies) {
            emotions.TriggerEmotion(EmotionalTrigger.AllyKilled, dead.Template);
        }
    }
}
```

## Modding Hooks

### Modify Emotion Application

```csharp
[HarmonyPatch(typeof(EmotionalStates), "TryApplyEmotionalState")]
class EmotionApplicationPatch {
    static bool Prefix(EmotionalStates __instance, EmotionalStateTemplate template, ref bool __result) {
        // Block certain emotions
        if (ShouldBlockEmotion(__instance.Owner, template)) {
            __result = false;
            return false;
        }
        return true;
    }
}
```

### Custom Emotion Triggers

```csharp
[HarmonyPatch(typeof(EmotionalStates), "TriggerEmotion")]
class CustomTriggerPatch {
    static void Postfix(EmotionalStates __instance, EmotionalTrigger trigger) {
        // Add custom emotion on certain triggers
        if (trigger == EmotionalTrigger.KilledEnemy) {
            var customTemplate = GetBonusTemplate();
            __instance.TryApplyEmotionalState(customTemplate, trigger, null, Random);
        }
    }
}
```

### Modify Duration

```csharp
[HarmonyPatch(typeof(EmotionalState), "ReduceRemainingDuration")]
class DurationPatch {
    static void Postfix(EmotionalState __instance) {
        // Extend positive emotions
        if (!__instance.Template.IsNegative && __instance.RemainingDuration == 1) {
            __instance.RemainingDuration = 2;  // Prevent expiry
        }
    }
}
```

### Intercept Mission Finish

```csharp
[HarmonyPatch(typeof(EmotionalStates), "OnMissionFinished")]
class MissionFinishPatch {
    static void Prefix(EmotionalStates __instance, bool isVictory) {
        // Clear all emotions on victory
        if (isVictory) {
            for (int i = __instance.States.Count - 1; i >= 0; i--) {
                if (__instance.States[i].Template.IsNegative) {
                    __instance.RemoveState(i);
                }
            }
        }
    }
}
```

## Key Constants

```c
// EmotionalStateType values
const int TYPE_TARGETED = 3;        // Requires target leader

// Default initialization values
const int INIT_LAST_MISSION = -1;   // 0xFFFFFFFF
const int INIT_LAST_OPERATION = -1; // 0xFFFFFFFF

// Save version threshold
const int VERSION_OPERATION_TRACKING = 101;
```

## Related Classes

- **BaseUnitLeader**: Owner of EmotionalStates (+0x58)
- **EmotionalStateTemplate**: Definition of emotion types
- **SkillTemplate**: Skill applied by emotion
- **EmotionalStatesConfig**: Global emotion configuration
- **EmotionalTriggerExtensions**: Trigger type utilities
- **MissionResult**: Triggers emotions after combat
