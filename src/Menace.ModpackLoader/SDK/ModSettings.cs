using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// SDK for modders to define custom settings that are rendered in-game and persisted.
///
/// Usage:
/// <code>
/// // In your plugin's OnInitialize:
/// ModSettings.Register("My Mod", settings => {
///     settings.AddHeader("Gameplay");
///     settings.AddSlider("SupplyMultiplier", "Supply Multiplier", 0.5f, 10f, 1f);
///     settings.AddToggle("EasyMode", "Easy Mode", false);
///     settings.AddNumber("StartingSquaddies", "Starting Squaddies", 1, 20, 4);
///     settings.AddDropdown("Difficulty", "Difficulty", new[] { "Easy", "Normal", "Hard" }, "Normal");
///     settings.AddText("CustomName", "Custom Name", "Player");
/// });
///
/// // Later, read values:
/// float multiplier = ModSettings.Get&lt;float&gt;("My Mod", "SupplyMultiplier");
/// bool easyMode = ModSettings.Get&lt;bool&gt;("My Mod", "EasyMode");
/// </code>
/// </summary>
public static class ModSettings
{
    private static readonly Dictionary<string, ModSettingsGroup> _groups = new();
    private static readonly object _fileLock = new();
    private static string _settingsPath;
    private static bool _initialized;
    private static bool _dirty;

    // Panel state
    private static Vector2 _scroll;
    private static readonly HashSet<string> _collapsedGroups = new();

    /// <summary>
    /// Event fired when any setting value changes.
    /// Subscribers receive (modName, settingKey, newValue).
    /// </summary>
    public static event Action<string, string, object> OnSettingChanged;

    /// <summary>
    /// Register settings for a mod. Call this during plugin initialization.
    /// If called multiple times for the same mod, settings are replaced.
    /// </summary>
    public static void Register(string modName, Action<SettingsBuilder> configure)
    {
        if (string.IsNullOrEmpty(modName))
        {
            SdkLogger.Warning("[ModSettings] Register called with null or empty modName");
            return;
        }
        if (configure == null)
        {
            SdkLogger.Warning($"[ModSettings] Register called with null configure callback for mod '{modName}'");
            return;
        }

        var builder = new SettingsBuilder(modName);
        configure(builder);

        var group = builder.Build();
        _groups[modName] = group;

        // Load saved values for this mod
        if (_initialized)
            LoadGroupValues(group);

        SdkLogger.Msg($"[ModSettings] Registered {group.Settings.Count} settings for '{modName}'");
    }

    /// <summary>
    /// Get a setting value. Returns the default if not found.
    /// </summary>
    public static T Get<T>(string modName, string key)
    {
        if (_groups.TryGetValue(modName, out var group))
        {
            var setting = group.Settings.FirstOrDefault(s => s.Key == key);
            if (setting != null && setting.Value is T typedValue)
                return typedValue;
            if (setting != null && setting.DefaultValue is T defaultTyped)
                return defaultTyped;
        }
        return default;
    }

    /// <summary>
    /// Get a setting value as object.
    /// </summary>
    public static object Get(string modName, string key)
    {
        if (_groups.TryGetValue(modName, out var group))
        {
            var setting = group.Settings.FirstOrDefault(s => s.Key == key);
            if (setting != null)
                return setting.Value ?? setting.DefaultValue;
        }
        return null;
    }

    /// <summary>
    /// Set a setting value programmatically.
    /// </summary>
    public static void Set<T>(string modName, string key, T value)
    {
        if (_groups.TryGetValue(modName, out var group))
        {
            var setting = group.Settings.FirstOrDefault(s => s.Key == key);
            if (setting != null)
            {
                setting.Value = value;
                _dirty = true;
                OnSettingChanged?.Invoke(modName, key, value);
            }
        }
    }

    /// <summary>
    /// Check if a mod has registered settings.
    /// </summary>
    public static bool HasSettings(string modName)
    {
        return _groups.ContainsKey(modName) && _groups[modName].Settings.Count > 0;
    }

