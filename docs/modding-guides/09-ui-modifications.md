# UI Modifications

Custom UI lets you display debug info, create configuration menus, and add in-game overlays. The Menace SDK provides several approaches depending on your needs.

## UI Options Overview

| Approach | Best For | Complexity |
|----------|----------|------------|
| DevConsole panels | Debug tools, inspectors | Low |
| Watch expressions | Live value monitoring | Very Low |
| ModSettings | Configuration UI | Low |
| OnGUI overlays | In-game HUDs, indicators | Medium |

All UI in Menace mods uses Unity's **IMGUI** (Immediate Mode GUI) system. This is the `GUI.*` API - you draw every frame in `OnGUI()`.

**Important:** Due to IL2CPP method unstripping limitations, `GUILayout.*` methods are not available. Use raw `GUI.*` calls with explicit `Rect` positioning instead.

## DevConsole Panel System

The developer console (toggle with `~` key) supports custom panels. This is the easiest way to add debug UI.

### Registering a Panel

```csharp
using Menace.SDK;

public class MyPlugin : IModpackPlugin
{
    public void OnLoad(string modpackName)
    {
        DevConsole.RegisterPanel("My Debug", DrawMyPanel);
    }

    private void DrawMyPanel(Rect area)
    {
        // area is the content region where you can draw
        float y = area.y;

        GUI.Label(new Rect(area.x, y, area.width, 20), "My Custom Panel");
        y += 24;

        if (GUI.Button(new Rect(area.x, y, 100, 24), "Do Something"))
        {
            DevConsole.Log("Button clicked!");
        }
    }

    // ... other interface methods
}
```

The callback receives a `Rect` defining the drawable area. Draw your UI within these bounds using `GUI.*` calls.

### Removing a Panel

```csharp
DevConsole.RemovePanel("My Debug");
```

## IMGUI Basics

Unity's IMGUI draws UI every frame. All positioning uses `Rect` structures.

### Rect Positioning

```csharp
// Rect(x, y, width, height)
// x,y = top-left corner position
// width,height = size

var labelRect = new Rect(10, 10, 200, 20);
GUI.Label(labelRect, "Hello World");
```

### Common GUI Controls

```csharp
private void DrawMyPanel(Rect area)
{
    float y = area.y;
    float x = area.x;
    float w = area.width;

    // Label - static text
    GUI.Label(new Rect(x, y, w, 20), "Status: Ready");
    y += 24;

    // Button - returns true when clicked
    if (GUI.Button(new Rect(x, y, 100, 24), "Click Me"))
    {
        // Handle click
    }
    y += 28;

    // Toggle - checkbox
    _myBool = GUI.Toggle(new Rect(x, y, 150, 20), _myBool, "Enable Feature");
    y += 24;

    // Horizontal slider
    _myFloat = GUI.HorizontalSlider(new Rect(x, y, 200, 20), _myFloat, 0f, 100f);
    y += 24;

    // Text field
    _myText = GUI.TextField(new Rect(x, y, 200, 20), _myText);
    y += 24;

    // Box - draws a bordered rectangle
    GUI.Box(new Rect(x, y, 200, 60), "Boxed Content");
}

private bool _myBool;
private float _myFloat = 50f;
private string _myText = "";
```

### Styling with GUIStyle

```csharp
private GUIStyle _headerStyle;
private GUIStyle _errorStyle;

private void InitStyles()
{
    if (_headerStyle != null) return;

    _headerStyle = new GUIStyle(GUI.skin.label);
    _headerStyle.fontSize = 16;
    _headerStyle.fontStyle = FontStyle.Bold;
    _headerStyle.normal.textColor = Color.white;

    _errorStyle = new GUIStyle(GUI.skin.label);
    _errorStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);
}

private void DrawMyPanel(Rect area)
{
    InitStyles();

    GUI.Label(new Rect(area.x, area.y, area.width, 24), "Header Text", _headerStyle);
    GUI.Label(new Rect(area.x, area.y + 28, area.width, 20), "Error message!", _errorStyle);
}
```

## Example: Custom Debug Panel

Here's a complete example of a debug panel that displays unit information:

