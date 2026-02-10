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

    public Task<bool> InstallMelonLoaderAsync(Action<string>? progressCallback = null)
    {
        try
        {
            progressCallback?.Invoke("Installing MelonLoader...");

            // MelonLoader is a required component (cached or bundled)
            var melonLoaderPath = ComponentManager.Instance.GetMelonLoaderPath();

            if (melonLoaderPath == null)
            {
                progressCallback?.Invoke("❌ MelonLoader not found. Please reinstall the app.");
                return Task.FromResult(false);
            }

            // Copy all MelonLoader files to game directory
            CopyDirectory(melonLoaderPath, _gameInstallPath, progressCallback);

            progressCallback?.Invoke("✓ MelonLoader installed successfully");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke($"❌ Error installing MelonLoader: {ex.Message}");
            return Task.FromResult(false);
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

        // Copy all subfolders (net6, net35, Dependencies, Documentation) into MelonLoader folder
        // The bundled structure has these directly inside third_party/bundled/MelonLoader/
        var destMelonLoaderDir = Path.Combine(destDir, "MelonLoader");
        Directory.CreateDirectory(destMelonLoaderDir);

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(subDir);
            var destSubDir = Path.Combine(destMelonLoaderDir, dirName);
            CopyDirectoryRecursive(subDir, destSubDir);
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

    public Task<bool> InstallDataExtractorAsync(Action<string>? progressCallback = null)
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
                return Task.FromResult(false);
            }

            // Copy to game's Mods folder
            var modsFolder = Path.Combine(_gameInstallPath, "Mods");
            Directory.CreateDirectory(modsFolder);

            var targetPath = Path.Combine(modsFolder, "Menace.DataExtractor.dll");
            File.Copy(dataExtractorDll, targetPath, overwrite: true);

            progressCallback?.Invoke("✓ DataExtractor mod installed successfully");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke($"❌ Error installing DataExtractor: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public Task<bool> InstallModpackLoaderAsync(Action<string>? progressCallback = null)
    {
        try
        {
            progressCallback?.Invoke("Installing ModpackLoader mod...");

            var modpackLoaderDir = Path.Combine(
                AppContext.BaseDirectory,
                "third_party", "bundled", "ModpackLoader");

            if (!Directory.Exists(modpackLoaderDir))
            {
                progressCallback?.Invoke("❌ Bundled ModpackLoader directory not found");
                return Task.FromResult(false);
            }

            var modsFolder = Path.Combine(_gameInstallPath, "Mods");
            Directory.CreateDirectory(modsFolder);

            // Copy all files (ModpackLoader.dll + Roslyn dependencies)
            foreach (var file in Directory.GetFiles(modpackLoaderDir, "*.dll"))
            {
                var targetPath = Path.Combine(modsFolder, Path.GetFileName(file));
                File.Copy(file, targetPath, overwrite: true);
            }

            progressCallback?.Invoke("✓ ModpackLoader mod installed successfully");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke($"❌ Error installing ModpackLoader: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public Task CleanModsDirectoryAsync(Action<string>? progressCallback = null)
    {
        var modsFolder = Path.Combine(_gameInstallPath, "Mods");

        if (Directory.Exists(modsFolder))
        {
            progressCallback?.Invoke("Deleting Mods directory contents...");

            foreach (var file in Directory.GetFiles(modsFolder))
                File.Delete(file);

            foreach (var dir in Directory.GetDirectories(modsFolder))
                Directory.Delete(dir, true);
        }

        Directory.CreateDirectory(modsFolder);
        progressCallback?.Invoke("✓ Mods directory cleaned");
        return Task.CompletedTask;
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

    public Task<bool> LaunchGameAsync(Action<string>? progressCallback = null)
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
                return Task.FromResult(false);
            }

            ProcessStartInfo startInfo;

            // On Linux, launch through Steam for Proton support
            if (OperatingSystem.IsLinux())
            {
                // Menace Steam App ID
                const string appId = "2432860";
                startInfo = new ProcessStartInfo
                {
                    FileName = "steam",
                    Arguments = $"steam://rungameid/{appId}",
                    UseShellExecute = true
                };
                progressCallback?.Invoke("Launching via Steam (Proton)...");
            }
            else
            {
                // On Windows, launch directly
                startInfo = new ProcessStartInfo
                {
                    FileName = gameExe,
                    WorkingDirectory = _gameInstallPath,
                    UseShellExecute = true
                };
            }

            Process.Start(startInfo);

            progressCallback?.Invoke("✓ Game launched. Waiting for data extraction...");
            progressCallback?.Invoke("Once the game finishes loading, check the Stats tab to see if data appeared.");

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke($"❌ Error launching game: {ex.Message}");
            return Task.FromResult(false);
        }
    }
}
