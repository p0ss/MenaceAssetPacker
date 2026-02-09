using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.ViewModels;

/// <summary>
/// ViewModel for the Code tab: browse vanilla decompiled code (read-only)
/// and edit per-modpack source files.
/// </summary>
public sealed class CodeEditorViewModel : ViewModelBase
{
    private readonly ModpackManager _modpackManager;
    private readonly VanillaCodeService _vanillaCodeService;
    private readonly CompilationService _compilationService;

    // Master copies of trees for filtering
    private List<CodeTreeNode> _allVanillaCodeNodes = new();
    private List<CodeTreeNode> _allModSourceNodes = new();

    public CodeEditorViewModel()
    {
        _modpackManager = new ModpackManager();
        _vanillaCodeService = new VanillaCodeService();
        _compilationService = new CompilationService();
        VanillaCodeTree = new ObservableCollection<CodeTreeNode>();
        ModSourceTree = new ObservableCollection<CodeTreeNode>();
        AvailableModpacks = new ObservableCollection<string>();

        LoadModpacks();
        LoadVanillaTree();
    }

    internal ModpackManager ModpackManager => _modpackManager;

    public ObservableCollection<CodeTreeNode> VanillaCodeTree { get; }
    public ObservableCollection<CodeTreeNode> ModSourceTree { get; }
    public ObservableCollection<string> AvailableModpacks { get; }

    // ---------------------------------------------------------------
    // Selected modpack
    // ---------------------------------------------------------------

    private string? _selectedModpack;
    public string? SelectedModpack
    {
        get => _selectedModpack;
        set
        {
            if (_selectedModpack != value)
            {
                this.RaiseAndSetIfChanged(ref _selectedModpack, value);
                LoadModSourceTree();
            }
        }
    }

    // ---------------------------------------------------------------
    // Selected file and content
    // ---------------------------------------------------------------

