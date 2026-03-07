using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Menace.Modkit.App.Models;

/// <summary>
/// Tracks metadata about game file backups (.original files) created during deployment.
/// Used to validate backup integrity and detect when backups become stale due to game updates.
/// Stored as backup-metadata.json in the game's data directory (e.g., Menace_Data/).
/// </summary>
public class BackupMetadata
{
    /// <summary>
    /// The game version when backups were created (detected from globalgamemanagers).
    /// Format: "{unityVersion}_{fileSize}" e.g. "2022.3.21f1_6291456"
    /// </summary>
    public string GameVersion { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the backup was created.
    /// </summary>
    public DateTime BackupCreatedAt { get; set; }

    /// <summary>
    /// SHA256 hashes of each backed up file, keyed by filename (without .original suffix).
    /// e.g., { "resources.assets": "abc123...", "globalgamemanagers": "def456..." }
    /// </summary>
    public Dictionary<string, string> FileHashes { get; set; } = new();

    /// <summary>
    /// The Modkit version that created this backup.
    /// </summary>
    public string ModkitVersion { get; set; } = string.Empty;

    /// <summary>
    /// Original file sizes in bytes, keyed by filename (without .original suffix).
    /// Used for quick validation before computing expensive hashes.
    /// </summary>
    public Dictionary<string, long> OriginalFileSizes { get; set; } = new();

    /// <summary>
    /// Whether this metadata was created by migrating from a legacy installation.
    /// Legacy metadata may have incomplete information (no hashes, estimated timestamps).
    /// </summary>
    public bool MigratedFromLegacy { get; set; }

    /// <summary>
    /// JSON serialization options for reading.
    /// </summary>
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// JSON serialization options for writing.
    /// </summary>
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Load backup metadata from a game data directory.
    /// </summary>
    /// <param name="gameDataDir">The game's data directory (e.g., Menace_Data).</param>
    /// <returns>The loaded metadata, or null if no metadata file exists.</returns>
    public static BackupMetadata? LoadFrom(string gameDataDir)
    {
        var metadataPath = Path.Combine(gameDataDir, "backup-metadata.json");
        if (!File.Exists(metadataPath))
            return null;

        try
        {
            var json = File.ReadAllText(metadataPath);
            return JsonSerializer.Deserialize<BackupMetadata>(json, ReadOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Save backup metadata to a game data directory.
    /// </summary>
    /// <param name="gameDataDir">The game's data directory (e.g., Menace_Data).</param>
    public void SaveTo(string gameDataDir)
    {
        var metadataPath = Path.Combine(gameDataDir, "backup-metadata.json");
        var json = JsonSerializer.Serialize(this, WriteOptions);
        File.WriteAllText(metadataPath, json);
    }

    /// <summary>
    /// Create backup metadata for the given backup files.
    /// </summary>
    /// <param name="gameDataDir">The game's data directory containing the backup files.</param>
    /// <param name="gameVersion">The detected game version string.</param>
    /// <param name="backupFiles">List of backup file names (with .original suffix).</param>
    /// <returns>A new BackupMetadata instance with computed hashes and sizes.</returns>
    public static BackupMetadata Create(string gameDataDir, string gameVersion, IEnumerable<string> backupFiles)
    {
        var metadata = new BackupMetadata
        {
            GameVersion = gameVersion,
            BackupCreatedAt = DateTime.UtcNow,
            ModkitVersion = Menace.ModkitVersion.MelonVersion
        };

        foreach (var backupFileName in backupFiles)
        {
            var backupPath = Path.Combine(gameDataDir, backupFileName);
            if (!File.Exists(backupPath))
                continue;

            // Strip .original suffix for the key
            var baseName = backupFileName.EndsWith(".original", StringComparison.OrdinalIgnoreCase)
                ? backupFileName[..^9] // Remove ".original" (9 chars)
                : backupFileName;

            var fileInfo = new FileInfo(backupPath);
            metadata.OriginalFileSizes[baseName] = fileInfo.Length;
            metadata.FileHashes[baseName] = ComputeFileHash(backupPath);
        }

        return metadata;
    }

    /// <summary>
    /// Validate that all backed up files exist and match their recorded metadata.
    /// </summary>
    /// <param name="gameDataDir">The game's data directory containing the backup files.</param>
    /// <returns>Validation result with details about any issues found.</returns>
    public BackupValidationResult ValidateBackups(string gameDataDir)
    {
        var result = new BackupValidationResult { IsValid = true };

        foreach (var (baseName, expectedSize) in OriginalFileSizes)
        {
            var backupPath = Path.Combine(gameDataDir, baseName + ".original");

            // Check file exists
            if (!File.Exists(backupPath))
            {
                result.IsValid = false;
                result.MissingFiles.Add(baseName);
                continue;
            }

            // Check file size (quick validation)
            var fileInfo = new FileInfo(backupPath);
            if (fileInfo.Length != expectedSize)
            {
                result.IsValid = false;
                result.SizeMismatches.Add(baseName, (expectedSize, fileInfo.Length));
                continue;
            }

            // Check hash if available (expensive but thorough)
            if (FileHashes.TryGetValue(baseName, out var expectedHash))
            {
                var actualHash = ComputeFileHash(backupPath);
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    result.IsValid = false;
                    result.HashMismatches.Add(baseName);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Check if the backup is stale (from a different game version).
    /// </summary>
    /// <param name="currentGameVersion">The current game version string.</param>
    /// <returns>True if the backup was created for a different game version.</returns>
    public bool IsBackupStale(string currentGameVersion)
    {
        if (string.IsNullOrEmpty(GameVersion) || string.IsNullOrEmpty(currentGameVersion))
            return false; // Can't determine staleness without version info

        return !string.Equals(GameVersion, currentGameVersion, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Compute SHA256 hash of a file.
    /// </summary>
    private static string ComputeFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}

/// <summary>
/// Result of validating backup files against their recorded metadata.
/// </summary>
public class BackupValidationResult
{
    /// <summary>
    /// Whether all backups are valid (exist, correct size, correct hash).
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of backup files that are missing.
    /// </summary>
    public List<string> MissingFiles { get; set; } = new();

    /// <summary>
    /// Map of files with size mismatches: baseName -> (expected, actual).
    /// </summary>
    public Dictionary<string, (long Expected, long Actual)> SizeMismatches { get; set; } = new();

    /// <summary>
    /// List of files with hash mismatches (file corrupted or modified).
    /// </summary>
    public List<string> HashMismatches { get; set; } = new();

    /// <summary>
    /// Get a human-readable summary of validation issues.
    /// </summary>
    public string GetSummary()
    {
        if (IsValid)
            return "All backups are valid.";

        var issues = new List<string>();

        if (MissingFiles.Count > 0)
            issues.Add($"Missing: {string.Join(", ", MissingFiles)}");

        if (SizeMismatches.Count > 0)
            issues.Add($"Size mismatch: {string.Join(", ", SizeMismatches.Keys)}");

        if (HashMismatches.Count > 0)
            issues.Add($"Corrupted: {string.Join(", ", HashMismatches)}");

        return string.Join("; ", issues);
    }
}
