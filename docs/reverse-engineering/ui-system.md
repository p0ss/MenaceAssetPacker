# UI System

## Overview

Menace uses Unity's UI Toolkit (formerly UIElements) for its interface. The tactical view has a dedicated HUD system for rendering elements over units.

## UI Architecture

### Key Classes

| Class | Purpose |
|-------|---------|
| UITacticalHUD | Manages all HUD elements in tactical view |
| BaseHUD | Abstract base for world-space HUD elements |
| EntityHUD | HUD for entities (extends BaseHUD) |
| UnitHUD | HUD for units (extends EntityHUD) |
| WorldSpaceIcon | Simple icon rendered at world position |
| BleedingWorldSpaceIcon | Text + icon with animation |

## UITacticalHUD

The central manager for tactical view UI:

```c
public class UITacticalHUD : IDisposable {
    // Fields
    private readonly UITactical m_UITactical;    // +0x10
    private readonly VisualElement m_HUDContainer; // +0x18
    private readonly List<BaseHUD> m_HUDList;    // +0x20
    private readonly List<UnitHUD> m_UnusedUnitHUDs; // +0x28
    private readonly List<WorldSpaceIcon> m_WorldSpaceIcons; // +0x48
    private readonly MovementHUD m_MovementHUD;  // +0x50

    // Key Methods
    public UITactical GetScreen();                               // 0x4FA580
    public UnitHUD AddActor(Actor _actor);                       // 0x7CC240
    public StructureHUD AddStructure(Structure _structure);      // 0x7CC6E0
    public void RemoveEntity(Entity _entity);                    // 0x7CD1C0
    public SimpleWorldSpaceIcon AddSimpleWorldSpaceIcon(...);    // 0x7CC620
    public BleedingWorldSpaceIcon AddBleedingWorldSpaceIcon(...); // 0x7CC460
    public void OnUpdate(float _deltaInSec);                     // 0x7CCDE0
}
```

## Adding Custom Text Over Units

### BleedingWorldSpaceIcon

The game already has a class for rendering text at world positions:

```c
public class BleedingWorldSpaceIcon : WorldSpaceIcon {
    // Constructor (0x7B5F50)
    public void .ctor(
        UITacticalHUD _tacticalHUD,
        string _text,
        Vector3 _worldPos,
        int _width = 24,
        int _height = 24
    );

    // Update text
    public void SetText(string _text);  // 0x7B5CE0
}
```

### Using It

```csharp
// Get the tactical HUD
UITactical uiTactical = // ... get from UIManager or similar
UITacticalHUD hud = uiTactical.GetHUD();

// Add text at a world position
Vector3 unitPos = actor.GetWorldPosition();
var icon = hud.AddBleedingWorldSpaceIcon("Custom Text", unitPos);

// Update later
icon.SetText("New Text");

// Remove when done
hud.RemoveWorldSpaceIcon(icon);
```

## BaseHUD Class

Base class for HUD elements with world-space positioning:

```c
public abstract class BaseHUD : InteractiveElement {
    // Static
    public static bool s_RenderWorldSpaceMarkers;  // 0x0 - Global toggle

    // Fields
    protected readonly FloatAnimation m_DetailsOpacityAnimation; // 0x4F0
    private readonly bool m_SnapToScreenEdges;     // 0x4F8
    private readonly bool m_FadeOutDetailsWhenOffscreen; // 0x4F9
    protected bool m_IsOffscreen;                  // 0x4FA
    private float m_DistanceToCamSquared;          // 0x4FC

    // Abstract Methods
    protected abstract Vector3 GetWorldPos();
    public abstract bool IsVisible();

    // Virtual Methods
    public virtual void OnUpdate(float _deltaInSec);       // 0x7B2480
    protected void UpdatePosition(Vector2 offset, Vector3 worldOffset, int iconSize); // 0x7B24A0
}
```

## UnitHUD

HUD specifically for units, showing health bars, status effects, etc:

```c
public class UnitHUD : EntityHUD {
    // UXML template path
    private const string UXML_PATH = "Tactical/Elements/unit_hud";

    // Created via UITacticalHUD.AddActor()
}
```

## Creating Custom HUD Elements

### Option 1: Use Existing Classes

