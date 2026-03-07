using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Manages atomic deployment transactions with staging and rollback capabilities.
///
/// Transaction lifecycle:
/// 1. Begin() - Creates staging directory, starts tracking changes
/// 2. StageFile/StageDirectory - Write all files to staging area
/// 3. StageGameFilePatch - Stage patched game files (verifies backups first)
/// 4. Commit() - Atomically swap staged files into place
/// 5. On failure: Rollback() - Remove any partial changes, restore backups
/// </summary>
public class DeployTransaction : IDisposable
{
    private readonly string _modsBasePath;
    private readonly string _gameDataDir;
    private readonly string _stagingDir;
    private readonly string _gameFileStagingDir;

    private TransactionState _state = TransactionState.NotStarted;
    private readonly List<StagedFile> _stagedFiles = new();
    private readonly List<StagedDirectory> _stagedDirectories = new();
    private readonly List<StagedGameFile> _stagedGameFiles = new();
    private readonly List<StagedRemoval> _stagedRemovals = new();
    private readonly List<string> _committedFiles = new();
    private readonly List<string> _committedDirectories = new();
    private readonly List<string> _removedDirectories = new();

    /// <summary>
    /// Files that were modified during commit and need to be restored on rollback.
    /// Maps destination path to backup path (or null if file didn't exist before).
    /// </summary>
    private readonly Dictionary<string, string?> _modifiedFilesForRollback = new();

    /// <summary>
    /// Directories that were created during commit (for cleanup on rollback).
    /// </summary>
    private readonly List<string> _createdDirectoriesForRollback = new();

    public DeployTransaction(string modsBasePath, string? gameDataDir)
    {
        _modsBasePath = modsBasePath;
        _gameDataDir = gameDataDir ?? string.Empty;
        _stagingDir = Path.Combine(modsBasePath, ".deploy-staging");
        _gameFileStagingDir = Path.Combine(_stagingDir, "game-files");
    }

    /// <summary>
    /// Begin a new deployment transaction.
    /// Creates the staging directory and prepares for file operations.
    /// </summary>
    public void Begin()
    {
        if (_state != TransactionState.NotStarted)
            throw new InvalidOperationException($"Cannot begin transaction in state: {_state}");

        ModkitLog.Info("[Transaction] BEGIN - Creating staging directory");

        // Clean up any leftover staging from a previous failed deploy
        if (Directory.Exists(_stagingDir))
        {
            ModkitLog.Warn("[Transaction] Cleaning up leftover staging directory from previous deploy");
            Directory.Delete(_stagingDir, recursive: true);
        }

        Directory.CreateDirectory(_stagingDir);
        Directory.CreateDirectory(_gameFileStagingDir);

        _state = TransactionState.InProgress;
        ModkitLog.Info($"[Transaction] Staging directory created: {_stagingDir}");
    }

    /// <summary>
    /// Stage a single file to be deployed.
    /// File is written to staging directory, not final destination.
    /// </summary>
    /// <param name="sourceContent">Content to write (byte array or will be copied from source path)</param>
    /// <param name="relativePath">Path relative to Mods/ where file should end up</param>
    public void StageFile(string sourcePath, string relativePath)
    {
        EnsureInProgress();

        var stagingPath = Path.Combine(_stagingDir, "mods", relativePath);
        var stagingParent = Path.GetDirectoryName(stagingPath);
        if (!string.IsNullOrEmpty(stagingParent))
            Directory.CreateDirectory(stagingParent);

        File.Copy(sourcePath, stagingPath, overwrite: true);

        _stagedFiles.Add(new StagedFile
        {
            StagingPath = stagingPath,
            FinalPath = Path.Combine(_modsBasePath, relativePath),
            RelativePath = relativePath
        });
    }

    /// <summary>
    /// Stage an entire directory to be deployed.
    /// Directory is copied to staging, not final destination.
    /// </summary>
    /// <param name="sourceDir">Source directory to copy</param>
    /// <param name="relativePath">Path relative to Mods/ where directory should end up</param>
    public void StageDirectory(string sourceDir, string relativePath)
    {
        EnsureInProgress();

        var stagingPath = Path.Combine(_stagingDir, "mods", relativePath);
        CopyDirectoryToStaging(sourceDir, stagingPath);

        _stagedDirectories.Add(new StagedDirectory
        {
            StagingPath = stagingPath,
            FinalPath = Path.Combine(_modsBasePath, relativePath),
            RelativePath = relativePath
        });
    }

