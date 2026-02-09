# Plan: Game MCP Server (Phase 2)

## Overview

Extend MCP integration into the running game, enabling live inspection, modification, and debugging of game state. The game server runs inside the MelonLoader process and communicates via HTTP with the Modkit MCP server, which proxies requests from Claude.

```
┌─────────────────────────────────────────────────────────────────┐
│  Claude / MCP Client                                            │
└────────────┬────────────────────────────────────────────────────┘
             │ stdio (MCP protocol)
             ▼
┌─────────────────────────────────────────────────────────────────┐
│  Menace.Modkit.Mcp (existing)                                   │
│  + GameBridgeTools.cs  ←── proxies to game server               │
└────────────┬────────────────────────────────────────────────────┘
             │ HTTP REST (localhost:7655)
             ▼
┌─────────────────────────────────────────────────────────────────┐
│  Menace.ModpackLoader (in-game)                                 │
│  + Mcp/GameMcpServer.cs     ←── HTTP listener                   │
│  + Mcp/Handlers/*.cs        ←── request handlers                │
└─────────────────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────┐
│  Game Runtime (Il2Cpp)                                          │
│  - Live game objects, templates, scenes                         │
└─────────────────────────────────────────────────────────────────┘
```

---

## Project Structure

### In-Game Server (ModpackLoader)

```
src/Menace.ModpackLoader/
├── Mcp/
│   ├── GameMcpServer.cs           # HTTP server, request routing
│   ├── GameMcpConfig.cs           # Port, auth, settings
│   ├── Handlers/
│   │   ├── StatusHandler.cs       # /status - game state, scene info
│   │   ├── QueryHandler.cs        # /query - find objects by type/name
│   │   ├── InspectHandler.cs      # /inspect - read object fields
│   │   ├── ModifyHandler.cs       # /modify - write object fields
│   │   ├── TemplateHandler.cs     # /templates - runtime template access
│   │   ├── ConsoleHandler.cs      # /console - execute commands
│   │   ├── LogHandler.cs          # /logs - recent errors/warnings
│   │   ├── EntityHandler.cs       # /entities - spawn, control, destroy units
│   │   ├── WaveHandler.cs         # /waves - wave control
│   │   └── SpawnerHandler.cs      # /spawners - spawner control
│   └── Serialization/
│       ├── Il2CppSerializer.cs    # Serialize Il2Cpp objects to JSON
│       └── ObjectResolver.cs      # Find objects by path/id
```

### Modkit Bridge (MCP Server)

```
src/Menace.Modkit.Mcp/
├── Tools/
│   └── GameBridgeTools.cs         # Proxy tools: game_*, live_*
```

---

## API Design

### HTTP Endpoints (Game Server)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/status` | GET | Game running state, current scene, frame count |
| `/query` | POST | Find objects by type, name pattern, component |
| `/inspect/{id}` | GET | Read all fields of an object |
| `/inspect/{id}/{field}` | GET | Read specific field |
| `/modify/{id}` | POST | Write fields to an object |
| `/templates` | GET | List loaded template types |
| `/templates/{type}` | GET | List instances of a template type |
| `/templates/{type}/{name}` | GET | Get runtime template data |
| `/templates/{type}/{name}` | POST | Modify template field (hot reload) |
| `/console` | POST | Execute a console command |
| `/logs` | GET | Recent log messages (errors, warnings) |
| `/entities` | GET | List all active entities |
| `/entities/spawn` | POST | Spawn a new entity |
| `/entities/{id}` | GET | Get entity state |
| `/entities/{id}` | DELETE | Destroy entity |
| `/entities/{id}/move` | POST | Command entity to move |
| `/entities/{id}/stop` | POST | Stop entity movement |
| `/entities/{id}/attack` | POST | Command entity to attack |
| `/entities/{id}/ability` | POST | Use an ability |
| `/entities/{id}/teleport` | POST | Instantly move entity |
| `/entities/{id}/ai` | POST | Enable/disable AI |
| `/waves/spawn` | POST | Trigger a wave |
| `/waves/clear` | POST | Kill all enemies |
| `/spawners` | GET | List all spawners |
| `/spawners/{id}/trigger` | POST | Trigger a spawner |
| `/spawners/{id}/disable` | POST | Disable a spawner |

### Request/Response Format

