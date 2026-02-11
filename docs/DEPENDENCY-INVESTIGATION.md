# Dependency Investigation Notes

This document records the findings from investigating assembly loading issues with MelonLoader 0.7.2 and the Menace Modkit.

## Environment

- **MelonLoader**: 0.7.2 (net6 runtime for Il2Cpp games)
- **ModpackLoader target**: net6.0
- **Modkit App target**: net10.0
- **Game runs under**: Wine/Proton on Linux

## Root Cause: Assembly Version Conflicts

### The Problem

`FileLoadException: Could not load file or assembly 'System.Collections.Immutable, Version=9.0.0.0'`

This occurred even when:
1. The file existed in UserLibs
2. The assembly version (9.0.0.0) matched
3. The PublicKeyToken (b03f5f7f11d50a3a) matched

### The Discovery

MelonLoader bundles its own `System.Collections.Immutable.dll` in `Dependencies/MonoBleedingEdgePatches/`:
- **MelonLoader's version**: 259,376 bytes, hash `7a3d21b9d8868d3f2ebae088c0ea5b05`
- **NuGet 9.0.0 netstandard2.0**: 259,360 bytes, hash `f707e8dd2a3039762d4e2293fadb5d54`

Both have:
- AssemblyVersion: 9.0.0.0
- PublicKeyToken: b03f5f7f11d50a3a

But they are **different builds** (16 bytes difference, different hashes). When MelonLoader loads its version first, subsequent attempts to load our version fail due to assembly identity conflicts.

### Solution Attempt 1: Ship MelonLoader's Exact Assemblies

**Hypothesis**: If we copy MelonLoader's exact assemblies (same bytes) to UserLibs, they should load without conflict.

**What we did**: Modified `build-redistributables.sh` to copy assemblies from `MelonLoader/Dependencies/MonoBleedingEdgePatches/` instead of NuGet:
- System.Collections.Immutable.dll
- System.Memory.dll
- System.Buffers.dll
- System.Runtime.CompilerServices.Unsafe.dll (used the 6.0 version)

**Result**: Mods started loading! But REPL initialization still failed with FileLoadException.

**Why it partially worked**: This got mods loading because the assembly versions matched. But...

### Solution Attempt 2: Don't Deploy MelonLoader-Provided Assemblies

**New hypothesis**: Even identical binaries cause conflicts when loaded from different paths. The assembly identity includes the load location.

**Analysis** (from parallel investigation):
- MelonLoader loads its copy from `Dependencies/MonoBleedingEdgePatches/` at startup
- When our code triggers a load from `UserLibs/`, .NET sees a duplicate strong-named assembly
- Even with identical bytes, the second load is rejected

**The fix**: Remove these from our build output entirely so they're never deployed to UserLibs:
- System.Collections.Immutable.dll
- System.Memory.dll
- System.Buffers.dll
- System.Runtime.CompilerServices.Unsafe.dll

The runtime will use MelonLoader's already-loaded copies instead of trying to load duplicates.

**Result**: FileNotFoundException! MelonLoader's resolver couldn't find the assemblies at all.

```
Could not load file or assembly 'System.Collections.Immutable, Version=9.0.0.0'. File not found.
Could not load file or assembly 'System.Reflection.Metadata, Version=9.0.0.0'. Could not find or load a specific file. (0x80131621)
```

**Why it failed**: MonoBleedingEdgePatches is NOT in MelonLoader's resolver search path. Those assemblies are loaded by a different bootstrap mechanism. When Roslyn asks for them, the resolver searches UserLibs/Mods/etc but not MonoBleedingEdgePatches, so it can't find them.

### Solution Attempt 3: Ship MelonLoader's Exact Copies (Revisited)

**Revised hypothesis**: Solution Attempt 1 actually worked for mods. The REPL failure was a separate issue - we were using the wrong System.Runtime.CompilerServices.Unsafe version (the older one, not the 6.0 one).

**What we tried**: Same as Attempt 1, but explicitly use `System.Runtime.CompilerServices.Unsafe.6.0.dll` from MelonLoader (renamed to remove the version suffix).

**Result**: Still FileLoadException! Even with identical binaries (verified via MD5 hash), MelonLoader fails to load them from UserLibs.

**Analysis**: The problem isn't the binary content - it's the **load order**. MelonLoader's bootstrap loads assemblies from MonoBleedingEdgePatches early at startup. When MelonLoader then scans UserLibs and tries to load the same assemblies, .NET rejects them as duplicates (same strong name, different load path).

From the log, we can see which assemblies succeed vs fail:
- **Succeed**: System.Memory, System.Buffers, System.Numerics.Vectors, System.Runtime.CompilerServices.Unsafe
- **Fail**: System.Collections.Immutable, System.Reflection.Metadata, System.Text.Encoding.CodePages

