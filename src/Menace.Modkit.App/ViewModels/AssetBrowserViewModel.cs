using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ReactiveUI;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.ViewModels;

public sealed class AssetBrowserViewModel : ViewModelBase
{
    private readonly AssetRipperService _assetRipperService;
    private readonly ModpackManager _modpackManager;

    // Master copy of all tree nodes (unfiltered)
    private List<AssetTreeNode> _allTreeNodes = new();
    // Set of relative paths that have staging replacements
    private readonly HashSet<string> _modpackAssetPaths = new(StringComparer.OrdinalIgnoreCase);

    // Tiered search index for ranked results
    private class SearchEntry
    {
        public string Name = "";     // filename
        public string Path = "";     // parent directory components
        public string FileType = ""; // file type category
    }
    private readonly Dictionary<AssetTreeNode, SearchEntry> _searchEntries = new();

    public AssetBrowserViewModel()
    {
        FolderTree = new ObservableCollection<AssetTreeNode>();
        _assetRipperService = new AssetRipperService();
        _modpackManager = new ModpackManager();
        AvailableModpacks = new ObservableCollection<string>();

        LoadModpacks();
        RefreshAssets();
    }

    public bool HasExtractedAssets => _assetRipperService.HasExtractedAssets();

    public ObservableCollection<AssetTreeNode> FolderTree { get; }
    public ObservableCollection<string> AvailableModpacks { get; }

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

    private string? _currentModpackName;
    public string? CurrentModpackName
    {
        get => _currentModpackName;
        set
        {
            if (_currentModpackName != value)
            {
                this.RaiseAndSetIfChanged(ref _currentModpackName, value);
                LoadModpackAssetPaths();
                LoadModifiedPreview();
                if (_showModpackOnly)
                    ApplySearchFilter();
            }
        }
    }

    private string _saveStatus = string.Empty;
    public string SaveStatus
    {
        get => _saveStatus;
        set => this.RaiseAndSetIfChanged(ref _saveStatus, value);
    }

    private bool _showModpackOnly;
    public bool ShowModpackOnly
    {
        get => _showModpackOnly;
        set
        {
            if (_showModpackOnly != value)
            {
                this.RaiseAndSetIfChanged(ref _showModpackOnly, value);
                ApplySearchFilter();
            }
        }
    }

    public async Task ExtractAssetsAsync()
    {
        IsExtracting = true;
        ExtractionStatus = "Starting extraction...";

        var lastError = string.Empty;
        var success = await _assetRipperService.ExtractAssetsAsync((progress) =>
        {
            ExtractionStatus = progress;
            if (progress.StartsWith("Error:") || progress.StartsWith("❌"))
            {
                lastError = progress;
            }
        });

        IsExtracting = false;

        if (success)
        {
            ExtractionStatus = "Extraction complete!";
            RefreshAssets();
        }
        else
        {
            if (!string.IsNullOrEmpty(lastError))
                ExtractionStatus = lastError;
            else
                ExtractionStatus = "Extraction failed. Make sure AssetRipper is installed and the game path is correct.";
        }
    }

    // --- Selection ---

