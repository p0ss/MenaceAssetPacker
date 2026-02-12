# What Is the Menace SDK?

The Menace SDK is the in-game code modding surface for Menace. It lets mods read game state, modify templates, query live IL2CPP objects, patch behavior, and run tooling (DevConsole/REPL) without rebuilding the game.

The SDK is exposed through the `Menace.SDK` namespace and is hosted by `Menace.ModpackLoader.dll`.

## What Actually Ships

- `Menace.ModpackLoader.dll` (MelonLoader mod running inside the game process)
- Public SDK APIs under `Menace.SDK` (for plugin authors)
- Loader/runtime systems (modpack discovery, DLL loading, template patching, DevConsole, REPL)

## Where It Runs

- In-game runtime: .NET 6 via MelonLoader (`Menace.ModpackLoader`)
- Modkit desktop app: .NET 10 (`Menace.Modkit.App`)

Important distinction:
- Your mod plugin code targets the in-game runtime (net6.0), not the Modkit app runtime.

## How Plugin Authors Use It

You build a net6.0 class library that implements `IModpackPlugin`, then place the DLL in a modpack `dlls/` folder. The loader discovers and calls your plugin automatically.

Minimal modpack layout:

```text
MyMod-modpack/
  modpack.json
  dlls/
    MyMod.dll
```

## Plugin Lifecycle

`IModpackPlugin` is the contract between your DLL and the loader:

| Method | Purpose |
|---|---|
| `OnInitialize(MelonLogger.Instance logger, Harmony harmony)` | One-time setup after plugin load. |
| `OnSceneLoaded(int buildIndex, string sceneName)` | Scene transition hook for scene-dependent logic. |
| `OnUpdate()` | Optional per-frame logic. |
| `OnGUI()` | Optional IMGUI drawing hook. |
| `OnUnload()` | Optional cleanup on unload/hot-reload/shutdown. |

## SDK vs Other Modding Paths

Use SDK code when you need logic and runtime behavior:
- Conditionals, event reactions, procedural logic, live queries, custom debug tooling

Use data patches/assets when changes are static:
- Field overrides in templates
- Texture/audio/model replacements

Many mods combine both: static patch data + small SDK plugin for runtime logic.

## Recommended Reading Order

1. [Getting Started: Your First Plugin](getting-started.md)
2. [GameObj API](api/game-obj.md)
3. [GameQuery API](api/game-query.md)
4. [Templates API](api/templates.md)
5. [GameState API](api/game-state.md)

