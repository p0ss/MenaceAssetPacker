using System;
using System.IO;
using Menace.Modkit.Core.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Central application settings service
/// </summary>
public class AppSettings
{
    private static AppSettings? _instance;
    private string _gameInstallPath = string.Empty;
    private string _extractedAssetsPath = string.Empty;
    private ExtractionSettings _extractionSettings = new();

    private AppSettings()
    {
        _gameInstallPath = DetectGameInstallPath();
    }

    private static string DetectGameInstallPath()
    {
        foreach (var steamCommon in GetSteamCommonPaths())
        {
            var eaPath = Path.Combine(steamCommon, "Menace");
            if (Directory.Exists(eaPath))
            {
                ModkitLog.Info($"Detected game install: {eaPath}");
                return eaPath;
            }

            var demoPath = Path.Combine(steamCommon, "Menace Demo");
            if (Directory.Exists(demoPath))
            {
                ModkitLog.Info($"Detected game install: {demoPath}");
                return demoPath;
            }
        }

        ModkitLog.Warn("Game install path not auto-detected. Set it manually in Settings.");
        return string.Empty;
    }

    private static string[] GetSteamCommonPaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsWindows())
        {
            return new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Steam", "steamapps", "common"),
                @"C:\Program Files (x86)\Steam\steamapps\common",
                @"D:\SteamLibrary\steamapps\common",
                @"E:\SteamLibrary\steamapps\common",
                Path.Combine(home, "Steam", "steamapps", "common"),
            };
        }

        if (OperatingSystem.IsMacOS())
        {
            return new[]
            {
                Path.Combine(home, "Library", "Application Support", "Steam", "steamapps", "common"),
            };
        }

        // Linux
        return new[]
        {
            Path.Combine(home, ".steam", "debian-installation", "steamapps", "common"),
            Path.Combine(home, ".steam", "steam", "steamapps", "common"),
            Path.Combine(home, ".local", "share", "Steam", "steamapps", "common"),
        };
    }

    public static AppSettings Instance => _instance ??= new AppSettings();

    public string GameInstallPath
    {
        get => _gameInstallPath;
        set => _gameInstallPath = value;
    }

    /// <summary>
    /// User-configured path to extracted assets directory.
    /// When empty, falls back to auto-detected paths.
    /// </summary>
    public string ExtractedAssetsPath
    {
        get => _extractedAssetsPath;
        set => _extractedAssetsPath = value;
    }

    public ExtractionSettings ExtractionSettings
    {
        get => _extractionSettings;
        set => _extractionSettings = value;
    }

    public event EventHandler? GameInstallPathChanged;
    public event EventHandler? ExtractedAssetsPathChanged;
    public event EventHandler? ExtractionSettingsChanged;

    public void SetGameInstallPath(string path)
    {
        _gameInstallPath = path;
        GameInstallPathChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetExtractedAssetsPath(string path)
    {
        _extractedAssetsPath = path;
        ExtractedAssetsPathChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetExtractionSettings(ExtractionSettings settings)
    {
        _extractionSettings = settings;
        ExtractionSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Resolves the effective extracted assets directory path.
    /// Priority: 1) User-configured path, 2) GameInstallPath/UserData/ExtractedAssets, 3) AppContext/out2/assets
    /// Returns null if no valid path is found.
    /// </summary>
    public static string? GetEffectiveAssetsPath()
    {
        // Priority 1: User-configured path
        var configured = Instance.ExtractedAssetsPath;
        if (!string.IsNullOrEmpty(configured) && Directory.Exists(configured))
            return configured;

        // Priority 2: Game install derived path
        var gameInstallPath = Instance.GameInstallPath;
        if (!string.IsNullOrEmpty(gameInstallPath) && Directory.Exists(gameInstallPath))
        {
            var derived = Path.Combine(gameInstallPath, "UserData", "ExtractedAssets");
            if (Directory.Exists(derived))
                return derived;
        }

        // Priority 3: AppContext fallback (out2/assets)
        var fallback = Path.Combine(AppContext.BaseDirectory, "out2", "assets");
        if (Directory.Exists(fallback))
            return fallback;

        return null;
    }

    /// <summary>
    /// Resolves the effective output path for AssetRipper extraction.
    /// Uses the configured path if set, otherwise defaults to AppContext/out2/assets.
    /// Unlike GetEffectiveAssetsPath, this always returns a path (for writing).
    /// </summary>
    public static string GetAssetExtractionOutputPath()
    {
        var configured = Instance.ExtractedAssetsPath;
        if (!string.IsNullOrEmpty(configured))
            return configured;

        return Path.Combine(AppContext.BaseDirectory, "out2", "assets");
    }
}
