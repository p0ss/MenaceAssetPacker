# Extraction Orchestration System

## Overview

The extraction orchestration system intelligently manages the IL2CPP dump generation, template extraction code generation, data extraction, and asset ripping pipeline. It uses hash-based caching to avoid redundant operations and only reprocesses when the game binary changes.

## Components

### 1. ExtractionManifest (`Menace.Modkit.Core/Models/ExtractionManifest.cs`)

Tracks the state of all extraction operations:

```json
{
  "UnityVersion": "6000.0.56f1",
  "GameAssemblyHash": "a1b2c3d4...",
  "MetadataHash": "e5f6g7h8...",
  "IL2CppDumpTimestamp": "2025-10-10T16:30:00Z",
  "TemplateCodeGenerationTimestamp": "2025-10-10T16:31:00Z",
  "DataExtractionTimestamp": "2025-10-10T16:32:00Z",
  "AssetRipTimestamp": "2025-10-10T16:35:00Z",
  "ExtractedTemplates": ["WeaponTemplate", "EntityTemplate", ...],
  "MinimalDumpPath": "UserData/ExtractionCache/il2cpp_templates.dump",
  "FullDumpPath": "UserData/ExtractionCache/il2cpp_full.dump",
  "DataExtractorHash": "f9g0h1i2...",
  "GameExecutablePath": "/path/to/Menace.exe"
}
```

**Stored at:** `{GameDir}/UserData/ExtractionCache/manifest.json`

### 2. ExtractionOrchestrator (`Menace.Modkit.Core/Services/ExtractionOrchestrator.cs`)

Main orchestration logic that:
- Computes SHA256 hashes of game binaries
- Determines what operations are needed
- Updates manifest after successful operations
- Provides cache invalidation logic

### 3. Python Tools

#### `extract_template_dump.py`
Extracts minimal IL2CPP dump containing only Template classes.

**Before:**
```
il2cpp_dump/dump.cs: 35 MB (874,630 lines)
```

**After:**
```
il2cpp_templates.dump: ~500 KB (~60 template classes)
```

**Usage:**
```bash
python3 extract_template_dump.py il2cpp_dump/dump.cs il2cpp_templates.dump
```

#### `generate_all_templates.py`
Generates complete template extraction code from minimal dump.

**Output:** `generated_extraction_code.cs` with the entire `ExtractTemplateDataDirect()` method

**Usage:**
```bash
python3 generate_all_templates.py
```

## Extraction Pipeline

### Trigger Conditions

| Operation | Triggers When |
|-----------|---------------|
| **IL2CPP Dump** | `GameAssembly.so/dll` hash changes OR `global-metadata.dat` hash changes |
| **Template Code Gen** | IL2CPP dump changes OR never generated |
| **Data Extraction** | Game binary changes OR DataExtractor.dll changes |
| **Asset Rip** | Game binary changes OR never ripped |

### Pipeline Flow

```
1. Check Manifest
   ↓
2. Compute Hashes (GameAssembly, Metadata, DataExtractor)
   ↓
3. Determine Operations Needed
   ↓
4. Execute Pipeline:

   [Game Updated?]
   ├── Yes → Run Il2CppDumper
   │         ↓
   │         Generate Full Dump (35MB)
   │         ↓
   │         Extract Minimal Dump (500KB)
   │         ↓
   │         Generate Template Code
   │         ↓
   │         Rebuild DataExtractor
   │         ↓
   │         Deploy & Run Extraction
   │         ↓
   │         Update Manifest
   │
   └── No → Check DataExtractor.dll
           ├── Changed → Rebuild & Extract
           └── Unchanged → Skip (use cached data)
```

## Hash-Based Caching

### What Gets Hashed

1. **GameAssembly.so/dll** - Main IL2CPP binary
2. **global-metadata.dat** - IL2CPP metadata
3. **Menace.DataExtractor.dll** - Extractor mod

### Cache Invalidation Rules

```csharp
// IL2CPP dump needed if:
GameAssemblyHash != CurrentHash || MetadataHash != CurrentHash

// Template code needed if:
IL2CppDumpChanged || TemplateCodeTimestamp == null

// Data extraction needed if:
GameBinaryChanged || DataExtractorChanged

// Asset rip needed if:
GameBinaryChanged || Never ripped before
```

