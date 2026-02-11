# Reference Resolution

This document describes how the app resolves C# compilation references for
modpacks (`ReferenceResolver`).

Primary implementation:

- `src/Menace.Modkit.App/Services/ReferenceResolver.cs`
- `src/Menace.Modkit.App/Services/CompilationService.cs`

## High-Level Flow

`CompilationService` calls:

- `ReferenceResolver.ResolveReferencesAsync(requestedReferences, ct)`

Resolution output is a `List<MetadataReference>`. Non-fatal issues are recorded
in `ResolutionIssues` and surfaced as compilation warnings.

## Resolution Order

The resolver runs in this order:

1. System/BCL references
2. Bundled third-party DLLs
3. Game-local MelonLoader/IL2CPP/mod DLLs
4. Explicit user-requested references (`manifest.code.references`)

All references are deduplicated by:

- Full path
- Assembly simple name

## System/BCL Strategy

System references are chosen with strict priority to keep compilation aligned
with MelonLoader's .NET 6 runtime:

1. Game `dotnet/` runtime, only if version matches configured `DotNetRefs`
2. Configured `DotNetRefs` from component cache or bundled payload
3. Global .NET 6 SDK reference pack (`Microsoft.NETCore.App.Ref`)
4. Global .NET 6 shared framework
5. Trusted Platform Assemblies fallback (last resort)

`DotNetRefs` expected version is read from `third_party/versions.json`.

If no matching local `DotNetRefs` is found, the resolver may attempt:

- `ComponentManager.DownloadComponentAsync("DotNetRefs")`

## Non-System Reference Sources

After system refs, the resolver adds managed non-framework references from:

- `third_party/bundled/**.dll` (recursive)
- `<Game>/MelonLoader/Il2CppAssemblies/*.dll`
- `<Game>/MelonLoader/**/*.dll` (recursive)
- `<Game>/Mods/*.dll`

Framework-like assemblies from game/bundled folders are explicitly filtered out
to avoid old BCL conflicts (for example old `System.*` and `mscorlib` copies).

## Requested References (`manifest.code.references`)

Requested names are normalized to assembly simple names:

- `Il2CppInterop.Runtime`
- `Il2CppInterop.Runtime.dll`

Both normalize to `Il2CppInterop.Runtime`.

Search roots include:

- `<Game>/MelonLoader`
- `<Game>/MelonLoader/Il2CppAssemblies`
- `<Game>/Mods`
- Installed MelonLoader component path and its `Il2CppAssemblies`
- `third_party/bundled`

If a requested reference is not found, a warning is emitted to
`ResolutionIssues`.

## Failure/Warning Signals

Typical issues reported by resolver warnings:

- Game install path missing or invalid
- `Il2CppAssemblies` missing
- `Assembly-CSharp.dll` missing
- MelonLoader directory missing
- Requested reference not found
- DotNetRefs version mismatch
- DotNetRefs download failure

Warnings are prefixed during compilation as:

- `[Reference Resolution] ...`

## Practical Notes

- Use the async API (`ResolveReferencesAsync`) in app code paths.
- Pass cancellation tokens through compile operations.
- Keep `third_party/versions.json` updated when bumping `DotNetRefs`.
- Prefer assembly simple names in `manifest.code.references`.