All requests and responses use JSON. Example:

```json
// POST /query
{
  "type": "UnitTemplate",
  "namePattern": "marine*",
  "limit": 10
}

// Response
{
  "success": true,
  "objects": [
    { "id": "unit_001", "name": "marine_squad_1", "type": "UnitTemplate" },
    { "id": "unit_002", "name": "marine_squad_2", "type": "UnitTemplate" }
  ],
  "totalCount": 2
}
```

```json
// POST /modify/unit_001
{
  "fields": {
    "Health": 200,
    "Armor": 50
  }
}

// Response
{
  "success": true,
  "modified": ["Health", "Armor"],
  "previousValues": { "Health": 100, "Armor": 25 }
}
```

```json
// POST /entities/spawn
{
  "template": "mod_unit.enemy.grunt",
  "position": { "x": 10.5, "y": 0, "z": 20.3 },
  "team": "enemy",
  "name": "test_grunt_1"
}

// Response
{
  "success": true,
  "id": "entity_1234",
  "name": "test_grunt_1",
  "template": "mod_unit.enemy.grunt",
  "position": { "x": 10.5, "y": 0, "z": 20.3 }
}
```

```json
// POST /entities/entity_1234/move
{
  "target": { "x": 50.0, "y": 0, "z": 30.0 }
}

// Response
{
  "success": true,
  "id": "entity_1234",
  "action": "move",
  "target": { "x": 50.0, "y": 0, "z": 30.0 }
}
```

```json
// POST /entities/entity_1234/attack
{
  "target_id": "player_unit_01"
}

// Response
{
  "success": true,
  "id": "entity_1234",
  "action": "attack",
  "target": "player_unit_01"
}
```

```json
// POST /entities/spawn (spawn group)
{
  "template": "mod_unit.enemy.grunt",
  "count": 5,
  "position": { "x": 10, "y": 0, "z": 20 },
  "radius": 5.0,
  "team": "enemy"
}

// Response
{
  "success": true,
  "spawned": [
    { "id": "entity_1234", "position": { "x": 8.2, "y": 0, "z": 21.1 } },
    { "id": "entity_1235", "position": { "x": 11.5, "y": 0, "z": 18.9 } },
    // ...
  ],
  "count": 5
}
```

---

## MCP Tools (Bridge)

### Tier 1: Core Game Tools

| Tool | Parameters | Description |
|------|------------|-------------|
| `game_status` | - | Check if game is running, get scene info |
| `game_query` | `type, namePattern?, limit?` | Find game objects |
| `game_inspect` | `id, field?` | Read object fields |
| `game_modify` | `id, field, value` | Write a field value |
| `game_logs` | `level?, limit?` | Get recent log messages |

### Tier 2: Template Tools

| Tool | Parameters | Description |
|------|------------|-------------|
| `live_template_list` | `type` | List runtime template instances |
| `live_template_get` | `type, name` | Get runtime template data |
| `live_template_set` | `type, name, field, value, persist?, modpack?` | Hot-modify a template (optionally persist to modpack) |
| `live_template_reload` | `type?, name?` | Force reload from disk |

### Tier 3: Advanced Tools

| Tool | Parameters | Description |
|------|------------|-------------|
| `game_console` | `command` | Execute console command |
| `game_scene` | `name?` | Get/change current scene |
| `game_pause` | `paused` | Pause/unpause game |
| `game_timescale` | `scale` | Set time scale (slow-mo) |

### Tier 4: Entity Control Tools

| Tool | Parameters | Description |
|------|------------|-------------|
| `entity_spawn` | `template, position, team?, name?` | Spawn a unit/enemy at position |
| `entity_destroy` | `id` | Destroy a unit |
| `entity_list` | `team?, type?` | List all active entities |
| `entity_move` | `id, target_position` | Command entity to move |
| `entity_stop` | `id` | Stop entity movement |
| `entity_attack` | `id, target_id` | Command entity to attack target |
| `entity_ability` | `id, ability_name, target?` | Use an ability |
| `entity_set_team` | `id, team` | Change entity team |
| `entity_set_ai` | `id, enabled` | Enable/disable AI control |
| `entity_teleport` | `id, position` | Instantly move entity |

### Tier 5: Wave/Spawn Control

