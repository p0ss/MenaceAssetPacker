# Compilation Troubleshooting

This guide covers common C# compilation failures in the Modkit Code Editor and
how to diagnose them quickly.

Primary pipeline:

- `src/Menace.Modkit.App/Services/CompilationService.cs`
- `src/Menace.Modkit.App/Services/ReferenceResolver.cs`

## Quick Checklist

1. Confirm game path is set correctly in app settings.
2. Ensure MelonLoader is installed in the game directory.
3. Run the game once so `MelonLoader/Il2CppAssemblies` is generated.
4. Verify setup components are up to date.
5. Rebuild after fixing any `[Reference Resolution]` warnings.

## How Compilation Works

`CompilationService` does the following:

1. Loads source files listed in `modpack.json` (`code.sources`)
2. Runs security scanning
3. Parses sources with Roslyn
4. Injects nullable-attribute polyfill
5. Resolves references asynchronously
6. Emits DLL to `<modpack>/build/<AssemblyName>.dll`

Resolver warnings are included in diagnostics as:

- `[Reference Resolution] ...`

Treat these as high-value troubleshooting signals.

## Common Errors

### `No source files to compile`

Cause:

- `code.sources` is empty.

Fix:

- Add source entries to `modpack.json` and ensure files exist.

### `No source files found on disk`

Cause:

- Source paths listed in manifest do not exist relative to modpack path.

Fix:

- Correct paths in `code.sources`.

### `No references resolved. Check that the game install path is set correctly and MelonLoader is installed.`

Cause:

- Resolver found no usable references.

Fix:

1. Recheck game path.
2. Confirm MelonLoader files exist.
3. Re-run setup to install/update required components.

### `[Reference Resolution] Il2CppAssemblies directory not found ...`

Cause:

- Proxies are not generated yet.

Fix:

- Launch the game once with MelonLoader installed, wait for initial generation.

### `[Reference Resolution] Requested reference 'X' was not found ...`

Cause:

- `code.references` includes an assembly simple name that cannot be located.

Fix:

1. Use assembly simple names (for example `MelonLoader`, not file paths).
2. Verify DLL exists in known search roots (MelonLoader, Il2CppAssemblies,
   Mods, bundled, component cache).

### DotNetRefs mismatch warning

Example:

- Game dotnet version does not match configured DotNetRefs version.

What happens:

- Resolver prefers configured DotNetRefs and may auto-download them.

Fix:

- Let setup update components, then retry compile.

## Reference Naming Tips

In `modpack.json`:

- Prefer assembly simple names in `code.references`.
- Extension is optional (`Foo` and `Foo.dll` normalize to the same lookup key).

## Output Expectations

On success:

- `Success = true`
- `OutputDllPath` points to `<modpack>/build/<sanitized-name>.dll`

Assembly names are sanitized from modpack name (invalid filename chars and
spaces are replaced).

## Runtime REPL Note

The in-game Console uses command-first dispatch, then C# evaluation fallback
when Roslyn is available. If unknown input is not being evaluated as C#,
runtime Roslyn support may be missing from that deployment.

## If Issues Persist

1. Re-run setup and environment checks.
2. Capture full diagnostics including `[Reference Resolution]` messages.
3. Verify component versions in `third_party/versions.json` versus local setup
   status.
