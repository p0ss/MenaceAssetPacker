using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.ViewModels;

public enum DownloadState
{
    None,
    Downloading,
    Success,
    Failed,
    Cancelled
}

/// <summary>
/// ViewModel for the setup/update screen that shows component status and handles downloads.
/// </summary>
public class SetupViewModel : ViewModelBase
{
    private readonly ComponentManager _componentManager;
    private readonly EnvironmentChecker _environmentChecker;
    private readonly AiAssistantService _aiAssistantService;
    private CancellationTokenSource? _downloadCts;

    public SetupViewModel()
    {
        _componentManager = ComponentManager.Instance;
        _environmentChecker = EnvironmentChecker.Instance;
        _aiAssistantService = AiAssistantService.Instance;
        RequiredComponents = new ObservableCollection<ComponentStatusViewModel>();
        OptionalComponents = new ObservableCollection<ComponentStatusViewModel>();
        EnvironmentChecks = new ObservableCollection<EnvironmentCheckViewModel>();
        AiClients = new ObservableCollection<AiClientViewModel>();
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
    public ObservableCollection<EnvironmentCheckViewModel> EnvironmentChecks { get; }
    public ObservableCollection<AiClientViewModel> AiClients { get; }

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

    /// <summary>
    /// True when there's download status to display.
    /// </summary>
    public bool HasDownloadStatus => !string.IsNullOrEmpty(DownloadStatus);

    /// <summary>
    /// Icon for the current download status.
    /// </summary>
    public string DownloadStatusIcon => _downloadState switch
    {
        DownloadState.Downloading => "\u21BB",  // Rotating arrows
        DownloadState.Success => "\u2713",      // Checkmark
        DownloadState.Failed => "\u2717",       // X
        DownloadState.Cancelled => "\u2014",    // Em dash
        _ => ""
    };

    /// <summary>
    /// Color for the download status icon.
    /// </summary>
    public string DownloadStatusColor => _downloadState switch
    {
        DownloadState.Downloading => "#6B9FFF",  // Blue
        DownloadState.Success => "#8ECDC8",      // Green
        DownloadState.Failed => "#FF6B6B",       // Red
        DownloadState.Cancelled => "#888888",    // Gray
        _ => "#888888"
    };

    private DownloadState _downloadState = DownloadState.None;
    private void SetDownloadState(DownloadState state, string message)
    {
        _downloadState = state;
        DownloadStatus = message;
        this.RaisePropertyChanged(nameof(HasDownloadStatus));
        this.RaisePropertyChanged(nameof(DownloadStatusIcon));
        this.RaisePropertyChanged(nameof(DownloadStatusColor));
    }

    /// <summary>
    /// True when the primary action button should be enabled.
    /// Enabled when: downloading is possible OR we can continue (no required pending).
    /// </summary>
    public bool CanDownload => !IsDownloading && !IsFixing && (HasPendingDownloads || !HasRequiredPending);
    public bool CanSkip => !IsDownloading && !IsFixing;

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
        set
        {
            this.RaiseAndSetIfChanged(ref _hasRequiredPending, value);
            this.RaisePropertyChanged(nameof(CanDownload));
        }
    }

    private bool _hasEnvironmentIssues;
    public bool HasEnvironmentIssues
    {
        get => _hasEnvironmentIssues;
        set => this.RaiseAndSetIfChanged(ref _hasEnvironmentIssues, value);
    }

    private bool _hasEnvironmentFailures;
    public bool HasEnvironmentFailures
    {
        get => _hasEnvironmentFailures;
        set => this.RaiseAndSetIfChanged(ref _hasEnvironmentFailures, value);
    }

    private bool _hasAnyAiClient;
    public bool HasAnyAiClient
    {
        get => _hasAnyAiClient;
        set => this.RaiseAndSetIfChanged(ref _hasAnyAiClient, value);
    }

    private bool _isMcpEnabled;
    public bool IsMcpEnabled
    {
        get => _isMcpEnabled;
        set
        {
            this.RaiseAndSetIfChanged(ref _isMcpEnabled, value);
            AppSettings.Instance.SetEnableMcpServer(value);
        }
    }

    private bool _isConfiguringAi;
    public bool IsConfiguringAi
    {
        get => _isConfiguringAi;
        set => this.RaiseAndSetIfChanged(ref _isConfiguringAi, value);
    }

    private bool _isFixing;
    public bool IsFixing
    {
        get => _isFixing;
        set
        {
            this.RaiseAndSetIfChanged(ref _isFixing, value);
            this.RaisePropertyChanged(nameof(CanDownload));
            this.RaisePropertyChanged(nameof(CanSkip));
        }
    }

    private string _fixStatus = "";
    public string FixStatus
    {
        get => _fixStatus;
        set => this.RaiseAndSetIfChanged(ref _fixStatus, value);
    }

