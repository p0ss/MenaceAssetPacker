using System;
using UnityEngine;

namespace Menace.SDK.Repl;

/// <summary>
/// IMGUI REPL panel for the DevConsole. Provides a text input field, submit on Enter,
/// scrollable output, and history navigation with up/down arrows.
/// Uses raw GUI.* calls for IL2CPP compatibility.
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
    private const float LineHeight = 18f;
    private const float InputHeight = 22f;

    internal static void Initialize(ConsoleEvaluator evaluator)
    {
        _evaluator = evaluator;
        _initialized = evaluator != null;
        // Panel registration removed - REPL is now integrated into the Console panel
    }

    /// <summary>
    /// Returns true if the REPL evaluator is available.
    /// </summary>
    internal static bool IsAvailable => _initialized && _evaluator != null;

    /// <summary>
    /// Evaluate a C# expression and return the result.
    /// </summary>
    internal static ConsoleEvaluator.EvalResult Evaluate(string input)
    {
        if (!IsAvailable)
            return new ConsoleEvaluator.EvalResult { Success = false, Error = "REPL not available" };
        return _evaluator.Evaluate(input);
    }

    internal static void Draw(Rect area)
    {
        if (!_initialized || _evaluator == null)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, LineHeight),
                "REPL not initialized. Roslyn may not be available.");
            return;
        }

        InitializeStyles();

        // Handle input events before drawing controls
        var e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            HandleInputKeys(e);
        }

        // Layout: output scroll at top, input bar at bottom
        float inputBarHeight = InputHeight + 4;
        float outputHeight = area.height - inputBarHeight;

        // Output area (manual scroll — GUI.BeginScrollView not unstripped)
        var history = _evaluator.History;
        float contentHeight = 0;
        foreach (var _ in history)
            contentHeight += LineHeight * 2 + 2;

        var outputRect = new Rect(area.x, area.y, area.width, outputHeight);
        _outputScroll.y = DevConsole.HandleScrollWheel(outputRect, contentHeight, _outputScroll.y);

        GUI.BeginGroup(outputRect);
        float sy = -_outputScroll.y;
        foreach (var (input, result) in history)
        {
            if (sy + LineHeight > 0 && sy < outputHeight)
                GUI.Label(new Rect(0, sy, outputRect.width, LineHeight), $"> {input}", _promptStyle);
            sy += LineHeight;

            if (sy + LineHeight > 0 && sy < outputHeight)
            {
                if (result.Success)
                {
                    var style = result.Value == null ? _nullStyle : _successStyle;
                    GUI.Label(new Rect(0, sy, outputRect.width, LineHeight), $"  {result.DisplayText}", style);
                }
                else
                {
                    GUI.Label(new Rect(0, sy, outputRect.width, LineHeight), $"  Error: {result.Error}", _errorStyle);
                }
            }
            sy += LineHeight + 2;
        }
        GUI.EndGroup();

        // Input bar
        float iy = area.y + outputHeight + 2;
        float promptWidth = 16;
        float runWidth = 44;
        float fieldWidth = area.width - promptWidth - runWidth - 8;

        GUI.Label(new Rect(area.x, iy, promptWidth, InputHeight), ">", _promptStyle);

        GUI.SetNextControlName(InputControlName);
        _inputText = GUI.TextField(
            new Rect(area.x + promptWidth + 2, iy, fieldWidth, InputHeight),
            _inputText ?? "", _inputStyle);
        GUI.FocusControl(InputControlName);

        if (GUI.Button(new Rect(area.x + promptWidth + fieldWidth + 6, iy, runWidth, InputHeight), "Run"))
        {
            SubmitInput();
        }
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
