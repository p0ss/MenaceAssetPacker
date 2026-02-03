# DevConsole

`Menace.SDK.DevConsole` -- IMGUI-based developer console overlay with a tabbed panel system.

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

### RegisterPanel

```csharp
public static void RegisterPanel(string name, Action drawCallback)
```

Register a custom panel in the console. The `drawCallback` is invoked during `OnGUI` when the panel's tab is active. If a panel with the same name already exists, its callback is replaced.

The callback should use `GUILayout` calls to draw its content. Exceptions in the callback are caught and displayed as an error message in the panel area.

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

The console initializes with five built-in panels:

### Errors

Displays all entries from `ModError.RecentErrors` in reverse chronological order (newest first). Entries are color-coded by severity: red for errors/fatal, yellow for warnings, blue for info. Includes a text filter by mod ID and a Clear button.

Duplicate errors show an occurrence count suffix (e.g., `(x5)`).

### Log

A scrollable list of messages added via `DevConsole.Log()`. Each entry is prefixed with a timestamp in `HH:mm:ss` format.

### Inspector

Displays detailed information about a `GameObj` set via `DevConsole.Inspect()`:

- Type name and Unity object name
- Pointer address
- `IsAlive` status
- All readable public properties from the managed proxy type, with their current values

Properties that throw on read display `<error reading>`. Values longer than 120 characters are truncated. Properties named `Pointer`, `WasCollected`, and `ObjectClass` are skipped.

### Watch

Displays all watch expressions added via `DevConsole.Watch()`. Each watch shows its label and the current return value of the getter function. Each watch has an `X` button to remove it.

### REPL

An interactive C# evaluation panel. See [REPL](repl.md) for details. This panel is registered by the REPL subsystem during initialization and is only available when Roslyn is present.

## Examples

### Logging from a mod

```csharp
DevConsole.Log("My mod initialized successfully");
DevConsole.Log($"Found {agents.Length} agents");
```

### Inspecting a game object

```csharp
var player = GameQuery.FindByName("Agent", "Player");
DevConsole.Inspect(player);
// Console switches to Inspector tab and displays all properties
```

### Adding watch expressions

```csharp
DevConsole.Watch("Player HP", () =>
{
    var p = GameQuery.FindByName("Agent", "Player");
    return p.IsNull ? "N/A" : p.ReadInt("health").ToString();
});

DevConsole.Watch("Scene", () => GameState.CurrentScene);
DevConsole.Watch("Agent Count", () => GameQuery.FindAll("Agent").Length.ToString());
```

### Registering a custom panel

```csharp
DevConsole.RegisterPanel("My Mod", () =>
{
    GUILayout.Label("Custom mod panel");
    if (GUILayout.Button("Heal All"))
    {
        foreach (var agent in GameQuery.FindAll("Agent"))
            agent.WriteInt("health", 100);
    }
});
```

### Removing a panel

```csharp
DevConsole.RemovePanel("My Mod");
```
