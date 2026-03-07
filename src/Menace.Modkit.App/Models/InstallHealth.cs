using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Menace.Modkit.App.Models;

/// <summary>
/// Represents the overall health state of the Modkit installation.
/// Used to determine what actions are available and what user intervention may be required.
/// </summary>
public enum InstallHealthState
{
    /// <summary>
    /// All components installed, backups valid, ready to deploy.
    /// </summary>
    Healthy,

    /// <summary>
    /// Fresh install or missing required components.
    /// </summary>
    NeedsSetup,

    /// <summary>
    /// Something broken but automatically fixable.
    /// </summary>
    NeedsRepair,

    /// <summary>
    /// Can restore from .original backup files.
    /// </summary>
    RepairableFromBackup,

    /// <summary>
    /// Backups invalid or stale, need Steam verify to reacquire vanilla files.
    /// </summary>
    ReacquireRequired,

    /// <summary>
    /// Cannot deploy due to a specific blocker (e.g., game running, permissions issue).
    /// </summary>
    DeployBlocked,

    /// <summary>
    /// Self-update has been staged, requires app restart to apply.
    /// </summary>
    UpdatePendingRestart,

    /// <summary>
    /// Old install format detected that needs migration (Phase 0.5).
    /// </summary>
    LegacyInstallDetected
}

/// <summary>
/// Detailed status information about the current installation health.
/// Provides human-readable explanations and guidance for resolving issues.
/// </summary>
public class InstallHealthStatus
{
    /// <summary>
    /// The current health state.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public InstallHealthState State { get; set; } = InstallHealthState.Healthy;

    /// <summary>
    /// Human-readable explanation of the current state.
    /// </summary>
    public string BlockingReason { get; set; } = string.Empty;

    /// <summary>
    /// What the user needs to do to resolve the issue (if any).
    /// </summary>
    public string RequiredUserAction { get; set; } = string.Empty;

    /// <summary>
    /// List of per-component issues detected during health check.
    /// </summary>
    public List<string> ComponentIssues { get; set; } = new();

    /// <summary>
    /// Whether deployment is currently possible.
    /// Only true when state is Healthy.
    /// </summary>
    public bool CanDeploy => State == InstallHealthState.Healthy;

    /// <summary>
    /// Whether extraction can proceed.
    /// True when state is Healthy or NeedsRepair (extraction may help fix issues).
    /// </summary>
    public bool CanExtract => State == InstallHealthState.Healthy ||
                               State == InstallHealthState.NeedsRepair ||
                               State == InstallHealthState.RepairableFromBackup;

    /// <summary>
    /// Whether the current state requires user intervention to resolve.
    /// True for states that cannot be automatically fixed.
    /// </summary>
    public bool RequiresUserIntervention => State == InstallHealthState.ReacquireRequired ||
                                             State == InstallHealthState.LegacyInstallDetected ||
                                             State == InstallHealthState.DeployBlocked;

    /// <summary>
    /// Whether the installation is in a working state (can do most operations).
    /// </summary>
    public bool IsOperational => State == InstallHealthState.Healthy ||
                                  State == InstallHealthState.UpdatePendingRestart;

    /// <summary>
    /// Whether there are any component issues to report.
    /// </summary>
    public bool HasComponentIssues => ComponentIssues.Count > 0;

    /// <summary>
    /// Get a short status summary suitable for display in a status bar.
    /// </summary>
    public string ShortSummary => State switch
    {
        InstallHealthState.Healthy => "Ready",
        InstallHealthState.NeedsSetup => "Setup Required",
        InstallHealthState.NeedsRepair => "Repair Needed",
        InstallHealthState.RepairableFromBackup => "Backup Available",
        InstallHealthState.ReacquireRequired => "Steam Verify Required",
        InstallHealthState.DeployBlocked => "Deploy Blocked",
        InstallHealthState.UpdatePendingRestart => "Restart to Update",
        InstallHealthState.LegacyInstallDetected => "Migration Required",
        _ => "Unknown"
    };

    /// <summary>
    /// Creates a healthy status with no issues.
    /// </summary>
    public static InstallHealthStatus CreateHealthy()
    {
        return new InstallHealthStatus
        {
            State = InstallHealthState.Healthy,
            BlockingReason = string.Empty,
            RequiredUserAction = string.Empty
        };
    }

    /// <summary>
    /// Creates a status indicating setup is required.
    /// </summary>
    public static InstallHealthStatus CreateNeedsSetup(string reason, List<string>? componentIssues = null)
    {
        return new InstallHealthStatus
        {
            State = InstallHealthState.NeedsSetup,
            BlockingReason = reason,
            RequiredUserAction = "Complete the setup wizard to install required components.",
            ComponentIssues = componentIssues ?? new List<string>()
        };
    }

    /// <summary>
    /// Creates a status indicating repair is needed.
    /// </summary>
    public static InstallHealthStatus CreateNeedsRepair(string reason, List<string>? componentIssues = null)
    {
        return new InstallHealthStatus
        {
            State = InstallHealthState.NeedsRepair,
            BlockingReason = reason,
            RequiredUserAction = "Run the repair wizard to fix installation issues.",
            ComponentIssues = componentIssues ?? new List<string>()
        };
    }

    /// <summary>
    /// Creates a status indicating restoration from backup is possible.
    /// </summary>
    public static InstallHealthStatus CreateRepairableFromBackup(string reason)
    {
        return new InstallHealthStatus
        {
            State = InstallHealthState.RepairableFromBackup,
            BlockingReason = reason,
            RequiredUserAction = "Use 'Restore Original' to recover game files from backup."
        };
    }

    /// <summary>
    /// Creates a status indicating Steam verification is required.
    /// </summary>
    public static InstallHealthStatus CreateReacquireRequired(string reason)
    {
        return new InstallHealthStatus
        {
            State = InstallHealthState.ReacquireRequired,
            BlockingReason = reason,
            RequiredUserAction = "Verify game files via Steam: Right-click game > Properties > Installed Files > Verify integrity of game files."
        };
    }

    /// <summary>
    /// Creates a status indicating deployment is blocked.
    /// </summary>
    public static InstallHealthStatus CreateDeployBlocked(string reason)
    {
        return new InstallHealthStatus
        {
            State = InstallHealthState.DeployBlocked,
            BlockingReason = reason,
            RequiredUserAction = "Resolve the blocking issue before deploying."
        };
    }

    /// <summary>
    /// Creates a status indicating an update is pending restart.
    /// </summary>
    public static InstallHealthStatus CreateUpdatePendingRestart()
    {
        return new InstallHealthStatus
        {
            State = InstallHealthState.UpdatePendingRestart,
            BlockingReason = "A Modkit update has been downloaded and is ready to install.",
            RequiredUserAction = "Restart the Modkit to apply the update."
        };
    }

    /// <summary>
    /// Creates a status indicating a legacy install was detected.
    /// </summary>
    public static InstallHealthStatus CreateLegacyInstallDetected(string reason)
    {
        return new InstallHealthStatus
        {
            State = InstallHealthState.LegacyInstallDetected,
            BlockingReason = reason,
            RequiredUserAction = "Run the migration wizard to update your installation to the new format."
        };
    }
}
