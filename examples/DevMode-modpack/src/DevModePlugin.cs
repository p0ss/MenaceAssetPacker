using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.DevMode;

public class DevModePlugin : IModpackPlugin
{
    private MelonLogger.Instance _log;
    private HarmonyLib.Harmony _harmony;
    private bool _devModeReady;
    private bool _sceneSeen;
    private bool _cheatsEnabled;
    private bool _earlyDiagDone;
    private int _updateCount;
    private string _statusMessage = "";
    private float _statusTime;

    // Reflection cache — all looked up by name, no hardcoded offsets
    private MethodInfo _tacticalStateGet;
    private MethodInfo _startDevModeAction;
    private ConstructorInfo _godModeActionCtor;
    private object _godModeTargetValue;          // GodModeAction.GodModeTarget.Target (0)
    private ConstructorInfo _deleteEntityActionCtor;
    private Type _entityTemplateType;
    private ConstructorInfo _spawnEntityActionCtor;
    private PropertyInfo _entityTypeProperty;   // EntityTemplate.Type
    private PropertyInfo _actorTypeProperty;    // EntityTemplate.ActorType
    private object _entityTypeActorValue;       // EntityType.Actor

    // DevSettings — enum indices looked up by name
    private int _cheatsEnabledIndex = -1;
    private int _showDevSettingsIndex = -1;

    // Factions — built dynamically from FactionType enum
    private Type _factionEnumType;
    private readonly List<(string Name, object EnumValue)> _factions = new();
    private int _selectedFactionIndex;

    // Entity list
    private readonly List<UnityEngine.Object> _entityTemplates = new();
    private readonly List<string> _entityNames = new();
    private readonly List<string> _entityActorTypes = new();
    private int _selectedEntityIndex;

    // GUI colors (applied via content strings, not GUIStyle — avoids IL2CPP unstripping issues)

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _log = logger;
        _harmony = harmony;
        _log.Msg("Menace Dev Mode v1.0.0");
        DevConsole.RegisterPanel("Dev Mode", DrawDevModePanel);
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        _log.Msg($"Scene loaded: '{sceneName}' (index {buildIndex})");

