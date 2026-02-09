# Conversation System

## Overview

The Conversation system manages in-game dialogue, barks, and event messages. Conversations are template-driven with role-based speaker assignment, conditional branching, and a flexible requirement system for contextual triggers.

## Architecture

```
BaseConversationManager (manages conversations)
├── PseudoRandom Random                           // +0x20
├── ConversationTemplate[] Templates              // +0x28
├── Dictionary<TriggerType,List<Template>> TriggerMap  // +0x30
├── Dictionary<Template,int> Repetitions          // +0x38
├── HashSet<TriggerType> CompletedTriggers        // +0x40
├── List<SpeakerTemplate> AvailableSpeakers       // +0x48
└── Dictionary<int,SpeakerTemplate> RoleSpeakers  // +0x50

ConversationTemplate (conversation definition)
├── ConversationType Type                         // +0x18
├── bool IsOnlyOnce                               // +0x1C
├── List<EventData> EventData                     // +0x30
├── ConversationCondition Condition               // +0x50
├── int Priority                                  // +0x5C
├── int Chance                                    // +0x64
├── List<Role> Roles                              // +0x68
├── List<ConversationTriggerType> TriggerTypes    // +0x70
├── ConversationNodeContainer NodeContainer       // +0x78
└── int RandomSeed                                // +0x80

Role (speaker role definition)
├── int Guid                                      // +0x1C
├── int TargetRoleIndex                           // +0x24 (-1 for none)
├── List<BaseRoleRequirement> Requirements        // +0x28
└── List<string> Tags                             // +0x30

ConversationPresenter (runtime presenter)
├── PseudoRandom Random                           // +0x10
├── IConversationView View                        // +0x18
├── ConversationTemplate CurrentTemplate          // +0x20
├── SpeakerPair CurrentSpeakers                   // +0x28
├── BaseConversationNode CurrentNode              // +0x38
├── bool IsFastForwarding                         // +0x40
└── SoundInstance CurrentSound                    // +0x48
```

## ConversationTriggerType Enum

```c
enum ConversationTriggerType {
    None = 0,
    MissionStart = 1,
    MissionEnd = 2,
    TurnStart = 3,
    TurnEnd = 4,
    RoundStart = 5,
    RoundEnd = 6,
    SkillUsed = 7,
    UnitKilled = 8,
    UnitDamaged = 9,
    UnitHealed = 10,
    CoverDestroyed = 11,
    ObjectiveCompleted = 12,
    ObjectiveFailed = 13,
    Idle = 14,
    Custom = 15,
    // ... additional types
}
```

## RoleRequirementType Enum

```c
enum RoleRequirementType {
    Empty = 0,
    ActionPoints = 1,
    CanSkillDestroyTarget = 2,
    CanSkillNotDestroyTarget = 3,
    DamageReceivedThisTurn = 4,
    Faction = 5,
    HasAllTags = 6,
    HasCover = 7,
    HasEmotionalStates = 8,
    HasEntityProperty = 9,
    HasItemWithTag = 10,
    HasLastSkillNotTags = 11,
    HasLastSkillTags = 12,
    HasNotTag = 13,
    HasOneTag = 14,
    HasRank = 15,
    Health = 16,
    IsActiveActor = 17,
    IsActor = 18,
    IsAlly = 19,
    IsAvailable = 20,
    IsDeployedWithOtherMoreThan = 21,
    IsEnemy = 22,
    IsHidden = 23,
    IsInRoster = 24,
    IsInside = 25,
    IsLastSkillOfType = 26,
    IsObjectiveTarget = 27,
    IsOnBattlefield = 28,
    IsSelected = 29,
    IsStandingOn = 30,
    IsType = 31,
    IsUnavailable = 32,
    IsUserOfLastUsedSkill = 33,
    IsUsesOfSkillUsed = 34,
    KnowsOf = 35,
    Morale = 36,
    ParticipatedInPreviousMission = 37,
    Statistic = 38,
    Suppression = 39,
    ThreatensDefendArea = 40,
    IsSkillUsed = 41,
    Hitpoints = 42
}
```

## BaseConversationManager Class

