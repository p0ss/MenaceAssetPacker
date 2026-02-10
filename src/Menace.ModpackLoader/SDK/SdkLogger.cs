using MelonLoader;

namespace Menace.SDK;

/// <summary>
/// Logging utility that writes to both MelonLogger (file) and DevConsole (in-game overlay).
/// Use this for important messages that should be visible in the in-game dev console.
/// </summary>
public static class SdkLogger
{
    private static MelonLogger.Instance _instance;

    /// <summary>
    /// Initialize with a MelonLogger instance. Call from OnInitializeMelon().
    /// </summary>
    public static void Initialize(MelonLogger.Instance logger)
    {
        _instance = logger;
    }

    /// <summary>
    /// Log an informational message to both MelonLoader and DevConsole.
    /// </summary>
    public static void Msg(string message)
    {
        _instance?.Msg(message);
        DevConsole.Log(message);
    }

    /// <summary>
    /// Log a warning to both MelonLoader and DevConsole.
    /// </summary>
    public static void Warning(string message)
    {
        _instance?.Warning(message);
        DevConsole.LogWarning(message);
    }

    /// <summary>
    /// Log an error to both MelonLoader and DevConsole.
    /// </summary>
    public static void Error(string message)
    {
        _instance?.Error(message);
        DevConsole.LogError(message);
    }

    /// <summary>
    /// Log a formatted message to both MelonLoader and DevConsole.
    /// </summary>
    public static void Msg(string format, params object[] args)
    {
        var message = string.Format(format, args);
        _instance?.Msg(message);
        DevConsole.Log(message);
    }
}
