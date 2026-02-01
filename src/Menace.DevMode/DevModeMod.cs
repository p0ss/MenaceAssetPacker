using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

[assembly: MelonInfo(typeof(Menace.DevMode.DevModeMod), "Menace Dev Mode", "1.0.0", "Menace Modkit")]
[assembly: MelonGame(null, null)]

namespace Menace.DevMode;

public class DevModeMod : MelonMod
{
    private bool _devModeReady;
    private bool _sceneSeen;
    private bool _cheatsEnabled;
    private bool _showUI;
    private string _statusMessage = "";
    private float _statusTime;

    // Reflection cache — all looked up by name, no hardcoded offsets
    private MethodInfo _tacticalStateGet;
    private MethodInfo _startDevModeAction;
    private object _godModeAction;
    private object _deleteEntityAction;
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

    // GUI
    private GUIStyle _boxStyle;
    private GUIStyle _headerStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _entityStyle;
    private GUIStyle _factionStyle;
    private GUIStyle _statusStyle;
    private GUIStyle _helpStyle;
    private bool _stylesInitialized;

    public override void OnInitializeMelon()
    {
        LoggerInstance.Msg("Menace Dev Mode v1.0.0");
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (sceneName == "Title" && !_sceneSeen)
        {
            _sceneSeen = true;
            MelonCoroutines.Start(WaitAndRunPhases());
        }
    }

    private System.Collections.IEnumerator WaitAndRunPhases()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            yield return new WaitForSeconds(2f);

