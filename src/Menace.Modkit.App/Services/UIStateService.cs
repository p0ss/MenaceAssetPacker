using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Exposes UI state to external tools (like MCP server) via a shared file.
/// This allows AI assistants to "see" what the user sees in the modkit.
/// State is written to ~/.menace-modkit/ui-state.json
/// </summary>
public sealed class UIStateService : IDisposable
{
    private static readonly Lazy<UIStateService> _instance = new(() => new UIStateService());
    public static UIStateService Instance => _instance.Value;

    private static readonly string StateFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".menace-modkit",
        "ui-state.json"
    );

    private CancellationTokenSource? _cts;
    private Window? _mainWindow;
    private Func<string>? _currentSectionGetter;
    private Func<string>? _currentViewGetter;
    private Timer? _updateTimer;

    private UIStateService() { }

    /// <summary>
    /// Start writing UI state to the shared file.
    /// Call this from App.OnFrameworkInitializationCompleted after the main window is created.
    /// </summary>
    public void Start(Window mainWindow, Func<string> currentSectionGetter, Func<string>? currentViewGetter = null)
    {
        _mainWindow = mainWindow;
        _currentSectionGetter = currentSectionGetter;
        _currentViewGetter = currentViewGetter;
        _cts = new CancellationTokenSource();

        // Ensure directory exists
        var dir = Path.GetDirectoryName(StateFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Write state immediately and then periodically
        WriteStateToFile();
        _updateTimer = new Timer(_ => WriteStateToFile(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        ModkitLog.Info($"[UIStateService] Writing UI state to {StateFilePath}");
    }

    private void WriteStateToFile()
    {
        if (_cts?.IsCancellationRequested == true) return;

        try
        {
            var state = GetUIState();
            if (state == null) return;

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Write atomically via temp file
            var tempPath = StateFilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, StateFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            // Don't spam logs - this runs every second
            System.Diagnostics.Debug.WriteLine($"[UIStateService] Write failed: {ex.Message}");
        }
    }

    private object? GetUIState()
    {
        // Must run on UI thread to access visual tree
        try
        {
            return Dispatcher.UIThread.Invoke(() =>
            {
                var state = new UIState
                {
                    CurrentSection = _currentSectionGetter?.Invoke() ?? "Unknown",
                    CurrentView = _currentViewGetter?.Invoke(),
                    Timestamp = DateTime.UtcNow.ToString("o")
                };

                if (_mainWindow != null)
                {
                    state.WindowTitle = _mainWindow.Title ?? "Menace Modkit";
                    state.Elements = ExtractUIElements(_mainWindow);
                }

                return state;
            });
        }
        catch
        {
            return null;
        }
    }

    private List<UIElement> ExtractUIElements(Control root, int maxDepth = 15)
    {
        var elements = new List<UIElement>();
        ExtractElementsRecursive(root, elements, 0, maxDepth);
        return elements;
    }

    private void ExtractElementsRecursive(Control control, List<UIElement> elements, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;

        // Skip invisible elements
        if (!control.IsVisible) return;

        // Extract text content from various control types
        var element = ExtractElement(control, depth);
        if (element != null)
        {
            elements.Add(element);
        }

        // Recurse into children
        if (control is Panel panel)
        {
            foreach (var child in panel.Children.OfType<Control>())
            {
                ExtractElementsRecursive(child, elements, depth + 1, maxDepth);
            }
        }
        else if (control is ContentControl contentControl && contentControl.Content is Control childControl)
        {
            ExtractElementsRecursive(childControl, elements, depth + 1, maxDepth);
        }
        else if (control is Decorator decorator && decorator.Child is Control decoratorChild)
        {
            ExtractElementsRecursive(decoratorChild, elements, depth + 1, maxDepth);
        }
        else if (control is ItemsControl itemsControl)
        {
            // For ListBox, DataGrid, etc. - just note the item count
            var itemCount = itemsControl.ItemCount;
            if (itemCount > 0 && element == null)
            {
                elements.Add(new UIElement
                {
                    Type = control.GetType().Name,
                    Text = $"[{itemCount} items]",
                    Depth = depth
                });
            }
        }
    }

    private UIElement? ExtractElement(Control control, int depth)
    {
        string? text = null;
        string? hint = null;
        string type = control.GetType().Name;

        switch (control)
        {
            case TextBlock tb when !string.IsNullOrWhiteSpace(tb.Text):
                text = tb.Text;
                // Detect headers by font size/weight
                if (tb.FontSize >= 16 || tb.FontWeight >= Avalonia.Media.FontWeight.SemiBold)
                {
                    type = "Header";
                }
                break;

            case Label label when label.Content is string labelText && !string.IsNullOrWhiteSpace(labelText):
                text = labelText;
                break;

            // CheckBox and RadioButton must come before Button (they inherit from it)
            case CheckBox checkBox:
                text = checkBox.Content?.ToString();
                hint = checkBox.IsChecked == true ? "checked" : "unchecked";
                if (string.IsNullOrWhiteSpace(text)) return null;
                break;

            case RadioButton radioButton:
                text = radioButton.Content?.ToString();
                hint = radioButton.IsChecked == true ? "selected" : "unselected";
                if (string.IsNullOrWhiteSpace(text)) return null;
                break;

            case Button btn:
                text = btn.Content?.ToString();
                if (string.IsNullOrWhiteSpace(text)) return null;
                break;

            case TextBox textBox:
                text = !string.IsNullOrWhiteSpace(textBox.Text) ? textBox.Text : null;
                hint = textBox.Watermark;
                if (text == null && hint == null) return null;
                break;

            case ComboBox comboBox:
                text = comboBox.SelectedItem?.ToString();
                hint = $"{comboBox.ItemCount} options";
                break;

            default:
                return null;
        }

        if (text == null && hint == null) return null;

        return new UIElement
        {
            Type = type,
            Text = text,
            Hint = hint,
            Depth = depth
        };
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _updateTimer?.Dispose();

        // Clean up state file on exit
        try
        {
            if (File.Exists(StateFilePath))
                File.Delete(StateFilePath);
        }
        catch { }
    }

    /// <summary>
    /// Path to the shared UI state file. Used by MCP server to read state.
    /// </summary>
    public static string GetStateFilePath() => StateFilePath;

    private class UIState
    {
        public string CurrentSection { get; set; } = "";
        public string? CurrentView { get; set; }
        public string? WindowTitle { get; set; }
        public string? Timestamp { get; set; }
        public List<UIElement> Elements { get; set; } = new();
    }

    private class UIElement
    {
        public string Type { get; set; } = "";
        public string? Text { get; set; }
        public string? Hint { get; set; }
        public int Depth { get; set; }
    }
}
