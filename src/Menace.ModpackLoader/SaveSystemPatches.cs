using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MelonLoader;
using Menace.SDK;
using Newtonsoft.Json;

namespace Menace.ModpackLoader;

/// <summary>
/// Watches for save file creation and writes .modmeta sidecar files.
/// Uses FileSystemWatcher instead of Harmony patches for IL2CPP compatibility.
/// </summary>
public static class SaveSystemPatches
{
    private static FileSystemWatcher _watcher;
    private static readonly object _lock = new();
    private static readonly HashSet<string> _pendingFiles = new();
    private static bool _initialized;
    private static string _savesPath;

    /// <summary>
    /// Initialize the save file watcher. Call after game paths are known.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        try
        {
            // Find the saves directory
            _savesPath = FindSavesPath();
            if (string.IsNullOrEmpty(_savesPath))
            {
                MelonLogger.Warning("[SaveSystemPatches] Saves directory not found, will retry later");
                return;
            }

            SetupWatcher(_savesPath);
            _initialized = true;
            MelonLogger.Msg($"[SaveSystemPatches] Watching for saves in: {_savesPath}");
            UnityEngine.Debug.Log($"[MODDED] Save tracking enabled - {ModRegistry.Count} mod(s) will be recorded");
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"[SaveSystemPatches] Failed to initialize: {ex.Message}");
        }
    }

    /// <summary>
    /// Try to reinitialize if not yet initialized (call periodically from scene load).
    /// </summary>
    public static void TryInitialize()
    {
        if (!_initialized)
            Initialize();
    }

    private static string FindSavesPath()
    {
        try
        {
            // Check common save locations
            var userDataPath = Path.Combine(Directory.GetCurrentDirectory(), "UserData", "Saves");
            if (Directory.Exists(userDataPath))
                return userDataPath;

            // Check Documents folder (Windows native or Wine/Proton)
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrEmpty(documents))
            {
                foreach (var gameName in new[] { "Menace", "Menace Demo" })
                {
                    var docSavePath = Path.Combine(documents, gameName, "Saves");
                    if (Directory.Exists(docSavePath))
                        return docSavePath;
                }
            }

            // On Linux, also check for Proton prefix paths
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var steamDir = Path.Combine(homeDir, ".steam", "steam", "steamapps");
                if (Directory.Exists(steamDir))
                {
                    var compatdataDir = Path.Combine(steamDir, "compatdata");
                    if (Directory.Exists(compatdataDir))
                    {
                        foreach (var appIdDir in Directory.GetDirectories(compatdataDir))
                        {
                            foreach (var gameName in new[] { "Menace", "Menace Demo" })
                            {
                                var protonSavePath = Path.Combine(appIdDir, "pfx", "drive_c", "users", "steamuser", "Documents", gameName, "Saves");
                                if (Directory.Exists(protonSavePath))
                                    return protonSavePath;
                            }
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"[SaveSystemPatches] Error finding saves path: {ex.Message}");
            return null;
        }
    }

    private static void SetupWatcher(string savesPath)
    {
        _watcher = new FileSystemWatcher(savesPath)
        {
            Filter = "*.save",
            NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnSaveFileChanged;
        _watcher.Changed += OnSaveFileChanged;
    }

    private static void OnSaveFileChanged(object sender, FileSystemEventArgs e)
    {
        // Queue the file for processing (debounce rapid events)
        lock (_lock)
        {
            if (_pendingFiles.Contains(e.FullPath))
                return;
            _pendingFiles.Add(e.FullPath);
        }

        // Process after a short delay to ensure the file is fully written
        ThreadPool.QueueUserWorkItem(ProcessSaveFileDelayed, e.FullPath);
    }

    private static void ProcessSaveFileDelayed(object state)
    {
        Thread.Sleep(500); // Wait for file to be fully written
        ProcessSaveFile((string)state);
    }

    private static void ProcessSaveFile(string savePath)
    {
        try
        {
            lock (_lock)
            {
                _pendingFiles.Remove(savePath);
            }

            if (!File.Exists(savePath))
                return;

            // Only write modmeta if we have mods loaded
            if (!ModRegistry.HasMods)
                return;

            WriteModMetaFile(savePath);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"[SaveSystemPatches] Error processing save: {ex.Message}");
        }
    }

    /// <summary>
    /// Write a .modmeta sidecar file with information about active mods.
    /// </summary>
    private static void WriteModMetaFile(string savePath)
    {
        try
        {
            var modmetaPath = savePath + ".modmeta";

            // Check if modmeta already exists and is up-to-date
            if (File.Exists(modmetaPath))
            {
                var saveTime = File.GetLastWriteTimeUtc(savePath);
                var metaTime = File.GetLastWriteTimeUtc(modmetaPath);
                if (metaTime >= saveTime)
                    return; // Already up-to-date
            }

            var modmeta = new ModMetaData
            {
                SavedWith = "Menace Modpack Loader",
                LoaderVersion = ModkitVersion.Short,
                Timestamp = DateTime.UtcNow.ToString("o"),
                GameVersion = GetGameVersion(),
                Mods = ModRegistry.GetLoadedMods()
            };

            var json = JsonConvert.SerializeObject(modmeta, Formatting.Indented);
            File.WriteAllText(modmetaPath, json, Encoding.UTF8);

            MelonLogger.Msg($"[SaveSystemPatches] Wrote modmeta: {Path.GetFileName(modmetaPath)}");

            // Log to Player.log for developer triage
            UnityEngine.Debug.Log($"[MODDED] Save '{Path.GetFileNameWithoutExtension(savePath)}' created with {modmeta.Mods.Count} mod(s) active");
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"[SaveSystemPatches] Failed to write modmeta: {ex.Message}");
        }
    }

    private static string GetGameVersion()
    {
        try
        {
            return UnityEngine.Application.version ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Cleanup on shutdown.
    /// </summary>
    public static void Shutdown()
    {
        try
        {
            _watcher?.Dispose();
            _watcher = null;
        }
        catch { }
    }

    /// <summary>
    /// Data structure for the .modmeta sidecar file.
    /// </summary>
    public class ModMetaData
    {
        [JsonProperty("savedWith")]
        public string SavedWith { get; set; }

        [JsonProperty("loaderVersion")]
        public string LoaderVersion { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("gameVersion")]
        public string GameVersion { get; set; }

        [JsonProperty("mods")]
        public List<ModInfo> Mods { get; set; } = new();
    }

    /// <summary>
    /// Information about a single mod.
    /// </summary>
    public class ModInfo
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }
    }
}

