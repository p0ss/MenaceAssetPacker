# Conversation

`Menace.SDK.Conversation` -- Static class for the conversation and dialogue system. Provides safe access to conversations, speakers, roles, and dialogue playback.

## Overview

The Conversation SDK wraps the game's dialogue system which consists of:
- **BaseConversationManager** - Manages conversation templates and speaker finding
- **ConversationPresenter** - Handles runtime dialogue playback
- **ConversationTemplate** - Defines conversations with roles and nodes
- **Role** - Defines speaker requirements and matching criteria

## Constants

### Trigger Types

Trigger types determine when a conversation can be activated.

```csharp
public const int TRIGGER_NONE = 0;
public const int TRIGGER_MISSION_START = 1;
public const int TRIGGER_MISSION_END = 2;
public const int TRIGGER_TURN_START = 3;
public const int TRIGGER_TURN_END = 4;
public const int TRIGGER_ROUND_START = 5;
public const int TRIGGER_ROUND_END = 6;
public const int TRIGGER_SKILL_USED = 7;
public const int TRIGGER_UNIT_KILLED = 8;
public const int TRIGGER_UNIT_DAMAGED = 9;
public const int TRIGGER_UNIT_HEALED = 10;
public const int TRIGGER_COVER_DESTROYED = 11;
public const int TRIGGER_OBJECTIVE_COMPLETED = 12;
public const int TRIGGER_OBJECTIVE_FAILED = 13;
public const int TRIGGER_IDLE = 14;
public const int TRIGGER_CUSTOM = 15;
public const int TRIGGER_TYPE_COUNT = 52;
```

### Role Requirement Types

Requirements define criteria for matching speakers to conversation roles.

```csharp
public const int REQ_EMPTY = 0;
public const int REQ_ACTION_POINTS = 1;
public const int REQ_CAN_SKILL_DESTROY_TARGET = 2;
public const int REQ_CAN_SKILL_NOT_DESTROY_TARGET = 3;
public const int REQ_DAMAGE_RECEIVED_THIS_TURN = 4;
public const int REQ_FACTION = 5;
public const int REQ_HAS_ALL_TAGS = 6;
public const int REQ_HAS_COVER = 7;
public const int REQ_HAS_EMOTIONAL_STATES = 8;
public const int REQ_HAS_ENTITY_PROPERTY = 9;
public const int REQ_HAS_ITEM_WITH_TAG = 10;
public const int REQ_HAS_LAST_SKILL_NOT_TAGS = 11;
public const int REQ_HAS_LAST_SKILL_TAGS = 12;
public const int REQ_HAS_NOT_TAG = 13;
public const int REQ_HAS_ONE_TAG = 14;
public const int REQ_HAS_RANK = 15;
public const int REQ_HEALTH = 16;
public const int REQ_IS_ACTIVE_ACTOR = 17;
public const int REQ_IS_ACTOR = 18;
public const int REQ_IS_ALLY = 19;
public const int REQ_IS_AVAILABLE = 20;
public const int REQ_IS_DEPLOYED_WITH_OTHER_MORE_THAN = 21;
public const int REQ_IS_ENEMY = 22;
public const int REQ_IS_HIDDEN = 23;
public const int REQ_IS_IN_ROSTER = 24;
public const int REQ_IS_INSIDE = 25;
public const int REQ_IS_LAST_SKILL_OF_TYPE = 26;
public const int REQ_IS_OBJECTIVE_TARGET = 27;
public const int REQ_IS_ON_BATTLEFIELD = 28;
public const int REQ_IS_SELECTED = 29;
public const int REQ_IS_STANDING_ON = 30;
public const int REQ_IS_TYPE = 31;
public const int REQ_IS_UNAVAILABLE = 32;
public const int REQ_IS_USER_OF_LAST_USED_SKILL = 33;
public const int REQ_IS_USES_OF_SKILL_USED = 34;
public const int REQ_KNOWS_OF = 35;
public const int REQ_MORALE = 36;
public const int REQ_PARTICIPATED_IN_PREVIOUS_MISSION = 37;
public const int REQ_STATISTIC = 38;
public const int REQ_SUPPRESSION = 39;
public const int REQ_THREATENS_DEFEND_AREA = 40;
public const int REQ_IS_SKILL_USED = 41;
public const int REQ_HITPOINTS = 42;
```

