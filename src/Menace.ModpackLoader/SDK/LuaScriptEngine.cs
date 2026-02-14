using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MoonSharp.Interpreter;

namespace Menace.SDK;

/// <summary>
/// Lua scripting engine that exposes console commands to Lua scripts.
///
/// Lua API:
///   cmd("command args")     - Execute a console command, returns (success, result)
///   log("message")          - Log to console
///   warn("message")         - Log warning
///   error("message")        - Log error
///   on("event", function)   - Register event callback
///   off("event", function)  - Unregister event callback
///
/// Events:
///   scene_loaded(sceneName)       - Fired when a scene loads
///   tactical_ready()              - Fired when tactical battle is ready
///   mission_start(missionInfo)    - Fired at mission start
///   turn_start(factionIndex)      - Fired at turn start
///   turn_end(factionIndex)        - Fired at turn end
/// </summary>
public class LuaScriptEngine
{
    private static LuaScriptEngine _instance;
    public static LuaScriptEngine Instance => _instance ??= new LuaScriptEngine();

    /// <summary>
    /// Number of Lua scripts currently loaded.
    /// </summary>
    public int LoadedScriptCount => _loadedScripts.Count;

    private readonly Script _lua;
    private readonly Dictionary<string, List<DynValue>> _eventHandlers = new();
    private readonly List<(string ModId, string ScriptPath, Script Script)> _loadedScripts = new();
    private MelonLogger.Instance _log;

