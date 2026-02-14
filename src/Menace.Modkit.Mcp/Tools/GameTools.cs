using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace Menace.Modkit.Mcp.Tools;

/// <summary>
/// MCP tools that proxy to the game's MCP HTTP server (localhost:7655).
/// These tools require the game to be running with the ModpackLoader installed.
/// </summary>
[McpServerToolType]
public static class GameTools
{
    private const string GameServerUrl = "http://127.0.0.1:7655";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool(Name = "game_status", ReadOnly = true)]
    [Description("Check if the game is running with ModpackLoader and get the current scene. Returns running status, scene name, version, and timestamp.")]
    public static async Task<string> GameStatus()
    {
        return await FetchFromGame("/status");
    }

    [McpServerTool(Name = "game_scene", ReadOnly = true)]
    [Description("Get the current game scene name and whether it's a tactical scene.")]
    public static async Task<string> GameScene()
    {
        return await FetchFromGame("/scene");
    }

    [McpServerTool(Name = "game_actors", ReadOnly = true)]
    [Description("List all actors (entities) in the current tactical scene. Can optionally filter by faction.")]
    public static async Task<string> GameActors(
        [Description("Optional faction index to filter by (0=player, 1=enemy, etc.)")] int? faction = null)
    {
        var query = faction.HasValue ? $"?faction={faction.Value}" : "";
        return await FetchFromGame($"/actors{query}");
    }

    [McpServerTool(Name = "game_actor", ReadOnly = true)]
    [Description("Get detailed information about a specific actor by name, or the currently active actor if no name provided.")]
    public static async Task<string> GameActor(
        [Description("The name of the actor to look up. If omitted, returns the active actor.")] string? name = null)
    {
        var query = string.IsNullOrEmpty(name) ? "" : $"?name={Uri.EscapeDataString(name)}";
        return await FetchFromGame($"/actor{query}");
    }

    [McpServerTool(Name = "game_tactical", ReadOnly = true)]
    [Description("Get the current tactical state including round number, faction turn, active actor, and unit counts.")]
    public static async Task<string> GameTactical()
    {
        return await FetchFromGame("/tactical");
    }

    [McpServerTool(Name = "game_templates", ReadOnly = true)]
    [Description("List all templates of a given type currently loaded in the game.")]
    public static async Task<string> GameTemplates(
        [Description("The template type to list (e.g., 'EntityTemplate', 'WeaponTemplate', 'SkillTemplate')")] string type = "EntityTemplate")
    {
        return await FetchFromGame($"/templates?type={Uri.EscapeDataString(type)}");
    }

    [McpServerTool(Name = "game_template", ReadOnly = true)]
    [Description("Get information about a specific template instance. Can optionally read a specific field.")]
    public static async Task<string> GameTemplate(
        [Description("The name of the template instance (e.g., 'enemy.pirate_grunt')")] string name,
        [Description("The template type (e.g., 'EntityTemplate')")] string type = "EntityTemplate",
        [Description("Optional specific field to read from the template")] string? field = null)
    {
        var query = $"?type={Uri.EscapeDataString(type)}&name={Uri.EscapeDataString(name)}";
        if (!string.IsNullOrEmpty(field))
            query += $"&field={Uri.EscapeDataString(field)}";
        return await FetchFromGame($"/template{query}");
    }

    [McpServerTool(Name = "game_inventory", ReadOnly = true)]
    [Description("Get the inventory of the currently active actor, including items and equipped weapons.")]
    public static async Task<string> GameInventory()
    {
        return await FetchFromGame("/inventory");
    }

    [McpServerTool(Name = "game_operation", ReadOnly = true)]
    [Description("Get information about the current operation (mission series) including planet, missions, and time remaining.")]
    public static async Task<string> GameOperation()
    {
        return await FetchFromGame("/operation");
    }

    [McpServerTool(Name = "game_blackmarket", ReadOnly = true)]
    [Description("Get black market inventory showing available items, prices, and expiration.")]
    public static async Task<string> GameBlackMarket()
    {
        return await FetchFromGame("/blackmarket");
    }

    [McpServerTool(Name = "game_roster", ReadOnly = true)]
    [Description("Get the player's roster of hired leaders including their status, health, and squad size.")]
    public static async Task<string> GameRoster()
    {
        return await FetchFromGame("/roster");
    }

    [McpServerTool(Name = "game_tilemap", ReadOnly = true)]
    [Description("Get information about the current tactical tilemap including dimensions and fog of war status.")]
    public static async Task<string> GameTileMap()
    {
        return await FetchFromGame("/tilemap");
    }

    [McpServerTool(Name = "game_errors", ReadOnly = true)]
    [Description("Get all mod errors that have been logged during the game session.")]
    public static async Task<string> GameErrors()
    {
        return await FetchFromGame("/errors");
    }

    // ==================== Tactical Analysis Tools ====================

    [McpServerTool(Name = "game_los", ReadOnly = true)]
    [Description("Check line of sight between two positions or actors. Returns whether there is clear LOS and the distance.")]
    public static async Task<string> GameLOS(
        [Description("Source actor name (alternative to from_x/from_y)")] string? actor = null,
        [Description("Target actor name (alternative to to_x/to_y)")] string? target = null,
        [Description("Source X coordinate")] int? from_x = null,
        [Description("Source Y coordinate")] int? from_y = null,
        [Description("Destination X coordinate")] int? to_x = null,
        [Description("Destination Y coordinate")] int? to_y = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(actor)) query.Add($"actor={Uri.EscapeDataString(actor)}");
        if (!string.IsNullOrEmpty(target)) query.Add($"target={Uri.EscapeDataString(target)}");
        if (from_x.HasValue) query.Add($"from_x={from_x.Value}");
        if (from_y.HasValue) query.Add($"from_y={from_y.Value}");
        if (to_x.HasValue) query.Add($"to_x={to_x.Value}");
        if (to_y.HasValue) query.Add($"to_y={to_y.Value}");

        var queryString = query.Count > 0 ? "?" + string.Join("&", query) : "";
        return await FetchFromGame($"/los{queryString}");
    }

    [McpServerTool(Name = "game_cover", ReadOnly = true)]
    [Description("Get cover values at a tile position or for an actor. Returns cover type (None, Half, Full) for each direction.")]
    public static async Task<string> GameCover(
        [Description("Actor name to get cover for (uses actor's position)")] string? actor = null,
        [Description("X coordinate of tile")] int? x = null,
        [Description("Y coordinate of tile")] int? y = null,
        [Description("Specific direction (0-7, 0=North clockwise). If omitted, returns all directions.")] int? direction = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(actor)) query.Add($"actor={Uri.EscapeDataString(actor)}");
        if (x.HasValue) query.Add($"x={x.Value}");
        if (y.HasValue) query.Add($"y={y.Value}");
        if (direction.HasValue) query.Add($"direction={direction.Value}");

        var queryString = query.Count > 0 ? "?" + string.Join("&", query) : "";
        return await FetchFromGame($"/cover{queryString}");
    }

    [McpServerTool(Name = "game_tile", ReadOnly = true)]
    [Description("Get detailed information about a specific tile including elevation, blocking status, occupant, and cover in all directions.")]
    public static async Task<string> GameTile(
        [Description("X coordinate of tile")] int x,
        [Description("Y coordinate of tile")] int y)
    {
        return await FetchFromGame($"/tile?x={x}&y={y}");
    }

    [McpServerTool(Name = "game_movement", ReadOnly = true)]
    [Description("Check if an actor can move to a destination and estimate the AP cost. Returns path validity, surface type, and whether actor can afford the move.")]
    public static async Task<string> GameMovement(
        [Description("Destination X coordinate")] int x,
        [Description("Destination Y coordinate")] int y,
        [Description("Actor name (defaults to active actor)")] string? actor = null)
    {
        var query = $"x={x}&y={y}";
        if (!string.IsNullOrEmpty(actor))
            query += $"&actor={Uri.EscapeDataString(actor)}";
        return await FetchFromGame($"/movement?{query}");
    }

    [McpServerTool(Name = "game_reachable", ReadOnly = true)]
    [Description("Get all tiles reachable by an actor within their current AP (or specified AP budget). Returns list of tiles with movement costs.")]
    public static async Task<string> GameReachable(
        [Description("Actor name (defaults to active actor)")] string? actor = null,
        [Description("Maximum AP to spend (defaults to actor's current AP)")] int? ap = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(actor)) query.Add($"actor={Uri.EscapeDataString(actor)}");
        if (ap.HasValue) query.Add($"ap={ap.Value}");

        var queryString = query.Count > 0 ? "?" + string.Join("&", query) : "";
        return await FetchFromGame($"/reachable{queryString}");
    }

    [McpServerTool(Name = "game_visibility", ReadOnly = true)]
    [Description("Get visibility information for an actor including vision range, detection, concealment, and current visibility state.")]
    public static async Task<string> GameVisibility(
        [Description("Actor name (defaults to active actor)")] string? actor = null)
    {
        var query = string.IsNullOrEmpty(actor) ? "" : $"?actor={Uri.EscapeDataString(actor)}";
        return await FetchFromGame($"/visibility{query}");
    }

    [McpServerTool(Name = "game_threats", ReadOnly = true)]
    [Description("Analyze threats to an actor. Returns list of enemies with their positions, distances, whether they can see the actor, their attack range, and whether they can currently attack.")]
    public static async Task<string> GameThreats(
        [Description("Actor name to analyze threats for (defaults to active actor)")] string? actor = null)
    {
        var query = string.IsNullOrEmpty(actor) ? "" : $"?actor={Uri.EscapeDataString(actor)}";
        return await FetchFromGame($"/threats{query}");
    }

    [McpServerTool(Name = "game_hitchance", ReadOnly = true)]
    [Description("Calculate hit chance for an attack. Uses the game's actual combat calculation including accuracy, cover, dodge, and distance penalties. If no target specified, returns hit chances against all valid enemies.")]
    public static async Task<string> GameHitChance(
        [Description("Attacker name (defaults to active actor)")] string? attacker = null,
        [Description("Target name. If omitted, returns chances against all enemies.")] string? target = null,
        [Description("Skill name to use (defaults to primary attack skill)")] string? skill = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(attacker)) query.Add($"attacker={Uri.EscapeDataString(attacker)}");
        if (!string.IsNullOrEmpty(target)) query.Add($"target={Uri.EscapeDataString(target)}");
        if (!string.IsNullOrEmpty(skill)) query.Add($"skill={Uri.EscapeDataString(skill)}");

        var queryString = query.Count > 0 ? "?" + string.Join("&", query) : "";
        return await FetchFromGame($"/hitchance{queryString}");
    }

    [McpServerTool(Name = "game_ai", ReadOnly = true)]
    [Description("Get AI decision-making info for enemy units. Shows what the AI is planning: selected behavior, target, tile scores. If no actor specified, returns AI intent for all enemy units.")]
    public static async Task<string> GameAI(
        [Description("Actor name. If omitted, returns all enemy AI states.")] string? actor = null,
        [Description("Info type: 'intent' (default), 'role' (AI config), 'behaviors' (all behaviors), 'tiles' (scored positions)")] string? type = null,
        [Description("Number of tiles to return (for type=tiles, default 10)")] int? count = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(actor)) query.Add($"actor={Uri.EscapeDataString(actor)}");
        if (!string.IsNullOrEmpty(type)) query.Add($"type={Uri.EscapeDataString(type)}");
        if (count.HasValue) query.Add($"count={count.Value}");

        var queryString = query.Count > 0 ? "?" + string.Join("&", query) : "";
        return await FetchFromGame($"/ai{queryString}");
    }

    [McpServerTool(Name = "game_ui", ReadOnly = true)]
    [Description("Get the current game UI state. Lists visible buttons, text, toggles, dropdowns, etc. Use this to see what menus/screens are showing and what can be clicked.")]
    public static async Task<string> GameUI()
    {
        return await FetchFromGame("/ui");
    }

    [McpServerTool(Name = "game_ui_diag", ReadOnly = true)]
    [Description("Get UI inspector diagnostic information including resolved types, assemblies, and canvas detection status. Useful for debugging UI inspection issues.")]
    public static async Task<string> GameUIDiag()
    {
        return await FetchFromGame("/ui-diag");
    }

    [McpServerTool(Name = "game_logs", ReadOnly = true)]
    [Description("Read recent game logs (MelonLoader/Latest.log). Useful for checking errors, mod loading, and debugging.")]
    public static async Task<string> GameLogs(
        [Description("Number of lines to return (default 100, max 1000)")] int? lines = null,
        [Description("Filter logs to lines containing this text")] string? filter = null)
    {
        var query = new List<string>();
        if (lines.HasValue) query.Add($"lines={lines.Value}");
        if (!string.IsNullOrEmpty(filter)) query.Add($"filter={Uri.EscapeDataString(filter)}");

        var queryString = query.Count > 0 ? "?" + string.Join("&", query) : "";
        return await FetchFromGame($"/logs{queryString}");
    }

    [McpServerTool(Name = "game_click", Destructive = false)]
    [Description("Click a button in the game UI. Can find buttons by path or name/text.")]
    public static async Task<string> GameClick(
        [Description("Full path to the button (from game_ui output)")] string? path = null,
        [Description("Button name or text label to find and click")] string? name = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(path)) query.Add($"path={Uri.EscapeDataString(path)}");
        if (!string.IsNullOrEmpty(name)) query.Add($"name={Uri.EscapeDataString(name)}");

        var queryString = query.Count > 0 ? "?" + string.Join("&", query) : "";
        return await FetchFromGame($"/click{queryString}");
    }

    [McpServerTool(Name = "game_repl", Destructive = false)]
    [Description("Execute C# code in the running game using Roslyn. Returns the evaluation result. Useful for inspecting game state, testing SDK code, and debugging without game restarts.")]
    public static async Task<string> GameRepl(
        [Description("C# code to evaluate. Can be expressions (return value) or statements.")] string code)
    {
        return await PostToGame("/repl", new { code });
    }

    /// <summary>
    /// Fetch data from the game's MCP HTTP server.
    /// Returns the JSON response or an error object if the game is not running.
    /// </summary>
    private static async Task<string> FetchFromGame(string endpoint)
    {
        try
        {
            var response = await HttpClient.GetAsync($"{GameServerUrl}{endpoint}");
            var json = await response.Content.ReadAsStringAsync();

            // Parse and re-serialize to ensure consistent formatting
            var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, JsonOptions);
        }
        catch (HttpRequestException)
        {
            return JsonSerializer.Serialize(new
            {
                error = "Game not running",
                message = "The game is not running or ModpackLoader is not installed. Start the game with MelonLoader to use game_ tools."
            }, JsonOptions);
        }
        catch (TaskCanceledException)
        {
            return JsonSerializer.Serialize(new
            {
                error = "Request timeout",
                message = "The game did not respond in time. It may be loading or frozen."
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = "Connection failed",
                message = ex.Message
            }, JsonOptions);
        }
    }

    /// <summary>
    /// POST data to the game's MCP HTTP server.
    /// Returns the JSON response or an error object if the game is not running.
    /// </summary>
    private static async Task<string> PostToGame(string endpoint, object data)
    {
        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(data, JsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");
            var response = await HttpClient.PostAsync($"{GameServerUrl}{endpoint}", content);
            var json = await response.Content.ReadAsStringAsync();

            // Parse and re-serialize to ensure consistent formatting
            var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, JsonOptions);
        }
        catch (HttpRequestException)
        {
            return JsonSerializer.Serialize(new
            {
                error = "Game not running",
                message = "The game is not running or ModpackLoader is not installed. Start the game with MelonLoader to use game_ tools."
            }, JsonOptions);
        }
        catch (TaskCanceledException)
        {
            return JsonSerializer.Serialize(new
            {
                error = "Request timeout",
                message = "The game did not respond in time. It may be loading or frozen."
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = "Connection failed",
                message = ex.Message
            }, JsonOptions);
        }
    }
}