### Default Values

```csharp
public const int DEFAULT_PRIORITY = 1;
public const int DEFAULT_CHANCE = 100;    // 100% chance
public const int NO_TARGET_ROLE = -1;
```

## Methods

### GetConversationManager

```csharp
public static GameObj GetConversationManager()
```

Get the BaseConversationManager instance.

**Returns:** `GameObj` wrapping the conversation manager, or `GameObj.Null` if not available.

### GetPresenter

```csharp
public static GameObj GetPresenter()
```

Get the ConversationPresenter instance.

**Returns:** `GameObj` wrapping the presenter, or `GameObj.Null` if not available.

### GetCurrentConversation

```csharp
public static GameObj GetCurrentConversation()
```

Get the currently playing conversation template.

**Returns:** `GameObj` wrapping the template, or `GameObj.Null` if no conversation is running.

### IsConversationRunning

```csharp
public static bool IsConversationRunning()
```

Check if a conversation is currently running.

**Returns:** `true` if a conversation is active, `false` otherwise.

### GetAvailableConversations

```csharp
public static List<ConversationInfo> GetAvailableConversations(int triggerType)
```

Get available conversation templates for a specific trigger type.

**Parameters:**
- `triggerType` - The trigger type to query (use `TRIGGER_*` constants)

**Returns:** List of available `ConversationInfo` objects.

### GetAllConversationTemplates

```csharp
public static List<ConversationInfo> GetAllConversationTemplates()
```

Get all registered conversation templates.

**Returns:** List of all `ConversationInfo` objects.

### GetConversationInfo

```csharp
public static ConversationInfo GetConversationInfo(GameObj template)
```

Get information about a conversation template.

**Parameters:**
- `template` - GameObj wrapping a ConversationTemplate

**Returns:** `ConversationInfo` with template details, or `null` on failure.

### GetRoles

```csharp
public static List<RoleInfo> GetRoles(GameObj template)
```

Get roles defined in a conversation template.

**Parameters:**
- `template` - GameObj wrapping a ConversationTemplate

**Returns:** List of `RoleInfo` objects.

### GetRoleInfo

```csharp
public static RoleInfo GetRoleInfo(GameObj role)
```

Get information about a role.

**Parameters:**
- `role` - GameObj wrapping a Role

**Returns:** `RoleInfo` with role details, or `null` on failure.

### GetPresenterState

```csharp
public static PresenterStateInfo GetPresenterState()
```

Get the current presenter state.

**Returns:** `PresenterStateInfo` with current state, or `null` if unavailable.

### TriggerConversation

```csharp
public static bool TriggerConversation(string templateName)
public static bool TriggerConversation(GameObj template)
```

Trigger a conversation by template name or template object.

**Parameters:**
- `templateName` - Name of the ConversationTemplate to trigger
- `template` - GameObj wrapping a ConversationTemplate

**Returns:** `true` if the conversation was triggered, `false` otherwise.

### CancelConversation

```csharp
public static bool CancelConversation()
```

Cancel the currently playing conversation.

**Returns:** `true` if a conversation was cancelled, `false` otherwise.

### ShowNextNode

```csharp
public static bool ShowNextNode()
```

Advance to the next node in the current conversation.

**Returns:** `true` if advanced, `false` otherwise.

### ProcessContinue

```csharp
public static bool ProcessContinue()
```

Process continue action (advance conversation).

**Returns:** `true` if processed, `false` otherwise.

### GetAllSpeakers

```csharp
public static List<SpeakerInfo> GetAllSpeakers()
```

Get all available speaker templates.

**Returns:** List of `SpeakerInfo` objects.

### GetSpeakerInfo

```csharp
public static SpeakerInfo GetSpeakerInfo(GameObj speaker)
```

Get information about a speaker template.

**Parameters:**
- `speaker` - GameObj wrapping a SpeakerTemplate

