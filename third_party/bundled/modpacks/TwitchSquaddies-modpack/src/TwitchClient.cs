#nullable disable
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;
using MelonLoader;

namespace Menace.TwitchSquaddies;

/// <summary>
/// HTTP client that polls the TwitchServer for draft pool status and picks viewers.
/// All HTTP calls run on thread pool; results are stored in volatile fields or queued
/// for main-thread consumption.
/// </summary>
public class TwitchClient
{
    private readonly MelonLogger.Instance _log;
    private readonly HttpClient _http;

    // Cached status (updated by background poll)
    private volatile bool _serverReachable;
    private volatile bool _connected;
    private volatile string _channel = "";
    private volatile int _draftPoolSize;
    private volatile bool _polling;

    public bool IsServerReachable => _serverReachable;
    public bool IsConnected => _connected;
    public string Channel => _channel;
    public int DraftPoolSize => _draftPoolSize;
    public string ServerUrl { get; set; } = "http://localhost:7654";

    public TwitchClient(MelonLogger.Instance log)
    {
        _log = log;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    }

    /// <summary>
    /// Poll server status. Call from OnUpdate at a throttled interval.
    /// Runs HTTP call on thread pool, updates volatile fields.
    /// </summary>
    public void PollStatus()
    {
        if (_polling) return;
        _polling = true;

        Task.Run(async () =>
        {
            try
            {
                var resp = await _http.GetStringAsync($"{ServerUrl}/api/status");
                _serverReachable = true;

                // Parse: {"connected":true,"channel":"foo","draftPoolSize":3}
                _connected = ExtractBool(resp, "connected");
                _channel = ExtractString(resp, "channel") ?? "";
                _draftPoolSize = ExtractInt(resp, "draftPoolSize");
            }
            catch
            {
                _serverReachable = false;
                _connected = false;
                _channel = "";
                _draftPoolSize = 0;
            }
            finally
            {
                _polling = false;
            }
        });
    }

    /// <summary>
    /// Pick a random viewer from the draft pool. Callback receives (username, displayName, latestMessage).
    /// Callback is invoked on thread pool — caller should queue results for main thread.
    /// </summary>
    public void PickViewer(Action<string, string, string> callback)
    {
        Task.Run(async () =>
        {
            try
            {
                var resp = await _http.PostAsync($"{ServerUrl}/api/draft/pick", null);
                var body = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    _log.Warning($"Pick failed: {body}");
                    return;
                }

                var username = ExtractString(body, "username") ?? "";
                var displayName = ExtractString(body, "displayName") ?? username;

                // Extract first message text from latestMessages array
                var latestMsg = ExtractFirstMessageText(body);

                callback(username, displayName, latestMsg);
            }
            catch (Exception ex)
            {
                _log.Warning($"PickViewer error: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Fetch the latest message for a specific user. Callback receives the message text.
    /// </summary>
    public void FetchLatestMessage(string username, Action<string> callback)
    {
        Task.Run(async () =>
        {
            try
            {
                var resp = await _http.GetStringAsync($"{ServerUrl}/api/messages/{username}");
                // Response is an array: [{"text":"...","displayName":"...","timestamp":"..."},...]
                // Extract the last "text" value
                var lastText = ExtractLastString(resp, "text") ?? "";
                callback(lastText);
            }
            catch (Exception ex)
            {
                _log.Warning($"FetchLatestMessage error: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Get the draft pool list (blocking-ish, for command output). Returns formatted string.
    /// </summary>
    public void FetchDraftList(Action<string> callback)
    {
        Task.Run(async () =>
        {
            try
            {
                var resp = await _http.GetStringAsync($"{ServerUrl}/api/draft");
                // Parse array of {username, displayName, joinedAt}
                var result = FormatDraftList(resp);
                callback(result);
            }
            catch (Exception ex)
            {
                callback($"Error: {ex.Message}");
            }
        });
    }

    // --- Manual JSON parsing helpers (no System.Text.Json dependency) ---

    private static string ExtractString(string json, string key)
    {
        var pattern = $"\"{key}\":\"";
        var idx = json.IndexOf(pattern, StringComparison.Ordinal);
        if (idx < 0) return null;
        idx += pattern.Length;
        var endIdx = json.IndexOf('"', idx);
        if (endIdx < 0) return null;
        return json.Substring(idx, endIdx - idx);
    }

    private static bool ExtractBool(string json, string key)
    {
        var pattern = $"\"{key}\":";
        var idx = json.IndexOf(pattern, StringComparison.Ordinal);
        if (idx < 0) return false;
        idx += pattern.Length;
        return json.Length > idx && json[idx] == 't';
    }

    private static int ExtractInt(string json, string key)
    {
        var pattern = $"\"{key}\":";
        var idx = json.IndexOf(pattern, StringComparison.Ordinal);
        if (idx < 0) return 0;
        idx += pattern.Length;
        var end = idx;
        while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
            end++;
        if (end == idx) return 0;
        int.TryParse(json.Substring(idx, end - idx), out var val);
        return val;
    }

    private static string ExtractFirstMessageText(string json)
    {
        // Look for "latestMessages":[{"text":"..." ...
        var pattern = "\"text\":\"";
        var idx = json.IndexOf(pattern, StringComparison.Ordinal);
        if (idx < 0) return "";
        idx += pattern.Length;
        var endIdx = json.IndexOf('"', idx);
        if (endIdx < 0) return "";
        return json.Substring(idx, endIdx - idx);
    }

    private static string ExtractLastString(string json, string key)
    {
        var pattern = $"\"{key}\":\"";
        var lastIdx = json.LastIndexOf(pattern, StringComparison.Ordinal);
        if (lastIdx < 0) return null;
        lastIdx += pattern.Length;
        var endIdx = json.IndexOf('"', lastIdx);
        if (endIdx < 0) return null;
        return json.Substring(lastIdx, endIdx - lastIdx);
    }

    private static string FormatDraftList(string json)
    {
        // Simple parsing of array of objects
        var lines = new System.Collections.Generic.List<string>();
        var pattern = "\"displayName\":\"";
        int searchFrom = 0;
        while (true)
        {
            var idx = json.IndexOf(pattern, searchFrom, StringComparison.Ordinal);
            if (idx < 0) break;
            idx += pattern.Length;
            var endIdx = json.IndexOf('"', idx);
            if (endIdx < 0) break;
            lines.Add($"  {json.Substring(idx, endIdx - idx)}");
            searchFrom = endIdx + 1;
        }
        if (lines.Count == 0) return "Draft pool is empty";
        return $"{lines.Count} viewers in draft pool:\n{string.Join("\n", lines)}";
    }
}
