# Template Modding via SDK

Data patches are great for static changes, but sometimes you need dynamic control. The Templates API lets you read, write, and clone game templates programmatically at runtime.

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

- `UnitTemplate` - Soldiers, aliens, vehicles
- `WeaponTemplate` - Guns, grenades, melee weapons
- `ArmorTemplate` - Body armor, helmets
- `ItemTemplate` - Consumables, equipment
- `AbilityTemplate` - Unit abilities and skills

## Reading Template Data

Use `Templates.Find()` to get a template, then `ReadField()` to read values:

```csharp
using Menace.SDK;

// Find a specific template
var marine = Templates.Find("UnitTemplate", "Marine");

if (!marine.IsNull)
{
    // Read individual fields
    int health = (int)Templates.ReadField(marine, "maxHealth");
    int armor = (int)Templates.ReadField(marine, "baseArmor");
    float accuracy = (float)Templates.ReadField(marine, "aimModifier");

    DevConsole.Log($"Marine: {health} HP, {armor} armor, {accuracy} aim");
}
```

### Nested Field Access

Use dot notation for nested properties:

```csharp
// Read nested fields
var weapon = Templates.Find("WeaponTemplate", "AssaultRifle");
var damageType = Templates.ReadField(weapon, "damageInfo.type");
var critMultiplier = Templates.ReadField(weapon, "damageInfo.criticalMultiplier");
```

## Writing Template Fields

Use `WriteField()` to modify template values:

```csharp
using Menace.SDK;

var rifleTemplate = Templates.Find("WeaponTemplate", "AssaultRifle");

// Single field write
Templates.WriteField(rifleTemplate, "damage", 15);
Templates.WriteField(rifleTemplate, "accuracy", 85);
Templates.WriteField(rifleTemplate, "clipSize", 30);
```

### Batch Writing

For multiple changes, use `WriteFields()` with a dictionary:

```csharp
using Menace.SDK;
using System.Collections.Generic;

var marine = Templates.Find("UnitTemplate", "Marine");

// Write multiple fields at once
int fieldsWritten = Templates.WriteFields(marine, new Dictionary<string, object>
{
    { "maxHealth", 120 },
    { "baseArmor", 2 },
    { "moveSpeed", 14 },
    { "aimModifier", 0.1f }
});

DevConsole.Log($"Modified {fieldsWritten} fields on Marine");
```

## Cloning Templates

Create new template variants with `Templates.Clone()`:

```csharp
using Menace.SDK;

// Clone an existing template
var eliteMarine = Templates.Clone("UnitTemplate", "Marine", "EliteMarine");

if (!eliteMarine.IsNull)
{
    // Customize the clone
    Templates.WriteField(eliteMarine, "maxHealth", 200);
    Templates.WriteField(eliteMarine, "baseArmor", 4);
    Templates.WriteField(eliteMarine, "displayName", "Elite Marine");

    DevConsole.Log("Created EliteMarine template");
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
var template = Templates.Find("WeaponTemplate", "PlasmaRifle");

if (template.IsNull)
{
    DevConsole.LogWarning("PlasmaRifle not found!");
    return;
}
```

### Check if Template Exists

```csharp
// Check existence before operations
if (Templates.Exists("UnitTemplate", "Sectoid"))
{
    var sectoid = Templates.Find("UnitTemplate", "Sectoid");
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
    int damage = (int)Templates.ReadField(weapon, "damage");
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
    var damage = Templates.ReadField(w, "damage");
    return damage != null && (int)damage >= 50;
}).ToArray();

DevConsole.Log($"Found {heavyWeapons.Length} heavy weapons");
```

## Example: Dynamic Difficulty Scaling

This example scales enemy stats based on mission count, creating progressively harder enemies:

```csharp
using MelonLoader;
using Menace.SDK;

namespace DynamicDifficulty;

public class DifficultyScaler : IModpackPlugin
{
    private int _missionsCompleted = 0;

    public void OnLoad(string modpackName)
    {
        GameState.OnMissionStart += OnMissionStart;
        GameState.OnMissionComplete += OnMissionComplete;

        DevConsole.Log($"[{modpackName}] Dynamic difficulty active");
    }

    private void OnMissionStart()
    {
        // Scale enemy templates based on missions completed
        float scaleFactor = 1.0f + (_missionsCompleted * 0.05f); // +5% per mission

        ScaleEnemyTemplates(scaleFactor);

        DevConsole.Log($"Difficulty scale: {scaleFactor:F2}x (Mission #{_missionsCompleted + 1})");
    }

    private void OnMissionComplete()
    {
        _missionsCompleted++;
    }

    private void ScaleEnemyTemplates(float scale)
    {
        // Find all enemy unit templates
        var enemyTypes = new[] { "Sectoid", "Muton", "Chryssalid", "Ethereal" };

        foreach (var enemyName in enemyTypes)
        {
            var enemy = Templates.Find("UnitTemplate", enemyName);
            if (enemy.IsNull) continue;

            // Read base values
            var baseHealth = Templates.ReadField(enemy, "maxHealth");
            var baseAim = Templates.ReadField(enemy, "aimModifier");

            if (baseHealth != null)
            {
                int scaledHealth = (int)((int)baseHealth * scale);
                Templates.WriteField(enemy, "maxHealth", scaledHealth);
            }

            if (baseAim != null)
            {
                // Improve aim slightly with scale
                float scaledAim = (float)baseAim + ((scale - 1.0f) * 0.1f);
                Templates.WriteField(enemy, "aimModifier", scaledAim);
            }
        }
    }

    public void OnSceneLoaded(int buildIndex, string sceneName) { }
    public void OnUpdate() { }
    public void OnGUI() { }
}
```