    /// <summary>
    /// Check if any mod has registered settings.
    /// </summary>
    public static bool HasAnySettings()
    {
        return _groups.Count > 0 && _groups.Values.Any(g => g.Settings.Count > 0);
    }

    /// <summary>
    /// Get all registered mod names that have settings.
    /// </summary>
    public static IEnumerable<string> GetRegisteredMods()
    {
        return _groups.Keys.OrderBy(k => k);
    }

    /// <summary>
    /// Get all settings definitions for a mod (used by MenuInjector for native UI).
    /// </summary>
    public static IEnumerable<SettingDefinition> GetSettingsForMod(string modName)
    {
        if (_groups.TryGetValue(modName, out var group))
            return group.Settings;
        return Enumerable.Empty<SettingDefinition>();
    }

    // --- Internal lifecycle ---

    internal static void Initialize()
    {
        // UserData directory is typically at <game>/UserData/
        var userDataDir = Path.Combine(Directory.GetCurrentDirectory(), "UserData");
        if (!Directory.Exists(userDataDir))
            Directory.CreateDirectory(userDataDir);

        _settingsPath = Path.Combine(userDataDir, "ModSettings.json");

        LoadAllSettings();
        _initialized = true;

        // Register the Settings panel in DevConsole
        DevConsole.RegisterPanel("Settings", DrawSettingsPanel);

        SdkLogger.Msg($"[ModSettings] Initialized. Settings file: {_settingsPath}");
    }

    internal static void Save()
    {
        if (!_dirty) return;

        try
        {
            var data = new Dictionary<string, Dictionary<string, object>>();
            foreach (var (modName, group) in _groups)
            {
                var modData = new Dictionary<string, object>();
                foreach (var setting in group.Settings)
                {
                    if (setting.Value != null)
                        modData[setting.Key] = setting.Value;
                }
                if (modData.Count > 0)
                    data[modName] = modData;
            }

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            lock (_fileLock)
            {
                File.WriteAllText(_settingsPath, json);
            }
            _dirty = false;
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[ModSettings] Failed to save: {ex.Message}");
        }
    }

