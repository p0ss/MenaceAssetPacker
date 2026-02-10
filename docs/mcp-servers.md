# Menace Modkit MCP Servers

The Menace Modkit provides two MCP (Model Context Protocol) servers that enable AI assistants like Claude to interact with the modkit and the game itself.

---

## A Note on AI in Modding

**This is entirely optional.** The MCP integration is an additional tool for modders who want it — nothing about the core modkit requires AI assistance. You can create, compile, and deploy modpacks entirely through the GUI or CLI without ever touching these features.

For those who do choose to use AI assistance, this integration provides several benefits:

1. **Governed outputs** — All AI-generated content goes through the same compilation pipeline, security scanning, and validation as hand-written mods. There's no special path that bypasses safety checks.

2. **Interoperability** — AI-assisted mods use the same modpack format, template patching system, and SDK as everything else. They're indistinguishable from manually-created mods in structure and deployment.

3. **Transparency** — The MCP protocol is an open standard. Everything the AI can do is explicitly defined and auditable through the tool definitions.

4. **Safety standards** — Rather than having AI tools operate outside the modding ecosystem, this creates a consistent framework where all mod creation — human or AI-assisted — follows the same rules.

We believe modders should have the freedom to use whatever tools work best for them. For some, that's pure manual creation. For others, AI assistance speeds up iteration or helps with unfamiliar systems. Both approaches produce valid, compliant modpacks that work identically in-game.

---

## Use Cases

### 1. Mod Developer Testing

**Scenario:** Developer has the game running on one screen and Claude on another. They're testing a new mod that modifies weapon damage or spawns custom units.

**Workflow:**
- Deploy mod changes via `deploy_modpack`
- Query game state with `game_actors`, `game_tactical`
- Verify template modifications took effect with `game_template`
- Check for runtime errors with `game_errors`
- Iterate rapidly without restarting the game

**Current Support:** ✅ Well supported
- Actor listing and inspection
- Template querying
- Error log access
- Tactical state visibility

**Gaps:**
- No way to execute console commands remotely (intentionally omitted for security)
- Limited real-time event streaming (polling only)

---

### 2. Player Assist

**Scenario:** Player wants tactical advice during combat. "Which move is best from here?" or "Should I flank or take cover?" Some players may want cheats or spawning.

**Workflow:**
- Query current tactical state with `game_tactical`
- Get all actor positions and status with `game_actors`
- Analyze the active actor's skills and AP with `game_actor`
- Check cover at positions with `game_cover`
- Query line of sight with `game_los`
- Get reachable tiles with `game_reachable`
- Analyze threats with `game_threats`

**Current Support:** ✅ Fully supported
- Actor positions and basic stats
- Skill lists with AP costs and ranges
- Tactical round/turn information
- Cover values per direction at any tile
- Line of sight between actors or tiles
- Movement cost estimation and reachable tiles
- Threat analysis (which enemies can attack)
- Hit chance calculation using game's actual combat formula

**Remaining Gaps:**
| Data | Purpose |
|------|---------|
| **Skill effects** | What does each skill actually do? (requires skill template parsing) |

---

### 3. Enemy AI Assist

**Scenario:** Claude orchestrates enemy turns with more sophisticated tactics than the built-in AI. Coordinated flanking, suppression, focus fire.

**Workflow:**
- On enemy turn start, query full game state with `game_actors`
- Analyze player positions, health, cover with `game_cover`, `game_visibility`
- Check LOS and attack ranges with `game_los`, `game_threats`
- Plan movement using `game_reachable`, `game_movement`
- Coordinate actions across multiple units

**Current Support:** ✅ Fully supported
- All actor positions and factions
- Health, suppression, morale for all units
- Equipped weapons and attack ranges
- Per-actor visibility info (vision, detection, concealment)
- Line of sight queries between any positions
- Reachable tiles and movement costs
- Threat analysis from any actor's perspective
- Hit chance calculation using game's combat formula

**Remaining Gaps:**
| Data | Purpose |
|------|---------|
| **AI intent** | What is the AI planning to do? (requires Agent/AIFaction SDK access) |

**Integration Approach:**
The goal is *minimal interface, maximum effect*. Rather than replacing the AI entirely:
1. Query game state at turn start
2. Identify high-value tactical opportunities the AI might miss
3. Suggest or inject a small number of commands
4. Let the existing AI handle routine actions

This requires understanding the existing AI system's decision points and how to influence them.

---

### Future Consideration: Game State Tracking

A queryable game state history would enable:

