# Menace.Modkit.Core - Maintainer Documentation

## Overview

**Menace.Modkit.Core** is the foundational library for the Menace modkit system, providing core infrastructure for game asset extraction, typetree caching, and runtime asset bundling. It serves as the bridge between raw game assets and modifiable formats.

**Location:** `src/Menace.Modkit.Core/`

## Architecture

### Technology Stack
- **.NET Target:** .NET 9.0 and .NET 10.0 (multi-target)
- **Key Dependencies:**
  - `AssetsTools.NET` (v3.0.3) - Binary asset manipulation
  - `SharpGLTF.Core` (v1.0.6) - GLB/GLTF model handling
  - `SixLabors.ImageSharp` (v3.1.6) - Image processing
  - `Microsoft.Extensions.DependencyInjection.Abstractions` (v8.0.0) - IoC container
  - `AssetRipper.IO.Files` (third-party project) - Asset file parsing

### Directory Structure

```
src/Menace.Modkit.Core/
├── Bundles/                          # Asset compilation & bundling
│   ├── BundleCompiler.cs            # Main compilation orchestrator
│   ├── BundleWriter.cs              # UnityFS format writer
│   ├── TemplatePatchSerializer.cs   # JSON->binary patch conversion
│   ├── MergedPatchSet.cs            # Patch aggregation model
│   ├── MergedCloneSet.cs            # Clone aggregation model
│   ├── AudioBundler.cs              # Audio asset collection
│   ├── AudioAssetCreator.cs         # Native AudioClip creation
│   ├── NativeTextureCreator.cs      # Texture2D/Sprite creation
│   ├── NativeSpriteCreator.cs       # Sprite-specific creation
│   ├── NativePrefabCreator.cs       # Prefab creation
│   ├── MeshAssetCreator.cs          # Mesh creation
│   ├── SpriteAssetCreator.cs        # Sprite utilities
│   ├── TextureAssetCreator.cs       # Texture utilities
│   ├── TextureBundler.cs            # Texture collection
│   └── GlbBundler.cs                # GLB/GLTF→Unity conversion
├── Typetrees/                        # Type metadata management
│   ├── ITypetreeCacheBuilder.cs     # Cache builder interface
│   ├── TypetreeCacheService.cs      # Main cache implementation
│   ├── TypetreeCacheRequest.cs      # Cache request parameters
│   ├── TypetreeCacheResult.cs       # Cache operation result
│   ├── TypetreeCacheManifest.cs     # Cache metadata
│   ├── TypetreeCacheFile.cs         # Cache file entry
│   ├── TypetreeCollection.cs        # Type collection model
│   ├── TypetreeDefinition.cs        # Single type definition
│   └── TypetreeNodeModel.cs         # Type tree node structure
├── Models/                           # Data models
│   ├── ExtractionManifest.cs        # Extraction state tracking
│   ├── ExtractionSettings.cs        # User configuration
│   └── AssetRipperProfile.cs        # Asset ripper profiles
├── Services/                         # Service layer
│   └── ExtractionOrchestrator.cs    # Extraction pipeline orchestration
├── DependencyInjection/              # IoC setup
│   └── ServiceCollectionExtensions.cs
├── UnityVersionDetector.cs           # Version detection utility
└── Menace.Modkit.Core.csproj
```

## Public API Surface

### Core Interfaces

#### `ITypetreeCacheBuilder`
**Location:** `Typetrees/ITypetreeCacheBuilder.cs:1-9`

```csharp
Task<TypetreeCacheResult> BuildAsync(
    TypetreeCacheRequest request,
    CancellationToken cancellationToken = default)
```

Purpose: Async interface for building typetree caches from game installations.

#### `IUnityVersionDetector`
**Location:** `UnityVersionDetector.cs:12-20`

```csharp
string? DetectVersion(string gamePath)
```

Purpose: Detects Unity version from game installation.

### Core Classes

#### `TypetreeCacheService`
**Location:** `Typetrees/TypetreeCacheService.cs:17`

Main typetree cache builder implementation:
- Scans game installation for asset files
- Extracts type metadata from SerializedFile objects
- Generates JSON representations of typetrees
- Creates manifest with fingerprinting for change detection

#### `BundleCompiler`
**Location:** `Bundles/BundleCompiler.cs:18`

Asset compilation orchestrator (1500+ lines):
- **Phase 1:** Asset Lookup - builds lookup dictionary of MonoBehaviour assets
- **Phase 2:** Clones - processes MergedCloneSet via raw byte cloning
- **Phase 3:** Patches - tracks patches (deferred to runtime)
- **Phase 4:** Audio - creates native AudioClip assets
- **Phase 5:** Textures/Sprites - PNG→Texture2D conversion
- **Phase 6:** Models/Prefabs - GLB/GLTF→Unity conversion
- **Phase 7:** Write - ResourceManager patching and UnityFS bundle writing

