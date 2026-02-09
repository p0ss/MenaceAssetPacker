# Event System

## Overview

The Event system extends the Conversation system to manage strategic layer events like mission select screens, operation outcomes, and character interactions. Events are conversations with additional metadata like priority, mandatory flags, and trigger conditions.

## Architecture

```
EventManager : BaseConversationManager
├── BaseConversationManager fields...           // +0x00 - 0x57
├── List<ConversationInstance> SystemMapEvents  // +0x58 (main map events)
├── List<ConversationInstance> MissionSelectEvents // +0x60 (pre-mission events)
├── List<ConversationInstance> UnavailableEvents // +0x68 (recovery events)
├── List<ConversationTemplate> GeneratedTemplates // +0x70 (cached templates)
├── bool IsEventRunning                         // +0x78
├── int CurrentEventIndex                       // +0x7C
├── ConversationInstance CurrentEvent           // +0x80
├── Action<EventFlags> OnEventsGenerated        // +0x88
└── Action OnCatastrophicAuthorityEventAdded    // +0x90

ConversationInstance (event runtime)
├── ConversationTemplate Template               // +0x10
├── Dictionary<int,SpeakerTemplate> Speakers    // +0x18
├── bool IsFinished                             // +0x24
├── ConversationTriggerType TriggerType         // +0x28
├── Action OnClosed                             // +0x30
└── Action OnFinished                           // +0x38

EventData (event metadata on ConversationTemplate)
├── string Title                                // +0x10 (localization key)
├── string Sender                               // +0x18 (sender name key)
├── string Message                              // +0x20 (message body key)
├── Sprite SenderImage                          // +0x28
├── Sprite BackgroundImage                      // +0x30
├── int Flags                                   // +0x48 (EventFlags enum)
├── int MinProgress                             // +0x4C (minimum game progress)
└── bool IsMandatory                            // +0x50
```

## EventFlags Enum

```c
enum EventFlags {
    None = 0,
    SystemMap = 1,        // Appears on main system map
    MissionSelect = 2,    // Appears before mission selection
    PostMission = 4,      // Appears after mission
    Mandatory = 8         // Must be completed
}
```

## EventManager Class

### Key Methods

```c
// Constructor (inherits from BaseConversationManager)
void EventManager.ctor();  // @ 18056db00

// Event generation
List<ConversationInstance> GeneratePotentialEvents(ConversationTriggerType trigger, EventFlags? flags);  // @ 18056ca70
void GenerateNewEvents(List<ConversationInstance> eventList, ConversationTriggerType trigger);  // @ 18056c3d0
void GenerateMissionSelectEvents();  // @ 18056c380
void GenerateSystemMapEvents();  // @ 18056ce50

// Event creation
ConversationInstance CreateEventInstance(ConversationTemplate template, ConversationTriggerType trigger, IConversationEntity entity);  // @ 18056c170
bool TryCreateAndStartEvent(ConversationTemplate template, ConversationTriggerType trigger);  // @ 18056d990

// Event playback
void Play(ConversationInstance instance);  // @ 18056d520

// Event queries
List<ConversationInstance> GetEvents(EventFlags flags);  // @ 18056cea0
bool IsAnyMandatoryEventUnfinished();  // @ 18056d0c0
bool IsAnyMandatoryMissionSelectEventUnfinished();  // @ 18056d1f0
bool IsAnyMandatorySystemMapEventUnfinished();  // @ 18056d220

// Event management
bool TryAddSystemMapEvent(ConversationTemplate template);  // @ 18056d900
bool TryRemoveEvent(ConversationInstance instance);  // @ 18056da60

// Special events
void AddCatastrophicAuthorityEvent();  // @ 18056ba70
void AddMenaceDetectedEvent();  // @ 18056bbc0

// Lifecycle
void AfterOperationOverEvent();  // @ 18056bc70
void AfterOperationResultEvent();  // @ 18056bea0
void AfterUnavailableOverEvent(BaseUnitLeader leader);  // @ 18056bf30
void AfterUnavailableOverEvents();  // @ 18056bfc0
void OnOperationFinished();  // @ 18056d340
void OnAfterOperationFinished();  // @ 18056d250
void OnStrategyVarChanged(VarType type, int oldValue, int newValue);  // @ 18056d3b0

// Persistence
void Init();  // @ 18056d000
void ProcessSaveState(SaveState state);  // @ 18056d5d0
```

### GeneratePotentialEvents Flow

