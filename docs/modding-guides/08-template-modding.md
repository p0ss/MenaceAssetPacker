# Template Modding via SDK

Data patches are great for static changes, but sometimes you need dynamic control. The Templates API lets you read, write, and clone game templates programmatically at runtime.

> [!NOTE]
> This guide provides in-depth template modification patterns. For initial setup, see
> [Getting Started: Your First Plugin](../coding-sdk/getting-started.md). For API reference, see [Templates API](../coding-sdk/api/templates.md).

## Why Use Code for Templates?

Code-based template modification excels when you need:

- **Conditional logic** - Modify templates based on game state, player choices, or mod settings
- **Runtime changes** - Adjust values during gameplay (e.g., difficulty scaling)
- **Complex modifications** - Changes that depend on multiple factors or calculations
- **Dynamic cloning** - Create template variants on-the-fly based on conditions
- **Cross-template coordination** - Modify multiple templates in relation to each other

If your changes are static (always the same values), use data patches instead. They're simpler and don't require code.

## Templates API Overview

The `Templates` class provides these core operations:

| Method | Purpose |
|--------|---------|
| `Find(type, name)` | Find a specific template by type and name |
| `FindAll(type)` | Get all templates of a given type |
| `Exists(type, name)` | Check if a template exists |
| `ReadField(template, field)` | Read a field value |
| `WriteField(template, field, value)` | Write a field value |
| `WriteFields(template, dict)` | Write multiple fields at once |
| `Clone(type, source, newName)` | Create a copy of a template |

Templates are ScriptableObjects - Unity's data containers. Common template types include:

- `WeaponTemplate` - Guns, grenades, melee weapons
- `EntityTemplate` - Units, enemies, buildings
- `ArmorTemplate` - Body armor, equipment slots
- `SkillTemplate` - Unit abilities and skills
- `BaseItemTemplate` - Accessories, consumables
- `UnitLeaderTemplate` - Squad leaders and pilots

## Reading Template Data

Use `Templates.Find()` to get a template, then `ReadField()` to read values:

```csharp
using Menace.SDK;

// Find a specific weapon template
var rifle = Templates.Find("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");

if (!rifle.IsNull)
{
    // Read individual fields
    float damage = (float)Templates.ReadField(rifle, "Damage");
    int maxRange = (int)Templates.ReadField(rifle, "MaxRange");
    float accuracy = (float)Templates.ReadField(rifle, "AccuracyBonus");

    DevConsole.Log($"ARC-762: {damage} dmg, {maxRange} range, {accuracy} accuracy");
}
```

### Nested Field Access

Use dot notation for nested properties:

```csharp
// Read nested fields
var weapon = Templates.Find("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");
var armorPen = Templates.ReadField(weapon, "ArmorPenetration");
```

## Writing Template Fields

Use `WriteField()` to modify template values:

```csharp
using Menace.SDK;

var rifle = Templates.Find("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");

// Single field write
Templates.WriteField(rifle, "Damage", 15.0f);
Templates.WriteField(rifle, "MaxRange", 9);
Templates.WriteField(rifle, "AccuracyBonus", 5.0f);
```

### Batch Writing

For multiple changes, use `WriteFields()` with a dictionary:

```csharp
using Menace.SDK;
using System.Collections.Generic;

var rifle = Templates.Find("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");

// Write multiple fields at once
int fieldsWritten = Templates.WriteFields(rifle, new Dictionary<string, object>
{
    { "Damage", 15.0f },
    { "MaxRange", 9 },
    { "AccuracyBonus", 5.0f },
    { "ArmorPenetration", 10.0f }
});

DevConsole.Log($"Modified {fieldsWritten} fields on ARC-762");
```

## Cloning Templates

Create new template variants with `Templates.Clone()`:

```csharp
using Menace.SDK;

// Clone an existing template
var heavyRifle = Templates.Clone("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762", "weapon.custom_heavy_rifle");

if (!heavyRifle.IsNull)
{
    // Customize the clone
    Templates.WriteField(heavyRifle, "Damage", 20.0f);
    Templates.WriteField(heavyRifle, "ArmorPenetration", 25.0f);
    Templates.WriteField(heavyRifle, "AccuracyBonus", -5.0f);

    DevConsole.Log("Created weapon.custom_heavy_rifle template");
}
```

### Clone Considerations

- Clones exist only at runtime - they're not saved to disk
- Clone names must be unique within their type
- Clones inherit all values from the source template
- The game's spawning systems can use cloned templates by name

## Finding Templates by Type and Name

### Find a Specific Template

```csharp
// Find by exact name
var template = Templates.Find("WeaponTemplate", "weapon.generic_dmr_tier_1_longshot");

if (template.IsNull)
{
    DevConsole.LogWarning("DMR template not found!");
    return;
}
```

### Check if Template Exists

```csharp
// Check existence before operations
if (Templates.Exists("WeaponTemplate", "weapon.generic_combat_shotgun_tier_1_cs185"))
{
    var shotgun = Templates.Find("WeaponTemplate", "weapon.generic_combat_shotgun_tier_1_cs185");
    // ... modify it
}
```

## Iterating All Templates of a Type

Use `Templates.FindAll()` to get all templates of a type:

```csharp
using Menace.SDK;

// Get all weapon templates
var allWeapons = Templates.FindAll("WeaponTemplate");

DevConsole.Log($"Found {allWeapons.Length} weapon templates");

foreach (var weapon in allWeapons)
{
    string name = weapon.GetName();
    float damage = (float)Templates.ReadField(weapon, "Damage");
    DevConsole.Log($"  {name}: {damage} damage");
}
```