    private static void LoadAllSettings()
    {
        if (!File.Exists(_settingsPath))
            return;

        try
        {
            string json;
            lock (_fileLock)
            {
                json = File.ReadAllText(_settingsPath);
            }
            var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(json);
            if (data == null) return;

            foreach (var (modName, modData) in data)
            {
                if (!_groups.TryGetValue(modName, out var group))
                    continue;

                foreach (var (key, element) in modData)
                {
                    var setting = group.Settings.FirstOrDefault(s => s.Key == key);
                    if (setting == null) continue;

                    setting.Value = ConvertJsonElement(element, setting.Type);
                }
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[ModSettings] Failed to load: {ex.Message}");
        }
    }

    private static void LoadGroupValues(ModSettingsGroup group)
    {
        if (!File.Exists(_settingsPath))
            return;

        try
        {
            string json;
            lock (_fileLock)
            {
                json = File.ReadAllText(_settingsPath);
            }
            var data = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(json);
            if (data == null || !data.TryGetValue(group.ModName, out var modData))
                return;

            foreach (var (key, element) in modData)
            {
                var setting = group.Settings.FirstOrDefault(s => s.Key == key);
                if (setting == null) continue;

                setting.Value = ConvertJsonElement(element, setting.Type);
            }
        }
        catch { }
    }

    private static object ConvertJsonElement(JsonElement element, SettingType type)
    {
        return type switch
        {
            SettingType.Toggle => element.GetBoolean(),
            SettingType.Slider => element.GetSingle(),
            SettingType.Number => element.GetInt32(),
            SettingType.Text or SettingType.Dropdown or SettingType.Keybinding => element.GetString(),
            _ => null
        };
    }

    // --- Settings Panel UI ---

    private static void DrawSettingsPanel(Rect area)
    {
        float y = area.y;
        const float lineHeight = 22f;
        const float padding = 4f;

        // Help text
        GUI.Label(new Rect(area.x, y, area.width, lineHeight),
            "Mod settings. Changes are saved automatically.", GetHelpStyle());
        y += lineHeight + padding;

        if (_groups.Count == 0)
        {
            GUI.Label(new Rect(area.x, y, area.width, lineHeight),
                "No mods have registered settings.", GetLabelStyle());
            return;
        }

        // Calculate content height
        float contentHeight = 0;
        foreach (var (modName, group) in _groups.OrderBy(g => g.Key))
        {
            contentHeight += 28; // Header
            if (!_collapsedGroups.Contains(modName))
            {
                foreach (var setting in group.Settings)
                {
                    contentHeight += setting.Type == SettingType.Header ? 24 : 28;
                }
                contentHeight += padding;
            }
        }

        // Scrollable area
        float scrollHeight = area.yMax - y;
        var viewRect = new Rect(area.x, y, area.width, scrollHeight);
        _scroll.y = DevConsole.HandleScrollWheel(viewRect, contentHeight, _scroll.y);

        GUI.BeginGroup(viewRect);
        float sy = -_scroll.y;

        foreach (var (modName, group) in _groups.OrderBy(g => g.Key))
        {
            // Mod header (collapsible)
            bool collapsed = _collapsedGroups.Contains(modName);
            string arrow = collapsed ? ">" : "v";
            if (GUI.Button(new Rect(0, sy, viewRect.width, 24), $"{arrow} {modName}", GetHeaderStyle()))
            {
                if (collapsed) _collapsedGroups.Remove(modName);
                else _collapsedGroups.Add(modName);
            }
            sy += 28;

            if (collapsed)
                continue;

            // Settings
            foreach (var setting in group.Settings)
            {
                if (sy + 28 > 0 && sy < scrollHeight)
                {
                    DrawSetting(new Rect(8, sy, viewRect.width - 16, 24), setting, modName);
                }
                sy += setting.Type == SettingType.Header ? 24 : 28;
            }
            sy += padding;
        }

        GUI.EndGroup();
    }

    private static void DrawSetting(Rect rect, SettingDefinition setting, string modName)
    {
        const float labelWidth = 200f;

        switch (setting.Type)
        {
            case SettingType.Header:
                GUI.Label(new Rect(rect.x, rect.y + 4, rect.width, rect.height),
                    setting.Label, GetSubHeaderStyle());
                break;

            case SettingType.Toggle:
                GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), setting.Label, GetLabelStyle());
                bool boolVal = setting.Value is bool b ? b : (bool)(setting.DefaultValue ?? false);
                bool newBoolVal = GUI.Toggle(new Rect(rect.x + labelWidth, rect.y, 20, rect.height), boolVal, "");
                if (newBoolVal != boolVal)
                {
                    setting.Value = newBoolVal;
                    _dirty = true;
                    OnSettingChanged?.Invoke(modName, setting.Key, newBoolVal);
                }
                break;

            case SettingType.Slider:
                GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), setting.Label, GetLabelStyle());
                float floatVal = setting.Value is float f ? f : (float)(setting.DefaultValue ?? 0f);

                // Use button-based slider to avoid GUI.HorizontalSlider unstripping issues
                float sliderX = rect.x + labelWidth;
                float step = (setting.Max - setting.Min) / 20f; // 20 steps

                // << button (large decrement)
                if (GUI.Button(new Rect(sliderX, rect.y, 24, rect.height), "<<"))
                {
                    float newVal = Math.Max(setting.Min, floatVal - step * 5);
                    setting.Value = newVal;
                    _dirty = true;
                    OnSettingChanged?.Invoke(modName, setting.Key, newVal);
                }

                // < button (small decrement)
                if (GUI.Button(new Rect(sliderX + 26, rect.y, 24, rect.height), "<"))
                {
                    float newVal = Math.Max(setting.Min, floatVal - step);
                    setting.Value = newVal;
                    _dirty = true;
                    OnSettingChanged?.Invoke(modName, setting.Key, newVal);
                }

                // Value display
                GUI.Label(new Rect(sliderX + 54, rect.y, 55, rect.height),
                    floatVal.ToString("F2"), GetLabelStyle());

                // > button (small increment)
                if (GUI.Button(new Rect(sliderX + 112, rect.y, 24, rect.height), ">"))
                {
                    float newVal = Math.Min(setting.Max, floatVal + step);
                    setting.Value = newVal;
                    _dirty = true;
                    OnSettingChanged?.Invoke(modName, setting.Key, newVal);
                }

                // >> button (large increment)
                if (GUI.Button(new Rect(sliderX + 138, rect.y, 24, rect.height), ">>"))
                {
                    float newVal = Math.Min(setting.Max, floatVal + step * 5);
                    setting.Value = newVal;
                    _dirty = true;
                    OnSettingChanged?.Invoke(modName, setting.Key, newVal);
                }
                break;

            case SettingType.Number:
                GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), setting.Label, GetLabelStyle());
                int intVal = setting.Value is int i ? i : (int)(setting.DefaultValue ?? 0);

                // - button
                if (GUI.Button(new Rect(rect.x + labelWidth, rect.y, 24, rect.height), "-"))
                {
                    int newVal = Math.Max((int)setting.Min, intVal - 1);
                    if (newVal != intVal)
                    {
                        setting.Value = newVal;
                        _dirty = true;
                        OnSettingChanged?.Invoke(modName, setting.Key, newVal);
                    }
                }

                // Value display
                GUI.Label(new Rect(rect.x + labelWidth + 28, rect.y, 50, rect.height),
                    intVal.ToString(), GetLabelStyle());

                // + button
                if (GUI.Button(new Rect(rect.x + labelWidth + 82, rect.y, 24, rect.height), "+"))
                {
                    int newVal = Math.Min((int)setting.Max, intVal + 1);
                    if (newVal != intVal)
                    {
                        setting.Value = newVal;
                        _dirty = true;
                        OnSettingChanged?.Invoke(modName, setting.Key, newVal);
                    }
                }
                break;

            case SettingType.Dropdown:
                GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), setting.Label, GetLabelStyle());
                string strVal = setting.Value as string ?? setting.DefaultValue as string ?? "";
                int currentIdx = Array.IndexOf(setting.Options, strVal);
                if (currentIdx < 0) currentIdx = 0;

                // Simple prev/next buttons for dropdown
                if (GUI.Button(new Rect(rect.x + labelWidth, rect.y, 24, rect.height), "<"))
                {
                    int newIdx = (currentIdx - 1 + setting.Options.Length) % setting.Options.Length;
                    setting.Value = setting.Options[newIdx];
                    _dirty = true;
                    OnSettingChanged?.Invoke(modName, setting.Key, setting.Options[newIdx]);
                }

                float dropdownWidth = rect.width - labelWidth - 52;
                GUI.Label(new Rect(rect.x + labelWidth + 28, rect.y, dropdownWidth, rect.height),
                    strVal, GetLabelStyle());

                if (GUI.Button(new Rect(rect.x + rect.width - 24, rect.y, 24, rect.height), ">"))
                {
                    int newIdx = (currentIdx + 1) % setting.Options.Length;
                    setting.Value = setting.Options[newIdx];
                    _dirty = true;
                    OnSettingChanged?.Invoke(modName, setting.Key, setting.Options[newIdx]);
                }
                break;

            case SettingType.Text:
                GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), setting.Label, GetLabelStyle());
                string textVal = setting.Value as string ?? setting.DefaultValue as string ?? "";
                // Note: GUI.TextField may not work well in IL2CPP, but we'll try
                string newTextVal = GUI.TextField(
                    new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth, rect.height),
                    textVal);
                if (newTextVal != textVal)
                {
                    setting.Value = newTextVal;
                    _dirty = true;
                    OnSettingChanged?.Invoke(modName, setting.Key, newTextVal);
                }
                break;

            case SettingType.Info:
                GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), setting.Label, GetLabelStyle());
                string infoVal = setting.StatusCallback?.Invoke() ?? "";
                GUI.Label(new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth, rect.height),
                    infoVal, GetInfoStyle());
                break;

            case SettingType.Keybinding:
                GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), setting.Label, GetLabelStyle());
                string keyVal = setting.Value as string ?? setting.DefaultValue as string ?? "";
                int keyIdx = Array.IndexOf(setting.Options, keyVal);
                if (keyIdx < 0) keyIdx = 0;

                // Prev key button
                if (GUI.Button(new Rect(rect.x + labelWidth, rect.y, 24, rect.height), "<"))
                {
                    int newIdx = (keyIdx - 1 + setting.Options.Length) % setting.Options.Length;
                    setting.Value = setting.Options[newIdx];
                    _dirty = true;
                    OnSettingChanged?.Invoke(modName, setting.Key, setting.Options[newIdx]);
                }

                // Display key name (user-friendly)
                float keyDisplayWidth = rect.width - labelWidth - 52;
                string displayName = KeybindingHelper.GetDisplayName(keyVal);
                GUI.Label(new Rect(rect.x + labelWidth + 28, rect.y, keyDisplayWidth, rect.height),
                    displayName, GetLabelStyle());

                // Next key button
                if (GUI.Button(new Rect(rect.x + rect.width - 24, rect.y, 24, rect.height), ">"))
                {
                    int newIdx = (keyIdx + 1) % setting.Options.Length;
                    setting.Value = setting.Options[newIdx];
                    _dirty = true;
                    OnSettingChanged?.Invoke(modName, setting.Key, setting.Options[newIdx]);
                }
                break;
        }
    }

    // --- Styles (lazy-initialized, matching DevConsole) ---

    private static GUIStyle _labelStyle;
    private static GUIStyle _headerStyle;
    private static GUIStyle _subHeaderStyle;
    private static GUIStyle _helpStyle;
    private static GUIStyle _infoStyle;
    private static bool _stylesInitialized;

    private static GUIStyle GetLabelStyle()
    {
        InitStyles();
        return _labelStyle;
    }

    private static GUIStyle GetHeaderStyle()
    {
        InitStyles();
        return _headerStyle;
    }

    private static GUIStyle GetSubHeaderStyle()
    {
        InitStyles();
        return _subHeaderStyle;
    }

    private static GUIStyle GetHelpStyle()
    {
        InitStyles();
        return _helpStyle;
    }

    private static GUIStyle GetInfoStyle()
    {
        InitStyles();
        return _infoStyle;
    }

    private static void InitStyles()
    {
        if (_stylesInitialized)
        {
            try
            {
                if (_labelStyle?.normal?.background == null || !_labelStyle.normal.background)
                    _stylesInitialized = false;
            }
            catch { _stylesInitialized = false; }
        }

        if (_stylesInitialized) return;
        _stylesInitialized = true;

        _labelStyle = new GUIStyle(GUI.skin.label);
        _labelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        _labelStyle.fontSize = 13;

        var headerBg = new Texture2D(1, 1);
        headerBg.hideFlags = HideFlags.HideAndDontSave;
        headerBg.SetPixel(0, 0, new Color(0.2f, 0.2f, 0.25f, 1f));
        headerBg.Apply();

        _headerStyle = new GUIStyle(GUI.skin.button);
        _headerStyle.normal.background = headerBg;
        _headerStyle.normal.textColor = Color.white;
        _headerStyle.fontSize = 14;
        _headerStyle.fontStyle = FontStyle.Bold;
        _headerStyle.alignment = TextAnchor.MiddleLeft;
        _headerStyle.padding = new RectOffset(8, 8, 4, 4);

        _subHeaderStyle = new GUIStyle(_labelStyle);
        _subHeaderStyle.fontStyle = FontStyle.Bold;
        _subHeaderStyle.normal.textColor = new Color(0.7f, 0.8f, 1f);

        _helpStyle = new GUIStyle(_labelStyle);
        _helpStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
        _helpStyle.fontStyle = FontStyle.Italic;

        _infoStyle = new GUIStyle(_labelStyle);
        _infoStyle.normal.textColor = new Color(0.6f, 0.9f, 0.6f); // Light green for status values
    }
}

