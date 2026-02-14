# Emotional State System

## Overview

The Emotional State system manages temporary morale and psychological effects on unit leaders. Emotions are triggered by in-game events (kills, injuries, deaths of allies) and apply skill modifiers that affect combat performance. Emotions have duration and can stack, replace, or cancel each other.

## Architecture

```
EmotionalStates (collection per BaseUnitLeader)
├── BaseUnitLeader Owner                         // +0x10 (owning unit)
├── List<EmotionalState> m_States                // +0x18 (active emotions)
├── int LastMissionParticipation                 // +0x20 (mission counter, init: -1)
└── int LastOperationParticipation               // +0x24 (operation counter, init: -1)

EmotionalState (single active emotion)
├── EmotionalStateTemplate Template              // +0x10 (emotion definition)
├── EmotionalTrigger Trigger                     // +0x18 (what caused it)
├── UnitLeaderTemplate TargetLeader              // +0x20 (optional target)
├── int RemainingDuration                        // +0x28 (missions remaining)
└── bool IsFirstMission                          // +0x2C (true if just applied)

EmotionalStateTemplate (emotion definition)
├── EmotionalStateType StateType                 // +0x78 (emotion type enum)
├── EffectTemplate Effect                        // +0x98 (effect modifier)
├── bool IsPositive                              // +0xCC (positive emotion)
├── bool IsSuperState                            // +0xCD (is super state)
├── IntRange Duration                            // +0xC0 (duration range)
└── EmotionalStateTemplate SuperState            // +0xD0 (super state reference)
```

## EmotionalStateType Enum

```c
enum EmotionalStateType {
    None = 0,
    AnimosityTowards = 1,   // Directed at specific target
    Determined = 2,
    Weary = 3,
    Disheartened = 4,
    Eager = 5,
    Frustrated = 6,
    Exhausted = 7,
    GoodwillTowards = 8,    // Directed at specific target
    Hesitant = 9,
    Overconfident = 10,
    Injured = 11,
    Bruised = 12,
    Euphoric = 13,
    Miserable = 14
}
```

## EmotionalTrigger Enum

```c
enum EmotionalTrigger {
    StabilizedBy = 0,
    StabilizedOthers = 1,
    ReceivedFriendlyFireFrom = 2,
    DeployedXTimesWithOther = 3,
    KilledXEnemyEntities = 4,
    KilledXEnemyMiniBosses = 5,
    DeployedInTheXMissionsBeforeCurrent = 6,
    NotDeployedInTheXMissionsBeforeCurrent = 7,
    KilledXCivElements = 8,
    SuccessOnFavPlanet = 9,
    FailedOnFavPlanet = 10,
    LostOverXPercentHitpoints = 11,
    GameEffect = 12,
    Event = 13,
    Cheat = 14,
    OtherLeaderKilledCivElementOnFavPlanet = 15,
    Fled = 16,
    NearDeathExperience = 17,
    LostAllSquaddies = 18,
    Last = 19
}
```

## EmotionalStates Class

### Field Layout

```c
public class EmotionalStates : ISaveStateProcessor, IEnumerable<EmotionalState> {
    // Object header                              // +0x00 - 0x0F
    BaseUnitLeader Owner;                         // +0x10 (owning unit leader)
    List<EmotionalState> m_States;                // +0x18 (list of active emotions)
    int LastMissionParticipation;                 // +0x20 (init: -1)
    int LastOperationParticipation;               // +0x24 (init: -1, version >= 101)
}
```

### Key Methods

