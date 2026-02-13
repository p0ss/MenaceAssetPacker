using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Exposes UI state to external tools (like MCP server) via a local HTTP endpoint.
/// This allows AI assistants to "see" what the user sees in the modkit.
/// </summary>
public sealed class UIStateService : IDisposable
{
    private static readonly Lazy<UIStateService> _instance = new(() => new UIStateService());
    public static UIStateService Instance => _instance.Value;

    private const int Port = 19847;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Window? _mainWindow;
    private Func<string>? _currentSectionGetter;
    private Func<string>? _currentViewGetter;

    private UIStateService() { }

    /// <summary>
    /// Start the HTTP server that exposes UI state.
    /// Call this from App.OnFrameworkInitializationCompleted after the main window is created.
    /// </summary>
    public void Start(Window mainWindow, Func<string> currentSectionGetter, Func<string>? currentViewGetter = null)
    {
        _mainWindow = mainWindow;
        _currentSectionGetter = currentSectionGetter;
        _currentViewGetter = currentViewGetter;

        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{Port}/");

        try
        {
            _listener.Start();
            Task.Run(() => ListenLoop(_cts.Token));
            ModkitLog.Info($"[UIStateService] Started on port {Port}");
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[UIStateService] Failed to start HTTP listener: {ex.Message}");
        }
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener?.IsListening == true)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context), ct);
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                ModkitLog.Warn($"[UIStateService] Error handling request: {ex.Message}");
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";

            object? responseData = path switch
            {
                "/" or "/state" => GetUIState(),
                "/health" => new { status = "ok" },
                _ => null
            };

            if (responseData == null)
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            var json = JsonSerializer.Serialize(responseData, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var buffer = Encoding.UTF8.GetBytes(json);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[UIStateService] Error sending response: {ex.Message}");
            try { context.Response.Close(); } catch { }
        }
    }

    private object GetUIState()
    {
        // Must run on UI thread to access visual tree
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
        _listener?.Stop();
        _listener?.Close();
    }

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
