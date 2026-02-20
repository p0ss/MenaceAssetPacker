# Tests, Build Scripts, and CI - Maintainer Documentation

## Overview

This document covers the testing infrastructure, build/deployment scripts, and CI workflows.

## Test Structure

### Menace.ModpackLoader.Tests

**Location:** `tests/Menace.ModpackLoader.Tests/`

**Configuration:**
- Target: .NET 8.0
- Framework: xUnit 2.5.3
- Roslyn: Microsoft.CodeAnalysis.CSharp 4.12.0

**Test Organization:**
```
Menace.ModpackLoader.Tests/
├── SDK/
│   ├── GameStateTests.cs          # 11 tests - lifecycle, scheduling
│   ├── GameTypeTests.cs
│   ├── GameObjTests.cs
│   └── Repl/
│       ├── ConsoleEvaluatorTests.cs
│       └── RuntimeReferenceResolverTests.cs
├── Helpers/
│   └── TemporaryDirectory.cs
└── stubs/
    ├── Stubs.MelonLoader/
    └── Stubs.Il2CppInterop/
```

**Key Tests:** `GameStateTests.cs` (85 lines) - Tests runtime state management, frame-based scheduling.

### Menace.Modkit.Tests

**Location:** `tests/Menace.Modkit.Tests/`

**Configuration:**
- Targets: net8.0, net9.0, net10.0 (multi-framework)
- Framework: xUnit 2.5.3
- JSON: Newtonsoft.Json 13.0.3

**Test Organization (Phase-Based):**

| Phase | Test Class | Tests | Purpose |
|-------|-----------|-------|---------|
| 0 | ManifestRoundTripTests | 9 | Serialization, V1→V2 migration |
| 0/1 | DependencyParsingTests | - | Version constraint validation |
| 1 | ConflictDetectionTests | 9 | Field & DLL conflicts |
| 2 | DeployStateRoundTripTests | - | Persistence |
| 4 | SecurityScannerTests | 11 | Pattern detection |
| 5 | PatchMergeTests | 10 | Last-wins merge semantics |
| 0↔6 | RuntimeManifestCompatTests | 11 | JSON contracts |

**Key Test Files:**

- **ManifestRoundTripTests.cs** (215 lines) - Full manifest lifecycle
- **ConflictDetectionTests.cs** (216 lines) - Patch overlap, DLL conflicts
- **PatchMergeTests.cs** (221 lines) - Critical merge transform
- **SecurityScannerTests.cs** (187 lines) - Dangerous pattern detection

### test_schema_generation.py

**Location:** `tests/test_schema_generation.py` (395 lines)

**Framework:** Python unittest

**Test Classes:**
- `TestSchemaGeneration` (8 tests) - Enum/struct/template parsing
- `TestSchemaFromFile` (1 test) - Roundtrip
- `TestSchemaDiff` (3 tests) - Change detection
- `TestValidation` (5 tests) - Coverage, naming, types

## Build Script

### build-redistributables.sh

**Location:** `build-redistributables.sh` (366 lines)

**Purpose:** Multi-platform build orchestration with component versioning.

**Phases:**

1. **Version Generation** (lines 15-81)
   - Extract version from `third_party/versions.json`
   - Generate `src/Shared/ModkitVersion.cs`

2. **DLL Compilation** (lines 91-144)
   - DataExtractor DLL (Release build)
   - ModpackLoader DLL with framework dependencies
   - Handle Roslyn 4.3.0 vs MelonLoader dependency conflict

3. **GUI App** (lines 149-182)
   - Linux x64 publish (self-contained, single-file)
   - Windows x64 publish

4. **Component Archives** (lines 189-248)
   - DataExtractor.zip, ModpackLoader.zip
   - MelonLoader, AssetRipper per platform
   - DevMode.zip, TwitchSquaddies.zip

5. **Checksums & Manifest** (lines 254-289)
   - SHA256 for each component
   - `manifest.json` with checksums, sizes, timestamp

6. **CLI Tool** (lines 297-307)

7. **App Archives** (lines 315-366)

**Critical Technical Details:**