            if (TryRunPhases())
            {
                LoggerInstance.Msg("Dev mode setup complete.");
                yield break;
            }
        }

        LoggerInstance.Warning("Failed to set up dev mode after 30 attempts.");
        _devModeReady = true;
    }

    private bool TryRunPhases()
    {
        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (gameAssembly == null)
            return false;

        _entityTemplateType = gameAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "EntityTemplate" && !t.IsAbstract);

        if (_entityTemplateType == null)
            return false;

        var il2cppType = Il2CppType.From(_entityTemplateType);
        var objects = Resources.FindObjectsOfTypeAll(il2cppType);

        if (objects == null || objects.Length == 0)
            return false;

        // Resolve all enums and properties by name before using them
        if (!ResolveReflectionCache(gameAssembly))
            return false;

        EnableCheats(gameAssembly);
        CacheActions(gameAssembly);
        LoadEntityTemplates(objects);

        _devModeReady = true;
        _showUI = true;
        return true;
    }

    /// <summary>
    /// Resolves all enum values, properties, and types by NAME.
    /// No hardcoded offsets — Il2CppInterop proxy types handle offset mapping.
    /// </summary>
    private bool ResolveReflectionCache(Assembly gameAssembly)
    {
        try
        {
            // EntityType enum — find the "Actor" value
            var entityTypeEnum = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "EntityType" && t.IsEnum);
            if (entityTypeEnum != null)
            {
                foreach (var name in Enum.GetNames(entityTypeEnum))
                {
                    if (name == "Actor")
                    {
                        _entityTypeActorValue = Enum.Parse(entityTypeEnum, name);
                        break;
                    }
                }
            }

            if (_entityTypeActorValue == null)
            {
                LoggerInstance.Warning("Could not find EntityType.Actor enum value");
                return false;
            }

            // EntityTemplate properties (Il2CppInterop exposes IL2CPP fields as properties)
            _entityTypeProperty = _entityTemplateType.GetProperty("Type",
                BindingFlags.Public | BindingFlags.Instance);
            _actorTypeProperty = _entityTemplateType.GetProperty("ActorType",
                BindingFlags.Public | BindingFlags.Instance);

            if (_entityTypeProperty == null)
            {
                LoggerInstance.Warning("EntityTemplate.Type property not found");
                return false;
            }

            LoggerInstance.Msg($"Resolved EntityTemplate.Type -> {_entityTypeProperty.PropertyType.Name}");
            if (_actorTypeProperty != null)
                LoggerInstance.Msg($"Resolved EntityTemplate.ActorType -> {_actorTypeProperty.PropertyType.Name}");

            // DeveloperSettingType enum — look up CheatsEnabled and ShowDeveloperSettings by name
            var devSettingEnum = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DeveloperSettingType" && t.IsEnum);
            if (devSettingEnum != null)
            {
                var names = Enum.GetNames(devSettingEnum);
                var values = Enum.GetValues(devSettingEnum);
                for (int i = 0; i < names.Length; i++)
                {
                    int val = Convert.ToInt32(values.GetValue(i));
                    if (names[i] == "CheatsEnabled") _cheatsEnabledIndex = val;
                    else if (names[i] == "ShowDeveloperSettings") _showDevSettingsIndex = val;
                }
            }

            if (_cheatsEnabledIndex >= 0)
                LoggerInstance.Msg($"DeveloperSettingType.CheatsEnabled = {_cheatsEnabledIndex}");
            if (_showDevSettingsIndex >= 0)
                LoggerInstance.Msg($"DeveloperSettingType.ShowDeveloperSettings = {_showDevSettingsIndex}");

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

                LoggerInstance.Msg($"FactionType: {_factions.Count} factions ({string.Join(", ", _factions.Select(f => f.Name))})");
            }

            // ActorType enum names (for display)
            var actorTypeEnum = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "ActorType" && t.IsEnum);
            if (actorTypeEnum != null)
                LoggerInstance.Msg($"ActorType values: {string.Join(", ", Enum.GetNames(actorTypeEnum))}");

            return true;
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"ResolveReflectionCache error: {ex.Message}");
            return false;
        }
    }

    private void EnableCheats(Assembly gameAssembly)
    {
        if (_cheatsEnabledIndex < 0)
        {
            LoggerInstance.Warning("CheatsEnabled enum index not resolved, skipping");
            return;
        }

        try
        {
            var devSettingsType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DevSettings");
            if (devSettingsType == null) return;

            var nativeFieldInfo = devSettingsType.GetField("NativeFieldInfoPtr_VALUES",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (nativeFieldInfo == null) return;

            IntPtr fieldInfoPtr = (IntPtr)nativeFieldInfo.GetValue(null);
            if (fieldInfoPtr == IntPtr.Zero) return;

            IntPtr arrayPtr;
            unsafe
            {
                IntPtr temp = IntPtr.Zero;
                IL2CPP.il2cpp_field_static_get_value(fieldInfoPtr, &temp);
                arrayPtr = temp;
            }

            if (arrayPtr == IntPtr.Zero) return;

            // IL2CPP array header is 0x20 bytes — this is an IL2CPP runtime constant, not game-specific
            const int headerSize = 0x20;
            const int elemSize = 4;

            Marshal.WriteInt32(arrayPtr + headerSize + (_cheatsEnabledIndex * elemSize), 1);

            if (_showDevSettingsIndex >= 0)
                Marshal.WriteInt32(arrayPtr + headerSize + (_showDevSettingsIndex * elemSize), 1);

            _cheatsEnabled = Marshal.ReadInt32(arrayPtr + headerSize + (_cheatsEnabledIndex * elemSize)) == 1;
            LoggerInstance.Msg($"CheatsEnabled = {_cheatsEnabled}");
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"EnableCheats error: {ex.Message}");
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
                _tacticalStateGet = tacticalStateType.GetMethod("Get",
                    BindingFlags.Public | BindingFlags.Static);
                _startDevModeAction = tacticalStateType.GetMethod("StartDevModeAction",
                    BindingFlags.Public | BindingFlags.Instance);
            }

            CacheSimpleAction(gameAssembly, "GodModeAction", out _godModeAction);
            CacheSimpleAction(gameAssembly, "DeleteEntityAction", out _deleteEntityAction);

            var spawnType = gameAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "SpawnEntityAction");
            if (spawnType != null)
            {
                _spawnEntityActionCtor = spawnType.GetConstructors()
                    .FirstOrDefault(c => c.GetParameters().Length == 2);
            }

            LoggerInstance.Msg($"Actions: GodMode={_godModeAction != null}, " +
                             $"Delete={_deleteEntityAction != null}, " +
                             $"SpawnCtor={_spawnEntityActionCtor != null}");
        }
        catch (Exception ex)
        {
            LoggerInstance.Warning($"CacheActions error: {ex.Message}");
        }
    }

    private void LoadEntityTemplates(UnityEngine.Object[] allObjects)
    {
        int totalCount = 0;
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
                if (ptr == IntPtr.Zero) continue;

                // Create typed proxy for property access (no hardcoded offsets)
                var typed = Activator.CreateInstance(_entityTemplateType, new object[] { ptr });

                // Filter: only EntityType.Actor
                var entityTypeVal = _entityTypeProperty.GetValue(typed);
                if (!Equals(entityTypeVal, _entityTypeActorValue)) continue;

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
                if (string.IsNullOrEmpty(name) || name == "(unknown)") continue;

                entries.Add((name, actorTypeName, obj));
            }
            catch { }
        }

        entries.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

        foreach (var (name, actorType, obj) in entries)
        {
            _entityTemplates.Add(obj);
            _entityNames.Add(name);
            _entityActorTypes.Add(actorType);
        }

        LoggerInstance.Msg($"Entity templates: {_entityTemplates.Count} actors (filtered from {totalCount} total)");
    }

    private void CacheSimpleAction(Assembly gameAssembly, string typeName, out object action)
    {
        action = null;
        var type = gameAssembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
        if (type != null)
        {
            var ctor = type.GetConstructor(Type.EmptyTypes);
            if (ctor != null)
                action = ctor.Invoke(null);
        }
    }

    private void SetStatus(string msg)
    {
        _statusMessage = msg;
        _statusTime = Time.time;
    }

    public override void OnUpdate()
    {
        if (!_devModeReady) return;

        try
        {
            if (Input.GetKeyDown(KeyCode.BackQuote))
                _showUI = !_showUI;

            if (!_showUI) return;

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
                TryStartSimpleAction(_godModeAction, "God Mode - click unit");
            }
            else if (Input.GetKeyDown(KeyCode.F3))
            {
                TryStartSimpleAction(_deleteEntityAction, "Delete - click unit");
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
            LoggerInstance.Warning($"Spawn error: {inner.Message}");
        }
    }

    private void TryStartSimpleAction(object action, string description)
    {
        if (_tacticalStateGet == null || _startDevModeAction == null || action == null)
            return;

        try
        {
            var state = _tacticalStateGet.Invoke(null, null);
            if (state == null)
            {
                SetStatus("Not in tactical");
                return;
            }

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

    // ==================== In-Game GUI ====================

    private void InitStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        var bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.85f));
        bgTex.Apply();

        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = bgTex, textColor = Color.white },
            padding = new RectOffset(10, 10, 8, 8),
        };

        _headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.8f, 0.2f) },
            alignment = TextAnchor.MiddleCenter,
        };

        _factionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.4f, 0.4f) },
        };

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = new Color(0.7f, 0.7f, 0.7f) },
        };

        _entityStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };

        _statusStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Italic,
            normal = { textColor = new Color(0.5f, 1f, 0.5f) },
        };

        _helpStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f) },
        };
    }

    public override void OnGUI()
    {
        if (!_devModeReady || !_showUI) return;

        InitStyles();

        float w = 300;
        float h = 180;
        float x = Screen.width - w - 80;
        float y = 10;

        GUI.Box(new Rect(x, y, w, h), "", _boxStyle);

        float cx = x + 10;
        float cy = y + 6;
        float cw = w - 20;

        GUI.Label(new Rect(cx, cy, cw, 20), "DEV MODE", _headerStyle);
        cy += 22;

        if (_entityTemplates.Count > 0 && _factions.Count > 0)
        {
            var faction = _factions[_selectedFactionIndex];
            GUI.Label(new Rect(cx, cy, cw, 18), faction.Name, _factionStyle);
            cy += 20;

            var entityName = _entityNames[_selectedEntityIndex];
            GUI.Label(new Rect(cx, cy, cw, 20), entityName, _entityStyle);
            cy += 18;

            var actorType = _entityActorTypes[_selectedEntityIndex];
            GUI.Label(new Rect(cx, cy, cw, 16),
                $"{actorType}  ({_selectedEntityIndex + 1}/{_entityTemplates.Count})", _labelStyle);
            cy += 20;
        }
        else
        {
            GUI.Label(new Rect(cx, cy, cw, 18), "No entities loaded", _labelStyle);
            cy += 40;
        }

        if (!string.IsNullOrEmpty(_statusMessage) && Time.time - _statusTime < 5f)
        {
            GUI.Label(new Rect(cx, cy, cw, 16), _statusMessage, _statusStyle);
        }
        cy += 18;

        GUI.Label(new Rect(cx, cy, cw, 14), "[ ]  Unit    \\  Faction    Enter  Spawn", _helpStyle);
        cy += 15;
        GUI.Label(new Rect(cx, cy, cw, 14), "F2  God mode    F3  Delete    ~  Hide", _helpStyle);
    }
}
