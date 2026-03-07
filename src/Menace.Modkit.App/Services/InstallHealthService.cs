using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Service for computing and monitoring the overall health state of the Modkit installation.
/// Aggregates status from ComponentManager, ModpackManager, and other services to provide
/// a unified view of what's working, what's broken, and what actions are available.
/// </summary>
public class InstallHealthService
{
    private static readonly Lazy<InstallHealthService> _instance = new(() => new InstallHealthService());
    public static InstallHealthService Instance => _instance.Value;

    // Cached health status for quick access
    private InstallHealthStatus? _cachedStatus;
    private DateTime _lastCheck = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private InstallHealthService()
    {
    }

    /// <summary>
    /// Get the current installation health status.
    /// Uses cached result if checked recently (within 30 seconds).
    /// </summary>
    /// <param name="forceRefresh">If true, bypass cache and recompute status.</param>
    public async Task<InstallHealthStatus> GetCurrentHealthAsync(bool forceRefresh = false)
    {
        // Check cache validity
        if (!forceRefresh && _cachedStatus != null && DateTime.UtcNow - _lastCheck < CacheDuration)
        {
            return _cachedStatus;
        }

        try
        {
            var status = await ComputeHealthStatusAsync();
            _cachedStatus = status;
            _lastCheck = DateTime.UtcNow;

            LogHealthStatus(status);
            return status;
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[InstallHealthService] Failed to compute health status: {ex.Message}");

            // Return a degraded status on error
            return new InstallHealthStatus
            {
                State = InstallHealthState.NeedsRepair,
                BlockingReason = $"Health check failed: {ex.Message}",
                RequiredUserAction = "Try restarting the Modkit. If the issue persists, check the log file.",
                ComponentIssues = new List<string> { $"Health check error: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Invalidate the cached health status, forcing a fresh check on next call.
    /// </summary>
    public void InvalidateCache()
    {
        _cachedStatus = null;
        _lastCheck = DateTime.MinValue;
        ModkitLog.Info("[InstallHealthService] Cache invalidated");
    }

    /// <summary>
    /// Get a formatted diagnostic summary suitable for logging or display.
    /// </summary>
    public string GetDiagnosticSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Install Health Diagnostic Summary ===");
        sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // Game path
        var gamePath = AppSettings.Instance.GameInstallPath;
        sb.AppendLine($"Game Path: {(string.IsNullOrEmpty(gamePath) ? "(not set)" : gamePath)}");
        sb.AppendLine($"Game Path Exists: {(!string.IsNullOrEmpty(gamePath) && Directory.Exists(gamePath))}");
        sb.AppendLine();

        // Cached status
        if (_cachedStatus != null)
        {
            sb.AppendLine($"Current State: {_cachedStatus.State}");
            sb.AppendLine($"Can Deploy: {_cachedStatus.CanDeploy}");
            sb.AppendLine($"Can Extract: {_cachedStatus.CanExtract}");
            sb.AppendLine($"Requires User Intervention: {_cachedStatus.RequiresUserIntervention}");

            if (!string.IsNullOrEmpty(_cachedStatus.BlockingReason))
            {
                sb.AppendLine($"Blocking Reason: {_cachedStatus.BlockingReason}");
            }

            if (_cachedStatus.ComponentIssues.Count > 0)
            {
                sb.AppendLine("Component Issues:");
                foreach (var issue in _cachedStatus.ComponentIssues)
                {
                    sb.AppendLine($"  - {issue}");
                }
            }
        }
        else
        {
            sb.AppendLine("Status: Not yet computed");
        }

        sb.AppendLine();

        // Component status
        sb.AppendLine("=== Component Status ===");
        try
        {
            var componentManager = ComponentManager.Instance;
            sb.AppendLine($"MelonLoader Path: {componentManager.GetMelonLoaderPath() ?? "(not found)"}");
            sb.AppendLine($"DataExtractor Path: {componentManager.GetDataExtractorPath() ?? "(not found)"}");
            sb.AppendLine($"ModpackLoader Path: {componentManager.GetModpackLoaderPath() ?? "(not found)"}");
            sb.AppendLine($"Has Staged Update: {componentManager.HasStagedUpdate()}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Error reading component status: {ex.Message}");
        }

        sb.AppendLine();

        // Backup status
        sb.AppendLine("=== Backup Status ===");
        if (!string.IsNullOrEmpty(gamePath) && Directory.Exists(gamePath))
        {
            var gameDataDirs = Directory.GetDirectories(gamePath, "*_Data");
            if (gameDataDirs.Length > 0)
            {
                var gameDataDir = gameDataDirs[0];

                // Check for backup metadata
                var metadata = BackupMetadata.LoadFrom(gameDataDir);
                if (metadata != null)
                {
                    sb.AppendLine($"  Metadata: backup-metadata.json found");
                    sb.AppendLine($"    Game Version: {metadata.GameVersion}");
                    sb.AppendLine($"    Backup Created: {metadata.BackupCreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                    sb.AppendLine($"    Modkit Version: {metadata.ModkitVersion}");
                    sb.AppendLine($"    Migrated from Legacy: {metadata.MigratedFromLegacy}");
                    sb.AppendLine($"    Files tracked: {metadata.FileHashes.Count}");

                    // Validate backups
                    var validation = metadata.ValidateBackups(gameDataDir);
                    sb.AppendLine($"    Validation: {(validation.IsValid ? "PASSED" : "FAILED")}");
                    if (!validation.IsValid)
                    {
                        sb.AppendLine($"    Issues: {validation.GetSummary()}");
                    }
                }
                else
                {
                    sb.AppendLine($"  Metadata: backup-metadata.json (not found)");
                }

                var backupFiles = new[] { "resources.assets.original", "globalgamemanagers.original" };

                foreach (var backupName in backupFiles)
                {
                    var backupPath = Path.Combine(gameDataDir, backupName);
                    if (File.Exists(backupPath))
                    {
                        var info = new FileInfo(backupPath);
                        sb.AppendLine($"  {backupName}: {info.Length / 1024 / 1024}MB, modified {info.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
                    }
                    else
                    {
                        sb.AppendLine($"  {backupName}: (not found)");
                    }
                }
            }
            else
            {
                sb.AppendLine("  No game data directory found");
            }
        }
        else
        {
            sb.AppendLine("  Cannot check backups (game path not set or doesn't exist)");
        }

        sb.AppendLine();
        sb.AppendLine("=== End Diagnostic Summary ===");

        return sb.ToString();
    }

    /// <summary>
    /// Compute the current health status by checking all subsystems.
    /// </summary>
    private async Task<InstallHealthStatus> ComputeHealthStatusAsync()
    {
        var componentIssues = new List<string>();

        // Priority 1: Check for staged self-update (non-blocking but important to notify)
        if (ComponentManager.Instance.HasStagedUpdate())
        {
            return InstallHealthStatus.CreateUpdatePendingRestart();
        }

        // Priority 2: Check game path configuration
        var gamePath = AppSettings.Instance.GameInstallPath;
        if (string.IsNullOrEmpty(gamePath))
        {
            componentIssues.Add("Game installation path not configured");
            return InstallHealthStatus.CreateNeedsSetup(
                "Game installation path is not set.",
                componentIssues);
        }

        if (!Directory.Exists(gamePath))
        {
            componentIssues.Add($"Game path does not exist: {gamePath}");
            return InstallHealthStatus.CreateNeedsSetup(
                "The configured game installation path does not exist.",
                componentIssues);
        }

        // Priority 3: Check required components
        var componentStatus = await CheckComponentStatusAsync();
        componentIssues.AddRange(componentStatus.Issues);

        if (componentStatus.NeedsSetup)
        {
            return InstallHealthStatus.CreateNeedsSetup(
                componentStatus.Summary,
                componentIssues);
        }

        // Priority 4: Check backup validity
        var backupStatus = CheckBackupStatus(gamePath);
        componentIssues.AddRange(backupStatus.Issues);

        if (backupStatus.BackupsCorrupted)
        {
            return InstallHealthStatus.CreateReacquireRequired(
                "Backup files appear corrupted. Steam verification is required to restore vanilla game files.");
        }

        if (backupStatus.BackupsStale)
        {
            // Backups exist but are from an old game version
            // Restoring old backups is unsafe - user must verify game files via Steam first
            return InstallHealthStatus.CreateReacquireRequired(
                "Backup files are from an older game version. Steam verification is required to restore vanilla game files before re-deploying.");
        }

        // Priority 5: Check for legacy install patterns
        // TODO: Delegate to LegacyInstallDetector when it exists (Phase 0.5)
        var legacyStatus = CheckLegacyInstallPatterns(gamePath);
        if (legacyStatus.IsLegacy)
        {
            return InstallHealthStatus.CreateLegacyInstallDetected(legacyStatus.Reason);
        }

        // Priority 6: Check deploy state
        var deployStatus = CheckDeployState(gamePath);
        componentIssues.AddRange(deployStatus.Issues);

        if (deployStatus.HasBlocker)
        {
            return InstallHealthStatus.CreateDeployBlocked(deployStatus.BlockerReason);
        }

        // Priority 7: If there are issues but nothing blocking, report as needing repair
        if (componentIssues.Count > 0)
        {
            return InstallHealthStatus.CreateNeedsRepair(
                "Some issues were detected but deployment may still work.",
                componentIssues);
        }

        // All checks passed
        return InstallHealthStatus.CreateHealthy();
    }

    /// <summary>
    /// Check status of required components.
    /// </summary>
    private async Task<(bool NeedsSetup, string Summary, List<string> Issues)> CheckComponentStatusAsync()
    {
        var issues = new List<string>();
        var needsSetup = false;

        try
        {
            // Use bundled manifest for offline-safe check
            var componentStatuses = await ComponentManager.Instance.GetComponentStatusAsync(useBundledManifestOnly: true);

            foreach (var status in componentStatuses.Where(s => s.Required))
            {
                switch (status.State)
                {
                    case ComponentState.NotInstalled:
                        issues.Add($"{status.Name}: Not installed");
                        needsSetup = true;
                        break;

                    case ComponentState.Outdated:
                        issues.Add($"{status.Name}: Incompatible version (installed: {status.InstalledVersion}, required: {status.LatestVersion})");
                        needsSetup = true;
                        break;

                    case ComponentState.UpdateAvailable:
                        // UpdateAvailable is informational, not blocking
                        // Don't add to issues, just note it
                        break;

                    case ComponentState.UpToDate:
                        // All good
                        break;
                }
            }

            // Check MelonLoader specifically (may be installed in game directory, not as component)
            var mlPath = ComponentManager.Instance.GetMelonLoaderPath();
            if (mlPath == null)
            {
                var gamePath = AppSettings.Instance.GameInstallPath;
                if (!string.IsNullOrEmpty(gamePath))
                {
                    var mlGameDir = Path.Combine(gamePath, "MelonLoader");
                    if (!Directory.Exists(mlGameDir))
                    {
                        if (!issues.Any(i => i.Contains("MelonLoader")))
                        {
                            issues.Add("MelonLoader: Not installed in game directory");
                            needsSetup = true;
                        }
                    }
                }
            }

            // NOTE: Il2CppAssemblies check removed from health check
            // These are generated when the game first runs with MelonLoader, so they won't exist for new installs
            // Instead, we check for them at code compilation time where they're actually needed
            // See: CodeModCompilationService for the runtime check

            var summary = needsSetup
                ? $"Required components missing or outdated: {string.Join(", ", issues.Take(3))}"
                : "All required components available";

            return (needsSetup, summary, issues);
        }
        catch (Exception ex)
        {
            issues.Add($"Component check failed: {ex.Message}");
            return (true, "Failed to check component status", issues);
        }
    }

    /// <summary>
    /// Check the validity of backup files (.original files in game data directory).
    /// Uses backup-metadata.json for validation when available, falls back to heuristics.
    /// </summary>
    private (bool BackupsCorrupted, bool BackupsStale, List<string> Issues) CheckBackupStatus(string gamePath)
    {
        var issues = new List<string>();
        var backupsCorrupted = false;
        var backupsStale = false;

        var gameDataDirs = Directory.GetDirectories(gamePath, "*_Data");
        if (gameDataDirs.Length == 0)
        {
            // No game data directory - might be wrong path, but that's handled elsewhere
            return (false, false, issues);
        }

        var gameDataDir = gameDataDirs[0];

        // Try to use backup-metadata.json for validation (preferred method)
        var metadata = BackupMetadata.LoadFrom(gameDataDir);
        if (metadata != null)
        {
            // Validate backups using stored metadata (hashes and sizes)
            var validationResult = metadata.ValidateBackups(gameDataDir);
            if (!validationResult.IsValid)
            {
                backupsCorrupted = true;
                issues.Add($"Backup validation failed: {validationResult.GetSummary()}");
            }

            // Check for staleness by comparing game versions
            var currentGameVersion = DetectGameVersionFromDataDir(gameDataDir);
            if (!string.IsNullOrEmpty(currentGameVersion) && metadata.IsBackupStale(currentGameVersion))
            {
                backupsStale = true;
                issues.Add($"Backups are from game version {metadata.GameVersion}, current version is {currentGameVersion}");
            }

            return (backupsCorrupted, backupsStale, issues);
        }

        // Fallback: No metadata file, use legacy heuristic-based validation
        // Expected minimum sizes for vanilla game files
        var expectedMinSizes = new Dictionary<string, long>
        {
            { "resources.assets.original", 500 * 1024 * 1024 }, // ~518MB for vanilla
            { "globalgamemanagers.original", 5 * 1024 * 1024 }  // ~6MB for vanilla
        };

        foreach (var (backupName, minSize) in expectedMinSizes)
        {
            var backupPath = Path.Combine(gameDataDir, backupName);
            if (File.Exists(backupPath))
            {
                var fileInfo = new FileInfo(backupPath);
                if (fileInfo.Length < minSize)
                {
                    issues.Add($"{backupName}: File too small ({fileInfo.Length / 1024 / 1024}MB, expected >{minSize / 1024 / 1024}MB) - may be corrupted");
                    backupsCorrupted = true;
                }

                // Check if backup is much older than the original file (potential staleness)
                var originalName = backupName.Replace(".original", "");
                var originalPath = Path.Combine(gameDataDir, originalName);
                if (File.Exists(originalPath))
                {
                    var originalInfo = new FileInfo(originalPath);
                    // If backup is more than 30 days older than original, flag as potentially stale
                    if (fileInfo.LastWriteTimeUtc < originalInfo.LastWriteTimeUtc.AddDays(-30))
                    {
                        issues.Add($"{backupName}: Backup may be from an older game version");
                        backupsStale = true;
                    }
                }
            }
            // Note: Missing backups are OK - they get created on first deploy
        }

        return (backupsCorrupted, backupsStale, issues);
    }

    /// <summary>
    /// Detect the game version from the globalgamemanagers file in the data directory.
    /// For staleness detection, we compare against the BACKUP file (.original) when it exists,
    /// since that represents the vanilla game state when backups were created.
    /// </summary>
    private static string? DetectGameVersionFromDataDir(string gameDataDir)
    {
        // Prefer .original backup for staleness comparison - this is the vanilla game state
        // When checking if backup metadata is stale, we need to compare metadata against
        // what the backup actually is (the .original file), not the patched live file.
        var originalGgmPath = Path.Combine(gameDataDir, "globalgamemanagers.original");
        var targetPath = File.Exists(originalGgmPath)
            ? originalGgmPath
            : Path.Combine(gameDataDir, "globalgamemanagers");

        if (!File.Exists(targetPath))
            return null;

        try
        {
            // Read the Unity version string from globalgamemanagers
            using var fs = File.OpenRead(targetPath);
            using var reader = new BinaryReader(fs);

            // Skip to version offset
            fs.Seek(0x14, SeekOrigin.Begin);
            var bytes = new List<byte>();
            byte b;
            while ((b = reader.ReadByte()) != 0 && bytes.Count < 50)
                bytes.Add(b);

            var unityVersion = System.Text.Encoding.ASCII.GetString(bytes.ToArray());
            var fileSize = new FileInfo(targetPath).Length;
            return $"{unityVersion}_{fileSize}";
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Check for legacy install patterns that need migration.
    /// </summary>
    private (bool IsLegacy, string Reason) CheckLegacyInstallPatterns(string gamePath)
    {
        var detector = new LegacyInstallDetector();
        var result = detector.Detect(gamePath);

        if (!result.IsLegacyInstall)
            return (false, string.Empty);

        // Build a user-facing reason from the detected issues
        var reason = result.DetectedIssues.Count switch
        {
            0 => "Legacy installation patterns detected.",
            1 => result.DetectedIssues[0],
            _ => $"{result.DetectedIssues.Count} legacy patterns detected: {result.DetectedIssues[0]}"
        };

        // Include confidence info if it's low (suggests a clean reset is better)
        if (result.ConfidenceScore < 0.5f)
        {
            reason += " A clean reinstall is recommended.";
        }

        return (true, reason);
    }

    /// <summary>
    /// Check the current deploy state for blockers.
    /// </summary>
    private (bool HasBlocker, string BlockerReason, List<string> Issues) CheckDeployState(string gamePath)
    {
        var issues = new List<string>();
        var hasBlocker = false;
        var blockerReason = string.Empty;

        // Check if there's a pending redeploy state
        var pendingRedeploy = PendingRedeployState.LoadFrom(gamePath);
        if (pendingRedeploy != null && pendingRedeploy.RedeployPending)
        {
            issues.Add("Pending redeploy from previous extraction");
            // This isn't a blocker, just informational
        }

        // Check if game might be running (simple heuristic: check for log file lock)
        var modsDir = Path.Combine(gamePath, "Mods");
        if (Directory.Exists(modsDir))
        {
            try
            {
                // Try to check MelonLoader log file
                var logFile = Path.Combine(gamePath, "MelonLoader", "Latest.log");
                if (File.Exists(logFile))
                {
                    // Try to open for exclusive access - if locked, game might be running
                    try
                    {
                        using var fs = File.Open(logFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                        // File opened successfully, game probably not running
                    }
                    catch (IOException)
                    {
                        // File is locked - game might be running
                        issues.Add("MelonLoader log file is locked (game may be running)");
                        // Don't make this a blocker since file locks can be stale
                    }
                }
            }
            catch
            {
                // Ignore errors in this check
            }
        }

        // Check for write permissions to Mods directory
        try
        {
            Directory.CreateDirectory(modsDir);
            var testFile = Path.Combine(modsDir, ".modkit_write_test");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
        }
        catch (UnauthorizedAccessException)
        {
            hasBlocker = true;
            blockerReason = "No write permission to the game's Mods folder.";
            issues.Add("Cannot write to Mods directory - permission denied");
        }
        catch (Exception ex)
        {
            issues.Add($"Mods directory write test failed: {ex.Message}");
        }

        return (hasBlocker, blockerReason, issues);
    }

    /// <summary>
    /// Log the health status for diagnostics.
    /// </summary>
    private void LogHealthStatus(InstallHealthStatus status)
    {
        var level = status.State switch
        {
            InstallHealthState.Healthy => "Info",
            InstallHealthState.UpdatePendingRestart => "Info",
            InstallHealthState.NeedsSetup => "Warn",
            InstallHealthState.NeedsRepair => "Warn",
            InstallHealthState.RepairableFromBackup => "Warn",
            _ => "Error"
        };

        var message = $"[InstallHealthService] State: {status.State}, CanDeploy: {status.CanDeploy}, CanExtract: {status.CanExtract}";

        switch (level)
        {
            case "Info":
                ModkitLog.Info(message);
                break;
            case "Warn":
                ModkitLog.Warn(message);
                break;
            default:
                ModkitLog.Error(message);
                break;
        }

        if (status.ComponentIssues.Count > 0)
        {
            foreach (var issue in status.ComponentIssues)
            {
                ModkitLog.Info($"[InstallHealthService]   - {issue}");
            }
        }
    }
}