/// <summary>
/// Builder for defining mod settings.
/// </summary>
public class SettingsBuilder
{
    private readonly string _modName;
    private readonly List<SettingDefinition> _settings = new();

    internal SettingsBuilder(string modName)
    {
        _modName = modName;
    }

    /// <summary>
    /// Add a section header (visual separator).
    /// </summary>
    public SettingsBuilder AddHeader(string label)
    {
        _settings.Add(new SettingDefinition
        {
            Key = $"__header_{_settings.Count}",
            Label = label,
            Type = SettingType.Header
        });
        return this;
    }

    /// <summary>
    /// Add a boolean toggle.
    /// </summary>
    public SettingsBuilder AddToggle(string key, string label, bool defaultValue = false)
    {
        _settings.Add(new SettingDefinition
        {
            Key = key,
            Label = label,
            Type = SettingType.Toggle,
            DefaultValue = defaultValue,
            Value = defaultValue
        });
        return this;
    }

    /// <summary>
    /// Add a float slider.
    /// </summary>
    public SettingsBuilder AddSlider(string key, string label, float min, float max, float defaultValue)
    {
        _settings.Add(new SettingDefinition
        {
            Key = key,
            Label = label,
            Type = SettingType.Slider,
            Min = min,
            Max = max,
            DefaultValue = defaultValue,
            Value = defaultValue
        });
        return this;
    }

