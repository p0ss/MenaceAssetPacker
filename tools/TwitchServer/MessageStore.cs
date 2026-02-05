using System.Collections.Concurrent;

namespace Menace.TwitchServer;

public record TwitchMessage(string Username, string DisplayName, string Text, DateTime Timestamp);

public class MessageStore
{
    private const int MaxMessagesPerUser = 20;
    private readonly ConcurrentDictionary<string, List<TwitchMessage>> _messages = new();
    private readonly object _lock = new();

    public void Add(string username, TwitchMessage message)
    {
        var key = username.ToLowerInvariant();
        lock (_lock)
        {
            if (!_messages.TryGetValue(key, out var list))
            {
                list = new List<TwitchMessage>();
                _messages[key] = list;
            }
            list.Add(message);
            if (list.Count > MaxMessagesPerUser)
                list.RemoveAt(0);
        }
    }

    public IReadOnlyList<TwitchMessage> GetMessages(string username, int limit = 5)
    {
        var key = username.ToLowerInvariant();
        lock (_lock)
        {
            if (!_messages.TryGetValue(key, out var list))
                return Array.Empty<TwitchMessage>();
            return list.TakeLast(limit).ToList();
        }
    }
}
