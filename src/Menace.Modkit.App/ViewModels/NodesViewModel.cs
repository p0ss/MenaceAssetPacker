#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Menace.Modkit.App.Services;
using Menace.Modkit.App.VisualEditor;
using ReactiveUI;

namespace Menace.Modkit.App.ViewModels;

/// <summary>
/// ViewModel for the Nodes tab: visual node-based mod editor.
/// </summary>
public sealed class NodesViewModel : ViewModelBase
{
    /// <summary>
    /// Special value in the modpack dropdown that triggers the create mod dialog.
    /// </summary>
    public const string CreateNewModOption = "+ Create New Mod...";

    private readonly SchemaService _schemaService;
    private readonly ModpackManager _modpackManager;
    private string? _selectedModpack;

    public NodesViewModel()
    {
        NodeGraph = new NodeGraphViewModel();
        _schemaService = new SchemaService();
        _modpackManager = new ModpackManager();
        AvailableModpacks = new ObservableCollection<string>();

        // Load schema from app directory (same pattern as StatsEditorViewModel)
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "schema.json");
        if (!File.Exists(schemaPath))
        {
            schemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "schema.json");
        }

        if (File.Exists(schemaPath))
        {
            _schemaService.LoadSchema(schemaPath);
        }

        LoadModpacks();
    }

    /// <summary>
    /// The node graph editor view model.
    /// </summary>
    public NodeGraphViewModel NodeGraph { get; }

    /// <summary>
    /// Schema service for template field lookups.
    /// </summary>
    public SchemaService SchemaService => _schemaService;

    /// <summary>
    /// Available modpacks for saving graphs to.
    /// </summary>
    public ObservableCollection<string> AvailableModpacks { get; }

    /// <summary>
    /// Currently selected modpack.
    /// </summary>
    public string? SelectedModpack
    {
        get => _selectedModpack;
        set => this.RaiseAndSetIfChanged(ref _selectedModpack, value);
    }

    /// <summary>
    /// Get available template instance IDs for a given template type.
    /// </summary>
    /// <param name="templateType">Template type (e.g., "ActorTemplate", "SkillTemplate")</param>
    /// <returns>List of template instance names</returns>
    public List<string> GetTemplateInstanceIds(string templateType)
    {
        return _schemaService.GetTemplateInstanceIds(templateType, _modpackManager.VanillaDataPath);
    }

    /// <summary>
    /// Whether vanilla data is available for template lookups.
    /// </summary>
    public bool HasVanillaData => _modpackManager.HasVanillaData();

    /// <summary>
    /// Load available modpacks into the dropdown.
    /// </summary>
    public void LoadModpacks()
    {
        AvailableModpacks.Clear();
        AvailableModpacks.Add(CreateNewModOption);
        foreach (var mp in _modpackManager.GetStagingModpacks())
            AvailableModpacks.Add(mp.Name);

        // Auto-select first real mod if available
        if (AvailableModpacks.Count > 1)
            SelectedModpack = AvailableModpacks[1];
    }

    /// <summary>
    /// Get the path where graphs should be saved for the selected modpack.
    /// </summary>
    public string? GetGraphSavePath(string graphName)
    {
        if (string.IsNullOrEmpty(_selectedModpack) || _selectedModpack == CreateNewModOption)
            return null;

        var modpack = _modpackManager.GetStagingModpacks()
            .Find(m => m.Name == _selectedModpack);

        if (modpack == null)
            return null;

        // Save graphs in a "graphs" subdirectory of the modpack
        var graphsDir = Path.Combine(modpack.Path, "graphs");
        Directory.CreateDirectory(graphsDir);

        var safeName = graphName.Replace(" ", "_");
        return Path.Combine(graphsDir, $"{safeName}.json");
    }

    /// <summary>
    /// Get the path to save generated code for the selected modpack.
    /// </summary>
    public string? GetCodeSavePath(string graphName)
    {
        if (string.IsNullOrEmpty(_selectedModpack) || _selectedModpack == CreateNewModOption)
            return null;

        var modpack = _modpackManager.GetStagingModpacks()
            .Find(m => m.Name == _selectedModpack);

        if (modpack == null)
            return null;

        // Save generated code in a "scripts" subdirectory of the modpack
        var scriptsDir = Path.Combine(modpack.Path, "scripts");
        Directory.CreateDirectory(scriptsDir);

        var safeName = graphName.Replace(" ", "_");
        return Path.Combine(scriptsDir, $"{safeName}Effect.cs");
    }

    /// <summary>
    /// Get all saved graphs from the selected modpack.
    /// </summary>
    public List<string> GetSavedGraphs()
    {
        var result = new List<string>();

        if (string.IsNullOrEmpty(_selectedModpack) || _selectedModpack == CreateNewModOption)
            return result;

        var modpack = _modpackManager.GetStagingModpacks()
            .Find(m => m.Name == _selectedModpack);

        if (modpack == null)
            return result;

        var graphsDir = Path.Combine(modpack.Path, "graphs");
        if (Directory.Exists(graphsDir))
        {
            foreach (var file in Directory.GetFiles(graphsDir, "*.json"))
            {
                result.Add(Path.GetFileNameWithoutExtension(file));
            }
        }

        return result;
    }

    /// <summary>
    /// Load a graph from the selected modpack by name.
    /// </summary>
    public string? LoadGraphJson(string graphName)
    {
        if (string.IsNullOrEmpty(_selectedModpack) || _selectedModpack == CreateNewModOption)
            return null;

        var modpack = _modpackManager.GetStagingModpacks()
            .Find(m => m.Name == _selectedModpack);

        if (modpack == null)
            return null;

        var graphPath = Path.Combine(modpack.Path, "graphs", $"{graphName}.json");
        if (File.Exists(graphPath))
            return File.ReadAllText(graphPath);

        return null;
    }
}
