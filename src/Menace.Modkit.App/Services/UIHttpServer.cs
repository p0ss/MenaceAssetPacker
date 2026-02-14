using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Menace.Modkit.App.ViewModels;

namespace Menace.Modkit.App.Services;

/// <summary>
/// HTTP server that exposes UI state and allows interactions for testing/automation.
/// Similar to the game's GameMcpServer but for the desktop Modkit app.
/// Default port: 21421
/// </summary>
public sealed class UIHttpServer : IDisposable
{
    private static readonly Lazy<UIHttpServer> _instance = new(() => new UIHttpServer());
    public static UIHttpServer Instance => _instance.Value;

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private MainViewModel? _mainViewModel;
    private readonly int _port;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private UIHttpServer(int port = 21421)
    {
        _port = port;
    }

    /// <summary>
    /// Start the HTTP server with a reference to the main view model.
    /// </summary>
    public void Start(MainViewModel mainViewModel)
    {
        if (_listener != null) return;

        _mainViewModel = mainViewModel;
        _cts = new CancellationTokenSource();

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Start();

            Task.Run(() => ListenLoop(_cts.Token));
            ModkitLog.Info($"[UIHttpServer] Started on http://127.0.0.1:{_port}/");
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[UIHttpServer] Failed to start: {ex.Message}");
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
            catch (Exception ex)
            {
                ModkitLog.Info($"[UIHttpServer] Listen error: {ex.Message}");
            }
        }
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        var path = request.Url?.AbsolutePath ?? "/";

