# Architecture

This document covers the technical architecture of the Menace Modkit — a modding toolkit for Unity IL2CPP games.

## System Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              DESIGN TIME                                     │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                     Menace.Modkit.App (Avalonia)                    │    │
│  │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌──────────────┐  │    │
│  │  │Stats Editor │ │Asset Browser│ │ Code Editor │ │Modpack Manager│ │    │
│  │  └──────┬──────┘ └──────┬──────┘ └──────┬──────┘ └──────┬───────┘  │    │
│  │         │               │               │               │          │    │
│  │         v               v               v               v          │    │
│  │  ┌─────────────────────────────────────────────────────────────┐   │    │
│  │  │                    Staging Directory                        │   │    │
│  │  │   modpacks/MyMod/modpack.json, stats/, assets/, src/        │   │    │
│  │  └──────────────────────────┬──────────────────────────────────┘   │    │
│  └─────────────────────────────┼──────────────────────────────────────┘    │
│                                │                                            │
│                                v                                            │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                       DeployManager                                  │   │
│  │  ┌──────────────┐ ┌────────────────┐ ┌───────────────────────────┐  │   │
│  │  │BundleCompiler│ │TextureBundler  │ │  CompilationService       │  │   │
│  │  │ (patches →   │ │ (images →      │ │  (C# sources → DLL via    │  │   │
│  │  │  .bundle)    │ │  texture dir)  │ │   Roslyn)                 │  │   │
│  │  └──────────────┘ └────────────────┘ └───────────────────────────┘  │   │
│  └──────────────────────────────┬──────────────────────────────────────┘   │
│                                 │                                           │
│                                 v                                           │
│                    Game/Mods/ folder (deployed)                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                               RUNTIME                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                          Game Process                                │    │
│  │  ┌─────────────┐    ┌────────────────────────────────────────────┐  │    │
│  │  │ MelonLoader │───>│           Menace.ModpackLoader             │  │    │
│  │  └─────────────┘    │  ┌─────────────────────────────────────┐   │  │    │
│  │                     │  │              SDK                     │   │  │    │
│  │                     │  │  GameType, GameObj, GameQuery,       │   │  │    │
│  │                     │  │  GameState, ModError, DevConsole,    │   │  │    │
│  │                     │  │  BattleLog, REPL                     │   │  │    │
│  │                     │  └─────────────────────────────────────┘   │  │    │
│  │                     │  ┌─────────────────────────────────────┐   │  │    │
│  │                     │  │         Loaded Modpacks              │   │  │    │
│  │                     │  │  - Template patches (IL2CPP reflect) │   │  │    │
│  │                     │  │  - Asset bundles                     │   │  │    │
│  │                     │  │  - Plugin DLLs (IModpackPlugin)      │   │  │    │
│  │                     │  └─────────────────────────────────────┘   │  │    │
│  │                     └────────────────────────────────────────────┘  │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                      TwitchServer (optional)                         │    │
│  │  Standalone .NET 8 process on localhost:7654                         │    │
│  │  - Twitch IRC connection                                             │    │
│  │  - Draft pool management                                             │    │
│  │  - HTTP API for modpack polling                                      │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Component Responsibilities

### Menace.Modkit.App

Cross-platform Avalonia desktop application for mod development.

| Component | Responsibility |
|-----------|----------------|
| **StatsEditorView** | Template browsing/editing with vanilla comparison |
| **AssetBrowserView** | Asset exploration and replacement setup |
| **CodeEditorView** | C# editing with syntax highlighting |
| **ModpacksView** | Modpack CRUD, deployment, export |
| **SettingsView** | App configuration, paths, game detection |
| **DeployManager** | Orchestrates compilation and file deployment |
| **CompilationService** | Roslyn-based C# compilation with security scanning |
| **ModpackManager** | Manifest loading, staging directory management |
| **SchemaService** | Loads extracted IL2CPP type metadata |

### Menace.ModpackLoader

MelonLoader mod that runs inside the game process.

| Component | Responsibility |
|-----------|----------------|
| **ModpackLoaderMod** | Entry point, modpack discovery, lifecycle |
| **TemplateInjection** | IL2CPP reflection for template patching |
| **BundleLoader** | Asset bundle loading and registration |
| **DllLoader** | Plugin DLL loading and lifecycle |
| **AssetReplacer** | Runtime texture/asset swapping |

### SDK (in ModpackLoader)

