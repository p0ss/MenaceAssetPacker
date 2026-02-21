# Uninstalling Menace Modkit

This guide explains how to remove the Menace Modkit and all related files.

## Quick Uninstall (Game Only)

To remove mods from the game while keeping the Modkit app:

1. Open the Modkit
2. Go to **Loader Settings**
3. Click **Uninstall from Game**

This removes MelonLoader, deployed mods, and runtime files from the game directory. Your saves in `UserData/Saves` are preserved.

## Full Uninstall

To completely remove everything:

### 1. Uninstall from Game

Use the button above, or manually delete these from your game folder:
- `MelonLoader/` - The mod loader framework
- `Mods/` - Deployed mod DLLs
- `UserLibs/` - Runtime dependencies
- `dotnet/` - MelonLoader's .NET runtime
- `version.dll` - MelonLoader hook (in game root)
- `dobby.dll`, `bootstrap.dll` - MelonLoader files (if present)

**Optional:** Delete `UserData/` if you don't want to keep extracted data and saves.

### 2. Delete the Modkit App

Delete the folder where you extracted the Modkit.

### 3. Delete User Data

**Linux:**
```bash
rm -rf ~/.menace-modkit/
rm -rf ~/.config/MenaceModkit/
```

**Windows:**
```
%USERPROFILE%\.menace-modkit\
%APPDATA%\MenaceModkit\
```

**macOS:**
```bash
rm -rf ~/.menace-modkit/
rm -rf ~/Library/Application\ Support/MenaceModkit/
```

## What Each Directory Contains

| Location | Contents |
|----------|----------|
| `~/.menace-modkit/components/` | Downloaded MelonLoader, DataExtractor, etc. |
| `~/.menace-modkit/ui-state.json` | UI state (window positions, etc.) |
| `~/.config/MenaceModkit/settings.json` | App settings (game path, preferences) |
| `{Game}/MelonLoader/` | Mod loader installation |
| `{Game}/Mods/` | Your deployed mods |
| `{Game}/UserData/` | Extracted game data, saves, cache |

## Reinstalling

After uninstalling, you can reinstall by:
1. Downloading a fresh copy of the Modkit
2. Running the Setup wizard
3. Re-downloading and deploying your mods
