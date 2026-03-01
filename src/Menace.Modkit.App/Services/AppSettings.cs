using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Menace.Modkit.Core.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Central application settings service.
/// Settings are persisted to a JSON file in the user's config directory
/// so they survive app updates and reinstalls.
/// </summary>
public class AppSettings
{
    private static AppSettings? _instance;
    private string _gameInstallPath = string.Empty;
    private string _extractedAssetsPath = string.Empty;
    private bool _enableDeveloperTools = false;
    private bool? _enableMcpServer = null; // null = auto-detect, true/false = explicit
    private bool _hasUsedModdingTools = false;
    private string _updateChannel = "stable";
    private ExtractionSettings _extractionSettings = new();

    private class PersistedSettings
    {
        [JsonPropertyName("gameInstallPath")]
        public string? GameInstallPath { get; set; }

        [JsonPropertyName("extractedAssetsPath")]
        public string? ExtractedAssetsPath { get; set; }

        [JsonPropertyName("enableDeveloperTools")]
        public bool EnableDeveloperTools { get; set; }

        [JsonPropertyName("enableMcpServer")]
        public bool? EnableMcpServer { get; set; }

        [JsonPropertyName("hasUsedModdingTools")]
        public bool HasUsedModdingTools { get; set; }

        [JsonPropertyName("updateChannel")]
        public string UpdateChannel { get; set; } = "stable";
    }

    private static string GetSettingsFilePath()
    {
        var configDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(configDir, "MenaceModkit", "settings.json");
    }

    private AppSettings()
    {
        LoadFromDisk();

        // Only auto-detect if no saved path
        if (string.IsNullOrEmpty(_gameInstallPath))
            _gameInstallPath = DetectGameInstallPath();
    }

