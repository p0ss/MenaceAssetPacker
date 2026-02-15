using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using Menace.Modkit.App.Controls;
using Menace.Modkit.App.Extensions;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.ViewModels;

/// <summary>
/// ViewModel for the Code tab: Lua script editor with API reference.
/// Focuses on Lua scripting for modders with an integrated API browser.
/// </summary>
public sealed class CodeEditorViewModel : ViewModelBase, ISearchableViewModel
{
    public const string CreateNewModOption = "+ Create New Mod...";

    private readonly ModpackManager _modpackManager;

    private List<LuaApiItem> _allLuaApiNodes = new();
    private List<CodeTreeNode> _allScriptNodes = new();

    public CodeEditorViewModel()
    {
        _modpackManager = new ModpackManager();
        LuaApiTree = new ObservableCollection<LuaApiItem>();
        ScriptsTree = new ObservableCollection<CodeTreeNode>();
        AvailableModpacks = new ObservableCollection<string>();
        SearchResults = new ObservableCollection<SearchResultItem>();
        ScriptTemplates = new ObservableCollection<string>();

        FileContent = "-- Select a .lua file from the Scripts panel to edit\n-- Or double-click an API item to insert code";
        IsReadOnly = true;

        LoadModpacks();
        LoadLuaApiTree();
        LoadScriptTemplates();
    }

    internal ModpackManager ModpackManager => _modpackManager;

    public ObservableCollection<LuaApiItem> LuaApiTree { get; }
    public ObservableCollection<CodeTreeNode> ScriptsTree { get; }
    public ObservableCollection<string> AvailableModpacks { get; }
    public ObservableCollection<string> ScriptTemplates { get; }

    public event Action<string>? InsertTextRequested;

    /// <summary>
    /// Action to navigate to Lua docs. Set by MainViewModel for cross-tab navigation.
    /// </summary>
    public Action? NavigateToLuaDocs { get; set; }

    // ISearchableViewModel implementation
    public ObservableCollection<SearchResultItem> SearchResults { get; }
    public ObservableCollection<string> SectionFilters { get; } = new() { "All Sections" };

    public bool IsSearching => SearchText.Length >= 3;

    private SearchPanelBuilder.SortOption _currentSortOption = SearchPanelBuilder.SortOption.Relevance;
    public SearchPanelBuilder.SortOption CurrentSortOption
    {
        get => _currentSortOption;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentSortOption, value);
            if (IsSearching) ApplySearchResultsSort();
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

    private string? _selectedModpack;
    public string? SelectedModpack
    {
        get => _selectedModpack;
        set
        {
            if (_selectedModpack != value)
            {
                this.RaiseAndSetIfChanged(ref _selectedModpack, value);
                LoadScriptsTree();
            }
        }
    }

    private string? _selectedTemplate;
    public string? SelectedTemplate
    {
        get => _selectedTemplate;
        set => this.RaiseAndSetIfChanged(ref _selectedTemplate, value);
    }

    private LuaApiItem? _selectedApiItem;
    public LuaApiItem? SelectedApiItem
    {
        get => _selectedApiItem;
        set => this.RaiseAndSetIfChanged(ref _selectedApiItem, value);
    }

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

    private string _buildStatus = string.Empty;
    public string BuildStatus
    {
        get => _buildStatus;
        set => this.RaiseAndSetIfChanged(ref _buildStatus, value);
    }

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

