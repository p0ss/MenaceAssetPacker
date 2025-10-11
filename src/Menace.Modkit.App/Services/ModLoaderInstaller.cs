using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Handles installation of MelonLoader and DataExtractor mod
/// </summary>
public class ModLoaderInstaller
{
    private readonly string _gameInstallPath;

    public ModLoaderInstaller(string gameInstallPath)
    {
        _gameInstallPath = gameInstallPath;
    }

    public async Task<bool> InstallMelonLoaderAsync(Action<string>? progressCallback = null)
    {
        try
        {
            progressCallback?.Invoke("Installing MelonLoader...");

            // Use bundled MelonLoader from third_party
            var bundledMelonLoader = Path.Combine(
                AppContext.BaseDirectory,
                "third_party", "bundled", "MelonLoader");

            if (!Directory.Exists(bundledMelonLoader))
            {
                progressCallback?.Invoke("❌ Bundled MelonLoader not found");
                return false;
            }

            // Copy all MelonLoader files to game directory
            CopyDirectory(bundledMelonLoader, _gameInstallPath, progressCallback);

            progressCallback?.Invoke("✓ MelonLoader installed successfully");
            return true;
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke($"❌ Error installing MelonLoader: {ex.Message}");
            return false;
        }
    }

    private void CopyDirectory(string sourceDir, string destDir, Action<string>? progressCallback = null)
    {
        // Copy version.dll to root
        var versionDll = Path.Combine(sourceDir, "version.dll");
        if (File.Exists(versionDll))
        {
            File.Copy(versionDll, Path.Combine(destDir, "version.dll"), overwrite: true);
        }

        // Copy MelonLoader folder
        var sourceMelonLoaderDir = Path.Combine(sourceDir, "MelonLoader");
        var destMelonLoaderDir = Path.Combine(destDir, "MelonLoader");

        if (Directory.Exists(sourceMelonLoaderDir))
        {
            CopyDirectoryRecursive(sourceMelonLoaderDir, destMelonLoaderDir);
        }
    }

    private void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectoryRecursive(dir, destSubDir);
        }
    }

    public async Task<bool> InstallDataExtractorAsync(Action<string>? progressCallback = null)
    {
        try
        {
            progressCallback?.Invoke("Installing DataExtractor mod...");

            // Use bundled DataExtractor from third_party
            var dataExtractorDll = Path.Combine(
                AppContext.BaseDirectory,
                "third_party", "bundled", "DataExtractor", "Menace.DataExtractor.dll");

            if (!File.Exists(dataExtractorDll))
            {
                progressCallback?.Invoke("❌ Bundled DataExtractor.dll not found");
                return false;
            }

            // Copy to game's Mods folder
            var modsFolder = Path.Combine(_gameInstallPath, "Mods");
            Directory.CreateDirectory(modsFolder);

            var targetPath = Path.Combine(modsFolder, "Menace.DataExtractor.dll");
            File.Copy(dataExtractorDll, targetPath, overwrite: true);

            progressCallback?.Invoke("✓ DataExtractor mod installed successfully");
            return true;
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke($"❌ Error installing DataExtractor: {ex.Message}");
            return false;
        }
    }

    public bool IsMelonLoaderInstalled()
    {
        var melonLoaderDll = Path.Combine(_gameInstallPath, "MelonLoader", "MelonLoader.dll");
        return File.Exists(melonLoaderDll);
    }

    public bool IsDataExtractorInstalled()
    {
        var dataExtractorDll = Path.Combine(_gameInstallPath, "Mods", "Menace.DataExtractor.dll");
        return File.Exists(dataExtractorDll);
    }

    public async Task<bool> LaunchGameAsync(Action<string>? progressCallback = null)
    {
        try
        {
            progressCallback?.Invoke("Launching game to extract data...");
            progressCallback?.Invoke("The game will start and automatically extract template data.");
            progressCallback?.Invoke("You can close the game once you reach the main menu.");

            var gameExe = Path.Combine(_gameInstallPath, "Menace.exe");
            if (!File.Exists(gameExe))
            {
                progressCallback?.Invoke("❌ Game executable not found");
                return false;
            }

            // Launch game
            var startInfo = new ProcessStartInfo
            {
                FileName = gameExe,
                WorkingDirectory = _gameInstallPath,
                UseShellExecute = true
            };

            Process.Start(startInfo);

            progressCallback?.Invoke("✓ Game launched. Waiting for data extraction...");
            progressCallback?.Invoke("Once the game finishes loading, check the Stats tab to see if data appeared.");

            return true;
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke($"❌ Error launching game: {ex.Message}");
            return false;
        }
    }
}
