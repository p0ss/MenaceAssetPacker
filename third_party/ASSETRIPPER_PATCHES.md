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
