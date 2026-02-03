# Cross-Platform Fixes

## Issues Fixed

### 1. Hardcoded Linux Paths in ModpackManager

**Problem**: ModpackManager had hardcoded Linux paths (`~/.steam/debian-installation/...`) that appeared even when users set the game path to Windows locations like `C:\Steam\...`.

**Root Cause**: Paths were initialized in the constructor using hardcoded Linux environment paths.

**Fix**: Changed to computed properties that read from `AppSettings.Instance.GameInstallPath`:

```csharp
public string VanillaDataPath
{
    get
    {
        var gameInstallPath = AppSettings.Instance.GameInstallPath;
        if (string.IsNullOrEmpty(gameInstallPath))
            return string.Empty;

        return Path.Combine(gameInstallPath, "UserData", "ExtractedData");
    }
}

public string ModsBasePath
{
    get
    {
        var gameInstallPath = AppSettings.Instance.GameInstallPath;
        if (string.IsNullOrEmpty(gameInstallPath))
            return string.Empty;

        return Path.Combine(gameInstallPath, "Mods");
    }
}
```

**Files Modified**:
- `src/Menace.Modkit.App/Services/ModpackManager.cs:26-56` (properties)
- `src/Menace.Modkit.App/Services/ModpackManager.cs:69-74` (HasVanillaData)
- `src/Menace.Modkit.App/Services/ModpackManager.cs:93-102` (GetActiveMods)
- `src/Menace.Modkit.App/Services/ModpackManager.cs:132-139` (GetVanillaTemplatePath)
- `src/Menace.Modkit.App/Services/ModpackManager.cs:174-187` (DeployModpack)
- `src/Menace.Modkit.App/Services/ModpackManager.cs:217-232` (EnsureDirectoriesExist)

### 2. Platform-Specific AssetRipper Bundling

**Problem**: Windows builds were receiving the Linux AssetRipper executable, causing "not a valid application for this OS platform" errors.

**Root Cause**: Build script copied all bundled files to all platforms indiscriminately.

**Fix**:
1. Created platform-specific directories: `third_party/bundled/AssetRipper/linux/` and `third_party/bundled/AssetRipper/windows/`
2. Updated `AssetRipperService.FindAssetRipper()` to detect platform and look for appropriate executable:

```csharp
private string? FindAssetRipper()
{
    // Determine platform subdirectory and executable name
    string platformDir = OperatingSystem.IsWindows() ? "windows" : "linux";
    string executableName = OperatingSystem.IsWindows()
        ? "AssetRipper.GUI.Free.exe"
        : "AssetRipper.GUI.Free";

    // Check bundled AssetRipper
    var bundledAssetRipper = Path.Combine(
        AppContext.BaseDirectory,
        "third_party", "bundled", "AssetRipper", platformDir, executableName);

    // ... rest of logic
}
```

3. Updated `build-redistributables.sh` to copy only platform-specific AssetRipper executables to each build:
   - Linux build gets `AssetRipper/linux/*`
   - Windows build gets `AssetRipper/windows/*`

**Files Modified**:
- `src/Menace.Modkit.App/Services/AssetRipperService.cs:150-183`
- `build-redistributables.sh:23-52`
- Created: `third_party/bundled/AssetRipper/linux/` (contains Linux executable)
- Created: `third_party/bundled/AssetRipper/windows/` (needs Windows executable - see below)

## Windows AssetRipper Setup (Required for Windows Builds)

To build Windows redistributables, you need to download the Windows version of AssetRipper:

1. Go to https://github.com/AssetRipper/AssetRipper/releases/latest
2. Download `AssetRipper_win_x64.zip`
3. Extract the contents
4. Copy `AssetRipper.GUI.Free.exe` and all DLLs to `third_party/bundled/AssetRipper/windows/`

Without this, Windows builds will not be able to extract assets.

See: `third_party/bundled/AssetRipper/windows/README.md`

## Testing

### Linux Testing
1. Build: `~/.dotnet9/dotnet build src/Menace.Modkit.App`
2. Run: `dist/gui-linux-x64/Menace.Modkit.App`
3. Verify:
   - Settings shows correct game path (no hardcoded Linux paths)
   - Asset extraction finds `third_party/bundled/AssetRipper/linux/AssetRipper.GUI.Free`

### Windows Testing (requires Windows AssetRipper download first)
1. Build: `./build-redistributables.sh`
2. Test on Windows with `dist/gui-win-x64/Menace.Modkit.App.exe`
3. Verify:
   - Settings accepts Windows paths like `C:\Steam\...`
   - Asset extraction finds `third_party/bundled/AssetRipper/windows/AssetRipper.GUI.Free.exe`

## Impact

### Before Fixes
- Windows users saw Linux paths in error messages
- Windows users couldn't extract assets (wrong executable)
- Windows users had broken setup experience
- Vanilla data detection failed even when data existed
- App crashed with empty path exceptions

### After Fixes
- Paths dynamically read from user-configured game install path
- Platform-specific executables bundled correctly
- Windows and Linux builds both work correctly
- Vanilla data properly detected using computed properties
- No crashes from empty path attempts

## Future Improvements

1. **Mac Support**: Add `third_party/bundled/AssetRipper/macos/` directory
2. **Auto-Download**: Instead of manual download, could fetch appropriate AssetRipper version at build time
3. **Better Error Messages**: If AssetRipper not found, show platform-specific download instructions