    /// <summary>
    /// Write content directly to the staging area.
    /// </summary>
    public void StageFileContent(string relativePath, string content)
    {
        EnsureInProgress();

        var stagingPath = Path.Combine(_stagingDir, "mods", relativePath);
        var stagingParent = Path.GetDirectoryName(stagingPath);
        if (!string.IsNullOrEmpty(stagingParent))
            Directory.CreateDirectory(stagingParent);

        File.WriteAllText(stagingPath, content);

        _stagedFiles.Add(new StagedFile
        {
            StagingPath = stagingPath,
            FinalPath = Path.Combine(_modsBasePath, relativePath),
            RelativePath = relativePath
        });
    }

    /// <summary>
    /// Write binary content directly to the staging area.
    /// </summary>
    public void StageFileContent(string relativePath, byte[] content)
    {
        EnsureInProgress();

        var stagingPath = Path.Combine(_stagingDir, "mods", relativePath);
        var stagingParent = Path.GetDirectoryName(stagingPath);
        if (!string.IsNullOrEmpty(stagingParent))
            Directory.CreateDirectory(stagingParent);

        File.WriteAllBytes(stagingPath, content);

        _stagedFiles.Add(new StagedFile
        {
            StagingPath = stagingPath,
            FinalPath = Path.Combine(_modsBasePath, relativePath),
            RelativePath = relativePath
        });
    }

    /// <summary>
    /// Stage a patched game file (e.g., resources.assets, globalgamemanagers).
    /// Verifies that a backup exists before allowing the patch.
    /// </summary>
    /// <param name="patchedFilePath">Path to the patched file to deploy</param>
    /// <param name="gameFileName">Name of the game file (e.g., "resources.assets")</param>
    public void StageGameFilePatch(string patchedFilePath, string gameFileName)
    {
        EnsureInProgress();

        if (string.IsNullOrEmpty(_gameDataDir))
            throw new InvalidOperationException("Game data directory not set - cannot patch game files");

        var originalPath = Path.Combine(_gameDataDir, gameFileName);
        var backupPath = Path.Combine(_gameDataDir, gameFileName + ".original");

        // Check if backup exists - if not, mark that we need to create it during commit
        var needsBackup = !File.Exists(backupPath);

        if (needsBackup)
        {
            // Verify original exists so we CAN create a backup during commit
            if (!File.Exists(originalPath))
            {
                throw new InvalidOperationException(
                    $"Cannot patch {gameFileName}: original file not found and no backup exists. " +
                    "Verify game files via Steam first.");
            }
            ModkitLog.Info($"[Transaction] Will create backup during commit: {gameFileName} -> {gameFileName}.original");
        }
        else
        {
            // Verify existing backup is valid (not empty/corrupted)
            var backupInfo = new FileInfo(backupPath);
            if (backupInfo.Length < 1024) // Less than 1KB is definitely wrong
            {
                throw new InvalidOperationException(
                    $"Backup file {gameFileName}.original appears corrupted (only {backupInfo.Length} bytes). " +
                    "Delete the backup and verify game files via Steam.");
            }
        }

        // Copy patched file to staging
        var stagingPath = Path.Combine(_gameFileStagingDir, gameFileName);
        File.Copy(patchedFilePath, stagingPath, overwrite: true);

        // Verify the staged patched file
        var stagedInfo = new FileInfo(stagingPath);
        if (stagedInfo.Length < 1024)
        {
            throw new InvalidOperationException(
                $"Patched file {gameFileName} appears invalid (only {stagedInfo.Length} bytes).");
        }

        _stagedGameFiles.Add(new StagedGameFile
        {
            StagingPath = stagingPath,
            FinalPath = originalPath,
            BackupPath = backupPath,
            GameFileName = gameFileName,
            NeedsBackupCreation = needsBackup
        });

        ModkitLog.Info($"[Transaction] Staged game file patch: {gameFileName} ({stagedInfo.Length / 1024 / 1024}MB)");
    }

