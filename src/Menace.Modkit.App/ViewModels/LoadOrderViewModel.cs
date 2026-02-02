using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.ViewModels;

/// <summary>
/// ViewModel for the load order and conflict resolution panel.
/// </summary>
public sealed class LoadOrderViewModel : ViewModelBase
{
    private readonly ModpackManager _modpackManager;
    private readonly ConflictDetector _conflictDetector;

    public LoadOrderViewModel(ModpackManager modpackManager)
    {
        _modpackManager = modpackManager;
        _conflictDetector = new ConflictDetector();
        OrderedModpacks = new ObservableCollection<LoadOrderItemViewModel>();
        FieldConflicts = new ObservableCollection<FieldConflict>();
        DllConflicts = new ObservableCollection<DllConflict>();
        DependencyIssues = new ObservableCollection<DependencyIssue>();
    }

    public ObservableCollection<LoadOrderItemViewModel> OrderedModpacks { get; }
    public ObservableCollection<FieldConflict> FieldConflicts { get; }
    public ObservableCollection<DllConflict> DllConflicts { get; }
    public ObservableCollection<DependencyIssue> DependencyIssues { get; }

    public bool HasConflicts => FieldConflicts.Count > 0 || DllConflicts.Count > 0;
    public bool HasDependencyIssues => DependencyIssues.Count > 0;
    public int TotalIssueCount => FieldConflicts.Count + DllConflicts.Count + DependencyIssues.Count;

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    /// <summary>
    /// Reload the modpack list and re-run conflict detection.
    /// </summary>
    public void Refresh()
    {
        var modpacks = _modpackManager.GetStagingModpacks()
            .OrderBy(m => m.LoadOrder)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        OrderedModpacks.Clear();
        foreach (var m in modpacks)
            OrderedModpacks.Add(new LoadOrderItemViewModel(m));

        RunConflictDetection(modpacks);
    }

    /// <summary>
    /// Move a modpack up in load order (lower number = loaded earlier).
    /// </summary>
    public void MoveUp(LoadOrderItemViewModel item)
    {
        var index = OrderedModpacks.IndexOf(item);
        if (index <= 0) return;

        OrderedModpacks.Move(index, index - 1);
        ReassignLoadOrders();
    }

    /// <summary>
    /// Move a modpack down in load order (higher number = loaded later, wins conflicts).
    /// </summary>
    public void MoveDown(LoadOrderItemViewModel item)
    {
        var index = OrderedModpacks.IndexOf(item);
        if (index < 0 || index >= OrderedModpacks.Count - 1) return;

        OrderedModpacks.Move(index, index + 1);
        ReassignLoadOrders();
    }

    private void ReassignLoadOrders()
    {
        var ordering = new List<(string, int)>();
        for (int i = 0; i < OrderedModpacks.Count; i++)
        {
            var order = (i + 1) * 10; // 10, 20, 30, ...
            OrderedModpacks[i].LoadOrder = order;
            ordering.Add((OrderedModpacks[i].Name, order));
        }

        _modpackManager.SaveLoadOrder(ordering);

        // Re-run conflict detection with new ordering
        var modpacks = OrderedModpacks.Select(o => o.Manifest).ToList();
        RunConflictDetection(modpacks);
    }

    private void RunConflictDetection(List<ModpackManifest> modpacks)
    {
        FieldConflicts.Clear();
        DllConflicts.Clear();
        DependencyIssues.Clear();

        var fieldConflicts = _conflictDetector.DetectFieldConflicts(modpacks);
        foreach (var c in fieldConflicts)
            FieldConflicts.Add(c);

        var dllConflicts = _conflictDetector.DetectDllConflicts(modpacks);
        foreach (var c in dllConflicts)
            DllConflicts.Add(c);

        var depIssues = _conflictDetector.DetectDependencyIssues(modpacks);
        foreach (var i in depIssues)
            DependencyIssues.Add(i);

        this.RaisePropertyChanged(nameof(HasConflicts));
        this.RaisePropertyChanged(nameof(HasDependencyIssues));
        this.RaisePropertyChanged(nameof(TotalIssueCount));

        if (TotalIssueCount > 0)
            StatusText = $"{FieldConflicts.Count} field conflict(s), {DllConflicts.Count} DLL conflict(s), {DependencyIssues.Count} dependency issue(s)";
        else
            StatusText = "No conflicts detected";
    }
}

public sealed class LoadOrderItemViewModel : ViewModelBase
{
    internal readonly ModpackManifest Manifest;

    public LoadOrderItemViewModel(ModpackManifest manifest)
    {
        Manifest = manifest;
    }

    public string Name => Manifest.Name;
    public string Version => Manifest.Version;
    public string Author => Manifest.Author;
    public bool HasCode => Manifest.HasCode;
    public bool HasPatches => Manifest.HasPatches;

    public int LoadOrder
    {
        get => Manifest.LoadOrder;
        set
        {
            if (Manifest.LoadOrder != value)
            {
                Manifest.LoadOrder = value;
                this.RaisePropertyChanged();
            }
        }
    }
}
