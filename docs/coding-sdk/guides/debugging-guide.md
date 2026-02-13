# Debugging Guide

The Menace SDK includes a built-in developer console, structured error reporting, live object inspection, and a REPL for runtime evaluation. This guide covers how to use each tool.

---

## DevConsole

The DevConsole is an IMGUI overlay with a tabbed panel interface. It is initialized automatically by the modpack loader.

### Opening the Console

Press the **~** (backtick/tilde) key to toggle the console. It renders in the top-left area of the screen.

### Built-in Panels

The console ships with these core panels:

| Tab | Purpose |
|-----|---------|
| **Battle Log** | View combat event telemetry with event-type filters |
| **Log** | View `ModError` entries and `DevConsole.Log()` messages with severity/mod filters |
| **Console** | Run SDK commands; unknown commands fall back to C# REPL evaluation when Roslyn is available |
| **Inspector** | Inspect a `GameObj` instance -- shows all readable properties |
| **Watch** | Live watch expressions that update every frame |

The **Settings** panel is added when `ModSettings` initializes.

---

## Log Panel

All errors, warnings, and info messages reported via `ModError` appear in the Log panel alongside `DevConsole.Log()` output. Each error entry shows:

- Timestamp (`HH:mm:ss`)
- Severity (`Info`, `Warning`, `Error`, `Fatal`)
- Mod ID (the mod that reported the error, or `Menace.SDK` for internal errors)
- Message text
- Occurrence count (deduplicated within a 5-second window)

### Filtering

Use severity toggles (`Err`, `Warn`, `Info`) and per-mod toggles to filter entries.

### Clearing

Click the **Clear** button to remove all entries.

### Error Notification

When errors exist and the console is closed, a small notification appears at the bottom-left of the screen: "N mod errors - press ~ for console". It auto-fades after 8 seconds of no new errors.

---

## ModError: Structured Error Reporting

Use `ModError` in your mod code instead of raw `MelonLogger` for errors that you want to appear in the DevConsole. All SDK methods use `ModError` internally.

### Reporting Errors

```csharp
// Error (default severity)
ModError.Report("MyMod", "Failed to find WeaponTemplate", ex);

// Warning
ModError.Warn("MyMod", "Template not found, will retry");

// Info
ModError.Info("MyMod", "Patches applied successfully");
```

Each call logs to both `MelonLogger` (visible in the MelonLoader console and `Player.log`) and the `ModError` ring buffer (visible in DevConsole).

### Querying Errors

```csharp
// All errors
IReadOnlyList<ModErrorEntry> all = ModError.RecentErrors;

// Errors for a specific mod
IReadOnlyList<ModErrorEntry> mine = ModError.GetErrors("MyMod");
```

### Error Events

Subscribe to `ModError.OnError` for real-time notifications:

```csharp
ModError.OnError += entry =>
{
    if (entry.Severity >= ErrorSeverity.Error)
        DevConsole.Log($"ERROR from {entry.ModId}: {entry.Message}");
};
```

### Rate Limiting and Deduplication

`ModError` enforces a per-mod rate limit (10 errors/second) and deduplicates identical messages within a 5-second window. This prevents runaway logging from hot-path errors (e.g., a per-frame patch that fails repeatedly). Duplicate entries show an occurrence count like `(x15)`.

The global ring buffer holds up to 1000 entries, with a per-mod cap of 200.

---

## Watch Panel

Add live watches to track values that change every frame. Watches are evaluated on every IMGUI draw call.

### Adding Watches

```csharp
// Watch a simple value
DevConsole.Watch("Scene", () => GameState.CurrentScene);

// Watch a game object's field
DevConsole.Watch("Pike Squad", () =>
{
    var pike = GameQuery.FindByName("EntityTemplate", "player_squad.pike");
    return pike.IsNull ? "N/A" : pike.GetName();
});

// Watch an expression
DevConsole.Watch("Weapon Count", () =>
    GameQuery.FindAll("WeaponTemplate").Length.ToString());
```

### Removing Watches

```csharp
DevConsole.Unwatch("Player HP");
```

Or click the **X** button next to a watch in the panel.

### Getter Errors

If a watch getter throws, the panel displays `<error: message>` instead of crashing.

---

## Inspector Panel

Inspect a `GameObj` to view all its public properties via managed reflection.

### Inspecting an Object

```csharp
var weapon = GameQuery.FindByName("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");
DevConsole.Inspect(weapon);
```