## Bundling for Distribution

### Required Bundled Tools

1. **Il2CppDumper** (Binary + Dependencies)
   - Location: `third_party/bundled/Il2CppDumper/`
   - Platforms: Windows (exe), Linux (binary)
   - Size: ~15 MB

2. **Python Scripts**
   - `extract_template_dump.py`
   - `generate_all_templates.py`
   - `generate_offset_code.py`
   - Location: `third_party/bundled/scripts/`

3. **OffsetDumper Mod** (Alternative to Il2CppDumper)
   - Can dump offsets at runtime using MelonLoader
   - Lighter weight but less complete
   - Already in: `src/Menace.DataExtractor/OffsetDumper.cs`

### Distribution Package Structure

```
Menace.Modkit.App/
├── Menace.Modkit.App.exe
├── third_party/
│   └── bundled/
│       ├── Il2CppDumper/
│       │   ├── Il2CppDumper.exe (Windows)
│       │   ├── Il2CppDumper (Linux)
│       │   └── ... (dependencies)
│       ├── scripts/
│       │   ├── extract_template_dump.py
│       │   ├── generate_all_templates.py
│       │   └── generate_offset_code.py
│       ├── MelonLoader/
│       ├── DataExtractor/
│       └── AssetRipper/
└── README.md
```

## Integration with UI

### Settings Tab

#### Extraction Settings

```
Performance & Caching:
  [✓] Auto-update on game version change
      Automatically regenerate extraction code when game updates

  [✓] Enable caching
      Cache extracted data and assets (huge speed improvement)

  [ ] Keep full IL2CPP dump
      Store complete dump (35MB) for reference, not just templates (500KB)

  [✓] Show extraction progress
      Display progress notifications during extraction

Asset Ripper Profile:
  ( ) Essential   - Sprites, Textures, Audio, Text only (fastest, ~30s)
  (•) Standard    - Essential + Meshes, Shaders, VFX, Prefabs (recommended, ~1-2min)
  ( ) Complete    - Everything including Unity internals (slowest, ~5-10min)
  ( ) Custom      - User-defined filter settings

  [Configure Custom Filter...]

Cache Management:
  Last extraction: 2 hours ago
  Cache size: 250 MB

  [View Cache Details]
  [Clear Cache]
  [Force Re-extract All]
```

#### Custom Asset Filter Settings

```
Assets to Include:
  [✓] Sprites & Textures (Sprite, Texture2D, Texture3D)
  [✓] Audio (AudioClip)
  [✓] Meshes & Models (Mesh, PrefabHierarchyObject)
  [✓] Visual Effects (Shader, VisualEffectAsset)
  [✓] Fonts & UI (Font, TextAsset)
  [✓] Terrain (TerrainData, Cubemap)
  [ ] Assemblies (DLLs)
  [ ] Unity Config
  [ ] Unity Scripts (non-Menace)

Scripts to Include:
  [✓] Menace namespace only
  [ ] All game scripts
  [ ] Unity engine scripts

[Save Custom Profile]  [Reset to Standard]
```

### Workflow

1. User launches modkit
2. Modkit checks manifest
3. If game updated:
   ```
   🔄 Game update detected (Unity 6000.0.56f1 → 6000.0.57f1)

   Running extraction pipeline:
   ├── ✓ Generating IL2CPP dump... (15s)
   ├── ✓ Extracting template dump... (2s)
   ├── ✓ Generating extraction code... (3s)
   ├── ✓ Building DataExtractor... (5s)
   ├── ✓ Extracting game data... (10s)
   └── ✓ Complete! Cached for future use.
   ```

4. Next launch (no changes):
   ```
   ✓ Using cached extraction data (last updated: 2 hours ago)
   ```

## Performance Improvements

### Before (No Caching, No Filtering)
- Every launch:
  - Generate full dump (35MB) → **~15s**
  - Parse all templates → **~5s**
  - Extract all data → **~10s**
  - Rip ALL assets (Unity config, DLLs, everything) → **~5-10 minutes**
  - **Total: ~6-11 minutes every launch** ❌

### After (With Caching + Smart Filtering)

#### First Launch
- Generate IL2CPP dump → **~15s**
- Extract minimal template dump (500KB) → **~2s**
- Generate extraction code → **~3s**
- Build DataExtractor → **~5s**
- Extract game data → **~10s**
- Rip assets (Standard profile) → **~1-2min**
- **Total: ~2-3 minutes first time** ✅

