using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Runs diagnostic checks against the install environment.
/// Used by testers to validate the modkit is working correctly on their platform.
/// </summary>
public class DiagnosticService
{
    public static DiagnosticService Instance { get; } = new();

    /// <summary>
    /// Run non-destructive diagnostic checks (safe to run anytime).
    /// </summary>
    public async Task<DiagnosticReport> RunDryAsync(
        IProgress<DiagnosticProgress>? progress = null,
        CancellationToken ct = default)
    {
        return await RunChecksAsync(GetDryRunChecks(), "Dry Run", progress, ct);
    }

    /// <summary>
    /// Run destructive diagnostic tests (deploys and undeploys a test modpack).
    /// WARNING: This modifies game files and should only be run when user understands the risk.
    /// </summary>
    public async Task<DiagnosticReport> RunDestructiveAsync(
        ModpackManager modpackManager,
        IProgress<DiagnosticProgress>? progress = null,
        CancellationToken ct = default)
    {
        var checks = GetDryRunChecks();
        // Add destructive tests after dry-run checks
        checks.Add(ct => TestDeployUndeployCycleAsync(modpackManager, ct));
        checks.Add(ct => TestTransactionRollbackAsync(modpackManager, ct));

        return await RunChecksAsync(checks, "Destructive", progress, ct);
    }

    /// <summary>
    /// Run all diagnostic checks (legacy method, runs dry checks only).
    /// </summary>
    public async Task<DiagnosticReport> RunAllAsync(
        IProgress<DiagnosticProgress>? progress = null,
        CancellationToken ct = default)
    {
        return await RunDryAsync(progress, ct);
    }

    private List<Func<CancellationToken, Task<DiagnosticCheck>>> GetDryRunChecks()
    {
        return new List<Func<CancellationToken, Task<DiagnosticCheck>>>
        {
            ct => CheckGamePathAsync(ct),
            ct => CheckInstallHealthAsync(ct),
            ct => CheckLegacyInstallAsync(ct),
            ct => CheckComponentManifestAsync(ct),
            ct => CheckComponentProvenanceAsync(ct),
            ct => CheckBackupIntegrityAsync(ct),
            ct => CheckTransactionCapabilityAsync(ct),
            ct => CheckFilePermissionsAsync(ct),
            ct => CheckModpacksAsync(ct)
        };
    }

    private async Task<DiagnosticReport> RunChecksAsync(
        List<Func<CancellationToken, Task<DiagnosticCheck>>> checks,
        string mode,
        IProgress<DiagnosticProgress>? progress,
        CancellationToken ct)
    {
        var report = new DiagnosticReport
        {
            StartTime = DateTime.UtcNow,
            Platform = Environment.OSVersion.ToString(),
            ModkitVersion = ModkitVersion.AppFull,
            RuntimeVersion = Environment.Version.ToString(),
            Mode = mode
        };

        int completed = 0;
        int total = checks.Count;

        foreach (var check in checks)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var result = await check(ct);
                report.Checks.Add(result);

                progress?.Report(new DiagnosticProgress
                {
                    CurrentCheck = result.Name,
                    Completed = ++completed,
                    Total = total,
                    LastResult = result.Status
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                report.Checks.Add(new DiagnosticCheck
                {
                    Name = "Unknown",
                    Status = DiagnosticStatus.Error,
                    Message = $"Check threw exception: {ex.Message}",
                    Details = ex.ToString()
                });
                completed++;
            }
        }

        report.EndTime = DateTime.UtcNow;
        report.Summary = GenerateSummary(report);

        return report;
    }