    private CodeTreeNode? _selectedFile;
    public CodeTreeNode? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (_selectedFile != value)
            {
                this.RaiseAndSetIfChanged(ref _selectedFile, value);
                LoadFileContent();
            }
        }
    }

    private string _fileContent = string.Empty;
    public string FileContent
    {
        get => _fileContent;
        set => this.RaiseAndSetIfChanged(ref _fileContent, value);
    }

    private bool _isReadOnly = true;
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set => this.RaiseAndSetIfChanged(ref _isReadOnly, value);
    }

    private string _currentFilePath = string.Empty;
    public string CurrentFilePath
    {
        get => _currentFilePath;
        set => this.RaiseAndSetIfChanged(ref _currentFilePath, value);
    }

    // ---------------------------------------------------------------
    // Build output (populated by Phase 4 compilation)
    // ---------------------------------------------------------------

    private string _buildOutput = string.Empty;
    public string BuildOutput
    {
        get => _buildOutput;
        set => this.RaiseAndSetIfChanged(ref _buildOutput, value);
    }

    private string _buildStatus = string.Empty;
    public string BuildStatus
    {
        get => _buildStatus;
        set => this.RaiseAndSetIfChanged(ref _buildStatus, value);
    }

    // ---------------------------------------------------------------
    // Search and filtering
    // ---------------------------------------------------------------

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
        var hasQuery = !string.IsNullOrWhiteSpace(_searchText);
        var query = hasQuery ? _searchText.Trim() : null;

        // Filter vanilla code tree
        VanillaCodeTree.Clear();
        if (!hasQuery)
        {
            foreach (var node in _allVanillaCodeNodes)
                VanillaCodeTree.Add(node);
        }
        else
        {
            foreach (var node in _allVanillaCodeNodes)
            {
                var filtered = FilterCodeNode(node, query);
                if (filtered != null)
                    VanillaCodeTree.Add(filtered);
            }
            // Multi-pass expansion to handle TreeView container creation timing
            SetExpansionState(VanillaCodeTree, true);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => SetExpansionState(VanillaCodeTree, true), Avalonia.Threading.DispatcherPriority.Background);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => SetExpansionState(VanillaCodeTree, true), Avalonia.Threading.DispatcherPriority.Background);
        }

        // Filter mod source tree
        ModSourceTree.Clear();
        if (!hasQuery)
        {
            foreach (var node in _allModSourceNodes)
                ModSourceTree.Add(node);
        }
        else
        {
            foreach (var node in _allModSourceNodes)
            {
                var filtered = FilterCodeNode(node, query);
                if (filtered != null)
                    ModSourceTree.Add(filtered);
            }
            // Multi-pass expansion to handle TreeView container creation timing
            SetExpansionState(ModSourceTree, true);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => SetExpansionState(ModSourceTree, true), Avalonia.Threading.DispatcherPriority.Background);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => SetExpansionState(ModSourceTree, true), Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    private CodeTreeNode? FilterCodeNode(CodeTreeNode node, string? query)
    {
        // File node: check if name matches
        if (node.IsFile)
        {
            if (query == null)
                return node;
            if (node.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                return node;
            return null;
        }

        // Folder: check if folder name matches (include all children)
        if (query != null && node.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            node.IsExpanded = true;
            return node;
        }

        // Check children recursively
        var matchingChildren = new List<CodeTreeNode>();
        foreach (var child in node.Children)
        {
            var filtered = FilterCodeNode(child, query);
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

        // Create filtered copy with only matching children
        var copy = new CodeTreeNode
        {
            Name = node.Name,
            FullPath = node.FullPath,
            IsFile = false,
            IsReadOnly = node.IsReadOnly,
            IsExpanded = true
        };
        foreach (var child in matchingChildren)
            copy.Children.Add(child);

        return copy;
    }

    public void ExpandAll()
    {
        // Set expansion state multiple times with UI thread yields to allow
        // TreeView to create containers for newly-visible children
        SetExpansionState(VanillaCodeTree, true);
        SetExpansionState(ModSourceTree, true);
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            SetExpansionState(VanillaCodeTree, true);
            SetExpansionState(ModSourceTree, true);
        }, Avalonia.Threading.DispatcherPriority.Background);
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            SetExpansionState(VanillaCodeTree, true);
            SetExpansionState(ModSourceTree, true);
        }, Avalonia.Threading.DispatcherPriority.Background);
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            SetExpansionState(VanillaCodeTree, true);
            SetExpansionState(ModSourceTree, true);
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    public void CollapseAll()
    {
        SetExpansionState(VanillaCodeTree, false);
        SetExpansionState(ModSourceTree, false);
    }

    private static void SetExpansionState(IEnumerable<CodeTreeNode> nodes, bool expanded)
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

    // ---------------------------------------------------------------
    // Data loading
    // ---------------------------------------------------------------

    private void LoadModpacks()
    {
        AvailableModpacks.Clear();
        var modpacks = _modpackManager.GetStagingModpacks();
        Services.ModkitLog.Info($"[CodeEditorViewModel] Found {modpacks.Count} modpacks");
        foreach (var mp in modpacks)
            AvailableModpacks.Add(mp.Name);

        if (AvailableModpacks.Count > 0 && _selectedModpack == null)
            SelectedModpack = AvailableModpacks[0];
    }

    private void LoadVanillaTree()
    {
        VanillaCodeTree.Clear();
        _allVanillaCodeNodes.Clear();
        var tree = _vanillaCodeService.BuildVanillaCodeTree();
        if (tree != null)
        {
            // Auto-expand the root node so users can see the contents
            tree.IsExpanded = true;
            Services.ModkitLog.Info($"[CodeEditorViewModel] Vanilla tree loaded: {tree.Name} with {tree.Children.Count} children");
            VanillaCodeTree.Add(tree);
            _allVanillaCodeNodes.Add(tree);
        }
        else
        {
            Services.ModkitLog.Info("[CodeEditorViewModel] Vanilla tree is null - no decompiled code found");
        }
    }

    private void LoadModSourceTree()
    {
        ModSourceTree.Clear();
        _allModSourceNodes.Clear();

        if (string.IsNullOrEmpty(_selectedModpack))
        {
            Services.ModkitLog.Info("[CodeEditorViewModel] No modpack selected, skipping mod source tree");
            return;
        }

        var modpacks = _modpackManager.GetStagingModpacks();
        var modpack = modpacks.FirstOrDefault(m => m.Name == _selectedModpack);
        if (modpack == null)
        {
            Services.ModkitLog.Warn($"[CodeEditorViewModel] Modpack '{_selectedModpack}' not found");
            return;
        }

        var tree = VanillaCodeService.BuildModSourceTree(modpack.Path, modpack.Name);
        // Auto-expand the root node
        tree.IsExpanded = true;
        Services.ModkitLog.Info($"[CodeEditorViewModel] Mod source tree loaded: {tree.Name} with {tree.Children.Count} children");
        ModSourceTree.Add(tree);
        _allModSourceNodes.Add(tree);
    }

    private void LoadFileContent()
    {
        Services.ModkitLog.Info($"[CodeEditorViewModel] LoadFileContent: _selectedFile={_selectedFile?.Name}, IsFile={_selectedFile?.IsFile}");

        if (_selectedFile == null || !_selectedFile.IsFile)
        {
            FileContent = "// Select a .cs file from the tree to view its contents";
            IsReadOnly = true;
            CurrentFilePath = string.Empty;
            return;
        }

        CurrentFilePath = _selectedFile.FullPath;
        IsReadOnly = _selectedFile.IsReadOnly;

        try
        {
            var content = File.ReadAllText(_selectedFile.FullPath);
            Services.ModkitLog.Info($"[CodeEditorViewModel] Loaded file: {_selectedFile.FullPath}, length={content.Length}");
            FileContent = content;
        }
        catch (Exception ex)
        {
            Services.ModkitLog.Error($"[CodeEditorViewModel] Error loading file: {ex.Message}");
            FileContent = $"// Error loading file: {ex.Message}";
            IsReadOnly = true;
        }
    }

    // ---------------------------------------------------------------
    // File operations
    // ---------------------------------------------------------------

    public void SaveFile()
    {
        if (string.IsNullOrEmpty(_selectedModpack))
        {
            BuildStatus = "Cannot save: No modpack selected";
            return;
        }

        if (_selectedFile == null)
        {
            BuildStatus = "Cannot save: No file open";
            return;
        }

        if (_selectedFile.IsReadOnly)
        {
            BuildStatus = "Cannot save: File is read-only";
            return;
        }

        try
        {
            File.WriteAllText(_selectedFile.FullPath, FileContent);

            // Also sync the manifest's source list
            var relativePath = GetModRelativePath(_selectedFile.FullPath);
            if (relativePath != null)
                _modpackManager.SaveStagingSource(_selectedModpack, relativePath, FileContent);

            BuildStatus = $"Saved: {_selectedFile.Name}";
        }
        catch (Exception ex)
        {
            BuildStatus = $"Save failed: {ex.Message}";
        }
    }

    public void AddFile(string fileName)
    {
        if (string.IsNullOrEmpty(_selectedModpack))
        {
            BuildStatus = "Cannot add file: No modpack selected";
            return;
        }

        if (string.IsNullOrEmpty(fileName))
        {
            BuildStatus = "Cannot add file: No filename provided";
            return;
        }

        if (!fileName.EndsWith(".cs"))
            fileName += ".cs";

        var relativePath = Path.Combine("src", fileName);
        _modpackManager.AddStagingSource(_selectedModpack, relativePath);
        LoadModSourceTree();
        BuildStatus = $"Added: {fileName}";
    }

    public void RemoveFile()
    {
        if (_selectedFile == null || _selectedFile.IsReadOnly || string.IsNullOrEmpty(_selectedModpack))
            return;

        var relativePath = GetModRelativePath(_selectedFile.FullPath);
        if (relativePath == null) return;

        _modpackManager.RemoveStagingSource(_selectedModpack, relativePath);
        FileContent = string.Empty;
        SelectedFile = null;
        LoadModSourceTree();
        BuildStatus = "File removed";
    }

    /// <summary>
    /// Compile the selected modpack's source code using Roslyn.
    /// </summary>
    public async Task BuildModpackAsync()
    {
        if (string.IsNullOrEmpty(_selectedModpack))
        {
            BuildStatus = "No modpack selected";
            return;
        }

        var modpacks = _modpackManager.GetStagingModpacks();
        var manifest = modpacks.FirstOrDefault(m => m.Name == _selectedModpack);
        if (manifest == null)
        {
            BuildStatus = "Modpack not found";
            return;
        }

        if (!manifest.Code.HasAnySources)
        {
            BuildStatus = "No source files to compile";
            return;
        }

        BuildStatus = "Compiling...";
        BuildOutput = string.Empty;

        var result = await _compilationService.CompileModpackAsync(manifest);

        // Format output
        var sb = new StringBuilder();

        if (result.SecurityWarnings.Count > 0)
        {
            sb.AppendLine("=== Security Scan ===");
            foreach (var warning in result.SecurityWarnings)
                sb.AppendLine(warning.ToString());
            sb.AppendLine();
        }

        if (result.Diagnostics.Count > 0)
        {
            sb.AppendLine("=== Compilation ===");
            foreach (var diag in result.Diagnostics)
                sb.AppendLine(diag.ToString());
            sb.AppendLine();
        }

        if (result.Success)
        {
            sb.AppendLine($"Build succeeded: {result.OutputDllPath}");

            // Update security status based on scan results
            if (result.SecurityWarnings.Count == 0)
            {
                manifest.SecurityStatus = SecurityStatus.SourceVerified;
                BuildStatus = "Build succeeded - Source Verified";
            }
            else
            {
                manifest.SecurityStatus = SecurityStatus.SourceWithWarnings;
                BuildStatus = $"Build succeeded - {result.SecurityWarnings.Count} security warning(s)";
            }
            manifest.SaveToFile();
        }
        else
        {
            var errorCount = result.Diagnostics.Count(d => d.Severity == Models.DiagnosticSeverity.Error);
            BuildStatus = $"Build failed - {errorCount} error(s)";
        }

        BuildOutput = sb.ToString();
    }

    public void RefreshAll()
    {
        LoadModpacks();
        LoadVanillaTree();
        LoadModSourceTree();
    }

    private string? GetModRelativePath(string fullPath)
    {
        if (string.IsNullOrEmpty(_selectedModpack)) return null;
        var modpacks = _modpackManager.GetStagingModpacks();
        var modpack = modpacks.FirstOrDefault(m => m.Name == _selectedModpack);
        if (modpack == null) return null;

        try
        {
            return Path.GetRelativePath(modpack.Path, fullPath);
        }
        catch
        {
            return null;
        }
    }
}
