# Extraction Orchestration System

## Overview

The extraction orchestration system manages the IL2CPP dump generation, template extraction, data extraction, and asset ripping pipeline. It uses hash-based caching to avoid redundant operations and only reprocesses when the game binary changes.

## Components

### ExtractionManifest

Tracks the state of all extraction operations:

```json
{
  "UnityVersion": "6000.0.72f1",
  "GameAssemblyHash": "a1b2c3d4...",
  "MetadataHash": "e5f6g7h8...",
  "IL2CppDumpTimestamp": "2025-10-10T16:30:00Z",
  "TemplateCodeGenerationTimestamp": "2025-10-10T16:31:00Z",
  "DataExtractionTimestamp": "2025-10-10T16:32:00Z",
  "AssetRipTimestamp": "2025-10-10T16:35:00Z",
  "ExtractedTemplates": ["WeaponTemplate", "EntityTemplate", ...],
  "DataExtractorHash": "f9g0h1i2...",
  "GameExecutablePath": "/path/to/Menace.exe"
}
```

**Location:** `{GameDir}/UserData/ExtractionCache/manifest.json`

### Services

| Service | File | Responsibility |
|---------|------|----------------|
| **ExtractionOrchestrator** | `Menace.Modkit.Core` | Hash computation, cache invalidation |
| **AssetRipperService** | `Menace.Modkit.App` | Asset extraction with profiles |
| **DataTemplateLoader** | `Menace.Modkit.App` | Loads extracted template JSON |
| **SchemaService** | `Menace.Modkit.App` | Loads IL2CPP type metadata |

## Pipeline Flow

```
1. App Launch
   ↓
2. Check Manifest (hash comparison)
   ↓
3. Determine Operations Needed
   ↓
4. Execute Pipeline:

   [Game Binary Changed?]
   ├── Yes → Full extraction pipeline
   │         ├── Deploy DataExtractor mod
   │         ├── Prompt user to launch game
   │         ├── DataExtractor writes templates to JSON
   │         ├── Run AssetRipper (if configured)
   │         └── Update manifest with new hashes
   │
   └── No → [DataExtractor Changed?]
           ├── Yes → Re-extract templates only
           └── No → Use cached data (instant)
```

## Hash-Based Caching

### What Gets Hashed

| File | Purpose |
|------|---------|
| `GameAssembly.so/dll` | Main IL2CPP binary |
| `global-metadata.dat` | IL2CPP metadata |
| `Menace.DataExtractor.dll` | Extractor mod version |

### Cache Invalidation Rules

```csharp
// Full re-extraction if:
GameAssemblyHash != CachedHash || MetadataHash != CachedHash

// Template re-extraction if:
DataExtractorHash != CachedHash

// Asset re-rip if:
GameBinaryChanged || NeverRipped
```

## Trigger Conditions

| Operation | Triggers When |
|-----------|---------------|
| **Data Extraction** | Game binary changes OR DataExtractor.dll changes |
| **Asset Ripping** | Game binary changes OR never ripped OR profile changed |
| **Schema Reload** | Extraction completed |

## Asset Ripper Profiles

| Profile | Assets Included | Time | Size |
|---------|----------------|------|------|
| **Essential** | Sprites, Textures, Audio, Text | ~30s | ~100 MB |
| **Standard** | Essential + Meshes, Shaders, VFX, Prefabs | ~1-2 min | ~250 MB |
| **Complete** | Everything including Unity internals | ~5-10 min | ~1-2 GB |

### Standard Profile Excludes

- AnimatorController, AnimatorOverrideController
- AssetBundle, BuildSettings, GraphicsSettings
- ComputeShader, MonoScript (Unity internals)
- PhysicsMaterial, LightingSettings
- NavMeshData, OcclusionCullingData
- Unity configuration files

## Performance

