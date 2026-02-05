#nullable disable
using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Menace.TwitchSquaddies;

public class TwitchSquaddiesPlugin : IModpackPlugin
{
    private MelonLogger.Instance _log;
    private HarmonyLib.Harmony _harmony;
    private SquaddieExplorer _explorer;
    private SquaddieReadWrite _readWrite;

    private bool _setupComplete;
    private bool _sceneSeen;
    private bool _earlyDiagDone;
    private int _updateCount;

    // Panel state
    private string _statusMessage = "";
    private float _statusTime;

    // Cached data (refreshed periodically)
    private List<SquaddieExplorer.SquaddieInfo> _cachedSquaddies = new();
    private List<SquaddieExplorer.LeaderInfo> _cachedLeaders = new();
    private float _lastRefreshTime;
    private const float RefreshInterval = 3f;

    // Twitch integration
    private TwitchClient _twitchClient;
    private ConcurrentQueue<(int id, object proxy, string displayName, string username)> _pendingAssignments = new();
    private float _lastTwitchPoll;
    private const float TwitchPollInterval = 3f;

    // Pending async results for commands
    private volatile string _pendingDraftList;

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _log = logger;
        _harmony = harmony;
        _explorer = new SquaddieExplorer(logger);
        _readWrite = new SquaddieReadWrite(logger, _explorer);
        _twitchClient = new TwitchClient(logger);

        _log.Msg("TwitchSquaddies v1.0.0");
        DevConsole.RegisterPanel("Squaddies", DrawSquaddiesPanel);
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        _log.Msg($"Scene loaded: '{sceneName}' (index {buildIndex})");

