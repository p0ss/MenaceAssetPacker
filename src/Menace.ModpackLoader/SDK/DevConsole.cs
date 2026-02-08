using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Menace.SDK.Repl;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// IMGUI-based developer console overlay. Toggle with backtick/tilde (~) key.
/// Uses raw GUI.* calls (not GUILayout) for IL2CPP compatibility where method
/// unstripping may fail for GUILayout methods.
/// Supports a tabbed panel system with built-in Errors, Log, Inspector, and Watch panels.
/// </summary>
public static class DevConsole
{
    public static bool IsVisible { get; set; }

    /// <summary>
    /// True when the console is visible and the mouse cursor is over it.
    /// Game input handlers should skip world clicks when this is true.
    /// </summary>
    public static bool IsMouseOverConsole => IsVisible && _mouseOverConsole;
    private static bool _mouseOverConsole;

    // Panel registry
    private static readonly List<PanelEntry> _panels = new();
    private static int _activePanel;

    // Inspector state
    private static GameObj _inspectedObj;
    private static Vector2 _inspectorScroll;

    // Watch state
    private static readonly List<(string Label, Func<string> Getter)> _watches = new();

    // Error panel state
    private static Vector2 _errorScroll;
    private static ErrorSeverity _errorSeverityFilter = ErrorSeverity.Info | ErrorSeverity.Warning | ErrorSeverity.Error | ErrorSeverity.Fatal;
    private static readonly HashSet<string> _errorModExcludes = new();

    // Log panel state
    private static readonly List<string> _logBuffer = new();
    private static readonly int LogBufferMax = 200;

    // Panel error diagnostic (dedup so we only log once per distinct error)
    private static string _lastPanelError;

    // Command registry
    private static readonly Dictionary<string, CommandEntry> _commands = new(StringComparer.OrdinalIgnoreCase);
    private static string _commandInput = "";
    private static readonly List<(string Input, string Output, bool IsError)> _commandHistory = new();
    private static Vector2 _commandScroll;
    private static int _commandHistoryNav = -1;
    private static readonly List<string> _commandInputHistory = new();

    // REPL evaluator (set by ModpackLoaderMod after Roslyn init)
    private static ConsoleEvaluator _replEvaluator;

    private class CommandEntry
    {
        public string Name;
        public string Usage;
        public string Description;
        public Func<string[], string> Handler;
    }

    // GUI styles (lazy-initialized)
    private static bool _stylesInitialized;
    private static GUIStyle _boxStyle;
    private static GUIStyle _tabActiveStyle;
    private static GUIStyle _tabInactiveStyle;
    private static GUIStyle _labelStyle;
    private static GUIStyle _errorStyle;
    private static GUIStyle _warnStyle;
    private static GUIStyle _infoStyle;
    private static GUIStyle _headerStyle;
    private static GUIStyle _helpStyle;


    // Layout constants
    private static Rect _consoleRect;
    private const float TitleHeight = 22f;
    private const float TabHeight = 26f;

    // Draggable window state
    private static Vector2 _consolePosition = new Vector2(10, 10);
    private static bool _isDragging;
    private static Vector2 _dragOffset;

    // Click passthrough prevention - track when we consumed a click this frame
    private static int _lastClickConsumedFrame = -1;

    // Right-click injection to cancel tile selection after left-click on console
    private static int _injectRightClickUntilFrame = -1;
    private const float LineHeight = 18f;
    private const float Padding = 8f;

    private class PanelEntry
    {
        public string Name;
        public Action<Rect> DrawCallback;
    }

    /// <summary>
    /// Register a custom panel in the console.
    /// The callback receives the content Rect where the panel should draw using raw GUI.* calls.
    /// </summary>
    public static void RegisterPanel(string name, Action<Rect> drawCallback)
    {
        if (string.IsNullOrEmpty(name) || drawCallback == null) return;

        // Replace existing panel with same name
        var existing = _panels.FindIndex(p => p.Name == name);
        if (existing >= 0)
        {
            _panels[existing].DrawCallback = drawCallback;
            return;
        }

        _panels.Add(new PanelEntry { Name = name, DrawCallback = drawCallback });
    }

    /// <summary>
    /// Remove a panel by name.
    /// </summary>
    public static void RemovePanel(string name)
    {
        var idx = _panels.FindIndex(p => p.Name == name);
        if (idx >= 0)
        {
            _panels.RemoveAt(idx);
            if (_activePanel >= _panels.Count)
                _activePanel = Math.Max(0, _panels.Count - 1);
        }
    }

    /// <summary>
    /// Set a GameObj to inspect in the Inspector panel.
    /// </summary>
    public static void Inspect(GameObj obj)
    {
        _inspectedObj = obj;
        _inspectorScroll = Vector2.zero;

        // Switch to Inspector tab
        var idx = _panels.FindIndex(p => p.Name == "Inspector");
        if (idx >= 0)
            _activePanel = idx;
    }

