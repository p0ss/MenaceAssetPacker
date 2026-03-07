using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using Menace.Modkit.App.Models;
using ReactiveUI;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Observable wrapper around InstallHealthService that provides reactive UI bindings.
/// This service tracks the installation health state and notifies the UI when it changes.
/// </summary>
public sealed class AppHealthStateService : ReactiveObject, IDisposable
{
    private static readonly Lazy<AppHealthStateService> _instance = new(() => new AppHealthStateService());
    public static AppHealthStateService Instance => _instance.Value;

    private readonly InstallHealthService _healthService;
    private bool _isRefreshing;

    private AppHealthStateService()
    {
        _healthService = InstallHealthService.Instance;

        // Initialize with a default healthy state (will be updated on first refresh)
        _currentStatus = InstallHealthStatus.CreateHealthy();
    }

    private InstallHealthStatus _currentStatus;
    /// <summary>
    /// The current installation health status. Observable for UI binding.
    /// </summary>
    public InstallHealthStatus CurrentStatus
    {
        get => _currentStatus;
        private set
        {
            this.RaiseAndSetIfChanged(ref _currentStatus, value);
            this.RaisePropertyChanged(nameof(State));
            this.RaisePropertyChanged(nameof(ShortSummary));
            this.RaisePropertyChanged(nameof(BlockingReason));
            this.RaisePropertyChanged(nameof(RequiredUserAction));
            this.RaisePropertyChanged(nameof(CanDeploy));
            this.RaisePropertyChanged(nameof(CanExtract));
            this.RaisePropertyChanged(nameof(IsHealthy));
            this.RaisePropertyChanged(nameof(NeedsAttention));
            this.RaisePropertyChanged(nameof(HasBlockingIssue));
            this.RaisePropertyChanged(nameof(StatusSeverity));
            this.RaisePropertyChanged(nameof(StatusColor));
            this.RaisePropertyChanged(nameof(ShowStatusBar));
        }
    }

    /// <summary>
    /// True while a health check is in progress.
    /// </summary>
    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => this.RaiseAndSetIfChanged(ref _isRefreshing, value);
    }

    // Convenience properties for UI binding
    public InstallHealthState State => CurrentStatus.State;
    public string ShortSummary => CurrentStatus.ShortSummary;
    public string BlockingReason => CurrentStatus.BlockingReason;
    public string RequiredUserAction => CurrentStatus.RequiredUserAction;
    public bool CanDeploy => CurrentStatus.CanDeploy;
    public bool CanExtract => CurrentStatus.CanExtract;
    public bool IsHealthy => CurrentStatus.State == InstallHealthState.Healthy;
    public bool NeedsAttention => CurrentStatus.State != InstallHealthState.Healthy;
    public bool HasBlockingIssue => CurrentStatus.RequiresUserIntervention;

    /// <summary>
    /// Severity level for UI styling: "info", "warning", or "error".
    /// </summary>
    public string StatusSeverity => CurrentStatus.State switch
    {
        InstallHealthState.Healthy => "info",
        InstallHealthState.UpdatePendingRestart => "info",
        InstallHealthState.NeedsSetup => "warning",
        InstallHealthState.NeedsRepair => "warning",
        InstallHealthState.RepairableFromBackup => "warning",
        _ => "error"
    };

    /// <summary>
    /// Color hex code for status display.
    /// </summary>
    public string StatusColor => StatusSeverity switch
    {
        "info" => "#8ECDC8",    // Teal/green for healthy
        "warning" => "#FFB74D", // Orange for warnings
        _ => "#FF6B6B"          // Red for errors
    };

    /// <summary>
    /// Whether to show the status bar. Hidden when healthy.
    /// </summary>
    public bool ShowStatusBar => CurrentStatus.State != InstallHealthState.Healthy;

    /// <summary>
    /// Event raised when health status changes.
    /// </summary>
    public event EventHandler<InstallHealthStatus>? HealthStatusChanged;

    /// <summary>
    /// Refresh the health status from the underlying service.
    /// </summary>
    /// <param name="forceRefresh">If true, bypass cache and recompute status.</param>
    public async Task RefreshAsync(bool forceRefresh = false)
    {
        if (IsRefreshing) return;

        IsRefreshing = true;
        try
        {
            var status = await _healthService.GetCurrentHealthAsync(forceRefresh);

            // Update on UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                CurrentStatus = status;
                HealthStatusChanged?.Invoke(this, status);
            });
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[AppHealthStateService] Failed to refresh health status: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>
    /// Invalidate the cached health status and trigger a refresh.
    /// Call this after actions that may change the installation state:
    /// - Deploy/Undeploy
    /// - Setup completion
    /// - Component installation
    /// - Game path changes
    /// </summary>
    public async Task InvalidateAndRefreshAsync()
    {
        _healthService.InvalidateCache();
        await RefreshAsync(forceRefresh: true);
    }

    /// <summary>
    /// Invalidate the cache without triggering an immediate refresh.
    /// The next read will trigger a fresh check.
    /// </summary>
    public void InvalidateCache()
    {
        _healthService.InvalidateCache();
    }

    /// <summary>
    /// Get a diagnostic summary for troubleshooting.
    /// </summary>
    public string GetDiagnosticSummary()
    {
        return _healthService.GetDiagnosticSummary();
    }

    public void Dispose()
    {
        // Nothing to dispose currently
    }
}
