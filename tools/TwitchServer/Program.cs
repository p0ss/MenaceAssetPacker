using Menace.TwitchServer;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:7654");

var app = builder.Build();

var irc = new TwitchIrcClient();
var draftPool = new DraftPool();
var messageStore = new MessageStore();

// Wire IRC messages into draft pool and message store
irc.MessageReceived += (username, displayName, text, timestamp) =>
{
    var msg = new TwitchMessage(username, displayName, text, timestamp);
    messageStore.Add(username, msg);

    if (text.Trim().Equals("!draft", StringComparison.OrdinalIgnoreCase))
    {
        draftPool.AddViewer(username, displayName);
        Console.WriteLine($"[Draft] {displayName} joined the draft pool ({draftPool.Count} total)");
    }
};

// --- Config file: auto-connect on startup ---

var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
if (File.Exists(configPath))
{
    Console.WriteLine($"[Config] Loading {configPath}");
    var configText = File.ReadAllText(configPath);
    var channel = ExtractJsonString(configText, "channel");
    var oauthToken = ExtractJsonString(configText, "oauthToken");

    if (!string.IsNullOrEmpty(channel) && !string.IsNullOrEmpty(oauthToken))
    {
        try
        {
            await irc.ConnectAsync(channel, oauthToken);
            Console.WriteLine($"[Config] Auto-connected to #{channel}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Config] Auto-connect failed: {ex.Message}");
        }
    }
    else
    {
        Console.WriteLine("[Config] config.json found but missing channel or oauthToken — skipping auto-connect");
    }
}
else
{
    var template = "{\n  \"channel\": \"your_channel_name\",\n  \"oauthToken\": \"oauth:your_token_here\"\n}\n";
    File.WriteAllText(configPath, template);
    Console.WriteLine($"[Config] Created template config at: {configPath}");
    Console.WriteLine("[Config] Edit it with your Twitch channel and OAuth token, then restart.");
    Console.WriteLine("[Config] Get a token at: https://twitchapps.com/tmi/");
}

// --- HTTP Endpoints ---

app.MapGet("/api/status", () => new
{
    connected = irc.IsConnected,
    channel = irc.Channel ?? "",
    draftPoolSize = draftPool.Count
});

app.MapGet("/api/draft", () => draftPool.GetAll().Select(v => new
{
    username = v.Username,
    displayName = v.DisplayName,
    joinedAt = v.JoinedAt
}));

app.MapPost("/api/draft/pick", () =>
{
    var viewer = draftPool.PickRandom();
    if (viewer == null)
        return Results.Json(new { error = "Draft pool is empty" }, statusCode: 404);

    var messages = messageStore.GetMessages(viewer.Username, 3);
    return Results.Json(new
    {
        username = viewer.Username,
        displayName = viewer.DisplayName,
        latestMessages = messages.Select(m => new { text = m.Text, timestamp = m.Timestamp })
    });
});

app.MapGet("/api/messages/{username}", (string username) =>
{
    var messages = messageStore.GetMessages(username, 10);
    return messages.Select(m => new
    {
        text = m.Text,
        displayName = m.DisplayName,
        timestamp = m.Timestamp
    });
});

app.MapPost("/api/connect", async (HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();

    // Manual JSON parsing for { "channel": "...", "oauthToken": "..." }
    var channel = ExtractJsonString(body, "channel");
    var oauthToken = ExtractJsonString(body, "oauthToken");

    if (string.IsNullOrEmpty(channel))
        return Results.BadRequest(new { error = "channel is required" });
    if (string.IsNullOrEmpty(oauthToken))
        return Results.BadRequest(new { error = "oauthToken is required" });

    try
    {
        await irc.ConnectAsync(channel, oauthToken);
        return Results.Ok(new { connected = true, channel = irc.Channel });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/disconnect", async () =>
{
    await irc.DisconnectAsync();
    return new { connected = false };
});

Console.WriteLine("TwitchServer starting on http://localhost:7654");
Console.WriteLine("Endpoints:");
Console.WriteLine("  GET  /api/status");
Console.WriteLine("  GET  /api/draft");
Console.WriteLine("  POST /api/draft/pick");
Console.WriteLine("  GET  /api/messages/{username}");
Console.WriteLine("  POST /api/connect    { \"channel\": \"...\", \"oauthToken\": \"oauth:...\" }");
Console.WriteLine("  POST /api/disconnect");

app.Run();

static string? ExtractJsonString(string json, string key)
{
    var pattern = $"\"{key}\"";
    var idx = json.IndexOf(pattern, StringComparison.Ordinal);
    if (idx < 0) return null;

    idx = json.IndexOf(':', idx + pattern.Length);
    if (idx < 0) return null;

    idx = json.IndexOf('"', idx + 1);
    if (idx < 0) return null;

    var endIdx = json.IndexOf('"', idx + 1);
    if (endIdx < 0) return null;

    return json[(idx + 1)..endIdx];
}