/// <summary>
/// Central registry of loaded mods for use by save system and other components.
/// </summary>
public static class ModRegistry
{
    private static readonly List<SaveSystemPatches.ModInfo> _loadedMods = new();

    /// <summary>
    /// Register a modpack as loaded.
    /// </summary>
    public static void RegisterModpack(string name, string version, string author)
    {
        _loadedMods.Add(new SaveSystemPatches.ModInfo
        {
            Id = SanitizeId(name),
            Name = name,
            Version = version ?? "1.0.0",
            Author = author ?? "Unknown"
        });
    }

    /// <summary>
    /// Register a plugin DLL as loaded.
    /// </summary>
    public static void RegisterPlugin(string modpackName, string pluginName)
    {
        // Plugins are tracked under their parent modpack, but we could track separately
        // For now, modpacks are the primary unit
    }

    /// <summary>
    /// Get information about all loaded mods.
    /// </summary>
    public static List<SaveSystemPatches.ModInfo> GetLoadedMods()
    {
        return new List<SaveSystemPatches.ModInfo>(_loadedMods);
    }

    /// <summary>
    /// Get count of loaded mods.
    /// </summary>
    public static int Count => _loadedMods.Count;

    /// <summary>
    /// Check if any mods are loaded.
    /// </summary>
    public static bool HasMods => _loadedMods.Count > 0;

    private static string SanitizeId(string name)
    {
        if (string.IsNullOrEmpty(name)) return "unknown";
        return string.Concat(name.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'));
    }
}
