using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using ReactiveUI;
using Menace.Modkit.App.Controls;
using Menace.Modkit.App.Extensions;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.ViewModels;

/// <summary>
/// ViewModel for browsing and viewing documentation markdown files.
/// </summary>
public sealed class DocsViewModel : ViewModelBase, ISearchableViewModel
{
    private string _docsPath = "";
    private DocTreeNode? _selectedNode;
    private string _markdownContent = "";
    private string _selectedTitle = "";
    private List<DocTreeNode> _allDocNodes = new();

    public DocsViewModel()
    {
        DocTree = new ObservableCollection<DocTreeNode>();
        SearchResults = new ObservableCollection<SearchResultItem>();
    }

    public ObservableCollection<DocTreeNode> DocTree { get; }

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

    public DocTreeNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedNode, value);
            if (value != null && value.IsFile)
                LoadDocument(value);
        }
    }

    public string MarkdownContent
    {
        get => _markdownContent;
        private set => this.RaiseAndSetIfChanged(ref _markdownContent, value);
    }

    public string SelectedTitle
    {
        get => _selectedTitle;
        private set => this.RaiseAndSetIfChanged(ref _selectedTitle, value);
    }

    public bool HasContent => !string.IsNullOrEmpty(MarkdownContent);

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
                var wasSearching = IsSearching;
                var currentSelection = _selectedNode;

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

                // When clearing search, preserve selection and focus it in tree
                if (wasSearching && !IsSearching && currentSelection != null)
                {
                    FocusSelectedInTree();
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
        if (item.SourceNode is DocTreeNode node)
        {
            SelectedNode = node;
        }
    }

    /// <summary>
    /// Called when user double-clicks a search result to select it and exit search mode.
    /// </summary>
    public void SelectAndExitSearch(SearchResultItem item)
    {
        if (item.SourceNode is DocTreeNode node)
        {
            // Clear search to switch back to tree view (use backing field to skip FocusSelectedInTree in setter)
            _searchText = string.Empty;
            this.RaisePropertyChanged(nameof(SearchText));
            this.RaisePropertyChanged(nameof(IsSearching));
            SearchResults.Clear();

            // Defer expansion and selection to give TreeView time to create containers
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Expand ancestors first
                ExpandToNode(node);

                // Then set and notify selection
                _selectedNode = node;
                this.RaisePropertyChanged(nameof(SelectedNode));
            }, Avalonia.Threading.DispatcherPriority.Background);
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

    private void ExpandToNode(DocTreeNode targetNode)
    {
        void ExpandParentsRecursive(IEnumerable<DocTreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.IsFile) continue;

                bool containsTarget = false;
                void CheckChildren(DocTreeNode n)
                {
                    if (n == targetNode) { containsTarget = true; return; }
                    foreach (var c in n.Children) CheckChildren(c);
                }
                CheckChildren(node);

                if (containsTarget)
                {
                    node.IsExpanded = true;
                    ExpandParentsRecursive(node.Children);
                }
            }
        }
        ExpandParentsRecursive(_allDocNodes);
    }

    /// <summary>
    /// Populates the section filter dropdown based on top-level folders.
    /// </summary>
    private void PopulateSectionFilters()
    {
        SectionFilters.Clear();
        SectionFilters.Add("All Sections");

        foreach (var node in _allDocNodes.Where(n => !n.IsFile).OrderBy(n => n.Name))
        {
            SectionFilters.Add(node.Name);
        }
    }

    private void GenerateSearchResults()
    {
        SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(_searchText)) return;

        var results = new List<SearchResultItem>();
        var searchLower = _searchText.ToLowerInvariant();
        var sectionFilter = _selectedSectionFilter;
        var filterBySection = !string.IsNullOrEmpty(sectionFilter) && sectionFilter != "All Sections";

        void SearchNode(DocTreeNode node, string parentPath, string topLevelFolder)
        {
            var currentPath = string.IsNullOrEmpty(parentPath)
                ? node.Name
                : $"{parentPath} / {node.Name}";

            if (node.IsFile)
            {
                // Apply section filter
                if (filterBySection && !topLevelFolder.Equals(sectionFilter, StringComparison.OrdinalIgnoreCase))
                    return;

                var nameLower = node.Name.ToLowerInvariant();
                if (nameLower.Contains(searchLower))
                {
                    var snippet = GetDocSnippet(node.FullPath);

                    results.Add(new SearchResultItem
                    {
                        Breadcrumb = parentPath,
                        Name = node.Name,
                        Snippet = snippet,
                        Score = nameLower.StartsWith(searchLower) ? 100 : 50,
                        SourceNode = node,
                        TypeIndicator = ".md"
                    });
                }
            }
            else
            {
                foreach (var child in node.Children)
                    SearchNode(child, currentPath, topLevelFolder);
            }
        }

        foreach (var root in _allDocNodes)
            SearchNode(root, "", root.Name);

        ApplySearchResultsSort(results);
    }

    private string GetDocSnippet(string path)
    {
        try
        {
            if (!File.Exists(path)) return "";

            var lines = File.ReadLines(path)
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"))
                .Take(3)
                .Select(l => l.Trim());

            return string.Join(" ", lines).Truncate(120);
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

    private void ApplySearchFilter()
    {
        DocTree.Clear();

        var hasQuery = !string.IsNullOrWhiteSpace(_searchText);
        var query = hasQuery ? _searchText.Trim() : null;

        if (!hasQuery)
        {
            foreach (var node in _allDocNodes)
                DocTree.Add(node);
            return;
        }

        foreach (var node in _allDocNodes)
        {
            var filtered = FilterDocNode(node, query);
            if (filtered != null)
                DocTree.Add(filtered);
        }

        // Multi-pass expansion to handle TreeView container creation timing
        SetExpansionState(DocTree, true);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => SetExpansionState(DocTree, true), Avalonia.Threading.DispatcherPriority.Background);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => SetExpansionState(DocTree, true), Avalonia.Threading.DispatcherPriority.Background);
    }

    private DocTreeNode? FilterDocNode(DocTreeNode node, string? query)
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
        var matchingChildren = new List<DocTreeNode>();
        foreach (var child in node.Children)
        {
            var filtered = FilterDocNode(child, query);
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
        var copy = new DocTreeNode
        {
            Name = node.Name,
            FullPath = node.FullPath,
            RelativePath = node.RelativePath,
            IsFile = false,
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
        SetExpansionState(DocTree, true);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => SetExpansionState(DocTree, true), Avalonia.Threading.DispatcherPriority.Background);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => SetExpansionState(DocTree, true), Avalonia.Threading.DispatcherPriority.Background);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => SetExpansionState(DocTree, true), Avalonia.Threading.DispatcherPriority.Background);
    }

    public void CollapseAll()
    {
        SetExpansionState(DocTree, false);
    }

    private static void SetExpansionState(IEnumerable<DocTreeNode> nodes, bool expanded)
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

    /// <summary>
    /// Initialize with the docs folder path.
    /// </summary>
    public void Initialize(string docsPath)
    {
        _docsPath = docsPath;
        RefreshDocTree();
    }

    public void RefreshDocTree()
    {
        DocTree.Clear();
        _allDocNodes.Clear();

        if (string.IsNullOrEmpty(_docsPath) || !Directory.Exists(_docsPath))
            return;

        try
        {
            // Build tree structure
            var root = BuildTree(_docsPath, "");

            // Add children of root directly to DocTree (don't show root "docs" folder)
            // Order: modding-guides first, then others alphabetically; index first in each folder
            var ordered = root.Children
                .OrderBy(n => n.IsFile) // folders first
                .ThenByDescending(n => n.RelativePath.StartsWith("modding-guides", StringComparison.OrdinalIgnoreCase)) // modding-guides first
                .ThenBy(n => n.Name);

            foreach (var child in ordered)
            {
                DocTree.Add(child);
                _allDocNodes.Add(child);
            }

            PopulateSectionFilters();
        }
        catch (Exception ex)
        {
            Services.ModkitLog.Error($"[DocsViewModel] Failed to load docs: {ex.Message}");
        }
    }

    private DocTreeNode BuildTree(string path, string relativePath)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name))
            name = "docs";

        var node = new DocTreeNode
        {
            Name = FormatFolderName(name),
            FullPath = path,
            RelativePath = relativePath,
            IsFile = false,
            IsExpanded = true
        };

        try
        {
            // Add subdirectories first
            foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d))
            {
                var dirName = Path.GetFileName(dir);
                var childRelPath = string.IsNullOrEmpty(relativePath)
                    ? dirName
                    : Path.Combine(relativePath, dirName);

                var childNode = BuildTree(dir, childRelPath);

                // Only add directories that contain markdown files (directly or in subdirs)
                if (HasMarkdownFiles(dir))
                {
                    node.Children.Add(childNode);
                }
            }

            // Add markdown files (index first, then sorted by name)
            var files = Directory.GetFiles(path, "*.md")
                .OrderByDescending(f => Path.GetFileNameWithoutExtension(f).Equals("index", StringComparison.OrdinalIgnoreCase))
                .ThenBy(f => f);

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                var fileRelPath = string.IsNullOrEmpty(relativePath)
                    ? fileName
                    : Path.Combine(relativePath, fileName);

                node.Children.Add(new DocTreeNode
                {
                    Name = FormatFileName(fileName),
                    FullPath = file,
                    RelativePath = fileRelPath,
                    IsFile = true
                });
            }
        }
        catch (Exception ex)
        {
            Services.ModkitLog.Warn($"[DocsViewModel] Failed to scan {path}: {ex.Message}");
        }

        return node;
    }

    private static bool HasMarkdownFiles(string directory)
    {
        try
        {
            return Directory.GetFiles(directory, "*.md", SearchOption.AllDirectories).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatFolderName(string name)
    {
        // Format folder name: replace dashes/underscores with spaces, title case
        var parts = name.Split('-', '_')
            .Where(w => !string.IsNullOrEmpty(w))
            .Select(w => char.ToUpper(w[0]) + (w.Length > 1 ? w.Substring(1).ToLower() : ""));
        return string.Join(" ", parts);
    }

    private static string FormatFileName(string fileName)
    {
        // Remove .md extension and format
        var name = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrEmpty(name))
            return fileName;

        // Strip leading numbers like "01-" or "02-"
        if (name.Length > 3 && char.IsDigit(name[0]) && char.IsDigit(name[1]) && name[2] == '-')
        {
            name = name.Substring(3);
        }

        var parts = name.Split('-', '_')
            .Where(w => !string.IsNullOrEmpty(w))
            .Select(w => char.ToUpper(w[0]) + (w.Length > 1 ? w.Substring(1).ToLower() : ""));
        return string.Join(" ", parts);
    }

    /// <summary>
    /// Navigate to a document by name pattern (case-insensitive partial match).
    /// Used for cross-tab navigation like "lua-scripting" or "Lua Scripting".
    /// </summary>
    public bool NavigateToDocByName(string namePattern)
    {
        if (string.IsNullOrEmpty(namePattern))
            return false;

        var patternLower = namePattern.ToLowerInvariant().Replace(" ", "").Replace("-", "");

        DocTreeNode? FindMatchingNode(IEnumerable<DocTreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.IsFile)
                {
                    var nameLower = node.Name.ToLowerInvariant().Replace(" ", "").Replace("-", "");
                    var pathLower = node.RelativePath.ToLowerInvariant().Replace(" ", "").Replace("-", "");
                    if (nameLower.Contains(patternLower) || pathLower.Contains(patternLower))
                        return node;
                }
                else
                {
                    var found = FindMatchingNode(node.Children);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }

        var matchingNode = FindMatchingNode(_allDocNodes);
        if (matchingNode != null)
        {
            ExpandToNode(matchingNode);
            SelectedNode = matchingNode;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Navigate to a document by relative path (for internal links).
    /// Validates paths to prevent navigation outside the docs directory.
    /// </summary>
    public void NavigateToRelativePath(string relativePath)
    {
        if (string.IsNullOrEmpty(_docsPath))
            return;

        try
        {
            // Handle relative paths from current document
            string targetPath;

            if (SelectedNode != null && !string.IsNullOrEmpty(SelectedNode.FullPath))
            {
                // Resolve relative to current document's directory
                var currentDir = Path.GetDirectoryName(SelectedNode.FullPath) ?? _docsPath;
                // Validate path stays within docs directory to prevent traversal attacks
                targetPath = Services.PathValidator.ValidatePathWithinBase(_docsPath,
                    Path.GetRelativePath(_docsPath, Path.GetFullPath(Path.Combine(currentDir, relativePath))));
            }
            else
            {
                // Resolve relative to docs root - validate path stays within docs directory
                targetPath = Services.PathValidator.ValidatePathWithinBase(_docsPath, relativePath);
            }

            // Find matching node in tree
            var node = FindNodeByPath(DocTree, targetPath);
            if (node != null)
            {
                SelectedNode = node;
            }
            else
            {
                Services.ModkitLog.Warn($"[DocsViewModel] Could not find document: {relativePath}");
            }
        }
        catch (System.Security.SecurityException ex)
        {
            Services.ModkitLog.Warn($"[DocsViewModel] Path traversal blocked for {relativePath}: {ex.Message}");
        }
        catch (Exception ex)
        {
            Services.ModkitLog.Warn($"[DocsViewModel] Failed to navigate to {relativePath}: {ex.Message}");
        }
    }

    private DocTreeNode? FindNodeByPath(IEnumerable<DocTreeNode> nodes, string fullPath)
    {
        foreach (var node in nodes)
        {
            if (node.FullPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
                return node;

            if (node.Children.Count > 0)
            {
                var found = FindNodeByPath(node.Children, fullPath);
                if (found != null)
                    return found;
            }
        }
        return null;
    }

    private void LoadDocument(DocTreeNode node)
    {
        try
        {
            SelectedTitle = node.Name;

            if (string.IsNullOrEmpty(node.FullPath))
            {
                MarkdownContent = "Invalid document entry.";
                this.RaisePropertyChanged(nameof(HasContent));
                return;
            }

            if (!File.Exists(node.FullPath))
            {
                MarkdownContent = $"File not found: {node.FullPath}";
                this.RaisePropertyChanged(nameof(HasContent));
                return;
            }

            // Read file with explicit encoding to handle various file formats
            MarkdownContent = File.ReadAllText(node.FullPath, System.Text.Encoding.UTF8);
            this.RaisePropertyChanged(nameof(HasContent));
        }
        catch (Exception ex)
        {
            MarkdownContent = $"Error loading document: {ex.Message}";
            Services.ModkitLog.Error($"[DocsViewModel] Failed to load {node.FullPath}: {ex.Message}\n{ex.StackTrace}");
            this.RaisePropertyChanged(nameof(HasContent));
        }
    }
}