| Feature | Description |
|---------|-------------|
| **Multiplayer foundation** | Serialize/sync game state across clients |
| **Stats & analytics** | Squad performance, accuracy over time, kills per mission |
| **Replay system** | Record and replay tactical decisions |
| **Learning AI** | Train on historical game states |
| **Squaddie tracking** | Individual squaddie combat history, "best marksman" etc. |

This is beyond the current MCP scope but the infrastructure we're building (game state queries) is a foundation for it.

---

## Implemented Tactical Endpoints

The following tactical analysis endpoints have been implemented:

### Implemented ✅

| Endpoint | Description |
|----------|-------------|
| `/los` | Line of sight queries between tiles or actors |
| `/cover` | Cover values at tile in all 8 directions |
| `/tile` | Full tile info (elevation, blocking, occupant, cover) |
| `/movement` | Path check with AP cost estimation |
| `/reachable` | All tiles reachable within AP budget |
| `/visibility` | Actor visibility info (vision, detection, concealment) |
| `/threats` | Threat analysis (enemies that can see/attack) |
| `/hitchance` | Hit chance calculation using game's combat formula |

### Future Enhancements

```
/ai_intent              - What the AI is planning
  Returns: {
    actor: "Grunt",
    state: "ReadyToExecute",
    behavior: "InflictDamage",
    targetTile: { x: 8, y: 5 },
    targetActor: "Player1",
    score: 150
  }
```

This requires adding Agent/AIFaction access to the SDK. The AI system is documented in `docs/reverse-engineering/ai-decisions.md` - it uses a utility-based decision system where each Agent evaluates Behaviors and tile scores.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│  Claude Code / MCP Client                                       │
└────────────┬───────────────────────────┬────────────────────────┘
             │ stdio                     │ stdio
             ▼                           ▼
┌────────────────────────┐    ┌───────────────────────────────────┐
│  Menace.Modkit.Mcp     │    │  (Other MCP servers)              │
│  - Modpack tools       │    │                                   │
│  - Template tools      │    │                                   │
│  - Build/deploy tools  │    │                                   │
│  - Game bridge tools ──┼────┼───────────────┐                   │
└────────────────────────┘    └───────────────┼───────────────────┘
                                              │ HTTP localhost:7655
                                              ▼
                              ┌───────────────────────────────────┐
                              │  GameMcpServer (in-game)          │
                              │  - Actor queries                  │
                              │  - Tactical state                 │
                              │  - Template inspection            │
                              │  - Inventory, roster, etc.        │
                              └───────────────────────────────────┘
