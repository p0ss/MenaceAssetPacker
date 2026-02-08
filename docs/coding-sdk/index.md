# Menace SDK

Menace SDK is a modding SDK for IL2CPP Unity games, built on top of
[MelonLoader](https://github.com/LavaGang/MelonLoader). It ships as a single
MelonLoader mod DLL (`Menace.ModpackLoader.dll`) that provides a layered API
for reading, writing, querying, and patching live game objects at runtime --
without requiring the game's source code or a recompilation of the game
assembly.

Everything lives under the `Menace.SDK` namespace.

---

## Architecture

```
Game Process (IL2CPP)
  |
  +-- MelonLoader
        |
        +-- Menace.ModpackLoader.dll   (MelonMod, single DLL)
              |
              +-- Modpack manifest loader   (modpack.json parser)
              +-- DllLoader                 (discovers + loads IModpackPlugin DLLs)
              +-- BundleLoader              (AssetBundle injection)
              +-- Template injection        (data-driven field patches)
              +-- Menace.SDK namespace      (public API, described below)
```

**Menace.ModpackLoader.dll** is a MelonMod that owns the full mod lifecycle.
Plugin authors never subclass `MelonMod` directly. Instead, they implement the
`IModpackPlugin` interface in a separate class library DLL, place it in their
modpack's `dlls/` folder, and let the loader discover it automatically.

---

## Tier Structure

The SDK is organized into four tiers, from low-level IL2CPP access up to
interactive tooling.

### Tier 1 -- IL2CPP Runtime Access

These types wrap raw `il2cpp_*` FFI calls behind safe, null-tolerant APIs.
Every read returns a default on failure; every write returns `false` on failure.
Nothing in Tier 1 throws.

| Type | Purpose |
|------|---------|
| `GameType` | Wrapper around an IL2CPP class pointer. Resolves types by name, caches lookups, provides field offset resolution, parent traversal, and managed proxy discovery. |
| `GameObj` | Safe handle for a live IL2CPP object. Reads and writes `int`, `float`, `bool`, `string`, `IntPtr`, and nested object fields by name or pre-cached offset. Checks `IsAlive` via `m_CachedPtr`. |
| `GameList` | Read-only wrapper for `IL2CPP List<T>`. Exposes `Count`, indexer, and `foreach` enumeration over the internal `_items` array. |
| `GameDict` | Read-only wrapper for `IL2CPP Dictionary<K,V>`. Iterates the `_entries` array, skipping tombstoned slots. Yields `(GameObj Key, GameObj Value)` pairs. |
| `GameArray` | Wrapper for IL2CPP native arrays. Provides `Length`, indexer for object elements, and `ReadInt`/`ReadFloat` for value-type arrays. |
| `GameQuery` | Static helpers for `FindObjectsOfTypeAll`. Find all objects by type name, by `GameType`, or by generic `<T>`. Includes `FindByName` and a per-scene cache (`FindAllCached`) that is cleared automatically on scene load. |
| `GamePatch` | Simplified Harmony patching. `GamePatch.Prefix(...)` and `GamePatch.Postfix(...)` accept a type name or `GameType`, resolve the target method, and apply the patch. Returns `false` and routes failures to `ModError` instead of throwing. |

### Tier 2 -- Templates and Game State

| Type | Purpose |
|------|---------|
| `Templates` | High-level API for reading, writing, and cloning game ScriptableObject templates. `Templates.Find(typeName, name)` locates a template; `Templates.WriteField(obj, "fieldName", value)` modifies it via managed reflection (supports dotted paths like `"Stats.MaxHealth"`). `Templates.Clone(typeName, source, newName)` duplicates a template in memory. |
| `GameState` | Scene awareness and deferred execution. Exposes `CurrentScene`, the `SceneLoaded` event, and `TacticalReady` (fires 30 frames after the tactical scene loads). `GameState.RunDelayed(frames, callback)` schedules a callback N frames in the future. `GameState.RunWhen(condition, callback)` polls a predicate once per frame and fires when it becomes true. Also provides `GameAssembly` for quick access to the `Assembly-CSharp` assembly. |

### Tier 3 -- Error Handling, Diagnostics, and Configuration

| Type | Purpose |
|------|---------|
| `ModError` | Central error sink. All SDK internals route failures here instead of throwing. Stores entries in a rate-limited, deduplicated ring buffer (1000 entries max). Errors are simultaneously written to MelonLoader's log. Public API: `ModError.Report(modId, message)`, `ModError.Warn(...)`, `ModError.Info(...)`, `ModError.GetErrors(modId)`. Subscribe to `ModError.OnError` for real-time notifications. |
| `DevConsole` | IMGUI overlay toggled with the **~** (backtick) key. Ships with built-in panels: **Battle Log**, **Log**, **Console**, **Inspector**, **Watch**, and **Settings**. Plugins can register custom panels via `DevConsole.RegisterPanel(name, drawCallback)`. |
| `ModSettings` | Configuration system for mods. Register settings with `ModSettings.Register(modName, builder => { ... })` using typed builders (toggle, slider, number, dropdown, text). Settings appear in the DevConsole **Settings** panel and persist to `UserData/ModSettings.json`. Read values with `ModSettings.Get<T>(modName, key)`. Subscribe to `ModSettings.OnSettingChanged` for real-time change notifications. |
| `ErrorNotification` | Passive bottom-left screen badge that displays "N mod errors -- press ~ for console" when errors exist and the console is hidden. Auto-fades after 8 seconds of no new errors. |

### Tier 4 -- REPL

The SDK embeds a Roslyn-based C# REPL that appears as the **REPL** tab in the
DevConsole. It automatically resolves metadata references from the game's
runtime directory (system BCL, MelonLoader, Il2CppInterop, IL2CPP proxy
assemblies, and loaded mod DLLs), compiles expressions or multi-statement
blocks to in-memory assemblies, and executes them. Default `using` directives
include `System`, `System.Linq`, `System.Collections.Generic`, `Menace.SDK`,
and `UnityEngine`.

The REPL initializes silently on startup. If Roslyn packages are not available
(e.g., stripped deploy), the REPL tab simply does not appear.

---

## Quick Start

A minimal plugin that finds a game type and reads a field:

```csharp
using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;

public class MyPlugin : IModpackPlugin
{
    private MelonLogger.Instance _log;

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _log = logger;
        _log.Msg("MyPlugin initialized");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        // Find the IL2CPP type for WeaponTemplate in Assembly-CSharp
        var weaponType = GameType.Find("WeaponTemplate");
        if (!weaponType.IsValid)
        {
            _log.Warning("WeaponTemplate type not found");
            return;
        }

        // Query all live WeaponTemplate instances
        var weapons = GameQuery.FindAll(weaponType);
        _log.Msg($"Found {weapons.Length} WeaponTemplate instances");

        foreach (var weapon in weapons)
        {
            // Read fields directly from IL2CPP memory
            string name = weapon.GetName();
            int damage = weapon.ReadInt("Damage");
            float range = weapon.ReadFloat("Range");
            _log.Msg($"  {name}: damage={damage}, range={range}");
        }
    }
}
```

Build this as a .NET 6 class library referencing `Menace.ModpackLoader.dll`,
place the output DLL in your modpack's `dlls/` directory alongside a
`modpack.json`, and drop the modpack folder into the game's `Mods/` directory.

---

## Example Mod

The SDK ships with **DevMode**, a working example mod that demonstrates SDK
features with functional implementations. Find it in:

- `third_party/bundled/modpacks/DevMode-modpack/src/DevModePlugin.cs`

DevMode provides:

| Feature | What It Does |
|---------|--------------|
| **ModSettings** | Working settings that affect gameplay (damage multiplier, accuracy bonus, enemy health) |
| **Templates API** | Modifies real `WeaponTemplate.Damage`, `EntityTemplate.Stats.HitpointsPerElement` at runtime |
| **DevConsole Panel** | Custom "Dev Mode" panel with entity spawning, god mode, delete tools |
| **Reflection-based Access** | Demonstrates safe runtime type discovery from Assembly-CSharp |

The settings in DevMode actually work — changing "All Weapon Damage" immediately
modifies all weapon templates in memory. Use this as a reference for building
mods that modify game data.

---

## Error Philosophy

The SDK is designed around the principle of **never crashing the game**. Every
public API method:

- Returns a default value (`0`, `0f`, `false`, `null`, `IntPtr.Zero`,
  `GameObj.Null`, `GameType.Invalid`, or an empty array) on failure.
- Routes the failure to `ModError` with context (caller name, field name,
  exception details).
- Never throws an exception to the caller.

This means a plugin with a bug will produce diagnostic output in the console
and MelonLoader log, but the game continues running. The `ErrorNotification`
badge alerts the player that something went wrong; pressing **~** opens the
console to the Errors panel for details.

Rate limiting (10 errors/second per mod ID) and deduplication (5-second window)
prevent a single broken read in an `OnUpdate` loop from flooding the log.

---

## Developer Console

Press **~** (backtick/tilde) at any time to toggle the developer console.

Built-in panels:

- **Battle Log** -- Combat events captured from the game's combat system. Filter
  by event type (hits, misses, deaths, etc.).
- **Log** -- Combined view of `ModError` entries and `DevConsole.Log()` messages
  in chronological order. Filter by severity and mod ID.
- **Console** -- Command-line interface for SDK commands and C# REPL. Type `help`
  for available commands, or enter C# expressions to evaluate.
- **Inspector** -- Call `DevConsole.Inspect(someGameObj)` from your plugin
  to view all readable properties on a live IL2CPP object.
- **Watch** -- Register live expressions with
  `DevConsole.Watch("label", () => someValue.ToString())`. They update every
  frame while the console is open.
- **Settings** -- Configure mod settings registered via `ModSettings.Register()`.
  Settings are grouped by mod with collapsible headers. Changes save automatically.

Plugins can add their own panels:

```csharp
DevConsole.RegisterPanel("My Panel", (Rect area) =>
{
    GUI.Label(new Rect(area.x, area.y, area.width, 18), "Hello from my custom panel");
});
```

---

## Mod Settings

The `ModSettings` API allows mods to define configurable options with automatic UI
and persistence. Settings appear in the DevConsole's **Settings** panel.

```csharp
// Register settings during initialization
ModSettings.Register("My Mod", settings => {
    settings.AddHeader("Difficulty");
    settings.AddSlider("DamageTaken", "Damage Taken", 0.5f, 3f, 1f);
    settings.AddNumber("MaxSquadSize", "Max Squad Size", 1, 12, 6);

    settings.AddHeader("Options");
    settings.AddToggle("ShowDamage", "Show Damage Numbers", true);
    settings.AddDropdown("Theme", "UI Theme", new[] { "Dark", "Light" }, "Dark");
});

// Read settings anywhere
float damage = ModSettings.Get<float>("My Mod", "DamageTaken");
bool showDmg = ModSettings.Get<bool>("My Mod", "ShowDamage");

// React to changes
ModSettings.OnSettingChanged += (mod, key, value) => {
    if (mod == "My Mod" && key == "DamageTaken")
        ApplyDamageMultiplier((float)value);
};
```

Settings are saved automatically to `UserData/ModSettings.json` on scene
transitions and game exit.

See [ModSettings API Reference](api/mod-settings.md) for full documentation.

---

## Bundled Modpacks

The SDK ships with modpacks in `third_party/bundled/modpacks/`:

| Modpack | Purpose |
|---------|---------|
| **DevMode** | **Recommended starting point.** Working gameplay tweaks (damage, accuracy, health multipliers), entity spawning, god mode, delete tools. All settings actually modify game templates. |
| **TwitchSquaddies** | Twitch integration for squaddie management — demonstrates runtime type discovery for game state access |

Copy DevMode and modify it for your own mods.

---

## API Reference

Detailed documentation for each SDK type:

- [GameType](api/game-type.md) -- IL2CPP type resolution and field offsets
- [GameObj](api/game-obj.md) -- Safe handle for live IL2CPP objects
- [GameQuery](api/game-query.md) -- FindObjectsOfTypeAll helpers
- [GamePatch](api/game-patch.md) -- Simplified Harmony patching
- [Collections](api/collections.md) -- GameList, GameDict, GameArray
- [Templates](api/templates.md) -- ScriptableObject template access
- [GameState](api/game-state.md) -- Scene awareness and deferred execution
- [ModError](api/mod-error.md) -- Error reporting and diagnostics
- [DevConsole](api/dev-console.md) -- Developer console and panels
- [ModSettings](api/mod-settings.md) -- Configuration system with UI
- [REPL](api/repl.md) -- Runtime C# expression evaluation