```csharp
using MelonLoader;
using Menace.UI;
using Menace.UI.Tactical;

[HarmonyPatch(typeof(UITacticalHUD), "OnUpdate")]
class CustomHUDPatch {
    static Dictionary<Actor, BleedingWorldSpaceIcon> customLabels = new();

    static void Postfix(UITacticalHUD __instance) {
        foreach (var actor in GetAllActors()) {
            if (!customLabels.ContainsKey(actor)) {
                var icon = __instance.AddBleedingWorldSpaceIcon(
                    GetCustomLabel(actor),
                    actor.GetWorldPosition()
                );
                customLabels[actor] = icon;
            } else {
                customLabels[actor].SetText(GetCustomLabel(actor));
            }
        }
    }
}
```

### Option 2: Create Custom BaseHUD Subclass

```csharp
public class CustomUnitLabel : BaseHUD {
    private Actor m_Actor;
    private Label m_Label;

    public CustomUnitLabel(Actor actor) : base("path/to/uxml", true, true) {
        m_Actor = actor;
        m_Label = this.Q<Label>("label");
    }

    protected override Vector3 GetWorldPos() {
        return m_Actor.GetWorldPosition();
    }

    public override bool IsVisible() {
        return m_Actor.IsAlive();
    }

    public override void OnUpdate(float deltaInSec) {
        base.OnUpdate(deltaInSec);
        m_Label.text = $"HP: {m_Actor.GetHitpoints()}";
    }
}
```

## Settings Screens

### Adding Custom Settings

Settings use the UI Toolkit pattern with UXML templates:

```c
// From PlayerSettings hierarchy
public class PlayerSettings {
    // Contains various setting categories
    // Each setting is a *PlayerSetting type (IntPlayerSetting, BoolPlayerSetting, etc.)
}
```

### Hooking Into Settings UI

```csharp
// Find and modify the settings panel
[HarmonyPatch(typeof(SettingsWindow), "CreateSettingsPanel")]
class SettingsPatch {
    static void Postfix(SettingsWindow __instance) {
        // Add custom UI elements to the settings panel
        var container = __instance.GetContentContainer();
        // Add your custom VisualElements
    }
}
```

## UI Toolkit Basics for Menace

### UXML Templates

UI templates are stored in Resources as UXML files:
- `Tactical/Elements/unit_hud`
- `Tactical/Elements/bleeding_world_space_icon`
- etc.

### Loading Templates

```csharp
// Game uses Resources.Load for UXML
var template = Resources.Load<VisualTreeAsset>("path/to/uxml");
var element = template.CloneTree();
```

### Key VisualElement Operations

```csharp
// Query elements
var label = root.Q<Label>("elementName");
var button = root.Q<Button>("buttonName");

// Style manipulation
element.style.display = DisplayStyle.Flex;
element.style.opacity = 1.0f;
element.AddToClassList("custom-class");
```

## Screen Coordinate System

For world-to-screen conversion:

```c
// BaseHUD.UpdatePosition converts world to screen
protected void UpdatePosition(Vector2 offsetScreen, Vector3 worldSpaceOffset, int iconSize) {
    Vector3 worldPos = GetWorldPos() + worldSpaceOffset;
    Vector2 screenPos = Camera.WorldToScreenPoint(worldPos);
    // Apply offset and clamp to screen edges if m_SnapToScreenEdges
    // Set element position
}
```

## Accessing UI From Mods

```csharp
// Get references to UI managers
var gameManager = GameManager.Instance;
var uiManager = gameManager.GetUIManager();

// In tactical mode
if (uiManager.GetActiveScreen() is UITactical tacticalUI) {
    var hud = tacticalUI.GetHUD();
    // Now you can add custom elements
}
```

## Common Patterns

### Updating UI Each Frame

```csharp
[HarmonyPatch(typeof(UITacticalHUD), "OnUpdate")]
class UIUpdatePatch {
    static void Postfix(UITacticalHUD __instance, float _deltaInSec) {
        // Your per-frame UI updates
    }
}
```

### Responding to Unit Selection

```csharp
[HarmonyPatch(typeof(UITactical), "OnActorSelected")]
class SelectionPatch {
    static void Postfix(UITactical __instance, Actor _actor) {
        // Update UI based on selected unit
    }
}
```
