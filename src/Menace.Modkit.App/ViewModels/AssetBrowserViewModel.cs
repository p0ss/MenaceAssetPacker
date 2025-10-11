using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;
using Menace.Modkit.App.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Menace.Modkit.App.ViewModels;

public sealed class AssetBrowserViewModel : ViewModelBase
{
  private readonly ObservableCollection<AssetFolderTreeNode> _rootFolders = new();
  private readonly AssetRipperService _assetRipperService;

  public AssetBrowserViewModel()
  {
    FolderTree = new ObservableCollection<AssetFolderTreeNode>();
    _assetRipperService = new AssetRipperService();

    RefreshAssets();
  }

  public bool HasExtractedAssets => _assetRipperService.HasExtractedAssets();

  public ObservableCollection<AssetFolderTreeNode> FolderTree { get; }

  private string _extractionStatus = string.Empty;
  public string ExtractionStatus
  {
    get => _extractionStatus;
    set => this.RaiseAndSetIfChanged(ref _extractionStatus, value);
  }

  private bool _isExtracting;
  public bool IsExtracting
  {
    get => _isExtracting;
    set => this.RaiseAndSetIfChanged(ref _isExtracting, value);
  }

  public async Task ExtractAssetsAsync()
  {
    IsExtracting = true;
    ExtractionStatus = "Starting extraction...";

    var lastError = string.Empty;
    var success = await _assetRipperService.ExtractAssetsAsync((progress) =>
    {
      ExtractionStatus = progress;
      // Capture error messages
      if (progress.StartsWith("Error:") || progress.StartsWith("❌"))
      {
        lastError = progress;
      }
    });

    IsExtracting = false;

    if (success)
    {
      ExtractionStatus = "✓ Extraction complete!";
      RefreshAssets();
    }
    else
    {
      // Show the actual error if we captured one
      if (!string.IsNullOrEmpty(lastError))
      {
        ExtractionStatus = lastError;
      }
      else
      {
        ExtractionStatus = "❌ Extraction failed. Make sure AssetRipper is installed and the game path is correct.";
      }
    }
  }

  private AssetFileInfo? _selectedAsset;
  public AssetFileInfo? SelectedAsset
  {
    get => _selectedAsset;
    set
    {
      this.RaiseAndSetIfChanged(ref _selectedAsset, value);
      LoadAssetPreview();
    }
  }

  private Bitmap? _previewImage;
  public Bitmap? PreviewImage
  {
    get => _previewImage;
    set => this.RaiseAndSetIfChanged(ref _previewImage, value);
  }

  private string _previewText = string.Empty;
  public string PreviewText
  {
    get => _previewText;
    set => this.RaiseAndSetIfChanged(ref _previewText, value);
  }

  private bool _hasImagePreview;
  public bool HasImagePreview
  {
    get => _hasImagePreview;
    set => this.RaiseAndSetIfChanged(ref _hasImagePreview, value);
  }

  private bool _hasTextPreview;
  public bool HasTextPreview
  {
    get => _hasTextPreview;
    set => this.RaiseAndSetIfChanged(ref _hasTextPreview, value);
  }

  private void RefreshAssets()
  {
    _rootFolders.Clear();
    FolderTree.Clear();

    // Try to find asset output path from game install or default location
    var gameInstallPath = AppSettings.Instance.GameInstallPath;
    string assetPath;

    if (!string.IsNullOrEmpty(gameInstallPath) && Directory.Exists(gameInstallPath))
    {
      assetPath = Path.Combine(gameInstallPath, "UserData", "ExtractedAssets");
    }
    else
    {
      assetPath = Path.Combine(Directory.GetCurrentDirectory(), "out2");
    }

    if (Directory.Exists(assetPath))
    {
      LoadAssetFolders(assetPath);
      ExtractionStatus = $"Loaded assets from: {assetPath}";
    }
    else
    {
      ExtractionStatus = "No assets found. Click 'Extract Assets with AssetRipper' to begin.";
    }
  }

  private void LoadAssetFolders(string rootPath)
  {
    var rootNode = BuildFolderTree(rootPath, null);

    foreach (var child in rootNode.Children)
    {
      FolderTree.Add(child);
    }
  }

  private AssetFolderTreeNode BuildFolderTree(string folderPath, AssetFolderTreeNode? parent)
  {
    var folderName = Path.GetFileName(folderPath);
    if (string.IsNullOrEmpty(folderName))
      folderName = folderPath;

    var node = new AssetFolderTreeNode
    {
      Name = folderName,
      FullPath = folderPath,
      Parent = parent
    };

    try
    {
      // Add subdirectories
      var subdirs = Directory.GetDirectories(folderPath)
        .OrderBy(d => Path.GetFileName(d));

      foreach (var subdir in subdirs)
      {
        var childNode = BuildFolderTree(subdir, node);
        node.Children.Add(childNode);
      }

      // Add files
      var files = Directory.GetFiles(folderPath)
        .Where(f => !f.EndsWith(".meta"))
        .OrderBy(f => Path.GetFileName(f));

      foreach (var file in files)
      {
        node.Files.Add(new AssetFileInfo
        {
          Name = Path.GetFileName(file),
          FullPath = file,
          FileType = GetFileType(file),
          Size = new FileInfo(file).Length
        });
      }
    }
    catch
    {
      // Ignore access errors
    }

    return node;
  }

  private string GetFileType(string filePath)
  {
    var ext = Path.GetExtension(filePath).ToLowerInvariant();
    return ext switch
    {
      ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tga" => "Image",
      ".wav" or ".mp3" or ".ogg" => "Audio",
      ".fbx" or ".obj" or ".dae" => "Model",
      ".cs" => "Script",
      ".json" or ".txt" or ".xml" => "Text",
      ".shader" => "Shader",
      ".mat" => "Material",
      ".prefab" => "Prefab",
      _ => Path.GetExtension(filePath)
    };
  }

