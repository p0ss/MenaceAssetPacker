# Menace.Modkit.Mcp - Maintainer Documentation

## Overview

**Menace.Modkit.Mcp** is a Model Context Protocol (MCP) server that exposes the Menace Modkit's functionality to Claude and other MCP-compatible clients. It enables AI-assisted modpack development, game analysis, and UI automation.

**Location:** `src/Menace.Modkit.Mcp/`

## Architecture

### Key Characteristics
- Standalone executable using .NET 10
- Runs with stdio transport (stdin/stdout for MCP protocol)
- Exposes ~60+ MCP tools and 5 resource types
- Integrates with App services and game runtime

### Directory Structure

```
src/Menace.Modkit.Mcp/
├── Program.cs                    # Entry point and DI setup
├── Menace.Modkit.Mcp.csproj
├── Tools/                        # MCP tool implementations
│   ├── CompilationTools.cs       # Compile & security scan
│   ├── DeploymentTools.cs        # Deploy/undeploy
│   ├── SourceTools.cs            # C# source files
│   ├── AssetTools.cs             # Asset management
│   ├── TemplateTools.cs          # Game data editing
│   ├── CloneTools.cs             # Cloning wizard
│   ├── ModpackTools.cs           # Modpack CRUD
│   ├── InfoTools.cs              # Environment info
│   ├── GameTools.cs              # Game runtime (HTTP proxy)
│   └── UITools.cs                # Modkit UI (HTTP proxy)
└── Resources/                    # MCP resource providers
    ├── ModpackResource.cs        # modkit://modpacks/*
    ├── TemplateResource.cs       # modkit://templates/*
    └── SchemaResource.cs         # modkit://schema/*
```

## Entry Point

