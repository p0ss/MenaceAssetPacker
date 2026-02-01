using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Menace.Modkit.App.Services;

public class AssetRipperService
{
    private readonly string _outputPath;
    private Process? _assetRipperProcess;
    private readonly int _port = 5734; // Fixed port for AssetRipper

    public AssetRipperService()
    {
        _outputPath = Path.Combine(
            AppContext.BaseDirectory,
            "out2", "assets");
    }

    public bool HasExtractedAssets()
    {
        return Directory.Exists(_outputPath) &&
               Directory.GetFiles(_outputPath, "*.*", SearchOption.AllDirectories).Length > 0;
    }

    public async Task<bool> ExtractAssetsAsync(Action<string>? progressCallback = null)
    {
        try
        {
            progressCallback?.Invoke("Starting AssetRipper server...");

            // Find AssetRipper executable
            var assetRipperPath = FindAssetRipper();
            if (assetRipperPath == null)
            {
                progressCallback?.Invoke("❌ AssetRipper not found. Expected at: third_party/bundled/AssetRipper/");
                return false;
            }

            // Get game data path
            var gameInstallPath = AppSettings.Instance.GameInstallPath;
            var dataPath = Path.Combine(gameInstallPath, "Menace_Data");
            if (!Directory.Exists(dataPath))
            {
                progressCallback?.Invoke($"❌ Game data not found at {dataPath}");
                return false;
            }

            // Create output directory
            Directory.CreateDirectory(_outputPath);

            // Start AssetRipper server (headless, no browser)
            var startInfo = new ProcessStartInfo
            {
                FileName = assetRipperPath,
                Arguments = $"--launch-browser=false --port={_port} --log=false",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(assetRipperPath)
            };

            _assetRipperProcess = Process.Start(startInfo);
            if (_assetRipperProcess == null)
            {
                progressCallback?.Invoke("❌ Failed to start AssetRipper process");
                return false;
            }

            // Wait for server to become available (retry with backoff)
            progressCallback?.Invoke("Waiting for AssetRipper server to start...");
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10);

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
                    progressCallback?.Invoke($"❌ AssetRipper exited with code {_assetRipperProcess.ExitCode}");
                    return false;
                }
            }

            if (!serverReady)
            {
                progressCallback?.Invoke("❌ AssetRipper server did not start within 15 seconds");
                return false;
            }

            // Load folder
            progressCallback?.Invoke($"Loading game assets from {dataPath}...");
            var loadResponse = await client.PostAsync(
                $"http://localhost:{_port}/LoadFolder",
                new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("Path", dataPath)
                }));

            if (!loadResponse.IsSuccessStatusCode)
            {
                progressCallback?.Invoke($"Error: Failed to load assets (Status: {loadResponse.StatusCode})");
                return false;
            }

            // Wait for loading to complete (check status periodically)
            progressCallback?.Invoke("Processing assets... this may take a minute...");
            await Task.Delay(10000); // Give it time to process

            // Export primary content
            progressCallback?.Invoke($"Exporting assets to {_outputPath}...");
            var exportResponse = await client.PostAsync(
                $"http://localhost:{_port}/Export/PrimaryContent",
                new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("Path", _outputPath)
                }));

            if (!exportResponse.IsSuccessStatusCode)
            {
                progressCallback?.Invoke($"Error: Failed to export assets (Status: {exportResponse.StatusCode})");
                return false;
            }

            // Wait for export to complete
            progressCallback?.Invoke("Exporting... this may take several minutes...");
            await Task.Delay(30000); // Export takes significant time

            // Check if files were created
            var fileCount = Directory.Exists(_outputPath)
                ? Directory.GetFiles(_outputPath, "*.*", SearchOption.AllDirectories).Length
                : 0;

            if (fileCount > 0)
            {
                progressCallback?.Invoke($"Asset extraction completed! Extracted {fileCount} files.");
                return true;
            }
            else
            {
                progressCallback?.Invoke("Warning: Export completed but no files were found");
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
}
