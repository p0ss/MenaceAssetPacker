using System;
using System.Collections.Generic;
using System.IO;

namespace Menace.Modkit.App.Models;

/// <summary>
/// Represents the type of save (matches game's SaveStateType enum).
/// </summary>
public enum SaveStateType
{
    None = 0,
    Manual = 1,
    Quick = 2,
    Auto = 3,
    Ironman = 4
}

/// <summary>
/// Represents the header metadata of a save file.
/// </summary>
public class SaveFileHeader
{
    /// <summary>
    /// Full path to the save file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// File name without extension.
    /// </summary>
    public string FileName => Path.GetFileNameWithoutExtension(FilePath);

    /// <summary>
    /// File modification time.
    /// </summary>
    public DateTime ModifiedTime { get; set; }

    /// <summary>
    /// Save file version (current is 101).
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Type of save (Manual, Auto, Quick).
    /// </summary>
    public SaveStateType SaveStateType { get; set; }

    /// <summary>
    /// When the save was created.
    /// </summary>
    public DateTime SaveTime { get; set; }

    /// <summary>
    /// Name of the current planet.
    /// </summary>
    public string PlanetName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the current operation.
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// Number of completed missions.
    /// </summary>
    public int CompletedMissions { get; set; }

    /// <summary>
    /// Total operation length.
    /// </summary>
    public int OperationLength { get; set; }

    /// <summary>
    /// Difficulty setting.
    /// </summary>
    public string Difficulty { get; set; } = string.Empty;

    /// <summary>
    /// Total play time in seconds.
    /// </summary>
    public double PlayTimeSeconds { get; set; }

    /// <summary>
    /// User-defined save game name.
    /// </summary>
    public string SaveGameName { get; set; } = string.Empty;

    /// <summary>
    /// Display name - uses SaveGameName if set, otherwise falls back to FileName.
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(SaveGameName) ? FileName : SaveGameName;

    /// <summary>
    /// Strategy configuration name.
    /// </summary>
    public string StrategyConfigName { get; set; } = string.Empty;

    /// <summary>
    /// Offset in file where the body starts (after header).
    /// </summary>
    public long BodyOffset { get; set; }

    /// <summary>
    /// Whether the header was parsed successfully.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Error message if parsing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Formatted play time (e.g., "2h 30m").
    /// </summary>
    public string PlayTimeFormatted
    {
        get
        {
            var ts = TimeSpan.FromSeconds(PlayTimeSeconds);
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{ts.Minutes}m {ts.Seconds}s";
        }
    }

    /// <summary>
    /// Display label for the save state type.
    /// </summary>
    public string TypeLabel => SaveStateType switch
    {
        SaveStateType.None => "None",
        SaveStateType.Manual => "Manual",
        SaveStateType.Quick => "Quick",
        SaveStateType.Auto => "Auto",
        SaveStateType.Ironman => "Ironman",
        _ => "Unknown"
    };

    /// <summary>
    /// Path to the associated screenshot file (.jpg or .png), if it exists.
    /// </summary>
    public string? ScreenshotPath
    {
        get
        {
            // Try .jpg first (used by the game), then .png as fallback
            var jpgPath = Path.ChangeExtension(FilePath, ".jpg");
            if (File.Exists(jpgPath))
                return jpgPath;

            var pngPath = Path.ChangeExtension(FilePath, ".png");
            return File.Exists(pngPath) ? pngPath : null;
        }
    }

    /// <summary>
    /// Parsed body data (StrategyState), loaded on demand.
    /// </summary>
    public SaveBodyData? BodyData { get; set; }

    /// <summary>
    /// Mod metadata from .modmeta sidecar file, if present.
    /// </summary>
    public ModMetaData? ModMeta { get; set; }

    /// <summary>
    /// Whether this save was created with mods active.
    /// </summary>
    public bool IsModded => ModMeta != null && ModMeta.Mods.Count > 0;
}

/// <summary>
/// Metadata about mods that were active when a save was created.
/// Loaded from .modmeta sidecar file.
/// </summary>
public class ModMetaData
{
    public string SavedWith { get; set; } = "";
    public string LoaderVersion { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public string GameVersion { get; set; } = "";
    public List<ModInfoEntry> Mods { get; set; } = new();
}

/// <summary>
/// Information about a single mod.
/// </summary>
public class ModInfoEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
}
