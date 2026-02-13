using System.ComponentModel;
using System.Text.Json;

using ModelContextProtocol.Server;

namespace Menace.Modkit.Mcp.Tools;

/// <summary>
/// Tools for inspecting the Modkit UI state.
/// Allows AI assistants to "see" what the user sees.
/// </summary>
[McpServerToolType]
public static class UITools
{
    private static readonly string StateFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".menace-modkit",
        "ui-state.json"
    );

    /// <summary>
    /// Get the current UI state of the Modkit application.
    /// Returns the current section, view, and visible text elements.
    /// Use this to understand what the user is looking at.
    /// </summary>
    [McpServerTool(Name = "modkit_ui", ReadOnly = true), Description("Get the current UI state of the Modkit application. Shows what section the user is in and what text/headings are visible on screen.")]
    public static object GetUIState()
    {
        try
        {
            if (!File.Exists(StateFilePath))
            {
                return new
                {
                    modkitRunning = false,
                    error = "Modkit app is not running. Please start the Menace Modkit application."
                };
            }

            // Check if file is stale (more than 5 seconds old)
            var fileInfo = new FileInfo(StateFilePath);
            var age = DateTime.UtcNow - fileInfo.LastWriteTimeUtc;
            if (age > TimeSpan.FromSeconds(5))
            {
                return new
                {
                    modkitRunning = false,
                    error = $"Modkit app state is stale ({age.TotalSeconds:F0}s old). The app may have closed or frozen."
                };
            }

            var json = File.ReadAllText(StateFilePath);
            var state = JsonSerializer.Deserialize<UIStateResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (state == null)
            {
                return new { error = "Failed to parse UI state" };
            }

            // Format the output for easy reading
            return new
            {
                modkitRunning = true,
                currentSection = state.CurrentSection,
                currentView = state.CurrentView,
                windowTitle = state.WindowTitle,
                visibleContent = FormatElements(state.Elements)
            };
        }
        catch (IOException ex)
        {
            return new
            {
                modkitRunning = false,
                error = $"Failed to read UI state: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new
            {
                modkitRunning = false,
                error = $"Error reading UI state: {ex.Message}"
            };
        }
    }

    private static List<FormattedElement> FormatElements(List<UIElement>? elements)
    {
        if (elements == null || elements.Count == 0)
            return new List<FormattedElement>();

        return elements
            .Where(e => !string.IsNullOrWhiteSpace(e.Text) || !string.IsNullOrWhiteSpace(e.Hint))
            .Select(e => new FormattedElement
            {
                Type = e.Type,
                Text = e.Text,
                State = e.Hint,
                Indent = e.Depth
            })
            .ToList();
    }

    private class UIStateResponse
    {
        public string CurrentSection { get; set; } = "";
        public string? CurrentView { get; set; }
        public string? WindowTitle { get; set; }
        public string? Timestamp { get; set; }
        public List<UIElement>? Elements { get; set; }
    }

    private class UIElement
    {
        public string Type { get; set; } = "";
        public string? Text { get; set; }
        public string? Hint { get; set; }
        public int Depth { get; set; }
    }

    private class FormattedElement
    {
        public string Type { get; set; } = "";
        public string? Text { get; set; }
        public string? State { get; set; }
        public int Indent { get; set; }
    }
}
