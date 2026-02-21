using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Menace.Modkit.Core.Models;

namespace Menace.Modkit.App.Services;

public class AssetRipperService
{
    private Process? _assetRipperProcess;
    private int _port;
    private readonly List<string> _processOutput = new();
    private CancellationTokenSource? _extractionCts;

    /// <summary>
    /// Indicates whether an extraction is currently in progress.
    /// </summary>
    public bool IsExtracting => _extractionCts != null;

    public AssetRipperService()
    {
        _port = FindAvailablePort();
    }

    /// <summary>
    /// Cancels the current extraction operation, if any.
    /// </summary>
    public void CancelExtraction()
    {
        _extractionCts?.Cancel();
    }

    /// <summary>
    /// Find an available port for the AssetRipper server.
    /// Uses a preferred port (5734) if available, otherwise finds a random available port.
    /// </summary>
    private static int FindAvailablePort()
    {
        const int preferredPort = 5734;

        // Try the preferred port first
        if (IsPortAvailable(preferredPort))
            return preferredPort;

        // If preferred port is busy, find a random available port
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            return port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    /// <summary>
    /// Finds the game data folder (Menace_Data) using case-insensitive search on Linux.
    /// Returns the full path to the data folder, or null if not found.
    /// </summary>
    private static string? FindGameDataPath(string gameInstallPath)
    {
        if (!Directory.Exists(gameInstallPath))
            return null;

        // Direct check (works on case-insensitive filesystems like Windows/macOS)
        var expectedPath = Path.Combine(gameInstallPath, "Menace_Data");
        if (Directory.Exists(expectedPath))
            return expectedPath;

        // On case-sensitive filesystems (Linux), search for the folder
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(gameInstallPath))
                {
                    var dirName = Path.GetFileName(dir);
                    if (dirName != null && dirName.Equals("Menace_Data", StringComparison.OrdinalIgnoreCase))
                        return dir;
                }
            }
            catch
            {
                // Directory access issues
            }
        }

        return null;
    }

    /// <summary>
    /// Resolved output path — uses configured or default path.
    /// </summary>
    private string OutputPath => AppSettings.GetAssetExtractionOutputPath();

    public bool HasExtractedAssets()
    {
        var effectivePath = AppSettings.GetEffectiveAssetsPath();
        if (effectivePath == null) return false;
        return Directory.GetFiles(effectivePath, "*.*", SearchOption.AllDirectories).Length > 0;
    }

    /// <summary>
    /// Check if AssetRipper is available (cached or bundled).
    /// </summary>
    public bool IsAssetRipperAvailable() => ComponentManager.Instance.GetAssetRipperPath() != null;

    /// <summary>
    /// Check if AssetRipper needs to be downloaded.
    /// </summary>
    public async Task<bool> NeedsAssetRipperDownloadAsync() =>
        !await ComponentManager.Instance.IsComponentCurrentAsync("AssetRipper");

    public async Task<bool> ExtractAssetsAsync(Action<string>? progressCallback = null, CancellationToken externalToken = default)
    {
        // Create a linked token source so we can cancel via CancelExtraction() or external token
        _extractionCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        var cancellationToken = _extractionCts.Token;

        try
        {
            // Check if extraction is actually needed (change detection)
            var gameInstallPath = AppSettings.Instance.GameInstallPath;
            if (await IsExtractionUpToDate(gameInstallPath, progressCallback))
            {
                return true;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Find AssetRipper executable (checks cache first, then bundled)
            var assetRipperPath = ComponentManager.Instance.GetAssetRipperPath();
            if (assetRipperPath == null)
            {
                // AssetRipper not available - UI should prompt for download
                progressCallback?.Invoke("AssetRipper not installed. Click 'Download AssetRipper' to enable asset extraction.");
                return false;
            }

            progressCallback?.Invoke("Starting AssetRipper server...");

            // Kill any stale AssetRipper on our port from a previous run
            KillExistingOnPort();

            // Get game data path (case-insensitive for Linux)
            var dataPath = FindGameDataPath(gameInstallPath);
            if (dataPath == null)
            {
                progressCallback?.Invoke($"Game data folder not found in {gameInstallPath}");
                return false;
            }

            // Create output directory
            Directory.CreateDirectory(OutputPath);

            // Start AssetRipper server (headless, no browser)
            var startInfo = new ProcessStartInfo
            {
                FileName = assetRipperPath,
                Arguments = $"--launch-browser=false --port={_port}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(assetRipperPath)
            };

            _assetRipperProcess = Process.Start(startInfo);
            if (_assetRipperProcess == null)
            {
                progressCallback?.Invoke("Failed to start AssetRipper process");
                return false;
            }

            // Consume stdout/stderr asynchronously to prevent pipe buffer deadlock.
            // AssetRipper produces thousands of lines during loading — if the pipe
            // fills up (64KB on Linux), the process blocks on write and dies.
            _processOutput.Clear();
            _assetRipperProcess.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    lock (_processOutput) _processOutput.Add(e.Data);
            };
            _assetRipperProcess.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    lock (_processOutput) _processOutput.Add($"[stderr] {e.Data}");
            };
            _assetRipperProcess.BeginOutputReadLine();
            _assetRipperProcess.BeginErrorReadLine();

            // Wait for server to become available (retry with backoff)
            progressCallback?.Invoke("Waiting for AssetRipper server to start...");
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(30);

            bool serverReady = false;
            for (int attempt = 0; attempt < 15; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(1000, cancellationToken);
                try
                {
                    var probe = await client.GetAsync($"http://localhost:{_port}/", cancellationToken);
                    if (probe.IsSuccessStatusCode)
                    {
                        serverReady = true;
                        break;
                    }
                }
                catch (HttpRequestException)
                {
                    // Server not ready yet
                }

                if (_assetRipperProcess.HasExited)
                {
                    DumpProcessLog();
                    progressCallback?.Invoke($"AssetRipper exited with code {_assetRipperProcess.ExitCode}. Full log: {GetProcessLogPath()}");
                    return false;
                }
            }

            if (!serverReady)
            {
                progressCallback?.Invoke("AssetRipper server did not start within 15 seconds");
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Configure export settings: Texture2D mode exports sprites as PNG
            progressCallback?.Invoke("Configuring export settings (SpriteExportMode: Texture2D)...");
            try
            {
                var settingsResponse = await client.PostAsync(
                    $"http://localhost:{_port}/Settings/Update",
                    new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("SpriteExportMode", "Texture2D"),
                        new KeyValuePair<string, string>("ImageExportFormat", "Png"),
                    }),
                    cancellationToken);

                if (!settingsResponse.IsSuccessStatusCode)
                    progressCallback?.Invoke($"Warning: Settings update returned {settingsResponse.StatusCode}, continuing with defaults");
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"Warning: Failed to update settings ({ex.Message}), continuing with defaults");
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Load folder (this call blocks until loading completes)
            progressCallback?.Invoke($"Loading game assets from {dataPath}...");
            var loadResponse = await client.PostAsync(
                $"http://localhost:{_port}/LoadFolder",
                new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("Path", dataPath)
                }),
                cancellationToken);

            if (!loadResponse.IsSuccessStatusCode)
            {
                progressCallback?.Invoke($"Failed to load assets (Status: {loadResponse.StatusCode})");
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Verify loading completed by checking collection count
            progressCallback?.Invoke("Verifying asset collections loaded...");
            await WaitForLoadCompletion(client, progressCallback, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Export primary content (returns immediately, export runs async)
            progressCallback?.Invoke($"Exporting assets to {OutputPath}...");
            var exportResponse = await client.PostAsync(
                $"http://localhost:{_port}/Export/PrimaryContent",
                new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("Path", OutputPath)
                }),
                cancellationToken);

            if (!exportResponse.IsSuccessStatusCode)
            {
                progressCallback?.Invoke($"Failed to export assets (Status: {exportResponse.StatusCode})");
                return false;
            }

            // Poll output directory until export stabilizes
            var fileCount = await WaitForExportCompletion(progressCallback, cancellationToken);

            if (fileCount > 0)
            {
                // Update manifest to record successful extraction
                await SaveAssetRipManifestAsync(gameInstallPath);
                progressCallback?.Invoke($"Asset extraction completed! Extracted {fileCount} files.");
                return true;
            }
            else
            {
                progressCallback?.Invoke("Export completed but no files were found");
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            progressCallback?.Invoke("Extraction was cancelled.");
            return false;
        }
        catch (HttpRequestException ex)
        {
            progressCallback?.Invoke("Failed to communicate with AssetRipper. Please check your internet connection and try again.");
            ModkitLog.Error($"[AssetRipperService] HTTP request failed: {ex.Message}");
            return false;
        }
        catch (IOException ex) when (ex.HResult == -2147024784) // Disk full
        {
            progressCallback?.Invoke("Not enough disk space to extract assets. Please free up space and try again.");
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            progressCallback?.Invoke("Permission denied. Try running the application as administrator or check folder permissions.");
            return false;
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke($"Extraction failed: {ex.Message}. See the log file for details.");
            ModkitLog.Error($"[AssetRipperService] Extraction failed: {ex}");
            return false;
        }
        finally
        {
            _extractionCts?.Dispose();
            _extractionCts = null;
            // Stop AssetRipper server
            if (_assetRipperProcess != null && !_assetRipperProcess.HasExited)
            {
                _assetRipperProcess.Kill();
                _assetRipperProcess.Dispose();
                _assetRipperProcess = null;
            }
        }
    }

    private async Task WaitForLoadCompletion(HttpClient client, Action<string>? progressCallback, CancellationToken cancellationToken = default)
    {
        // Try /Collections/Count to verify loading — not all AssetRipper versions have this.
        // If the endpoint doesn't exist (404), skip verification and proceed.
        var timeout = TimeSpan.FromMinutes(10);
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var elapsed = (int)(DateTime.UtcNow - start).TotalSeconds;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                var response = await client.GetAsync($"http://localhost:{_port}/Collections/Count", cts.Token);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    progressCallback?.Invoke("Load verification endpoint not available, proceeding to export...");
                    return;
                }

                var content = await response.Content.ReadAsStringAsync(cts.Token);
                var count = ParseCollectionCount(content);

                if (count > 0)
                {
                    progressCallback?.Invoke($"Loaded {count} asset collections.");
                    return;
                }

                progressCallback?.Invoke($"Loading assets... ({elapsed}s)");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // Re-throw if it's the main cancellation token
            }
            catch (OperationCanceledException)
            {
                progressCallback?.Invoke($"Loading assets... ({elapsed}s, server busy)");
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"Loading assets... ({elapsed}s, {ex.GetType().Name})");
            }

            if (_assetRipperProcess?.HasExited == true)
            {
                DumpProcessLog();
                var lastLines = GetLastProcessOutput(10);
                throw new Exception($"AssetRipper crashed (code {_assetRipperProcess.ExitCode}). Full log: {GetProcessLogPath()}\nLast output: {lastLines}");
            }

            await Task.Delay(3000, cancellationToken);
        }

        progressCallback?.Invoke("Load verification timed out, proceeding to export...");
    }

    private static int ParseCollectionCount(string content)
    {
        content = content.Trim();

        // Try bare integer first
        if (int.TryParse(content, out var bare))
            return bare;

        // Try JSON: could be {"count":N}, {"Count":N}, or just N as JSON
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Bare JSON number
            if (root.ValueKind == JsonValueKind.Number)
                return root.GetInt32();

            // JSON object — look for any integer property
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Number)
                        return prop.Value.GetInt32();
                }
            }
        }
        catch
        {
            // Not valid JSON
        }

        return -1;
    }

    private async Task<int> WaitForExportCompletion(Action<string>? progressCallback, CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromMinutes(30);
        var start = DateTime.UtcNow;
        int lastFileCount = 0;
        int stableChecks = 0;
        const int stableThreshold = 3; // 3 consecutive checks with no change = done

        while (DateTime.UtcNow - start < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(5000, cancellationToken);

            int currentCount;
            try
            {
                currentCount = Directory.Exists(OutputPath)
                    ? Directory.GetFiles(OutputPath, "*.*", SearchOption.AllDirectories).Length
                    : 0;
            }
            catch
            {
                // Directory enumeration can fail transiently during writes
                continue;
            }

            progressCallback?.Invoke($"Exporting... {currentCount} files written");

            if (currentCount > 0 && currentCount == lastFileCount)
            {
                stableChecks++;
                if (stableChecks >= stableThreshold)
                {
                    return currentCount;
                }
            }
            else
            {
                stableChecks = 0;
                lastFileCount = currentCount;
            }

            if (_assetRipperProcess?.HasExited == true)
            {
                // Process exited — final count
                var finalCount = Directory.Exists(OutputPath)
                    ? Directory.GetFiles(OutputPath, "*.*", SearchOption.AllDirectories).Length
                    : 0;
                return finalCount;
            }
        }

        progressCallback?.Invoke("Warning: Export timed out after 30 minutes");
        return Directory.Exists(OutputPath)
            ? Directory.GetFiles(OutputPath, "*.*", SearchOption.AllDirectories).Length
            : 0;
    }

    private string GetLastProcessOutput(int lineCount)
    {
        lock (_processOutput)
        {
            var start = Math.Max(0, _processOutput.Count - lineCount);
            var lines = _processOutput.GetRange(start, _processOutput.Count - start);
            return string.Join("\n", lines);
        }
    }

    private void KillExistingOnPort()
    {
        // Kill any previous AssetRipper processes (by name) and anything on our port
        try
        {
            foreach (var proc in Process.GetProcessesByName("AssetRipper.GUI.Free"))
            {
                try { proc.Kill(); proc.WaitForExit(3000); }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Process may have already exited
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException)
        {
            // GetProcessesByName can fail on some systems
            ModkitLog.Info($"[AssetRipperService] Could not enumerate processes: {ex.Message}");
        }

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                // Use ArgumentList to prevent command injection
                var info = new ProcessStartInfo("fuser")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                info.ArgumentList.Add("-k");
                info.ArgumentList.Add($"{_port}/tcp");
                Process.Start(info)?.WaitForExit(3000);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // Non-critical: process may not exist or we may lack permissions
            ModkitLog.Info($"[AssetRipperService] Note: Could not kill existing process on port: {ex.Message}");
        }
    }

    private string GetProcessLogPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "assetripper_crash.log");
    }

    private void DumpProcessLog()
    {
        try
        {
            lock (_processOutput)
            {
                File.WriteAllLines(GetProcessLogPath(), _processOutput);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Non-critical: logging failure shouldn't stop the main operation
        }
    }

    /// <summary>
    /// Check if the current extracted assets are already up to date with the game version.
    /// Compares GameAssembly hash against the manifest from the last extraction.
    /// </summary>
    private async Task<bool> IsExtractionUpToDate(string gameInstallPath, Action<string>? progressCallback)
    {
        if (string.IsNullOrEmpty(gameInstallPath) || !Directory.Exists(gameInstallPath))
            return false;

        // Must have existing extracted assets to skip
        var effectivePath = AppSettings.GetEffectiveAssetsPath();
        if (effectivePath == null)
            return false;

        try
        {
            var manifestPath = GetAssetRipManifestPath(gameInstallPath);
            if (!File.Exists(manifestPath))
                return false;

            var json = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<ExtractionManifest>(json);
            if (manifest == null || manifest.AssetRipTimestamp == null)
                return false;

            // Compute current GameAssembly hash
            progressCallback?.Invoke("Checking if assets are up to date...");
            var currentHash = await ComputeGameAssemblyHash(gameInstallPath);
            if (string.IsNullOrEmpty(currentHash))
                return false;

            if (!manifest.NeedsAssetRip(currentHash, manifest.AssetRipProfileHash))
            {
                // Also verify that the output directory still exists with files
                int fileCount;
                try
                {
                    fileCount = Directory.GetFiles(effectivePath, "*.*", SearchOption.AllDirectories).Length;
                }
                catch
                {
                    return false;
                }

                if (fileCount > 0)
                {
                    var age = DateTime.UtcNow - manifest.AssetRipTimestamp.Value;
                    string ageStr = age.TotalHours < 1
                        ? $"{(int)age.TotalMinutes} minutes"
                        : age.TotalDays < 1
                            ? $"{(int)age.TotalHours} hours"
                            : $"{(int)age.TotalDays} days";

                    progressCallback?.Invoke(
                        $"Assets are up to date ({fileCount} files, extracted {ageStr} ago). Skipping extraction.");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[AssetRipperService] Change detection failed: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Save manifest after successful extraction so future runs can detect changes.
    /// </summary>
    private async Task SaveAssetRipManifestAsync(string gameInstallPath)
    {
        try
        {
            var manifestPath = GetAssetRipManifestPath(gameInstallPath);
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);

            // Load existing manifest or create new
            ExtractionManifest manifest;
            if (File.Exists(manifestPath))
            {
                var json = await File.ReadAllTextAsync(manifestPath);
                manifest = JsonSerializer.Deserialize<ExtractionManifest>(json) ?? new ExtractionManifest();
            }
            else
            {
                manifest = new ExtractionManifest();
            }

            // Update asset rip fields
            manifest.AssetRipTimestamp = DateTime.UtcNow;
            manifest.CachedAssetsPath = OutputPath;
            manifest.GameAssemblyHash = await ComputeGameAssemblyHash(gameInstallPath);

            var updatedJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(manifestPath, updatedJson);
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[AssetRipperService] Failed to save manifest: {ex.Message}");
        }
    }

    private static string GetAssetRipManifestPath(string gameInstallPath)
    {
        return Path.Combine(gameInstallPath, "UserData", "ExtractionCache", "manifest.json");
    }

    private static async Task<string> ComputeGameAssemblyHash(string gameInstallPath)
    {
        var soPath = Path.Combine(gameInstallPath, "GameAssembly.so");
        var dllPath = Path.Combine(gameInstallPath, "GameAssembly.dll");
        var assemblyPath = File.Exists(soPath) ? soPath : dllPath;

        if (!File.Exists(assemblyPath))
            return string.Empty;

        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(assemblyPath);
        var hash = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