| Tool | Parameters | Description |
|------|------------|-------------|
| `wave_spawn` | `wave_id` | Trigger a specific wave |
| `wave_clear` | - | Kill all enemies |
| `spawn_group` | `template, count, position, radius?, team?` | Spawn multiple units in area |
| `spawner_list` | - | List all spawners in scene |
| `spawner_trigger` | `spawner_id` | Manually trigger a spawner |
| `spawner_disable` | `spawner_id` | Disable a spawner |

---

## Implementation Details

### GameMcpServer.cs

```csharp
using System.Net;
using MelonLoader;

namespace Menace.ModpackLoader.Mcp;

public class GameMcpServer
{
    private HttpListener? _listener;
    private readonly int _port;
    private readonly CancellationTokenSource _cts = new();

    public bool IsRunning => _listener?.IsListening ?? false;

    public GameMcpServer(int port = 7655)
    {
        _port = port;
    }

    public void Start()
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
        _listener.Start();

        MelonLogger.Msg($"[MCP] Game server listening on port {_port}");

        // Handle requests on thread pool
        Task.Run(AcceptLoop);
    }

    public void Stop()
    {
        _cts.Cancel();
        _listener?.Stop();
    }

    private async Task AcceptLoop()
    {
        while (!_cts.IsCancellationRequested && _listener?.IsListening == true)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                MelonLogger.Warning($"[MCP] Accept error: {ex.Message}");
            }
        }
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            var path = request.Url?.AbsolutePath ?? "/";
            var result = path switch
            {
                "/status" => StatusHandler.Handle(request),
                "/query" => await QueryHandler.Handle(request),
                "/console" => await ConsoleHandler.Handle(request),
                "/logs" => LogHandler.Handle(request),
                var p when p.StartsWith("/inspect/") => InspectHandler.Handle(request, p),
                var p when p.StartsWith("/modify/") => await ModifyHandler.Handle(request, p),
                var p when p.StartsWith("/templates") => TemplateHandler.Handle(request, p),
                _ => new { error = "Not found" }
            };

            var json = JsonSerializer.Serialize(result);
            var bytes = Encoding.UTF8.GetBytes(json);

            response.ContentType = "application/json";
            response.StatusCode = 200;
            await response.OutputStream.WriteAsync(bytes);
        }
        catch (Exception ex)
        {
            var error = JsonSerializer.Serialize(new { error = ex.Message });
            var bytes = Encoding.UTF8.GetBytes(error);
            response.StatusCode = 500;
            await response.OutputStream.WriteAsync(bytes);
        }
        finally
        {
            response.Close();
        }
    }
}
```

### Il2CppSerializer.cs

```csharp
namespace Menace.ModpackLoader.Mcp.Serialization;

/// <summary>
/// Serializes Il2Cpp objects to JSON, handling:
/// - Il2CppSystem.String → string
/// - Il2CppSystem.Collections.Generic.List → array
/// - Circular references (by ID)
/// - Unity objects (name, instanceId)
/// </summary>
public static class Il2CppSerializer
{
    public static JsonElement Serialize(object obj, int maxDepth = 3)
    {
        return SerializeValue(obj, 0, maxDepth, new HashSet<int>());
    }

    private static JsonElement SerializeValue(object? obj, int depth, int maxDepth, HashSet<int> seen)
    {
        if (obj == null) return JsonNull;
        if (depth > maxDepth) return JsonString("[max depth]");

        // Handle Il2Cpp primitives
        if (obj is Il2CppSystem.String s) return JsonString(s.ToString());
        if (obj is bool b) return JsonBool(b);
        if (obj is int i) return JsonNumber(i);
        if (obj is float f) return JsonNumber(f);
        // ... etc

        // Handle Unity objects
        if (obj is UnityEngine.Object unityObj)
        {
            var id = unityObj.GetInstanceID();
            if (seen.Contains(id)) return JsonString($"[circular:{id}]");
            seen.Add(id);

            return SerializeUnityObject(unityObj, depth, maxDepth, seen);
        }

        // Handle Il2Cpp collections
        if (obj is Il2CppSystem.Collections.IList list)
        {
            return SerializeList(list, depth, maxDepth, seen);
        }

        // Handle generic objects via reflection
        return SerializeObject(obj, depth, maxDepth, seen);
    }
}
```

### GameBridgeTools.cs (Modkit MCP)

