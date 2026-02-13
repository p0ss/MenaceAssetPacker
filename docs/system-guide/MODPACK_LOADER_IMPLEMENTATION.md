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
│   BundleCompiler                 │  Merge patches -> UnityFS bundle
└──────────────┬───────────────────┘
               │  deploys to
               ▼
┌──────────────────────────────────┐
│     Mods/<ModpackName>/          │
│     ├── modpack.json             │  V2 manifest
│     ├── dlls/                    │  Compiled plugin DLLs
│     ├── *.bundle                 │  AssetBundles (models, audio, etc.)
│     └── assets/                  │  Loose asset files (images)
└──────────────┬───────────────────┘
               │  loaded by
               ▼
┌──────────────────────────────────┐
│     ModpackLoaderMod (MelonMod)  │  Entry point, runs in game
│     ├── BundleLoader             │  AssetBundle loading + asset registry
│     ├── AssetReplacer            │  Multi-type asset replacement
│     ├── DllLoader                │  Assembly.LoadFrom, IModpackPlugin discovery
│     └── TemplateInjection        │  Reflection-based field injection
└──────────────┬───────────────────┘
               │  injects into
               ▼
┌──────────────────────────────────┐
│     Menace Game (Unity IL2CPP)   │
│     Assembly-CSharp              │
│     ScriptableObject templates   │
│     Textures, Meshes, Audio, etc │
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
    "Assets/Texture2D/arc_assault_rifle_t1_BaseMap.png": "assets/custom_rifle.png"
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
| `assets` | Asset path -> local file path mappings. Used for disk-file texture replacement (PNG/JPG/TGA/BMP). |
| `bundles` | List of `.bundle` files to load via `AssetBundle.LoadFromFile`. Primary path for all non-texture assets (models, audio, materials, prefabs). |
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
3. On every scene load: start coroutine to apply asset replacements after a short delay (**AssetReplacer.ApplyAllReplacements**). This runs whenever disk-file replacements or bundle assets are registered.

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

## BundleLoader -- asset registry

Scans modpack directories for `*.bundle` files, calls `AssetBundle.LoadFromFile`, and registers every loaded asset in a queryable registry. This is the primary path for non-texture content: 3D models (GLB/FBX), audio, materials, prefabs, and any other Unity asset type.

### Asset registration

When a bundle is loaded, `LoadAllAssets()` is called and each asset is stored in two indices:

- **Name index** (`_assetsByName`): asset name -> list of objects (multiple types may share a name).
- **Type+name index** (`_assetsByTypeAndName`): `"TypeName:assetName"` -> single object. Last-loaded wins for the same key, so modpack load order determines which replacement takes effect.

### Query API

```csharp
// By type and name
Texture2D tex = BundleLoader.GetAsset<Texture2D>("MyTexture");
AudioClip clip = BundleLoader.GetAsset<AudioClip>("BattleTheme");
GameObject prefab = BundleLoader.GetAsset<GameObject>("PirateCaptain");

// By name only (first match, any type)
UnityEngine.Object asset = BundleLoader.GetAsset("SomeAsset");

// All assets of a given IL2CPP type name
List<UnityEngine.Object> meshes = BundleLoader.GetAssetsByType("Mesh");

// Existence check
bool exists = BundleLoader.HasAsset("CustomModel");

// Stats
int bundles = BundleLoader.LoadedBundleCount;
int assets = BundleLoader.LoadedAssetCount;
```

Provides `UnloadAll()` for cleanup (unloads all bundles, clears registry).

## AssetReplacer -- multi-type asset replacement

Handles replacement of existing game assets after each scene load. Supports two sources:

### 1. Disk-file replacements

Registered from the modpack `assets` map. Asset kind is inferred from file extension:

| Extension | Kind | Runtime strategy |
|-----------|------|-----------------|
| `.png`, `.jpg`, `.jpeg`, `.tga`, `.bmp` | Texture | `ImageConversion.LoadImage` overwrites existing `Texture2D` in-place |
| `.wav`, `.ogg`, `.mp3` | Audio | Registered; requires bundle for actual replacement |
| `.glb`, `.gltf`, `.fbx`, `.obj` | Model | Registered; requires bundle for actual replacement |
| `.mat` | Material | Registered; requires bundle for actual replacement |

Only image textures can be loaded from raw disk files at runtime. All other types require Unity's serialization and must come from AssetBundles.

### 2. Bundle-sourced replacements

For each asset type present in `BundleLoader`'s registry, `AssetReplacer` searches for matching game objects (same name, same type) and applies type-specific in-place overwrite:

| Type | Strategy |
|------|----------|
| `Texture2D` | `Graphics.CopyTexture(bundleTex, gameTex)` -- all materials/UI update automatically |
| `AudioClip` | `GetData`/`SetData` -- copy samples from bundle clip to game clip |
| `Mesh` | Clear + copy vertices, normals, tangents, UVs, triangles, bone weights, submeshes; recalculate bounds |
| `Material` | Find all `Renderer` components using a material with the same name, swap `sharedMaterials` to the bundle version |
| `GameObject` (prefab) | Recursive hierarchy copy matching children by name; swaps `MeshFilter.sharedMesh`, `Renderer.sharedMaterials`, `SkinnedMeshRenderer` mesh/materials on each matched child |

Bundle assets that don't match any existing game object are treated as **new content** -- they remain in memory via `BundleLoader` and can be queried by plugin code.

### IL2CPP limitations

`Resources.Load<T>()` is generic in IL2CPP and cannot be Harmony-patched. Instead, `AssetReplacer` uses `Resources.FindObjectsOfTypeAll(il2cppType)` to locate all loaded objects of each type, then matches by name. This runs after a short delay (10 frames) on each scene load to ensure assets are initialized.

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
| `ModpackLoaderMod.cs` | MelonMod entry point, modpack loading, template application, scene lifecycle |
| `TemplateInjection.cs` | Partial class with field injection handlers |
| `AssetInjectionPatches.cs` | `AssetReplacer` -- multi-type asset replacement (textures, audio, meshes, materials, prefabs) |
| `DllLoader.cs` | DLL loading, plugin discovery, lifecycle forwarding |
| `BundleLoader.cs` | AssetBundle loading and queryable asset registry |
| `IModpackPlugin.cs` | Plugin interface definition |
| `Menace.ModpackLoader.csproj` | Project configuration |

## Build

```bash
dotnet build src/Menace.ModpackLoader -c Release
```

Output: `src/Menace.ModpackLoader/bin/Release/net6.0/Menace.ModpackLoader.dll`

Deploy to game: copy DLL to `<GameInstall>/Mods/Menace.ModpackLoader.dll`.