    /// <summary>
    /// Show the console and switch to a specific panel by name.
    /// </summary>
    public static void ShowPanel(string panelName)
    {
        IsVisible = true;
        var idx = _panels.FindIndex(p => p.Name.Equals(panelName, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0)
            _activePanel = idx;
    }

    /// <summary>
    /// Add a live watch expression.
    /// </summary>
    public static void Watch(string label, Func<string> valueGetter)
    {
        if (string.IsNullOrEmpty(label) || valueGetter == null) return;
        Unwatch(label);
        _watches.Add((label, valueGetter));
    }

    /// <summary>
    /// Remove a watch by label.
    /// </summary>
    public static void Unwatch(string label)
    {
        _watches.RemoveAll(w => w.Label == label);
    }

    /// <summary>
    /// Append a message to the log panel.
    /// </summary>
    public static void Log(string message)
    {
        _logBuffer.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        while (_logBuffer.Count > LogBufferMax)
            _logBuffer.RemoveAt(0);
    }

    /// <summary>
    /// Register a command that can be executed from the Commands panel.
    /// The handler receives the arguments (split by space after the command name)
    /// and should return a result string to display, or null for no output.
    /// </summary>
    public static void RegisterCommand(string name, string usage, string description, Func<string[], string> handler)
    {
        if (string.IsNullOrEmpty(name) || handler == null) return;
        _commands[name] = new CommandEntry
        {
            Name = name,
            Usage = usage,
            Description = description,
            Handler = handler
        };
    }

    /// <summary>
    /// Register a command (short form without description).
    /// </summary>
    public static void RegisterCommand(string name, string usage, Func<string[], string> handler)
    {
        RegisterCommand(name, usage, "", handler);
    }

    /// <summary>
    /// Remove a registered command.
    /// </summary>
    public static void RemoveCommand(string name)
    {
        if (!string.IsNullOrEmpty(name))
            _commands.Remove(name);
    }

    /// <summary>
    /// Set the REPL evaluator for C# expression evaluation in the Console panel.
    /// Called by ModpackLoaderMod after Roslyn initialization.
    /// </summary>
    public static void SetReplEvaluator(ConsoleEvaluator evaluator)
    {
        _replEvaluator = evaluator;
    }

    // --- Internal lifecycle ---

    internal static void Initialize()
    {
        // Register built-in panels in display order
        _panels.Clear();
        _panels.Add(new PanelEntry { Name = "Battle Log", DrawCallback = DrawBattleLogPanel });
        _panels.Add(new PanelEntry { Name = "Log", DrawCallback = DrawLogPanel });
        _panels.Add(new PanelEntry { Name = "Console", DrawCallback = DrawConsolePanel });
        _panels.Add(new PanelEntry { Name = "Inspector", DrawCallback = DrawInspectorPanel });
        _panels.Add(new PanelEntry { Name = "Watch", DrawCallback = DrawWatchPanel });

        RegisterCoreCommands();
    }

    private static void RegisterCoreCommands()
    {
        // Built-in help command
        RegisterCommand("help", "", "List all registered commands", args =>
        {
            var lines = new List<string>();
            foreach (var cmd in _commands.Values.OrderBy(c => c.Name))
            {
                var usage = string.IsNullOrEmpty(cmd.Usage) ? "" : $" {cmd.Usage}";
                var desc = string.IsNullOrEmpty(cmd.Description) ? "" : $" - {cmd.Description}";
                lines.Add($"  {cmd.Name}{usage}{desc}");
            }
            return string.Join("\n", lines);
        });

        // find <type> - List all instances of a type
        RegisterCommand("find", "<type>", "List all instances of a type", args =>
        {
            if (args.Length == 0)
                return "Usage: find <type>";
            var typeName = args[0];
            var results = GameQuery.FindAll(typeName);
            if (results.Length == 0)
                return $"No instances of '{typeName}' found";
            var lines = new List<string> { $"Found {results.Length} {typeName}:" };
            foreach (var obj in results.Take(50))
            {
                var name = obj.GetName() ?? "<unnamed>";
                lines.Add($"  {name}");
            }
            if (results.Length > 50)
                lines.Add($"  ... and {results.Length - 50} more");
            return string.Join("\n", lines);
        });

        // findbyname <type> <name> - Find instance by name
        RegisterCommand("findbyname", "<type> <name>", "Find instance by name", args =>
        {
            if (args.Length < 2)
                return "Usage: findbyname <type> <name>";
            var typeName = args[0];
            var name = string.Join(" ", args.Skip(1));
            var obj = GameQuery.FindByName(typeName, name);
            if (obj.IsNull)
                return $"No '{typeName}' with name '{name}' found";
            return $"Found: {obj.GetTypeName()} '{obj.GetName()}' @ 0x{obj.Pointer:X}";
        });

        // inspect <type> <name> - Find and inspect an object
        RegisterCommand("inspect", "<type> <name>", "Find and inspect an object", args =>
        {
            if (args.Length < 2)
                return "Usage: inspect <type> <name>";
            var typeName = args[0];
            var name = string.Join(" ", args.Skip(1));
            var obj = GameQuery.FindByName(typeName, name);
            if (obj.IsNull)
                return $"No '{typeName}' with name '{name}' found";
            Inspect(obj);
            return $"Inspecting: {obj.GetTypeName()} '{obj.GetName()}'";
        });

        // templates <type> - List all templates of a type
        RegisterCommand("templates", "<type>", "List all templates of a type", args =>
        {
            if (args.Length == 0)
                return "Usage: templates <type>";
            var typeName = args[0];
            if (!typeName.EndsWith("Template"))
                typeName += "Template";
            var results = GameQuery.FindAll(typeName);
            if (results.Length == 0)
                return $"No templates of type '{typeName}' found";
            var lines = new List<string> { $"Found {results.Length} {typeName}:" };
            foreach (var obj in results.OrderBy(o => o.GetName()).Take(50))
            {
                var name = obj.GetName() ?? "<unnamed>";
                lines.Add($"  {name}");
            }
            if (results.Length > 50)
                lines.Add($"  ... and {results.Length - 50} more");
            return string.Join("\n", lines);
        });

        // template <type> <name> - Inspect a specific template
        RegisterCommand("template", "<type> <name>", "Inspect a specific template", args =>
        {
            if (args.Length < 2)
                return "Usage: template <type> <name>";
            var typeName = args[0];
            if (!typeName.EndsWith("Template"))
                typeName += "Template";
            var name = string.Join(" ", args.Skip(1));
            var obj = GameQuery.FindByName(typeName, name);
            if (obj.IsNull)
                return $"No '{typeName}' with name '{name}' found";
            Inspect(obj);
            return $"Inspecting: {obj.GetTypeName()} '{obj.GetName()}'";
        });

        // scene - Show current scene name
        RegisterCommand("scene", "", "Show current scene name", args =>
        {
            try
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                return $"Current scene: {scene.name} (index {scene.buildIndex})";
            }
            catch (Exception ex)
            {
                return $"Error getting scene: {ex.Message}";
            }
        });

        // errors [modId] - Show recent errors
        RegisterCommand("errors", "[modId]", "Show recent errors (optionally filtered by mod)", args =>
        {
            var errors = ModError.RecentErrors;
            if (errors.Count == 0)
                return "No errors recorded";

            var modFilter = args.Length > 0 ? args[0] : null;
            var filtered = modFilter != null
                ? errors.Where(e => e.ModId.Equals(modFilter, StringComparison.OrdinalIgnoreCase)).ToList()
                : errors;

            if (filtered.Count == 0)
                return $"No errors for mod '{modFilter}'";

            var lines = new List<string> { $"Recent errors ({filtered.Count}):" };
            foreach (var e in filtered.TakeLast(20))
            {
                var countSuffix = e.OccurrenceCount > 1 ? $" (x{e.OccurrenceCount})" : "";
                lines.Add($"  [{e.Severity}] [{e.ModId}] {e.Message}{countSuffix}");
            }
            return string.Join("\n", lines);
        });

        // clear - Clear console output
        RegisterCommand("clear", "", "Clear console output", args =>
        {
            _commandHistory.Clear();
            _logBuffer.Clear();
            return null;
        });
    }

