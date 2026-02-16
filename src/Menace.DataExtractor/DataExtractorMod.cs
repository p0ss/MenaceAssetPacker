using MelonLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

[assembly: MelonInfo(typeof(Menace.DataExtractor.DataExtractorMod), "Menace Data Extractor", "6.0.4", "MenaceModkit")]
[assembly: MelonGame(null, null)]

namespace Menace.DataExtractor
{
    public class DataExtractorMod : MelonMod
    {
        private const string ExtractorVersion = "6.0.4"; // GC-skip tracking + fingerprint protection

        // Singleton instance for static method access from DevConsole
        private static DataExtractorMod _instance;

        private string _outputPath = "";
        private string _debugLogPath = "";
        private string _fingerprintPath = "";
        private bool _hasSaved = false;

        /// <summary>
        /// Static method for DevConsole Settings panel to trigger extraction.
        /// </summary>
        public static void TriggerExtraction(bool force)
        {
            if (_instance == null)
            {
                MelonLoader.MelonLogger.Warning("[DataExtractor] TriggerExtraction called but instance not ready");
                return;
            }

            if (_instance._extractionInProgress || _instance._manualExtractionInProgress)
            {
                MelonLoader.MelonLogger.Msg("[DataExtractor] Extraction already in progress");
                return;
            }

            if (force)
            {
                // Delete fingerprint to force re-extraction
                try { if (File.Exists(_instance._fingerprintPath)) File.Delete(_instance._fingerprintPath); } catch { }
            }

            _instance._extractionInProgress = true;
            _instance._isManualExtraction = false;
            _instance.LoggerInstance.Msg($"=== EXTRACTION TRIGGERED (DevConsole, force={force}) ===");
            _instance.ShowExtractionProgress($"Extraction started from Settings panel (force={force})...");
            MelonCoroutines.Start(_instance.RunExtractionCoroutine(_instance._cachedFingerprint));
        }

        /// <summary>
        /// Static method for DevConsole Settings panel to get extraction status.
        /// </summary>
        public static string GetExtractionStatus()
        {
            if (_instance == null)
                return "DataExtractor not initialized";

            if (_instance._extractionInProgress || _instance._manualExtractionInProgress)
                return "Extraction in progress...";

            if (_instance._hasSaved && _instance.IsExtractionCurrent(_instance._cachedFingerprint))
                return "Up to date";

            return "Extraction needed";
        }

        // Tracking for GC-skipped instances during extraction
        // If too many objects are garbage collected between Phase 1 and Phase 2,
        // we don't save the fingerprint so re-extraction happens on next launch.
        private int _totalInstancesProcessed = 0;
        private int _gcSkippedInstances = 0;
        private const float MaxGcSkipPercentage = 0.01f; // 1% threshold - any higher indicates unstable extraction

        // Properties to skip during extraction
        private static readonly HashSet<string> SkipProperties = new(StringComparer.Ordinal)
        {
            "Pointer", "ObjectClass", "WasCollected", "m_CachedPtr",
            "hideFlags", "serializationData", "SerializationData",
            "SerializedBytesString", "UnitySerializedFields",
            "PrefabModificationsReapplied"
        };

        // Base types where we stop walking the inheritance chain
        private static readonly HashSet<string> StopBaseTypes = new(StringComparer.Ordinal)
        {
            "Object", "Il2CppObjectBase", "Il2CppSystem.Object",
            "ScriptableObject", "SerializedScriptableObject"
        };

        private MethodInfo _tryCastMethod;

        // Sentinel value indicating TryReadFieldDirect can't handle this type
        private static readonly object _skipSentinel = new object();

        // Cache: templateTypeName -> { propName -> (fieldPtr, offset) }
        // Avoids redundant il2cpp_class_get_field_from_name + il2cpp_field_get_offset calls
        private readonly Dictionary<string, Dictionary<string, (IntPtr field, uint offset)>> _fieldInfoCache = new();

        // Cache: IL2CPP class name -> managed Type (for ScriptableObject-based discovery)
        private readonly Dictionary<string, Type> _il2cppNameToType = new(StringComparer.Ordinal);

        // Cache: IL2CPP class name -> native class pointer (captured during classification, avoids
        // il2cpp_object_get_class on data pointers which can SIGSEGV on stale/garbage pointers)
        private readonly Dictionary<string, IntPtr> _il2cppClassPtrCache = new(StringComparer.Ordinal);

        // DevConsole reflection (for showing extraction progress in-game)
        private static Type _devConsoleType;
        private static MethodInfo _devConsoleLog;
        private static MethodInfo _devConsoleShowPanel;
        private static PropertyInfo _devConsoleIsVisible;
        private static bool _devConsoleAvailable;

        // Schema-driven extraction: loaded from embedded schema.json
        private Dictionary<string, SchemaType> _schemaTypes = new();
        private Dictionary<string, SchemaType> _embeddedClasses = new();
        private Dictionary<string, SchemaType> _structTypes = new();  // Value type structs
        private EventHandlerParser _effectHandlerParser = new();  // Polymorphic effect handler parsing
        private bool _schemaLoaded = false;

        private class SchemaType
        {
            public string Name { get; set; }
            public string BaseClass { get; set; }
            public bool IsAbstract { get; set; }
            public List<SchemaField> Fields { get; set; } = new();
        }

        private class SchemaField
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public uint Offset { get; set; }
            public string Category { get; set; }
            public string ElementType { get; set; }
        }

        public override void OnInitializeMelon()
        {
            _instance = this;

            var modsDir = Path.GetDirectoryName(typeof(DataExtractorMod).Assembly.Location) ?? "";
            var rootDir = Directory.GetParent(modsDir)?.FullName ?? "";
            _outputPath = Path.Combine(rootDir, "UserData", "ExtractedData");
            _debugLogPath = Path.Combine(_outputPath, "_extraction_debug.log");
            _fingerprintPath = Path.Combine(_outputPath, "_extraction_fingerprint.txt");
            Directory.CreateDirectory(_outputPath);

            // Clear previous debug log
            try { if (File.Exists(_debugLogPath)) File.Delete(_debugLogPath); } catch { }

            _tryCastMethod = typeof(Il2CppObjectBase).GetMethod("TryCast");

            // Load embedded schema for schema-driven extraction
            LoadEmbeddedSchema();

            LoggerInstance.Msg("===========================================");
            LoggerInstance.Msg($"Menace Data Extractor v{ExtractorVersion} (Manual Extraction Mode)");
            LoggerInstance.Msg($"Output path: {_outputPath}");
            LoggerInstance.Msg("===========================================");

            // Check extraction status and report to user
            var currentFingerprint = ComputeGameFingerprint(rootDir);
            _cachedFingerprint = currentFingerprint;

            // Check for force extraction flag from modkit
            var forceExtractionFlagPath = Path.Combine(_outputPath, "_force_extraction.flag");
            _forceExtractionPending = File.Exists(forceExtractionFlagPath);
            if (_forceExtractionPending)
            {
                LoggerInstance.Msg("Force extraction flag detected from modkit!");
                // Delete the flag file now that we've seen it
                try { File.Delete(forceExtractionFlagPath); } catch { }
            }

            if (IsExtractionCurrent(currentFingerprint) && !_forceExtractionPending)
            {
                LoggerInstance.Msg("Extracted data is up to date.");
                _hasSaved = true;
                PlayerLog("Data Extractor ready. Extraction up to date.");
            }
            else
            {
                var reason = _forceExtractionPending ? "force extraction requested" : "game data changed";
                LoggerInstance.Msg($"Extraction needed ({reason}) - will auto-trigger when game is ready");
                PlayerLog($"Data Extractor: {reason}. Extraction will start automatically.");
                // Auto-trigger extraction when force flag is set
                _autoExtractionPending = true;
            }

            // Register the extract command when DevConsole becomes available
            // (DevConsole may not be initialized yet, so we'll retry in OnUpdate)
            _commandRegistrationPending = true;
        }

        private bool _forceExtractionPending = false;
        private bool _autoExtractionPending = false;

        // Cached fingerprint for manual extraction
        private string _cachedFingerprint;
        private bool _commandRegistrationPending = false;
        private bool _commandRegistered = false;