### First Launch (Cold Cache)
- Detect game installation → ~1s
- Deploy DataExtractor → ~2s
- User launches game, extraction runs → ~10-30s
- Asset ripping (Standard) → ~1-2 min
- **Total: ~2-3 minutes**

### Subsequent Launches (Warm Cache)
- Load cached manifest → ~100ms
- Verify hashes match → ~500ms
- Load cached data → ~1s
- **Total: ~2 seconds**

### Game Update
- Detect hash change → ~1s
- Full pipeline re-run → ~2-3 min
- Cache updated for next time

## UI Integration

### Settings Tab

```
Extraction Settings:
  [✓] Auto-update on game version change
  [✓] Enable caching

Asset Ripper Profile:
  ( ) Essential   - Sprites, Textures, Audio only (fastest)
  (•) Standard    - Recommended for modding
  ( ) Complete    - Everything (slowest)

Cache Management:
  Last extraction: 2 hours ago
  Cache size: 250 MB

  [View Cache Details]  [Clear Cache]  [Force Re-extract]
```

### Progress Notifications

When extraction runs:

```
🔄 Game update detected

Running extraction pipeline:
├── ✓ Deploying DataExtractor...
├── ⏳ Waiting for game extraction...
├── ✓ Templates extracted (72 types)
├── ✓ Assets ripped (Standard profile)
└── ✓ Complete! Cached for future use.
```

## External Dependencies

The orchestration system relies on components managed by the app's setup flow (`ComponentManager`):

### Required Tools

| Tool | Purpose | Installation |
|------|---------|--------------|
| **MelonLoader** | Mod injection framework | Auto-installed by app |
| **AssetRipper** | Asset extraction | Downloaded by app setup (or loaded from bundled copy) |
| **Il2CppDumper** | IL2CPP analysis (optional) | For advanced users |

### Tool Paths

The app resolves tool paths automatically:
- **AssetRipper** — Component cache first, bundled fallback
- **Game Install Path** — Auto-detected or manually configured by user

The app bundles:
- `Menace.DataExtractor.dll` — Template extraction mod
- `Menace.ModpackLoader.dll` — Runtime mod loader
- Python scripts for internal processing

## Python Scripts

Internal scripts used by the orchestration system:

| Script | Purpose |
|--------|---------|
| `generate_all_templates.py` | Generates extraction code from IL2CPP dump |
| `generate_injection_code.py` | Generates injection code for patching |
| `extract_template_dump.py` | Extracts minimal template dump (35MB → 500KB) |
| `match_asset_references.py` | Matches instance IDs to asset files |

These are primarily for modkit developers maintaining game compatibility.

## Troubleshooting

### Cache Corruption

```bash
# Delete cache and force regeneration
rm -rf "$GAME_DIR/UserData/ExtractionCache"
# Or use "Clear Cache" button in Settings
```

### Extraction Not Running

1. Check game path is correctly set
2. Verify MelonLoader is installed
3. Check `MelonLoader/Latest.log` for errors
4. Try "Force Re-extract" in Settings

### AssetRipper Errors

1. Verify AssetRipper path in Settings
2. Check AssetRipper version compatibility
3. Try "Essential" profile (fewer assets, less likely to fail)
4. Check disk space (Complete profile needs 1-2GB)

### Hash Mismatch Issues

If manifest says "up to date" but data seems wrong:
1. Click "Force Re-extract" in Settings
2. Or delete `UserData/ExtractionCache/manifest.json`
3. Restart the modkit app

## API Example

```csharp
// Check if extraction is needed
var orchestrator = new ExtractionOrchestrator(gameInstallPath);
var status = await orchestrator.CheckStatusAsync();

if (status.NeedsExtraction)
{
    // Show progress UI
    await orchestrator.RunExtractionAsync(progress => {
        UpdateProgressBar(progress.Stage, progress.Percent);
    });
}

// Load cached data
var templates = await DataTemplateLoader.LoadAsync(gameInstallPath);
```
