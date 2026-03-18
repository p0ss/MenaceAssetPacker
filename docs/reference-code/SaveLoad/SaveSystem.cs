// =============================================================================
// MENACE REFERENCE CODE - Save/Load System
// =============================================================================
// Accurate documentation of the save/load system based on binary analysis.
// The game uses a direct binary format with BinaryWriter/BinaryReader.
// No JSON, no compression, no magic numbers.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;

namespace Menace.Strategy
{
    // =========================================================================
    // SAVE TYPES
    // =========================================================================

    /// <summary>
    /// Enumeration of save types used by the save system.
    /// </summary>
    public enum SaveType
    {
        /// <summary>Manual save created by the player.</summary>
        Manual = 1,

        /// <summary>Quick save (default name: StringLiteral_14696).</summary>
        Quick = 2,

        /// <summary>Auto save with timestamp (prefix: StringLiteral_13404).</summary>
        Auto = 3,

        /// <summary>Iron Man save (prefix: StringLiteral_5982).</summary>
        IronMan = 4
    }

    // =========================================================================
    // SAVE SYSTEM
    // =========================================================================

    /// <summary>
    /// Main entry point for save/load operations.
    /// Located in Menace.Strategy namespace.
    ///
    /// Save files are stored in:
    ///   Windows: %USERPROFILE%/AppData/LocalLow/[Publisher]/[GameName]/Saves/
    ///   Linux: ~/.config/unity3d/[Publisher]/[GameName]/Saves/
    ///
    /// File extension: .save (StringLiteral_5788)
    /// Screenshot extension: .jpg (StringLiteral_5732)
    /// </summary>
    public static class SaveSystem
    {
        // =====================================================================
        // STATIC FIELDS
        // =====================================================================

        // TypeInfo offset +0xb8: char pathSeparator (platform-specific separator)

        // =====================================================================
        // PATH FUNCTIONS
        // =====================================================================

        /// <summary>
        /// Gets and creates the save file folder path.
        /// Calls GetAndCreateUserDataFolderPath with "Saves" subfolder.
        ///
        /// Address: 0x1805a8280
        /// </summary>
        /// <returns>Full path to the Saves folder</returns>
        public static string GetAndCreateSaveFileFolderPath()
        {
            // Calls: GetAndCreateUserDataFolderPath(StringLiteral_9580) // "Saves"
            return GetAndCreateUserDataFolderPath("Saves");
        }

        /// <summary>
        /// Gets and creates a user data folder path with the specified subfolder.
        /// Handles cross-platform path construction and legacy path migration on Windows.
        ///
        /// Address: 0x1805a82e0
        /// </summary>
        /// <param name="subfolder">Subfolder name (e.g., "Saves")</param>
        /// <returns>Full path to the subfolder</returns>
        public static string GetAndCreateUserDataFolderPath(string subfolder)
        {
            // Get OS version
            var osVersion = Environment.OSVersion;

            // Platform check: Windows (< 4) has special handling
            if (osVersion.Platform < PlatformID.Unix)
            {
                // Check for legacy path marker file at persistentDataPath + pathSeparator + "legacy_marker"
                string persistentPath = UnityEngine.Application.persistentDataPath;
                string legacyMarker = persistentPath + GetPathSeparator() + "legacy_marker"; // StringLiteral_17816

                if (!File.Exists(legacyMarker))
                {
                    // Check for old Documents-based path
                    string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    string oldBasePath = documentsPath + GetPathSeparator() + "MenaceData"; // StringLiteral_13543

                    if (Directory.Exists(oldBasePath))
                    {
                        // Migrate from old path to new path
                        string oldSavesPath = oldBasePath + GetPathSeparator() + "Saves";
                        if (Directory.Exists(oldSavesPath))
                        {
                            string newSavesPath = persistentPath + GetPathSeparator() + "Saves";
                            Directory.Move(oldSavesPath, newSavesPath);
                        }

                        // Migrate individual files
                        TryMoveFile(oldBasePath, persistentPath, "legacy_marker");     // StringLiteral_17816
                        TryMoveFile(oldBasePath, persistentPath, "settings.json");      // StringLiteral_8525
                        TryMoveFile(oldBasePath, persistentPath, "player_settings.json"); // StringLiteral_19509

                        Directory.Delete(oldBasePath);

                        // Log migration
                        UnityEngine.Debug.Log("Migrated save data from " + oldBasePath + " to " + persistentPath);
                    }
                }
            }

            // Construct final path: persistentDataPath + separator + subfolder
            string finalPath = UnityEngine.Application.persistentDataPath + GetPathSeparator() + subfolder;

            // Create directory if it doesn't exist
            if (!Directory.Exists(finalPath))
            {
                Directory.CreateDirectory(finalPath);
            }

            return finalPath;
        }

        /// <summary>
        /// Gets the full path for a save file given a save name.
        ///
        /// Address: 0x1805a8950
        /// </summary>
        /// <param name="saveName">Name of the save (without extension)</param>
        /// <returns>Full path including .save extension</returns>
        public static string GetSaveFilePath(string saveName)
        {
            // Get save folder path
            string folderPath = GetAndCreateUserDataFolderPath("Saves"); // StringLiteral_9580

            // Read path separator from static field
            char separator = GetPathSeparator();

            // Concatenate: folderPath + separator + saveName + ".save"
            return folderPath + separator + saveName + ".save"; // StringLiteral_5788
        }