### Key Methods

```c
// Constructor
void BaseConversationManager.ctor(int seed);  // @ 180549f80

// Speaker finding
bool TryFindSpeakersForConversation(FindRequest request, out Dictionary speakers, HashSet<SpeakerTemplate> exclude);  // @ 180549980
bool TryFindSpeakerForRole(FindRequest request, Role role, out SpeakerTemplate speaker);  // @ 1805495d0

// Template queries
List<ConversationTemplate> GetAvailableConversationTemplates(ConversationTriggerType trigger);  // @ 180549050
IConversationEntity GetEntityForSpeakerTemplate(SpeakerTemplate speaker);  // @ 180549140

// Repetition tracking
void IncrementConversationRepetition(ConversationTemplate template);  // @ 1805491a0
void RemoveRepeatedConversations(ConversationTemplate template, int count);  // @ 180549470

// Persistence
void ProcessSaveState(SaveState state);  // @ 180549280
```

### TryFindSpeakersForConversation Flow

```c
// @ 180549980
bool TryFindSpeakersForConversation(FindRequest request, out Dictionary speakers, HashSet<SpeakerTemplate> exclude) {
    if (RoleSpeakers == null) return false;

    RoleSpeakers.Clear();  // +0x50

    ConversationTemplate template = request.Template;
    List<Role> roles = template.Roles;  // +0x68

    for (int i = 0; i < roles.Count; i++) {
        Role role = roles[i];

        // Refresh available speakers list
        AvailableSpeakers.Clear();  // +0x48
        AvailableSpeakers.AddRange(DataTemplateLoader.GetAll<SpeakerTemplate>());

        // Remove already assigned speakers
        foreach (SpeakerTemplate assigned in RoleSpeakers.Values) {
            AvailableSpeakers.Remove(assigned);
        }

        // Remove excluded speakers
        if (exclude != null) {
            foreach (SpeakerTemplate ex in exclude) {
                AvailableSpeakers.Remove(ex);
            }
        }

        // Shuffle for randomness
        Random.Shuffle(AvailableSpeakers);  // +0x20

        // Find matching speaker
        SpeakerTemplate speaker;
        if (!TryFindSpeakerForRole(request, role, out speaker)) {
            if (!role.IsOptional) {  // +0x18
                speakers = null;
                return false;
            }
            continue;
        }

        RoleSpeakers[role.Guid] = speaker;  // +0x1C
    }

    speakers = new Dictionary(RoleSpeakers);
    return true;
}
```

## Role Class

### Role Field Layout

```c
public class Role {
    // Object header                              // +0x00 - 0x0F
    bool IsOptional;                              // +0x18 (allows missing speaker)
    int Guid;                                     // +0x1C (unique role ID)
    byte PositionFlags;                           // +0x20 (position requirements)
    int TargetRoleIndex;                          // +0x24 (-1 for none)
    List<BaseRoleRequirement> Requirements;       // +0x28 (requirements list)
    List<string> Tags;                            // +0x30 (tag strings)
}
```

### FulfillsSpeakerAllRequirements Flow

```c
// @ 180577c00
bool FulfillsSpeakerAllRequirements(FindRequest request, SpeakerTemplate speaker, IConversationEntity entity, bool checkTactical) {
    if (Requirements == null) return false;  // +0x28

    foreach (BaseRoleRequirement req in Requirements) {
        if (req == null) continue;

        RoleRequirementType type = req.GetType();

        // Skip tactical requirements if not in tactical mode
        if (checkTactical) {
            switch (type) {
                case RoleRequirementType.IsActiveActor:
                case RoleRequirementType.HasCover:
                case RoleRequirementType.DamageReceivedThisTurn:
                // ... other tactical types
                    continue;
            }
        }

        // Check requirement
        if (!req.FulfillsRequirement(request, speaker, entity)) {
            return false;
        }
    }

    // All requirements passed - check second pass for tactical mode
    if (!checkTactical) return true;

    foreach (BaseRoleRequirement req in Requirements) {
        RoleRequirementType type = req.GetType();

        // Only check tactical requirements
        switch (type) {
            case RoleRequirementType.IsActiveActor:
            case RoleRequirementType.HasCover:
            // ...
                if (!req.FulfillsRequirement(request, speaker, entity)) {
                    return false;
                }
                break;
        }
    }

    return true;
}
```

