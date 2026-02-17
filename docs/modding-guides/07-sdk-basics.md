# SDK Basics

Data patches and asset replacements can do a lot, but sometimes you need code. The Menace SDK provides a safe, high-level API for interacting with the game.

> [!NOTE]
> This guide provides conceptual overview and examples. For the canonical setup instructions, see
> [Getting Started: Your First Plugin](../coding-sdk/getting-started.md) and [What Is the SDK?](../coding-sdk/what-is-sdk.md).

## Why Code Mods?

Code lets you:
- React to game events (unit death, mission start, etc.)
- Add conditional logic (if health < 50%, do X)
- Create new mechanics that don't exist in the base game
- Display custom UI elements
- Query and modify game state dynamically

## The Modkit Workflow

You have two options for code mods:

### Option A: Source Files (Recommended)

Write `.cs` files in your modpack's `src/` folder. The Modkit compiles them for you:

```
MyMod-modpack/
  modpack.json
  src/
    MyPlugin.cs
```

**Advantages:**
- Source code is visible and auditable
- Modkit can verify security
- Easy to share and collaborate
- Auto-compiled on deployment

### Option B: Prebuilt DLLs

Compile yourself and drop the DLL in `dlls/`:

```
MyMod-modpack/
  modpack.json
  dlls/
    MyMod.dll
```

**Disadvantages:**
- DLLs can contain hidden malicious code
- Less trust from users
- Harder to review for mod packs

We strongly recommend Option A. See the Advanced Code guide for DLL considerations.

## Your First Code Mod

Create `src/HelloWorld.cs`:

```csharp
using MelonLoader;
using HarmonyLib;
using Menace.ModpackLoader;
using Menace.SDK;

namespace MyMod;

public class HelloWorld : IModpackPlugin
{
    private MelonLogger.Instance _log;

    public void OnInitialize(MelonLogger.Instance logger, Harmony harmony)
    {
        _log = logger;
        _log.Msg("Hello from code!");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        _log.Msg($"Scene loaded: {sceneName}");
    }

    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```

Update `modpack.json`:

```json
{
  "manifestVersion": 2,
  "name": "HelloWorld",
  "version": "1.0.0",
  "code": {
    "sources": ["src/HelloWorld.cs"]
  }
}
```

Deploy and run the game. Check the MelonLoader console - you'll see your messages!

## SDK Core APIs

The SDK provides several key classes:

### GameQuery - Finding Objects

```csharp
using Menace.SDK;

// Find all weapons
var weapons = GameQuery.FindAll("WeaponTemplate");

// Find a specific weapon
var rifle = GameQuery.FindByName("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");
```

### GameObj - Working with Objects

```csharp
// GameObj wraps IL2CPP objects with a safe API
var rifle = GameQuery.FindByName("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");
if (rifle.IsNull) return;

// Read fields by name
float damage = rifle.ReadFloat("Damage");
int maxRange = rifle.ReadInt("MaxRange");
string name = rifle.GetName();

// Write fields - returns false on failure
bool ok = rifle.WriteFloat("Damage", 15.0f);
```

### GameState - Scene Events and Deferred Execution

```csharp
using Menace.SDK;

public class MyPlugin : IModpackPlugin
{
    private MelonLogger.Instance _log;

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _log = logger;

        // Subscribe to scene events
        GameState.SceneLoaded += OnSceneLoaded;
        GameState.TacticalReady += OnTacticalReady;
    }

    private void OnSceneLoaded(string sceneName)
    {
        _log.Msg($"Scene loaded: {sceneName}");
    }

    private void OnTacticalReady()
    {
        // Fires 30 frames after tactical scene loads - safe to query game objects
        _log.Msg("Tactical battle ready!");
        var enemies = GameQuery.FindAll("Actor");
        _log.Msg($"Found {enemies.Length} actors");
    }
}
```

> **Note:** For combat events like unit deaths or damage, use the Lua scripting system (which exposes `actor_killed`, `damage_received`, etc.) or implement Harmony patches on the relevant game methods.
```

### Templates - Modifying Game Data

```csharp
using Menace.SDK;

// Read template data
var damage = Templates.ReadField(rifle, "Damage");

// Write template data
Templates.WriteField(rifle, "Damage", 25.0f);

// Clone a template
var heavyRifle = Templates.Clone("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762", "weapon.custom_heavy_rifle");
Templates.WriteField(heavyRifle, "Damage", 20.0f);
```

### DevConsole - Debug Output

```csharp
using Menace.SDK;

// Print to the in-game developer console (~ key)
DevConsole.Log("Something happened");
DevConsole.LogWarning("This might be a problem");
DevConsole.LogError("Something went wrong!");
```

## Example: Weapon Damage Buff on Tactical Start

Let's make a mod that buffs all player weapons when entering tactical combat:

```csharp
using MelonLoader;
using HarmonyLib;
using Menace.ModpackLoader;
using Menace.SDK;

namespace WeaponBuff;

public class WeaponBuffPlugin : IModpackPlugin
{
    private const float DAMAGE_MULTIPLIER = 1.25f;
    private MelonLogger.Instance _log;

    public void OnInitialize(MelonLogger.Instance logger, Harmony harmony)
    {
        _log = logger;
        GameState.TacticalReady += OnTacticalReady;
        DevConsole.Log($"Weapon Buff active! +{(DAMAGE_MULTIPLIER - 1) * 100}% damage");
    }

    private void OnTacticalReady()
    {
        var weapons = GameQuery.FindAll("WeaponTemplate");
        int buffed = 0;

        foreach (var weapon in weapons)
        {
            if (weapon.IsNull) continue;

            float baseDamage = weapon.ReadFloat("Damage");
            float newDamage = baseDamage * DAMAGE_MULTIPLIER;

            if (weapon.WriteFloat("Damage", newDamage))
            {
                buffed++;
            }
        }

        _log.Msg($"Buffed {buffed} weapons");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName) { }
    public void OnUpdate() { }
    public void OnGUI() { }
    public void OnUnload() { }
}
```

## The IModpackPlugin Interface

All plugins implement this interface:

```csharp
public interface IModpackPlugin
{
    void OnInitialize(MelonLogger.Instance logger, Harmony harmony);
    void OnSceneLoaded(int buildIndex, string sceneName);
    void OnUpdate();    // Optional - per-frame logic
    void OnGUI();       // Optional - IMGUI drawing
    void OnUnload();    // Optional - cleanup on shutdown/hot-reload
}
```

- **OnInitialize** - Store the logger and harmony instance, subscribe to events. Do not query game objects here — the game assembly may not be fully initialized.
- **OnSceneLoaded** - React to scene changes. Safe to query objects, apply patches, modify templates.
- **OnUpdate** - Per-frame logic (be careful with performance!)
- **OnGUI** - Draw debug UI using Unity's IMGUI system
- **OnUnload** - Clean up resources, unpatch Harmony, remove watches

## Next Steps

Explore the SDK API documentation:
- [GameQuery](../coding-sdk/api/game-query.md) - Finding game objects
- [GameObj](../coding-sdk/api/game-obj.md) - Object wrapper with ReadInt/WriteFloat/etc.
- [GameState](../coding-sdk/api/game-state.md) - Scene events and deferred execution
- [Templates](../coding-sdk/api/templates.md) - Template reading/writing/cloning
- [DevConsole](../coding-sdk/api/dev-console.md) - Debug output and panels
- [ModSettings](../coding-sdk/api/mod-settings.md) - Persistent mod configuration

---

**Next:** [Template Modding](08-template-modding.md)
