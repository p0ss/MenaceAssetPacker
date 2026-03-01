using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Manages downloadable components (MelonLoader, AssetRipper, etc.) with version checking
/// and on-demand downloading. Components are cached in ~/.menace-modkit/components/.
/// </summary>
public sealed class ComponentManager : IDisposable
{
    private static readonly Lazy<ComponentManager> _instance = new(() => new ComponentManager());
    public static ComponentManager Instance => _instance.Value;

    /// <summary>
    /// Base URL for component downloads from the repo.
    /// </summary>
    private const string GitHubRawBaseUrl = "https://raw.githubusercontent.com/p0ss/MenaceAssetPacker/main/third_party/components";

    private readonly string _componentsCachePath;
    private readonly string _platform;
    private readonly HttpClient _httpClient;
    private bool _disposed;

    private VersionsManifest? _cachedRemoteManifest;
    private DateTime _lastRemoteFetch = DateTime.MinValue;
    private static readonly TimeSpan RemoteCacheDuration = TimeSpan.FromMinutes(5);

    private ComponentManager()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _componentsCachePath = Path.Combine(home, ".menace-modkit", "components");
        _platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : "linux-x64";
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
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
    /// Path where components are cached.
    /// </summary>
    public string ComponentsCachePath => _componentsCachePath;

    /// <summary>
    /// Current platform identifier.
    /// </summary>
    public string Platform => _platform;

    /// <summary>
    /// Get the status of all components (what's installed, outdated, missing).
    /// </summary>
    public async Task<List<ComponentStatus>> GetComponentStatusAsync(
        bool forceRemoteFetch = false,
        bool useBundledManifestOnly = false)
    {
        var manifest = useBundledManifestOnly
            ? GetBundledManifest()
            : await GetVersionsManifestAsync(forceRemoteFetch);
        var localManifest = GetLocalManifest();
        var bundledManifest = GetBundledManifest();
        var results = new List<ComponentStatus>();

        foreach (var (name, component) in manifest.Components)
        {
            var status = new ComponentStatus
            {
                Name = name,
                Description = component.Description,
                Category = component.Category,
                Required = component.Required,
                RequiredFor = component.RequiredFor,
                LatestVersion = component.Version,
                DownloadSize = GetDownloadSize(name, component),
                InstallPath = component.InstallPath,
                Channel = CurrentChannel
            };

            // Special case: Modkit component compares against running app version
            if (name == "Modkit")
            {
                status.InstalledVersion = ModkitVersion.MelonVersion;
                status.State = CompareVersions(ModkitVersion.MelonVersion, component.Version) >= 0
                    ? ComponentState.UpToDate
                    : ComponentState.Outdated;
            }
            // Check if installed (downloaded to cache)
            else if (localManifest.Components.TryGetValue(name, out var installed))
            {
                status.InstalledVersion = installed.Version;
                status.InstalledAt = installed.InstalledAt;
                status.State = installed.Version == component.Version
                    ? ComponentState.UpToDate
                    : ComponentState.Outdated;
            }
            // Check if bundled with the app
            else if (IsBundledComponentPresent(name))
            {
                // Use bundled version from the bundled manifest
                if (bundledManifest.Components.TryGetValue(name, out var bundled))
                {
                    status.InstalledVersion = bundled.Version;
                    status.State = bundled.Version == component.Version
                        ? ComponentState.UpToDate
                        : ComponentState.Outdated;
                }
                else
                {
                    // Bundled but no version info - assume it's current
                    status.InstalledVersion = component.Version;
                    status.State = ComponentState.UpToDate;
                }
            }
            // Check if already installed in game directory (MelonLoader specific)
            else if (name == "MelonLoader" && IsGameMelonLoaderPresent())
            {
                // User already has MelonLoader in their game.
                // Check version compatibility and whether update is available.
                var gamePath = AppSettings.Instance.GameInstallPath;
                if (!string.IsNullOrWhiteSpace(gamePath))
                {
                    var installer = new ModLoaderInstaller(gamePath);
                    var installedVersion = installer.GetInstalledMelonLoaderVersion();
                    status.InstalledVersion = installedVersion;

                    if (string.IsNullOrEmpty(installedVersion))
                    {
                        status.State = ComponentState.Outdated;
                    }
                    else if (NormalizeVersion(installedVersion) == NormalizeVersion(component.Version))
                    {
                        // Exact match
                        status.State = ComponentState.UpToDate;
                    }
                    else if (installer.IsInstalledMelonLoaderVersionCompatible(out _, out _, out _))
                    {
                        // Same family (e.g., 0.7.2.x) - compatible but update available
                        status.State = ComponentState.UpdateAvailable;
                    }
                    else
                    {
                        // Incompatible version - requires update
                        status.State = ComponentState.Outdated;
                    }
                }
                else
                {
                    status.State = ComponentState.NotInstalled;
                }
            }
            else
            {
                status.State = ComponentState.NotInstalled;
            }

            results.Add(status);
        }

        return results;
    }

