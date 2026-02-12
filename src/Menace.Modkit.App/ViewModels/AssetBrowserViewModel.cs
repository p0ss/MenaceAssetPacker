using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ReactiveUI;
using Menace.Modkit.App.Controls;
using Menace.Modkit.App.Extensions;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;
using GlbLinkedTexture = Menace.Modkit.App.Services.GlbLinkedTexture;

namespace Menace.Modkit.App.ViewModels;

public sealed class AssetBrowserViewModel : ViewModelBase, ISearchableViewModel
{
    /// <summary>
    /// Special value in the modpack dropdown that triggers create-mod flow.
    /// </summary>
    public const string CreateNewModOption = "+ Create New Mod...";

    private const string ModpackNewAssetsSectionName = "Modpack New Assets";

    private readonly AssetRipperService _assetRipperService;
    private readonly ModpackManager _modpackManager;
    private ReferenceGraphService? _referenceGraphService;

    // Top-level nodes (for restoring tree view after filtering)
    private List<AssetTreeNode> _topLevelNodes = new();
    // Flat list of ALL nodes (for expand/collapse operations)
    private List<AssetTreeNode> _allTreeNodes = new();
    // Set of relative paths that have staging replacements
    private readonly HashSet<string> _modpackAssetPaths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Event fired when user clicks on an asset backlink to navigate to a template in the Stats Editor.
    /// Parameters: modpackName, templateType, instanceName
    /// </summary>
    public event Action<string?, string, string>? NavigateToTemplate;

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
        SearchResults = new ObservableCollection<SearchResultItem>();
        _assetRipperService = new AssetRipperService();
        _modpackManager = new ModpackManager();
        AvailableModpacks = new ObservableCollection<string>();
        AssetBacklinks = new ObservableCollection<ReferenceEntry>();

