# ArmyGeneration

`Menace.SDK.ArmyGeneration` -- Static class for army generation operations including army creation, budget management, and unit selection.

## Overview

The ArmyGeneration SDK provides safe access to the game's army creation system. Use this to query army templates, inspect army compositions, check entity costs, and validate template availability based on game progress and budget.

## Internal Field Mappings

The SDK maps to these internal game fields:
- `Army.m_Budget` - Total budget for the army
- `Army.m_Entries` - List of army entries
- `ArmyEntry.EntityTemplate` - The entity template for this entry
- `ArmyEntry.m_Amount` - Count of units for this entry
- `EntityTemplate.ArmyPointCost` - Point cost for the entity
- `ArmyTemplate.PossibleUnits` - List of possible unit entries
- `ArmyTemplateEntry.Weight` - Selection weight (used for random unit selection)

## Constants

### Default Spawn Area

```csharp
public const byte DEFAULT_SPAWN_AREA = 3;
```

The default spawn area index used for army spawning.

## Methods

### GetArmyInfo

```csharp
public static ArmyInfo GetArmyInfo(GameObj army)
```

Get detailed information about an army object.

**Parameters:**
- `army` - The army object to inspect

**Returns:** `ArmyInfo` containing template name, budget information, unit counts, and all entries. Returns `null` if the army is invalid.

### GetEntryInfo

```csharp
public static ArmyEntryInfo GetEntryInfo(GameObj entry)
```

Get information about a single army entry (a unit type and its count within an army).

**Parameters:**
- `entry` - The army entry object to inspect

**Returns:** `ArmyEntryInfo` with template name, count, and cost information. Returns `null` if the entry is invalid.

### IsTemplatePickable

```csharp
public static bool IsTemplatePickable(GameObj template, int progress, int budget)
```

Check if an army template is available for selection at the current game progress and budget.

**Parameters:**
- `template` - The army template to check
- `progress` - Current game progress value
- `budget` - Available budget points

**Returns:** `true` if the template can be picked, `false` otherwise.

### GetEntityCost

```csharp
public static int GetEntityCost(GameObj entityTemplate)
```

Get the point cost for an entity template.

**Parameters:**
- `entityTemplate` - The entity template to check

**Returns:** The cost in points, or `0` if not found.

### GetArmyTemplates

```csharp
public static List<string> GetArmyTemplates()
```

Get a sorted list of all available army template names in the game.

**Returns:** A `List<string>` of army template names, sorted alphabetically.

## Types

### ArmyInfo

Information structure for a complete army.

```csharp
public class ArmyInfo
{
    public string TemplateName { get; set; }    // Name of the army template
    public int TotalBudget { get; set; }        // Maximum budget for this army
    public int UsedBudget { get; set; }         // Budget spent on units
    public int UnitCount { get; set; }          // Total number of individual units
    public int EntryCount { get; set; }         // Number of distinct unit types
    public List<ArmyEntryInfo> Entries { get; set; }  // List of unit entries
    public IntPtr Pointer { get; set; }         // Native pointer to the army object
}
```

### ArmyEntryInfo

Information structure for a single unit type entry within an army.

```csharp
public class ArmyEntryInfo
{
    public string TemplateName { get; set; }    // Name of the entity template
    public int Count { get; set; }              // Number of this unit type (or Weight for template entries)
    public int Cost { get; set; }               // Cost per individual unit (ArmyPointCost)
    public int TotalCost { get; set; }          // Total cost (Cost * Count)
    public IntPtr Pointer { get; set; }         // Native pointer to the entry object
}
```

**Note:** When inspecting `ArmyTemplateEntry` objects (via `GetArmyTemplateEntries`), the `Count` field contains the selection `Weight` used for random unit selection, not the actual unit count.

## Examples

### Listing all army templates

```csharp
var templates = ArmyGeneration.GetArmyTemplates();
DevConsole.Log($"Found {templates.Count} army templates:");
foreach (var name in templates)
{
    DevConsole.Log($"  {name}");
}
```

### Inspecting an army composition

```csharp
var army = GameQuery.FindByName("Army", "EnemyArmy");
var info = ArmyGeneration.GetArmyInfo(army);
if (info != null)
{
    DevConsole.Log($"Army: {info.TemplateName}");
    DevConsole.Log($"Budget: {info.UsedBudget}/{info.TotalBudget} points");
    DevConsole.Log($"Units: {info.UnitCount} total across {info.EntryCount} types");

    foreach (var entry in info.Entries)
    {
        DevConsole.Log($"  {entry.TemplateName} x{entry.Count} ({entry.TotalCost} pts)");
    }
}
```

### Checking entity costs

```csharp
var template = GameQuery.FindByName("EntityTemplate", "HeavyInfantry");
var cost = ArmyGeneration.GetEntityCost(template);
DevConsole.Log($"HeavyInfantry costs {cost} points");
```

### Validating template availability

```csharp
var template = GameQuery.FindByName("ArmyTemplate", "EliteSquad");
int currentProgress = 50;
int availableBudget = 1000;

if (ArmyGeneration.IsTemplatePickable(template, currentProgress, availableBudget))
{
    DevConsole.Log("EliteSquad is available for selection");
}
else
{
    DevConsole.Log("EliteSquad is not available (progress or budget too low)");
}
```

### Calculating remaining budget

```csharp
var army = GameQuery.FindByName("Army", "PlayerArmy");
var info = ArmyGeneration.GetArmyInfo(army);
if (info != null)
{
    int remaining = info.TotalBudget - info.UsedBudget;
    DevConsole.Log($"Remaining budget: {remaining} points");

    // Check if we can afford another unit
    var heavyTemplate = GameQuery.FindByName("EntityTemplate", "HeavyInfantry");
    int heavyCost = ArmyGeneration.GetEntityCost(heavyTemplate);

    if (heavyCost <= remaining)
    {
        DevConsole.Log($"Can afford HeavyInfantry ({heavyCost} pts)");
    }
}
```

## Console Commands

The following console commands are registered by `RegisterConsoleCommands()`:

- `armytemplates` - List all available army templates (shows first 20, with count of remaining)
- `entitycost <name>` - Get the point cost for an entity template by name

### Command Examples

```
> armytemplates
Army Templates (15):
  AssaultSquad
  DefenseForce
  EliteSquad
  HeavySupport
  ...

> entitycost HeavyInfantry
Entity: HeavyInfantry
Cost: 150 points
```