    private async Task<DiagnosticCheck> CheckGamePathAsync(CancellationToken ct)
    {
        await Task.Yield();

        var check = new DiagnosticCheck { Name = "Game Path" };
        var gamePath = AppSettings.Instance.GameInstallPath;

        if (string.IsNullOrEmpty(gamePath))
        {
            check.Status = DiagnosticStatus.Skipped;
            check.Message = "No game path configured";
            return check;
        }

        if (!Directory.Exists(gamePath))
        {
            check.Status = DiagnosticStatus.Fail;
            check.Message = $"Game path does not exist: {gamePath}";
            return check;
        }

        // Check for game data directory
        var dataDirs = Directory.GetDirectories(gamePath, "*_Data");
        if (dataDirs.Length == 0)
        {
            check.Status = DiagnosticStatus.Fail;
            check.Message = "No *_Data directory found - may not be a Unity game";
            check.Details = $"Path: {gamePath}";
            return check;
        }

        // Check for Mods directory
        var modsPath = Path.Combine(gamePath, "Mods");
        var modsExist = Directory.Exists(modsPath);

        check.Status = DiagnosticStatus.Pass;
        check.Message = "Game path valid";
        check.Details = $"Path: {gamePath}\nData dir: {Path.GetFileName(dataDirs[0])}\nMods folder: {(modsExist ? "exists" : "not created yet")}";

        return check;
    }

    private async Task<DiagnosticCheck> CheckInstallHealthAsync(CancellationToken ct)
    {
        var check = new DiagnosticCheck { Name = "Install Health" };

        try
        {
            var health = await InstallHealthService.Instance.GetCurrentHealthAsync(forceRefresh: true);

            check.Details = $"State: {health.State}";

            if (health.ComponentIssues.Count > 0)
            {
                check.Details += $"\nIssues:\n- " + string.Join("\n- ", health.ComponentIssues);
            }

            switch (health.State)
            {
                case InstallHealthState.Healthy:
                    check.Status = DiagnosticStatus.Pass;
                    check.Message = "Install is healthy";
                    break;
                case InstallHealthState.NeedsSetup:
                    check.Status = DiagnosticStatus.Warn;
                    check.Message = "Setup required";
                    break;
                case InstallHealthState.NeedsRepair:
                    check.Status = DiagnosticStatus.Warn;
                    check.Message = "Install needs repair";
                    break;
                case InstallHealthState.LegacyInstallDetected:
                    check.Status = DiagnosticStatus.Warn;
                    check.Message = "Legacy installation detected";
                    break;
                case InstallHealthState.ReacquireRequired:
                    check.Status = DiagnosticStatus.Fail;
                    check.Message = "Steam verify required";
                    break;
                case InstallHealthState.DeployBlocked:
                    check.Status = DiagnosticStatus.Fail;
                    check.Message = "Deploy blocked";
                    break;
                default:
                    check.Status = DiagnosticStatus.Warn;
                    check.Message = $"State: {health.State}";
                    break;
            }
        }
        catch (Exception ex)
        {
            check.Status = DiagnosticStatus.Error;
            check.Message = "Failed to check install health";
            check.Details = ex.Message;
        }

        return check;
    }

    private async Task<DiagnosticCheck> CheckLegacyInstallAsync(CancellationToken ct)
    {
        await Task.Yield();

        var check = new DiagnosticCheck { Name = "Legacy Install Detection" };
        var gamePath = AppSettings.Instance.GameInstallPath;

        if (string.IsNullOrEmpty(gamePath))
        {
            check.Status = DiagnosticStatus.Skipped;
            check.Message = "No game path configured";
            return check;
        }

        try
        {
            var detector = new LegacyInstallDetector();
            var result = detector.Detect(gamePath);

            check.Details = $"Is legacy install: {result.IsLegacyInstall}\n" +
                $"Confidence score: {result.ConfidenceScore:P0}\n" +
                $"Issues detected: {result.DetectedIssues.Count}";

            if (result.IsLegacyInstall)
            {
                check.Status = DiagnosticStatus.Warn;
                check.Message = "Legacy installation patterns found";
                if (result.DetectedIssues.Count > 0)
                {
                    check.Details += $"\n\nDetected issues:\n- " +
                        string.Join("\n- ", result.DetectedIssues.Take(5));
                    if (result.DetectedIssues.Count > 5)
                        check.Details += $"\n... (+{result.DetectedIssues.Count - 5} more)";
                }
            }
            else
            {
                check.Status = DiagnosticStatus.Pass;
                check.Message = "No legacy patterns detected";
            }
        }
        catch (Exception ex)
        {
            check.Status = DiagnosticStatus.Error;
            check.Message = "Failed to check for legacy install";
            check.Details = ex.Message;
        }

        return check;
    }

