# Emotions

`Menace.SDK.Emotions` -- Static class for managing squaddie emotional states and morale effects.

The Emotions SDK provides safe access to the game's Emotional State system. Emotions are triggered by in-game events (kills, injuries, ally deaths) and apply skill modifiers that affect combat performance. Each emotion has a duration measured in missions and can be positive or negative.

## Enums

### EmotionalStateType

```csharp
public enum EmotionalStateType
{
    None = 0,
    AnimosityTowards = 1,  // Animosity towards a specific target
    Determined = 2,        // Focused and resolute
    Weary = 3,             // Tired from extended duty
    Disheartened = 4,      // Morale reduced
    Eager = 5,             // Enthusiastic and ready for action
    Frustrated = 6,        // Annoyed and less effective
    Exhausted = 7,         // Severely fatigued
    GoodwillTowards = 8,   // Goodwill towards a specific target
    Hesitant = 9,          // Uncertain and cautious
    Overconfident = 10,    // Too bold, may make mistakes
    Injured = 11,          // Physically wounded
    Bruised = 12,          // Minor physical damage
    Euphoric = 13,         // Extremely positive mood
    Miserable = 14         // Extremely negative mood
}
```

### EmotionalTrigger

```csharp
public enum EmotionalTrigger
{
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

## Methods

### GetEmotionalStates

```csharp
public static GameObj GetEmotionalStates(GameObj leader)
```

Get the EmotionalStates collection for a unit leader.

**Parameters:**
- `leader` - The BaseUnitLeader GameObj

**Returns:** `GameObj` representing the EmotionalStates collection, or `GameObj.Null` if not found.

### GetEmotionalStatesInfo

```csharp
public static EmotionalStatesInfo GetEmotionalStatesInfo(GameObj leader)
```

Get detailed information about all emotional states for a unit leader.

**Parameters:**
- `leader` - The BaseUnitLeader GameObj

**Returns:** `EmotionalStatesInfo` with all active emotions, or `null` if not available.

### HasEmotion

```csharp
public static bool HasEmotion(GameObj leader, EmotionalStateType type)
```

Check if a unit leader has a specific emotional state type.

### HasAnyEmotion

```csharp
public static bool HasAnyEmotion(GameObj leader, params EmotionalStateType[] types)
```

Check if a unit leader has any of the specified emotional state types.

### HasAllEmotions

```csharp
public static bool HasAllEmotions(GameObj leader, params EmotionalStateType[] types)
```

Check if a unit leader has all of the specified emotional state types.

### GetStateSet

```csharp
public static HashSet<EmotionalStateType> GetStateSet(GameObj leader)
```

Get the set of all active emotional state types for a unit leader.

### GetEmotionInfo

```csharp
public static EmotionalStateInfo GetEmotionInfo(GameObj leader, EmotionalStateType type)
```

Get information about a specific active emotion type on a leader.

**Returns:** `EmotionalStateInfo` if found, `null` otherwise.

### TriggerEmotion

```csharp
public static EmotionResult TriggerEmotion(GameObj leader, EmotionalTrigger trigger, GameObj target = default)
```

Trigger an emotion on a unit leader based on a game event.

**Parameters:**
- `leader` - The BaseUnitLeader GameObj
- `trigger` - The trigger event causing the emotion
- `target` - Optional target leader for targeted emotions

**Returns:** `EmotionResult` indicating success/failure.

### ApplyEmotion

```csharp
public static EmotionResult ApplyEmotion(GameObj leader, string templateName, EmotionalTrigger trigger = EmotionalTrigger.Cheat, GameObj target = default)
```

Apply a specific emotional state template to a leader by name.

**Parameters:**
- `leader` - The BaseUnitLeader GameObj
- `templateName` - Name of the EmotionalStateTemplate to apply
- `trigger` - The trigger causing this emotion
- `target` - Optional target leader for targeted emotions

**Returns:** `EmotionResult` indicating success/failure.

### RemoveEmotion

```csharp
public static EmotionResult RemoveEmotion(GameObj leader, EmotionalStateType type)
```

Remove a specific emotional state type from a leader.

### ClearEmotions

```csharp
public static int ClearEmotions(GameObj leader)
```

Remove all emotional states from a leader.

**Returns:** Number of emotions removed.

### ClearNegativeEmotions

```csharp
public static int ClearNegativeEmotions(GameObj leader)
```

Clear all negative emotions (Weary, Disheartened, Frustrated, Exhausted, etc.) from a leader.

**Returns:** Number of negative emotions removed.

### ClearPositiveEmotions

```csharp
public static int ClearPositiveEmotions(GameObj leader)
```

Clear all positive emotions from a leader.

**Returns:** Number of positive emotions removed.

### ExtendDuration

```csharp
public static EmotionResult ExtendDuration(GameObj leader, EmotionalStateType type, int missions = 1)
```

Extend the duration of an active emotion.

**Parameters:**
- `leader` - The BaseUnitLeader GameObj
- `type` - The emotional state type to extend
- `missions` - Number of missions to add to duration (default: 1)

### GetRemainingDuration

```csharp
public static int GetRemainingDuration(GameObj leader, EmotionalStateType type)
```

Get the remaining duration of an active emotion.

**Returns:** Remaining missions, or -1 if emotion not found.

### IsNegativeType

```csharp
public static bool IsNegativeType(EmotionalStateType type)
```

Check if an emotion type is negative (not positive). Returns `true` for Weary, Disheartened, Frustrated, Exhausted, Hesitant, Injured, Bruised, and Miserable.

### RequiresTarget

```csharp
public static bool RequiresTarget(EmotionalStateType type)
```

Check if an emotion type requires a target leader. Returns `true` for AnimosityTowards and GoodwillTowards.

### GetTypeName

```csharp
public static string GetTypeName(EmotionalStateType type)
```

Get the human-readable name of an emotional state type.

### GetTriggerName

```csharp
public static string GetTriggerName(EmotionalTrigger trigger)
```

Get the human-readable name of an emotional trigger.

### GetAvailableTemplates

```csharp
public static string[] GetAvailableTemplates()
```

Get all available emotion template names.

**Returns:** Sorted array of template names.

## Types

### EmotionalStateInfo

Information about a single active emotional state.

```csharp
public class EmotionalStateInfo
{
    public EmotionalStateType Type { get; set; }
    public string TypeName { get; set; }
    public string TemplateName { get; set; }
    public EmotionalTrigger Trigger { get; set; }
    public string TriggerName { get; set; }
    public string TargetLeaderName { get; set; }  // For targeted emotions
    public int RemainingDuration { get; set; }    // Missions remaining
    public bool IsFirstMission { get; set; }      // Just applied this mission
    public bool IsPositive { get; set; }
    public bool IsSuperState { get; set; }
    public string SkillName { get; set; }         // Effect modifier applied
    public IntPtr Pointer { get; set; }
}
```

### EmotionalStatesInfo

Information about a unit leader's emotional states collection.

```csharp
public class EmotionalStatesInfo
{
    public string OwnerName { get; set; }
    public IntPtr OwnerPointer { get; set; }
    public List<EmotionalStateInfo> ActiveStates { get; set; }
    public int LastMissionParticipation { get; set; }   // -1 if never
    public int LastOperationParticipation { get; set; } // -1 if never
    public int StateCount { get; }           // Total active emotions
    public int PositiveCount { get; }        // Non-negative emotions
    public int NegativeCount { get; }        // Negative emotions
    public IntPtr Pointer { get; set; }
}
```

### EmotionResult

Result from emotional state operations.

```csharp
public class EmotionResult
{
    public bool Success { get; set; }
    public string Error { get; set; }
    public EmotionalStateType StateType { get; set; }
    public string Action { get; set; }  // "Added", "Extended", "Replaced", "Reduced", "Removed"

