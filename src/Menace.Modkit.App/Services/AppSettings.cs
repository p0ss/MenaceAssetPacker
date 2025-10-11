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
    private ExtractionSettings _extractionSettings = new();

    private AppSettings()
    {
        // Set default path
        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".steam", "debian-installation", "steamapps", "common", "Menace Demo");

        if (Directory.Exists(defaultPath))
        {
            _gameInstallPath = defaultPath;
        }
    }

    public static AppSettings Instance => _instance ??= new AppSettings();

    public string GameInstallPath
    {
        get => _gameInstallPath;
        set => _gameInstallPath = value;
    }

    public ExtractionSettings ExtractionSettings
    {
        get => _extractionSettings;
        set => _extractionSettings = value;
    }

    public event EventHandler? GameInstallPathChanged;
    public event EventHandler? ExtractionSettingsChanged;

    public void SetGameInstallPath(string path)
    {
        _gameInstallPath = path;
        GameInstallPathChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetExtractionSettings(ExtractionSettings settings)
    {
        _extractionSettings = settings;
        ExtractionSettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
