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
            ModkitLog.Info($"[MelonLoader] InstallMelonLoaderAsync started, gameInstallPath={_gameInstallPath}");

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
                ModkitLog.Info("[MelonLoader] Already fully installed, skipping");
                progressCallback?.Invoke("✓ MelonLoader already installed");
                return Task.FromResult(true);
            }

            progressCallback?.Invoke("Installing MelonLoader...");

            // MelonLoader is a required component (cached or bundled)
            var melonLoaderPath = ComponentManager.Instance.GetMelonLoaderPath();
            ModkitLog.Info($"[MelonLoader] Component path: {melonLoaderPath ?? "(null)"}");

            if (melonLoaderPath == null)
            {
                ModkitLog.Error("[MelonLoader] Component path is null - MelonLoader not found in cache or bundled");
                progressCallback?.Invoke("❌ MelonLoader component not found. Go to Setup tab to download required components.");
                return Task.FromResult(false);
            }

            // Verify the source directory exists and has expected files
            if (!Directory.Exists(melonLoaderPath))
            {
                ModkitLog.Error($"[MelonLoader] Source directory does not exist: {melonLoaderPath}");
                progressCallback?.Invoke("❌ MelonLoader source directory not found");
                return Task.FromResult(false);
            }

            var sourceVersionDll = Path.Combine(melonLoaderPath, "version.dll");
            if (!File.Exists(sourceVersionDll))
            {
                ModkitLog.Error($"[MelonLoader] version.dll not found in source: {sourceVersionDll}");
                progressCallback?.Invoke("❌ MelonLoader package is incomplete (missing version.dll)");
                return Task.FromResult(false);
            }

            // Copy all MelonLoader files to game directory
            CopyDirectory(melonLoaderPath, _gameInstallPath, progressCallback);

            progressCallback?.Invoke("✓ MelonLoader installed successfully");
            return Task.FromResult(true);
        }
        catch (InvalidOperationException ex)
        {
            // Specific error already logged with user-friendly message
            progressCallback?.Invoke($"❌ {ex.Message}");
            return Task.FromResult(false);
        }
        catch (FileNotFoundException ex)
        {
            progressCallback?.Invoke($"❌ {ex.Message}");
            ModkitLog.Error($"[MelonLoader] File not found: {ex.FileName}");
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke($"❌ Error installing MelonLoader: {ex.Message}");
            ModkitLog.Error($"[MelonLoader] Unexpected error during installation: {ex}");

            // Provide additional guidance for common Windows issues
            if (OperatingSystem.IsWindows())
            {
                progressCallback?.Invoke("  Common fixes:");
                progressCallback?.Invoke("  • Close the game if it's running");
                progressCallback?.Invoke("  • Temporarily disable antivirus");
                progressCallback?.Invoke("  • Run the modkit as administrator");
            }

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
        ModkitLog.Info($"[MelonLoader] CopyDirectory: source={sourceDir}, dest={destDir}");

        // Copy version.dll to root - this is CRITICAL for MelonLoader to work
        var versionDll = Path.Combine(sourceDir, "version.dll");
        var destVersionDll = Path.Combine(destDir, "version.dll");

        if (File.Exists(versionDll))
        {
            try
            {
                File.Copy(versionDll, destVersionDll, overwrite: true);
                ModkitLog.Info($"[MelonLoader] Copied version.dll to {destVersionDll}");
            }
            catch (IOException ex) when (ex.HResult == -2147024864) // 0x80070020 - file in use
            {
                progressCallback?.Invoke("❌ version.dll is locked - please close the game and any antivirus that may be scanning it");
                ModkitLog.Error($"[MelonLoader] version.dll is locked by another process: {ex.Message}");
                throw new InvalidOperationException("version.dll is locked by another process. Please close the game and try again.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                progressCallback?.Invoke("❌ Access denied copying version.dll - try running as administrator");
                ModkitLog.Error($"[MelonLoader] Access denied copying version.dll: {ex.Message}");
                throw new InvalidOperationException("Access denied when copying version.dll. Try running as administrator.", ex);
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"❌ Failed to copy version.dll: {ex.Message}");
                ModkitLog.Error($"[MelonLoader] Failed to copy version.dll: {ex}");
                throw;
            }
        }
        else
        {
            progressCallback?.Invoke("❌ version.dll not found in MelonLoader package - installation may be corrupted");
            ModkitLog.Error($"[MelonLoader] version.dll not found in source: {versionDll}");
            throw new FileNotFoundException("version.dll not found in MelonLoader package. Try re-downloading components.", versionDll);
        }

        // Copy all subfolders (net6, net35, Dependencies, Documentation) into MelonLoader folder
        // The bundled structure has these directly inside third_party/bundled/MelonLoader/
        var destMelonLoaderDir = Path.Combine(destDir, "MelonLoader");
        Directory.CreateDirectory(destMelonLoaderDir);

        var subDirs = Directory.GetDirectories(sourceDir);
        ModkitLog.Info($"[MelonLoader] Found {subDirs.Length} subdirectories to copy");

        foreach (var subDir in subDirs)
        {
            var dirName = Path.GetFileName(subDir);
            var destSubDir = Path.Combine(destMelonLoaderDir, dirName);
            progressCallback?.Invoke($"  Copying {dirName}...");
            CopyDirectoryRecursive(subDir, destSubDir, progressCallback);
        }

        // Verify critical files were copied
        if (!File.Exists(destVersionDll))
        {
            progressCallback?.Invoke("❌ version.dll was not copied successfully");
            ModkitLog.Error("[MelonLoader] version.dll verification failed - file does not exist after copy");
            throw new InvalidOperationException("version.dll was not copied successfully. Installation failed.");
        }

        var melonLoaderDll = Path.Combine(destMelonLoaderDir, "net6", "MelonLoader.dll");
        if (!File.Exists(melonLoaderDll))
        {
            // Try alternate location
            melonLoaderDll = Path.Combine(destMelonLoaderDir, "MelonLoader.dll");
        }
        if (!File.Exists(melonLoaderDll))
        {
            progressCallback?.Invoke("⚠ MelonLoader.dll not found after copy - installation may be incomplete");
            ModkitLog.Warn("[MelonLoader] MelonLoader.dll not found after copy");
        }
        else
        {
            ModkitLog.Info($"[MelonLoader] Installation verified: {melonLoaderDll}");
        }
    }

    private void CopyDirectoryRecursive(string sourceDir, string destDir, Action<string>? progressCallback = null)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destDir, fileName);
            try
            {
                File.Copy(file, destFile, overwrite: true);
            }
            catch (IOException ex) when (ex.HResult == -2147024864) // 0x80070020 - file in use
            {
                ModkitLog.Warn($"[MelonLoader] File locked, skipping: {fileName} - {ex.Message}");
                progressCallback?.Invoke($"  ⚠ Skipped locked file: {fileName}");
                // Continue with other files - some locked files may not be critical
            }
            catch (UnauthorizedAccessException ex)
            {
                ModkitLog.Warn($"[MelonLoader] Access denied, skipping: {fileName} - {ex.Message}");
                progressCallback?.Invoke($"  ⚠ Access denied: {fileName}");
            }
            catch (Exception ex)
            {
                ModkitLog.Error($"[MelonLoader] Failed to copy {fileName}: {ex.Message}");
                throw; // Re-throw unexpected errors
            }
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectoryRecursive(dir, destSubDir, progressCallback);
        }
    }

    public Task<bool> InstallDataExtractorAsync(Action<string>? progressCallback = null, bool forceExtraction = false)
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

            // Only trigger extraction for users who have used Modding Tools features,
            // or if explicitly requested (e.g., Force Extract button).
            // This prevents freeze for users who just want to play mods via Mod Loader.
            bool shouldExtract = forceExtraction || AppSettings.Instance.HasUsedModdingTools;

            if (shouldExtract)
            {
                WriteForceExtractionFlag();
                progressCallback?.Invoke("✓ DataExtractor mod installed successfully");
                progressCallback?.Invoke("  Extraction will run automatically on next game launch");
            }
            else
            {
                progressCallback?.Invoke("✓ DataExtractor mod installed (extraction skipped for Mod Loader users)");
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke($"❌ Error installing DataExtractor: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    // Legacy assemblies that older versions placed in Mods/ instead of UserLibs/.
    // These need to be cleaned from Mods/ to avoid duplicate assembly loading.
    private static readonly string[] LegacyAssembliesToCleanFromMods = new[]
    {
        "System.Collections.Immutable.dll",
        "System.Memory.dll",
        "System.Buffers.dll",
        "System.Reflection.Metadata.dll",
        "Microsoft.CodeAnalysis.dll",
        "Microsoft.CodeAnalysis.CSharp.dll"
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

            // Clean legacy dependency copies from Mods/ (older versions put them there)
            foreach (var legacyDll in LegacyAssembliesToCleanFromMods)
            {
                var legacyModsPath = Path.Combine(modsFolder, legacyDll);
                if (File.Exists(legacyModsPath))
                {
                    File.Delete(legacyModsPath);
                    progressCallback?.Invoke($"  Removed legacy {legacyDll} from Mods/");
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

        // Restore original game data files (resources.assets, globalgamemanagers)
        RestoreOriginalGameData(progressCallback);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Restore original game data from backups during Clean Redeploy.
    /// This ensures the game files are vanilla before a fresh deploy.
    /// </summary>
    private void RestoreOriginalGameData(Action<string>? progressCallback = null)
    {
        // Find the *_Data directory
        var gameDataDir = Directory.GetDirectories(_gameInstallPath, "*_Data").FirstOrDefault();
        if (string.IsNullOrEmpty(gameDataDir))
        {
            ModkitLog.Warn("[ModLoaderInstaller] No *_Data directory found, cannot restore game data");
            return;
        }

        var filesToRestore = new[] {
            ("resources.assets.original", "resources.assets"),
            ("globalgamemanagers.original", "globalgamemanagers")
        };
        int restored = 0;

        foreach (var (backupName, originalName) in filesToRestore)
        {
            var backupPath = Path.Combine(gameDataDir, backupName);
            var originalPath = Path.Combine(gameDataDir, originalName);

            if (File.Exists(backupPath))
            {
                try
                {
                    var backupSize = new FileInfo(backupPath).Length;
                    progressCallback?.Invoke($"Restoring {originalName} from backup ({backupSize / 1024 / 1024}MB)...");
                    File.Copy(backupPath, originalPath, overwrite: true);
                    restored++;
                    ModkitLog.Info($"[ModLoaderInstaller] Restored: {backupName} -> {originalName}");
                }
                catch (Exception ex)
                {
                    progressCallback?.Invoke($"⚠ Failed to restore {originalName}: {ex.Message}");
                    ModkitLog.Error($"[ModLoaderInstaller] Failed to restore {originalName}: {ex.Message}");
                }
            }
            else
            {
                progressCallback?.Invoke($"⚠ No backup found for {originalName} - verify game files via Steam");
                ModkitLog.Warn($"[ModLoaderInstaller] No backup found: {backupPath}");
            }
        }

        if (restored > 0)
        {
            progressCallback?.Invoke($"✓ Restored {restored} game file(s) from backup");
        }
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
        // Use channel-specific manifest
        var filename = AppSettings.Instance.IsBetaChannel ? "versions-beta.json" : "versions.json";
        var candidatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "third_party", filename),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "third_party", filename),
            // Fall back to stable manifest
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
    /// Write a flag file that tells DataExtractor to run extraction on next game launch.
    /// </summary>
    public void WriteForceExtractionFlag()
    {
        try
        {
            var flagPath = Path.Combine(_gameInstallPath, "UserData", "ExtractedData", "_force_extraction.flag");
            var flagDir = Path.GetDirectoryName(flagPath);
            if (!string.IsNullOrEmpty(flagDir))
                Directory.CreateDirectory(flagDir);

            File.WriteAllText(flagPath,
                $"Force extraction requested by modkit at {DateTime.UtcNow:O}\n" +
                "This file will be deleted when extraction starts.\n" +
                "Delete this file manually to cancel pending extraction.");

            ModkitLog.Info($"[ModLoaderInstaller] Force extraction flag written: {flagPath}");
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[ModLoaderInstaller] Failed to write force extraction flag: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if force extraction is pending.
    /// </summary>
    public bool IsForceExtractionPending()
    {
        var flagPath = Path.Combine(_gameInstallPath, "UserData", "ExtractedData", "_force_extraction.flag");
        return File.Exists(flagPath);
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