        /// <summary>
        /// Gets the platform-specific path separator character.
        /// Stored in SaveSystem TypeInfo at offset +0xb8.
        /// </summary>
        private static char GetPathSeparator()
        {
            return Path.DirectorySeparatorChar;
        }

        // =====================================================================
        // SAVE OPERATION
        // =====================================================================

        /// <summary>
        /// Saves the current game state.
        ///
        /// Address: 0x1805a9170
        /// </summary>
        /// <param name="saveType">Type of save (Manual=1, Quick=2, Auto=3, IronMan=4)</param>
        /// <param name="customPath">Optional custom save path (can be null)</param>
        /// <param name="operationName">Name for the operation/save</param>
        public static void Save(int saveType, string customPath, string operationName)
        {
            // Get temp save path for working file
            string tempPath = GetSaveFilePath("temp"); // StringLiteral_7414

            // Get current StrategyState
            var strategyState = StrategyState.Current; // StrategyState TypeInfo +0xb8
            if (strategyState == null)
            {
                UnityEngine.Debug.LogError("Cannot save: StrategyState is null"); // StringLiteral_17193
                return;
            }

            // Check Iron Man mode (offset +0x28 of StrategyState)
            bool isIronMan = strategyState.IsIronMan;

            if (isIronMan)
            {
                // In Iron Man mode: force save type to IronMan
                if (saveType == (int)SaveType.Auto)
                {
                    saveType = (int)SaveType.IronMan;
                }
                else if (saveType != (int)SaveType.IronMan)
                {
                    // Non-auto saves not allowed in Iron Man
                    return;
                }
                // Use Iron Man save name from StrategyState +0x30
                operationName = strategyState.IronManSaveName;
            }

            // Show notification
            var uiManager = UIManager.Instance;
            string notification = Loca.TranslateNotification("Saving..."); // StringLiteral_9588
            uiManager.ShowNotification(notification);

            // Open file for writing
            using (var fileStream = File.OpenWrite(tempPath))
            {
                // Seek to beginning
                fileStream.Seek(0, SeekOrigin.Begin);

                // Create SaveState in write mode
                var saveState = new SaveState(fileStream, SaveMode.Write, strategyState, saveType, operationName);
                saveState.FilePath = tempPath;

                // Process save state via coroutine
                var coroutine = strategyState.ProcessSaveState(saveState);
                while (coroutine.MoveNext())
                {
                    // Execute coroutine steps
                }

                // Close the save state
                saveState.Close();

                // Save screenshot alongside save file
                string screenshotPath = saveState.SaveScreenshot();

                // Log if debug enabled
                if (LogLevelExtensions.LogDebug(0x17))
                {
                    UnityEngine.Debug.Log("Saved to: " + tempPath); // StringLiteral_9572
                }
            }

            // Determine final save name/path
            string finalPath;
            if (string.IsNullOrWhiteSpace(customPath))
            {
                string fileName;
                switch ((SaveType)saveType)
                {
                    case SaveType.Manual:
                        // Use operation name converted to snake_case
                        fileName = operationName.ToSnakeCaseFileName();
                        if (string.IsNullOrWhiteSpace(fileName?.Replace('_', ' ')))
                        {
                            // Fallback to timestamp
                            fileName = "Save " + TimeToString(DateTime.Now); // StringLiteral_9133
                        }
                        break;

                    case SaveType.Quick:
                        fileName = "Quick Save"; // StringLiteral_14696
                        break;

                    case SaveType.Auto:
                        fileName = "Auto Save " + TimeToString(DateTime.Now); // StringLiteral_13404
                        break;

                    case SaveType.IronMan:
                        fileName = strategyState.IronManSaveName.ToSnakeCaseFileName();
                        if (string.IsNullOrWhiteSpace(fileName?.Replace('_', ' ')))
                        {
                            // Fallback to operation index
                            fileName = "Iron Man " + strategyState.CurrentOperation.GetIndex();
                        }
                        break;

                    default:
                        fileName = customPath;
                        break;
                }

                finalPath = GetSaveFilePath(fileName);
            }
            else
            {
                finalPath = customPath;
            }

            if (string.IsNullOrWhiteSpace(finalPath))
            {
                return;
            }

            // Handle auto-save rotation
            if (saveType == (int)SaveType.Auto)
            {
                var sortedSaves = GetSortedSaveStates();
                int autoSaveCount = 0;
                int maxAutoSaves = PlayerSettings.GetInt(PlayerSettingType.MaxAutoSaves); // ID: 10

                foreach (var existingSave in sortedSaves)
                {
                    if (existingSave.SaveType == (int)SaveType.Auto)
                    {
                        autoSaveCount++;
                        if (autoSaveCount >= maxAutoSaves)
                        {
                            // Delete oldest auto-saves
                            Delete(existingSave.FilePath);
                        }
                    }
                }
            }

            // Copy temp file to final location
            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }
            File.Copy(tempPath, finalPath);

            // Copy screenshot if exists
            string screenshotSource = tempPath.Replace(".save", ".jpg"); // StringLiteral_5788 -> StringLiteral_5732
            string screenshotDest = finalPath.Replace(".save", ".jpg");

            if (File.Exists(screenshotDest))
            {
                File.Delete(screenshotDest);
            }
            if (File.Exists(screenshotSource))
            {
                File.Copy(screenshotSource, screenshotDest);
            }

            if (LogLevelExtensions.LogDebug(0x17))
            {
                UnityEngine.Debug.Log("Saved to: " + finalPath);
            }
        }

        // =====================================================================
        // LOAD OPERATION
        // =====================================================================