        try
        {
            object? result = path switch
            {
                "/" => GetHealthStatus(),
                "/ui" or "/ui/state" => GetUIState(),
                "/ui/navigate" when request.HttpMethod == "POST" => await HandleNavigate(request),
                "/ui/select" when request.HttpMethod == "POST" => await HandleSelect(request),
                "/ui/set-field" when request.HttpMethod == "POST" => await HandleSetField(request),
                "/ui/click" when request.HttpMethod == "POST" => await HandleClick(request),
                "/ui/actions" => GetAvailableActions(),
                _ => new { error = "Not found", path }
            };

            var json = JsonSerializer.Serialize(result, JsonOptions);
            var buffer = Encoding.UTF8.GetBytes(json);

            response.StatusCode = 200;
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer);
        }
        catch (Exception ex)
        {
            var errorJson = JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
            var buffer = Encoding.UTF8.GetBytes(errorJson);
            response.StatusCode = 500;
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer);
        }
        finally
        {
            response.Close();
        }
    }

    private object GetHealthStatus()
    {
        return new
        {
            running = true,
            app = "Menace Modkit",
            version = "v22",
            port = _port,
            time = DateTime.Now.ToString("o")
        };
    }

    private object GetUIState()
    {
        return Dispatcher.UIThread.Invoke(() =>
        {
            if (_mainViewModel == null)
                return (object)new { error = "MainViewModel not available" };

            var state = new Dictionary<string, object?>
            {
                ["section"] = _mainViewModel.CurrentSection.ToString(),
                ["subSection"] = _mainViewModel.CurrentSubSection,
                ["view"] = _mainViewModel.SelectedViewModel?.GetType().Name ?? "Unknown",
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            };

            // Add view-specific details
            var viewData = GetViewSpecificData(_mainViewModel.SelectedViewModel);
            foreach (var kvp in viewData)
                state[kvp.Key] = kvp.Value;

            return state;
        });
    }

    private Dictionary<string, object?> GetViewSpecificData(ViewModelBase? viewModel)
    {
        var data = new Dictionary<string, object?>();

        switch (viewModel)
        {
            case StatsEditorViewModel stats:
                data["templateTypes"] = stats.TreeNodes
                    .Where(n => n.IsCategory)
                    .Select(n => n.Name)
                    .ToList();
                data["selectedModpack"] = stats.CurrentModpackName;
                data["availableModpacks"] = stats.AvailableModpacks.ToList();
                data["hasModifications"] = stats.HasModifications;
                data["saveStatus"] = stats.SaveStatus;
                data["searchText"] = stats.SearchText;
                data["isSearching"] = stats.IsSearching;

                if (stats.SelectedNode != null)
                {
                    data["selectedNode"] = new Dictionary<string, object?>
                    {
                        ["name"] = stats.SelectedNode.Name,
                        ["isCategory"] = stats.SelectedNode.IsCategory,
                        ["templateType"] = stats.SelectedNode.Template?.GetType().Name
                    };

                    // Include visible properties (limit to avoid huge responses)
                    if (stats.VanillaProperties != null)
                    {
                        data["vanillaProperties"] = stats.VanillaProperties
                            .Take(50)
                            .ToDictionary(kvp => kvp.Key, kvp => FormatValue(kvp.Value));
                    }

                    if (stats.ModifiedProperties != null)
                    {
                        data["modifiedProperties"] = stats.ModifiedProperties
                            .Take(50)
                            .ToDictionary(kvp => kvp.Key, kvp => FormatValue(kvp.Value));
                    }
                }

                // Include search results if searching
                if (stats.IsSearching && stats.SearchResults != null)
                {
                    data["searchResultCount"] = stats.SearchResults.Count;
                }
                break;

            case ModpacksViewModel modpacks:
                data["selectedModpack"] = modpacks.SelectedModpack?.Name;
                data["modpackCount"] = modpacks.AllModpacks?.Count ?? 0;
                if (modpacks.AllModpacks != null)
                {
                    data["modpacks"] = modpacks.AllModpacks
                        .Take(20)
                        .Select(m => new Dictionary<string, object?>
                        {
                            ["name"] = m.Name,
                            ["version"] = m.Version,
                            ["author"] = m.Author,
                            ["loadOrder"] = m.LoadOrder
                        })
                        .ToList();
                }
                break;

            case AssetBrowserViewModel assets:
                data["currentModpack"] = assets.CurrentModpackName;
                data["searchText"] = assets.SearchText;
                data["isSearching"] = assets.IsSearching;
                break;

            case CodeEditorViewModel code:
                data["currentFile"] = code.CurrentFilePath;
                break;

            case DocsViewModel docs:
                data["searchText"] = docs.SearchText;
                break;

            case SaveEditorViewModel saves:
                data["saveCount"] = saves.SaveFiles?.Count ?? 0;
                data["selectedSave"] = saves.SelectedSave?.FileName;
                if (saves.SaveFiles != null)
                {
                    data["saves"] = saves.SaveFiles
                        .Take(20)
                        .Select(s => s.FileName)
                        .ToList();
                }
                break;
        }

        return data;
    }

    private static object? FormatValue(object? value)
    {
        if (value == null) return null;
        if (value is string or int or long or float or double or bool or decimal)
            return value;
        if (value is JsonElement je)
            return je.ValueKind == JsonValueKind.String ? je.GetString() : je.ToString();
        return value.ToString();
    }

    private async Task<object> HandleNavigate(HttpListenerRequest request)
    {
        var body = await ReadBody(request);
        var section = body.GetValueOrDefault("section")?.ToString();
        var subSection = body.GetValueOrDefault("subSection")?.ToString();

        if (string.IsNullOrEmpty(section))
            return new { error = "Missing 'section' parameter" };

        return Dispatcher.UIThread.Invoke<object>(() =>
        {
            if (_mainViewModel == null)
                return new { error = "MainViewModel not available" };

            try
            {
                switch (section.ToLowerInvariant())
                {
                    case "home":
                        _mainViewModel.NavigateToHome();
                        break;
                    case "modloader":
                        _mainViewModel.NavigateToModLoader();
                        if (!string.IsNullOrEmpty(subSection))
                        {
                            switch (subSection.ToLowerInvariant())
                            {
                                case "loadorder": _mainViewModel.NavigateToLoadOrder(); break;
                                case "saves": _mainViewModel.NavigateToSaves(); break;
                                case "settings": _mainViewModel.NavigateToLoaderSettings(); break;
                            }
                        }
                        break;
                    case "moddingtools":
                        _mainViewModel.NavigateToModdingTools();
                        if (!string.IsNullOrEmpty(subSection))
                        {
                            switch (subSection.ToLowerInvariant())
                            {
                                case "data": _mainViewModel.NavigateToData(); break;
                                case "assets": _mainViewModel.NavigateToAssets(); break;
                                case "code": _mainViewModel.NavigateToCode(); break;
                                case "docs": _mainViewModel.NavigateToDocs(); break;
                                case "settings": _mainViewModel.NavigateToToolSettings(); break;
                            }
                        }
                        break;
                    default:
                        return new { error = $"Unknown section: {section}" };
                }

                return new { success = true, section = _mainViewModel.CurrentSection.ToString(), subSection = _mainViewModel.CurrentSubSection };
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        });
    }

    private async Task<object> HandleSelect(HttpListenerRequest request)
    {
        var body = await ReadBody(request);
        var target = body.GetValueOrDefault("target")?.ToString();
        var value = body.GetValueOrDefault("value")?.ToString();

        if (string.IsNullOrEmpty(target))
            return new { error = "Missing 'target' parameter" };

        return Dispatcher.UIThread.Invoke<object>(() =>
        {
            if (_mainViewModel == null)
                return new { error = "MainViewModel not available" };

            try
            {
                switch (target.ToLowerInvariant())
                {
                    case "modpack":
                        if (_mainViewModel.SelectedViewModel is StatsEditorViewModel stats)
                        {
                            if (value != null && stats.AvailableModpacks.Contains(value))
                            {
                                stats.CurrentModpackName = value;
                                return new { success = true, selected = value };
                            }
                            return new { error = $"Modpack not found: {value}" };
                        }
                        return new { error = "Not on Stats Editor view" };

                    case "template":
                        if (_mainViewModel.SelectedViewModel is StatsEditorViewModel statsVm)
                        {
                            var node = FindTemplateNode(statsVm.TreeNodes, value);
                            if (node != null)
                            {
                                statsVm.SelectedNode = node;
                                return new { success = true, selected = node.Name };
                            }
                            return new { error = $"Template not found: {value}" };
                        }
                        return new { error = "Not on Stats Editor view" };

                    case "templatetype":
                        if (_mainViewModel.SelectedViewModel is StatsEditorViewModel statsEditor)
                        {
                            var category = statsEditor.TreeNodes.FirstOrDefault(n =>
                                n.IsCategory && n.Name.Equals(value, StringComparison.OrdinalIgnoreCase));
                            if (category != null)
                            {
                                category.IsExpanded = true;
                                statsEditor.SelectedNode = category;
                                return new { success = true, selected = category.Name, templateCount = category.Children.Count };
                            }
                            return new { error = $"Template type not found: {value}" };
                        }
                        return new { error = "Not on Stats Editor view" };

                    default:
                        return new { error = $"Unknown target: {target}" };
                }
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        });
    }

    private TreeNodeViewModel? FindTemplateNode(IEnumerable<TreeNodeViewModel> nodes, string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        foreach (var node in nodes)
        {
            if (!node.IsCategory && node.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return node;

            if (node.Children.Count > 0)
            {
                var found = FindTemplateNode(node.Children, name);
                if (found != null) return found;
            }
        }
        return null;
    }

    private async Task<object> HandleSetField(HttpListenerRequest request)
    {
        var body = await ReadBody(request);
        var field = body.GetValueOrDefault("field")?.ToString();
        var value = body.GetValueOrDefault("value")?.ToString();

        if (string.IsNullOrEmpty(field))
            return new { error = "Missing 'field' parameter" };

        return Dispatcher.UIThread.Invoke<object>(() =>
        {
            if (_mainViewModel?.SelectedViewModel is not StatsEditorViewModel stats)
                return new { error = "Not on Stats Editor view" };

            if (stats.SelectedNode?.Template == null)
                return new { error = "No template selected" };

            try
            {
                stats.UpdateModifiedProperty(field, value ?? "");
                return new { success = true, field, value };
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        });
    }

    private async Task<object> HandleClick(HttpListenerRequest request)
    {
        var body = await ReadBody(request);
        var button = body.GetValueOrDefault("button")?.ToString();

        if (string.IsNullOrEmpty(button))
            return new { error = "Missing 'button' parameter" };

        return Dispatcher.UIThread.Invoke<object>(() =>
        {
            if (_mainViewModel == null)
                return new { error = "MainViewModel not available" };

            try
            {
                switch (button.ToLowerInvariant())
                {
                    // Navigation buttons
                    case "home": _mainViewModel.NavigateToHome(); break;
                    case "modloader": _mainViewModel.NavigateToModLoader(); break;
                    case "moddingtools": _mainViewModel.NavigateToModdingTools(); break;
                    case "loadorder": _mainViewModel.NavigateToLoadOrder(); break;
                    case "saves": _mainViewModel.NavigateToSaves(); break;
                    case "data": _mainViewModel.NavigateToData(); break;
                    case "assets": _mainViewModel.NavigateToAssets(); break;
                    case "code": _mainViewModel.NavigateToCode(); break;
                    case "docs": _mainViewModel.NavigateToDocs(); break;
                    case "settings": _mainViewModel.NavigateToToolSettings(); break;

                    default:
                        return new { error = $"Unknown button: {button}" };
                }

                return new { success = true, clicked = button };
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        });
    }

    private object GetAvailableActions()
    {
        return Dispatcher.UIThread.Invoke<object>(() =>
        {
            return new
            {
                navigation = new[] { "home", "modloader", "moddingtools" },
                subSections = new Dictionary<string, string[]>
                {
                    ["modloader"] = new[] { "loadorder", "saves", "settings" },
                    ["moddingtools"] = new[] { "data", "assets", "code", "docs", "settings" }
                }
            };
        });
    }

    private static async Task<Dictionary<string, object?>> ReadBody(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        var body = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(body))
            return new Dictionary<string, object?>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(body) ?? new();
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener?.Close();
        ModkitLog.Info("[UIHttpServer] Stopped");
    }
}
