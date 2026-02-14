using System.ComponentModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;

using ModelContextProtocol.Server;

namespace Menace.Modkit.Mcp.Tools;

/// <summary>
/// Tools for inspecting and interacting with the Modkit UI.
/// Communicates with the desktop app's HTTP server on port 21421.
/// </summary>
[McpServerToolType]
public static class UITools
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private const string BaseUrl = "http://127.0.0.1:21421";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// Get the current UI state of the Modkit application.
    /// Returns the current section, view, and detailed view-specific data.
    /// </summary>
    [McpServerTool(Name = "modkit_ui", ReadOnly = true), Description("Get the current UI state of the Modkit application. Shows section, view, and detailed content like selected templates, field values, and available actions.")]
    public static async Task<object> GetUIState()
    {
        try
        {
            var response = await Http.GetAsync($"{BaseUrl}/ui/state");
            if (!response.IsSuccessStatusCode)
            {
                return new { modkitRunning = false, error = $"HTTP {(int)response.StatusCode}" };
            }

            var json = await response.Content.ReadAsStringAsync();
            var state = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
            return new { modkitRunning = true, state };
        }
        catch (HttpRequestException)
        {
            return new { modkitRunning = false, error = "Modkit app is not running. Please start the Menace Modkit application." };
        }
        catch (TaskCanceledException)
        {
            return new { modkitRunning = false, error = "Connection timed out. The app may be frozen or not responding." };
        }
        catch (Exception ex)
        {
            return new { modkitRunning = false, error = ex.Message };
        }
    }

    /// <summary>
    /// Navigate to a section in the Modkit app.
    /// </summary>
    [McpServerTool(Name = "modkit_navigate"), Description("Navigate to a section in the Modkit app. Sections: 'Home', 'ModLoader', 'ModdingTools'. SubSections for ModLoader: 'LoadOrder', 'Saves', 'Settings'. SubSections for ModdingTools: 'Data', 'Assets', 'Code', 'Docs', 'Settings'.")]
    public static async Task<object> Navigate(string section, string? subSection = null)
    {
        try
        {
            var body = new { section, subSection };
            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await Http.PostAsync($"{BaseUrl}/ui/navigate", content);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
        }
        catch (HttpRequestException)
        {
            return new { error = "Modkit app is not running" };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    /// <summary>
    /// Select an item in the current view.
    /// </summary>
    [McpServerTool(Name = "modkit_select"), Description("Select an item in the current view. Targets: 'modpack' (select active modpack), 'templateType' (expand a template category like 'WeaponTemplate'), 'template' (select a specific template by name).")]
    public static async Task<object> Select(string target, string value)
    {
        try
        {
            var body = new { target, value };
            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await Http.PostAsync($"{BaseUrl}/ui/select", content);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
        }
        catch (HttpRequestException)
        {
            return new { error = "Modkit app is not running" };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    /// <summary>
    /// Set a field value on the currently selected template.
    /// </summary>
    [McpServerTool(Name = "modkit_set_field"), Description("Set a field value on the currently selected template in the Stats Editor. Requires being on the Data view with a template selected.")]
    public static async Task<object> SetField(string field, object? value)
    {
        try
        {
            var body = new { field, value };
            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await Http.PostAsync($"{BaseUrl}/ui/set-field", content);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
        }
        catch (HttpRequestException)
        {
            return new { error = "Modkit app is not running" };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    /// <summary>
    /// Click a button in the Modkit app.
    /// </summary>
    [McpServerTool(Name = "modkit_click"), Description("Click a button or trigger an action. Navigation: 'home', 'modloader', 'moddingtools', 'loadorder', 'saves', 'data', 'assets', 'code', 'docs', 'settings'. Actions: 'save' (save changes), 'revert' (discard changes).")]
    public static async Task<object> Click(string button)
    {
        try
        {
            var body = new { button };
            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await Http.PostAsync($"{BaseUrl}/ui/click", content);

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
        }
        catch (HttpRequestException)
        {
            return new { error = "Modkit app is not running" };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }

    /// <summary>
    /// Get available actions for the current view.
    /// </summary>
    [McpServerTool(Name = "modkit_actions", ReadOnly = true), Description("Get the list of available navigation and actions for the current view.")]
    public static async Task<object> GetActions()
    {
        try
        {
            var response = await Http.GetAsync($"{BaseUrl}/ui/actions");
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
        }
        catch (HttpRequestException)
        {
            return new { error = "Modkit app is not running" };
        }
        catch (Exception ex)
        {
            return new { error = ex.Message };
        }
    }
}