    private string _totalDownloadSize = "";
    public string TotalDownloadSize
    {
        get => _totalDownloadSize;
        set => this.RaiseAndSetIfChanged(ref _totalDownloadSize, value);
    }

    /// <summary>
    /// Load component status from ComponentManager and run environment checks.
    /// </summary>
    public async Task LoadComponentsAsync()
    {
        IsLoading = true;

        try
        {
            // Run environment checks first
            var envResults = await _environmentChecker.RunAllChecksAsync();

            EnvironmentChecks.Clear();
            foreach (var result in envResults)
            {
                EnvironmentChecks.Add(new EnvironmentCheckViewModel(result, this));
            }

            HasEnvironmentIssues = envResults.Any(r => r.Status != CheckStatus.Passed);
            HasEnvironmentFailures = envResults.Any(r => r.Status == CheckStatus.Failed);

            // Detect AI clients
            var aiClients = await _aiAssistantService.DetectClientsAsync();
            AiClients.Clear();
            foreach (var client in aiClients)
            {
                AiClients.Add(new AiClientViewModel(client, this));
            }
            HasAnyAiClient = aiClients.Exists(c => c.IsInstalled);

            // Auto-enable MCP if clients are detected and setting is null (first run)
            var mcpSetting = AppSettings.Instance.EnableMcpServer;
            if (mcpSetting == null && HasAnyAiClient)
            {
                // Auto-enable for first-time users with AI clients
                IsMcpEnabled = true;
                ModkitLog.Info("[Setup] Auto-enabled MCP server (AI client detected)");
            }
            else
            {
                _isMcpEnabled = mcpSetting ?? false;
                this.RaisePropertyChanged(nameof(IsMcpEnabled));
            }

            // Then load component status
            var statuses = await _componentManager.GetComponentStatusAsync(forceRemoteFetch: true);

            RequiredComponents.Clear();
            OptionalComponents.Clear();

            foreach (var status in statuses)
            {
                // Optional components get a callback to update pending status when selection changes
                var vm = new ComponentStatusViewModel(status, status.Required ? null : UpdatePendingStatus);
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

            SetDownloadState(DownloadState.Downloading, $"Downloading {toDownload.Count} component(s)...");

            var progress = new Progress<MultiDownloadProgress>(p =>
            {
                CurrentComponent = p.CurrentComponent;
                _downloadState = DownloadState.Downloading;
                DownloadStatus = $"Downloading {p.CurrentComponent}: {p.Message}";
                this.RaisePropertyChanged(nameof(HasDownloadStatus));
                this.RaisePropertyChanged(nameof(DownloadStatusIcon));
                this.RaisePropertyChanged(nameof(DownloadStatusColor));
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

            ModkitLog.Info($"[Setup] Download result: {(success ? "success" : "failed")}");

            // Refresh component status
            await LoadComponentsAsync();

            ModkitLog.Info($"[Setup] After refresh - HasRequiredPending: {HasRequiredPending}");

            if (success && !HasRequiredPending)
            {
                SetDownloadState(DownloadState.Success, "All components installed!");
                await Task.Delay(500);
                SetupComplete?.Invoke();
            }
            else if (!success)
            {
                SetDownloadState(DownloadState.Failed, "Some downloads failed. Check log for details.");
                ModkitLog.Warn("[Setup] Some downloads failed");
            }
            else if (HasRequiredPending)
            {
                // Download succeeded but still have pending requirements - show what's still needed
                var pending = RequiredComponents.Where(c => c.Status.State != ComponentState.UpToDate).ToList();
                var pendingNames = string.Join(", ", pending.Select(c => c.Name));
                SetDownloadState(DownloadState.Failed, $"Still need: {pendingNames}");
                ModkitLog.Warn($"[Setup] Download succeeded but still pending: {pendingNames}");
            }
        }
        catch (OperationCanceledException)
        {
            SetDownloadState(DownloadState.Cancelled, "Download cancelled.");
        }
        catch (Exception ex)
        {
            SetDownloadState(DownloadState.Failed, $"Error: {ex.Message}");
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

    /// <summary>
    /// Execute an auto-fix action for an environment check.
    /// </summary>
    public async Task ExecuteAutoFixAsync(AutoFixAction action)
    {
        if (IsFixing) return;

        IsFixing = true;
        FixStatus = "";

        try
        {
            var success = await _environmentChecker.ExecuteAutoFixAsync(action, msg =>
            {
                FixStatus = msg;
            });

            if (success)
            {
                FixStatus = "Fix applied. Re-checking environment...";
                await Task.Delay(500);
                // Reload to check if fix worked
                await LoadComponentsAsync();
            }
        }
        catch (Exception ex)
        {
            FixStatus = $"Error: {ex.Message}";
            ModkitLog.Error($"[SetupViewModel] Auto-fix failed: {ex.Message}");
        }
        finally
        {
            IsFixing = false;
        }
    }

    /// <summary>
    /// Open the diagnostic log file.
    /// </summary>
    public void OpenDiagnosticLog()
    {
        ModkitLog.OpenLogFile();
    }

    /// <summary>
    /// Write a full diagnostic report and open it.
    /// </summary>
    public async Task WriteDiagnosticReportAsync()
    {
        await _environmentChecker.WriteDiagnosticReportAsync();
        ModkitLog.OpenLogFile();
    }

    /// <summary>
    /// Open a URL in the default browser.
    /// </summary>
    public void OpenUrl(string url)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(url)
            {
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"Failed to open URL: {ex.Message}");
        }
    }

    /// <summary>
    /// Configure MCP for a specific AI client.
    /// </summary>
    public async Task ConfigureAiClientAsync(string clientName)
    {
        if (IsConfiguringAi) return;

        IsConfiguringAi = true;
        try
        {
            var success = await _aiAssistantService.ConfigureClientAsync(clientName);
            if (success)
            {
                ModkitLog.Info($"[Setup] Configured MCP for {clientName}");
                // Refresh client status
                var aiClients = await _aiAssistantService.DetectClientsAsync();
                AiClients.Clear();
                foreach (var client in aiClients)
                {
                    AiClients.Add(new AiClientViewModel(client, this));
                }
            }
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[Setup] Failed to configure {clientName}: {ex.Message}");
        }
        finally
        {
            IsConfiguringAi = false;
        }
    }
}

/// <summary>
/// ViewModel wrapper for AiClientStatus with UI-specific properties.
/// </summary>
public class AiClientViewModel : ViewModelBase
{
    private readonly SetupViewModel _parent;

    public AiClientViewModel(AiClientStatus status, SetupViewModel parent)
    {
        Status = status;
        _parent = parent;
    }

    public AiClientStatus Status { get; }

    public string Name => Status.Name;
    public string Description => Status.Description;
    public bool IsInstalled => Status.IsInstalled;
    public bool IsConfigured => Status.IsConfigured;
    public string? ConfigPath => Status.ConfigPath;
    public string? SetupDocsUrl => Status.SetupDocsUrl;

    public bool NeedsConfiguration => IsInstalled && !IsConfigured;
    public bool HasSetupDocs => !string.IsNullOrEmpty(SetupDocsUrl);

    public async Task ConfigureAsync()
    {
        await _parent.ConfigureAiClientAsync(Name);
    }

    public void OpenSetupDocs()
    {
        if (HasSetupDocs)
        {
            _parent.OpenUrl(SetupDocsUrl!);
        }
    }
}

/// <summary>
/// ViewModel wrapper for EnvironmentCheckResult with UI-specific properties.
/// </summary>
public class EnvironmentCheckViewModel : ViewModelBase
{
    private readonly SetupViewModel _parent;

    public EnvironmentCheckViewModel(EnvironmentCheckResult result, SetupViewModel parent)
    {
        Result = result;
        _parent = parent;
    }

    public EnvironmentCheckResult Result { get; }

    public string Name => Result.Name;
    public string Description => Result.Description;
    public string Details => Result.Details;
    public CheckStatus Status => Result.Status;
    public CheckCategory Category => Result.Category;
    public string? FixInstructions => Result.FixInstructions;
    public string? FixUrl => Result.FixUrl;
    public bool CanAutoFix => Result.CanAutoFix;
    public AutoFixAction AutoFixAction => Result.AutoFixAction;

    public bool IsPassed => Result.Status == CheckStatus.Passed;
    public bool IsWarning => Result.Status == CheckStatus.Warning;
    public bool IsFailed => Result.Status == CheckStatus.Failed;
    public bool HasIssue => Result.Status != CheckStatus.Passed;
    public bool HasFixUrl => !string.IsNullOrEmpty(Result.FixUrl);

    public async Task ExecuteAutoFixAsync()
    {
        await _parent.ExecuteAutoFixAsync(Result.AutoFixAction);
    }

    public void OpenFixUrl()
    {
        if (!string.IsNullOrEmpty(Result.FixUrl))
        {
            _parent.OpenUrl(Result.FixUrl);
        }
    }
}

/// <summary>
/// ViewModel wrapper for ComponentStatus with UI-specific properties.
/// </summary>
public class ComponentStatusViewModel : ViewModelBase
{
    private readonly Action? _onSelectionChanged;

    public ComponentStatusViewModel(ComponentStatus status, Action? onSelectionChanged = null)
    {
        Status = status;
        _onSelectionChanged = onSelectionChanged;
        IsSelected = false; // Optional components start unselected
    }

    public ComponentStatus Status { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isSelected, value) != value)
                return;
            _onSelectionChanged?.Invoke();
        }
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
