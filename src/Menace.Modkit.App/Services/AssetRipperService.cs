using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Menace.Modkit.Core.Models;

namespace Menace.Modkit.App.Services;

public class AssetRipperService
{
    private Process? _assetRipperProcess;
    private readonly int _port = 5734; // Fixed port for AssetRipper
    private readonly List<string> _processOutput = new();

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

    public async Task<bool> ExtractAssetsAsync(Action<string>? progressCallback = null)
    {
        try
        {
            // Check if extraction is actually needed (change detection)
            var gameInstallPath = AppSettings.Instance.GameInstallPath;
            if (await IsExtractionUpToDate(gameInstallPath, progressCallback))
            {
                return true;
            }

            progressCallback?.Invoke("Starting AssetRipper server...");

            // Kill any stale AssetRipper on our port from a previous run
            KillExistingOnPort();

            // Find AssetRipper executable
            var assetRipperPath = FindAssetRipper();
            if (assetRipperPath == null)
            {
                progressCallback?.Invoke("AssetRipper not found. Expected at: third_party/bundled/AssetRipper/");
                return false;
            }

            // Get game data path
            var dataPath = Path.Combine(gameInstallPath, "Menace_Data");
            if (!Directory.Exists(dataPath))
            {
                progressCallback?.Invoke($"Game data not found at {dataPath}");
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
                await Task.Delay(1000);
                try
                {
                    var probe = await client.GetAsync($"http://localhost:{_port}/");
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
                    }));

                if (!settingsResponse.IsSuccessStatusCode)
                    progressCallback?.Invoke($"Warning: Settings update returned {settingsResponse.StatusCode}, continuing with defaults");
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"Warning: Failed to update settings ({ex.Message}), continuing with defaults");
            }

            // Load folder (this call blocks until loading completes)
            progressCallback?.Invoke($"Loading game assets from {dataPath}...");
            var loadResponse = await client.PostAsync(
                $"http://localhost:{_port}/LoadFolder",
                new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("Path", dataPath)
                }));

            if (!loadResponse.IsSuccessStatusCode)
            {
                progressCallback?.Invoke($"Failed to load assets (Status: {loadResponse.StatusCode})");
                return false;
            }

            // Verify loading completed by checking collection count
            progressCallback?.Invoke("Verifying asset collections loaded...");
            await WaitForLoadCompletion(client, progressCallback);

            // Export primary content (returns immediately, export runs async)
            progressCallback?.Invoke($"Exporting assets to {OutputPath}...");
            var exportResponse = await client.PostAsync(
                $"http://localhost:{_port}/Export/PrimaryContent",
                new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("Path", OutputPath)
                }));

            if (!exportResponse.IsSuccessStatusCode)
            {
                progressCallback?.Invoke($"Failed to export assets (Status: {exportResponse.StatusCode})");
                return false;
            }

            // Poll output directory until export stabilizes
            var fileCount = await WaitForExportCompletion(progressCallback);

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
        catch (Exception ex)
        {
            progressCallback?.Invoke($"Error: {ex.Message}");
            return false;
        }
        finally
        {
            // Stop AssetRipper server
            if (_assetRipperProcess != null && !_assetRipperProcess.HasExited)
            {
                _assetRipperProcess.Kill();
                _assetRipperProcess.Dispose();
                _assetRipperProcess = null;
            }
        }
    }

    private async Task WaitForLoadCompletion(HttpClient client, Action<string>? progressCallback)
    {
        // Try /Collections/Count to verify loading — not all AssetRipper versions have this.
        // If the endpoint doesn't exist (404), skip verification and proceed.
        var timeout = TimeSpan.FromMinutes(10);
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            var elapsed = (int)(DateTime.UtcNow - start).TotalSeconds;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
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

            await Task.Delay(3000);
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

    private async Task<int> WaitForExportCompletion(Action<string>? progressCallback)
    {
        var timeout = TimeSpan.FromMinutes(30);
        var start = DateTime.UtcNow;
        int lastFileCount = 0;
        int stableChecks = 0;
        const int stableThreshold = 3; // 3 consecutive checks with no change = done

        while (DateTime.UtcNow - start < timeout)
        {
            await Task.Delay(5000);

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

    private string? FindAssetRipper()
    {
        // Determine platform subdirectory and executable name
        string platformDir = OperatingSystem.IsWindows() ? "windows" : "linux";
        string executableName = OperatingSystem.IsWindows()
            ? "AssetRipper.GUI.Free.exe"
            : "AssetRipper.GUI.Free";

        // Search in multiple locations
        var candidates = new[]
        {
            // Bundled with app output
            Path.Combine(AppContext.BaseDirectory, "third_party", "bundled", "AssetRipper", platformDir, executableName),
            // Source tree (development)
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "third_party", "bundled", "AssetRipper", platformDir, executableName),
            // Alongside the executable
            Path.Combine(AppContext.BaseDirectory, "AssetRipper", platformDir, executableName),
        };

        foreach (var candidate in candidates)
        {
            var resolved = Path.GetFullPath(candidate);
            if (File.Exists(resolved))
            {
                // Ensure it's executable on Linux/Mac
                if (!OperatingSystem.IsWindows())
                {
                    try
                    {
                        var chmod = Process.Start("chmod", $"+x \"{resolved}\"");
                        chmod?.WaitForExit();
                    }
                    catch
                    {
                        // chmod failed, might already be executable
                    }
                }

                return resolved;
            }
        }

        return null;
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
                try { proc.Kill(); proc.WaitForExit(3000); } catch { }
            }
        }
        catch { }

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                var info = new ProcessStartInfo("fuser", $"-k {_port}/tcp")
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                Process.Start(info)?.WaitForExit(3000);
            }
        }
        catch { }
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
        catch { }
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
