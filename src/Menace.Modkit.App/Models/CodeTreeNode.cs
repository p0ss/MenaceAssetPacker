using System.Collections.ObjectModel;
using ReactiveUI;
using Menace.Modkit.App.ViewModels;

namespace Menace.Modkit.App.Models;

/// <summary>
/// A node in the code file tree (vanilla decompiled or mod source).
/// </summary>
public class CodeTreeNode : ViewModelBase
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public bool IsFile { get; set; }
    public bool IsDirectory => !IsFile;
    public bool IsReadOnly { get; set; }
    public ObservableCollection<CodeTreeNode> Children { get; } = new();

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public override string ToString() => Name;
}
