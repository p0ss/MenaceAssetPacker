using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Menace;
using Microsoft.Win32;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Checks the user's environment for compatibility issues and provides fixes.
/// Results are logged to the diagnostic log for troubleshooting.
/// </summary>
public class EnvironmentChecker
{
    private static readonly Lazy<EnvironmentChecker> _instance = new(() => new EnvironmentChecker());
    public static EnvironmentChecker Instance => _instance.Value;

    /// <summary>
    /// Run all environment checks and return results.
    /// </summary>
    public async Task<List<EnvironmentCheckResult>> RunAllChecksAsync()
    {
        var results = new List<EnvironmentCheckResult>();

        ModkitLog.Info("=== Starting Environment Diagnostics ===");
        ModkitLog.Info($"Platform: {RuntimeInformation.OSDescription}");
        ModkitLog.Info($"Architecture: {RuntimeInformation.OSArchitecture}");
        ModkitLog.Info($"App Version: {ModkitVersion.Short}");

        // Run checks in order of importance
        results.Add(await CheckDotNetRuntimeAsync());
        results.Add(await CheckGamePathAsync());
        results.Add(await CheckMelonLoaderAsync());
        results.Add(await CheckDataExtractorAsync());
        results.Add(await CheckModpackLoaderAsync());
        results.Add(await CheckIl2CppAssembliesAsync());

        if (OperatingSystem.IsWindows())
        {
            results.Add(await CheckVcRedistAsync());
        }

        results.Add(await CheckBundledDependenciesAsync());

        // Log summary
        var passed = results.Count(r => r.Status == CheckStatus.Passed);
        var warnings = results.Count(r => r.Status == CheckStatus.Warning);
        var failed = results.Count(r => r.Status == CheckStatus.Failed);

        ModkitLog.Info($"=== Environment Check Complete: {passed} passed, {warnings} warnings, {failed} failed ===");

        return results;
    }

    private async Task<EnvironmentCheckResult> CheckDotNetRuntimeAsync()
    {
        var result = new EnvironmentCheckResult
        {
            Name = ".NET Runtime",
            Category = CheckCategory.Runtime
        };

        try
        {
            var psi = new ProcessStartInfo("dotnet", "--list-runtimes")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                result.Status = CheckStatus.Warning;
                result.Description = ".NET CLI not found";
                result.Details = "The dotnet command could not be executed. Modpack compilation may not work.";
                result.FixInstructions = "Install .NET 8 SDK from https://dotnet.microsoft.com/download";
                result.FixUrl = "https://dotnet.microsoft.com/download";
                ModkitLog.Warn("[EnvCheck] dotnet CLI not found");
                return result;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var runtimes = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.Contains("Microsoft.NETCore.App"))
                .ToList();

            ModkitLog.Info($"[EnvCheck] Found {runtimes.Count} .NET runtimes");
            foreach (var runtime in runtimes.Take(5))
            {
                ModkitLog.Info($"[EnvCheck]   {runtime.Trim()}");
            }

            // Check for compatible versions (6.x, 8.x, 9.x, 10.x)
            var hasCompatible = runtimes.Any(r =>
                r.Contains("Microsoft.NETCore.App 6.") ||
                r.Contains("Microsoft.NETCore.App 8.") ||
                r.Contains("Microsoft.NETCore.App 9.") ||
                r.Contains("Microsoft.NETCore.App 10."));

            if (hasCompatible)
            {
                result.Status = CheckStatus.Passed;
                result.Description = $"Found {runtimes.Count} runtime(s)";
                result.Details = string.Join("\n", runtimes.Take(5).Select(r => r.Trim()));
            }
            else if (runtimes.Count > 0)
            {
                result.Status = CheckStatus.Warning;
                result.Description = "No compatible .NET runtime found";
                result.Details = "Found runtimes but none are .NET 6/8/9/10. Modpack compilation may fail.";
                result.FixInstructions = "Install .NET 8 SDK from https://dotnet.microsoft.com/download";
                result.FixUrl = "https://dotnet.microsoft.com/download";
                ModkitLog.Warn("[EnvCheck] No compatible .NET runtime (need 6.x, 8.x, 9.x, or 10.x)");
            }
            else
            {
                result.Status = CheckStatus.Warning;
                result.Description = "No .NET runtimes installed";
                result.Details = "dotnet CLI exists but no runtimes are installed.";
                result.FixInstructions = "Install .NET 8 SDK from https://dotnet.microsoft.com/download";
                result.FixUrl = "https://dotnet.microsoft.com/download";
                ModkitLog.Warn("[EnvCheck] No .NET runtimes installed");
            }
        }
        catch (Exception ex)
        {
            result.Status = CheckStatus.Warning;
            result.Description = ".NET check failed";
            result.Details = ex.Message;
            result.FixInstructions = "Install .NET 8 SDK from https://dotnet.microsoft.com/download";
            result.FixUrl = "https://dotnet.microsoft.com/download";
            ModkitLog.Warn($"[EnvCheck] .NET check failed: {ex.Message}");
        }

