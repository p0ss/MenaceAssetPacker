using System;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.ViewModels;

public sealed class ModpacksViewModel : ViewModelBase
{
    private readonly ModpackManager _modpackManager;

    public ModpacksViewModel()
    {
        _modpackManager = new ModpackManager();
        StagingModpacks = new ObservableCollection<ModpackItemViewModel>();
        ActiveMods = new ObservableCollection<ModpackItemViewModel>();

        LoadModpacks();
    }

    public ObservableCollection<ModpackItemViewModel> StagingModpacks { get; }
    public ObservableCollection<ModpackItemViewModel> ActiveMods { get; }

    private ModpackItemViewModel? _selectedModpack;
    public ModpackItemViewModel? SelectedModpack
    {
        get => _selectedModpack;
        set => this.RaiseAndSetIfChanged(ref _selectedModpack, value);
    }

    private void LoadModpacks()
    {
        StagingModpacks.Clear();
        ActiveMods.Clear();

        // Load staging modpacks
        foreach (var modpack in _modpackManager.GetStagingModpacks())
        {
            StagingModpacks.Add(new ModpackItemViewModel(modpack, _modpackManager));
        }

        // Load active mods
        foreach (var modpack in _modpackManager.GetActiveMods())
        {
            ActiveMods.Add(new ModpackItemViewModel(modpack, _modpackManager));
        }
    }

    public void CreateNewModpack(string name, string author, string description)
    {
        var modpack = _modpackManager.CreateModpack(name, author, description);
        StagingModpacks.Add(new ModpackItemViewModel(modpack, _modpackManager));
        SelectedModpack = StagingModpacks.Last();
    }

    public void RefreshModpacks()
    {
        LoadModpacks();
    }
}

public sealed class ModpackItemViewModel : ViewModelBase
{
    private readonly ModpackInfo _modpack;
    private readonly ModpackManager _manager;

    public ModpackItemViewModel(ModpackInfo modpack, ModpackManager manager)
    {
        _modpack = modpack;
        _manager = manager;
        LoadFiles();
    }

    public string Name
    {
        get => _modpack.Name;
        set
        {
            if (_modpack.Name != value)
            {
                _modpack.Name = value;
                this.RaisePropertyChanged();
                SaveMetadata();
            }
        }
    }

    public string Author
    {
        get => _modpack.Author;
        set
        {
            if (_modpack.Author != value)
            {
                _modpack.Author = value;
                this.RaisePropertyChanged();
                SaveMetadata();
            }
        }
    }

    public string Description
    {
        get => _modpack.Description;
        set
        {
            if (_modpack.Description != value)
            {
                _modpack.Description = value;
                this.RaisePropertyChanged();
                SaveMetadata();
            }
        }
    }

    public string Version
    {
        get => _modpack.Version;
        set
        {
            if (_modpack.Version != value)
            {
                _modpack.Version = value;
                this.RaisePropertyChanged();
                SaveMetadata();
            }
        }
    }

    public DateTime CreatedDate => _modpack.CreatedDate;
    public DateTime ModifiedDate => _modpack.ModifiedDate;
    public string Path => _modpack.Path;

    private ObservableCollection<string> _files = new();
    public ObservableCollection<string> Files
    {
        get => _files;
        set => this.RaiseAndSetIfChanged(ref _files, value);
    }

    private void LoadFiles()
    {
        _files.Clear();
        if (System.IO.Directory.Exists(_modpack.Path))
        {
            var files = System.IO.Directory.GetFiles(_modpack.Path, "*.*", System.IO.SearchOption.AllDirectories);
            foreach (var file in files)
            {
                // Show relative path from modpack directory
                var relativePath = System.IO.Path.GetRelativePath(_modpack.Path, file);
                _files.Add(relativePath);
            }
        }
    }

    private void SaveMetadata()
    {
        _manager.UpdateModpackMetadata(_modpack);
    }

    public void Deploy()
    {
        _manager.DeployModpack(_modpack.Name);
    }

    public void Export(string exportPath)
    {
        _manager.ExportModpack(_modpack.Name, exportPath);
    }
}