    /// <summary>
    /// Stage a directory for removal during commit.
    /// Directory is backed up for rollback, then removed atomically with other changes.
    /// </summary>
    /// <param name="relativePath">Path relative to Mods/ of the directory to remove</param>
    public void StageDirectoryRemoval(string relativePath)
    {
        EnsureInProgress();

        var finalPath = Path.Combine(_modsBasePath, relativePath);
        if (!Directory.Exists(finalPath))
        {
            ModkitLog.Info($"[Transaction] Skipping removal of non-existent directory: {relativePath}");
            return;
        }

        _stagedRemovals.Add(new StagedRemoval
        {
            FinalPath = finalPath,
            RelativePath = relativePath
        });

        ModkitLog.Info($"[Transaction] Staged directory removal: {relativePath}");
    }

    /// <summary>
    /// Commit all staged changes atomically.
    /// On success, staged files are moved to their final destinations.
    /// On failure, any partial changes are rolled back.
    /// </summary>
    public void Commit()
    {
        EnsureInProgress();

        ModkitLog.Info($"[Transaction] COMMIT - Deploying {_stagedDirectories.Count} directories, {_stagedFiles.Count} files, {_stagedGameFiles.Count} game files, removing {_stagedRemovals.Count} directories");

        try
        {
            _state = TransactionState.Committing;

            // Phase 0: Remove directories that are no longer needed (before deploying new ones)
            foreach (var removal in _stagedRemovals)
            {
                if (!Directory.Exists(removal.FinalPath))
                    continue;

                // Back up the directory for rollback
                var backupDir = removal.FinalPath + ".rollback-backup";
                if (Directory.Exists(backupDir))
                    Directory.Delete(backupDir, recursive: true);
                Directory.Move(removal.FinalPath, backupDir);
                _modifiedFilesForRollback[removal.FinalPath] = backupDir;
                _removedDirectories.Add(removal.FinalPath);
                ModkitLog.Info($"[Transaction] Removed directory: {removal.RelativePath}");
            }

            // Phase 1: Deploy directories (modpacks)
            foreach (var dir in _stagedDirectories)
            {
                // Track if directory existed before (for rollback)
                if (Directory.Exists(dir.FinalPath))
                {
                    // Create a temporary backup of the existing directory
                    var backupDir = dir.FinalPath + ".rollback-backup";
                    if (Directory.Exists(backupDir))
                        Directory.Delete(backupDir, recursive: true);
                    Directory.Move(dir.FinalPath, backupDir);
                    _modifiedFilesForRollback[dir.FinalPath] = backupDir;
                }
                else
                {
                    _createdDirectoriesForRollback.Add(dir.FinalPath);
                }

                // Move staged directory to final location
                Directory.Move(dir.StagingPath, dir.FinalPath);
                _committedDirectories.Add(dir.FinalPath);
                ModkitLog.Info($"[Transaction] Committed directory: {dir.RelativePath}");
            }

            // Phase 2: Deploy individual files
            foreach (var file in _stagedFiles)
            {
                var finalParent = Path.GetDirectoryName(file.FinalPath);
                if (!string.IsNullOrEmpty(finalParent) && !Directory.Exists(finalParent))
                {
                    Directory.CreateDirectory(finalParent);
                    _createdDirectoriesForRollback.Add(finalParent);
                }

                // Track if file existed before (for rollback)
                if (File.Exists(file.FinalPath))
                {
                    var backupFile = file.FinalPath + ".rollback-backup";
                    File.Copy(file.FinalPath, backupFile, overwrite: true);
                    _modifiedFilesForRollback[file.FinalPath] = backupFile;
                }
                else
                {
                    _modifiedFilesForRollback[file.FinalPath] = null;
                }

                File.Move(file.StagingPath, file.FinalPath, overwrite: true);
                _committedFiles.Add(file.FinalPath);
            }

            // Phase 3: Deploy game file patches (most critical - do last)
            // First, create any needed backups DURING COMMIT (not earlier)
            foreach (var gameFile in _stagedGameFiles.Where(gf => gf.NeedsBackupCreation))
            {
                ModkitLog.Info($"[Transaction] Creating backup during commit: {gameFile.GameFileName} -> {gameFile.GameFileName}.original");
                File.Copy(gameFile.FinalPath, gameFile.BackupPath);
                _backupsCreatedDuringCommit.Add(gameFile.BackupPath);
            }

            // Now deploy the patches
            foreach (var gameFile in _stagedGameFiles)
            {
                // Track that we're modifying this file (backup now guaranteed to exist)
                _modifiedFilesForRollback[gameFile.FinalPath] = gameFile.BackupPath;

                File.Copy(gameFile.StagingPath, gameFile.FinalPath, overwrite: true);
                ModkitLog.Info($"[Transaction] Committed game file patch: {gameFile.GameFileName}");
            }

            // Create backup-metadata.json if we have game file patches
            if (_stagedGameFiles.Count > 0 && !string.IsNullOrEmpty(_gameDataDir))
            {
                CreateBackupMetadata();
            }

            // Success - clean up staging and rollback backups
            CleanupStagingDirectory();
            CleanupRollbackBackups();

            _state = TransactionState.Committed;
            ModkitLog.Info("[Transaction] COMMIT SUCCESSFUL");
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[Transaction] COMMIT FAILED: {ex.Message}");
            _state = TransactionState.Failed;

            // Attempt rollback
            try
            {
                Rollback();
            }
            catch (Exception rollbackEx)
            {
                ModkitLog.Error($"[Transaction] ROLLBACK ALSO FAILED: {rollbackEx.Message}");
            }

            throw;
        }
    }