        return result;
    }

    private Task<EnvironmentCheckResult> CheckGamePathAsync()
    {
        var result = new EnvironmentCheckResult
        {
            Name = "Game Installation",
            Category = CheckCategory.Game
        };

        var gamePath = AppSettings.Instance.GameInstallPath;

        if (string.IsNullOrEmpty(gamePath))
        {
            result.Status = CheckStatus.Failed;
            result.Description = "Game path not configured";
            result.Details = "The modkit could not automatically detect the game installation.";
            result.FixInstructions = "Set the game installation path in Settings.";
            result.CanAutoFix = false;
            ModkitLog.Warn("[EnvCheck] Game path not set");
            return Task.FromResult(result);
        }

        if (!Directory.Exists(gamePath))
        {
            result.Status = CheckStatus.Failed;
            result.Description = "Game path does not exist";
            result.Details = $"Path: {gamePath}";
            result.FixInstructions = "Update the game path in Settings to point to your Menace installation.";
            result.CanAutoFix = false;
            ModkitLog.Warn($"[EnvCheck] Game path does not exist: {gamePath}");
            return Task.FromResult(result);
        }

        // Check for game executable (platform-specific)
        var exeName = OperatingSystem.IsWindows() ? "Menace.exe" : "Menace.x86_64";
        var exePath = Path.Combine(gamePath, exeName);

        // On Linux/macOS, also check for the .exe since Proton/Wine runs it
        if (!File.Exists(exePath) && !OperatingSystem.IsWindows())
        {
            var winExePath = Path.Combine(gamePath, "Menace.exe");
            if (File.Exists(winExePath))
                exePath = winExePath;
        }

        if (!File.Exists(exePath))
        {
            // Check if any Menace executable exists
            var hasAnyExe = File.Exists(Path.Combine(gamePath, "Menace.exe")) ||
                           File.Exists(Path.Combine(gamePath, "Menace.x86_64")) ||
                           File.Exists(Path.Combine(gamePath, "Menace"));

            if (!hasAnyExe)
            {
                result.Status = CheckStatus.Warning;
                result.Description = "Game executable not found";
                result.Details = $"Expected game executable at: {gamePath}";
                result.FixInstructions = "Verify the game path points to the correct Menace installation folder.";
                ModkitLog.Warn($"[EnvCheck] Game executable not found in {gamePath}");
                return Task.FromResult(result);
            }
        }

        result.Status = CheckStatus.Passed;
        result.Description = Path.GetFileName(gamePath);
        result.Details = gamePath;
        ModkitLog.Info($"[EnvCheck] Game found: {gamePath}");

        return Task.FromResult(result);
    }

    private Task<EnvironmentCheckResult> CheckMelonLoaderAsync()
    {
        var result = new EnvironmentCheckResult
        {
            Name = "MelonLoader",
            Category = CheckCategory.Game
        };

        var gamePath = AppSettings.Instance.GameInstallPath;
        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
        {
            result.Status = CheckStatus.Warning;
            result.Description = "Cannot check (no game path)";
            result.Details = "Set the game path first.";
            return Task.FromResult(result);
        }

        var mlDir = Path.Combine(gamePath, "MelonLoader");
        var versionDll = Path.Combine(gamePath, "version.dll");

        if (!Directory.Exists(mlDir))
        {
            result.Status = CheckStatus.Failed;
            result.Description = "Not installed";
            result.Details = "MelonLoader is required for mods to work.";
            result.FixInstructions = "Click 'Install MelonLoader' to install it automatically.";
            result.CanAutoFix = true;
            result.AutoFixAction = AutoFixAction.InstallMelonLoader;
            ModkitLog.Warn("[EnvCheck] MelonLoader directory not found");
            return Task.FromResult(result);
        }

        if (!File.Exists(versionDll))
        {
            result.Status = CheckStatus.Failed;
            result.Description = "Missing version.dll";
            result.Details = "The version.dll hook file is missing from the game folder.";
            result.FixInstructions = "Click 'Install' to reinstall MelonLoader.";
            result.CanAutoFix = true;
            result.AutoFixAction = AutoFixAction.InstallMelonLoader;
            ModkitLog.Warn("[EnvCheck] version.dll missing from game directory");
            return Task.FromResult(result);
        }

        // Check for MelonLoader.dll to verify full installation
        // It can be in the root, or in net6/ subdirectory (newer versions)
        var mlDll = Path.Combine(mlDir, "MelonLoader.dll");
        var mlDllNet6 = Path.Combine(mlDir, "net6", "MelonLoader.dll");
        if (!File.Exists(mlDll) && !File.Exists(mlDllNet6))
        {
            result.Status = CheckStatus.Warning;
            result.Description = "Missing core DLL";
            result.Details = "MelonLoader.dll not found. Installation may be incomplete.";
            result.FixInstructions = "Click 'Install' to reinstall MelonLoader.";
            result.CanAutoFix = true;
            result.AutoFixAction = AutoFixAction.InstallMelonLoader;
            ModkitLog.Warn("[EnvCheck] MelonLoader.dll not found");
            return Task.FromResult(result);
        }

        var installer = new ModLoaderInstaller(gamePath);
        if (!installer.IsInstalledMelonLoaderVersionCompatible(out var installedVersion, out var expectedVersion, out var reason))
        {
            result.Status = CheckStatus.Failed;
            result.Description = "Unsupported version";
            result.Details = string.IsNullOrWhiteSpace(reason)
                ? $"Installed MelonLoader version {installedVersion ?? "unknown"} is not compatible."
                : reason!;
            result.FixInstructions = string.IsNullOrWhiteSpace(expectedVersion)
                ? "Click 'Install' to reinstall MelonLoader."
                : $"Click 'Install' to install MelonLoader {expectedVersion}.";
            result.CanAutoFix = true;
            result.AutoFixAction = AutoFixAction.InstallMelonLoader;
            ModkitLog.Warn($"[EnvCheck] Incompatible MelonLoader version: installed={installedVersion ?? "unknown"}, required={expectedVersion ?? "unknown"}, reason={reason ?? "n/a"}");
            return Task.FromResult(result);
        }

        result.Status = CheckStatus.Passed;
        result.Description = "Installed";
        var versionSuffix = string.IsNullOrWhiteSpace(installedVersion) ? "" : $" (v{installedVersion})";
        result.Details = $"{mlDir}{versionSuffix}";
        ModkitLog.Info($"[EnvCheck] MelonLoader found: {mlDir}");

        return Task.FromResult(result);
    }

    private Task<EnvironmentCheckResult> CheckDataExtractorAsync()
    {
        var result = new EnvironmentCheckResult
        {
            Name = "DataExtractor",
            Category = CheckCategory.Game
        };

        var gamePath = AppSettings.Instance.GameInstallPath;
        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
        {
            result.Status = CheckStatus.Warning;
            result.Description = "Cannot check (no game path)";
            result.Details = "Set the game path first.";
            return Task.FromResult(result);
        }

        var dataExtractorDll = Path.Combine(gamePath, "Mods", "Menace.DataExtractor.dll");
        if (!File.Exists(dataExtractorDll))
        {
            result.Status = CheckStatus.Failed;
            result.Description = "Not installed";
            result.Details = "DataExtractor is required to extract game data for modding.";
            result.FixInstructions = "Click 'Install' to install DataExtractor.";
            result.CanAutoFix = true;
            result.AutoFixAction = AutoFixAction.InstallDataExtractor;
            ModkitLog.Warn("[EnvCheck] DataExtractor not found in Mods folder");
            return Task.FromResult(result);
        }

        result.Status = CheckStatus.Passed;
        result.Description = "Installed";
        result.Details = dataExtractorDll;
        ModkitLog.Info($"[EnvCheck] DataExtractor found: {dataExtractorDll}");

        return Task.FromResult(result);
    }

    private Task<EnvironmentCheckResult> CheckModpackLoaderAsync()
    {
        var result = new EnvironmentCheckResult
        {
            Name = "ModpackLoader",
            Category = CheckCategory.Game
        };

        var gamePath = AppSettings.Instance.GameInstallPath;
        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
        {
            result.Status = CheckStatus.Warning;
            result.Description = "Cannot check (no game path)";
            result.Details = "Set the game path first.";
            return Task.FromResult(result);
        }

        var modpackLoaderDll = Path.Combine(gamePath, "Mods", "Menace.ModpackLoader.dll");
        if (!File.Exists(modpackLoaderDll))
        {
            result.Status = CheckStatus.Failed;
            result.Description = "Not installed";
            result.Details = "ModpackLoader is required to load modpacks in-game.";
            result.FixInstructions = "Click 'Install' to install ModpackLoader.";
            result.CanAutoFix = true;
            result.AutoFixAction = AutoFixAction.InstallModpackLoader;
            ModkitLog.Warn("[EnvCheck] ModpackLoader not found in Mods folder");
            return Task.FromResult(result);
        }

        // Check that ModpackLoader support dependencies are in UserLibs.
        // Derive from bundled payload to avoid drift when dependency sets change.
        var userLibsPath = Path.Combine(gamePath, "UserLibs");
        var requiredDeps = GetExpectedModpackLoaderDependencies();
        var missingDeps = requiredDeps.Where(dep => !File.Exists(Path.Combine(userLibsPath, dep))).ToList();
        if (missingDeps.Count > 0)
        {
            result.Status = CheckStatus.Warning;
            result.Description = "Missing dependencies";
            result.Details = $"ModpackLoader dependencies not found in UserLibs: {string.Join(", ", missingDeps)}";
            result.FixInstructions = "Click 'Install' to reinstall ModpackLoader with dependencies.";
            result.CanAutoFix = true;
            result.AutoFixAction = AutoFixAction.InstallModpackLoader;
            ModkitLog.Warn($"[EnvCheck] ModpackLoader missing UserLibs dependencies: {string.Join(", ", missingDeps)}");
            return Task.FromResult(result);
        }

        // Legacy installs placed dependencies in Mods/. These can cause load-context conflicts.
        var modsPath = Path.Combine(gamePath, "Mods");
        var legacyDepsInMods = requiredDeps.Where(dep => File.Exists(Path.Combine(modsPath, dep))).ToList();
        if (legacyDepsInMods.Count > 0)
        {
            result.Status = CheckStatus.Warning;
            result.Description = "Legacy dependency placement";
            result.Details = $"Dependencies should be in UserLibs, not Mods: {string.Join(", ", legacyDepsInMods)}";
            result.FixInstructions = "Click 'Install' to reinstall ModpackLoader and clean legacy copies.";
            result.CanAutoFix = true;
            result.AutoFixAction = AutoFixAction.InstallModpackLoader;
            ModkitLog.Warn($"[EnvCheck] Legacy ModpackLoader dependencies found in Mods: {string.Join(", ", legacyDepsInMods)}");
            return Task.FromResult(result);
        }

        result.Status = CheckStatus.Passed;
        result.Description = "Installed";
        result.Details = modpackLoaderDll;
        ModkitLog.Info($"[EnvCheck] ModpackLoader found: {modpackLoaderDll}");

        return Task.FromResult(result);
    }

    private static List<string> GetExpectedModpackLoaderDependencies()
    {
        var bundledDir = Path.Combine(
            AppContext.BaseDirectory,
            "third_party",
            "bundled",
            "ModpackLoader");

        if (Directory.Exists(bundledDir))
        {
            var bundledDeps = Directory.GetFiles(bundledDir, "*.dll")
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Where(name => !name!.StartsWith("Menace.ModpackLoader", StringComparison.OrdinalIgnoreCase))
                .Select(name => name!)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (bundledDeps.Count > 0)
                return bundledDeps;
        }

        // Fallback for development environments with partial bundled content.
        return new List<string>
        {
            "Microsoft.CodeAnalysis.dll",
            "Microsoft.CodeAnalysis.CSharp.dll",
            "System.Collections.Immutable.dll",
            "System.Reflection.Metadata.dll",
            "System.Text.Encoding.CodePages.dll",
            "Newtonsoft.Json.dll",
            "SharpGLTF.Core.dll"
        };
    }

    private Task<EnvironmentCheckResult> CheckIl2CppAssembliesAsync()
    {
        var result = new EnvironmentCheckResult
        {
            Name = "IL2CPP Assemblies",
            Category = CheckCategory.Game
        };

        var gamePath = AppSettings.Instance.GameInstallPath;
        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
        {
            result.Status = CheckStatus.Warning;
            result.Description = "Cannot check (no game path)";
            result.Details = "Set the game path first.";
            return Task.FromResult(result);
        }

        var il2cppDir = Path.Combine(gamePath, "MelonLoader", "Il2CppAssemblies");

        if (!Directory.Exists(il2cppDir))
        {
            result.Status = CheckStatus.Failed;
            result.Description = "Not generated";
            result.Details = "IL2CPP assemblies are needed for mod compilation.";
            result.FixInstructions = "Run the game once with MelonLoader installed. The assemblies are generated on first launch.";
            result.CanAutoFix = true;
            result.AutoFixAction = AutoFixAction.LaunchGame;
            ModkitLog.Warn("[EnvCheck] Il2CppAssemblies directory not found");
            return Task.FromResult(result);
        }

        var dllCount = Directory.GetFiles(il2cppDir, "*.dll").Length;
        ModkitLog.Info($"[EnvCheck] Found {dllCount} IL2CPP assemblies");

        if (dllCount < 50)
        {
            result.Status = CheckStatus.Warning;
            result.Description = $"May be incomplete ({dllCount} DLLs)";
            result.Details = "Expected 50+ assemblies. Try running the game again.";
            result.FixInstructions = "Run the game and wait for it to fully load to the main menu.";
            result.CanAutoFix = true;
            result.AutoFixAction = AutoFixAction.LaunchGame;
            ModkitLog.Warn($"[EnvCheck] Only {dllCount} IL2CPP assemblies found (expected 50+)");
            return Task.FromResult(result);
        }

        result.Status = CheckStatus.Passed;
        result.Description = $"{dllCount} assemblies";
        result.Details = il2cppDir;

        return Task.FromResult(result);
    }

    private Task<EnvironmentCheckResult> CheckVcRedistAsync()
    {
        var result = new EnvironmentCheckResult
        {
            Name = "Visual C++ Runtime",
            Category = CheckCategory.Runtime
        };

        if (!OperatingSystem.IsWindows())
        {
            result.Status = CheckStatus.Passed;
            result.Description = "Not required";
            result.Details = "VC++ Redistributable is only needed on Windows.";
            return Task.FromResult(result);
        }

        try
        {
            // Check registry for VC++ 2015-2022 Redistributable (x64)
            var vcKeys = new[]
            {
                @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
                @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"
            };

            foreach (var keyPath in vcKeys)
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyPath);
                if (key != null)
                {
                    var installed = key.GetValue("Installed");
                    if (installed != null && (int)installed == 1)
                    {
                        var version = key.GetValue("Version")?.ToString() ?? "unknown";
                        result.Status = CheckStatus.Passed;
                        result.Description = "Installed";
                        result.Details = $"Version: {version}";
                        ModkitLog.Info($"[EnvCheck] VC++ Redistributable found: {version}");
                        return Task.FromResult(result);
                    }
                }
            }

            result.Status = CheckStatus.Warning;
            result.Description = "May not be installed";
            result.Details = "The Visual C++ Redistributable could not be detected. Some features may not work.";
            result.FixInstructions = "Download and install from: https://aka.ms/vs/17/release/vc_redist.x64.exe";
            result.FixUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe";
            ModkitLog.Warn("[EnvCheck] VC++ Redistributable not detected in registry");
        }
        catch (Exception ex)
        {
            result.Status = CheckStatus.Warning;
            result.Description = "Check failed";
            result.Details = ex.Message;
            ModkitLog.Warn($"[EnvCheck] VC++ check failed: {ex.Message}");
        }

        return Task.FromResult(result);
    }

    private Task<EnvironmentCheckResult> CheckBundledDependenciesAsync()
    {
        var result = new EnvironmentCheckResult
        {
            Name = "Bundled Components",
            Category = CheckCategory.Modkit
        };

        var details = new List<string>();
        var allPresent = true;

        // Check for MelonLoader (bundled, cached, or already in game)
        var mlPath = ComponentManager.Instance.GetMelonLoaderPath();
        var mlInGame = IsGameMelonLoaderInstalled();
        if (mlPath != null && Directory.Exists(mlPath))
        {
            details.Add($"MelonLoader: {mlPath}");
            ModkitLog.Info($"[EnvCheck] MelonLoader bundled: {mlPath}");
        }
        else if (mlInGame)
        {
            details.Add("MelonLoader: installed in game");
            ModkitLog.Info("[EnvCheck] MelonLoader already installed in game directory");
        }
        else
        {
            details.Add("MelonLoader: not found");
            allPresent = false;
            ModkitLog.Warn("[EnvCheck] MelonLoader bundle not found");
        }

        // Check for bundled DataExtractor
        var dePath = ComponentManager.Instance.GetDataExtractorPath();
        if (dePath != null && File.Exists(dePath))
        {
            details.Add($"DataExtractor: present");
            ModkitLog.Info($"[EnvCheck] DataExtractor bundled: {dePath}");
        }
        else
        {
            details.Add("DataExtractor: not found");
            allPresent = false;
            ModkitLog.Warn("[EnvCheck] DataExtractor bundle not found");
        }

        // Check for bundled ModpackLoader
        var mpPath = ComponentManager.Instance.GetModpackLoaderPath();
        if (mpPath != null && Directory.Exists(mpPath))
        {
            details.Add($"ModpackLoader: present");
            ModkitLog.Info($"[EnvCheck] ModpackLoader bundled: {mpPath}");
        }
        else
        {
            details.Add("ModpackLoader: not found");
            allPresent = false;
            ModkitLog.Warn("[EnvCheck] ModpackLoader bundle not found");
        }

        if (allPresent)
        {
            result.Status = CheckStatus.Passed;
            result.Description = "All present";
            result.Details = "MelonLoader, DataExtractor, ModpackLoader";
        }
        else
        {
            result.Status = CheckStatus.Warning;
            result.Description = "Some components not bundled";
            result.Details = string.Join("\n", details);
            result.FixInstructions = "Components will be downloaded if needed. This is normal for development builds.";
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// Execute an auto-fix action.
    /// </summary>
    public async Task<bool> ExecuteAutoFixAsync(AutoFixAction action, Action<string>? progressCallback = null)
    {
        var gamePath = AppSettings.Instance.GameInstallPath;

        switch (action)
        {
            case AutoFixAction.InstallMelonLoader:
                if (string.IsNullOrEmpty(gamePath))
                {
                    progressCallback?.Invoke("Error: Game path not set");
                    return false;
                }
                var mlInstaller = new ModLoaderInstaller(gamePath);
                return await mlInstaller.InstallMelonLoaderAsync(progressCallback);

            case AutoFixAction.LaunchGame:
                if (string.IsNullOrEmpty(gamePath))
                {
                    progressCallback?.Invoke("Error: Game path not set");
                    return false;
                }
                var launcher = new ModLoaderInstaller(gamePath);
                return await launcher.LaunchGameAsync(progressCallback);

            case AutoFixAction.InstallDataExtractor:
                if (string.IsNullOrEmpty(gamePath))
                {
                    progressCallback?.Invoke("Error: Game path not set");
                    return false;
                }
                var deInstaller = new ModLoaderInstaller(gamePath);
                return await deInstaller.InstallDataExtractorAsync(progressCallback);

            case AutoFixAction.InstallModpackLoader:
                if (string.IsNullOrEmpty(gamePath))
                {
                    progressCallback?.Invoke("Error: Game path not set");
                    return false;
                }
                var mpInstaller = new ModLoaderInstaller(gamePath);
                return await mpInstaller.InstallModpackLoaderAsync(progressCallback);

            default:
                progressCallback?.Invoke("Unknown fix action");
                return false;
        }
    }

    /// <summary>
    /// Check if MelonLoader is already installed in the game directory.
    /// </summary>
    private bool IsGameMelonLoaderInstalled()
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
    /// Write a full diagnostic report to the log.
    /// </summary>
    public async Task WriteDiagnosticReportAsync()
    {
        ModkitLog.Info("========================================");
        ModkitLog.Info("  MENACE MODKIT DIAGNOSTIC REPORT");
        ModkitLog.Info($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        ModkitLog.Info("========================================");

        var results = await RunAllChecksAsync();

        ModkitLog.Info("");
        ModkitLog.Info("SUMMARY:");
        foreach (var result in results)
        {
            var icon = result.Status switch
            {
                CheckStatus.Passed => "[OK]",
                CheckStatus.Warning => "[WARN]",
                CheckStatus.Failed => "[FAIL]",
                _ => "[?]"
            };
            ModkitLog.Info($"  {icon} {result.Name}: {result.Description}");
            if (!string.IsNullOrEmpty(result.Details))
                ModkitLog.Info($"       {result.Details}");
            if (!string.IsNullOrEmpty(result.FixInstructions) && result.Status != CheckStatus.Passed)
                ModkitLog.Info($"       Fix: {result.FixInstructions}");
        }

        ModkitLog.Info("");
        ModkitLog.Info("SYSTEM INFO:");
        ModkitLog.Info($"  OS: {RuntimeInformation.OSDescription}");
        ModkitLog.Info($"  Architecture: {RuntimeInformation.OSArchitecture}");
        ModkitLog.Info($"  .NET Runtime: {RuntimeInformation.FrameworkDescription}");
        ModkitLog.Info($"  App Directory: {AppContext.BaseDirectory}");
        ModkitLog.Info($"  Game Path: {AppSettings.Instance.GameInstallPath}");

        ModkitLog.Info("========================================");
    }
}

/// <summary>
/// Result of an environment check.
/// </summary>
public class EnvironmentCheckResult
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Details { get; set; } = "";
    public CheckStatus Status { get; set; } = CheckStatus.Passed;
    public CheckCategory Category { get; set; } = CheckCategory.Runtime;
    public string? FixInstructions { get; set; }
    public string? FixUrl { get; set; }
    public bool CanAutoFix { get; set; }
    public AutoFixAction AutoFixAction { get; set; }
}

public enum CheckStatus
{
    Passed,
    Warning,
    Failed
}

public enum CheckCategory
{
    Runtime,
    Game,
    Modkit
}

public enum AutoFixAction
{
    None,
    InstallMelonLoader,
    LaunchGame,
    InstallDataExtractor,
    InstallModpackLoader
}
