# Advanced Code & Security

This guide covers advanced modding techniques and the important security considerations around code mods.

## When You Need More Than the SDK

The SDK covers common use cases, but sometimes you need:
- Direct Harmony patches to intercept game methods
- Access to game systems the SDK doesn't expose
- Performance-critical code that bypasses SDK wrappers
- Integration with external libraries

For these cases, you'll write more advanced code or ship prebuilt DLLs.

## Harmony Patching

Harmony lets you intercept and modify game methods at runtime.

### Patch Types

| Type | When It Runs | Use Case |
|------|--------------|----------|
| **Prefix** | Before original method | Skip method, modify parameters |
| **Postfix** | After original method | Modify return value, react to calls |
| **Transpiler** | At patch time | Rewrite IL instructions (advanced) |

### Basic Example

```csharp
using HarmonyLib;
using MelonLoader;

namespace MyMod;

[HarmonyPatch(typeof(SomeGameClass), "SomeMethod")]
public class MyPatch
{
    // Run before the original method
    // Return false to skip the original
    static bool Prefix(SomeGameClass __instance, ref int damage)
    {
        damage = damage / 2; // Halve incoming damage
        return true; // Run original method
    }

    // Run after the original method
    static void Postfix(SomeGameClass __instance, ref int __result)
    {
        __result *= 2; // Double the return value
    }
}
```

### Setting Up Harmony

Initialize Harmony in your plugin's `OnLoad`:

```csharp
public void OnLoad(string modpackName)
{
    var harmony = new HarmonyLib.Harmony($"com.myname.{modpackName}");
    harmony.PatchAll(); // Apply all [HarmonyPatch] classes in this assembly
}
```

Harmony patches are powerful but dangerous:
- Wrong patches can crash the game
- Incompatible with game updates
- Can conflict with other mods

Use SDK methods when possible; Harmony when necessary.

## IL2CPP Considerations

Menace uses Unity's IL2CPP backend. This means:
- Game code is compiled to C++
- You interact via Il2CppInterop proxy types
- Some .NET features don't work (reflection limitations)
- Generic types need special handling

```csharp
// IL2CPP requires explicit type conversion
var gameObject = obj.TryCast<UnityEngine.GameObject>();

// Collections need Il2Cpp wrappers
var list = new Il2CppSystem.Collections.Generic.List<int>();
```

The SDK handles most IL2CPP complexity. When working outside the SDK, consult the Il2CppInterop documentation.

---

## Building DLLs

If you need prebuilt DLLs (external dependencies, complex multi-file projects, team CI/CD), here's how to set up a project.

### When to Use DLLs vs Source

| Use DLLs When | Use Source (.cs) When |
|---------------|----------------------|
| External NuGet dependencies | Single-file mods |
| Multi-file projects with interfaces | Learning/experimenting |
| CI/CD build pipelines | Community mods (auditable) |
| Performance testing (Debug/Release) | SDK-based mods |

**Rule of thumb:** If it can work as source files, ship it as source for SourceVerified status.

### Project Setup

Create a .NET Framework 4.7.2 class library:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <LangVersion>latest</LangVersion>
    <OutputPath>..\MyMod-modpack\dlls\</OutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>