    /// <summary>
    /// Apply Harmony prefix patches on UnityEngine.Input mouse methods to suppress
    /// game-world clicks when the cursor is over the console. IMGUI event consumption
    /// alone is insufficient because game code reads Input.GetMouseButton* in Update().
    /// </summary>
    internal static void ApplyInputPatches(HarmonyLib.Harmony harmony)
    {
        try
        {
            var inputType = typeof(UnityEngine.Input);

            // Patch Input.GetMouseButton*(int) — the legacy mouse API
            var mousePrefix = new HarmonyMethod(typeof(DevConsole).GetMethod(
                nameof(PrefixBlockMouse), BindingFlags.NonPublic | BindingFlags.Static));
            foreach (var methodName in new[] { "GetMouseButtonDown", "GetMouseButton", "GetMouseButtonUp" })
            {
                var target = inputType.GetMethod(methodName, new[] { typeof(int) });
                if (target != null)
                    harmony.Patch(target, prefix: mousePrefix);
            }

            // Patch Input.GetKey*(KeyCode) — game may use KeyCode.Mouse0 instead
            var keyPrefix = new HarmonyMethod(typeof(DevConsole).GetMethod(
                nameof(PrefixBlockMouseKey), BindingFlags.NonPublic | BindingFlags.Static));
            foreach (var methodName in new[] { "GetKey", "GetKeyDown", "GetKeyUp" })
            {
                var target = inputType.GetMethod(methodName, new[] { typeof(KeyCode) });
                if (target != null)
                    harmony.Patch(target, prefix: keyPrefix);
            }
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Warning($"[DevConsole] Failed to apply input patches: {ex.Message}");
        }

        // Patch EventSystem.IsPointerOverGameObject — many Unity games check
        // this before processing world clicks; returning true blocks them.
        try
        {
            var esType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => { try { return a.GetType("UnityEngine.EventSystems.EventSystem"); } catch { return null; } })
                .FirstOrDefault(t => t != null);

            if (esType != null)
            {
                var poPrefix = new HarmonyMethod(typeof(DevConsole).GetMethod(
                    nameof(PrefixPointerOverGameObject), BindingFlags.NonPublic | BindingFlags.Static));

                var m1 = esType.GetMethod("IsPointerOverGameObject", Type.EmptyTypes);
                if (m1 != null) harmony.Patch(m1, prefix: poPrefix);

                var m2 = esType.GetMethod("IsPointerOverGameObject", new[] { typeof(int) });
                if (m2 != null) harmony.Patch(m2, prefix: poPrefix);
            }
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Warning($"[DevConsole] EventSystem patch skipped: {ex.Message}");
        }

