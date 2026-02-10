#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using MelonLoader;
using Menace.SDK;

namespace Menace.ModpackLoader.Mcp;

/// <summary>
/// Lightweight HTTP server that exposes SDK functionality for MCP integration.
/// Runs inside the game process and listens on localhost:7655.
///
/// Controlled via ModSettings: DevConsole (~) -> Settings -> MCP Server
/// </summary>
public static class GameMcpServer
{
    private static HttpListener _listener;
    private static Thread _serverThread;
    private static bool _running;
    private static MelonLogger.Instance _log;
    private static bool _initialized;

    public const int PORT = 7655;
    public const string BASE_URL = "http://127.0.0.1:7655/";
    private const string SETTINGS_NAME = "MCP Server";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Whether the MCP server is currently running.
    /// </summary>
    public static bool IsRunning => _running;

    /// <summary>
    /// Initialize the MCP server settings and optionally start if enabled.
    /// Call this once during mod initialization.
    /// </summary>
    public static void Initialize(MelonLogger.Instance logger)
    {
        if (_initialized) return;
        _initialized = true;
        _log = logger;

        // Register settings
        RegisterSettings();

        // Register console commands
        RegisterCommands();

        // Start if enabled
        if (IsEnabled())
        {
            Start();
        }
        else
        {
            _log.Msg("[GameMcp] Server disabled in settings (enable in DevConsole -> Settings -> MCP Server)");
        }
    }

    private static void RegisterSettings()
    {
        ModSettings.Register(SETTINGS_NAME, settings =>
        {
            settings.AddToggle("Enabled", "Enable MCP Server", true);
            settings.AddHeader("Server Info");
            settings.AddInfo("Status", "Status", () => _running ? "Running" : "Stopped");
            settings.AddInfo("URL", "URL", BASE_URL);
        });

        // Listen for setting changes
        ModSettings.OnSettingChanged += OnSettingChanged;
    }

    private static void OnSettingChanged(string modName, string key, object value)
    {
        if (modName != SETTINGS_NAME) return;

        if (key == "Enabled" && value is bool enabled)
        {
            if (enabled && !_running)
            {
                Start();
            }
            else if (!enabled && _running)
            {
                Stop();
            }
        }
    }

    private static bool IsEnabled()
    {
        return ModSettings.Get<bool>(SETTINGS_NAME, "Enabled");
    }

    private static void RegisterCommands()
    {
        // mcp - Show MCP server status
        DevConsole.RegisterCommand("mcp", "", "Show MCP server status", _ =>
        {
            var status = _running ? "RUNNING" : "STOPPED";
            var enabled = IsEnabled() ? "enabled" : "disabled";
            return $"MCP Server: {status}\n" +
                   $"  URL: {BASE_URL}\n" +
                   $"  Setting: {enabled}\n" +
                   $"\nUse 'mcp start' or 'mcp stop' to control manually.\n" +
                   $"Configure in DevConsole -> Settings -> MCP Server";
        });

        // mcp start - Start the server
        DevConsole.RegisterCommand("mcp start", "", "Start the MCP server", _ =>
        {
            if (_running)
                return "MCP server is already running";

            Start();
            return _running ? $"MCP server started on {BASE_URL}" : "Failed to start MCP server";
        });

        // mcp stop - Stop the server
        DevConsole.RegisterCommand("mcp stop", "", "Stop the MCP server", _ =>
        {
            if (!_running)
                return "MCP server is not running";

            Stop();
            return "MCP server stopped";
        });
    }

    /// <summary>
    /// Start the MCP HTTP server.
    /// </summary>
    public static void Start(MelonLogger.Instance logger)
    {
        _log = logger;
        Start();
    }