```

**Two servers, one integration:**
- **Menace.Modkit.Mcp** - Design-time server for modpack development (stdio transport)
- **GameMcpServer** - Runtime server inside the game process (HTTP on localhost:7655)

The Modkit MCP server includes "bridge" tools that proxy requests to the in-game server, so you only need to configure one MCP server in Claude Code.

---

## Setup

### Prerequisites

1. **.NET 10 SDK** - Required to build and run the Modkit MCP server
2. **Game with MelonLoader** - The game must have MelonLoader installed
3. **ModpackLoader deployed** - Run the Modkit app and deploy to install the ModpackLoader

### Building the MCP Server

```bash
cd /path/to/MenaceAssetPacker
dotnet build src/Menace.Modkit.Mcp/Menace.Modkit.Mcp.csproj
```

The built server will be at:
```
src/Menace.Modkit.Mcp/bin/Debug/net10.0/Menace.Modkit.Mcp.dll
```

---

## Configuration

### Claude Code Configuration

Add the Menace Modkit MCP server to your Claude Code settings. The configuration file location depends on your setup:

- **Project-level**: `.claude/mcp.json` in your project root
- **User-level**: `~/.claude/mcp.json`

**Configuration:**

```json
{
  "mcpServers": {
    "menace-modkit": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/path/to/MenaceAssetPacker/src/Menace.Modkit.Mcp"
      ]
    }
  }
}
```

Or if using the built DLL directly:

```json
{
  "mcpServers": {
    "menace-modkit": {
      "command": "dotnet",
      "args": [
        "/path/to/MenaceAssetPacker/src/Menace.Modkit.Mcp/bin/Debug/net10.0/Menace.Modkit.Mcp.dll"
      ]
    }
  }
}
```

### Verifying the Setup

After configuring, restart Claude Code. You should see the Menace Modkit tools available. Try:

```
Use the modpack_list tool to show available modpacks
```

---

## Available Tools

### Modpack Management

| Tool | Description |
|------|-------------|
| `modpack_list` | List all modpacks in staging (optionally include deployed) |
| `modpack_create` | Create a new modpack with name, author, description |
| `modpack_get` | Get detailed info about a specific modpack |
| `modpack_update` | Update modpack metadata (author, version, description, load order) |
| `modpack_delete` | Delete a modpack from staging |

### Template Editing

| Tool | Description |
|------|-------------|
| `template_types` | List all available template types (EntityTemplate, WeaponTemplate, etc.) |
| `template_list` | List all instances of a template type |
| `template_get` | Get a specific template's data |
| `template_patch` | Apply field overrides to a template |
| `template_reset` | Reset a template to vanilla values |
| `template_clone` | Clone a template with a new name |

### Source Code Management

| Tool | Description |
|------|-------------|
| `source_list` | List source files in a modpack |
| `source_read` | Read a source file's contents |
| `source_write` | Write/update a source file |
| `source_add` | Add a new source file to a modpack |

### Build & Deployment

| Tool | Description |
|------|-------------|
| `compile_modpack` | Compile a modpack and get diagnostics |
| `security_scan` | Scan source code for security issues |
| `deploy_modpack` | Deploy a single modpack to the game |
| `deploy_all` | Deploy all modpacks |
| `undeploy_all` | Remove all deployed modpacks |
| `deploy_status` | Check deployment status |

### Game Bridge Tools (require game running)

| Tool | Description |
|------|-------------|
| `game_status` | Check if game is running, get current scene |
| `game_scene` | Get current scene name and tactical status |
| `game_actors` | List all actors on the tactical map |
| `game_actor` | Get detailed info about an actor (or active actor) |
| `game_tactical` | Get tactical state (round, faction, unit counts) |
| `game_templates` | List templates loaded in game |
| `game_template` | Inspect a specific template in game |
| `game_inventory` | Get active actor's inventory |
| `game_operation` | Get current operation info |
| `game_blackmarket` | Get black market inventory |
| `game_roster` | Get player's hired leaders |
| `game_tilemap` | Get tilemap dimensions and fog of war status |
| `game_errors` | Get mod errors logged during session |

### Tactical Analysis Tools (require game running)

| Tool | Description |
|------|-------------|
| `game_los` | Check line of sight between positions or actors |
| `game_cover` | Get cover values at a tile (None, Half, Full per direction) |
| `game_tile` | Get full tile info (elevation, blocking, occupant, cover) |
| `game_movement` | Check path to destination, estimate AP cost |
| `game_reachable` | Get all tiles reachable within AP budget |
| `game_visibility` | Get actor visibility info (vision, detection, concealment) |
| `game_threats` | Analyze threats to actor (enemies that can see/attack) |
| `game_hitchance` | Calculate hit chance using game's actual combat formula |

---

## Usage Examples

### Creating a Modpack

```
Create a new modpack called "BetterWeapons" by me with description "Rebalances weapon damage"
```

Claude will use `modpack_create` to create the modpack structure.

### Modifying Templates

```
In the BetterWeapons modpack, increase the damage of the assault_rifle weapon template by 20%
```

Claude will:
1. Use `template_get` to read the current weapon stats
2. Use `template_patch` to apply the damage increase

### Checking Game State

```
What enemies are currently on the tactical map?
```

Claude will use `game_actors` to list all actors with faction filter for enemies.

### Debugging Mods

```
Are there any mod errors in the current game session?
```

Claude will use `game_errors` to fetch the error log.

### Full Development Workflow

```
1. Create a modpack that doubles the health of all pirate units
2. Compile it and check for errors
3. Deploy it to the game
```

Claude will orchestrate multiple tools:
1. `modpack_create` - Create the modpack
2. `template_list` with type EntityTemplate - Find pirate templates
3. `template_patch` - Double the health values
4. `compile_modpack` - Build and check for errors
5. `deploy_modpack` - Deploy to game

---

## Game Server Details

The in-game MCP server (GameMcpServer) runs on `http://127.0.0.1:7655`. It is **enabled by default** but can be controlled via settings.

### Enabling/Disabling the Server

The MCP server can be controlled in two ways:

**Via DevConsole Settings:**
1. Press `~` to open the DevConsole
2. Go to **Settings** tab
3. Find **MCP Server** section
4. Toggle **Enable MCP Server** on/off

The server starts/stops immediately when you change the setting.

**Via Console Commands:**
```
mcp          # Show server status
mcp start    # Start the server
mcp stop     # Stop the server
```

### Why Make It Optional?

The MCP server is entirely optional. Some users may prefer:
- Not running additional network services
- Reduced resource usage when AI assistance isn't needed
- Explicit control over when external tools can query game state

The server only binds to localhost (127.0.0.1) and cannot be accessed from other machines.

### Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/status` | GET | Server status, version, current scene |
| `/scene` | GET | Current scene name and tactical flag |
| `/actors` | GET | List actors, optional `?faction=N` filter |
| `/actor` | GET | Actor details, optional `?name=X` or active actor |
| `/templates` | GET | List templates, `?type=EntityTemplate` |
| `/template` | GET | Template details, `?type=X&name=Y&field=Z` |
| `/tactical` | GET | Tactical state (round, faction, counts) |
| `/errors` | GET | Mod error log |
| `/inventory` | GET | Active actor's inventory |
| `/operation` | GET | Current operation info |
| `/blackmarket` | GET | Black market items |
| `/roster` | GET | Hired leaders |
| `/tilemap` | GET | Map dimensions and fog of war |
| `/los` | GET | Line of sight check, `?from_x=&from_y=&to_x=&to_y=` or `?actor=&target=` |
| `/cover` | GET | Cover values, `?x=&y=` or `?actor=`, optional `?direction=` |
| `/tile` | GET | Full tile info, `?x=&y=` |
| `/movement` | GET | Path check, `?x=&y=`, optional `?actor=` |
| `/reachable` | GET | Reachable tiles, optional `?actor=&ap=` |
| `/visibility` | GET | Visibility info, optional `?actor=` |
| `/threats` | GET | Threat analysis, optional `?actor=` |
| `/hitchance` | GET | Hit chance calculation, `?attacker=&target=&skill=` |

### Direct HTTP Access

You can also query the game server directly (useful for debugging):

```bash
# Check if game is running
curl http://127.0.0.1:7655/status

# List all actors
curl http://127.0.0.1:7655/actors

# Get tactical state
curl http://127.0.0.1:7655/tactical

# List EntityTemplates
curl "http://127.0.0.1:7655/templates?type=EntityTemplate"
```

### Response Format

All endpoints return JSON:

```json
{
  "running": true,
  "scene": "TacticalScene",
  "version": "1.0.0",
  "time": "2026-02-10T12:34:56.789Z"
}
```

Error responses include an `error` field:

```json
{
  "error": "No active actor"
}
```

---

## Troubleshooting

### "Game not running" errors

The game bridge tools require the game to be running with ModpackLoader installed. If you see this error:
1. Launch the game through Steam (with MelonLoader)
2. Wait for the game to fully load
3. Try the tool again

### MCP server not appearing in Claude Code

1. Check the configuration file path is correct
2. Verify `dotnet` is in your PATH
3. Check Claude Code logs for connection errors
4. Try running the server manually: `dotnet run --project src/Menace.Modkit.Mcp`

### Template changes not appearing in game

1. Use `compile_modpack` to rebuild after changes
2. Use `deploy_modpack` to deploy the updated modpack
3. Restart the game (most template changes require a restart)

### Port 7655 already in use

If another process is using port 7655, the in-game server won't start. Check:
```bash
# Linux/Mac
lsof -i :7655

# Windows
netstat -ano | findstr 7655
```

---

## Security Considerations

1. **Localhost only** - The game HTTP server binds to 127.0.0.1 only, not accessible from network
2. **Path validation** - All file operations validate paths stay within modpack directories
3. **Security scanning** - Source code is scanned before compilation (SecurityScanner)
4. **No arbitrary execution** - The game server exposes read-only queries, no arbitrary code execution

---

## Developer Notes

### Adding New Game Endpoints

To add a new endpoint to the game server:

1. Add the route in `GameMcpServer.cs`:
```csharp
var result = path switch
{
    // ... existing routes
    "/myendpoint" => HandleMyEndpoint(request),
    _ => new { error = "Unknown endpoint", path }
};
```

2. Implement the handler:
```csharp
private static object HandleMyEndpoint(HttpListenerRequest request)
{
    // Use SDK classes to get data
    return new { /* JSON response */ };
}
```

3. Add the corresponding MCP tool in `GameTools.cs`:
```csharp
[McpServerTool(Name = "game_myendpoint", ReadOnly = true)]
[Description("Description of what this does")]
public static async Task<string> GameMyEndpoint()
{
    return await FetchFromGame("/myendpoint");
}
```

### Adding New Modkit Tools

To add a new design-time tool:

1. Create or update a file in `src/Menace.Modkit.Mcp/Tools/`
2. Add the tool method with attributes:
```csharp
[McpServerTool(Name = "my_tool", ReadOnly = true)]
[Description("What this tool does")]
public static string MyTool(
    ModpackManager modpackManager,  // Injected service
    [Description("Parameter description")] string param)
{
    // Implementation
    return JsonSerializer.Serialize(result, JsonOptions);
}
```

Tools are auto-discovered via `WithToolsFromAssembly()` in `Program.cs`.