**Dependency Handling (lines 104-127):**
```bash
# Roslyn 4.3.0 requires System.Collections.Immutable 6.0.0
# MelonLoader bundles 9.0.0 of the same assembly
# Solution: Copy different versions from NuGet cache (netstandard2.0)
```

**Output Structure:**
```
dist/
├── DataExtractor/
├── ModpackLoader/
├── gui-{linux,win}-x64/
├── cli-{linux,win}-x64/
├── components/
│   ├── *.zip, *.tar.gz
│   ├── *.sha256
│   └── manifest.json
└── menace-modkit-*.{tar.gz,zip}
```

## GitHub Actions Workflow

### release.yml

**Location:** `.github/workflows/release.yml` (94 lines)

**Trigger:** Tag push `v*`

**Pipeline:**

| Stage | Action |
|-------|--------|
| Checkout | Git checkout with submodules |
| .NET Setup | Setup-dotnet action (.NET 10.0.x) |
| Version Extract | Parse git tag |
| ModkitVersion Gen | Generate version constants |
| GUI Build (Linux) | `dotnet publish` linux-x64 |
| GUI Build (Windows) | `dotnet publish` win-x64 |
| Bundle Files | Copy versions.json, bundled components |
| Create Archives | tar.gz (Linux), ZIP (Windows) |
| Create Release | softprops/action-gh-release@v2 |

**Release Output:**
```yaml
name: "Menace Modkit v${{ steps.version.outputs.VERSION }}"
files:
  - dist/menace-modkit-linux-x64.tar.gz
  - dist/menace-modkit-win-x64.zip
```

## Test Gaps & Issues

### Critical Gaps

| Gap | Impact | Severity |
|-----|--------|----------|
| No CI test execution | Tests not run on PR/merge | HIGH |
| No component release | Archives not uploaded to GitHub | MEDIUM |
| Python tests not in CI | Schema tests not automated | MEDIUM |
| Missing RuntimeCompilerTests | Referenced but not found | HIGH |

### Inconsistencies

| Issue | Details |
|-------|---------|
| Version generation duplication | Both build script and CI generate ModkitVersion.cs |
| Bundled copy management | Multiple locations (third_party/bundled/, third_party/components/) |
| Release strategy mismatch | Build script mentions separate component release, CI doesn't implement |

### TODOs in Related Code

| TODO | File | Category |
|------|------|----------|
| Check m_CachedPtr offset | generated/sdk/GeneratedSDK.cs:41 | SDK Generation |
| Create Texture2D from GLB | GlbBundler.cs:580 | Asset Bundling |
| Open modal for complex types | BulkEditorPanel.axaml.cs:574 | UI/UX |
| Add platform adapters | ModUpdateChecker.cs:91 | Cross-Platform |
| Parse OperationResources | SDK/Roster.cs:234 | Game Logic |

## Diagnostic Tools

### tools/doctor.sh

**Location:** `tools/doctor.sh` (169 lines)

**Checks:**
- .NET Runtime (dotnet CLI, versions)
- Steam / Game (path detection, MelonLoader, assemblies)
- Linux/Proton setup
- Modkit bundle (dependencies)

**Exit Codes:** 0 (pass), 1 (issues found)

## Running Tests Locally

### .NET Tests
```bash
dotnet test Menace.Modkit.sln
```

### Python Tests
```bash
python -m pytest tests/test_schema_generation.py -v
```

## Release Process

### Local Build
```bash
./build-redistributables.sh
```

### GitHub Release
```bash
git tag v27.0.0
git push origin main v27.0.0
# CI automatically creates release from tag
```

## Recommendations

1. **Create CI Test Workflow**
   - Add `.github/workflows/test.yml` for PR/push
   - Execute all test projects
   - Publish coverage reports

2. **Document Version Management**
   - Single source: `third_party/versions.json`
   - Add validation step

3. **Implement Component Release**
   - Extend release.yml to upload component archives
   - Include checksums in release body

4. **Test Coverage Expansion**
   - Create missing RuntimeCompilerTests.cs
   - Add end-to-end tests
   - Security-related integration tests

5. **Document Dependency Conflicts**
   - Create `docs/DEPENDENCY-CONFLICTS.md`
   - Add CI validation for conflicts
