# Development Setup

This document describes how to set up a development environment for MenaceAssetPacker.

## Prerequisites

- **.NET 10 SDK** (or later)
- **MelonLoader 0.7.x** - Required for runtime mod development
- A copy of the game with MelonLoader installed (for building mods that reference game assemblies)

## Environment Variables

The build system uses configurable paths for MelonLoader and game assemblies. You can set these via environment variables or MSBuild properties.

### Environment Variables

| Variable | Description |
|----------|-------------|
| `MELONLOADER_PATH` | Path to MelonLoader installation directory containing `MelonLoader.dll`, `0Harmony.dll`, and `Il2CppInterop.*.dll` |
| `GAME_ASSEMBLIES_PATH` | Path to game's `MelonLoader/Il2CppAssemblies` folder containing Unity and game-specific assemblies |

### Example Setup (Linux/macOS)

```bash
export MELONLOADER_PATH=/path/to/MelonLoader
export GAME_ASSEMBLIES_PATH=/path/to/game/MelonLoader/Il2CppAssemblies
```

### Example Setup (Windows PowerShell)

```powershell
$env:MELONLOADER_PATH = "C:\path\to\MelonLoader"
$env:GAME_ASSEMBLIES_PATH = "C:\path\to\game\MelonLoader\Il2CppAssemblies"
```

### Alternative: MSBuild Properties

You can also pass these as MSBuild properties:

```bash
dotnet build /p:MelonLoaderPath=/path/to/MelonLoader /p:GameAssembliesPath=/path/to/assemblies
```

## Default Paths

If environment variables are not set, the build looks for assemblies in:
- `third_party/MelonLoader/` for MelonLoader DLLs
- `third_party/GameAssemblies/` for game assemblies

### Setting Up Symlinks (Recommended)

The easiest way to set up the build environment is to create symlinks:

```bash
# Linux/macOS
cd third_party
ln -s /path/to/your/MelonLoader MelonLoader
ln -s "/path/to/game/MelonLoader/Il2CppAssemblies" GameAssemblies

# Example with typical Steam paths:
ln -s ~/.steam/steam/steamapps/common/Menace\ Demo/MelonLoader/Il2CppAssemblies GameAssemblies
```

```powershell
# Windows (run as Administrator)
cd third_party
mklink /D MelonLoader C:\path\to\MelonLoader
mklink /D GameAssemblies "C:\path\to\game\MelonLoader\Il2CppAssemblies"
```

These symlinks are gitignored and won't be committed.

## Building

### Full Solution

```bash
dotnet build
```

### Individual Projects

```bash
# GUI Application
dotnet build src/Menace.Modkit.App

# ModpackLoader (runtime mod)
dotnet build src/Menace.ModpackLoader

# DataExtractor (runtime mod)
dotnet build src/Menace.DataExtractor
```

## Running Tests

```bash
dotnet test
```

## Project Structure

```
src/
  Menace.Modkit.App/       # Avalonia GUI application
  Menace.Modkit.Cli/       # CLI tool
  Menace.Modkit.Core/      # Shared models and utilities
  Menace.ModpackLoader/    # MelonLoader mod for loading modpacks at runtime
  Menace.DataExtractor/    # MelonLoader mod for extracting game data
  Shared/                  # Shared code (version, etc.)

third_party/
  bundled/                 # Pre-built binaries bundled with releases
  AssetRipper/             # AssetRipper source (submodule)
  MelonLoader/             # MelonLoader DLLs (not checked in)
  GameAssemblies/          # Game assemblies (not checked in)

tests/
  Menace.ModpackLoader.Tests/  # Unit tests

docs/                      # User documentation
tools/                     # Development and diagnostic tools
```

## Building Redistributables

To build release packages:

```bash
# Slim build (tools downloaded on demand)
./build-redistributables.sh

# Bundled build (includes all tools)
./build-redistributables.sh --bundle
```

Output will be in `dist/`.

## Code Style

- Follow standard C# conventions
- Use file-scoped namespaces
- Nullable reference types are enabled project-wide
- Prefer explicit types over `var` when the type isn't obvious

## Security Considerations

- All path inputs are validated to prevent traversal attacks
- Archive extraction validates entry paths (Zip Slip protection)
- REPL code is sandboxed with blocked dangerous namespaces
- Unverified DLLs require explicit user approval

See [Security Policy](docs/system-guide/SECURITY.md) for the security policy and reporting process.