    /// <summary>
    /// Add an integer number with +/- buttons.
    /// </summary>
    public SettingsBuilder AddNumber(string key, string label, int min, int max, int defaultValue)
    {
        _settings.Add(new SettingDefinition
        {
            Key = key,
            Label = label,
            Type = SettingType.Number,
            Min = min,
            Max = max,
            DefaultValue = defaultValue,
            Value = defaultValue
        });
        return this;
    }

    /// <summary>
    /// Add a dropdown selection.
    /// </summary>
    public SettingsBuilder AddDropdown(string key, string label, string[] options, string defaultValue = null)
    {
        _settings.Add(new SettingDefinition
        {
            Key = key,
            Label = label,
            Type = SettingType.Dropdown,
            Options = options,
            DefaultValue = defaultValue ?? (options.Length > 0 ? options[0] : ""),
            Value = defaultValue ?? (options.Length > 0 ? options[0] : "")
        });
        return this;
    }

    /// <summary>
    /// Add a text input field.
    /// </summary>
    public SettingsBuilder AddText(string key, string label, string defaultValue = "")
    {
        _settings.Add(new SettingDefinition
        {
            Key = key,
            Label = label,
            Type = SettingType.Text,
            DefaultValue = defaultValue,
            Value = defaultValue
        });
        return this;
    }

