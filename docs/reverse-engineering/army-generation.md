# Army Generation System

## Overview

The Army generation system creates enemy forces for tactical missions using a budget-based weighted random selection algorithm. It selects units from ArmyTemplate definitions based on available budget points and campaign progress.

## Architecture

```
Army Generation Flow
├── MissionArmy (weighted template selection)
│   └── ArmyTemplate (unit pool)
│       └── ArmyTemplateEntry[] (unit definitions)
│           └── EntityTemplate + cost + weight
│
├── Army (generated result)
│   └── ArmyEntry[] (selected units)
│       └── EntityTemplate + count
│
└── PossibleArmyEntry (runtime selection state)
    └── Weight tracking for repeated picks
```

## Army Class

### Army Field Layout

```c
public class Army {
    // Object header                      // +0x00 - 0x0F
    ArmyTemplate Template;                // +0x10 (source template)
    List<ArmyEntry> Entries;              // +0x18 (selected units)
    int TotalBudget;                      // +0x20 (original budget)
    RectInt[] SpawnAreas;                 // +0x28 (spawn region array)
    byte SpawnAreaIndex;                  // +0x30 (current spawn area, init: 3)
}
```

### Key Army Methods

```c
// Constructor
void Army.ctor(ArmyTemplate template, int budget);  // @ 180564740

// Generation
static Army CreateArmy(PseudoRandom random, ArmyTemplate template, int budget, int progress);  // @ 180563450
static Army GetRandomArmy(PseudoRandom random, List<MissionArmy> armies, int budget, int progress);  // @ 180564060

// Queries
int GetConversationActorTypeCount(ConversationActorType type);  // @ 180563c00
int GetEntriesWithTagCount(string tag);   // @ 180563dc0

// Spawn configuration
void SetSpawnAreas(RectInt[] areas);      // @ 1805642e0

// Debug
string ToString();                        // @ 180564310
```

## ArmyEntry Class

Represents a selected unit type with count.

### ArmyEntry Field Layout

```c
public class ArmyEntry {
    // Object header                      // +0x00 - 0x0F
    EntityTemplate Template;              // +0x10 (unit template)
    int Count;                            // +0x18 (number to spawn, init: 1)
}

// Constructor @ 180563410
void ArmyEntry.ctor(EntityTemplate template) {
    this.Template = template;  // +0x10
    this.Count = 1;            // +0x18
}
```

### GetCost Method

```c
// @ 1805633d0
int GetCost() {
    return Template.Cost * Count;  // Template+0xAC * Count
}
```

## ArmyTemplate Class

Defines the pool of possible units for army generation.

### ArmyTemplate Key Fields

```c
public class ArmyTemplate : DataTemplate {
    // DataTemplate header
    Vector2Int ProgressRange;             // +0x88 (min/max campaign progress)
    List<ArmyTemplateEntry> Entries;      // +0x90 (possible units)
}
```

### IsPickable Method

Determines if this template is valid for current mission context.

```c
// @ 18057f000
bool IsPickable(int progress, int budget) {
    // Check progress range
    int minProgress = ProgressRange.x;    // +0x88
    if (progress < minProgress) return false;

    int maxProgress = GetMax(ProgressRange);  // +0x88
    if (progress > maxProgress) return false;

    // Check entries exist
    if (Entries == null || Entries.Count < 1) return false;

    // Check at least one entry is affordable
    int affordableCount = 0;
    foreach (ArmyTemplateEntry entry in Entries) {
        if (entry.Template == null) continue;

        // Check entry progress range
        int entryMin = entry.Template.ProgressRange.x;  // Template+0xB0
        if (progress < entryMin) continue;

        int entryMax = GetMax(entry.Template.ProgressRange);
        if (progress > entryMax) continue;

        // Check affordability
        int cost = entry.Template.Cost;  // Template+0xAC
        if (cost <= budget) {
            affordableCount++;
        }
    }

    return affordableCount > 0;
}
```

## ArmyTemplateEntry Class