## ConversationPresenter Class

### Key Methods

```c
// Constructor
void ConversationPresenter.ctor();  // implicit

// Playback
void PlayConversation(FindConversationSpeakersResult result);  // @ 180553530
void ShowNode(BaseConversationNode node);  // @ 180553fd0
void ShowNextNode();  // @ 180553f60
void ProcessContinue();  // @ 180553ae0
void CancelConversation();  // @ 1805532f0

// Display
void ShowText(string text);  // @ 1805546e0
void ShowSpeaker(SpeakerTemplate speaker);  // @ 180554610
void ShowRole(Role role);  // @ 1805544c0
void ShowChoices();  // @ 180553e20
void HideRole();  // @ 180553440

// Audio
void PlaySound(string soundId);  // @ 180553a20

// Utilities
string ReplaceTextPlaceholders(string text);  // @ 180553b80
Role GetNextRole();  // @ 180553300
bool IsConversationRunning();  // @ 180553520
```

### PlayConversation Flow

```c
// @ 180553530
void PlayConversation(FindConversationSpeakersResult result) {
    ConversationTemplate template = result.Template;

    // Get cooldown for this trigger type
    ConversationTriggerType trigger = template.TriggerTypes[0];  // +0x70
    float cooldown = ConversationConfig.Get().Cooldowns[trigger];  // +0x48

    if (cooldown <= 0.0f) {
        // No cooldown
        TriggerCooldowns[trigger] = 0;
    } else {
        // Set cooldown expiry
        TriggerCooldowns[trigger] = Time.time + cooldown;
    }

    SetConversation(template);

    // Check if conversation supports fast-forwarding
    ConversationNodeContainer nodes = template.NodeContainer;  // +0x78
    if (nodes.Count < 3) {
        // Check for variation node
        BaseConversationNode firstNode = nodes[0];
        if (firstNode is VariationConversationNode variation) {
            if (variation.Variations.Count > 1) {
                IsFastForwarding = true;  // +0x40
            }
        }
    } else {
        IsFastForwarding = true;
    }

    if (IsFastForwarding) {
        // Pre-execute all nodes
        nodes.ForEachNode(OnFastForwardNode);
    }

    // Notify view
    View.OnConversationStarted(template);  // +0x18

    if (template.SupportsRolePositions()) {
        View.SetRolePositions(result.Speakers.Positions);
    }

    // Start at "Start" label
    BaseConversationNode startNode = template.GetNodeByLabel("Start");
    ShowNode(startNode);
}
```

## Conversation Node Types

### BaseConversationNode

Base class for all conversation nodes.

```c
public class BaseConversationNode {
    // Object header                              // +0x00 - 0x0F
    int Guid;                                     // +0x10 (unique node ID)
    string Label;                                 // +0x18 (optional label)
    List<BaseConversationNode> Children;          // +0x20 (child nodes)
}
```

### SayConversationNode

Displays speaker dialogue.

```c
public class SayConversationNode : BaseConversationNode {
    // BaseConversationNode fields...
    int RoleIndex;                                // +0x28 (speaker role)
    string Text;                                  // +0x30 (localization key)
    string SoundId;                               // +0x38 (audio clip ID)
}
```

### ChoiceConversationNode

Presents player choices.

```c
public class ChoiceConversationNode : SayConversationNode {
    // SayConversationNode fields...
    List<Choice> Choices;                         // +0x30 (choice options)
}

public class Choice {
    // Object header                              // +0x00 - 0x0F
    string Text;                                  // +0x10 (choice text key)
    List<BaseConversationNode> ResultNodes;       // +0x18 (result branch)
    ConversationCondition Condition;              // +0x38 (visibility condition)
    bool IsDefault;                               // +0x40 (default selection)
}
```

### ActionConversationNode

Executes an action during conversation.

```c
public class ActionConversationNode : BaseConversationNode {
    // BaseConversationNode fields...
    BaseConversationNodeAction Action;            // +0x18 (action to execute)
}
```