    /// <summary>
    /// Add a read-only info display with dynamic content.
    /// The callback is called each frame to get the current value.
    /// </summary>
    public SettingsBuilder AddInfo(string key, string label, Func<string> valueCallback)
    {
        _settings.Add(new SettingDefinition
        {
            Key = key,
            Label = label,
            Type = SettingType.Info,
            StatusCallback = valueCallback
        });
        return this;
    }

    /// <summary>
    /// Add a read-only info display with static content.
    /// </summary>
    public SettingsBuilder AddInfo(string key, string label, string staticValue)
    {
        _settings.Add(new SettingDefinition
        {
            Key = key,
            Label = label,
            Type = SettingType.Info,
            StatusCallback = () => staticValue
        });
        return this;
    }

    /// <summary>
    /// Add a keybinding selector. Uses Unity KeyCode names.
    /// </summary>
    public SettingsBuilder AddKeybinding(string key, string label, string defaultKeyName)
    {
        _settings.Add(new SettingDefinition
        {
            Key = key,
            Label = label,
            Type = SettingType.Keybinding,
            Options = KeybindingHelper.CommonKeyNames,
            DefaultValue = defaultKeyName,
            Value = defaultKeyName
        });
        return this;
    }

    internal ModSettingsGroup Build()
    {
        return new ModSettingsGroup
        {
            ModName = _modName,
            Settings = _settings.ToList()
        };
    }
}

