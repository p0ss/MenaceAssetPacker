using System;
using System.IO;
using System.Security;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Security utilities for path validation to prevent path traversal attacks.
/// </summary>
public static class PathValidator
{
    /// <summary>
    /// Validates that a path resolves within a given base directory.
    /// Prevents path traversal attacks (e.g., "../../../etc/passwd").
    /// </summary>
    /// <param name="basePath">The base directory that the path must stay within.</param>
    /// <param name="relativePath">The relative path to validate.</param>
    /// <returns>The fully resolved absolute path.</returns>
    /// <exception cref="SecurityException">Thrown if the path escapes the base directory.</exception>
    public static string ValidatePathWithinBase(string basePath, string relativePath)
    {
        if (string.IsNullOrEmpty(basePath))
            throw new ArgumentNullException(nameof(basePath));
        if (string.IsNullOrEmpty(relativePath))
            throw new ArgumentNullException(nameof(relativePath));

        var fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath));
        var baseFullPath = Path.GetFullPath(basePath);

        // Ensure base path ends with directory separator for proper prefix matching
        if (!baseFullPath.EndsWith(Path.DirectorySeparatorChar))
            baseFullPath += Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(baseFullPath, StringComparison.Ordinal) &&
            !fullPath.Equals(baseFullPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.Ordinal))
        {
            throw new SecurityException($"Path traversal blocked: '{relativePath}' resolves outside base directory");
        }

        return fullPath;
    }

    /// <summary>
    /// Validates a zip/archive entry path to prevent Zip Slip attacks.
    /// </summary>
    /// <param name="destinationDir">The extraction destination directory.</param>
    /// <param name="entryPath">The archive entry's internal path.</param>
    /// <returns>The validated full extraction path.</returns>
    /// <exception cref="SecurityException">Thrown if the entry would extract outside the destination.</exception>
    public static string ValidateArchiveEntryPath(string destinationDir, string entryPath)
    {
        if (string.IsNullOrEmpty(destinationDir))
            throw new ArgumentNullException(nameof(destinationDir));
        if (string.IsNullOrEmpty(entryPath))
            throw new ArgumentNullException(nameof(entryPath));

        // Normalize the entry path to handle different separators
        var normalizedEntry = entryPath.Replace('\\', Path.DirectorySeparatorChar)
                                        .Replace('/', Path.DirectorySeparatorChar);

        var fullPath = Path.GetFullPath(Path.Combine(destinationDir, normalizedEntry));
        var destFullPath = Path.GetFullPath(destinationDir);

        // Ensure destination path ends with directory separator for proper prefix matching
        if (!destFullPath.EndsWith(Path.DirectorySeparatorChar))
            destFullPath += Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(destFullPath, StringComparison.Ordinal))
        {
            throw new SecurityException($"Zip entry path traversal blocked: '{entryPath}'");
        }

        return fullPath;
    }

    /// <summary>
    /// Checks if a path is safe (doesn't contain traversal patterns) without throwing.
    /// </summary>
    /// <param name="basePath">The base directory.</param>
    /// <param name="relativePath">The relative path to check.</param>
    /// <returns>True if the path is safe, false otherwise.</returns>
    public static bool IsPathSafe(string basePath, string relativePath)
    {
        try
        {
            ValidatePathWithinBase(basePath, relativePath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
