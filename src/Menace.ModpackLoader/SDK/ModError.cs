using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MelonLoader;

namespace Menace.SDK;

public enum ErrorSeverity { Info, Warning, Error, Fatal }

public class ModErrorEntry
{
    public string ModId;
    public string Message;
    public string Context;
    public ErrorSeverity Severity;
    public DateTime Timestamp;
    public Exception Exception;
    public int OccurrenceCount = 1;
}

/// <summary>
/// Central error reporting for the Menace SDK. Never throws — routes all failures
/// to MelonLogger and stores them in a queryable ring buffer.
/// </summary>
public static class ModError
{
    public static event Action<ModErrorEntry> OnError;

    // Ring buffer settings
    private const int MaxEntriesPerMod = 200;
    private const int GlobalMaxEntries = 1000;
    private const double DedupeWindowSeconds = 5.0;
    private const int RateLimitPerSecond = 10;

    private static readonly List<ModErrorEntry> _entries = new();
    private static readonly object _lock = new();

    // Rate limiting: per-mod token buckets
    private static readonly Dictionary<string, RateBucket> _rateBuckets = new();

    private class RateBucket
    {
        public double Tokens;
        public DateTime LastRefill;

        public RateBucket()
        {
            Tokens = RateLimitPerSecond;
            LastRefill = DateTime.UtcNow;
        }

        public bool TryConsume()
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - LastRefill).TotalSeconds;
            Tokens = Math.Min(RateLimitPerSecond, Tokens + elapsed * RateLimitPerSecond);
            LastRefill = now;

            if (Tokens >= 1.0)
            {
                Tokens -= 1.0;
                return true;
            }
            return false;
        }
    }

    public static IReadOnlyList<ModErrorEntry> RecentErrors
    {
        get
        {
            lock (_lock)
                return _entries.ToList();
        }
    }

    public static void Report(string modId, string message, Exception ex = null,
        ErrorSeverity severity = ErrorSeverity.Error)
    {
        AddEntry(modId ?? "unknown", null, message, severity, ex);
    }

    public static void Warn(string modId, string message, Exception ex = null)
    {
        AddEntry(modId ?? "unknown", null, message, ErrorSeverity.Warning, ex);
    }

    public static void Info(string modId, string message, Exception ex = null)
    {
        AddEntry(modId ?? "unknown", null, message, ErrorSeverity.Info, ex);
    }

    public static void Fatal(string modId, string message, Exception ex = null)
    {
        AddEntry(modId ?? "unknown", null, message, ErrorSeverity.Fatal, ex);
    }

    internal static void ReportInternal(string context, string message, Exception ex = null)
    {
        AddEntry("Menace.SDK", context, message, ErrorSeverity.Error, ex);
    }

    internal static void WarnInternal(string context, string message)
    {
        AddEntry("Menace.SDK", context, message, ErrorSeverity.Warning, null);
    }

    internal static void InfoInternal(string source, string message)
    {
        AddEntry("Menace.SDK", source, message, ErrorSeverity.Info, null);
    }

    public static IReadOnlyList<ModErrorEntry> GetErrors(string modId = null)
    {
        lock (_lock)
        {
            if (modId == null)
                return _entries.ToList();
            return _entries.Where(e => e.ModId == modId).ToList();
        }
    }

    public static void Clear()
    {
        lock (_lock)
            _entries.Clear();
    }

    private static void AddEntry(string modId, string context, string message,
        ErrorSeverity severity, Exception ex)
    {
        ModErrorEntry entry;

        lock (_lock)
        {
            // Rate limiting
            if (!_rateBuckets.TryGetValue(modId, out var bucket))
            {
                bucket = new RateBucket();
                _rateBuckets[modId] = bucket;
            }

            if (!bucket.TryConsume())
                return; // rate limited, silently drop

            // Deduplication: if same mod+message within window, increment count
            var now = DateTime.UtcNow;
            for (int i = _entries.Count - 1; i >= Math.Max(0, _entries.Count - 20); i--)
            {
                var existing = _entries[i];
                if (existing.ModId == modId &&
                    existing.Message == message &&
                    (now - existing.Timestamp).TotalSeconds < DedupeWindowSeconds)
                {
                    Interlocked.Increment(ref existing.OccurrenceCount);
                    existing.Timestamp = now;
                    return;
                }
            }

            entry = new ModErrorEntry
            {
                ModId = modId,
                Message = message,
                Context = context,
                Severity = severity,
                Timestamp = now,
                Exception = ex
            };

            _entries.Add(entry);

            // Enforce per-mod limit
            var modCount = _entries.Count(e => e.ModId == modId);
            if (modCount > MaxEntriesPerMod)
            {
                var idx = _entries.FindIndex(e => e.ModId == modId);
                if (idx >= 0) _entries.RemoveAt(idx);
            }

            // Enforce global limit
            while (_entries.Count > GlobalMaxEntries)
                _entries.RemoveAt(0);
        }

        // Log to MelonLogger outside the lock
        var prefix = string.IsNullOrEmpty(context) ? $"[{modId}]" : $"[{modId}:{context}]";
        switch (severity)
        {
            case ErrorSeverity.Info:
                MelonLogger.Msg($"{prefix} {message}");
                break;
            case ErrorSeverity.Warning:
                MelonLogger.Warning($"{prefix} {message}");
                break;
            case ErrorSeverity.Error:
            case ErrorSeverity.Fatal:
                MelonLogger.Error($"{prefix} {message}");
                if (ex != null)
                    MelonLogger.Error($"{prefix} {ex}");
                break;
        }

        try { OnError?.Invoke(entry); }
        catch { /* never crash from event handlers */ }
    }
}