The failed ones are already loaded by MelonLoader's bootstrap or the .NET 6 runtime before UserLibs is scanned.

### Solution Attempt 4: Don't Ship Conflicting Assemblies

**Hypothesis**: If assemblies are already loaded by MelonLoader's bootstrap, we shouldn't ship them at all. When Roslyn needs them, the runtime should return the already-loaded copies.

**What we're doing**:
1. Remove from UserLibs deployment:
   - System.Collections.Immutable.dll (loaded by MelonLoader bootstrap)
   - System.Reflection.Metadata.dll (fails because its dependency S.C.I. can't load)
   - System.Text.Encoding.CodePages.dll (loaded by .NET 6 runtime)

2. Keep in UserLibs:
   - System.Memory.dll (not pre-loaded, loads successfully)
   - System.Buffers.dll (not pre-loaded, loads successfully)
   - System.Runtime.CompilerServices.Unsafe.dll (not pre-loaded, loads successfully)
   - System.Numerics.Vectors.dll (not pre-loaded, loads successfully)

**Risk**: Solution Attempt 2 showed FileNotFoundException when these assemblies weren't in UserLibs. However, that was different - we removed ALL framework assemblies. This time we're only removing the ones that cause conflicts, and hoping the runtime can resolve them from what's already loaded.

**Result**: FileNotFoundException! Same as Attempt 2 - MelonLoader's resolver doesn't check already-loaded assemblies, it only searches disk paths (UserLibs, Mods, etc.).

### Solution Attempt 5: Downgrade Roslyn to Avoid Version Conflict

**Key discovery**: Comparing with v16 of the modkit (which worked), we found:
- v16 used Roslyn 4.3.0 → depends on System.Collections.Immutable **6.0.0**
- Current uses Roslyn 4.14.0 → depends on System.Collections.Immutable **9.0.0**

MelonLoader 0.7.2 bundles System.Collections.Immutable **9.0.0** in MonoBleedingEdgePatches.

The problem isn't that we need to match versions - it's that **matching versions causes conflicts** because .NET sees two strong-named assemblies with the same identity from different paths.

**The fix**: Use a Roslyn version that depends on a DIFFERENT System.Collections.Immutable version than what MelonLoader bundles. Different versions can coexist; same versions from different paths cause FileLoadException.

**What we did**:
1. Downgrade Microsoft.CodeAnalysis.CSharp from 4.14.0 to 4.3.0
2. This pulls in System.Collections.Immutable 6.0.0 instead of 9.0.0
3. Ship S.C.I 6.0.0 and S.R.M 6.0.0 to UserLibs (copied from NuGet cache, netstandard2.0 builds)
4. MelonLoader's 9.0.0 stays in MonoBleedingEdgePatches - no conflict

**Result**: ✅ SUCCESS! REPL initializes correctly. Both assembly versions coexist.

## Dependency Chain

### Roslyn 4.3.0 (working) vs 4.14.0 (broken)

| Roslyn Version | S.C.Immutable | S.R.Metadata | Status |
|----------------|---------------|--------------|--------|
| 4.3.0          | 6.0.0         | 6.0.0        | ✓ Works (different from MelonLoader's 9.0.0) |
| 4.14.0         | 9.0.0         | 9.0.0        | ✗ Conflicts with MelonLoader's 9.0.0 |

### Roslyn (Microsoft.CodeAnalysis.CSharp 4.3.0) requires:
- System.Collections.Immutable 6.0.0
- System.Reflection.Metadata 6.0.0
- System.Text.Encoding.CodePages 6.0.0

### System.Collections.Immutable 6.0.0 (netstandard2.0 build) requires:
- System.Memory 4.5.5
- System.Buffers 4.5.1
- System.Numerics.Vectors 4.5.0
- System.Runtime.CompilerServices.Unsafe 6.0.0

### Why .NET 6 doesn't copy these automatically:
For .NET 6, System.Memory, System.Buffers, etc. are "inbox" framework assemblies - they're part of the runtime and don't need to be shipped separately. But MelonLoader's assembly resolver can't find them in the standard .NET 6 runtime paths, so they must be explicitly included.

## MelonLoader Assembly Resolution

MelonLoader looks for mod dependencies in:
1. MelonLoader's own folders (net6/, Dependencies/)
2. Mods/ folder
3. UserLibs/ folder

**Important**: MonoBleedingEdgePatches is for Mono compatibility patches, but its assemblies can still be loaded and cause conflicts with other versions.

## Files Shipped in UserLibs

After Solution Attempt 5 (Roslyn 4.3.0 downgrade), these go to UserLibs:
- Microsoft.CodeAnalysis.dll (2.8 MB) - Roslyn 4.3.0
- Microsoft.CodeAnalysis.CSharp.dll (6.1 MB) - Roslyn 4.3.0
- Newtonsoft.Json.dll (712 KB)
- SharpGLTF.Core.dll (526 KB)
- System.Collections.Immutable.dll (194 KB) - **VERSION 6.0.0** (different from MelonLoader's 9.0.0!)
- System.Reflection.Metadata.dll (466 KB) - **VERSION 6.0.0**

**Key insight**: S.C.I 6.0.0 ≠ MelonLoader's 9.0.0, so they can coexist. We copy from NuGet cache
(netstandard2.0 builds) because .NET 6 won't copy them automatically (they're "inbox").

## Files Shipped in Mods

- Menace.ModpackLoader.dll
- Menace.DataExtractor.dll

## REPL Investigation (Ongoing)

REPL initialization uses Roslyn for runtime C# compilation. Key files:
- `src/Menace.ModpackLoader/ModpackLoaderMod.cs` - Entry point, `InitializeRepl()`
- `src/Menace.ModpackLoader/SDK/Repl/RuntimeReferenceResolver.cs` - Assembly discovery
- `src/Menace.ModpackLoader/SDK/Repl/RuntimeCompiler.cs` - Roslyn compilation wrapper

### Known Issues in REPL Code

1. **Silent catch blocks** in RuntimeReferenceResolver swallow FileLoadException without logging
2. **No validation** of loaded assembly versions
3. `InitializeReplCore()` has `[MethodImpl(NoInlining)]` to prevent JIT resolution issues

### REPL Reference Resolution Order

1. System/BCL: `{gameRoot}/dotnet`, `{gameRoot}/MelonLoader/net6`
2. MelonLoader assemblies: `{gameRoot}/MelonLoader`
3. Il2Cpp assemblies: `{gameRoot}/MelonLoader/Il2CppAssemblies`
4. Mod DLLs: `{gameRoot}/Mods/{modpackName}/dlls`, `{gameRoot}/Mods/`
5. ModpackLoader itself via reflection
6. Fallback: `AppDomain.CurrentDomain.GetAssemblies()`

## Build Script Changes

`build-redistributables.sh` now:
1. Builds ModpackLoader for net6.0 with Roslyn 4.3.0 (NOT newer versions!)
2. Copies System.Collections.Immutable 6.0.0 and System.Reflection.Metadata 6.0.0 from NuGet cache (netstandard2.0 builds)
3. These 6.0.0 versions are DIFFERENT from MelonLoader's bundled 9.0.0, so no conflict
4. Copies to `third_party/bundled/ModpackLoader/`
5. Seeds to `~/Documents/MenaceModkit/runtime/` at app startup
6. Deploys to game's `UserLibs/` (non-Menace DLLs) or `Mods/` (Menace.* DLLs)

**Note**: Only src/Menace.ModpackLoader uses Roslyn 4.3.0. The desktop app (Menace.Modkit.App) can use newer Roslyn since it doesn't run under MelonLoader.

## Lessons Learned

1. **Same assembly version ≠ same binary**: Two DLLs with identical AssemblyVersion and PublicKeyToken can still be different builds that conflict.

2. **MelonLoader's MonoBleedingEdgePatches matters**: Even though it's for Mono, these assemblies can be loaded and cause conflicts.

3. **Framework assemblies must be explicit for MelonLoader**: Unlike normal .NET 6 apps, MelonLoader can't resolve inbox framework assemblies automatically.

4. **MonoBleedingEdgePatches isn't in the resolver path**: MelonLoader loads these assemblies via bootstrap, not the resolver. The resolver searches UserLibs/Mods but not MonoBleedingEdgePatches. ~~You MUST ship them to UserLibs~~ **OUTDATED** - see lesson 5.

5. **Different versions CAN coexist; same versions from different paths CANNOT**: The solution isn't to match MelonLoader's version - it's to use a DIFFERENT version entirely. MelonLoader has S.C.I 9.0.0, so we use Roslyn 4.3.0 which needs S.C.I 6.0.0. Both can be loaded because they're different assemblies.

6. **When debugging assembly conflicts, compare with a working version**: v16 worked, v19 didn't. Comparing the shipped DLLs revealed the Roslyn upgrade was the culprit.

---
Last updated: 2026-02-11
**Resolution**: Solution Attempt 5 - Downgrade Roslyn to 4.3.0 (uses S.C.I 6.0.0, different from MelonLoader's 9.0.0)