    private AssetTreeNode? _selectedNode;
    public AssetTreeNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode != value)
            {
                this.RaiseAndSetIfChanged(ref _selectedNode, value);
                LoadAssetPreview();
                LoadModifiedPreview();
            }
        }
    }

    // --- Vanilla preview ---

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

    // --- Modified preview ---

    private Bitmap? _modifiedPreviewImage;
    public Bitmap? ModifiedPreviewImage
    {
        get => _modifiedPreviewImage;
        set => this.RaiseAndSetIfChanged(ref _modifiedPreviewImage, value);
    }

    private string _modifiedPreviewText = string.Empty;
    public string ModifiedPreviewText
    {
        get => _modifiedPreviewText;
        set => this.RaiseAndSetIfChanged(ref _modifiedPreviewText, value);
    }

    private bool _hasModifiedImagePreview;
    public bool HasModifiedImagePreview
    {
        get => _hasModifiedImagePreview;
        set => this.RaiseAndSetIfChanged(ref _hasModifiedImagePreview, value);
    }

    private bool _hasModifiedTextPreview;
    public bool HasModifiedTextPreview
    {
        get => _hasModifiedTextPreview;
        set => this.RaiseAndSetIfChanged(ref _hasModifiedTextPreview, value);
    }

    private bool _hasModifiedReplacement;
    public bool HasModifiedReplacement
    {
        get => _hasModifiedReplacement;
        set => this.RaiseAndSetIfChanged(ref _hasModifiedReplacement, value);
    }

    // --- Data loading ---

    private void LoadModpacks()
    {
        AvailableModpacks.Clear();
        foreach (var mp in _modpackManager.GetStagingModpacks())
            AvailableModpacks.Add(mp.Name);
    }

    private void LoadModpackAssetPaths()
    {
        _modpackAssetPaths.Clear();
        if (string.IsNullOrEmpty(_currentModpackName))
            return;

        foreach (var path in _modpackManager.GetStagingAssetPaths(_currentModpackName))
            _modpackAssetPaths.Add(path);
    }

    private void RefreshAssets()
    {
        FolderTree.Clear();
        _allTreeNodes.Clear();
        _searchEntries.Clear();

        var assetPath = AppSettings.GetEffectiveAssetsPath();

        if (assetPath != null)
        {
            LoadAssetFolders(assetPath);
            ExtractionStatus = $"Loaded assets from: {assetPath}";
        }
        else
        {
            ExtractionStatus = "No assets found. Click 'Extract Assets' to begin.";
        }
    }

    // Top-level folders to exclude — Scripts are handled by the Code screen,
    // and Assemblies are core Unity binaries not suitable for replacement.
    private static readonly HashSet<string> ExcludedTopLevelFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Assemblies", "Scripts"
    };

    private void LoadAssetFolders(string rootPath)
    {
        var rootNode = BuildFolderTree(rootPath);

        foreach (var child in rootNode.Children)
        {
            if (!child.IsFile && ExcludedTopLevelFolders.Contains(child.Name))
                continue;
            FolderTree.Add(child);
        }

        BuildSearchIndex(FolderTree);
        _allTreeNodes = FolderTree.ToList();
    }

    private AssetTreeNode BuildFolderTree(string folderPath)
    {
        var folderName = Path.GetFileName(folderPath);
        if (string.IsNullOrEmpty(folderName))
            folderName = folderPath;

        var node = new AssetTreeNode
        {
            Name = folderName,
            FullPath = folderPath,
            IsFile = false
        };

        try
        {
            // Add subdirectories first
            var subdirs = Directory.GetDirectories(folderPath)
                .OrderBy(d => Path.GetFileName(d));

            foreach (var subdir in subdirs)
            {
                var childNode = BuildFolderTree(subdir);
                node.Children.Add(childNode);
            }

            // Add files as children (after folders)
            var files = Directory.GetFiles(folderPath)
                .Where(f => !f.EndsWith(".meta"))
                .OrderBy(f => Path.GetFileName(f));

            foreach (var file in files)
            {
                node.Children.Add(new AssetTreeNode
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    IsFile = true,
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

    // --- Preview loading ---

    private void LoadAssetPreview()
    {
        HasImagePreview = false;
        HasTextPreview = false;
        PreviewImage = null;
        PreviewText = string.Empty;

        if (_selectedNode == null || !_selectedNode.IsFile)
            return;

        var ext = Path.GetExtension(_selectedNode.FullPath).ToLowerInvariant();

        if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp")
        {
            try
            {
                PreviewImage = new Bitmap(_selectedNode.FullPath);
                HasImagePreview = true;
                PreviewText = $"{_selectedNode.Name}\n{FormatFileSize(_selectedNode.Size)}\n{PreviewImage.PixelSize.Width}x{PreviewImage.PixelSize.Height}";
            }
            catch (Exception ex)
            {
                PreviewText = $"Error loading image: {ex.Message}";
                HasTextPreview = true;
            }
        }
        else if (ext is ".txt" or ".json" or ".xml" or ".cs" or ".shader")
        {
            try
            {
                var text = File.ReadAllText(_selectedNode.FullPath);
                PreviewText = text.Length > 5000 ? text.Substring(0, 5000) + "\n..." : text;
                HasTextPreview = true;
            }
            catch (Exception ex)
            {
                PreviewText = $"Error loading file: {ex.Message}";
                HasTextPreview = true;
            }
        }
        else
        {
            PreviewText = $"{_selectedNode.Name}\n{_selectedNode.FileType}\n{FormatFileSize(_selectedNode.Size)}";
            HasTextPreview = true;
        }
    }

    private void LoadModifiedPreview()
    {
        HasModifiedImagePreview = false;
        HasModifiedTextPreview = false;
        HasModifiedReplacement = false;
        ModifiedPreviewImage = null;
        ModifiedPreviewText = string.Empty;

        if (_selectedNode == null || !_selectedNode.IsFile || string.IsNullOrEmpty(_currentModpackName))
            return;

        var relativePath = GetAssetRelativePath(_selectedNode.FullPath);
        if (relativePath == null)
            return;

        var stagingPath = _modpackManager.GetStagingAssetPath(_currentModpackName, relativePath);
        if (stagingPath == null)
            return;

        HasModifiedReplacement = true;
        var ext = Path.GetExtension(stagingPath).ToLowerInvariant();

        if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp")
        {
            try
            {
                ModifiedPreviewImage = new Bitmap(stagingPath);
                HasModifiedImagePreview = true;
                ModifiedPreviewText = $"Replacement: {Path.GetFileName(stagingPath)}\n{FormatFileSize(new FileInfo(stagingPath).Length)}\n{ModifiedPreviewImage.PixelSize.Width}x{ModifiedPreviewImage.PixelSize.Height}";
            }
            catch (Exception ex)
            {
                ModifiedPreviewText = $"Error loading replacement: {ex.Message}";
                HasModifiedTextPreview = true;
            }
        }
        else if (ext is ".txt" or ".json" or ".xml" or ".cs" or ".shader")
        {
            try
            {
                var text = File.ReadAllText(stagingPath);
                ModifiedPreviewText = text.Length > 5000 ? text.Substring(0, 5000) + "\n..." : text;
                HasModifiedTextPreview = true;
            }
            catch (Exception ex)
            {
                ModifiedPreviewText = $"Error loading replacement: {ex.Message}";
                HasModifiedTextPreview = true;
            }
        }
        else
        {
            ModifiedPreviewText = $"Replacement: {Path.GetFileName(stagingPath)}\n{FormatFileSize(new FileInfo(stagingPath).Length)}";
            HasModifiedTextPreview = true;
        }
    }

    // --- Modpack operations ---

    public string? GetAssetRelativePath(string fullPath)
    {
        var assetPath = AppSettings.GetEffectiveAssetsPath();
        if (assetPath == null)
            return null;

        return Path.GetRelativePath(assetPath, fullPath);
    }

    public bool ReplaceAssetInModpack(string sourceFilePath)
    {
        if (_selectedNode == null || !_selectedNode.IsFile || string.IsNullOrEmpty(_currentModpackName))
            return false;

        var relativePath = GetAssetRelativePath(_selectedNode.FullPath);
        if (relativePath == null)
            return false;

        try
        {
            _modpackManager.SaveStagingAsset(_currentModpackName, relativePath, sourceFilePath);
            _modpackAssetPaths.Add(relativePath);
            LoadModifiedPreview();
            SaveStatus = $"Replaced: {_selectedNode.Name}";
            return true;
        }
        catch (Exception ex)
        {
            SaveStatus = $"Replace failed: {ex.Message}";
            return false;
        }
    }

    public void ClearAssetReplacement()
    {
        if (_selectedNode == null || !_selectedNode.IsFile || string.IsNullOrEmpty(_currentModpackName))
            return;

        var relativePath = GetAssetRelativePath(_selectedNode.FullPath);
        if (relativePath == null)
            return;

        try
        {
            _modpackManager.RemoveStagingAsset(_currentModpackName, relativePath);
            _modpackAssetPaths.Remove(relativePath);
            LoadModifiedPreview();
            SaveStatus = $"Cleared: {_selectedNode.Name}";
        }
        catch (Exception ex)
        {
            SaveStatus = $"Clear failed: {ex.Message}";
        }
    }

    public bool ExportAsset(string destinationPath)
    {
        if (_selectedNode == null || !_selectedNode.IsFile)
            return false;

        try
        {
            File.Copy(_selectedNode.FullPath, destinationPath, true);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Export failed: {ex.Message}");
            return false;
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

    // --- Search and filtering ---

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                this.RaiseAndSetIfChanged(ref _searchText, value);
                ApplySearchFilter();
            }
        }
    }

    private void ApplySearchFilter()
    {
        FolderTree.Clear();

        var hasQuery = !string.IsNullOrWhiteSpace(_searchText);
        var query = hasQuery ? _searchText.Trim() : null;

        if (!hasQuery && !_showModpackOnly)
        {
            foreach (var node in _allTreeNodes)
                FolderTree.Add(node);
            return;
        }

        var scores = new Dictionary<AssetTreeNode, int>();

        foreach (var node in _allTreeNodes)
        {
            var filtered = FilterNode(node, query, scores);
            if (filtered != null)
                FolderTree.Add(filtered);
        }

        // Sort results by score when there's an active search query
        if (hasQuery)
        {
            foreach (var node in FolderTree)
                SortByScore(node, scores);

            var sortedRoots = FolderTree.OrderByDescending(n =>
                scores.TryGetValue(n, out var s) ? s : 0).ToList();
            FolderTree.Clear();
            foreach (var n in sortedRoots)
                FolderTree.Add(n);
        }

        // Auto-expand filtered results
        SetExpansionState(FolderTree, true);
    }

    private AssetTreeNode? FilterNode(AssetTreeNode node, string? query, Dictionary<AssetTreeNode, int> scores)
    {
        // File (leaf) node
        if (node.IsFile)
        {
            // Modpack-only filter
            if (_showModpackOnly && !string.IsNullOrEmpty(_currentModpackName))
            {
                var relativePath = GetAssetRelativePath(node.FullPath);
                if (relativePath == null || !_modpackAssetPaths.Contains(relativePath))
                    return null;
            }

            if (query == null)
                return node;

            var score = ScoreMatch(node, query);
            if (score < 0)
                return null;

            scores[node] = score;
            return node;
        }

        // Folder name matches query (and not modpack-only) -> include entire subtree
        if (query != null && !_showModpackOnly &&
            node.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            return node;

        // Check children recursively
        var matchingChildren = new List<AssetTreeNode>();
        foreach (var child in node.Children)
        {
            var filtered = FilterNode(child, query, scores);
            if (filtered != null)
                matchingChildren.Add(filtered);
        }

        if (matchingChildren.Count == 0)
            return null;

        if (matchingChildren.Count == node.Children.Count)
            return node;

        var copy = new AssetTreeNode
        {
            Name = node.Name,
            FullPath = node.FullPath,
            IsFile = false
        };
        foreach (var child in matchingChildren)
            copy.Children.Add(child);

        return copy;
    }

    public void ExpandAll()
    {
        SetExpansionState(FolderTree, true);
    }

    public void CollapseAll()
    {
        SetExpansionState(FolderTree, false);
    }

    private static void SetExpansionState(IEnumerable<AssetTreeNode> nodes, bool expanded)
    {
        foreach (var node in nodes)
        {
            if (!node.IsFile)
            {
                node.IsExpanded = expanded;
                SetExpansionState(node.Children, expanded);
            }
        }
    }

    private void BuildSearchIndex(IEnumerable<AssetTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsFile)
            {
                var entry = new SearchEntry
                {
                    Name = node.Name,
                    Path = System.IO.Path.GetDirectoryName(node.FullPath) ?? "",
                    FileType = node.FileType
                };
                _searchEntries[node] = entry;
            }
            else
            {
                BuildSearchIndex(node.Children);
            }
        }
    }

    private int ScoreMatch(AssetTreeNode node, string query)
    {
        if (!_searchEntries.TryGetValue(node, out var entry)) return -1;
        if (entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) return 100;
        if (entry.Path.Contains(query, StringComparison.OrdinalIgnoreCase)) return 40;
        if (entry.FileType.Contains(query, StringComparison.OrdinalIgnoreCase)) return 20;
        return -1;
    }

    private int SortByScore(AssetTreeNode node, Dictionary<AssetTreeNode, int> scores)
    {
        if (node.IsFile)
            return scores.TryGetValue(node, out var s) ? s : 0;

        int maxChild = 0;
        foreach (var child in node.Children)
        {
            var childScore = SortByScore(child, scores);
            if (childScore > maxChild) maxChild = childScore;
        }

        var sorted = node.Children.OrderByDescending(c =>
            scores.TryGetValue(c, out var s) ? s : 0).ToList();
        node.Children.Clear();
        foreach (var c in sorted) node.Children.Add(c);

        scores[node] = maxChild;
        return maxChild;
    }
}

public sealed class AssetTreeNode : ViewModelBase
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsFile { get; set; }
    public string FileType { get; set; } = string.Empty;
    public long Size { get; set; }
    public ObservableCollection<AssetTreeNode> Children { get; } = new();

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public override string ToString() => Name;
}
