using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Menace.Modkit.App.Models;

/// <summary>
/// Tracks detailed information about a single deployed file.
/// Used for validation before undeploy to detect modifications.
/// </summary>
public class DeployedFileInfo
{
    /// <summary>
    /// Path relative to the Mods/ folder.
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hash of the file at deploy time.
    /// </summary>
    public string FileHash { get; set; } = string.Empty;

    /// <summary>
    /// Name of the modpack this file came from, or "runtime" for infrastructure files.
    /// </summary>
    public string SourceModpack { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this specific file was deployed.
    /// </summary>
    public DateTime DeployedAt { get; set; }

    /// <summary>
    /// Size of the file in bytes at deploy time.
    /// </summary>
    public long FileSize { get; set; }
}

/// <summary>
/// Tracks what is currently deployed in the game's Mods/ folder.
/// Persisted as deploy-state.json alongside the staging area.
/// </summary>
public class DeployState
{
    public List<DeployedModpack> DeployedModpacks { get; set; } = new();

    /// <summary>
    /// Legacy list of deployed file paths (relative to Mods/).
    /// Preserved for backward compatibility with older deploy states.
    /// </summary>
    public List<string> DeployedFiles { get; set; } = new();

    /// <summary>
    /// Detailed information about each deployed file including hashes and source modpacks.
    /// New in v2 - enables pre-undeploy validation.
    /// </summary>
    public List<DeployedFileInfo> DeployedFileInfos { get; set; } = new();

    public DateTime LastDeployTimestamp { get; set; }

    /// <summary>
    /// Game version (from Unity Application.version) when mods were last deployed.
    /// Used to detect game updates and trigger cleanup of stale patched files.
    /// </summary>
    public string? GameVersion { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static DeployState LoadFrom(string filePath)
    {
        if (!File.Exists(filePath))
            return new DeployState();

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<DeployState>(json, ReadOptions) ?? new DeployState();
        }
        catch
        {
            return new DeployState();
        }
    }