Defines a single unit option within an ArmyTemplate.

```c
public class ArmyTemplateEntry {
    // Object header                      // +0x00 - 0x0F
    EntityTemplate Template;              // +0x10 (unit to spawn)
    int Weight;                           // +0x14 (selection weight)
    // Additional fields for cost/progress constraints
}
```

### EntityTemplate Cost/Progress Fields

```c
// Relevant EntityTemplate fields for army generation
public class EntityTemplate {
    // ... other fields ...
    int Cost;                             // +0xAC (budget cost)
    Vector2Int ProgressRange;             // +0xB0 (valid progress range)
}
```

## PossibleArmyEntry Class

Runtime wrapper for weighted selection with depletion.

### PossibleArmyEntry Field Layout

```c
public class PossibleArmyEntry {
    // Object header                      // +0x00 - 0x0F
    EntityTemplate Template;              // +0x10 (source template)
    float CurrentWeight;                  // +0x18 (current selection weight)
    float WeightMultiplier;               // +0x1C (decay on pick)
}
```

### Weight Methods

```c
// @ 1805905f0
int GetWeight() {
    return (int)CurrentWeight;  // +0x18
}

// @ 180590600
void OnPicked() {
    // Reduce weight for next pick (discourages duplicates)
    CurrentWeight = CurrentWeight * WeightMultiplier;  // +0x18 * +0x1C
}
```

## CreateArmy Algorithm

Main army generation algorithm using budget-based weighted selection.

```c
// @ 180563450
static Army CreateArmy(PseudoRandom random, ArmyTemplate template, int budget, int progress) {
    List<PossibleArmyEntry> possibleEntries = new List<PossibleArmyEntry>();

    // 1. Build list of valid entries
    foreach (ArmyTemplateEntry entry in template.Entries) {  // +0x90
        if (entry.Weight < 1) continue;
        if (entry.Template == null) continue;

        // Check progress range
        int minProgress = entry.Template.ProgressRange.x;  // +0xB0
        int maxProgress = GetMax(entry.Template.ProgressRange);
        if (progress < minProgress || progress > maxProgress) continue;

        // Check affordability
        int cost = entry.Template.Cost;  // +0xAC
        if (cost > budget) continue;

        // Apply SwapArmyListEntryEffect (game effects can swap units)
        EntityTemplate actualTemplate = entry.Template;
        StrategyState.ForEachActiveGameEffect<SwapArmyListEntryEffect>((effect) => {
            // Effect may replace template
            actualTemplate = effect.GetSwappedTemplate(actualTemplate);
        });

        possibleEntries.Add(new PossibleArmyEntry(actualTemplate));
    }

    // 2. Create army
    Army army = new Army(template, budget);
    int remainingBudget = budget;

    // 3. Fill army with weighted random selection
    while (remainingBudget > 0) {
        PossibleArmyEntry selected;
        if (!TryGetNextWeightedRandom(possibleEntries, random, out selected)) {
            break;  // No more valid entries
        }

        int cost = selected.Template.Cost;  // +0xAC
        if (cost > remainingBudget) {
            // Can't afford, remove from pool
            possibleEntries.Remove(selected);
            continue;
        }

        // Update weight for next pick
        selected.OnPicked();

        // Check if weight depleted
        if (selected.GetWeight() < 1) {
            possibleEntries.Remove(selected);
        }

        // Add to army (increment count if already present)
        bool found = false;
        foreach (ArmyEntry entry in army.Entries) {
            if (entry.Template == selected.Template) {
                entry.Count++;  // +0x18
                found = true;
                break;
            }
        }

        if (!found) {
            army.Entries.Add(new ArmyEntry(selected.Template));
        }

        remainingBudget -= cost;
    }

    return army;
}
```

## GetRandomArmy Algorithm

Selects from multiple MissionArmy options.

