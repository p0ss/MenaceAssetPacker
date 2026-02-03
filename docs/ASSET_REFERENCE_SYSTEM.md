# Asset Reference Resolution System

## Overview

This system automatically resolves Unity Object references (instance IDs) to human-readable asset names and links them to AssetRipper exported files.

## Architecture

```
┌─────────────────────────┐
│   Game Runtime          │
│   (DataExtractor)       │  Extracts templates + builds reference table
└──────────┬──────────────┘
           │ Produces
           ↓
┌─────────────────────────┐
│  AssetReferences.json   │  { InstanceId → Name, Type, AssetPath }
│  AccessoryTemplate.json │  Contains instance ID references
└──────────┬──────────────┘
           │ Processed by
           ↓
┌─────────────────────────┐
│  match_asset_references │  Python script - matches refs to AssetRipper files
│         .py             │  Updates AssetPath fields
└──────────┬──────────────┘
           │ Used by
           ↓
┌─────────────────────────┐
│  AssetReferenceResolver │  C# service in modkit
│  StatsEditorViewModel   │  Displays: "icon_ammo → Sprites/icon.png"
└─────────────────────────┘
```

## Components

### 1. DataExtractor Mod (C# - Game Runtime)

**Files:**
- `src/Menace.DataExtractor/DataExtractorMod.cs`

**Features:**
- Extracts all 861 fields from 72 template types
- For Unity Object reference fields:
  - Registers instance ID, name, and type
  - Attempts to dereference LocalizedString fields to get text
- Produces:
  - `TemplateType.json` - Template data with instance IDs
  - `AssetReferences.json` - Lookup table

**AssetReferences.json format:**
```json
[
  {
    "InstanceId": 1976593696,
    "Name": "items.armor_piercing_ammo.title",
    "Type": "LocalizedLine",
    "AssetPath": ""
  },
  {
    "InstanceId": 1937563168,
    "Name": "icon_ammo_armor_piercing",
    "Type": "Sprite",
    "AssetPath": "ExportedProject/Assets/Sprites/icon_ammo_armor_piercing.png"
  }
]
```

### 2. Asset Matching Script (Python)

**File:** `match_asset_references.py`

**Usage:**
```bash
python3 match_asset_references.py
```

**Features:**
- Scans AssetRipper export directory
- Builds index of asset files by name
- Matches instance IDs to asset files
- Updates `AssetPath` field in `AssetReferences.json`
- Supports fuzzy matching for name variations

**Matching Logic:**
1. **Exact match** by filename (without extension)
2. **Type-aware matching**: Prefers `.png` for Sprites, `.wav` for AudioClips, etc.
3. **Fuzzy match**: Removes special characters and compares

### 3. AssetReferenceResolver Service (C# - Modkit)

**File:** `src/Menace.Modkit.App/Services/AssetReferenceResolver.cs`

**API:**
```csharp
var resolver = new AssetReferenceResolver();
resolver.LoadReferences(gameInstallPath);

var resolved = resolver.Resolve(instanceId);

if (resolved.IsReference)
{
    // Display: resolved.DisplayValue (asset name)
    // Link to: resolved.AssetPath (file path)
    // Type: resolved.AssetType (Sprite, LocalizedLine, etc.)
}
```

**ResolvedAsset Properties:**
- `DisplayValue` - Human-readable text for UI
- `IsReference` - Is this an asset reference?
- `AssetName` - Name from runtime
- `AssetType` - Unity type (Sprite, AudioClip, etc.)
- `AssetPath` - Relative path to AssetRipper file
- `HasAssetFile` - Whether asset file exists
- `CanLinkToAssetBrowser` - Can navigate to asset browser

### 4. UI Integration

**File:** `src/Menace.Modkit.App/ViewModels/StatsEditorViewModel.cs`

**Features:**
- Loads `AssetReferenceResolver` on startup
- Automatically resolves instance IDs when displaying template properties
- Shows asset references as:
  - `icon_ammo → Sprites/icon.png` (with asset file)
  - `icon_ammo (no asset file)` (without file)
  - `[Ref:1937563168]` (unknown reference)

## Workflow

### Initial Setup

