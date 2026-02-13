# SDK Basics

Data patches and asset replacements can do a lot, but sometimes you need code. The Menace SDK provides a safe, high-level API for interacting with the game.

> [!WARNING]
> This guide is legacy and includes older SDK snippets (for example `OnLoad`, `GameObj.Get<T>()`, and `GameObj.Set()`).
> Use [Getting Started: Your First Plugin](../coding-sdk/getting-started.md) and [What Is the SDK?](../coding-sdk/what-is-sdk.md) for current lifecycle and API signatures.

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
using Menace.SDK;

namespace MyMod;

public class HelloWorld : IModpackPlugin
{
    public void OnLoad(string modpackName)
    {
        MelonLogger.Msg($"[{modpackName}] Hello from code!");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        MelonLogger.Msg($"Scene loaded: {sceneName}");
    }

    public void OnUpdate() { }
    public void OnGUI() { }
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
// GameObj wraps Unity objects with a convenient API
var rifle = GameQuery.FindByName("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");

// Read fields
float damage = rifle.ReadFloat("Damage");
int maxRange = rifle.ReadInt("MaxRange");

// Write fields
rifle.WriteFloat("Damage", 15.0f);
```

### GameState - Game Events

```csharp
using Menace.SDK;

public class MyPlugin : IModpackPlugin
{
    public void OnLoad(string modpackName)
    {
        // Subscribe to events
        GameState.OnMissionStart += OnMissionStarted;
        GameState.OnUnitDeath += OnUnitDied;
    }

    private void OnMissionStarted()
    {
        MelonLogger.Msg("Mission started!");
    }

    private void OnUnitDied(GameObj unit)
    {
        string name = unit.Get<string>("displayName");
        MelonLogger.Msg($"{name} died!");
    }
}
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

## Example: Heal on Kill

Let's make a mod where units heal when they get a kill:

```csharp
using MelonLoader;
using Menace.SDK;

namespace HealOnKill;

public class HealOnKillPlugin : IModpackPlugin
{
    private const int HEAL_AMOUNT = 20;

    public void OnLoad(string modpackName)
    {
        GameState.OnUnitKill += OnUnitGotKill;
        DevConsole.Log($"[{modpackName}] Heal on Kill active! +{HEAL_AMOUNT} HP per kill");
    }

    private void OnUnitGotKill(GameObj killer, GameObj victim)
    {
        if (killer.IsNull) return;

        // Only heal player units
        if (!killer.Get<bool>("isPlayerControlled")) return;

        int currentHealth = killer.Get<int>("currentHealth");
        int maxHealth = killer.Get<int>("maxHealth");

        int newHealth = Math.Min(currentHealth + HEAL_AMOUNT, maxHealth);
        killer.Set("currentHealth", newHealth);

        string name = killer.Get<string>("displayName");
        DevConsole.Log($"{name} healed for {HEAL_AMOUNT}!");
    }

    public void OnSceneLoaded(int buildIndex, string sceneName) { }
    public void OnUpdate() { }
    public void OnGUI() { }
}
```

## The IModpackPlugin Interface

All plugins implement this interface:

```csharp
public interface IModpackPlugin
{
    void OnLoad(string modpackName);           // Called once when mod loads
    void OnSceneLoaded(int index, string name); // Called on scene transitions
    void OnUpdate();                            // Called every frame
    void OnGUI();                               // Called for IMGUI drawing
}
```

- **OnLoad** - Initialize your mod, subscribe to events
- **OnSceneLoaded** - React to scene changes, reinitialize if needed
- **OnUpdate** - Per-frame logic (be careful with performance!)
- **OnGUI** - Draw debug UI using Unity's IMGUI system

## Next Steps

Explore the SDK API documentation:
- `GameQuery` - Finding game objects
- `GameObj` - Object wrapper with Get/Set
- `GameState` - Events and state tracking
- `Templates` - Template reading/writing/cloning
- `DevConsole` - Debug output
- `ModSettings` - Persistent mod configuration

---

**Next:** [Template Modding](08-template-modding.md)