```csharp
[McpServerToolType]
public static class GameBridgeTools
{
    private static readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(5) };
    private const string GameServerUrl = "http://127.0.0.1:7655";

    [McpServerTool(Name = "game_status", ReadOnly = true)]
    [Description("Check if the game is running and get current state (scene, frame count, etc.)")]
    public static async Task<string> GameStatus()
    {
        try
        {
            var response = await _client.GetAsync($"{GameServerUrl}/status");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            return JsonSerializer.Serialize(new { running = false, error = response.StatusCode.ToString() });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { running = false, error = ex.Message });
        }
    }

    [McpServerTool(Name = "game_query", ReadOnly = true)]
    [Description("Find game objects by type and name pattern")]
    public static async Task<string> GameQuery(
        [Description("Object type to search for")] string type,
        [Description("Name pattern (supports * wildcard)")] string? namePattern = null,
        [Description("Max results to return")] int limit = 20)
    {
        var request = new { type, namePattern, limit };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        try
        {
            var response = await _client.PostAsync($"{GameServerUrl}/query", content);
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool(Name = "game_modify")]
    [Description("Modify a field on a live game object")]
    public static async Task<string> GameModify(
        [Description("Object ID from game_query")] string id,
        [Description("Field name to modify")] string field,
        [Description("New value")] string value)
    {
        var request = new { fields = new Dictionary<string, object> { [field] = ParseValue(value) } };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        try
        {
            var response = await _client.PostAsync($"{GameServerUrl}/modify/{id}", content);
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool(Name = "live_template_set")]
    [Description("Hot-modify a template value in the running game (takes effect immediately)")]
    public static async Task<string> LiveTemplateSet(
        [Description("Template type")] string type,
        [Description("Template instance name")] string name,
        [Description("Field to modify")] string field,
        [Description("New value")] string value,
        [Description("If true, also write the change to a modpack on disk")] bool persist = false,
        [Description("Modpack to persist to (required if persist=true)")] string? modpack = null)
    {
        var request = new { field, value = ParseValue(value), persist, modpack };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        try
        {
            var response = await _client.PostAsync($"{GameServerUrl}/templates/{type}/{name}", content);
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}
```

---

## Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `src/Menace.ModpackLoader/Mcp/GameMcpServer.cs` | HTTP server and routing |
| `src/Menace.ModpackLoader/Mcp/GameMcpConfig.cs` | Configuration (port, enabled) |
| `src/Menace.ModpackLoader/Mcp/Handlers/StatusHandler.cs` | Game status endpoint |
| `src/Menace.ModpackLoader/Mcp/Handlers/QueryHandler.cs` | Object query endpoint |
| `src/Menace.ModpackLoader/Mcp/Handlers/InspectHandler.cs` | Object inspection |
| `src/Menace.ModpackLoader/Mcp/Handlers/ModifyHandler.cs` | Object modification |
| `src/Menace.ModpackLoader/Mcp/Handlers/TemplateHandler.cs` | Runtime template access |
| `src/Menace.ModpackLoader/Mcp/Handlers/ConsoleHandler.cs` | Console command execution |
| `src/Menace.ModpackLoader/Mcp/Handlers/LogHandler.cs` | Log retrieval |
| `src/Menace.ModpackLoader/Mcp/Handlers/EntityHandler.cs` | Entity spawn/control/destroy |
| `src/Menace.ModpackLoader/Mcp/Handlers/WaveHandler.cs` | Wave spawn/clear |
| `src/Menace.ModpackLoader/Mcp/Handlers/SpawnerHandler.cs` | Spawner control |
| `src/Menace.ModpackLoader/Mcp/Serialization/Il2CppSerializer.cs` | Il2Cpp → JSON |
| `src/Menace.ModpackLoader/Mcp/Serialization/ObjectResolver.cs` | Find objects by ID |
| `src/Menace.Modkit.Mcp/Tools/GameBridgeTools.cs` | MCP bridge tools |
| `src/Menace.Modkit.Mcp/Tools/EntityTools.cs` | Entity control MCP tools |

### Modify Existing

| File | Change |
|------|--------|
| `src/Menace.ModpackLoader/ModpackLoaderMod.cs` | Start/stop GameMcpServer |
| `src/Menace.ModpackLoader/SDK/ModSettings.cs` | Add MCP enabled/port settings |