</Project>
```

### Required References

| Assembly | Location |
|----------|----------|
| `MelonLoader.dll` | `<Game>/MelonLoader/net6/` |
| `Il2CppInterop.Runtime.dll` | `<Game>/MelonLoader/net6/` |
| `0Harmony.dll` | `<Game>/MelonLoader/net6/` |
| `Menace.ModpackLoader.dll` | `<Game>/Mods/` |
| `Assembly-CSharp.dll` | `<Game>/MelonLoader/Il2CppAssemblies/` |

Add references with `<Private>false</Private>` (they're runtime dependencies).

### Packaging

Place DLLs in your modpack's `dlls/` folder and reference in `modpack.json`:

```json
{
  "code": {
    "dlls": ["dlls/MyMod.dll"]
  }
}
```

---

## Security: The DLL Problem

Here's the uncomfortable truth: **DLL mods can do anything**.

A malicious DLL could:
- Read your files and passwords
- Install malware
- Mine cryptocurrency
- Join your computer to a botnet
- Delete your save files

Unity mods run with **full system permissions**. There's no sandbox.

### The Trust Hierarchy

| Mod Type | Trust Level | Why |
|----------|-------------|-----|
| Data patches (JSON) | **Safe** | Can only change values the loader allows |
| Asset replacements | **Safe** | Just image/model files |
| Source code (.cs) | **Auditable** | You can read exactly what it does |
| Prebuilt DLLs | **Dangerous** | Could contain anything |

### Source-Verified Mods

The Modkit supports a `securityStatus` field:

```json
{
  "securityStatus": "SourceVerified"
}
```

This means:
- All code is provided as source files
- The Modkit compiles it (no hidden code)
- Anyone can audit the source

For maximum safety, **only use mods marked SourceVerified**.

### If You Must Use DLLs

Sometimes there's no alternative (closed-source dependency, performance requirements, etc.):

1. **Only download from trusted sources**
2. **Check if source is available** - many DLL mods have public source
3. **Scan with antivirus** - won't catch everything, but helps
4. **Run in a VM** if you're paranoid
5. **Check mod reputation** - established modders with history

### For Mod Authors: Why Share Source

If you're making mods, consider:

**Sharing source code:**
- Users trust you more
- Others can learn from your work
- Community can help fix bugs
- Mod survives if you disappear
- Gets the "SourceVerified" badge

**Keeping source private:**
- Others can't "steal" your work (debatable benefit)
- Users are suspicious
- Can't get verified status

We strongly encourage source sharing. The modding community thrives on openness.

---

## Extending the SDK

Found something the SDK doesn't support? You have options:

### 1. Request an SDK Addition

File an issue or PR on the Modkit repository. If it's broadly useful, we'll add it:

- New events (`GameState.OnX`)
- New query methods
- Access to game systems

This benefits everyone and keeps mods source-verifiable.

### 2. Write a Wrapper

Create your own helper that uses SDK primitives:

```csharp
public static class MyHelpers
{
    public static void HealAllUnits(int amount)
    {
        var units = GameQuery.FindAll("UnitTemplate");
        foreach (var unit in units)
        {
            if (unit.Get<bool>("isPlayerControlled"))
            {
                int current = unit.Get<int>("currentHealth");
                int max = unit.Get<int>("maxHealth");
                unit.Set("currentHealth", Math.Min(current + amount, max));
            }
        }
    }
}
```

### 3. Harmony Patch (Last Resort)

If you must patch game methods directly:

- Document exactly what you're patching and why
- Include the patch in your source (not a separate DLL)
- Test thoroughly after game updates
- Be prepared for breakage

---

## Debugging Tips

### MelonLoader Console

Press F5 in-game to toggle the MelonLoader console. All `MelonLogger.Msg()` output appears here.

### DevConsole (In-Game)

Press ~ (tilde) for the Menace developer console:

```csharp
DevConsole.Log("Debug info here");
DevConsole.LogError("This is red!");
```

### Unity Player.log

Check `%USERPROFILE%\AppData\LocalLow\<Company>\<Game>\Player.log` for Unity errors.

### Attach Debugger

For serious debugging:
1. Build with debug symbols
2. Launch game
3. Attach Visual Studio/Rider to the game process
4. Set breakpoints in your code

---

## Publishing Your Mod

Ready to share?

1. **Test thoroughly** - Play through various scenarios
2. **Write a README** - What does it do? How to install?
3. **Include source** - Get that SourceVerified status
4. **Version properly** - Semantic versioning (1.0.0, 1.0.1, etc.)
5. **List compatibility** - Which game version(s)?

Good places to publish:

- Thunderstore
- Moddb / Mod.io  
- Game banana
- GitHub Releases   (poor discovery)
- Nexus Mods (taken over by crypto bros) 
- Discord communities 

---

## Summary

| Approach | Difficulty | Safety | Flexibility |
|----------|------------|--------|-------------|
| Data patches | Easy | Safe | Limited |
| Asset replacement | Easy | Safe | Visual only |
| SDK source mods | Medium | Auditable | Good |
| Harmony patches | Hard | Auditable* | Full |
| Prebuilt DLLs | Varies | Dangerous | Full |

*If source is provided

Start simple. Use data patches and SDK methods. Only reach for advanced techniques when necessary.

Happy modding!

---

**Back to:** [Modding Index](index.md)
