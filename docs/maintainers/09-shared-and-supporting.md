# Shared & Supporting Projects - Maintainer Documentation

## Overview

This document covers smaller supporting projects and shared configuration.

## src/Shared

**Purpose:** Centralized version information shared across desktop app and in-game loader.

**Contents:** Single file - `ModkitVersion.cs` (37 lines)

### ModkitVersion.cs

**Location:** `src/Shared/ModkitVersion.cs`

```csharp
// AUTO-GENERATED from third_party/versions.json
// DO NOT EDIT MANUALLY - run build-redistributables.sh

namespace Menace;
public static class ModkitVersion
{
    public const int BuildNumber = 26;
    public const string MelonVersion = "26.0.0";
    public const string Short = "v26";
    public const string AppFull = "Menace Modkit v26";
    public const string LoaderFull = "Menace Modpack Loader v26";
}
```

### Usage Pattern

File is **linked** (not referenced) into projects via .csproj:

**Menace.Modkit.App:**
```xml
<Compile Include="..\Shared\ModkitVersion.cs" Link="Services\ModkitVersion.cs" />
```
- Used in `App.axaml.cs` for startup banner
- Used in `MainWindow.axaml.cs` for window title

**Menace.ModpackLoader:**
```xml
<Compile Include="..\Shared\ModkitVersion.cs" Link="ModkitVersion.cs" />
```
- Used in `[assembly: MelonInfo]` attribute
- Used for console output and Player.log

### Dependency Flow

```
versions.json (third_party/)
    ↓ [build-redistributables.sh generates]
ModkitVersion.cs (src/Shared/)
    ↓ [linked via csproj]
    ├─→ Menace.Modkit.App
    └─→ Menace.ModpackLoader
```

### Known Issue

**Version Mismatch (Current):**
- `ModkitVersion.cs` shows BuildNumber = 26
- `versions.json` shows ModpackLoader = "27.0.0"
- Build script needs to run before next release

## src/Directory.Build.props

**Purpose:** Shared MSBuild configuration for MelonLoader-targeting projects.

**Location:** `src/Directory.Build.props` (22 lines)

### Configuration

```xml
<PropertyGroup>
  <!-- Priority: ENV > CLI > Default -->
  <MelonLoaderPath Condition="'$(MelonLoaderPath)' == '' AND '$(MELONLOADER_PATH)' != ''">
    $(MELONLOADER_PATH)
  </MelonLoaderPath>
  <MelonLoaderPath Condition="'$(MelonLoaderPath)' == ''">
    $(MSBuildThisFileDirectory)../third_party/MelonLoader
  </MelonLoaderPath>

  <GameAssembliesPath Condition="'$(GameAssembliesPath)' == '' AND '$(GAME_ASSEMBLIES_PATH)' != ''">
    $(GAME_ASSEMBLIES_PATH)
  </GameAssembliesPath>
  <GameAssembliesPath Condition="'$(GameAssembliesPath)' == ''">
    $(MSBuildThisFileDirectory)../third_party/GameAssemblies
  </GameAssembliesPath>
</PropertyGroup>
```

### Configuration Precedence

```
1. Command-line:     dotnet build /p:MelonLoaderPath=/custom/path
2. Environment:      export MELONLOADER_PATH=/custom/path
3. Default:          ../third_party/MelonLoader
```

### Dependent Projects

Used by:
- Menace.ModpackLoader.csproj
- Menace.Modkit.Mcp.csproj
- Menace.Modkit.Cli.csproj
- Menace.DataExtractor.csproj

### Benefits

- Single source of truth for dependency paths
- Enables building from custom game installations
- Supports CI/CD with environment variable overrides

## Removed Projects (Historical Reference)

### src/Menace.ReflectionTest (DELETED)

**Status:** Deleted in commit `c68a7d5`

**Purpose:** Test harness for Unity IL2Cpp reflection capabilities.

**Key Testing Approaches:**
1. `Il2CppObjectBase.TryCast<T>()` via reflection
2. UnityEngine.Object base properties
3. Direct Marshal.Read at known offsets
4. Constructor(IntPtr) cast

**Deletion Rationale:** Reflection testing infrastructure became obsolete once SDK stabilized.

### src/Menace.StatModifier (DELETED)

**Status:** Deleted in commit `c68a7d5`

**Purpose:** Example mod demonstrating runtime stat modification via Harmony patches.

**Architecture:**
- MelonLoader mod
- Harmony prefix patches on property getters
- JSON-defined overrides from ModifiedData folder

**Covered Templates:**
- WeaponTemplate (MinRange, Damage, AccuracyBonus, etc.)
- ArmorTemplate (Armor, DamageResistance, etc.)
- AccessoryTemplate
- EntityTemplate (partial)

**Deletion Rationale:** Superseded by SDK runtime scripting (C# + Lua).

## TODOs and Issues

### Version Management

1. **Regenerate ModkitVersion.cs**
   - Run `build-redistributables.sh` before release
   - Update BuildNumber 26 → 27

2. **Consider Automation**
   - Add MSBuild task to auto-generate
   - Parse versions.json at build time

### Directory.Build.props

1. **Documentation**
   - Clarify absolute vs. relative path handling
   - Document where to obtain GameAssemblies

2. **Path Validation**
   - No check if paths actually exist
   - Consider adding validation task

## Summary Table

| Component | Status | Lines | Purpose |
|-----------|--------|-------|---------|
| Shared/ModkitVersion.cs | ACTIVE | 37 | Version constants |
| Directory.Build.props | ACTIVE | 22 | Build path config |
| ReflectionTest | DELETED | 311 | IL2Cpp reflection testing |
| StatModifier | DELETED | 577 | Harmony stat override PoC |

## Maintainer Notes

1. **Before Release:**
   - Run `build-redistributables.sh`
   - Verify ModkitVersion.cs matches versions.json

2. **For New Contributors:**
   - Set `MELONLOADER_PATH` and `GAME_ASSEMBLIES_PATH` environment variables
   - OR place assemblies in `third_party/MelonLoader` and `third_party/GameAssemblies`

3. **For CI/CD:**
   - Use `/p:MelonLoaderPath=` and `/p:GameAssembliesPath=` MSBuild properties
