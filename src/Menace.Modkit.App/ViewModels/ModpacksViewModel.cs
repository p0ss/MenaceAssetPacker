using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ReactiveUI;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.ViewModels;

public sealed class ModpacksViewModel : ViewModelBase
{
    private readonly ModpackManager _modpackManager;
    private readonly DeployManager _deployManager;
    private readonly ModUpdateChecker _modUpdateChecker;

    public ModpacksViewModel()
    {
        _modpackManager = new ModpackManager();
        _deployManager = new DeployManager(_modpackManager);
        _modUpdateChecker = new ModUpdateChecker();
        _modUpdateChecker.LoadCache();
        AllModpacks = new ObservableCollection<ModpackItemViewModel>();
        LoadOrderVM = new LoadOrderViewModel(_modpackManager);

        LoadModpacks();
        QueueUpdateCheck();
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

    private bool _isCheckingUpdates;
    public bool IsCheckingUpdates
    {
        get => _isCheckingUpdates;
        set => this.RaiseAndSetIfChanged(ref _isCheckingUpdates, value);
    }

    private string _updateStatus = "Updates not checked yet";
    public string UpdateStatus
    {
        get => _updateStatus;
        set => this.RaiseAndSetIfChanged(ref _updateStatus, value);
    }

    public int UpdateCount => AllModpacks.Count(m => m.HasUpdateAvailable);

    public ObservableCollection<ModpackItemViewModel> AllModpacks { get; }

    /// <summary>
    /// Callback for navigating to a stats entry in the stats editor.
    /// Set by MainViewModel to wire up cross-tab navigation.
    /// Parameters: modpackName, templateType, instanceName.
    /// </summary>
    public Action<string, string, string>? NavigateToStatsEntry { get; set; }

    private ModpackItemViewModel? _selectedModpack;
    public ModpackItemViewModel? SelectedModpack
    {
        get => _selectedModpack;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedModpack, value);
            this.RaisePropertyChanged(nameof(DeployToggleText));
            value?.RefreshStatsPatches();
        }
    }

    public string DeployToggleText =>
        SelectedModpack?.IsDeployed == true ? "Undeploy" : "Deploy to Game";

    private static readonly HashSet<string> InfrastructureDlls = new(StringComparer.OrdinalIgnoreCase)
    {
        "Menace.DataExtractor.dll",
        "Menace.ModpackLoader.dll"
    };

    /// <summary>
    /// Prefixes for system/framework DLLs that should not appear as mods.
    /// These are dependencies deployed alongside mods, not mods themselves.
    /// </summary>
    private static readonly string[] SystemDllPrefixes =
    {
        "System.",
        "Microsoft.",
        "Newtonsoft.",
        "Mono.",
        "mscorlib",
        "netstandard",
        "ICSharpCode.",
        "NuGet.",
    };

    private static bool IsSystemDll(string fileName)
    {
        foreach (var prefix in SystemDllPrefixes)
        {
            if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// DLL filename substrings that indicate a known-conflicting mod, mapped to a warning message.
    /// Matched case-insensitively against detected DLL filenames.
    /// </summary>
    private static readonly List<(string FileNameContains, string Warning)> ConflictingMods = new()
    {
        ("UnityExplorer", "UnityExplorer conflicts with Menace mods and may cause crashes. Remove it before playing with modpacks."),
    };

    private void LoadModpacks()
    {
        AllModpacks.Clear();

        var deployedNames = new HashSet<string>(
            _modpackManager.GetActiveMods().Select(m => m.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var manifest in _modpackManager.GetStagingModpacks()
            .OrderBy(m => m.LoadOrder)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
        {
            var vm = new ModpackItemViewModel(manifest, _modpackManager);
            vm.IsDeployed = deployedNames.Contains(manifest.Name);
            AllModpacks.Add(vm);
        }

        // Track DLL filenames accounted for by bundled standalone mods
        var knownDllFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Scan bundled standalone mods
        foreach (var (name, author, version, description, dllSourcePath, dllFileName) in GetBundledStandaloneMods())
        {
            knownDllFileNames.Add(dllFileName);
            var modsPath = _modpackManager.ModsBasePath;
            bool isDeployed = !string.IsNullOrEmpty(modsPath)
                && File.Exists(Path.Combine(modsPath, dllFileName));

            var vm = new ModpackItemViewModel(name, author, version, description,
                dllSourcePath, dllFileName, isDeployed, _modpackManager);
            AllModpacks.Add(vm);
        }

        // Scan game's Mods/ directory for unknown standalone DLLs
        var modsBase = _modpackManager.ModsBasePath;
        if (!string.IsNullOrEmpty(modsBase) && Directory.Exists(modsBase))
        {
            foreach (var dllPath in Directory.GetFiles(modsBase, "*.dll"))
            {
                var fileName = Path.GetFileName(dllPath);
                if (InfrastructureDlls.Contains(fileName)) continue;
                if (knownDllFileNames.Contains(fileName)) continue;
                if (IsSystemDll(fileName)) continue;

                var displayName = Path.GetFileNameWithoutExtension(fileName);
                var vm = new ModpackItemViewModel(displayName, "Unknown", "", "",
                    null, fileName, true, _modpackManager);

                // Check for known-conflicting mods
                foreach (var (substring, warning) in ConflictingMods)
                {
                    if (fileName.Contains(substring, StringComparison.OrdinalIgnoreCase))
                    {
                        vm.ConflictWarning = warning;
                        break;
                    }
                }

                AllModpacks.Add(vm);
            }
        }

        this.RaisePropertyChanged(nameof(UpdateCount));
    }

    private void QueueUpdateCheck(bool forceRefresh = false)
    {
        _ = CheckForUpdatesAsync(forceRefresh).ContinueWith(t =>
        {
            var ex = t.Exception?.GetBaseException();
            if (ex != null)
                ModkitLog.Warn($"[ModpacksViewModel] Background update check failed: {ex.Message}");
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private List<(string Name, string Author, string Version, string Description, string DllSourcePath, string DllFileName)> GetBundledStandaloneMods()
    {
        var results = new List<(string, string, string, string, string, string)>();
        var standaloneDir = Path.Combine(AppContext.BaseDirectory, "third_party", "bundled", "standalone");
        if (!Directory.Exists(standaloneDir))
            return results;

        foreach (var modDir in Directory.GetDirectories(standaloneDir))
        {
            var metaPath = Path.Combine(modDir, "mod.json");
            if (!File.Exists(metaPath)) continue;

            try
            {
                var json = File.ReadAllText(metaPath);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? Path.GetFileName(modDir) : Path.GetFileName(modDir);
                var author = root.TryGetProperty("author", out var a) ? a.GetString() ?? "Unknown" : "Unknown";
                var version = root.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
                var description = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";

                // Find the DLL file in the mod directory
                var dlls = Directory.GetFiles(modDir, "*.dll");
                if (dlls.Length == 0) continue;

                var dllPath = dlls[0];
                var dllFileName = Path.GetFileName(dllPath);

                results.Add((name, author, version, description, dllPath, dllFileName));
            }
            catch
            {
                // Skip malformed metadata
            }
        }

        return results;
    }

    public void CreateNewModpack(string name, string author, string description)
    {
        var manifest = _modpackManager.CreateModpack(name, author, description);
        var vm = new ModpackItemViewModel(manifest, _modpackManager);
        AllModpacks.Add(vm);
        SelectedModpack = vm;
    }

    /// <summary>
    /// Import a modpack from a zip file.
    /// Returns true if import was successful.
    /// </summary>
    public bool ImportModpackFromZip(string zipPath)
    {
        try
        {
            var manifest = _modpackManager.ImportModpackFromZip(zipPath);
            if (manifest != null)
            {
                RefreshModpacks();
                // Select the newly imported modpack
                SelectedModpack = AllModpacks.FirstOrDefault(m => m.Name == manifest.Name);
                DeployStatus = $"Imported: {manifest.Name}";
                return true;
            }
        }
        catch (Exception ex)
        {
            DeployStatus = $"Import failed: {ex.Message}";
            Services.ModkitLog.Error($"[ModpacksViewModel] Import failed: {ex}");
        }
        return false;
    }

    /// <summary>
    /// Import multiple modpacks from zip files.
    /// </summary>
    public void ImportModpacksFromZips(IEnumerable<string> zipPaths)
    {
        int imported = 0;
        int failed = 0;

        foreach (var zipPath in zipPaths)
        {
            try
            {
                var manifest = _modpackManager.ImportModpackFromZip(zipPath);
                if (manifest != null)
                    imported++;
                else
                    failed++;
            }
            catch (Exception ex)
            {
                failed++;
                Services.ModkitLog.Warn($"[ModpacksViewModel] Failed to import {zipPath}: {ex.Message}");
            }
        }

        RefreshModpacks();

        if (imported > 0 && failed == 0)
            DeployStatus = $"Imported {imported} mod(s)";
        else if (imported > 0 && failed > 0)
            DeployStatus = $"Imported {imported} mod(s), {failed} failed";
        else if (failed > 0)
            DeployStatus = $"Import failed for {failed} file(s)";
    }

    public void DeleteSelectedModpack()
    {
        if (SelectedModpack == null)
            return;

        var name = SelectedModpack.Manifest.Name;
        var dirName = System.IO.Path.GetFileName(SelectedModpack.Path);

        if (_modpackManager.DeleteStagingModpack(dirName))
        {
            AllModpacks.Remove(SelectedModpack);
            SelectedModpack = AllModpacks.FirstOrDefault();
            DeployStatus = $"Deleted: {name}";
            LoadOrderVM.Refresh();
        }
    }

    public async Task ToggleDeploySelectedAsync()
    {
        if (SelectedModpack == null || IsDeploying) return;

        if (SelectedModpack.IsStandalone)
        {
            await ToggleDeployStandaloneAsync(SelectedModpack);
            return;
        }

        if (SelectedModpack.IsDeployed)
        {
            IsDeploying = true;
            DeployStatus = "Undeploying...";
            try
            {
                if (_modpackManager.UndeployMod(SelectedModpack.Name))
                {
                    DeployStatus = $"Undeployed: {SelectedModpack.Name}";
                    RefreshModpacks();
                }
                else
                {
                    DeployStatus = $"Failed to undeploy: {SelectedModpack.Name}";
                }
            }
            finally
            {
                IsDeploying = false;
            }
        }
        else
        {
            await DeploySingleAsync();
        }
    }

    private async Task ToggleDeployStandaloneAsync(ModpackItemViewModel mod)
    {
        var modsPath = _modpackManager.ModsBasePath;
        if (string.IsNullOrEmpty(modsPath) || string.IsNullOrEmpty(mod.DllFileName))
        {
            DeployStatus = "Game install path not set";
            return;
        }

        IsDeploying = true;
        var targetPath = Path.Combine(modsPath, mod.DllFileName);

        try
        {
            if (mod.IsDeployed)
            {
                // Undeploy: delete the DLL from Mods/
                DeployStatus = $"Removing {mod.DllFileName}...";
                await Task.Run(() =>
                {
                    if (File.Exists(targetPath))
                        File.Delete(targetPath);
                });
                DeployStatus = $"Undeployed: {mod.Name}";
            }
            else
            {
                // Deploy: copy DLL from bundled source to Mods/
                if (string.IsNullOrEmpty(mod.DllSourcePath) || !File.Exists(mod.DllSourcePath))
                {
                    DeployStatus = $"No source DLL available for {mod.Name}";
                    return;
                }

                DeployStatus = $"Deploying {mod.DllFileName}...";
                await Task.Run(() =>
                {
                    Directory.CreateDirectory(modsPath);
                    File.Copy(mod.DllSourcePath, targetPath, true);
                });
                DeployStatus = $"Deployed: {mod.Name}";
            }

            RefreshModpacks();
        }
        catch (Exception ex)
        {
            DeployStatus = $"Failed: {ex.Message}";
        }
        finally
        {
            IsDeploying = false;
        }
    }

    public async Task DeploySingleAsync()
    {
        if (SelectedModpack == null || IsDeploying) return;

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
        if (IsDeploying) return;

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
        if (IsDeploying) return;

        IsDeploying = true;
        DeployStatus = "Undeploying...";

        try
        {
            var progress = new Progress<string>(s => DeployStatus = s);
            var result = await _deployManager.UndeployAllAsync(progress);
            DeployStatus = result.Message;

            if (result.Success)
                RefreshModpacks();
        }
        catch (Exception ex)
        {
            DeployStatus = $"Undeploy failed: {ex.Message}";
        }
        finally
        {
            IsDeploying = false;
        }
    }

    public void MoveUp() => MoveItemUp(SelectedModpack);
    public void MoveDown() => MoveItemDown(SelectedModpack);

    public void MoveItemUp(ModpackItemViewModel? item)
    {
        if (item == null) return;
        var index = AllModpacks.IndexOf(item);
        if (index <= 0) return;
        AllModpacks.Move(index, index - 1);
        SelectedModpack = item;
        ReassignLoadOrders();
    }

    public void MoveItemDown(ModpackItemViewModel? item)
    {
        if (item == null) return;
        var index = AllModpacks.IndexOf(item);
        if (index < 0 || index >= AllModpacks.Count - 1) return;
        AllModpacks.Move(index, index + 1);
        SelectedModpack = item;
        ReassignLoadOrders();
    }

    public void MoveItem(ModpackItemViewModel item, int targetIndex)
    {
        var currentIndex = AllModpacks.IndexOf(item);
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= AllModpacks.Count || currentIndex == targetIndex)
            return;
        AllModpacks.Move(currentIndex, targetIndex);
        SelectedModpack = item;
        ReassignLoadOrders();
    }

    private void ReassignLoadOrders()
    {
        for (int i = 0; i < AllModpacks.Count; i++)
        {
            AllModpacks[i].LoadOrder = (i + 1) * 10;
        }
        LoadOrderVM.Refresh();
    }

    public void RefreshModpacks()
    {
        var selectedName = SelectedModpack?.Name;
        LoadModpacks();
        LoadOrderVM.Refresh();
        if (selectedName != null)
            SelectedModpack = AllModpacks.FirstOrDefault(m => m.Name == selectedName);
        this.RaisePropertyChanged(nameof(DeployToggleText));
        QueueUpdateCheck();
    }

    public async Task CheckForUpdatesAsync(bool forceRefresh = false)
    {
        if (IsCheckingUpdates)
            return;

        IsCheckingUpdates = true;
        if (forceRefresh)
            _modUpdateChecker.ClearCache();

        try
        {
            var candidates = AllModpacks.Where(m => m.CanCheckForUpdates).ToList();
            if (candidates.Count == 0)
            {
                UpdateStatus = "No mods with repository URLs configured";
                this.RaisePropertyChanged(nameof(UpdateCount));
                return;
            }

            UpdateStatus = forceRefresh ? "Checking for updates..." : "Refreshing update status...";
            foreach (var item in candidates)
                item.BeginUpdateCheck();

            var tasks = candidates.Select(async item =>
                (Item: item, Info: await _modUpdateChecker.CheckForUpdateAsync(item.Manifest)));
            var results = await Task.WhenAll(tasks);

            var updates = 0;
            var errors = 0;
            foreach (var (item, info) in results)
            {
                item.ApplyUpdateInfo(info);
                if (info.HasUpdate) updates++;
                if (!string.IsNullOrWhiteSpace(info.Error)) errors++;
            }

            _modUpdateChecker.SaveCache();

            UpdateStatus = updates > 0
                ? $"{updates} update(s) available"
                : errors > 0
                    ? $"No updates found ({errors} check error(s))"
                    : $"All {candidates.Count} tracked mod(s) are up to date";
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Update check failed: {ex.Message}";
            ModkitLog.Warn($"[ModpacksViewModel] Update check failed: {ex.Message}");
        }
        finally
        {
            IsCheckingUpdates = false;
            this.RaisePropertyChanged(nameof(UpdateCount));
        }
    }
}

public enum ModUpdateState
{
    NoRepository,
    Checking,
    UpdateAvailable,
    UpToDate,
    Error
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

    /// <summary>
    /// Constructor for standalone DLL mods (bundled or detected).
    /// Creates a synthetic manifest in memory — SaveMetadata() is a no-op.
    /// </summary>
    public ModpackItemViewModel(string name, string author, string version,
        string description, string? dllSourcePath, string dllFileName,
        bool isDeployed, ModpackManager manager)
    {
        _manifest = new ModpackManifest
        {
            Name = name,
            Author = author,
            Version = version,
            Description = description,
        };
        _manager = manager;
        IsStandalone = true;
        DllSourcePath = dllSourcePath;
        DllFileName = dllFileName;
        _isDeployed = isDeployed;
    }

    private bool _isStandalone;
    public bool IsStandalone
    {
        get => _isStandalone;
        set => this.RaiseAndSetIfChanged(ref _isStandalone, value);
    }

    public string? DllSourcePath { get; set; }
    public string? DllFileName { get; set; }

    private string? _conflictWarning;
    public string? ConflictWarning
    {
        get => _conflictWarning;
        set => this.RaiseAndSetIfChanged(ref _conflictWarning, value);
    }

    public bool HasConflict => !string.IsNullOrEmpty(ConflictWarning);

    private bool _isCheckingForUpdate;
    public bool IsCheckingForUpdate
    {
        get => _isCheckingForUpdate;
        private set => this.RaiseAndSetIfChanged(ref _isCheckingForUpdate, value);
    }

    private bool _hasUpdateAvailable;
    public bool HasUpdateAvailable
    {
        get => _hasUpdateAvailable;
        private set => this.RaiseAndSetIfChanged(ref _hasUpdateAvailable, value);
    }

    private string _latestVersion = string.Empty;
    public string LatestVersion
    {
        get => _latestVersion;
        private set => this.RaiseAndSetIfChanged(ref _latestVersion, value);
    }

    private string? _updateCheckError;
    public string? UpdateCheckError
    {
        get => _updateCheckError;
        private set => this.RaiseAndSetIfChanged(ref _updateCheckError, value);
    }

    public bool HasUpdateCheckError => !string.IsNullOrWhiteSpace(UpdateCheckError);
    public bool CanCheckForUpdates => !string.IsNullOrWhiteSpace(_manifest.RepositoryUrl);
    public bool ShowUpdateStatus => IsCheckingForUpdate || HasUpdateAvailable || HasUpdateCheckError || CanCheckForUpdates;

    public ModUpdateState UpdateState
    {
        get
        {
            if (IsCheckingForUpdate) return ModUpdateState.Checking;
            if (HasUpdateCheckError) return ModUpdateState.Error;
            if (HasUpdateAvailable) return ModUpdateState.UpdateAvailable;
            if (CanCheckForUpdates) return ModUpdateState.UpToDate;
            return ModUpdateState.NoRepository;
        }
    }

    public string UpdateSummary => UpdateState switch
    {
        ModUpdateState.Checking => "Checking for updates...",
        ModUpdateState.UpdateAvailable => $"Update available: {VersionDisplay} -> {FormatVersion(LatestVersion)}",
        ModUpdateState.UpToDate => "Up to date",
        ModUpdateState.Error => $"Update check failed: {UpdateCheckError}",
        _ => "No repository configured"
    };

    internal ModpackManifest Manifest => _manifest;

    private bool _isDeployed;
    public bool IsDeployed
    {
        get => _isDeployed;
        set => this.RaiseAndSetIfChanged(ref _isDeployed, value);
    }

    public string Name
    {
        get => _manifest.Name;
        set
        {
            if (_manifest.Name != value)
            {
                _manifest.Name = value;
                this.RaisePropertyChanged();
                if (!IsStandalone) SaveMetadata();
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
                if (!IsStandalone) SaveMetadata();
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
                if (!IsStandalone) SaveMetadata();
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
                this.RaisePropertyChanged(nameof(VersionDisplay));
                RaiseUpdateStateProperties();
                if (!IsStandalone) SaveMetadata();
            }
        }
    }

    public string VersionDisplay => string.IsNullOrEmpty(Version) ? "" : $"v{Version}";

    public int LoadOrder
    {
        get => _manifest.LoadOrder;
        set
        {
            if (_manifest.LoadOrder != value)
            {
                _manifest.LoadOrder = value;
                this.RaisePropertyChanged();
                if (!IsStandalone) SaveMetadata();
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
            if (!IsStandalone) SaveMetadata();
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
                if (!IsStandalone) SaveMetadata();
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

    private ObservableCollection<StatsPatchEntry> _statsPatches = new();
    public ObservableCollection<StatsPatchEntry> StatsPatches
    {
        get => _statsPatches;
        set => this.RaiseAndSetIfChanged(ref _statsPatches, value);
    }

    public bool HasStatsPatches => _statsPatches.Count > 0;

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

    public void RefreshStatsPatches()
    {
        _statsPatches.Clear();
        if (IsStandalone || string.IsNullOrEmpty(_manifest.Path)) return;

        var statsDir = System.IO.Path.Combine(_manifest.Path, "stats");
        if (!System.IO.Directory.Exists(statsDir)) return;

        foreach (var file in System.IO.Directory.GetFiles(statsDir, "*.json"))
        {
            var templateType = System.IO.Path.GetFileNameWithoutExtension(file);
            try
            {
                using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(file));
                foreach (var instance in doc.RootElement.EnumerateObject())
                {
                    var fields = new List<string>();
                    if (instance.Value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var field in instance.Value.EnumerateObject())
                            fields.Add($"{field.Name} = {field.Value}");
                    }
                    _statsPatches.Add(new StatsPatchEntry
                    {
                        TemplateType = templateType,
                        InstanceName = instance.Name,
                        Fields = fields
                    });
                }
            }
            catch { }
        }
        this.RaisePropertyChanged(nameof(HasStatsPatches));
    }

    private void SaveMetadata()
    {
        if (IsStandalone) return;
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

    public void BeginUpdateCheck()
    {
        IsCheckingForUpdate = true;
        UpdateCheckError = null;
        RaiseUpdateStateProperties();
    }

    public void ApplyUpdateInfo(ModUpdateInfo info)
    {
        IsCheckingForUpdate = false;
        HasUpdateAvailable = info.HasUpdate;
        LatestVersion = info.LatestVersion ?? string.Empty;
        UpdateCheckError = string.IsNullOrWhiteSpace(info.Error) ? null : info.Error;
        RaiseUpdateStateProperties();
    }

    private void RaiseUpdateStateProperties()
    {
        this.RaisePropertyChanged(nameof(HasUpdateCheckError));
        this.RaisePropertyChanged(nameof(CanCheckForUpdates));
        this.RaisePropertyChanged(nameof(ShowUpdateStatus));
        this.RaisePropertyChanged(nameof(UpdateState));
        this.RaisePropertyChanged(nameof(UpdateSummary));
    }

    private static string FormatVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return "";
        return version.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? version : $"v{version}";
    }
}

public class StatsPatchEntry
{
    public string TemplateType { get; set; } = "";
    public string InstanceName { get; set; } = "";
    public List<string> Fields { get; set; } = new();
    public string DisplayName => $"{InstanceName}";
    public string FieldSummary => string.Join(", ", Fields);
}
