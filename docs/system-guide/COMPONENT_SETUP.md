# Component Setup

This document describes how required tools are versioned, downloaded, and
resolved at runtime by the setup flow.

Primary implementation:

- `src/Menace.Modkit.App/Services/ComponentManager.cs`
- `src/Menace.Modkit.App/ViewModels/SetupViewModel.cs`
- `third_party/versions.json`

## Source Of Truth

Component definitions live in:

- `third_party/versions.json`

This manifest defines:

- Component name
- Version
- Required/optional status
- Install path
- Per-platform download URLs

It also drives runtime version constants:

- `build-redistributables.sh` generates `src/Shared/ModkitVersion.cs` from
  `components.ModpackLoader.version`
- `release.yml` regenerates the same file in CI before publishing artifacts

It also carries `remoteUrl`, which can be fetched to refresh available versions.

## Where Components Live

Local cache:

- `~/.menace-modkit/components/`

Local install manifest:

- `~/.menace-modkit/components/manifest.json`

Bundled fallback content:

- `third_party/bundled/...`

## Setup Decision Logic

`NeedsSetupAsync()` returns `true` when any required component is not up to
date.

Required and optional components are surfaced in the setup UI via
`SetupViewModel`.

## Download/Install Lifecycle

For each component:

1. Resolve manifest entry (`GetVersionsManifestAsync`)
2. Select download for current platform (`win-x64`/`linux-x64`) or `any`
3. Download archive to temp file
4. Optionally validate SHA256 if provided
5. Extract archive into component install directory
6. Mark install/version in local manifest

Extraction safety:

- Zip extraction uses path traversal protection
- `tar.gz` extraction shells out to `tar`
- Linux executable permissions are applied to extensionless files

## Resolution Priority

Convenience getters prefer cached installs, then bundled fallback:

- `GetMelonLoaderPath()`
- `GetDataExtractorPath()`
- `GetModpackLoaderPath()`
- `GetDotNetRefsPath()`
- `GetAssetRipperPath()`

AssetRipper also resolves platform-specific executable names:

- Windows: `AssetRipper.GUI.Free.exe`
- Linux: `AssetRipper.GUI.Free`

## Remote Manifest Behavior

Manifest lookup behavior:

- Try remote manifest (`remoteUrl`) with short timeout
- Cache remote response in-memory for ~5 minutes
- Fall back to bundled `third_party/versions.json` when remote is unavailable

## Environment Checks Integration

The setup screen runs environment diagnostics (`EnvironmentChecker`) and can
offer auto-fixes such as:

- Install MelonLoader
- Launch game (to generate IL2CPP assemblies)
- Install DataExtractor

## Operational Notes

- Keep `third_party/versions.json` and release artifacts in sync.
- If local cache state is suspect, remove/reinstall affected components via
  setup rather than editing cache manifest manually.
- For reference-compilation issues, also see:
  `docs/system-guide/REFERENCE_RESOLUTION.md`.