```c
// @ 180564060
static Army GetRandomArmy(PseudoRandom random, List<MissionArmy> armies, int budget, int progress) {
    if (armies == null) return null;

    // Filter to pickable armies
    List<MissionArmy> validArmies = new List<MissionArmy>();
    foreach (MissionArmy ma in armies) {
        if (ma.Weight < 1) continue;
        if (ma.Template == null) continue;
        if (ma.Template.IsPickable(progress, budget)) {
            validArmies.Add(ma);
        }
    }

    if (validArmies.Count < 1) return null;

    // Weighted random selection
    MissionArmy selected = WeightedRandom(validArmies, random);
    if (selected == null) return null;

    // Generate army from selected template
    return CreateArmy(random, selected.Template, budget, progress);
}
```

## MissionArmy Class

Associates an ArmyTemplate with a weight for mission-level selection.

```c
public class MissionArmy {
    // Object header                      // +0x00 - 0x0F
    ArmyTemplate Template;                // +0x10 (army template)
    int Weight;                           // +0x18 (selection weight)
}
```

## Budget and Progress Scaling

### Budget Calculation

Budget typically comes from mission difficulty settings:

```c
// From MissionDifficultyTemplate
int baseBudget = difficultyTemplate.EnemyArmyPoints;

// Multipliers may apply
float mult = mission.GetEnemyArmyPointsMult();
int finalBudget = (int)(baseBudget * mult);
```

### Progress Filtering

Campaign progress gates unit availability:

```c
// Unit available when:
//   ProgressRange.x <= currentProgress <= ProgressRange.y

// Early game (progress 0-5): Basic infantry, light vehicles
// Mid game (progress 6-15): Heavy weapons, APCs, tanks
// Late game (progress 16+): Elite units, heavy armor
```

## Spawn Area Assignment

After generation, spawn areas define where units appear on the map.

```c
// @ 1805642e0
void SetSpawnAreas(RectInt[] areas) {
    this.SpawnAreas = areas;  // +0x28
}

// SpawnAreaIndex (+0x30) tracks distribution across areas
```

## Modding Hooks

### Modify Army Budget

```csharp
[HarmonyPatch(typeof(Army), "CreateArmy")]
class BudgetPatch {
    static void Prefix(ref int budget) {
        // Increase enemy army budget by 50%
        budget = (int)(budget * 1.5f);
    }
}
```

### Custom Unit Selection

```csharp
[HarmonyPatch(typeof(ArmyTemplate), "IsPickable")]
class PickablePatch {
    static void Postfix(ArmyTemplate __instance, int progress, int budget, ref bool __result) {
        // Force specific template to always be pickable
        if (__instance.Name == "EliteSquad") {
            __result = true;
        }
    }
}
```

### Override Weight Calculation

```csharp
[HarmonyPatch(typeof(PossibleArmyEntry), "OnPicked")]
class WeightPatch {
    static bool Prefix(PossibleArmyEntry __instance) {
        // Prevent weight reduction (allow more duplicates)
        return false;
    }
}
```

### Add Custom Units to Army

```csharp
[HarmonyPatch(typeof(Army), "CreateArmy")]
class AddUnitsPatch {
    static void Postfix(Army __result, int budget) {
        // Always add a sniper to enemy armies
        var sniperTemplate = DataTemplateLoader.Get<EntityTemplate>("Sniper");
        if (sniperTemplate != null) {
            __result.Entries.Add(new ArmyEntry(sniperTemplate));
        }
    }
}
```

## Key Constants

```c
// Default spawn area index
const byte DEFAULT_SPAWN_AREA = 3;

// Minimum weight threshold
const int MIN_WEIGHT = 1;

// Progress range encoding
// ProgressRange.x = minimum progress
// ProgressRange.y = maximum progress (use GetMax for proper extraction)
```

## Related Classes

- **EntityTemplate**: Unit definition with cost and stats
- **MissionDifficultyTemplate**: Provides base army budget
- **Mission**: Holds armies dictionary by faction
- **DataTemplateLoader**: Loads army/entity templates
- **SwapArmyListEntryEffect**: Game effect that can replace units
