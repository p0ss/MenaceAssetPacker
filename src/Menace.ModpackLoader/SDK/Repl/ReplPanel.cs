using System;
using UnityEngine;

namespace Menace.SDK.Repl;

/// <summary>
/// IMGUI REPL panel for the DevConsole. Provides a text input field, submit on Enter,
/// scrollable output, and history navigation with up/down arrows.
/// </summary>
internal static class ReplPanel
{
    private static ConsoleEvaluator _evaluator;
    private static string _inputText = "";
    private static Vector2 _outputScroll;
    private static int _historyIndex = -1;
    private static bool _initialized;

    // GUI styles
    private static GUIStyle _inputStyle;
    private static GUIStyle _outputStyle;
    private static GUIStyle _successStyle;
    private static GUIStyle _errorStyle;
    private static GUIStyle _nullStyle;
    private static GUIStyle _promptStyle;
    private static bool _stylesInitialized;

    private const string InputControlName = "ReplInput";

    internal static void Initialize(ConsoleEvaluator evaluator)
    {
        _evaluator = evaluator;
        _initialized = evaluator != null;

        if (_initialized)
        {
            DevConsole.RegisterPanel("REPL", Draw);
        }
    }

    internal static void Draw()
    {
        if (!_initialized || _evaluator == null)
        {
            GUILayout.Label("REPL not initialized. Roslyn may not be available.");
            return;
        }

        InitializeStyles();

        // Output area
        _outputScroll = GUILayout.BeginScrollView(_outputScroll, GUILayout.ExpandHeight(true));

        foreach (var (input, result) in _evaluator.History)
        {
            // Show input
            GUILayout.Label($"> {input}", _promptStyle);

            // Show result
            if (result.Success)
            {
                var style = result.Value == null ? _nullStyle : _successStyle;
                GUILayout.Label($"  {result.DisplayText}", style);
            }
            else
            {
                GUILayout.Label($"  Error: {result.Error}", _errorStyle);
            }

            GUILayout.Space(2);
        }

        GUILayout.EndScrollView();

        // Input area
        GUILayout.BeginHorizontal();
        GUILayout.Label(">", _promptStyle, GUILayout.Width(14));

        // Handle input events before the text field
        var e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            HandleInputKeys(e);
        }

        GUI.SetNextControlName(InputControlName);
        _inputText = GUILayout.TextField(_inputText, _inputStyle);
        GUI.FocusControl(InputControlName);

        if (GUILayout.Button("Run", GUILayout.Width(40)))
        {
            SubmitInput();
        }

        GUILayout.EndHorizontal();
    }

    private static void HandleInputKeys(Event e)
    {
        if (GUI.GetNameOfFocusedControl() != InputControlName)
            return;

        switch (e.keyCode)
        {
            case KeyCode.Return or KeyCode.KeypadEnter:
                SubmitInput();
                e.Use();
                break;

            case KeyCode.UpArrow:
                NavigateHistory(-1);
                e.Use();
                break;

            case KeyCode.DownArrow:
                NavigateHistory(1);
                e.Use();
                break;
        }
    }

    private static void SubmitInput()
    {
        var input = _inputText?.Trim();
        if (string.IsNullOrEmpty(input)) return;

        _inputText = "";
        _historyIndex = -1;

        try
        {
            _evaluator.Evaluate(input);
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("ReplPanel", $"Evaluation failed: {ex.Message}", ex);
        }

        // Auto-scroll to bottom
        _outputScroll.y = float.MaxValue;
    }

    private static void NavigateHistory(int direction)
    {
        var history = _evaluator.History;
        if (history.Count == 0) return;

        if (_historyIndex == -1)
        {
            // Start navigating from the end
            _historyIndex = direction < 0 ? history.Count - 1 : 0;
        }
        else
        {
            _historyIndex += direction;
        }

        _historyIndex = Math.Clamp(_historyIndex, 0, history.Count - 1);
        _inputText = history[_historyIndex].Input;
    }

    private static void InitializeStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        _inputStyle = new GUIStyle(GUI.skin.textField);
        _inputStyle.fontSize = 13;
        _inputStyle.normal.textColor = Color.white;

        _outputStyle = new GUIStyle(GUI.skin.label);
        _outputStyle.fontSize = 13;
        _outputStyle.wordWrap = true;
        _outputStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

        _successStyle = new GUIStyle(_outputStyle);
        _successStyle.normal.textColor = new Color(0.4f, 0.9f, 0.4f);

        _errorStyle = new GUIStyle(_outputStyle);
        _errorStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);

        _nullStyle = new GUIStyle(_outputStyle);
        _nullStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

        _promptStyle = new GUIStyle(_outputStyle);
        _promptStyle.normal.textColor = new Color(0.5f, 0.8f, 1f);
        _promptStyle.fontStyle = FontStyle.Bold;
    }
}