```csharp
using MelonLoader;
using Menace.SDK;
using UnityEngine;

namespace DebugTools;

public class UnitDebugPanel : IModpackPlugin
{
    private Vector2 _scroll;
    private GUIStyle _labelStyle;
    private GUIStyle _headerStyle;

    public void OnLoad(string modpackName)
    {
        DevConsole.RegisterPanel("Units", DrawUnitsPanel);
        DevConsole.Log($"[{modpackName}] Unit debug panel registered");
    }

    private void DrawUnitsPanel(Rect area)
    {
        InitStyles();

        float y = area.y;
        const float lineHeight = 20f;

        // Header
        GUI.Label(new Rect(area.x, y, area.width, 24),
            "Active Units", _headerStyle);
        y += 28;

        // Find all units
        var units = GameQuery.FindAll("Actor");

        GUI.Label(new Rect(area.x, y, area.width, lineHeight),
            $"Found {units.Length} units", _labelStyle);
        y += lineHeight + 4;

        // List units (simple manual scroll)
        foreach (var unit in units)
        {
            if (y > area.yMax) break; // Stop if we've run out of space

            string name = unit.GetName() ?? "<unnamed>";
            int hp = 0;
            int maxHp = 0;

            try
            {
                hp = unit.Get<int>("currentHealth");
                maxHp = unit.Get<int>("maxHealth");
            }
            catch { }

            string line = $"  {name}: {hp}/{maxHp} HP";
            GUI.Label(new Rect(area.x, y, area.width, lineHeight), line, _labelStyle);
            y += lineHeight;
        }
    }

    private void InitStyles()
    {
        if (_labelStyle != null) return;

        _labelStyle = new GUIStyle(GUI.skin.label);
        _labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        _labelStyle.fontSize = 13;

        _headerStyle = new GUIStyle(_labelStyle);
        _headerStyle.fontSize = 15;
        _headerStyle.fontStyle = FontStyle.Bold;
        _headerStyle.normal.textColor = Color.white;
    }

    public void OnSceneLoaded(int buildIndex, string sceneName) { }
    public void OnUpdate() { }
    public void OnGUI() { }
}
```

## Watch Expressions

For quick live monitoring without building a full panel, use watch expressions:

```csharp
using Menace.SDK;

public void OnLoad(string modpackName)
{
    // Add watches - they appear in the Watch panel
    DevConsole.Watch("Player HP", () =>
    {
        var player = GameQuery.FindByName("Actor", "PlayerSquaddie");
        if (player.IsNull) return "N/A";
        return $"{player.Get<int>("currentHealth")}/{player.Get<int>("maxHealth")}";
    });

    DevConsole.Watch("Enemy Count", () =>
    {
        var enemies = GameQuery.FindAll("Actor");
        int count = 0;
        foreach (var e in enemies)
        {
            if (e.Get<int>("factionIndex") == 1) count++;
        }
        return count.ToString();
    });

    DevConsole.Watch("Game Time", () => Time.time.ToString("F1") + "s");
}

// Remove a watch when no longer needed
public void Cleanup()
{
    DevConsole.Unwatch("Player HP");
}
```

Watches update every frame and appear in the DevConsole's "Watch" tab.

## ModSettings for Configuration UI

For user-configurable options, use `ModSettings`. It automatically generates UI in the DevConsole's Settings panel:

```csharp
using Menace.SDK;

public void OnLoad(string modpackName)
{
    ModSettings.Register("My Mod", settings =>
    {
        settings.AddHeader("Gameplay");
        settings.AddToggle("GodMode", "God Mode", false);
        settings.AddSlider("DamageMultiplier", "Damage Multiplier", 0.1f, 5f, 1f);
        settings.AddNumber("StartingMoney", "Starting Money", 0, 10000, 1000);

        settings.AddHeader("Display");
        settings.AddToggle("ShowOverlay", "Show Health Overlay", true);
        settings.AddDropdown("OverlayPosition", "Overlay Position",
            new[] { "Top Left", "Top Right", "Bottom Left", "Bottom Right" },
            "Top Left");
    });
}

// Read settings anywhere in your code
public void OnUpdate()
{
    bool godMode = ModSettings.Get<bool>("My Mod", "GodMode");
    float damageMulti = ModSettings.Get<float>("My Mod", "DamageMultiplier");

    if (godMode)
    {
        // Apply god mode logic
    }
}
```

Settings are automatically persisted to `UserData/ModSettings.json`.

### Available Setting Types

| Method | Type | Description |
|--------|------|-------------|
| `AddHeader(label)` | - | Section header (visual only) |
| `AddToggle(key, label, default)` | bool | Checkbox |
| `AddSlider(key, label, min, max, default)` | float | Horizontal slider |
| `AddNumber(key, label, min, max, default)` | int | Integer with +/- buttons |
| `AddDropdown(key, label, options, default)` | string | Selection from options |
| `AddText(key, label, default)` | string | Text input field |

### Reacting to Setting Changes

```csharp
ModSettings.OnSettingChanged += (modName, key, value) =>
{
    if (modName == "My Mod" && key == "DamageMultiplier")
    {
        DevConsole.Log($"Damage multiplier changed to {value}");
    }
};
```

## Overlay Displays with OnGUI

For in-game overlays that display outside the DevConsole, use `OnGUI()`:

