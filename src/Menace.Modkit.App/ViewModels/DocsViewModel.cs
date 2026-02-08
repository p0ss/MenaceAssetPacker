using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using ReactiveUI;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.ViewModels;

/// <summary>
/// ViewModel for browsing and viewing documentation markdown files.
/// </summary>
public sealed class DocsViewModel : ViewModelBase
{
    private string _docsPath = "";
    private DocTreeNode? _selectedNode;
    private string _markdownContent = "";
    private string _selectedTitle = "";

    public DocsViewModel()
    {
        DocTree = new ObservableCollection<DocTreeNode>();
    }

    public ObservableCollection<DocTreeNode> DocTree { get; }

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
            }
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
    /// Navigate to a document by relative path (for internal links).
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
                targetPath = Path.GetFullPath(Path.Combine(currentDir, relativePath));
            }
            else
            {
                // Resolve relative to docs root
                targetPath = Path.GetFullPath(Path.Combine(_docsPath, relativePath));
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
