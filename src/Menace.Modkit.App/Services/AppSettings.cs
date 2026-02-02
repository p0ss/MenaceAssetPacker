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
        // Try EA install first, then fall back to Demo
        var steamCommon = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".steam", "debian-installation", "steamapps", "common");

        var eaPath = Path.Combine(steamCommon, "Menace");
        var demoPath = Path.Combine(steamCommon, "Menace Demo");

        if (Directory.Exists(eaPath))
        {
            _gameInstallPath = eaPath;
        }
        else if (Directory.Exists(demoPath))
        {
            _gameInstallPath = demoPath;
        }
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
