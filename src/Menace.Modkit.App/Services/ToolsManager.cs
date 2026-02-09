using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Manages external tools (AssetRipper, MelonLoader, etc.) that are downloaded on demand
/// rather than bundled with the app. Tools are cached in ~/.menace-modkit/tools/.
/// </summary>
public sealed class ToolsManager : IDisposable
{
    private static readonly Lazy<ToolsManager> _instance = new(() => new ToolsManager());
    public static ToolsManager Instance => _instance.Value;

    private readonly string _toolsCachePath;
    private readonly string _platform;
    private readonly HttpClient _httpClient;
    private bool _disposed;

    private ToolsManager()
    {
        // Cache tools in user's home directory
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _toolsCachePath = Path.Combine(home, ".menace-modkit", "tools");
        _platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : "linux-x64";
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MenaceModkit");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Path where tools are cached.
    /// </summary>
    public string ToolsCachePath => _toolsCachePath;

    /// <summary>
    /// Check if tools are installed and match the expected version.
    /// </summary>
    public bool AreToolsInstalled()
    {
        return GetCachedToolsVersion() >= GetExpectedToolsVersion();
    }

    /// <summary>
    /// Check if cached tools are outdated (installed but older than expected).
    /// </summary>
    public bool AreToolsOutdated()
    {
        var cached = GetCachedToolsVersion();
        if (cached == 0) return false; // Not installed, not "outdated"
        return cached < GetExpectedToolsVersion();
    }

    /// <summary>
    /// Get the version of currently cached tools, or 0 if not installed.
    /// </summary>
    public int GetCachedToolsVersion()
    {
        var versionFile = Path.Combine(_toolsCachePath, "tools-version.json");
        if (!File.Exists(versionFile))
            return 0;

        try
        {
            var json = File.ReadAllText(versionFile);
            var installed = JsonSerializer.Deserialize<ToolsVersionInfo>(json);
            return installed?.ToolsVersion ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Check if tools are bundled with the app (legacy/bundled builds).
    /// </summary>
    public bool AreToolsBundled()
    {
        var bundledPath = GetBundledToolsPath();
        if (bundledPath == null) return false;

        // Check for AssetRipper in bundled location
        var assetRipperDir = Path.Combine(bundledPath, "AssetRipper");
        return Directory.Exists(assetRipperDir) &&
               Directory.GetFiles(assetRipperDir, "*", SearchOption.AllDirectories).Length > 0;
    }

    /// <summary>
    /// Get path to AssetRipper executable, checking cache first, then bundled.
    /// Returns null if not found or if cached version is outdated.
    /// </summary>
    public string? GetAssetRipperPath()
    {
        string execName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "AssetRipper.GUI.Free.exe"
            : "AssetRipper.GUI.Free";

        // Check cache first (only if version is current)
        var cachePath = Path.Combine(_toolsCachePath, "AssetRipper", execName);
        if (File.Exists(cachePath))
        {
            if (AreToolsInstalled())
            {
                return cachePath;
            }
            else
            {
                ModkitLog.Info($"[ToolsManager] Cached AssetRipper is outdated (v{GetCachedToolsVersion()} < v{GetExpectedToolsVersion()})");
                // Don't return cached path - will trigger re-download
            }
        }

        // Check bundled (always valid, doesn't need version check)
        var bundled = GetBundledToolsPath();
        if (bundled != null)
        {
            var platformDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux";
            var bundledPath = Path.Combine(bundled, "AssetRipper", platformDir, execName);
            if (File.Exists(bundledPath))
                return bundledPath;
        }

        return null;
    }

    /// <summary>
    /// Get path to MelonLoader directory, checking cache first, then bundled.
    /// Returns null if not found or if cached version is outdated.
    /// </summary>
    public string? GetMelonLoaderPath()
    {
        // Check cache first (only if version is current)
        var cachePath = Path.Combine(_toolsCachePath, "MelonLoader");
        if (Directory.Exists(cachePath) && Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories).Length > 0)
        {
            if (AreToolsInstalled())
            {
                return cachePath;
            }
            else
            {
                ModkitLog.Info($"[ToolsManager] Cached MelonLoader is outdated (v{GetCachedToolsVersion()} < v{GetExpectedToolsVersion()})");
                // Don't return cached path - will trigger re-download
            }
        }

        // Check bundled (always valid)
        var bundled = GetBundledToolsPath();
        if (bundled != null)
        {
            var bundledPath = Path.Combine(bundled, "MelonLoader");
            if (Directory.Exists(bundledPath))
                return bundledPath;
        }

        return null;
    }

    /// <summary>
    /// Download and install tools from GitHub release.
    /// </summary>
    public async Task<bool> DownloadToolsAsync(Action<string, int>? progressCallback = null)
    {
        try
        {
            var downloadUrl = GetToolsDownloadUrl();
            if (string.IsNullOrEmpty(downloadUrl))
            {
                progressCallback?.Invoke("No download URL configured for this platform", 0);
                return false;
            }

            progressCallback?.Invoke("Preparing download...", 0);

            // Create cache directory
            Directory.CreateDirectory(_toolsCachePath);

            // Download to temp file
            var tempFile = Path.Combine(Path.GetTempPath(), $"menace-tools-{Guid.NewGuid()}.tmp");
            try
            {
                progressCallback?.Invoke($"Downloading tools from GitHub...", 5);

                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = File.Create(tempFile);

                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var percent = (int)(totalRead * 80 / totalBytes) + 10; // 10-90%
                        var mb = totalRead / (1024.0 * 1024.0);
                        var totalMb = totalBytes / (1024.0 * 1024.0);
                        progressCallback?.Invoke($"Downloading... {mb:F1} / {totalMb:F1} MB", percent);
                    }
                }

                fileStream.Close();

                // Extract archive
                progressCallback?.Invoke("Extracting tools...", 92);

                // Clear existing tools
                if (Directory.Exists(_toolsCachePath))
                {
                    foreach (var dir in Directory.GetDirectories(_toolsCachePath))
                    {
                        try { Directory.Delete(dir, true); }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            ModkitLog.Warn($"[ToolsManager] Failed to delete {dir}: {ex.Message}");
                        }
                    }
                }

                // Extract based on file type (with Zip Slip protection)
                if (downloadUrl.EndsWith(".zip"))
                {
                    SafeExtractZip(tempFile, _toolsCachePath);
                }
                else if (downloadUrl.EndsWith(".tar.gz"))
                {
                    await ExtractTarGzAsync(tempFile, _toolsCachePath);
                }

                // Make AssetRipper executable on Linux (using ArgumentList to prevent injection)
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var assetRipperExe = Path.Combine(_toolsCachePath, "AssetRipper", "AssetRipper.GUI.Free");
                    if (File.Exists(assetRipperExe))
                    {
                        try
                        {
                            var psi = new ProcessStartInfo("chmod")
                            {
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            psi.ArgumentList.Add("+x");
                            psi.ArgumentList.Add(assetRipperExe);
                            Process.Start(psi)?.WaitForExit();
                        }
                        catch (Exception ex)
                        {
                            // Non-critical: log as info since chmod failure on non-Linux is expected
                            ModkitLog.Info($"[ToolsManager] Failed to set executable permission: {ex.Message}");
                        }
                    }
                }

                progressCallback?.Invoke("Tools installed successfully!", 100);
                return true;
            }
            finally
            {
                // Clean up temp file
                try { File.Delete(tempFile); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Non-critical: temp file cleanup failure is not an error
                    ModkitLog.Info($"[ToolsManager] Could not delete temp file: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[ToolsManager] Download failed: {ex.Message}");
            progressCallback?.Invoke($"Download failed: {ex.Message}", 0);
            return false;
        }
    }

    /// <summary>
    /// Safely extract a zip file with Zip Slip protection.
    /// Validates each entry path before extraction.
    /// </summary>
    private static void SafeExtractZip(string archivePath, string destinationPath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var destFullPath = Path.GetFullPath(destinationPath);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.FullName))
                continue;

