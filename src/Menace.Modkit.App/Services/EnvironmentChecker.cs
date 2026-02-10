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
            result.Description = "Incomplete installation";
            result.Details = "version.dll is missing - MelonLoader won't load.";
            result.FixInstructions = "Click 'Install MelonLoader' to reinstall it.";
            result.CanAutoFix = true;
            result.AutoFixAction = AutoFixAction.InstallMelonLoader;
            ModkitLog.Warn("[EnvCheck] version.dll missing from game directory");
            return Task.FromResult(result);
        }

        // Check for MelonLoader.dll to verify full installation
        var mlDll = Path.Combine(mlDir, "MelonLoader.dll");
        if (!File.Exists(mlDll))
        {
            result.Status = CheckStatus.Warning;
            result.Description = "May be incomplete";
            result.Details = "MelonLoader.dll not found in MelonLoader directory.";
            result.FixInstructions = "Try reinstalling MelonLoader.";
            result.CanAutoFix = true;
            result.AutoFixAction = AutoFixAction.InstallMelonLoader;
            ModkitLog.Warn("[EnvCheck] MelonLoader.dll not found");
            return Task.FromResult(result);
        }

        result.Status = CheckStatus.Passed;
        result.Description = "Installed";
        result.Details = mlDir;
        ModkitLog.Info($"[EnvCheck] MelonLoader found: {mlDir}");

        return Task.FromResult(result);
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

        var missing = new List<string>();
        var found = new List<string>();

        // Check for bundled MelonLoader
        var mlPath = ComponentManager.Instance.GetMelonLoaderPath();
        if (mlPath != null && Directory.Exists(mlPath))
            found.Add("MelonLoader");
        else
            missing.Add("MelonLoader");

        // Check for bundled DataExtractor
        var dePath = ComponentManager.Instance.GetDataExtractorPath();
        if (dePath != null && File.Exists(dePath))
            found.Add("DataExtractor");
        else
            missing.Add("DataExtractor");

        // Check for bundled ModpackLoader
        var mpPath = ComponentManager.Instance.GetModpackLoaderPath();
        if (mpPath != null && Directory.Exists(mpPath))
            found.Add("ModpackLoader");
        else
            missing.Add("ModpackLoader");

        ModkitLog.Info($"[EnvCheck] Bundled components found: {string.Join(", ", found)}");
        if (missing.Count > 0)
            ModkitLog.Warn($"[EnvCheck] Bundled components missing: {string.Join(", ", missing)}");

        if (missing.Count == 0)
        {
            result.Status = CheckStatus.Passed;
            result.Description = "All present";
            result.Details = string.Join(", ", found);
        }
        else if (found.Count > 0)
        {
            result.Status = CheckStatus.Warning;
            result.Description = $"Missing: {string.Join(", ", missing)}";
            result.Details = $"Found: {string.Join(", ", found)}";
            result.FixInstructions = "Some bundled components are missing. Try reinstalling the modkit.";
        }
        else
        {
            result.Status = CheckStatus.Failed;
            result.Description = "All missing";
            result.Details = "No bundled components found. The modkit installation may be corrupted.";
            result.FixInstructions = "Reinstall the modkit from the official release.";
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

            default:
                progressCallback?.Invoke("Unknown fix action");
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
    InstallDataExtractor
}
