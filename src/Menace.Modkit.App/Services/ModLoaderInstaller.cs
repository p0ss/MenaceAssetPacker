using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Handles installation of MelonLoader and DataExtractor mod
/// </summary>
public class ModLoaderInstaller
{
    private readonly string _gameInstallPath;
    private static readonly string[] UnsupportedMelonLoaderVersions = { "0.9.1" };

    public ModLoaderInstaller(string gameInstallPath)
    {
        _gameInstallPath = gameInstallPath;
    }

    public Task<bool> InstallMelonLoaderAsync(Action<string>? progressCallback = null)
    {
        try
        {
            if (IsMelonLoaderInstalled() &&
                !IsInstalledMelonLoaderVersionCompatible(out var installedVersion, out var expectedVersion, out var reason))
            {
                var expectedText = string.IsNullOrWhiteSpace(expectedVersion) ? "required version" : expectedVersion;
                progressCallback?.Invoke($"Incompatible MelonLoader detected ({installedVersion ?? "unknown"}). Reinstalling {expectedText}.");
                if (!string.IsNullOrWhiteSpace(reason))
                    progressCallback?.Invoke($"  Reason: {reason}");
            }

            // Check if MelonLoader is already fully installed (DLL + version.dll)
            if (IsMelonLoaderFullyInstalled())
            {
                progressCallback?.Invoke("✓ MelonLoader already installed");
                return Task.FromResult(true);
            }

            progressCallback?.Invoke("Installing MelonLoader...");

            // MelonLoader is a required component (cached or bundled)
            var melonLoaderPath = ComponentManager.Instance.GetMelonLoaderPath();

            if (melonLoaderPath == null)
            {
                progressCallback?.Invoke("❌ MelonLoader component not found. Go to Setup tab to download required components.");
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

    /// <summary>
    /// Check if MelonLoader is fully installed (both MelonLoader.dll and version.dll present).
    /// This is stricter than IsMelonLoaderInstalled() which only checks for the DLL.
    /// </summary>
    public bool IsMelonLoaderFullyInstalled()
    {
        var versionDll = Path.Combine(_gameInstallPath, "version.dll");
        if (!File.Exists(versionDll))
            return false;

        if (!IsMelonLoaderInstalled())
            return false;

        return IsInstalledMelonLoaderVersionCompatible(out _, out _, out _);
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

    // Assemblies bundled with MelonLoader 0.7.2+ that we should NOT deploy
    // (and should remove if present from previous installs)
    private static readonly string[] MelonLoaderProvidedAssemblies = new[]
    {
        "System.Collections.Immutable.dll",
        "System.Memory.dll",
        "System.Buffers.dll"
    };

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
            var userLibsFolder = Path.Combine(_gameInstallPath, "UserLibs");
            Directory.CreateDirectory(modsFolder);
            Directory.CreateDirectory(userLibsFolder);

            // Remove assemblies that MelonLoader now provides (from previous installs)
            foreach (var melonProvided in MelonLoaderProvidedAssemblies)
            {
                var legacyUserLibsPath = Path.Combine(userLibsFolder, melonProvided);
                if (File.Exists(legacyUserLibsPath))
                {
                    File.Delete(legacyUserLibsPath);
                    progressCallback?.Invoke($"  Removed legacy {melonProvided} (MelonLoader provides this)");
                }

                var legacyModsPath = Path.Combine(modsFolder, melonProvided);
                if (File.Exists(legacyModsPath))
                {
                    File.Delete(legacyModsPath);
                }
            }

            // Copy ModpackLoader.dll to Mods folder
            // Copy dependencies (Roslyn, etc.) to UserLibs where MelonLoader can find them
            foreach (var file in Directory.GetFiles(modpackLoaderDir, "*.dll"))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.StartsWith("Menace.ModpackLoader", StringComparison.OrdinalIgnoreCase))
                {
                    // Main mod DLL goes to Mods
                    var targetPath = Path.Combine(modsFolder, fileName);
                    File.Copy(file, targetPath, overwrite: true);
                }
                else
                {
                    // Dependencies go to UserLibs for MelonLoader's assembly resolver
                    var targetPath = Path.Combine(userLibsFolder, fileName);
                    File.Copy(file, targetPath, overwrite: true);

                    // Remove legacy copies from Mods to avoid duplicate load contexts.
                    var legacyModsPath = Path.Combine(modsFolder, fileName);
                    if (File.Exists(legacyModsPath))
                    {
                        File.Delete(legacyModsPath);
                    }
                }
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
        var userLibsFolder = Path.Combine(_gameInstallPath, "UserLibs");

        if (Directory.Exists(modsFolder))
        {
            progressCallback?.Invoke("Cleaning Mods directory...");

            foreach (var file in Directory.GetFiles(modsFolder))
                File.Delete(file);

            foreach (var dir in Directory.GetDirectories(modsFolder))
                Directory.Delete(dir, true);
        }

        if (Directory.Exists(userLibsFolder))
        {
            progressCallback?.Invoke("Cleaning UserLibs directory...");

            foreach (var file in Directory.GetFiles(userLibsFolder))
                File.Delete(file);

            foreach (var dir in Directory.GetDirectories(userLibsFolder))
                Directory.Delete(dir, true);
        }

        Directory.CreateDirectory(modsFolder);
        Directory.CreateDirectory(userLibsFolder);
        progressCallback?.Invoke("✓ Mods and UserLibs directories cleaned");
        return Task.CompletedTask;
    }

    public bool IsMelonLoaderInstalled()
    {
        var mlDir = Path.Combine(_gameInstallPath, "MelonLoader");
        if (!Directory.Exists(mlDir))
            return false;

        // Check for core DLL in either root or net6 subdirectory (matches ComponentManager.IsGameMelonLoaderPresent)
        var melonLoaderDll = Path.Combine(mlDir, "MelonLoader.dll");
        var melonLoaderDllNet6 = Path.Combine(mlDir, "net6", "MelonLoader.dll");
        return File.Exists(melonLoaderDll) || File.Exists(melonLoaderDllNet6);
    }

    public string? GetInstalledMelonLoaderVersion()
    {
        var mlDir = Path.Combine(_gameInstallPath, "MelonLoader");
        var candidatePaths = new[]
        {
            Path.Combine(mlDir, "net6", "MelonLoader.dll"),
            Path.Combine(mlDir, "MelonLoader.dll"),
            Path.Combine(mlDir, "net35", "MelonLoader.dll")
        };

        foreach (var path in candidatePaths)
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var version = AssemblyName.GetAssemblyName(path).Version;
                if (version != null)
                    return version.ToString();
            }
            catch
            {
                // Fall back to file metadata below.
            }

            try
            {
                var fileVersion = FileVersionInfo.GetVersionInfo(path).FileVersion;
                if (!string.IsNullOrWhiteSpace(fileVersion))
                    return fileVersion;
            }
            catch
            {
                // Ignore and continue to next candidate.
            }
        }

        return null;
    }

