# Roster & Unit Management System

## Overview

The Roster system manages the player's collection of unit leaders, their squaddies (individual soldiers), perks, equipment, and deployment status. It tracks hired vs available units, handles mission participation, and manages unit progression.

## Architecture

```
Roster (singleton via StrategyState)
├── HiredLeaders (List<BaseUnitLeader>)      // Active player units
├── AvailableForHire (List<UnitLeaderTemplate>)  // Recruitable
├── DeadLeaders (List<BaseUnitLeader>)       // Killed in action
├── DismissedLeaders (List<BaseUnitLeader>)  // Fired units
├── AwaitingBurial (List<BaseUnitLeader>)    // Pending burial ceremony
└── LeaderLookup (Dictionary<Template, Leader>)

BaseUnitLeader
├── UnitLeaderTemplate                        // Definition
├── UnitLeaderAttributes                      // Stats (accuracy, etc.)
├── SkillContainer                            // Skills
├── ItemContainer                             // Equipment
├── List<PerkTemplate>                        // Earned perks
├── UnitStatistics                            // Combat stats tracking
├── EmotionalStates                           // Morale/emotions
└── List<Squaddie>                            // Squad members (SquadLeader only)

Squaddie
├── Name, Gender, SkinColor                   // Identity
├── HomePlanetType                            // Background
└── EntityTemplate                            // Unit type
```

## Roster Class

### Roster Field Layout

```c
public class Roster {
    // Object header                          // +0x00 - 0x0F
    List<BaseUnitLeader> HiredLeaders;        // +0x10 (active player units)
    List<UnitLeaderTemplate> AvailableForHire; // +0x18 (recruitable templates)
    List<BaseUnitLeader> DeadLeaders;         // +0x20 (KIA units)
    List<BaseUnitLeader> DismissedLeaders;    // +0x28 (fired units)
    List<BaseUnitLeader> AwaitingBurial;      // +0x30 (pending burial)
    Dictionary<UnitLeaderTemplate,BaseUnitLeader> LeaderLookup;  // +0x38
    UnitLeaderTemplate SoldierTemplate;       // +0x40 (default infantry template, Type=0)
    UnitLeaderTemplate VehicleTemplate;       // +0x48 (default vehicle template, Type=1)
}
```

### Key Roster Methods

```c
// Constructor
void Roster.ctor();                           // @ 1805a4c60

// Unit creation
BaseUnitLeader CreateUnitLeader(UnitLeaderTemplate template);  // @ 1805a21a0
void AddHirableLeader(UnitLeaderTemplate template);            // @ 1805a1f30

// Hiring/firing
void HireLeader(BaseUnitLeader leader);       // @ 1805a3d80
bool TryDismissLeader(BaseUnitLeader leader); // @ 1805a4b20

// Death handling
void OnPermanentDeath(BaseUnitLeader leader); // @ 1805a4310
bool IsPermanentlyDead(UnitLeaderTemplate template);  // @ 1805a4190
bool TryBuryNextLeader();                     // @ 1805a49d0

// Queries
int GetCount();                               // @ 1805a3000
int GetAvailableUnits();                      // @ 1805a2ed0
int GetAvailableUnitsOfType(UnitLeaderType type);  // @ 1805a2d20
BaseUnitLeader GetLeaderByTemplate(UnitLeaderTemplate template, out LeaderStatus status);  // @ 1805a3520
BaseUnitLeader GetHiredLeaderBySpeakerTemplate(SpeakerTemplate speaker);  // @ 1805a3100
BaseUnitLeader GetTempLeader(UnitLeaderTemplate template);  // @ 1805a3b40
bool HasAliveAvailableLeader();               // @ 1805a3c10

// Item queries
List<BaseUnitLeader> GetItemUsers(BaseItemTemplate item);  // @ 1805a32c0

// Iteration
void ForEachHiredLeader(Action<BaseUnitLeader> callback);  // @ 1805a26a0
void ForEachLeader(Action<BaseUnitLeader> callback);       // @ 1805a2850

// Events
void OnOperationFinished();                   // @ 1805a4210

// Persistence
void ProcessSaveState(SaveState state);       // @ 1805a4930
void Init();                                  // @ 1805a4080
```