    /// <summary>
    /// Check if a component is present in the bundled third_party folder.
    /// </summary>
    private bool IsBundledComponentPresent(string componentName)
    {
        switch (componentName)
        {
            case "MelonLoader":
                return GetBundledPath("MelonLoader") != null;

            case "DataExtractor":
                var dePath = GetBundledPath("DataExtractor");
                return dePath != null && File.Exists(Path.Combine(dePath, "Menace.DataExtractor.dll"));

            case "ModpackLoader":
                return GetBundledPath("ModpackLoader") != null;

            case "AssetRipper":
                return GetBundledAssetRipperPath() != null;

            case "DotNetRefs":
                return GetBundledPath("dotnet-refs") != null;

            // Addons are download-only
            default:
                return false;
        }
    }

    /// <summary>
    /// Check if MelonLoader is installed in the game directory.
    /// </summary>
    private bool IsGameMelonLoaderPresent()
    {
        try
        {
            var gamePath = AppSettings.Instance.GameInstallPath;
            if (string.IsNullOrEmpty(gamePath))
                return false;

            var mlDir = Path.Combine(gamePath, "MelonLoader");
            if (!Directory.Exists(mlDir))
                return false;

            // Check for core DLL in either root or net6 subdirectory
            var mlDll = Path.Combine(mlDir, "MelonLoader.dll");
            var mlDllNet6 = Path.Combine(mlDir, "net6", "MelonLoader.dll");
            return File.Exists(mlDll) || File.Exists(mlDllNet6);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if any required components need to be installed or updated.
    /// </summary>
    public async Task<bool> NeedsSetupAsync()
    {
        // Startup should be fast/offline-safe: use bundled versions and avoid remote fetch here.
        var statuses = await GetComponentStatusAsync(useBundledManifestOnly: true);
        // UpdateAvailable is non-blocking (compatible version installed)
        // Only NotInstalled and Outdated (incompatible) require setup
        return statuses.Any(s => s.Required &&
            (s.State == ComponentState.NotInstalled || s.State == ComponentState.Outdated));
    }

    /// <summary>
    /// Get components that need downloading (required + selected optional).
    /// </summary>
    public async Task<List<ComponentStatus>> GetPendingDownloadsAsync(HashSet<string>? selectedOptional = null)
    {
        var statuses = await GetComponentStatusAsync();
        return statuses
            .Where(s => s.State != ComponentState.UpToDate &&
                       (s.Required || selectedOptional?.Contains(s.Name) == true))
            .ToList();
    }

    /// <summary>
    /// Download and install a single component.
    /// </summary>
    public async Task<bool> DownloadComponentAsync(
        string componentName,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var manifest = await GetVersionsManifestAsync();
        if (!manifest.Components.TryGetValue(componentName, out var component))
        {
            progress?.Report(new DownloadProgress($"Unknown component: {componentName}", 0, 0));
            return false;
        }

        var downloadInfo = GetDownloadInfo(componentName, component);
        if (downloadInfo == null)
        {
            progress?.Report(new DownloadProgress($"No download available for {_platform}", 0, 0));
            return false;
        }

        try
        {
            progress?.Report(new DownloadProgress($"Connecting...", 0, 0));

            // Create component directory
            var componentPath = Path.Combine(_componentsCachePath, component.InstallPath);
            Directory.CreateDirectory(componentPath);

            // Download to temp file
            var tempFile = Path.Combine(Path.GetTempPath(), $"menace-{componentName}-{Guid.NewGuid()}.tmp");
            try
            {
                using var response = await _httpClient.GetAsync(downloadInfo.Url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? downloadInfo.Size;
                using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                using var fileStream = File.Create(tempFile);

                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;
                var stopwatch = Stopwatch.StartNew();
                long lastReportedBytes = 0;
                var lastReportTime = stopwatch.ElapsedMilliseconds;

                while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    totalRead += bytesRead;

                    var elapsed = stopwatch.ElapsedMilliseconds;
                    if (elapsed - lastReportTime >= 300 || totalRead == totalBytes)
                    {
                        var bytesPerSecond = (totalRead - lastReportedBytes) * 1000.0 / Math.Max(1, elapsed - lastReportTime);
                        lastReportedBytes = totalRead;
                        lastReportTime = elapsed;

                        if (totalBytes > 0)
                        {
                            var percent = (int)(totalRead * 90 / totalBytes); // 0-90%
                            var mb = totalRead / (1024.0 * 1024.0);
                            var totalMb = totalBytes / (1024.0 * 1024.0);
                            var speedMb = bytesPerSecond / (1024.0 * 1024.0);
                            progress?.Report(new DownloadProgress(
                                $"{mb:F1} / {totalMb:F1} MB ({speedMb:F1} MB/s)",
                                percent,
                                (long)bytesPerSecond));
                        }
                    }
                }

                fileStream.Close();

                // Verify checksum if provided
                if (!string.IsNullOrEmpty(downloadInfo.Sha256))
                {
                    progress?.Report(new DownloadProgress("Verifying...", 92, 0));
                    var actualHash = await ComputeFileHashAsync(tempFile, ct);
                    if (!string.Equals(actualHash, downloadInfo.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        progress?.Report(new DownloadProgress("Checksum mismatch!", 0, 0));
                        return false;
                    }
                }

                // Extract archive
                progress?.Report(new DownloadProgress("Extracting...", 95, 0));

                // Clear existing component directory
                if (Directory.Exists(componentPath))
                {
                    try { Directory.Delete(componentPath, true); }
                    catch { /* ignore */ }
                }
                Directory.CreateDirectory(componentPath);

                // Extract based on file type
                if (downloadInfo.Url.EndsWith(".zip"))
                {
                    SafeExtractZip(tempFile, componentPath);
                }
                else if (downloadInfo.Url.EndsWith(".tar.gz"))
                {
                    await ExtractTarGzAsync(tempFile, componentPath, ct);
                }

                // Set executable permissions on Linux
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    SetExecutablePermissions(componentPath);
                }

                // Update local manifest
                progress?.Report(new DownloadProgress("Updating manifest...", 98, 0));
                UpdateLocalManifest(componentName, component.Version, component.InstallPath);

                progress?.Report(new DownloadProgress("Complete!", 100, 0));
                return true;
            }
            finally
            {
                try { File.Delete(tempFile); } catch { /* ignore */ }
            }
        }
        catch (OperationCanceledException)
        {
            progress?.Report(new DownloadProgress("Cancelled", 0, 0));
            return false;
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[ComponentManager] Download failed for {componentName}: {ex.Message}");
            progress?.Report(new DownloadProgress($"Error: {ex.Message}", 0, 0));
            return false;
        }
    }

    /// <summary>
    /// Download multiple components with overall progress tracking.
    /// </summary>
    public async Task<bool> DownloadComponentsAsync(
        IEnumerable<string> componentNames,
        IProgress<MultiDownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var names = componentNames.ToList();
        var totalComponents = names.Count;
        var completedComponents = 0;
        var allSucceeded = true;

        foreach (var name in names)
        {
            ct.ThrowIfCancellationRequested();

            var componentProgress = new Progress<DownloadProgress>(p =>
            {
                progress?.Report(new MultiDownloadProgress(
                    name,
                    p.Message,
                    completedComponents,
                    totalComponents,
                    p.PercentComplete,
                    p.BytesPerSecond));
            });

            var success = await DownloadComponentAsync(name, componentProgress, ct);
            if (!success)
            {
                allSucceeded = false;
                // Continue with other components
            }

            completedComponents++;
        }

        return allSucceeded;
    }

    /// <summary>
    /// Get the path to a component's installation directory, or null if not installed.
    /// </summary>
    public string? GetComponentPath(string componentName)
    {
        var localManifest = GetLocalManifest();
        if (!localManifest.Components.TryGetValue(componentName, out var installed))
            return null;

        var path = Path.Combine(_componentsCachePath, installed.InstallPath);
        return Directory.Exists(path) ? path : null;
    }

    /// <summary>
    /// Get the path to a specific executable within a component.
    /// </summary>
    public string? GetExecutablePath(string componentName, string executableName)
    {
        var componentPath = GetComponentPath(componentName);
        if (componentPath == null) return null;

        var execPath = Path.Combine(componentPath, executableName);
        return File.Exists(execPath) ? execPath : null;
    }

    /// <summary>
    /// Check if a component is installed and up to date.
    /// </summary>
    public async Task<bool> IsComponentCurrentAsync(string componentName)
    {
        var statuses = await GetComponentStatusAsync();
        var status = statuses.FirstOrDefault(s => s.Name == componentName);
        return status?.State == ComponentState.UpToDate;
    }

    // --- Convenience methods for specific components ---

    public string? GetAssetRipperPath()
    {
        var componentPath = GetComponentPath("AssetRipper");
        if (componentPath == null)
        {
            // Fall back to bundled path
            return GetBundledAssetRipperPath();
        }

        var execName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "AssetRipper.GUI.Free.exe"
            : "AssetRipper.GUI.Free";
        var execPath = Path.Combine(componentPath, execName);
        return File.Exists(execPath) ? execPath : GetBundledAssetRipperPath();
    }

    public string? GetMelonLoaderPath()
    {
        var componentPath = GetComponentPath("MelonLoader");
        if (componentPath != null && Directory.Exists(componentPath))
            return componentPath;

        // Fall back to bundled path
        return GetBundledPath("MelonLoader");
    }

    public string? GetDataExtractorPath()
    {
        var componentPath = GetComponentPath("DataExtractor");
        if (componentPath != null)
        {
            var dllPath = Path.Combine(componentPath, "Menace.DataExtractor.dll");
            if (File.Exists(dllPath)) return dllPath;
        }

        // Fall back to bundled path
        var bundled = GetBundledPath("DataExtractor");
        if (bundled != null)
        {
            var dllPath = Path.Combine(bundled, "Menace.DataExtractor.dll");
            if (File.Exists(dllPath)) return dllPath;
        }

        return null;
    }

    public string? GetModpackLoaderPath()
    {
        var componentPath = GetComponentPath("ModpackLoader");
        if (componentPath != null && Directory.Exists(componentPath))
            return componentPath;

        return GetBundledPath("ModpackLoader");
    }

    public string? GetDotNetRefsPath()
    {
        var componentPath = GetComponentPath("DotNetRefs");
        if (componentPath != null && Directory.Exists(componentPath))
            return componentPath;

        return GetBundledPath("dotnet-refs");
    }

    // --- Private helpers ---

    private async Task<VersionsManifest> GetVersionsManifestAsync(bool forceRemote = false)
    {
        // Try remote first (with caching)
        if (forceRemote || _cachedRemoteManifest == null || DateTime.UtcNow - _lastRemoteFetch > RemoteCacheDuration)
        {
            try
            {
                var remoteManifest = await FetchRemoteManifestAsync();
                if (remoteManifest != null)
                {
                    _cachedRemoteManifest = remoteManifest;
                    _lastRemoteFetch = DateTime.UtcNow;
                    return remoteManifest;
                }
            }
            catch (Exception ex)
            {
                ModkitLog.Warn($"[ComponentManager] Failed to fetch remote manifest: {ex.Message}");
            }
        }

        if (_cachedRemoteManifest != null)
            return _cachedRemoteManifest;

        // Fall back to bundled versions.json
        return GetBundledManifest();
    }

    /// <summary>
    /// Get the appropriate manifest URL based on the current update channel.
    /// </summary>
    private string GetManifestUrlForChannel()
    {
        var channel = AppSettings.Instance.UpdateChannel;
        const string baseUrl = "https://raw.githubusercontent.com/p0ss/MenaceAssetPacker/main/third_party";

        return channel == "beta"
            ? $"{baseUrl}/versions-beta.json"
            : $"{baseUrl}/versions.json";
    }

    /// <summary>
    /// Returns the current update channel ("stable" or "beta").
    /// </summary>
    public string CurrentChannel => AppSettings.Instance.UpdateChannel;

    /// <summary>
    /// Returns true if the user is on the beta channel.
    /// </summary>
    public bool IsBetaChannel => AppSettings.Instance.IsBetaChannel;

    /// <summary>
    /// Invalidate the cached remote manifest.
    /// Call this when the update channel changes to force a fresh fetch.
    /// </summary>
    public void InvalidateManifestCache()
    {
        _cachedRemoteManifest = null;
        _lastRemoteFetch = DateTime.MinValue;
        ModkitLog.Info("[ComponentManager] Manifest cache invalidated");
    }

    private async Task<VersionsManifest?> FetchRemoteManifestAsync()
    {
        var manifestUrl = GetManifestUrlForChannel();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var json = await _httpClient.GetStringAsync(manifestUrl, cts.Token);
            return JsonSerializer.Deserialize<VersionsManifest>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[ComponentManager] Failed to fetch manifest from {manifestUrl}: {ex.Message}");
            return null;
        }
    }

    private VersionsManifest GetBundledManifest()
    {
        var path = FindVersionsJson(forCurrentChannel: true);
        if (path == null)
            return new VersionsManifest();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<VersionsManifest>(json, JsonOptions) ?? new VersionsManifest();
        }
        catch
        {
            return new VersionsManifest();
        }
    }

