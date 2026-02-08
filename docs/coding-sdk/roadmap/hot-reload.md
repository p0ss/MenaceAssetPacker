# Roadmap: Hot Reload (Tier 5)

**Status: NOT IMPLEMENTED -- future design sketch.**

This document outlines the design for live deployment and hot reload of mod DLLs without restarting the game. This is a Tier 5 feature -- high complexity, significant technical risk.

---

## Goal

Allow mod developers to iterate on code mods without restarting the game. When a mod DLL is recompiled:

1. The loader detects the change
2. The old plugin is unloaded (cleanup, unpatch)
3. The new DLL is loaded
4. The new plugin is initialized

This would reduce the edit-compile-test cycle from minutes (game restart) to seconds.

---

## Approach

### File Watcher

A `FileSystemWatcher` on each modpack's `dlls/` directory, monitoring for `.dll` file changes. Debounce writes (IDEs often write multiple times) with a short delay (e.g., 500ms).

```
dlls/
  MyMod.dll       <-- watcher detects change
  MyMod.pdb       <-- also updated
```

### Unload Phase

Call `IModpackPlugin.OnUnload()` on all plugins from the changed assembly. The plugin is responsible for:

- Calling `harmony.UnpatchSelf()` to remove all Harmony patches
- Removing DevConsole panels via `DevConsole.RemovePanel`
- Removing watches via `DevConsole.Unwatch`
- Unsubscribing from `GameState.SceneLoaded`, `GameState.TacticalReady`, `ModError.OnError`
- Releasing any Unity resources (textures, GameObjects) the mod created

The `OnUnload()` lifecycle hook already exists on `IModpackPlugin`.

### Load Phase

Load the new DLL via `Assembly.LoadFrom` with shadow copy to avoid file locks:

```
1. Copy dlls/MyMod.dll -> temp/MyMod_{timestamp}.dll
2. Assembly.LoadFrom(temp path)
3. Discover IModpackPlugin implementations
4. Call OnInitialize(logger, harmony)
5. Call OnSceneLoaded with current scene
```

Shadow copy is required because the file may still be locked by the compiler or IDE.

### Reference Counting

Track which assemblies were loaded from which modpack directory. On reload, only unload/reload plugins from the changed assembly -- leave other mods untouched.

---

## Challenges

### Harmony Patch Cleanup

Harmony patches reference methods from the old assembly. `UnpatchSelf()` should handle this, but edge cases exist:

- Transpiler patches that injected IL from the old assembly
- Patches that captured closures over old-assembly types
- Other mods that patched the same methods (interleaved patch chains)

Mitigation: Validate that all patches from the old assembly are removed. If any remain, log a warning and skip reload.

### IL2CPP Type Registration

IL2CPP class pointers are cached globally by `GameType` and `OffsetCache`. If the new assembly references types differently:

- `GameType._nameCache` may hold stale entries
- `OffsetCache._cache` may hold offsets for types that changed layout

Mitigation: Clear caches for the affected mod's types on unload. This is safe because caches are lazy-populated.

### Assembly Unloading

.NET does not support true assembly unloading without `AssemblyLoadContext` (which requires .NET Core 3+). Under MelonLoader's .NET 6 runtime, `AssemblyLoadContext.Unload()` is available but requires the assembly to be loaded into a collectible ALC.

If collectible ALCs are not feasible, the fallback is to load each reload into a new context without unloading old ones. This leaks memory on each reload but is acceptable during development.

### State Persistence

The old plugin's state (cached objects, configuration, runtime data) is lost on reload. Mods that need state persistence across reloads would need to serialize state to disk or a shared dictionary.

### Unity Thread Safety

File watcher callbacks arrive on a thread pool thread. All Unity operations (including Harmony patching) must run on the main thread. Queue the reload via `GameState.RunDelayed(1, ...)`.

---

## Prerequisites

- `IModpackPlugin.OnUnload()` lifecycle hook -- **already implemented**
- Shadow copy infrastructure for DLL loading
- Per-assembly plugin tracking in `DllLoader` -- **partially implemented** (tracks loaded assemblies)
- Cache invalidation hooks in `GameType` and `OffsetCache`

---

## Estimated Complexity

**High.** The core file watcher and reload loop are straightforward, but robust cleanup of Harmony patches, IL2CPP caches, and Unity resources is difficult to get right. Assembly leaking is likely in the initial implementation. Transpiler patches are probably not safely reloadable.

A practical first version could support hot reload for mods that use only `GamePatch.Prefix`/`Postfix` (no transpilers), do not hold persistent Unity objects, and implement `OnUnload()` properly.
