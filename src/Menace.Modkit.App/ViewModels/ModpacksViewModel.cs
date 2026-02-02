using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.ViewModels;

public sealed class ModpacksViewModel : ViewModelBase
{
    private readonly ModpackManager _modpackManager;

    private readonly DeployManager _deployManager;

    public ModpacksViewModel()
    {
        _modpackManager = new ModpackManager();
        _deployManager = new DeployManager(_modpackManager);
        StagingModpacks = new ObservableCollection<ModpackItemViewModel>();
        ActiveMods = new ObservableCollection<ModpackItemViewModel>();
        LoadOrderVM = new LoadOrderViewModel(_modpackManager);

        LoadModpacks();
    }

    public ModpackManager ModpackManager => _modpackManager;
    public DeployManager DeployManager => _deployManager;
    public LoadOrderViewModel LoadOrderVM { get; }

    private string _deployStatus = string.Empty;
    public string DeployStatus
    {
        get => _deployStatus;
        set => this.RaiseAndSetIfChanged(ref _deployStatus, value);
    }

    private bool _isDeploying;
    public bool IsDeploying
    {
        get => _isDeploying;
        set => this.RaiseAndSetIfChanged(ref _isDeploying, value);
    }

    public async Task DeploySingleAsync()
    {
        if (SelectedModpack == null) return;

        IsDeploying = true;
        DeployStatus = "Deploying...";

        try
        {
            var progress = new Progress<string>(s => DeployStatus = s);
            var result = await _deployManager.DeploySingleAsync(SelectedModpack.Manifest, progress);
            DeployStatus = result.Message;

            if (result.Success)
                RefreshModpacks();
        }
        catch (Exception ex)
        {
            DeployStatus = $"Deploy failed: {ex.Message}";
        }
        finally
        {
            IsDeploying = false;
        }
    }

    public async Task DeployAllAsync()
    {
        IsDeploying = true;
        DeployStatus = "Deploying...";

        try
        {
            var progress = new Progress<string>(s => DeployStatus = s);
            var result = await _deployManager.DeployAllAsync(progress);
            DeployStatus = result.Message;

            if (result.Success)
                RefreshModpacks();
        }
        catch (Exception ex)
        {
            DeployStatus = $"Deploy failed: {ex.Message}";
        }
        finally
        {
            IsDeploying = false;
        }
    }

    public async Task UndeployAllAsync()
    {
        IsDeploying = true;
        DeployStatus = "Undeploying...";

        var progress = new Progress<string>(s => DeployStatus = s);
        var result = await _deployManager.UndeployAllAsync(progress);

        DeployStatus = result.Message;
        IsDeploying = false;

        if (result.Success)
            RefreshModpacks();
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

        foreach (var manifest in _modpackManager.GetStagingModpacks())
            StagingModpacks.Add(new ModpackItemViewModel(manifest, _modpackManager));

        foreach (var manifest in _modpackManager.GetActiveMods())
            ActiveMods.Add(new ModpackItemViewModel(manifest, _modpackManager));
    }

    public void CreateNewModpack(string name, string author, string description)
    {
        var manifest = _modpackManager.CreateModpack(name, author, description);
        StagingModpacks.Add(new ModpackItemViewModel(manifest, _modpackManager));
        SelectedModpack = StagingModpacks.Last();
    }

    public void DeleteSelectedModpack()
    {
        if (SelectedModpack == null)
            return;

        var name = SelectedModpack.Manifest.Name;
        var dirName = System.IO.Path.GetFileName(SelectedModpack.Path);

        if (_modpackManager.DeleteStagingModpack(dirName))
        {
            StagingModpacks.Remove(SelectedModpack);
            SelectedModpack = StagingModpacks.FirstOrDefault();
            DeployStatus = $"Deleted: {name}";
            LoadOrderVM.Refresh();
        }
    }

    public void UndeploySelectedMod()
    {
        if (_selectedActiveMod == null)
            return;

        var name = _selectedActiveMod.Manifest.Name;
        var dirName = System.IO.Path.GetFileName(_selectedActiveMod.Path);

        if (_modpackManager.UndeployMod(dirName))
        {
            ActiveMods.Remove(_selectedActiveMod);
            _selectedActiveMod = null;
            this.RaisePropertyChanged(nameof(SelectedActiveMod));
            DeployStatus = $"Undeployed: {name}";
        }
    }

