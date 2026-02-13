# DevConsole

`Menace.SDK.DevConsole` -- IMGUI-based developer console overlay with a tabbed panel system.

## Overview

DevConsole is the primary debugging and runtime inspection tool for Menace mods. It provides:

- **Battle Log** - Real-time combat event tracking (hits, misses, damage, deaths)
- **Log Panel** - Unified error and message logging with severity filtering
- **Console** - Command-line interface for SDK commands and game manipulation
- **Inspector** - Object property viewer via managed reflection
- **Watch Panel** - Live variable monitoring with custom expressions
- **Settings** - Mod configuration UI generated from `ModSettings`

Mods can extend the console with custom panels and commands. All SDK systems register console commands automatically.

## Toggle

Press the **backtick/tilde** key (`~`) to toggle the console. The console renders as a semi-transparent dark overlay occupying up to 60% width and 70% height of the screen (capped at 900x700 pixels), anchored to the top-left corner.

When the console is hidden and errors exist, a small notification appears at the bottom-left of the screen showing the error count and a reminder to press `~`. The notification auto-fades after 8 seconds of no new errors.

## Properties

### IsVisible

```csharp
public static bool IsVisible { get; set; }
```

Whether the console is currently visible. Can be set programmatically. Toggled by the `~` key on each frame.

## Methods

### ShowPanel

```csharp
public static void ShowPanel(string panelName)
```

Open the console and switch to a specific panel by name. Useful for programmatically jumping to a specific tab (e.g., opening Settings when the player clicks a "Configure Mod" button in your custom UI).

### RegisterPanel

```csharp
public static void RegisterPanel(string name, Action<Rect> drawCallback)
```

Register a custom panel in the console. The `drawCallback` is invoked during `OnGUI` when the panel's tab is active, receiving the content `Rect` where the panel should draw. If a panel with the same name already exists, its callback is replaced.

The callback should use raw `GUI.*` calls (not `GUILayout`) to draw its content, using the provided `Rect` for positioning. `GUILayout` methods are unavailable in IL2CPP builds due to method unstripping failures. Exceptions in the callback are caught and displayed as an error message in the panel area.

### RemovePanel

```csharp
public static void RemovePanel(string name)
```

Remove a panel by name. If the removed panel was the active tab, the selection moves to the nearest valid tab.

### Inspect

```csharp
public static void Inspect(GameObj obj)
```

Set a `GameObj` to inspect in the Inspector panel and switch to the Inspector tab. The Inspector panel uses managed reflection to enumerate and display all public properties of the object's IL2CppInterop proxy type.

### Watch

```csharp
public static void Watch(string label, Func<string> valueGetter)
```

Add a live watch expression to the Watch panel. The `valueGetter` function is called every frame the panel is visible. If a watch with the same label already exists, it is replaced.

### Unwatch

```csharp
public static void Unwatch(string label)
```

Remove a watch by its label.

### Log

```csharp
public static void Log(string message)
```

Append a timestamped message to the Log panel. The log buffer holds up to 200 messages; older messages are evicted from the front.

## Built-in Panels

The console initializes with five built-in panels, displayed in this order:

### Battle Log

Displays combat events captured from the game's DevCombatLog system via Harmony patches. Events include:

- **Hits** (green) - successful attacks with damage dealt
- **Misses** (red) - failed attack rolls
- **Suppression** (yellow) - suppression applied to units
- **Morale** (orange) - morale state changes
- **Armor Penetration** (blue-gray) - armor piercing results
- **Deaths** (white, bold) - unit deaths
- **Skills** (light blue) - skill/ability usage

Each entry shows a timestamp and detailed information (hit chance, roll, damage, etc.). Use the filter toggles to show/hide specific event types. The Clear button empties the log.

### Log

A unified panel showing both `ModError` entries and `DevConsole.Log()` messages in chronological order. Entries are color-coded by severity:

- **Errors/Fatal** (red)
- **Warnings** (yellow)
- **Info** (blue)

Severity filter toggles (Err/Warn/Info) control which entries are visible. Per-mod filter toggles appear when multiple mods have logged errors. The Clear button empties both the error list and log buffer.