  private void LoadAssetPreview()
  {
    HasImagePreview = false;
    HasTextPreview = false;
    PreviewImage = null;
    PreviewText = string.Empty;

    if (SelectedAsset == null)
      return;

    var ext = Path.GetExtension(SelectedAsset.FullPath).ToLowerInvariant();

    // Image preview
    if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp")
    {
      try
      {
        PreviewImage = new Bitmap(SelectedAsset.FullPath);
        HasImagePreview = true;
        PreviewText = $"{SelectedAsset.Name}\n{FormatFileSize(SelectedAsset.Size)}\n{PreviewImage.PixelSize.Width}x{PreviewImage.PixelSize.Height}";
      }
      catch (Exception ex)
      {
        PreviewText = $"Error loading image: {ex.Message}";
        HasTextPreview = true;
      }
    }
    // Text preview
    else if (ext is ".txt" or ".json" or ".xml" or ".cs" or ".shader")
    {
      try
      {
        var text = File.ReadAllText(SelectedAsset.FullPath);
        PreviewText = text.Length > 5000 ? text.Substring(0, 5000) + "\n..." : text;
        HasTextPreview = true;
      }
      catch (Exception ex)
      {
        PreviewText = $"Error loading file: {ex.Message}";
        HasTextPreview = true;
      }
    }
    // Generic info
    else
    {
      PreviewText = $"{SelectedAsset.Name}\n{SelectedAsset.FileType}\n{FormatFileSize(SelectedAsset.Size)}";
      HasTextPreview = true;
    }
  }

  private string FormatFileSize(long bytes)
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

  private string _searchText = string.Empty;
  public string SearchText
  {
    get => _searchText;
    set => this.RaiseAndSetIfChanged(ref _searchText, value);
  }

  /// <summary>
  /// Replace an asset in the current modpack
  /// </summary>
  public async Task<bool> ReplaceAssetInModpackAsync(string originalAssetPath, string replacementFilePath, string? currentModpackId)
  {
    if (string.IsNullOrEmpty(currentModpackId))
    {
      // TODO: Show dialog to select or create modpack
      return false;
    }

    try
    {
      // Get modpack directory
      var gameInstallPath = AppSettings.Instance.GameInstallPath;
      if (string.IsNullOrEmpty(gameInstallPath))
        return false;

      var modpackPath = Path.Combine(gameInstallPath, "Mods", currentModpackId, "Assets");
      Directory.CreateDirectory(modpackPath);

      // Determine asset relative path within extracted assets
      var extractedAssetsPath = Path.Combine(gameInstallPath, "UserData", "ExtractedAssets");
      var relativePath = Path.GetRelativePath(extractedAssetsPath, originalAssetPath);

      // Copy replacement file to modpack preserving directory structure
      var destinationPath = Path.Combine(modpackPath, relativePath);
      var destinationDir = Path.GetDirectoryName(destinationPath);
      if (!string.IsNullOrEmpty(destinationDir))
        Directory.CreateDirectory(destinationDir);

      File.Copy(replacementFilePath, destinationPath, true);

      // Update modpack manifest with asset replacement entry
      UpdateModpackManifest(gameInstallPath, currentModpackId, relativePath);

      return true;
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine($"Asset replacement failed: {ex.Message}");
      return false;
    }
  }

  /// <summary>
  /// Export an asset to a file
  /// </summary>
  public bool ExportAsset(string sourcePath, string destinationPath)
  {
    try
    {
      File.Copy(sourcePath, destinationPath, true);
      return true;
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine($"Export failed: {ex.Message}");
      return false;
    }
  }

  /// <summary>
  /// Update modpack manifest to register asset replacement
  /// </summary>
  private void UpdateModpackManifest(string gameInstallPath, string modpackId, string assetRelativePath)
  {
    try
    {
      var modpackDir = Path.Combine(gameInstallPath, "Mods", modpackId);
      var manifestPath = Path.Combine(modpackDir, "modpack.json");

      JObject manifest;
      if (File.Exists(manifestPath))
      {
        var json = File.ReadAllText(manifestPath);
        manifest = JObject.Parse(json);
      }
      else
      {
        // Create new manifest
        manifest = new JObject
        {
          ["name"] = modpackId,
          ["version"] = "1.0.0",
          ["author"] = "Menace Modkit",
          ["templates"] = new JObject(),
          ["assets"] = new JObject()
        };
      }

      // Add or update asset entry
      var assets = manifest["assets"] as JObject ?? new JObject();
      var assetKey = assetRelativePath.Replace("\\", "/"); // Normalize path separators
      var assetFilePath = Path.Combine("Assets", assetRelativePath).Replace("\\", "/");
      assets[assetKey] = assetFilePath;
      manifest["assets"] = assets;

      // Write back to file with formatting
      File.WriteAllText(manifestPath, manifest.ToString(Formatting.Indented));
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine($"Failed to update manifest: {ex.Message}");
    }
  }
}

public sealed class AssetFolderTreeNode : ViewModelBase
{
  public string Name { get; set; } = string.Empty;
  public string FullPath { get; set; } = string.Empty;
  public AssetFolderTreeNode? Parent { get; set; }
  public ObservableCollection<AssetFolderTreeNode> Children { get; } = new();
  public ObservableCollection<AssetFileInfo> Files { get; } = new();

  public bool HasChildren => Children.Count > 0 || Files.Count > 0;
}

public sealed class AssetFileInfo : ViewModelBase
{
  public string Name { get; set; } = string.Empty;
  public string FullPath { get; set; } = string.Empty;
  public string FileType { get; set; } = string.Empty;
  public long Size { get; set; }
}
