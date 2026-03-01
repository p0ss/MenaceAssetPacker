# Release Channels

The Menace Modkit supports two release channels: **Stable** and **Beta**. This allows users to opt into bleeding-edge builds while keeping a stable default for most users.

## Channel Overview

| Channel | Audience | Update Frequency | Risk Level |
|---------|----------|------------------|------------|
| **Stable** | Most users | When well-tested | Low |
| **Beta** | Early adopters, testers | Frequent | Higher |

## Versioning Scheme

We use a major version strategy to distinguish channels:

- **Beta line** (e.g., v31.x): Active development, new features, may have bugs
- **Stable line** (e.g., v32.0): Promoted from beta when ready, well-tested
- **Stable patches** (e.g., v32.0.1, v32.0.2): Bug fixes to stable, no new features

### Example Timeline

```
v31.0.0 (beta) → v31.0.1 (beta) → v31.0.2 (beta)
    ↓ (promote when stable)
v32.0.0 (stable) → v32.0.1 (stable patch) → v32.0.2 (stable patch)
    ↓ (continue development)
v33.0.0 (beta) → ...
```

## Manifest Files

Each channel has its own version manifest:

| Channel | Local File | Remote URL |
|---------|------------|------------|
| Stable | `third_party/versions.json` | `https://raw.githubusercontent.com/p0ss/MenaceAssetPacker/main/third_party/versions.json` |
| Beta | `third_party/versions-beta.json` | `https://raw.githubusercontent.com/p0ss/MenaceAssetPacker/main/third_party/versions-beta.json` |

Both files are bundled with the app. When the remote fetch fails, the app falls back to the bundled manifest for the user's selected channel.

## How Channel Selection Works

### User-Facing

Users select their channel in **Tool Settings → Release Channel**:
- Radio buttons for Stable (recommended) / Beta
- Warning banner shown when Beta is selected
- Setup screen shows a `[BETA]` badge when on beta channel

### Technical Flow

1. User's channel preference is stored in `AppSettings.UpdateChannel` (persisted to `settings.json`)
2. `ComponentManager.GetManifestUrlForChannel()` returns the appropriate remote URL
3. `ComponentManager.FetchRemoteManifestAsync()` fetches from that URL
4. On failure, `ComponentManager.GetBundledManifest()` falls back to the bundled manifest for the current channel
5. When channel changes, `ComponentManager.InvalidateManifestCache()` clears the cached manifest

### Key Files

| File | Purpose |
|------|---------|
| `Services/AppSettings.cs` | `UpdateChannel` property, `SetUpdateChannel()`, persistence |
| `Services/ComponentManager.cs` | `GetManifestUrlForChannel()`, `FindVersionsJson()`, manifest caching |
| `ViewModels/ToolSettingsViewModel.cs` | `IsStableChannel`/`IsBetaChannel` bindings, `RefreshChannelInfoAsync()` |
| `Views/ToolSettingsView.axaml.cs` | `BuildReleaseChannelSection()` UI |
| `ViewModels/SetupViewModel.cs` | `IsBetaChannel`, `ShowChannelBadge` for header badge |

## Releasing

### Via GitHub Actions (Recommended)

Use the manual workflow dispatch:

1. Go to **Actions → Release → Run workflow**
2. Enter the version number (e.g., `31.0.3` for beta, `32.0.0` for stable)
3. Select the channel (`beta` or `stable`)
4. Run the workflow

The workflow will:
- Build the app with the specified version
- Update the appropriate manifest file(s)
- Commit the manifest changes
- Create a GitHub release (marked as pre-release for beta)

### Channel-Specific Behavior

| Channel | Manifests Updated | GitHub Release |
|---------|-------------------|----------------|
| Beta | `versions-beta.json` only | Pre-release |
| Stable | Both `versions.json` and `versions-beta.json` | Full release |

**Why update both for stable?** When promoting to stable, beta users should also see the new version (it's newer than their current beta).

### Via Git Tag (Legacy)

Pushing a tag still works and defaults to stable channel:

```bash
git tag v32.0.0
git push origin v32.0.0
```

## Component States

The app distinguishes between different update states:

| State | Meaning | Blocks Setup? |
|-------|---------|---------------|
| `UpToDate` | Installed version matches latest | No |
| `UpdateAvailable` | Compatible version installed, newer exists | No |
| `Outdated` | Incompatible version, must update | Yes |
| `NotInstalled` | Not installed | Yes |

`UpdateAvailable` is informational only - the user can continue without updating. This is important for beta users who may have a newer beta version than what's in the stable manifest.

## Testing Channel Switching

1. Build and run the app
2. Go to **Tool Settings → Release Channel**
3. Select Beta → verify warning appears
4. Check for updates → verify request goes to `versions-beta.json`
5. Switch to Stable → verify request goes to `versions.json`
6. Restart app on Beta → verify `[BETA]` badge shows in Setup header

## Future Enhancements

Not yet implemented:

- In-game smoke tests that run on beta builds before promotion
- Automatic beta-to-stable promotion after X days without issues
- Per-component channel selection (e.g., stable Modkit + beta ModpackLoader)
- Rollback capability in UI
- Discord webhook notifications for new releases