            var entryPath = Path.GetFullPath(Path.Combine(destinationPath, entry.FullName));

            // Validate path stays within destination (Zip Slip protection)
            if (!entryPath.StartsWith(destFullPath + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                !entryPath.Equals(destFullPath, StringComparison.Ordinal))
            {
                throw new System.Security.SecurityException($"Zip entry path traversal blocked: {entry.FullName}");
            }

            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                Directory.CreateDirectory(entryPath);
            }
            else
            {
                var entryDir = Path.GetDirectoryName(entryPath);
                if (!string.IsNullOrEmpty(entryDir))
                    Directory.CreateDirectory(entryDir);
                entry.ExtractToFile(entryPath, overwrite: true);
            }
        }
    }

    private async Task ExtractTarGzAsync(string archivePath, string destinationPath)
    {
        // Use tar command with ArgumentList to prevent command injection
        var psi = new ProcessStartInfo("tar")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-xzf");
        psi.ArgumentList.Add(archivePath);
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(destinationPath);

        var process = Process.Start(psi);
        if (process != null)
        {
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                throw new Exception($"tar extraction failed with exit code {process.ExitCode}");
        }
    }

    private string? GetToolsDownloadUrl()
    {
        try
        {
            var versionsPath = FindVersionsJson();
            if (versionsPath == null) return null;

            var json = File.ReadAllText(versionsPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("toolsDownloadUrls", out var urls) &&
                urls.TryGetProperty(_platform, out var url))
            {
                return url.GetString();
            }
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[ToolsManager] Failed to read download URL: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Get the tools version expected by this app build.
    /// </summary>
    public int GetExpectedToolsVersion() => Menace.ModkitVersion.ToolsVersion;

    private string? FindVersionsJson()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "third_party", "versions.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "third_party", "versions.json"),
        };

        foreach (var candidate in candidates)
        {
            var resolved = Path.GetFullPath(candidate);
            if (File.Exists(resolved))
                return resolved;
        }

        return null;
    }

    private string? GetBundledToolsPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "third_party", "bundled"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "third_party", "bundled"),
        };

        foreach (var candidate in candidates)
        {
            var resolved = Path.GetFullPath(candidate);
            if (Directory.Exists(resolved))
                return resolved;
        }

        return null;
    }

    private class ToolsVersionInfo
    {
        public int ToolsVersion { get; set; }
        public string Platform { get; set; } = "";
    }
}