## Example: Conditional Weapon Modifications

This example modifies weapons based on mod settings chosen by the player:

```csharp
using MelonLoader;
using Menace.SDK;

namespace WeaponTweaks;

public class WeaponModifier : IModpackPlugin
{
    public void OnLoad(string modpackName)
    {
        // Register mod settings
        ModSettings.Register(modpackName, settings =>
        {
            settings.AddHeader("Weapon Balance");
            settings.AddSlider("DamageMultiplier", "Damage Multiplier", 0.5f, 2.0f, 1.0f);
            settings.AddToggle("LargerClips", "Larger Magazines", false);
            settings.AddDropdown("WeaponStyle", "Weapon Style",
                new[] { "Vanilla", "Realistic", "Arcade" }, "Vanilla");
        });

        // Apply initial modifications
        ApplyWeaponMods(modpackName);

        // Re-apply when settings change
        ModSettings.OnSettingChanged += (mod, key, value) =>
        {
            if (mod == modpackName)
                ApplyWeaponMods(modpackName);
        };
    }

    private void ApplyWeaponMods(string modpackName)
    {
        float damageMultiplier = ModSettings.Get<float>(modpackName, "DamageMultiplier");
        bool largerClips = ModSettings.Get<bool>(modpackName, "LargerClips");
        string style = ModSettings.Get<string>(modpackName, "WeaponStyle");

        var allWeapons = Templates.FindAll("WeaponTemplate");

        foreach (var weapon in allWeapons)
        {
            // Apply damage multiplier
            var baseDamage = Templates.ReadField(weapon, "damage");
            if (baseDamage != null)
            {
                int scaledDamage = (int)((int)baseDamage * damageMultiplier);
                Templates.WriteField(weapon, "damage", scaledDamage);
            }

            // Apply larger clips if enabled
            if (largerClips)
            {
                var baseClip = Templates.ReadField(weapon, "clipSize");
                if (baseClip != null)
                {
                    int largerClip = (int)((int)baseClip * 1.5f);
                    Templates.WriteField(weapon, "clipSize", largerClip);
                }
            }

            // Apply weapon style presets
            ApplyWeaponStyle(weapon, style);
        }

        DevConsole.Log($"Applied weapon mods: {damageMultiplier}x damage, " +
            $"clips={largerClips}, style={style}");
    }

    private void ApplyWeaponStyle(GameObj weapon, string style)
    {
        switch (style)
        {
            case "Realistic":
                // Lower damage, higher accuracy, smaller clips
                var damage = Templates.ReadField(weapon, "damage");
                if (damage != null)
                    Templates.WriteField(weapon, "damage", (int)((int)damage * 0.8f));

                var accuracy = Templates.ReadField(weapon, "accuracy");
                if (accuracy != null)
                    Templates.WriteField(weapon, "accuracy", (int)accuracy + 10);
                break;

            case "Arcade":
                // Higher damage, faster fire rate, larger splash
                var arcadeDamage = Templates.ReadField(weapon, "damage");
                if (arcadeDamage != null)
                    Templates.WriteField(weapon, "damage", (int)((int)arcadeDamage * 1.3f));

                var fireRate = Templates.ReadField(weapon, "fireRate");
                if (fireRate != null)
                    Templates.WriteField(weapon, "fireRate", (float)fireRate * 1.2f);
                break;

            case "Vanilla":
            default:
                // No additional changes
                break;
        }
    }

    public void OnSceneLoaded(int buildIndex, string sceneName) { }
    public void OnUpdate() { }
    public void OnGUI() { }
}
```

## Best Practices

### When to Modify Templates

| Timing | Use Case | Method |
|--------|----------|--------|
| **OnLoad** | One-time setup, applying mod settings | `IModpackPlugin.OnLoad()` |
| **OnMissionStart** | Per-mission scaling, difficulty adjustments | `GameState.OnMissionStart` |
| **OnSettingChanged** | React to user changing mod settings | `ModSettings.OnSettingChanged` |

Avoid modifying templates in `OnUpdate()` - it runs every frame and will tank performance.

### Caching Templates

For frequently accessed templates, cache the `GameObj` reference:

```csharp
public class CachingExample : IModpackPlugin
{
    // Cache template references
    private GameObj _marineTemplate;
    private GameObj _rifleTemplate;

    public void OnLoad(string modpackName)
    {
        // Look up once, use many times
        _marineTemplate = Templates.Find("UnitTemplate", "Marine");
        _rifleTemplate = Templates.Find("WeaponTemplate", "AssaultRifle");
    }

    private void ModifyMarine()
    {
        // Use cached reference - no lookup cost
        if (!_marineTemplate.IsNull)
        {
            Templates.WriteField(_marineTemplate, "maxHealth", 150);
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
var template = Templates.Find("UnitTemplate", "NonExistent");
if (template.IsNull)
{
    DevConsole.LogWarning("Template not found");
    return;
}

// WriteField returns false on failure
bool success = Templates.WriteField(template, "invalidField", 100);
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
var marineTemplate = Templates.Find("UnitTemplate", "Marine");
Templates.WriteField(marineTemplate, "maxHealth", 200);

// This modifies an INSTANCE - affects only this specific marine
var liveMarines = GameQuery.FindAll("Marine");
foreach (var marine in liveMarines)
{
    marine.Set("currentHealth", 200); // Uses GameObj.Set, not Templates
}
```

---

**Previous:** [SDK Basics](07-sdk-basics.md) | **Next:** [UI Modifications](09-ui-modifications.md)

**See also:** [Templates API Reference](../coding-sdk/api/templates.md)
