# ModpackLoader Implementation

Architecture and internals of the Menace modpack loading system.

## Overview

ModpackLoader is a MelonLoader mod (`Menace.ModpackLoader.dll`) that discovers modpacks at game startup, applies data patches, loads asset bundles, compiles/loads plugin DLLs, and forwards Unity lifecycle events to plugins.

## Architecture

```
┌──────────────────────────────────┐
│        Menace Modkit App         │  Avalonia UI -- author modpacks
│   CompilationService (Roslyn)    │  Compile src/ -> build/*.dll
│   SecurityScanner                │  Scan sources for dangerous patterns
│   DeployManager                  │  Copy modpack + DLL to Mods/
└──────────────┬───────────────────┘
               │  deploys to
               ▼
┌──────────────────────────────────┐
│     Mods/<ModpackName>/          │
│     ├── modpack.json             │  V2 manifest
│     ├── dlls/                    │  Compiled plugin DLLs
│     ├── bundles/                 │  AssetBundles (.bundle)
│     └── Assets/                  │  Loose asset files
└──────────────┬───────────────────┘
               │  loaded by
               ▼
┌──────────────────────────────────┐
│     ModpackLoaderMod (MelonMod)  │  Entry point, runs in game
│     ├── BundleLoader             │  AssetBundle.LoadFromFile
│     ├── DllLoader                │  Assembly.LoadFrom, IModpackPlugin discovery
│     └── TemplateInjection        │  Reflection-based field injection
└──────────────┬───────────────────┘
               │  injects into
               ▼
┌──────────────────────────────────┐
│     Menace Game (Unity IL2CPP)   │
│     Assembly-CSharp              │
│     ScriptableObject templates   │
└──────────────────────────────────┘
```

## V2 manifest format

```json
{
  "manifestVersion": 2,
  "name": "MyModpack",
  "version": "1.0.0",
  "author": "Author",
  "description": "What this modpack does.",
  "loadOrder": 100,
  "patches": {
    "WeaponTemplate": {
      "Pistol_Basic": { "Damage": 50, "Accuracy": 80 }
    }
  },
  "assets": {
    "Assets/Textures/icon.png": "Assets/Textures/icon.png"
  },
  "bundles": ["bundles/custom.bundle"],
  "code": {
    "sources": ["src/MyPlugin.cs"],
    "references": ["MelonLoader", "Assembly-CSharp", "Menace.ModpackLoader"],
    "prebuiltDlls": []
  },
  "securityStatus": "SourceVerified"
}
```

All sections are optional. A data-only modpack has `patches` and/or `assets` with no `code`. A code-only modpack has `code` with empty `patches`/`assets`.

V1 manifests (using `templates` instead of `patches`, no `manifestVersion` or `code`) are still supported for backward compatibility.

### Manifest sections

| Section | Purpose |
|---------|---------|
| `patches` | Template field overrides: `TypeName -> InstanceName -> FieldName -> Value`. Applied via reflection + `Marshal.Write*`. |
| `assets` | Asset path -> local file path mappings for texture/sprite replacement. |
| `bundles` | List of `.bundle` files to load via `AssetBundle.LoadFromFile`. |
| `code` | Source files, assembly references, and prebuilt DLLs for plugin compilation. |
| `securityStatus` | Trust label from SecurityScanner (`SourceVerified`, `SourceWithWarnings`, `Unreviewed`). |

## Loading pipeline

### 1. ModpackLoaderMod.OnInitializeMelon

1. Scan `Mods/*/modpack.json`, parse each, sort by `loadOrder`.
2. For each modpack (in order):
   - Register asset replacements from `assets`.
   - **BundleLoader.LoadBundles** -- load `.bundle` files from modpack directory.
   - **DllLoader.LoadModDlls** -- load DLLs from `dlls/`, discover `IModpackPlugin` implementations.
3. **DllLoader.InitializeAllPlugins** -- call `OnInitialize` on every discovered plugin.

### 2. ModpackLoaderMod.OnSceneWasLoaded