        if (!_sceneSeen)
        {
            _log.Msg($"First scene '{sceneName}', starting setup coroutine...");
            _sceneSeen = true;
            MelonCoroutines.Start(WaitAndSetupCore());
        }
    }

    /// <summary>
    /// Phase 1: Enable cheats and cache action types. No FindObjectsOfTypeAll needed.
    /// This works even on Unity versions where FindObjectsOfTypeAll crashes.
    /// </summary>
    private System.Collections.IEnumerator WaitAndSetupCore()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            yield return new WaitForSeconds(2f);

            _log.Msg($"Setup attempt {attempt + 1}/30...");

            if (TrySetupCore())
            {
                _log.Msg("Dev mode core setup complete (cheats + actions).");
                yield break;
            }
        }

        _log.Warning("Failed to set up dev mode core after 30 attempts.");
        _devModeReady = true;
    }

    /// <summary>
    /// Core setup: resolve types, enable cheats, cache action constructors.
    /// Does NOT call FindObjectsOfTypeAll — pure reflection against Assembly-CSharp.
    /// </summary>
    private bool TrySetupCore()
    {
        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (gameAssembly == null)
        {
            _log.Msg("Assembly-CSharp not loaded yet");
            return false;
        }

        _log.Msg($"Assembly-CSharp found, {gameAssembly.GetTypes().Length} types");

        _entityTemplateType = gameAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "EntityTemplate" && !t.IsAbstract);

        if (_entityTemplateType == null)
        {
            // Dump type names that look relevant to help diagnose renamed types
            var candidates = gameAssembly.GetTypes()
                .Where(t => t.Name.Contains("Entity") || t.Name.Contains("Template"))
                .Select(t => t.Name)
                .Take(30);
            _log.Msg($"EntityTemplate not found. Candidates: {string.Join(", ", candidates)}");
            return false;
        }

        // Resolve all enums and properties by name before using them
        if (!ResolveReflectionCache(gameAssembly))
            return false;

        EnableCheats(gameAssembly);
        CacheActions(gameAssembly);
        TryLoadEntityTemplates(gameAssembly);

        _devModeReady = true;
        return true;
    }

    /// <summary>
    /// Loads entity templates via DataTemplateLoader.GetAll&lt;EntityTemplate&gt;().
    /// Uses the game's own data pipeline — no FindObjectsOfTypeAll needed.
    /// </summary>
    private void TryLoadEntityTemplates(Assembly gameAssembly)
    {
        try
        {
            _log.Msg("Loading entity templates via DataTemplateLoader...");

            var loaderType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DataTemplateLoader");

            if (loaderType == null)
            {
                // Dump loader-like types to see what the EA build calls it
                var loaderCandidates = gameAssembly.GetTypes()
                    .Where(t => t.Name.Contains("Loader") || t.Name.Contains("Template") || t.Name.Contains("Data"))
                    .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static).Any(m => m.Name.Contains("Get")))
                    .Select(t => t.Name)
                    .Take(20);
                _log.Warning($"DataTemplateLoader not found. Static getter types: {string.Join(", ", loaderCandidates)}");
                return;
            }

            // Log all public static methods on the loader
            var loaderMethods = loaderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))}){(m.IsGenericMethodDefinition ? "<T>" : "")}")
                .ToList();
            _log.Msg($"DataTemplateLoader methods: {string.Join(", ", loaderMethods)}");

            // DataTemplateLoader.GetAll<EntityTemplate>()
            var getAllMethod = loaderType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "GetAll" && m.IsGenericMethodDefinition);

            if (getAllMethod == null)
            {
                _log.Warning("DataTemplateLoader.GetAll<T> not found — spawn menu unavailable");
                return;
            }

            var getAllEntity = getAllMethod.MakeGenericMethod(_entityTemplateType);
            var collection = getAllEntity.Invoke(null, null);

            if (collection == null)
            {
                _log.Warning("DataTemplateLoader.GetAll<EntityTemplate>() returned null");
                return;
            }

            _log.Msg($"GetAll returned type: {collection.GetType().FullName}");

            var entityObjects = EnumerateIl2CppCollection(collection);

            if (entityObjects.Count > 0)
            {
                LoadEntityTemplates(entityObjects.ToArray());
            }
            else
            {
                _log.Warning("No EntityTemplate instances returned — spawn menu unavailable");
            }
        }
        catch (Exception ex)
        {
            _log.Warning($"Could not load entity templates: {ex.Message}");
        }
    }

    /// <summary>
    /// Enumerates an IL2CPP collection object using multiple strategies.
    /// IL2CPP collections don't implement managed System.Collections.IEnumerable,
    /// so we need to go through Il2CppInterop's type system.
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

                    if (results.Count > 0)
                    {
                        _log.Msg($"Enumerated via Il2Cpp IEnumerable: {results.Count} items");
                        return results;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Msg($"Il2Cpp IEnumerable strategy failed: {ex.Message}");
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

                        if (results.Count > 0)
                        {
                            _log.Msg($"Enumerated via reflection GetEnumerator: {results.Count} items");
                            return results;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.Msg($"Reflection GetEnumerator strategy failed: {ex.Message}");
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
                _log.Msg($"Collection Count={count}, trying indexer...");

                var indexer = collType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p => p.GetIndexParameters().Length == 1);

                if (indexer != null)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var item = indexer.GetValue(collection, new object[] { i });
                        AddAsUnityObject(results, item);
                    }

                    if (results.Count > 0)
                    {
                        _log.Msg($"Enumerated via Count+indexer: {results.Count} items");
                        return results;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.Msg($"Count+indexer strategy failed: {ex.Message}");
        }

        // Strategy 4: managed IEnumerable (last resort, works for Il2CppArrayBase etc.)
        if (collection is System.Collections.IEnumerable managedEnumerable)
        {
            foreach (var item in managedEnumerable)
                AddAsUnityObject(results, item);

            if (results.Count > 0)
                _log.Msg($"Enumerated via managed IEnumerable: {results.Count} items");
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
    /// Resolves all enum values, properties, and types by NAME.
    /// No hardcoded offsets — Il2CppInterop proxy types handle offset mapping.
    /// </summary>
    private bool ResolveReflectionCache(Assembly gameAssembly)
    {
        try
        {
            // Log all EntityTemplate properties so we can see what the EA build has
            var etProps = _entityTemplateType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => $"{p.Name}:{p.PropertyType.Name}")
                .ToList();
            _log.Msg($"EntityTemplate properties ({etProps.Count}): {string.Join(", ", etProps)}");

            // EntityType enum — find the "Actor" value
            var entityTypeEnum = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "EntityType" && t.IsEnum);
            if (entityTypeEnum != null)
            {
                var allNames = Enum.GetNames(entityTypeEnum);
                _log.Msg($"EntityType enum values: {string.Join(", ", allNames)}");
                foreach (var name in allNames)
                {
                    if (name == "Actor")
                    {
                        _entityTypeActorValue = Enum.Parse(entityTypeEnum, name);
                        break;
                    }
                }
            }
            else
            {
                _log.Warning("EntityType enum not found in assembly");
            }

            if (_entityTypeActorValue == null)
            {
                _log.Warning("Could not find EntityType.Actor enum value");
                return false;
            }

            // EntityTemplate properties (Il2CppInterop exposes IL2CPP fields as properties)
            _entityTypeProperty = _entityTemplateType.GetProperty("Type",
                BindingFlags.Public | BindingFlags.Instance);
            _actorTypeProperty = _entityTemplateType.GetProperty("ActorType",
                BindingFlags.Public | BindingFlags.Instance);

            if (_entityTypeProperty == null)
            {
                _log.Warning("EntityTemplate.Type property not found");
                return false;
            }

            _log.Msg($"Resolved EntityTemplate.Type -> {_entityTypeProperty.PropertyType.Name}");
            if (_actorTypeProperty != null)
                _log.Msg($"Resolved EntityTemplate.ActorType -> {_actorTypeProperty.PropertyType.Name}");

            // DeveloperSettingType enum — look up CheatsEnabled and ShowDeveloperSettings by name
            var devSettingEnum = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DeveloperSettingType" && t.IsEnum);
            if (devSettingEnum != null)
            {
                var names = Enum.GetNames(devSettingEnum);
                var values = Enum.GetValues(devSettingEnum);
                // Log all enum values so we can see what the EA build has
                var enumEntries = new List<string>();
                for (int i = 0; i < names.Length; i++)
                {
                    int val = Convert.ToInt32(values.GetValue(i));
                    enumEntries.Add($"{names[i]}={val}");
                    if (names[i] == "CheatsEnabled") _cheatsEnabledIndex = val;
                    else if (names[i] == "ShowDeveloperSettings") _showDevSettingsIndex = val;
                }
                _log.Msg($"DeveloperSettingType values ({names.Length}): {string.Join(", ", enumEntries)}");
            }
            else
            {
                var enumCandidates = gameAssembly.GetTypes()
                    .Where(t => t.IsEnum && (t.Name.Contains("Developer") || t.Name.Contains("Setting") || t.Name.Contains("Cheat")))
                    .Select(t => t.Name)
                    .Take(15);
                _log.Warning($"DeveloperSettingType enum not found. Candidates: {string.Join(", ", enumCandidates)}");
            }

            if (_cheatsEnabledIndex >= 0)
                _log.Msg($"DeveloperSettingType.CheatsEnabled = {_cheatsEnabledIndex}");
            else
                _log.Warning("CheatsEnabled not found in DeveloperSettingType");
            if (_showDevSettingsIndex >= 0)
                _log.Msg($"DeveloperSettingType.ShowDeveloperSettings = {_showDevSettingsIndex}");
            else
                _log.Warning("ShowDeveloperSettings not found in DeveloperSettingType");

            // FactionType enum — build the full list dynamically
            _factionEnumType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "FactionType" && t.IsEnum);
            if (_factionEnumType != null)
            {
                var names = Enum.GetNames(_factionEnumType);
                var values = Enum.GetValues(_factionEnumType);

                // Collect all, then sort: enemy-looking factions first, Player/Neutral last
                var enemies = new List<(string Name, object Value)>();
                var others = new List<(string Name, object Value)>();

                for (int i = 0; i < names.Length; i++)
                {
                    var name = names[i];
                    var val = values.GetValue(i);

                    if (name == "Player" || name == "PlayerAI" || name == "Neutral")
                        others.Add((name, val));
                    else
                        enemies.Add((name, val));
                }

                _factions.AddRange(enemies);
                _factions.AddRange(others);

                _log.Msg($"FactionType: {_factions.Count} factions ({string.Join(", ", _factions.Select(f => f.Name))})");
            }

            // ActorType enum names (for display)
            var actorTypeEnum = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "ActorType" && t.IsEnum);
            if (actorTypeEnum != null)
                _log.Msg($"ActorType values: {string.Join(", ", Enum.GetNames(actorTypeEnum))}");

            return true;
        }
        catch (Exception ex)
        {
            _log.Warning($"ResolveReflectionCache error: {ex.Message}");
            return false;
        }
    }

    private void EnableCheats(Assembly gameAssembly)
    {
        if (_cheatsEnabledIndex < 0)
        {
            _log.Warning("CheatsEnabled enum index not resolved, skipping");
            return;
        }

        try
        {
            var devSettingsType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DevSettings");
            if (devSettingsType == null)
            {
                // Look for renamed settings types
                var settingsCandidates = gameAssembly.GetTypes()
                    .Where(t => t.Name.Contains("Setting") || t.Name.Contains("Dev") || t.Name.Contains("Cheat"))
                    .Select(t => t.Name)
                    .Take(20);
                _log.Warning($"DevSettings type not found. Candidates: {string.Join(", ", settingsCandidates)}");
                return;
            }

            // Log all fields on DevSettings so we can see what the EA build has
            var allFields = devSettingsType.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .Select(f => f.Name)
                .ToList();
            _log.Msg($"DevSettings fields ({allFields.Count}): {string.Join(", ", allFields.Take(30))}");

            var nativeFieldInfo = devSettingsType.GetField("NativeFieldInfoPtr_VALUES",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (nativeFieldInfo == null)
            {
                _log.Warning("DevSettings.NativeFieldInfoPtr_VALUES not found");
                return;
            }

            IntPtr fieldInfoPtr = (IntPtr)nativeFieldInfo.GetValue(null);
            _log.Msg($"DevSettings fieldInfoPtr = 0x{fieldInfoPtr.ToInt64():X}");
            if (fieldInfoPtr == IntPtr.Zero) return;

            IntPtr arrayPtr;
            unsafe
            {
                IntPtr temp = IntPtr.Zero;
                IL2CPP.il2cpp_field_static_get_value(fieldInfoPtr, &temp);
                arrayPtr = temp;
            }

            _log.Msg($"DevSettings arrayPtr = 0x{arrayPtr.ToInt64():X}");
            if (arrayPtr == IntPtr.Zero) return;

            // IL2CPP array header is 0x20 bytes — this is an IL2CPP runtime constant, not game-specific
            const int headerSize = 0x20;
            const int elemSize = 4;

            // Read array length from IL2CPP array header (length is at offset 0x18)
            int arrayLength = Marshal.ReadInt32(arrayPtr + 0x18);
            _log.Msg($"DevSettings VALUES array length = {arrayLength}, CheatsEnabled index = {_cheatsEnabledIndex}, ShowDevSettings index = {_showDevSettingsIndex}");

            if (_cheatsEnabledIndex >= arrayLength)
            {
                _log.Warning($"CheatsEnabled index {_cheatsEnabledIndex} is out of bounds (array length {arrayLength})");
                return;
            }

            Marshal.WriteInt32(arrayPtr + headerSize + (_cheatsEnabledIndex * elemSize), 1);

            if (_showDevSettingsIndex >= 0 && _showDevSettingsIndex < arrayLength)
                Marshal.WriteInt32(arrayPtr + headerSize + (_showDevSettingsIndex * elemSize), 1);

            _cheatsEnabled = Marshal.ReadInt32(arrayPtr + headerSize + (_cheatsEnabledIndex * elemSize)) == 1;
            _log.Msg($"CheatsEnabled = {_cheatsEnabled}");
        }
        catch (Exception ex)
        {
            _log.Warning($"EnableCheats error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void CacheActions(Assembly gameAssembly)
    {
        try
        {
            var tacticalStateType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "TacticalState");
            if (tacticalStateType != null)
            {
                // Log all public methods so we can see what's available
                var methods = tacticalStateType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                    .Where(m => !m.IsSpecialName)
                    .Select(m => $"{(m.IsStatic ? "static " : "")}{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})")
                    .ToList();
                _log.Msg($"TacticalState methods ({methods.Count}): {string.Join(", ", methods.Take(25))}");

                _tacticalStateGet = tacticalStateType.GetMethod("Get",
                    BindingFlags.Public | BindingFlags.Static);
                _startDevModeAction = tacticalStateType.GetMethod("StartDevModeAction",
                    BindingFlags.Public | BindingFlags.Instance);

                _log.Msg($"TacticalState.Get={_tacticalStateGet != null}, StartDevModeAction={_startDevModeAction != null}");
            }
            else
            {
                var stateCandidates = gameAssembly.GetTypes()
                    .Where(t => t.Name.Contains("Tactical") || t.Name.Contains("State"))
                    .Select(t => t.Name)
                    .Take(20);
                _log.Warning($"TacticalState not found. Candidates: {string.Join(", ", stateCandidates)}");
            }

            // GodModeAction — EA build takes GodModeTarget enum, demo was parameterless
            var godModeType = gameAssembly.GetTypes().FirstOrDefault(t => t.Name == "GodModeAction");
            if (godModeType != null)
            {
                // Try parameterless first (demo), then 1-param (EA)
                _godModeActionCtor = godModeType.GetConstructor(Type.EmptyTypes);
                if (_godModeActionCtor == null)
                {
                    _godModeActionCtor = godModeType.GetConstructors()
                        .FirstOrDefault(c => c.GetParameters().Length == 1);

                    if (_godModeActionCtor != null)
                    {
                        // Resolve GodModeTarget.Target (the "click on a unit" mode)
                        var targetEnum = godModeType.GetNestedTypes()
                            .FirstOrDefault(t => t.IsEnum && t.Name == "GodModeTarget");
                        if (targetEnum != null)
                        {
                            var targetName = Enum.GetNames(targetEnum).FirstOrDefault(n => n == "Target") ?? Enum.GetNames(targetEnum).First();
                            _godModeTargetValue = Enum.Parse(targetEnum, targetName);
                            _log.Msg($"GodModeAction: using 1-param ctor with GodModeTarget.{targetName}");
                        }
                        else
                        {
                            _log.Warning("GodModeAction: found 1-param ctor but couldn't resolve GodModeTarget enum");
                            _godModeActionCtor = null;
                        }
                    }
                }
                else
                {
                    _log.Msg("GodModeAction: using parameterless ctor (demo build)");
                }
            }

            CacheActionCtor(gameAssembly, "DeleteEntityAction", out _deleteEntityActionCtor);

            var spawnType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "SpawnEntityAction");
            if (spawnType != null)
            {
                // Log all constructors so we can see what signatures are available
                var ctors = spawnType.GetConstructors()
                    .Select(c => $"({string.Join(", ", c.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})")
                    .ToList();
                _log.Msg($"SpawnEntityAction constructors: {string.Join(", ", ctors)}");

                _spawnEntityActionCtor = spawnType.GetConstructors()
                    .FirstOrDefault(c => c.GetParameters().Length == 2);
            }
            else
            {
                var actionCandidates = gameAssembly.GetTypes()
                    .Where(t => t.Name.Contains("Spawn") || t.Name.Contains("Action"))
                    .Select(t => t.Name)
                    .Take(20);
                _log.Warning($"SpawnEntityAction not found. Candidates: {string.Join(", ", actionCandidates)}");
            }

            _log.Msg($"Actions: GodMode={_godModeActionCtor != null}, " +
                             $"Delete={_deleteEntityActionCtor != null}, " +
                             $"SpawnCtor={_spawnEntityActionCtor != null}");
        }
        catch (Exception ex)
        {
            _log.Warning($"CacheActions error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void LoadEntityTemplates(UnityEngine.Object[] allObjects)
    {
        int totalCount = 0;
        int nullPtrCount = 0;
        int nonActorCount = 0;
        int noNameCount = 0;
        int errorCount = 0;
        var seenEntityTypes = new HashSet<string>();
        var entries = new List<(string name, string actorType, UnityEngine.Object obj)>();

        foreach (var obj in allObjects)
        {
            if (obj == null) continue;
            totalCount++;

            try
            {
                IntPtr ptr = IntPtr.Zero;
                if (obj is Il2CppObjectBase il2cppObj)
                    ptr = il2cppObj.Pointer;
                if (ptr == IntPtr.Zero) { nullPtrCount++; continue; }

                // Create typed proxy for property access (no hardcoded offsets)
                var typed = Activator.CreateInstance(_entityTemplateType, new object[] { ptr });

                // Filter: only EntityType.Actor
                var entityTypeVal = _entityTypeProperty.GetValue(typed);
                var entityTypeName = entityTypeVal?.ToString() ?? "null";
                seenEntityTypes.Add(entityTypeName);

                if (!Equals(entityTypeVal, _entityTypeActorValue)) { nonActorCount++; continue; }

                // Read ActorType for display
                string actorTypeName = "Unit";
                if (_actorTypeProperty != null)
                {
                    var actorVal = _actorTypeProperty.GetValue(typed);
                    if (actorVal != null)
                        actorTypeName = actorVal.ToString();
                }

                string name = "(unknown)";
                try { name = obj.name; } catch { }
                if (string.IsNullOrEmpty(name) || name == "(unknown)") { noNameCount++; continue; }

                entries.Add((name, actorTypeName, obj));
            }
            catch (Exception ex)
            {
                errorCount++;
                if (errorCount <= 3)
                    _log.Warning($"Entity iteration error #{errorCount}: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        _log.Msg($"Entity scan: {totalCount} total, {entries.Count} actors, {nonActorCount} non-actor, {nullPtrCount} null ptr, {noNameCount} unnamed, {errorCount} errors");
        _log.Msg($"EntityType values seen: {string.Join(", ", seenEntityTypes)}");

        entries.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

        foreach (var (name, actorType, obj) in entries)
        {
            _entityTemplates.Add(obj);
            _entityNames.Add(name);
            _entityActorTypes.Add(actorType);
        }

        _log.Msg($"Entity templates: {_entityTemplates.Count} actors (filtered from {totalCount} total)");

        // Log first 20 entity names for diagnostics
        var sample = entries.Take(20).Select(e => $"{e.name} ({e.actorType})");
        _log.Msg($"First entities: {string.Join(", ", sample)}");
    }

    private void CacheActionCtor(Assembly gameAssembly, string typeName, out ConstructorInfo ctor)
    {
        ctor = null;
        var type = gameAssembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
        if (type != null)
            ctor = type.GetConstructor(Type.EmptyTypes);
    }

    private void SetStatus(string msg)
    {
        _statusMessage = msg;
        _statusTime = Time.time;
    }

    public void OnUpdate()
    {
        _updateCount++;

        // Early diagnostics — runs once after a few frames, regardless of scene callbacks
        if (!_earlyDiagDone && _updateCount == 60)
        {
            _earlyDiagDone = true;
            _log.Msg($"--- Early diagnostics (frame {_updateCount}) ---");
            _log.Msg($"sceneSeen={_sceneSeen}, devModeReady={_devModeReady}");
            _log.Msg($"Active scene: '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}'");

            try
            {
                int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
                _log.Msg($"Scenes in build settings: {sceneCount}");
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    _log.Msg($"  Loaded scene [{i}]: '{s.name}' (path: {s.path}, isLoaded: {s.isLoaded})");
                }
            }
            catch (Exception ex)
            {
                _log.Warning($"Scene enumeration error: {ex.Message}");
            }

            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var gameAsm = assemblies.FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                _log.Msg($"Loaded assemblies: {assemblies.Length}, Assembly-CSharp: {(gameAsm != null ? $"yes ({gameAsm.GetTypes().Length} types)" : "NOT FOUND")}");
            }
            catch (Exception ex)
            {
                _log.Warning($"Assembly check error: {ex.Message}");
            }

            if (!_sceneSeen)
                _log.Warning("OnSceneWasLoaded has NOT fired yet — scene callback may not be working");

            _log.Msg("--- End early diagnostics ---");
        }

        if (!_devModeReady) return;

        try
        {
            if (!DevConsole.IsVisible) return;

            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                if (_entityTemplates.Count > 0)
                {
                    _selectedEntityIndex = (_selectedEntityIndex + 1) % _entityTemplates.Count;
                    SetStatus("");
                }
            }
            else if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                if (_entityTemplates.Count > 0)
                {
                    _selectedEntityIndex = (_selectedEntityIndex - 1 + _entityTemplates.Count) % _entityTemplates.Count;
                    SetStatus("");
                }
            }
            else if (Input.GetKeyDown(KeyCode.Backslash))
            {
                if (_factions.Count > 0)
                {
                    _selectedFactionIndex = (_selectedFactionIndex + 1) % _factions.Count;
                    SetStatus("");
                }
            }
            else if (Input.GetKeyDown(KeyCode.Return))
            {
                TrySpawnEntity();
            }
            else if (Input.GetKeyDown(KeyCode.F2))
            {
                TryStartAction(_godModeActionCtor, _godModeTargetValue, "God Mode - click unit");
            }
            else if (Input.GetKeyDown(KeyCode.F3))
            {
                TryStartAction(_deleteEntityActionCtor, null, "Delete - click unit");
            }
        }
        catch { }
    }

    private void TrySpawnEntity()
    {
        if (_tacticalStateGet == null || _startDevModeAction == null ||
            _spawnEntityActionCtor == null || _factionEnumType == null ||
            _entityTemplates.Count == 0 || _factions.Count == 0)
        {
            SetStatus("Spawn system not ready");
            return;
        }

        try
        {
            var state = _tacticalStateGet.Invoke(null, null);
            if (state == null)
            {
                SetStatus("Not in tactical");
                return;
            }

            var rawObj = _entityTemplates[_selectedEntityIndex];
            var ptr = ((Il2CppObjectBase)rawObj).Pointer;
            var typedTemplate = Activator.CreateInstance(_entityTemplateType, new object[] { ptr });

            var faction = _factions[_selectedFactionIndex];

            var action = _spawnEntityActionCtor.Invoke(new object[] { typedTemplate, faction.EnumValue });

            SetStatus("Click tile to place");
            _startDevModeAction.Invoke(state, new[] { action });
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException ?? ex;
            SetStatus($"Error: {inner.Message}");
            _log.Warning($"Spawn error: {inner.Message}");
        }
    }

    private void TryStartAction(ConstructorInfo actionCtor, object ctorArg, string description)
    {
        if (_tacticalStateGet == null || _startDevModeAction == null || actionCtor == null)
            return;

        try
        {
            var state = _tacticalStateGet.Invoke(null, null);
            if (state == null)
            {
                SetStatus("Not in tactical");
                return;
            }

            // Create action instance on demand (not ahead of time) to avoid
            // stale IL2CPP objects persisting across scene transitions
            var action = ctorArg != null
                ? actionCtor.Invoke(new[] { ctorArg })
                : actionCtor.Invoke(null);
            SetStatus(description);
            _startDevModeAction.Invoke(state, new[] { action });
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException ?? ex;
            if (!inner.Message.Contains("null"))
                SetStatus($"Error: {inner.Message}");
        }
    }

    // ==================== DevConsole Panel ====================

    private const float LH = 22f;   // line height — enough for font size 13 + padding
    private const float BH = 24f;   // button height
    private const float BW = 32f;   // small nav button width

    private void DrawDevModePanel(Rect area)
    {
        float cx = area.x;
        float cy = area.y;
        float cw = area.width;

        if (!_devModeReady)
        {
            GUI.Label(new Rect(cx, cy, cw, LH), "Dev Mode loading...");
            return;
        }

        GUI.Label(new Rect(cx, cy, cw, LH), $"Cheats: {(_cheatsEnabled ? "ON" : "OFF")}");
        cy += LH + 2;

        if (_entityTemplates.Count > 0 && _factions.Count > 0)
        {
            // --- Faction row: [<] FactionName [>] ---
            float btnX = cx;
            if (GUI.Button(new Rect(btnX, cy, BW, BH), "<"))
            {
                _selectedFactionIndex = (_selectedFactionIndex - 1 + _factions.Count) % _factions.Count;
                SetStatus("");
            }
            btnX += BW + 4;

            var faction = _factions[_selectedFactionIndex];
            GUI.Label(new Rect(btnX, cy + 2, cw - BW * 2 - 12, LH), $"Faction: {faction.Name}");

            if (GUI.Button(new Rect(cx + cw - BW, cy, BW, BH), ">"))
            {
                _selectedFactionIndex = (_selectedFactionIndex + 1) % _factions.Count;
                SetStatus("");
            }
            cy += BH + 4;

            // --- Unit row: [<] UnitName [>] ---
            btnX = cx;
            if (GUI.Button(new Rect(btnX, cy, BW, BH), "<"))
            {
                _selectedEntityIndex = (_selectedEntityIndex - 1 + _entityTemplates.Count) % _entityTemplates.Count;
                SetStatus("");
            }
            btnX += BW + 4;

            var entityName = _entityNames[_selectedEntityIndex];
            var actorType = _entityActorTypes[_selectedEntityIndex];
            GUI.Label(new Rect(btnX, cy + 2, cw - BW * 2 - 12, LH),
                $"{entityName}  [{actorType}]  ({_selectedEntityIndex + 1}/{_entityTemplates.Count})");

            if (GUI.Button(new Rect(cx + cw - BW, cy, BW, BH), ">"))
            {
                _selectedEntityIndex = (_selectedEntityIndex + 1) % _entityTemplates.Count;
                SetStatus("");
            }
            cy += BH + 6;

            // --- Action buttons ---
            float abw = 80f;
            float gap = 6f;
            float abx = cx;

            if (GUI.Button(new Rect(abx, cy, abw, BH), "Spawn"))
                TrySpawnEntity();
            abx += abw + gap;

            if (GUI.Button(new Rect(abx, cy, abw, BH), "God Mode"))
                TryStartAction(_godModeActionCtor, _godModeTargetValue, "God Mode - click unit");
            abx += abw + gap;

            if (GUI.Button(new Rect(abx, cy, abw, BH), "Delete"))
                TryStartAction(_deleteEntityActionCtor, null, "Delete - click unit");
            cy += BH + 4;
        }
        else
        {
            GUI.Label(new Rect(cx, cy, cw, LH), "No entities loaded");
            cy += LH + 4;
        }

        // --- Status message ---
        if (!string.IsNullOrEmpty(_statusMessage))
        {
            float elapsed = 0f;
            try { elapsed = Time.time - _statusTime; } catch { }
            if (elapsed < 5f)
                GUI.Label(new Rect(cx, cy, cw, LH), _statusMessage);
        }
    }

    public void OnGUI()
    {
        // Drawing is handled by the DevConsole panel system
    }
}
