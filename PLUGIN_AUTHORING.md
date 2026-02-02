# Plugin Authoring Guide

How to write code plugins for Menace modpacks using the `IModpackPlugin` interface.

## What is IModpackPlugin?

`IModpackPlugin` is the contract that all code-bearing modpacks implement. ModpackLoader discovers classes implementing this interface in compiled DLLs, instantiates them, and forwards Unity lifecycle events.

Use a code plugin when your modpack needs runtime behaviour: Harmony patches, IMGUI overlays, input handling, coroutines, or any logic beyond static data/asset replacement. Data-only modpacks (stat patches, asset swaps, bundles) don't need a plugin at all.

## Lifecycle methods

```csharp
public interface IModpackPlugin
{
    void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony);
    void OnSceneLoaded(int buildIndex, string sceneName);
    void OnUpdate() { }   // default no-op
    void OnGUI() { }      // default no-op
}
```

### Call order

1. **OnInitialize** -- called once after the DLL is loaded and the plugin is instantiated. You receive a per-modpack logger and a pre-created `Harmony` instance (ID: `com.menace.modpack.<assembly>.<type>`). Store both as fields.
2. **OnSceneLoaded** -- called every time Unity finishes loading a scene. Equivalent to `MelonMod.OnSceneWasLoaded`. Use this to trigger delayed setup (e.g. wait for templates, then apply patches).
3. **OnUpdate** -- called every frame. Override only if you need per-frame input or logic. Default is a no-op.
4. **OnGUI** -- called every frame during the IMGUI pass. Override only if you draw IMGUI elements. Default is a no-op.

All calls are wrapped in try/catch by `DllLoader`; a crash in one plugin won't take down others.

## Modpack structure

```
MyModpack/
  modpack.json
  src/
    MyPlugin.cs
```

The `modpack.json` manifest declares everything -- metadata, data patches, asset replacements, and the `code` section for compiled plugins.

## The `code` manifest section

```json
{
  "code": {
    "sources": ["src/MyPlugin.cs"],
    "references": [
      "MelonLoader",
      "0Harmony",
      "Assembly-CSharp",
      "Menace.ModpackLoader"
    ],
    "prebuiltDlls": []
  }
}
```

| Field | Description |
|-------|-------------|
| `sources` | Relative paths to `.cs` files. Compiled by `CompilationService` at deploy time. |
| `references` | Assembly names to resolve. See *Available references* below. |
| `prebuiltDlls` | Paths to pre-compiled DLLs to include without recompilation. |

`CompilationService` uses Roslyn to compile sources into a single DLL placed in `build/`. During deploy, that DLL is copied to the modpack's `dlls/` directory in the game's `Mods/` folder.

## Available references

These assemblies can be listed in `references` by name:

| Name | Location | Purpose |
|------|----------|---------|
| `MelonLoader` | `MelonLoader/MelonLoader.dll` | Logger, coroutines |
| `0Harmony` | `MelonLoader/0Harmony.dll` | Harmony patching |
| `Il2CppInterop.Runtime` | `MelonLoader/Il2CppInterop.Runtime.dll` | IL2CPP type proxies |
| `Il2CppInterop.Common` | `MelonLoader/Il2CppInterop.Common.dll` | IL2CPP common utilities |
| `Il2Cppmscorlib` | `Il2CppAssemblies/Il2Cppmscorlib.dll` | IL2CPP base types |
| `Assembly-CSharp` | `Il2CppAssemblies/Assembly-CSharp.dll` | Game types |
| `UnityEngine.CoreModule` | `Il2CppAssemblies/UnityEngine.CoreModule.dll` | Core Unity API |
| `UnityEngine.InputLegacyModule` | `Il2CppAssemblies/...` | `Input.GetKeyDown` etc. |
| `UnityEngine.IMGUIModule` | `Il2CppAssemblies/...` | `GUI.*`, `GUIStyle` |
| `UnityEngine.TextRenderingModule` | `Il2CppAssemblies/...` | Font/text rendering |
| `Menace.ModpackLoader` | `Mods/Menace.ModpackLoader.dll` | `IModpackPlugin` interface |

`ReferenceResolver` searches the game install path for these by name. Only list what you actually use.

## Migrating from MelonMod

If you have an existing `MelonMod`, follow this checklist:

1. **Remove assembly attributes** -- delete `[assembly: MelonInfo(...)]` and `[assembly: MelonGame(...)]`.
2. **Change base class** -- `class MyMod : MelonMod` becomes `class MyPlugin : IModpackPlugin`.
3. **Replace lifecycle methods:**

   | MelonMod | IModpackPlugin |
   |----------|----------------|
   | `OnInitializeMelon()` | `OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)` |
   | `OnSceneWasLoaded(int, string)` | `OnSceneLoaded(int, string)` |
   | `OnUpdate()` (override) | `OnUpdate()` (interface method) |
   | `OnGUI()` (override) | `OnGUI()` (interface method) |

