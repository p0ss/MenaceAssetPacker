# AssetRipper Patches

This document describes modifications made to AssetRipper for use with MenaceAssetPacker.

## Upstream Version

Based on AssetRipper version **1.3.4** from https://github.com/AssetRipper/AssetRipper

## Patches Applied

### 1. Build Configuration

- Modified to build headless/server mode for automation
- Configured for integration with MenaceAssetPacker's asset extraction workflow

### 2. Export Settings

Default export settings optimized for modding workflow:
- `SpriteExportMode: Texture2D` - Exports sprites as PNG images
- `ImageExportFormat: Png` - Uses PNG for all texture exports

### 3. Recursive Bundle Collection Resolution (Root Cause Fix)

**File:** `Source/AssetRipper.Assets/Bundles/Bundle.cs`

Fixed `ResolveCollection()` and `ResolveResource()` to recursively search descendant bundles:

**Problem:** PPtr references in materials would fail to resolve textures (~40% of prefab exports had missing textures) because `TryResolveFromChildBundles()` only searched direct child bundles' collections, not recursively into nested bundle structures.

Unity asset bundles can contain nested FileContainers which become nested SerializedBundles. When a prefab in bundle A referenced a texture with an internal name like "CAB-abc123" (which lives inside bundle B's nested structure), the resolution would fail because:
1. When resolving from bundle A, it walks up to the root GameBundle
2. GameBundle searches its direct child bundles (including B)
3. But B's nested child bundles were NOT searched - only B's direct collections

**Solution:** Made `TryResolveFromChildBundles()` recursive, so it searches all descendant bundles, not just direct children. This ensures that collections/resources in any level of the bundle hierarchy are found.

### 4. Enhanced Disk Dependency Lookup (Supplementary Fix)

**File:** `Source/AssetRipper.Import/Structure/Platforms/PlatformGameStructure.cs`

Enhanced `RequestDependency()` method with fallback strategies for finding dependency files on disk:

1. Case-insensitive matching in Files dictionary
2. Match by filename only (ignoring path)
3. Match by filename without extension
4. Case variations when searching DataPaths
5. Recursive search in StreamingAssets subdirectories

This handles edge cases where dependencies need to be loaded from disk with different naming conventions.

### 5. GLB Export Enhancements

**File:** `Source/AssetRipper.Export.Modules.Models/GlbLevelBuilder.cs`

- Added SkinnedMeshRenderer support with skeleton hierarchy
- Added animation export from AnimatorController
- Added fallback texture lookup by material name when PPtr resolution fails
- Added TextureNameCache for efficient bundle-wide texture searches

## How to Update

When upstream AssetRipper releases a new version:

1. **Check Release Notes**
   - Review changes at https://github.com/AssetRipper/AssetRipper/releases
   - Note any API changes that may affect integration

2. **Update Submodule**
   ```bash
   cd third_party/AssetRipper
   git fetch origin
   git checkout <new-version-tag>
   cd ../..
   git add third_party/AssetRipper
   ```

3. **Test Integration**
   - Build the solution
   - Test asset extraction workflow
   - Verify exported asset formats are correct

4. **Update Bundled Binaries**
   - Build AssetRipper for target platforms
   - Copy to `third_party/bundled/AssetRipper/`
   - Update `third_party/versions.json` with new version

5. **Update This Document**
   - Update the upstream version
   - Document any new patches required

## Version History

| Date | Upstream Version | Notes |
|------|------------------|-------|
| Initial | 1.3.4 | Initial integration |

## Building AssetRipper

```bash
cd third_party/AssetRipper/Source
dotnet build -c Release
```

Output binaries are in `0Bins/`.

## Known Issues

- Large asset files (>2GB) may cause memory issues during extraction
- Some shader formats may not decompile correctly

## Contact

For issues with AssetRipper itself, see: https://github.com/AssetRipper/AssetRipper/issues

For issues with the integration in MenaceAssetPacker, create an issue in this repository.