## BaseUnitLeader Class

Base class for all unit leaders (soldiers and vehicles).

### BaseUnitLeader Field Layout

```c
public class BaseUnitLeader {
    // Object header                          // +0x00 - 0x0F
    UnitLeaderTemplate Template;              // +0x10 (unit definition)
    // padding                                // +0x18
    EntityTemplate OverrideTemplate;          // +0x20 (optional override)
    // padding                                // +0x28
    UnitLeaderAttributes Attributes;          // +0x30 (stats like accuracy)
    SkillContainer Skills;                    // +0x38 (available skills)
    ItemContainer Items;                      // +0x40 (equipped items)
    List<PerkTemplate> Perks;                 // +0x48 (earned perks)
    UnitStatistics Statistics;                // +0x50 (combat tracking)
    EmotionalStates Emotions;                 // +0x58 (morale system)
    List<int> SomeIntList;                    // +0x60 (unknown purpose)
    // padding                                // +0x68
    StrategicDuration UnavailableDuration;    // +0x68 (injury recovery time)
    // padding                                // +0x78
    int SomeFlag;                             // +0x78 (cleared on mission finish)
}
```

### Key BaseUnitLeader Methods

```c
// Constructor
void BaseUnitLeader.ctor(UnitLeaderTemplate template, ItemContainerConfig config);  // @ 1805b2c20

// Perks
void AddPerk(PerkTemplate perk, bool payResources);  // @ 1805affe0
bool HasPerk(PerkTemplate perk);              // @ 1805b19d0
PerkTemplate GetPerkByRank(int rank);         // @ 1805b15c0
PerkTemplate GetLastPerk();                   // @ 1805b14e0
int GetPerkCount();                           // @ 1805b1640

// Rank/promotion
int GetRank();                                // @ 1805b1760
RankTemplate GetRankTemplate();               // @ 1805b16e0
int GetPromotionCost();                       // @ 1805b1680
bool CanBePromoted();                         // @ 1805b06d0
bool CanBeDemoted();                          // @ 1805b0680
int GetDemoteRefundAmount();                  // @ 1805b07a0
int GetFullDemotePromotionRefund();           // @ 1805b0fe0

// Properties
int GetGrowthPotential();                     // @ 1805b1220
float GetHitpointsPct();                      // @ 1805b13e0
int GetCurrentElements();                     // @ 1805b0760
int GetMaxElements();                         // @ 1805b1570
EntityProperty GetEntityProperty(PropertyType type);  // @ 1805b0fc0
int GetItemCountPerUnit(int itemType);        // @ 1805b1440

// Status
LeaderStatus GetStatus();                     // @ 1805b1910
bool IsDeployable();                          // @ 1805b1c60
bool IsUnavailable();                         // @ 1805b1d70
bool HasInjuredState();                       // @ 1805b1980
bool IsBleedingOut();                         // @ 1805b1c50

// Deployment
int GetDeployCosts();                         // @ 1805b08a0

// Display
string GetNickname();                         // @ 1805b1590
Sprite GetStandingImage();                    // @ 1805b1870
TooltipData AppendTooltipData(...);           // @ 1805b0320

// Tags
bool HasTag(string tag);                      // @ 1805b1b90
bool HasItemWithTag(string tag);              // @ 1805b19b0

// Events
void OnMissionStarted();                      // @ 1805b2010
void OnMissionFinished();                     // @ 1805b1f30
void OnOperationFinished();                   // @ 1805b2250
void OnDismiss();                             // @ 1805b1de0

// Squad members (SquadLeader subclass)
Squaddie GetSquaddie(int index);              // @ 1805b1790
bool HasSquaddie(Squaddie squaddie);          // @ 1805b1b30
Skill GetLastSkillUsed();                     // @ 1805b1550
Tile GetLastTileAttacked();                   // @ 1805b1560

// Conversation
ConversationEntityType GetConversationEntityType();  // @ 1805b0730
SpeakerTemplate GetSpeakerTemplate();         // @ 1805a0880
```