```c
// Constructor
void EmotionalStates.ctor();  // @ 1805b8f30

// State management
EmotionalState AddEmotionalState(EmotionalStateTemplate template, EmotionalTrigger trigger, UnitLeaderTemplate target, PseudoRandom random, int mission);  // @ 1805b4940
void RemoveState(int index);  // @ 1805b5060
bool TryApplyEmotionalState(EmotionalStateTemplate template, EmotionalTrigger trigger, UnitLeaderTemplate target, PseudoRandom random, bool showAsReward);  // @ 1805b5920
bool TryRemoveRandomEmotionalStates(int count, PseudoRandom random);  // @ 1805b6360

// Queries
int GetStateIdx(EmotionalStateType type);  // @ 1805b4b10
HashSet<EmotionalStateType> GetStateSet();  // @ 1805b4bd0
bool HasState(EmotionalStateType type);  // @ 1805b4d00
bool HasStates(EmotionalStateType[] types);  // @ 1805b4d20

// Iteration
IEnumerator<EmotionalState> GetEnumerator();  // @ 1805b4a90

// Events
void TriggerEmotion(EmotionalTrigger trigger, UnitLeaderTemplate target, PseudoRandom random, int mission);  // @ 1805b51b0

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
bool TryApplyEmotionalState(EmotionalStateTemplate newTemplate, EmotionalTrigger trigger, UnitLeaderTemplate target, PseudoRandom random, bool showAsReward) {
    if (Owner == null) return false;

    EmotionalStateType newType = newTemplate.StateType;  // +0x78
    EmotionalStatesConfig config = StrategyConfig.Instance.EmotionalConfig;  // +0x1A8

    // Check for existing emotion of same type
    foreach (EmotionalState existing in m_States) {  // +0x18
        EmotionalStateTemplate existingTemplate = existing.GetTemplate();  // +0x10

        if (existingTemplate.StateType != newType) continue;

        // Same type found - handle based on polarity
        if (existingTemplate.IsPositive == newTemplate.IsPositive) {  // +0xCC
            // Same polarity
            if (existingTemplate.IsSuperState) {  // +0xCD
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
            if (!existingTemplate.IsSuperState) {
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
    for (int i = m_States.Count - 1; i >= 0; i--) {
        EmotionalState state = m_States[i];
        EmotionalStateTemplate template = state.GetTemplate();  // +0x10

        if (template.StateType == EmotionalStateType.AnimosityTowards || template.StateType == EmotionalStateType.GoodwillTowards) {
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
    for (int i = m_States.Count - 1; i >= 0; i--) {
        EmotionalState state = m_States[i];
        state.IsFirstMission = false;  // +0x2C

        // Check for "permanent until victory" type
        EmotionalStateTemplate template = state.GetTemplate();
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

### AddEffect Flow

```c
// @ 1805b3550
void AddEffect(BaseUnitLeader owner) {
    EmotionalStateTemplate template = GetTemplate();  // +0x10
    if (template == null) return;

    EffectTemplate effectTemplate = template.Effect;  // +0x98
    if (effectTemplate == null) {
        // No effect to add - log debug message
        return;
    }

    // Targeted emotions don't add effects if no target
    if (template.StateType != EmotionalStateType.AnimosityTowards && template.StateType != EmotionalStateType.GoodwillTowards) {
        EffectContainer effects = owner.Effects;  // +0x38
        Effect effect = effectTemplate.CreateEffect();
        effects.Add(effect);

        // Warn about deprecated flags
        if (effectTemplate.HasDeprecatedFlag1) {  // +0x117
            Debug.LogWarning("Effect has deprecated flag 1");
        }
        if (effectTemplate.HasDeprecatedFlag2) {  // +0x118
            Debug.LogWarning("Effect has deprecated flag 2");
        }
    }

    // Log debug message
    Debug.Log($"Added emotion effect {effectTemplate.ID} for {template.StateType} triggered by {Trigger}");
}
```

## EmotionalStateTemplate Class

### Key Fields

```c
public class EmotionalStateTemplate : DataTemplate {
    // DataTemplate fields...
    EmotionalStateType StateType;                 // +0x78 (emotion type)
    Sprite Icon;                                  // +0x80
    EffectTemplate Effect;                        // +0x98 (applied effect modifier)
    IntRange Duration;                            // +0xC0 (random duration range)
    bool IsPositive;                              // +0xCC (positive emotion)
    bool IsSuperState;                            // +0xCD (is super state)
    EmotionalStateTemplate SuperState;            // +0xD0 (super state reference)
}
```

### Key Methods

```c
// Constructor
void EmotionalStateTemplate.ctor();  // @ 1805b3380

// Queries
static EmotionalStateTemplate GetByType(EmotionalStateType type);  // @ 1805b2f30
bool NeedsTarget();  // @ 1805b3370 (true for StateType == AnimosityTowards or GoodwillTowards)
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
            emotions.TriggerEmotion(EmotionalTrigger.KilledXEnemyEntities, null, random, mission);
        }

        // Check for wounds (significant HP loss)
        if (leader.WasWoundedThisMission) {
            emotions.TriggerEmotion(EmotionalTrigger.LostOverXPercentHitpoints, null, random, mission);
        }

        // Check for stabilizing others
        foreach (BaseUnitLeader stabilized in stabilizedAllies) {
            emotions.TriggerEmotion(EmotionalTrigger.StabilizedOthers, stabilized.GetTemplate(), random, mission);
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
        if (__instance.GetTemplate().IsPositive && __instance.RemainingDuration == 1) {
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
        // Clear all negative emotions on victory
        if (isVictory) {
            for (int i = __instance.m_States.Count - 1; i >= 0; i--) {
                if (!__instance.m_States[i].GetTemplate().IsPositive) {
                    __instance.RemoveState(i);
                }
            }
        }
    }
}
```

## Key Constants

```c
// EmotionalStateType values that require target
const int TYPE_ANIMOSITY_TOWARDS = 1;   // Requires target leader
const int TYPE_GOODWILL_TOWARDS = 8;    // Requires target leader

// Default initialization values
const int INIT_LAST_MISSION = -1;   // 0xFFFFFFFF
const int INIT_LAST_OPERATION = -1; // 0xFFFFFFFF

// Save version threshold
const int VERSION_OPERATION_TRACKING = 101;
```

## Related Classes

- **BaseUnitLeader**: Owner of EmotionalStates (+0x58)
- **EmotionalStateTemplate**: Definition of emotion types
- **EffectTemplate**: Effect applied by emotion
- **EmotionalStatesConfig**: Global emotion configuration
- **EmotionalTriggerExtensions**: Trigger type utilities
- **MissionResult**: Triggers emotions after combat