4. **Replace `LoggerInstance`** -- store the logger from `OnInitialize` in a field (e.g. `_log`) and use that instead.
5. **Replace `HarmonyInstance`** -- store the harmony from `OnInitialize` in a field. Don't create your own `Harmony` instance.
6. **Add `using Menace.ModpackLoader;`** to get the `IModpackPlugin` interface.
7. **Add `Menace.ModpackLoader` reference** to your csproj or `modpack.json` references.

Things that stay the same:
- `MelonCoroutines.Start()` is static, works without a `MelonMod` base class.
- All reflection, IL2CPP memory access, and IMGUI drawing code is unchanged.
- Harmony patch methods (static `Prefix`/`Postfix`) work identically.

## Dependency graph

```
Menace.ModpackLoader.dll  (MelonMod, loaded by MelonLoader)
    provides: IModpackPlugin, DllLoader
    depends on: MelonLoader, 0Harmony, UnityEngine.CoreModule

YourPlugin.dll  (compiled from modpack sources)
    implements: IModpackPlugin
    depends on: Menace.ModpackLoader (for interface), plus whatever game/Unity refs you need
```

Your plugin DLL references `Menace.ModpackLoader` with `<Private>false</Private>` -- it's already loaded at runtime, so your DLL just needs the compile-time type info. No circular dependencies.

## Worked examples

### BalanceMod -- Harmony patches, no GUI

**`examples/BalanceMod-modpack/modpack.json`:**
```json
{
  "manifestVersion": 2,
  "name": "BalanceMod",
  "version": "1.0.0",
  "author": "Menace Modkit",
  "description": "Balance tweaks: always-crawl for pinned units, capped suppression stun duration.",
  "loadOrder": 50,
  "patches": {},
  "assets": {},
  "code": {
    "sources": ["src/BalanceModPlugin.cs"],
    "references": [
      "MelonLoader", "0Harmony", "Il2CppInterop.Runtime",
      "Il2Cppmscorlib", "Assembly-CSharp", "Menace.ModpackLoader"
    ],
    "prebuiltDlls": []
  },
  "securityStatus": "SourceVerified"
}
```

Key patterns in `BalanceModPlugin.cs`:
- Stores `_harmony` from `OnInitialize`, applies patches in `OnSceneLoaded` when scene is `"Tactical"`.
- Uses `MelonCoroutines.Start()` to delay patch application until types are initialized.
- Harmony postfix/prefix methods are `public static` on the plugin class itself.
- Does not override `OnUpdate` or `OnGUI` (uses interface defaults).

### DevMode -- IMGUI overlay, input handling, coroutines

**`examples/DevMode-modpack/modpack.json`:**
```json
{
  "manifestVersion": 2,
  "name": "DevMode",
  "version": "1.0.0",
  "author": "Menace Modkit",
  "description": "In-game developer tools: cheats, god mode, entity spawning, delete, IMGUI overlay.",
  "loadOrder": 10,
  "code": {
    "sources": ["src/DevModePlugin.cs"],
    "references": [
      "MelonLoader", "0Harmony", "Il2CppInterop.Runtime", "Il2CppInterop.Common",
      "Il2Cppmscorlib", "Assembly-CSharp", "UnityEngine.CoreModule",
      "UnityEngine.InputLegacyModule", "UnityEngine.IMGUIModule",
      "UnityEngine.TextRenderingModule", "Menace.ModpackLoader"
    ],
    "prebuiltDlls": []
  },
  "securityStatus": "SourceVerified"
}
```

Key patterns in `DevModePlugin.cs`:
- Overrides both `OnUpdate` (keyboard input) and `OnGUI` (IMGUI overlay).
- Extra Unity module references: `InputLegacyModule`, `IMGUIModule`, `TextRenderingModule`.
- Heavy reflection against `Assembly-CSharp` to resolve game types by name at runtime.
- Uses `MelonCoroutines.Start()` for async setup with retries.
- IL2CPP memory access via `Marshal.ReadInt32`/`WriteInt32` for DevSettings manipulation.

## Security scanning

Source modpacks are scanned by `SecurityScanner` at deploy time. It flags patterns like network access, process spawning, P/Invoke, and assembly loading. Results are advisory -- `securityStatus` in the manifest records the outcome:

| Status | Meaning |
|--------|---------|
| `SourceVerified` | Source scanned, no warnings. |
| `SourceWithWarnings` | Source scanned, advisory warnings present. |
| `Unreviewed` | Prebuilt DLL, source not available for scanning. |

DllLoader logs the trust label when loading each DLL.