## Squaddie Class

Represents individual soldiers within a squad.

### Squaddie Field Layout

```c
public class Squaddie {
    // Object header                          // +0x00 - 0x0F
    int NameSeed;                             // +0x10 (random seed for name generation)
    Gender Gender;                            // +0x14 (byte enum)
    SkinColor SkinColor;                      // +0x15 (byte enum)
    HomePlanetType HomePlanet;                // +0x18 (int enum)
    // padding                                // +0x1C
    EntityTemplate Template;                  // +0x20 (unit type override)
    string FirstName;                         // +0x28 (generated or custom)
    string LastName;                          // +0x30 (generated or custom)
    int SomeInt;                              // +0x38 (unknown)
}
```

### Key Squaddie Methods

```c
// Constructor
void Squaddie.ctor(int nameSeed, int homePlanet, Gender gender, SkinColor skin, string firstName, string lastName);  // @ 1805c2660

// Queries
BaseUnitLeader GetCurrentLeader();            // @ 1805c1bd0
string GetHomePlanetName();                   // @ 1805c1e60
TooltipData GetTooltipData();                 // @ 1805c1e80
string ToString();                            // @ 1805c2230

// Persistence
void ProcessSaveState(SaveState state);       // @ 1805c2130
```

## Save State Serialization

### Roster Save Order

```
ProcessSaveState order:
1. HiredLeaders list      @ +0x10 (via ProcessLeaderList)
2. DeadLeaders list       @ +0x20 (via ProcessLeaderList)
3. DismissedLeaders list  @ +0x28 (via ProcessLeaderList)
4. AwaitingBurial list    @ +0x30 (via ProcessLeaderList)
5. AvailableForHire       @ +0x18 (DataTemplates list)
```

### Squaddie Save Order

```
ProcessSaveState order:
1. NameSeed               @ +0x10 (int)
2. Gender                 @ +0x14 (Gender enum)
3. SkinColor              @ +0x15 (SkinColor enum)
4. HomePlanet             @ +0x18 (HomePlanetType enum)
5. FirstName              @ +0x28 (string)
6. LastName               @ +0x30 (string)
7. SomeInt                @ +0x38 (int)
8. Template               @ +0x20 (EntityTemplate)
```

## Deployment Cost Calculation

```c
// @ 1805b08a0
int GetDeployCosts() {
    EntityTemplate template = GetTemplate();  // virtual call
    if (template == null) return 0;

    int baseCost = template.DeployCost;       // +0xD4
    int perkCount = Perks.Count;              // +0x48

    // Check if has free first perk
    EntityTemplate baseTemplate = Template.BaseTemplate;  // +0x168
    int effectivePerkCount = (baseTemplate != null) ? perkCount - 1 : perkCount;

    int perkCost = effectivePerkCount * template.PerkDeployCost;  // +0xD8

    int total = baseCost + perkCost;

    // SquadLeader adds per-squaddie cost
    if (this is SquadLeader squadLeader) {
        int squaddieCount = squadLeader.Squaddies.Count;  // +0x60
        total += squaddieCount * template.SquaddieDeployCost;  // +0xDC
    }

    // Apply skill modifiers
    Skills.CollectHandlersOfType<ISkillChangeSupplyCosts>();
    // ... item cost calculation with modifiers ...

    // Apply property multiplier
    float mult = EntityProperties.GetPropertyValue(PropertyType.DeployCostMult);  // 0x40
    total = (int)(total * mult);

    // Apply operation modifier
    Operation op = StrategyState.GetCurrentOperation();
    if (op != null) {
        float opMult = GetOperationDeployCostMult(op.Properties);
        total = (int)(total * opMult);
    }

    return total;
}
```

