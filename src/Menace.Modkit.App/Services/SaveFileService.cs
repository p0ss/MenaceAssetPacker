using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Service for reading, writing, and managing save files.
/// </summary>
public class SaveFileService
{
    private const int CURRENT_SAVE_VERSION = 101;
    // Be lenient with version checking - we only read/write the header
    private const int OLDEST_SUPPORTED_VERSION = 1;
    // Minimum version for body parsing (versions < 22 have different format)
    private const int MINIMUM_BODY_VERSION = 22;
    // Maximum version we know how to parse body data for
    private const int MAXIMUM_BODY_VERSION = 101;

    /// <summary>
    /// Gets the save folder path based on the game install path.
    /// Checks multiple locations including Proton/Wine prefixes on Linux.
    /// Returns null if the game path is not configured.
    /// </summary>
    public string? GetSaveFolderPath()
    {
        var (path, _) = GetSaveFolderPathWithReason();
        return path;
    }

    /// <summary>
    /// Gets diagnostic info about the save folder path for debugging.
    /// </summary>
    public (string? path, string reason) GetSaveFolderPathWithReason()
    {
        var gameInstallPath = AppSettings.Instance.GameInstallPath;

        if (string.IsNullOrEmpty(gameInstallPath))
            return (null, "Game install path is not set. Go to Settings to configure it.");

        if (!Directory.Exists(gameInstallPath))
            return (null, $"Game install path does not exist: {gameInstallPath}");

        // Try standard location first (Windows or native)
        var savesPath = Path.Combine(gameInstallPath, "UserData", "Saves");
        if (Directory.Exists(savesPath))
        {
            ModkitLog.Info($"[SaveFileService] Found saves at standard path: {savesPath}");
            return (savesPath, "OK");
        }

        // On Linux with Proton/Wine, saves are in the Wine prefix
        // Path: ~/.steam/.../compatdata/{APP_ID}/pfx/drive_c/users/steamuser/Documents/{GameName}/Saves/
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var protonPath = TryFindProtonSavePath(gameInstallPath);
            if (protonPath != null && Directory.Exists(protonPath))
            {
                ModkitLog.Info($"[SaveFileService] Found saves at Proton path: {protonPath}");
                return (protonPath, "OK");
            }
        }