    // Supported events
    private static readonly HashSet<string> ValidEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "scene_loaded",
        "tactical_ready",
        "mission_start",
        "turn_start",
        "turn_end",
        "actor_killed",
        "actor_damaged",
        "ability_used"
    };

    private LuaScriptEngine()
    {
        // Create Lua state with limited permissions (no OS/IO access)
        _lua = new Script(CoreModules.Preset_SoftSandbox);

        // Register API functions
        _lua.Globals["cmd"] = (Func<string, DynValue>)LuaCmd;
        _lua.Globals["log"] = (Action<string>)LuaLog;
        _lua.Globals["warn"] = (Action<string>)LuaWarn;
        _lua.Globals["error"] = (Action<string>)LuaError;
        _lua.Globals["on"] = (Action<string, DynValue>)LuaOn;
        _lua.Globals["off"] = (Action<string, DynValue>)LuaOff;
        _lua.Globals["emit"] = (Action<string, DynValue[]>)LuaEmit;

        // Utility functions
        _lua.Globals["sleep"] = (Action<int>)LuaSleep;
        _lua.Globals["commands"] = (Func<Table>)LuaGetCommands;
        _lua.Globals["has_command"] = (Func<string, bool>)DevConsole.HasCommand;

        // Initialize event handler lists
        foreach (var evt in ValidEvents)
            _eventHandlers[evt] = new List<DynValue>();
    }

    /// <summary>
    /// Initialize the Lua engine with a logger.
    /// </summary>
    public void Initialize(MelonLogger.Instance logger)
    {
        _log = logger;
        _log?.Msg("[LuaEngine] Initialized");

        // Register console commands for Lua
        DevConsole.RegisterCommand("lua", "<code>", "Execute Lua code", args =>
        {
            if (args.Length == 0) return "Usage: lua <code>";
            var code = string.Join(" ", args);
            return ExecuteString(code);
        });

        DevConsole.RegisterCommand("luafile", "<path>", "Execute Lua file", args =>
        {
            if (args.Length == 0) return "Usage: luafile <path>";
            return ExecuteFile(args[0]);
        });

        DevConsole.RegisterCommand("luaevents", "", "List registered Lua event handlers", _ =>
        {
            var lines = new List<string> { "Lua Event Handlers:" };
            foreach (var kvp in _eventHandlers.Where(k => k.Value.Count > 0))
            {
                lines.Add($"  {kvp.Key}: {kvp.Value.Count} handler(s)");
            }
            return lines.Count > 1 ? string.Join("\n", lines) : "No event handlers registered";
        });

        DevConsole.RegisterCommand("luascripts", "", "List loaded Lua scripts", _ =>
        {
            if (_loadedScripts.Count == 0) return "No Lua scripts loaded";
            var lines = new List<string> { "Loaded Lua Scripts:" };
            foreach (var (modId, path, _) in _loadedScripts)
            {
                lines.Add($"  [{modId}] {Path.GetFileName(path)}");
            }
            return string.Join("\n", lines);
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Lua API Functions
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// cmd("command args") - Execute console command from Lua.
    /// Returns a table with { success = bool, result = string }
    /// </summary>
    private DynValue LuaCmd(string input)
    {
        var (success, result) = DevConsole.ExecuteCommandWithResult(input);

        // Return as Lua table for easy access
        var table = new Table(_lua);
        table["success"] = success;
        table["result"] = result;

        // Also try to parse structured data for certain commands
        table["data"] = TryParseCommandResult(input, result);

        return DynValue.NewTable(table);
    }

    /// <summary>
    /// Attempt to parse command results into structured Lua tables.
    /// </summary>
    private DynValue TryParseCommandResult(string command, string result)
    {
        // For now, return nil - can be enhanced to parse specific command outputs
        // into Lua tables (e.g., roster command → table of leaders)
        return DynValue.Nil;
    }

    private void LuaLog(string message)
    {
        DevConsole.Log($"[Lua] {message}");
    }

    private void LuaWarn(string message)
    {
        DevConsole.LogWarning($"[Lua] {message}");
    }

    private void LuaError(string message)
    {
        DevConsole.LogError($"[Lua] {message}");
    }

    /// <summary>
    /// on("event", callback) - Register event handler.
    /// </summary>
    private void LuaOn(string eventName, DynValue callback)
    {
        if (!ValidEvents.Contains(eventName))
        {
            LuaWarn($"Unknown event: {eventName}. Valid events: {string.Join(", ", ValidEvents)}");
            return;
        }

        if (callback.Type != DataType.Function)
        {
            LuaError($"on() requires a function callback, got {callback.Type}");
            return;
        }

        _eventHandlers[eventName].Add(callback);
    }

    /// <summary>
    /// off("event", callback) - Unregister event handler.
    /// </summary>
    private void LuaOff(string eventName, DynValue callback)
    {
        if (_eventHandlers.TryGetValue(eventName, out var handlers))
        {
            handlers.RemoveAll(h => h.Equals(callback));
        }
    }

    /// <summary>
    /// emit("event", args...) - Fire an event (for testing/custom events).
    /// </summary>
    private void LuaEmit(string eventName, params DynValue[] args)
    {
        FireEvent(eventName, args);
    }

    /// <summary>
    /// sleep(frames) - Yield for N frames (only works in coroutines).
    /// </summary>
    private void LuaSleep(int frames)
    {
        // Note: This is a placeholder. True async sleep would require coroutine support.
        _log?.Msg($"[Lua] sleep({frames}) called - async sleep not yet implemented");
    }

    /// <summary>
    /// commands() - Get list of all available console commands.
    /// </summary>
    private Table LuaGetCommands()
    {
        var table = new Table(_lua);
        int i = 1;
        foreach (var name in DevConsole.GetCommandNames().OrderBy(n => n))
        {
            table[i++] = name;
        }
        return table;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Script Execution
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Execute Lua code string.
    /// </summary>
    public string ExecuteString(string code, string chunkName = "console")
    {
        try
        {
            var result = _lua.DoString(code, null, chunkName);
            if (result.IsNil() || result.IsVoid())
                return "(ok)";
            return result.ToPrintString();
        }
        catch (ScriptRuntimeException ex)
        {
            return $"Lua error: {ex.DecoratedMessage}";
        }
        catch (SyntaxErrorException ex)
        {
            return $"Lua syntax error: {ex.DecoratedMessage}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Execute Lua file.
    /// </summary>
    public string ExecuteFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return $"File not found: {path}";

            var code = File.ReadAllText(path);
            return ExecuteString(code, Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            return $"Error loading file: {ex.Message}";
        }
    }

    /// <summary>
    /// Load and execute a Lua script from a modpack.
    /// </summary>
    public bool LoadModpackScript(string modId, string scriptPath)
    {
        try
        {
            if (!File.Exists(scriptPath))
            {
                _log?.Warning($"[LuaEngine] Script not found: {scriptPath}");
                return false;
            }

            var code = File.ReadAllText(scriptPath);

            // Create a new script instance for isolation (optional - could share state)
            var script = new Script(CoreModules.Preset_SoftSandbox);

            // Copy API functions to new script
            script.Globals["cmd"] = (Func<string, DynValue>)LuaCmd;
            script.Globals["log"] = (Action<string>)LuaLog;
            script.Globals["warn"] = (Action<string>)LuaWarn;
            script.Globals["error"] = (Action<string>)LuaError;
            script.Globals["on"] = (Action<string, DynValue>)LuaOn;
            script.Globals["off"] = (Action<string, DynValue>)LuaOff;
            script.Globals["emit"] = (Action<string, DynValue[]>)LuaEmit;
            script.Globals["commands"] = (Func<Table>)LuaGetCommands;
            script.Globals["has_command"] = (Func<string, bool>)DevConsole.HasCommand;

            // Set mod context
            script.Globals["MOD_ID"] = modId;
            script.Globals["SCRIPT_PATH"] = scriptPath;

            // Execute the script
            script.DoString(code, null, Path.GetFileName(scriptPath));

            _loadedScripts.Add((modId, scriptPath, script));
            _log?.Msg($"[LuaEngine] Loaded script: {Path.GetFileName(scriptPath)} from {modId}");

            return true;
        }
        catch (ScriptRuntimeException ex)
        {
            _log?.Error($"[LuaEngine] Runtime error in {scriptPath}: {ex.DecoratedMessage}");
            return false;
        }
        catch (SyntaxErrorException ex)
        {
            _log?.Error($"[LuaEngine] Syntax error in {scriptPath}: {ex.DecoratedMessage}");
            return false;
        }
        catch (Exception ex)
        {
            _log?.Error($"[LuaEngine] Error loading {scriptPath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Unload all scripts from a modpack.
    /// </summary>
    public void UnloadModpackScripts(string modId)
    {
        _loadedScripts.RemoveAll(s => s.ModId == modId);
        _log?.Msg($"[LuaEngine] Unloaded scripts for {modId}");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Event System
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Fire an event to all registered Lua handlers.
    /// </summary>
    public void FireEvent(string eventName, params DynValue[] args)
    {
        if (!_eventHandlers.TryGetValue(eventName, out var handlers) || handlers.Count == 0)
            return;

        foreach (var handler in handlers.ToList()) // ToList to allow modification during iteration
        {
            try
            {
                _lua.Call(handler, args);
            }
            catch (ScriptRuntimeException ex)
            {
                _log?.Warning($"[LuaEngine] Error in {eventName} handler: {ex.DecoratedMessage}");
            }
            catch (Exception ex)
            {
                _log?.Warning($"[LuaEngine] Error in {eventName} handler: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Fire event with simple string argument.
    /// </summary>
    public void FireEvent(string eventName, string arg)
    {
        FireEvent(eventName, DynValue.NewString(arg));
    }

    /// <summary>
    /// Fire event with table argument.
    /// </summary>
    public void FireEventWithTable(string eventName, Dictionary<string, object> data)
    {
        var table = new Table(_lua);
        foreach (var kvp in data)
        {
            table[kvp.Key] = DynValue.FromObject(_lua, kvp.Value);
        }
        FireEvent(eventName, DynValue.NewTable(table));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Event Triggers (called from game hooks)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Called when a scene is loaded.
    /// </summary>
    public void OnSceneLoaded(string sceneName)
    {
        FireEvent("scene_loaded", sceneName);
    }

    /// <summary>
    /// Called when tactical battle is ready.
    /// </summary>
    public void OnTacticalReady()
    {
        FireEvent("tactical_ready");
    }

    /// <summary>
    /// Called at mission start.
    /// </summary>
    public void OnMissionStart(string missionName, string biome, int difficulty)
    {
        FireEventWithTable("mission_start", new Dictionary<string, object>
        {
            ["name"] = missionName,
            ["biome"] = biome,
            ["difficulty"] = difficulty
        });
    }

    /// <summary>
    /// Called at turn start.
    /// </summary>
    public void OnTurnStart(int factionIndex, string factionName)
    {
        FireEventWithTable("turn_start", new Dictionary<string, object>
        {
            ["faction"] = factionIndex,
            ["factionName"] = factionName
        });
    }

    /// <summary>
    /// Called at turn end.
    /// </summary>
    public void OnTurnEnd(int factionIndex, string factionName)
    {
        FireEventWithTable("turn_end", new Dictionary<string, object>
        {
            ["faction"] = factionIndex,
            ["factionName"] = factionName
        });
    }
}