1. Forward to **DllLoader.NotifySceneLoaded** (all plugins).
2. On first `MainMenu` scene: start coroutine to wait for templates, then **ApplyAllModpacks** (template injection).

### 3. ModpackLoaderMod.OnUpdate / OnGUI

Forward to **DllLoader.NotifyUpdate** and **DllLoader.NotifyOnGUI** respectively.

## DllLoader -- plugin lifecycle

```
Assembly.LoadFrom(dll)
    → GetTypes() → filter IModpackPlugin implementations
    → Activator.CreateInstance() for each
    → Store as PluginInstance { Plugin, ModpackName, TypeName, Harmony }

InitializeAllPlugins()
    → plugin.OnInitialize(logger, harmony) for each

NotifySceneLoaded(buildIndex, sceneName)
    → plugin.OnSceneLoaded(...) for each

NotifyUpdate()
    → plugin.OnUpdate() for each

NotifyOnGUI()
    → plugin.OnGUI() for each
```

Each plugin gets its own `MelonLogger.Instance` (named after the modpack) and `HarmonyLib.Harmony` instance (ID: `com.menace.modpack.<assembly>.<type>`). All forwarding calls are individually try/caught.

## BundleLoader

Scans modpack directories for `*.bundle` files, calls `AssetBundle.LoadFromFile`, and logs loaded assets. Provides `UnloadAll()` for cleanup.

## CompilationService (Modkit App)

Runs at deploy time in the Modkit UI (not at game runtime):

1. Reads `code.sources` from the manifest, resolves absolute paths.
2. Runs `SecurityScanner.ScanSources` on all source files.
3. Parses sources with Roslyn (`CSharpSyntaxTree.ParseText`, C# 10).
4. Resolves references via `ReferenceResolver` (searches MelonLoader and Il2CppAssemblies directories by assembly name).
5. Compiles with `CSharpCompilation.Create` (Release, AnyCpu, DLL output).
6. Emits to `build/<SanitizedName>.dll` inside the modpack directory.
7. Returns `CompilationResult` with success/failure, diagnostics, and security warnings.

## SecurityScanner

Pattern-based static analysis of source files. Flags:

- **Danger:** Network access (`HttpClient`, `WebRequest`, `Socket`), process spawning (`Process.Start`), registry access, base64+assembly load combos.
- **Warning:** System folder access, dynamic assembly loading, P/Invoke, extern methods.

Results are advisory. The `securityStatus` field in the manifest records the scan outcome, and DllLoader logs the trust label when loading each DLL.

## Template injection

`TemplateInjection.cs` (partial class of `ModpackLoaderMod`) contains the `ApplyTemplateModifications` method with field handlers for game template types. The injection flow:

1. Find template type in `Assembly-CSharp` by name.
2. Use `Il2CppType.From()` + `Resources.FindObjectsOfTypeAll()` to locate instances.
3. Match instance names against the patch dictionary.
4. For each matched field, use `Marshal.Write*` at the IL2CPP object pointer + field offset.

V2 `patches` and V1 `templates` both follow this `TypeName -> InstanceName -> FieldName -> Value` structure and share the same application logic.

## Source files

| File | Description |
|------|-------------|
| `ModpackLoaderMod.cs` | MelonMod entry point, modpack loading, template application |
| `TemplateInjection.cs` | Partial class with field injection handlers |
| `AssetInjectionPatches.cs` | Harmony patches for Unity asset interception |
| `DllLoader.cs` | DLL loading, plugin discovery, lifecycle forwarding |
| `BundleLoader.cs` | AssetBundle loading |
| `IModpackPlugin.cs` | Plugin interface definition |
| `Menace.ModpackLoader.csproj` | Project configuration |

## Build

```bash
dotnet build src/Menace.ModpackLoader -c Release
```

Output: `src/Menace.ModpackLoader/bin/Release/net6.0/Menace.ModpackLoader.dll`

Deploy to game: copy DLL to `<GameInstall>/Mods/Menace.ModpackLoader.dll`.