#### `ExtractionOrchestrator`
**Location:** `Services/ExtractionOrchestrator.cs:14`

Smart caching and extraction planning:
- Computes SHA256 hashes of game binaries
- Determines what extraction operations are needed
- Caches extraction state in `UserData/ExtractionCache/manifest.json`

### Data Models

#### `MergedPatchSet`
**Location:** `Bundles/MergedPatchSet.cs:12`

Structure: `templateType → instanceName → field → value`
- Last-wins merge strategy
- Applied to existing assets at runtime via injection system

#### `MergedCloneSet`
**Location:** `Bundles/MergedCloneSet.cs:10`

Structure: `templateType → { newName → sourceName }`
- Source assets are cloned (raw bytes) with new m_ID

### Dependency Injection

**Location:** `DependencyInjection/ServiceCollectionExtensions.cs:11`

```csharp
services.AddMenaceModkitCore()
// Registers:
// - ITypetreeCacheBuilder → TypetreeCacheService (Singleton)
// - IUnityVersionDetector → UnityVersionDetector (Singleton)
```

## Data Flow

### Extraction Pipeline
```
Game Installation
    ↓
UnityVersionDetector.DetectVersion()
    ↓
ExtractionOrchestrator.PlanExtractionAsync()
    ↓
ExtractionPlan { NeedsIL2CppDump, NeedsTemplateCodeGen, ... }
    ↓
External processes (IL2CPP dump, template code gen, etc.)
    ↓
ExtractionOrchestrator.UpdateManifest()
```

### Bundle Compilation Pipeline
```
Modpack Files + Game Assets
    ↓
BundleCompiler.CompileDataPatchBundleAsync()
    ├─ Load resources.assets
    ├─ Parse ResourceManager + globalgamemanagers
    ├─ Process clones, audio, textures, models
    ├─ Patch ResourceManager
    └─ Write UnityFS bundle
        ↓
BundleCompileResult
```

## Dependencies

### Dependent Projects (consume Core)
| Project | Purpose |
|---------|---------|
| Menace.Modkit.Cli | CLI interface |
| Menace.Modkit.App | WPF GUI application |
| Menace.Modkit.Mcp | MCP server |

## TODOs and Known Issues

### TODOs

1. **GlbBundler.cs:580** - Texture2D Asset Creation from GLB Materials
   ```csharp
   // TODO: Create Texture2D assets from GLB materials
   ```

2. **GlbBundler.cs:712** - Proper Asset Creation via AssetsTools.NET
   ```csharp
   // TODO: Use AssetsTools.NET to create proper Unity assets
   ```

### Architectural Notes

1. **Patch Application Limitation** (BundleCompiler.cs:282-311)
   - Raw byte cloning doesn't support field-level patching
   - Patches are tracked but actual field modifications deferred to runtime

2. **Unity 6 Type Tree Absence** (BundleCompiler.cs:216-218)
   - Unity 6 doesn't embed type trees
   - Uses raw byte scanning with pattern matching for m_ID discovery

3. **Memory Usage** (BundleCompiler.cs:198-209)
   - No streaming for resource.assets (entire file loaded to memory)
   - Large game assets could cause memory pressure

## Configuration

### Hard-Coded Constants

| Component | Constant | Value | Purpose |
|-----------|----------|-------|---------|
| ExtractionOrchestrator | Cache directory | `{gameInstallPath}/UserData/ExtractionCache` | Smart cache location |
| TypetreeCacheService | Tool version | `"0.1.0-dev"` | Manifest metadata |
| BundleWriter | CAB name | `"CAB-modpack"` | UnityFS internal name |
| BundleCompiler | ResourceManager Type ID | `147` | Type ID lookup |

## Usage Example

```csharp
// 1. Register services
var services = new ServiceCollection();
services.AddMenaceModkitCore();
var sp = services.BuildServiceProvider();

// 2. Detect game version
var detector = sp.GetRequiredService<IUnityVersionDetector>();
var unityVersion = detector.DetectVersion(gamePath);

// 3. Plan extraction
var orchestrator = new ExtractionOrchestrator(gamePath);
var plan = await orchestrator.PlanExtractionAsync();

// 4. Compile modpack bundles
var compiler = new BundleCompiler();
var result = await compiler.CompileDataPatchBundleAsync(
    mergedPatches, mergedClones, audioEntries, textureEntries, modelEntries,
    gameDataPath, unityVersion, outputBundlePath);
```
