# Roadmap: Advanced Features (Tier 6)

**Status: NOT IMPLEMENTED -- future design sketches.**

This document collects Tier 6 feature ideas that would improve the modding ecosystem beyond the core SDK. None of these are currently planned for implementation -- they are recorded here as design direction for future work.

---

## Event Bus

**Goal:** Centralized publish/subscribe system for cross-mod communication.

Mods currently have no way to communicate with each other. An event bus would allow mods to publish events that other mods can subscribe to, enabling composable mod ecosystems.

### Sketch

```csharp
// Publisher (e.g., a damage mod)
EventBus.Publish("agent.damaged", new {
    AgentName = "Soldier_01",
    Damage = 15,
    Source = "Explosion"
});

// Subscriber (e.g., a UI mod)
EventBus.Subscribe("agent.damaged", (string topic, object data) =>
{
    DevConsole.Log($"Agent damaged: {data}");
});
```

### Design Considerations

- Events are fire-and-forget, no return values
- Topic strings, not typed channels (mods may not share assemblies)
- Subscribers receive `object` data -- no compile-time type safety across mod boundaries
- Event dispatch on the main thread only
- Per-topic subscriber lists with weak references to avoid leaking unloaded mods
- Rate limiting to prevent event storms
- DevConsole panel to inspect event flow in real time

---

## Config System

**Goal:** Per-mod configuration with save/load and DevConsole integration.

Mods need a way to expose tunable settings to users. Currently, settings are hardcoded or require editing JSON files.

### Sketch

```csharp
public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
{
    var config = ModConfig.Load("MyMod");

    float damageMultiplier = config.GetFloat("DamageMultiplier", defaultValue: 1.5f);
    bool enableLogging = config.GetBool("EnableLogging", defaultValue: false);
    int maxSquadSize = config.GetInt("MaxSquadSize", defaultValue: 8);

    config.Save();  // writes defaults for any new keys
}
```

### Design Considerations

- Config files stored as JSON in `Mods/<modpack>/config.json`
- Schema-less: mods define keys with defaults at load time
- `ModConfig.Load` creates the file with defaults if it does not exist
- DevConsole integration: a "Config" panel that shows all registered config values and allows live editing
- Config changes via DevConsole take effect immediately (callback hooks)
- Config values are typed (int, float, bool, string, enum)
- No hot reload of config files from disk (reload requires game restart or explicit `ModConfig.Reload()`)

---

## Save/Load State

**Goal:** Mod state persistence across game sessions.

Some mods need to track state that survives game restarts -- custom progression, unlocks, statistics, user preferences beyond simple config.

### Sketch

```csharp
// Save state
var state = ModState.Get("MyMod");
state.Set("KillCount", 42);
state.Set("UnlockedWeapons", new[] { "Plasma_Mk3", "RailGun" });
state.Save();  // writes to Mods/<modpack>/state.json

// Load state (called automatically on mod init)
int kills = state.GetInt("KillCount", defaultValue: 0);
string[] unlocks = state.GetStringArray("UnlockedWeapons", defaultValue: Array.Empty<string>());
```

### Design Considerations

- JSON-backed, human-readable save files
- Separate from game save files (mod state persists across save slots)
- Auto-save on scene transition or manual `Save()` call
- State versioning: mods declare a `stateVersion` integer, migration callback runs when version changes
- State data is per-modpack, not per-plugin (multiple plugins in one modpack share state)
- No cross-mod state access (mods cannot read each other's state files)

---

## IDE Integration

**Goal:** VS Code extension for REPL connection, live editing, and diagnostics.

The DevConsole REPL is functional but limited by the in-game text field. An IDE integration would provide a richer development experience.

### Sketch

- VS Code extension connects to the game via a local TCP socket (e.g., `localhost:9999`)
- REPL commands sent from VS Code editor, results displayed in a panel
- Syntax highlighting and IntelliSense from the game's type metadata
- Error panel mirrored in VS Code's Problems view
- Watch expressions editable from VS Code
- Log streaming to VS Code's Output panel

### Design Considerations

- TCP server runs inside the MelonLoader mod, accepting connections only from localhost
- Protocol: JSON-RPC over TCP (simple, well-tooled)
- Security: localhost-only binding, optional token authentication
- Performance: REPL evaluation is synchronous on the main thread, so IDE commands block until the next frame
- The game-side server is optional -- enabled via config flag, not loaded by default
- Extension is separate from the SDK, distributed via VS Code marketplace

---

## Package Manager

**Goal:** Mod dependency resolution and versioning.

As the modding ecosystem grows, mods will depend on each other. A package manager would handle:

- Dependency declaration in `modpack.json`
- Automatic download and installation of dependencies
- Version constraint resolution (semver ranges)
- Conflict detection (two mods patching the same template field)

### Sketch

```json
{
  "manifestVersion": 2,
  "name": "My Advanced Mod",
  "version": "2.0.0",
  "dependencies": [
    { "name": "CoreLib", "version": ">=1.2.0 <2.0.0" },
    { "name": "UIFramework", "version": "^3.1.0" }
  ]
}
```

### Design Considerations

- Dependency resolution at modpack load time, before `OnInitialize`
- Missing dependencies prevent the mod from loading (with clear error message)
- Version constraints use semver syntax
- No central package registry initially -- dependencies are resolved from the local `Mods/` folder
- Future: optional remote registry for discovery and download (like npm, but for mods)
- Load order automatically respects dependency graph (depended-on mods load first)
- Conflict detection: warn when two mods modify the same template field (last-writer-wins with log warning)
- The Menace Modkit desktop app could provide a UI for browsing, installing, and updating mods

---

## Priority and Dependencies

These features are roughly ordered by value and feasibility:

1. **Config System** -- low complexity, high value for mod users
2. **Save/Load State** -- medium complexity, enables a new class of mods
3. **Event Bus** -- medium complexity, enables mod composability
4. **Package Manager** -- high complexity, critical for ecosystem growth
5. **IDE Integration** -- high complexity, developer-only, can wait for ecosystem maturity

None of these features have hard dependencies on each other, but the Package Manager benefits from the Config System (per-mod config is part of the package contract), and IDE Integration benefits from the Event Bus (event flow visualization).
