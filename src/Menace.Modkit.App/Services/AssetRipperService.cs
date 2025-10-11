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
                progressCallback?.Invoke("Error: AssetRipper not found");
                return false;
            }

            // Get game data path
            var gameInstallPath = AppSettings.Instance.GameInstallPath;
            var dataPath = Path.Combine(gameInstallPath, "Menace_Data");
            if (!Directory.Exists(dataPath))
            {
                progressCallback?.Invoke($"Error: Game data not found at {dataPath}");
                return false;
            }

            // Create output directory
            Directory.CreateDirectory(_outputPath);

            // Start AssetRipper server (headless, no browser)
            var startInfo = new ProcessStartInfo
            {
                FileName = assetRipperPath,
                Arguments = $"--launch-browser=false --port={_port}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _assetRipperProcess = Process.Start(startInfo);
            if (_assetRipperProcess == null)
            {
                progressCallback?.Invoke("Error: Failed to start AssetRipper");
                return false;
            }

            // Wait for server to start
            progressCallback?.Invoke("Waiting for AssetRipper server to start...");
            await Task.Delay(3000);

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10);

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
        // Check bundled AssetRipper
        var bundledAssetRipper = Path.Combine(
            AppContext.BaseDirectory,
            "third_party", "bundled", "AssetRipper", "AssetRipper.GUI.Free");

        if (File.Exists(bundledAssetRipper))
        {
            // Ensure it's executable on Linux/Mac
            try
            {
                var chmod = Process.Start("chmod", $"+x \"{bundledAssetRipper}\"");
                chmod?.WaitForExit();
            }
            catch
            {
                // chmod failed, might already be executable or on Windows
            }

            return bundledAssetRipper;
        }

        return null;
    }
}
