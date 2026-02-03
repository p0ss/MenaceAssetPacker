# Menace Modkit Redistributables

This document describes the redistributable packages built for the Menace Modkit.

## Building Redistributables

Run the build script:
```bash
./build-redistributables.sh
```

This creates self-contained, single-file executables for Windows and Linux in the `dist/` directory.

## Packages

### GUI Application (Menace.Modkit.App)

**Linux x64:**
- Archive: `dist/menace-modkit-gui-linux-x64.tar.gz`
- Extract and run: `./Menace.Modkit.App`

**Windows x64:**
- Archive: `dist/menace-modkit-gui-win-x64.zip`
- Extract and run: `Menace.Modkit.App.exe`

**Features:**
- Asset Browser (view and search game assets)
- Stats Editor (modify game statistics)
- Settings panel with:
  - Unity version detection
  - Typetree cache building

### CLI Tool (Menace.Modkit.Cli)

**Linux x64:**
- Archive: `dist/menace-modkit-cli-linux-x64.tar.gz`
- Extract and run: `./Menace.Modkit.Cli --help`

**Windows x64:**
- Archive: `dist/menace-modkit-cli-win-x64.zip`
- Extract and run: `Menace.Modkit.Cli.exe --help`

**Commands:**
```bash
# Extract typetrees from game installation
Menace.Modkit.Cli cache-typetrees --source <game-path> --output <output-path>

# Example
Menace.Modkit.Cli cache-typetrees --source "/path/to/Menace" --output "./typetrees"
```

## Distribution

All packages are:
- **Self-contained**: No .NET runtime installation required
- **Single-file**: All dependencies embedded in one executable
- **Cross-platform**: Native builds for Linux and Windows

### Package Sizes

| Package | Compressed | Extracted |
|---------|-----------|-----------|
| GUI Linux x64 | ~40 MB | ~93 MB |
| GUI Windows x64 | ~42 MB | ~98 MB |
| CLI Linux x64 | ~31 MB | ~72 MB |
| CLI Windows x64 | ~31 MB | ~72 MB |

## Extracting Typetrees

**Important:** The Menace demo ships with stripped typetrees in its release build. For best results extracting typetrees, use the **Windows build** in a VM.

See [WINDOWS-TYPETREE-EXTRACTION.md](WINDOWS-TYPETREE-EXTRACTION.md) for detailed instructions.

Quick reference:
```powershell
# On Windows
.\Menace.Modkit.Cli.exe cache-typetrees --source "C:\Games\Menace" --output "C:\Typetrees"
```

The extracted typetrees can then be transferred back to Linux for use in your modding workflow.

## System Requirements

### Linux
- x64 processor
- glibc 2.31 or later
- X11 or Wayland (for GUI)

### Windows
- Windows 10 or later (x64)
- No additional dependencies

## Notes

- Built with .NET 9.0 RC
- Uses AssetRipper.IO.Files for Unity asset parsing
- GUI built with Avalonia UI framework
- CLI uses Spectre.Console for rich terminal output

## Detected Unity Version

The tool detects Unity version `6000.0.56f1` from the Menace demo. This corresponds to Unity 2023.3.x (Unity changed their versioning scheme).

## Support

For issues or questions, refer to the main [README.md](README.md) and [ProjectPlan.md](ProjectPlan.md).