    private ModpackItemViewModel? _selectedActiveMod;
    public ModpackItemViewModel? SelectedActiveMod
    {
        get => _selectedActiveMod;
        set => this.RaiseAndSetIfChanged(ref _selectedActiveMod, value);
    }

    public void RefreshModpacks()
    {
        LoadModpacks();
        LoadOrderVM.Refresh();
    }
}

public sealed class ModpackItemViewModel : ViewModelBase
{
    private readonly ModpackManifest _manifest;
    private readonly ModpackManager _manager;

    public ModpackItemViewModel(ModpackManifest manifest, ModpackManager manager)
    {
        _manifest = manifest;
        _manager = manager;
        LoadFiles();
    }

    internal ModpackManifest Manifest => _manifest;

    public string Name
    {
        get => _manifest.Name;
        set
        {
            if (_manifest.Name != value)
            {
                _manifest.Name = value;
                this.RaisePropertyChanged();
                SaveMetadata();
            }
        }
    }

    public string Author
    {
        get => _manifest.Author;
        set
        {
            if (_manifest.Author != value)
            {
                _manifest.Author = value;
                this.RaisePropertyChanged();
                SaveMetadata();
            }
        }
    }

    public string Description
    {
        get => _manifest.Description;
        set
        {
            if (_manifest.Description != value)
            {
                _manifest.Description = value;
                this.RaisePropertyChanged();
                SaveMetadata();
            }
        }
    }

    public string Version
    {
        get => _manifest.Version;
        set
        {
            if (_manifest.Version != value)
            {
                _manifest.Version = value;
                this.RaisePropertyChanged();
                SaveMetadata();
            }
        }
    }

    public int LoadOrder
    {
        get => _manifest.LoadOrder;
        set
        {
            if (_manifest.LoadOrder != value)
            {
                _manifest.LoadOrder = value;
                this.RaisePropertyChanged();
                SaveMetadata();
            }
        }
    }

    public string DependenciesText
    {
        get => string.Join(", ", _manifest.Dependencies);
        set
        {
            var deps = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            _manifest.Dependencies = deps;
            this.RaisePropertyChanged();
            SaveMetadata();
        }
    }

    public bool HasCode => _manifest.HasCode;

    public SecurityStatus SecurityStatus
    {
        get => _manifest.SecurityStatus;
        set
        {
            if (_manifest.SecurityStatus != value)
            {
                _manifest.SecurityStatus = value;
                this.RaisePropertyChanged();
                SaveMetadata();
            }
        }
    }

    public string SecurityStatusDisplay => _manifest.SecurityStatus switch
    {
        SecurityStatus.SourceVerified => "Source Verified",
        SecurityStatus.SourceWithWarnings => "Source (Warnings)",
        SecurityStatus.UnverifiedBinary => "Unverified Binary",
        _ => "Unreviewed"
    };

    public DateTime CreatedDate => _manifest.CreatedDate;
    public DateTime ModifiedDate => _manifest.ModifiedDate;
    public string Path => _manifest.Path;

    private ObservableCollection<string> _files = new();
    public ObservableCollection<string> Files
    {
        get => _files;
        set => this.RaiseAndSetIfChanged(ref _files, value);
    }

    private void LoadFiles()
    {
        _files.Clear();
        if (System.IO.Directory.Exists(_manifest.Path))
        {
            var files = System.IO.Directory.GetFiles(_manifest.Path, "*.*", System.IO.SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var relativePath = System.IO.Path.GetRelativePath(_manifest.Path, file);
                _files.Add(relativePath);
            }
        }
    }

    private void SaveMetadata()
    {
        _manager.UpdateModpackMetadata(_manifest);
    }

    public void Deploy()
    {
        _manager.DeployModpack(_manifest.Name);
    }

    public void Export(string exportPath)
    {
        _manager.ExportModpack(_manifest.Name, exportPath);
    }
}