**Returns:** `SpeakerInfo` with details, or `null` on failure.

### FindSpeaker

```csharp
public static GameObj FindSpeaker(string name)
```

Find a speaker template by name.

**Parameters:**
- `name` - Name to search for

**Returns:** `GameObj` wrapping the speaker template, or `GameObj.Null` if not found.

### FindConversation

```csharp
public static GameObj FindConversation(string name)
```

Find a conversation template by name.

**Parameters:**
- `name` - Name to search for

**Returns:** `GameObj` wrapping the template, or `GameObj.Null` if not found.

### GetRepetitionCount

```csharp
public static int GetRepetitionCount(GameObj template)
```

Get the number of times a conversation has been played.

**Parameters:**
- `template` - GameObj wrapping a ConversationTemplate

**Returns:** Repetition count.

### IsTriggerCompleted

```csharp
public static bool IsTriggerCompleted(int triggerType)
```

Check if a trigger type has been completed.

**Parameters:**
- `triggerType` - Trigger type to check (use `TRIGGER_*` constants)

**Returns:** `true` if the trigger has been completed.

### GetTriggerTypeName

```csharp
public static string GetTriggerTypeName(int triggerType)
```

Get the human-readable name for a trigger type.

**Parameters:**
- `triggerType` - Trigger type value (use `TRIGGER_*` constants)

**Returns:** Human-readable name (e.g., "MissionStart", "UnitKilled").

### GetRequirementTypeName

```csharp
public static string GetRequirementTypeName(int requirementType)
```

Get the human-readable name for a role requirement type.

**Parameters:**
- `requirementType` - Requirement type value (use `REQ_*` constants)

**Returns:** Human-readable name (e.g., "Faction", "IsEnemy").

## Types

### ConversationInfo

```csharp
public class ConversationInfo
{
    public string TemplateName { get; set; }      // Name of the conversation template
    public int Type { get; set; }                  // Conversation type identifier
    public bool IsOnlyOnce { get; set; }          // Whether the conversation can only play once
    public int Priority { get; set; }              // Priority level for conversation selection
    public int Chance { get; set; }                // Chance percentage (0-100) of triggering
    public int RoleCount { get; set; }             // Number of roles defined
    public int TriggerType { get; set; }           // Primary trigger type
    public string TriggerTypeName { get; set; }    // Human-readable trigger type name
    public int RepetitionCount { get; set; }       // Times this conversation has been played
    public int RandomSeed { get; set; }            // Random seed for variations
    public IntPtr Pointer { get; set; }            // Pointer to the native object
}
```

### RoleInfo

```csharp
public class RoleInfo
{
    public int Guid { get; set; }                  // Unique identifier for this role
    public bool IsOptional { get; set; }           // Whether this role is optional
    public int TargetRoleIndex { get; set; }       // Target role index for relationships (-1 for none)
    public int PositionFlags { get; set; }         // Position flags for speaker positioning
    public int RequirementCount { get; set; }      // Number of requirements for this role
    public List<string> Tags { get; set; }         // Tags associated with this role
    public IntPtr Pointer { get; set; }            // Pointer to the native object
}
```

### SpeakerInfo

```csharp
public class SpeakerInfo
{
    public string TemplateName { get; set; }       // Name of the speaker template
    public string DisplayName { get; set; }        // Display name of the speaker
    public IntPtr Pointer { get; set; }            // Pointer to the native object
}
```

### PresenterStateInfo

```csharp
public class PresenterStateInfo
{
    public bool IsRunning { get; set; }            // Whether a conversation is currently running
    public bool IsFastForwarding { get; set; }     // Whether fast-forwarding is active
    public string CurrentTemplateName { get; set; } // Name of the current conversation template
    public string CurrentNodeLabel { get; set; }   // Label of the current node being displayed
    public bool HasActiveSound { get; set; }       // Whether audio is currently playing
    public IntPtr Pointer { get; set; }            // Pointer to the presenter object
}
```

## Examples

### Listing available conversations

