using ReactiveUI;
using System;
using System.IO;
using System.Reactive;
using Menace.Modkit.App.Services;
using Menace.Modkit.Core.Models;

namespace Menace.Modkit.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
  private string _installPathStatus = string.Empty;
  private string _cacheStatus = "Calculating...";

  public SettingsViewModel(IServiceProvider serviceProvider)
  {
    // Load from app settings
    ValidateInstallPath();
    UpdateCacheStatus();

    // Commands
    ViewCacheDetailsCommand = ReactiveCommand.Create(ViewCacheDetails);
    ClearCacheCommand = ReactiveCommand.Create(ClearCache);
    ForceReExtractCommand = ReactiveCommand.Create(ForceReExtract);
  }

  public string GameInstallPath
  {
    get => AppSettings.Instance.GameInstallPath;
    set
    {
      if (AppSettings.Instance.GameInstallPath != value)
      {
        AppSettings.Instance.SetGameInstallPath(value);
        this.RaisePropertyChanged();
        ValidateInstallPath();
        UpdateCacheStatus();
      }
    }
  }

  public string InstallPathStatus
  {
    get => _installPathStatus;
    private set => this.RaiseAndSetIfChanged(ref _installPathStatus, value);
  }

  // Extraction Settings Properties
  public bool AutoUpdateOnGameChange
  {
    get => AppSettings.Instance.ExtractionSettings.AutoUpdateOnGameChange;
    set
    {
      if (AppSettings.Instance.ExtractionSettings.AutoUpdateOnGameChange != value)
      {
        AppSettings.Instance.ExtractionSettings.AutoUpdateOnGameChange = value;
        AppSettings.Instance.SetExtractionSettings(AppSettings.Instance.ExtractionSettings);
        this.RaisePropertyChanged();
      }
    }
  }

  public bool EnableCaching
  {
    get => AppSettings.Instance.ExtractionSettings.EnableCaching;
    set
    {
      if (AppSettings.Instance.ExtractionSettings.EnableCaching != value)
      {
        AppSettings.Instance.ExtractionSettings.EnableCaching = value;
        AppSettings.Instance.SetExtractionSettings(AppSettings.Instance.ExtractionSettings);
        this.RaisePropertyChanged();
      }
    }
  }

  public bool KeepFullIL2CppDump
  {
    get => AppSettings.Instance.ExtractionSettings.KeepFullIL2CppDump;
    set
    {
      if (AppSettings.Instance.ExtractionSettings.KeepFullIL2CppDump != value)
      {
        AppSettings.Instance.ExtractionSettings.KeepFullIL2CppDump = value;
        AppSettings.Instance.SetExtractionSettings(AppSettings.Instance.ExtractionSettings);
        this.RaisePropertyChanged();
      }
    }
  }

  public bool ShowExtractionProgress
  {
    get => AppSettings.Instance.ExtractionSettings.ShowExtractionProgress;
    set
    {
      if (AppSettings.Instance.ExtractionSettings.ShowExtractionProgress != value)
      {
        AppSettings.Instance.ExtractionSettings.ShowExtractionProgress = value;
        AppSettings.Instance.SetExtractionSettings(AppSettings.Instance.ExtractionSettings);
        this.RaisePropertyChanged();
      }
    }
  }

  public AssetRipperProfileType SelectedAssetProfile
  {
    get => AppSettings.Instance.ExtractionSettings.AssetProfile;
    set
    {
      if (AppSettings.Instance.ExtractionSettings.AssetProfile != value)
      {
        AppSettings.Instance.ExtractionSettings.AssetProfile = value;
        AppSettings.Instance.SetExtractionSettings(AppSettings.Instance.ExtractionSettings);
        this.RaisePropertyChanged();
      }
    }
  }

  public bool IsEssentialProfile
  {
    get => SelectedAssetProfile == AssetRipperProfileType.Essential;
    set { if (value) SelectedAssetProfile = AssetRipperProfileType.Essential; }
  }

  public bool IsStandardProfile
  {
    get => SelectedAssetProfile == AssetRipperProfileType.Standard;
    set { if (value) SelectedAssetProfile = AssetRipperProfileType.Standard; }
  }

  public bool IsCompleteProfile
  {
    get => SelectedAssetProfile == AssetRipperProfileType.Complete;
    set { if (value) SelectedAssetProfile = AssetRipperProfileType.Complete; }
  }

  public bool IsCustomProfile
  {
    get => SelectedAssetProfile == AssetRipperProfileType.Custom;
    set { if (value) SelectedAssetProfile = AssetRipperProfileType.Custom; }
  }

  public string CacheStatus
  {
    get => _cacheStatus;
    private set => this.RaiseAndSetIfChanged(ref _cacheStatus, value);
  }

  // Commands
  public ReactiveCommand<Unit, Unit> ViewCacheDetailsCommand { get; }
  public ReactiveCommand<Unit, Unit> ClearCacheCommand { get; }
  public ReactiveCommand<Unit, Unit> ForceReExtractCommand { get; }

  private void ValidateInstallPath()
  {
    if (string.IsNullOrWhiteSpace(GameInstallPath))
    {
      InstallPathStatus = "Please set the game installation directory";
      return;
    }

    if (!Directory.Exists(GameInstallPath))
    {
      InstallPathStatus = "❌ Directory not found";
      return;
    }

    var menaceDataPath = Path.Combine(GameInstallPath, "Menace_Data");
    if (!Directory.Exists(menaceDataPath))
    {
      InstallPathStatus = "❌ Not a valid Menace installation (Menace_Data folder not found)";
      return;
    }

    InstallPathStatus = "✓ Game installation found";
  }

  private void UpdateCacheStatus()
  {
    if (string.IsNullOrWhiteSpace(GameInstallPath) || !Directory.Exists(GameInstallPath))
    {
      CacheStatus = "No cache";
      return;
    }

    var cachePath = Path.Combine(GameInstallPath, "UserData", "ExtractionCache");
    if (!Directory.Exists(cachePath))
    {
      CacheStatus = "No cache";
      return;
    }

    try
    {
      var manifestPath = Path.Combine(cachePath, "manifest.json");
      if (!File.Exists(manifestPath))
      {
        CacheStatus = "No cache";
        return;
      }

      var manifestInfo = new FileInfo(manifestPath);
      var timeSince = DateTime.Now - manifestInfo.LastWriteTime;

      var cacheSize = GetDirectorySize(cachePath);
      var sizeStr = FormatBytes(cacheSize);

      string timeStr = timeSince.TotalHours < 1
        ? $"{(int)timeSince.TotalMinutes} minutes ago"
        : timeSince.TotalDays < 1
          ? $"{(int)timeSince.TotalHours} hours ago"
          : $"{(int)timeSince.TotalDays} days ago";

      CacheStatus = $"Last extraction: {timeStr} | Cache size: {sizeStr}";
    }
    catch
    {
      CacheStatus = "Error reading cache";
    }
  }

  private void ViewCacheDetails()
  {
    // TODO: Show detailed cache information dialog
  }

  private void ClearCache()
  {
    if (string.IsNullOrWhiteSpace(GameInstallPath) || !Directory.Exists(GameInstallPath))
      return;

    var cachePath = Path.Combine(GameInstallPath, "UserData", "ExtractionCache");
    if (Directory.Exists(cachePath))
    {
      try
      {
        Directory.Delete(cachePath, true);
        UpdateCacheStatus();
      }
      catch (Exception ex)
      {
        CacheStatus = $"Error clearing cache: {ex.Message}";
      }
    }
  }

  private void ForceReExtract()
  {
    ClearCache();
    // TODO: Trigger re-extraction
  }

  private static long GetDirectorySize(string path)
  {
    var dirInfo = new DirectoryInfo(path);
    return dirInfo.GetFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
  }

  private static string FormatBytes(long bytes)
  {
    string[] sizes = { "B", "KB", "MB", "GB" };
    double len = bytes;
    int order = 0;
    while (len >= 1024 && order < sizes.Length - 1)
    {
      order++;
      len = len / 1024;
    }
    return $"{len:0.##} {sizes[order]}";
  }
}
