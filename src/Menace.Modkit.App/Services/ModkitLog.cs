using System;
using System.IO;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Simple file logger for the modkit app.
/// Writes to ~/Documents/MenaceModkit/modkit.log
/// </summary>
public static class ModkitLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "MenaceModkit", "modkit.log");

    private static readonly object _lock = new();

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
            lock (_lock)
            {
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            Console.WriteLine(line);
        }
        catch { }
    }
}