        // Check AppData/LocalLow (Unity's PersistentDataPath) - this is where the full game saves
        var localLowPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localLowPath))
        {
            // LocalApplicationData gives us AppData/Local, but Unity uses AppData/LocalLow
            var localLowParent = Path.GetDirectoryName(localLowPath);
            if (!string.IsNullOrEmpty(localLowParent))
            {
                var appDataSavePath = Path.Combine(localLowParent, "LocalLow", "Overhype Studios", "Menace", "Saves");
                if (Directory.Exists(appDataSavePath))
                {
                    ModkitLog.Info($"[SaveFileService] Found saves in AppData/LocalLow: {appDataSavePath}");
                    return (appDataSavePath, "OK");
                }
            }
        }

        // Check Windows Documents folder as fallback (demo may use this)
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrEmpty(documentsPath))
        {
            // Try both "Menace" and "Menace Demo"
            foreach (var gameName in new[] { "Menace", "Menace Demo" })
            {
                var docSavePath = Path.Combine(documentsPath, gameName, "Saves");
                if (Directory.Exists(docSavePath))
                {
                    ModkitLog.Info($"[SaveFileService] Found saves in Documents: {docSavePath}");
                    return (docSavePath, "OK");
                }
            }
        }

        var checkedPaths = $"Checked:\n- {savesPath}";
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            checkedPaths += "\n- Proton/Wine prefix paths (AppData/LocalLow and Documents)";
        }
        checkedPaths += $"\n- AppData/LocalLow/Overhype Studios/Menace/Saves\n- Documents folder";

        return (null, $"Saves folder not found.\n{checkedPaths}\n\nPlay the game to create saves.");
    }

    /// <summary>
    /// Tries to find the Proton/Wine prefix save path for a Steam game.
    /// </summary>
    private static string? TryFindProtonSavePath(string gameInstallPath)
    {
        try
        {
            // gameInstallPath is like: ~/.steam/.../steamapps/common/Menace
            // We need to find: ~/.steam/.../steamapps/compatdata/{APP_ID}/pfx/drive_c/users/steamuser/Documents/Menace/Saves

            // Navigate up to steamapps
            var steamappsDir = gameInstallPath;
            while (!string.IsNullOrEmpty(steamappsDir) && !steamappsDir.EndsWith("steamapps"))
            {
                steamappsDir = Path.GetDirectoryName(steamappsDir);
            }

            if (string.IsNullOrEmpty(steamappsDir) || !Directory.Exists(steamappsDir))
                return null;

            var compatdataDir = Path.Combine(steamappsDir, "compatdata");
            if (!Directory.Exists(compatdataDir))
                return null;

            // Get the game folder name (e.g., "Menace" or "Menace Demo")
            var gameFolderName = Path.GetFileName(gameInstallPath);

            // First, try to find the correct app ID from the manifest file
            // This ensures we use the right compatdata folder for the installed game
            var appId = FindAppIdForGame(steamappsDir, gameFolderName);
            if (!string.IsNullOrEmpty(appId))
            {
                var appIdCompatDir = Path.Combine(compatdataDir, appId);
                if (Directory.Exists(appIdCompatDir))
                {
                    var pfxBase = Path.Combine(appIdCompatDir, "pfx", "drive_c", "users", "steamuser");

                    // Full game uses Unity's persistent data path: AppData/LocalLow/Overhype Studios/Menace/Saves
                    var appDataPath = Path.Combine(pfxBase, "AppData", "LocalLow", "Overhype Studios", "Menace", "Saves");
                    if (Directory.Exists(appDataPath))
                    {
                        ModkitLog.Info($"[SaveFileService] Found saves via AppData path (appId {appId}): {appDataPath}");
                        return appDataPath;
                    }

                    // Demo uses Documents folder: Documents/Menace Demo/Saves
                    var documentsPath = Path.Combine(pfxBase, "Documents", gameFolderName, "Saves");
                    if (Directory.Exists(documentsPath))
                    {
                        ModkitLog.Info($"[SaveFileService] Found saves via Documents path (appId {appId}): {documentsPath}");
                        return documentsPath;
                    }

                    // Try other common folder names in Documents
                    foreach (var docFolderName in new[] { "Menace", "Menace Demo" })
                    {
                        documentsPath = Path.Combine(pfxBase, "Documents", docFolderName, "Saves");
                        if (Directory.Exists(documentsPath))
                        {
                            ModkitLog.Info($"[SaveFileService] Found saves via Documents variant (appId {appId}): {documentsPath}");
                            return documentsPath;
                        }
                    }
                }
            }

            // Fallback: search all app IDs, checking both AppData and Documents paths
            // Prioritize AppData/LocalLow (full game) over Documents (demo)
            string? fallbackPath = null;
            foreach (var appIdDir in Directory.GetDirectories(compatdataDir))
            {
                var pfxBase = Path.Combine(appIdDir, "pfx", "drive_c", "users", "steamuser");

                // First priority: AppData/LocalLow path (full game)
                var appDataPath = Path.Combine(pfxBase, "AppData", "LocalLow", "Overhype Studios", "Menace", "Saves");
                if (Directory.Exists(appDataPath))
                {
                    ModkitLog.Info($"[SaveFileService] Found saves via fallback AppData search: {appDataPath}");
                    return appDataPath;
                }

                // Second priority: Documents path with exact game folder name
                var documentsPath = Path.Combine(pfxBase, "Documents", gameFolderName, "Saves");
                if (Directory.Exists(documentsPath))
                {
                    // For full game, return immediately; for demo, save as fallback
                    if (gameFolderName == "Menace")
                    {
                        ModkitLog.Info($"[SaveFileService] Found saves via fallback Documents search: {documentsPath}");
                        return documentsPath;
                    }
                    else if (fallbackPath == null)
                    {
                        fallbackPath = documentsPath;
                    }
                }
            }

            if (fallbackPath != null)
            {
                ModkitLog.Info($"[SaveFileService] Found saves via fallback: {fallbackPath}");
            }

            return fallbackPath;
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[SaveFileService] Error searching Proton paths: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Finds the Steam app ID for a game by parsing appmanifest files in steamapps.
    /// </summary>
    private static string? FindAppIdForGame(string steamappsDir, string gameFolderName)
    {
        try
        {
            // Look for appmanifest_*.acf files that reference the game folder
            foreach (var manifestPath in Directory.GetFiles(steamappsDir, "appmanifest_*.acf"))
            {
                var content = File.ReadAllText(manifestPath);

                // Check if this manifest is for our game by looking for installdir
                // Format: "installdir"		"Menace"
                if (content.Contains($"\"installdir\"") &&
                    content.Contains($"\"{gameFolderName}\"", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract app ID from filename: appmanifest_2337210.acf -> 2337210
                    var fileName = Path.GetFileNameWithoutExtension(manifestPath);
                    if (fileName.StartsWith("appmanifest_"))
                    {
                        var appId = fileName.Substring("appmanifest_".Length);
                        ModkitLog.Info($"[SaveFileService] Found appId {appId} for game folder '{gameFolderName}'");
                        return appId;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[SaveFileService] Error finding app ID: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Discovers all save files and parses their headers.
    /// </summary>
    public List<SaveFileHeader> DiscoverSaveFiles()
    {
        var saves = new List<SaveFileHeader>();

        var saveFolderPath = GetSaveFolderPath();
        if (saveFolderPath == null || !Directory.Exists(saveFolderPath))
            return saves;

        try
        {
            foreach (var file in Directory.GetFiles(saveFolderPath, "*.save"))
            {
                var header = ParseHeader(file);
                saves.Add(header);
            }

            // Sort by modification time, most recent first
            saves.Sort((a, b) => b.ModifiedTime.CompareTo(a.ModifiedTime));
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[SaveFileService] Error discovering save files: {ex.Message}");
        }

        return saves;
    }

    /// <summary>
    /// Parses the header from a save file.
    /// </summary>
    public SaveFileHeader ParseHeader(string filePath)
    {
        var header = new SaveFileHeader
        {
            FilePath = filePath,
            ModifiedTime = File.GetLastWriteTime(filePath)
        };

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);

            // Version (int32)
            header.Version = reader.ReadInt32();
            ModkitLog.Info($"[SaveFileService] Parsing save with version {header.Version}: {filePath}");

            // SaveStateType - ALWAYS int32 in all versions
            header.SaveStateType = (SaveStateType)reader.ReadInt32();

            // DateTime (int64 ticks)
            var ticks = reader.ReadInt64();
            header.SaveTime = new DateTime(ticks);

            // PlanetName (length-prefixed string)
            header.PlanetName = reader.ReadString();

            // OperationName (length-prefixed string)
            header.OperationName = reader.ReadString();

            // CompletedMissions (int32)
            header.CompletedMissions = reader.ReadInt32();

            // OperationLength (int32)
            header.OperationLength = reader.ReadInt32();

            // Difficulty (length-prefixed string)
            header.Difficulty = reader.ReadString();

            // StrategyConfigName (only if version > 27)
            if (header.Version > 27)
            {
                header.StrategyConfigName = reader.ReadString();
            }

            // PlayTimeSeconds (double)
            header.PlayTimeSeconds = reader.ReadDouble();

            // SaveGameName (length-prefixed string)
            header.SaveGameName = reader.ReadString();

            // Record where the body starts
            header.BodyOffset = fs.Position;
            header.IsValid = true;
        }
        catch (EndOfStreamException)
        {
            header.IsValid = false;
            header.ErrorMessage = "Save file appears truncated or corrupted.";
            ModkitLog.Warn($"[SaveFileService] End of stream parsing header for {filePath}");
        }
        catch (Exception ex)
        {
            header.IsValid = false;
            header.ErrorMessage = "Unable to read save file.";
            ModkitLog.Error($"[SaveFileService] Error parsing header for {filePath}: {ex.Message}");
        }

        // Try to load modmeta sidecar file
        header.ModMeta = LoadModMeta(filePath);

        return header;
    }

    /// <summary>
    /// Loads the .modmeta sidecar file if it exists.
    /// </summary>
    private ModMetaData? LoadModMeta(string saveFilePath)
    {
        try
        {
            var modmetaPath = saveFilePath + ".modmeta";
            if (!File.Exists(modmetaPath))
                return null;

            var json = File.ReadAllText(modmetaPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return JsonSerializer.Deserialize<ModMetaData>(json, options);
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[SaveFileService] Failed to load modmeta: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses the save body (StrategyState and nested data).
    /// Uses a two-phase approach: first parse essential data (resources),
    /// then optionally parse extended data (squaddies, leaders, planets).
    /// Note: Only supports version 22+ (full game). Demo saves (version < 22) have a different format.
    /// </summary>
    public SaveBodyData ParseBody(SaveFileHeader header)
    {
        var body = new SaveBodyData();

        if (!header.IsValid || header.BodyOffset <= 0)
        {
            body.IsValid = false;
            body.ErrorMessage = "Invalid header";
            return body;
        }

        // Check version compatibility - versions < 22 (demo) have different body format
        if (header.Version < MINIMUM_BODY_VERSION)
        {
            body.IsValid = false;
            body.ErrorMessage = $"Save editing not supported for demo saves (version {header.Version}).";
            ModkitLog.Info($"[SaveFileService] Skipping body parsing for version {header.Version} (< {MINIMUM_BODY_VERSION})");
            return body;
        }

        // Check if save is from a newer game version than we support
        if (header.Version > MAXIMUM_BODY_VERSION)
        {
            body.IsValid = false;
            body.ErrorMessage = $"Save is from a newer game version ({header.Version}). Update the modkit to edit this save.";
            ModkitLog.Info($"[SaveFileService] Skipping body parsing for version {header.Version} (> {MAXIMUM_BODY_VERSION})");
            return body;
        }

        try
        {
            using var fs = new FileStream(header.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fileLength = fs.Length;

            // Validate body offset
            if (header.BodyOffset >= fileLength)
            {
                body.IsValid = false;
                body.ErrorMessage = "Save data could not be parsed (invalid structure).";
                ModkitLog.Warn($"[SaveFileService] Body offset ({header.BodyOffset}) is beyond file length ({fileLength})");
                return body;
            }

            fs.Seek(header.BodyOffset, SeekOrigin.Begin);
            using var reader = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);

            ModkitLog.Info($"[SaveFileService] Parsing body at offset {header.BodyOffset}, file length {fileLength}, remaining {fileLength - header.BodyOffset} bytes");

            // StrategyState fields (see save-system.md)
            // Check we have enough bytes for essential fields
            var remainingBytes = fileLength - fs.Position;
            if (remainingBytes < 30) // Minimum for basic fields
            {
                body.IsValid = false;
                body.ErrorMessage = "Save data too short to parse. The file may be corrupted.";
                ModkitLog.Warn($"[SaveFileService] Not enough data for body parsing ({remainingBytes} bytes remaining)");
                return body;
            }

            body.TotalPlayTimeInSec = reader.ReadDouble();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: TotalPlayTime: {body.TotalPlayTimeInSec}");

            // Track ironman offset for surgical edits
            body.IronmanOffset = fs.Position;
            body.Ironman = reader.ReadBoolean();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: Ironman: {body.Ironman}");

            body.IronmanSaveGameName = reader.ReadString();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: IronmanSaveGameName: '{body.IronmanSaveGameName}' (len={body.IronmanSaveGameName.Length})");

            body.Seed = reader.ReadInt32();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: Seed: {body.Seed}");

            body.HasPickedInitialItemPack = reader.ReadBoolean();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: HasPickedInitialItemPack: {body.HasPickedInitialItemPack}");

            body.HasPickedInitialLeaders = reader.ReadBoolean();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: HasPickedInitialLeaders: {body.HasPickedInitialLeaders}");

            // For version > 27, there's a boolean field then StrategyConfigName instead of GlobalDifficulty string
            if (header.Version > 27)
            {
                var hasStrategyConfig = reader.ReadBoolean();
                ModkitLog.Info($"[SaveFileService] @{fs.Position}: HasStrategyConfig: {hasStrategyConfig}");

                var strategyConfigName = reader.ReadString();
                ModkitLog.Info($"[SaveFileService] @{fs.Position}: StrategyConfigName: '{strategyConfigName}'");
                body.GlobalDifficulty = strategyConfigName;
            }
            else
            {
                body.GlobalDifficulty = reader.ReadString();
                ModkitLog.Info($"[SaveFileService] @{fs.Position}: GlobalDifficulty: '{body.GlobalDifficulty}' (len={body.GlobalDifficulty.Length})");
            }

            // Track StrategyVars offset for surgical edits
            body.StrategyVarsOffset = fs.Position;
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: About to read StrategyVars, remaining bytes: {fileLength - fs.Position}");

            // Read array count first to log it
            int varsCount = reader.ReadInt32();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: StrategyVars count: {varsCount}");

            body.StrategyVars = new int[varsCount];
            for (int i = 0; i < varsCount; i++)
                body.StrategyVars[i] = reader.ReadInt32();

            ModkitLog.Info($"[SaveFileService] StrategyVars: Credits={body.Credits}, Intel={body.Intelligence}, Authority={body.Authority}");

            // CheckCorruption marker - int32 = 42 (0x2A) for version >= 7
            if (header.Version >= 7)
            {
                int corruptionMarker = reader.ReadInt32();
                if (corruptionMarker != 42)
                {
                    ModkitLog.Warn($"[SaveFileService] Corruption marker mismatch: expected 42, got {corruptionMarker}");
                }
            }

            // Mark as valid - we have the essential resource data
            body.IsValid = true;
            ModkitLog.Info($"[SaveFileService] Parsed essential body data (resources) successfully");

            // Try to parse extended data, but don't fail if we can't
            try
            {
                // ShipUpgrades - skip
                ModkitLog.Info($"[SaveFileService] @{fs.Position}: Parsing ShipUpgrades...");
                SkipShipUpgrades(reader);

                // OwnedItems - skip
                ModkitLog.Info($"[SaveFileService] @{fs.Position}: Parsing OwnedItems...");
                SkipOwnedItems(reader, header.Version);

                // BlackMarket - skip
                ModkitLog.Info($"[SaveFileService] @{fs.Position}: Parsing BlackMarket...");
                SkipBlackMarket(reader);

                // StoryFactions - skip
                ModkitLog.Info($"[SaveFileService] @{fs.Position}: Parsing StoryFactions...");
                SkipStoryFactions(reader);

                // Squaddies
                ModkitLog.Info($"[SaveFileService] @{fs.Position}: Parsing Squaddies...");
                // Debug: peek at next bytes
                long peekPos = fs.Position;
                byte[] peekBytes = reader.ReadBytes(32);
                fs.Position = peekPos;
                ModkitLog.Info($"[SaveFileService] @{fs.Position}: Squaddies peek bytes: {BitConverter.ToString(peekBytes)}");
                body.Squaddies = ParseSquaddies(reader);

                // Roster (leaders)
                ModkitLog.Info($"[SaveFileService] @{fs.Position}: Parsing Roster...");
                var roster = ParseRoster(reader, header.Version);
                body.HiredLeaders = roster.hired;
                body.DismissedLeaders = roster.dismissed;
                body.DeadLeaders = roster.dead;

                // BattlePlan - skip
                ModkitLog.Info($"[SaveFileService] @{fs.Position}: Parsing BattlePlan...");
                SkipBattlePlan(reader);

                // PlanetManager - track offset for surgical edits
                ModkitLog.Info($"[SaveFileService] @{fs.Position}: Parsing PlanetManager...");
                body.PlanetsOffset = fs.Position;
                body.Planets = ParsePlanetManager(reader);

                // OperationsManager
                ModkitLog.Info($"[SaveFileService] @{fs.Position}: Parsing OperationsManager...");
                body.CurrentOperation = ParseOperationsManager(reader, header.Version);

                body.BodyEndOffset = fs.Position;
                ModkitLog.Info($"[SaveFileService] Parsed extended body data successfully, read {body.BodyEndOffset - header.BodyOffset} bytes");
            }
            catch (Exception extEx)
            {
                // Extended parsing failed, but we still have the essential data
                ModkitLog.Warn($"[SaveFileService] Extended body parsing failed: {extEx.Message}");
                // Keep IsValid = true since we have resources
            }
        }
        catch (EndOfStreamException ex)
        {
            // Specific handling for truncated/incompatible saves
            body.IsValid = false;
            body.ErrorMessage = "Save format not compatible with this version of the modkit. Editing not available.";
            ModkitLog.Warn($"[SaveFileService] End of stream during body parsing (save format mismatch): {ex.Message}");
        }
        catch (Exception ex)
        {
            body.IsValid = false;
            body.ErrorMessage = "Failed to parse save data. The file may be corrupted or from an unsupported version.";
            ModkitLog.Error($"[SaveFileService] Error parsing body: {ex.Message}\n{ex.StackTrace}");
        }

        return body;
    }

    #region Body Parsing Helpers

    private static int[] ReadIntArray(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        var arr = new int[count];
        for (int i = 0; i < count; i++)
            arr[i] = reader.ReadInt32();
        return arr;
    }

    private static string[] ReadStringArray(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        var arr = new string[count];
        for (int i = 0; i < count; i++)
            arr[i] = reader.ReadString();
        return arr;
    }

    /// <summary>
    /// Skip a list of template IDs. In version 101+, each entry has a byte prefix.
    /// </summary>
    private static void SkipTemplateList(BinaryReader reader, bool hasEntryPrefix = true)
    {
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            // In version 101+, each template entry has an extra byte (0x01) prefix
            if (hasEntryPrefix)
                reader.ReadByte();
            reader.ReadString(); // template ID
        }
    }

    private static void SkipShipUpgrades(BinaryReader reader)
    {
        var fs = reader.BaseStream;

        // m_SlotOverrides (template array)
        int slotOverridesCount = reader.ReadInt32();
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: ShipUpgrades.SlotOverrides count: {slotOverridesCount}");
        for (int i = 0; i < slotOverridesCount; i++)
            reader.ReadString();

        // Debug: peek at next few bytes to understand structure
        long peekPos = fs.Position;
        byte[] peekBytes = reader.ReadBytes(8);
        fs.Position = peekPos;
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: ShipUpgrades peek bytes: {BitConverter.ToString(peekBytes)}");

        // New fields added in recent versions - multiple byte-count string lists
        // Keep reading until we hit a count of 0 (terminator)
        int listIndex = 0;
        while (true)
        {
            int count = reader.ReadByte();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: ShipUpgrades.DiceList[{listIndex}] count (byte): {count}");
            if (count == 0)
                break;
            for (int i = 0; i < count; i++)
            {
                var str = reader.ReadString();
                ModkitLog.Info($"[SaveFileService] @{fs.Position}: ShipUpgrades.DiceList[{listIndex}][{i}]: '{str}'");
            }
            listIndex++;
            if (listIndex > 20) // Safety limit
            {
                ModkitLog.Warn($"[SaveFileService] Too many dice lists, stopping");
                break;
            }
        }

        // m_PermanentUpgrades (template list)
        int permUpgradesCount = reader.ReadInt32();
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: ShipUpgrades.PermanentUpgrades count: {permUpgradesCount}");
        for (int i = 0; i < permUpgradesCount; i++)
            reader.ReadString();

        // m_SlotLevels (int array)
        int slotLevelsCount = reader.ReadInt32();
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: ShipUpgrades.SlotLevels count: {slotLevelsCount}");
        for (int i = 0; i < slotLevelsCount; i++)
            reader.ReadInt32();

        // m_UpgradeAmounts (dict)
        int dictCount = reader.ReadInt32();
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: ShipUpgrades.UpgradeAmounts count: {dictCount}");
        for (int i = 0; i < dictCount; i++)
        {
            reader.ReadString(); // template
            reader.ReadInt32();  // amount
        }
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: ShipUpgrades done");
    }

    private static void SkipOwnedItems(BinaryReader reader, int version)
    {
        var fs = reader.BaseStream;

        // Debug: peek at next bytes to understand structure
        long peekPos = fs.Position;
        byte[] peekBytes = reader.ReadBytes(32);
        fs.Position = peekPos;
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: OwnedItems peek bytes: {BitConverter.ToString(peekBytes)}");

        // m_PurchasedDossiers (if version > 26)
        if (version > 26)
        {
            var dossiers = ReadIntArray(reader);
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: OwnedItems.PurchasedDossiers count: {dossiers.Length}");
        }

        // Vehicles
        int vehicleCount = reader.ReadInt32();
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: OwnedItems.Vehicles count: {vehicleCount}");
        for (int i = 0; i < vehicleCount; i++)
        {
            var template = reader.ReadString(); // EntityTemplate
            var guid = reader.ReadString(); // GUID
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: OwnedItems.Vehicle[{i}]: {template}");
            SkipVehicle(reader, version);
        }

        // Items (template -> list of GUIDs)
        int templateCount = reader.ReadInt32();
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: OwnedItems.ItemTemplates count: {templateCount}");
        for (int i = 0; i < templateCount; i++)
        {
            var template = reader.ReadString(); // BaseItemTemplate
            int itemCount = reader.ReadInt32();
            for (int j = 0; j < itemCount; j++)
                reader.ReadString(); // item GUID
            if (i < 5) // Only log first few
                ModkitLog.Info($"[SaveFileService] @{fs.Position}: OwnedItems.ItemTemplate[{i}]: {template} ({itemCount} items)");
        }
        if (templateCount > 5)
            ModkitLog.Info($"[SaveFileService] ... and {templateCount - 5} more item templates");

        // m_SeenItems
        int seenCount = reader.ReadInt32();
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: OwnedItems.SeenItems count: {seenCount}");
        for (int i = 0; i < seenCount; i++)
            reader.ReadString();

        ModkitLog.Info($"[SaveFileService] @{fs.Position}: OwnedItems done");
    }

    private static void SkipVehicle(BinaryReader reader, int version)
    {
        if (version >= 24)
        {
            reader.ReadSingle(); // m_HealthPercent
            reader.ReadSingle(); // m_ShieldPercent
        }
        else
        {
            reader.ReadInt32(); // old format
        }
        SkipTemplateList(reader); // m_OverrideSkills
    }

    /// <summary>
    /// Skip the real OwnedItems data that appears after the "*" squaddie entry.
    /// This data uses standard .NET format WITH count prefixes - the byte 0x70 is actually
    /// the low byte of vehicle count (2), not a string length!
    /// Format: int32 vehicle count, then vehicles, then items, then seen items.
    /// </summary>
    private static void SkipRealOwnedItems(BinaryReader reader)
    {
        var fs = reader.BaseStream;

        // Peek first few bytes to understand the format
        long startPos = fs.Position;
        byte[] peek = reader.ReadBytes(8);
        fs.Position = startPos;
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: RealOwnedItems peek: {BitConverter.ToString(peek)}");

        // The format here is STANDARD OwnedItems with counts, but the first byte happens to be
        // ASCII because the string happens to start with 'p' (0x70) which looks like a letter.
        // Actually, let me re-analyze: the vehicle count should be at the START.

        // Read vehicle count as int32
        int vehicleCount = reader.ReadInt32();
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: RealOwnedItems.Vehicles count: {vehicleCount}");

        // Sanity check - if count is huge, we're reading the wrong format
        if (vehicleCount < 0 || vehicleCount > 1000)
        {
            ModkitLog.Warn($"[SaveFileService] Vehicle count {vehicleCount} seems wrong, seeking back");
            fs.Position = startPos;

            // Try alternate format: maybe vehicles are listed without a count
            // Each vehicle: template$guid string, health, shield, skills
            int vehicleIdx = 0;
            while (vehicleIdx < 50) // Safety limit
            {
                long checkPos = fs.Position;
                // Read string and check if it contains '$' (vehicle identifier)
                try
                {
                    var str = reader.ReadString();
                    if (string.IsNullOrEmpty(str) || !str.Contains('$'))
                    {
                        // Not a vehicle - seek back and stop
                        fs.Position = checkPos;
                        break;
                    }
                    reader.ReadSingle(); // health
                    reader.ReadSingle(); // shield
                    SkipTemplateList(reader); // skills
                    vehicleIdx++;
                }
                catch
                {
                    fs.Position = checkPos;
                    break;
                }
            }
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: After {vehicleIdx} vehicles (alternate format)");
        }
        else
        {
            // Standard format with count
            for (int i = 0; i < vehicleCount; i++)
            {
                var templateGuid = reader.ReadString(); // Combined template$guid
                reader.ReadSingle(); // health
                reader.ReadSingle(); // shield
                SkipTemplateList(reader); // override skills
                if (i < 3)
                    ModkitLog.Info($"[SaveFileService] @{fs.Position}: Vehicle[{i}]: {templateGuid.Substring(0, Math.Min(60, templateGuid.Length))}...");
            }
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: After {vehicleCount} vehicles");
        }

        // Items: count, then (template string, sub-count, GUIDs)
        int itemCount = reader.ReadInt32();
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: RealOwnedItems.Items count: {itemCount}");
        if (itemCount >= 0 && itemCount < 10000)
        {
            for (int i = 0; i < itemCount; i++)
            {
                reader.ReadString(); // template
                int subCount = reader.ReadInt32();
                for (int j = 0; j < subCount; j++)
                    reader.ReadString(); // GUID
            }
        }
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: After items");

        // Seen items: count, then strings
        int seenCount = reader.ReadInt32();
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: RealOwnedItems.Seen count: {seenCount}");
        if (seenCount >= 0 && seenCount < 10000)
        {
            for (int i = 0; i < seenCount; i++)
                reader.ReadString();
        }

        ModkitLog.Info($"[SaveFileService] @{fs.Position}: RealOwnedItems done");
    }

    /// <summary>
    /// Skip per-squaddie owned items (vehicles, equipment) in newer save format.
    /// Format: vehicles with embedded GUIDs, items, accessories, etc.
    /// </summary>
    private static void SkipSquaddieOwnedItems(BinaryReader reader)
    {
        var fs = reader.BaseStream;

        // The format seems to be:
        // - Vehicles list (string template+GUID, float health, float shield, int skills_count, skills...)
        // - Items list (string template, count, GUIDs...)
        // We need to find where this ends and the next squaddie begins.

        // Read until we find a pattern that looks like the start of a new section
        // (typically a small int32 followed by byte values for gender/skin)

        // For now, use a simpler approach: read the vehicle and item data using counts
        // First check if we have int32 counts or direct string data

        // Peek to see what format we have
        long peekPos = fs.Position;
        byte[] peek = reader.ReadBytes(8);
        fs.Position = peekPos;
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: SquaddieOwnedItems peek: {BitConverter.ToString(peek)}");

        // Check if first 4 bytes look like a reasonable count (< 1000)
        int possibleCount = peek[0] | (peek[1] << 8) | (peek[2] << 16) | (peek[3] << 24);

        if (possibleCount < 0 || possibleCount > 1000)
        {
            // Doesn't look like a count, probably direct string data
            // This format has no count prefix - we need to read strings until we hit non-string data
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: SquaddieOwnedItems: No count prefix, scanning for structure...");

            // Try to read vehicle entries directly
            // Format appears to be: template$GUID (string), health (float), shield (float), skills (list)
            // Followed by items and seen items

            // Actually, let's try the full OwnedItems format but positioned here
            // Read vehicles
            int vehicleCount = reader.ReadInt32();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: SquaddieOwnedItems.Vehicles raw count: {vehicleCount}");

            // If count is unreasonable, this format doesn't have counts
            if (vehicleCount < 0 || vehicleCount > 100)
            {
                // Seek back and try to find end by scanning
                fs.Position = peekPos;
                ScanToSectionEnd(reader);
                return;
            }

            for (int i = 0; i < vehicleCount; i++)
            {
                reader.ReadString(); // template
                reader.ReadString(); // GUID
                reader.ReadSingle(); // health
                reader.ReadSingle(); // shield
                SkipTemplateList(reader); // skills
            }

            // Read items
            int itemCount = reader.ReadInt32();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: SquaddieOwnedItems.Items count: {itemCount}");
            for (int i = 0; i < itemCount; i++)
            {
                reader.ReadString(); // template
                int subCount = reader.ReadInt32();
                for (int j = 0; j < subCount; j++)
                    reader.ReadString(); // GUID
            }

            // Read seen items
            int seenCount = reader.ReadInt32();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: SquaddieOwnedItems.Seen count: {seenCount}");
            for (int i = 0; i < seenCount; i++)
                reader.ReadString();
        }
        else
        {
            // Looks like it has counts, use standard format
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: SquaddieOwnedItems: Has count prefix ({possibleCount})");

            // Vehicles
            int vehicleCount = reader.ReadInt32();
            for (int i = 0; i < vehicleCount; i++)
            {
                reader.ReadString(); // template
                reader.ReadString(); // GUID
                reader.ReadSingle(); // health
                reader.ReadSingle(); // shield
                SkipTemplateList(reader); // skills
            }

            // Items
            int itemCount = reader.ReadInt32();
            for (int i = 0; i < itemCount; i++)
            {
                reader.ReadString(); // template
                int subCount = reader.ReadInt32();
                for (int j = 0; j < subCount; j++)
                    reader.ReadString(); // GUID
            }

            // Seen items
            int seenCount = reader.ReadInt32();
            for (int i = 0; i < seenCount; i++)
                reader.ReadString();
        }

        ModkitLog.Info($"[SaveFileService] @{fs.Position}: SquaddieOwnedItems done");
    }

    /// <summary>
    /// Scan forward to find the end of a section by looking for structure markers.
    /// </summary>
    private static void ScanToSectionEnd(BinaryReader reader)
    {
        var fs = reader.BaseStream;
        long startPos = fs.Position;

        // Look for a pattern that indicates start of next section:
        // - A small int32 (0-100) followed by reasonable data
        // - Or end of file

        while (fs.Position < fs.Length - 8)
        {
            byte[] window = reader.ReadBytes(8);
            int val = window[0] | (window[1] << 8) | (window[2] << 16) | (window[3] << 24);

            // Look for a small count value (0-50) followed by patterns
            if (val >= 0 && val <= 50)
            {
                // Check if following bytes look reasonable
                // For squaddie: ID, gender byte, skin byte, etc.
                // For dead count: just the count then similar structure

                // Check if byte 4-5 could be gender/skin (0 or 1) after a small ID
                if (window[4] == 0 && window[5] == 0)
                {
                    // Could be dead count = val, next ID = small value
                    fs.Position -= 4; // Back up to re-read as dead count
                    ModkitLog.Info($"[SaveFileService] @{fs.Position}: ScanToSectionEnd found potential section start (val={val})");
                    return;
                }
            }

            // Move forward 1 byte and try again
            fs.Position -= 7;
        }

        // Didn't find anything, just use end
        fs.Position = startPos;
        ModkitLog.Warn($"[SaveFileService] @{fs.Position}: ScanToSectionEnd couldn't find section boundary");
    }

    private static void SkipBlackMarket(BinaryReader reader)
    {
        // m_ItemStacks → List<BlackMarketItemStack>
        int stackCount = reader.ReadInt32();
        for (int i = 0; i < stackCount; i++)
        {
            reader.ReadString();  // m_Template (BaseItemTemplate)
            reader.ReadInt32();   // m_Amount
            reader.ReadByte();    // m_Type (BlackMarketStackType enum, byte)

            // Items list
            int itemCount = reader.ReadInt32();
            for (int j = 0; j < itemCount; j++)
            {
                reader.ReadString(); // BaseItemTemplate
                reader.ReadString(); // itemGUID
            }
        }
    }

    private static void SkipStoryFactions(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            reader.ReadString(); // m_Template (StoryFactionTemplate)
            reader.ReadInt32();  // m_Reputation
            reader.ReadInt32();  // m_Status (StoryFactionStatus enum, int32)
            SkipTemplateList(reader); // m_UnlockedShipUpgrades
            ReadIntArray(reader); // m_DiscoveredMissions
        }
    }

    private static List<SquaddieData> ParseSquaddies(BinaryReader reader)
    {
        var fs = reader.BaseStream;
        var squaddies = new List<SquaddieData>();

        int nextId = reader.ReadInt32(); // m_NextSquaddieID
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: Squaddies.NextId: {nextId}");

        // m_AllSquaddies
        int allCount = reader.ReadInt32();
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: Squaddies.AllCount: {allCount}");
        for (int i = 0; i < allCount; i++)
        {
            long startPos = fs.Position;
            ModkitLog.Info($"[SaveFileService] @{startPos}: Starting Squaddie[{i}]");

            // Peek at first 20 bytes for debugging
            byte[] peek = reader.ReadBytes(20);
            fs.Position = startPos;
            ModkitLog.Info($"[SaveFileService] @{startPos}: Squaddie[{i}] peek: {BitConverter.ToString(peek)}");

            int id = reader.ReadInt32();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: Squaddie[{i}].ID = {id}");

            byte gender = reader.ReadByte();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: Squaddie[{i}].Gender = {gender}");

            byte skinColor = reader.ReadByte();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: Squaddie[{i}].SkinColor = {skinColor}");

            int homePlanetType = reader.ReadInt32();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: Squaddie[{i}].HomePlanetType = {homePlanetType}");

            int portraitIndex = reader.ReadInt32();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: Squaddie[{i}].PortraitIndex = {portraitIndex}");

            string firstName = reader.ReadString();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: Squaddie[{i}].FirstName = '{firstName}' (len={firstName.Length})");

            string lastName = reader.ReadString();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: Squaddie[{i}].LastName = '{lastName}' (len={lastName.Length})");

            string templateName = reader.ReadString();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: Squaddie[{i}].TemplateName = '{templateName}' (len={templateName.Length})");

            // In newer save versions, the "*" template indicates a company/squad inventory entry
            // The data after this entry is complex (OwnedItems, BlackMarket, StoryFactions, real squaddies)
            // but the AllCount=3 is not trustworthy. We need to skip past all this data.
            if (templateName == "*")
            {
                ModkitLog.Info($"[SaveFileService] @{fs.Position}: Found company entry (template='*'), scanning for Roster start");

                // Add this entry
                squaddies.Add(new SquaddieData
                {
                    ID = id,
                    Gender = gender,
                    SkinColor = skinColor,
                    HomePlanetType = homePlanetType,
                    PortraitIndex = portraitIndex,
                    FirstName = firstName,
                    LastName = lastName,
                    TemplateName = templateName
                });

                // The data format after "*" is complex. Scan forward to find the Roster start.
                // The Roster section begins with HiredCount (small int32, typically 0-10) followed by
                // ActorType string (like "Infantry") for each leader.
                // Look for pattern: small int32 + byte < 20 + "Infantry" or similar string
                long scanStart = fs.Position;
                bool found = false;

                // Scan byte by byte looking for Roster signature
                while (fs.Position < fs.Length - 20)
                {
                    long checkPos = fs.Position;
                    byte[] window = reader.ReadBytes(20);

                    // Read potential HiredCount as int32
                    int hiredCount = window[0] | (window[1] << 8) | (window[2] << 16) | (window[3] << 24);

                    // HiredCount should be 1-15 (reasonable roster size)
                    if (hiredCount >= 1 && hiredCount <= 15)
                    {
                        // Next byte should be string length for ActorType (like "Infantry" = 8 chars)
                        int strLen = window[4];
                        if (strLen >= 5 && strLen <= 20) // "Infantry" is 8, "Vehicle" is 7, etc.
                        {
                            // Check if the string looks like an ActorType
                            // "Infantry" starts with 'I' (0x49), "Vehicle" starts with 'V' (0x56)
                            char firstChar = (char)window[5];
                            if (firstChar == 'I' || firstChar == 'V')
                            {
                                // Read the potential string to verify
                                string potentialActorType = "";
                                for (int j = 0; j < strLen && j < 15; j++)
                                    potentialActorType += (char)window[5 + j];

                                if (potentialActorType.StartsWith("Infantry") || potentialActorType.StartsWith("Vehicle"))
                                {
                                    fs.Position = checkPos;
                                    ModkitLog.Info($"[SaveFileService] @{fs.Position}: Found Roster start: HiredCount={hiredCount}, ActorType='{potentialActorType}'");
                                    found = true;
                                    break;
                                }
                            }
                        }
                    }

                    // Move forward by 1 byte and try again
                    fs.Position = checkPos + 1;
                }

                if (found)
                {
                    // We found the Roster section - the parsing will continue from here
                    // Skip reading DeadCount since we're directly at Roster
                    ModkitLog.Info($"[SaveFileService] @{fs.Position}: Squaddies done (scanned to Roster)");
                    return squaddies;
                }
                else
                {
                    ModkitLog.Warn($"[SaveFileService] Couldn't find Roster section, returning partial data");
                    fs.Position = scanStart;
                    return squaddies;
                }
            }

            var squaddie = new SquaddieData
            {
                ID = id,
                Gender = gender,
                SkinColor = skinColor,
                HomePlanetType = homePlanetType,
                PortraitIndex = portraitIndex,
                FirstName = firstName,
                LastName = lastName,
                TemplateName = templateName
            };
            squaddies.Add(squaddie);
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: Finished Squaddie[{i}], consumed {fs.Position - startPos} bytes");
        }

        // m_DeadSquaddies - same format
        int deadCount = reader.ReadInt32();
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: Squaddies.DeadCount: {deadCount}");
        for (int i = 0; i < deadCount; i++)
        {
            reader.ReadInt32(); // ID
            reader.ReadByte();  // Gender (byte)
            reader.ReadByte();  // SkinColor (byte)
            reader.ReadInt32(); // HomePlanetType
            reader.ReadInt32(); // PortraitIndex (moved before names)
            reader.ReadString(); // FirstName
            reader.ReadString(); // LastName
            reader.ReadString(); // Template
        }
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: Squaddies done");

        return squaddies;
    }

    private static (List<LeaderData> hired, List<LeaderData> dismissed, List<LeaderData> dead) ParseRoster(BinaryReader reader, int version)
    {
        var fs = reader.BaseStream;

        // Peek at the next few bytes to understand structure
        long peekPos = fs.Position;
        byte[] peekBytes = reader.ReadBytes(32);
        fs.Position = peekPos;
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: Roster peek bytes: {BitConverter.ToString(peekBytes)}");

        ModkitLog.Info($"[SaveFileService] @{fs.Position}: Parsing Roster.HiredLeaders...");
        var hired = ParseLeaderList(reader, version, "Hired");
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: Parsing Roster.DismissedLeaders...");
        var dismissed = ParseLeaderList(reader, version, "Dismissed");
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: Parsing Roster.UnburiedLeaders...");
        var unburied = ParseLeaderList(reader, version, "Unburied"); // dead unburied
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: Parsing Roster.BuriedLeaders...");
        var buried = ParseLeaderList(reader, version, "Buried");   // dead buried

        // m_HirableLeaders
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: Parsing Roster.HirableLeaders...");
        SkipTemplateList(reader);

        // Combine dead lists
        var dead = new List<LeaderData>();
        dead.AddRange(unburied);
        dead.AddRange(buried);

        ModkitLog.Info($"[SaveFileService] @{fs.Position}: Roster done");
        return (hired, dismissed, dead);
    }

    private static List<LeaderData> ParseLeaderList(BinaryReader reader, int version, string listName = "")
    {
        var fs = reader.BaseStream;
        var leaders = new List<LeaderData>();
        int count = reader.ReadInt32();
        ModkitLog.Info($"[SaveFileService] @{fs.Position}: {listName}Leaders count: {count}");

        // Sanity check
        if (count < 0 || count > 100)
        {
            ModkitLog.Warn($"[SaveFileService] {listName}Leaders count {count} seems wrong, treating as 0");
            fs.Position -= 4; // Seek back
            return leaders;
        }

        for (int i = 0; i < count; i++)
        {
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: Parsing {listName}Leader[{i}]...");

            // In version 101+, ActorType is stored as a string (e.g., "Infantry", "Vehicle")
            // instead of int32, and there's an extra byte field after it
            string actorTypeStr = reader.ReadString();
            int actorType = actorTypeStr switch
            {
                "Infantry" => 0,
                "Vehicle" => 1,
                _ => 0
            };

            // There's an extra byte between ActorType and TemplateName in newer versions
            byte extraField = reader.ReadByte();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: {listName}Leader[{i}] extraField={extraField}");

            var leader = new LeaderData
            {
                ActorType = actorType,
                TemplateName = reader.ReadString()
            };
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: {listName}Leader[{i}]: ActorType='{actorTypeStr}' ({actorType}), Template='{leader.TemplateName}'");
            leaders.Add(leader);

            // Skip BaseUnitLeader.ProcessSaveState - this is complex
            SkipBaseUnitLeader(reader, version);
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: {listName}Leader[{i}] done");
        }

        return leaders;
    }

    private static void SkipBaseUnitLeader(BinaryReader reader, int version)
    {
        // UnitLeaderAttributes.Values (float array for version >= 26, int array / 1000 for older)
        int attrCount = reader.ReadInt32();
        for (int i = 0; i < attrCount; i++)
        {
            if (version >= 26)
                reader.ReadSingle();
            else
                reader.ReadInt32(); // old format: int / 1000
        }

        // m_Perks (template list)
        SkipTemplateList(reader);

        // ItemContainer
        SkipItemContainer(reader);

        // UnitStatistics
        SkipUnitStatistics(reader);

        // EmotionalStates
        SkipEmotionalStates(reader, version);

        // StrategicDuration
        reader.ReadInt32(); // m_RemainingMissions
        reader.ReadInt32(); // m_TotalMissions

        // m_ActiveConversation (template)
        reader.ReadString();

        // m_HealthStatus (LeaderHealthStatus, read as int32 enum)
        reader.ReadInt32();

        // m_SortedSkillIndices
        ReadIntArray(reader);
    }

    private static void SkipItemContainer(BinaryReader reader)
    {
        int slotCount = reader.ReadInt32();
        for (int i = 0; i < slotCount; i++)
        {
            reader.ReadInt32(); // slotIndex
            int itemCount = reader.ReadInt32();
            for (int j = 0; j < itemCount; j++)
                reader.ReadString(); // item GUID
        }
    }

    private static void SkipUnitStatistics(BinaryReader reader)
    {
        ReadIntArray(reader); // m_Kills
        ReadIntArray(reader); // m_Deaths
        SkipTemplateList(reader); // m_KilledEntities
        ReadIntArray(reader); // m_MissionStats
        ReadIntArray(reader); // m_DamageDealt
        SkipTemplateList(reader); // m_KilledLeaders
        SkipTemplateList(reader); // m_AssistedKills
        reader.ReadString(); // m_Nemesis template

        // m_FoeEncounters dict
        int foeCount = reader.ReadInt32();
        for (int i = 0; i < foeCount; i++)
        {
            reader.ReadString(); // template
            reader.ReadInt32();  // count
        }

        // m_AbilityUsages (uint array)
        int abilityCount = reader.ReadInt32();
        for (int i = 0; i < abilityCount; i++)
            reader.ReadUInt32();
    }

    private static void SkipEmotionalStates(BinaryReader reader, int version)
    {
        // m_States → List<EmotionalState>
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            reader.ReadString();  // m_Template (EmotionalStateTemplate)
            reader.ReadInt32();   // m_Trigger (EmotionalTrigger enum, int32)
            reader.ReadString();  // m_CausedBy (UnitLeaderTemplate)
            reader.ReadInt32();   // m_DurationRemaining
            reader.ReadBoolean(); // m_IsActive
        }

        reader.ReadInt32(); // m_LastTriggeredIndex

        if (version >= 101)
        {
            reader.ReadInt32(); // m_LastSkillTriggeredIndex
        }
    }

    private static void SkipBattlePlan(BinaryReader reader)
    {
        // BattlePlan deployment data
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            reader.ReadInt32();  // entityType (should be 0 for Infantry)
            reader.ReadString(); // UnitLeaderTemplate
            reader.ReadInt32();  // gridX
            reader.ReadInt32();  // gridY
        }
    }

    private static List<PlanetData> ParsePlanetManager(BinaryReader reader)
    {
        var planets = new List<PlanetData>();

        // PseudoRandom (4 uint32s)
        reader.ReadUInt32(); // state0
        reader.ReadUInt32(); // state1
        reader.ReadUInt32(); // state2
        reader.ReadUInt32(); // state3

        // m_Planets
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var planet = new PlanetData
            {
                TemplateName = reader.ReadString(),
                Control = reader.ReadInt32(),
                ControlChange = reader.ReadInt32()
            };
            planets.Add(planet);
        }

        return planets;
    }

    private static OperationData? ParseOperationsManager(BinaryReader reader, int version)
    {
        // m_NotAvailableOperations
        SkipTemplateList(reader);

        // m_Seed
        reader.ReadInt32();

        // m_CurrentOperation (nullable)
        bool hasCurrentOp = reader.ReadBoolean();
        OperationData? currentOp = null;

        if (hasCurrentOp)
        {
            currentOp = new OperationData
            {
                TemplateName = reader.ReadString(),
                StoryFaction = reader.ReadString(),
                Faction = reader.ReadString()
            };

            // Skip OperationResult
            SkipOperationResult(reader);

            currentOp.Planet = reader.ReadString();
            reader.ReadString(); // duration template
            currentOp.CurrentMission = reader.ReadInt32();

            // Skip remaining Operation fields for now
            // This is getting complex, just capture the basics
        }

        return currentOp;
    }

    private static void SkipOperationResult(BinaryReader reader)
    {
        // OperationResult structure - basic fields
        reader.ReadInt32(); // result type
        reader.ReadInt32(); // score or similar
    }

    #endregion

    /// <summary>
    /// Disables ironman mode in a save file by setting the flag to false.
    /// This is a surgical edit at a known offset.
    /// </summary>
    public bool DisableIronman(SaveFileHeader header)
    {
        if (!header.IsValid || header.BodyOffset <= 0)
            return false;

        try
        {
            // Ironman flag is at: BodyOffset + 8 (after double m_TotalPlayTimeInSec)
            var ironmanOffset = header.BodyOffset + 8;

            using var fs = new FileStream(header.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            fs.Seek(ironmanOffset, SeekOrigin.Begin);

            // Read current value
            var currentValue = fs.ReadByte();
            if (currentValue == 0)
            {
                // Already disabled
                return true;
            }

            // Write false (0)
            fs.Seek(ironmanOffset, SeekOrigin.Begin);
            fs.WriteByte(0);

            // Update the cached body data
            if (header.BodyData != null)
                header.BodyData.Ironman = false;

            ModkitLog.Info($"[SaveFileService] Disabled ironman mode for: {header.FilePath}");
            return true;
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[SaveFileService] Error disabling ironman: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Writes body data changes (resources, ironman flag) using surgical edits.
    /// Creates a backup file (.bak) before modifying.
    /// </summary>
    public bool WriteBodyChanges(SaveFileHeader header)
    {
        if (!header.IsValid || header.BodyData == null || !header.BodyData.IsValid)
        {
            ModkitLog.Error("[SaveFileService] Cannot write body: invalid header or body data");
            return false;
        }

        var body = header.BodyData;

        try
        {
            // Create backup
            var backupPath = header.FilePath + ".bak";
            File.Copy(header.FilePath, backupPath, overwrite: true);
            ModkitLog.Info($"[SaveFileService] Created backup: {backupPath}");

            using var fs = new FileStream(header.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using var writer = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: true);

            // Write Ironman flag
            if (body.IronmanOffset > 0)
            {
                fs.Seek(body.IronmanOffset, SeekOrigin.Begin);
                writer.Write(body.Ironman);
                ModkitLog.Info($"[SaveFileService] Wrote Ironman={body.Ironman} at offset {body.IronmanOffset}");
            }

            // Write StrategyVars (resources)
            if (body.StrategyVarsOffset > 0 && body.StrategyVars != null && body.StrategyVars.Length > 0)
            {
                fs.Seek(body.StrategyVarsOffset, SeekOrigin.Begin);
                writer.Write(body.StrategyVars.Length);
                foreach (var v in body.StrategyVars)
                    writer.Write(v);
                ModkitLog.Info($"[SaveFileService] Wrote {body.StrategyVars.Length} StrategyVars at offset {body.StrategyVarsOffset}");
            }

            // Write Planet control values
            if (body.PlanetsOffset > 0 && body.Planets.Count > 0)
            {
                fs.Seek(body.PlanetsOffset, SeekOrigin.Begin);
                using var reader = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);

                // Skip PseudoRandom (4 uint32s)
                reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadUInt32();

                // Read planet count to verify
                int planetCount = reader.ReadInt32();
                if (planetCount != body.Planets.Count)
                {
                    ModkitLog.Warn($"[SaveFileService] Planet count mismatch: file has {planetCount}, data has {body.Planets.Count}");
                }

                // For each planet, we need to skip the template string and write control values
                for (int i = 0; i < Math.Min(planetCount, body.Planets.Count); i++)
                {
                    // Skip template name (length-prefixed string)
                    var templateName = reader.ReadString();

                    // Now we're at the Control position - write it
                    long controlOffset = fs.Position;
                    fs.Seek(controlOffset, SeekOrigin.Begin);
                    writer.Write(body.Planets[i].Control);
                    writer.Write(body.Planets[i].ControlChange);

                    // Move reader past the values we wrote
                    fs.Seek(controlOffset + 8, SeekOrigin.Begin);
                }

                ModkitLog.Info($"[SaveFileService] Wrote control values for {Math.Min(planetCount, body.Planets.Count)} planets");
            }

            ModkitLog.Info($"[SaveFileService] Saved body changes for: {header.FilePath}");
            return true;
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[SaveFileService] Error writing body: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Writes updated header to the save file, preserving the body.
    /// Creates a backup file (.bak) before modifying.
    /// </summary>
    public bool WriteHeader(SaveFileHeader header)
    {
        if (!header.IsValid || header.BodyOffset <= 0)
        {
            ModkitLog.Error("[SaveFileService] Cannot write header: invalid header or no body offset");
            return false;
        }

        try
        {
            // Read the original file
            var originalBytes = File.ReadAllBytes(header.FilePath);

            // Create backup
            var backupPath = header.FilePath + ".bak";
            File.Copy(header.FilePath, backupPath, overwrite: true);
            ModkitLog.Info($"[SaveFileService] Created backup: {backupPath}");

            // Read the body portion
            var bodyBytes = new byte[originalBytes.Length - header.BodyOffset];
            Array.Copy(originalBytes, header.BodyOffset, bodyBytes, 0, bodyBytes.Length);

            // Write the new file
            using var fs = new FileStream(header.FilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: true);

            // Version (int32)
            writer.Write(header.Version);

            // SaveStateType - ALWAYS int32 in all versions
            writer.Write((int)header.SaveStateType);

            // DateTime (int64 ticks)
            writer.Write(header.SaveTime.Ticks);

            // PlanetName
            writer.Write(header.PlanetName ?? "");

            // OperationName
            writer.Write(header.OperationName ?? "");

            // CompletedMissions
            writer.Write(header.CompletedMissions);

            // OperationLength
            writer.Write(header.OperationLength);

            // Difficulty
            writer.Write(header.Difficulty ?? "");

            // StrategyConfigName (only if version > 27)
            if (header.Version > 27)
            {
                writer.Write(header.StrategyConfigName ?? "");
            }

            // PlayTimeSeconds
            writer.Write(header.PlayTimeSeconds);

            // SaveGameName
            writer.Write(header.SaveGameName ?? "");

            // Update body offset to new position
            header.BodyOffset = fs.Position;

            // Write body
            writer.Write(bodyBytes);

            ModkitLog.Info($"[SaveFileService] Saved header for: {header.FilePath}");
            return true;
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[SaveFileService] Error writing header: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Deletes a save file and its associated screenshot.
    /// </summary>
    public bool DeleteSaveFile(string filePath)
    {
        try
        {
            // Delete the save file
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                ModkitLog.Info($"[SaveFileService] Deleted save file: {filePath}");
            }

            // Delete the associated screenshot if it exists (.jpg or .png)
            foreach (var ext in new[] { ".jpg", ".png" })
            {
                var screenshotPath = Path.ChangeExtension(filePath, ext);
                if (File.Exists(screenshotPath))
                {
                    File.Delete(screenshotPath);
                    ModkitLog.Info($"[SaveFileService] Deleted screenshot: {screenshotPath}");
                }
            }

            // Delete any backup file
            var backupPath = filePath + ".bak";
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
                ModkitLog.Info($"[SaveFileService] Deleted backup: {backupPath}");
            }

            // Delete modmeta sidecar file if it exists
            var modmetaPath = filePath + ".modmeta";
            if (File.Exists(modmetaPath))
            {
                File.Delete(modmetaPath);
                ModkitLog.Info($"[SaveFileService] Deleted modmeta: {modmetaPath}");
            }

            return true;
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[SaveFileService] Error deleting save file: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Duplicates a save file with a new name.
    /// </summary>
    public SaveFileHeader? DuplicateSaveFile(SaveFileHeader source, string newName)
    {
        if (!source.IsValid)
        {
            ModkitLog.Error("[SaveFileService] Cannot duplicate invalid save file");
            return null;
        }

        try
        {
            var saveFolderPath = GetSaveFolderPath();
            if (saveFolderPath == null)
                return null;

            // Generate new file path
            var newFileName = SanitizeFileName(newName) + ".save";
            var newFilePath = Path.Combine(saveFolderPath, newFileName);

            // Ensure unique filename
            var counter = 1;
            while (File.Exists(newFilePath))
            {
                newFileName = $"{SanitizeFileName(newName)}_{counter}.save";
                newFilePath = Path.Combine(saveFolderPath, newFileName);
                counter++;
            }

            // Copy the save file
            File.Copy(source.FilePath, newFilePath);

            // Copy the screenshot if it exists (preserving the original extension)
            var sourceScreenshot = source.ScreenshotPath;
            if (sourceScreenshot != null && File.Exists(sourceScreenshot))
            {
                var screenshotExt = Path.GetExtension(sourceScreenshot);
                var newScreenshotPath = Path.ChangeExtension(newFilePath, screenshotExt);
                File.Copy(sourceScreenshot, newScreenshotPath);
            }

            // Parse the new file and update the save name
            var newHeader = ParseHeader(newFilePath);
            if (newHeader.IsValid)
            {
                newHeader.SaveGameName = newName;
                newHeader.SaveTime = DateTime.Now;
                WriteHeader(newHeader);
            }

            ModkitLog.Info($"[SaveFileService] Duplicated save to: {newFilePath}");
            return newHeader;
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[SaveFileService] Error duplicating save: {ex.Message}");
            return null;
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new System.Text.StringBuilder();
        foreach (var c in name)
        {
            if (Array.IndexOf(invalid, c) < 0)
                sanitized.Append(c);
            else
                sanitized.Append('_');
        }
        return sanitized.ToString();
    }

    /// <summary>
    /// Opens the save folder in the system file explorer.
    /// </summary>
    public void OpenSaveFolder()
    {
        var saveFolderPath = GetSaveFolderPath();
        if (saveFolderPath == null || !Directory.Exists(saveFolderPath))
        {
            ModkitLog.Warn("[SaveFileService] Save folder does not exist");
            return;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = saveFolderPath,
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                System.Diagnostics.Process.Start("open", saveFolderPath);
            }
            else // Linux
            {
                System.Diagnostics.Process.Start("xdg-open", saveFolderPath);
            }
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[SaveFileService] Error opening save folder: {ex.Message}");
        }
    }
}