        BattleLog.ApplyPatches(harmony);
    }

    private static bool PrefixBlockMouse(int __0, ref bool __result)
    {
        int button = __0;

        // Inject right-click to cancel tile selection after left-clicking on console
        if (button == 1 && Time.frameCount <= _injectRightClickUntilFrame)
        {
            __result = true;
            return false; // Skip original - return our injected true
        }

        // Block left-clicks if mouse is over console OR if we recently consumed a click
        if (button == 0 && (IsMouseOverConsole || (Time.frameCount - _lastClickConsumedFrame) <= 1))
        {
            __result = false;
            return false;
        }

        return true;
    }

    // Block Input.GetKey*(KeyCode.Mouse0 .. Mouse6) when cursor is over console
    private static bool PrefixBlockMouseKey(KeyCode __0, ref bool __result)
    {
        // Inject right-click (Mouse1) to cancel tile selection
        if (__0 == KeyCode.Mouse1 && Time.frameCount <= _injectRightClickUntilFrame)
        {
            __result = true;
            return false;
        }

        // Block left-click (Mouse0) if over console or recently consumed
        if (__0 == KeyCode.Mouse0)
        {
            if (IsMouseOverConsole || (Time.frameCount - _lastClickConsumedFrame) <= 1)
            {
                __result = false;
                return false;
            }
        }

        return true;
    }

    // Make EventSystem think the pointer is over a UI element so game
    // scripts that check IsPointerOverGameObject() skip world input.
    private static bool PrefixPointerOverGameObject(ref bool __result)
    {
        if (IsMouseOverConsole)
        {
            __result = true;
            return false;
        }
        return true;
    }

    internal static void Update()
    {
        try
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.BackQuote))
                IsVisible = !IsVisible;

            // Update mouse-over state using Input.mousePosition (screen coords,
            // Y=0 at bottom) vs _consoleRect (GUI coords, Y=0 at top).
            if (IsVisible && _consoleRect.width > 0)
            {
                var mp = UnityEngine.Input.mousePosition;
                float guiY = Screen.height - mp.y;
                _mouseOverConsole = _consoleRect.Contains(new Vector2(mp.x, guiY));
            }
            else
            {
                _mouseOverConsole = false;
            }
        }
        catch
        {
            // Input may not be available in all contexts
        }
    }

    internal static void Draw()
    {
        if (!IsVisible) return;

        try
        {
            InitializeStyles();

            // Console size
            float w = Math.Min(Screen.width * 0.6f, 900f);
            float h = Math.Min(Screen.height * 0.7f, 700f);

            // Clamp position to screen bounds
            _consolePosition.x = Mathf.Clamp(_consolePosition.x, 0, Screen.width - w);
            _consolePosition.y = Mathf.Clamp(_consolePosition.y, 0, Screen.height - h);

            _consoleRect = new Rect(_consolePosition.x, _consolePosition.y, w, h);

            // Track whether the mouse is over the console this frame so
            // Update()-based input handlers can skip game-world clicks.
            _mouseOverConsole = _consoleRect.Contains(Event.current.mousePosition);

            // Background
            GUI.Box(_consoleRect, "", _boxStyle);

            float cx = _consoleRect.x + Padding;
            float cy = _consoleRect.y + Padding;
            float cw = _consoleRect.width - Padding * 2;

            // Title bar (draggable)
            var titleRect = new Rect(cx, cy, cw - 30, TitleHeight);
            GUI.Label(titleRect, "Menace Modkit Console", _headerStyle);

            // Handle title bar dragging
            HandleTitleBarDrag(titleRect);
            if (GUI.Button(new Rect(cx + cw - 24, cy, 24, 20), "X"))
                IsVisible = false;
            cy += TitleHeight;

            // Tab bar
            float tabX = cx;
            for (int i = 0; i < _panels.Count; i++)
            {
                var style = i == _activePanel ? _tabActiveStyle : _tabInactiveStyle;
                float tw = _panels[i].Name.Length * 8 + 20;
                if (GUI.Button(new Rect(tabX, cy, tw, TabHeight), _panels[i].Name, style))
                    _activePanel = i;
                tabX += tw + 2;
            }
            cy += TabHeight + 4;

            // Panel content area
            var contentRect = new Rect(cx, cy, cw, _consoleRect.yMax - Padding - cy);

            if (_activePanel >= 0 && _activePanel < _panels.Count)
            {
                try
                {
                    _panels[_activePanel].DrawCallback?.Invoke(contentRect);
                }
                catch (Exception ex)
                {
                    GUI.Label(new Rect(contentRect.x, contentRect.y, contentRect.width, LineHeight),
                        $"Panel error: {ex.Message}", _errorStyle);

                    // Log the full exception once per distinct error so we can
                    // diagnose which method is failing (e.g. unstripped IL2CPP).
                    var key = $"{_panels[_activePanel].Name}:{ex.Message}";
                    if (key != _lastPanelError)
                    {
                        _lastPanelError = key;
                        MelonLoader.MelonLogger.Error($"[DevConsole] Panel '{_panels[_activePanel].Name}' threw:\n{ex}");
                    }
                }
            }

            // Consume any mouse events inside the console rect that weren't
            // already handled by a button/scroll/text field. This prevents
            // other OnGUI handlers from seeing stray clicks on the panel.
            var ev = Event.current;
            if (ev != null && _mouseOverConsole)
            {
                if (ev.type == EventType.MouseDown ||
                    ev.type == EventType.MouseUp ||
                    ev.type == EventType.MouseDrag ||
                    ev.type == EventType.ScrollWheel)
                {
                    // If this is a left-click, inject a right-click for the next few frames
                    // to cancel any tile selection the game might have started
                    if (ev.type == EventType.MouseDown && ev.button == 0)
                    {
                        _lastClickConsumedFrame = Time.frameCount;
                        _injectRightClickUntilFrame = Time.frameCount + 3; // Inject for a few frames
                    }
                    ev.Use();
                }
            }
        }
        catch (Exception ex)
        {
            MelonLoader.MelonLogger.Warning($"[DevConsole] Draw error: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle dragging the console window by the title bar.
    /// </summary>
    private static void HandleTitleBarDrag(Rect titleRect)
    {
        var ev = Event.current;
        if (ev == null) return;

        switch (ev.type)
        {
            case EventType.MouseDown:
                if (titleRect.Contains(ev.mousePosition) && ev.button == 0)
                {
                    _isDragging = true;
                    _dragOffset = ev.mousePosition - _consolePosition;
                    ev.Use();
                }
                break;

            case EventType.MouseUp:
                if (_isDragging && ev.button == 0)
                {
                    _isDragging = false;
                    ev.Use();
                }
                break;

            case EventType.MouseDrag:
                if (_isDragging)
                {
                    _consolePosition = ev.mousePosition - _dragOffset;
                    ev.Use();
                }
                break;
        }
    }

    // --- Built-in panels ---

    private static void DrawBattleLogPanel(Rect area)
    {
        InitializeStyles();
        float y = area.y;

        // Help text
        GUI.Label(new Rect(area.x, y, area.width, LineHeight),
            "Combat events from the current battle. Use filters to show/hide event types.", _helpStyle);
        y += LineHeight + 2;

        // Delegate to BattleLog with adjusted rect
        var adjustedArea = new Rect(area.x, y, area.width, area.height - LineHeight - 2);
        BattleLog.DrawPanel(adjustedArea);
    }

    /// <summary>
    /// Merged Log panel - shows both ModError entries and DevConsole.Log() messages
    /// in chronological order with severity filtering.
    /// </summary>
    private static void DrawLogPanel(Rect area)
    {
        InitializeStyles();
        float y = area.y;

        // Help text
        GUI.Label(new Rect(area.x, y, area.width, LineHeight),
            "Errors and log messages from mods. Filter by severity using the toggles.", _helpStyle);
        y += LineHeight + 2;

        float bx = area.x;

        // Clear button
        if (GUI.Button(new Rect(bx, y, 50, 20), "Clear"))
        {
            ModError.Clear();
            _logBuffer.Clear();
        }
        bx += 56;

        // Severity filter toggles
        DrawSeverityToggle(ref bx, y, "Err", ErrorSeverity.Error | ErrorSeverity.Fatal);
        DrawSeverityToggle(ref bx, y, "Warn", ErrorSeverity.Warning);
        DrawSeverityToggle(ref bx, y, "Info", ErrorSeverity.Info);

        // Per-mod filter toggles (built from current error list)
        var errors = ModError.RecentErrors;
        var modIds = new HashSet<string>();
        foreach (var e in errors)
            modIds.Add(e.ModId);

        if (modIds.Count > 1)
        {
            bx += 8;
            foreach (var modId in modIds.OrderBy(m => m))
            {
                bool excluded = _errorModExcludes.Contains(modId);
                string label = excluded ? $"[ ]{modId}" : $"[x]{modId}";
                float tw = label.Length * 7 + 12;
                if (bx + tw > area.x + area.width)
                {
                    y += 22;
                    bx = area.x;
                }
                var style = excluded ? _tabInactiveStyle : _tabActiveStyle;
                if (GUI.Button(new Rect(bx, y, tw, 20), label, style ?? GUI.skin.button))
                {
                    if (excluded) _errorModExcludes.Remove(modId);
                    else _errorModExcludes.Add(modId);
                }
                bx += tw + 2;
            }
        }

        y += 24;

        // Build merged log entries: errors + log messages, sorted by timestamp
        var entries = new List<(DateTime Time, string Text, GUIStyle Style)>();

        // Add error entries
        foreach (var entry in errors)
        {
            if ((_errorSeverityFilter & entry.Severity) == 0) continue;
            if (_errorModExcludes.Contains(entry.ModId)) continue;

            var style = entry.Severity switch
            {
                ErrorSeverity.Error or ErrorSeverity.Fatal => _errorStyle,
                ErrorSeverity.Warning => _warnStyle,
                _ => _infoStyle
            };

            var countSuffix = entry.OccurrenceCount > 1 ? $" (x{entry.OccurrenceCount})" : "";
            var text = $"[{entry.Timestamp:HH:mm:ss}] [{entry.Severity}] [{entry.ModId}] {entry.Message}{countSuffix}";
            entries.Add((entry.Timestamp, text, style));
        }

        // Add log buffer entries (parse timestamp from format "[HH:mm:ss] message")
        foreach (var line in _logBuffer)
        {
            // Only show log entries if Info filter is on
            if ((_errorSeverityFilter & ErrorSeverity.Info) == 0) continue;

            DateTime time = DateTime.Now;
            if (line.Length > 10 && line[0] == '[' && line[9] == ']')
            {
                if (TimeSpan.TryParse(line.Substring(1, 8), out var ts))
                    time = DateTime.Today.Add(ts);
            }
            entries.Add((time, $"[Log] {line}", _labelStyle));
        }

        // Sort by time (oldest first for display)
        entries.Sort((a, b) => a.Time.CompareTo(b.Time));

        // Scrollable list (manual scroll — GUI.BeginScrollView not unstripped)
        float scrollHeight = area.yMax - y;
        float contentHeight = entries.Count * LineHeight;
        var viewRect = new Rect(area.x, y, area.width, scrollHeight);
        _errorScroll.y = HandleScrollWheel(viewRect, contentHeight, _errorScroll.y);

        GUI.BeginGroup(viewRect);
        float sy = -_errorScroll.y;
        foreach (var (_, text, style) in entries)
        {
            if (sy + LineHeight > 0 && sy < scrollHeight)
            {
                GUI.Label(new Rect(0, sy, viewRect.width, LineHeight), text, style);
            }
            sy += LineHeight;
        }

        GUI.EndGroup();
    }

    private static void DrawInspectorPanel(Rect area)
    {
        InitializeStyles();
        float y = area.y;

        // Help text
        GUI.Label(new Rect(area.x, y, area.width, LineHeight),
            "Inspect game objects. Use 'inspect <type> <name>' in Console to select.", _helpStyle);
        y += LineHeight + 2;

        if (_inspectedObj.IsNull)
        {
            GUI.Label(new Rect(area.x, y, area.width, LineHeight),
                "No object selected.", _labelStyle);
            return;
        }

        var typeName = _inspectedObj.GetTypeName();
        var objName = _inspectedObj.GetName() ?? "<unnamed>";
        GUI.Label(new Rect(area.x, y, area.width, 20),
            $"{typeName} - '{objName}' @ 0x{_inspectedObj.Pointer:X}", _headerStyle);
        y += 20;

        GUI.Label(new Rect(area.x, y, area.width, LineHeight),
            $"Alive: {_inspectedObj.IsAlive}", _labelStyle);
        y += LineHeight + 4;

        // Build property list
        var propLines = new List<(string name, string value, GUIStyle style)>();
        var gameType = _inspectedObj.GetGameType();
        var managedType = gameType?.ManagedType;

        if (managedType != null)
        {
            try
            {
                var ptrCtor = managedType.GetConstructor(new[] { typeof(IntPtr) });
                if (ptrCtor != null)
                {
                    var proxy = ptrCtor.Invoke(new object[] { _inspectedObj.Pointer });
                    var props = managedType.GetProperties(
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.Instance);

                    foreach (var prop in props.OrderBy(p => p.Name))
                    {
                        if (!prop.CanRead) continue;
                        if (prop.Name is "Pointer" or "WasCollected" or "ObjectClass") continue;

                        string value;
                        try
                        {
                            var val = prop.GetValue(proxy);
                            value = val?.ToString() ?? "null";
                            if (value.Length > 120) value = value[..120] + "...";
                        }
                        catch
                        {
                            value = "<error reading>";
                        }

                        propLines.Add(($"  {prop.Name}", $"= {value}", _labelStyle));
                    }
                }
            }
            catch (Exception ex)
            {
                propLines.Add(("", $"Reflection error: {ex.Message}", _errorStyle));
            }
        }
        else
        {
            propLines.Add(("", "No managed type available for reflection.", _warnStyle));
        }

        // Scrollable property list (manual scroll)
        float scrollHeight = area.yMax - y;
        float contentHeight = propLines.Count * LineHeight;
        var viewRect = new Rect(area.x, y, area.width, scrollHeight);
        _inspectorScroll.y = HandleScrollWheel(viewRect, contentHeight, _inspectorScroll.y);

        GUI.BeginGroup(viewRect);
        float sy = -_inspectorScroll.y;
        foreach (var (name, value, style) in propLines)
        {
            if (sy + LineHeight > 0 && sy < scrollHeight)
            {
                if (!string.IsNullOrEmpty(name))
                {
                    GUI.Label(new Rect(0, sy, 250, LineHeight), name, _labelStyle);
                    GUI.Label(new Rect(254, sy, viewRect.width - 254, LineHeight), value, style);
                }
                else
                {
                    GUI.Label(new Rect(0, sy, viewRect.width, LineHeight), value, style);
                }
            }
            sy += LineHeight;
        }
        GUI.EndGroup();
    }

    private static void DrawWatchPanel(Rect area)
    {
        InitializeStyles();
        float y = area.y;

        // Help text
        GUI.Label(new Rect(area.x, y, area.width, LineHeight),
            "Live value monitoring. Use DevConsole.Watch() to add expressions.", _helpStyle);
        y += LineHeight + 2;

        if (_watches.Count == 0)
        {
            GUI.Label(new Rect(area.x, y, area.width, LineHeight),
                "No watches added.", _labelStyle);
            return;
        }

        for (int i = _watches.Count - 1; i >= 0; i--)
        {
            var (label, getter) = _watches[i];

            string value;
            try
            {
                value = getter() ?? "null";
            }
            catch (Exception ex)
            {
                value = $"<error: {ex.Message}>";
            }

            GUI.Label(new Rect(area.x, y, 250, LineHeight), $"  {label}", _labelStyle);
            GUI.Label(new Rect(area.x + 254, y, area.width - 284, LineHeight), $"= {value}", _labelStyle);
            if (GUI.Button(new Rect(area.x + area.width - 24, y, 20, LineHeight), "X"))
                _watches.RemoveAt(i);
            y += LineHeight;

            if (y > area.yMax) break;
        }
    }

    // --- Commands panel (keyboard-captured input, no GUI.TextField) ---

    /// <summary>
    /// Console panel - merged Commands + REPL. Commands are tried first,
    /// then REPL evaluation if available.
    /// </summary>
    private static void DrawConsolePanel(Rect area)
    {
        InitializeStyles();

        float y = area.y;

        // Help text
        GUI.Label(new Rect(area.x, y, area.width, LineHeight),
            "Type 'help' to see available commands.", _helpStyle);
        y += LineHeight + 2;

        // Adjust area for help text
        area = new Rect(area.x, y, area.width, area.height - LineHeight - 2);

        // Handle keyboard input for the command line
        var e = Event.current;
        if (e != null && e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                ExecuteCommand(_commandInput);
                _commandInputHistory.Add(_commandInput);
                _commandInput = "";
                _commandHistoryNav = -1;
                e.Use();
            }
            else if (e.keyCode == KeyCode.Backspace)
            {
                if (_commandInput.Length > 0)
                    _commandInput = _commandInput[..^1];
                e.Use();
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                _commandInput = "";
                _commandHistoryNav = -1;
                e.Use();
            }
            else if (e.keyCode == KeyCode.UpArrow)
            {
                if (_commandInputHistory.Count > 0)
                {
                    if (_commandHistoryNav == -1)
                        _commandHistoryNav = _commandInputHistory.Count - 1;
                    else if (_commandHistoryNav > 0)
                        _commandHistoryNav--;
                    _commandInput = _commandInputHistory[_commandHistoryNav];
                }
                e.Use();
            }
            else if (e.keyCode == KeyCode.DownArrow)
            {
                if (_commandHistoryNav >= 0)
                {
                    _commandHistoryNav++;
                    if (_commandHistoryNav >= _commandInputHistory.Count)
                    {
                        _commandHistoryNav = -1;
                        _commandInput = "";
                    }
                    else
                    {
                        _commandInput = _commandInputHistory[_commandHistoryNav];
                    }
                }
                e.Use();
            }
            else if (e.character != 0 && !char.IsControl(e.character))
            {
                _commandInput += e.character;
                e.Use();
            }
        }

        // Layout: output history at top, input prompt at bottom
        float inputBarHeight = 24f;
        float outputHeight = area.height - inputBarHeight - 4;

        // Output history (scrollable)
        float contentHeight = 0;
        foreach (var entry in _commandHistory)
        {
            contentHeight += LineHeight; // input line
            var outputLines = entry.Output?.Split('\n') ?? Array.Empty<string>();
            contentHeight += Math.Max(1, outputLines.Length) * LineHeight;
            contentHeight += 2; // spacing
        }

        var outputRect = new Rect(area.x, area.y, area.width, outputHeight);
        _commandScroll.y = HandleScrollWheel(outputRect, contentHeight, _commandScroll.y);

        GUI.BeginGroup(outputRect);
        float sy = -_commandScroll.y;
        foreach (var (input, output, isError) in _commandHistory)
        {
            // Input line
            if (sy + LineHeight > 0 && sy < outputHeight)
                GUI.Label(new Rect(0, sy, outputRect.width, LineHeight), $"> {input}", _infoStyle);
            sy += LineHeight;

            // Output line(s)
            if (!string.IsNullOrEmpty(output))
            {
                var style = isError ? _errorStyle : _labelStyle;
                foreach (var line in output.Split('\n'))
                {
                    if (sy + LineHeight > 0 && sy < outputHeight)
                        GUI.Label(new Rect(8, sy, outputRect.width - 8, LineHeight), line, style);
                    sy += LineHeight;
                }
            }
            else
            {
                sy += LineHeight;
            }
            sy += 2;
        }
        GUI.EndGroup();

        // Input prompt at bottom
        float iy = area.y + outputHeight + 4;

        // Blinking cursor
        bool cursorVisible = ((int)(Time.time * 2)) % 2 == 0;
        string cursor = cursorVisible ? "|" : "";
        GUI.Label(new Rect(area.x, iy, area.width, inputBarHeight),
            $"> {_commandInput}{cursor}", _infoStyle);

        // Run button
        if (GUI.Button(new Rect(area.x + area.width - 44, iy, 40, 20), "Run"))
        {
            ExecuteCommand(_commandInput);
            _commandInputHistory.Add(_commandInput);
            _commandInput = "";
            _commandHistoryNav = -1;
        }
    }

    private static void ExecuteCommand(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var cmdName = parts[0];
        var args = parts.Skip(1).ToArray();

        // First try registered commands
        if (_commands.TryGetValue(cmdName, out var cmd))
        {
            try
            {
                var result = cmd.Handler(args);
                _commandHistory.Add((input, result ?? "(ok)", false));
            }
            catch (Exception ex)
            {
                _commandHistory.Add((input, $"Error: {ex.Message}", true));
            }
        }
        // If no command matches and REPL is available, evaluate as C# expression
        else if (_replEvaluator != null)
        {
            try
            {
                var result = _replEvaluator.Evaluate(input);
                if (result.Success)
                {
                    _commandHistory.Add((input, result.DisplayText ?? "null", false));
                }
                else
                {
                    _commandHistory.Add((input, result.Error ?? "Unknown error", true));
                }
            }
            catch (Exception ex)
            {
                _commandHistory.Add((input, $"REPL error: {ex.Message}", true));
            }
        }
        else
        {
            _commandHistory.Add((input, $"Unknown command: {cmdName}. Type 'help' for available commands.", true));
        }

        // Keep history bounded
        while (_commandHistory.Count > 100)
            _commandHistory.RemoveAt(0);

        // Auto-scroll to bottom
        _commandScroll.y = float.MaxValue;
    }

    private static void DrawSeverityToggle(ref float x, float y, string label, ErrorSeverity mask)
    {
        bool isOn = (_errorSeverityFilter & mask) != 0;
        string text = isOn ? $"[x]{label}" : $"[ ]{label}";
        float tw = text.Length * 7 + 12;
        var style = isOn ? (_tabActiveStyle ?? GUI.skin.button) : (_tabInactiveStyle ?? GUI.skin.button);
        if (GUI.Button(new Rect(x, y, tw, 20), text, style))
            _errorSeverityFilter ^= mask;
        x += tw + 2;
    }

    // --- Manual scroll helper (GUI.BeginScrollView is not unstripped in IL2CPP) ---

    /// <summary>
    /// Handle mouse-wheel scrolling over a rect. Returns updated scroll-Y.
    /// Use with GUI.BeginGroup/EndGroup for clipping instead of BeginScrollView.
    /// </summary>
    internal static float HandleScrollWheel(Rect viewRect, float contentHeight, float currentScrollY)
    {
        var e = Event.current;
        if (e != null && e.type == EventType.ScrollWheel && viewRect.Contains(e.mousePosition))
        {
            currentScrollY += e.delta.y * LineHeight * 3;
            e.Use();
        }
        float maxScroll = Math.Max(0, contentHeight - viewRect.height);
        return Math.Max(0, Math.Min(currentScrollY, maxScroll));
    }

    // --- Style initialization ---

    private static void InitializeStyles()
    {
        // Texture2D objects are destroyed on scene transitions even though
        // managed references survive.  Detect this and re-create everything.
        if (_stylesInitialized)
        {
            try
            {
                if (_boxStyle != null && _boxStyle.normal.background == null)
                    _stylesInitialized = false;
            }
            catch { _stylesInitialized = false; }
        }

        if (_stylesInitialized) return;
        _stylesInitialized = true;

        // Semi-transparent dark background
        var bgTex = new Texture2D(1, 1);
        bgTex.hideFlags = HideFlags.HideAndDontSave;
        bgTex.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.12f, 0.92f));
        bgTex.Apply();

        _boxStyle = new GUIStyle(GUI.skin.box);
        _boxStyle.normal.background = bgTex;

        var tabActiveBg = new Texture2D(1, 1);
        tabActiveBg.hideFlags = HideFlags.HideAndDontSave;
        tabActiveBg.SetPixel(0, 0, new Color(0.25f, 0.25f, 0.3f, 1f));
        tabActiveBg.Apply();

        var tabInactiveBg = new Texture2D(1, 1);
        tabInactiveBg.hideFlags = HideFlags.HideAndDontSave;
        tabInactiveBg.SetPixel(0, 0, new Color(0.15f, 0.15f, 0.18f, 1f));
        tabInactiveBg.Apply();

        _tabActiveStyle = new GUIStyle(GUI.skin.button);
        _tabActiveStyle.normal.background = tabActiveBg;
        _tabActiveStyle.normal.textColor = Color.white;
        _tabActiveStyle.fontStyle = FontStyle.Bold;

        _tabInactiveStyle = new GUIStyle(GUI.skin.button);
        _tabInactiveStyle.normal.background = tabInactiveBg;
        _tabInactiveStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

        _labelStyle = new GUIStyle(GUI.skin.label);
        _labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        _labelStyle.fontSize = 13;
        _labelStyle.wordWrap = true;

        _errorStyle = new GUIStyle(_labelStyle);
        _errorStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);

        _warnStyle = new GUIStyle(_labelStyle);
        _warnStyle.normal.textColor = new Color(1f, 0.8f, 0.3f);

        _infoStyle = new GUIStyle(_labelStyle);
        _infoStyle.normal.textColor = new Color(0.5f, 0.8f, 1f);

        _headerStyle = new GUIStyle(_labelStyle);
        _headerStyle.fontSize = 15;
        _headerStyle.fontStyle = FontStyle.Bold;
        _headerStyle.normal.textColor = Color.white;

        _helpStyle = new GUIStyle(_labelStyle);
        _helpStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
        _helpStyle.fontStyle = FontStyle.Italic;
    }
}
