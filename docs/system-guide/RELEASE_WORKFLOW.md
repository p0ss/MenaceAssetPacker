# Release Workflow

This guide is for maintainers preparing and shipping a Modkit release.

Primary automation:

- GitHub Actions workflow: `.github/workflows/release.yml`
- Local build script: `build-redistributables.sh`

## What Triggers A Release

`release.yml` runs on:

- Tag push matching `v*` (for example `v19.0.0`)
- Manual run (`workflow_dispatch`) with `version` input (for example `19.0.0`)

Release version is read from `third_party/versions.json`:

- `components.ModpackLoader.version`

Workflow guards fail the run if:

- Tag version (`vX.Y.Z`) does not match `versions.json`
- `workflow_dispatch` input `version` does not match `versions.json`

## Prerequisites

- Write access to push tags and create releases
- .NET SDK 10.x for local builds
- `zip`, `tar`, and `sha256sum` available locally (for `build-redistributables.sh`)
- Bundled component directories populated in `third_party/bundled/`

Optional tooling:

- GitHub CLI (`gh`) for manual dispatch and post-release uploads

## Pre-Release Checklist

1. Update `third_party/versions.json` with intended component versions.
2. Run local packaging smoke test:
   - `./build-redistributables.sh`
   - This regenerates `src/Shared/ModkitVersion.cs` from `versions.json`.
3. Run tests:
   - `dotnet test tests/Menace.Modkit.Tests`
   - `dotnet test tests/Menace.ModpackLoader.Tests`
4. Confirm generated component manifest:
   - `dist/components/manifest.json`

Note: `build-redistributables.sh` updates bundled runtime DLL copies in:

- `third_party/bundled/DataExtractor/`
- `third_party/bundled/ModpackLoader/`

If those updates are intended for release, commit them before tagging.

Note: CI release also regenerates `src/Shared/ModkitVersion.cs` from
`third_party/versions.json` before building.

## Recommended Release Path (Tag Push)

1. Commit release changes.
2. Create and push tag:

```bash
git tag v19.0.0
git push origin v19.0.0
```

3. Monitor GitHub Actions `Release` workflow.
4. Verify the GitHub release named/tagged `v19.0.0`.

## Manual Release Path (Workflow Dispatch)

Run from CLI:

```bash
gh workflow run release.yml -f version=19.0.0
```

Or run from Actions UI with the same `version` input.

Manual dispatch is useful for re-running packaging from the current commit
without pushing a new tag event. The `version` input must match
`third_party/versions.json`.

## What The Workflow Builds

The workflow publishes:

- GUI app archives (`linux-x64`, `win-x64`)
- CLI archives (`linux-x64`, `win-x64`)
- `docs.zip`
- Component archives in `dist/components/`:
  - `DataExtractor.zip`
  - `ModpackLoader.zip`
  - `DotNetRefs.zip` (if bundled refs exist)
  - `MelonLoader-linux-x64.tar.gz`
  - `MelonLoader-win-x64.zip`
  - `AssetRipper-linux-x64.tar.gz`
  - `AssetRipper-win-x64.zip`
- Per-file `.sha256` files
- `dist/components/manifest.json`

The workflow then creates/updates the GitHub release via
`softprops/action-gh-release`.

## Component Manifest And `versions.json`

Release builds generate `dist/components/manifest.json` containing SHA256 + size
for each component archive.

Example (from `build-redistributables.sh`):

```json
{
  "generatedAt": "2026-02-11T02:07:12Z",
  "components": {
    "AssetRipper-linux-x64.tar.gz": {"sha256": "3f8856068fd1aa984d5947be25ef99451dd3509a94f50d0182261c148f6952c2", "size": 45357228},
    "AssetRipper-win-x64.zip": {"sha256": "07e26d876de59b3f32aff04c6b3e2c469181567978dd156db0b1b46d1ffc2fd1", "size": 49141019},
    "DataExtractor.zip": {"sha256": "3703b861d99d0487d25b6666e780b6919359647a5f2ed5f47e72e025d8062be7", "size": 154100},
    "DotNetRefs.zip": {"sha256": "720dedaebded7764fcac0530984ca8dc2ae3d1d404edfc47c1bbc1c159e25aa1", "size": 2503191},
    "MelonLoader-linux-x64.tar.gz": {"sha256": "a102cf019367534ece0b242088ccea99064ebf3c42cfbac3b44c7ac3b05d29d6", "size": 17640608},
    "MelonLoader-win-x64.zip": {"sha256": "fc783d09881afc768cc031e162e29bbd6b00cdf3280ba855afcb863f5baebb25", "size": 17659562},
    "ModpackLoader.zip": {"sha256": "41761df71e1cc0d00e25a6966c961c5fe6282d3858a051ff2b50d9471a95df7c", "size": 4842712}
  }
}
```

Use these values to update `third_party/versions.json` entries (`sha256`, `size`
and version fields as appropriate).

## Important: Component Download Channel

Current `third_party/versions.json` component URLs point to a fixed release tag
channel (`.../releases/download/components-v1/...`).

After a new build, keep runtime downloads working by doing one of:

1. Upload new component archives to `components-v1`, or
2. Update component URLs in `third_party/versions.json` to a new tag/release.

If you keep the fixed `components-v1` channel, this command is the usual
post-release sync step:

```bash
gh release upload components-v1 dist/components/*.zip dist/components/*.tar.gz dist/components/manifest.json --clobber
```

Then commit/push updated `third_party/versions.json` if checksum/size/version
values changed.

## Verification Checklist

After release completes:

1. Download and smoke-test GUI archive on Linux/Windows.
2. Confirm component archives exist in the expected release/channel.
3. Verify Setup in app can download required components from `versions.json`.
4. Confirm in-game loader starts and REPL initializes.

## Quick Commands

```bash
# Tag release
git tag v19.0.0 && git push origin v19.0.0

# Manual workflow dispatch
gh workflow run release.yml -f version=19.0.0

# Upload refreshed component channel assets
gh release upload components-v1 dist/components/*.zip dist/components/*.tar.gz dist/components/manifest.json --clobber
```