        if (!_sceneSeen)
        {
            _log.Msg($"First scene '{sceneName}', starting setup coroutine...");
            _sceneSeen = true;
            MelonCoroutines.Start(WaitAndSetup());
        }
    }

    private System.Collections.IEnumerator WaitAndSetup()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            yield return new WaitForSeconds(2f);

            _log.Msg($"Setup attempt {attempt + 1}/30...");

            if (_explorer.TrySetup())
            {
                _log.Msg("SquaddieExplorer setup complete.");

                _readWrite.Setup(_harmony);
                _log.Msg("SquaddieReadWrite setup complete.");

                _setupComplete = true;
                RegisterCommands();
                _log.Msg("TwitchSquaddies setup complete — panel + commands registered");
                yield break;
            }
        }

        _log.Warning("Failed to set up TwitchSquaddies after 30 attempts.");
        _setupComplete = true; // Mark done to stop showing "loading"
    }

    public void OnUpdate()
    {
        _updateCount++;

        // Early diagnostics — runs once after a few frames
        if (!_earlyDiagDone && _updateCount == 60)
        {
            _earlyDiagDone = true;
            _log.Msg($"--- TwitchSquaddies early diagnostics (frame {_updateCount}) ---");
            _log.Msg($"sceneSeen={_sceneSeen}, setupComplete={_setupComplete}, explorerReady={_explorer?.IsReady}");
            _log.Msg($"Active scene: '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}'");
            _log.Msg("--- End early diagnostics ---");
        }

        // Poll Twitch server status
        float now = Time.time;
        if (now - _lastTwitchPoll > TwitchPollInterval)
        {
            _lastTwitchPoll = now;
            _twitchClient.PollStatus();
        }

        // Drain pending assignments on main thread
        while (_pendingAssignments.TryDequeue(out var assignment))
        {
            try
            {
                _readWrite.WriteSquaddieNickname(assignment.proxy, assignment.displayName);
                _readWrite.SetTwitchMessage(assignment.id, $"[Twitch] {assignment.displayName}");
                SetStatus($"Assigned {assignment.displayName} to #{assignment.id}");
                _log.Msg($"Twitch assignment: {assignment.displayName} ({assignment.username}) -> squaddie #{assignment.id}");

                // Fetch latest message to show on home planet
                _twitchClient.FetchLatestMessage(assignment.username, msg =>
                {
                    if (!string.IsNullOrEmpty(msg))
                        _readWrite.SetTwitchMessage(assignment.id, msg);
                });
            }
            catch (Exception ex)
            {
                _log.Warning($"Assignment error for #{assignment.id}: {ex.Message}");
            }
        }
    }

    public void OnGUI()
    {
        // Drawing is handled by the DevConsole panel system
    }

    // ==================== Commands ====================

    private void RegisterCommands()
    {
        DevConsole.RegisterCommand("sq.list", "",
            "List all alive squaddies (refresh first)",
            args =>
            {
                RefreshData();
                if (_cachedSquaddies.Count == 0) return "No squaddies found (wrong scene?)";
                var lines = _cachedSquaddies.Select(s =>
                    $"  #{s.Id}  {s.Name} \"{s.Nickname}\"  Planet:{s.HomePlanetName}");
                return $"{_cachedSquaddies.Count} squaddies:\n{string.Join("\n", lines)}";
            });

        DevConsole.RegisterCommand("sq.name", "<id> <new name>",
            "Set a squaddie's name",
            args =>
            {
                if (args.Length < 2) return "Usage: sq.name <id> <new name>";
                if (!int.TryParse(args[0], out int id)) return $"Invalid ID: {args[0]}";
                var name = string.Join(" ", args.Skip(1));
                var squaddie = FindSquaddie(id);
                if (squaddie == null) return $"Squaddie #{id} not found (try sq.list first)";
                if (squaddie.Proxy == null) return $"Squaddie #{id} has no proxy";
                bool ok = _readWrite.WriteSquaddieName(squaddie.Proxy, name);
                return ok ? $"Name set to '{name}'" : $"Failed: {_readWrite.LastOperationResult}";
            });

        DevConsole.RegisterCommand("sq.nick", "<id> <new nickname>",
            "Set a squaddie's nickname",
            args =>
            {
                if (args.Length < 2) return "Usage: sq.nick <id> <new nickname>";
                if (!int.TryParse(args[0], out int id)) return $"Invalid ID: {args[0]}";
                var nick = string.Join(" ", args.Skip(1));
                var squaddie = FindSquaddie(id);
                if (squaddie == null) return $"Squaddie #{id} not found (try sq.list first)";
                if (squaddie.Proxy == null) return $"Squaddie #{id} has no proxy";
                bool ok = _readWrite.WriteSquaddieNickname(squaddie.Proxy, nick);
                return ok ? $"Nickname set to '{nick}'" : $"Failed: {_readWrite.LastOperationResult}";
            });

        DevConsole.RegisterCommand("sq.twitch", "<id> <message>",
            "Set Twitch message (replaces home planet display)",
            args =>
            {
                if (args.Length < 2) return "Usage: sq.twitch <id> <message>";
                if (!int.TryParse(args[0], out int id)) return $"Invalid ID: {args[0]}";
                var msg = string.Join(" ", args.Skip(1));
                _readWrite.SetTwitchMessage(id, msg);
                return $"Twitch message set for #{id}: '{msg}'";
            });

        DevConsole.RegisterCommand("sq.twitch.clear", "<id>",
            "Clear Twitch message (restore real planet name)",
            args =>
            {
                if (args.Length < 1) return "Usage: sq.twitch.clear <id>";
                if (!int.TryParse(args[0], out int id)) return $"Invalid ID: {args[0]}";
                _readWrite.ClearTwitchMessage(id);
                return $"Twitch message cleared for #{id}";
            });

        DevConsole.RegisterCommand("sq.twitch.clearall", "",
            "Clear all Twitch messages",
            args =>
            {
                _readWrite.ClearAllTwitchMessages();
                return "All Twitch messages cleared";
            });

        // Twitch integration commands
        DevConsole.RegisterCommand("twitch.status", "",
            "Show Twitch server connection status",
            args =>
            {
                if (!_twitchClient.IsServerReachable)
                    return $"TwitchServer: OFFLINE (url: {_twitchClient.ServerUrl})";
                if (!_twitchClient.IsConnected)
                    return $"TwitchServer: running but not connected to Twitch (url: {_twitchClient.ServerUrl})";
                return $"TwitchServer: connected to #{_twitchClient.Channel}, draft pool: {_twitchClient.DraftPoolSize} viewers";
            });

        DevConsole.RegisterCommand("twitch.draft", "",
            "List viewers in the draft pool",
            args =>
            {
                if (!_twitchClient.IsServerReachable) return "TwitchServer is offline";
                _pendingDraftList = null;
                _twitchClient.FetchDraftList(result => _pendingDraftList = result);
                // Return immediate acknowledgment — result arrives async
                return "Fetching draft list...";
            });

        DevConsole.RegisterCommand("twitch.pick", "<squaddie-id>",
            "Pick a random viewer from draft pool and assign to squaddie",
            args =>
            {
                if (args.Length < 1) return "Usage: twitch.pick <squaddie-id>";
                if (!int.TryParse(args[0], out int id)) return $"Invalid ID: {args[0]}";
                if (!_twitchClient.IsServerReachable) return "TwitchServer is offline";
                if (_twitchClient.DraftPoolSize == 0) return "Draft pool is empty";

                var squaddie = FindSquaddie(id);
                if (squaddie == null) return $"Squaddie #{id} not found (try sq.list first)";
                if (squaddie.Proxy == null) return $"Squaddie #{id} has no proxy";

                _twitchClient.PickViewer((username, displayName, latestMsg) =>
                {
                    _pendingAssignments.Enqueue((id, squaddie.Proxy, displayName, username));
                });

                return $"Picking viewer for squaddie #{id}...";
            });

        DevConsole.RegisterCommand("twitch.url", "<url>",
            "Set TwitchServer URL (default: http://localhost:7654)",
            args =>
            {
                if (args.Length < 1) return $"Current URL: {_twitchClient.ServerUrl}";
                _twitchClient.ServerUrl = args[0];
                return $"TwitchServer URL set to: {args[0]}";
            });

        _log.Msg("Registered sq.* and twitch.* commands");
    }

    private SquaddieExplorer.SquaddieInfo FindSquaddie(int id)
    {
        return _cachedSquaddies.FirstOrDefault(s => s.Id == id);
    }

    private void RefreshData()
    {
        try
        {
            _cachedSquaddies = _explorer.GetAllSquaddieInfo();
            _cachedLeaders = _explorer.GetAllLeaderInfo();
        }
        catch (Exception ex)
        {
            _log.Warning($"Refresh error: {ex.Message}");
        }
    }

    // ==================== DevConsole Panel ====================

    private void DrawSquaddiesPanel(Rect area)
    {
        float cx = area.x;
        float cy = area.y;
        float cw = area.width;

        if (!_setupComplete)
        {
            GUI.Label(new Rect(cx, cy, cw, 18), "TwitchSquaddies loading...");
            return;
        }

        if (!_explorer.IsReady)
        {
            GUI.Label(new Rect(cx, cy, cw, 18), "Explorer setup failed — check MelonLoader log");
            return;
        }

        // Header with Twitch status
        string twitchStatus;
        if (!_twitchClient.IsServerReachable)
            twitchStatus = "Server: OFFLINE";
        else if (!_twitchClient.IsConnected)
            twitchStatus = "Server: not connected";
        else
            twitchStatus = $"Twitch: #{_twitchClient.Channel} | Pool: {_twitchClient.DraftPoolSize}";

        GUI.Label(new Rect(cx, cy, cw, 20), $"TWITCH SQUADDIES — {twitchStatus}");
        cy += 22;

        // Manual refresh button
        if (GUI.Button(new Rect(cx, cy, 120, 20), "Refresh Data"))
        {
            _log.Msg("Manual refresh starting...");
            RefreshData();
            _log.Msg($"Got {_cachedSquaddies.Count} squaddies, {_cachedLeaders.Count} leaders");
            SetStatus($"Refreshed: {_cachedSquaddies.Count} squaddies, {_cachedLeaders.Count} leaders");
        }
        cy += 24;

        GUI.Label(new Rect(cx, cy, cw, 16),
            $"Alive: {_cachedSquaddies.Count}  Leaders: {_cachedLeaders.Count}");
        cy += 20;

        // Status line
        if (!string.IsNullOrEmpty(_statusMessage))
        {
            float elapsed = 0f;
            try { elapsed = Time.time - _statusTime; } catch { }
            if (elapsed < 5f)
            {
                GUI.Label(new Rect(cx, cy, cw, 16), _statusMessage);
                cy += 18;
            }
        }

        // ReadWrite status
        if (_readWrite.IsReady && !string.IsNullOrEmpty(_readWrite.LastOperationResult))
        {
            GUI.Label(new Rect(cx, cy, cw, 16), $"Last op: {_readWrite.LastOperationResult}");
            cy += 18;
        }

        cy += 4;

        float maxY = area.yMax;

        // Draw leaders and their squaddies (no scroll view — unstripped in IL2CPP)
        foreach (var leader in _cachedLeaders)
        {
            if (cy > maxY) break;

            GUI.Label(new Rect(cx, cy, cw, 20),
                $"== {leader.Nickname ?? "?"} ({leader.HealthStatus ?? "?"}) - {leader.TemplateName ?? "?"} ==");
            cy += 20;

            foreach (int squaddieId in leader.SquaddieIds)
            {
                if (cy > maxY) break;

                var squaddie = _cachedSquaddies.FirstOrDefault(s => s.Id == squaddieId);
                if (squaddie == null)
                {
                    GUI.Label(new Rect(cx + 8, cy, cw - 8, 16), $"  Squaddie #{squaddieId} (not in alive list)");
                    cy += 18;
                    continue;
                }

                cy = DrawSquaddieEntry(squaddie, cx, cy, cw, maxY);
            }

            cy += 6;
        }

        // Unassigned squaddies (not under any leader)
        var assignedIds = new HashSet<int>(_cachedLeaders.SelectMany(l => l.SquaddieIds));
        var unassigned = _cachedSquaddies.Where(s => !assignedIds.Contains(s.Id)).ToList();

        if (unassigned.Count > 0 && cy < maxY)
        {
            GUI.Label(new Rect(cx, cy, cw, 20), "== Unassigned ==");
            cy += 20;

            foreach (var squaddie in unassigned)
            {
                if (cy > maxY) break;
                cy = DrawSquaddieEntry(squaddie, cx, cy, cw, maxY);
            }
        }
    }

    private float DrawSquaddieEntry(SquaddieExplorer.SquaddieInfo squaddie, float cx, float cy, float cw, float maxY)
    {
        float indent = cx + 8;
        bool showDice = _twitchClient.IsServerReachable && _twitchClient.IsConnected && _twitchClient.DraftPoolSize > 0;
        float diceWidth = showDice ? 28 : 0;
        float labelWidth = cw - 8 - diceWidth;

        // Dice button — pick random viewer for this squaddie
        if (showDice)
        {
            if (GUI.Button(new Rect(indent, cy, 24, 16), "?"))
            {
                var proxy = squaddie.Proxy;
                var id = squaddie.Id;
                _twitchClient.PickViewer((username, displayName, latestMsg) =>
                {
                    _pendingAssignments.Enqueue((id, proxy, displayName, username));
                });
                SetStatus($"Picking viewer for #{squaddie.Id}...");
            }
        }

        // Info line
        GUI.Label(new Rect(indent + diceWidth, cy, labelWidth, 16),
            $"#{squaddie.Id}  {squaddie.Name} \"{squaddie.Nickname}\"  " +
            $"Gender:{squaddie.Gender}  Skin:{squaddie.SkinColor}  " +
            $"Missions:{squaddie.MissionsParticipated}  Planet:{squaddie.HomePlanetName}");
        cy += 18;

        return cy;
    }

    private void SetStatus(string msg)
    {
        _statusMessage = msg;
        _statusTime = Time.time;
    }
}