        /// <summary>
        /// Initiates loading a save file. Shows confirmation dialog if not in Iron Man mode.
        ///
        /// Address: 0x1805a8fa0
        /// </summary>
        /// <param name="saveState">SaveState object with the save to load</param>
        public static void Load(SaveState saveState)
        {
            // Check if currently in Iron Man mode
            var currentStrategy = StrategyState.Current;

            if (currentStrategy == null || currentStrategy.IsIronMan)
            {
                // No confirmation needed - load directly
                ExecLoad(saveState);
                return;
            }

            // Show confirmation dialog
            var dialog = UIManager.Instance.AddSimpleDialog();
            dialog.InitYesNo(
                "Confirm Load",           // StringLiteral_8381
                "Load this save?",         // StringLiteral_9848
                "Unsaved progress will be lost.", // StringLiteral_11246
                () => ExecLoad(saveState), // On Yes
                null,                      // On No
                true                       // Default to Yes
            );
        }

        /// <summary>
        /// Executes the actual load operation.
        ///
        /// Address: 0x1805a8080
        /// </summary>
        /// <param name="saveState">SaveState object with the save to load</param>
        public static void ExecLoad(SaveState saveState)
        {
            string filePath = saveState?.FilePath;

            // Check file exists
            if (!File.Exists(filePath))
            {
                UnityEngine.Debug.LogError("Save file not found: " + filePath); // StringLiteral_16841
                return;
            }

            // Close all UI screens
            UIManager.Instance.CloseAllScreens(true);

            // Show loading overlay
            UIManager.Instance.ShowLoadingOverlay(0x11);

            // Start loading coroutine
            StrategyScheduler.Execute(new LoadSaveGameCoroutine(filePath));
        }

        // =====================================================================
        // LOAD COROUTINE
        // =====================================================================

        /// <summary>
        /// Coroutine that handles async save game loading.
        ///
        /// Address: 0x1805aeca0 (MoveNext)
        /// </summary>
        private class LoadSaveGameCoroutine : IEnumerator
        {
            private int _state;
            private string _filePath;
            private SaveState _saveState;
            private StrategyState _strategyState;
            private DisplayClass _displayClass;

            public LoadSaveGameCoroutine(string filePath)
            {
                _filePath = filePath;
                _state = 0;
            }

            public bool MoveNext()
            {
                switch (_state)
                {
                    case 0:
                        // Step 0: Initialize, increment loading progress
                        _state = 1;
                        _displayClass = new DisplayClass();
                        UIManager.IncrementLoadingProgress();
                        return true;

                    case 1:
                        // Step 1: Load file, clear existing states
                        _state = 2;

                        // Create memory stream and copy file content
                        using (var memoryStream = new MemoryStream())
                        {
                            using (var fileStream = File.OpenRead(_filePath))
                            {
                                fileStream.CopyTo(memoryStream);
                            }
                            memoryStream.Seek(0, SeekOrigin.Begin);

                            // Remove TacticalState if in mission
                            if (TacticalState.Current != null && TacticalManager.Instance != null)
                            {
                                TacticalManager.Instance.Finish(3);
                                StateManager.Instance.RemoveState<TacticalState>();
                            }

                            // Remove existing StrategyState
                            if (StrategyState.Current != null)
                            {
                                StateManager.Instance.RemoveState<StrategyState>();
                            }

                            // Create SaveState in read mode
                            _saveState = new SaveState(memoryStream, SaveMode.Read, null, 0, "");
                            _saveState.FilePath = _filePath;

                            // Load strategy config based on save's config ID
                            if (!string.IsNullOrWhiteSpace(_saveState.StrategyConfigId))
                            {
                                var config = Resources.Load<StrategyConfig>("Configs/" + _saveState.StrategyConfigId);
                                StrategyConfig.InitCurrent(config);
                            }
                            else
                            {
                                StrategyConfig.InitCurrent(null);
                            }

                            // Create new StrategyState
                            StateManager.Instance.TryAddState<StrategyState>();
                            _strategyState = StrategyState.Current;
                        }

                        UIManager.IncrementLoadingProgress();
                        return true;

                    case 2:
                        // Step 2: Load screenshot, set Iron Man mode
                        _saveState.LoadScreenshot();

                        if (_saveState.SaveType == (int)SaveType.IronMan)
                        {
                            _strategyState.SetIronMan(true, _saveState.CustomSaveName);
                        }
                        else
                        {
                            _strategyState.SetIronMan(false, "");
                        }

                        // Ensure strategy scene is loaded
                        _displayClass.SceneLoaded = false;
                        SceneManagerHelper.EnsureSceneIsLoaded("StrategyScene", // StringLiteral_14607
                            (scene, mode) => { _displayClass.SceneLoaded = true; });

                        _state = 3;
                        return true;

                    case 3:
                        // Step 3: Wait for scene load
                        if (!_displayClass.SceneLoaded)
                        {
                            // Wait for end of frame
                            return true;
                        }

                        // Start ProcessSaveState coroutine
                        _state = 4;
                        return true;

                    case 4:
                        // Step 4: Process save state
                        var processCoroutine = _strategyState.ProcessSaveState(_saveState);
                        while (processCoroutine.MoveNext())
                        {
                            // Could yield here for incremental loading
                        }
                        return false;

                    default:
                        return false;
                }
            }

            public object Current => null;
            public void Reset() { }
        }

        // =====================================================================
        // DELETE OPERATION
        // =====================================================================

