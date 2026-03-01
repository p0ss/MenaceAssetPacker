# Menace Modkit - Maintainer Documentation

This directory contains detailed architectural documentation for maintainers and contributors working on the Menace Modkit codebase.

## Documentation Index

| Document | Component | Purpose |
|----------|-----------|---------|
| [01-core.md](01-core.md) | Menace.Modkit.Core | Foundational library for asset extraction and bundling |
| [02-app.md](02-app.md) | Menace.Modkit.App | Desktop GUI application (Avalonia/ReactiveUI) |
| [03-cli.md](03-cli.md) | Menace.Modkit.Cli | Command-line interface |
| [04-modpack-loader.md](04-modpack-loader.md) | Menace.ModpackLoader | Runtime injection framework (MelonLoader mod) |
| [05-data-extractor.md](05-data-extractor.md) | Menace.DataExtractor | Game data extraction mod |
| [06-mcp.md](06-mcp.md) | Menace.Modkit.Mcp | Model Context Protocol server for AI integration |
| [07-tools.md](07-tools.md) | tools/ | Python tools, diagnostics, and utilities |
| [08-tests-and-build.md](08-tests-and-build.md) | tests/, CI | Testing infrastructure and build/release process |
| [09-shared-and-supporting.md](09-shared-and-supporting.md) | src/Shared, etc. | Shared configuration and supporting projects |
| [10-release-channels.md](10-release-channels.md) | Release system | Stable/Beta channel system and release workflow |

## Quick Reference

### Project Dependencies

```
Menace.Modkit.Core
    ↑
    ├── Menace.Modkit.App (references Core)
    ├── Menace.Modkit.Cli (references Core)
    └── Menace.Modkit.Mcp (references Core via App)

Menace.ModpackLoader (runtime, references game assemblies)
    ↑
    └── Menace.DataExtractor (extraction, runs in-game)
```

### Key File Locations

| Purpose | Location |
|---------|----------|
| Stable version manifest | `third_party/versions.json` |
| Beta version manifest | `third_party/versions-beta.json` |
| Shared version constants | `src/Shared/ModkitVersion.cs` |
| Build configuration | `src/Directory.Build.props` |
| Build script | `build-redistributables.sh` |
| CI workflow | `.github/workflows/release.yml` |
| Schema definition | `generated/schema.json` |

### Common Maintenance Tasks

**Update version:**
```bash
# Edit third_party/versions.json, then:
./build-redistributables.sh
```

**Run tests:**
```bash
dotnet test Menace.Modkit.sln
python -m pytest tests/test_schema_generation.py -v
```

**Check system dependencies:**
```bash
./tools/doctor.sh  # Linux
powershell -ExecutionPolicy Bypass -File tools/doctor.ps1  # Windows
```

**Release (via workflow dispatch - recommended):**
```
Actions → Release → Run workflow
  - version: 31.0.3 (beta) or 32.0.0 (stable)
  - channel: beta or stable
```

**Release (via git tag - defaults to stable):**
```bash
git tag v32.0.0
git push origin v32.0.0
```

See [10-release-channels.md](10-release-channels.md) for full details.

## Known Issues Across Codebase

### Critical TODOs

| Location | Issue |
|----------|-------|
| GlbBundler.cs:580 | Texture2D creation from GLB materials not implemented |
| GlbBundler.cs:712 | Full AssetsTools.NET asset creation incomplete |
| BulkEditorPanel.axaml.cs:574 | Complex type editor modal missing |

### Architectural Notes

1. **Runtime cloning/replacement disabled** - Production expects compiled bundles from Phase 5 builder
2. **IL2CPP offsets fragile** - Hardcoded offsets break on game updates
3. **No CI test execution** - Tests not run automatically on PR/merge
4. **Version mismatch** - ModkitVersion.cs may be out of sync with versions.json

### Dependency Conflicts

Roslyn 4.3.0 requires System.Collections.Immutable 6.0.0, but MelonLoader bundles 9.0.0. The build script handles this by copying specific versions from NuGet cache.

## API Surface Between Components

### Core → App/CLI/MCP

```csharp
// Core services (via DI)
ITypetreeCacheBuilder.BuildAsync(TypetreeCacheRequest)
IUnityVersionDetector.DetectVersion(string gamePath)
BundleCompiler.CompileDataPatchBundleAsync(...)
ExtractionOrchestrator.PlanExtractionAsync()
```

### App → MCP

```csharp
// Services exposed to MCP
ModpackManager  // Modpack CRUD
CompilationService  // C# compilation
DeployManager  // Deployment orchestration
SecurityScanner  // Code analysis
```

### ModpackLoader → Game

```csharp
// Plugin interface
IModpackPlugin {
    OnInitialize(logger, harmony)
    OnSceneLoaded(buildIndex, sceneName)
    OnUpdate()  // optional
    OnGUI()  // optional
}
```

### MCP → External

```
HTTP Endpoints:
- Game server: localhost:7655 (requires game running)
- UI server: localhost:21421 (requires app running)

MCP Resources:
- modkit://modpacks/*
- modkit://templates/*
- modkit://schema/*
```

## Contributing

When making changes:

1. **Update tests** for any logic changes
2. **Update this documentation** for architectural changes
3. **Run `validate_extraction.py`** after schema changes
4. **Run `doctor.sh`** before testing runtime changes
5. **Check `diff_schemas.py`** when updating for new game versions
