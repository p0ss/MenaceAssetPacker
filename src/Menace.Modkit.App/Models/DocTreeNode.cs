using System.Collections.ObjectModel;
using ReactiveUI;
using Menace.Modkit.App.ViewModels;

namespace Menace.Modkit.App.Models;

/// <summary>
/// A node in the documentation tree (folder or markdown file).
/// </summary>
public class DocTreeNode : ViewModelBase
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public bool IsFile { get; set; }
    public bool IsDirectory => !IsFile;
    public ObservableCollection<DocTreeNode> Children { get; } = new();

    private bool _isExpanded = true; // Folders start expanded
    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public override string ToString() => Name;
}