        LoadModpacks();
        RefreshAssets();
    }

    public bool HasExtractedAssets => _assetRipperService.HasExtractedAssets();

    /// <summary>
    /// True if AssetRipper is available for use.
    /// </summary>
    public bool IsAssetRipperAvailable => _assetRipperService.IsAssetRipperAvailable();

    /// <summary>
    /// Get the ModpackManager for use by wizards and dialogs.
    /// </summary>
    public ModpackManager? GetModpackManager() => _modpackManager;

    public ObservableCollection<AssetTreeNode> FolderTree { get; }
    public ObservableCollection<string> AvailableModpacks { get; }
    public ObservableCollection<ReferenceEntry> AssetBacklinks { get; }

    // ISearchableViewModel implementation
    public ObservableCollection<SearchResultItem> SearchResults { get; }
    public ObservableCollection<string> SectionFilters { get; } = new() { "All Sections" };

    /// <summary>
    /// True when search mode is active (3+ characters entered).
    /// </summary>
    public bool IsSearching => SearchText.Length >= 3;

    private SearchPanelBuilder.SortOption _currentSortOption = SearchPanelBuilder.SortOption.Relevance;
    public SearchPanelBuilder.SortOption CurrentSortOption
    {
        get => _currentSortOption;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentSortOption, value);
            if (IsSearching) ApplySearchSort();
        }
    }

    private string? _selectedSectionFilter = "All Sections";
    public string? SelectedSectionFilter
    {
        get => _selectedSectionFilter;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedSectionFilter, value);
            if (IsSearching) GenerateSearchResults();
        }
    }

    /// <summary>
    /// Set the reference graph service for querying asset backlinks.
    /// Called by MainWindow to share the service from StatsEditorViewModel.
    /// </summary>
    public void SetReferenceGraphService(ReferenceGraphService service)
    {
        _referenceGraphService = service;
        // Refresh backlinks if we already have a selection
        LoadAssetBacklinks();
    }

    /// <summary>
    /// Request navigation to a template in the Stats Editor.
    /// Called when user clicks on an asset backlink.
    /// </summary>
    public void RequestNavigateToTemplate(ReferenceEntry entry)
    {
        NavigateToTemplate?.Invoke(_currentModpackName, entry.SourceTemplateType, entry.SourceInstanceName);
    }

    /// <summary>
    /// Navigate to a specific asset file from another view.
    /// Sets the active modpack, expands the tree, and selects the target node.
    /// </summary>
    public void NavigateToAssetEntry(string modpackName, string assetRelativePath)
    {
        if (string.IsNullOrWhiteSpace(modpackName) || string.IsNullOrWhiteSpace(assetRelativePath))
            return;

        if (!AvailableModpacks.Contains(modpackName))
            RefreshModpacks();

        CurrentModpackName = modpackName;

        if (_allTreeNodes.Count == 0)
            RefreshAssets();

        var normalizedRelativePath = assetRelativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        AssetTreeNode? targetNode = null;
        var assetRoot = AppSettings.GetEffectiveAssetsPath();
        if (!string.IsNullOrEmpty(assetRoot))
        {
            var vanillaPath = Path.GetFullPath(Path.Combine(assetRoot, normalizedRelativePath));
            targetNode = FindNodeByPath(vanillaPath);
        }

        // Staged-only asset (new file not present in extracted vanilla assets).
        if (targetNode == null)
        {
            var stagingPath = _modpackManager.GetStagingAssetPath(modpackName, normalizedRelativePath);
            if (!string.IsNullOrEmpty(stagingPath))
            {
                if (!ShowModpackOnly)
                    ShowModpackOnly = true;
                else
                    ApplySearchFilter();

                targetNode = FindNodeByPath(stagingPath);
            }
        }

        if (targetNode == null)
            return;

        ExpandToNode(targetNode);
        SelectedNode = targetNode;
    }

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

    private int _vanillaImageWidth;
    public int VanillaImageWidth
    {
        get => _vanillaImageWidth;
        set => this.RaiseAndSetIfChanged(ref _vanillaImageWidth, value);
    }

    private int _vanillaImageHeight;
    public int VanillaImageHeight
    {
        get => _vanillaImageHeight;
        set => this.RaiseAndSetIfChanged(ref _vanillaImageHeight, value);
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

    private int _modifiedImageWidth;
    public int ModifiedImageWidth
    {
        get => _modifiedImageWidth;
        set => this.RaiseAndSetIfChanged(ref _modifiedImageWidth, value);
    }

    private int _modifiedImageHeight;
    public int ModifiedImageHeight
    {
        get => _modifiedImageHeight;
        set => this.RaiseAndSetIfChanged(ref _modifiedImageHeight, value);
    }

    // --- GLB preview ---

    private bool _hasGlbPreview;
    public bool HasGlbPreview
    {
        get => _hasGlbPreview;
        set => this.RaiseAndSetIfChanged(ref _hasGlbPreview, value);
    }

    private Avalonia.Media.Imaging.Bitmap? _glbPreviewImage;
    public Avalonia.Media.Imaging.Bitmap? GlbPreviewImage
    {
        get => _glbPreviewImage;
        set => this.RaiseAndSetIfChanged(ref _glbPreviewImage, value);
    }

    private GlbService? _glbService;
    public ObservableCollection<GlbLinkedTexture> GlbLinkedTextures { get; } = new();

    private void LoadGlbPreview()
    {
        HasGlbPreview = false;
        GlbLinkedTextures.Clear();
        GlbPreviewImage = null;

        if (_selectedNode == null || !_selectedNode.IsFile)
            return;

        var ext = Path.GetExtension(_selectedNode.FullPath).ToLowerInvariant();
        if (ext != ".glb")
            return;

        // Initialize GLB service if needed
        if (_glbService == null)
        {
            var extractedPath = AppSettings.GetEffectiveAssetsPath();
            if (extractedPath != null)
                _glbService = new GlbService(extractedPath);
        }

        if (_glbService == null)
            return;

        try
        {
            // Generate 3D preview
            GlbPreviewImage = GlbPreviewRenderer.RenderPreview(_selectedNode.FullPath, 200, 200);

            var linkedTextures = _glbService.GetLinkedTextures(_selectedNode.FullPath);
            foreach (var texture in linkedTextures)
                GlbLinkedTextures.Add(texture);

            HasGlbPreview = linkedTextures.Count > 0 || GlbPreviewImage != null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading GLB preview: {ex.Message}");
        }
    }

    /// <summary>
    /// Export the currently selected GLB with all linked textures embedded.
    /// </summary>
    public async Task<string?> ExportPackagedGlbAsync()
    {
        if (_selectedNode == null || !_selectedNode.IsFile || _glbService == null)
            return null;

        var ext = Path.GetExtension(_selectedNode.FullPath).ToLowerInvariant();
        if (ext != ".glb")
            return null;

        // Create output path in a temp/export location
        var fileName = Path.GetFileNameWithoutExtension(_selectedNode.Name) + "_packaged.glb";
        var outputDir = Path.Combine(Path.GetTempPath(), "MenaceModkit", "exports");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, fileName);

        var success = await Task.Run(() => _glbService.ExportPackaged(_selectedNode.FullPath, outputPath));

        return success ? outputPath : null;
    }

    /// <summary>
    /// Import an edited GLB, extracting its textures back to the Texture2D folder.
    /// </summary>
    public async Task<bool> ImportGlbAsync(string importedGlbPath)
    {
        if (_selectedNode == null || _glbService == null)
            return false;

        return await Task.Run(() => _glbService.ImportAndExtractTextures(importedGlbPath, _selectedNode.FullPath));
    }

    /// <summary>
    /// Navigate to a linked texture in the asset tree.
    /// </summary>
    public void NavigateToLinkedTexture(GlbLinkedTexture texture)
    {
        if (texture.FoundPath == null)
            return;

        // Find the node in the tree that matches this path
        var targetNode = FindNodeByPath(texture.FoundPath);
        if (targetNode != null)
        {
            ExpandToNode(targetNode);
            SelectedNode = targetNode;
        }
    }

    private AssetTreeNode? FindNodeByPath(string fullPath)
    {
        var fromAllNodes = FindNodeByPathInCollection(_allTreeNodes, fullPath);
        if (fromAllNodes != null)
            return fromAllNodes;

        // Filtered/virtual nodes (e.g. modpack-only staged additions) live in FolderTree.
        return FindNodeByPathInCollection(FolderTree, fullPath);
    }

    private static AssetTreeNode? FindNodeByPathInCollection(IEnumerable<AssetTreeNode> nodes, string fullPath)
    {
        foreach (var node in nodes)
        {
            var found = FindNodeByPathRecursive(node, fullPath);
            if (found != null)
                return found;
        }
        return null;
    }

    private static AssetTreeNode? FindNodeByPathRecursive(AssetTreeNode node, string fullPath)
    {
        if (node.IsFile && PathsEqual(node.FullPath, fullPath))
            return node;

        foreach (var child in node.Children)
        {
            var found = FindNodeByPathRecursive(child, fullPath);
            if (found != null)
                return found;
        }
        return null;
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            var leftPath = Path.GetFullPath(left);
            var rightPath = Path.GetFullPath(right);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(leftPath, rightPath, comparison);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }
    }

    private void ExpandToNode(AssetTreeNode node)
    {
        // Walk up the parent chain and expand each node
        var current = node.Parent;
        while (current != null)
        {
            current.IsExpanded = true;
            current = current.Parent;
        }
    }

    // --- Data loading ---

    /// <summary>
    /// Refresh the list of available modpacks from the staging directory.
    /// Call this when the view becomes visible to pick up newly created modpacks.
    /// </summary>
    public void RefreshModpacks()
    {
        var previousSelection = _currentModpackName;
        AvailableModpacks.Clear();
        AvailableModpacks.Add(CreateNewModOption);
        foreach (var mp in _modpackManager.GetStagingModpacks())
            AvailableModpacks.Add(mp.Name);

        // Restore selection if it still exists, otherwise clear it (skip create option)
        if (previousSelection != null && AvailableModpacks.Contains(previousSelection))
            CurrentModpackName = previousSelection;
        else if (AvailableModpacks.Count > 1)
            CurrentModpackName = AvailableModpacks[1];
        else
            CurrentModpackName = null;
    }

    /// <summary>
    /// Create a new modpack and select it in the dropdown.
    /// </summary>
    public void CreateModpack(string name, string? author, string? description)
    {
        var manifest = _modpackManager.CreateModpack(name, author ?? "", description ?? "");
        RefreshModpacks();
        CurrentModpackName = manifest.Name;
    }

    private void LoadModpacks()
    {
        AvailableModpacks.Clear();
        AvailableModpacks.Add(CreateNewModOption);
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
        _topLevelNodes.Clear();
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

        // Filter out excluded folders first, then check for single-child
        var effectiveChildren = rootNode.Children
            .Where(c => c.IsFile || !ExcludedTopLevelFolders.Contains(c.Name))
            .ToList();

        // Descend through single-child folders until we reach a level with multiple children
        while (effectiveChildren.Count == 1 && !effectiveChildren[0].IsFile && effectiveChildren[0].Children.Count > 0)
        {
            effectiveChildren = effectiveChildren[0].Children.ToList();
        }

        // Build top-level list
        _topLevelNodes = new List<AssetTreeNode>();
        foreach (var child in effectiveChildren)
        {
            if (!child.IsFile && ExcludedTopLevelFolders.Contains(child.Name))
                continue;
            child.Parent = null;  // These are now top-level (clear stale parent from skipped folders)
            _topLevelNodes.Add(child);
            FolderTree.Add(child);
        }

        BuildSearchIndex(FolderTree);

        // Build flat list of ALL nodes for expand/collapse operations
        _allTreeNodes = FlattenTree(FolderTree);

        PopulateSectionFilters();
    }

    /// <summary>
    /// Flattens the tree into a list of all nodes (including all descendants).
    /// </summary>
    private static List<AssetTreeNode> FlattenTree(IEnumerable<AssetTreeNode> roots)
    {
        var result = new List<AssetTreeNode>();

        void Flatten(AssetTreeNode node)
        {
            result.Add(node);
            foreach (var child in node.Children)
                Flatten(child);
        }

        foreach (var root in roots)
            Flatten(root);

        return result;
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
                childNode.Parent = node;
                node.Children.Add(childNode);
            }

            // Add files as children (after folders)
            var files = Directory.GetFiles(folderPath)
                .Where(f => !f.EndsWith(".meta"))
                .OrderBy(f => Path.GetFileName(f));

            foreach (var file in files)
            {
                var fileNode = new AssetTreeNode
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    IsFile = true,
                    FileType = GetFileType(file),
                    Size = new FileInfo(file).Length,
                    Parent = node
                };
                node.Children.Add(fileNode);
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

    private void LoadAssetBacklinks()
    {
        AssetBacklinks.Clear();

        if (_selectedNode == null || !_selectedNode.IsFile || _referenceGraphService == null)
            return;

        // Get the asset name (without path and extension for matching)
        var assetName = Path.GetFileNameWithoutExtension(_selectedNode.Name);
        var backlinks = _referenceGraphService.GetAssetBacklinks(assetName);
        foreach (var entry in backlinks)
            AssetBacklinks.Add(entry);
    }

    private void LoadAssetPreview()
    {
        HasImagePreview = false;
        HasTextPreview = false;
        PreviewImage = null;
        PreviewText = string.Empty;
        VanillaImageWidth = 0;
        VanillaImageHeight = 0;

        // Also load backlinks and GLB preview
        LoadAssetBacklinks();
        LoadGlbPreview();

        if (_selectedNode == null || !_selectedNode.IsFile)
            return;

        var ext = Path.GetExtension(_selectedNode.FullPath).ToLowerInvariant();

        if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp")
        {
            try
            {
                PreviewImage = new Bitmap(_selectedNode.FullPath);
                HasImagePreview = true;
                VanillaImageWidth = PreviewImage.PixelSize.Width;
                VanillaImageHeight = PreviewImage.PixelSize.Height;
                PreviewText = $"{_selectedNode.Name}\n{FormatFileSize(_selectedNode.Size)}\n{VanillaImageWidth}x{VanillaImageHeight}";
            }
            catch (Exception ex)
            {
                PreviewText = $"Error loading image: {ex.Message}";
                HasTextPreview = true;
            }
        }
        else if (ext is ".glb")
        {
            // GLB files show material/texture info
            var materialCount = GlbLinkedTextures.Select(t => t.MaterialName).Distinct().Count();
            var embeddedCount = GlbLinkedTextures.Count(t => t.IsEmbedded);
            var foundCount = GlbLinkedTextures.Count(t => !t.IsEmbedded && t.IsFound);
            var missingCount = GlbLinkedTextures.Count(t => !t.IsEmbedded && !t.IsFound);

            PreviewText = $"{_selectedNode.Name}\n{FormatFileSize(_selectedNode.Size)}\n\n" +
                          $"Materials: {materialCount}\n" +
                          $"Textures: {embeddedCount} embedded, {foundCount} linked, {missingCount} missing";
            HasTextPreview = true;
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
        ModifiedImageWidth = 0;
        ModifiedImageHeight = 0;

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
                ModifiedImageWidth = ModifiedPreviewImage.PixelSize.Width;
                ModifiedImageHeight = ModifiedPreviewImage.PixelSize.Height;
                ModifiedPreviewText = $"Replacement: {Path.GetFileName(stagingPath)}\n{FormatFileSize(new FileInfo(stagingPath).Length)}\n{ModifiedImageWidth}x{ModifiedImageHeight}";
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
        if (assetPath != null)
        {
            var fullAssetPath = Path.GetFullPath(assetPath);
            var fullCandidate = Path.GetFullPath(fullPath);
            if (fullCandidate.StartsWith(fullAssetPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                return Path.GetRelativePath(assetPath, fullPath);
        }

        if (string.IsNullOrEmpty(_currentModpackName))
            return null;

        // Fallback for staged-only assets that don't exist in extracted vanilla tree.
        foreach (var relativePath in _modpackAssetPaths)
        {
            var stagingPath = _modpackManager.GetStagingAssetPath(_currentModpackName, relativePath);
            if (!string.IsNullOrEmpty(stagingPath) && PathsEqual(stagingPath, fullPath))
                return relativePath;
        }

        return null;
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

    /// <summary>
    /// Add a new asset file to the selected folder within the current modpack.
    /// Supports both vanilla folders and virtual "Modpack New Assets" folders.
    /// </summary>
    public bool AddAssetToModpackFolder(string sourceFilePath, AssetTreeNode targetFolderNode)
    {
        if (targetFolderNode == null || targetFolderNode.IsFile || string.IsNullOrEmpty(_currentModpackName))
            return false;

        try
        {
            var folderRelativePath = GetAssetRelativeFolderPath(targetFolderNode);
            if (folderRelativePath == null)
            {
                SaveStatus = "Add failed: selected folder is not a valid asset target.";
                return false;
            }

            var fileName = Path.GetFileName(sourceFilePath);
            var relativePath = string.IsNullOrEmpty(folderRelativePath)
                ? fileName
                : Path.Combine(folderRelativePath, fileName);

            _modpackManager.SaveStagingAsset(_currentModpackName, relativePath, sourceFilePath);
            _modpackAssetPaths.Add(relativePath);

            // Rebuild and re-focus so newly added staged-only assets appear immediately.
            RefreshAssets();
            if (_showModpackOnly)
                ApplySearchFilter();

            NavigateToAssetEntry(_currentModpackName, relativePath);
            SaveStatus = $"Added: {relativePath}";
            return true;
        }
        catch (Exception ex)
        {
            SaveStatus = $"Add failed: {ex.Message}";
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

    private string? GetAssetRelativeFolderPath(AssetTreeNode folderNode)
    {
        if (folderNode == null || folderNode.IsFile)
            return null;

        var assetPath = AppSettings.GetEffectiveAssetsPath();
        if (!string.IsNullOrEmpty(assetPath))
        {
            var fullAssetPath = Path.GetFullPath(assetPath);
            var fullFolderPath = Path.GetFullPath(folderNode.FullPath);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (fullFolderPath.StartsWith(fullAssetPath, comparison))
                return Path.GetRelativePath(assetPath, fullFolderPath);
        }

        // Virtual modpack-only tree: reconstruct path from node ancestry.
        var segments = new List<string>();
        var current = folderNode;
        while (current != null)
        {
            if (string.Equals(current.Name, ModpackNewAssetsSectionName, StringComparison.OrdinalIgnoreCase))
                break;

            segments.Add(current.Name);
            current = current.Parent;
        }

        if (current == null)
            return null;

        if (segments.Count == 0)
            return string.Empty;

        segments.Reverse();
        return Path.Combine(segments.ToArray());
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
                this.RaisePropertyChanged(nameof(IsSearching));

                // Only generate search results when 3+ characters entered
                if (IsSearching)
                {
                    GenerateSearchResults();
                }
                else
                {
                    SearchResults.Clear();
                }
            }
        }
    }

    /// <summary>
    /// Forces search to execute immediately (called when Enter is pressed).
    /// </summary>
    public void ExecuteSearch()
    {
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            GenerateSearchResults();
        }
    }

    /// <summary>
    /// Called when user clicks on a search result to select it.
    /// </summary>
    public void SelectSearchResult(SearchResultItem item)
    {
        if (item.SourceNode is AssetTreeNode node)
        {
            SelectedNode = node;
        }
    }

    /// <summary>
    /// Called when user double-clicks a search result to select it and exit search mode.
    /// </summary>
    public void SelectAndExitSearch(SearchResultItem item)
    {
        if (item.SourceNode is AssetTreeNode node)
        {
            // Clear search to switch back to tree view
            _searchText = string.Empty;
            this.RaisePropertyChanged(nameof(SearchText));
            this.RaisePropertyChanged(nameof(IsSearching));
            SearchResults.Clear();

            // Defer expansion and selection to give TreeView time to create containers
            // Use Loaded priority which fires after layout is complete
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Expand ancestors first
                ExpandToNode(node);

                // Then set and notify selection after another frame
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _selectedNode = node;
                    this.RaisePropertyChanged(nameof(SelectedNode));
                }, Avalonia.Threading.DispatcherPriority.Background);
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }

    /// <summary>
    /// Expands tree to show and focus the currently selected item.
    /// </summary>
    public void FocusSelectedInTree()
    {
        if (_selectedNode != null)
        {
            ExpandToNode(_selectedNode);
        }
    }

    /// <summary>
    /// Populates the section filter dropdown based on top-level folders.
    /// </summary>
    private void PopulateSectionFilters()
    {
        SectionFilters.Clear();
        SectionFilters.Add("All Sections");

        // Use _topLevelNodes which contains the top-level asset categories (AudioClip, Mesh, etc.)
        foreach (var node in _topLevelNodes.Where(n => !n.IsFile).OrderBy(n => n.Name))
        {
            SectionFilters.Add(node.Name);
        }
    }

    private void GenerateSearchResults()
    {
        SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(_searchText)) return;

        var searchLower = _searchText.ToLowerInvariant();
        var results = new List<SearchResultItem>();
        var sectionFilter = _selectedSectionFilter;
        var filterBySection = !string.IsNullOrEmpty(sectionFilter) && sectionFilter != "All Sections";

        void SearchNode(AssetTreeNode node, string parentPath, string topLevelFolder)
        {
            var currentPath = string.IsNullOrEmpty(parentPath)
                ? node.Name
                : $"{parentPath} / {node.Name}";

            if (node.IsFile)
            {
                // Apply section filter
                if (filterBySection && !topLevelFolder.Equals(sectionFilter, StringComparison.OrdinalIgnoreCase))
                    return;

                var score = ScoreMatch(node, _searchText);
                if (score > 0)
                {
                    results.Add(new SearchResultItem
                    {
                        Breadcrumb = parentPath,
                        Name = node.Name,
                        Snippet = node.FileType ?? "",
                        Score = score,
                        SourceNode = node,
                        TypeIndicator = Path.GetExtension(node.Name)
                    });
                }
            }
            else
            {
                foreach (var child in node.Children)
                    SearchNode(child, currentPath, topLevelFolder);
            }
        }

        // Search from top-level nodes (not flat list)
        foreach (var root in _topLevelNodes)
            SearchNode(root, "", root.Name);

        ApplySearchSort(results);
    }

    private void ApplySearchSort(List<SearchResultItem>? results = null)
    {
        results ??= SearchResults.ToList();

        var sorted = CurrentSortOption switch
        {
            SearchPanelBuilder.SortOption.NameAsc => results.OrderBy(r => r.Name),
            SearchPanelBuilder.SortOption.NameDesc => results.OrderByDescending(r => r.Name),
            SearchPanelBuilder.SortOption.PathAsc => results.OrderBy(r => r.Breadcrumb),
            SearchPanelBuilder.SortOption.PathDesc => results.OrderByDescending(r => r.Breadcrumb),
            _ => results.OrderByDescending(r => r.Score)
        };

        SearchResults.Clear();
        foreach (var item in sorted)
            SearchResults.Add(item);
    }

    private void ApplySearchFilter()
    {
        FolderTree.Clear();

        var hasQuery = !string.IsNullOrWhiteSpace(_searchText);
        var query = hasQuery ? _searchText.Trim() : null;

        if (!hasQuery && !_showModpackOnly)
        {
            // Restore original top-level structure
            foreach (var node in _topLevelNodes)
                FolderTree.Add(node);
            return;
        }

        var scores = new Dictionary<AssetTreeNode, int>();

        // Filter from top-level nodes (preserves tree structure)
        foreach (var node in _topLevelNodes)
        {
            var filtered = FilterNode(node, query, scores);
            if (filtered != null)
                FolderTree.Add(filtered);
        }

        if (_showModpackOnly && !string.IsNullOrEmpty(_currentModpackName))
        {
            AddMissingModpackAssetNodes(query, scores);
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

            // Auto-expand filtered results only when actively filtering
            ExpandAllInCollection(FolderTree);
        }
    }

    private static void ExpandAllInCollection(IEnumerable<AssetTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (!node.IsFile)
            {
                node.IsExpanded = true;
                ExpandAllInCollection(node.Children);
            }
        }
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
        {
            // Mark for expansion so the matching folder and its contents are visible
            node.IsExpanded = true;
            return node;
        }

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

        // If all children match, return original node (expanded for visibility)
        if (matchingChildren.Count == node.Children.Count)
        {
            node.IsExpanded = true;
            return node;
        }

        // Create a filtered copy with only matching children, pre-expanded
        var copy = new AssetTreeNode
        {
            Name = node.Name,
            FullPath = node.FullPath,
            IsFile = false,
            IsExpanded = true
        };
        foreach (var child in matchingChildren)
        {
            child.Parent = copy;
            copy.Children.Add(child);
        }

        return copy;
    }

    private void AddMissingModpackAssetNodes(string? query, Dictionary<AssetTreeNode, int> scores)
    {
        if (string.IsNullOrEmpty(_currentModpackName) || _modpackAssetPaths.Count == 0)
            return;

        var assetRoot = AppSettings.GetEffectiveAssetsPath();
        var additionsRoot = new AssetTreeNode
        {
            Name = ModpackNewAssetsSectionName,
            FullPath = $"modpack://{_currentModpackName}/new-assets",
            IsFile = false,
            IsExpanded = true
        };

        foreach (var relativePath in _modpackAssetPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var stagingPath = _modpackManager.GetStagingAssetPath(_currentModpackName, relativePath);
            if (string.IsNullOrEmpty(stagingPath) || !File.Exists(stagingPath))
                continue;

            var normalized = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            // Only include assets that don't exist in extracted vanilla assets.
            if (!string.IsNullOrEmpty(assetRoot))
            {
                var vanillaPath = Path.GetFullPath(Path.Combine(assetRoot, normalized));
                if (File.Exists(vanillaPath))
                    continue;
            }

            var fileName = Path.GetFileName(normalized);
            if (!string.IsNullOrEmpty(query) &&
                !fileName.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !normalized.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddVirtualAssetNode(additionsRoot, normalized, stagingPath, query, scores);
        }

        if (additionsRoot.Children.Count > 0)
        {
            FolderTree.Add(additionsRoot);
            if (!scores.ContainsKey(additionsRoot))
                scores[additionsRoot] = 10;
        }
    }

    private void AddVirtualAssetNode(
        AssetTreeNode root,
        string relativePath,
        string stagingPath,
        string? query,
        Dictionary<AssetTreeNode, int> scores)
    {
        var segments = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return;

        var current = root;
        for (int i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            var existing = current.Children.FirstOrDefault(c => !c.IsFile && c.Name.Equals(segment, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                existing = new AssetTreeNode
                {
                    Name = segment,
                    FullPath = Path.Combine(current.FullPath, segment),
                    IsFile = false,
                    IsExpanded = true,
                    Parent = current
                };
                current.Children.Add(existing);
            }
            current = existing;
        }

        var fileNode = new AssetTreeNode
        {
            Name = segments[^1],
            FullPath = stagingPath,
            IsFile = true,
            FileType = GetFileType(stagingPath),
            Size = new FileInfo(stagingPath).Length,
            Parent = current
        };
        current.Children.Add(fileNode);

        // Give missing-assets entries a stable score when query sorting is active.
        var score = 10;
        if (!string.IsNullOrEmpty(query))
        {
            if (fileNode.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                score = 100;
            else if (relativePath.Contains(query, StringComparison.OrdinalIgnoreCase))
                score = 40;
        }

        scores[fileNode] = score;
        if (!scores.TryGetValue(current, out var existingScore) || score > existingScore)
            scores[current] = score;
        if (!scores.TryGetValue(root, out var rootScore) || score > rootScore)
            scores[root] = score;
    }

    public void ExpandAll()
    {
        // Use the flat list to expand all folders at once
        foreach (var node in _allTreeNodes.Where(n => !n.IsFile))
        {
            node.IsExpanded = true;
        }
    }

    public void CollapseAll()
    {
        // Use the flat list to collapse all folders at once
        foreach (var node in _allTreeNodes.Where(n => !n.IsFile))
        {
            node.IsExpanded = false;
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
    public AssetTreeNode? Parent { get; set; }
    public ObservableCollection<AssetTreeNode> Children { get; } = new();

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public override string ToString() => Name;
}