```csharp
public class HealthOverlay : IModpackPlugin
{
    private bool _showOverlay = true;
    private GUIStyle _overlayStyle;

    public void OnLoad(string modpackName)
    {
        ModSettings.Register("Health Overlay", settings =>
        {
            settings.AddToggle("Enabled", "Show Overlay", true);
        });
    }

    public void OnGUI()
    {
        _showOverlay = ModSettings.Get<bool>("Health Overlay", "Enabled");
        if (!_showOverlay) return;

        InitStyle();

        // Draw in top-left corner
        float y = 10;
        GUI.Label(new Rect(10, y, 300, 24), "=== Squad Health ===", _overlayStyle);
        y += 28;

        var units = GameQuery.FindAll("Actor");
        foreach (var unit in units)
        {
            if (!unit.Get<bool>("isPlayerControlled")) continue;

            string name = unit.GetName() ?? "Unknown";
            int hp = unit.Get<int>("currentHealth");
            int maxHp = unit.Get<int>("maxHealth");
            float pct = maxHp > 0 ? (float)hp / maxHp : 0;

            // Color based on health
            Color c = pct > 0.5f ? Color.green : (pct > 0.25f ? Color.yellow : Color.red);
            _overlayStyle.normal.textColor = c;

            GUI.Label(new Rect(10, y, 300, 20), $"{name}: {hp}/{maxHp}", _overlayStyle);
            y += 22;
        }
    }

    private void InitStyle()
    {
        if (_overlayStyle != null) return;

        _overlayStyle = new GUIStyle(GUI.skin.label);
        _overlayStyle.fontSize = 14;
        _overlayStyle.fontStyle = FontStyle.Bold;

        // Add shadow/outline for readability
        var bg = new Texture2D(1, 1);
        bg.SetPixel(0, 0, new Color(0, 0, 0, 0.5f));
        bg.Apply();
        _overlayStyle.normal.background = bg;
        _overlayStyle.padding = new RectOffset(4, 4, 2, 2);
    }

    public void OnSceneLoaded(int buildIndex, string sceneName) { }
    public void OnUpdate() { }
}
```

## Example: Unit Health Overlay

A complete example showing health bars above units in the game world:

```csharp
using MelonLoader;
using Menace.SDK;
using UnityEngine;

namespace HealthBars;

public class UnitHealthBars : IModpackPlugin
{
    private GUIStyle _barBackgroundStyle;
    private GUIStyle _barFillStyle;
    private GUIStyle _nameStyle;
    private Texture2D _greenTex;
    private Texture2D _yellowTex;
    private Texture2D _redTex;
    private Texture2D _bgTex;

    public void OnLoad(string modpackName)
    {
        ModSettings.Register("Health Bars", settings =>
        {
            settings.AddToggle("Enabled", "Show Health Bars", true);
            settings.AddToggle("ShowEnemies", "Show Enemy Health", false);
            settings.AddSlider("BarWidth", "Bar Width", 30f, 100f, 60f);
            settings.AddSlider("VerticalOffset", "Vertical Offset", 0f, 100f, 40f);
        });
    }

    public void OnGUI()
    {
        if (!ModSettings.Get<bool>("Health Bars", "Enabled")) return;

        InitStyles();

        var cam = Camera.main;
        if (cam == null) return;

        bool showEnemies = ModSettings.Get<bool>("Health Bars", "ShowEnemies");
        float barWidth = ModSettings.Get<float>("Health Bars", "BarWidth");
        float yOffset = ModSettings.Get<float>("Health Bars", "VerticalOffset");

        var actors = GameQuery.FindAll("Actor");
        foreach (var actor in actors)
        {
            if (actor.IsNull) continue;

            bool isPlayer = actor.Get<bool>("isPlayerControlled");
            if (!isPlayer && !showEnemies) continue;

            int hp = actor.Get<int>("currentHealth");
            int maxHp = actor.Get<int>("maxHealth");
            if (hp <= 0 || maxHp <= 0) continue;

            // Get world position and convert to screen
            var worldPos = GetActorPosition(actor);
            if (worldPos == Vector3.zero) continue;

            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            // Check if in front of camera
            if (screenPos.z < 0) continue;

            // Convert to GUI coordinates (Y is flipped)
            float screenX = screenPos.x;
            float screenY = Screen.height - screenPos.y - yOffset;

            // Draw health bar
            float pct = (float)hp / maxHp;
            float barHeight = 6f;

            // Background
            GUI.Box(new Rect(screenX - barWidth / 2, screenY, barWidth, barHeight), "", _barBackgroundStyle);

            // Fill
            var fillStyle = GetFillStyle(pct);
            GUI.Box(new Rect(screenX - barWidth / 2, screenY, barWidth * pct, barHeight), "", fillStyle);

            // Name below bar
            string name = actor.GetName() ?? "";
            if (!string.IsNullOrEmpty(name))
            {
                GUI.Label(new Rect(screenX - 50, screenY + barHeight + 2, 100, 16), name, _nameStyle);
            }
        }
    }

    private Vector3 GetActorPosition(GameObj actor)
    {
        try
        {
            // Try to get Transform position
            var transform = actor.Get<Transform>("transform");
            if (transform != null)
                return transform.position;
        }
        catch { }

        return Vector3.zero;
    }

    private GUIStyle GetFillStyle(float pct)
    {
        if (pct > 0.5f)
        {
            _barFillStyle.normal.background = _greenTex;
        }
        else if (pct > 0.25f)
        {
            _barFillStyle.normal.background = _yellowTex;
        }
        else
        {
            _barFillStyle.normal.background = _redTex;
        }
        return _barFillStyle;
    }

    private void InitStyles()
    {
        if (_barBackgroundStyle != null) return;

        _bgTex = MakeTexture(new Color(0, 0, 0, 0.7f));
        _greenTex = MakeTexture(new Color(0.2f, 0.8f, 0.2f, 1f));
        _yellowTex = MakeTexture(new Color(0.9f, 0.8f, 0.1f, 1f));
        _redTex = MakeTexture(new Color(0.9f, 0.2f, 0.2f, 1f));

        _barBackgroundStyle = new GUIStyle(GUI.skin.box);
        _barBackgroundStyle.normal.background = _bgTex;

        _barFillStyle = new GUIStyle(GUI.skin.box);
        _barFillStyle.normal.background = _greenTex;

        _nameStyle = new GUIStyle(GUI.skin.label);
        _nameStyle.fontSize = 10;
        _nameStyle.alignment = TextAnchor.MiddleCenter;
        _nameStyle.normal.textColor = Color.white;
    }

    private Texture2D MakeTexture(Color color)
    {
        var tex = new Texture2D(1, 1);
        tex.hideFlags = HideFlags.HideAndDontSave;
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    public void OnSceneLoaded(int buildIndex, string sceneName) { }
    public void OnUpdate() { }
}
```