                if (IsSearching)
                    GenerateSearchResults();
                else
                    SearchResults.Clear();
            }
        }
    }

    public void InsertSelectedApiItem()
    {
        if (_selectedApiItem != null && !_selectedApiItem.IsCategory && !string.IsNullOrEmpty(_selectedApiItem.InsertText))
        {
            InsertTextRequested?.Invoke(_selectedApiItem.InsertText);
        }
    }

    public void InsertApiItem(LuaApiItem item)
    {
        if (item != null && !item.IsCategory && !string.IsNullOrEmpty(item.InsertText))
        {
            InsertTextRequested?.Invoke(item.InsertText);
        }
    }

    public void ExecuteSearch()
    {
        if (!string.IsNullOrWhiteSpace(_searchText))
            GenerateSearchResults();
    }

    public void SelectSearchResult(SearchResultItem item)
    {
        if (item.SourceNode is CodeTreeNode node)
            SelectedFile = node;
    }

    public void SelectAndExitSearch(SearchResultItem item)
    {
        if (item.SourceNode is CodeTreeNode node)
        {
            _searchText = string.Empty;
            this.RaisePropertyChanged(nameof(SearchText));
            this.RaisePropertyChanged(nameof(IsSearching));
            SearchResults.Clear();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _selectedFile = node;
                this.RaisePropertyChanged(nameof(SelectedFile));
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    public void FocusSelectedInTree() { }

    private void PopulateSectionFilters()
    {
        SectionFilters.Clear();
        SectionFilters.Add("All Sections");
        SectionFilters.Add("Scripts");
        SectionFilters.Add("API Reference");
    }

    private void GenerateSearchResults()
    {
        SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(_searchText)) return;

        var results = new List<SearchResultItem>();
        var sectionFilter = _selectedSectionFilter;
        var filterBySection = !string.IsNullOrEmpty(sectionFilter) && sectionFilter != "All Sections";
        var searchLower = _searchText.ToLowerInvariant();

        void SearchScriptNode(CodeTreeNode node, string parentPath)
        {
            var currentPath = string.IsNullOrEmpty(parentPath) ? node.Name : $"{parentPath} / {node.Name}";

            if (node.IsFile)
            {
                if (filterBySection && sectionFilter == "API Reference") return;

                var nameLower = node.Name.ToLowerInvariant();
                if (nameLower.Contains(searchLower))
                {
                    var snippet = GetFileSnippet(node.FullPath);
                    results.Add(new SearchResultItem
                    {
                        Breadcrumb = "[Script] " + parentPath,
                        Name = node.Name,
                        Snippet = snippet,
                        Score = nameLower.StartsWith(searchLower) ? 100 : 50,
                        SourceNode = node,
                        TypeIndicator = ".lua"
                    });
                }
            }
            else
            {
                foreach (var child in node.Children)
                    SearchScriptNode(child, currentPath);
            }
        }

        void SearchApiNode(LuaApiItem item, string parentPath)
        {
            if (filterBySection && sectionFilter == "Scripts") return;

            var currentPath = string.IsNullOrEmpty(parentPath) ? item.Name : $"{parentPath} / {item.Name}";
            var nameLower = item.Name.ToLowerInvariant();
            var descLower = item.Description.ToLowerInvariant();

            if (nameLower.Contains(searchLower) || descLower.Contains(searchLower))
            {
                var typeIndicator = item.ItemType == LuaApiItemType.Event ? "event" :
                                   item.ItemType == LuaApiItemType.Function ? "func" : "cat";
                results.Add(new SearchResultItem
                {
                    Breadcrumb = "[API] " + parentPath,
                    Name = item.Name,
                    Snippet = item.Description,
                    Score = nameLower.StartsWith(searchLower) ? 100 : (nameLower.Contains(searchLower) ? 75 : 50),
                    SourceNode = item,
                    TypeIndicator = typeIndicator
                });
            }

            foreach (var child in item.Children)
                SearchApiNode(child, currentPath);
        }

        foreach (var root in _allScriptNodes)
            SearchScriptNode(root, "");

        foreach (var root in _allLuaApiNodes)
            SearchApiNode(root, "");

        ApplySearchResultsSort(results);
    }

    private string GetFileSnippet(string path)
    {
        try
        {
            if (!File.Exists(path)) return "";

            using var reader = new StreamReader(path);
            var lines = new List<string>();
            for (int i = 0; i < 3 && !reader.EndOfStream; i++)
            {
                var line = reader.ReadLine()?.Trim();
                if (!string.IsNullOrEmpty(line) && !line.StartsWith("--"))
                    lines.Add(line);
            }
            return string.Join(" | ", lines).Truncate(120);
        }
        catch
        {
            return "";
        }
    }

    private void ApplySearchResultsSort(List<SearchResultItem>? results = null)
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

    public void ExpandAll()
    {
        SetExpansionState(ScriptsTree, true);
        SetLuaApiExpansionState(LuaApiTree, true);
    }

    public void CollapseAll()
    {
        SetExpansionState(ScriptsTree, false);
        SetLuaApiExpansionState(LuaApiTree, false);
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

    private static void SetLuaApiExpansionState(IEnumerable<LuaApiItem> items, bool expanded)
    {
        foreach (var item in items)
        {
            item.IsExpanded = expanded;
            SetLuaApiExpansionState(item.Children, expanded);
        }
    }

    private void LoadModpacks()
    {
        AvailableModpacks.Clear();
        AvailableModpacks.Add(CreateNewModOption);
        var modpacks = _modpackManager.GetStagingModpacks();
        foreach (var mp in modpacks)
            AvailableModpacks.Add(mp.Name);

        if (AvailableModpacks.Count > 1 && _selectedModpack == null)
            SelectedModpack = AvailableModpacks[1];
    }

    private void LoadLuaApiTree()
    {
        LuaApiTree.Clear();
        _allLuaApiNodes.Clear();

        var apiTree = LuaApiReference.GetApiTree();
        foreach (var item in apiTree)
        {
            LuaApiTree.Add(item);
            _allLuaApiNodes.Add(item);
        }

        PopulateSectionFilters();
    }

    private void LoadScriptTemplates()
    {
        ScriptTemplates.Clear();
        foreach (var (name, _, _) in LuaApiReference.GetScriptTemplates())
            ScriptTemplates.Add(name);
    }

    private void LoadScriptsTree()
    {
        ScriptsTree.Clear();
        _allScriptNodes.Clear();

        if (string.IsNullOrEmpty(_selectedModpack))
            return;

        var modpacks = _modpackManager.GetStagingModpacks();
        var modpack = modpacks.FirstOrDefault(m => m.Name == _selectedModpack);
        if (modpack == null)
            return;

        var scriptsDir = Path.Combine(modpack.Path, "scripts");
        if (!Directory.Exists(scriptsDir))
        {
            try { Directory.CreateDirectory(scriptsDir); }
            catch { return; }
        }

        var tree = BuildScriptsTree(scriptsDir, modpack.Name);
        foreach (var child in tree.Children)
        {
            child.IsExpanded = true;
            ScriptsTree.Add(child);
            _allScriptNodes.Add(child);
        }
    }

    private static CodeTreeNode BuildScriptsTree(string scriptsDir, string modpackName)
    {
        var root = new CodeTreeNode
        {
            Name = modpackName,
            FullPath = scriptsDir,
            IsFile = false,
            IsReadOnly = false,
            IsExpanded = true
        };

        if (Directory.Exists(scriptsDir))
        {
            foreach (var file in Directory.GetFiles(scriptsDir, "*.lua", SearchOption.AllDirectories).OrderBy(f => f))
            {
                var relativePath = Path.GetRelativePath(scriptsDir, file);
                var parts = relativePath.Split(Path.DirectorySeparatorChar);

                var current = root;
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    var existing = current.Children.FirstOrDefault(c => c.Name == parts[i] && !c.IsFile);
                    if (existing == null)
                    {
                        existing = new CodeTreeNode
                        {
                            Name = parts[i],
                            FullPath = Path.Combine(current.FullPath, parts[i]),
                            IsFile = false,
                            IsReadOnly = false,
                            IsExpanded = true
                        };
                        current.Children.Add(existing);
                    }
                    current = existing;
                }

                current.Children.Add(new CodeTreeNode
                {
                    Name = parts[^1],
                    FullPath = file,
                    IsFile = true,
                    IsReadOnly = false
                });
            }
        }

        return root;
    }

    private void LoadFileContent()
    {
        if (_selectedFile == null || !_selectedFile.IsFile)
        {
            FileContent = "-- Select a .lua file from the Scripts panel to edit\n-- Or double-click an API item to insert code";
            IsReadOnly = true;
            CurrentFilePath = string.Empty;
            return;
        }

        CurrentFilePath = _selectedFile.FullPath;
        IsReadOnly = _selectedFile.IsReadOnly;

        try
        {
            FileContent = File.ReadAllText(_selectedFile.FullPath);
        }
        catch (Exception ex)
        {
            FileContent = $"-- Error loading file: {ex.Message}";
            IsReadOnly = true;
        }
    }

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

        if (!fileName.EndsWith(".lua"))
            fileName += ".lua";

        var modpacks = _modpackManager.GetStagingModpacks();
        var modpack = modpacks.FirstOrDefault(m => m.Name == _selectedModpack);
        if (modpack == null)
        {
            BuildStatus = "Cannot add file: Modpack not found";
            return;
        }

        var scriptsDir = Path.Combine(modpack.Path, "scripts");
        Directory.CreateDirectory(scriptsDir);

        var fullPath = Path.Combine(scriptsDir, fileName);
        if (File.Exists(fullPath))
        {
            BuildStatus = $"File already exists: {fileName}";
            return;
        }

        var content = GetTemplateContent();
        File.WriteAllText(fullPath, content);

        LoadScriptsTree();
        BuildStatus = $"Added: {fileName}";
    }

    private string GetTemplateContent()
    {
        if (string.IsNullOrEmpty(_selectedTemplate))
            return "-- New Lua Script\nlog(\"Script loaded!\")\n";

        var templates = LuaApiReference.GetScriptTemplates();
        var template = templates.FirstOrDefault(t => t.Name == _selectedTemplate);
        return template.Content ?? "-- New Lua Script\nlog(\"Script loaded!\")\n";
    }

    public void RemoveFile()
    {
        if (_selectedFile == null || _selectedFile.IsReadOnly || string.IsNullOrEmpty(_selectedModpack))
            return;

        try
        {
            if (File.Exists(_selectedFile.FullPath))
                File.Delete(_selectedFile.FullPath);

            FileContent = "-- Select a .lua file from the Scripts panel to edit";
            SelectedFile = null;
            LoadScriptsTree();
            BuildStatus = "File removed";
        }
        catch (Exception ex)
        {
            BuildStatus = $"Failed to remove file: {ex.Message}";
        }
    }

    public void RefreshAll()
    {
        LoadModpacks();
        LoadLuaApiTree();
        LoadScriptsTree();
    }

    public void CreateModpack(string name, string? author, string? description)
    {
        var manifest = _modpackManager.CreateModpack(name, author ?? "", description ?? "");
        LoadModpacks();
        SelectedModpack = manifest.Name;
    }
}