    /// <summary>
    /// Start the MCP HTTP server.
    /// </summary>
    public static void Start()
    {
        if (_running)
        {
            _log?.Warning("[GameMcp] Server already running");
            return;
        }

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(BASE_URL);
            _listener.Start();
            _running = true;

            _serverThread = new Thread(ServerLoop)
            {
                IsBackground = true,
                Name = "GameMcpServer"
            };
            _serverThread.Start();

            _log?.Msg($"[GameMcp] Server started on {BASE_URL}");
        }
        catch (Exception ex)
        {
            _log?.Error($"[GameMcp] Failed to start server: {ex.Message}");
        }
    }

    /// <summary>
    /// Stop the MCP HTTP server.
    /// </summary>
    public static void Stop()
    {
        if (!_running) return;

        _running = false;
        try
        {
            _listener?.Stop();
            _listener?.Close();
        }
        catch { }
        _listener = null;
        _log?.Msg("[GameMcp] Server stopped");
    }

    private static void ServerLoop()
    {
        while (_running)
        {
            try
            {
                var context = _listener.GetContext();
                ThreadPool.QueueUserWorkItem(HandleRequestCallback, context);
            }
            catch (HttpListenerException) when (!_running)
            {
                // Expected when stopping
            }
            catch (Exception ex)
            {
                if (_log != null) _log.Warning($"[GameMcp] Error accepting request: {ex.Message}");
            }
        }
    }

    private static void HandleRequestCallback(object state)
    {
        HandleRequest((HttpListenerContext)state);
    }

    private static void HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            // Route request
            var path = request.Url?.AbsolutePath?.ToLowerInvariant() ?? "/";
            var result = path switch
            {
                "/" or "/status" => HandleStatus(),
                "/scene" => HandleScene(),
                "/actors" => HandleActors(request),
                "/actor" => HandleActor(request),
                "/templates" => HandleTemplates(request),
                "/template" => HandleTemplate(request),
                "/tactical" => HandleTactical(),
                "/errors" => HandleErrors(),
                "/inventory" => HandleInventory(request),
                "/operation" => HandleOperation(),
                "/blackmarket" => HandleBlackMarket(),
                "/roster" => HandleRoster(),
                "/tilemap" => HandleTileMap(),
                // New tactical analysis endpoints
                "/los" => HandleLOS(request),
                "/cover" => HandleCover(request),
                "/tile" => HandleTile(request),
                "/movement" => HandleMovement(request),
                "/reachable" => HandleReachable(request),
                "/visibility" => HandleVisibility(request),
                "/threats" => HandleThreats(request),
                "/hitchance" => HandleHitChance(request),
                "/ai" => HandleAI(request),
                _ => new { error = "Unknown endpoint", path }
            };

            SendJson(response, result);
        }
        catch (Exception ex)
        {
            SendJson(response, new { error = ex.Message }, 500);
        }
    }

    // ==================== Handlers ====================

    private static object HandleStatus()
    {
        return new
        {
            running = true,
            scene = GameState.CurrentScene,
            version = Menace.ModkitVersion.MelonVersion,
            time = DateTime.Now.ToString("o")
        };
    }

    private static object HandleScene()
    {
        return new
        {
            scene = GameState.CurrentScene,
            isTactical = GameState.CurrentScene?.Contains("Tactical") ?? false
        };
    }

    private static object HandleActors(HttpListenerRequest request)
    {
        int.TryParse(request.QueryString["faction"], out int faction);
        var factionFilter = request.QueryString["faction"] != null ? faction : -1;

        var actors = EntitySpawner.ListEntities(factionFilter);
        var result = new List<object>();

        foreach (var actor in actors)
        {
            var info = EntitySpawner.GetEntityInfo(actor);
            var position = EntityMovement.GetPosition(actor);
            if (info != null)
            {
                result.Add(new
                {
                    name = info.Name,
                    typeName = info.TypeName,
                    faction = info.FactionIndex,
                    x = position?.x,
                    y = position?.y,
                    isAlive = info.IsAlive,
                    pointer = info.Pointer.ToString("X")
                });
            }
        }

        return new { count = result.Count, actors = result };
    }

    private static object HandleActor(HttpListenerRequest request)
    {
        var name = request.QueryString["name"];
        if (string.IsNullOrEmpty(name))
        {
            // Return active actor
            var active = TacticalController.GetActiveActor();
            if (active.IsNull)
                return new { error = "No active actor" };

            return GetActorDetails(active);
        }

        // Find by name
        var actors = EntitySpawner.ListEntities();
        foreach (var actor in actors)
        {
            if (actor.GetName()?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
                return GetActorDetails(actor);
        }

        return new { error = $"Actor '{name}' not found" };
    }

    private static object GetActorDetails(GameObj actor)
    {
        var info = EntitySpawner.GetEntityInfo(actor);
        var combat = EntityCombat.GetCombatInfo(actor);
        var movement = EntityMovement.GetMovementInfo(actor);
        var skills = EntityCombat.GetSkills(actor);

        return new
        {
            name = info?.Name,
            typeName = info?.TypeName,
            faction = info?.FactionIndex,
            position = movement?.Position != null ? new { x = movement.Position.Value.x, y = movement.Position.Value.y } : null,
            isAlive = info?.IsAlive,
            combat = combat != null ? new
            {
                hp = combat.CurrentHP,
                maxHp = combat.MaxHP,
                suppression = combat.Suppression,
                suppressionState = combat.SuppressionState,
                morale = combat.Morale,
                isStunned = combat.IsStunned
            } : null,
            movement = movement != null ? new
            {
                direction = movement.Direction,
                directionName = movement.DirectionName,
                ap = movement.CurrentAP,
                apAtTurnStart = movement.APAtTurnStart,
                isMoving = movement.IsMoving
            } : null,
            skills = skills.Select(s => new
            {
                name = s.Name,
                apCost = s.APCost,
                range = s.Range,
                canUse = s.CanUse
            }).ToList()
        };
    }

    private static object HandleTemplates(HttpListenerRequest request)
    {
        var typeName = request.QueryString["type"] ?? "EntityTemplate";
        var templates = Templates.FindAll(typeName);

        return new
        {
            type = typeName,
            count = templates.Length,
            templates = templates.Select(t => new
            {
                name = t.GetName(),
                pointer = t.Pointer.ToString("X")
            }).ToList()
        };
    }

    private static object HandleTemplate(HttpListenerRequest request)
    {
        var typeName = request.QueryString["type"] ?? "EntityTemplate";
        var name = request.QueryString["name"];

        if (string.IsNullOrEmpty(name))
            return new { error = "Missing 'name' parameter" };

        var template = Templates.Find(typeName, name);
        if (template.IsNull)
            return new { error = $"Template '{typeName}/{name}' not found" };

        // Read common fields
        var fields = new Dictionary<string, object>();
        var fieldName = request.QueryString["field"];
        if (!string.IsNullOrEmpty(fieldName))
        {
            var value = Templates.ReadField(template, fieldName);
            fields[fieldName] = value?.ToString() ?? "null";
        }

        return new
        {
            type = typeName,
            name = template.GetName(),
            pointer = template.Pointer.ToString("X"),
            fields
        };
    }

    private static object HandleTactical()
    {
        var state = TacticalController.GetTacticalState();
        if (state == null)
            return new { error = "Not in tactical scene" };

        return new
        {
            round = state.RoundNumber,
            faction = state.CurrentFaction,
            factionName = state.CurrentFactionName,
            isPlayerTurn = state.IsPlayerTurn,
            isPaused = state.IsPaused,
            timeScale = state.TimeScale,
            isMissionRunning = state.IsMissionRunning,
            activeActor = state.ActiveActorName,
            players = new { alive = state.PlayerAliveCount, dead = state.PlayerDeadCount },
            enemies = new { alive = state.EnemyAliveCount, dead = state.EnemyDeadCount }
        };
    }

    private static object HandleErrors()
    {
        var errors = ModError.RecentErrors;
        return new
        {
            count = errors.Count,
            errors = errors.Select(e => new
            {
                modId = e.ModId,
                severity = e.Severity.ToString(),
                message = e.Message,
                timestamp = e.Timestamp.ToString("o")
            }).ToList()
        };
    }

    private static object HandleInventory(HttpListenerRequest request)
    {
        var actor = TacticalController.GetActiveActor();
        if (actor.IsNull)
            return new { error = "No active actor" };

        var container = Inventory.GetContainer(actor);
        if (container.IsNull)
            return new { error = "No inventory container" };

        var items = Inventory.GetAllItems(container);
        var weapons = Inventory.GetEquippedWeapons(actor);

        return new
        {
            actorName = actor.GetName(),
            totalItems = items.Count,
            totalValue = Inventory.GetTotalTradeValue(container),
            items = items.Select(i => new
            {
                name = i.TemplateName,
                slot = i.SlotTypeName,
                value = i.TradeValue,
                rarity = i.Rarity
            }).ToList(),
            equippedWeapons = weapons.Select(w => w.TemplateName).ToList()
        };
    }

    private static object HandleOperation()
    {
        var info = Operation.GetOperationInfo();
        if (info == null)
            return new { active = false };

        return new
        {
            active = true,
            name = info.TemplateName,
            planet = info.Planet,
            enemyFaction = info.EnemyFaction,
            friendlyFaction = info.FriendlyFaction,
            currentMission = info.CurrentMissionIndex + 1,
            totalMissions = info.MissionCount,
            timeSpent = info.TimeSpent,
            timeLimit = info.TimeLimit,
            timeRemaining = info.TimeRemaining
        };
    }

    private static object HandleBlackMarket()
    {
        var info = BlackMarket.GetBlackMarketInfo();
        if (info == null)
            return new { available = false };

        var stacks = BlackMarket.GetAvailableStacks();
        return new
        {
            available = true,
            stackCount = info.StackCount,
            totalItems = info.TotalItemCount,
            stacks = stacks.Select(s => new
            {
                name = s.TemplateName,
                count = s.ItemCount,
                value = s.TradeValue,
                operationsRemaining = s.OperationsRemaining,
                type = s.TypeName
            }).ToList()
        };
    }

    private static object HandleRoster()
    {
        var leaders = Roster.GetHiredLeaders();
        return new
        {
            hiredCount = Roster.GetHiredCount(),
            availableCount = Roster.GetAvailableCount(),
            leaders = leaders.Select(l => new
            {
                name = l.Nickname,
                template = l.TemplateName,
                rank = l.RankName,
                status = l.StatusName,
                health = l.HealthPercent,
                isDeployable = l.IsDeployable,
                squadSize = l.SquaddieCount
            }).ToList()
        };
    }

    private static object HandleTileMap()
    {
        var info = TileMap.GetMapInfo();
        if (info == null)
            return new { available = false };

        return new
        {
            available = true,
            width = info.Width,
            height = info.Height,
            fogOfWar = info.UseFogOfWar
        };
    }

    // ==================== Tactical Analysis Handlers ====================

    private static object HandleLOS(HttpListenerRequest request)
    {
        // Check LOS between two tiles or from actor to tile/target
        var fromX = request.QueryString["from_x"];
        var fromY = request.QueryString["from_y"];
        var toX = request.QueryString["to_x"];
        var toY = request.QueryString["to_y"];
        var actorName = request.QueryString["actor"];
        var targetName = request.QueryString["target"];

        // Actor to target check
        if (!string.IsNullOrEmpty(actorName) && !string.IsNullOrEmpty(targetName))
        {
            var actor = FindActorByName(actorName);
            var target = FindActorByName(targetName);
            if (actor.IsNull) return new { error = $"Actor '{actorName}' not found" };
            if (target.IsNull) return new { error = $"Target '{targetName}' not found" };

            var canSee = LineOfSight.CanActorSee(actor, target);
            var actorPos = EntityMovement.GetPosition(actor);
            var targetPos = EntityMovement.GetPosition(target);
            var distance = actorPos.HasValue && targetPos.HasValue
                ? TileMap.GetDistance(actorPos.Value.x, actorPos.Value.y, targetPos.Value.x, targetPos.Value.y)
                : -1f;

            return new
            {
                hasLOS = canSee,
                actor = actorName,
                target = targetName,
                distance = distance
            };
        }

        // Actor to tile check
        if (!string.IsNullOrEmpty(actorName) && !string.IsNullOrEmpty(toX) && !string.IsNullOrEmpty(toY))
        {
            var actor = FindActorByName(actorName);
            if (actor.IsNull) return new { error = $"Actor '{actorName}' not found" };

            var actorPos = EntityMovement.GetPosition(actor);
            if (!actorPos.HasValue) return new { error = "Could not get actor position" };

            if (!int.TryParse(toX, out int tx) || !int.TryParse(toY, out int ty))
                return new { error = "Invalid to_x or to_y" };

            var hasLos = LineOfSight.HasLOS(actorPos.Value.x, actorPos.Value.y, tx, ty);
            var distance = TileMap.GetDistance(actorPos.Value.x, actorPos.Value.y, tx, ty);

            return new
            {
                hasLOS = hasLos,
                from = new { x = actorPos.Value.x, y = actorPos.Value.y },
                to = new { x = tx, y = ty },
                distance = distance
            };
        }

        // Tile to tile check
        if (!string.IsNullOrEmpty(fromX) && !string.IsNullOrEmpty(fromY) &&
            !string.IsNullOrEmpty(toX) && !string.IsNullOrEmpty(toY))
        {
            if (!int.TryParse(fromX, out int fx) || !int.TryParse(fromY, out int fy) ||
                !int.TryParse(toX, out int tx) || !int.TryParse(toY, out int ty))
                return new { error = "Invalid coordinates" };

            var hasLos = LineOfSight.HasLOS(fx, fy, tx, ty);
            var distance = TileMap.GetDistance(fx, fy, tx, ty);

            return new
            {
                hasLOS = hasLos,
                from = new { x = fx, y = fy },
                to = new { x = tx, y = ty },
                distance = distance
            };
        }

        return new { error = "Provide from_x/from_y/to_x/to_y, or actor/target, or actor/to_x/to_y" };
    }

    private static object HandleCover(HttpListenerRequest request)
    {
        var xStr = request.QueryString["x"];
        var yStr = request.QueryString["y"];
        var actorName = request.QueryString["actor"];
        var dirStr = request.QueryString["direction"];

        int x, y;

        // Get position from actor or coordinates
        if (!string.IsNullOrEmpty(actorName))
        {
            var actor = FindActorByName(actorName);
            if (actor.IsNull) return new { error = $"Actor '{actorName}' not found" };

            var pos = EntityMovement.GetPosition(actor);
            if (!pos.HasValue) return new { error = "Could not get actor position" };
            x = pos.Value.x;
            y = pos.Value.y;
        }
        else if (!string.IsNullOrEmpty(xStr) && !string.IsNullOrEmpty(yStr))
        {
            if (!int.TryParse(xStr, out x) || !int.TryParse(yStr, out y))
                return new { error = "Invalid x or y" };
        }
        else
        {
            return new { error = "Provide x/y coordinates or actor name" };
        }

        // Get cover for specific direction or all
        if (!string.IsNullOrEmpty(dirStr) && int.TryParse(dirStr, out int dir))
        {
            var cover = TileMap.GetCover(x, y, dir);
            return new
            {
                x = x,
                y = y,
                direction = dir,
                directionName = TileMap.GetDirectionName(dir),
                cover = cover,
                coverName = TileMap.GetCoverName(cover)
            };
        }

        // All directions
        var allCover = TileMap.GetAllCover(x, y);
        var coverList = new List<object>();
        for (int d = 0; d < 8; d++)
        {
            coverList.Add(new
            {
                direction = d,
                directionName = TileMap.GetDirectionName(d),
                cover = allCover[d],
                coverName = TileMap.GetCoverName(allCover[d])
            });
        }

        return new
        {
            x = x,
            y = y,
            covers = coverList
        };
    }

    private static object HandleTile(HttpListenerRequest request)
    {
        var xStr = request.QueryString["x"];
        var yStr = request.QueryString["y"];

        if (string.IsNullOrEmpty(xStr) || string.IsNullOrEmpty(yStr))
            return new { error = "Provide x and y coordinates" };

        if (!int.TryParse(xStr, out int x) || !int.TryParse(yStr, out int y))
            return new { error = "Invalid x or y" };

        var info = TileMap.GetTileInfo(x, y);
        if (info == null)
            return new { error = $"Tile at ({x}, {y}) not found" };

        var allCover = TileMap.GetAllCover(x, y);

        return new
        {
            x = info.X,
            y = info.Y,
            elevation = info.Elevation,
            isBlocked = info.IsBlocked,
            hasActor = info.HasActor,
            actorName = info.ActorName,
            isVisibleToPlayer = info.IsVisibleToPlayer,
            blocksLOS = info.BlocksLOS,
            hasEffects = info.HasEffects,
            cover = new
            {
                north = allCover[0],
                northeast = allCover[1],
                east = allCover[2],
                southeast = allCover[3],
                south = allCover[4],
                southwest = allCover[5],
                west = allCover[6],
                northwest = allCover[7]
            }
        };
    }

    private static object HandleMovement(HttpListenerRequest request)
    {
        var actorName = request.QueryString["actor"];
        var xStr = request.QueryString["x"];
        var yStr = request.QueryString["y"];

        GameObj actor;
        if (!string.IsNullOrEmpty(actorName))
        {
            actor = FindActorByName(actorName);
            if (actor.IsNull) return new { error = $"Actor '{actorName}' not found" };
        }
        else
        {
            actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return new { error = "No active actor" };
        }

        if (string.IsNullOrEmpty(xStr) || string.IsNullOrEmpty(yStr))
            return new { error = "Provide x and y destination coordinates" };

        if (!int.TryParse(xStr, out int goalX) || !int.TryParse(yStr, out int goalY))
            return new { error = "Invalid x or y" };

        var pos = EntityMovement.GetPosition(actor);
        if (!pos.HasValue)
            return new { error = "Could not get actor position" };

        // Check if can enter destination
        var canEnter = Pathfinding.CanEnter(actor, goalX, goalY);

        // Get movement cost for destination
        var moveCost = Pathfinding.GetMovementCost(actor, goalX, goalY);

        // Estimate total path cost
        var estimatedCost = Pathfinding.EstimateCost(pos.Value.x, pos.Value.y, goalX, goalY);

        // Check current AP
        var movementInfo = EntityMovement.GetMovementInfo(actor);
        var currentAP = movementInfo?.CurrentAP ?? 0;

        return new
        {
            actor = actor.GetName(),
            from = new { x = pos.Value.x, y = pos.Value.y },
            to = new { x = goalX, y = goalY },
            canEnter = canEnter,
            destinationCost = moveCost.TotalCost,
            surfaceType = moveCost.SurfaceTypeName,
            estimatedTotalCost = estimatedCost,
            currentAP = currentAP,
            canAfford = currentAP >= estimatedCost,
            distance = TileMap.GetDistance(pos.Value.x, pos.Value.y, goalX, goalY)
        };
    }

    private static object HandleReachable(HttpListenerRequest request)
    {
        var actorName = request.QueryString["actor"];
        var apStr = request.QueryString["ap"];

        GameObj actor;
        if (!string.IsNullOrEmpty(actorName))
        {
            actor = FindActorByName(actorName);
            if (actor.IsNull) return new { error = $"Actor '{actorName}' not found" };
        }
        else
        {
            actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return new { error = "No active actor" };
        }

        // Get max AP to use
        int maxAP;
        if (!string.IsNullOrEmpty(apStr) && int.TryParse(apStr, out int ap))
        {
            maxAP = ap;
        }
        else
        {
            var movementInfo = EntityMovement.GetMovementInfo(actor);
            maxAP = movementInfo?.CurrentAP ?? 50;
        }

        var reachable = Pathfinding.GetReachableTiles(actor, maxAP);

        return new
        {
            actor = actor.GetName(),
            maxAP = maxAP,
            tileCount = reachable.Count,
            tiles = reachable.Select(t => new { x = t.x, y = t.y, cost = t.cost }).ToList()
        };
    }

    private static object HandleVisibility(HttpListenerRequest request)
    {
        var actorName = request.QueryString["actor"];

        GameObj actor;
        if (!string.IsNullOrEmpty(actorName))
        {
            actor = FindActorByName(actorName);
            if (actor.IsNull) return new { error = $"Actor '{actorName}' not found" };
        }
        else
        {
            actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return new { error = "No active actor" };
        }

        var visInfo = LineOfSight.GetVisibilityInfo(actor);
        if (visInfo == null)
            return new { error = "Could not get visibility info" };

        var pos = EntityMovement.GetPosition(actor);

        return new
        {
            actor = actor.GetName(),
            position = pos.HasValue ? new { x = pos.Value.x, y = pos.Value.y } : null,
            state = visInfo.State,
            stateName = visInfo.StateName,
            isVisible = visInfo.IsVisible,
            isMarked = visInfo.IsMarked,
            vision = visInfo.Vision,
            detection = visInfo.Detection,
            concealment = visInfo.Concealment
        };
    }

    private static object HandleThreats(HttpListenerRequest request)
    {
        var actorName = request.QueryString["actor"];

        GameObj actor;
        if (!string.IsNullOrEmpty(actorName))
        {
            actor = FindActorByName(actorName);
            if (actor.IsNull) return new { error = $"Actor '{actorName}' not found" };
        }
        else
        {
            actor = TacticalController.GetActiveActor();
            if (actor.IsNull) return new { error = "No active actor" };
        }

        var actorInfo = EntitySpawner.GetEntityInfo(actor);
        var actorPos = EntityMovement.GetPosition(actor);
        if (!actorPos.HasValue)
            return new { error = "Could not get actor position" };

        // Find all enemies that could be threats
        var allActors = EntitySpawner.ListEntities();
        var threats = new List<object>();

        foreach (var other in allActors)
        {
            var otherInfo = EntitySpawner.GetEntityInfo(other);
            if (otherInfo == null || !otherInfo.IsAlive) continue;
            if (otherInfo.FactionIndex == actorInfo?.FactionIndex) continue; // Same faction

            var otherPos = EntityMovement.GetPosition(other);
            if (!otherPos.HasValue) continue;

            var canSee = LineOfSight.CanActorSee(other, actor);
            var distance = TileMap.GetDistance(otherPos.Value.x, otherPos.Value.y, actorPos.Value.x, actorPos.Value.y);
            var attackRange = EntityCombat.GetAttackRange(other);

            var inRange = attackRange >= distance;

            threats.Add(new
            {
                name = otherInfo.Name,
                faction = otherInfo.FactionIndex,
                position = new { x = otherPos.Value.x, y = otherPos.Value.y },
                distance = distance,
                canSeeTarget = canSee,
                attackRange = attackRange,
                inAttackRange = inRange,
                canAttack = canSee && inRange
            });
        }

        return new
        {
            actor = actor.GetName(),
            position = new { x = actorPos.Value.x, y = actorPos.Value.y },
            threatCount = threats.Count,
            activeThreats = threats.Count(t => ((dynamic)t).canAttack),
            threats = threats
        };
    }

    private static object HandleAI(HttpListenerRequest request)
    {
        var actorName = request.QueryString["actor"];
        var infoType = request.QueryString["type"] ?? "intent"; // intent, role, behaviors, tiles

        // If no actor specified, return AI info for all enemy actors
        if (string.IsNullOrEmpty(actorName))
        {
            var allActors = EntitySpawner.ListEntities();
            var results = new List<object>();

            foreach (var actor in allActors)
            {
                var info = EntitySpawner.GetEntityInfo(actor);
                if (info == null || !info.IsAlive) continue;
                if (info.FactionIndex == 0) continue; // Skip player faction

                var agentInfo = AI.GetAgentInfo(actor);
                if (!agentInfo.HasAgent) continue;

                results.Add(new
                {
                    actor = info.Name,
                    faction = info.FactionIndex,
                    state = agentInfo.StateName,
                    behavior = agentInfo.SelectedBehavior,
                    score = agentInfo.BehaviorScore,
                    targetActor = agentInfo.TargetActorName,
                    targetTile = new { x = agentInfo.TargetTileX, y = agentInfo.TargetTileY },
                    intent = AI.GetAIIntent(actor)
                });
            }

            return new { count = results.Count, agents = results };
        }

        // Get specific actor
        var targetActor = FindActorByName(actorName);
        if (targetActor.IsNull)
            return new { error = $"Actor '{actorName}' not found" };

        return infoType.ToLowerInvariant() switch
        {
            "role" => GetRoleDataResponse(targetActor),
            "behaviors" => GetBehaviorsResponse(targetActor),
            "tiles" => GetTileScoresResponse(targetActor, request),
            _ => GetIntentResponse(targetActor)
        };
    }

    private static object GetIntentResponse(GameObj actor)
    {
        var agentInfo = AI.GetAgentInfo(actor);
        if (!agentInfo.HasAgent)
            return new { actor = actor.GetName(), hasAgent = false, reason = "Player unit or no AI" };

        return new
        {
            actor = actor.GetName(),
            hasAgent = true,
            state = agentInfo.State,
            stateName = agentInfo.StateName,
            behavior = agentInfo.SelectedBehavior,
            score = agentInfo.BehaviorScore,
            targetActor = agentInfo.TargetActorName,
            targetTile = new { x = agentInfo.TargetTileX, y = agentInfo.TargetTileY },
            evaluatedTiles = agentInfo.EvaluatedTileCount,
            availableBehaviors = agentInfo.AvailableBehaviorCount,
            intent = AI.GetAIIntent(actor)
        };
    }

    private static object GetRoleDataResponse(GameObj actor)
    {
        var role = AI.GetRoleData(actor);
        return new
        {
            actor = actor.GetName(),
            weights = new
            {
                utility = role.UtilityScale,
                safety = role.SafetyScale,
                distance = role.DistanceScale,
                friendlyFire = role.FriendlyFirePenalty
            },
            behaviorWeights = new
            {
                move = role.MoveWeight,
                damage = role.InflictDamageWeight,
                suppress = role.InflictSuppressionWeight,
                stun = role.StunWeight
            },
            settings = new
            {
                canEvade = role.IsAllowedToEvadeEnemies,
                stayHidden = role.AttemptToStayOutOfSight,
                peekCover = role.PeekInAndOutOfCover,
                avoidEnemies = role.AvoidOpponents,
                seekCover = role.CoverAgainstOpponents,
                avoidThreats = role.ThreatFromOpponents
            }
        };
    }

    private static object GetBehaviorsResponse(GameObj actor)
    {
        var behaviors = AI.GetBehaviors(actor);
        return new
        {
            actor = actor.GetName(),
            count = behaviors.Count,
            behaviors = behaviors.Select(b => new
            {
                type = b.TypeName,
                score = b.Score,
                isSelected = b.IsSelected,
                targetActor = b.TargetActorName,
                targetTile = new { x = b.TargetTileX, y = b.TargetTileY }
            }).ToList()
        };
    }

    private static object GetTileScoresResponse(GameObj actor, HttpListenerRequest request)
    {
        int.TryParse(request.QueryString["count"], out int count);
        if (count <= 0) count = 10;

        var tiles = AI.GetTileScores(actor, count);
        return new
        {
            actor = actor.GetName(),
            count = tiles.Count,
            tiles = tiles.Select(t => new
            {
                x = t.X,
                y = t.Y,
                finalScore = t.FinalScore,
                utilityScore = t.UtilityScore,
                safetyScore = t.SafetyScore,
                distanceScore = t.DistanceScore,
                coverLevel = t.CoverLevel,
                visibleToEnemies = t.VisibleToOpponents
            }).ToList()
        };
    }

    private static object HandleHitChance(HttpListenerRequest request)
    {
        var attackerName = request.QueryString["attacker"];
        var targetName = request.QueryString["target"];
        var skillName = request.QueryString["skill"];

        GameObj attacker;
        if (!string.IsNullOrEmpty(attackerName))
        {
            attacker = FindActorByName(attackerName);
            if (attacker.IsNull) return new { error = $"Attacker '{attackerName}' not found" };
        }
        else
        {
            attacker = TacticalController.GetActiveActor();
            if (attacker.IsNull) return new { error = "No active actor and no attacker specified" };
        }

        // If no target specified, return hit chances against all valid targets
        if (string.IsNullOrEmpty(targetName))
        {
            var allResults = CombatSimulation.GetAllHitChances(attacker);
            return new
            {
                attacker = attacker.GetName(),
                targets = allResults.Select(r => new
                {
                    target = r.targetName,
                    hitChance = r.result.FinalHitChance,
                    accuracy = r.result.Accuracy,
                    coverMult = r.result.CoverMult,
                    dodgeMult = r.result.DodgeMult,
                    distance = r.result.Distance,
                    distancePenalty = r.result.DistancePenalty,
                    skill = r.result.SkillName
                }).ToList()
            };
        }

        // Get hit chance against specific target
        var target = FindActorByName(targetName);
        if (target.IsNull) return new { error = $"Target '{targetName}' not found" };

        CombatSimulation.HitChanceResult result;
        if (!string.IsNullOrEmpty(skillName))
        {
            result = CombatSimulation.GetHitChance(attacker, target, skillName);
        }
        else
        {
            result = CombatSimulation.GetHitChance(attacker, target);
        }

        if (result.FinalHitChance < 0)
            return new { error = "Could not calculate hit chance" };

        return new
        {
            attacker = attacker.GetName(),
            target = target.GetName(),
            skill = result.SkillName,
            hitChance = result.FinalHitChance,
            accuracy = result.Accuracy,
            coverMult = result.CoverMult,
            dodgeMult = result.DodgeMult,
            distance = result.Distance,
            distancePenalty = result.DistancePenalty,
            includesDistance = result.IncludesDistance
        };
    }

    // ==================== Helpers ====================

    private static GameObj FindActorByName(string name)
    {
        var actors = EntitySpawner.ListEntities();
        foreach (var actor in actors)
        {
            if (actor.GetName()?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
                return actor;
        }
        return GameObj.Null;
    }

    private static void SendJson(HttpListenerResponse response, object data, int statusCode = 200)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json";
        response.Headers.Add("Access-Control-Allow-Origin", "*");

        var json = JsonSerializer.Serialize(data, JsonOptions);
        var buffer = Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.Close();
    }
}