    /// <summary>
    /// Roll back all changes made during commit.
    /// Restores modified files from backups and removes newly created files.
    /// </summary>
    public void Rollback()
    {
        if (_state == TransactionState.NotStarted || _state == TransactionState.RolledBack)
            return;

        ModkitLog.Warn("[Transaction] ROLLBACK - Restoring previous state");

        var rollbackErrors = new List<string>();

        // Restore modified files from backups
        foreach (var (finalPath, backupPath) in _modifiedFilesForRollback)
        {
            try
            {
                if (backupPath == null)
                {
                    // File didn't exist before - delete it
                    if (File.Exists(finalPath))
                        File.Delete(finalPath);
                    else if (Directory.Exists(finalPath))
                        Directory.Delete(finalPath, recursive: true);
                }
                else if (backupPath.EndsWith(".rollback-backup"))
                {
                    // Temporary backup we created - restore it
                    if (File.Exists(backupPath))
                    {
                        if (File.Exists(finalPath))
                            File.Delete(finalPath);
                        File.Move(backupPath, finalPath);
                    }
                    else if (Directory.Exists(backupPath))
                    {
                        if (Directory.Exists(finalPath))
                            Directory.Delete(finalPath, recursive: true);
                        Directory.Move(backupPath, finalPath);
                    }
                }
                else
                {
                    // Game file with .original backup - restore from backup
                    if (File.Exists(backupPath))
                    {
                        File.Copy(backupPath, finalPath, overwrite: true);
                        ModkitLog.Info($"[Transaction] Restored game file from backup: {Path.GetFileName(finalPath)}");
                    }
                }
            }
            catch (Exception ex)
            {
                rollbackErrors.Add($"Failed to restore {finalPath}: {ex.Message}");
            }
        }

        // Remove backups that were created during this (failed) commit
        foreach (var backupPath in _backupsCreatedDuringCommit)
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                    ModkitLog.Info($"[Transaction] Removed backup created during failed commit: {Path.GetFileName(backupPath)}");
                }
            }
            catch (Exception ex)
            {
                rollbackErrors.Add($"Failed to remove backup {backupPath}: {ex.Message}");
            }
        }

        // Remove directories that were created during commit
        foreach (var dir in _createdDirectoriesForRollback.OrderByDescending(d => d.Length))
        {
            try
            {
                if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);
            }
            catch { /* Ignore cleanup errors for directories */ }
        }

        // Clean up staging directory
        CleanupStagingDirectory();
        CleanupRollbackBackups();

        _state = TransactionState.RolledBack;

        if (rollbackErrors.Count > 0)
        {
            ModkitLog.Error($"[Transaction] ROLLBACK completed with {rollbackErrors.Count} errors:");
            foreach (var error in rollbackErrors)
                ModkitLog.Error($"  - {error}");
        }
        else
        {
            ModkitLog.Info("[Transaction] ROLLBACK SUCCESSFUL - Previous state restored");
        }
    }

    /// <summary>
    /// Get a list of all files that were successfully committed.
    /// </summary>
    public IReadOnlyList<string> GetCommittedFiles() => _committedFiles.AsReadOnly();

    /// <summary>
    /// Get a list of all directories that were successfully committed.
    /// </summary>
    public IReadOnlyList<string> GetCommittedDirectories() => _committedDirectories.AsReadOnly();

    public void Dispose()
    {
        // If transaction was never committed, clean up staging
        if (_state == TransactionState.InProgress || _state == TransactionState.Failed)
        {
            try
            {
                CleanupStagingDirectory();
                CleanupRollbackBackups();
            }
            catch { /* Best effort cleanup */ }
        }
    }

    private void EnsureInProgress()
    {
        if (_state != TransactionState.InProgress)
            throw new InvalidOperationException($"Transaction not in progress (state: {_state})");
    }

    private void CleanupStagingDirectory()
    {
        try
        {
            if (Directory.Exists(_stagingDir))
            {
                Directory.Delete(_stagingDir, recursive: true);
                ModkitLog.Info("[Transaction] Cleaned up staging directory");
            }
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[Transaction] Failed to clean up staging directory: {ex.Message}");
        }
    }

    private void CleanupRollbackBackups()
    {
        try
        {
            // Clean up any .rollback-backup files/directories we created
            foreach (var (_, backupPath) in _modifiedFilesForRollback)
            {
                if (backupPath != null && backupPath.EndsWith(".rollback-backup"))
                {
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    else if (Directory.Exists(backupPath))
                        Directory.Delete(backupPath, recursive: true);
                }
            }
        }
        catch { /* Best effort cleanup */ }
    }

    /// <summary>
    /// Create backup-metadata.json with hashes and metadata for backup files.
    /// Called during commit after backups are created.
    /// </summary>
    private void CreateBackupMetadata()
    {
        try
        {
            var backupFiles = new[] { "resources.assets.original", "globalgamemanagers.original" }
                .Where(f => File.Exists(Path.Combine(_gameDataDir, f)))
                .ToList();

            if (backupFiles.Count == 0)
            {
                ModkitLog.Warn("[Transaction] No backup files found for metadata creation");
                return;
            }

            // Detect game version from the BACKUP file (vanilla source), not patched
            var gameVersion = DetectGameVersionFromBackup();

            ModkitLog.Info($"[Transaction] Creating backup-metadata.json for {backupFiles.Count} backup(s)...");

            var metadata = BackupMetadata.Create(_gameDataDir, gameVersion, backupFiles);
            metadata.SaveTo(_gameDataDir);

            ModkitLog.Info($"[Transaction] Backup metadata created: version={gameVersion}");
        }
        catch (Exception ex)
        {
            // Don't fail commit if metadata creation fails
            ModkitLog.Warn($"[Transaction] Failed to create backup metadata: {ex.Message}");
        }
    }

    /// <summary>
    /// Detect game version from the backup file (globalgamemanagers.original).
    /// </summary>
    private string DetectGameVersionFromBackup()
    {
        var backupPath = Path.Combine(_gameDataDir, "globalgamemanagers.original");
        if (!File.Exists(backupPath))
            return "unknown";

        try
        {
            using var fs = File.OpenRead(backupPath);
            using var reader = new BinaryReader(fs);

            fs.Seek(0x14, SeekOrigin.Begin);
            var bytes = new List<byte>();
            byte b;
            while ((b = reader.ReadByte()) != 0 && bytes.Count < 50)
                bytes.Add(b);

            var unityVersion = System.Text.Encoding.ASCII.GetString(bytes.ToArray());
            var fileSize = new FileInfo(backupPath).Length;
            return $"{unityVersion}_{fileSize}";
        }
        catch
        {
            return "unknown";
        }
    }

    private static void CopyDirectoryToStaging(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            CopyDirectoryToStaging(dir, Path.Combine(destDir, dirName));
        }
    }

    private enum TransactionState
    {
        NotStarted,
        InProgress,
        Committing,
        Committed,
        Failed,
        RolledBack
    }

    private class StagedFile
    {
        public string StagingPath { get; set; } = string.Empty;
        public string FinalPath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
    }

    private class StagedDirectory
    {
        public string StagingPath { get; set; } = string.Empty;
        public string FinalPath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
    }

    private class StagedGameFile
    {
        public string StagingPath { get; set; } = string.Empty;
        public string FinalPath { get; set; } = string.Empty;
        public string BackupPath { get; set; } = string.Empty;
        public string GameFileName { get; set; } = string.Empty;
        /// <summary>
        /// If true, backup needs to be created during commit (file didn't have .original yet).
        /// </summary>
        public bool NeedsBackupCreation { get; set; }
    }

    private class StagedRemoval
    {
        public string FinalPath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Tracks backups that were created during commit (for rollback).
    /// </summary>
    private readonly List<string> _backupsCreatedDuringCommit = new();
}