1. **Extract game data:**
   ```bash
   # Run game with DataExtractor mod
   # Waits for game to load, extracts templates
   # Produces: UserData/ExtractedData/*.json + AssetReferences.json
   ```

2. **Export assets with AssetRipper:**
   ```bash
   # Use AssetRipper to extract game assets
   # Produces: ExportedProject/Assets/**/*.png, *.prefab, etc.
   ```

3. **Match references:**
   ```bash
   python3 match_asset_references.py
   # Scans AssetRipper exports
   # Updates AssetReferences.json with AssetPath fields
   ```

4. **Use modkit:**
   - Modkit loads `AssetReferences.json`
   - Asset names appear instead of instance IDs
   - Click asset names to view in asset browser (future)

### After Game Update

1. **Re-extract:**
   ```bash
   # Run game (new offsets/templates may exist)
   ```

2. **Re-match:**
   ```bash
   python3 match_asset_references.py
   ```

3. **Modkit auto-reloads** references

## Field Type Handling

| Field Type | Extraction | Display |
|------------|------------|---------|
| `int`, `float`, `bool` | Direct read | `42`, `1.5`, `true` |
| `LocalizedLine` | Try to read text @ offset 0x40 | `"Armor Piercing Ammo"` or asset name |
| `Sprite`, `Texture2D` | Instance ID → lookup | `icon_ammo → Sprites/icon.png` |
| `AudioClip` | Instance ID → lookup | `explosion_sound → Audio/explosion.wav` |
| `GameObject`, `Prefab` | Instance ID → lookup | `model_tank → Prefabs/model_tank.prefab` |
| Unknown reference | Instance ID | `[Ref:1976593696]` |

## Error Handling

**No AssetReferences.json:**
- Modkit displays raw instance IDs
- Still functional, just less user-friendly

**Asset not found:**
- Displays: `asset_name (no asset file)`
- Still shows the asset name from runtime

**AssetRipper exports missing:**
- `match_asset_references.py` prompts for path
- Can be run multiple times as exports become available

## Future Enhancements

### Clickable Links
Add command to StatsEditorViewModel:
```csharp
public void NavigateToAsset(long instanceId)
{
    var resolved = _assetResolver.Resolve(instanceId);
    if (resolved.CanLinkToAssetBrowser)
    {
        // Navigate to Asset Browser tab
        // Select file at: resolved.AssetPath
    }
}
```

### Asset Preview Thumbnails
- For Sprite references, show thumbnail inline
- For AudioClip, show waveform or play button
- For Prefab, show 3D preview

### Reverse Lookup
- From Asset Browser, show which templates reference this asset
- "Used by: WeaponTemplate/pistol.Icon, ArmorTemplate/vest.IconEquipment"

### Hot Reload
- Watch `AssetReferences.json` for changes
- Auto-reload when `match_asset_references.py` runs

## Files Created/Modified

### Created:
- `src/Menace.DataExtractor/DataExtractorMod.cs` - Added AssetReference tracking
- `src/Menace.Modkit.App/Services/AssetReferenceResolver.cs` - Resolver service
- `match_asset_references.py` - Asset matching script
- `ASSET_REFERENCE_SYSTEM.md` - This document

### Modified:
- `src/Menace.DataExtractor/DataExtractorMod.cs` - Reference tracking + saving
- `src/Menace.Modkit.App/ViewModels/StatsEditorViewModel.cs` - UI integration
- `src/Menace.Modkit.App/Services/ModpackManager.cs` - Added GetGameInstallPath()
- `generate_all_templates.py` - Added ReadUnityObjectReference calls

## Testing

1. **Verify extraction:**
   ```bash
   cat ~/.steam/.../Menace\ Demo/UserData/ExtractedData/AssetReferences.json | head -20
   ```

2. **Run matching:**
   ```bash
   python3 match_asset_references.py
   # Should show: "✅ Matched X/Y asset references"
   ```

3. **Check modkit:**
   - Open modkit
   - Navigate to Stats Editor
   - Select AccessoryTemplate → armor_piercing_ammo
   - Verify fields show asset names instead of numbers

## Success Criteria

✅ Asset references extracted during gameplay
✅ Python script matches references to files
✅ Modkit displays asset names instead of IDs
✅ Graceful fallback when assets not found
✅ Automatic after game updates (just re-run extraction + matching)