---

## Security Considerations

1. **Localhost Only**: Bind to `127.0.0.1` only, never `0.0.0.0`
2. **No Arbitrary Code**: Console commands go through registered command system
3. **Rate Limiting**: Limit requests per second to prevent game lag
4. **Validation**: Validate all object IDs and field names before access
5. **Read-Only Default**: Modification tools require explicit opt-in setting
6. **Timeout**: All operations have timeout to prevent game freeze

---

## Integration with ModpackLoader

### Startup

```csharp
// In ModpackLoaderMod.OnApplicationStart()
if (ModSettings.McpServerEnabled)
{
    _mcpServer = new GameMcpServer(ModSettings.McpServerPort);
    _mcpServer.Start();
}
```

### Main Thread Dispatch

Game state must be accessed from the main thread. Use a dispatcher:

```csharp
public static class MainThreadDispatcher
{
    private static readonly ConcurrentQueue<Action> _queue = new();

    public static void Enqueue(Action action) => _queue.Enqueue(action);

    public static Task<T> RunOnMainThread<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();
        Enqueue(() =>
        {
            try { tcs.SetResult(func()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    // Called from OnUpdate()
    public static void ProcessQueue()
    {
        while (_queue.TryDequeue(out var action))
            action();
    }
}
```

---

## Testing Plan

1. **Unit Tests**: Test serialization, parsing, routing in isolation
2. **Integration Test**: Start game with MCP enabled, verify `/status` responds
3. **Query Test**: Query for known object types, verify results
4. **Modify Test**: Modify a template value, verify change in-game
5. **Stress Test**: Send many requests, verify game doesn't lag

### Manual Testing Steps

1. Build and deploy ModpackLoader with MCP server
2. Launch game
3. Verify `http://127.0.0.1:7655/status` returns game state
4. Use Modkit MCP `game_status` tool from Claude
5. Query for units: `game_query("UnitTemplate", "marine*")`
6. Modify a value: `live_template_set("WeaponTemplate", "cannon", "Damage", "999")`
7. Verify change takes effect in-game

---

## Implementation Order

### Phase 1: Core Infrastructure
1. **GameMcpServer basics** - HTTP listener, routing, status endpoint
2. **StatusHandler** - Return game state, scene, frame count
3. **Il2CppSerializer** - Serialize basic types
4. **QueryHandler** - Find objects by type/name
5. **InspectHandler** - Read object fields
6. **GameBridgeTools** - Proxy tools in Modkit MCP

### Phase 2: Modification
7. **ModifyHandler** - Write fields (with main thread dispatch)
8. **TemplateHandler** - Runtime template access with persist option
9. **ConsoleHandler** - Execute commands
10. **LogHandler** - Log retrieval

### Phase 3: Entity Control
11. **EntityHandler** - Spawn, list, destroy entities
12. **Entity movement** - move, stop, teleport commands
13. **Entity combat** - attack, ability commands
14. **Entity AI** - enable/disable AI control
15. **EntityTools** - MCP bridge tools for entities

### Phase 4: Wave/Spawner Control
16. **WaveHandler** - Trigger waves, clear enemies
17. **SpawnerHandler** - List, trigger, disable spawners
18. **Spawn groups** - Spawn multiple units at once

---

## Design Decisions

1. **Persistence**: Live template changes are runtime-only by default. Add `persist: true` flag to write changes back to a modpack on disk.
2. **No Undo**: Modifications are not reversible. User can reload from disk if needed.
3. **Localhost Only**: No authentication token needed, binding to 127.0.0.1 is sufficient.

### Persistence Flow

When `persist: true`:
```
live_template_set(type, name, field, value, persist=true, modpack="MyMod")
    │
    ▼
Game Server applies change to runtime
    │
    ▼
Game Server writes to staging: ~/Documents/MenaceModkit/staging/{modpack}/stats/{type}.json
    │
    ▼
Returns { success: true, persisted: true, modpack: "MyMod" }
```

The game server writes directly to the staging directory (same location the Modkit uses). This avoids needing a callback to the Modkit MCP server.

## Open Questions

1. **Object Lifetime**: How to handle objects that get destroyed between query and modify?
2. **WebSocket**: Worth adding for log streaming, or polling is fine?