    private async Task<DiagnosticCheck> CheckComponentManifestAsync(CancellationToken ct)
    {
        await Task.Yield();

        var check = new DiagnosticCheck { Name = "Component Manifest" };

        try
        {
            var allStatuses = await ComponentManager.Instance.GetComponentStatusAsync();
            var statusStrings = new List<string>();
            var requiredComponents = new[] { "MelonLoader", "DataExtractor", "ModpackLoader", "DotNetRefs", "AssetRipper" };

            foreach (var name in requiredComponents)
            {
                var status = allStatuses.FirstOrDefault(s =>
                    string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

                if (status == null)
                {
                    statusStrings.Add($"{name}: UNKNOWN");
                    continue;
                }

                var stateStr = status.State switch
                {
                    ComponentState.NotInstalled => "NOT_INSTALLED",
                    ComponentState.UpToDate => "OK",
                    ComponentState.Outdated => "OUTDATED",
                    ComponentState.UpdateAvailable => "UPDATE_AVAIL",
                    _ => status.State.ToString()
                };
                statusStrings.Add($"{name}: {stateStr} (v{status.InstalledVersion ?? "none"})");
            }

            var notInstalled = statusStrings.Count(s => s.Contains("NOT_INSTALLED"));
            var outdated = statusStrings.Count(s => s.Contains("OUTDATED"));

            check.Details = string.Join("\n", statusStrings);

            if (notInstalled > 0)
            {
                check.Status = DiagnosticStatus.Warn;
                check.Message = $"{notInstalled} component(s) not installed";
            }
            else if (outdated > 0)
            {
                check.Status = DiagnosticStatus.Warn;
                check.Message = $"{outdated} component(s) outdated";
            }
            else
            {
                check.Status = DiagnosticStatus.Pass;
                check.Message = "All components installed";
            }
        }
        catch (Exception ex)
        {
            check.Status = DiagnosticStatus.Error;
            check.Message = "Failed to check component manifest";
            check.Details = ex.Message;
        }

        return check;
    }

    private async Task<DiagnosticCheck> CheckComponentProvenanceAsync(CancellationToken ct)
    {
        var check = new DiagnosticCheck { Name = "Component Provenance" };

        try
        {
            var results = await ComponentManager.Instance.ValidateProvenanceAsync(ct);
            var summary = ComponentManager.Instance.GetProvenanceSummary();

            check.Details = $"Total: {summary.TotalComponents}\n" +
                $"With provenance: {summary.WithProvenance}\n" +
                $"Downloaded: {summary.Downloaded}\n" +
                $"Bundled: {summary.Bundled}\n" +
                $"Legacy: {summary.Legacy}";

            var issues = results.Where(r => r.Status == ProvenanceStatus.PathMissing ||
                                            r.Status == ProvenanceStatus.HashMismatch ||
                                            r.Status == ProvenanceStatus.MissingData).ToList();

            if (issues.Count > 0)
            {
                check.Status = DiagnosticStatus.Warn;
                check.Message = $"{issues.Count} provenance issue(s)";
                check.Details += "\n\nIssues:\n- " + string.Join("\n- ",
                    issues.Select(i => $"{i.ComponentName}: {i.Status} - {i.Message}"));
            }
            else if (summary.TotalComponents == 0)
            {
                check.Status = DiagnosticStatus.Skipped;
                check.Message = "No components installed";
            }
            else
            {
                check.Status = DiagnosticStatus.Pass;
                check.Message = "Component provenance valid";
            }
        }
        catch (Exception ex)
        {
            check.Status = DiagnosticStatus.Error;
            check.Message = "Failed to validate provenance";
            check.Details = ex.Message;
        }

        return check;
    }

    private async Task<DiagnosticCheck> CheckBackupIntegrityAsync(CancellationToken ct)
    {
        await Task.Yield();

        var check = new DiagnosticCheck { Name = "Backup Integrity" };
        var gamePath = AppSettings.Instance.GameInstallPath;

        if (string.IsNullOrEmpty(gamePath))
        {
            check.Status = DiagnosticStatus.Skipped;
            check.Message = "No game path configured";
            return check;
        }

        var gameDataDir = Directory.GetDirectories(gamePath, "*_Data").FirstOrDefault();
        if (string.IsNullOrEmpty(gameDataDir))
        {
            check.Status = DiagnosticStatus.Skipped;
            check.Message = "No game data directory found";
            return check;
        }

        try
        {
            var backupFiles = new[] { "resources.assets.original", "globalgamemanagers.original" };
            var foundBackups = new List<string>();
            var backupDetails = new List<string>();

            foreach (var backupName in backupFiles)
            {
                var backupPath = Path.Combine(gameDataDir, backupName);
                if (File.Exists(backupPath))
                {
                    var info = new FileInfo(backupPath);
                    foundBackups.Add(backupName);
                    backupDetails.Add($"{backupName}: {info.Length / 1024 / 1024}MB");
                }
            }

            if (foundBackups.Count == 0)
            {
                check.Status = DiagnosticStatus.Skipped;
                check.Message = "No backups found (mods not deployed)";
                return check;
            }

            // Check for backup metadata
            var metadataPath = Path.Combine(gameDataDir, "backup-metadata.json");
            var hasMetadata = File.Exists(metadataPath);
            backupDetails.Add($"backup-metadata.json: {(hasMetadata ? "present" : "MISSING")}");

            if (hasMetadata)
            {
                var metadata = BackupMetadata.LoadFrom(gameDataDir);
                if (metadata != null)
                {
                    backupDetails.Add($"Metadata game version: {metadata.GameVersion}");
                    backupDetails.Add($"Metadata created: {metadata.BackupCreatedAt:u}");

                    // Verify hashes
                    int hashMatches = 0;
                    int hashMismatches = 0;
                    foreach (var backupName in foundBackups)
                    {
                        var baseName = backupName.Replace(".original", "");
                        if (metadata.FileHashes.TryGetValue(baseName, out var expectedHash))
                        {
                            var actualHash = DeployState.ComputeFileHash(Path.Combine(gameDataDir, backupName));
                            if (string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                                hashMatches++;
                            else
                                hashMismatches++;
                        }
                    }
                    backupDetails.Add($"Hash verification: {hashMatches} match, {hashMismatches} mismatch");

                    if (hashMismatches > 0)
                    {
                        check.Status = DiagnosticStatus.Fail;
                        check.Message = $"{hashMismatches} backup(s) failed hash verification";
                    }
                    else
                    {
                        check.Status = DiagnosticStatus.Pass;
                        check.Message = "Backups verified";
                    }
                }
                else
                {
                    check.Status = DiagnosticStatus.Warn;
                    check.Message = "Failed to parse backup metadata";
                }
            }
            else
            {
                check.Status = DiagnosticStatus.Warn;
                check.Message = "Backups exist but no metadata (legacy backup)";
            }

            check.Details = string.Join("\n", backupDetails);
        }
        catch (Exception ex)
        {
            check.Status = DiagnosticStatus.Error;
            check.Message = "Failed to check backup integrity";
            check.Details = ex.Message;
        }

        return check;
    }

    private async Task<DiagnosticCheck> CheckTransactionCapabilityAsync(CancellationToken ct)
    {
        await Task.Yield();

        var check = new DiagnosticCheck { Name = "Transaction Capability" };
        var gamePath = AppSettings.Instance.GameInstallPath;

        if (string.IsNullOrEmpty(gamePath))
        {
            check.Status = DiagnosticStatus.Skipped;
            check.Message = "No game path configured";
            return check;
        }

        var modsPath = Path.Combine(gamePath, "Mods");

        try
        {
            // Test 1: Can create staging directory
            var stagingPath = Path.Combine(modsPath, ".deploy-staging-test");
            Directory.CreateDirectory(stagingPath);

            // Test 2: Can write files
            var testFile = Path.Combine(stagingPath, "test.txt");
            await File.WriteAllTextAsync(testFile, "diagnostic test", ct);

            // Test 3: Can read back
            var readBack = await File.ReadAllTextAsync(testFile, ct);
            var readOk = readBack == "diagnostic test";

            // Test 4: Can delete
            File.Delete(testFile);
            Directory.Delete(stagingPath);

            if (readOk)
            {
                check.Status = DiagnosticStatus.Pass;
                check.Message = "Transaction operations working";
                check.Details = "Create dir: OK\nWrite file: OK\nRead file: OK\nDelete: OK";
            }
            else
            {
                check.Status = DiagnosticStatus.Fail;
                check.Message = "File read/write mismatch";
                check.Details = $"Expected: 'diagnostic test'\nGot: '{readBack}'";
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            check.Status = DiagnosticStatus.Fail;
            check.Message = "Permission denied for transaction operations";
            check.Details = ex.Message;
        }
        catch (Exception ex)
        {
            check.Status = DiagnosticStatus.Error;
            check.Message = "Transaction capability check failed";
            check.Details = ex.Message;
        }

        return check;
    }

    private async Task<DiagnosticCheck> CheckFilePermissionsAsync(CancellationToken ct)
    {
        await Task.Yield();

        var check = new DiagnosticCheck { Name = "File Permissions" };
        var gamePath = AppSettings.Instance.GameInstallPath;

        if (string.IsNullOrEmpty(gamePath))
        {
            check.Status = DiagnosticStatus.Skipped;
            check.Message = "No game path configured";
            return check;
        }

        var issues = new List<string>();

        // Check game directory
        if (!CanWriteToDirectory(gamePath))
            issues.Add($"Cannot write to game directory: {gamePath}");

        // Check Mods directory
        var modsPath = Path.Combine(gamePath, "Mods");
        if (Directory.Exists(modsPath) && !CanWriteToDirectory(modsPath))
            issues.Add($"Cannot write to Mods directory: {modsPath}");

        // Check UserLibs directory
        var userLibsPath = Path.Combine(gamePath, "UserLibs");
        if (Directory.Exists(userLibsPath) && !CanWriteToDirectory(userLibsPath))
            issues.Add($"Cannot write to UserLibs directory: {userLibsPath}");

        // Check game data directory
        var gameDataDir = Directory.GetDirectories(gamePath, "*_Data").FirstOrDefault();
        if (!string.IsNullOrEmpty(gameDataDir) && !CanWriteToDirectory(gameDataDir))
            issues.Add($"Cannot write to game data directory: {gameDataDir}");

        if (issues.Count == 0)
        {
            check.Status = DiagnosticStatus.Pass;
            check.Message = "File permissions OK";
            check.Details = "All required directories are writable";
        }
        else
        {
            check.Status = DiagnosticStatus.Fail;
            check.Message = $"{issues.Count} permission issue(s)";
            check.Details = string.Join("\n", issues);
        }

        return check;
    }

    private async Task<DiagnosticCheck> CheckModpacksAsync(CancellationToken ct)
    {
        await Task.Yield();

        var check = new DiagnosticCheck { Name = "Modpacks" };

        try
        {
            // Standard staging path: ~/Documents/MenaceModkit/staging/
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var stagingPath = Path.Combine(documentsPath, "MenaceModkit", "staging");
            if (!Directory.Exists(stagingPath))
            {
                check.Status = DiagnosticStatus.Skipped;
                check.Message = "No staging directory found";
                return check;
            }

            var modpackDirs = Directory.GetDirectories(stagingPath)
                .Where(d => File.Exists(Path.Combine(d, "modpack.json")))
                .ToList();

            if (modpackDirs.Count == 0)
            {
                check.Status = DiagnosticStatus.Pass;
                check.Message = "No modpacks in staging";
                check.Details = $"Staging path: {stagingPath}";
                return check;
            }

            var details = new List<string> { $"Found {modpackDirs.Count} modpack(s):" };
            foreach (var dir in modpackDirs.Take(10))
            {
                var name = Path.GetFileName(dir);
                var manifestPath = Path.Combine(dir, "modpack.json");
                try
                {
                    var json = await File.ReadAllTextAsync(manifestPath, ct);
                    var doc = JsonDocument.Parse(json);
                    var version = doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() : "?";
                    details.Add($"  - {name} v{version}");
                }
                catch
                {
                    details.Add($"  - {name} (invalid manifest)");
                }
            }

            if (modpackDirs.Count > 10)
                details.Add($"  ... and {modpackDirs.Count - 10} more");

            check.Status = DiagnosticStatus.Pass;
            check.Message = $"{modpackDirs.Count} modpack(s) in staging";
            check.Details = string.Join("\n", details);
        }
        catch (Exception ex)
        {
            check.Status = DiagnosticStatus.Error;
            check.Message = "Failed to check modpacks";
            check.Details = ex.Message;
        }

        return check;
    }

    // ========== DESTRUCTIVE TESTS ==========
    // These tests actually deploy and undeploy, modifying game files.

    private async Task<DiagnosticCheck> TestDeployUndeployCycleAsync(ModpackManager modpackManager, CancellationToken ct)
    {
        var check = new DiagnosticCheck { Name = "Deploy/Undeploy Cycle (DESTRUCTIVE)" };
        var gamePath = AppSettings.Instance.GameInstallPath;

        if (string.IsNullOrEmpty(gamePath))
        {
            check.Status = DiagnosticStatus.Skipped;
            check.Message = "No game path configured";
            return check;
        }

        var modsPath = Path.Combine(gamePath, "Mods");
        var testModpackName = "_DiagnosticTestModpack";
        var testModpackPath = Path.Combine(modsPath, testModpackName);
        var stages = new List<string>();

        try
        {
            // Stage 1: Create a minimal test modpack in staging
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var stagingPath = Path.Combine(documentsPath, "MenaceModkit", "staging");
            if (!Directory.Exists(stagingPath))
            {
                Directory.CreateDirectory(stagingPath);
            }

            var testStagingPath = Path.Combine(stagingPath, testModpackName);
            if (Directory.Exists(testStagingPath))
                Directory.Delete(testStagingPath, true);

            Directory.CreateDirectory(testStagingPath);
            var manifest = new
            {
                name = testModpackName,
                version = "1.0.0-diagnostic",
                description = "Diagnostic test modpack - safe to delete",
                author = "DiagnosticService"
            };
            await File.WriteAllTextAsync(
                Path.Combine(testStagingPath, "modpack.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
                ct);
            stages.Add("Created test modpack in staging: OK");

            // Stage 2: Deploy the test modpack
            var deployManager = new DeployManager(modpackManager);
            var testManifest = new ModpackManifest
            {
                Name = testModpackName,
                Path = testStagingPath,
                Version = "1.0.0-diagnostic",
                LoadOrder = 9999
            };

            // Deploy single modpack (simpler for testing)
            var deployResult = await deployManager.DeploySingleAsync(testManifest, null, ct);
            if (!deployResult.Success)
            {
                stages.Add($"Deploy: FAILED - {deployResult.Message}");
                check.Status = DiagnosticStatus.Fail;
                check.Message = "Deploy failed";
                check.Details = string.Join("\n", stages);
                return check;
            }
            stages.Add("Deployed test modpack: OK");

            // Stage 3: Verify deployment
            if (!Directory.Exists(testModpackPath))
            {
                stages.Add("Verify deployment: FAILED - modpack directory not found");
                check.Status = DiagnosticStatus.Fail;
                check.Message = "Deployment verification failed";
                check.Details = string.Join("\n", stages);
                return check;
            }
            stages.Add("Verified deployment exists: OK");

            // Stage 4: Undeploy
            var undeployResult = await deployManager.UndeployAllAsync(null, ct);
            if (!undeployResult.Success)
            {
                stages.Add($"Undeploy: FAILED - {undeployResult.Message}");
                check.Status = DiagnosticStatus.Fail;
                check.Message = "Undeploy failed";
                check.Details = string.Join("\n", stages);
                return check;
            }
            stages.Add("Undeployed: OK");

            // Stage 5: Verify undeploy
            if (Directory.Exists(testModpackPath))
            {
                stages.Add("Verify undeploy: FAILED - modpack directory still exists");
                check.Status = DiagnosticStatus.Warn;
                check.Message = "Undeploy incomplete";
                check.Details = string.Join("\n", stages);
                return check;
            }
            stages.Add("Verified undeploy complete: OK");

            // Cleanup test modpack from staging
            if (Directory.Exists(testStagingPath))
                Directory.Delete(testStagingPath, true);
            stages.Add("Cleaned up test modpack: OK");

            check.Status = DiagnosticStatus.Pass;
            check.Message = "Full deploy/undeploy cycle succeeded";
            check.Details = string.Join("\n", stages);
        }
        catch (Exception ex)
        {
            stages.Add($"Exception: {ex.Message}");
            check.Status = DiagnosticStatus.Error;
            check.Message = "Deploy/undeploy cycle threw exception";
            check.Details = string.Join("\n", stages) + $"\n\n{ex}";

            // Attempt cleanup
            try
            {
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var testStagingPath = Path.Combine(documentsPath, "MenaceModkit", "staging", testModpackName);
                if (Directory.Exists(testStagingPath))
                    Directory.Delete(testStagingPath, true);
                if (Directory.Exists(testModpackPath))
                    Directory.Delete(testModpackPath, true);
            }
            catch { /* best effort */ }
        }

        return check;
    }

    private async Task<DiagnosticCheck> TestTransactionRollbackAsync(ModpackManager modpackManager, CancellationToken ct)
    {
        var check = new DiagnosticCheck { Name = "Transaction Rollback (DESTRUCTIVE)" };
        var gamePath = AppSettings.Instance.GameInstallPath;

        if (string.IsNullOrEmpty(gamePath))
        {
            check.Status = DiagnosticStatus.Skipped;
            check.Message = "No game path configured";
            return check;
        }

        var modsPath = Path.Combine(gamePath, "Mods");
        var gameDataDir = Directory.GetDirectories(gamePath, "*_Data").FirstOrDefault();

        if (string.IsNullOrEmpty(gameDataDir))
        {
            check.Status = DiagnosticStatus.Skipped;
            check.Message = "No game data directory found";
            return check;
        }

        var stages = new List<string>();

        try
        {
            // Create a transaction
            using var transaction = new DeployTransaction(modsPath, gameDataDir);
            transaction.Begin();
            stages.Add("Transaction started: OK");

            // Stage some test files
            var testDir = Path.Combine(Path.GetTempPath(), $"diag-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDir);
            await File.WriteAllTextAsync(Path.Combine(testDir, "test.txt"), "rollback test", ct);

            transaction.StageDirectory(testDir, "_RollbackTestDir");
            stages.Add("Staged test directory: OK");

            // Get the expected final path
            var finalPath = Path.Combine(modsPath, "_RollbackTestDir");

            // Verify it doesn't exist yet (staging only)
            if (Directory.Exists(finalPath))
            {
                stages.Add("Pre-commit check: FAILED - directory exists before commit");
                check.Status = DiagnosticStatus.Fail;
                check.Message = "Staging isolation failed";
                check.Details = string.Join("\n", stages);
                Directory.Delete(testDir, true);
                return check;
            }
            stages.Add("Pre-commit isolation verified: OK");

            // Simulate rollback (dispose without commit)
            transaction.Dispose();
            stages.Add("Transaction disposed (rollback): OK");

            // Verify rollback worked
            if (Directory.Exists(finalPath))
            {
                stages.Add("Rollback verification: FAILED - directory exists after rollback");
                check.Status = DiagnosticStatus.Fail;
                check.Message = "Rollback failed";
                check.Details = string.Join("\n", stages);
                // Clean up
                try { Directory.Delete(finalPath, true); } catch { }
                return check;
            }
            stages.Add("Rollback verified (no artifacts): OK");

            // Cleanup temp dir
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
            stages.Add("Cleaned up temp files: OK");

            check.Status = DiagnosticStatus.Pass;
            check.Message = "Transaction rollback works correctly";
            check.Details = string.Join("\n", stages);
        }
        catch (Exception ex)
        {
            stages.Add($"Exception: {ex.Message}");
            check.Status = DiagnosticStatus.Error;
            check.Message = "Transaction rollback test threw exception";
            check.Details = string.Join("\n", stages) + $"\n\n{ex}";
        }

        return check;
    }

    private static bool CanWriteToDirectory(string path)
    {
        try
        {
            var testFile = Path.Combine(path, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GenerateSummary(DiagnosticReport report)
    {
        var pass = report.Checks.Count(c => c.Status == DiagnosticStatus.Pass);
        var warn = report.Checks.Count(c => c.Status == DiagnosticStatus.Warn);
        var fail = report.Checks.Count(c => c.Status == DiagnosticStatus.Fail);
        var error = report.Checks.Count(c => c.Status == DiagnosticStatus.Error);
        var skip = report.Checks.Count(c => c.Status == DiagnosticStatus.Skipped);

        var parts = new List<string>();
        if (pass > 0) parts.Add($"{pass} passed");
        if (warn > 0) parts.Add($"{warn} warnings");
        if (fail > 0) parts.Add($"{fail} failed");
        if (error > 0) parts.Add($"{error} errors");
        if (skip > 0) parts.Add($"{skip} skipped");

        return string.Join(", ", parts);
    }
}

/// <summary>
/// Progress update during diagnostic run.
/// </summary>
public class DiagnosticProgress
{
    public string CurrentCheck { get; set; } = "";
    public int Completed { get; set; }
    public int Total { get; set; }
    public DiagnosticStatus LastResult { get; set; }
}

/// <summary>
/// Complete diagnostic report with all check results.
/// </summary>
public class DiagnosticReport
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Platform { get; set; } = "";
    public string ModkitVersion { get; set; } = "";
    public string RuntimeVersion { get; set; } = "";
    public string Mode { get; set; } = "Dry Run";
    public string Summary { get; set; } = "";
    public List<DiagnosticCheck> Checks { get; set; } = new();

    public TimeSpan Duration => EndTime - StartTime;

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        });
    }
}

/// <summary>
/// Individual diagnostic check result.
/// </summary>
public class DiagnosticCheck
{
    public string Name { get; set; } = "";
    public DiagnosticStatus Status { get; set; }
    public string Message { get; set; } = "";
    public string? Details { get; set; }
}

/// <summary>
/// Status of a diagnostic check.
/// </summary>
public enum DiagnosticStatus
{
    Pass,
    Warn,
    Fail,
    Error,
    Skipped
}