### BaseIfConversationNode

Conditional branching node.

```c
public class BaseIfConversationNode : BaseConversationNode {
    // BaseConversationNode fields...
    ConversationCondition Condition;              // +0x28 (branch condition)
    List<BaseConversationNode> TrueNodes;         // +0x30 (true branch)
    List<BaseConversationNode> FalseNodes;        // +0x38 (false branch)
}
```

## Save State Serialization

### BaseConversationManager Save Order

```
ProcessSaveState order:
1. Random                 @ +0x20 (PseudoRandom)
2. Repetitions dictionary @ +0x38 (ConversationTemplate → int)
3. On load: RemoveRepeatedConversations for each entry
4. CompletedTriggers      @ +0x40 (HashSet<ConversationTriggerType>)
```

## Example Role Requirements

### IsActiveActorRoleRequirement

```c
// @ 180575c70
bool FulfillsRequirement(FindRequest request, SpeakerTemplate speaker, IConversationEntity entity) {
    TacticalManager manager = TacticalManager.Instance;
    if (manager == null) return false;

    // Check if entity is the active actor
    return manager.ActiveActor == entity;  // +0x50
}
```

### HasRankRoleRequirement

```c
// @ 180575910
bool FulfillsRequirement(FindRequest request, SpeakerTemplate speaker, IConversationEntity entity) {
    if (entity == null) return false;

    List<UnitRankType> validRanks = this.Ranks;  // +0x20

    foreach (UnitRankType rank in validRanks) {
        int entityRank = entity.GetRank();  // via IConversationEntity interface
        if (entityRank == (int)rank) {
            return true;
        }
    }

    return false;
}
```

## Modding Hooks

### Intercept Conversation Start

```csharp
[HarmonyPatch(typeof(ConversationPresenter), "PlayConversation")]
class ConversationStartPatch {
    static void Prefix(FindConversationSpeakersResult result) {
        Logger.Msg($"Starting conversation: {result.Template.name}");
    }
}
```

### Modify Speaker Selection

```csharp
[HarmonyPatch(typeof(BaseConversationManager), "TryFindSpeakerForRole")]
class SpeakerSelectionPatch {
    static void Postfix(ref bool __result, Role role, ref SpeakerTemplate speaker) {
        // Override speaker for specific roles
        if (role.Guid == SpecialRoleGuid) {
            speaker = GetCustomSpeaker();
            __result = true;
        }
    }
}
```

### Add Custom Requirement

```csharp
[HarmonyPatch(typeof(Role), "FulfillsSpeakerAllRequirements")]
class CustomRequirementPatch {
    static void Postfix(Role __instance, ref bool __result, IConversationEntity entity) {
        // Add custom requirement check
        if (__result && __instance.HasTag("CustomCheck")) {
            __result = CustomRequirement.Check(entity);
        }
    }
}
```

### Skip Conversations

```csharp
[HarmonyPatch(typeof(ConversationPresenter), "ShowNode")]
class SkipConversationPatch {
    static bool Prefix(ConversationPresenter __instance, BaseConversationNode node) {
        // Skip certain conversation types
        if (ShouldSkip(__instance.CurrentTemplate)) {
            __instance.CancelConversation();
            return false;
        }
        return true;
    }
}
```

## Key Constants

```c
// Cooldown array size (number of trigger types)
const int TRIGGER_TYPE_COUNT = 52;  // 0x34

// Default values
const int DEFAULT_PRIORITY = 1;
const int DEFAULT_CHANCE = 100;

// Special role indices
const int NO_TARGET_ROLE = -1;  // 0xFFFFFFFF
```

## Related Classes

- **ConversationTemplate**: Conversation definition with roles and nodes
- **ConversationPresenter**: Runtime playback manager
- **Role**: Speaker role with requirements
- **BaseRoleRequirement**: Abstract requirement base class
- **BaseConversationNode**: Abstract node base class
- **ConversationCondition**: Expression-based conditions
- **SpeakerTemplate**: Speaker character definition
- **IConversationEntity**: Entity interface for conversation participants