    public bool IsInstalledMelonLoaderVersionCompatible(
        out string? installedVersion,
        out string? expectedVersion,
        out string? reason)
    {
        installedVersion = GetInstalledMelonLoaderVersion();
        expectedVersion = GetConfiguredMelonLoaderVersion();
        reason = null;

        if (string.IsNullOrWhiteSpace(installedVersion))
        {
            reason = "Could not determine installed MelonLoader version.";
            return false;
        }

        var installedParsed = ParseLooseVersion(installedVersion);
        if (IsExplicitlyUnsupportedVersion(installedVersion, installedParsed))
        {
            reason = $"MelonLoader {installedVersion} is known incompatible with this Unity version.";
            return false;
        }

        // If we can't read configured version metadata, only enforce explicit deny-list.
        if (string.IsNullOrWhiteSpace(expectedVersion))
            return true;

        var expectedParsed = ParseLooseVersion(expectedVersion);
        if (installedParsed == null || expectedParsed == null)
        {
            if (!string.Equals(installedVersion, expectedVersion, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Installed MelonLoader {installedVersion} does not match required {expectedVersion}.";
                return false;
            }
            return true;
        }

        // Require same major/minor/patch family and at least required build when present.
        if (installedParsed.Major != expectedParsed.Major ||
            installedParsed.Minor != expectedParsed.Minor ||
            installedParsed.Build != expectedParsed.Build)
        {
            reason = $"Installed MelonLoader {installedVersion} is not in the required {expectedVersion} family.";
            return false;
        }

        if (expectedParsed.Revision > 0 && installedParsed.Revision < expectedParsed.Revision)
        {
            reason = $"Installed MelonLoader build {installedVersion} is older than required {expectedVersion}.";
            return false;
        }

        return true;
    }

    private static bool IsExplicitlyUnsupportedVersion(string rawVersion, Version? parsed)
    {
        if (parsed != null)
        {
            if (parsed.Major == 0 && parsed.Minor == 9 && parsed.Build == 1)
                return true;
        }

        var normalized = NormalizeVersionFamily(rawVersion);
        return UnsupportedMelonLoaderVersions.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeVersionFamily(string version)
    {
        var parsed = ParseLooseVersion(version);
        if (parsed != null)
            return $"{parsed.Major}.{parsed.Minor}.{parsed.Build}";

        return version.Trim();
    }

    private static Version? ParseLooseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var matches = Regex.Matches(value, @"\d+");
        if (matches.Count < 2)
            return null;

        var numbers = new List<int>(4);
        foreach (Match match in matches)
        {
            if (numbers.Count == 4)
                break;
            if (int.TryParse(match.Value, out var parsed))
                numbers.Add(parsed);
        }

        if (numbers.Count < 2)
            return null;

        while (numbers.Count < 4)
            numbers.Add(0);

        return new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
    }

    private static string? GetConfiguredMelonLoaderVersion()
    {
        var candidatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "third_party", "versions.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "third_party", "versions.json")
        };

        foreach (var path in candidatePaths)
        {
            if (!File.Exists(path))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("components", out var components) &&
                    components.TryGetProperty("MelonLoader", out var melonLoader) &&
                    melonLoader.TryGetProperty("version", out var versionElement))
                {
                    return versionElement.GetString();
                }
            }
            catch
            {
                // Ignore malformed JSON and try fallback path.
            }
        }

        return null;
    }

    public bool IsDataExtractorInstalled()
    {
        var dataExtractorDll = Path.Combine(_gameInstallPath, "Mods", "Menace.DataExtractor.dll");
        return File.Exists(dataExtractorDll);
    }

    /// <summary>
    /// Install all required components (MelonLoader, DataExtractor, ModpackLoader).
    /// Used by Clean Redeploy to ensure a complete installation.
    /// </summary>
    public async Task<bool> InstallAllRequiredAsync(Action<string>? progressCallback = null)
    {
        if (!await InstallMelonLoaderAsync(progressCallback))
            return false;

        if (!await InstallDataExtractorAsync(progressCallback))
            return false;

        if (!await InstallModpackLoaderAsync(progressCallback))
            return false;

        return true;
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
