using System.Net.Sockets;
using System.Text;

namespace Menace.TwitchServer;

public class TwitchIrcClient : IAsyncDisposable
{
    private TcpClient? _tcp;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;
    private Task? _readLoop;

    public bool IsConnected => _tcp?.Connected == true;
    public string? Channel { get; private set; }

    public event Action<string, string, string, DateTime>? MessageReceived;

    public async Task ConnectAsync(string channel, string oauthToken)
    {
        await DisconnectAsync();

        _tcp = new TcpClient();
        await _tcp.ConnectAsync("irc.chat.twitch.tv", 6667);

        var stream = _tcp.GetStream();
        _reader = new StreamReader(stream, Encoding.UTF8);
        _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        await _writer.WriteLineAsync("CAP REQ :twitch.tv/tags");
        await _writer.WriteLineAsync($"PASS {oauthToken}");
        await _writer.WriteLineAsync($"NICK justinfan{Random.Shared.Next(10000, 99999)}");
        await _writer.WriteLineAsync($"JOIN #{channel.ToLowerInvariant()}");

        Channel = channel.ToLowerInvariant();
        _cts = new CancellationTokenSource();

        _readLoop = Task.Run(() => ReadLoopAsync(_cts.Token));

        Console.WriteLine($"[IRC] Connected to #{Channel}");
    }

    public async Task DisconnectAsync()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            if (_readLoop != null)
            {
                try { await _readLoop; } catch { }
            }
            _cts.Dispose();
            _cts = null;
        }

        _reader?.Dispose();
        _writer?.Dispose();
        _tcp?.Dispose();
        _reader = null;
        _writer = null;
        _tcp = null;
        Channel = null;

        Console.WriteLine("[IRC] Disconnected");
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _reader != null)
            {
                var line = await _reader.ReadLineAsync(ct);
                if (line == null) break;

                if (line.StartsWith("PING"))
                {
                    if (_writer != null)
                        await _writer.WriteLineAsync("PONG" + line[4..]);
                    continue;
                }

                if (line.Contains("PRIVMSG"))
                    ParsePrivMsg(line);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"[IRC] Read loop error: {ex.Message}");
        }
    }

    private void ParsePrivMsg(string raw)
    {
        // Format: @tags :user!user@user.tmi.twitch.tv PRIVMSG #channel :message
        var username = "";
        var displayName = "";
        var text = "";

        // Parse tags for display-name
        if (raw.StartsWith('@'))
        {
            var tagsEnd = raw.IndexOf(' ');
            if (tagsEnd > 0)
            {
                var tags = raw[1..tagsEnd];
                foreach (var tag in tags.Split(';'))
                {
                    var eq = tag.IndexOf('=');
                    if (eq < 0) continue;
                    var key = tag[..eq];
                    var val = tag[(eq + 1)..];

                    if (key == "display-name" && !string.IsNullOrEmpty(val))
                        displayName = val;
                }
                raw = raw[(tagsEnd + 1)..];
            }
        }

        // Parse :user!user@user.tmi.twitch.tv PRIVMSG #channel :message
        if (raw.StartsWith(':'))
        {
            var bangIdx = raw.IndexOf('!');
            if (bangIdx > 0)
                username = raw[1..bangIdx];
        }

        var msgIdx = raw.IndexOf(" PRIVMSG ");
        if (msgIdx < 0) return;

        var afterPrivmsg = raw[(msgIdx + 9)..];
        var colonIdx = afterPrivmsg.IndexOf(':');
        if (colonIdx >= 0)
            text = afterPrivmsg[(colonIdx + 1)..];

        if (string.IsNullOrEmpty(displayName))
            displayName = username;

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(text))
            MessageReceived?.Invoke(username, displayName, text, DateTime.UtcNow);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
