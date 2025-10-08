using System.IO;
using System.Linq;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.IO.Files.SerializedFiles.Parser;

namespace Menace.Modkit;

/// <summary>
/// Detects Unity version from a game installation directory.
/// </summary>
public interface IUnityVersionDetector
{
  /// <summary>
  /// Attempts to detect the Unity version from the game installation.
  /// </summary>
  /// <param name="gamePath">Path to the game installation directory.</param>
  /// <returns>Unity version string if detected, null otherwise.</returns>
  string? DetectVersion(string gamePath);
}

public sealed class UnityVersionDetector : IUnityVersionDetector
{
  public string? DetectVersion(string gamePath)
  {
    if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
    {
      Console.WriteLine($"[UnityVersionDetector] Invalid or missing game path: {gamePath}");
      return null;
    }

    Console.WriteLine($"[UnityVersionDetector] Detecting Unity version from: {gamePath}");

    // Use AssetRipper to properly parse asset files
    var version = TryDetectFromAssetFiles(gamePath);
    if (version != null)
    {
      Console.WriteLine($"[UnityVersionDetector] ✓ Detected from asset files: {version}");
      return version;
    }

    // Fallback to UnityPlayer.dll on Windows
    version = TryDetectFromUnityPlayerDll(gamePath);
    if (version != null)
    {
      Console.WriteLine($"[UnityVersionDetector] ✓ Detected from UnityPlayer.dll: {version}");
      return version;
    }

    Console.WriteLine($"[UnityVersionDetector] ❌ Could not detect Unity version");
    return null;
  }

  private static string? TryDetectFromAssetFiles(string gamePath)
  {
    try
    {
      // Look for asset files in *_Data folders
      var dataFolders = Directory.GetDirectories(gamePath, "*_Data", SearchOption.TopDirectoryOnly);
      Console.WriteLine($"[UnityVersionDetector] Found {dataFolders.Length} *_Data folders");

      foreach (var dataFolder in dataFolders)
      {
        Console.WriteLine($"[UnityVersionDetector] Checking folder: {dataFolder}");

        // Try globalgamemanagers first (most reliable)
        var candidates = new[]
        {
          Path.Combine(dataFolder, "globalgamemanagers"),
          Path.Combine(dataFolder, "globalgamemanagers.assets"),
          Path.Combine(dataFolder, "resources.assets"),
          Path.Combine(dataFolder, "level0")
        };

        foreach (var candidatePath in candidates)
        {
          if (!File.Exists(candidatePath))
          {
            Console.WriteLine($"[UnityVersionDetector]   File does not exist: {Path.GetFileName(candidatePath)}");
            continue;
          }

          Console.WriteLine($"[UnityVersionDetector]   Trying: {Path.GetFileName(candidatePath)}");

          try
          {
            if (!SchemeReader.IsReadableFile(candidatePath, LocalFileSystem.Instance))
            {
              Console.WriteLine($"[UnityVersionDetector]     Not readable by AssetRipper");
              continue;
            }

            using var file = SchemeReader.LoadFile(candidatePath, LocalFileSystem.Instance);
            Console.WriteLine($"[UnityVersionDetector]     Loaded as {file.GetType().Name}");

            if (file is SerializedFile serializedFile)
            {
              var version = serializedFile.Version.ToString();
              Console.WriteLine($"[UnityVersionDetector]     Version: {version}");
              if (!string.IsNullOrWhiteSpace(version))
                return version;
            }
            else if (file is FileContainer container)
            {
              var serializedFiles = container.FetchSerializedFiles().ToList();
              Console.WriteLine($"[UnityVersionDetector]     Container has {serializedFiles.Count} serialized files");
              if (serializedFiles.Count > 0)
              {
                var version = serializedFiles[0].Version.ToString();
                Console.WriteLine($"[UnityVersionDetector]     Version: {version}");
                if (!string.IsNullOrWhiteSpace(version))
                  return version;
              }
            }
          }
          catch (Exception ex)
          {
            Console.WriteLine($"[UnityVersionDetector]     Exception: {ex.Message}");
            // Try next candidate
            continue;
          }
        }
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"[UnityVersionDetector] Exception in TryDetectFromAssetFiles: {ex.Message}");
    }

    return null;
  }

  private static string? TryDetectFromUnityPlayerDll(string gamePath)
  {
    try
    {
      var unityPlayerPath = Path.Combine(gamePath, "UnityPlayer.dll");
      if (File.Exists(unityPlayerPath))
      {
        var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(unityPlayerPath);
        if (!string.IsNullOrWhiteSpace(versionInfo.ProductVersion))
        {
          // ProductVersion often contains Unity version
          var version = versionInfo.ProductVersion.Split(' ').FirstOrDefault();
          if (!string.IsNullOrWhiteSpace(version) && version.Contains('.'))
          {
            return version;
          }
        }
      }
    }
    catch
    {
      // Ignore
    }

    return null;
  }
}