### Filtering Templates

Combine with LINQ or manual filtering:

```csharp
using Menace.SDK;
using System.Linq;

// Find all high-damage weapons
var allWeapons = Templates.FindAll("WeaponTemplate");
var heavyWeapons = allWeapons.Where(w =>
{
    var damage = Templates.ReadField(w, "Damage");
    return damage != null && (float)damage >= 15.0f;
}).ToArray();

DevConsole.Log($"Found {heavyWeapons.Length} heavy weapons");
```

## Example: Dynamic Difficulty Scaling

This example scales weapon damage based on mission progress:

```csharp
using MelonLoader;
using HarmonyLib;
using Menace.ModpackLoader;
using Menace.SDK;

namespace DynamicDifficulty;

public class DifficultyScaler : IModpackPlugin
{
    private MelonLogger.Instance _log;
    private int _missionsCompleted = 0;

    public void OnInitialize(MelonLogger.Instance logger, Harmony harmony)
    {
        _log = logger;
        GameState.TacticalReady += OnTacticalReady;
        DevConsole.Log("Dynamic difficulty active");
    }

    private void OnTacticalReady()
    {
        // Scale weapons based on missions completed
        float scaleFactor = 1.0f + (_missionsCompleted * 0.05f); // +5% per mission
        ScaleWeaponTemplates(scaleFactor);
        DevConsole.Log($"Difficulty scale: {scaleFactor:F2}x (Mission #{_missionsCompleted + 1})");
    }

    private void ScaleWeaponTemplates(float scale)
    {
        var allWeapons = Templates.FindAll("WeaponTemplate");

        foreach (var weapon in allWeapons)
        {
            var baseDamage = Templates.ReadField(weapon, "Damage");
            if (baseDamage != null)
            {
                float scaledDamage = (float)baseDamage * scale;
                Templates.WriteField(weapon, "Damage", scaledDamage);
            }
        }
    }

    public void OnSceneLoaded(int buildIndex, string sceneName) { }
    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```

## Best Practices

### When to Modify Templates

| Timing | Use Case | Method |
|--------|----------|--------|
| **OnInitialize** | One-time setup, applying mod settings | `IModpackPlugin.OnInitialize()` |
| **TacticalReady** | Per-mission scaling, difficulty adjustments | `GameState.TacticalReady` |
| **SceneLoaded** | React to scene transitions | `GameState.SceneLoaded` |

Avoid modifying templates in `OnUpdate()` - it runs every frame and will tank performance.

### Caching Templates

For frequently accessed templates, cache the `GameObj` reference after the game is ready:

```csharp
public class CachingExample : IModpackPlugin
{
    // Cache template references
    private GameObj _rifleTemplate;
    private GameObj _shotgunTemplate;

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        // Templates aren't available yet - wait for TacticalReady or SceneLoaded
        GameState.TacticalReady += CacheTemplates;
    }

    private void CacheTemplates()
    {
        // Look up once, use many times
        _rifleTemplate = Templates.Find("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");
        _shotgunTemplate = Templates.Find("WeaponTemplate", "weapon.generic_combat_shotgun_tier_1_cs185");
    }

    private void ModifyRifle()
    {
        // Use cached reference - no lookup cost
        if (!_rifleTemplate.IsNull)
        {
            Templates.WriteField(_rifleTemplate, "Damage", 15.0f);
        }
    }

    // ...
}
```

### Performance Tips

1. **Batch operations** - Use `WriteFields()` for multiple changes instead of individual `WriteField()` calls
2. **Cache lookups** - Store `GameObj` references instead of calling `Find()` repeatedly
3. **Avoid per-frame** - Never modify templates in `OnUpdate()`
4. **Early exit** - Check `IsNull` before operations to avoid wasted work
5. **Limit iteration** - When using `FindAll()`, filter early to reduce processing

### Error Handling

Template operations return useful values for error handling:

```csharp
// Find returns GameObj.Null if not found
var template = Templates.Find("WeaponTemplate", "weapon.nonexistent");
if (template.IsNull)
{
    DevConsole.LogWarning("Template not found");
    return;
}

// WriteField returns false on failure
bool success = Templates.WriteField(template, "InvalidField", 100);
if (!success)
{
    DevConsole.LogWarning("Failed to write field");
}

// WriteFields returns count of successful writes
int written = Templates.WriteFields(template, myFields);
if (written < myFields.Count)
{
    DevConsole.LogWarning($"Only {written}/{myFields.Count} fields written");
}
```

### Template Modification vs. Instance Modification

Understanding the difference is crucial:

- **Template modification** - Changes the *blueprint*. All future spawns use new values.
- **Instance modification** - Changes a *specific object* already in the scene.

```csharp
// This modifies the TEMPLATE - affects future spawns
var rifleTemplate = Templates.Find("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");
Templates.WriteField(rifleTemplate, "Damage", 20.0f);

// This modifies an INSTANCE - affects only this specific weapon
var liveWeapons = GameQuery.FindAll("WeaponTemplate");
foreach (var weapon in liveWeapons)
{
    weapon.WriteFloat("Damage", 20.0f); // Uses GameObj.WriteFloat, not Templates
}
```

---

**Previous:** [SDK Basics](07-sdk-basics.md) | **Next:** [UI Modifications](09-ui-modifications.md)

**See also:** [Templates API Reference](../coding-sdk/api/templates.md)