    private void LoadFromDisk()
    {
        try
        {
            var path = GetSettingsFilePath();
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<PersistedSettings>(json);
            if (data == null) return;

            if (!string.IsNullOrEmpty(data.GameInstallPath))
            {
                _gameInstallPath = data.GameInstallPath;
                ModkitLog.Info($"Loaded saved game install path: {_gameInstallPath}");
            }

            if (!string.IsNullOrEmpty(data.ExtractedAssetsPath))
            {
                _extractedAssetsPath = data.ExtractedAssetsPath;
                ModkitLog.Info($"Loaded saved extracted assets path: {_extractedAssetsPath}");
            }

            _enableDeveloperTools = data.EnableDeveloperTools;
            if (_enableDeveloperTools)
                ModkitLog.Info("Developer tools enabled");

            _enableMcpServer = data.EnableMcpServer;
            if (_enableMcpServer.HasValue)
                ModkitLog.Info($"MCP server: {(_enableMcpServer.Value ? "enabled" : "disabled")}");

            _hasUsedModdingTools = data.HasUsedModdingTools;

            if (!string.IsNullOrEmpty(data.UpdateChannel))
            {
                _updateChannel = data.UpdateChannel;
                if (_updateChannel == "beta")
                    ModkitLog.Info("Update channel: beta");
            }
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"Failed to load settings: {ex.Message}");
        }
    }

    private void SaveToDisk()
    {
        try
        {
            var path = GetSettingsFilePath();
            var dir = Path.GetDirectoryName(path);
            if (dir != null)
                Directory.CreateDirectory(dir);

            var data = new PersistedSettings
            {
                GameInstallPath = string.IsNullOrEmpty(_gameInstallPath) ? null : _gameInstallPath,
                ExtractedAssetsPath = string.IsNullOrEmpty(_extractedAssetsPath) ? null : _extractedAssetsPath,
                EnableDeveloperTools = _enableDeveloperTools,
                EnableMcpServer = _enableMcpServer,
                HasUsedModdingTools = _hasUsedModdingTools,
                UpdateChannel = _updateChannel
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"Failed to save settings: {ex.Message}");
        }
    }

    private static string DetectGameInstallPath()
    {
        var gameNames = new[] { "Menace", "Menace Demo" };

        // Check Steam first
        foreach (var steamCommon in GetSteamCommonPaths())
        {
            foreach (var gameName in gameNames)
            {
                var gamePath = Path.Combine(steamCommon, gameName);
                if (Directory.Exists(gamePath))
                {
                    ModkitLog.Info($"Detected game install (Steam): {gamePath}");
                    return gamePath;
                }
            }
        }

        // Check GOG
        foreach (var gogGames in GetGogGamePaths())
        {
            foreach (var gameName in gameNames)
            {
                var gamePath = Path.Combine(gogGames, gameName);
                if (Directory.Exists(gamePath))
                {
                    ModkitLog.Info($"Detected game install (GOG): {gamePath}");
                    return gamePath;
                }
            }
        }

        // Check Xbox Game Pass / Microsoft Store (Windows only)
        foreach (var xboxPath in GetXboxGamePassPaths())
        {
            foreach (var gameName in gameNames)
            {
                // Game Pass uses "Content" subdirectory for actual game files
                var gamePath = Path.Combine(xboxPath, gameName, "Content");
                if (Directory.Exists(gamePath) && HasGameDataFolder(gamePath))
                {
                    ModkitLog.Info($"Detected game install (Xbox/Game Pass): {gamePath}");
                    return gamePath;
                }
                // Also check without Content subfolder
                gamePath = Path.Combine(xboxPath, gameName);
                if (Directory.Exists(gamePath) && HasGameDataFolder(gamePath))
                {
                    ModkitLog.Info($"Detected game install (Xbox/Game Pass): {gamePath}");
                    return gamePath;
                }
            }
        }

        ModkitLog.Warn("Game install path not auto-detected. Set it manually in Settings.");
        return string.Empty;
    }

    /// <summary>
    /// Discovers GOG Galaxy game installation folders.
    /// </summary>
    private static List<string> GetGogGamePaths()
    {
        var paths = new List<string>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsWindows())
        {
            // Standard GOG Galaxy installation paths
            paths.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GOG Galaxy", "Games"));
            paths.Add(@"C:\Program Files (x86)\GOG Galaxy\Games");
            paths.Add(@"C:\GOG Games");
            paths.Add(Path.Combine(home, "GOG Games"));

            // Check common alternate drive locations
            foreach (var drive in new[] { "D:", "E:", "F:", "G:" })
            {
                paths.Add(Path.Combine(drive, "GOG Games"));
                paths.Add(Path.Combine(drive, "GOG Galaxy", "Games"));
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            paths.Add(Path.Combine(home, "Library", "Application Support", "GOG.com", "Games"));
            paths.Add(Path.Combine(home, "GOG Games"));
        }
        else // Linux (GOG games via Wine/Heroic/Lutris)
        {
            paths.Add(Path.Combine(home, "GOG Games"));
            paths.Add(Path.Combine(home, "Games", "GOG"));
            // Heroic Games Launcher (default and common locations)
            paths.Add(Path.Combine(home, "Games", "Heroic"));
            paths.Add(Path.Combine(home, "Games", "Heroic", "GOG"));
            paths.Add(Path.Combine(home, ".config", "heroic", "GOG"));
            // Lutris
            paths.Add(Path.Combine(home, "Games"));
            paths.Add(Path.Combine(home, ".local", "share", "lutris", "runners", "wine", "prefix", "drive_c", "GOG Games"));
            // Bottles
            paths.Add(Path.Combine(home, ".var", "app", "com.usebottles.bottles", "data", "bottles"));
        }

        // Filter to existing directories
        return paths.Where(Directory.Exists).Distinct().ToList();
    }

    /// <summary>
    /// Discovers Xbox Game Pass / Microsoft Store game installation folders.
    /// </summary>
    private static List<string> GetXboxGamePassPaths()
    {
        var paths = new List<string>();

        if (!OperatingSystem.IsWindows())
            return paths;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Default Xbox app install location
        paths.Add(@"C:\XboxGames");

        // Check common alternate drive locations
        foreach (var drive in new[] { "C:", "D:", "E:", "F:", "G:" })
        {
            paths.Add(Path.Combine(drive, "XboxGames"));
            paths.Add(Path.Combine(drive, "Xbox Games"));
            paths.Add(Path.Combine(drive, "Games", "Xbox"));
        }

        // WindowsApps is usually restricted, but some setups allow access
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        paths.Add(Path.Combine(programFiles, "WindowsApps"));

        // Also check ModifiableWindowsApps (less restricted)
        paths.Add(Path.Combine(programFiles, "ModifiableWindowsApps"));

        return paths.Where(Directory.Exists).Distinct().ToList();
    }

    /// <summary>
    /// Checks if a directory contains a Unity game data folder (case-insensitive).
    /// Looks for Menace_Data or similar patterns.
    /// </summary>
    private static bool HasGameDataFolder(string gamePath)
    {
        return FindGameDataFolder(gamePath) != null;
    }

    /// <summary>
    /// Finds the game data folder within an installation directory.
    /// Handles case sensitivity on Linux by searching for Menace_Data case-insensitively.
    /// Returns the actual folder name found, or null if not found.
    /// </summary>
    private static string? FindGameDataFolder(string gamePath)
    {
        if (!Directory.Exists(gamePath))
            return null;

        try
        {
            // On Windows/macOS, filesystem is case-insensitive, so direct check works
            var expectedPath = Path.Combine(gamePath, "Menace_Data");
            if (Directory.Exists(expectedPath))
                return "Menace_Data";

            // On Linux (case-sensitive), search for the folder
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
            {
                var dirs = Directory.GetDirectories(gamePath);
                foreach (var dir in dirs)
                {
                    var dirName = Path.GetFileName(dir);
                    if (dirName.Equals("Menace_Data", StringComparison.OrdinalIgnoreCase))
                        return dirName;
                }
            }
        }
        catch
        {
            // Directory access issues - return null
        }

        return null;
    }

    /// <summary>
    /// Discovers Steam library folders by parsing libraryfolders.vdf,
    /// with fallback to common hardcoded paths.
    /// </summary>
    private static List<string> GetSteamCommonPaths()
    {
        var paths = new List<string>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Find Steam's main installation directory
        var steamRoots = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            steamRoots.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            steamRoots.Add(@"C:\Program Files (x86)\Steam");
            steamRoots.Add(Path.Combine(home, "Steam"));
        }
        else if (OperatingSystem.IsMacOS())
        {
            steamRoots.Add(Path.Combine(home, "Library", "Application Support", "Steam"));
        }
        else // Linux
        {
            steamRoots.Add(Path.Combine(home, ".steam", "debian-installation"));
            steamRoots.Add(Path.Combine(home, ".steam", "steam"));
            steamRoots.Add(Path.Combine(home, ".local", "share", "Steam"));
            // Flatpak Steam
            steamRoots.Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"));
            // Snap Steam
            steamRoots.Add(Path.Combine(home, "snap", "steam", "common", ".steam", "steam"));
        }

        // Try to parse libraryfolders.vdf for additional library paths
        foreach (var steamRoot in steamRoots.Where(Directory.Exists).Distinct())
        {
            var vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdfPath))
            {
                try
                {
                    var libraryPaths = ParseLibraryFoldersVdf(vdfPath);
                    foreach (var libPath in libraryPaths)
                    {
                        var commonPath = Path.Combine(libPath, "steamapps", "common");
                        if (Directory.Exists(commonPath) && !paths.Contains(commonPath))
                        {
                            paths.Add(commonPath);
                            ModkitLog.Info($"Found Steam library: {commonPath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModkitLog.Warn($"Failed to parse {vdfPath}: {ex.Message}");
                }
            }

            // Also add the main Steam common folder
            var mainCommon = Path.Combine(steamRoot, "steamapps", "common");
            if (Directory.Exists(mainCommon) && !paths.Contains(mainCommon))
                paths.Add(mainCommon);
        }

        // Fallback: add hardcoded common locations if we found nothing
        if (paths.Count == 0)
        {
            ModkitLog.Warn("No Steam libraries found via VDF, using hardcoded fallbacks");

            if (OperatingSystem.IsWindows())
            {
                paths.Add(@"C:\Program Files (x86)\Steam\steamapps\common");
                paths.Add(@"D:\SteamLibrary\steamapps\common");
                paths.Add(@"E:\SteamLibrary\steamapps\common");
                paths.Add(@"F:\SteamLibrary\steamapps\common");
            }
            else if (OperatingSystem.IsMacOS())
            {
                paths.Add(Path.Combine(home, "Library", "Application Support", "Steam", "steamapps", "common"));
            }
            else
            {
                paths.Add(Path.Combine(home, ".steam", "debian-installation", "steamapps", "common"));
                paths.Add(Path.Combine(home, ".steam", "steam", "steamapps", "common"));
                paths.Add(Path.Combine(home, ".local", "share", "Steam", "steamapps", "common"));
            }
        }

        return paths;
    }

    /// <summary>
    /// Parse Steam's libraryfolders.vdf to extract library paths.
    /// VDF is Valve's simple key-value format.
    /// </summary>
    private static List<string> ParseLibraryFoldersVdf(string vdfPath)
    {
        var libraryPaths = new List<string>();
        var lines = File.ReadAllLines(vdfPath);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Look for "path" entries: "path"		"D:\\SteamLibrary"
            if (trimmed.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
            {
                var parts = trimmed.Split('"', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var path = parts[1].Replace("\\\\", "\\");
                    if (Directory.Exists(path))
                        libraryPaths.Add(path);
                }
            }
        }

        return libraryPaths;
    }

    public static AppSettings Instance => _instance ??= new AppSettings();

    public string GameInstallPath
    {
        get => _gameInstallPath;
        set => _gameInstallPath = value;
    }

    /// <summary>
    /// User-configured path to extracted assets directory.
    /// When empty, falls back to auto-detected paths.
    /// </summary>
    public string ExtractedAssetsPath
    {
        get => _extractedAssetsPath;
        set => _extractedAssetsPath = value;
    }

    public ExtractionSettings ExtractionSettings
    {
        get => _extractionSettings;
        set => _extractionSettings = value;
    }

    /// <summary>
    /// When enabled, deploys developer/test modpacks like TestTacticalSDK.
    /// These are useful for SDK development and debugging but not for end users.
    /// </summary>
    public bool EnableDeveloperTools
    {
        get => _enableDeveloperTools;
        set => _enableDeveloperTools = value;
    }

    /// <summary>
    /// Whether the MCP server is enabled. Null means auto-detect (enable if AI clients found).
    /// </summary>
    public bool? EnableMcpServer
    {
        get => _enableMcpServer;
        set => _enableMcpServer = value;
    }

    /// <summary>
    /// Returns true if MCP should be enabled based on setting or auto-detection.
    /// </summary>
    public bool IsMcpEnabled => _enableMcpServer ?? false;

    /// <summary>
    /// True if the user has used Modding Tools features (implying they want data extraction).
    /// Set automatically when user navigates to Modding Tools from the home screen.
    /// Used to skip automatic data extraction for users who only use the Mod Loader.
    /// </summary>
    public bool HasUsedModdingTools
    {
        get => _hasUsedModdingTools;
        set => _hasUsedModdingTools = value;
    }

    /// <summary>
    /// The release channel the user is subscribed to ("stable" or "beta").
    /// Beta channel receives bleeding-edge builds; Stable receives well-tested releases.
    /// </summary>
    public string UpdateChannel => _updateChannel;

    /// <summary>
    /// Returns true if the user is on the beta channel.
    /// </summary>
    public bool IsBetaChannel => _updateChannel == "beta";

    public event EventHandler? GameInstallPathChanged;
    public event EventHandler? ExtractedAssetsPathChanged;
    public event EventHandler? ExtractionSettingsChanged;
    public event EventHandler? EnableDeveloperToolsChanged;
    public event EventHandler? EnableMcpServerChanged;
    public event EventHandler? UpdateChannelChanged;

    public void SetGameInstallPath(string path)
    {
        _gameInstallPath = path;
        SaveToDisk();
        GameInstallPathChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetExtractedAssetsPath(string path)
    {
        _extractedAssetsPath = path;
        SaveToDisk();
        ExtractedAssetsPathChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetExtractionSettings(ExtractionSettings settings)
    {
        _extractionSettings = settings;
        ExtractionSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetEnableDeveloperTools(bool enabled)
    {
        _enableDeveloperTools = enabled;
        SaveToDisk();
        EnableDeveloperToolsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetEnableMcpServer(bool? enabled)
    {
        _enableMcpServer = enabled;
        SaveToDisk();
        EnableMcpServerChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Set the release channel ("stable" or "beta").
    /// </summary>
    public void SetUpdateChannel(string channel)
    {
        if (_updateChannel != channel && (channel == "stable" || channel == "beta"))
        {
            _updateChannel = channel;
            SaveToDisk();
            ModkitLog.Info($"Update channel changed to: {channel}");
            UpdateChannelChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Mark that the user has used modding tools features.
    /// This enables automatic data extraction on game launch.
    /// </summary>
    public void SetHasUsedModdingTools(bool value = true)
    {
        if (_hasUsedModdingTools != value)
        {
            _hasUsedModdingTools = value;
            SaveToDisk();
            if (value)
                ModkitLog.Info("User has used Modding Tools - data extraction enabled");
        }
    }

    /// <summary>
    /// Validate the game install path has the expected structure for compilation.
    /// Returns a list of issues found (empty if valid).
    /// </summary>
    public static List<string> ValidateGameInstallPath(string? path = null)
    {
        var issues = new List<string>();
        path ??= Instance.GameInstallPath;

        if (string.IsNullOrEmpty(path))
        {
            issues.Add("Game install path is not set");
            return issues;
        }

        if (!Directory.Exists(path))
        {
            issues.Add($"Game install path does not exist: {path}");
            return issues;
        }

        // Check for MelonLoader installation
        var mlDir = Path.Combine(path, "MelonLoader");
        if (!Directory.Exists(mlDir))
            issues.Add("MelonLoader directory not found - is MelonLoader installed?");

        // Check for Il2CppAssemblies (needed for compilation references)
        var il2cppDir = Path.Combine(path, "MelonLoader", "Il2CppAssemblies");
        if (!Directory.Exists(il2cppDir))
            issues.Add("Il2CppAssemblies not found - run the game once with MelonLoader to generate them");

        // Check for dotnet runtime (needed for System references)
        var dotnetDir = Path.Combine(path, "dotnet");
        if (!Directory.Exists(dotnetDir))
            issues.Add("dotnet directory not found - MelonLoader may not have extracted the runtime yet");

        return issues;
    }

    /// <summary>
    /// Check if the game install path is valid for mod compilation.
    /// </summary>
    public static bool IsGameInstallPathValid(string? path = null)
    {
        return ValidateGameInstallPath(path).Count == 0;
    }

    /// <summary>
    /// Resolves the effective extracted assets directory path.
    /// Priority: 1) User-configured path, 2) GameInstallPath/UserData/ExtractedAssets, 3) AppContext/out2/assets
    /// Returns null if no valid path is found.
    /// </summary>
    public static string? GetEffectiveAssetsPath()
    {
        // Priority 1: User-configured path
        var configured = Instance.ExtractedAssetsPath;
        if (!string.IsNullOrEmpty(configured) && Directory.Exists(configured))
            return configured;

        // Priority 2: Game install derived path
        var gameInstallPath = Instance.GameInstallPath;
        if (!string.IsNullOrEmpty(gameInstallPath) && Directory.Exists(gameInstallPath))
        {
            var derived = Path.Combine(gameInstallPath, "UserData", "ExtractedAssets");
            if (Directory.Exists(derived))
                return derived;
        }

        // Priority 3: AppContext fallback (out2/assets)
        var fallback = Path.Combine(AppContext.BaseDirectory, "out2", "assets");
        if (Directory.Exists(fallback))
            return fallback;

        return null;
    }

    /// <summary>
    /// Resolves the effective output path for AssetRipper extraction.
    /// Uses the configured path if set, otherwise defaults to AppContext/out2/assets.
    /// Unlike GetEffectiveAssetsPath, this always returns a path (for writing).
    /// </summary>
    public static string GetAssetExtractionOutputPath()
    {
        var configured = Instance.ExtractedAssetsPath;
        if (!string.IsNullOrEmpty(configured))
            return configured;

        return Path.Combine(AppContext.BaseDirectory, "out2", "assets");
    }
}
