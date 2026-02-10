using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.ViewModels;

/// <summary>
/// ViewModel for the setup/update screen that shows component status and handles downloads.
/// </summary>
public class SetupViewModel : ViewModelBase
{
    private readonly ComponentManager _componentManager;
    private CancellationTokenSource? _downloadCts;

    public SetupViewModel()
    {
        _componentManager = ComponentManager.Instance;
        RequiredComponents = new ObservableCollection<ComponentStatusViewModel>();
        OptionalComponents = new ObservableCollection<ComponentStatusViewModel>();
    }

    /// <summary>
    /// Event raised when setup is complete and app should continue.
    /// </summary>
    public event Action? SetupComplete;

    /// <summary>
    /// Event raised when user skips setup.
    /// </summary>
    public event Action? SetupSkipped;

    public ObservableCollection<ComponentStatusViewModel> RequiredComponents { get; }
    public ObservableCollection<ComponentStatusViewModel> OptionalComponents { get; }

    private bool _isLoading = true;
    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    private bool _isDownloading;
    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            this.RaiseAndSetIfChanged(ref _isDownloading, value);
            this.RaisePropertyChanged(nameof(CanDownload));
            this.RaisePropertyChanged(nameof(CanSkip));
        }
    }

    private string _downloadStatus = "";
    public string DownloadStatus
    {
        get => _downloadStatus;
        set => this.RaiseAndSetIfChanged(ref _downloadStatus, value);
    }

    private string _currentComponent = "";
    public string CurrentComponent
    {
        get => _currentComponent;
        set => this.RaiseAndSetIfChanged(ref _currentComponent, value);
    }

    private int _overallProgress;
    public int OverallProgress
    {
        get => _overallProgress;
        set => this.RaiseAndSetIfChanged(ref _overallProgress, value);
    }

    private int _currentProgress;
    public int CurrentProgress
    {
        get => _currentProgress;
        set => this.RaiseAndSetIfChanged(ref _currentProgress, value);
    }

    private string _downloadSpeed = "";
    public string DownloadSpeed
    {
        get => _downloadSpeed;
        set => this.RaiseAndSetIfChanged(ref _downloadSpeed, value);
    }

    public bool CanDownload => !IsDownloading && HasPendingDownloads;
    public bool CanSkip => !IsDownloading;

    private bool _hasPendingDownloads;
    public bool HasPendingDownloads
    {
        get => _hasPendingDownloads;
        set
        {
            this.RaiseAndSetIfChanged(ref _hasPendingDownloads, value);
            this.RaisePropertyChanged(nameof(CanDownload));
        }
    }

    private bool _hasRequiredPending;
    public bool HasRequiredPending
    {
        get => _hasRequiredPending;
        set => this.RaiseAndSetIfChanged(ref _hasRequiredPending, value);
    }

    private string _totalDownloadSize = "";
    public string TotalDownloadSize
    {
        get => _totalDownloadSize;
        set => this.RaiseAndSetIfChanged(ref _totalDownloadSize, value);
    }

    /// <summary>
    /// Load component status from ComponentManager.
    /// </summary>
    public async Task LoadComponentsAsync()
    {
        IsLoading = true;

        try
        {
            var statuses = await _componentManager.GetComponentStatusAsync(forceRemoteFetch: true);

            RequiredComponents.Clear();
            OptionalComponents.Clear();

            foreach (var status in statuses)
            {
                var vm = new ComponentStatusViewModel(status);
                if (status.Required)
                    RequiredComponents.Add(vm);
                else
                    OptionalComponents.Add(vm);
            }

            UpdatePendingStatus();
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[SetupViewModel] Failed to load components: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Toggle selection of an optional component.
    /// </summary>
    public void ToggleOptionalComponent(ComponentStatusViewModel component)
    {
        component.IsSelected = !component.IsSelected;
        UpdatePendingStatus();
    }

    private void UpdatePendingStatus()
    {
        var pendingRequired = RequiredComponents
            .Where(c => c.Status.State != ComponentState.UpToDate)
            .ToList();

        var pendingOptional = OptionalComponents
            .Where(c => c.IsSelected && c.Status.State != ComponentState.UpToDate)
            .ToList();

        HasRequiredPending = pendingRequired.Count > 0;
        HasPendingDownloads = pendingRequired.Count > 0 || pendingOptional.Count > 0;

        var totalSize = pendingRequired.Sum(c => c.Status.DownloadSize)
                      + pendingOptional.Sum(c => c.Status.DownloadSize);

        TotalDownloadSize = totalSize > 0
            ? $"{totalSize / (1024.0 * 1024.0):F0} MB"
            : "";
    }

    /// <summary>
    /// Download all pending components (required + selected optional).
    /// </summary>
    public async Task DownloadAsync()
    {
        if (IsDownloading) return;

        IsDownloading = true;
        _downloadCts = new CancellationTokenSource();

        try
        {
            // Get list of components to download
            var toDownload = RequiredComponents
                .Where(c => c.Status.State != ComponentState.UpToDate)
                .Select(c => c.Status.Name)
                .Concat(OptionalComponents
                    .Where(c => c.IsSelected && c.Status.State != ComponentState.UpToDate)
                    .Select(c => c.Status.Name))
                .ToList();

            if (toDownload.Count == 0)
            {
                SetupComplete?.Invoke();
                return;
            }

            var progress = new Progress<MultiDownloadProgress>(p =>
            {
                CurrentComponent = p.CurrentComponent;
                DownloadStatus = p.Message;
                CurrentProgress = p.CurrentPercent;
                OverallProgress = p.OverallPercent;

                if (p.BytesPerSecond > 0)
                    DownloadSpeed = $"{p.BytesPerSecond / (1024.0 * 1024.0):F1} MB/s";
                else
                    DownloadSpeed = "";

                // Update individual component status
                var componentVm = RequiredComponents.Concat(OptionalComponents)
                    .FirstOrDefault(c => c.Status.Name == p.CurrentComponent);
                if (componentVm != null)
                {
                    componentVm.DownloadProgress = p.CurrentPercent;
                    componentVm.IsDownloading = p.CurrentPercent > 0 && p.CurrentPercent < 100;
                }
            });

            var success = await _componentManager.DownloadComponentsAsync(toDownload, progress, _downloadCts.Token);

            // Refresh component status
            await LoadComponentsAsync();

            if (success && !HasRequiredPending)
            {
                DownloadStatus = "All components installed!";
                await Task.Delay(500);
                SetupComplete?.Invoke();
            }
            else if (!success)
            {
                DownloadStatus = "Some downloads failed. Please retry.";
            }
        }
        catch (OperationCanceledException)
        {
            DownloadStatus = "Download cancelled.";
        }
        catch (Exception ex)
        {
            DownloadStatus = $"Error: {ex.Message}";
            ModkitLog.Error($"[SetupViewModel] Download failed: {ex}");
        }
        finally
        {
            IsDownloading = false;
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    /// <summary>
    /// Cancel the current download.
    /// </summary>
    public void CancelDownload()
    {
        _downloadCts?.Cancel();
    }

    /// <summary>
    /// Skip setup (only if no required components are pending).
    /// </summary>
    public void Skip()
    {
        if (!HasRequiredPending)
        {
            SetupSkipped?.Invoke();
        }
    }

    /// <summary>
    /// Continue to app (called when all required components are installed).
    /// </summary>
    public void Continue()
    {
        SetupComplete?.Invoke();
    }
}

/// <summary>
/// ViewModel wrapper for ComponentStatus with UI-specific properties.
/// </summary>
public class ComponentStatusViewModel : ViewModelBase
{
    public ComponentStatusViewModel(ComponentStatus status)
    {
        Status = status;
        IsSelected = false; // Optional components start unselected
    }

    public ComponentStatus Status { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    private bool _isDownloading;
    public bool IsDownloading
    {
        get => _isDownloading;
        set => this.RaiseAndSetIfChanged(ref _isDownloading, value);
    }

    private int _downloadProgress;
    public int DownloadProgress
    {
        get => _downloadProgress;
        set => this.RaiseAndSetIfChanged(ref _downloadProgress, value);
    }

    // Convenience properties for UI binding
    public string Name => Status.Name;
    public string Description => Status.Description;
    public string LatestVersion => Status.LatestVersion;
    public string InstalledVersion => Status.InstalledVersion ?? "";
    public string DownloadSize => Status.DownloadSizeDisplay;
    public ComponentState State => Status.State;
    public bool IsUpToDate => Status.State == ComponentState.UpToDate;
    public bool IsOutdated => Status.State == ComponentState.Outdated;
    public bool IsNotInstalled => Status.State == ComponentState.NotInstalled;
    public bool NeedsAction => Status.State != ComponentState.UpToDate;
}