        /// <summary>
        /// Try to register the 'extract' command with DevConsole.
        /// Called from OnUpdate until successful.
        /// </summary>
        private void TryRegisterExtractCommand()
        {
            if (_commandRegistered) return;

            InitDevConsoleReflection();
            if (!_devConsoleAvailable) return;

            try
            {
                // Find DevConsole.RegisterCommand via reflection
                var registerMethod = _devConsoleType?.GetMethod("RegisterCommand",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(string), typeof(string), typeof(Func<string[], string>) },
                    null);

                if (registerMethod != null)
                {
                    Func<string[], string> extractHandler = args =>
                    {
                        if (_extractionInProgress)
                            return "Extraction already in progress.";

                        bool force = args.Length > 0 && args[0].Equals("force", StringComparison.OrdinalIgnoreCase);

                        if (!force && _hasSaved && IsExtractionCurrent(_cachedFingerprint))
                            return "Extraction is already up to date. Use 'extract force' to re-extract.";

                        _extractionInProgress = true;
                        _isManualExtraction = false; // Not the F11 additive mode
                        LoggerInstance.Msg("=== MANUAL EXTRACTION TRIGGERED (extract command) ===");
                        MelonCoroutines.Start(RunExtractionCoroutine(_cachedFingerprint));
                        return "Extraction started. Progress will be shown in the Log panel.";
                    };

                    registerMethod.Invoke(null, new object[]
                    {
                        "extract",
                        "[force]",
                        "Extract game templates for modding. Use 'force' to re-extract.",
                        extractHandler
                    });

                    LoggerInstance.Msg("[DataExtractor] Registered 'extract' command with DevConsole");
                    _commandRegistered = true;
                    _commandRegistrationPending = false;

                    // Also register an 'extractstatus' command to check status
                    Func<string[], string> statusHandler = args =>
                    {
                        var lines = new List<string>();
                        lines.Add($"=== Data Extractor v{ExtractorVersion} ===");

                        if (_extractionInProgress || _manualExtractionInProgress)
                            lines.Add("Status: Extraction in progress...");
                        else if (_hasSaved && IsExtractionCurrent(_cachedFingerprint))
                            lines.Add("Status: Extraction is up to date");
                        else
                            lines.Add("Status: Extraction needed - run 'extract' command");

                        if (_totalInstancesProcessed > 0)
                        {
                            float gcPercent = (float)_gcSkippedInstances / _totalInstancesProcessed * 100f;
                            lines.Add($"Last run: {_gcSkippedInstances}/{_totalInstancesProcessed} objects GC'd ({gcPercent:F1}%)");
                            if (gcPercent > 1f)
                                lines.Add("WARNING: High GC rate indicates unstable extraction. Run 'extract force' from stable screen.");
                        }

                        lines.Add("");
                        lines.Add("Commands: 'extract' (normal), 'extract force' (re-extract)");
                        lines.Add("Hotkey: F11 (additive extraction for combat templates)");

                        return string.Join("\n", lines);
                    };

                    registerMethod.Invoke(null, new object[]
                    {
                        "extractstatus",
                        "",
                        "Show data extraction status and statistics",
                        statusHandler
                    });
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"[DataExtractor] Failed to register command: {ex.Message}");
                _commandRegistrationPending = false; // Don't keep trying
            }
        }

        private bool _extractionInProgress = false;

        /// <summary>
        /// Load the embedded schema.json for schema-driven extraction.
        /// The schema defines field offsets for types that aren't exposed via IL2CppInterop.
        /// </summary>
        private void LoadEmbeddedSchema()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("schema.json");
                if (stream == null)
                {
                    LoggerInstance.Warning("Schema not found in embedded resources");
                    return;
                }

                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                var root = JObject.Parse(json);

                // Parse template types (top-level templates)
                if (root["templates"] is JObject templateTypes)
                {
                    foreach (var kvp in templateTypes)
                    {
                        var typeName = kvp.Key;
                        var typeData = kvp.Value as JObject;
                        if (typeData == null) continue;

                        var schemaType = ParseSchemaType(typeName, typeData);
                        if (schemaType != null)
                            _schemaTypes[typeName] = schemaType;
                    }
                }

                // Parse embedded classes (non-template types like Army, ArmyEntry)
                if (root["embedded_classes"] is JObject embeddedClasses)
                {
                    foreach (var kvp in embeddedClasses)
                    {
                        var className = kvp.Key;
                        var classData = kvp.Value as JObject;
                        if (classData == null) continue;

                        var schemaType = ParseSchemaType(className, classData);
                        if (schemaType != null)
                            _embeddedClasses[className] = schemaType;
                    }
                }

                // Parse value type structs (like OperationTrustChange, OperationResources)
                if (root["structs"] is JObject structs)
                {
                    foreach (var kvp in structs)
                    {
                        var structName = kvp.Key;
                        var structData = kvp.Value as JObject;
                        if (structData == null) continue;

                        var schemaType = ParseSchemaType(structName, structData);
                        if (schemaType != null)
                            _structTypes[structName] = schemaType;
                    }
                }

                // Parse effect handlers (polymorphic types for SkillEventHandlerTemplate)
                int effectHandlerCount = 0;
                if (root["effect_handlers"] is JObject effectHandlers)
                {
                    foreach (var kvp in effectHandlers)
                    {
                        var handlerName = kvp.Key;
                        var handlerData = kvp.Value as JObject;
                        if (handlerData == null) continue;

                        var schema = ParseEffectHandlerSchema(handlerName, handlerData);
                        if (schema != null)
                        {
                            _effectHandlerParser.RegisterSchema(schema);
                            effectHandlerCount++;
                            DebugLog($"  Registered effect handler: {handlerName} with {schema.Fields.Count} fields");
                        }
                    }
                }

                _schemaLoaded = true;
                LoggerInstance.Msg($"Schema loaded: {_schemaTypes.Count} template types, {_embeddedClasses.Count} embedded classes, {_structTypes.Count} struct types, {effectHandlerCount} effect handlers");
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Failed to load schema: {ex.Message}");
            }
        }

        private SchemaType ParseSchemaType(string name, JObject data)
        {
            var schemaType = new SchemaType
            {
                Name = name,
                BaseClass = data["base_class"]?.ToString(),
                IsAbstract = data["is_abstract"]?.Value<bool>() ?? false
            };

            if (data["fields"] is JArray fields)
            {
                foreach (var fieldToken in fields)
                {
                    var fieldData = fieldToken as JObject;
                    if (fieldData == null) continue;

                    var offsetStr = fieldData["offset"]?.ToString() ?? "0x0";
                    uint offset = 0;
                    if (offsetStr.StartsWith("0x"))
                        uint.TryParse(offsetStr.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out offset);
                    else
                        uint.TryParse(offsetStr, out offset);

                    schemaType.Fields.Add(new SchemaField
                    {
                        Name = fieldData["name"]?.ToString(),
                        Type = fieldData["type"]?.ToString(),
                        Offset = offset,
                        Category = fieldData["category"]?.ToString(),
                        ElementType = fieldData["element_type"]?.ToString()
                    });
                }
            }

            return schemaType;
        }

        private EventHandlerParser.EffectHandlerSchema ParseEffectHandlerSchema(string name, JObject data)
        {
            var schema = new EventHandlerParser.EffectHandlerSchema
            {
                Name = name,
                TypeName = data["type_name"]?.ToString(),
                BaseClass = data["base_class"]?.ToString()
            };

            // Parse aliases
            if (data["aliases"] is JArray aliases)
            {
                foreach (var alias in aliases)
                {
                    var aliasStr = alias?.ToString();
                    if (!string.IsNullOrEmpty(aliasStr))
                        schema.Aliases.Add(aliasStr);
                }
            }

            // Parse fields
            if (data["fields"] is JArray fields)
            {
                foreach (var fieldToken in fields)
                {
                    var fieldData = fieldToken as JObject;
                    if (fieldData == null) continue;

                    var offsetStr = fieldData["offset"]?.ToString() ?? "0x0";
                    uint offset = 0;
                    if (offsetStr.StartsWith("0x"))
                        uint.TryParse(offsetStr.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out offset);
                    else
                        uint.TryParse(offsetStr, out offset);

                    schema.Fields.Add(new EventHandlerParser.EffectHandlerField
                    {
                        Name = fieldData["name"]?.ToString(),
                        Type = fieldData["type"]?.ToString(),
                        Offset = offset,
                        Category = fieldData["category"]?.ToString()
                    });
                }
            }

            return schema;
        }

        /// <summary>
        /// Check if a type is marked as abstract in the schema.
        /// Used to skip extracting types like SkillEventHandlerTemplate that are base classes.
        /// </summary>
        private bool IsSchemaAbstract(string typeName)
        {
            if (_schemaTypes.TryGetValue(typeName, out var schemaType))
                return schemaType.IsAbstract;
            return false;
        }

        private bool _manualExtractionInProgress = false;
        private bool _isManualExtraction = false; // True when triggered by F11, skips clean and merges results

        // Track frames for auto-extraction delay (wait for game to stabilize)
        private int _frameCount = 0;
        private const int AutoExtractionDelayFrames = 300; // ~5 seconds at 60fps

        /// <summary>
        /// Allow manual extraction trigger via F11 key (additive mode) and command registration.
        /// F11 is ADDITIVE - it merges new templates with existing extracted data.
        /// Use 'extract' command for full extraction.
        /// </summary>
        public override void OnUpdate()
        {
            _frameCount++;

            // Try to register command if pending
            if (_commandRegistrationPending && !_commandRegistered)
            {
                TryRegisterExtractCommand();
            }

            // Auto-extraction when force flag was set by modkit
            // Wait for game to stabilize (a few seconds after loading)
            if (_autoExtractionPending && !_extractionInProgress && !_manualExtractionInProgress)
            {
                if (_frameCount >= AutoExtractionDelayFrames)
                {
                    _autoExtractionPending = false;
                    _extractionInProgress = true;
                    _isManualExtraction = false;

                    var reason = _forceExtractionPending ? "force extraction from modkit" : "extraction needed";
                    LoggerInstance.Msg($"=== AUTO-EXTRACTION TRIGGERED ({reason}) ===");

                    // Try to find and open DevConsole to show progress
                    InitDevConsoleReflection();
                    ShowExtractionProgress($"Auto-extraction starting ({reason})...");

                    MelonCoroutines.Start(RunExtractionCoroutine(_cachedFingerprint));
                }
            }

            // F11 = trigger ADDITIVE manual extraction (captures templates currently in memory)
            // Can be used to extract combat templates while in battle
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F11) && !_manualExtractionInProgress && !_extractionInProgress)
            {
                _manualExtractionInProgress = true;
                _isManualExtraction = true;
                LoggerInstance.Msg("=== ADDITIVE EXTRACTION TRIGGERED (F11) ===");
                LoggerInstance.Msg("Additive mode: will merge with existing extracted data");
                ShowExtractionProgress("Additive extraction triggered - merging with existing data...");

                MelonCoroutines.Start(RunExtractionCoroutine(_cachedFingerprint));
            }
        }

        /// <summary>
        /// Try to find DevConsole type via reflection from ModpackLoader.
        /// This allows showing extraction progress in-game when both mods are loaded.
        /// </summary>
        private void InitDevConsoleReflection()
        {
            if (_devConsoleAvailable) return; // Already initialized successfully

            try
            {
                // Look for DevConsole in ALL loaded assemblies
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                LoggerInstance.Msg($"[DevConsole] Searching {assemblies.Length} assemblies...");

                foreach (var asm in assemblies)
                {
                    try
                    {
                        // Try to find DevConsole type directly
                        var dcType = asm.GetType("Menace.SDK.DevConsole");
                        if (dcType != null)
                        {
                            LoggerInstance.Msg($"[DevConsole] Found in: {asm.GetName().Name}");
                            _devConsoleType = dcType;
                            _devConsoleLog = dcType.GetMethod("Log", BindingFlags.Public | BindingFlags.Static);
                            _devConsoleShowPanel = dcType.GetMethod("ShowPanel", BindingFlags.Public | BindingFlags.Static);
                            _devConsoleIsVisible = dcType.GetProperty("IsVisible", BindingFlags.Public | BindingFlags.Static);
                            // Only require Log for availability - ShowPanel is optional
                            _devConsoleAvailable = _devConsoleLog != null;
                            LoggerInstance.Msg($"[DevConsole] Found! Log={_devConsoleLog != null}, ShowPanel={_devConsoleShowPanel != null}");
                            return;
                        }
                    }
                    catch { }
                }
                LoggerInstance.Msg("[DevConsole] Type 'Menace.SDK.DevConsole' not found in any assembly");
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"[DevConsole] Init failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Show extraction progress message in the DevConsole (if available) and MelonLogger.
        /// Opens the Log panel automatically so the player can see what's happening.
        /// </summary>
        private void ShowExtractionProgress(string message)
        {
            LoggerInstance.Msg(message);

            // Try DevConsole logging even if initialization previously failed - ModpackLoader might be loaded now
            if (_devConsoleLog == null && !_devConsoleAvailable)
            {
                InitDevConsoleReflection();
            }

            // Log to DevConsole if available (don't require ShowPanel for logging to work)
            if (_devConsoleLog != null)
            {
                try
                {
                    // Open the Log panel if method is available
                    _devConsoleShowPanel?.Invoke(null, new object[] { "Log" });
                    _devConsoleLog.Invoke(null, new object[] { $"[DataExtractor] {message}" });
                }
                catch (Exception ex)
                {
                    LoggerInstance.Warning($"[DevConsole] Call failed: {ex.Message}");
                    _devConsoleLog = null; // Mark as broken so we stop trying
                }
            }
        }

        /// <summary>
        /// Compute a fingerprint of the game's data files to detect when re-extraction is needed.
        /// Includes the extractor version so updated extraction logic triggers re-extraction.
        /// </summary>
        private string ComputeGameFingerprint(string gameRoot)
        {
            // Include extractor version so logic changes invalidate the cache
            var extractorVersion = ExtractorVersion;

            try
            {
                // Primary: GameAssembly.dll (changes on every game update)
                var gameAssembly = Path.Combine(gameRoot, "GameAssembly.dll");
                if (File.Exists(gameAssembly))
                {
                    var info = new FileInfo(gameAssembly);
                    return $"Extractor|{extractorVersion}|GameAssembly|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
                }

                // Fallback: globalgamemanagers
                var dataDirs = Directory.GetDirectories(gameRoot, "*_Data");
                foreach (var dataDir in dataDirs)
                {
                    var ggm = Path.Combine(dataDir, "globalgamemanagers");
                    if (File.Exists(ggm))
                    {
                        var info = new FileInfo(ggm);
                        return $"Extractor|{extractorVersion}|GGM|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Check if the stored fingerprint matches the current game data
        /// and that extracted JSON files actually exist.
        /// </summary>
        private bool IsExtractionCurrent(string currentFingerprint)
        {
            if (string.IsNullOrEmpty(currentFingerprint))
                return false;

            try
            {
                if (!File.Exists(_fingerprintPath))
                    return false;

                var storedFingerprint = File.ReadAllText(_fingerprintPath).Trim();
                if (storedFingerprint != currentFingerprint)
                    return false;

                // Verify that extracted data actually exists (at least a few JSON files)
                var jsonFiles = Directory.GetFiles(_outputPath, "*.json");
                return jsonFiles.Length >= 3;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Save the fingerprint after successful extraction.
        /// </summary>
        private void SaveFingerprint(string fingerprint)
        {
            if (string.IsNullOrEmpty(fingerprint))
                return;

            try
            {
                File.WriteAllText(_fingerprintPath, fingerprint);
            }
            catch { }
        }

        // Write directly to a file and flush — survives native crashes
        private void DebugLog(string message)
        {
            try
            {
                using var sw = new StreamWriter(_debugLogPath, append: true);
                sw.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
                sw.Flush();
            }
            catch { }
        }

        // Shared state between PrepareExtraction and the phase loops
        private List<Type> _pendingTemplateTypes;

        // Cached method references for incremental loading (set during PrepareExtraction)
        private MethodInfo _getAllMethod;
        private MethodInfo _getBaseFolderMethod;
        private MethodInfo _loadAllMethod;
        private MethodInfo _findObjectsMethod;
        private Type _loaderType;

        // Scenes that are considered stable for data extraction (no active scene transitions)
        private static readonly HashSet<string> StableScenes = new(StringComparer.OrdinalIgnoreCase)
        {
            "OCI",           // Operation Command Interface - user confirmed stable
            "Tactical",      // Tactical combat - should be stable
            "MainMenu",      // Main menu - stable
            "SplashScreen",  // Initial splash - stable but early
            "StrategicMap",  // Strategy layer - stable
            "Barracks",      // Unit management - stable
        };

        private System.Collections.IEnumerator RunExtractionCoroutine(string fingerprint = null)
        {
            // Only log to MelonLogger during early wait - DevConsole not safe yet
            LoggerInstance.Msg("Waiting for stable game state...");

            // Wait for a stable scene before extraction to avoid GC during scene transitions
            // Scene transitions cause Unity's GC to be very active, which can collect
            // objects we're trying to extract, causing AccessViolationException.
            float waited = 0f;
            float maxWait = 120f; // 2 minute timeout
            string lastScene = "";
            float sceneStableTime = 0f;
            float requiredStableTime = 3f; // Scene must be stable for 3 seconds

            while (waited < maxWait)
            {
                yield return null;
                waited += Time.deltaTime;

                // Get current scene name
                string currentScene = "";
                try
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    currentScene = scene.name ?? "";
                }
                catch { }

                // Track scene stability
                if (currentScene == lastScene)
                {
                    sceneStableTime += Time.deltaTime;
                }
                else
                {
                    if (!string.IsNullOrEmpty(currentScene) && currentScene != lastScene)
                        LoggerInstance.Msg($"Scene changed: {lastScene} -> {currentScene}");
                    lastScene = currentScene;
                    sceneStableTime = 0f;
                }

                // Check if we're in a stable scene that has been stable for long enough
                if (StableScenes.Contains(currentScene) && sceneStableTime >= requiredStableTime)
                {
                    LoggerInstance.Msg($"Scene '{currentScene}' stable for {sceneStableTime:F1}s, starting extraction");
                    break;
                }

                // Also allow extraction after minimum wait if scene has been stable (even if not in our list)
                if (waited >= 10f && sceneStableTime >= 5f && !string.IsNullOrEmpty(currentScene))
                {
                    LoggerInstance.Msg($"Scene '{currentScene}' stable for {sceneStableTime:F1}s (unknown scene, using extended stability), starting extraction");
                    break;
                }

                // Log progress every 10 seconds (only to MelonLogger - DevConsole not safe yet)
                if ((int)waited % 10 == 0 && waited > 0 && Time.deltaTime > 0)
                {
                    LoggerInstance.Msg($"Waiting for stable scene... (current: {currentScene}, {waited:F0}s)");
                }
            }

            if (waited >= maxWait)
            {
                LoggerInstance.Warning($"Timeout waiting for stable scene after {maxWait}s, proceeding anyway");
            }

            // Now that game is stable, retry DevConsole lookup (ModpackLoader should be initialized)
            if (!_devConsoleAvailable)
            {
                InitDevConsoleReflection();
            }

            // From here on, ShowExtractionProgress is safe to use
            ShowExtractionProgress("Starting template extraction...");

            for (int attempt = 1; attempt <= 60; attempt++)
            {
                if (PrepareExtraction())
                {
                    // Extraction is ready — process templates in TWO PASSES:
                    // Pass 1: Templates WITH Resource paths (loads parent templates)
                    // Pass 2: Templates WITHOUT paths ("loose" templates, embedded in parents)
                    // This ensures loose templates like ArmyListTemplate are findable after
                    // their parent templates (FactionTemplate) are loaded.

                    DebugLog("=== INCREMENTAL EXTRACTION: Two-pass mode ===");

                    // Reset GC-skip counters for this extraction run
                    _totalInstancesProcessed = 0;
                    _gcSkippedInstances = 0;

                    // Partition templates by whether they have a GetBaseFolder path
                    var templatesWithPath = new List<Type>();
                    var templatesWithoutPath = new List<Type>();

                    foreach (var t in _pendingTemplateTypes)
                    {
                        string path = null;
                        try
                        {
                            path = _getBaseFolderMethod?.Invoke(null, new object[] { t }) as string;
                        }
                        catch { }

                        if (!string.IsNullOrEmpty(path))
                            templatesWithPath.Add(t);
                        else
                            templatesWithoutPath.Add(t);
                    }

                    DebugLog($"  Pass 1: {templatesWithPath.Count} templates with paths");
                    DebugLog($"  Pass 2: {templatesWithoutPath.Count} loose templates (embedded in parents)");

                    int totalTypes = _pendingTemplateTypes.Count;
                    int successCount = 0;
                    int skippedCount = 0;
                    int typeIndex = 0;

                    // === PASS 1: Templates with paths ===
                    DebugLog("=== PASS 1: Templates with Resource paths ===");
                    foreach (var templateType in templatesWithPath)
                    {
                        typeIndex++;
                        ShowExtractionProgress($"[Pass 1] {templateType.Name}... [{typeIndex}/{totalTypes}]");

                        // === LOAD: Get objects for this type only ===
                        DebugLog($">>> LOAD {templateType.Name}");
                        var objects = LoadTypeObjects(templateType);

                        if (objects == null || objects.Count == 0)
                        {
                            DebugLog($">>> SKIP {templateType.Name} (no instances)");
                            skippedCount++;
                            yield return null;
                            continue;
                        }

                        // === PHASE 1: Extract primitives ===
                        DebugLog($">>> P1 START {templateType.Name} ({objects.Count} instances)");
                        TypeContext typeCtx = null;
                        try
                        {
                            typeCtx = ExtractTypePhase1(templateType, objects);
                        }
                        catch (Exception ex)
                        {
                            DebugLog($"  P1 EXCEPTION: {ex.Message}");
                            ShowExtractionProgress($"ERROR extracting {templateType.Name}: {ex.Message}");
                        }

                        if (typeCtx == null || typeCtx.Instances.Count == 0)
                        {
                            DebugLog($">>> SKIP {templateType.Name} (no instances extracted)");
                            skippedCount++;
                            objects.Clear();
                            objects = null;
                            yield return null;
                            continue;
                        }

                        // Yield after phase 1 to give game a frame
                        yield return null;

                        // === PHASE 2: Fill references ===
                        LoggerInstance.Msg($"  Phase 2: {templateType.Name} - filling references...");
                        DebugLog($">>> P2 START {templateType.Name}");
                        int instIdx = 0;
                        int yieldCounter = 0;
                        foreach (var inst in typeCtx.Instances)
                        {
                            instIdx++;
                            if (instIdx % 50 == 0 || instIdx >= typeCtx.Instances.Count - 20)
                                LoggerInstance.Msg($"    P2 [{instIdx}/{typeCtx.Instances.Count}] {inst.Name}...");

                            // Pre-flight check: skip objects that were garbage collected between phases
                            _totalInstancesProcessed++;
                            try
                            {
                                if (inst.CastObj is Il2CppObjectBase il2cppCheck && il2cppCheck.WasCollected)
                                {
                                    _gcSkippedInstances++;
                                    LoggerInstance.Msg($"    P2 [{instIdx}] {inst.Name} - SKIPPED (garbage collected)");
                                    yieldCounter++;
                                    continue;
                                }
                            }
                            catch (Exception gcEx)
                            {
                                // Modded templates may have invalid CastObj references
                                LoggerInstance.Msg($"    P2 [{instIdx}] {inst.Name} - GC check failed: {gcEx.Message}, attempting anyway");
                            }

                            try
                            {
                                FillReferenceProperties(inst, templateType);
                                if (instIdx >= typeCtx.Instances.Count - 20)
                                    LoggerInstance.Msg($"      [{instIdx}] FillReferenceProperties done");
                            }
                            catch (Exception instEx)
                            {
                                LoggerInstance.Msg($"    P2 [{instIdx}] EXCEPTION: {instEx.Message}");
                                DebugLog($"    P2 instance {instIdx}/{typeCtx.Instances.Count} [{inst.Name}] EXCEPTION: {instEx.Message}");
                            }
                            yieldCounter++;
                        }
                        LoggerInstance.Msg($"  Phase 2 complete: {instIdx} instances processed (GC-skipped: {_gcSkippedInstances})");

                        // Yield after phase 2 (and give proportional frames for large types)
                        int extraYields = yieldCounter / 50;
                        for (int y = 0; y <= extraYields; y++)
                            yield return null;

                        // === PHASE 3: Fix unknown names ===
                        LoggerInstance.Msg($"  Phase 3: {templateType.Name} - fixing names...");
                        DebugLog($">>> P3 START {templateType.Name}");
                        foreach (var inst in typeCtx.Instances)
                        {
                            if (inst.Name != null && inst.Name.StartsWith("unknown_") && inst.Pointer != IntPtr.Zero)
                            {
                                try
                                {
                                    var unityObj = objects.FirstOrDefault(o =>
                                        o is Il2CppObjectBase ib && ib.Pointer == inst.Pointer);
                                    if (unityObj != null && !string.IsNullOrEmpty(unityObj.name))
                                    {
                                        inst.Data["m_ID"] = unityObj.name;
                                        inst.Name = unityObj.name;
                                    }
                                }
                                catch { }
                            }
                        }

                        // === SAVE: Write this type to disk ===
                        LoggerInstance.Msg($"  Saving {templateType.Name}...");
                        DebugLog($">>> SAVE {templateType.Name}");
                        var dataList = typeCtx.Instances.Select(i => (object)i.Data).ToList();
                        SaveSingleTemplateType(templateType.Name, dataList);
                        successCount++;

                        LoggerInstance.Msg($"  {templateType.Name}: {typeCtx.Instances.Count} instances");
                        ShowExtractionProgress($"Saved {templateType.Name} ({typeCtx.Instances.Count}) [{successCount}/{totalTypes - skippedCount}]");

                        // === RELEASE: Clear references so GC can reclaim memory ===
                        DebugLog($">>> RELEASE {templateType.Name}");
                        typeCtx.Instances.Clear();
                        typeCtx = null;
                        dataList.Clear();
                        dataList = null;
                        objects.Clear();
                        objects = null;

                        // Yield to let GC run and give game a frame
                        yield return null;
                    }

                    // === PASS 2: Loose templates (no Resource path, embedded in parents) ===
                    // These templates are now findable via FindObjectsOfTypeAll since their
                    // parent templates (e.g., FactionTemplate) were loaded in Pass 1.
                    DebugLog("=== PASS 2: Loose templates (embedded in parents) ===");
                    foreach (var templateType in templatesWithoutPath)
                    {
                        typeIndex++;
                        ShowExtractionProgress($"[Pass 2] {templateType.Name}... [{typeIndex}/{totalTypes}]");

                        // === LOAD: FindObjectsOfTypeAll will find embedded instances ===
                        DebugLog($">>> LOAD {templateType.Name} (loose template)");
                        LoggerInstance.Msg($"  Loading {templateType.Name}...");
                        List<UnityEngine.Object> objects = null;
                        bool loadFailed = false;
                        try
                        {
                            objects = LoadTypeObjects(templateType);
                        }
                        catch (Exception loadEx)
                        {
                            LoggerInstance.Error($"  LOAD FAILED {templateType.Name}: {loadEx.Message}");
                            DebugLog($">>> LOAD EXCEPTION {templateType.Name}: {loadEx}");
                            skippedCount++;
                            loadFailed = true;
                        }

                        if (loadFailed)
                        {
                            yield return null;
                            continue;
                        }

                        if (objects == null || objects.Count == 0)
                        {
                            DebugLog($">>> SKIP {templateType.Name} (no instances found - may need specific game state)");
                            skippedCount++;
                            yield return null;
                            continue;
                        }

                        // === PHASE 1: Extract primitives ===
                        DebugLog($">>> P1 START {templateType.Name} ({objects.Count} instances)");
                        LoggerInstance.Msg($"  Phase 1: {templateType.Name} ({objects.Count} instances)");
                        TypeContext typeCtx = null;
                        try
                        {
                            typeCtx = ExtractTypePhase1(templateType, objects);
                        }
                        catch (Exception ex)
                        {
                            LoggerInstance.Error($"  P1 EXCEPTION {templateType.Name}: {ex.Message}");
                            DebugLog($"  P1 EXCEPTION: {ex.Message}\n{ex.StackTrace}");
                            ShowExtractionProgress($"ERROR extracting {templateType.Name}: {ex.Message}");
                        }

                        LoggerInstance.Msg($"    P1 returned, checking typeCtx...");
                        if (typeCtx == null || typeCtx.Instances.Count == 0)
                        {
                            LoggerInstance.Msg($"    typeCtx null or empty, skipping...");
                            DebugLog($">>> SKIP {templateType.Name} (no instances extracted)");
                            skippedCount++;
                            objects.Clear();
                            objects = null;
                            yield return null;
                            continue;
                        }

                        LoggerInstance.Msg($"    typeCtx has {typeCtx.Instances.Count} instances, yielding...");
                        // Yield after phase 1 to give game a frame
                        yield return null;
                        LoggerInstance.Msg($"    yield complete, starting Phase 2...");

                        // === PHASE 2: Fill references ===
                        LoggerInstance.Msg($"  Phase 2: {templateType.Name} - filling references (loose)...");
                        DebugLog($">>> P2 START {templateType.Name}");
                        int instIdx = 0;
                        int yieldCounter = 0;
                        int totalInstances = typeCtx.Instances.Count;
                        LoggerInstance.Msg($"    P2 iterating over {totalInstances} instances...");
                        foreach (var inst in typeCtx.Instances)
                        {
                            instIdx++;
                            // Log first instance and every 50
                            if (instIdx == 1 || instIdx % 50 == 0 || instIdx >= totalInstances - 20)
                                LoggerInstance.Msg($"    P2 [{instIdx}/{totalInstances}] {inst?.Name ?? "NULL"}...");

                            // Pre-flight check: skip objects that were garbage collected between phases
                            _totalInstancesProcessed++;
                            try
                            {
                                if (inst.CastObj is Il2CppObjectBase il2cppCheck && il2cppCheck.WasCollected)
                                {
                                    _gcSkippedInstances++;
                                    LoggerInstance.Msg($"    P2 [{instIdx}] {inst?.Name ?? "NULL"} - SKIPPED (garbage collected)");
                                    yieldCounter++;
                                    continue;
                                }
                            }
                            catch (Exception gcEx)
                            {
                                // Modded templates may have invalid CastObj references
                                LoggerInstance.Msg($"    P2 [{instIdx}] {inst?.Name ?? "NULL"} - GC check failed: {gcEx.Message}, attempting anyway");
                            }

                            try
                            {
                                FillReferenceProperties(inst, templateType);
                                if (instIdx >= typeCtx.Instances.Count - 20)
                                    LoggerInstance.Msg($"      [{instIdx}] FillReferenceProperties done");
                            }
                            catch (Exception instEx)
                            {
                                LoggerInstance.Msg($"    P2 [{instIdx}] EXCEPTION: {instEx.Message}");
                                DebugLog($"    P2 instance {instIdx}/{typeCtx.Instances.Count} [{inst.Name}] EXCEPTION: {instEx.Message}");
                            }
                            yieldCounter++;
                        }
                        LoggerInstance.Msg($"  Phase 2 complete: {instIdx} instances processed (GC-skipped: {_gcSkippedInstances})");

                        // Yield after phase 2 (and give proportional frames for large types)
                        int extraYields = yieldCounter / 50;
                        for (int y = 0; y <= extraYields; y++)
                            yield return null;

                        // === PHASE 3: Fix unknown names ===
                        LoggerInstance.Msg($"  Phase 3: {templateType.Name} - fixing names...");
                        DebugLog($">>> P3 START {templateType.Name}");
                        foreach (var inst in typeCtx.Instances)
                        {
                            if (inst.Name != null && inst.Name.StartsWith("unknown_") && inst.Pointer != IntPtr.Zero)
                            {
                                try
                                {
                                    var unityObj = objects.FirstOrDefault(o =>
                                        o is Il2CppObjectBase ib && ib.Pointer == inst.Pointer);
                                    if (unityObj != null && !string.IsNullOrEmpty(unityObj.name))
                                    {
                                        inst.Data["m_ID"] = unityObj.name;
                                        inst.Name = unityObj.name;
                                    }
                                }
                                catch { }
                            }
                        }

                        // === SAVE: Write this type to disk ===
                        LoggerInstance.Msg($"  Saving {templateType.Name}...");
                        DebugLog($">>> SAVE {templateType.Name}");
                        var dataList = typeCtx.Instances.Select(i => (object)i.Data).ToList();
                        SaveSingleTemplateType(templateType.Name, dataList);
                        successCount++;

                        LoggerInstance.Msg($"  {templateType.Name}: {typeCtx.Instances.Count} instances (loose)");
                        ShowExtractionProgress($"Saved {templateType.Name} ({typeCtx.Instances.Count}) [{successCount}/{totalTypes - skippedCount}]");

                        // === RELEASE: Clear references so GC can reclaim memory ===
                        DebugLog($">>> RELEASE {templateType.Name}");
                        typeCtx.Instances.Clear();
                        typeCtx = null;
                        dataList.Clear();
                        dataList = null;
                        objects.Clear();
                        objects = null;

                        // Yield to let GC run and give game a frame
                        yield return null;
                    }

                    DebugLog($"=== Extraction complete: {successCount} types saved, {skippedCount} skipped ===");
                    LoggerInstance.Msg($"Extraction complete: {successCount} types saved, {skippedCount} skipped");
                    LoggerInstance.Msg($"GC statistics: {_gcSkippedInstances}/{_totalInstancesProcessed} instances garbage collected during extraction");

                    // Check if too many instances were GC'd during extraction
                    // This indicates the game was in an unstable state (scene transition, etc.)
                    float gcSkipPercentage = _totalInstancesProcessed > 0
                        ? (float)_gcSkippedInstances / _totalInstancesProcessed
                        : 0f;
                    bool extractionUnstable = gcSkipPercentage > MaxGcSkipPercentage;

                    if (extractionUnstable)
                    {
                        LoggerInstance.Warning($"=== EXTRACTION QUALITY WARNING ===");
                        LoggerInstance.Warning($"  {_gcSkippedInstances} instances ({gcSkipPercentage:P1}) were garbage collected during extraction.");
                        LoggerInstance.Warning($"  This indicates the game was in an unstable state (scene transition, loading, etc.).");
                        LoggerInstance.Warning($"  Extracted data may be incomplete. Run 'extract force' from a stable screen.");
                        LoggerInstance.Warning($"  TIP: Wait for the game to fully load (OCI, Barracks, StrategicMap) before extraction.");
                        ShowExtractionProgress("WARNING: Extraction incomplete due to game instability!");
                        ShowExtractionProgress($"{_gcSkippedInstances} objects were unloaded during extraction.");
                        ShowExtractionProgress("Run 'extract force' from a stable screen to re-extract.");
                        ShowExtractionProgress("TIP: OCI, Barracks, or StrategicMap are stable screens.");
                    }

                    ShowExtractionProgress($"Extraction complete: {successCount} template types saved");

                    _hasSaved = successCount > 0;
                    // Only save fingerprint if extraction was stable (few or no GC-skipped instances)
                    if (_hasSaved && !_isManualExtraction && !extractionUnstable)
                        SaveFingerprint(fingerprint);
                    else if (extractionUnstable)
                        LoggerInstance.Msg("Fingerprint NOT saved due to extraction instability");

                    LoggerInstance.Msg($"Extraction completed on attempt {attempt}");
                    if (_isManualExtraction)
                        ShowExtractionProgress($"Additive extraction complete! {successCount} types processed (merged with existing).");
                    else if (!extractionUnstable)
                        ShowExtractionProgress($"Extraction complete! {successCount} template types extracted.");
                    if (!extractionUnstable)
                        ShowExtractionProgress("Game data is now ready for modding.");
                    _isManualExtraction = false;
                    _manualExtractionInProgress = false;
                    _extractionInProgress = false;
                    yield break;
                }

                // Wait ~2 seconds before retrying
                float retryWait = 0f;
                while (retryWait < 2f)
                {
                    yield return null;
                    retryWait += Time.deltaTime;
                }
            }

            LoggerInstance.Warning("Could not extract templates after 60 attempts");
            _extractionInProgress = false;
            _manualExtractionInProgress = false;
            ShowExtractionProgress("Extraction failed after 60 attempts. Please try again from a stable game screen.");
        }

        // Holds context for a single template instance between phases
        private class InstanceContext
        {
            public Dictionary<string, object> Data;
            public object CastObj;
            public IntPtr Pointer; // IL2CPP native object pointer
            public string Name;
        }

        // Holds context for a template type between phases
        private class TypeContext
        {
            public Type TemplateType;
            public List<InstanceContext> Instances = new();
        }

        /// <summary>
        /// Get the IL2CPP class name for a native object pointer by walking up
        /// to find the most-derived class name.
        /// </summary>
        private string GetIl2CppClassName(IntPtr objectPointer)
        {
            try
            {
                IntPtr klass = IL2CPP.il2cpp_object_get_class(objectPointer);
                if (klass == IntPtr.Zero) return null;
                IntPtr namePtr = IL2CPP.il2cpp_class_get_name(klass);
                if (namePtr == IntPtr.Zero) return null;
                return Marshal.PtrToStringAnsi(namePtr);
            }
            catch { return null; }
        }

        /// <summary>
        /// Enumerates an IL2CPP collection returned by DataTemplateLoader.GetAll&lt;T&gt;().
        /// IL2CPP collections don't implement managed System.Collections.IEnumerable,
        /// so we use Il2CppInterop's TryCast and reflection-based fallbacks.
        /// </summary>
        private List<UnityEngine.Object> EnumerateIl2CppCollection(object collection)
        {
            var results = new List<UnityEngine.Object>();

            // Strategy 1: TryCast to Il2CppSystem.Collections.IEnumerable (IL2CPP-level cast)
            if (collection is Il2CppObjectBase il2cppObj)
            {
                try
                {
                    var il2cppEnumerable = il2cppObj.TryCast<Il2CppSystem.Collections.IEnumerable>();
                    if (il2cppEnumerable != null)
                    {
                        var enumerator = il2cppEnumerable.GetEnumerator();
                        while (enumerator.MoveNext())
                        {
                            var current = enumerator.Current;
                            if (current == null) continue;
                            var unityObj = current.TryCast<UnityEngine.Object>();
                            if (unityObj != null)
                                results.Add(unityObj);
                        }
                        if (results.Count > 0) return results;
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"    Il2Cpp IEnumerable strategy failed: {ex.Message}");
                }
            }

            // Strategy 2: Reflection-based GetEnumerator on the IL2CPP proxy type
            try
            {
                var collType = collection.GetType();
                var getEnumeratorMethod = collType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "GetEnumerator" && m.GetParameters().Length == 0);

                if (getEnumeratorMethod != null)
                {
                    var enumerator = getEnumeratorMethod.Invoke(collection, null);
                    if (enumerator != null)
                    {
                        var enumType = enumerator.GetType();
                        var moveNext = enumType.GetMethod("MoveNext",
                            BindingFlags.Public | BindingFlags.Instance);
                        var currentProp = enumType.GetProperty("Current",
                            BindingFlags.Public | BindingFlags.Instance);

                        if (moveNext != null && currentProp != null)
                        {
                            while ((bool)moveNext.Invoke(enumerator, null))
                            {
                                var item = currentProp.GetValue(enumerator);
                                AddAsUnityObject(results, item);
                            }
                            if (results.Count > 0) return results;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"    Reflection GetEnumerator strategy failed: {ex.Message}");
            }

            // Strategy 3: Count property + indexer
            try
            {
                var collType = collection.GetType();
                var countProp = collType.GetProperty("Count",
                    BindingFlags.Public | BindingFlags.Instance);

                if (countProp != null)
                {
                    int count = Convert.ToInt32(countProp.GetValue(collection));
                    var indexer = collType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(p => p.GetIndexParameters().Length == 1);

                    if (indexer != null)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            var item = indexer.GetValue(collection, new object[] { i });
                            AddAsUnityObject(results, item);
                        }
                        if (results.Count > 0) return results;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"    Count+indexer strategy failed: {ex.Message}");
            }

            // Strategy 4: managed IEnumerable (last resort)
            if (collection is System.Collections.IEnumerable managedEnumerable)
            {
                foreach (var item in managedEnumerable)
                    AddAsUnityObject(results, item);
            }

            return results;
        }

        private void AddAsUnityObject(List<UnityEngine.Object> results, object item)
        {
            if (item == null) return;

            if (item is UnityEngine.Object unityObj)
            {
                results.Add(unityObj);
                return;
            }

            if (item is Il2CppObjectBase il2cppItem)
            {
                var cast = il2cppItem.TryCast<UnityEngine.Object>();
                if (cast != null)
                    results.Add(cast);
            }
        }

        /// <summary>
        /// Preparation phase: discovers template types and caches method references.
        /// Does NOT load all templates into memory - that happens incrementally during extraction.
        /// Returns true when ready to begin incremental extraction.
        /// </summary>
        private bool PrepareExtraction()
        {
            try
            {
                var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (gameAssembly == null)
                    return false;

                var templateTypes = gameAssembly.GetTypes()
                    .Where(t => t.Name.EndsWith("Template") && !t.IsAbstract)
                    .Where(t => !IsSchemaAbstract(t.Name)) // Also skip types marked abstract in schema
                    .OrderBy(t => t.Name)
                    .ToList();

                if (templateTypes.Count == 0)
                    return false;

                // Build lookup: IL2CPP class name -> managed Type
                _il2cppNameToType.Clear();
                foreach (var t in templateTypes)
                    _il2cppNameToType[t.Name] = t;

                LoggerInstance.Msg($"Found {templateTypes.Count} template types, preparing incremental extraction...");

                // Clean previous extraction results (skip in manual/additive mode)
                if (!_isManualExtraction)
                    CleanOutputDirectory();
                else
                    DebugLog("Skipping CleanOutputDirectory (additive mode)");

                DebugLog($"=== Extraction started: {templateTypes.Count} types (incremental mode) ===");
                for (int i = 0; i < templateTypes.Count; i++)
                    DebugLog($"  [{i}] {templateTypes[i].Name}");

                // Cache loader methods for incremental loading
                _loaderType = gameAssembly.GetTypes()
                    .FirstOrDefault(t => t.Name == "DataTemplateLoader");

                if (_loaderType == null)
                {
                    DebugLog("  DataTemplateLoader not found — cannot extract");
                    return false;
                }

                _getAllMethod = _loaderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "GetAll" && m.IsGenericMethodDefinition);

                if (_getAllMethod == null)
                {
                    DebugLog("  DataTemplateLoader.GetAll<T> not found");
                    return false;
                }

                // Cache fallback methods
                _getBaseFolderMethod = _loaderType.GetMethod("GetBaseFolder",
                    BindingFlags.Public | BindingFlags.Static);

                _loadAllMethod = typeof(Resources).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "LoadAll" && m.IsGenericMethodDefinition &&
                                    m.GetParameters().Length == 1);

                _findObjectsMethod = typeof(Resources).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "FindObjectsOfTypeAll" && m.IsGenericMethodDefinition);

                // Quick readiness check: load just one key type to verify m_ID is populated
                var testType = templateTypes.FirstOrDefault(t => t.Name == "WeaponTemplate") ?? templateTypes[0];
                var testObjects = LoadTypeObjects(testType);
                if (testObjects == null || testObjects.Count == 0)
                {
                    DebugLog("  Templates not ready yet (no test objects loaded), will retry");
                    return false;
                }

                // Check m_ID on test objects
                if (_il2cppClassPtrCache.TryGetValue(testType.Name, out var testClass))
                {
                    var sampleObj = testObjects[0] as Il2CppObjectBase;
                    if (sampleObj != null && sampleObj.Pointer != IntPtr.Zero)
                    {
                        IntPtr idField = FindNativeField(testClass, "m_ID");
                        if (idField != IntPtr.Zero)
                        {
                            uint idOffset = IL2CPP.il2cpp_field_get_offset(idField);
                            if (idOffset > 0)
                            {
                                IntPtr strPtr = Marshal.ReadIntPtr(sampleObj.Pointer + (int)idOffset);
                                if (strPtr == IntPtr.Zero)
                                {
                                    DebugLog($"  {testType.Name}[0] m_ID=null (not yet initialized), will retry");
                                    return false;
                                }
                                string id = IL2CPP.Il2CppStringToManaged(strPtr);
                                if (string.IsNullOrEmpty(id))
                                {
                                    DebugLog($"  {testType.Name}[0] m_ID is empty, will retry");
                                    return false;
                                }
                                DebugLog($"  Readiness check passed: {testType.Name}[0] m_ID={id}");
                            }
                        }
                    }
                }

                // Clear test objects to free memory before starting real extraction
                testObjects.Clear();
                testObjects = null;

                // Ready — store template types for incremental processing
                _pendingTemplateTypes = templateTypes;
                return true;
            }
            catch (Exception ex)
            {
                DebugLog($"PREPARE FATAL: {ex.Message}\n{ex.StackTrace}");
                LoggerInstance.Error($"PrepareExtraction error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Load objects for a single template type on demand.
        /// This is called incrementally during extraction to avoid loading everything into memory at once.
        /// </summary>
        private List<UnityEngine.Object> LoadTypeObjects(Type templateType)
        {
            List<UnityEngine.Object> objects = null;
            string loadMethodUsed = null;

            // Strategy 1: DataTemplateLoader.GetAll<T>()
            if (_getAllMethod != null)
            {
                try
                {
                    var getAllGeneric = _getAllMethod.MakeGenericMethod(templateType);
                    var collection = getAllGeneric.Invoke(null, null);
                    if (collection != null)
                    {
                        objects = EnumerateIl2CppCollection(collection);
                        if (objects.Count > 0)
                            loadMethodUsed = "DataTemplateLoader.GetAll";
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"    GetAll<{templateType.Name}> failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            // Strategy 2: Special handling for ConversationTemplate
            if ((objects == null || objects.Count == 0) && templateType.Name == "ConversationTemplate")
            {
                try
                {
                    var loadUncached = templateType.GetMethod("LoadAllUncached",
                        BindingFlags.Public | BindingFlags.Static);
                    if (loadUncached != null)
                    {
                        var loadResult = loadUncached.Invoke(null, null);
                        if (loadResult != null)
                        {
                            objects = EnumerateIl2CppCollection(loadResult);
                            if (objects.Count > 0)
                                loadMethodUsed = "LoadAllUncached";
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"    ConversationTemplate.LoadAllUncached failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            // Strategy 3: Resources.LoadAll with GetBaseFolder path
            string basePath = null;
            if ((objects == null || objects.Count == 0) && _getBaseFolderMethod != null && _loadAllMethod != null)
            {
                try
                {
                    basePath = _getBaseFolderMethod.Invoke(null, new object[] { templateType }) as string;
                    if (!string.IsNullOrEmpty(basePath))
                    {
                        var loadAllGeneric = _loadAllMethod.MakeGenericMethod(templateType);
                        var loadResult = loadAllGeneric.Invoke(null, new object[] { basePath });
                        if (loadResult != null)
                        {
                            objects = EnumerateIl2CppCollection(loadResult);
                            if (objects.Count > 0)
                                loadMethodUsed = $"Resources.LoadAll(\"{basePath}\")";
                            else
                                DebugLog($"    {templateType.Name}: Resources.LoadAll(\"{basePath}\") returned 0 objects");
                        }
                    }
                    else
                    {
                        DebugLog($"    {templateType.Name}: GetBaseFolder returned null/empty (no Resources path)");
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"    {templateType.Name} Resources.LoadAll failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            // Strategy 4: FindObjectsOfTypeAll (finds already-loaded objects)
            if ((objects == null || objects.Count == 0) && _findObjectsMethod != null)
            {
                try
                {
                    var findGeneric = _findObjectsMethod.MakeGenericMethod(templateType);
                    var findResult = findGeneric.Invoke(null, null);
                    if (findResult != null)
                    {
                        objects = EnumerateIl2CppCollection(findResult);
                        if (objects.Count > 0)
                            loadMethodUsed = "FindObjectsOfTypeAll";
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"    {templateType.Name} FindObjectsOfTypeAll failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            // Cache IL2CPP class pointer from first valid object
            if (objects != null && objects.Count > 0)
            {
                foreach (var obj in objects)
                {
                    if (obj is Il2CppObjectBase il2cppBase && il2cppBase.Pointer != IntPtr.Zero)
                    {
                        try
                        {
                            IntPtr klass = IL2CPP.il2cpp_object_get_class(il2cppBase.Pointer);
                            if (klass != IntPtr.Zero)
                            {
                                _il2cppClassPtrCache[templateType.Name] = klass;
                                break;
                            }
                        }
                        catch { }
                    }
                }
                DebugLog($"  Loaded {objects.Count} {templateType.Name} instances via {loadMethodUsed}");
            }
            else
            {
                // Log why this type had no instances
                DebugLog($"  {templateType.Name}: NO INSTANCES FOUND");
                DebugLog($"    - GetAll: returned 0");
                DebugLog($"    - Resources path: {basePath ?? "(none)"}");
                DebugLog($"    - FindObjectsOfTypeAll: returned 0");
                DebugLog($"    This template may only be loaded in a specific game state (e.g., combat)");
            }

            return objects ?? new List<UnityEngine.Object>();
        }

        /// <summary>
        /// Phase 1: Extract only primitive/safe properties from pre-collected objects.
        /// No per-type FindObjectsOfTypeAll calls. No IL2CPP property getters.
        /// </summary>
        private TypeContext ExtractTypePhase1(Type templateType, List<UnityEngine.Object> objects)
        {
            LoggerInstance.Msg($"    P1 entering loop for {templateType.Name}...");
            DebugLog($"  {objects.Count} instances to extract");
            var typeCtx = new TypeContext { TemplateType = templateType };

            // Get the cached IL2CPP class pointer (captured during classification)
            // This avoids calling il2cpp_object_get_class on potentially-stale object pointers
            IntPtr cachedKlass = IntPtr.Zero;
            _il2cppClassPtrCache.TryGetValue(templateType.Name, out cachedKlass);
            LoggerInstance.Msg($"    P1 cachedKlass=0x{cachedKlass.ToInt64():X}");

            for (int i = 0; i < objects.Count; i++)
            {
                var obj = objects[i];
                if (obj == null) { DebugLog($"  [{i}] null, skip"); continue; }

                // Get the IL2CPP pointer — if zero, object is invalid
                Il2CppObjectBase il2cppCheck = null;
                try
                {
                    il2cppCheck = obj as Il2CppObjectBase;
                }
                catch (Exception castEx)
                {
                    LoggerInstance.Msg($"    [{i}] Il2CppObjectBase cast exception: {castEx.Message}");
                    continue;
                }

                if (il2cppCheck == null || il2cppCheck.Pointer == IntPtr.Zero)
                {
                    DebugLog($"  [{i}] zero pointer, skip");
                    continue;
                }

                // Check m_CachedPtr using cached class (no il2cpp_object_get_class call)
                IntPtr objPointer = il2cppCheck.Pointer;
                if (cachedKlass != IntPtr.Zero)
                {
                    try
                    {
                        if (!IsUnityObjectAliveWithClass(objPointer, cachedKlass))
                        {
                            DebugLog($"  [{i}] destroyed (m_CachedPtr=0), skip");
                            continue;
                        }
                    }
                    catch (Exception aliveEx)
                    {
                        LoggerInstance.Msg($"    [{i}] IsUnityObjectAliveWithClass exception: {aliveEx.Message}");
                        continue;
                    }
                }

                // Read the name via direct memory using cached class
                string objName = null;
                if (cachedKlass != IntPtr.Zero)
                {
                    try
                    {
                        objName = ReadObjectNameWithClass(objPointer, cachedKlass);
                    }
                    catch (Exception nameEx)
                    {
                        LoggerInstance.Msg($"    [{i}] ReadObjectNameWithClass exception: {nameEx.Message}");
                        objName = $"unknown_{i}";
                    }
                }

                if (objName == null) objName = $"unknown_{i}";

                // Log every 50 instances to console, or every instance for last 20
                if (i % 50 == 0 || i >= objects.Count - 20)
                    LoggerInstance.Msg($"    [{i}/{objects.Count}] {objName}...");
                DebugLog($"  [{i}] {objName}");

                InstanceContext instCtx = null;
                try
                {
                    instCtx = ExtractPrimitives(obj, templateType, objName);
                    if (i >= objects.Count - 20)
                        LoggerInstance.Msg($"      [{i}] ExtractPrimitives done, instCtx={(instCtx != null ? "OK" : "null")}");
                    if (instCtx != null)
                    {
                        // Use m_ID as the canonical name if available (more reliable
                        // than ReadObjectNameDirect which can't read Unity native properties)
                        if (instCtx.Data.TryGetValue("m_ID", out var idVal) && idVal is string idStr && !string.IsNullOrEmpty(idStr))
                        {
                            instCtx.Name = idStr;
                            instCtx.Data["name"] = idStr;
                        }
                        // Fallback for types without m_ID (e.g. ConversationTemplate):
                        // try Path field, then Unity Object.name
                        else if (instCtx.Data.TryGetValue("Path", out var pathVal) && pathVal is string pathStr && !string.IsNullOrEmpty(pathStr))
                        {
                            instCtx.Name = pathStr;
                            instCtx.Data["name"] = pathStr;
                        }
                        else if (instCtx.Name != null && instCtx.Name.StartsWith("unknown_") && instCtx.Pointer != IntPtr.Zero)
                        {
                            try
                            {
                                var nameObj = new UnityEngine.Object(instCtx.Pointer);
                                string unityName = nameObj.name;
                                if (!string.IsNullOrEmpty(unityName))
                                {
                                    instCtx.Name = unityName;
                                    instCtx.Data["name"] = unityName;
                                }
                            }
                            catch { }
                        }
                        typeCtx.Instances.Add(instCtx);
                        if (i >= objects.Count - 20)
                            LoggerInstance.Msg($"      [{i}] added to typeCtx");
                    }
                }
                catch (Exception ex)
                {
                    LoggerInstance.Msg($"    [{i}] P1 EXCEPTION: {ex.Message}");
                    DebugLog($"  [{i}] P1 FAILED: {ex.Message}");
                }
            }

            LoggerInstance.Msg($"    P1 complete: {typeCtx.Instances.Count} instances extracted");
            return typeCtx;
        }

        /// <summary>
        /// Extract only non-reference properties from a template instance.
        /// Uses direct memory reads via IL2CPP field offsets — completely bypasses
        /// property getters to avoid native SIGSEGV crashes.
        /// Returns an InstanceContext with the data dict and cast object for Phase 2.
        /// </summary>
        private InstanceContext ExtractPrimitives(UnityEngine.Object obj, Type templateType, string objName)
        {
            DebugLog($"    TryCast<{templateType.Name}>...");
            object castObj = null;
            try
            {
                var genericTryCast = _tryCastMethod.MakeGenericMethod(templateType);
                castObj = genericTryCast.Invoke(obj, null);
            }
            catch (Exception ex)
            {
                DebugLog($"    TryCast failed: {ex.Message}");
                return null;
            }

            if (castObj == null)
            {
                DebugLog($"    TryCast returned null");
                return null;
            }

            var il2cppBase = castObj as Il2CppObjectBase;
            if (il2cppBase == null || il2cppBase.Pointer == IntPtr.Zero)
            {
                DebugLog($"    Invalid pointer after TryCast");
                return null;
            }

            // Use cached class pointer from classification (no il2cpp_object_get_class on data pointer)
            IntPtr klass = IntPtr.Zero;
            _il2cppClassPtrCache.TryGetValue(templateType.Name, out klass);
            DebugLog($"    TryCast OK ptr={il2cppBase.Pointer} klass={klass}, reading primitives (direct)...");

            var data = new Dictionary<string, object>();
            data["name"] = objName;

            var currentType = templateType;
            while (currentType != null && !StopBaseTypes.Contains(currentType.Name))
            {
                PropertyInfo[] props;
                try
                {
                    props = currentType.GetProperties(
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                }
                catch
                {
                    currentType = currentType.BaseType;
                    continue;
                }

                foreach (var prop in props)
                {
                    if (!prop.CanRead) continue;
                    if (SkipProperties.Contains(prop.Name)) continue;
                    if (data.ContainsKey(prop.Name)) continue;
                    if (ShouldSkipPropertyType(prop)) continue;

                    // Skip IL2CPP reference properties — Phase 2 handles these
                    if (IsIl2CppReferenceProperty(prop))
                        continue;

                    DebugLog($"    .{prop.Name} ({prop.PropertyType.Name})");

                    // Read field value directly from memory (no property getter calls)
                    if (klass != IntPtr.Zero)
                    {
                        object directValue = TryReadFieldDirect(
                            klass, templateType.Name, prop.Name, prop.PropertyType, il2cppBase.Pointer);
                        if (directValue != _skipSentinel)
                        {
                            data[prop.Name] = directValue;
                            continue;
                        }
                    }

                    // Field not found or type not supported for direct read — skip
                    // (Do NOT fall back to property getter — that's what causes crashes)
                    DebugLog($"      -> skipped (no direct read)");
                }

                currentType = currentType.BaseType;
            }

            DebugLog($"    Done: {data.Count} primitive fields");
            return new InstanceContext
            {
                Data = data,
                CastObj = castObj,
                Pointer = il2cppBase.Pointer,
                Name = objName
            };
        }

        /// <summary>
        /// Look up a native field by name (trying multiple naming conventions)
        /// and cache the result. Returns (fieldPtr, offset).
        /// </summary>
        private (IntPtr field, uint offset) GetCachedFieldInfo(IntPtr klass, string typeName, string propName)
        {
            if (!_fieldInfoCache.TryGetValue(typeName, out var typeCache))
            {
                typeCache = new Dictionary<string, (IntPtr, uint)>();
                _fieldInfoCache[typeName] = typeCache;
            }

            if (typeCache.TryGetValue(propName, out var cached))
                return cached;

            // Look up the field in the native class (tries multiple naming conventions)
            IntPtr field = FindNativeField(klass, propName);
            uint offset = 0;

            if (field != IntPtr.Zero)
            {
                DebugLog($"      [cache] il2cpp_field_get_offset({typeName}.{propName})...");
                offset = IL2CPP.il2cpp_field_get_offset(field);
                DebugLog($"      [cache] offset={offset}");
            }
            else
            {
                DebugLog($"      [cache] field not found for {typeName}.{propName}");
            }

            var result = (field, offset);
            typeCache[propName] = result;
            return result;
        }

        /// <summary>
        /// Find a native IL2CPP field by name, trying several naming conventions.
        /// Walks parent classes because fields may be defined on a base type.
        /// </summary>
        private IntPtr FindNativeField(IntPtr klass, string propName)
        {
            string[] namesToTry = new[]
            {
                propName,
                propName.Length > 0 && char.IsUpper(propName[0])
                    ? char.ToLower(propName[0]) + propName.Substring(1) : null,
                "_" + propName,
                "m_" + propName,
                $"<{propName}>k__BackingField"
            };

            IntPtr searchKlass = klass;
            while (searchKlass != IntPtr.Zero)
            {
                foreach (var name in namesToTry)
                {
                    if (name == null) continue;
                    IntPtr field = IL2CPP.il2cpp_class_get_field_from_name(searchKlass, name);
                    if (field != IntPtr.Zero) return field;
                }
                searchKlass = IL2CPP.il2cpp_class_get_parent(searchKlass);
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Read a field value directly from the object's memory at the field's offset.
        /// Completely bypasses IL2CPP interop property getters.
        /// Returns _skipSentinel if the field can't be found or the type isn't supported.
        /// </summary>
        private object TryReadFieldDirect(IntPtr klass, string typeName, string propName, Type propType, IntPtr objectPointer)
        {
            try
            {
                var (field, offset) = GetCachedFieldInfo(klass, typeName, propName);

                if (field == IntPtr.Zero || offset == 0)
                    return _skipSentinel;

                IntPtr addr = objectPointer + (int)offset;

                // Check IL2CPP native type to catch float/double mismatches
                // IL2CppInterop sometimes maps float fields to double properties
                IntPtr fieldType = IL2CPP.il2cpp_field_get_type(field);
                if (fieldType != IntPtr.Zero)
                {
                    int nativeType = IL2CPP.il2cpp_type_get_type(fieldType);
                    // IL2CPP says float (R4) but IL2CppInterop reflected type says double — trust IL2CPP
                    if (nativeType == 11 && propType == typeof(double))
                        return BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(addr)), 0);
                    // IL2CPP says double (R8) but reflected type says float — trust C# type,
                    // read 4 bytes as float. IL2CPP metadata misreports float fields as R8.
                    if (nativeType == 12 && propType == typeof(float))
                        return ReadFloat(addr);
                }

                // Primitive types — direct Marshal reads
                if (propType == typeof(int))
                    return Marshal.ReadInt32(addr);
                if (propType == typeof(uint))
                    return (int)(uint)Marshal.ReadInt32(addr); // store as int for JSON
                if (propType == typeof(float))
                    return BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(addr)), 0);
                if (propType == typeof(bool))
                    return Marshal.ReadByte(addr) != 0;
                if (propType == typeof(byte))
                    return (int)Marshal.ReadByte(addr);
                if (propType == typeof(short))
                    return (int)Marshal.ReadInt16(addr);
                if (propType == typeof(ushort))
                    return (int)(ushort)Marshal.ReadInt16(addr);
                if (propType == typeof(long))
                    return Marshal.ReadInt64(addr);
                if (propType == typeof(ulong))
                    return (long)(ulong)Marshal.ReadInt64(addr);
                if (propType == typeof(double))
                    return ReadDoubleValidated(addr);

                // String — read the Il2CppString pointer, then convert
                if (propType == typeof(string))
                {
                    IntPtr strPtr = Marshal.ReadIntPtr(addr);
                    if (strPtr == IntPtr.Zero) return null;
                    return IL2CPP.Il2CppStringToManaged(strPtr);
                }

                // Enum — read based on underlying type
                if (propType.IsEnum)
                {
                    var underlying = Enum.GetUnderlyingType(propType);
                    if (underlying == typeof(int))
                        return Marshal.ReadInt32(addr);
                    if (underlying == typeof(byte))
                        return (int)Marshal.ReadByte(addr);
                    if (underlying == typeof(short))
                        return (int)Marshal.ReadInt16(addr);
                    if (underlying == typeof(long))
                        return Marshal.ReadInt64(addr);
                    return Marshal.ReadInt32(addr); // default
                }

                // Unity struct types — read component floats directly
                string typeName2 = propType.Name;
                if (typeName2 == "Vector2")
                {
                    return new Dictionary<string, object>
                    {
                        { "x", ReadFloat(addr) },
                        { "y", ReadFloat(addr + 4) }
                    };
                }
                if (typeName2 == "Vector3")
                {
                    return new Dictionary<string, object>
                    {
                        { "x", ReadFloat(addr) },
                        { "y", ReadFloat(addr + 4) },
                        { "z", ReadFloat(addr + 8) }
                    };
                }
                if (typeName2 == "Vector4" || typeName2 == "Quaternion")
                {
                    return new Dictionary<string, object>
                    {
                        { "x", ReadFloat(addr) },
                        { "y", ReadFloat(addr + 4) },
                        { "z", ReadFloat(addr + 8) },
                        { "w", ReadFloat(addr + 12) }
                    };
                }
                if (typeName2 == "Color")
                {
                    return new Dictionary<string, object>
                    {
                        { "r", ReadFloat(addr) },
                        { "g", ReadFloat(addr + 4) },
                        { "b", ReadFloat(addr + 8) },
                        { "a", ReadFloat(addr + 12) }
                    };
                }
                if (typeName2 == "Rect")
                {
                    return new Dictionary<string, object>
                    {
                        { "x", ReadFloat(addr) },
                        { "y", ReadFloat(addr + 4) },
                        { "width", ReadFloat(addr + 8) },
                        { "height", ReadFloat(addr + 12) }
                    };
                }
                if (typeName2 == "Vector2Int")
                {
                    return new Dictionary<string, object>
                    {
                        { "x", Marshal.ReadInt32(addr) },
                        { "y", Marshal.ReadInt32(addr + 4) }
                    };
                }
                if (typeName2 == "Vector3Int")
                {
                    return new Dictionary<string, object>
                    {
                        { "x", Marshal.ReadInt32(addr) },
                        { "y", Marshal.ReadInt32(addr + 4) },
                        { "z", Marshal.ReadInt32(addr + 8) }
                    };
                }
                if (typeName2 == "Color32")
                {
                    return new Dictionary<string, object>
                    {
                        { "r", (int)Marshal.ReadByte(addr) },
                        { "g", (int)Marshal.ReadByte(addr + 1) },
                        { "b", (int)Marshal.ReadByte(addr + 2) },
                        { "a", (int)Marshal.ReadByte(addr + 3) }
                    };
                }

                // Check if this is a value type struct we can read via IL2CPP metadata
                if (propType.IsValueType && !propType.IsPrimitive && !propType.IsEnum)
                {
                    // Get the IL2CPP type info for this field
                    IntPtr structFieldType = IL2CPP.il2cpp_field_get_type(field);
                    if (structFieldType != IntPtr.Zero)
                    {
                        int typeEnum = IL2CPP.il2cpp_type_get_type(structFieldType);
                        if (typeEnum == 17) // IL2CPP_TYPE_VALUETYPE
                        {
                            // Read the struct fields recursively
                            object structValue = ReadValueTypeField(addr, structFieldType, 0);
                            if (structValue != null)
                                return structValue;
                        }
                    }
                }

                // Type not supported for direct read
                return _skipSentinel;
            }
            catch (Exception ex)
            {
                DebugLog($"      direct-read error: {ex.GetType().Name}: {ex.Message}");
                return _skipSentinel;
            }
        }

        private static float ReadFloat(IntPtr addr)
        {
            return BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(addr)), 0);
        }

        /// <summary>
        /// Read a double from memory with validation.
        /// IL2CPP metadata sometimes reports float fields as R8 (double). When this happens,
        /// reading 8 bytes produces corrupted doubles (e.g. float 1.0 → 5.26E-315, 0.0078125, etc.).
        /// Detect these invalid values and re-read as 4-byte float instead.
        /// </summary>
        private static object ReadDoubleValidated(IntPtr addr)
        {
            double d = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(addr));
            if (d == 0.0) return d;
            // Subnormal, NaN, or Infinity doubles are never valid game data
            if (!double.IsNormal(d))
                return (double)ReadFloat(addr);
            // Extremely small normal doubles (< 1e-100) are also almost certainly
            // corrupted floats — game stat values never use such magnitudes.
            // E.g. 7.748E-304 = float(1.0) + 4 adjacent bytes read as 8-byte double
            if (Math.Abs(d) < 1e-100)
                return (double)ReadFloat(addr);
            return d;
        }

        /// <summary>
        /// Check if a Unity Object is still alive by reading its m_CachedPtr field.
        /// Unity sets m_CachedPtr to zero when an object is destroyed.
        /// If m_CachedPtr is zero, ANY property access (including .name) will crash.
        /// </summary>
        private bool IsUnityObjectAlive(IntPtr objectPointer)
        {
            try
            {
                IntPtr klass = IL2CPP.il2cpp_object_get_class(objectPointer);
                if (klass == IntPtr.Zero) return false;

                IntPtr cachedPtrField = IL2CPP.il2cpp_class_get_field_from_name(klass, "m_CachedPtr");
                if (cachedPtrField == IntPtr.Zero) return true; // can't check, assume alive

                uint offset = IL2CPP.il2cpp_field_get_offset(cachedPtrField);
                if (offset == 0) return true; // can't check, assume alive

                IntPtr nativePtr = Marshal.ReadIntPtr(objectPointer + (int)offset);
                return nativePtr != IntPtr.Zero;
            }
            catch
            {
                return false; // error reading = treat as dead
            }
        }

        /// <summary>
        /// Read a Menace template object's name directly from memory.
        /// Unity's Object.name has no backing field in IL2CPP (it's a native engine property),
        /// so we use m_ID from Menace.Tools.DataTemplate which all templates inherit.
        /// </summary>
        private string ReadObjectNameDirect(IntPtr objectPointer)
        {
            try
            {
                IntPtr klass = IL2CPP.il2cpp_object_get_class(objectPointer);
                if (klass == IntPtr.Zero) return null;

                // m_ID is on Menace.Tools.DataTemplate — FindNativeField walks parents
                IntPtr idField = FindNativeField(klass, "m_ID");
                if (idField != IntPtr.Zero)
                {
                    uint offset = IL2CPP.il2cpp_field_get_offset(idField);
                    if (offset != 0)
                    {
                        IntPtr strPtr = Marshal.ReadIntPtr(objectPointer + (int)offset);
                        if (strPtr != IntPtr.Zero)
                        {
                            string id = IL2CPP.Il2CppStringToManaged(strPtr);
                            if (!string.IsNullOrEmpty(id))
                                return id;
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Phase 2: Fill in IL2CPP reference properties for an already-extracted instance.
        /// Uses direct memory reads — NO property getters are called.
        /// Returns true if any reference properties were added.
        /// </summary>
        // Track which instance we're processing for crash debugging
        private string _currentFillRefInstance = null;
        private string _currentFillRefProp = null;

        private bool FillReferenceProperties(InstanceContext inst, Type templateType)
        {
            _currentFillRefInstance = inst?.Name ?? "null";
            _currentFillRefProp = "entry";

            try
            {
                if (inst == null || inst.Pointer == IntPtr.Zero)
                    return false;

                // Check if the Il2Cpp object was garbage collected between Phase 1 and Phase 2.
                // Reading from a collected object's pointer causes AccessViolationException.
                // Wrap in try-catch since modded objects may have invalid CastObj references
                try
                {
                    if (inst.CastObj is Il2CppObjectBase il2cppObj && il2cppObj.WasCollected)
                    {
                        DebugLog($"    [{inst.Name}] object was garbage collected, skipping");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"    [{inst.Name}] failed GC check (modded template?): {ex.Message}, attempting anyway");
                    // Continue - modded templates may not have valid CastObj but pointer might still work
                }

                // Use cached class pointer (no il2cpp_object_get_class on data pointer)
                IntPtr klass = IntPtr.Zero;
                _il2cppClassPtrCache.TryGetValue(templateType.Name, out klass);
                if (klass == IntPtr.Zero)
                {
                    DebugLog($"    [{inst.Name}] no cached class pointer for {templateType.Name}");
                    return false;
                }

            bool addedAny = false;
            var currentType = templateType;

            while (currentType != null && !StopBaseTypes.Contains(currentType.Name))
            {
                PropertyInfo[] props;
                try
                {
                    props = currentType.GetProperties(
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                }
                catch
                {
                    currentType = currentType.BaseType;
                    continue;
                }

                foreach (var prop in props)
                {
                    if (!prop.CanRead) continue;
                    if (SkipProperties.Contains(prop.Name)) continue;
                    if (inst.Data.ContainsKey(prop.Name)) continue;
                    if (ShouldSkipPropertyType(prop)) continue;

                    // Only handle reference properties in this phase
                    if (!IsIl2CppReferenceProperty(prop))
                        continue;

                    _currentFillRefProp = prop.Name;
                    DebugLog($"    [{inst.Name}].{prop.Name} ({prop.PropertyType.Name})");

                    // Get the IL2CPP field + offset
                    IntPtr field;
                    uint offset;
                    try
                    {
                        (field, offset) = GetCachedFieldInfo(klass, templateType.Name, prop.Name);
                    }
                    catch { continue; }
                    if (field == IntPtr.Zero || offset == 0)
                    {
                        DebugLog($"      -> field not found, skip");
                        continue;
                    }

                    // Get field type from IL2CPP METADATA (safe — reads type tables, not object memory)
                    IntPtr fieldType;
                    int typeEnum;
                    try
                    {
                        fieldType = IL2CPP.il2cpp_field_get_type(field);
                        if (fieldType == IntPtr.Zero) { DebugLog($"      -> no type info"); continue; }
                        typeEnum = IL2CPP.il2cpp_type_get_type(fieldType);
                    }
                    catch { continue; }

                    // Read the raw pointer stored in the field
                    IntPtr refPtr;
                    try
                    {
                        refPtr = Marshal.ReadIntPtr(inst.Pointer + (int)offset);
                    }
                    catch { continue; }
                    if (refPtr == IntPtr.Zero)
                    {
                        inst.Data[prop.Name] = null;
                        DebugLog($"      -> null");
                        addedAny = true;
                        continue;
                    }

                    DebugLog($"      typeEnum={typeEnum} refPtr={refPtr}");

                    // Route based on IL2CPP type enum — all classification from metadata, no pointer dereference
                    if (typeEnum == 29) // IL2CPP_TYPE_SZARRAY — single-dimension array
                    {
                        object arrayVal = ReadArrayFromFieldMetadata(refPtr, fieldType, 0);
                        if (arrayVal != null)
                        {
                            inst.Data[prop.Name] = arrayVal;
                            addedAny = true;
                        }
                        DebugLog($"      -> array ({(arrayVal is List<object> l ? l.Count + " items" : "error")})");
                    }
                    else if (typeEnum == 18 || typeEnum == 21) // IL2CPP_TYPE_CLASS or IL2CPP_TYPE_GENERICINST
                    {
                        // Get the expected class from metadata (safe)
                        IntPtr expectedClass = IL2CPP.il2cpp_class_from_type(fieldType);
                        if (expectedClass == IntPtr.Zero) { DebugLog($"      -> no class"); continue; }

                        string className = GetClassNameSafe(expectedClass);
                        DebugLog($"      expected class: {className}");

                        if (IsLocalizationClass(className, expectedClass))
                        {
                            // Localization string — read m_DefaultTranslation using known class
                            string locText = ReadLocalizedStringWithClass(refPtr, expectedClass);
                            if (locText != null)
                            {
                                inst.Data[prop.Name] = locText;
                                if (prop.Name == "Title" && !inst.Data.ContainsKey("DisplayTitle"))
                                    inst.Data["DisplayTitle"] = locText;
                                else if (prop.Name == "ShortName" && !inst.Data.ContainsKey("DisplayShortName"))
                                    inst.Data["DisplayShortName"] = locText;
                                else if (prop.Name == "Description" && !inst.Data.ContainsKey("DisplayDescription"))
                                    inst.Data["DisplayDescription"] = locText;
                                addedAny = true;
                                DebugLog($"      -> localized: {(locText.Length > 60 ? locText[..60] + "..." : locText)}");
                            }
                            else
                            {
                                DebugLog($"      -> localization read failed");
                            }
                        }
                        else if (IsUnityObjectClass(expectedClass))
                        {
                            // Unity Object — check alive using known class, then read name
                            // Uses graduated name-reading: m_ID -> m_Name -> Unity .name
                            if (IsUnityObjectAliveWithClass(refPtr, expectedClass))
                            {
                                string assetName = ReadUnityAssetNameWithClass(refPtr, expectedClass, className);
                                if (prop.Name == "Icon")
                                {
                                    inst.Data["HasIcon"] = true;
                                    if (assetName != null)
                                        inst.Data["IconAssetName"] = assetName;
                                    DebugLog($"      -> HasIcon=true, name={assetName ?? "(unknown)"}");
                                }
                                else if (ShouldExtractTemplateInline(className))
                                {
                                    // Template type not extracted standalone — include field data inline
                                    DebugLog($"      -> reading inline template ({className})...");
                                    var nested = ReadNestedObjectDirect(refPtr, expectedClass, className, 0);
                                    DebugLog($"      -> inline template ({className}) read complete");
                                    if (nested is Dictionary<string, object> nestedDict)
                                    {
                                        nestedDict["name"] = assetName ?? className;
                                        inst.Data[prop.Name] = nestedDict;
                                    }
                                    else
                                    {
                                        inst.Data[prop.Name] = assetName ?? $"({className})";
                                    }
                                    DebugLog($"      -> inline template ({className}) done");
                                }
                                else
                                {
                                    inst.Data[prop.Name] = assetName ?? $"({className})";
                                    DebugLog($"      -> {inst.Data[prop.Name]}");
                                }
                                addedAny = true;
                            }
                            else
                            {
                                if (prop.Name == "Icon")
                                    inst.Data["HasIcon"] = false;
                                else
                                    inst.Data[prop.Name] = null;
                                addedAny = true;
                                DebugLog($"      -> dead object");
                            }
                        }
                        else if (IsIl2CppListClass(className, expectedClass))
                        {
                            // IL2CPP List<T> — read contents via _items array + _size
                            object listContents = ReadIl2CppListDirect(refPtr, expectedClass, 0);
                            inst.Data[prop.Name] = listContents;
                            addedAny = true;
                            DebugLog($"      -> list ({(listContents is List<object> lc ? lc.Count + " items" : "empty/error")})");
                        }
                        else
                        {
                            // Nested non-Unity object — read fields using metadata-known class
                            object nested = ReadNestedObjectDirect(refPtr, expectedClass, className, 0);
                            inst.Data[prop.Name] = nested;
                            addedAny = true;
                            DebugLog($"      -> nested ({className})");
                        }
                    }
                    else
                    {
                        DebugLog($"      -> unhandled typeEnum {typeEnum}");
                    }
                }

                currentType = currentType.BaseType;
            }

            // Schema-driven extraction: extract fields defined in schema but not found via reflection
            if (_schemaLoaded && _schemaTypes.TryGetValue(templateType.Name, out var schemaType))
            {
                DebugLog($"    [{inst.Name}] starting schema fields...");
                bool schemaAdded = FillSchemaFields(inst, schemaType, klass);
                DebugLog($"    [{inst.Name}] schema fields done");
                addedAny = addedAny || schemaAdded;
            }

            DebugLog($"    [{inst.Name}] FillReferenceProperties complete");
            return addedAny;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[FillReferenceProperties] Crash on {_currentFillRefInstance}.{_currentFillRefProp}: {ex.GetType().Name}: {ex.Message}");
                DebugLog($"    [{_currentFillRefInstance}] CRASHED at {_currentFillRefProp}: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Schema-driven field extraction: reads fields at schema-defined offsets
        /// that weren't found via IL2CppInterop reflection.
        /// </summary>
        private bool FillSchemaFields(InstanceContext inst, SchemaType schemaType, IntPtr klass)
        {
            if (inst.Pointer == IntPtr.Zero) return false;

            bool addedAny = false;
            foreach (var field in schemaType.Fields)
            {
                // Skip fields already extracted
                if (inst.Data.ContainsKey(field.Name)) continue;
                if (field.Offset == 0) continue;

                DebugLog($"    [schema] {inst.Name}.{field.Name} offset=0x{field.Offset:X} type={field.Type} cat={field.Category}");

                try
                {
                    object value = ReadSchemaField(inst.Pointer, field, klass, 0);

                    // For reference/localization/unity_asset fields, record null values explicitly
                    // (these are legitimately nullable). For other categories, null means "failed to read".
                    bool isNullableCategory = field.Category is "reference" or "localization" or "unity_asset";

                    if (value != null)
                    {
                        inst.Data[field.Name] = value;
                        addedAny = true;
                        DebugLog($"    [schema] -> extracted {field.Name}");
                    }
                    else if (isNullableCategory)
                    {
                        inst.Data[field.Name] = null;
                        addedAny = true;
                        DebugLog($"    [schema] -> recorded null {field.Name}");
                    }
                    else
                    {
                        DebugLog($"    [schema] -> null/failed");
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"    [schema] -> error: {ex.Message}");
                }
            }

            return addedAny;
        }

        /// <summary>
        /// Read a field value using schema-defined offset and type information.
        /// </summary>
        private object ReadSchemaField(IntPtr objPtr, SchemaField field, IntPtr klass, int depth)
        {
            if (depth > 8) return null;

            // Validate offset looks reasonable before reading memory
            if (field.Offset > 0x10000) return null;

            IntPtr addr = objPtr + (int)field.Offset;

            // Validate computed address looks reasonable
            if (addr.ToInt64() < 0x10000 || addr.ToInt64() > 0x7FFFFFFFFFFF) return null;

            switch (field.Category)
            {
                case "primitive":
                    return ReadSchemaPrimitive(addr, field.Type);

                case "enum":
                    return Marshal.ReadInt32(addr);

                case "localization":
                    IntPtr locPtr = Marshal.ReadIntPtr(addr);
                    if (locPtr == IntPtr.Zero) return null;
                    if (locPtr.ToInt64() < 0x10000) return null;
                    return ReadLocalizedStringDirect(locPtr);

                case "reference":
                    IntPtr refPtr = Marshal.ReadIntPtr(addr);
                    if (refPtr == IntPtr.Zero) return null;
                    // Validate pointer looks reasonable (not a small int masquerading as pointer)
                    if (refPtr.ToInt64() < 0x10000)
                        return (int)refPtr.ToInt64();
                    return ReadSchemaReference(refPtr, field.Type, depth);

                case "collection":
                    IntPtr colPtr = Marshal.ReadIntPtr(addr);
                    if (colPtr == IntPtr.Zero) return new List<object>();
                    if (colPtr.ToInt64() < 0x10000) return new List<object>(); // Invalid pointer
                    return ReadSchemaCollection(colPtr, field, depth);

                case "unity_asset":
                    IntPtr assetPtr = Marshal.ReadIntPtr(addr);
                    if (assetPtr == IntPtr.Zero) return null;
                    if (assetPtr.ToInt64() < 0x10000) return null; // Invalid pointer
                    return ReadUnityAssetName(assetPtr);

                case "struct":
                    // Handle common struct types inline
                    if (field.Type == "Vector2Int")
                    {
                        int x = Marshal.ReadInt32(addr);
                        int y = Marshal.ReadInt32(addr + 4);
                        return new Dictionary<string, object> { { "x", x }, { "y", y } };
                    }
                    if (field.Type == "Vector2")
                    {
                        float x = ReadFloat(addr);
                        float y = ReadFloat(addr + 4);
                        return new Dictionary<string, object> { { "x", x }, { "y", y } };
                    }
                    if (field.Type == "Vector3")
                    {
                        float x = ReadFloat(addr);
                        float y = ReadFloat(addr + 4);
                        float z = ReadFloat(addr + 8);
                        return new Dictionary<string, object> { { "x", x }, { "y", y }, { "z", z } };
                    }
                    // Check for schema-defined struct types (like OperationTrustChange)
                    if (_structTypes.TryGetValue(field.Type, out var structSchema))
                    {
                        return ReadStructFromSchema(addr, structSchema, depth + 1);
                    }
                    // Unknown struct - skip
                    return null;

                default:
                    return null;
            }
        }

        private object ReadSchemaPrimitive(IntPtr addr, string typeName)
        {
            return typeName switch
            {
                "int" or "Int32" => Marshal.ReadInt32(addr),
                "uint" or "UInt32" => (int)(uint)Marshal.ReadInt32(addr),
                "float" or "Single" => ReadFloat(addr),
                "double" or "Double" => ReadDoubleValidated(addr),
                "bool" or "Boolean" => Marshal.ReadByte(addr) != 0,
                "byte" or "Byte" => (int)Marshal.ReadByte(addr),
                "short" or "Int16" => (int)Marshal.ReadInt16(addr),
                "ushort" or "UInt16" => (int)(ushort)Marshal.ReadInt16(addr),
                "long" or "Int64" => Marshal.ReadInt64(addr),
                "string" or "String" => ReadIl2CppStringAt(addr),
                _ => null
            };
        }

        private object ReadSchemaReference(IntPtr refPtr, string typeName, int depth)
        {
            // Check if it's a template type with schema - extract inline with full data
            if (_schemaTypes.TryGetValue(typeName, out var templateSchema))
            {
                var result = new Dictionary<string, object>();
                string name = ReadUnityAssetName(refPtr);
                if (name != null)
                    result["name"] = name;

                // Extract fields from schema
                foreach (var field in templateSchema.Fields)
                {
                    if (field.Offset == 0) continue;
                    try
                    {
                        object value = ReadSchemaField(refPtr, field, IntPtr.Zero, depth + 1);
                        bool isNullableCategory = field.Category is "reference" or "localization" or "unity_asset";
                        if (value != null || isNullableCategory)
                            result[field.Name] = value;
                    }
                    catch { }
                }

                return result.Count > 0 ? result : (name ?? $"({typeName})");
            }

            // Check if it's an embedded class we know about
            if (_embeddedClasses.TryGetValue(typeName, out var embeddedSchema))
            {
                return ReadEmbeddedObject(refPtr, embeddedSchema, depth + 1);
            }

            // Template type without schema - return name only
            if (typeName.EndsWith("Template"))
            {
                return ReadUnityAssetName(refPtr) ?? $"({typeName})";
            }

            // Fallback: try to read as Unity object name
            return ReadUnityAssetName(refPtr);
        }

        private object ReadSchemaCollection(IntPtr colPtr, SchemaField field, int depth)
        {
            // Determine if it's a List<T> or T[]
            bool isList = field.Type.StartsWith("List<");
            string elementType = field.ElementType;

            if (isList)
            {
                return ReadSchemaList(colPtr, elementType, depth);
            }
            else
            {
                return ReadSchemaArray(colPtr, elementType, depth);
            }
        }

        private List<object> ReadSchemaList(IntPtr listPtr, string elementType, int depth)
        {
            if (depth > 8) return new List<object>();

            try
            {
                // List<T> layout: _size at some offset, _items (T[]) at another
                // Find these fields dynamically
                IntPtr listClass = IL2CPP.il2cpp_object_get_class(listPtr);
                if (listClass == IntPtr.Zero) return new List<object>();

                IntPtr sizeField = FindNativeField(listClass, "_size");
                IntPtr itemsField = FindNativeField(listClass, "_items");
                if (sizeField == IntPtr.Zero || itemsField == IntPtr.Zero)
                    return new List<object>();

                uint sizeOffset = IL2CPP.il2cpp_field_get_offset(sizeField);
                uint itemsOffset = IL2CPP.il2cpp_field_get_offset(itemsField);

                int size = Marshal.ReadInt32(listPtr + (int)sizeOffset);
                DebugLog($"          List<{elementType}>: size={size}");
                if (size <= 0) return new List<object>();
                if (size > 500)
                {
                    LoggerInstance.Warning($"          List<{elementType}>: suspicious size {size}, capping to 500");
                    size = 500;
                }

                IntPtr arrayPtr = Marshal.ReadIntPtr(listPtr + (int)itemsOffset);
                if (arrayPtr == IntPtr.Zero) return new List<object>();

                return ReadSchemaArrayElements(arrayPtr, elementType, size, depth);
            }
            catch (Exception ex)
            {
                DebugLog($"        ReadSchemaList error: {ex.Message}");
                return new List<object>();
            }
        }

        private List<object> ReadSchemaArray(IntPtr arrayPtr, string elementType, int depth)
        {
            if (depth > 8) return new List<object>();

            try
            {
                // IL2CPP array layout: [object header 2*IntPtr] [bounds IntPtr] [length int32] [elements...]
                int headerSize = IntPtr.Size * 3;
                int length = Marshal.ReadInt32(arrayPtr + headerSize);
                if (length <= 0) return new List<object>();
                if (length > 500) length = 500;

                return ReadSchemaArrayElements(arrayPtr, elementType, length, depth);
            }
            catch (Exception ex)
            {
                DebugLog($"        ReadSchemaArray error: {ex.Message}");
                return new List<object>();
            }
        }

        private List<object> ReadSchemaArrayElements(IntPtr arrayPtr, string elementType, int count, int depth)
        {
            var result = new List<object>();

            // Elements offset: after header + length (aligned to IntPtr)
            int headerSize = IntPtr.Size * 3;
            int elementsOffset = headerSize + IntPtr.Size;

            // Check if element type is a known embedded class
            bool isEmbeddedClass = _embeddedClasses.TryGetValue(elementType, out var embeddedSchema);
            bool isTemplateRef = _schemaTypes.ContainsKey(elementType) || elementType.EndsWith("Template");
            bool isPrimitive = IsPrimitiveTypeName(elementType);
            bool isEffectHandler = elementType == "SkillEventHandlerTemplate";

            for (int i = 0; i < count; i++)
            {
                try
                {
                    IntPtr elemPtr = Marshal.ReadIntPtr(arrayPtr + elementsOffset + i * IntPtr.Size);
                    if (elemPtr == IntPtr.Zero)
                    {
                        result.Add(null);
                        continue;
                    }

                    if (isEffectHandler)
                    {
                        // Polymorphic effect handler - detect type and parse using schema
                        var handlerData = ReadEffectHandler(elemPtr, depth + 1);
                        result.Add(handlerData);
                    }
                    else if (isEmbeddedClass)
                    {
                        // Validate pointer looks reasonable before reading
                        if (elemPtr.ToInt64() < 0x10000)
                        {
                            DebugLog($"        element[{i}]: suspicious pointer {elemPtr}");
                            result.Add($"({elementType})");
                            continue;
                        }
                        var obj = ReadEmbeddedObject(elemPtr, embeddedSchema, depth + 1);
                        result.Add(obj);
                    }
                    else if (isTemplateRef)
                    {
                        result.Add(ReadUnityAssetName(elemPtr));
                    }
                    else if (isPrimitive)
                    {
                        // For primitive arrays, elements are inline (not pointers)
                        // This case shouldn't happen for reference arrays
                        result.Add(elemPtr.ToString());
                    }
                    else
                    {
                        // Unknown type - try to read as Unity object name
                        result.Add(ReadUnityAssetName(elemPtr) ?? $"({elementType})");
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"        element[{i}] error: {ex.Message}");
                    result.Add($"(error: {elementType})");
                }
            }

            return result;
        }

        /// <summary>
        /// Read a polymorphic effect handler by detecting its type and parsing fields via IL2CPP reflection.
        /// </summary>
        private object ReadEffectHandler(IntPtr handlerPtr, int depth)
        {
            if (depth > 8) return "(SkillEventHandlerTemplate)";

            try
            {
                // Validate pointer
                if (handlerPtr.ToInt64() < 0x10000)
                {
                    DebugLog($"        effect handler: suspicious pointer {handlerPtr}");
                    return "(SkillEventHandlerTemplate)";
                }

                // Get IL2CPP class info
                IntPtr klass = IL2CPP.il2cpp_object_get_class(handlerPtr);
                if (klass == IntPtr.Zero)
                {
                    DebugLog($"        effect handler: failed to get class at {handlerPtr}");
                    return "(SkillEventHandlerTemplate)";
                }

                IntPtr classNamePtr = IL2CPP.il2cpp_class_get_name(klass);
                string typeName = classNamePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(classNamePtr) : "Unknown";

                var result = new Dictionary<string, object>
                {
                    ["_type"] = typeName
                };

                // Fields to skip (inherited from base classes, internal Unity fields)
                var skipFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "m_CachedPtr", "m_InstanceID", "m_UnityRuntimeErrorString",
                    "m_ObjectHideFlags", "m_ID", "m_LocalizedStrings",
                    "Pointer", "WasCollected", "ObjectClass"
                };

                // Walk class hierarchy and read fields
                IntPtr walkKlass = klass;
                while (walkKlass != IntPtr.Zero)
                {
                    IntPtr iter = IntPtr.Zero;
                    IntPtr field;
                    while ((field = IL2CPP.il2cpp_class_get_fields(walkKlass, ref iter)) != IntPtr.Zero)
                    {
                        try
                        {
                            IntPtr fieldNamePtr = IL2CPP.il2cpp_field_get_name(field);
                            if (fieldNamePtr == IntPtr.Zero) continue;
                            string fieldName = Marshal.PtrToStringAnsi(fieldNamePtr);
                            if (string.IsNullOrEmpty(fieldName)) continue;
                            if (skipFields.Contains(fieldName)) continue;
                            if (result.ContainsKey(fieldName)) continue;

                            uint fieldOffset = IL2CPP.il2cpp_field_get_offset(field);
                            IntPtr fieldType = IL2CPP.il2cpp_field_get_type(field);
                            if (fieldType == IntPtr.Zero) continue;

                            uint fieldAttrs = IL2CPP.il2cpp_type_get_attrs(fieldType);
                            if ((fieldAttrs & 0x10) != 0) continue; // skip static

                            int typeEnum = IL2CPP.il2cpp_type_get_type(fieldType);
                            IntPtr fieldAddr = handlerPtr + (int)fieldOffset;

                            object value = typeEnum switch
                            {
                                1 => Marshal.ReadByte(fieldAddr) != 0,           // BOOLEAN
                                2 => (int)Marshal.ReadByte(fieldAddr),            // CHAR
                                3 => (int)(sbyte)Marshal.ReadByte(fieldAddr),     // I1
                                4 => (int)Marshal.ReadByte(fieldAddr),            // U1
                                5 => (int)Marshal.ReadInt16(fieldAddr),           // I2
                                6 => (int)(ushort)Marshal.ReadInt16(fieldAddr),   // U2
                                7 => Marshal.ReadInt32(fieldAddr),                // I4
                                8 => (int)(uint)Marshal.ReadInt32(fieldAddr),     // U4
                                9 => Marshal.ReadInt64(fieldAddr),                // I8
                                10 => (long)(ulong)Marshal.ReadInt64(fieldAddr),  // U8
                                11 => ReadFloat(fieldAddr),                       // R4
                                12 => ReadFloat(fieldAddr),                       // R8 (treat as float for safety)
                                14 => ReadIl2CppStringAt(fieldAddr),              // STRING
                                // Skip complex types (CLASS, VALUETYPE, ARRAY) for now to avoid crashes
                                _ => null
                            };

                            if (value != null)
                                result[fieldName] = value;
                        }
                        catch
                        {
                            // Skip fields that fail to read
                        }
                    }

                    // Walk to parent class
                    walkKlass = IL2CPP.il2cpp_class_get_parent(walkKlass);
                    if (walkKlass != IntPtr.Zero)
                    {
                        IntPtr parentNamePtr = IL2CPP.il2cpp_class_get_name(walkKlass);
                        string parentName = parentNamePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(parentNamePtr) : "";
                        // Stop at base Unity/IL2CPP classes
                        if (parentName == "Object" || parentName == "ScriptableObject" ||
                            parentName == "MonoBehaviour" || parentName == "SkillEventHandlerTemplate")
                            break;
                    }
                }

                DebugLog($"        effect handler: {typeName} with {result.Count - 1} fields");
                return result;
            }
            catch (Exception ex)
            {
                DebugLog($"        effect handler error: {ex.Message}");
                return "(SkillEventHandlerTemplate)";
            }
        }

        private object ReadEmbeddedObject(IntPtr objPtr, SchemaType schema, int depth)
        {
            if (depth > 8) return $"({schema.Name})";

            var result = new Dictionary<string, object>();

            // For ScriptableObject-derived types, validate pointer and try to get name
            // These have Unity object headers and need special handling
            bool isUnityObject = !string.IsNullOrEmpty(schema.BaseClass) &&
                                 (schema.BaseClass == "ScriptableObject" ||
                                  schema.BaseClass == "SerializedScriptableObject" ||
                                  schema.BaseClass.Contains("ScriptableObject") ||
                                  schema.BaseClass == "MonoBehaviour" ||
                                  schema.BaseClass == "Component" ||
                                  schema.BaseClass == "Object" ||
                                  schema.BaseClass == "UnityEngine.Object");

            if (isUnityObject)
            {
                try
                {
                    // Validate this is a real IL2CPP object by getting its class
                    IntPtr klass = IL2CPP.il2cpp_object_get_class(objPtr);
                    if (klass == IntPtr.Zero)
                    {
                        DebugLog($"        {schema.Name}: invalid object pointer");
                        return $"({schema.Name})";
                    }

                    // Get the object's name/ID first
                    string name = ReadUnityAssetName(objPtr);
                    if (!string.IsNullOrEmpty(name))
                        result["name"] = name;
                }
                catch (Exception ex)
                {
                    DebugLog($"        {schema.Name}: validation failed: {ex.Message}");
                    return $"({schema.Name})";
                }
            }

            // Read all fields from the schema
            foreach (var field in schema.Fields)
            {
                if (field.Offset == 0) continue;

                try
                {
                    object value = ReadSchemaField(objPtr, field, IntPtr.Zero, depth);
                    bool isNullableCategory = field.Category is "reference" or "localization" or "unity_asset";
                    if (value != null || isNullableCategory)
                        result[field.Name] = value;
                }
                catch (Exception ex)
                {
                    DebugLog($"        {schema.Name}.{field.Name}: error: {ex.Message}");
                }
            }

            return result.Count > 0 ? result : $"({schema.Name})";
        }

        /// <summary>
        /// Read a value type struct from schema. Unlike embedded objects,
        /// structs are stored inline at the address (no IL2CPP object header).
        /// </summary>
        private object ReadStructFromSchema(IntPtr addr, SchemaType schema, int depth)
        {
            if (depth > 8) return $"({schema.Name})";

            var result = new Dictionary<string, object>();

            foreach (var field in schema.Fields)
            {
                if (field.Offset == 0 && field.Name != schema.Fields[0]?.Name)
                    continue; // Skip invalid offsets (except first field which can be at 0)

                try
                {
                    IntPtr fieldAddr = addr + (int)field.Offset;
                    object value = ReadStructFieldValue(fieldAddr, field, depth);
                    bool isNullableCategory = field.Category is "reference" or "localization" or "unity_asset";
                    if (value != null || isNullableCategory)
                        result[field.Name] = value;
                }
                catch (Exception ex)
                {
                    DebugLog($"        {schema.Name}.{field.Name}: error: {ex.Message}");
                }
            }

            return result.Count > 0 ? result : $"({schema.Name})";
        }

        /// <summary>
        /// Read a single field value from a struct (stored inline, no object header).
        /// </summary>
        private object ReadStructFieldValue(IntPtr addr, SchemaField field, int depth)
        {
            string category = field.Category ?? "primitive";

            switch (category)
            {
                case "primitive":
                    return ReadSchemaPrimitive(addr, field.Type);

                case "enum":
                    return Marshal.ReadInt32(addr);

                case "struct":
                    // Nested struct - check for schema
                    if (_structTypes.TryGetValue(field.Type, out var nestedSchema))
                    {
                        return ReadStructFromSchema(addr, nestedSchema, depth + 1);
                    }
                    // Handle common Unity types
                    if (field.Type == "Vector2Int")
                    {
                        int x = Marshal.ReadInt32(addr);
                        int y = Marshal.ReadInt32(addr + 4);
                        return new Dictionary<string, object> { { "x", x }, { "y", y } };
                    }
                    if (field.Type == "Vector2")
                    {
                        float x = ReadFloat(addr);
                        float y = ReadFloat(addr + 4);
                        return new Dictionary<string, object> { { "x", x }, { "y", y } };
                    }
                    return null;

                default:
                    return null;
            }
        }

        private bool IsPrimitiveTypeName(string typeName)
        {
            return typeName switch
            {
                "int" or "Int32" or "uint" or "UInt32" or
                "float" or "Single" or "double" or "Double" or
                "bool" or "Boolean" or "byte" or "Byte" or
                "short" or "Int16" or "ushort" or "UInt16" or
                "long" or "Int64" or "string" or "String" => true,
                _ => false
            };
        }

        private string ReadLocalizedStringDirect(IntPtr locPtr)
        {
            try
            {
                // Try to find m_DefaultTranslation field
                IntPtr klass = IL2CPP.il2cpp_object_get_class(locPtr);
                if (klass == IntPtr.Zero) return null;

                IntPtr field = FindNativeField(klass, "m_DefaultTranslation");
                if (field == IntPtr.Zero) return null;

                uint offset = IL2CPP.il2cpp_field_get_offset(field);
                if (offset == 0) return null;

                IntPtr strPtr = Marshal.ReadIntPtr(locPtr + (int)offset);
                if (strPtr == IntPtr.Zero) return null;

                return IL2CPP.Il2CppStringToManaged(strPtr);
            }
            catch
            {
                return null;
            }
        }

        private string ReadUnityAssetName(IntPtr objPtr)
        {
            try
            {
                IntPtr klass = IL2CPP.il2cpp_object_get_class(objPtr);
                if (klass == IntPtr.Zero) return null;

                // Try m_ID first (DataTemplate)
                IntPtr idField = FindNativeField(klass, "m_ID");
                if (idField != IntPtr.Zero)
                {
                    uint offset = IL2CPP.il2cpp_field_get_offset(idField);
                    if (offset != 0)
                    {
                        IntPtr strPtr = Marshal.ReadIntPtr(objPtr + (int)offset);
                        if (strPtr != IntPtr.Zero)
                        {
                            string id = IL2CPP.Il2CppStringToManaged(strPtr);
                            if (!string.IsNullOrEmpty(id)) return id;
                        }
                    }
                }

                // Try m_Name (Unity Object)
                IntPtr nameField = FindNativeField(klass, "m_Name");
                if (nameField != IntPtr.Zero)
                {
                    uint offset = IL2CPP.il2cpp_field_get_offset(nameField);
                    if (offset != 0)
                    {
                        IntPtr strPtr = Marshal.ReadIntPtr(objPtr + (int)offset);
                        if (strPtr != IntPtr.Zero)
                        {
                            string name = IL2CPP.Il2CppStringToManaged(strPtr);
                            if (!string.IsNullOrEmpty(name)) return name;
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        // ── Metadata-driven helpers (never call il2cpp_object_get_class on data pointers) ──

        /// <summary>
        /// Check if a class (from metadata) is an IL2CPP List by walking the parent chain.
        /// IL2CPP generic List shows as "List`1" in metadata.
        /// </summary>
        private bool IsIl2CppListClass(string className, IntPtr klass)
        {
            if (className == "List`1") return true;

            IntPtr parent = IL2CPP.il2cpp_class_get_parent(klass);
            while (parent != IntPtr.Zero)
            {
                string pName = GetClassNameSafe(parent);
                if (pName == "List`1") return true;
                if (pName == "Object") break;
                parent = IL2CPP.il2cpp_class_get_parent(parent);
            }
            // Heuristic fallback: class has _size and _items fields (List<T> memory layout)
            IntPtr sizeField = IL2CPP.il2cpp_class_get_field_from_name(klass, "_size");
            IntPtr itemsField = IL2CPP.il2cpp_class_get_field_from_name(klass, "_items");
            return sizeField != IntPtr.Zero && itemsField != IntPtr.Zero;
        }

        /// <summary>
        /// Read the contents of an IL2CPP List&lt;T&gt; by accessing its _items array and _size.
        /// Uses field metadata for type classification — no il2cpp_object_get_class on element pointers.
        /// </summary>
        private object ReadIl2CppListDirect(IntPtr listPtr, IntPtr listClass, int depth)
        {
            if (depth > 8) return null;

            try
            {
                // Read _size
                IntPtr sizeField = FindNativeField(listClass, "_size");
                if (sizeField == IntPtr.Zero)
                {
                    DebugLog($"        list: _size field not found");
                    return new List<object>();
                }
                uint sizeOffset = IL2CPP.il2cpp_field_get_offset(sizeField);
                if (sizeOffset == 0) return new List<object>();
                int size = Marshal.ReadInt32(listPtr + (int)sizeOffset);

                DebugLog($"        list: _size={size}");
                if (size <= 0) return new List<object>();
                if (size > 100) size = 100; // safety cap

                // Read _items array pointer
                IntPtr itemsField = FindNativeField(listClass, "_items");
                if (itemsField == IntPtr.Zero)
                {
                    DebugLog($"        list: _items field not found");
                    return new List<object>();
                }
                uint itemsOffset = IL2CPP.il2cpp_field_get_offset(itemsField);
                if (itemsOffset == 0) return new List<object>();
                IntPtr arrayPtr = Marshal.ReadIntPtr(listPtr + (int)itemsOffset);
                if (arrayPtr == IntPtr.Zero)
                {
                    DebugLog($"        list: _items is null");
                    return new List<object>();
                }

                // Get element type from the _items field type metadata
                IntPtr itemsFieldType = IL2CPP.il2cpp_field_get_type(itemsField);
                if (itemsFieldType == IntPtr.Zero) return new List<object>();

                // _items is T[] (SZARRAY), get element class from array class
                IntPtr arrayClass = IL2CPP.il2cpp_class_from_type(itemsFieldType);
                if (arrayClass == IntPtr.Zero) return new List<object>();

                IntPtr elemClass = IL2CPP.il2cpp_class_get_element_class(arrayClass);
                if (elemClass == IntPtr.Zero) return new List<object>();

                bool elemIsValueType = IL2CPP.il2cpp_class_is_valuetype(elemClass);
                string elemClassName = GetClassNameSafe(elemClass);

                DebugLog($"        list: elem={elemClassName} isValue={elemIsValueType}");

                // Read array elements — same layout as ReadArrayFromFieldMetadata
                // Il2CppArray: [object header (2*IntPtr)] [IntPtr bounds] [int32 max_length] [elements...]
                int headerSize = IntPtr.Size * 3;
                int elementsOffset = headerSize + IntPtr.Size; // length padded to IntPtr

                var result = new List<object>();

                if (elemIsValueType)
                {
                    int elemSize = IL2CPP.il2cpp_class_instance_size(elemClass) - IntPtr.Size * 2;
                    if (elemSize <= 0) elemSize = 4;

                    for (int i = 0; i < size; i++)
                    {
                        IntPtr addr = arrayPtr + elementsOffset + i * elemSize;
                        result.Add(ReadValueTypeElement(addr, elemClassName, elemSize, elemClass, depth));
                    }
                }
                else
                {
                    // Classify element type from metadata
                    bool elemIsUnityObject = IsUnityObjectClass(elemClass);
                    bool elemIsLocalization = IsLocalizationClass(elemClassName, elemClass);
                    bool elemIsList = IsIl2CppListClass(elemClassName, elemClass);
                    bool extractInline = elemIsUnityObject && ShouldExtractTemplateInline(elemClassName);
                    bool elemIsEffectHandler = IsEffectHandlerClass(elemClassName, elemClass);

                    if (elemIsEffectHandler)
                        DebugLog($"        list is effect handler list: {elemClassName}");

                    for (int i = 0; i < size; i++)
                    {
                        IntPtr elemPtr = Marshal.ReadIntPtr(arrayPtr + elementsOffset + i * IntPtr.Size);
                        if (elemPtr == IntPtr.Zero)
                        {
                            result.Add(null);
                            continue;
                        }

                        // Check for polymorphic effect handlers first (they are Unity objects too)
                        if (elemIsEffectHandler)
                        {
                            DebugLog($"          effect handler elem[{i}] at {elemPtr}");
                            if (IsUnityObjectAliveWithClass(elemPtr, elemClass))
                            {
                                var handlerData = ReadEffectHandler(elemPtr, depth + 1);
                                DebugLog($"          effect handler elem[{i}] result: {(handlerData is Dictionary<string, object> d ? d["_type"] : handlerData)}");
                                result.Add(handlerData);
                            }
                            else
                            {
                                DebugLog($"          effect handler elem[{i}] not alive");
                                result.Add(null);
                            }
                        }
                        else if (elemIsUnityObject)
                        {
                            if (IsUnityObjectAliveWithClass(elemPtr, elemClass))
                            {
                                string assetName = ReadUnityAssetNameWithClass(elemPtr, elemClass, elemClassName) ?? $"({elemClassName})";
                                if (extractInline && depth < 2)
                                {
                                    var nested = ReadNestedObjectDirect(elemPtr, elemClass, elemClassName, depth + 1);
                                    if (nested is Dictionary<string, object> nestedDict)
                                    {
                                        nestedDict["name"] = assetName;
                                        result.Add(nestedDict);
                                        continue;
                                    }
                                }
                                result.Add(assetName);
                            }
                            else
                                result.Add(null);
                        }
                        else if (elemIsLocalization)
                        {
                            result.Add(ReadLocalizedStringWithClass(elemPtr, elemClass) ?? "");
                        }
                        else if (elemIsList)
                        {
                            result.Add(ReadIl2CppListDirect(elemPtr, elemClass, depth + 1));
                        }
                        else if (elemClassName == "String")
                        {
                            // IL2CPP String — read directly, don't walk fields
                            string str = IL2CPP.Il2CppStringToManaged(elemPtr);
                            result.Add(str);
                        }
                        else
                        {
                            result.Add(ReadNestedObjectDirect(elemPtr, elemClass, elemClassName, depth + 1));
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                DebugLog($"        list read error: {ex.Message}");
                return new List<object>();
            }
        }

        /// <summary>
        /// Get a class name from IL2CPP metadata (safe, no object memory reads).
        /// </summary>
        private string GetClassNameSafe(IntPtr klass)
        {
            if (klass == IntPtr.Zero) return "?";
            IntPtr namePtr = IL2CPP.il2cpp_class_get_name(klass);
            return namePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(namePtr) : "?";
        }

        /// <summary>
        /// Check if a class (from metadata) is a localization type by walking its parent chain.
        /// </summary>
        private bool IsLocalizationClass(string className, IntPtr klass)
        {
            if (className == "LocalizedLine" || className == "LocalizedMultiLine" ||
                className == "BaseLocalizedString")
                return true;

            // Check parent chain
            IntPtr parent = IL2CPP.il2cpp_class_get_parent(klass);
            while (parent != IntPtr.Zero)
            {
                string pName = GetClassNameSafe(parent);
                if (pName == "BaseLocalizedString")
                    return true;
                parent = IL2CPP.il2cpp_class_get_parent(parent);
            }
            return false;
        }

        /// <summary>
        /// Read m_DefaultTranslation from a localization object using a known class (from metadata).
        /// Does NOT call il2cpp_object_get_class — the class comes from field type metadata.
        /// </summary>
        private string ReadLocalizedStringWithClass(IntPtr objPtr, IntPtr klass)
        {
            try
            {
                IntPtr field = FindNativeField(klass, "m_DefaultTranslation");
                if (field == IntPtr.Zero) return null;

                uint offset = IL2CPP.il2cpp_field_get_offset(field);
                if (offset == 0) return null;

                IntPtr strPtr = Marshal.ReadIntPtr(objPtr + (int)offset);
                if (strPtr == IntPtr.Zero) return null;

                return IL2CPP.Il2CppStringToManaged(strPtr);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a Unity Object's native pointer is alive, using a known class (from metadata).
        /// Does NOT call il2cpp_object_get_class — the class comes from field type metadata.
        /// </summary>
        private bool IsUnityObjectAliveWithClass(IntPtr objectPointer, IntPtr klass)
        {
            try
            {
                IntPtr cachedPtrField = FindNativeField(klass, "m_CachedPtr");
                if (cachedPtrField == IntPtr.Zero) return true; // can't check, assume alive

                uint offset = IL2CPP.il2cpp_field_get_offset(cachedPtrField);
                if (offset == 0) return true;

                IntPtr nativePtr = Marshal.ReadIntPtr(objectPointer + (int)offset);
                return nativePtr != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Read a template object's name (m_ID) using a known class (from metadata).
        /// Only reads m_ID — does NOT call Unity's .name property (which can SIGSEGV).
        /// For Unity asset names (Sprites etc.), use ReadUnityAssetNameWithClass separately.
        /// </summary>
        private string ReadObjectNameWithClass(IntPtr objectPointer, IntPtr klass)
        {
            try
            {
                // Strategy 1: m_ID (standard DataTemplate path)
                IntPtr idField = FindNativeField(klass, "m_ID");
                if (idField != IntPtr.Zero)
                {
                    uint offset = IL2CPP.il2cpp_field_get_offset(idField);
                    if (offset != 0)
                    {
                        IntPtr strPtr = Marshal.ReadIntPtr(objectPointer + (int)offset);
                        if (strPtr != IntPtr.Zero)
                        {
                            string id = IL2CPP.Il2CppStringToManaged(strPtr);
                            if (!string.IsNullOrEmpty(id))
                                return id;
                        }
                    }
                }

                // Strategy 2: Path field (ConversationTemplate's localization base key)
                IntPtr pathField = FindNativeField(klass, "Path");
                if (pathField != IntPtr.Zero)
                {
                    uint offset = IL2CPP.il2cpp_field_get_offset(pathField);
                    if (offset != 0)
                    {
                        IntPtr strPtr = Marshal.ReadIntPtr(objectPointer + (int)offset);
                        if (strPtr != IntPtr.Zero)
                        {
                            string path = IL2CPP.Il2CppStringToManaged(strPtr);
                            if (!string.IsNullOrEmpty(path))
                                return path;
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Read a Unity asset's name using multiple strategies:
        /// 1. m_ID field (for DataTemplate-derived objects)
        /// 2. m_Name field via IL2CPP metadata (for standard Unity Objects like Sprite, Texture2D)
        /// 3. Unity .name property as last resort (wrapped in try-catch for SIGSEGV safety)
        /// </summary>
        private string ReadUnityAssetNameWithClass(IntPtr objectPointer, IntPtr klass, string className)
        {
            // Strategy 1: m_ID (works for DataTemplate-derived objects)
            string name = ReadObjectNameWithClass(objectPointer, klass);
            if (!string.IsNullOrEmpty(name))
                return name;

            // Strategy 2: m_Name field via IL2CPP metadata
            // Unity Objects store their name in a managed m_Name field in some builds
            try
            {
                IntPtr nameField = FindNativeField(klass, "m_Name");
                if (nameField != IntPtr.Zero)
                {
                    uint offset = IL2CPP.il2cpp_field_get_offset(nameField);
                    if (offset != 0)
                    {
                        IntPtr strPtr = Marshal.ReadIntPtr(objectPointer + (int)offset);
                        if (strPtr != IntPtr.Zero)
                        {
                            string mName = IL2CPP.Il2CppStringToManaged(strPtr);
                            if (!string.IsNullOrEmpty(mName))
                            {
                                DebugLog($"        m_Name strategy: {mName}");
                                return mName;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"        m_Name strategy failed: {ex.Message}");
            }

            // Strategy 3: Unity .name property (last resort, can SIGSEGV on some objects)
            try
            {
                var obj = new UnityEngine.Object(objectPointer);
                string unityName = obj.name;
                if (!string.IsNullOrEmpty(unityName))
                {
                    DebugLog($"        Unity .name strategy: {unityName}");
                    return unityName;
                }
            }
            catch (Exception ex)
            {
                DebugLog($"        Unity .name strategy failed: {ex.Message}");
            }

            return null;
        }


        /// <summary>
        /// Read an IL2CPP array using field type metadata for element classification.
        /// The element class comes from metadata (safe), not from il2cpp_object_get_class on elements.
        /// Il2CppArray layout: [Il2CppObject header (2*IntPtr)] [IntPtr bounds] [int32 max_length] [elements...]
        /// </summary>
        private object ReadArrayFromFieldMetadata(IntPtr arrayPtr, IntPtr fieldType, int depth)
        {
            if (depth > 8) return null;

            try
            {
                // Get array class and element class from METADATA (safe)
                IntPtr arrayClass = IL2CPP.il2cpp_class_from_type(fieldType);
                if (arrayClass == IntPtr.Zero) return null;

                IntPtr elemClass = IL2CPP.il2cpp_class_get_element_class(arrayClass);
                if (elemClass == IntPtr.Zero) return null;

                // Read array length from known Il2CppArray layout
                int headerSize = IntPtr.Size * 3; // object header (2 ptrs) + bounds ptr
                int length = Marshal.ReadInt32(arrayPtr + headerSize);

                DebugLog($"        array length={length}");

                // Sanity check the length
                if (length < 0 || length > 10000) return null;
                if (length == 0) return new List<object>();
                if (length > 100) length = 100; // cap for safety

                int elementsOffset = headerSize + IntPtr.Size; // length is padded to IntPtr alignment
                bool elemIsValueType = IL2CPP.il2cpp_class_is_valuetype(elemClass);
                string elemClassName = GetClassNameSafe(elemClass);

                var result = new List<object>();

                if (elemIsValueType)
                {
                    int elemSize = IL2CPP.il2cpp_class_instance_size(elemClass) - IntPtr.Size * 2;
                    if (elemSize <= 0) elemSize = 4;

                    for (int i = 0; i < length; i++)
                    {
                        IntPtr addr = arrayPtr + elementsOffset + i * elemSize;
                        result.Add(ReadValueTypeElement(addr, elemClassName, elemSize, elemClass, depth));
                    }
                }
                else
                {
                    // Classify element type from metadata (safe, no data pointer dereference)
                    bool elemIsUnityObject = IsUnityObjectClass(elemClass);
                    bool elemIsLocalization = IsLocalizationClass(elemClassName, elemClass);
                    bool extractInline = elemIsUnityObject && ShouldExtractTemplateInline(elemClassName);

                    for (int i = 0; i < length; i++)
                    {
                        IntPtr elemPtr = Marshal.ReadIntPtr(arrayPtr + elementsOffset + i * IntPtr.Size);
                        if (elemPtr == IntPtr.Zero)
                        {
                            result.Add(null);
                            continue;
                        }

                        if (elemIsUnityObject)
                        {
                            if (IsUnityObjectAliveWithClass(elemPtr, elemClass))
                            {
                                string assetName = ReadUnityAssetNameWithClass(elemPtr, elemClass, elemClassName) ?? $"({elemClassName})";
                                // For template types not extracted standalone, include field data
                                if (extractInline && depth < 2)
                                {
                                    var nested = ReadNestedObjectDirect(elemPtr, elemClass, elemClassName, depth + 1);
                                    if (nested is Dictionary<string, object> nestedDict)
                                    {
                                        nestedDict["name"] = assetName;
                                        result.Add(nestedDict);
                                        continue;
                                    }
                                }
                                result.Add(assetName);
                            }
                            else
                                result.Add(null);
                        }
                        else if (elemIsLocalization)
                        {
                            result.Add(ReadLocalizedStringWithClass(elemPtr, elemClass) ?? "");
                        }
                        else if (elemClassName == "String")
                        {
                            string str = IL2CPP.Il2CppStringToManaged(elemPtr);
                            result.Add(str);
                        }
                        else
                        {
                            result.Add(ReadNestedObjectDirect(elemPtr, elemClass, elemClassName, depth + 1));
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                DebugLog($"        array read error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Read an IL2CPP reference object via direct memory. Handles arrays,
        /// Unity Object references, and nested objects — no property getters.
        /// </summary>
        private object ReadReferenceDirect(IntPtr objPtr, Type expectedType, int depth)
        {
            if (objPtr == IntPtr.Zero) return null;
            if (depth > 3) return "(max depth)";

            try
            {
                IntPtr klass = IL2CPP.il2cpp_object_get_class(objPtr);
                if (klass == IntPtr.Zero) return null;

                IntPtr classNamePtr = IL2CPP.il2cpp_class_get_name(klass);
                string className = classNamePtr != IntPtr.Zero
                    ? Marshal.PtrToStringAnsi(classNamePtr) : "?";

                // Check if this is an IL2CPP array (native Il2CppArray)
                // Il2CppArray layout: [Il2CppObject header] [IntPtr bounds] [int32 max_length] [elements...]
                // The class will have a rank > 0, or the type name ends with "[]"
                if (IsIl2CppArrayClass(klass))
                {
                    return ReadIl2CppArrayDirect(objPtr, klass, depth);
                }

                // Unity Object reference -> return its name (check alive first)
                if (IsUnityObjectClass(klass))
                {
                    if (!IsUnityObjectAlive(objPtr))
                        return "(destroyed)";
                    string name = ReadObjectNameDirect(objPtr);
                    return name ?? "(unnamed)";
                }

                // Nested IL2CPP object -> read its primitive fields
                return ReadNestedObjectDirect(objPtr, klass, className, depth);
            }
            catch (Exception ex)
            {
                DebugLog($"      ReadReferenceDirect error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if an IL2CPP class represents an array type.
        /// </summary>
        private bool IsIl2CppArrayClass(IntPtr klass)
        {
            try
            {
                int rank = IL2CPP.il2cpp_class_get_rank(klass);
                return rank > 0;
            }
            catch { return false; }
        }

        /// <summary>
        /// Check if an IL2CPP class derives from UnityEngine.Object
        /// by walking the parent chain and checking class names.
        /// </summary>
        private bool IsUnityObjectClass(IntPtr klass)
        {
            try
            {
                IntPtr check = klass;
                while (check != IntPtr.Zero)
                {
                    IntPtr namePtr = IL2CPP.il2cpp_class_get_name(check);
                    if (namePtr != IntPtr.Zero)
                    {
                        string name = Marshal.PtrToStringAnsi(namePtr);
                        if (name == "Object")
                        {
                            // Verify it's UnityEngine.Object, not System.Object
                            IntPtr nsPtr = IL2CPP.il2cpp_class_get_namespace(check);
                            string ns = nsPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(nsPtr) : "";
                            if (ns == "UnityEngine")
                                return true;
                        }
                    }
                    check = IL2CPP.il2cpp_class_get_parent(check);
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if an IL2CPP class derives from SkillEventHandlerTemplate
        /// by walking the parent chain and checking class names.
        /// These are polymorphic effect handlers that need special field extraction.
        /// </summary>
        private bool IsEffectHandlerClass(string className, IntPtr klass)
        {
            // Direct check first
            if (className == "SkillEventHandlerTemplate")
                return true;

            try
            {
                IntPtr check = IL2CPP.il2cpp_class_get_parent(klass);
                while (check != IntPtr.Zero)
                {
                    IntPtr namePtr = IL2CPP.il2cpp_class_get_name(check);
                    if (namePtr != IntPtr.Zero)
                    {
                        string name = Marshal.PtrToStringAnsi(namePtr);
                        if (name == "SkillEventHandlerTemplate")
                            return true;
                        // Stop at base Unity classes
                        if (name == "ScriptableObject" || name == "Object")
                            break;
                    }
                    check = IL2CPP.il2cpp_class_get_parent(check);
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if a Unity Object class is a game template type that should have its fields
        /// extracted inline (rather than just its name) when encountered as a reference.
        /// Returns false for recognized template types (they're extracted as standalone entries).
        /// </summary>
        private bool ShouldExtractTemplateInline(string className)
        {
            // Recognized template types are extracted as standalone JSON files,
            // so references to them should just use the name, not inline extraction
            if (_il2cppNameToType.ContainsKey(className))
                return false;

            // Unknown types that look like templates - extract inline
            return className.EndsWith("Template");
        }

        /// <summary>
        /// Read an IL2CPP array directly from memory.
        /// Il2CppArray layout: [object header (2*IntPtr)] [IntPtr bounds] [int32 max_length] [elements...]
        /// </summary>
        private object ReadIl2CppArrayDirect(IntPtr arrayPtr, IntPtr arrayKlass, int depth)
        {
            try
            {
                // Get element class to determine element size and type
                IntPtr elemKlass = IL2CPP.il2cpp_class_get_element_class(arrayKlass);

                // Read array length: offset = 2*IntPtr (object header) + IntPtr (bounds)
                int headerSize = IntPtr.Size * 3; // object header (2 ptrs) + bounds ptr
                int length = Marshal.ReadInt32(arrayPtr + headerSize);

                if (length <= 0) return new List<object>();
                if (length > 100) length = 100; // safety cap

                // Elements start after header + length field (aligned)
                int elementsOffset = headerSize + IntPtr.Size; // length is padded to IntPtr alignment

                // Determine if elements are value types or references
                bool elemIsValueType = IL2CPP.il2cpp_class_is_valuetype(elemKlass);

                var result = new List<object>();

                if (!elemIsValueType)
                {
                    // Reference array: each element is an IntPtr
                    for (int i = 0; i < length; i++)
                    {
                        IntPtr elemPtr = Marshal.ReadIntPtr(arrayPtr + elementsOffset + i * IntPtr.Size);
                        if (elemPtr == IntPtr.Zero)
                        {
                            result.Add(null);
                            continue;
                        }

                        // Check if element is a Unity Object (return name) or nested object
                        IntPtr elemObjKlass = IL2CPP.il2cpp_object_get_class(elemPtr);
                        if (IsUnityObjectClass(elemObjKlass))
                        {
                            result.Add(ReadObjectNameDirect(elemPtr));
                        }
                        else
                        {
                            result.Add(ReadReferenceDirect(elemPtr, null, depth + 1));
                        }
                    }
                }
                else
                {
                    // Value type array: each element is inline at class instance size
                    int elemSize = IL2CPP.il2cpp_class_instance_size(elemKlass) - IntPtr.Size * 2;
                    if (elemSize <= 0) elemSize = 4; // fallback

                    IntPtr elemNamePtr = IL2CPP.il2cpp_class_get_name(elemKlass);
                    string elemName = elemNamePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(elemNamePtr) : "?";

                    for (int i = 0; i < length; i++)
                    {
                        IntPtr addr = arrayPtr + elementsOffset + i * elemSize;
                        // Try to read as common value types
                        object val = ReadValueTypeElement(addr, elemName, elemSize, elemKlass, depth);
                        result.Add(val);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                DebugLog($"      ReadIl2CppArrayDirect error: {ex.Message}");
                return new List<object>();
            }
        }

        /// <summary>
        /// Read a value type array element at the given address.
        /// For complex structs, reads all fields recursively.
        /// </summary>
        private object ReadValueTypeElement(IntPtr addr, string elemTypeName, int elemSize, IntPtr elemClass = default, int depth = 0)
        {
            try
            {
                switch (elemTypeName)
                {
                    case "Int32": return Marshal.ReadInt32(addr);
                    case "Single": return ReadFloat(addr);
                    case "Boolean": return Marshal.ReadByte(addr) != 0;
                    case "Byte": return (int)Marshal.ReadByte(addr);
                    case "Int16": return (int)Marshal.ReadInt16(addr);
                    case "Int64": return Marshal.ReadInt64(addr);
                    case "Double": return BitConverter.Int64BitsToDouble(Marshal.ReadInt64(addr));
                    case "UInt32": return (int)(uint)Marshal.ReadInt32(addr);
                    default:
                        // For structs, read all fields (stored inline at addr)
                        if (elemClass == IntPtr.Zero || depth > 8)
                            return $"({elemTypeName})";
                        return ReadValueTypeStructFields(addr, elemClass, elemTypeName, depth);
                }
            }
            catch { return null; }
        }

        /// <summary>
        /// Read fields from a value type struct stored inline at the given address.
        /// Similar to ReadValueTypeField but for array elements.
        /// </summary>
        private object ReadValueTypeStructFields(IntPtr addr, IntPtr klass, string structName, int depth)
        {
            if (depth > 8) return $"({structName})";

            try
            {
                // Check if it's an enum by looking for the standard "value__" field
                IntPtr valueField = IL2CPP.il2cpp_class_get_field_from_name(klass, "value__");
                if (valueField != IntPtr.Zero)
                {
                    // It's an enum — read as int32
                    return Marshal.ReadInt32(addr);
                }

                var result = new Dictionary<string, object>();

                // Pass 1: Collect all field offsets (for R8→R4 detection)
                var offsetSet = new HashSet<uint>();
                IntPtr walkKlass = klass;
                while (walkKlass != IntPtr.Zero)
                {
                    IntPtr iter = IntPtr.Zero;
                    IntPtr f;
                    while ((f = IL2CPP.il2cpp_class_get_fields(walkKlass, ref iter)) != IntPtr.Zero)
                    {
                        IntPtr ft = IL2CPP.il2cpp_field_get_type(f);
                        if (ft == IntPtr.Zero) continue;
                        uint fa = IL2CPP.il2cpp_type_get_attrs(ft);
                        if ((fa & 0x10) != 0) continue; // skip static
                        uint fo = IL2CPP.il2cpp_field_get_offset(f);
                        offsetSet.Add(fo);
                    }
                    walkKlass = IL2CPP.il2cpp_class_get_parent(walkKlass);
                    if (walkKlass != IntPtr.Zero)
                    {
                        IntPtr pnp = IL2CPP.il2cpp_class_get_name(walkKlass);
                        string pn = pnp != IntPtr.Zero ? Marshal.PtrToStringAnsi(pnp) : "";
                        if (pn == "ValueType" || pn == "Object") break;
                    }
                }
                var sortedOffsets = offsetSet.OrderBy(x => x).ToArray();

                // Pass 2: Read field values
                walkKlass = klass;
                while (walkKlass != IntPtr.Zero)
                {
                    IntPtr iter = IntPtr.Zero;
                    IntPtr field;
                    while ((field = IL2CPP.il2cpp_class_get_fields(walkKlass, ref iter)) != IntPtr.Zero)
                    {
                        IntPtr fieldNamePtr = IL2CPP.il2cpp_field_get_name(field);
                        if (fieldNamePtr == IntPtr.Zero) continue;
                        string fieldName = Marshal.PtrToStringAnsi(fieldNamePtr);
                        if (string.IsNullOrEmpty(fieldName)) continue;
                        if (SkipProperties.Contains(fieldName)) continue;
                        if (result.ContainsKey(fieldName)) continue;

                        uint fieldOffset = IL2CPP.il2cpp_field_get_offset(field);
                        IntPtr fieldType = IL2CPP.il2cpp_field_get_type(field);
                        if (fieldType == IntPtr.Zero) continue;

                        uint fieldAttrs = IL2CPP.il2cpp_type_get_attrs(fieldType);
                        if ((fieldAttrs & 0x10) != 0) continue; // skip static

                        int typeEnum = IL2CPP.il2cpp_type_get_type(fieldType);
                        IntPtr fieldAddr = addr + (int)fieldOffset;

                        // Detect R8 (double) fields that are actually R4 (float)
                        bool r8IsActuallyR4 = false;
                        if (typeEnum == 12)
                        {
                            int idx = Array.BinarySearch(sortedOffsets, fieldOffset);
                            if (idx < 0) idx = ~idx; else idx++;
                            uint gap = idx < sortedOffsets.Length ? sortedOffsets[idx] - fieldOffset : 8;
                            r8IsActuallyR4 = gap < 8;
                        }

                        object value = typeEnum switch
                        {
                            1 => Marshal.ReadByte(fieldAddr) != 0,           // BOOLEAN
                            2 => (int)Marshal.ReadByte(fieldAddr),            // CHAR
                            3 => (int)(sbyte)Marshal.ReadByte(fieldAddr),     // I1
                            4 => (int)Marshal.ReadByte(fieldAddr),            // U1
                            5 => (int)Marshal.ReadInt16(fieldAddr),           // I2
                            6 => (int)(ushort)Marshal.ReadInt16(fieldAddr),   // U2
                            7 => Marshal.ReadInt32(fieldAddr),                // I4
                            8 => (int)(uint)Marshal.ReadInt32(fieldAddr),     // U4
                            9 => Marshal.ReadInt64(fieldAddr),                // I8
                            10 => (long)(ulong)Marshal.ReadInt64(fieldAddr),  // U8
                            11 => ReadFloat(fieldAddr),                       // R4
                            12 => r8IsActuallyR4 ? (object)(double)ReadFloat(fieldAddr) : ReadDoubleValidated(fieldAddr),
                            14 => ReadIl2CppStringAt(fieldAddr),              // STRING
                            17 => ReadValueTypeField(fieldAddr, fieldType, depth + 1),   // VALUETYPE
                            18 or 21 => ReadNestedRefField(fieldAddr, fieldType, depth + 1), // CLASS or GENERICINST
                            29 => ReadNestedArrayField(fieldAddr, fieldType, depth + 1), // SZARRAY
                            _ => null
                        };

                        // Type enums 18 (CLASS) and 21 (GENERICINST) are reference types that can be null
                        bool isReferenceType = typeEnum == 18 || typeEnum == 21;
                        if (value != null || isReferenceType)
                            result[fieldName] = value;
                    }

                    walkKlass = IL2CPP.il2cpp_class_get_parent(walkKlass);
                    if (walkKlass != IntPtr.Zero)
                    {
                        IntPtr pnp = IL2CPP.il2cpp_class_get_name(walkKlass);
                        string pn = pnp != IntPtr.Zero ? Marshal.PtrToStringAnsi(pnp) : "";
                        if (pn == "ValueType" || pn == "Object") break;
                    }
                }

                return result.Count > 0 ? result : $"({structName})";
            }
            catch
            {
                return $"({structName})";
            }
        }

        /// <summary>
        /// Read a nested IL2CPP object's fields directly from memory.
        /// Returns a Dictionary of field name -> value for JSON serialization.
        /// </summary>
        private int _nestedDebugCount = 0;

        private object ReadNestedObjectDirect(IntPtr objPtr, IntPtr klass, string className, int depth)
        {
            bool doLog = _nestedDebugCount < 3;
            if (doLog) _nestedDebugCount++;

            try
            {
                var result = new Dictionary<string, object>();

                // Pass 1: Collect all instance field offsets (sorted) so we can detect
                // R8 (double) fields that are actually R4 (float) in memory.
                // IL2CPP metadata sometimes reports float fields as double;
                // we detect this by checking the gap to the next field: if < 8, it's R4.
                var offsetSet = new HashSet<uint>();
                {
                    IntPtr wk = klass;
                    while (wk != IntPtr.Zero)
                    {
                        IntPtr it = IntPtr.Zero;
                        IntPtr f;
                        while ((f = IL2CPP.il2cpp_class_get_fields(wk, ref it)) != IntPtr.Zero)
                        {
                            IntPtr ft = IL2CPP.il2cpp_field_get_type(f);
                            if (ft == IntPtr.Zero) continue;
                            uint fa = IL2CPP.il2cpp_type_get_attrs(ft);
                            if ((fa & 0x10) != 0) continue; // skip static
                            uint fo = IL2CPP.il2cpp_field_get_offset(f);
                            if (fo != 0) offsetSet.Add(fo);
                        }
                        wk = IL2CPP.il2cpp_class_get_parent(wk);
                    }
                }
                var sortedOffsets = offsetSet.OrderBy(x => x).ToArray();

                // Pass 2: Read field values
                IntPtr walkKlass = klass;
                while (walkKlass != IntPtr.Zero)
                {
                    if (doLog)
                    {
                        IntPtr cn = IL2CPP.il2cpp_class_get_name(walkKlass);
                        string cname = cn != IntPtr.Zero ? Marshal.PtrToStringAnsi(cn) : "?";
                        DebugLog($"        [nested] class={cname}");
                    }

                    IntPtr iter = IntPtr.Zero;
                    IntPtr field;
                    while ((field = IL2CPP.il2cpp_class_get_fields(walkKlass, ref iter)) != IntPtr.Zero)
                    {
                        IntPtr fieldNamePtr = IL2CPP.il2cpp_field_get_name(field);
                        if (fieldNamePtr == IntPtr.Zero) continue;
                        string fieldName = Marshal.PtrToStringAnsi(fieldNamePtr);
                        if (string.IsNullOrEmpty(fieldName)) continue;

                        // Skip problematic fields (same as top-level)
                        if (SkipProperties.Contains(fieldName)) continue;

                        // Skip fields already read from a more-derived class
                        if (result.ContainsKey(fieldName)) continue;

                        uint fieldOffset = IL2CPP.il2cpp_field_get_offset(field);
                        if (fieldOffset == 0) continue;

                        IntPtr fieldType = IL2CPP.il2cpp_field_get_type(field);
                        if (fieldType == IntPtr.Zero) continue;

                        // Skip static fields — their offsets are in the static data area, not the object
                        uint fieldAttrs = IL2CPP.il2cpp_type_get_attrs(fieldType);
                        if ((fieldAttrs & 0x10) != 0) continue; // FIELD_ATTRIBUTE_STATIC

                        int typeEnum = IL2CPP.il2cpp_type_get_type(fieldType);
                        IntPtr addr = objPtr + (int)fieldOffset;

                        // Detect R8 (double) fields that are actually R4 (float) in memory:
                        // Find the gap to the next field; if < 8 bytes, this can't be a double.
                        bool r8IsActuallyR4 = false;
                        if (typeEnum == 12)
                        {
                            // Binary search for the next offset after fieldOffset
                            int idx = Array.BinarySearch(sortedOffsets, fieldOffset);
                            if (idx < 0) idx = ~idx; else idx++;
                            uint gap = idx < sortedOffsets.Length ? sortedOffsets[idx] - fieldOffset : 8;
                            r8IsActuallyR4 = gap < 8;
                        }

                        // Always log field being read for inline templates (helps diagnose crashes)
                        DebugLog($"        [nested] reading: {fieldName} typeEnum={typeEnum} offset={fieldOffset}{(r8IsActuallyR4 ? " (R8→R4)" : "")}");

                        object value = typeEnum switch
                        {
                            1 => Marshal.ReadByte(addr) != 0,           // IL2CPP_TYPE_BOOLEAN
                            2 => (int)Marshal.ReadByte(addr),            // IL2CPP_TYPE_CHAR
                            3 => (int)(sbyte)Marshal.ReadByte(addr),     // IL2CPP_TYPE_I1
                            4 => (int)Marshal.ReadByte(addr),            // IL2CPP_TYPE_U1
                            5 => (int)Marshal.ReadInt16(addr),           // IL2CPP_TYPE_I2
                            6 => (int)(ushort)Marshal.ReadInt16(addr),   // IL2CPP_TYPE_U2
                            7 => Marshal.ReadInt32(addr),                // IL2CPP_TYPE_I4
                            8 => (int)(uint)Marshal.ReadInt32(addr),     // IL2CPP_TYPE_U4
                            9 => Marshal.ReadInt64(addr),                // IL2CPP_TYPE_I8
                            10 => (long)(ulong)Marshal.ReadInt64(addr),  // IL2CPP_TYPE_U8
                            11 => ReadFloat(addr),                       // IL2CPP_TYPE_R4
                            12 => r8IsActuallyR4 ? (object)(double)ReadFloat(addr) : ReadDoubleValidated(addr),
                            14 => ReadIl2CppStringAt(addr),              // IL2CPP_TYPE_STRING
                            17 => ReadValueTypeField(addr, fieldType, depth),   // IL2CPP_TYPE_VALUETYPE (enums + structs)
                            18 or 21 => ReadNestedRefField(addr, fieldType, depth), // IL2CPP_TYPE_CLASS or GENERICINST
                            29 => ReadNestedArrayField(addr, fieldType, depth), // IL2CPP_TYPE_SZARRAY
                            _ => null
                        };

                        DebugLog($"        [nested] read complete: {fieldName} = {(value == null ? "null" : value.GetType().Name)}");

                        // Type enums 18 (CLASS) and 21 (GENERICINST) are reference types that can legitimately be null
                        bool isReferenceType = typeEnum == 18 || typeEnum == 21;
                        if (value != null || isReferenceType)
                            result[fieldName] = value;
                    }

                    walkKlass = IL2CPP.il2cpp_class_get_parent(walkKlass);
                }

                return result.Count > 0 ? result : $"({className})";
            }
            catch
            {
                return $"({className})";
            }
        }

        /// <summary>
        /// Read a value type field (enum or struct) from a nested object.
        /// Enums are read as their underlying int value; structs are recursively extracted.
        /// </summary>
        private object ReadValueTypeField(IntPtr addr, IntPtr fieldType, int depth)
        {
            try
            {
                if (depth > 8) return null; // Prevent infinite recursion

                IntPtr klass = IL2CPP.il2cpp_class_from_type(fieldType);
                if (klass == IntPtr.Zero) return null;

                // Check if it's an enum by looking for the standard "value__" field
                IntPtr valueField = IL2CPP.il2cpp_class_get_field_from_name(klass, "value__");
                if (valueField != IntPtr.Zero)
                {
                    // It's an enum — read as int32 (covers most enums)
                    return Marshal.ReadInt32(addr);
                }

                // For structs, iterate and read all fields (stored inline at addr)
                var result = new Dictionary<string, object>();

                // Get struct class name for debugging
                IntPtr namePtr = IL2CPP.il2cpp_class_get_name(klass);
                string structName = namePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(namePtr) : "?";

                // Check if we have a schema definition for this struct type
                if (_structTypes.TryGetValue(structName, out var structSchema))
                {
                    DebugLog($"        [struct] Using schema for {structName} ({structSchema.Fields.Count} fields)");
                    return ReadStructFromSchema(addr, structSchema, depth);
                }

                // Pass 1: Collect all field offsets (for R8→R4 detection)
                var offsetSet = new HashSet<uint>();
                IntPtr walkKlass = klass;
                while (walkKlass != IntPtr.Zero)
                {
                    IntPtr iter = IntPtr.Zero;
                    IntPtr f;
                    while ((f = IL2CPP.il2cpp_class_get_fields(walkKlass, ref iter)) != IntPtr.Zero)
                    {
                        IntPtr ft = IL2CPP.il2cpp_field_get_type(f);
                        if (ft == IntPtr.Zero) continue;
                        uint fa = IL2CPP.il2cpp_type_get_attrs(ft);
                        if ((fa & 0x10) != 0) continue; // skip static
                        uint fo = IL2CPP.il2cpp_field_get_offset(f);
                        // For value types, offset 0 is valid (first field), but skip if it looks wrong
                        offsetSet.Add(fo);
                    }
                    walkKlass = IL2CPP.il2cpp_class_get_parent(walkKlass);
                    // Stop at System.ValueType
                    if (walkKlass != IntPtr.Zero)
                    {
                        IntPtr pnp = IL2CPP.il2cpp_class_get_name(walkKlass);
                        string pn = pnp != IntPtr.Zero ? Marshal.PtrToStringAnsi(pnp) : "";
                        if (pn == "ValueType" || pn == "Object") break;
                    }
                }
                var sortedOffsets = offsetSet.OrderBy(x => x).ToArray();

                // Pass 2: Read field values
                walkKlass = klass;
                while (walkKlass != IntPtr.Zero)
                {
                    IntPtr iter = IntPtr.Zero;
                    IntPtr field;
                    while ((field = IL2CPP.il2cpp_class_get_fields(walkKlass, ref iter)) != IntPtr.Zero)
                    {
                        IntPtr fieldNamePtr = IL2CPP.il2cpp_field_get_name(field);
                        if (fieldNamePtr == IntPtr.Zero) continue;
                        string fieldName = Marshal.PtrToStringAnsi(fieldNamePtr);
                        if (string.IsNullOrEmpty(fieldName)) continue;

                        // Skip problematic fields
                        if (SkipProperties.Contains(fieldName)) continue;

                        // Skip fields already read
                        if (result.ContainsKey(fieldName)) continue;

                        uint fieldOffset = IL2CPP.il2cpp_field_get_offset(field);
                        IntPtr fieldTypePtr = IL2CPP.il2cpp_field_get_type(field);
                        if (fieldTypePtr == IntPtr.Zero) continue;

                        uint fieldAttrs = IL2CPP.il2cpp_type_get_attrs(fieldTypePtr);
                        if ((fieldAttrs & 0x10) != 0) continue; // FIELD_ATTRIBUTE_STATIC

                        int typeEnum = IL2CPP.il2cpp_type_get_type(fieldTypePtr);
                        // For value types, the struct is stored inline at addr
                        // Field address is addr + fieldOffset (no object header to skip)
                        IntPtr fieldAddr = addr + (int)fieldOffset;

                        // R8→R4 detection
                        bool r8IsActuallyR4 = false;
                        if (typeEnum == 12)
                        {
                            int idx = Array.BinarySearch(sortedOffsets, fieldOffset);
                            if (idx < 0) idx = ~idx; else idx++;
                            uint gap = idx < sortedOffsets.Length ? sortedOffsets[idx] - fieldOffset : 8;
                            r8IsActuallyR4 = gap < 8;
                        }

                        object value = typeEnum switch
                        {
                            1 => Marshal.ReadByte(fieldAddr) != 0,           // IL2CPP_TYPE_BOOLEAN
                            2 => (int)Marshal.ReadByte(fieldAddr),            // IL2CPP_TYPE_CHAR
                            3 => (int)(sbyte)Marshal.ReadByte(fieldAddr),     // IL2CPP_TYPE_I1
                            4 => (int)Marshal.ReadByte(fieldAddr),            // IL2CPP_TYPE_U1
                            5 => (int)Marshal.ReadInt16(fieldAddr),           // IL2CPP_TYPE_I2
                            6 => (int)(ushort)Marshal.ReadInt16(fieldAddr),   // IL2CPP_TYPE_U2
                            7 => Marshal.ReadInt32(fieldAddr),                // IL2CPP_TYPE_I4
                            8 => (int)(uint)Marshal.ReadInt32(fieldAddr),     // IL2CPP_TYPE_U4
                            9 => Marshal.ReadInt64(fieldAddr),                // IL2CPP_TYPE_I8
                            10 => (long)(ulong)Marshal.ReadInt64(fieldAddr),  // IL2CPP_TYPE_U8
                            11 => ReadFloat(fieldAddr),                       // IL2CPP_TYPE_R4
                            12 => r8IsActuallyR4 ? (object)(double)ReadFloat(fieldAddr) : ReadDoubleValidated(fieldAddr),
                            14 => ReadIl2CppStringAt(fieldAddr),              // IL2CPP_TYPE_STRING
                            17 => ReadValueTypeField(fieldAddr, fieldTypePtr, depth + 1), // Nested struct/enum
                            18 or 21 => ReadNestedRefField(fieldAddr, fieldTypePtr, depth + 1), // Class/generic
                            29 => ReadNestedArrayField(fieldAddr, fieldTypePtr, depth + 1), // Array
                            _ => null
                        };

                        // Type enums 18 (CLASS) and 21 (GENERICINST) are reference types that can be null
                        bool isReferenceType = typeEnum == 18 || typeEnum == 21;
                        if (value != null || isReferenceType)
                            result[fieldName] = value;
                    }

                    walkKlass = IL2CPP.il2cpp_class_get_parent(walkKlass);
                    if (walkKlass != IntPtr.Zero)
                    {
                        IntPtr pnp = IL2CPP.il2cpp_class_get_name(walkKlass);
                        string pn = pnp != IntPtr.Zero ? Marshal.PtrToStringAnsi(pnp) : "";
                        if (pn == "ValueType" || pn == "Object") break;
                    }
                }

                return result.Count > 0 ? result : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Read a class/object reference field from a nested object.
        /// Handles Unity Object references (returns asset name) and nested objects.
        /// </summary>
        private object ReadNestedRefField(IntPtr addr, IntPtr fieldType, int depth)
        {
            try
            {
                IntPtr refPtr = Marshal.ReadIntPtr(addr);
                if (refPtr == IntPtr.Zero) return null;

                IntPtr expectedClass = IL2CPP.il2cpp_class_from_type(fieldType);
                if (expectedClass == IntPtr.Zero) return null;

                string className = GetClassNameSafe(expectedClass);

                // Unity object references just read the asset name — no recursion,
                // so allow them at any depth.
                if (IsUnityObjectClass(expectedClass))
                {
                    if (IsUnityObjectAliveWithClass(refPtr, expectedClass))
                        return ReadUnityAssetNameWithClass(refPtr, expectedClass, className);
                    return null;
                }

                // Depth limit for recursive extraction of non-Unity types
                if (depth > 8) return null;

                // Handle IL2CPP List<T> — extract as array instead of flat object
                if (IsIl2CppListClass(className, expectedClass))
                    return ReadIl2CppListDirect(refPtr, expectedClass, depth + 1);

                return ReadNestedObjectDirect(refPtr, expectedClass, className, depth + 1);
            }
            catch { return null; }
        }

        /// <summary>
        /// Read an array field (SZARRAY) from a nested object.
        /// </summary>
        private object ReadNestedArrayField(IntPtr addr, IntPtr fieldType, int depth)
        {
            try
            {
                if (depth > 8) return null;
                IntPtr arrayPtr = Marshal.ReadIntPtr(addr);
                if (arrayPtr == IntPtr.Zero) return null;
                return ReadArrayFromFieldMetadata(arrayPtr, fieldType, depth + 1);
            }
            catch { return null; }
        }

        /// <summary>
        /// Read an IL2CPP string pointer at the given address.
        /// </summary>
        private string ReadIl2CppStringAt(IntPtr addr)
        {
            try
            {
                IntPtr strPtr = Marshal.ReadIntPtr(addr);
                if (strPtr == IntPtr.Zero) return null;
                return IL2CPP.Il2CppStringToManaged(strPtr);
            }
            catch { return null; }
        }

        /// <summary>
        /// Check if a property returns an IL2CPP reference type (array, list, object)
        /// that could cause a native crash if the backing field is null.
        /// </summary>
        private bool IsIl2CppReferenceProperty(PropertyInfo prop)
        {
            var pt = prop.PropertyType;
            try
            {
                // Il2CppReferenceArray<T>, Il2CppStructArray<T>
                if (pt.IsGenericType)
                {
                    var genDef = pt.GetGenericTypeDefinition();
                    if (genDef != null)
                    {
                        var genName = genDef.Name;
                        if (genName.StartsWith("Il2CppReferenceArray") ||
                            genName.StartsWith("Il2CppStructArray"))
                            return true;
                    }
                }
                // Any Il2CppObjectBase-derived type (including Il2Cpp collections, nested objects)
                if (typeof(Il2CppObjectBase).IsAssignableFrom(pt))
                    return true;
            }
            catch
            {
                // If we can't determine the type, treat as reference for safety
                return true;
            }
            return false;
        }

        private bool ShouldSkipPropertyType(PropertyInfo prop)
        {
            var propType = prop.PropertyType;

            // Skip delegates/actions
            if (typeof(Delegate).IsAssignableFrom(propType))
                return true;

            // Skip IntPtr/UIntPtr
            if (propType == typeof(IntPtr) || propType == typeof(UIntPtr))
                return true;

            // Skip indexer properties
            if (prop.GetIndexParameters().Length > 0)
                return true;

            return false;
        }

        private void SaveSingleTemplateType(string typeName, List<object> templates)
        {
            try
            {
                string filePath = Path.Combine(_outputPath, $"{typeName}.json");

                // In manual/additive mode, merge with existing data
                if (_isManualExtraction && File.Exists(filePath))
                {
                    try
                    {
                        var existingJson = File.ReadAllText(filePath);
                        var existingList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(existingJson);
                        if (existingList != null && existingList.Count > 0)
                        {
                            // Build set of existing IDs
                            var existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var existing in existingList)
                            {
                                if (existing.TryGetValue("m_ID", out var idObj) && idObj is string id)
                                    existingIds.Add(id);
                            }

                            // Add new templates that don't already exist
                            int added = 0;
                            foreach (var newTemplate in templates)
                            {
                                if (newTemplate is Dictionary<string, object> dict &&
                                    dict.TryGetValue("m_ID", out var idObj) && idObj is string id)
                                {
                                    if (!existingIds.Contains(id))
                                    {
                                        existingList.Add(dict);
                                        existingIds.Add(id);
                                        added++;
                                    }
                                }
                            }

                            if (added > 0)
                            {
                                templates = existingList.Cast<object>().ToList();
                                DebugLog($"  Merged {added} new {typeName} instances with {existingList.Count - added} existing");
                            }
                            else
                            {
                                DebugLog($"  No new {typeName} instances to add (all already exist)");
                                return; // Skip write if nothing changed
                            }
                        }
                    }
                    catch (Exception mergeEx)
                    {
                        DebugLog($"  Merge failed for {typeName}, overwriting: {mergeEx.Message}");
                    }
                }

                var json = JsonConvert.SerializeObject(templates, Formatting.Indented, new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    NullValueHandling = NullValueHandling.Include,
                    MaxDepth = 10
                });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                DebugLog($"  Failed to save {typeName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete all previous .json files in the output directory so stale template
        /// types (abstract base classes with no instances) don't persist across runs.
        /// </summary>
        private void CleanOutputDirectory()
        {
            try
            {
                foreach (var file in Directory.GetFiles(_outputPath, "*.json"))
                {
                    try { File.Delete(file); } catch { }
                }
                DebugLog("Cleaned output directory");
            }
            catch { }
        }

        public override void OnApplicationQuit()
        {
            if (!_hasSaved)
            {
                LoggerInstance.Warning("Extraction did not complete before quit");
            }
        }

        private static void PlayerLog(string message)
        {
            UnityEngine.Debug.Log($"[MODDED] {message}");
        }
    }
}