    private LocalManifest GetLocalManifest()
    {
        var path = Path.Combine(_componentsCachePath, "manifest.json");
        if (!File.Exists(path))
            return new LocalManifest();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<LocalManifest>(json, JsonOptions) ?? new LocalManifest();
        }
        catch
        {
            return new LocalManifest();
        }
    }

    private void UpdateLocalManifest(string componentName, string version, string installPath)
    {
        Directory.CreateDirectory(_componentsCachePath);
        var manifest = GetLocalManifest();

        manifest.Components[componentName] = new InstalledComponent
        {
            Version = version,
            InstallPath = installPath,
            InstalledAt = DateTime.UtcNow
        };

        var path = Path.Combine(_componentsCachePath, "manifest.json");
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private DownloadInfo? GetDownloadInfo(string componentName, ComponentInfo component)
    {
        // Get size/sha256 from versions.json if available
        DownloadInfo? baseInfo = null;
        if (component.Downloads.TryGetValue(_platform, out var platformDownload))
            baseInfo = platformDownload;
        else if (component.Downloads.TryGetValue("any", out var anyDownload))
            baseInfo = anyDownload;

        // Construct URL from app version
        var url = BuildComponentUrl(componentName);
        if (url == null)
            return null;

        return new DownloadInfo
        {
            Url = url,
            Sha256 = baseInfo?.Sha256 ?? "",
            Size = baseInfo?.Size ?? 0
        };
    }

    /// <summary>
    /// Build the download URL for a component.
    /// Components are downloaded directly from the repo via raw.githubusercontent.com.
    /// </summary>
    private string? BuildComponentUrl(string componentName)
    {
        var filename = componentName switch
        {
            // Core components
            "MelonLoader" => _platform == "win-x64" ? "MelonLoader-win-x64.zip" : "MelonLoader-linux-x64.tar.gz",
            "AssetRipper" => _platform == "win-x64" ? "AssetRipper-win-x64.zip" : "AssetRipper-linux-x64.tar.gz",
            "DataExtractor" => "DataExtractor.zip",
            "ModpackLoader" => "ModpackLoader.zip",
            "DotNetRefs" => "DotNetRefs.zip",
            // Addons
            "TwitchSquaddies" => "TwitchSquaddies.zip",
            "DevMode" => "DevMode.zip",
            _ => null
        };

        if (filename == null)
            return null;

        return $"{GitHubRawBaseUrl}/{filename}";
    }

    private long GetDownloadSize(string componentName, ComponentInfo component)
    {
        var info = GetDownloadInfo(componentName, component);
        return info?.Size ?? 0;
    }

    private string? FindVersionsJson(bool forCurrentChannel = false)
    {
        var filename = forCurrentChannel && IsBetaChannel ? "versions-beta.json" : "versions.json";

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "third_party", filename),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "third_party", filename),
        };

        foreach (var candidate in candidates)
        {
            var resolved = Path.GetFullPath(candidate);
            if (File.Exists(resolved))
                return resolved;
        }

        // Fall back to stable manifest if beta not found
        if (forCurrentChannel && IsBetaChannel)
            return FindVersionsJson(forCurrentChannel: false);

        return null;
    }

    private string? GetBundledPath(string subPath)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "third_party", "bundled", subPath),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "third_party", "bundled", subPath),
        };

        foreach (var candidate in candidates)
        {
            var resolved = Path.GetFullPath(candidate);
            if (Directory.Exists(resolved))
                return resolved;
        }

        return null;
    }

    private string? GetBundledAssetRipperPath()
    {
        var bundled = GetBundledPath("AssetRipper");
        if (bundled == null) return null;

        var platformDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" : "linux";
        var execName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "AssetRipper.GUI.Free.exe"
            : "AssetRipper.GUI.Free";

        var path = Path.Combine(bundled, platformDir, execName);
        return File.Exists(path) ? path : null;
    }

    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream, ct);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private static void SafeExtractZip(string archivePath, string destinationPath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var destFullPath = Path.GetFullPath(destinationPath);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.FullName))
                continue;

            var entryPath = Path.GetFullPath(Path.Combine(destinationPath, entry.FullName));

            // Zip Slip protection
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

    private static async Task ExtractTarGzAsync(string archivePath, string destinationPath, CancellationToken ct)
    {
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
            await process.WaitForExitAsync(ct);
            if (process.ExitCode != 0)
                throw new Exception($"tar extraction failed with exit code {process.ExitCode}");
        }
    }

    private static void SetExecutablePermissions(string directory)
    {
        try
        {
            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                // Set executable for files without extensions (likely binaries)
                if (string.IsNullOrEmpty(Path.GetExtension(file)))
                {
                    var psi = new ProcessStartInfo("chmod")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    psi.ArgumentList.Add("+x");
                    psi.ArgumentList.Add(file);
                    Process.Start(psi)?.WaitForExit(1000);
                }
            }
        }
        catch { /* ignore */ }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Compare two semver-ish version strings.
    /// Returns: positive if a > b, negative if a &lt; b, 0 if equal.
    /// </summary>
    private static int CompareVersions(string a, string b)
    {
        var partsA = a.Split('.', '-', '+');
        var partsB = b.Split('.', '-', '+');

        var maxParts = Math.Max(partsA.Length, partsB.Length);

        for (int i = 0; i < maxParts; i++)
        {
            var partA = i < partsA.Length ? partsA[i] : "0";
            var partB = i < partsB.Length ? partsB[i] : "0";

            // Try numeric comparison first
            if (int.TryParse(partA, out var numA) && int.TryParse(partB, out var numB))
            {
                if (numA != numB)
                    return numA.CompareTo(numB);
            }
            else
            {
                // Fall back to string comparison
                var cmp = string.Compare(partA, partB, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                    return cmp;
            }
        }

        return 0;
    }

    /// <summary>
    /// Normalize a version string for comparison.
    /// Extracts all numeric parts to handle versions like "0.7.2-ci.2403" vs assembly version "0.7.2.2403".
    /// </summary>
    private static string NormalizeVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return "";

        // Extract all numeric parts and join them
        var numbers = System.Text.RegularExpressions.Regex.Matches(version, @"\d+");
        var parts = new List<string>();
        foreach (System.Text.RegularExpressions.Match match in numbers)
        {
            parts.Add(match.Value);
        }
        return string.Join(".", parts);
    }

    // --- Self-Update Support ---

    /// <summary>
    /// Path to the update staging directory.
    /// </summary>
    public string UpdateStagingPath => Path.Combine(_componentsCachePath, "update-staging");

    /// <summary>
    /// Check if a self-update has been staged and is ready to apply.
    /// </summary>
    public bool HasStagedUpdate()
    {
        var stagingPath = UpdateStagingPath;
        if (!Directory.Exists(stagingPath))
            return false;

        // Check for marker file that indicates update is ready
        var markerFile = Path.Combine(stagingPath, ".update-ready");
        return File.Exists(markerFile);
    }

    /// <summary>
    /// Download the Modkit update and stage it for installation.
    /// Returns true if the update is ready to apply (requires app restart).
    /// </summary>
    public async Task<bool> DownloadAndStageSelfUpdateAsync(
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var manifest = await GetVersionsManifestAsync();
        if (!manifest.Components.TryGetValue("Modkit", out var component))
        {
            progress?.Report(new DownloadProgress("Modkit component not found in manifest", 0, 0));
            return false;
        }

        var downloadInfo = GetDownloadInfo("Modkit", component);
        if (downloadInfo == null)
        {
            progress?.Report(new DownloadProgress($"No download available for {_platform}", 0, 0));
            return false;
        }

        try
        {
            progress?.Report(new DownloadProgress("Connecting...", 0, 0));

            // Download to temp file
            var tempFile = Path.Combine(Path.GetTempPath(), $"menace-modkit-{Guid.NewGuid()}.tmp");
            try
            {
                using var response = await _httpClient.GetAsync(downloadInfo.Url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? downloadInfo.Size;
                using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                using var fileStream = File.Create(tempFile);

                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;
                var stopwatch = Stopwatch.StartNew();
                long lastReportedBytes = 0;
                var lastReportTime = stopwatch.ElapsedMilliseconds;

                while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    totalRead += bytesRead;

                    var elapsed = stopwatch.ElapsedMilliseconds;
                    if (elapsed - lastReportTime >= 300 || totalRead == totalBytes)
                    {
                        var bytesPerSecond = (totalRead - lastReportedBytes) * 1000.0 / Math.Max(1, elapsed - lastReportTime);
                        lastReportedBytes = totalRead;
                        lastReportTime = elapsed;

                        if (totalBytes > 0)
                        {
                            var percent = (int)(totalRead * 85 / totalBytes);
                            var mb = totalRead / (1024.0 * 1024.0);
                            var totalMb = totalBytes / (1024.0 * 1024.0);
                            var speedMb = bytesPerSecond / (1024.0 * 1024.0);
                            progress?.Report(new DownloadProgress(
                                $"{mb:F1} / {totalMb:F1} MB ({speedMb:F1} MB/s)",
                                percent,
                                (long)bytesPerSecond));
                        }
                    }
                }

                fileStream.Close();

                // Verify checksum if provided
                if (!string.IsNullOrEmpty(downloadInfo.Sha256))
                {
                    progress?.Report(new DownloadProgress("Verifying...", 88, 0));
                    var actualHash = await ComputeFileHashAsync(tempFile, ct);
                    if (!string.Equals(actualHash, downloadInfo.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        progress?.Report(new DownloadProgress("Checksum mismatch!", 0, 0));
                        return false;
                    }
                }

                // Extract to staging directory
                progress?.Report(new DownloadProgress("Staging update...", 92, 0));

                var stagingPath = UpdateStagingPath;
                if (Directory.Exists(stagingPath))
                {
                    try { Directory.Delete(stagingPath, true); }
                    catch { /* ignore */ }
                }
                Directory.CreateDirectory(stagingPath);

                // Extract based on file type
                if (downloadInfo.Url.EndsWith(".zip"))
                {
                    SafeExtractZip(tempFile, stagingPath);
                }
                else if (downloadInfo.Url.EndsWith(".tar.gz"))
                {
                    await ExtractTarGzAsync(tempFile, stagingPath, ct);
                }

                // Set executable permissions on Linux
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    SetExecutablePermissions(stagingPath);
                }

                // Create updater script
                progress?.Report(new DownloadProgress("Creating updater...", 96, 0));
                CreateUpdaterScript(stagingPath);

                // Mark update as ready
                var markerFile = Path.Combine(stagingPath, ".update-ready");
                await File.WriteAllTextAsync(markerFile, component.Version, ct);

                progress?.Report(new DownloadProgress("Update staged - restart to apply", 100, 0));
                return true;
            }
            finally
            {
                try { File.Delete(tempFile); } catch { /* ignore */ }
            }
        }
        catch (OperationCanceledException)
        {
            progress?.Report(new DownloadProgress("Cancelled", 0, 0));
            return false;
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[ComponentManager] Self-update download failed: {ex.Message}");
            progress?.Report(new DownloadProgress($"Error: {ex.Message}", 0, 0));
            return false;
        }
    }

    /// <summary>
    /// Create an updater script that will apply the staged update after the app exits.
    /// </summary>
    private void CreateUpdaterScript(string stagingPath)
    {
        var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var appExe = Environment.ProcessPath ?? Path.Combine(appDir, "Menace.Modkit.App");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var scriptPath = Path.Combine(stagingPath, "apply-update.bat");
            var script = $@"@echo off
echo Waiting for Modkit to exit...
:waitloop
tasklist /FI ""PID eq {Environment.ProcessId}"" 2>nul | find /i ""{Environment.ProcessId}"" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto waitloop
)
echo Applying update...
xcopy /E /Y /I ""{stagingPath}\*"" ""{appDir}\"" >nul 2>&1
if errorlevel 1 (
    echo Update failed. Please manually extract the update from:
    echo {stagingPath}
    pause
    exit /b 1
)
echo Update complete. Restarting...
start """" ""{appExe}""
rmdir /S /Q ""{stagingPath}""
";
            File.WriteAllText(scriptPath, script);
        }
        else
        {
            var scriptPath = Path.Combine(stagingPath, "apply-update.sh");
            var script = $@"#!/bin/bash
echo ""Waiting for Modkit to exit...""
while kill -0 {Environment.ProcessId} 2>/dev/null; do
    sleep 1
done
echo ""Applying update...""
cp -r ""{stagingPath}/""* ""{appDir}/"" 2>/dev/null
if [ $? -ne 0 ]; then
    echo ""Update failed. Please manually copy files from:""
    echo ""{stagingPath}""
    exit 1
fi
chmod +x ""{appExe}"" 2>/dev/null
echo ""Update complete. Restarting...""
""{appExe}"" &
rm -rf ""{stagingPath}""
";
            File.WriteAllText(scriptPath, script);
            // Make script executable
            var psi = new ProcessStartInfo("chmod")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("+x");
            psi.ArgumentList.Add(scriptPath);
            Process.Start(psi)?.WaitForExit(1000);
        }
    }

    /// <summary>
    /// Launch the updater script and exit the app.
    /// Call this after DownloadAndStageSelfUpdateAsync returns true.
    /// </summary>
    public void LaunchUpdaterAndExit()
    {
        var stagingPath = UpdateStagingPath;
        if (!HasStagedUpdate())
        {
            ModkitLog.Error("[ComponentManager] No staged update found");
            return;
        }

        string scriptPath;
        ProcessStartInfo psi;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            scriptPath = Path.Combine(stagingPath, "apply-update.bat");
            psi = new ProcessStartInfo("cmd.exe", $"/c \"{scriptPath}\"")
            {
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Minimized
            };
        }
        else
        {
            scriptPath = Path.Combine(stagingPath, "apply-update.sh");
            psi = new ProcessStartInfo("/bin/bash", scriptPath)
            {
                UseShellExecute = true
            };
        }

        if (!File.Exists(scriptPath))
        {
            ModkitLog.Error($"[ComponentManager] Updater script not found: {scriptPath}");
            return;
        }

        ModkitLog.Info("[ComponentManager] Launching updater script and exiting...");
        Process.Start(psi);
        Environment.Exit(0);
    }
}

// --- JSON Models ---

public class VersionsManifest
{
    public int SchemaVersion { get; set; }
    public string RemoteUrl { get; set; } = "";
    public Dictionary<string, ComponentInfo> Components { get; set; } = new();
}

public class ComponentInfo
{
    public string Version { get; set; } = "";
    public bool Required { get; set; }
    public string Category { get; set; } = "core";
    public string? RequiredFor { get; set; }
    public string Description { get; set; } = "";
    public string InstallPath { get; set; } = "";
    public Dictionary<string, DownloadInfo> Downloads { get; set; } = new();
}

public class DownloadInfo
{
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Size { get; set; }
}

public class LocalManifest
{
    public Dictionary<string, InstalledComponent> Components { get; set; } = new();
}

public class InstalledComponent
{
    public string Version { get; set; } = "";
    public string InstallPath { get; set; } = "";
    public DateTime InstalledAt { get; set; }
}

// --- Status Models ---

public enum ComponentState
{
    NotInstalled,
    Outdated,          // Incompatible version - blocks setup
    UpdateAvailable,   // Compatible but newer exists - informational only
    UpToDate
}

public class ComponentStatus
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public bool Required { get; set; }
    public string? RequiredFor { get; set; }
    public string? InstalledVersion { get; set; }
    public string LatestVersion { get; set; } = "";
    public DateTime? InstalledAt { get; set; }
    public long DownloadSize { get; set; }
    public string InstallPath { get; set; } = "";
    public ComponentState State { get; set; }

    /// <summary>
    /// The update channel this component info was fetched from ("stable" or "beta").
    /// </summary>
    public string Channel { get; set; } = "stable";

    public string DownloadSizeDisplay => DownloadSize > 0
        ? $"{DownloadSize / (1024.0 * 1024.0):F0} MB"
        : "Unknown";

    public string StateDisplay => State switch
    {
        ComponentState.UpToDate => "Up to date",
        ComponentState.UpdateAvailable => $"Update available ({InstalledVersion} → {LatestVersion})",
        ComponentState.Outdated => $"Incompatible ({InstalledVersion} → {LatestVersion})",
        ComponentState.NotInstalled => "Not installed",
        _ => "Unknown"
    };
}

/// <summary>
/// Progress information for single component download.
/// </summary>
public record DownloadProgress(string Message, int PercentComplete, long BytesPerSecond);

/// <summary>
/// Progress information for multi-component download.
/// </summary>
public record MultiDownloadProgress(
    string CurrentComponent,
    string Message,
    int CompletedComponents,
    int TotalComponents,
    int CurrentPercent,
    long BytesPerSecond)
{
    public int OverallPercent => TotalComponents > 0
        ? (CompletedComponents * 100 + CurrentPercent) / TotalComponents
        : 0;
}
