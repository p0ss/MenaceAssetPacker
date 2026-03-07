using System.Collections.Generic;
using System.Linq;

namespace Menace.Modkit.App.Models;

/// <summary>
/// Result of pre-undeploy validation. Checks that deployed files match expected state
/// and identifies any unexpected changes or orphaned files.
/// </summary>
public class UndeployValidationResult
{
    /// <summary>
    /// Whether the validation passed without any issues.
    /// False if files were modified, missing, or unknown files were found.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Files that exist but have been modified since deployment (hash/size mismatch).
    /// These files will still be removed, but the modification is logged as a warning.
    /// </summary>
    public List<ModifiedFileInfo> ModifiedFiles { get; set; } = new();

    /// <summary>
    /// Files that were tracked in deploy state but no longer exist on disk.
    /// These are logged as informational (file was already removed).
    /// </summary>
    public List<string> MissingFiles { get; set; } = new();

    /// <summary>
    /// Files found in Mods/ folder that weren't deployed by us.
    /// These could be user-created files or leftover from manual installs.
    /// </summary>
    public List<string> UnknownFiles { get; set; } = new();

    /// <summary>
    /// Files in modpack directories that weren't tracked (added after deploy).
    /// </summary>
    public List<string> UntrackedFiles { get; set; } = new();

    /// <summary>
    /// Issues with backup files needed for game restoration.
    /// </summary>
    public BackupValidationSummary? BackupValidation { get; set; }

    /// <summary>
    /// Human-readable summary of all validation issues.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Whether undeploy should proceed. True unless there are critical issues
    /// that would leave the game in a broken state.
    /// </summary>
    public bool ShouldProceed { get; set; } = true;

    /// <summary>
    /// Warnings that should be displayed to the user before proceeding.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Build a human-readable summary from the validation results.
    /// </summary>
    public void BuildSummary()
    {
        var parts = new List<string>();

        if (ModifiedFiles.Count > 0)
        {
            parts.Add($"{ModifiedFiles.Count} file(s) modified since deploy");
            Warnings.Add($"Modified files will be removed: {string.Join(", ", ModifiedFiles.Select(f => f.RelativePath).Take(5))}{(ModifiedFiles.Count > 5 ? "..." : "")}");
        }

        if (MissingFiles.Count > 0)
        {
            parts.Add($"{MissingFiles.Count} tracked file(s) already removed");
        }

        if (UnknownFiles.Count > 0)
        {
            parts.Add($"{UnknownFiles.Count} unknown file(s) in Mods/");
            Warnings.Add($"Unknown files will NOT be removed: {string.Join(", ", UnknownFiles.Take(5))}{(UnknownFiles.Count > 5 ? "..." : "")}");
        }

        if (UntrackedFiles.Count > 0)
        {
            parts.Add($"{UntrackedFiles.Count} untracked file(s) in modpack directories");
        }

        if (BackupValidation != null && !BackupValidation.IsValid)
        {
            parts.Add($"Backup issues: {BackupValidation.Summary}");
            if (BackupValidation.IsCritical)
            {
                Warnings.Add($"CRITICAL: {BackupValidation.Summary}");
                ShouldProceed = false;
            }
            else
            {
                Warnings.Add($"Warning: {BackupValidation.Summary}");
            }
        }

        IsValid = ModifiedFiles.Count == 0 && MissingFiles.Count == 0 &&
                  UnknownFiles.Count == 0 && UntrackedFiles.Count == 0 &&
                  (BackupValidation?.IsValid ?? true);

        Summary = parts.Count > 0 ? string.Join("; ", parts) : "All files match expected state";
    }
}

/// <summary>
/// Information about a file that was modified after deployment.
/// </summary>
public class ModifiedFileInfo
{
    /// <summary>
    /// Relative path from the Mods/ directory.
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Expected file size from deploy state.
    /// </summary>
    public long ExpectedSize { get; set; }

    /// <summary>
    /// Actual file size on disk.
    /// </summary>
    public long ActualSize { get; set; }

    /// <summary>
    /// Expected hash from deploy state (if available).
    /// </summary>
    public string? ExpectedHash { get; set; }

    /// <summary>
    /// Actual hash computed from file on disk.
    /// </summary>
    public string? ActualHash { get; set; }

    /// <summary>
    /// What kind of modification was detected.
    /// </summary>
    public ModificationType ModificationType { get; set; }
}

/// <summary>
/// Type of modification detected for a deployed file.
/// </summary>
public enum ModificationType
{
    /// <summary>File size changed.</summary>
    SizeChanged,

    /// <summary>File hash changed (content modified).</summary>
    ContentChanged,

    /// <summary>Both size and hash changed.</summary>
    Both
}

/// <summary>
/// Summary of backup file validation for game restoration.
/// </summary>
public class BackupValidationSummary
{
    /// <summary>
    /// Whether all backups are valid and ready for restoration.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Whether backup issues are critical (would break game restoration).
    /// </summary>
    public bool IsCritical { get; set; }

    /// <summary>
    /// Human-readable summary of backup validation.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// List of backup files that are missing.
    /// </summary>
    public List<string> MissingBackups { get; set; } = new();

    /// <summary>
    /// List of backup files that failed hash validation.
    /// </summary>
    public List<string> CorruptedBackups { get; set; } = new();

    /// <summary>
    /// Whether backup metadata was found.
    /// </summary>
    public bool HasMetadata { get; set; }
}

/// <summary>
/// Options for cleaning up orphaned files during undeploy.
/// </summary>
public class OrphanedFileCleanupOptions
{
    /// <summary>
    /// Whether to remove files in Mods/ that aren't tracked in deploy state.
    /// </summary>
    public bool RemoveUntrackedFiles { get; set; }

    /// <summary>
    /// Whether to remove empty directories after cleanup.
    /// </summary>
    public bool RemoveEmptyDirectories { get; set; } = true;

    /// <summary>
    /// Patterns for files to never remove (e.g., user config files).
    /// </summary>
    public List<string> ProtectedPatterns { get; set; } = new()
    {
        "*.config",
        "*.user",
        "*.local",
        "config.json",
        "settings.json"
    };

    /// <summary>
    /// If true, only log what would be removed without actually removing.
    /// </summary>
    public bool DryRun { get; set; }
}