Duplicate errors show an occurrence count suffix (e.g., `(x5)`).

### Console

A command-line interface for executing SDK commands and C# expressions. Type `help` to see available commands:

**Query Commands:**
- `find <type>` - List all instances of a type
- `findbyname <type> <name>` - Find instance by name
- `inspect <type> <name>` - Find and inspect an object
- `templates <type>` - List all templates of a type
- `template <type> <name>` - Inspect a specific template
- `scene` - Show current scene name
- `errors [modId]` - Show recent errors
- `clear` - Clear console output

**Entity Spawning:**
- `spawn <template> <x> <y> [faction]` - Spawn a unit at tile (default faction=1/enemy)
- `kill` - Kill the selected actor
- `enemies` - List all enemy actors
- `actors [faction]` - List all actors (optionally filtered by faction)
- `clearwave` - Clear all enemies from the map

**Movement:**
- `move <x> <y>` - Move selected actor to tile
- `teleport <x> <y>` - Teleport selected actor to tile
- `pos` - Show selected actor position and facing
- `facing [dir]` - Get/set facing (0-7 or N/NE/E/SE/S/SW/W/NW)
- `ap [value]` - Get/set action points

**Combat:**
- `skills` - List skills for selected actor
- `damage <amount>` - Apply damage to selected actor
- `heal <amount>` - Heal selected actor
- `suppression [value]` - Get/set suppression (0-100)
- `morale [value]` - Get/set morale
- `stun` - Toggle stun on selected actor
- `combat` - Show combat info for selected actor

**Tactical State:**
- `round` - Show current round number
- `nextround` - Advance to next round
- `faction` - Show current faction
- `endturn` - End the current turn
- `skipai` - Skip the AI turn
- `pause` - Toggle game pause
- `timescale [value]` - Get/set time scale (1.0 = normal)
- `status` - Show tactical state summary
- `win` - Finish mission (victory)

If a command is not recognized and Roslyn is available, the input is evaluated as a C# expression. Use arrow keys to navigate command history.

### Inspector

Displays detailed information about a `GameObj` set via `DevConsole.Inspect()` or the `inspect` command:

- Type name and Unity object name
- Pointer address
- `IsAlive` status
- All readable public properties from the managed proxy type, with their current values

Properties that throw on read display `<error reading>`. Values longer than 120 characters are truncated. Properties named `Pointer`, `WasCollected`, and `ObjectClass` are skipped.

### Watch

Displays all watch expressions added via `DevConsole.Watch()`. Each watch shows its label and the current return value of the getter function. Each watch has an `X` button to remove it.

## Examples

### Logging from a mod

```csharp
DevConsole.Log("My mod initialized successfully");
DevConsole.Log($"Found {weapons.Length} weapons");
```

### Inspecting a game object

```csharp
var weapon = GameQuery.FindByName("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");
DevConsole.Inspect(weapon);
// Console switches to Inspector tab and displays all properties
```

### Adding watch expressions

```csharp
DevConsole.Watch("ARC-762 Damage", () =>
{
    var w = GameQuery.FindByName("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");
    return w.IsNull ? "N/A" : w.ReadFloat("Damage").ToString();
});

DevConsole.Watch("Scene", () => GameState.CurrentScene);
DevConsole.Watch("Weapon Count", () => GameQuery.FindAll("WeaponTemplate").Length.ToString());
```

### Registering a custom panel

```csharp
DevConsole.RegisterPanel("My Mod", (Rect area) =>
{
    float y = area.y;
    GUI.Label(new Rect(area.x, y, area.width, 18), "Custom mod panel");
    y += 20;
    if (GUI.Button(new Rect(area.x, y, 100, 22), "Buff Weapons"))
    {
        foreach (var weapon in GameQuery.FindAll("WeaponTemplate"))
            weapon.WriteFloat("Damage", weapon.ReadFloat("Damage") * 1.5f);
    }
});
```

### Removing a panel

```csharp
DevConsole.RemovePanel("My Mod");
```