```c
// @ 18056ca70
List<ConversationInstance> GeneratePotentialEvents(ConversationTriggerType trigger, EventFlags? flags) {
    int currentProgress = StrategyState.Instance.GetVar(VarType.Progress);  // +4

    List<ConversationInstance> result = new List<ConversationInstance>();
    int maxPriority = 0;

    List<ConversationTemplate> available = GetAvailableConversationTemplates(trigger);

    foreach (ConversationTemplate template in available) {
        if (template == null) continue;

        // Check priority
        if (template.Priority < maxPriority) continue;  // +0x60

        // Get event data
        EventData eventData = template.GetEventData();

        // Filter by flags if specified
        if (flags.HasValue) {
            if (eventData == null) continue;
            if (eventData.Flags != flags.Value) continue;  // +0x48
        } else {
            if (eventData == null) continue;
        }

        // Check progress requirement
        if (eventData.MinProgress > currentProgress) continue;  // +0x4C

        // Evaluate condition
        if (!template.EvaluateCondition()) continue;

        // Find speakers
        FindConversationSpeakersResult speakerResult;
        if (!TryFindSpeakersForConversation(template, out speakerResult)) continue;

        // Clear lower priority events
        if (template.Priority > maxPriority) {
            result.Clear();
            maxPriority = template.Priority;
        }

        // Create instance
        ConversationInstance instance = new ConversationInstance(template, speakerResult.Speakers, trigger);
        result.Add(instance);
    }

    return result;
}
```

### OnOperationFinished Flow

```c
// @ 18056d340
void OnOperationFinished() {
    CurrentEvent = null;  // +0x80
    CurrentEventIndex = 0;  // +0x7C

    // Clear system map events
    SystemMapEvents.Clear();  // +0x58
}
```

## ConversationInstance Class

### Field Layout

```c
public class ConversationInstance : ISaveStateProcessor {
    // Object header                              // +0x00 - 0x0F
    ConversationTemplate Template;                // +0x10 (event definition)
    Dictionary<int,SpeakerTemplate> Speakers;     // +0x18 (role → speaker mapping)
    bool IsFinished;                              // +0x24 (completion flag)
    ConversationTriggerType TriggerType;          // +0x28 (event trigger)
    Action OnClosed;                              // +0x30 (close callback)
    Action OnFinished;                            // +0x38 (finish callback)
    int Hashcode;                                 // +0x40 (for comparison)
}
```

### Key Methods

```c
// Constructor
void ConversationInstance.ctor(ConversationTemplate template, Dictionary<int,SpeakerTemplate> speakers, ConversationTriggerType trigger);  // @ 180551960

// State
void SetFinished();  // @ 180550570
void InvokeOnClosed();  // @ 180550270

// Queries
SpeakerTemplate TryGetSpeaker(int roleGuid);  // @ 180551690
string TryGetRoleTextReplacement(int roleGuid, string key);  // @ 1805511e0
bool TryGetIntVarValue(string varName, out int value);  // @ 1805505a0
string GetNamesForLog();  // @ 180550020

// Internal
void UpdateHashcode();  // @ 180551720

// Persistence
void ProcessSaveState(SaveState state);  // @ 180550290
```

### ProcessSaveState Flow

```c
// @ 180550290
void ProcessSaveState(SaveState state) {
    // Save/load template
    state.ProcessConversationTemplate(ref Template);  // +0x10

    // Save/load finished flag
    state.ProcessBool(ref IsFinished);  // +0x24

    // Save/load trigger type
    state.ProcessEnum<ConversationTriggerType>(ref TriggerType);  // +0x28

    if (state.IsLoading) {
        // Load speakers
        Speakers.Clear();  // +0x18
        int count = state.ReadInt();
        for (int i = 0; i < count; i++) {
            int roleGuid = state.ReadInt();
            SpeakerTemplate speaker = state.ReadDataTemplate<SpeakerTemplate>();
            Speakers[roleGuid] = speaker;
        }
        UpdateHashcode();
    } else {
        // Save speakers
        state.WriteInt(Speakers.Count);
        foreach (var pair in Speakers) {
            state.WriteInt(pair.Key);
            state.WriteDataTemplate(pair.Value);
        }
    }
}
```

## EventData Class

Metadata for conversations that are events.

### Field Layout

