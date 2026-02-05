using System.Collections.Concurrent;

namespace Menace.TwitchServer;

public record ViewerInfo(string Username, string DisplayName, DateTime JoinedAt);

public class DraftPool
{
    private readonly ConcurrentDictionary<string, ViewerInfo> _viewers = new();

    public void AddViewer(string username, string displayName)
    {
        var key = username.ToLowerInvariant();
        _viewers[key] = new ViewerInfo(key, displayName, DateTime.UtcNow);
    }

    public ViewerInfo? PickRandom()
    {
        var keys = _viewers.Keys.ToArray();
        if (keys.Length == 0) return null;

        var key = keys[Random.Shared.Next(keys.Length)];
        _viewers.TryRemove(key, out var viewer);
        return viewer;
    }

    public IReadOnlyList<ViewerInfo> GetAll() => _viewers.Values.ToList();

    public int Count => _viewers.Count;

    public void Clear() => _viewers.Clear();
}
