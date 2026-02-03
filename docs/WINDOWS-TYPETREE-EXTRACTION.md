# Windows Typetree Extraction Guide

This guide explains how to extract Unity typetrees from the Menace game using a Windows VM. Typetrees are required for modifying game data and scripts.

## Why Windows?

The Menace demo ships with stripped typetrees (`HasTypeTree: False` in Unity asset files). This is standard for release builds to reduce file size. However, AssetRipper can still extract typetree information on Windows by combining:
1. The asset file metadata
2. Unity's built-in class database (embedded in AssetRipper)

While the logic is cross-platform, you may get better results on Windows for Unity-specific operations.

## Prerequisites

### On Linux (Host)
- Build the redistributables: `./build-redistributables.sh`
- Copy the Windows build to your VM:
  ```bash
  # Extract the Windows CLI build
  unzip dist/menace-modkit-cli-win-x64.zip -d menace-modkit-windows
  ```

### On Windows (VM)
- Copy the extracted `menace-modkit-windows` folder to your Windows VM
- Copy your Menace game installation directory to the VM (or use a shared folder)

## Extraction Steps

### 1. Prepare Directories on Windows

```powershell
# Example paths - adjust as needed
$MENACE_INSTALL = "C:\Games\Menace"
$OUTPUT_DIR = "C:\MenaceTypetrees"

# Create output directory
New-Item -ItemType Directory -Force -Path $OUTPUT_DIR
```

### 2. Run the Typetree Extraction

Navigate to the folder containing `Menace.Modkit.Cli.exe`:

```powershell
cd C:\Path\To\cli-win-x64

# Run the extraction
.\Menace.Modkit.Cli.exe cache-typetrees --source $MENACE_INSTALL --output $OUTPUT_DIR
```

Or as a single command:
```powershell
.\Menace.Modkit.Cli.exe cache-typetrees --source "C:\Games\Menace" --output "C:\MenaceTypetrees"
```

### 3. Verify the Output

After extraction completes, check the output directory:
```powershell
ls $OUTPUT_DIR
```

You should see:
- `typetree-cache.json` - The manifest file listing all extracted typetrees
- `typetrees/` - Directory containing the extracted `.json` files

Example manifest structure:
```json
{
  "GameVersion": "demo",
  "UnityVersion": "6000.0.56f1",
  "SourcePath": "C:\\Games\\Menace",
  "CreatedAtUtc": "2025-10-07T23:45:12Z",
  "Files": [
    {
      "Source": "Menace_Data/globalgamemanagers",
      "SerializedFile": "globalgamemanagers",
      "UnityVersion": "6000.0.56f1",
      "TypeCount": 42,
      "Output": "typetrees/globalgamemanagers.json"
    }
  ]
}
```

### 4. Transfer Back to Linux

Copy the entire output directory back to your Linux host:

```bash
# On Linux, from the VM shared folder or via scp
cp -r /path/to/vm/share/MenaceTypetrees ~/.config/MenaceModkit/Typetrees
```

Or use the GUI app's configured location:
```bash
# Default location for the GUI app
cp -r /path/to/vm/share/MenaceTypetrees ~/.local/share/MenaceModkit/Typetrees
```

## Using the GUI Alternative

If you prefer, you can also use the GUI build on Windows:

1. Extract `dist/menace-modkit-gui-win-x64.zip`
2. Run `Menace.Modkit.App.exe`
3. Go to Settings tab
4. Set "Menace Install Path" to your game directory
5. Click "Detect Unity Version" (should show 6000.0.56f1)
6. Click "Build Typetree Cache"
7. The typetrees will be extracted to `%APPDATA%\MenaceModkit\Typetrees`

## Troubleshooting

### "No typetree files extracted"

Check the console output. If you see `HasTypeTree: False` for all files, this is expected behavior for release builds. The extraction should still work on Windows even with stripped typetrees, as AssetRipper uses Unity's class database as a fallback.

### "Could not detect Unity version"

Make sure the path points to the root game directory containing:
- `Menace_Data/` folder
- `Menace.exe` (or the game executable)

### Permission Errors

Run the command prompt or PowerShell as Administrator if you encounter permission issues.

## Next Steps

Once you have the typetrees extracted:
1. Import them into your modding workflow
2. Use them to understand Unity object structures
3. Modify game stats and data based on the typetree definitions

The typetree JSON files contain the complete structure of Unity serialized objects, including:
- Field names and types
- Byte sizes and offsets
- Version information
- Script type indices

This metadata is essential for creating mods that modify game data without reverse engineering the binary formats manually.