/// <summary>
/// Types of settings controls available.
/// </summary>
public enum SettingType
{
    Header,
    Toggle,
    Slider,
    Number,
    Dropdown,
    Text,
    Info,
    Keybinding
}

/// <summary>
/// Definition of a single setting (used by MenuInjector for native UI rendering).
/// </summary>
public class SettingDefinition
{
    public string Key { get; set; }
    public string Label { get; set; }
    public SettingType Type { get; set; }
    public object DefaultValue { get; set; }
    public object Value { get; set; }
    public float Min { get; set; }
    public float Max { get; set; }
    public string[] Options { get; set; }

    /// <summary>
    /// Callback for dynamic info display (SettingType.Info only).
    /// Called each frame to get the current display value.
    /// </summary>
    public Func<string> StatusCallback { get; set; }
}

internal class ModSettingsGroup
{
    public string ModName { get; set; }
    public List<SettingDefinition> Settings { get; set; } = new();
}

/// <summary>
/// Helper for keybinding settings - provides common key names and conversion utilities.
/// </summary>
public static class KeybindingHelper
{
    /// <summary>
    /// Common keys available for keybindings, using Unity KeyCode names.
    /// </summary>
    public static readonly string[] CommonKeyNames = new[]
    {
        // Function keys
        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        // Special keys
        "BackQuote", "Tab", "CapsLock", "LeftShift", "RightShift", "LeftControl", "RightControl",
        "LeftAlt", "RightAlt", "Space", "Return", "Backspace", "Delete", "Insert", "Home", "End",
        "PageUp", "PageDown", "Escape",
        // Arrow keys
        "UpArrow", "DownArrow", "LeftArrow", "RightArrow",
        // Number keys
        "Alpha0", "Alpha1", "Alpha2", "Alpha3", "Alpha4", "Alpha5", "Alpha6", "Alpha7", "Alpha8", "Alpha9",
        // Letter keys
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
        "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        // Numpad
        "Keypad0", "Keypad1", "Keypad2", "Keypad3", "Keypad4", "Keypad5",
        "Keypad6", "Keypad7", "Keypad8", "Keypad9", "KeypadPlus", "KeypadMinus",
        // Mouse (for reference, though GetKeyDown works differently)
        "Mouse0", "Mouse1", "Mouse2", "Mouse3", "Mouse4"
    };

    /// <summary>
    /// Convert a key name string to Unity KeyCode.
    /// Returns KeyCode.None if not found.
    /// </summary>
    public static KeyCode GetKeyCode(string keyName)
    {
        if (string.IsNullOrEmpty(keyName))
            return KeyCode.None;

        if (Enum.TryParse<KeyCode>(keyName, ignoreCase: true, out var keyCode))
            return keyCode;

        return KeyCode.None;
    }

    /// <summary>
    /// Get a user-friendly display name for a key.
    /// </summary>
    public static string GetDisplayName(string keyName)
    {
        if (string.IsNullOrEmpty(keyName))
            return "(None)";

        return keyName switch
        {
            "BackQuote" => "` (Backtick)",
            "Alpha0" => "0",
            "Alpha1" => "1",
            "Alpha2" => "2",
            "Alpha3" => "3",
            "Alpha4" => "4",
            "Alpha5" => "5",
            "Alpha6" => "6",
            "Alpha7" => "7",
            "Alpha8" => "8",
            "Alpha9" => "9",
            "Return" => "Enter",
            "LeftShift" => "L-Shift",
            "RightShift" => "R-Shift",
            "LeftControl" => "L-Ctrl",
            "RightControl" => "R-Ctrl",
            "LeftAlt" => "L-Alt",
            "RightAlt" => "R-Alt",
            "KeypadPlus" => "Num +",
            "KeypadMinus" => "Num -",
            _ => keyName
        };
    }
}