## Mission Lifecycle

### OnMissionStarted

Called when unit is deployed to a mission.

```c
void OnMissionStarted() {
    // Setup for tactical combat
}
```

### OnMissionFinished

Called after mission completion.

```c
// @ 1805b1f30
void OnMissionFinished() {
    SomeFlag = 0;  // +0x78

    // Update unavailable duration (injury recovery)
    StrategicDuration.OnMissionFinished();  // +0x68

    // Reset skill/item container owners
    Skills.SetOwner(this);       // +0x38
    Items.Owner = this;          // +0x40

    // Roll for attribute increases
    int growthPotential = GetGrowthPotential();
    PseudoRandom random = TacticalManager.Instance.Random;
    Attributes.RollForAttributeIncreases(growthPotential, random);  // +0x30

    // Virtual call for subclass handling
    OnMissionFinishedVirtual();
}
```

## Perk System

### AddPerk

```c
// @ 1805affe0
void AddPerk(PerkTemplate perk, bool payResources) {
    if (perk == null) return;

    if (payResources) {
        RankTemplate rank = GetRankTemplate();
        int cost = rank.PerkCost;              // +0x90

        // Apply discount multiplier
        float discount = Template.PerkCostDiscount;  // +0xF0
        cost = (int)(cost * Clamp(discount));

        StrategyState.ChangeVar(VarType.Resources, -cost);
    }

    // Add to perk list
    Perks.Add(perk);  // +0x48

    // Create and add associated skill
    Skill skill = perk.CreateSkill();
    Skills.Add(skill);  // +0x38
}
```

## LeaderStatus Enum

```c
enum LeaderStatus {
    Hired = 0,
    AvailableForHire = 1,
    Dead = 2,
    Dismissed = 3,
    AwaitingBurial = 4
}
```

## UnitLeaderType Enum

```c
enum UnitLeaderType {
    Soldier = 0,
    Vehicle = 1
}
```

## Modding Hooks

### Modify Deployment Costs

```csharp
[HarmonyPatch(typeof(BaseUnitLeader), "GetDeployCosts")]
class DeployCostPatch {
    static void Postfix(ref int __result) {
        // Halve all deployment costs
        __result /= 2;
    }
}
```

### Custom Perk Logic

```csharp
[HarmonyPatch(typeof(BaseUnitLeader), "AddPerk")]
class PerkPatch {
    static void Postfix(BaseUnitLeader __instance, PerkTemplate perk) {
        Logger.Msg($"{__instance.GetNickname()} gained perk: {perk.Name}");
    }
}
```

### Intercept Mission Finish

```csharp
[HarmonyPatch(typeof(BaseUnitLeader), "OnMissionFinished")]
class MissionFinishPatch {
    static void Postfix(BaseUnitLeader __instance) {
        // Grant bonus XP or healing
        Logger.Msg($"{__instance.GetNickname()} finished mission");
    }
}
```

### Modify Hiring

```csharp
[HarmonyPatch(typeof(Roster), "HireLeader")]
class HirePatch {
    static void Prefix(BaseUnitLeader leader) {
        Logger.Msg($"Hiring: {leader.Template.Name}");
    }
}
```

## Key Constants

```c
// UnitLeaderTemplate.Type values
const int TYPE_SOLDIER = 0;
const int TYPE_VEHICLE = 1;

// Default roster access
// Roster is at StrategyState+0x70
```

## Related Classes

- **UnitLeaderTemplate**: Definition of unit leader types
- **RankTemplate**: Promotion ranks with costs and bonuses
- **PerkTemplate**: Unlockable abilities/bonuses
- **UnitLeaderAttributes**: Stat bonuses (accuracy, damage, etc.)
- **EmotionalStates**: Morale and emotional modifiers
- **UnitStatistics**: Combat performance tracking
- **ItemContainer**: Equipment management
- **SkillContainer**: Skill management