```csharp
// Get all conversations
var allConversations = Conversation.GetAllConversationTemplates();
foreach (var conv in allConversations)
{
    DevConsole.Log($"{conv.TemplateName} - {conv.TriggerTypeName} ({conv.RoleCount} roles)");
}

// Get conversations for a specific trigger
var missionStartConvs = Conversation.GetAvailableConversations(Conversation.TRIGGER_MISSION_START);
DevConsole.Log($"Found {missionStartConvs.Count} mission start conversations");
```

### Triggering a conversation

```csharp
// Trigger by name
bool success = Conversation.TriggerConversation("IntroDialogue");
if (success)
    DevConsole.Log("Conversation started!");

// Trigger by template object
var template = Conversation.FindConversation("VictoryDialogue");
if (!template.IsNull)
{
    Conversation.TriggerConversation(template);
}
```

### Checking conversation state

```csharp
// Check if a conversation is running
if (Conversation.IsConversationRunning())
{
    var state = Conversation.GetPresenterState();
    DevConsole.Log($"Playing: {state.CurrentTemplateName}");
    DevConsole.Log($"Current node: {state.CurrentNodeLabel}");
    DevConsole.Log($"Audio playing: {state.HasActiveSound}");
}
```

### Controlling conversation playback

```csharp
// Advance to next line
if (Conversation.IsConversationRunning())
{
    Conversation.ProcessContinue();
}

// Skip the entire conversation
Conversation.CancelConversation();
```

### Inspecting conversation roles

```csharp
var template = Conversation.FindConversation("SquadBanter");
if (!template.IsNull)
{
    var info = Conversation.GetConversationInfo(template);
    DevConsole.Log($"Conversation: {info.TemplateName}");
    DevConsole.Log($"Priority: {info.Priority}, Chance: {info.Chance}%");
    DevConsole.Log($"Only once: {info.IsOnlyOnce}");

    var roles = Conversation.GetRoles(template);
    foreach (var role in roles)
    {
        var optional = role.IsOptional ? " (optional)" : "";
        var tags = role.Tags.Count > 0 ? $" [{string.Join(", ", role.Tags)}]" : "";
        DevConsole.Log($"  Role {role.Guid}{optional}{tags} - {role.RequirementCount} requirements");
    }
}
```

### Working with speakers

```csharp
// List all speakers
var speakers = Conversation.GetAllSpeakers();
foreach (var speaker in speakers)
{
    var display = !string.IsNullOrEmpty(speaker.DisplayName) ? $" ({speaker.DisplayName})" : "";
    DevConsole.Log($"Speaker: {speaker.TemplateName}{display}");
}

// Find a specific speaker
var narrator = Conversation.FindSpeaker("Narrator");
if (!narrator.IsNull)
{
    var info = Conversation.GetSpeakerInfo(narrator);
    DevConsole.Log($"Found speaker: {info.DisplayName}");
}
```

### Checking trigger completion

```csharp
// Check which triggers have been completed
for (int i = 0; i <= Conversation.TRIGGER_CUSTOM; i++)
{
    if (Conversation.IsTriggerCompleted(i))
    {
        DevConsole.Log($"Completed: {Conversation.GetTriggerTypeName(i)}");
    }
}
```

### Monitoring conversation repetitions

```csharp
var template = Conversation.FindConversation("RandomBanter");
if (!template.IsNull)
{
    int playCount = Conversation.GetRepetitionCount(template);
    DevConsole.Log($"This conversation has been played {playCount} time(s)");
}
```

## Console Commands

The following console commands are registered by `RegisterConsoleCommands()`:

| Command | Arguments | Description |
|---------|-----------|-------------|
| `conversations` | `[trigger]` | List available conversations. Optionally filter by trigger type. |
| `conversation` | `<name>` | Show detailed information about a specific conversation. |
| `speakers` | | List all speaker templates. |
| `conversationstatus` | | Show current conversation state (running, node, audio). |
| `skipconversation` | | Skip/cancel the currently playing conversation. |
| `nextline` | | Advance to the next line in the conversation. |
| `playconversation` | `<name>` | Trigger a conversation by name. |
| `triggers` | | List all conversation trigger types and their completion status. |