**Location:** `Program.cs:1-36`

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Log to stderr (stdout reserved for MCP protocol)
builder.Logging.AddConsole(options => {
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// Register services from App layer
builder.Services.AddSingleton<ModpackManager>();
builder.Services.AddSingleton<CompilationService>();
builder.Services.AddSingleton(sp => new DeployManager(sp.GetRequiredService<ModpackManager>()));
builder.Services.AddSingleton<SecurityScanner>();

// Register MCP server
builder.Services
    .AddMcpServer(options => {
        options.ServerInfo = new() {
            Name = "menace-modkit",
            Version = Menace.ModkitVersion.MelonVersion
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();  // Auto-discovers tools

await builder.Build().RunAsync();
```

## Tools Overview (60+ tools)

### Information Tools (`InfoTools.cs`)
| Tool | Flags | Description |
|------|-------|-------------|
| `modkit_info` | ReadOnly | Environment info, paths, version |
| `game_status` | ReadOnly | Game installation status |

### Modpack Management (`ModpackTools.cs`)
| Tool | Flags | Description |
|------|-------|-------------|
| `modpack_list` | ReadOnly | List staging/deployed modpacks |
| `modpack_create` | - | Create new modpack |
| `modpack_get` | ReadOnly | Get modpack details |
| `modpack_delete` | Destructive | Delete modpack |
| `modpack_update` | - | Update metadata |

### Source Code (`SourceTools.cs`)
| Tool | Flags | Description |
|------|-------|-------------|
| `source_list` | ReadOnly | List C# source files |
| `source_read` | ReadOnly | Read source content |
| `source_write` | - | Write/update source |
| `source_add` | - | Add new source file |
| `source_delete` | Destructive | Delete source file |

### Assets (`AssetTools.cs`)
| Tool | Flags | Description |
|------|-------|-------------|
| `asset_list` | ReadOnly | List assets with grouping |
| `asset_info` | ReadOnly | Get asset details |
| `asset_delete` | Destructive | Delete asset file |
| `extraction_status` | ReadOnly | Check extraction status |

### Compilation & Security (`CompilationTools.cs`)
| Tool | Flags | Description |
|------|-------|-------------|
| `compile_modpack` | - | Compile C# sources to DLL |
| `security_scan` | ReadOnly | Scan for dangerous patterns |

### Deployment (`DeploymentTools.cs`)
| Tool | Flags | Description |
|------|-------|-------------|
| `deploy_modpack` | Destructive | Deploy single modpack |
| `deploy_all` | Destructive | Deploy all staging modpacks |
| `undeploy_all` | Destructive | Remove all deployed mods |
| `deploy_status` | ReadOnly | Get deployment state |

### Template Editing (`TemplateTools.cs`)
| Tool | Flags | Description |
|------|-------|-------------|
| `template_types` | ReadOnly | List template types |
| `template_list` | ReadOnly | List instances with filtering |
| `template_get` | ReadOnly | Get instance (vanilla or merged) |
| `template_set_field` | - | Set single field |
| `template_set_fields` | - | Set multiple fields |
| `template_patch` | - | Apply JSON patches |
| `template_reset` | Destructive | Reset to vanilla |
| `template_clone` | - | Create clone definition |

### Cloning Wizard (`CloneTools.cs`)
| Tool | Flags | Description |
|------|-------|-------------|
| `clone_analyze` | ReadOnly | Analyze dependencies |
| `clone_create` | - | Create clone with properties |
| `clone_set_asset` | - | Set asset on clone |
| `clone_list` | ReadOnly | List clones by type |
| `clone_delete` | Destructive | Delete clone |

### Game Runtime (`GameTools.cs`)
**HTTP Proxy to localhost:7655** (requires game running)

| Tool | Description |
|------|-------------|
| `game_status` | Running status, scene, version |
| `game_templates` | List loaded templates |
| `game_actors` | Tactical scene entities |
| `game_tactical` | Round, faction turn |
| `game_los` | Line of sight |
| `game_cover` | Cover values |
| `game_movement` | Movability and AP cost |
| `game_hitchance` | Combat hit calculation |
| `game_ai` | AI decision data |
| `game_repl` | Execute C# in game |

### Modkit UI (`UITools.cs`)
**HTTP Proxy to localhost:21421** (requires app running)

| Tool | Description |
|------|-------------|
| `modkit_ui` | Current UI state |
| `modkit_navigate` | Navigate to section |
| `modkit_select` | Select item |
| `modkit_set_field` | Set field on template |
| `modkit_click` | Click button/action |
| `modkit_actions` | Available actions |

## MCP Resources

### ModpackResource (`modkit://modpacks/*`)
- `modkit://modpacks` - List all modpacks
- `modkit://modpack/{name}/manifest` - Get manifest
- `modkit://modpack/{name}/patches/{type}` - Get patches
- `modkit://modpack/{name}/sources` - List sources
- `modkit://modpack/{name}/assets` - List assets

### TemplateResource (`modkit://templates/*`)
- `modkit://templates/{type}` - List instances
- `modkit://templates/{type}/{instance}` - Get instance

### SchemaResource (`modkit://schema/*`)
- `modkit://schema/{type}` - Get field definitions

## Tool Implementation Pattern

```csharp
[McpServerToolType]
public static class XxxTools
{
    [McpServerTool(Name = "tool_name", ReadOnly = true, Destructive = false)]
    [Description("Human-readable description")]
    public static async Task<string> MethodName(
        ServiceType service,
        [Description("param description")] string param1,
        CancellationToken cancellationToken = default)
    {
        // Implementation
        return JsonSerializer.Serialize(new { success = true }, JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
```

## Integration Architecture

```
Claude/AI Client
      ↓
    MCP Protocol (stdio)
      ↓
  Menace.Modkit.Mcp
      ├─→ App Services (ModpackManager, CompilationService, etc.)
      ├─→ Game HTTP Server (localhost:7655)
      │   └─→ In-game ModpackLoader
      └─→ Modkit UI Server (localhost:21421)
          └─→ Desktop app UI
```

## Workflow Examples

### Create and Deploy Modpack
```
1. modpack_create → creates modpack directory
2. source_add → scaffolds C# file
3. source_write → writes implementation
4. compile_modpack → compiles to DLL
5. security_scan → checks for issues
6. deploy_modpack → deploys to game
7. game_status → verifies mod loaded
```

### Clone and Modify Entity
```
1. clone_analyze → discovers dependencies
2. clone_create → creates clone
3. clone_set_asset → sets custom asset
4. template_patch → applies modifications
5. deploy_all → redeploys
6. game_template → verifies in-game
```

## Dependencies

### NuGet Packages
- `ModelContextProtocol` (0.8.0-preview.1) - MCP server
- `Microsoft.Extensions.Hosting` (8.0.0) - DI and hosting

### Services (from App)
- `ModpackManager` - Modpack management
- `CompilationService` - C# compilation
- `DeployManager` - Deployment
- `SecurityScanner` - Code scanning

## Running

```bash
# Build
dotnet build src/Menace.Modkit.Mcp/Menace.Modkit.Mcp.csproj

# Run (reads MCP from stdin, writes to stdout, logs to stderr)
./bin/Debug/net10.0/Menace.Modkit.Mcp.exe
```

### MCP Client Configuration
```json
{
  "name": "menace-modkit",
  "command": "/path/to/Menace.Modkit.Mcp.exe"
}
```

## Notes

- No TODOs, FIXMEs, or documented issues found
- HTTP proxies timeout after 5 seconds
- Path traversal protection via simple checks
- All JSON output uses camelCase
