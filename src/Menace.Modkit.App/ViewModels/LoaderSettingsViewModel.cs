using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.ViewModels;

/// <summary>
/// Settings for end users running mods - game installation, deployment, logs.
/// </summary>
public sealed class LoaderSettingsViewModel : ViewModelBase
{
    private string _installPathStatus = string.Empty;
    private string _cleanRedeployStatus = string.Empty;
    private bool _isCleanRedeploying;

    public LoaderSettingsViewModel()
    {
        ValidateInstallPath();

        // Commands
        CleanRedeployCommand = ReactiveCommand.CreateFromTask(CleanRedeployAsync);
        OpenModkitLogCommand = ReactiveCommand.Create(OpenModkitLog);
        OpenMelonLoaderLogCommand = ReactiveCommand.Create(OpenMelonLoaderLog);
        OpenModkitLogFolderCommand = ReactiveCommand.Create(OpenModkitLogFolder);
        OpenMelonLoaderLogFolderCommand = ReactiveCommand.Create(OpenMelonLoaderLogFolder);
        OpenSavesFolderCommand = ReactiveCommand.Create(OpenSavesFolder);
    }

    public string GameInstallPath
    {
        get => AppSettings.Instance.GameInstallPath;
        set
        {
            // Normalize the path: trim whitespace, remove trailing slashes
            var normalizedPath = NormalizePath(value);
            if (AppSettings.Instance.GameInstallPath != normalizedPath)
            {
                AppSettings.Instance.SetGameInstallPath(normalizedPath);
                this.RaisePropertyChanged();
                ValidateInstallPath();
                this.RaisePropertyChanged(nameof(MelonLoaderLogPath));
            }
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        // Trim whitespace and quotes (users sometimes paste paths with quotes)
        path = path.Trim().Trim('"', '\'');

        // Remove trailing directory separators
        path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // Normalize path separators for the current platform
        if (OperatingSystem.IsWindows())
            path = path.Replace('/', '\\');
        else
            path = path.Replace('\\', '/');

        return path;
    }

    public string InstallPathStatus
    {
        get => _installPathStatus;
        private set => this.RaiseAndSetIfChanged(ref _installPathStatus, value);
    }

    public string CleanRedeployStatus
    {
        get => _cleanRedeployStatus;
        private set => this.RaiseAndSetIfChanged(ref _cleanRedeployStatus, value);
    }

    public bool IsCleanRedeploying
    {
        get => _isCleanRedeploying;
        private set => this.RaiseAndSetIfChanged(ref _isCleanRedeploying, value);
    }

    // Log paths
    public string ModkitLogPath => ModkitLog.LogPath;
    public string MelonLoaderLogPath => string.IsNullOrEmpty(GameInstallPath)
        ? "(set game install path)"
        : Path.Combine(GameInstallPath, "MelonLoader", "Latest.log");

    // Commands
    public ReactiveCommand<Unit, Unit> CleanRedeployCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenModkitLogCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenMelonLoaderLogCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenModkitLogFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenMelonLoaderLogFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenSavesFolderCommand { get; }

    private void ValidateInstallPath()
    {
        if (string.IsNullOrWhiteSpace(GameInstallPath))
        {
            InstallPathStatus = "Please set the game installation directory";
            return;
        }

        if (!Directory.Exists(GameInstallPath))
        {
            InstallPathStatus = "❌ Directory not found";
            return;
        }

        // Check for Menace_Data folder (case-insensitive on Linux)
        if (!HasGameDataFolder(GameInstallPath))
        {
            InstallPathStatus = "❌ Not a valid Menace installation (Menace_Data folder not found)";
            return;
        }

        InstallPathStatus = "✓ Game installation found";
    }

    /// <summary>
    /// Checks if a directory contains a Unity game data folder (case-insensitive).
    /// </summary>
    private static bool HasGameDataFolder(string gamePath)
    {
        // Direct check (works on case-insensitive filesystems like Windows/macOS)
        var expectedPath = Path.Combine(gamePath, "Menace_Data");
        if (Directory.Exists(expectedPath))
            return true;

        // On case-sensitive filesystems (Linux), search for the folder
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
        {
            try
            {
                var dirs = Directory.GetDirectories(gamePath);
                foreach (var dir in dirs)
                {
                    var dirName = Path.GetFileName(dir);
                    if (dirName != null && dirName.Equals("Menace_Data", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch
            {
                // Directory access issues
            }
        }

        return false;
    }

    private async Task CleanRedeployAsync()
    {
        IsCleanRedeploying = true;
        try
        {
            if (string.IsNullOrWhiteSpace(GameInstallPath) || !Directory.Exists(GameInstallPath))
            {
                CleanRedeployStatus = "❌ Set a valid game installation path first";
                return;
            }

            var installer = new ModLoaderInstaller(GameInstallPath);

            CleanRedeployStatus = "Cleaning Mods directory...";
            await installer.CleanModsDirectoryAsync(s => CleanRedeployStatus = s);

            // Install all required components (same as Setup would)
            if (!await installer.InstallAllRequiredAsync(s => CleanRedeployStatus = s))
                return;

            CleanRedeployStatus = "✓ Clean redeploy complete. Go to Load Order and click Deploy All to redeploy your mods.";
        }
        catch (Exception ex)
        {
            CleanRedeployStatus = $"❌ Error during clean redeploy: {ex.Message}";
        }
        finally
        {
            IsCleanRedeploying = false;
        }
    }

    private void OpenModkitLog()
    {
        var path = ModkitLog.LogPath;
        if (File.Exists(path))
            OpenFileInDefaultApp(path);
    }

    private void OpenMelonLoaderLog()
    {
        if (string.IsNullOrEmpty(GameInstallPath))
            return;

        var path = Path.Combine(GameInstallPath, "MelonLoader", "Latest.log");
        if (File.Exists(path))
            OpenFileInDefaultApp(path);
    }

    private void OpenModkitLogFolder()
    {
        var dir = Path.GetDirectoryName(ModkitLog.LogPath);
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            OpenFolderInExplorer(dir);
    }

    private void OpenMelonLoaderLogFolder()
    {
        if (string.IsNullOrEmpty(GameInstallPath))
            return;

        var dir = Path.Combine(GameInstallPath, "MelonLoader");
        if (Directory.Exists(dir))
            OpenFolderInExplorer(dir);
    }

    private void OpenSavesFolder()
    {
        if (string.IsNullOrEmpty(GameInstallPath))
            return;

        var dir = Path.Combine(GameInstallPath, "UserData", "Saves");
        if (Directory.Exists(dir))
            OpenFolderInExplorer(dir);
    }

    private static void OpenFileInDefaultApp(string filePath)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", filePath);
            else // Linux
                Process.Start("xdg-open", filePath);
        }
        catch { }
    }

    private static void OpenFolderInExplorer(string folderPath)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo("explorer.exe", folderPath) { UseShellExecute = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", folderPath);
            else // Linux
                Process.Start("xdg-open", folderPath);
        }
        catch { }
    }
}