#### Subsequent Launches (No Changes)
- Load cached data → **~1 second** ⚡
- Load cached assets → **~1 second** ⚡
- **Total: ~2 seconds** 🚀

#### Game Update
- Detect hash change → **~1s**
- Full pipeline → **~2-3 minutes**
- Cache for next time → **✓**

### Asset Ripper Profile Comparison

| Profile | Assets Included | Extraction Time | Cache Size | Use Case |
|---------|----------------|-----------------|------------|----------|
| **Essential** | Sprites, Textures, Audio, Text | ~30 seconds | ~100 MB | Quick modding, texture replacement |
| **Standard** (recommended) | Essential + Meshes, Shaders, VFX, Prefabs, Menace scripts | ~1-2 minutes | ~250 MB | Full modding capabilities |
| **Complete** | Everything including Unity internals, all DLLs, config | ~5-10 minutes | ~1-2 GB | Advanced reverse engineering |

### What Gets Excluded (Standard Profile)

**Excluded from /Assets/:**
- AnimatorController, AnimatorOverrideController
- AssetBundle
- BuildSettings, GraphicsSettings, etc.
- ComputeShader, MonoScript (Unity internals)
- PhysicsMaterial, LightingSettings
- NavMeshData, OcclusionCullingData
- All Unity configuration files

**Excluded from /Scripts/:**
- Unity engine namespaces (UnityEngine.*, Unity.*)
- Third-party libraries (unless explicitly included)
- Only keep: `Menace.*` namespace

**Excluded Entirely:**
- `/Assemblies/` folder (DLLs) - unless "Include Assemblies" checked
- Decompiled Unity engine code
- Build configuration files

### Minimal Dump Benefits
- **Faster parsing:** 500KB vs 35MB (70x smaller)
- **Faster bundling:** Include templates-only dump in distribution
- **Less disk space:** Cache takes 500KB instead of 35MB
- **Faster regeneration:** Only parse what's needed for template extraction

## Future Enhancements

1. **Incremental Updates**
   - Only regenerate changed templates
   - Diff old vs new dump to identify changes

2. **Cloud Cache**
   - Share extraction cache across users
   - Download pre-generated code for known game versions

3. **Runtime Offset Discovery**
   - Use OffsetDumper at runtime (no Il2CppDumper needed)
   - More portable but less complete

4. **Version Database**
   - Maintain database of Unity version → offset mappings
   - Predict offsets for new Unity versions

## Troubleshooting

### Cache Corruption
```bash
# Delete cache and force regeneration
rm -rf "$GAME_DIR/UserData/ExtractionCache"
```

### Manual Dump Generation
```bash
# 1. Generate full dump with Il2CppDumper
cd Il2CppDumper
./Il2CppDumper GameAssembly.so global-metadata.dat output/

# 2. Extract minimal dump
python3 extract_template_dump.py output/dump.cs il2cpp_templates.dump

# 3. Generate code
python3 generate_all_templates.py il2cpp_templates.dump

# 4. Copy generated_extraction_code.cs into DataExtractorMod.cs
```

### Hash Mismatch Issues
If manifest says "up to date" but data seems wrong:
1. Check manifest hashes match current files
2. Delete manifest.json to force regeneration
3. Verify Il2CppDumper output is for correct Unity version

## API Example

```csharp
// In your app startup
var orchestrator = new ExtractionOrchestrator(gameInstallPath);

// Plan what's needed
var plan = await orchestrator.PlanExtractionAsync();

if (plan.NeedsAnyOperation)
{
    Console.WriteLine("Extraction needed:");
    if (plan.NeedsIL2CppDump) Console.WriteLine("  - IL2CPP dump");
    if (plan.NeedsTemplateCodeGen) Console.WriteLine("  - Template code");
    if (plan.NeedsDataExtraction) Console.WriteLine("  - Data extraction");
    if (plan.NeedsAssetRip) Console.WriteLine("  - Asset ripping");

    // Run extraction pipeline...
    var results = await RunExtractionPipeline(plan);

    // Update manifest
    orchestrator.UpdateManifest(plan, results);
}
else
{
    Console.WriteLine("✓ Using cached data");
}
```
