# ModError

`Menace.SDK.ModError` -- Central error reporting for the Menace SDK.

Never throws. All failures are routed to `MelonLogger` and stored in a queryable ring buffer. The SDK's own internal errors are also funneled through this system.

## ErrorSeverity Enum

```csharp
public enum ErrorSeverity { Info, Warning, Error, Fatal }
```

## ModErrorEntry Class

```csharp
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
```

`OccurrenceCount` is incremented when a duplicate message from the same mod is reported within the deduplication window (see below).

## Methods

### Report

```csharp
public static void Report(string modId, string message, Exception ex = null,
    ErrorSeverity severity = ErrorSeverity.Error)
```

Report an error (or other severity). If `modId` is null, it defaults to `"unknown"`.

Logged to `MelonLogger` as:
- `ErrorSeverity.Info` -- `MelonLogger.Msg`
- `ErrorSeverity.Warning` -- `MelonLogger.Warning`
- `ErrorSeverity.Error` / `Fatal` -- `MelonLogger.Error` (exception is also logged if present)

### Warn

```csharp
public static void Warn(string modId, string message)
```

Shorthand for `Report(modId, message, null, ErrorSeverity.Warning)`.

### Info

```csharp
public static void Info(string modId, string message)
```

Shorthand for `Report(modId, message, null, ErrorSeverity.Info)`.

### GetErrors

```csharp
public static IReadOnlyList<ModErrorEntry> GetErrors(string modId = null)
```

Return a snapshot of stored errors. If `modId` is provided, only entries from that mod are returned. Returns a copy of the list (thread-safe).

### RecentErrors

```csharp
public static IReadOnlyList<ModErrorEntry> RecentErrors { get; }
```

Property returning a snapshot of all stored errors. Equivalent to `GetErrors(null)`.

### Clear

```csharp
public static void Clear()
```

Remove all stored error entries.

## Events

### OnError

```csharp
public static event Action<ModErrorEntry> OnError;
```

Fired after each new error entry is stored and logged. If the event handler itself throws, the exception is silently swallowed (the error system must never crash).

## Ring Buffer and Rate Limiting

The error store uses a ring buffer with the following limits:

| Setting | Value | Description |
|---------|-------|-------------|
| Max entries per mod | **200** | When a mod exceeds this count, the oldest entry from that mod is evicted. |
| Global max entries | **1000** | When the total exceeds this, the oldest entry (any mod) is evicted from the front. |
| Rate limit | **10/sec** per mod | Per-mod token bucket. Reports exceeding the rate are silently dropped. Tokens refill continuously. |
| Deduplication window | **5 seconds** | If the same `(modId, message)` pair is reported within 5 seconds, the existing entry's `OccurrenceCount` is incremented and its `Timestamp` is updated instead of creating a new entry. The dedup scan checks the last 20 entries. |

These limits prevent a broken mod from flooding the log or consuming unbounded memory.

## Examples

### Reporting errors from a mod

```csharp
try
{
    // risky operation
}
catch (Exception ex)
{
    ModError.Report("MyMod", "Failed to load weapon data", ex);
}
```

### Warnings and info

```csharp
ModError.Warn("MyMod", "Template 'LaserRifle' not found, using fallback");
ModError.Info("MyMod", "Loaded 42 weapon templates");
```

### Querying errors

```csharp
// All errors
var all = ModError.GetErrors();
DevConsole.Log($"Total errors: {all.Count}");

// Errors from a specific mod
var myErrors = ModError.GetErrors("MyMod");
foreach (var entry in myErrors)
{
    DevConsole.Log($"[{entry.Severity}] {entry.Message} (x{entry.OccurrenceCount})");
}
```

### Subscribing to new errors

```csharp
ModError.OnError += entry =>
{
    if (entry.Severity >= ErrorSeverity.Error)
        DevConsole.Log($"NEW ERROR from {entry.ModId}: {entry.Message}");
};
```

### Clearing the error buffer

```csharp
ModError.Clear();
```