Runtime API for mod developers.

| Component | Responsibility |
|-----------|----------------|
| **GameType** | IL2CPP type lookup and caching |
| **GameObj** | Safe wrapper for IL2CPP object pointers |
| **GameQuery** | FindObjectsOfType with caching |
| **GameState** | Scene tracking, delayed execution |
| **GamePatch** | Harmony patch helpers |
| **ModError** | Centralized error reporting with rate limiting |
| **DevConsole** | IMGUI overlay, panel system, commands |
| **BattleLog** | Combat event capture via Harmony |
| **REPL** | Roslyn-based C# evaluation |

### TwitchServer

Standalone server for Twitch chat integration.

| Component | Responsibility |
|-----------|----------------|
| **Program.cs** | HTTP endpoints, config loading |
| **TwitchIrcClient** | Raw TCP IRC client |
| **DraftPool** | Thread-safe viewer draft pool |
| **MessageStore** | Per-user chat history |

## Data Flow

### Template Patching

```
1. DataExtractor runs in-game, exports templates to JSON
2. User edits templates in Stats Editor
3. Changes saved to staging modpack as patch JSON
4. DeployManager merges patches across all modpacks
5. BundleCompiler writes merged data to .bundle file
6. At runtime, ModpackLoader loads bundle and patches IL2CPP objects
```

### Asset Replacement

```
1. User selects replacement image in Asset Browser
2. Image copied to modpack assets/ directory
3. TextureBundler compiles to game-compatible format
4. At runtime, AssetReplacer intercepts texture loads
```

### Code Compilation

```
1. User writes C# in Code Editor
2. SecurityScanner checks for dangerous APIs
3. CompilationService invokes Roslyn
4. DLL written to modpack build/ directory
5. At runtime, DllLoader loads DLL and instantiates IModpackPlugin
```

### Twitch Integration

```
1. TwitchServer connects to Twitch IRC
2. Viewers type !draft to join pool
3. Modpack polls /api/status every 3 seconds
4. Streamer clicks dice button or runs twitch.pick
5. Server returns random viewer from pool
6. Modpack writes viewer name to squaddie via IL2CPP
```

## Key Design Decisions

### IL2CPP Compatibility

Unity IL2CPP strips unused code. The SDK uses:
- **Il2CppInterop** proxy types for managed access
- **Raw pointer arithmetic** for fields without proxies
- **OffsetCache** for field offset lookup
- **GUI.\*** instead of GUILayout (layout methods stripped)

### Error Isolation

- Mod errors are caught and reported via `ModError`, never crash the game
- Panel rendering wrapped in try-catch
- Rate limiting prevents error spam (10/sec per mod)
- Deduplication within 5-second windows

### Network Isolation

TwitchServer runs as a separate process to:
- Keep network I/O out of the game's main thread
- Allow the game to run without Twitch features
- Simplify credential handling (config file outside game directory)

### Security

- **SecurityScanner** flags dangerous APIs (file I/O, reflection, process spawning)
- **SecurityStatus** field tracks review state per modpack
- Compiled DLLs are sandboxed by the game's AppDomain

## Directory Structure

```
MenaceAssetPacker/
├── src/
│   ├── Menace.Modkit.App/          # Desktop GUI
│   │   ├── Views/                  # Avalonia views
│   │   ├── ViewModels/             # MVVM view models
│   │   └── Services/               # Business logic
│   ├── Menace.Modkit.Core/         # Shared library
│   ├── Menace.Modkit.Cli/          # CLI tool
│   ├── Menace.ModpackLoader/       # Runtime mod
│   │   └── SDK/                    # Public SDK API
│   └── Menace.DataExtractor/       # Data extraction mod
├── tools/
│   └── TwitchServer/               # Twitch integration server
├── third_party/
│   └── bundled/
│       ├── ModpackLoader/          # Pre-built runtime DLLs
│       └── modpacks/               # Example modpacks
├── examples/                       # Modpack examples
├── tests/                          # Test suites
└── docs/
    └── wiki/                       # SDK documentation
```

## Testing Strategy

| Suite | Coverage |
|-------|----------|
| **Menace.Modkit.Tests** | Manifest parsing, patch merging, conflict detection, security scanning |
| **Menace.ModpackLoader.Tests** | SDK APIs (GameType, GameObj, collections, REPL compilation) |

Tests use xUnit and run without the game installed. IL2CPP-specific behavior is tested via mocks.
