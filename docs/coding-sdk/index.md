# Menace SDK

Menace SDK is a modding SDK for IL2CPP Unity games, built on top of
[MelonLoader](https://github.com/LavaGang/MelonLoader). It ships as a single
MelonLoader mod DLL (`Menace.ModpackLoader.dll`) that provides a layered API
for reading, writing, querying, and patching live game objects at runtime --
without requiring the game's source code or a recompilation of the game
assembly.

Everything lives under the `Menace.SDK` namespace.

---

## Start Here

If you are new to SDK code mods, read these in order:

1. [What Is the SDK?](what-is-sdk.md)
2. [Getting Started: Your First Plugin](getting-started.md)
3. [API Reference](#api-reference)

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

The SDK is organized into nine tiers, from low-level IL2CPP access up to
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

### Tier 4 -- Tactical Control

High-level APIs for controlling entities and game state during tactical combat.

| Type | Purpose |
|------|---------|
| `EntitySpawner` | Spawn and destroy entities. `SpawnUnit(template, x, y, faction)` creates actors at tile positions. `ListEntities(faction)` queries actors. `ClearEnemies()` removes all enemies. |
| `EntityMovement` | Control entity movement. `MoveTo(actor, x, y)` pathfinds and moves. `Teleport(actor, x, y)` instant repositioning. `GetMovementRange(actor)` returns reachable tiles. Manage facing direction and action points. |
| `EntityCombat` | Combat actions and status effects. `Attack(attacker, target)` uses primary weapon. `UseAbility(actor, skill, target)` for abilities. Manage suppression, morale, HP. Query skills with `GetSkills(actor)`. |
| `TacticalController` | Game state control. `GetCurrentRound()`, `NextRound()`, `EndTurn()` for turn management. `SetPaused()`, `SetTimeScale()` for time control. `SpawnWave()` for enemy spawning. `GetTacticalState()` for full status. |

### Tier 5 -- Map & Environment

APIs for querying and manipulating the tactical map, tiles, and environmental effects.

| Type | Purpose |
|------|---------|
| `TileMap` | Query tile information. `GetTile(x, y)` returns tile data. `GetCover(x, y, direction)` checks cover values. `GetMapInfo()` returns map dimensions. `GetAdjacentTiles()` for neighbor queries. |
| `Pathfinding` | Find paths and check reachability. `FindPath(from, to)` returns tile path. `GetMovementCost(from, to)` calculates AP cost. `IsReachable(actor, x, y)` checks if tile is reachable. |
| `LineOfSight` | LOS and visibility checks. `HasLOS(from, to)` checks line of sight. `GetVisibleTiles(actor)` returns visible tile set. `IsVisible(actor, target)` checks target visibility. |
| `TileEffects` | Environmental effects on tiles. `SpawnFire(x, y)`, `SpawnSmoke(x, y)` create effects. `GetTileEffects(x, y)` queries active effects. `ClearEffects(x, y)` removes effects. |

### Tier 6 -- Strategy Layer

APIs for campaign/strategy state including missions, operations, roster, and economy.

| Type | Purpose |
|------|---------|
| `Mission` | Mission state and objectives. `GetMissionInfo()` returns current mission data. `GetObjectives()` lists objectives. `CompleteObjective()` marks objectives done. |
| `Operation` | Campaign operations. `GetOperationInfo()` returns operation state. `GetCurrentMission()` returns active mission. `GetMissions()` lists all missions in operation. |
| `Roster` | Unit roster management. `GetHiredLeaders()` lists all hired units. `GetHirableLeaders()` lists recruitable templates. `HireLeader(template)` hires a leader. `DismissLeader(leader)` fires a leader. `GetLeaderInfo(leader)` returns unit details. `FindByNickname(name)` searches roster. |
| `Perks` | Perk and skill management. `GetLeaderPerks(leader)` lists learned perks. `GetPerkTrees(leader)` shows available perk trees. `AddPerk(leader, perk)` grants a perk. `RemoveLastPerk(leader)` demotes. `GetAvailablePerks(leader)` returns unlearned perks. `CanBePromoted(leader)` checks eligibility. |
| `Inventory` | Items and equipment. `GetContainer(actor)` gets inventory. `GetAllItems(container)` lists items. `GetEquippedWeapons(actor)` returns equipped weapons. Trade value calculations. |
| `ArmyGeneration` | Army spawning and budgets. `GetArmyInfo(army)` returns army composition. `GetArmyTemplates()` lists available templates. `GetEntityCost(template)` returns spawn cost. |
| `Vehicle` | Vehicle information. `GetVehicleInfo(entity)` returns vehicle data. `GetModularVehicle(entity)` returns slot info. `IsVehicle(entity)` type check. |
| `BlackMarket` | Shop system access. `GetBlackMarketInfo()` returns shop state. `GetAvailableStacks()` lists items for sale. Item generation and timeout tracking. |

### Tier 7 -- Social & Dialogue

APIs for conversations, events, and squaddie emotional states.

| Type | Purpose |
|------|---------|
| `Conversation` | Dialogue system. `GetActiveConversation()` returns current dialogue. `TriggerConversation(template)` starts dialogue. `GetConversationInfo()` returns conversation state. |
| `Emotions` | Squaddie emotional states. `GetEmotionalState(squaddie)` returns emotions. `GetActiveEffects(squaddie)` lists active emotional effects. Trigger emotional changes. |

### Tier 8 -- AI System

APIs for inspecting, modifying, and coordinating AI decision-making.

| Type | Purpose |
|------|---------|
| `AI` | AI decision system access. `GetAgent(actor)` gets the AI agent. `GetAgentInfo(actor)` returns state (evaluating, ready, executing). `GetRoleData(actor)` returns AI config (utility/safety weights). `GetBehaviors(actor)` lists available actions. `GetTileScores(actor)` returns scored positions. `SetRoleDataFloat/Bool(actor, field, value)` modifies AI config. `IsAnyFactionEvaluating()` checks write safety. |
| `AICoordination` | Coordinated AI behavior (Combined Arms-style). `InitializeTurnState(faction)` sets up turn state. `ClassifyUnit(actor)` returns role (Suppressor/DamageDealer). `ClassifyFormationBand(actor)` returns position band. `CalculateAgentScoreMultiplier()` for sequencing/focus fire. `ApplyTileScoreModifiers()` for Center of Forces/Formation Depth. Thread-safe per-agent writes. |

### Tier 9 -- REPL

The SDK embeds a Roslyn-based C# REPL in the DevConsole **Console** panel. It
automatically resolves metadata references from the game's
runtime directory (system BCL, MelonLoader, Il2CppInterop, IL2CPP proxy
assemblies, and loaded mod DLLs), compiles expressions or multi-statement
blocks to in-memory assemblies, and executes them. Default `using` directives
include `System`, `System.Linq`, `System.Collections.Generic`, `Menace.SDK`,
and `UnityEngine`.

The REPL is initialized on startup. If Roslyn packages are not available
(e.g., stripped deploy), unknown non-command input in the Console will not be
evaluated as C#.

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
console to the Log panel for details.

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

### Built-in Console Commands

The SDK registers extensive console commands for debugging and testing. Type `help` in the console to see all available commands.

**Entity & Combat:**
| Command | Description |
|---------|-------------|
| `spawn <template> [x y] [faction]` | Spawn entity at position |
| `kill [name]` | Kill entity (or active actor) |
| `heal [name] [amount]` | Heal entity |
| `damage <target> <amount>` | Deal damage to entity |
| `suppress <target> <amount>` | Apply suppression |
| `skills [name]` | List actor's skills |
| `actors [faction]` | List all actors |

**Movement & Positioning:**
| Command | Description |
|---------|-------------|
| `move <x> <y>` | Move active actor to tile |
| `teleport <x> <y>` | Instant teleport to tile |
| `facing [direction]` | Get/set facing direction |
| `range [name]` | Show movement range |

**Map & Environment:**
| Command | Description |
|---------|-------------|
| `tile <x> <y>` | Get tile info |
| `cover <x> <y>` | Get cover values |
| `mapinfo` | Show map dimensions |
| `path <x1> <y1> <x2> <y2>` | Find path between tiles |
| `los <x1> <y1> <x2> <y2>` | Check line of sight |
| `fire <x> <y>` | Spawn fire effect |
| `smoke <x> <y>` | Spawn smoke effect |

**Tactical State:**
| Command | Description |
|---------|-------------|
| `tactical` | Show tactical state |
| `round` | Show current round |
| `nextround` | Advance to next round |
| `endturn` | End current turn |
| `pause` | Toggle pause |
| `timescale <value>` | Set time scale |

**Strategy Layer:**
| Command | Description |
|---------|-------------|
| `mission` | Show current mission |
| `objectives` | List mission objectives |
| `operation` | Show current operation |
| `roster` | List hired units |
| `unit <nickname>` | Show unit info |
| `available` | Show deployable units |

**Items & Economy:**
| Command | Description |
|---------|-------------|
| `inventory` | Show active actor inventory |
| `weapons` | Show equipped weapons |
| `vehicle` | Show vehicle info |
| `blackmarket` | Show shop info |
| `armytemplates` | List army templates |

**Dialogue & Social:**
| Command | Description |
|---------|-------------|
| `conversations` | List conversation templates |
| `emotions <squaddie>` | Show emotional state |

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

### Core (Tier 1-3)
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

### Tactical Control (Tier 4)
- [EntitySpawner](api/entity-spawner.md) -- Entity spawning and destruction
- [EntityMovement](api/entity-movement.md) -- Movement and pathfinding control
- [EntityCombat](api/entity-combat.md) -- Combat actions and status effects
- [TacticalController](api/tactical-controller.md) -- Game state and turn management

### Map & Environment (Tier 5)
- [TileMap](api/tile-map.md) -- Tile queries and map information
- [Pathfinding](api/pathfinding.md) -- Path finding and movement costs
- [LineOfSight](api/line-of-sight.md) -- LOS and visibility checks
- [TileEffects](api/tile-effects.md) -- Fire, smoke, and environmental effects

### Strategy Layer (Tier 6)
- [Mission](api/mission.md) -- Mission state and objectives
- [Operation](api/operation.md) -- Campaign operations
- [Roster](api/roster.md) -- Unit roster management
- [Inventory](api/inventory.md) -- Items and equipment
- [ArmyGeneration](api/army-generation.md) -- Army spawning and budgets
- [Vehicle](api/vehicle.md) -- Vehicle information and control
- [BlackMarket](api/black-market.md) -- Shop system access

### Social & Dialogue (Tier 7)
- [Conversation](api/conversation.md) -- Dialogue system
- [Emotions](api/emotions.md) -- Squaddie emotional states