    public static EmotionResult Failed(string error);
    public static EmotionResult Ok(EmotionalStateType type, string action);
}
```

## Examples

### Checking a squaddie's emotions

```csharp
var leader = Roster.FindByNickname("Phantom");
var info = Emotions.GetEmotionalStatesInfo(leader);

if (info != null && info.StateCount > 0)
{
    DevConsole.Log($"{info.OwnerName} has {info.StateCount} emotions:");
    DevConsole.Log($"  Positive: {info.PositiveCount}, Negative: {info.NegativeCount}");

    foreach (var state in info.ActiveStates)
    {
        var polarity = state.IsPositive ? "[+]" : "[-]";
        DevConsole.Log($"  {polarity} {state.TypeName}: {state.RemainingDuration} missions");
    }
}
```

### Triggering an emotion from an event

```csharp
var killer = Roster.FindByNickname("Reaper");

// Simulate killing enemies - may trigger Determined, Eager, etc.
var result = Emotions.TriggerEmotion(killer, EmotionalTrigger.KilledXEnemyEntities);
if (result.Success)
    DevConsole.Log("Emotion triggered successfully");
```

### Applying a specific emotion template

```csharp
var squaddie = Roster.FindByNickname("Ghost");

// Apply a specific emotion template by name
var result = Emotions.ApplyEmotion(squaddie, "Inspired_Leadership");
if (result.Success)
    DevConsole.Log($"Applied emotion: {result.Action}");
