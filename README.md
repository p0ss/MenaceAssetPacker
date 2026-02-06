# Menace Modkit

A modding toolkit for [Menace](https://store.steampowered.com/app/2546040/Menace/) (Unity IL2CPP). Provides a desktop GUI for creating, editing, and deploying modpacks — including stat tweaks, asset replacements, texture swaps, and custom C# code — without touching game files directly.

## What It Does

- **Stats Editor** — Browse and edit game templates (weapons, armor, entities, accessories) with side-by-side vanilla comparison
- **Asset Browser** — Browse extracted game assets (textures, meshes, audio, prefabs) and set up replacements
- **Code Editor** — Write C# mod code with in-app compilation, security scanning, and DLL packaging
- **Modpack Management** — Create, version, and organize modpacks with dependency tracking and load ordering
- **One-Click Deploy** — Compile and deploy modpacks to the game's Mods/ folder, with automatic bundle generation
- **Conflict Detection** — Identifies overlapping modifications across modpacks before deployment
- **Runtime SDK** — In-game API for IL2CPP type resolution, object access, collections, error handling, and a Roslyn REPL ([docs](docs/wiki/index.md))
- **Dev Console** — In-game IMGUI overlay with tabbed panels for combat logging, error inspection, live watches, and C# evaluation
- **Twitch Integration** — Standalone server + modpack for viewer-controlled squaddies via chat commands

## Getting Started

### Requirements

- .NET 10+ SDK (for the desktop app)
- Menace or Menace Demo installed via Steam
- Linux or Windows

### Quick Start

```bash
# Build the desktop app
dotnet build src/Menace.Modkit.App

# Run it
dotnet run --project src/Menace.Modkit.App
```

On first launch the app will:
1. Detect your game installation
2. Install MelonLoader (the IL2CPP mod framework) if needed
3. Deploy the DataExtractor mod to pull vanilla template data from the game
4. Prompt you to launch the game once so templates can be extracted

After that, the Stats Editor, Asset Browser, and Code Editor are all functional.

### Building Redistributables

```bash
# Produces self-contained builds for Linux and Windows under dist/
./build-redistributables.sh
```

This creates:
- `dist/gui-linux-x64/` — Linux GUI with bundled dependencies
- `dist/gui-win-x64/` — Windows GUI with bundled dependencies

## Projects

| Project | Description |
|---------|-------------|
| **Menace.Modkit.App** | Avalonia desktop GUI — stats editor, asset browser, code editor, modpack manager |
| **Menace.Modkit.Core** | Shared library — asset bundle compilation, type trees, patch merging |
| **Menace.Modkit.Cli** | Command-line tool for building typetree caches from game installations |
| **Menace.ModpackLoader** | MelonLoader runtime mod — loads modpacks, provides SDK and DevConsole |
| **Menace.DataExtractor** | MelonLoader mod — extracts game template data and IL2CPP metadata to JSON |
| **TwitchServer** | Standalone HTTP server for Twitch chat integration (tools/TwitchServer) |
| **Menace.Modkit.Tests** | Test suite (xUnit) — manifest roundtrips, patch merging, conflict detection, security scanning |
| **Menace.ModpackLoader.Tests** | Test suite (xUnit) — SDK API coverage (GameType, GameObj, GameState, ModError, collections, REPL compiler) |

Example modpacks in `third_party/bundled/modpacks/`:
- **DevMode-modpack** — In-game dev tools (unit spawning, god mode, entity deletion)
- **CombinedArms-modpack** — AI coordination (focus fire, formations, sequencing)
- **TwitchSquaddies-modpack** — Twitch viewer integration for squaddie control

## Modpack Structure

A modpack is a directory with a `modpack.json` manifest:

```
MyModpack/
  modpack.json          # Manifest (name, author, version, load order, dependencies)
  stats/                # Template data patches (JSON)
    WeaponTemplate.json
    EntityTemplate.json
  assets/               # Asset replacements (textures, etc.)
    textures/
  src/                  # C# source files (compiled to DLL on deploy)
    MyMod.cs
  build/                # Compiled output (generated)
    MyModpack.dll
```

### Manifest Fields

```json
{
  "manifestVersion": 2,
  "name": "My Modpack",
  "version": "1.0.0",
  "author": "Modder",
  "description": "What this modpack changes",
  "loadOrder": 100,
  "dependencies": ["SomeOtherMod>=1.0"],
  "patches": { },
  "assets": { },
  "code": { "sources": ["src/MyMod.cs"], "references": [], "prebuiltDlls": [] },
  "bundles": [],
  "securityStatus": "Unreviewed"
}
```

Patches use a `templateType -> instanceName -> fieldName -> value` structure. Lower `loadOrder` values load first; conflicts resolve last-wins.

## How Deployment Works

```
Staging modpacks (in app)
    |
    v
DeployManager merges patches across all active modpacks
    |
    +--> BundleCompiler: patches -> UnityFS .bundle file
    +--> TextureBundler: images -> textures/ dir + manifest
    +--> CompilationService: .cs sources -> DLL
    |
    v
Deployed to game's Mods/ folder with runtime manifest
    |
    v
Game launches with MelonLoader
    |
    v
ModpackLoader discovers modpack.json files
    +--> Loads .bundle files via AssetBundle.LoadFromFile()
    +--> Applies template patches via IL2CPP reflection
    +--> Loads compiled DLLs
```

The app tracks deployment state so it can clean up old files and detect when staging modpacks have changed since the last deploy.

## Architecture

See [ARCHITECTURE.md](ARCHITECTURE.md) for the full technical breakdown including system diagrams, data flow, and component responsibilities.

Key libraries:
- **Avalonia** — Cross-platform desktop UI
- **AssetsTools.NET** — Unity asset reading/writing and bundle compilation
- **Roslyn** — C# compilation for mod code
- **MelonLoader** — IL2CPP mod injection at runtime
- **Harmony** — Runtime method patching

## Dev Console

The ModpackLoader includes an in-game developer console (toggle with backtick `~`). It provides:

| Panel | Purpose |
|-------|---------|
| **Battle Log** | Combat events with filtering (hits, misses, suppression, morale, deaths) |
| **Log** | Merged error and log messages with severity filtering |
| **Console** | Command line with SDK commands + C# REPL evaluation |
| **Inspector** | Property viewer for game objects |
| **Watch** | Live expression monitoring |

Built-in commands include `find <type>`, `inspect <type> <name>`, `templates <type>`, `scene`, and `errors`. Unknown commands fall back to Roslyn C# evaluation when available.

See [docs/wiki/api/dev-console.md](docs/wiki/api/dev-console.md) for full documentation.

## Twitch Integration

The **TwitchSquaddies** system lets Twitch viewers control squaddies in-game:

1. **TwitchServer** (`tools/TwitchServer/`) — Standalone .NET 8 server that connects to Twitch IRC and exposes a local HTTP API on port 7654
2. **TwitchSquaddies modpack** — Polls the server, adds a DevConsole panel, and provides commands to assign viewers to squaddies

### Quick Start

```bash
# Start the Twitch server (first run creates config.json template)
cd tools/TwitchServer
dotnet run

# Edit config.json with your channel and OAuth token
# Get a token at https://twitchapps.com/tmi/

# Launch the game with the TwitchSquaddies modpack deployed
```

Viewers type `!draft` in chat to enter the pool. Use the dice button in the Squaddies panel or `twitch.pick <id>` to assign a random viewer to a squaddie.

See [third_party/bundled/modpacks/TwitchSquaddies-modpack/README.md](third_party/bundled/modpacks/TwitchSquaddies-modpack/README.md) for full setup and API documentation.

## Running Tests

```bash
# App tests — manifests, patch merging, conflict detection, security scanning
dotnet test tests/Menace.Modkit.Tests

# SDK tests — GameType, GameObj, GameState, ModError, collections, REPL compiler
dotnet test tests/Menace.ModpackLoader.Tests
```

## License

This project is licensed under the [GNU General Public License v3.0](LICENSE).
