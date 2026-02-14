using System;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// Small unobtrusive error notification at bottom-left of screen.
/// Shows "N mod errors - press ~ for console" when errors exist and
/// the DevConsole is not visible. Auto-fades after 8 seconds of no new errors.
/// </summary>
internal static class ErrorNotification
{
    private static int _lastErrorCount;
    private static float _lastErrorTime;
    private const float FadeDuration = 8f;

    private static GUIStyle _notifStyle;
    private static bool _styleInitialized;

    internal static void Draw()
    {
        // Don't show if console is visible
        if (DevConsole.IsVisible) return;

        var errors = ModError.RecentErrors;
        var errorCount = 0;
        foreach (var e in errors)
        {
            if (e.Severity >= ErrorSeverity.Warning)
                errorCount++;
        }

        if (errorCount == 0) return;

        // Track new errors for fade timer
        if (errorCount != _lastErrorCount)
        {
            _lastErrorCount = errorCount;
            _lastErrorTime = Time.unscaledTime;
        }

        var elapsed = Time.unscaledTime - _lastErrorTime;
        if (elapsed > FadeDuration) return;

        // Fade alpha
        var alpha = elapsed < FadeDuration - 2f
            ? 1f
            : 1f - (elapsed - (FadeDuration - 2f)) / 2f;

        InitializeStyle();

        var prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));

        var text = errorCount == 1
            ? "1 mod error - press ~ for console"
            : $"{errorCount} mod errors - press ~ for console";

        var size = _notifStyle.CalcSize(new GUIContent(text));
        var rect = new Rect(
            10,
            Screen.height - size.y - 10,
            size.x + 16,
            size.y + 4);

        GUI.Label(rect, text, _notifStyle);
        GUI.color = prevColor;
    }

    private static void InitializeStyle()
    {
        // Check if texture was destroyed (e.g., scene transition) and reinitialize
        if (_notifStyle?.normal?.background == null || !_notifStyle.normal.background)
        {
            _styleInitialized = false;
        }

        if (_styleInitialized) return;
        _styleInitialized = true;

        var bgTex = new Texture2D(1, 1)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        bgTex.SetPixel(0, 0, new Color(0.15f, 0.05f, 0.05f, 0.85f));
        bgTex.Apply();

        _notifStyle = new GUIStyle(GUI.skin.label);
        _notifStyle.normal.background = bgTex;
        _notifStyle.normal.textColor = new Color(1f, 0.6f, 0.4f);
        _notifStyle.fontSize = 13;
        _notifStyle.padding = new RectOffset(8, 8, 2, 2);
    }
}