This switches to the Inspector tab and displays:
- Type name and Unity object name
- Memory address (`0x...`)
- `IsAlive` status
- All readable public properties on the IL2CppInterop proxy type, with current values

Properties that throw on read show `<error reading>` instead of crashing the panel.

### Inspector Limitations

- Only properties exposed by the IL2CppInterop managed proxy are visible. Private IL2CPP fields without proxy properties are not shown.
- If no managed proxy type exists for the object's class, the panel displays "No managed type available for reflection."
- Values are truncated to 120 characters.

---

## REPL in Console

The REPL compiles and evaluates C# expressions at runtime using Roslyn from the **Console** panel. It is available when the runtime compiler initializes successfully (requires Roslyn assemblies to be present).

### Usage

1. Open the DevConsole (~ key)
2. Switch to the **Console** tab
3. Type an expression in the input field
4. Press **Enter** or click **Run**

### Examples

```
> GameType.Find("WeaponTemplate").IsValid
  true

> GameQuery.FindAll("WeaponTemplate").Length
  42

> var w = GameQuery.FindByName("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762")
  {WeaponTemplate 'weapon.generic_assault_rifle_tier1_ARC_762' @ 0x1A3F0020}

> Templates.ReadField(w, "Damage")
  12.0

> GameState.CurrentScene
  "Tactical"
```

### History Navigation

Use the **Up/Down** arrow keys to navigate through previous inputs.

### Limitations

- Roslyn must be available at runtime. If REPL initialization fails, unknown non-command input will not evaluate as C#.
- The REPL uses the SDK's namespace imports. You may need to fully qualify types outside `Menace.SDK`.
- Compilation errors are shown in red. Runtime exceptions show the inner exception type and message.

---

## DevConsole.Log

Write arbitrary messages to the Log panel:

```csharp
DevConsole.Log("Template scan complete: found 42 weapons");
DevConsole.Log($"Current scene: {GameState.CurrentScene}");
```

The log buffer holds the most recent 200 entries with timestamps.

---

## Custom Panels

Register your own panels in the DevConsole:

```csharp
DevConsole.RegisterPanel("My Debug", (Rect area) =>
{
    float y = area.y;
    GUI.Label(new Rect(area.x, y, area.width, 18), "Custom debug info here");
    y += 20;
    if (GUI.Button(new Rect(area.x, y, 120, 22), "Do something"))
    {
        // your action
    }
});
```

Remove a custom panel:

```csharp
DevConsole.RemovePanel("My Debug");
```

Panel draw callbacks receive a `Rect` parameter defining the content area and should use raw `GUI.*` calls (not `GUILayout`) for positioning. `GUILayout` methods are unavailable in IL2CPP builds.

---

## Timing-Sensitive Debugging

Some issues only manifest at specific moments in the game lifecycle. Use `GameState` helpers to time your debug code.

### RunDelayed

Execute code after a frame delay:

```csharp
GameState.RunDelayed(30, () =>
{
    var templates = GameQuery.FindAll("WeaponTemplate");
    DevConsole.Log($"Weapons after 30 frames: {templates.Length}");
});
```

### RunWhen

Execute code when a condition becomes true:

```csharp
GameState.RunWhen(
    () => GameState.IsScene("Tactical"),
    () =>
    {
        DevConsole.Log("Entered tactical scene");
        DevConsole.Watch("Turn", () => "TODO: read turn counter");
    },
    maxAttempts: 300  // check for up to 300 frames (~5 seconds)
);
```

If the condition is not met within `maxAttempts` frames, the action is discarded.

### Scene Events

```csharp
GameState.SceneLoaded += sceneName =>
{
    DevConsole.Log($"Scene loaded: {sceneName}");
};

GameState.TacticalReady += () =>
{
    DevConsole.Log("Tactical scene ready (30 frames after load)");
};
```

---

## Tips

- **Start in Console with REPL input.** Before writing code, test SDK calls interactively to verify type names, field names, and return values.
- **Check the Log tab first.** When something is not working, the SDK has likely already reported why.
- **Use Watch for per-frame values.** Avoid logging per-frame values to the Log panel -- it fills the buffer quickly. Use Watch instead.
- **Inspect before patching.** Use `DevConsole.Inspect` to examine an object's properties before writing code that reads or writes its fields.
- **Filter errors by mod.** When debugging with multiple mods loaded, use the mod toggles in the Log tab.
- **ModError.Report in patch methods.** Wrap patch method bodies in try/catch and report to `ModError` for visibility in the console.
