using System;
using System.IO;

namespace Menace.Modkit.Tests.Helpers;

/// <summary>
/// Creates a uniquely-named temp directory that is deleted on disposal.
/// </summary>
public sealed class TemporaryDirectory : IDisposable
{
    public string Path { get; }

    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "menace-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// Write a file at a relative path inside this temp directory. Creates intermediate directories.
    /// </summary>
    public string WriteFile(string relativePath, string content)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        var dir = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    /// <summary>
    /// Create a subdirectory at a relative path inside this temp directory.
    /// </summary>
    public string CreateSubdirectory(string relativePath)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
