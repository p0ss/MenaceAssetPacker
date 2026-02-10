using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Simple file logger for the modkit app.
/// Writes to ~/Documents/MenaceModkit/modkit.log
/// </summary>
public static class ModkitLog
{
    private static readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "MenaceModkit", "modkit.log");

    private static readonly object _lock = new();

    /// <summary>
    /// Gets the full path to the modkit log file.
    /// </summary>
    public static string LogPath => _logPath;

    /// <summary>
    /// Gets the directory containing the log file.
    /// </summary>
    public static string LogDirectory => Path.GetDirectoryName(_logPath) ?? "";

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(_logPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
            lock (_lock)
            {
                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
            Console.WriteLine(line);
        }
        catch { }
    }

    /// <summary>
    /// Open the log file in the system's default text editor.
    /// </summary>
    public static void OpenLogFile()
    {
        try
        {
            if (!File.Exists(_logPath))
            {
                Info("Log file opened (was empty)");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(_logPath) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", _logPath);
            }
            else // Linux
            {
                Process.Start("xdg-open", _logPath);
            }
        }
        catch (Exception ex)
        {
            Error($"Failed to open log file: {ex.Message}");
        }
    }

    /// <summary>
    /// Open the log directory in the system's file manager.
    /// </summary>
    public static void OpenLogDirectory()
    {
        try
        {
            var dir = Path.GetDirectoryName(_logPath);
            if (string.IsNullOrEmpty(dir)) return;

            Directory.CreateDirectory(dir);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", dir);
            }
            else // Linux
            {
                Process.Start("xdg-open", dir);
            }
        }
        catch (Exception ex)
        {
            Error($"Failed to open log directory: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear the log file and start fresh.
    /// </summary>
    public static void ClearLog()
    {
        try
        {
            lock (_lock)
            {
                if (File.Exists(_logPath))
                    File.Delete(_logPath);
            }
            Info("Log cleared");
        }
        catch { }
    }
}