        /// <summary>
        /// Deletes a save file and its associated screenshot.
        ///
        /// Address: 0x1805a7f10
        /// </summary>
        /// <param name="filePath">Path to the save file</param>
        public static void Delete(string filePath)
        {
            if (filePath == null)
            {
                return;
            }

            // Delete screenshot if exists
            string screenshotPath = filePath.Replace(".save", ".jpg");
            if (File.Exists(screenshotPath))
            {
                File.Delete(screenshotPath);
            }

            // Delete save file
            if (!File.Exists(filePath))
            {
                UnityEngine.Debug.LogError("Save file not found: " + filePath); // StringLiteral_16505
            }
            else
            {
                File.Delete(filePath);
            }
        }

        // =====================================================================
        // UTILITY FUNCTIONS
        // =====================================================================

        /// <summary>
        /// Gets all save states sorted by timestamp (newest first).
        ///
        /// Address: 0x1805a8b90
        /// </summary>
        /// <returns>List of SaveState objects sorted by timestamp</returns>
        public static List<SaveState> GetSortedSaveStates()
        {
            var saves = new List<SaveState>();

            // Ensure persistent data path exists
            string persistentPath = UnityEngine.Application.persistentDataPath;
            if (!Directory.Exists(persistentPath))
            {
                Directory.CreateDirectory(persistentPath);
            }

            // Get save folder path
            string saveFolder = GetAndCreateUserDataFolderPath("Saves");
            if (!Directory.Exists(saveFolder))
            {
                Directory.CreateDirectory(saveFolder);
                return saves; // Empty list for new folder
            }

            // Get all files in save folder
            string[] files = Directory.GetFiles(saveFolder, "*.*"); // StringLiteral_3238

            foreach (string file in files)
            {
                // Skip screenshot files
                if (file.EndsWith(".jpg")) // StringLiteral_7422
                {
                    continue;
                }

                // Try to read save state header
                if (TryGetSaveState(file, out SaveState saveState))
                {
                    saves.Add(saveState);
                }
            }

            // Sort by timestamp descending (newest first)
            saves.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));

            return saves;
        }

        /// <summary>
        /// Attempts to read a save state header from a file.
        ///
        /// Address: 0x1805aa100
        /// </summary>
        /// <param name="filePath">Path to the save file</param>
        /// <param name="saveState">Output save state (header only)</param>
        /// <returns>True if save state was read successfully</returns>
        public static bool TryGetSaveState(string filePath, out SaveState saveState)
        {
            using (var stream = File.OpenRead(filePath))
            {
                saveState = new SaveState(stream, SaveMode.Read, null, 0, "");
                saveState.FilePath = filePath;

                // Load screenshot thumbnail
                saveState.LoadScreenshot();

                // Close the file
                saveState.Close();

                // Log version info if debug enabled
                if (LogLevelExtensions.LogInfo(0x17))
                {
                    if (!saveState.IsValid && saveState.Version < MinVersion)
                    {
                        UnityEngine.Debug.Log($"Save {filePath} has old version {saveState.Version}, minimum is {MinVersion}");
                    }
                }

                return saveState.IsValid;
            }
        }

        /// <summary>
        /// Converts DateTime to string format: "YYYY.MM.DD HH:MM:SS"
        ///
        /// Address: 0x1805a9c90
        /// </summary>
        /// <param name="dateTime">DateTime to convert</param>
        /// <returns>Formatted string</returns>
        public static string TimeToString(DateTime dateTime)
        {
            // Format: "{0:0000}.{1:00}.{2:00} {3:00}:{4:00}:{5:00}" (StringLiteral_8097)
            return string.Format("{0:0000}.{1:00}.{2:00} {3:00}:{4:00}:{5:00}",
                dateTime.Year,
                dateTime.Month,
                dateTime.Day,
                dateTime.Hour,
                dateTime.Minute,
                dateTime.Second);
        }

        /// <summary>
        /// Tries to move a file from old path to new path.
        /// Used during legacy path migration.
        /// </summary>
        private static void TryMoveFile(string oldFolder, string newFolder, string fileName)
        {
            string oldPath = oldFolder + GetPathSeparator() + fileName;
            string newPath = newFolder + GetPathSeparator() + fileName;

            if (File.Exists(oldPath))
            {
                File.Move(oldPath, newPath);
            }
        }

        // =====================================================================
        // CONSTANTS
        // =====================================================================

        /// <summary>Minimum supported save version (0x16 = 22)</summary>
        public const int MinVersion = 0x16;

        /// <summary>Maximum supported save version (0x65 = 101)</summary>
        public const int MaxVersion = 0x65;

        /// <summary>Current save version (0x65 = 101)</summary>
        public const int CurrentVersion = 0x65;
    }

    // =========================================================================
    // SAVE MODE ENUM
    // =========================================================================

    /// <summary>
    /// Mode for SaveState operations.
    /// </summary>
    public enum SaveMode
    {
        /// <summary>Reading from a save file.</summary>
        Read = 0,

        /// <summary>Writing to a save file.</summary>
        Write = 1
    }

    // =========================================================================
    // SAVE STATE CLASS
    // =========================================================================

    /// <summary>
    /// Represents a save state, handling both reading and writing of save data.
    /// Uses BinaryReader/BinaryWriter directly - no JSON, no compression.
    ///
    /// Constructor Address: 0x1805a77e0
    /// </summary>
    public class SaveState
    {
        // =====================================================================
        // FIELD LAYOUT (based on decompiled code)
        // =====================================================================
        //
        // Offset | Type          | Field Description
        // -------|---------------|------------------------------------------
        // +0x10  | Stream        | File/Memory stream reference
        // +0x18  | int           | Mode (0=Read, 1=Write)
        // +0x1c  | bool          | IsValid flag
        // +0x20  | int           | Version number
        // +0x24  | int           | Save type (1=Manual, 2=Quick, 3=Auto, 4=IronMan)
        // +0x28  | DateTime      | Timestamp (8 bytes)
        // +0x30  | string        | Planet name
        // +0x38  | string        | Operation name
        // +0x40  | int           | Completed missions count
        // +0x44  | int           | Total missions count
        // +0x48  | string        | Campaign template ID
        // +0x50  | double        | Play time (seconds)
        // +0x58  | string        | Custom save name
        // +0x60  | string        | Strategy config ID
        // +0x68  | Texture2D     | Screenshot/thumbnail texture
        // +0x70  | string        | File path
        // +0x78  | BinaryWriter  | Writer reference (null when reading)
        // +0x80  | BinaryReader  | Reader reference (null when writing)

        // =====================================================================
        // PROPERTIES
        // =====================================================================

        /// <summary>Stream for reading/writing. Offset: +0x10</summary>
        public Stream Stream { get; private set; }

        /// <summary>Mode (0=Read, 1=Write). Offset: +0x18</summary>
        public int Mode { get; private set; }

        /// <summary>Whether the save state is valid. Offset: +0x1c</summary>
        public bool IsValid { get; private set; }

        /// <summary>Save format version. Offset: +0x20</summary>
        public int Version { get; private set; }

        /// <summary>Save type (1=Manual, 2=Quick, 3=Auto, 4=IronMan). Offset: +0x24</summary>
        public int SaveType { get; private set; }

        /// <summary>When save was created. Offset: +0x28</summary>
        public DateTime Timestamp { get; private set; }

        /// <summary>Current planet name. Offset: +0x30</summary>
        public string PlanetName { get; private set; }

        /// <summary>Current operation name. Offset: +0x38</summary>
        public string OperationName { get; private set; }

        /// <summary>Number of completed missions. Offset: +0x40</summary>
        public int CompletedMissions { get; private set; }

        /// <summary>Total number of missions. Offset: +0x44</summary>
        public int TotalMissions { get; private set; }

        /// <summary>Campaign template ID. Offset: +0x48</summary>
        public string CampaignTemplateId { get; private set; }

        /// <summary>Total play time in seconds. Offset: +0x50</summary>
        public double PlayTime { get; private set; }

        /// <summary>Custom save name (user-defined). Offset: +0x58</summary>
        public string CustomSaveName { get; private set; }

        /// <summary>Strategy config ID. Offset: +0x60</summary>
        public string StrategyConfigId { get; private set; }

        /// <summary>Screenshot texture. Offset: +0x68</summary>
        public UnityEngine.Texture2D Screenshot { get; private set; }

        /// <summary>Full file path. Offset: +0x70</summary>
        public string FilePath { get; set; }

        /// <summary>Binary writer (null when reading). Offset: +0x78</summary>
        private BinaryWriter _writer;

        /// <summary>Binary reader (null when writing). Offset: +0x80</summary>
        private BinaryReader _reader;

        // =====================================================================
        // CONSTRUCTOR
        // =====================================================================

        /// <summary>
        /// Creates a SaveState for reading or writing.
        ///
        /// Address: 0x1805a77e0
        /// </summary>
        /// <param name="stream">Stream to read from or write to</param>
        /// <param name="mode">Read (0) or Write (1)</param>
        /// <param name="strategyState">Strategy state for writing (can be null for reading)</param>
        /// <param name="saveType">Type of save (1-4)</param>
        /// <param name="customName">Custom save name</param>
        public SaveState(Stream stream, SaveMode mode, StrategyState strategyState, int saveType, string customName)
        {
            Stream = stream;
            Mode = (int)mode;

            if (mode == SaveMode.Write)
            {
                // Writing mode
                if (strategyState == null)
                {
                    throw new ArgumentNullException("strategyState", "StrategyState cannot be null when writing");
                }

                _writer = new BinaryWriter(stream);

                // Write header
                Version = SaveSystem.CurrentVersion;
                _writer.Write(Version); // 0x65 = 101

                SaveType = saveType;
                _writer.Write(SaveType);

                Timestamp = DateTime.Now;
                _writer.Write(Timestamp.Ticks);

                // Get current operation info
                var currentOp = strategyState.GetCurrentOperation();

                // Planet name
                if (currentOp != null)
                {
                    var planet = currentOp.GetPlanet();
                    PlanetName = planet?.Template?.Name ?? "";
                }
                else
                {
                    PlanetName = ""; // StringLiteral_13683 (empty string)
                }
                _writer.Write(PlanetName);

                // Operation name
                if (currentOp != null)
                {
                    OperationName = currentOp.Template?.Name ?? "";
                }
                else
                {
                    OperationName = "";
                }
                _writer.Write(OperationName);

                // Completed missions count
                if (currentOp != null)
                {
                    CompletedMissions = currentOp.Result?.GetCompletedMissions() ?? 0;
                }
                else
                {
                    CompletedMissions = 0;
                }
                _writer.Write(CompletedMissions);

                // Total missions count
                if (currentOp != null)
                {
                    TotalMissions = currentOp.MissionList?.Missions?.Count ?? 0;
                }
                else
                {
                    TotalMissions = 0;
                }
                _writer.Write(TotalMissions);

                // Store operation reference for screenshot
                Screenshot = currentOp?.ScreenshotTexture;

                // Campaign template ID
                if (strategyState.DifficultyTemplate != null)
                {
                    CampaignTemplateId = strategyState.DifficultyTemplate.GetID();
                }
                else
                {
                    CampaignTemplateId = "";
                }
                _writer.Write(CampaignTemplateId);

                // Strategy config ID (version > 0x1b check, but we're writing current version)
                if (StrategyConfig.Current != null)
                {
                    StrategyConfigId = StrategyConfig.Current.GetID();
                }
                else
                {
                    StrategyConfigId = "";
                }
                _writer.Write(StrategyConfigId);

                // Play time
                PlayTime = strategyState.PlayTime;
                _writer.Write(PlayTime);

                // Custom save name
                if (string.IsNullOrWhiteSpace(customName))
                {
                    customName = "";
                }
                CustomSaveName = customName;
                _writer.Write(CustomSaveName);

                IsValid = true;
            }
            else
            {
                // Reading mode
                _reader = new BinaryReader(stream);

                // Read version
                Version = _reader.ReadInt32();

                // Validate version range [0x16, 0x65] = [22, 101]
                if (Version < SaveSystem.MinVersion || Version > SaveSystem.MaxVersion)
                {
                    IsValid = false;
                    return;
                }

                // Read save type
                SaveType = _reader.ReadInt32();

                // Read timestamp (as ticks)
                long ticks = _reader.ReadInt64();
                Timestamp = new DateTime(ticks);

                // Read planet name
                PlanetName = _reader.ReadString();

                // Read operation name
                OperationName = _reader.ReadString();

                // Read mission counts
                CompletedMissions = _reader.ReadInt32();
                TotalMissions = _reader.ReadInt32();

                // Read campaign template ID
                CampaignTemplateId = _reader.ReadString();

                // Read strategy config ID (only if version > 0x1b = 27)
                if (Version > 0x1b)
                {
                    StrategyConfigId = _reader.ReadString();
                }

                // Read play time
                PlayTime = _reader.ReadDouble();

                // Read custom save name
                CustomSaveName = _reader.ReadString();

                IsValid = true;
            }
        }

        // =====================================================================
        // METHODS
        // =====================================================================

        /// <summary>
        /// Returns true if this SaveState is in loading (read) mode.
        ///
        /// Address: 0x18055b340
        /// </summary>
        public bool IsLoading()
        {
            return Mode == 0;
        }

        /// <summary>
        /// Closes the save state, flushing and disposing writer/reader.
        ///
        /// Address: 0x1805a51e0
        /// </summary>
        public void Close()
        {
            if (_writer != null)
            {
                _writer.Flush();
                _writer.Dispose();
                _writer = null;
            }

            if (_reader != null)
            {
                _reader.Dispose();
                _reader = null;
            }

            Stream?.Dispose();
        }

        /// <summary>
        /// Checks for corruption by verifying a magic value (0x2A = 42).
        /// Writes 42 when saving, reads and verifies 42 when loading.
        ///
        /// Address: 0x1805a50d0
        /// </summary>
        public void CheckCorruption()
        {
            // Only check for version >= 7
            if (Version < 7)
            {
                return;
            }

            if (Mode == (int)SaveMode.Write)
            {
                // Write corruption check marker
                _writer.Write(0x2A); // 42
            }
            else
            {
                // Read and verify corruption check marker
                int marker = _reader.ReadInt32();
                if (marker != 0x2A)
                {
                    UnityEngine.Debug.LogError(
                        string.Format("Save corruption detected: expected {0}, got {1}",
                            0x2A, marker)); // StringLiteral_9548
                }
            }
        }

        /// <summary>
        /// Saves a screenshot alongside the save file.
        ///
        /// Address: 0x1805a7420
        /// </summary>
        /// <returns>Path to the screenshot file, or null if no screenshot</returns>
        public string SaveScreenshot()
        {
            string screenshotPath = FilePath.Replace(".save", ".jpg");

            if (Screenshot == null)
            {
                // No screenshot - delete existing if present
                if (File.Exists(screenshotPath))
                {
                    File.Delete(screenshotPath);
                }
                return null;
            }

            // Encode to JPEG with quality 90 (0x5A)
            byte[] jpegData = UnityEngine.ImageConversion.EncodeToJPG(Screenshot, 90);
            File.WriteAllBytes(screenshotPath, jpegData);

            return screenshotPath;
        }

        /// <summary>
        /// Loads the screenshot thumbnail from disk.
        ///
        /// Address: 0x1805a5310
        /// </summary>
        public void LoadScreenshot()
        {
            if (!IsValid)
            {
                return;
            }

            if (Screenshot != null)
            {
                return; // Already loaded
            }

            string screenshotPath = FilePath.Replace(".save", ".jpg");
            if (File.Exists(screenshotPath))
            {
                // Create 200x200 texture
                Screenshot = new UnityEngine.Texture2D(200, 200, UnityEngine.TextureFormat.RGB24, false);
                byte[] jpegData = File.ReadAllBytes(screenshotPath);
                UnityEngine.ImageConversion.LoadImage(Screenshot, jpegData);
            }
        }

        // =====================================================================
        // PROCESS METHODS (bidirectional read/write)
        // =====================================================================

        /// <summary>
        /// Processes an integer value (reads or writes based on mode).
        /// Address: 0x1805a6240
        /// </summary>
        public void ProcessInt(ref int value)
        {
            if (Mode == (int)SaveMode.Write)
            {
                _writer.Write(value);
            }
            else
            {
                value = _reader.ReadInt32();
            }
        }

        /// <summary>
        /// Processes a boolean value.
        /// Address: 0x1805a5460
        /// </summary>
        public void ProcessBool(ref bool value)
        {
            if (Mode == (int)SaveMode.Write)
            {
                _writer.Write(value);
            }
            else
            {
                value = _reader.ReadBoolean();
            }
        }

        /// <summary>
        /// Processes a double value.
        /// Address: 0x1805a5f30
        /// </summary>
        public void ProcessDouble(ref double value)
        {
            if (Mode == (int)SaveMode.Write)
            {
                _writer.Write(value);
            }
            else
            {
                value = _reader.ReadDouble();
            }
        }

        /// <summary>
        /// Processes a string value.
        /// Address: 0x1805a6930
        /// </summary>
        public void ProcessString(ref string value)
        {
            if (Mode == (int)SaveMode.Write)
            {
                _writer.Write(value ?? "");
            }
            else
            {
                value = _reader.ReadString();
            }
        }

        /// <summary>
        /// Processes a float value.
        /// Address: 0x1805a60c0
        /// </summary>
        public void ProcessFloat(ref float value)
        {
            if (Mode == (int)SaveMode.Write)
            {
                _writer.Write(value);
            }
            else
            {
                value = _reader.ReadSingle();
            }
        }

        /// <summary>
        /// Processes an unsigned integer value.
        /// Address: 0x1805a6c40
        /// </summary>
        public void ProcessUInt(ref uint value)
        {
            if (Mode == (int)SaveMode.Write)
            {
                _writer.Write(value);
            }
            else
            {
                value = _reader.ReadUInt32();
            }
        }

        // Direct write methods
        public void WriteInt(int value) { _writer.Write(value); }      // 0x1805a7780
        public void WriteString(string value) { _writer.Write(value ?? ""); } // 0x1805a77b0
        public void WriteBool(bool value) { _writer.Write(value); }    // 0x1805a7660
        public void WriteFloat(float value) { _writer.Write(value); }  // 0x1805a7750

        // Direct read methods
        public int ReadInt() { return _reader.ReadInt32(); }           // 0x1805a73c0
        public string ReadString() { return _reader.ReadString(); }    // 0x1805a73f0
        public bool ReadBool() { return _reader.ReadBoolean(); }       // 0x1805a72e0
        public float ReadFloat() { return _reader.ReadSingle(); }      // 0x1805a7390
    }

    // =========================================================================
    // STRATEGY STATE SAVE PROCESSING
    // =========================================================================

    /// <summary>
    /// Extension methods for StrategyState save/load processing.
    /// </summary>
    public static class StrategyStateSaveExtensions
    {
        /// <summary>
        /// Processes all save state data for StrategyState.
        /// This is a coroutine that yields to allow incremental loading UI.
        ///
        /// Address: 0x18064c130 (ProcessSaveState coroutine MoveNext)
        /// </summary>
        /// <remarks>
        /// StrategyState field offsets used by ProcessSaveState:
        ///
        /// Offset | Type                  | Description
        /// -------|-----------------------|----------------------------------
        /// +0x20  | double               | PlayTime
        /// +0x28  | bool                 | IsIronMan
        /// +0x30  | string               | IronManSaveName
        /// +0x38  | int                  | CurrentDay
        /// +0x3c  | bool                 | Unknown bool 1
        /// +0x3d  | bool                 | Unknown bool 2
        /// +0x48  | GlobalDifficultyTemplate | DifficultyTemplate
        /// +0x50  | PlanetManager        | PlanetManager
        /// +0x58  | OperationsManager    | OperationsManager
        /// +0x60  | MissionResult        | LastMissionResult
        /// +0x68  | Squaddies            | Squaddies
        /// +0x70  | Roster               | Roster
        /// +0x78  | BattlePlan           | BattlePlan
        /// +0x80  | OwnedItems           | OwnedItems
        /// +0x88  | BlackMarket          | BlackMarket
        /// +0x90  | UnknownManager       | Unknown manager
        /// +0x98  | List<OffmapAbility>  | OffmapAbilities
        /// +0xa0  | ShipUpgrades         | ShipUpgrades
        /// +0xa8  | BarksManager         | BarksManager
        /// +0xb0  | EventManager         | EventManager
        /// +0xb8  | StoryFactions        | StoryFactions
        /// +0xc0  | int[]                | IntArray (unknown purpose)
        /// +0xc8  | Dictionary           | ConversationVariables
        /// +0xd0  | List<ConvEffect>     | ConversationEffects
        ///
        /// Processing order:
        ///  1. PlayTime (+0x20)
        ///  2. IsIronMan (+0x28)
        ///  3. IronManSaveName (+0x30)
        ///  4. CurrentDay (+0x38)
        ///  5. Unknown bools (+0x3c, +0x3d)
        ///  6. DifficultyTemplate (+0x48)
        ///  7. IntArray (+0xc0)
        ///  8. [Corruption Check]
        ///  9. ShipUpgrades.ProcessSaveState (+0xa0)
        /// 10. [Corruption Check]
        /// 11. OwnedItems.ProcessSaveState (+0x80)
        /// 12. BlackMarket.ProcessSaveState (+0x88)
        /// 13. StoryFactions.ProcessSaveState (+0xb8)
        /// 14. [Corruption Check]
        /// 15. Squaddies.ProcessSaveState (+0x68)
        /// 16. Roster.ProcessSaveState (+0x70)
        /// 17. [Corruption Check]
        /// 18. BattlePlan.ProcessSaveState (+0x78)
        /// 19. [Corruption Check]
        /// 20. PlanetManager.ProcessSaveState (+0x50)
        /// 21. [Corruption Check]
        /// 22. OperationsManager.ProcessSaveState (+0x58)
        /// 23. [Corruption Check]
        /// 24. MissionResult (+0x60) via ProcessObject
        /// 25. [Corruption Check]
        /// 26. ConversationVariables (+0xc8)
        /// 27. BarksManager.ProcessSaveState (+0xa8)
        /// 28. ConversationEffects (+0xd0)
        /// 29. [Corruption Check]
        /// 30. EventManager.ProcessSaveState (+0xb0)
        /// 31. UnknownManager.ProcessSaveState (+0x90)
        /// 32. OffmapAbilities (+0x98)
        /// 33. [Corruption Check]
        /// 34. Apply active game effects
        /// 35. Close SaveState
        /// 36. Open appropriate UI screen
        /// </remarks>
        public static IEnumerator ProcessSaveStateFlow(StrategyState state, SaveState saveState)
        {
            // Step 0: Initialize
            if (saveState.IsLoading())
            {
                UIManager.IncrementLoadingProgress();
                yield return null;
            }

            // Initialize components when loading
            if (saveState.IsLoading())
            {
                state.ShipUpgrades?.Init();
                state.EventManager?.Init();
                UIManager.IncrementLoadingProgress();
                yield return null;
            }

            // StoryFactions init
            if (saveState.IsLoading())
            {
                state.StoryFactions?.Init();
                UIManager.IncrementLoadingProgress();
                yield return null;
            }

            // Process basic fields
            double playTime = state.PlayTime;
            saveState.ProcessDouble(ref playTime);
            state.PlayTime = playTime;

            bool isIronMan = state.IsIronMan;
            saveState.ProcessBool(ref isIronMan);
            state.IsIronMan = isIronMan;

            string ironManName = state.IronManSaveName;
            saveState.ProcessString(ref ironManName);
            state.IronManSaveName = ironManName;

            int currentDay = state.CurrentDay;
            saveState.ProcessInt(ref currentDay);
            state.CurrentDay = currentDay;

            // Process remaining data via sub-managers
            // Each has its own ProcessSaveState method that handles its data
            // Corruption checks are interspersed throughout

            // ... (detailed processing of all fields follows the same pattern)

            yield return null;
        }
    }

    // =========================================================================
    // PLACEHOLDER TYPES (for compilation reference only)
    // =========================================================================

    // These are stub types to make the reference code compile-able.
    // Actual implementations are in the game binary.

    public class StrategyState
    {
        public static StrategyState Current;
        public bool IsIronMan;
        public string IronManSaveName;
        public double PlayTime;
        public int CurrentDay;
        public object DifficultyTemplate;
        public object ShipUpgrades;
        public object EventManager;
        public object StoryFactions;
        public object CurrentOperation;
        public Operation GetCurrentOperation() => null;
        public IEnumerator ProcessSaveState(SaveState saveState) => null;
        public void SetIronMan(bool enabled, string saveName) { }
    }

    public class Operation
    {
        public object Template;
        public object Result;
        public object MissionList;
        public UnityEngine.Texture2D ScreenshotTexture;
        public object GetPlanet() => null;
        public int GetIndex() => 0;
    }

    public class StrategyConfig
    {
        public static StrategyConfig Current;
        public static void InitCurrent(StrategyConfig config) { }
        public string GetID() => "";
    }

    public class TacticalState
    {
        public static TacticalState Current;
    }

    public class TacticalManager
    {
        public static TacticalManager Instance;
        public void Finish(int reason) { }
    }

    public class StateManager
    {
        public static StateManager Instance;
        public void RemoveState<T>() { }
        public void TryAddState<T>() { }
    }

    public class UIManager
    {
        public static UIManager Instance;
        public void ShowNotification(string text) { }
        public object AddSimpleDialog() => null;
        public void CloseAllScreens(bool force) { }
        public void ShowLoadingOverlay(int type) { }
        public static void IncrementLoadingProgress() { }
    }

    public static class PlayerSettings
    {
        public static int GetInt(PlayerSettingType type) => 3;
    }

    public enum PlayerSettingType { MaxAutoSaves = 10 }

    public static class StrategyScheduler
    {
        public static void Execute(object coroutine) { }
    }

    public static class SceneManagerHelper
    {
        public static void EnsureSceneIsLoaded(string scene, Action<object, object> callback) { }
    }

    public static class Loca
    {
        public static string TranslateNotification(string key) => key;
    }

    public static class LogLevelExtensions
    {
        public static bool LogDebug(int category) => false;
        public static bool LogInfo(int category) => false;
    }

    public static class Resources
    {
        public static T Load<T>(string path) => default;
    }

    public class DisplayClass
    {
        public bool SceneLoaded;
    }
}

namespace UnityEngine
{
    public static class Application
    {
        public static string persistentDataPath => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }

    public static class Debug
    {
        public static void Log(string message) => Console.WriteLine($"[LOG] {message}");
        public static void LogError(string message) => Console.WriteLine($"[ERROR] {message}");
    }

    public class Texture2D
    {
        public Texture2D(int width, int height, TextureFormat format, bool mipChain) { }
    }

    public enum TextureFormat { RGB24 = 3 }

    public static class ImageConversion
    {
        public static byte[] EncodeToJPG(Texture2D texture, int quality) => Array.Empty<byte>();
        public static bool LoadImage(Texture2D texture, byte[] data) => true;
    }
}