## Best Practices

### Performance

IMGUI redraws every frame, so keep `OnGUI()` fast:

```csharp
// BAD - query every frame
public void OnGUI()
{
    var units = GameQuery.FindAll("Actor"); // Slow!
    foreach (var u in units) { ... }
}

// GOOD - cache and update periodically
private GameObj[] _cachedUnits;
private float _lastUpdate;

public void OnUpdate()
{
    if (Time.time - _lastUpdate > 0.5f) // Update twice per second
    {
        _cachedUnits = GameQuery.FindAll("Actor");
        _lastUpdate = Time.time;
    }
}

public void OnGUI()
{
    if (_cachedUnits == null) return;
    foreach (var u in _cachedUnits) { ... }
}
```

### Positioning

Screen coordinates start at top-left (0,0). Use `Screen.width` and `Screen.height` for responsive layouts:

```csharp
// Top-right corner
var rect = new Rect(Screen.width - 210, 10, 200, 100);

// Bottom-center
var rect = new Rect((Screen.width - 200) / 2, Screen.height - 60, 200, 50);

// Percentage-based sizing
float w = Screen.width * 0.3f;
float h = Screen.height * 0.2f;
var rect = new Rect(10, 10, w, h);
```

### Styling Consistency

Create styles once and reuse them. Handle texture destruction on scene changes:

```csharp
private GUIStyle _myStyle;
private bool _stylesValid;

private void EnsureStyles()
{
    // Textures can be destroyed on scene change
    if (_myStyle != null && _myStyle.normal.background == null)
        _stylesValid = false;

    if (_stylesValid) return;
    _stylesValid = true;

    _myStyle = new GUIStyle(GUI.skin.label);
    // ... configure style
}
```

### Visibility Controls

Always provide a way to hide your UI:

```csharp
public void OnLoad(string modpackName)
{
    ModSettings.Register("My Overlay", settings =>
    {
        settings.AddToggle("Visible", "Show Overlay", true);
        settings.AddSlider("Opacity", "Opacity", 0f, 1f, 0.8f);
    });
}

public void OnGUI()
{
    if (!ModSettings.Get<bool>("My Overlay", "Visible")) return;

    float opacity = ModSettings.Get<float>("My Overlay", "Opacity");
    GUI.color = new Color(1, 1, 1, opacity);

    // Draw UI...

    GUI.color = Color.white; // Reset
}
```

### Avoiding Conflicts

The DevConsole handles input blocking when visible. For your own overlays:

```csharp
public void OnGUI()
{
    // Skip drawing if DevConsole is open and mouse is over it
    if (DevConsole.IsMouseOverConsole) return;

    // Your overlay drawing...
}
```

---

**Previous:** [Template Modding](08-template-modding.md) | **Next:** [Advanced Code](10-advanced-code.md)

**Back to:** [Modding Index](index.md)