```c
public class EventData {
    // Object header                              // +0x00 - 0x0F
    string Title;                                 // +0x10 (localization key)
    string Sender;                                // +0x18 (sender name key)
    string Message;                               // +0x20 (message body key)
    Sprite SenderImage;                           // +0x28
    Sprite BackgroundImage;                       // +0x30
    Color BackgroundColor;                        // +0x38
    int Flags;                                    // +0x48 (EventFlags)
    int MinProgress;                              // +0x4C (minimum progress, default: 1)
    bool IsMandatory;                             // +0x50
}
```

### Key Methods

```c
// Constructor
void EventData.ctor();  // @ 18055acb0 (sets MinProgress = 1)

// Queries
bool IsMandatory();  // @ 18055aca0 (checks Flags & Mandatory)
bool IsIntro();  // @ 18055ac90

// Utilities
EventData Clone();  // @ 18055abc0
```

## Save State Serialization

### EventManager Save Order

```
ProcessSaveState order:
1. Base BaseConversationManager state
2. IsEventRunning             @ +0x78 (bool)
3. SystemMapEvents list       @ +0x58 (List<ConversationInstance>)
4. MissionSelectEvents list   @ +0x60 (List<ConversationInstance>)
5. UnavailableEvents list     @ +0x68 (List<ConversationInstance>)
6. GeneratedTemplates list    @ +0x70 (List<ConversationTemplate>)
```

### ConversationInstance Save Order

```
ProcessSaveState order:
1. Template                   @ +0x10 (ConversationTemplate reference)
2. IsFinished                 @ +0x24 (bool)
3. TriggerType                @ +0x28 (ConversationTriggerType enum)
4. Speakers count             (int)
5. For each speaker:
   a. RoleGuid                (int)
   b. SpeakerTemplate         (DataTemplate reference)
```

## Event Flow Examples

### Mission Select Event Flow

1. `GenerateMissionSelectEvents()` is called
2. Calls `GenerateNewEvents(MissionSelectEvents, TriggerType.MissionSelect)`
3. `GeneratePotentialEvents()` filters and creates eligible events
4. Events added to `MissionSelectEvents` list
5. `OnEventsGenerated` callback invoked with `EventFlags.MissionSelect`
6. UI displays available events
7. Player selects event, `Play()` is called
8. Event conversation runs
9. `SetFinished()` called on completion

### System Map Event Flow

1. `GenerateSystemMapEvents()` is called
2. Filters for `EventFlags.SystemMap` events
3. Events added to `SystemMapEvents` list
4. `OnEventsGenerated` callback invoked
5. Events displayed on system map UI
6. Mandatory events block progression

## Modding Hooks

### Intercept Event Generation

```csharp
[HarmonyPatch(typeof(EventManager), "GeneratePotentialEvents")]
class EventGenerationPatch {
    static void Postfix(ref List<ConversationInstance> __result, ConversationTriggerType trigger) {
        // Filter or add custom events
        foreach (var evt in __result.ToList()) {
            if (ShouldBlockEvent(evt.Template)) {
                __result.Remove(evt);
            }
        }
    }
}
```

### Add Custom Event

```csharp
[HarmonyPatch(typeof(EventManager), "GenerateMissionSelectEvents")]
class CustomEventPatch {
    static void Postfix(EventManager __instance) {
        var customTemplate = GetCustomEventTemplate();
        if (customTemplate != null) {
            __instance.TryAddSystemMapEvent(customTemplate);
        }
    }
}
```

### Modify Event Playback

```csharp
[HarmonyPatch(typeof(EventManager), "Play")]
class EventPlayPatch {
    static void Prefix(ConversationInstance instance) {
        Logger.Msg($"Playing event: {instance.Template.name}");
    }
}
```

### Override Mandatory Check

```csharp
[HarmonyPatch(typeof(EventManager), "IsAnyMandatoryEventUnfinished")]
class MandatoryCheckPatch {
    static void Postfix(ref bool __result) {
        // Skip mandatory events for testing
        if (DebugSettings.SkipMandatoryEvents) {
            __result = false;
        }
    }
}
```

## Key Constants

```c
// Event trigger for mission select screen
const int TRIGGER_MISSION_SELECT = 1;

// Default progress requirement
const int DEFAULT_MIN_PROGRESS = 1;

// StrategyState.VarType for progress
const int VAR_PROGRESS = 4;
```

## Related Classes

- **BaseConversationManager**: Base class with conversation management
- **ConversationTemplate**: Template with optional EventData
- **ConversationPresenter**: Conversation playback
- **StrategyState**: Access to progress variables
- **SpeakerTemplate**: Character definitions for event speakers
