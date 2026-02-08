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

        // Check Windows Documents folder as fallback
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
            checkedPaths += "\n- Proton/Wine prefix paths";
        }
        checkedPaths += $"\n- Documents folder";

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

            // Search all app IDs for matching saves
            foreach (var appIdDir in Directory.GetDirectories(compatdataDir))
            {
                // Try both game names
                foreach (var gameName in new[] { gameFolderName, "Menace", "Menace Demo" })
                {
                    var savePath = Path.Combine(appIdDir, "pfx", "drive_c", "users", "steamuser", "Documents", gameName, "Saves");
                    if (Directory.Exists(savePath))
                    {
                        return savePath;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[SaveFileService] Error searching Proton paths: {ex.Message}");
            return null;
        }
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
        catch (Exception ex)
        {
            header.IsValid = false;
            header.ErrorMessage = $"Failed to parse header: {ex.Message}";
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
            body.ErrorMessage = $"Body editing not supported for save version {header.Version} (demo/old format). Minimum supported version is {MINIMUM_BODY_VERSION}.";
            ModkitLog.Info($"[SaveFileService] Skipping body parsing for version {header.Version} (< {MINIMUM_BODY_VERSION})");
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
                body.ErrorMessage = $"Body offset ({header.BodyOffset}) is beyond file length ({fileLength})";
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
                body.ErrorMessage = $"Not enough data for body parsing ({remainingBytes} bytes remaining)";
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

            body.GlobalDifficulty = reader.ReadString();
            ModkitLog.Info($"[SaveFileService] @{fs.Position}: GlobalDifficulty: '{body.GlobalDifficulty}' (len={body.GlobalDifficulty.Length})");

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
                SkipShipUpgrades(reader);

                // OwnedItems - skip
                SkipOwnedItems(reader, header.Version);

                // BlackMarket - skip
                SkipBlackMarket(reader);

                // StoryFactions - skip
                SkipStoryFactions(reader);

                // Squaddies
                body.Squaddies = ParseSquaddies(reader);

                // Roster (leaders)
                var roster = ParseRoster(reader, header.Version);
                body.HiredLeaders = roster.hired;
                body.DismissedLeaders = roster.dismissed;
                body.DeadLeaders = roster.dead;

                // BattlePlan - skip
                SkipBattlePlan(reader);

                // PlanetManager - track offset for surgical edits
                body.PlanetsOffset = fs.Position;
                body.Planets = ParsePlanetManager(reader);

                // OperationsManager
                body.CurrentOperation = ParseOperationsManager(reader, header.Version);

                body.BodyEndOffset = fs.Position;
                ModkitLog.Info($"[SaveFileService] Parsed extended body data successfully, read {body.BodyEndOffset - header.BodyOffset} bytes");
            }
            catch (Exception extEx)
            {
                // Extended parsing failed, but we still have the essential data
                ModkitLog.Warn($"[SaveFileService] Extended body parsing failed (squaddies/leaders/planets): {extEx.Message}");
                // Keep IsValid = true since we have resources
            }
        }
        catch (Exception ex)
        {
            body.IsValid = false;
            body.ErrorMessage = $"Failed to parse body: {ex.Message}";
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

    private static void SkipTemplateList(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
            reader.ReadString(); // template ID
    }

    private static void SkipShipUpgrades(BinaryReader reader)
    {
        // m_SlotOverrides (template array)
        SkipTemplateList(reader);
        // m_PermanentUpgrades (template list)
        SkipTemplateList(reader);
        // m_SlotLevels (int array)
        ReadIntArray(reader);
        // m_UpgradeAmounts (dict)
        int dictCount = reader.ReadInt32();
        for (int i = 0; i < dictCount; i++)
        {
            reader.ReadString(); // template
            reader.ReadInt32();  // amount
        }
    }

    private static void SkipOwnedItems(BinaryReader reader, int version)
    {
        // m_PurchasedDossiers (if version > 26)
        if (version > 26)
            ReadIntArray(reader);

        // Vehicles
        int vehicleCount = reader.ReadInt32();
        for (int i = 0; i < vehicleCount; i++)
        {
            reader.ReadString(); // EntityTemplate
            reader.ReadString(); // GUID
            SkipVehicle(reader, version);
        }

        // Items (template -> list of GUIDs)
        int templateCount = reader.ReadInt32();
        for (int i = 0; i < templateCount; i++)
        {
            reader.ReadString(); // BaseItemTemplate
            int itemCount = reader.ReadInt32();
            for (int j = 0; j < itemCount; j++)
                reader.ReadString(); // item GUID
        }

        // m_SeenItems
        SkipTemplateList(reader);
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
        var squaddies = new List<SquaddieData>();

        int nextId = reader.ReadInt32(); // m_NextSquaddieID

        // m_AllSquaddies
        int allCount = reader.ReadInt32();
        for (int i = 0; i < allCount; i++)
        {
            var squaddie = new SquaddieData
            {
                ID = reader.ReadInt32(),
                Gender = reader.ReadByte(),      // byte enum
                SkinColor = reader.ReadByte(),   // byte enum
                HomePlanetType = reader.ReadInt32(),
                FirstName = reader.ReadString(),
                LastName = reader.ReadString(),
                PortraitIndex = reader.ReadInt32(),
                TemplateName = reader.ReadString()
            };
            squaddies.Add(squaddie);
        }

        // m_DeadSquaddies - same format
        int deadCount = reader.ReadInt32();
        for (int i = 0; i < deadCount; i++)
        {
            reader.ReadInt32(); // ID
            reader.ReadByte();  // Gender (byte)
            reader.ReadByte();  // SkinColor (byte)
            reader.ReadInt32(); // HomePlanetType
            reader.ReadString(); // FirstName
            reader.ReadString(); // LastName
            reader.ReadInt32(); // PortraitIndex
            reader.ReadString(); // Template
        }

        return squaddies;
    }

    private static (List<LeaderData> hired, List<LeaderData> dismissed, List<LeaderData> dead) ParseRoster(BinaryReader reader, int version)
    {
        var hired = ParseLeaderList(reader, version);
        var dismissed = ParseLeaderList(reader, version);
        var unburied = ParseLeaderList(reader, version); // dead unburied
        var buried = ParseLeaderList(reader, version);   // dead buried

        // m_HirableLeaders
        SkipTemplateList(reader);

        // Combine dead lists
        var dead = new List<LeaderData>();
        dead.AddRange(unburied);
        dead.AddRange(buried);

        return (hired, dismissed, dead);
    }

    private static List<LeaderData> ParseLeaderList(BinaryReader reader, int version)
    {
        var leaders = new List<LeaderData>();
        int count = reader.ReadInt32();

        for (int i = 0; i < count; i++)
        {
            var leader = new LeaderData
            {
                ActorType = reader.ReadInt32(),
                TemplateName = reader.ReadString()
            };
            leaders.Add(leader);

            // Skip BaseUnitLeader.ProcessSaveState - this is complex
            SkipBaseUnitLeader(reader, version);
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