else
    DevConsole.Log($"Failed: {result.Error}");
```

### Removing negative emotions (morale boost)

```csharp
var leader = Roster.FindByNickname("Raven");

// Clear just negative emotions
int removed = Emotions.ClearNegativeEmotions(leader);
DevConsole.Log($"Cleared {removed} negative emotion(s)");

// Or remove a specific emotion type
var result = Emotions.RemoveEmotion(leader, EmotionalStateType.Hesitant);
```

### Checking for specific emotion types

```csharp
var leader = Roster.FindByNickname("Wolf");

// Check for a single emotion
if (Emotions.HasEmotion(leader, EmotionalStateType.Miserable))
{
    DevConsole.Log("Unit is miserable - consider sending to therapy");
}

// Check for any negative emotions
if (Emotions.HasAnyEmotion(leader,
    EmotionalStateType.Disheartened,
    EmotionalStateType.Exhausted,
    EmotionalStateType.Miserable))
{
    DevConsole.Log("Unit has negative emotions affecting performance");
}

// Get all active emotion types
var activeTypes = Emotions.GetStateSet(leader);
foreach (var type in activeTypes)
{
    DevConsole.Log($"Active: {Emotions.GetTypeName(type)}");
}
```

### Extending emotion duration

```csharp
var leader = Roster.FindByNickname("Ace");

// Check if they have Determined and extend it
if (Emotions.HasEmotion(leader, EmotionalStateType.Determined))
{
    int remaining = Emotions.GetRemainingDuration(leader, EmotionalStateType.Determined);
    DevConsole.Log($"Determined has {remaining} missions remaining");

    // Extend by 2 more missions
    var result = Emotions.ExtendDuration(leader, EmotionalStateType.Determined, 2);
    if (result.Success)
        DevConsole.Log("Extended Determined duration");
}
```

### Listing available templates

```csharp
var templates = Emotions.GetAvailableTemplates();
DevConsole.Log($"Available emotion templates ({templates.Length}):");
foreach (var template in templates)
{
    DevConsole.Log($"  {template}");
}
```

## Console Commands

The following console commands are registered by `RegisterConsoleCommands()`:

| Command | Arguments | Description |
|---------|-----------|-------------|
| `emotions` | `<nickname>` | Show emotional states for a unit |
| `triggeremotion` | `<nickname> <trigger>` | Trigger an emotion (KilledXEnemyEntities, GameEffect, Event, etc.) |
| `applyemotion` | `<nickname> <template>` | Apply an emotion template to a unit |
| `removeemotion` | `<nickname> <type>` | Remove an emotion type (Determined, Weary, Eager, etc.) |
| `clearemotions` | `<nickname> [negative\|positive]` | Clear all, negative, or positive emotions from a unit |
| `emotemplates` | | List available emotion templates |
| `hasemotion` | `<nickname> <type>` | Check if a unit has a specific emotion type |
| `extendemotion` | `<nickname> <type> [missions]` | Extend the duration of an active emotion |

### Console Command Examples

```
> emotions Phantom
Emotional States for Phantom (2 active):
  Positive: 1, Negative: 1
  [+] Determined: 3 missions
  [-] Disheartened: 2 missions -> Fallen Ally [NEW]

> triggeremotion Reaper KilledXEnemyEntities
Triggered KilledXEnemyEntities on Reaper

> applyemotion Ghost Inspired_Leadership
Applied 'Inspired_Leadership' to Ghost: Applied

> removeemotion Raven Hesitant
Removed Hesitant from Raven

> clearemotions Wolf negative
Removed 2 negative emotion(s) from Wolf

> emotemplates
Emotion Templates (12):
  Angry_Combat
  Confident_Victory
  Fear_Ambush
  ...

> hasemotion Ace Determined
Ace HAS Determined (3 missions remaining)

> extendemotion Ace Determined 2
Extended Determined by 2 mission(s)
```
