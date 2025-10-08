using ReactiveUI;
using System;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using Menace.Modkit.Typetrees;
using Microsoft.Extensions.DependencyInjection;

namespace Menace.Modkit.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
  private readonly IUnityVersionDetector _versionDetector;
  private readonly ITypetreeCacheBuilder _cacheBuilder;

  private string _menaceInstallPath = string.Empty;
  private string _unityVersionStatus = "Unity version not detected.";
  private string _typetreeStatus = "Typetree cache not created.";
  private string? _detectedUnityVersion;

  public SettingsViewModel(IServiceProvider serviceProvider)
  {
    _versionDetector = serviceProvider.GetRequiredService<IUnityVersionDetector>();
    _cacheBuilder = serviceProvider.GetRequiredService<ITypetreeCacheBuilder>();

    DetectUnityVersion = ReactiveCommand.Create(HandleDetectUnityVersion);
    BuildTypetreeCache = ReactiveCommand.CreateFromTask(HandleBuildTypetreeCacheAsync);
  }

  public string MenaceInstallPath
  {
    get => _menaceInstallPath;
    set => this.RaiseAndSetIfChanged(ref _menaceInstallPath, value);
  }

  public string UnityVersionStatus
  {
    get => _unityVersionStatus;
    private set => this.RaiseAndSetIfChanged(ref _unityVersionStatus, value);
  }

  public string TypetreeStatus
  {
    get => _typetreeStatus;
    private set => this.RaiseAndSetIfChanged(ref _typetreeStatus, value);
  }

  public ReactiveCommand<Unit, Unit> DetectUnityVersion { get; }

  public ReactiveCommand<Unit, Unit> BuildTypetreeCache { get; }

  private void HandleDetectUnityVersion()
  {
    if (string.IsNullOrWhiteSpace(MenaceInstallPath))
    {
      UnityVersionStatus = "Set the Menace install path to detect the Unity version.";
      return;
    }

    if (!Directory.Exists(MenaceInstallPath))
    {
      UnityVersionStatus = $"Directory not found: {MenaceInstallPath}";
      return;
    }

    var version = _versionDetector.DetectVersion(MenaceInstallPath);
    if (version != null)
    {
      _detectedUnityVersion = version;
      UnityVersionStatus = $"✓ Detected Unity {version}";
    }
    else
    {
      _detectedUnityVersion = null;
      UnityVersionStatus = "❌ Could not detect Unity version. Make sure this is a valid game installation.";
    }
  }

  private async Task HandleBuildTypetreeCacheAsync()
  {
    if (string.IsNullOrWhiteSpace(MenaceInstallPath))
    {
      TypetreeStatus = "Provide the Menace install path before building the typetree cache.";
      return;
    }

    if (!Directory.Exists(MenaceInstallPath))
    {
      TypetreeStatus = $"Directory not found: {MenaceInstallPath}";
      return;
    }

    try
    {
      TypetreeStatus = "Building typetree cache... This may take a few minutes.";

      // Get the project root (go up from the game directory to find a good place for cache)
      var cacheOutputPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MenaceModkit",
        "Typetrees"
      );

      var request = new TypetreeCacheRequest(
        SourcePath: MenaceInstallPath,
        OutputPath: cacheOutputPath,
        GameVersion: "demo", // Could extract from game files if needed
        UnityVersion: _detectedUnityVersion
      );

      var result = await _cacheBuilder.BuildAsync(request);

      // Read the manifest to check what was actually built
      var manifest = System.Text.Json.JsonSerializer.Deserialize<TypetreeCacheManifest>(
        await File.ReadAllTextAsync(result.ManifestPath));

      var fileCount = manifest?.Files?.Count ?? 0;

      if (fileCount == 0)
      {
        TypetreeStatus = $"⚠️ Typetree cache completed but no files were extracted.\nManifest: {result.ManifestPath}\nMake sure the path points to a valid Unity game installation.";
      }
      else
      {
        TypetreeStatus = $"✓ Typetree cache built successfully!\nExtracted {fileCount} typetree file(s)\nManifest: {result.ManifestPath}\nCreated: {result.CreatedAt:yyyy-MM-dd HH:mm:ss}";
      }
    }
    catch (Exception ex)
    {
      TypetreeStatus = $"❌ Error building typetree cache: {ex.Message}\n\nStack trace:\n{ex.StackTrace}";
    }
  }
}
