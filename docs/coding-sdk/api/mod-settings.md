# ModSettings

`Menace.SDK.ModSettings` -- Configuration system for mods with automatic UI and persistence.

## Overview

ModSettings provides a way for mods to define configurable options that:

- Appear automatically in the DevConsole **Settings** panel
- Persist to `UserData/ModSettings.json` across game sessions
- Can be read and written programmatically
- Fire events when values change

This is intended for **configuration options** (difficulty settings, UI preferences, gameplay tweaks) rather than cheats. Settings are organized by mod and grouped with headers.

## Registering Settings

Call `ModSettings.Register()` during your plugin's `OnInitialize` to define settings:

```csharp
public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
{
    ModSettings.Register("My Mod", settings => {
        settings.AddHeader("Difficulty");
        settings.AddSlider("DamageTaken", "Damage Taken Multiplier", 0.5f, 3f, 1f);
        settings.AddNumber("MaxSquadSize", "Max Squad Size", 1, 12, 6);

        settings.AddHeader("UI Options");
        settings.AddToggle("ShowDamageNumbers", "Show Damage Numbers", true);
        settings.AddDropdown("Theme", "UI Theme", new[] { "Dark", "Light", "Classic" }, "Dark");

        settings.AddHeader("Misc");
        settings.AddText("PlayerName", "Custom Name", "Commander");
    });
}
```

## Setting Types

### AddHeader

```csharp
settings.AddHeader(string label)
```

Adds a visual section header. Headers are not stored or retrieved -- they only affect the UI layout.

### AddToggle

```csharp
settings.AddToggle(string key, string label, bool defaultValue)
```

A boolean on/off toggle. Renders as a checkbox.

### AddSlider

```csharp
settings.AddSlider(string key, string label, float min, float max, float defaultValue)
```

A floating-point value with a horizontal slider. The current value is displayed to the right of the slider.

### AddNumber

```csharp
settings.AddNumber(string key, string label, int min, int max, int defaultValue)
```

An integer value with **-** and **+** buttons for adjustment.

### AddDropdown

```csharp
settings.AddDropdown(string key, string label, string[] options, string defaultValue)
```

A selection from a fixed list of options. Renders with **<** and **>** buttons to cycle through choices.

### AddText

```csharp
settings.AddText(string key, string label, string defaultValue)
```

A free-form text input field.

## Reading Settings

Use `ModSettings.Get<T>()` to read the current value of a setting:

```csharp
float damageMultiplier = ModSettings.Get<float>("My Mod", "DamageTaken");
int maxSquad = ModSettings.Get<int>("My Mod", "MaxSquadSize");
bool showNumbers = ModSettings.Get<bool>("My Mod", "ShowDamageNumbers");
string theme = ModSettings.Get<string>("My Mod", "Theme");
```

If the setting doesn't exist or the type doesn't match, the default value for that type is returned.

## Writing Settings

Use `ModSettings.Set<T>()` to change a setting programmatically:

```csharp
ModSettings.Set("My Mod", "MaxSquadSize", 8);
ModSettings.Set("My Mod", "ShowDamageNumbers", false);
```

This triggers the `OnSettingChanged` event and marks the settings as dirty for saving.

## Change Events

Subscribe to `ModSettings.OnSettingChanged` to react when any setting changes:

```csharp
ModSettings.OnSettingChanged += (modName, key, value) =>
{
    if (modName != "My Mod") return;

    _log.Msg($"Setting changed: {key} = {value}");

    if (key == "DamageTaken" && value is float mult)
    {
        ApplyDamageMultiplier(mult);
    }
};
```

The event fires for both UI changes and programmatic `Set()` calls.

## Persistence

Settings are automatically saved to `UserData/ModSettings.json`:

- On scene transitions
- When the game closes
- Only when values have changed (dirty flag)

The JSON file can be edited manually if needed:

```json
{
  "My Mod": {
    "DamageTaken": 1.5,
    "MaxSquadSize": 8,
    "ShowDamageNumbers": true,
    "Theme": "Dark"
  },
  "Another Mod": {
    "SomeOption": "value"
  }
}
```

## In-Game UI

Settings appear in the DevConsole under the **Settings** tab (press `~` to open). Each mod's settings are grouped under a collapsible header. Click the mod name to expand/collapse its settings.

```
▼ My Mod
  -- Difficulty --
  Damage Taken Multiplier   [====|====] 1.50
  Max Squad Size            [- 8 +]

  -- UI Options --
  Show Damage Numbers       [x]
  UI Theme                  [< Dark >]

  -- Misc --
  Custom Name               [Commander    ]

▶ Another Mod (collapsed)
```

## API Reference

### Static Methods

| Method | Description |
|--------|-------------|
| `Register(string modName, Action<SettingsBuilder> configure)` | Define settings for a mod |
| `Get<T>(string modName, string key)` | Read a setting value |
| `Get(string modName, string key)` | Read a setting as `object` |
| `Set<T>(string modName, string key, T value)` | Write a setting value |
| `HasSettings(string modName)` | Check if a mod has registered settings |
| `GetRegisteredMods()` | List all mods with settings |

### Events

| Event | Signature | Description |
|-------|-----------|-------------|
| `OnSettingChanged` | `Action<string, string, object>` | Fires when any setting changes (modName, key, newValue) |

### SettingsBuilder Methods

| Method | Parameters | Description |
|--------|------------|-------------|
| `AddHeader` | `string label` | Visual section header |
| `AddToggle` | `string key, string label, bool defaultValue` | Boolean toggle |
| `AddSlider` | `string key, string label, float min, float max, float defaultValue` | Float slider |
| `AddNumber` | `string key, string label, int min, int max, int defaultValue` | Integer with +/- |
| `AddDropdown` | `string key, string label, string[] options, string defaultValue` | Selection list |
| `AddText` | `string key, string label, string defaultValue` | Text input |

## Complete Example

```csharp
using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;

public class DifficultyPlugin : IModpackPlugin
{
    private MelonLogger.Instance _log;

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _log = logger;

        // Register settings
        ModSettings.Register("Difficulty Mod", settings => {
            settings.AddHeader("Combat");
            settings.AddSlider("PlayerDamage", "Player Damage Taken", 0.5f, 3f, 1f);
            settings.AddSlider("EnemyHealth", "Enemy Health", 0.5f, 3f, 1f);

            settings.AddHeader("Economy");
            settings.AddSlider("SupplyRate", "Supply Rate", 0.25f, 2f, 1f);
            settings.AddNumber("StartingCredits", "Starting Credits", 0, 10000, 1000);

            settings.AddHeader("Options");
            settings.AddToggle("Ironman", "Ironman Mode", false);
            settings.AddDropdown("AutosaveFreq", "Autosave Frequency",
                new[] { "Never", "Rare", "Normal", "Frequent" }, "Normal");
        });

        // React to changes
        ModSettings.OnSettingChanged += OnSettingChanged;

        _log.Msg("Difficulty Mod initialized");
    }

    private void OnSettingChanged(string mod, string key, object value)
    {
        if (mod != "Difficulty Mod") return;
        _log.Msg($"[Difficulty] {key} changed to {value}");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        // Read settings when scene loads
        float playerDmg = ModSettings.Get<float>("Difficulty Mod", "PlayerDamage");
        float enemyHp = ModSettings.Get<float>("Difficulty Mod", "EnemyHealth");
        bool ironman = ModSettings.Get<bool>("Difficulty Mod", "Ironman");

        _log.Msg($"Settings: PlayerDmg={playerDmg}x, EnemyHP={enemyHp}x, Ironman={ironman}");

        // Apply settings to game...
    }

    public void OnUpdate() { }
    public void OnGUI() { }
}
```