    public void SaveTo(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(filePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    /// <summary>
    /// Validate that all deployed files still exist with expected hashes.
    /// Returns a list of validation errors (empty if all files are valid).
    /// </summary>
    /// <param name="modsBasePath">Base path to the game's Mods/ folder</param>
    public List<FileValidationError> ValidateDeployedFiles(string modsBasePath)
    {
        var errors = new List<FileValidationError>();

        foreach (var fileInfo in DeployedFileInfos)
        {
            var fullPath = Path.Combine(modsBasePath, fileInfo.RelativePath);

            if (!File.Exists(fullPath))
            {
                errors.Add(new FileValidationError
                {
                    RelativePath = fileInfo.RelativePath,
                    ErrorType = FileValidationErrorType.Missing,
                    Message = "File no longer exists",
                    ExpectedSize = fileInfo.FileSize,
                    ExpectedHash = fileInfo.FileHash
                });
                continue;
            }

            var currentFileInfo = new FileInfo(fullPath);

            // Quick size check first (much faster than hashing)
            if (currentFileInfo.Length != fileInfo.FileSize)
            {
                errors.Add(new FileValidationError
                {
                    RelativePath = fileInfo.RelativePath,
                    ErrorType = FileValidationErrorType.SizeMismatch,
                    Message = $"Size changed: expected {fileInfo.FileSize} bytes, found {currentFileInfo.Length} bytes",
                    ExpectedSize = fileInfo.FileSize,
                    ActualSize = currentFileInfo.Length,
                    ExpectedHash = fileInfo.FileHash
                });
                continue;
            }

            // Full hash check for files with same size
            var currentHash = ComputeFileHash(fullPath);
            if (!string.Equals(currentHash, fileInfo.FileHash, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new FileValidationError
                {
                    RelativePath = fileInfo.RelativePath,
                    ErrorType = FileValidationErrorType.HashMismatch,
                    Message = "File content has been modified since deployment",
                    ExpectedSize = fileInfo.FileSize,
                    ActualSize = currentFileInfo.Length,
                    ExpectedHash = fileInfo.FileHash,
                    ActualHash = currentHash
                });
            }
        }

        return errors;
    }

    /// <summary>
    /// Find files in the Mods/ folder that are not tracked in the deploy state.
    /// These are "orphaned" files that may have been manually added or left over
    /// from a previous deployment that wasn't properly cleaned up.
    /// </summary>
    /// <param name="modsBasePath">Base path to the game's Mods/ folder</param>
    /// <param name="excludePatterns">Optional patterns to exclude (e.g., "Menace.*.dll" for core DLLs)</param>
    public List<string> GetOrphanedFiles(string modsBasePath, IEnumerable<string>? excludePatterns = null)
    {
        var orphaned = new List<string>();

        if (!Directory.Exists(modsBasePath))
            return orphaned;

        // Build a set of tracked relative paths for fast lookup
        var trackedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fileInfo in DeployedFileInfos)
        {
            trackedPaths.Add(fileInfo.RelativePath);
        }
        // Also include legacy DeployedFiles for backward compatibility
        foreach (var filePath in DeployedFiles)
        {
            trackedPaths.Add(filePath);
        }

        // Build exclusion patterns
        var patterns = excludePatterns?.ToList() ?? new List<string>();

        // Scan all files in Mods/
        foreach (var fullPath in Directory.GetFiles(modsBasePath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(modsBasePath, fullPath);
            var fileName = Path.GetFileName(fullPath);

            // Check exclusion patterns
            bool excluded = false;
            foreach (var pattern in patterns)
            {
                if (MatchesWildcard(fileName, pattern))
                {
                    excluded = true;
                    break;
                }
            }

            if (excluded)
                continue;

            // Check if tracked
            if (!trackedPaths.Contains(relativePath))
            {
                orphaned.Add(relativePath);
            }
        }

        return orphaned;
    }

    /// <summary>
    /// Get all files from a specific modpack.
    /// </summary>
    /// <param name="modpackName">Name of the modpack</param>
    public List<DeployedFileInfo> GetFilesFromModpack(string modpackName)
    {
        return DeployedFileInfos
            .Where(f => string.Equals(f.SourceModpack, modpackName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Find files that have been modified since deployment.
    /// Returns DeployedFileInfo for each file that is missing, has different size, or different hash.
    /// </summary>
    /// <param name="modsBasePath">Base path to the game's Mods/ folder</param>
    public List<DeployedFileInfo> GetModifiedFiles(string modsBasePath)
    {
        var modified = new List<DeployedFileInfo>();

        foreach (var fileInfo in DeployedFileInfos)
        {
            var fullPath = Path.Combine(modsBasePath, fileInfo.RelativePath);

            if (!File.Exists(fullPath))
            {
                // File was deleted - consider it modified
                modified.Add(fileInfo);
                continue;
            }

            var currentFileInfo = new FileInfo(fullPath);

            // Quick size check first
            if (currentFileInfo.Length != fileInfo.FileSize)
            {
                modified.Add(fileInfo);
                continue;
            }

            // Full hash check for files with same size
            var currentHash = ComputeFileHash(fullPath);
            if (!string.Equals(currentHash, fileInfo.FileHash, StringComparison.OrdinalIgnoreCase))
            {
                modified.Add(fileInfo);
            }
        }

        return modified;
    }

    /// <summary>
    /// Compute SHA256 hash of a file.
    /// </summary>
    public static string ComputeFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Create a DeployedFileInfo for a file.
    /// </summary>
    /// <param name="fullPath">Full path to the file</param>
    /// <param name="modsBasePath">Base path to calculate relative path from</param>
    /// <param name="sourceModpack">Name of the source modpack</param>
    public static DeployedFileInfo CreateFileInfo(string fullPath, string modsBasePath, string sourceModpack)
    {
        var fileInfo = new FileInfo(fullPath);
        return new DeployedFileInfo
        {
            RelativePath = Path.GetRelativePath(modsBasePath, fullPath),
            FileHash = ComputeFileHash(fullPath),
            SourceModpack = sourceModpack,
            DeployedAt = DateTime.Now,
            FileSize = fileInfo.Length
        };
    }

    /// <summary>
    /// Simple wildcard pattern matching (supports * and ?).
    /// </summary>
    private static bool MatchesWildcard(string input, string pattern)
    {
        // Convert wildcard pattern to regex
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(input, regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}

public class DeployedModpack
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public int LoadOrder { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SecurityStatus SecurityStatus { get; set; }
}

/// <summary>
/// Represents an error found during file validation.
/// </summary>
public class FileValidationError
{
    /// <summary>
    /// Relative path of the file with the error.
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Type of validation error.
    /// </summary>
    public FileValidationErrorType ErrorType { get; set; }

    /// <summary>
    /// Human-readable description of the error.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Expected file size (from deploy state).
    /// </summary>
    public long ExpectedSize { get; set; }

    /// <summary>
    /// Actual file size on disk (if file exists).
    /// </summary>
    public long ActualSize { get; set; }

    /// <summary>
    /// Expected file hash (from deploy state).
    /// </summary>
    public string? ExpectedHash { get; set; }

    /// <summary>
    /// Actual file hash on disk (if computed).
    /// </summary>
    public string? ActualHash { get; set; }
}

/// <summary>
/// Types of file validation errors.
/// </summary>
public enum FileValidationErrorType
{
    /// <summary>File was expected but is missing from disk.</summary>
    Missing,

    /// <summary>File exists but has different size than expected.</summary>
    SizeMismatch,

    /// <summary>File exists with correct size but content hash differs.</summary>
    HashMismatch
}
