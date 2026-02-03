using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// IMGUI-based developer console overlay. Toggle with backtick/tilde (~) key.
/// Supports a tabbed panel system with built-in Errors, Log, Inspector, and Watch panels.
/// </summary>
public static class DevConsole
{
    public static bool IsVisible { get; set; }

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
    private static string _errorModFilter = "";

    // Log panel state
    private static readonly List<string> _logBuffer = new();
    private static readonly int LogBufferMax = 200;
    private static Vector2 _logScroll;

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

    // Layout
    private static Rect _consoleRect;
    private const float TabHeight = 30f;
    private const float Padding = 8f;

    private class PanelEntry
    {
        public string Name;
        public Action DrawCallback;
    }

    /// <summary>
    /// Register a custom panel in the console.
    /// </summary>
    public static void RegisterPanel(string name, Action drawCallback)
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

    // --- Internal lifecycle ---

    internal static void Initialize()
    {
        // Register built-in panels
        _panels.Clear();
        _panels.Add(new PanelEntry { Name = "Errors", DrawCallback = DrawErrorsPanel });
        _panels.Add(new PanelEntry { Name = "Log", DrawCallback = DrawLogPanel });
        _panels.Add(new PanelEntry { Name = "Inspector", DrawCallback = DrawInspectorPanel });
        _panels.Add(new PanelEntry { Name = "Watch", DrawCallback = DrawWatchPanel });
    }

    internal static void Update()
    {
        try
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.BackQuote))
                IsVisible = !IsVisible;
        }
        catch
        {
            // Input may not be available in all contexts
        }
    }

    internal static void Draw()
    {
        if (!IsVisible) return;

        InitializeStyles();

        // Console occupies top-left portion of screen
        float w = Math.Min(Screen.width * 0.6f, 900f);
        float h = Math.Min(Screen.height * 0.7f, 700f);
        _consoleRect = new Rect(10, 10, w, h);

        GUI.Box(_consoleRect, "", _boxStyle);

        GUILayout.BeginArea(new Rect(
            _consoleRect.x + Padding,
            _consoleRect.y + Padding,
            _consoleRect.width - Padding * 2,
            _consoleRect.height - Padding * 2));

        // Title bar
        GUILayout.BeginHorizontal();
        GUILayout.Label("Menace SDK Console", _headerStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("X", GUILayout.Width(24), GUILayout.Height(20)))
            IsVisible = false;
        GUILayout.EndHorizontal();

        // Tab bar
        GUILayout.BeginHorizontal();
        for (int i = 0; i < _panels.Count; i++)
        {
            var style = i == _activePanel ? _tabActiveStyle : _tabInactiveStyle;
            if (GUILayout.Button(_panels[i].Name, style, GUILayout.Height(TabHeight)))
                _activePanel = i;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // Active panel content
        if (_activePanel >= 0 && _activePanel < _panels.Count)
        {
            try
            {
                _panels[_activePanel].DrawCallback?.Invoke();
            }
            catch (Exception ex)
            {
                GUILayout.Label($"Panel error: {ex.Message}", _errorStyle);
            }
        }

        GUILayout.EndArea();
    }

    // --- Built-in panels ---

    private static void DrawErrorsPanel()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Filter:", _labelStyle, GUILayout.Width(40));
        _errorModFilter = GUILayout.TextField(_errorModFilter, GUILayout.Width(200));
        if (GUILayout.Button("Clear", GUILayout.Width(50)))
            ModError.Clear();
        GUILayout.EndHorizontal();

        _errorScroll = GUILayout.BeginScrollView(_errorScroll);

        var errors = ModError.RecentErrors;
        for (int i = errors.Count - 1; i >= 0; i--)
        {
            var entry = errors[i];
            if (!string.IsNullOrEmpty(_errorModFilter) &&
                !entry.ModId.Contains(_errorModFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            var style = entry.Severity switch
            {
                ErrorSeverity.Error or ErrorSeverity.Fatal => _errorStyle,
                ErrorSeverity.Warning => _warnStyle,
                _ => _infoStyle
            };

            var countSuffix = entry.OccurrenceCount > 1 ? $" (x{entry.OccurrenceCount})" : "";
            GUILayout.Label(
                $"[{entry.Timestamp:HH:mm:ss}] [{entry.Severity}] [{entry.ModId}] {entry.Message}{countSuffix}",
                style);
        }

        GUILayout.EndScrollView();
    }

    private static void DrawLogPanel()
    {
        _logScroll = GUILayout.BeginScrollView(_logScroll);

        foreach (var line in _logBuffer)
            GUILayout.Label(line, _labelStyle);

        GUILayout.EndScrollView();
    }

    private static void DrawInspectorPanel()
    {
        if (_inspectedObj.IsNull)
        {
            GUILayout.Label("No object selected. Use DevConsole.Inspect(obj) to inspect.", _labelStyle);
            return;
        }

        var typeName = _inspectedObj.GetTypeName();
        var objName = _inspectedObj.GetName() ?? "<unnamed>";
        GUILayout.Label($"{typeName} - '{objName}' @ 0x{_inspectedObj.Pointer:X}", _headerStyle);
        GUILayout.Label($"Alive: {_inspectedObj.IsAlive}", _labelStyle);

        GUILayout.Space(4);

        _inspectorScroll = GUILayout.BeginScrollView(_inspectorScroll);

        // Try to enumerate fields via managed reflection
        var gameType = _inspectedObj.GetGameType();
        var managedType = gameType?.ManagedType;
        if (managedType != null)
        {
            try
            {
                // Create a proxy wrapper to read properties
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
                        // Skip known problematic properties
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

                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"  {prop.Name}", _labelStyle, GUILayout.Width(250));
                        GUILayout.Label($"= {value}", _labelStyle);
                        GUILayout.EndHorizontal();
                    }
                }
            }
            catch (Exception ex)
            {
                GUILayout.Label($"Reflection error: {ex.Message}", _errorStyle);
            }
        }
        else
        {
            GUILayout.Label("No managed type available for reflection.", _warnStyle);
        }

        GUILayout.EndScrollView();
    }

    private static void DrawWatchPanel()
    {
        if (_watches.Count == 0)
        {
            GUILayout.Label("No watches. Use DevConsole.Watch(label, getter) to add.", _labelStyle);
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

            GUILayout.BeginHorizontal();
            GUILayout.Label($"  {label}", _labelStyle, GUILayout.Width(250));
            GUILayout.Label($"= {value}", _labelStyle);
            if (GUILayout.Button("X", GUILayout.Width(20)))
                _watches.RemoveAt(i);
            GUILayout.EndHorizontal();
        }
    }

    // --- Style initialization ---

    private static void InitializeStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        // Semi-transparent dark background
        var bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.12f, 0.92f));
        bgTex.Apply();

        _boxStyle = new GUIStyle(GUI.skin.box);
        _boxStyle.normal.background = bgTex;

        var tabActiveBg = new Texture2D(1, 1);
        tabActiveBg.SetPixel(0, 0, new Color(0.25f, 0.25f, 0.3f, 1f));
        tabActiveBg.Apply();

        var tabInactiveBg = new Texture2D(1, 1);
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
    }
}
