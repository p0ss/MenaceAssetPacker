#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Menace.SDK;

/// <summary>
/// Injects a "Mods" menu item into the game's main menu using UIToolkit.
/// The settings panel uses IMGUI for IL2CPP compatibility.
/// </summary>
public static class MenuInjector
{
    private static bool _initialized;
    private static bool _injected;
    private static Button _modsButton;

    // Configuration
    public static string MenuButtonText { get; set; } = "Mods";

    // IMGUI settings panel state
    private static bool _showSettingsPanel;
    private static Vector2 _settingsScroll;
    private static Rect _panelRect;

    /// <summary>
    /// Initialize the menu injector. Called automatically by ModpackLoader.
    /// </summary>
    internal static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        SdkLogger.Msg("[MenuInjector] Initialized for UIToolkit");
    }

    /// <summary>
    /// Called on scene load to attempt menu injection.
    /// </summary>
    internal static void OnSceneLoaded(string sceneName)
    {
        // Reset injection state on scene change
        _injected = false;
        _modsButton = null;
        _showSettingsPanel = false;

        SdkLogger.Msg($"[MenuInjector] Scene loaded: '{sceneName}'");

        if (!ModSettings.HasAnySettings())
        {
            SdkLogger.Msg("[MenuInjector] No mod settings registered, skipping injection");
            return;
        }

        // Check if this looks like a main menu scene
        if (!IsMainMenuScene(sceneName))
        {
            SdkLogger.Msg($"[MenuInjector] Scene '{sceneName}' does not look like main menu");
            return;
        }

        // Delay injection to let the UI fully initialize
        GameState.RunDelayed(30, () => TryInjectMenuButton());
    }

    private static bool IsMainMenuScene(string sceneName)
    {
        var lower = sceneName.ToLowerInvariant();
        return lower.Contains("menu") ||
               lower.Contains("title") ||
               lower.Contains("main") ||
               lower.Contains("start") ||
               lower.Contains("frontend") ||
               lower == "init" ||
               lower == "boot";
    }

    private static void TryInjectMenuButton()
    {
        if (_injected) return;

        try
        {
            SdkLogger.Msg("[MenuInjector] Attempting UIToolkit menu injection...");

            // Find active UIDocuments
            var docs = UnityEngine.Object.FindObjectsOfType<UIDocument>();
            if (docs == null || docs.Length == 0)
            {
                SdkLogger.Msg("[MenuInjector] No UIDocument found");
                return;
            }

            foreach (var doc in docs)
            {
                if (doc == null || !doc.gameObject.activeInHierarchy) continue;
                var root = doc.rootVisualElement;
                if (root == null) continue;

                // Strategy 1: Find a button container (element with multiple Button children)
                var buttonContainer = FindButtonContainer(root);
                if (buttonContainer != null)
                {
                    InjectIntoContainer(buttonContainer);
                    return;
                }

                // Strategy 2: Find a specific button to inject near
                var referenceButton = FindReferenceButton(root);
                if (referenceButton != null)
                {
                    InjectNearButton(referenceButton);
                    return;
                }
            }

            SdkLogger.Msg("[MenuInjector] Could not find suitable injection point");
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[MenuInjector] Injection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Find a container element that holds menu buttons.
    /// </summary>
    private static VisualElement FindButtonContainer(VisualElement root)
    {
        var buttons = QueryAll<Button>(root);
        if (buttons.Count < 2) return null;

        // Group buttons by parent
        var parentGroups = new Dictionary<VisualElement, List<Button>>();
        foreach (var btn in buttons)
        {
            var parent = btn.parent;
            if (parent == null) continue;

            if (!parentGroups.ContainsKey(parent))
                parentGroups[parent] = new List<Button>();
            parentGroups[parent].Add(btn);
        }

        // Find a parent with multiple menu-like buttons
        foreach (var kvp in parentGroups)
        {
            var parent = kvp.Key;
            var buttonList = kvp.Value;
            if (buttonList.Count < 2) continue;

            int menuLikeCount = 0;
            foreach (var btn in buttonList)
            {
                if (IsMenuButton(btn))
                    menuLikeCount++;
            }

            if (menuLikeCount >= 2)
            {
                SdkLogger.Msg($"[MenuInjector] Found button container with {buttonList.Count} buttons");
                return parent;
            }
        }

        return null;
    }

    /// <summary>
    /// Find a button that looks like a menu item we can inject near.
    /// </summary>
    private static Button FindReferenceButton(VisualElement root)
    {
        var buttons = QueryAll<Button>(root);

        // Priority order of button names/text to look for
        string[] priorityNames = {
            "settings", "options", "config",
            "newgame", "new game", "start",
            "continue", "load",
            "quit", "exit"
        };

        foreach (var targetName in priorityNames)
        {
            foreach (var btn in buttons)
            {
                var btnName = btn.name?.ToLowerInvariant() ?? "";
                var btnText = GetButtonText(btn)?.ToLowerInvariant() ?? "";

                if (btnName.Contains(targetName) || btnText.Contains(targetName))
                {
                    SdkLogger.Msg($"[MenuInjector] Found reference button: {btn.name} ('{GetButtonText(btn)}')");
                    return btn;
                }
            }
        }

        return null;
    }

    private static bool IsMenuButton(Button btn)
    {
        var text = GetButtonText(btn)?.ToLowerInvariant() ?? "";
        var name = btn.name?.ToLowerInvariant() ?? "";

        string[] menuKeywords = {
            "new", "continue", "load", "save", "settings", "options",
            "tutorial", "quit", "exit", "credits", "extras", "play",
            "start", "campaign", "multiplayer", "skirmish"
        };

        return menuKeywords.Any(k => text.Contains(k) || name.Contains(k));
    }

    private static string GetButtonText(Button btn)
    {
        // Try direct text property
        if (!string.IsNullOrWhiteSpace(btn.text))
            return btn.text;

        // Try to find a Label child
        var label = UQueryExtensions.Q<Label>(btn, null, (string)null);
        if (label != null && !string.IsNullOrWhiteSpace(label.text))
            return label.text;

        return null;
    }

    /// <summary>
    /// Inject the Mods button into a container.
    /// </summary>
    private static void InjectIntoContainer(VisualElement container)
    {
        _modsButton = CreateModsButton();

        // Find a good position (before Settings/Options/Quit if possible)
        int insertIndex = container.childCount;
        for (int i = 0; i < container.childCount; i++)
        {
            var child = container[i];
            if (child is Button btn)
            {
                var text = GetButtonText(btn)?.ToLowerInvariant() ?? "";
                var name = btn.name?.ToLowerInvariant() ?? "";

                if (text.Contains("settings") || text.Contains("options") ||
                    text.Contains("quit") || text.Contains("exit") ||
                    name.Contains("settings") || name.Contains("options") ||
                    name.Contains("quit") || name.Contains("exit"))
                {
                    insertIndex = i;
                    break;
                }
            }
        }

        container.Insert(insertIndex, _modsButton);
        _injected = true;
        SdkLogger.Msg($"[MenuInjector] Successfully injected Mods button at index {insertIndex}");
    }

    /// <summary>
    /// Inject near a reference button.
    /// </summary>
    private static void InjectNearButton(Button referenceButton)
    {
        var parent = referenceButton.parent;
        if (parent == null)
        {
            SdkLogger.Warning("[MenuInjector] Reference button has no parent");
            return;
        }

        _modsButton = CreateModsButton();

        // Copy styles from reference button
        CopyButtonStyles(referenceButton, _modsButton);

        // Insert after the reference button
        int refIndex = parent.IndexOf(referenceButton);
        parent.Insert(refIndex + 1, _modsButton);

        _injected = true;
        SdkLogger.Msg($"[MenuInjector] Successfully injected Mods button after '{referenceButton.name}'");
    }

    private static Button CreateModsButton()
    {
        var btn = new Button();
        btn.name = "ModsButton";
        btn.text = MenuButtonText;

        // Hook up click via the Clickable manipulator
        btn.clickable.clicked += (Action)OnModsButtonClick;

        // Add some basic styling
        btn.style.marginTop = 4;
        btn.style.marginBottom = 4;

        return btn;
    }

    private static void CopyButtonStyles(Button source, Button target)
    {
        try
        {
            var srcStyle = source.resolvedStyle;
            target.style.width = srcStyle.width;
            target.style.height = srcStyle.height;
            target.style.fontSize = srcStyle.fontSize;
            target.style.paddingLeft = srcStyle.paddingLeft;
            target.style.paddingRight = srcStyle.paddingRight;
            target.style.paddingTop = srcStyle.paddingTop;
            target.style.paddingBottom = srcStyle.paddingBottom;
            target.style.marginLeft = srcStyle.marginLeft;
            target.style.marginRight = srcStyle.marginRight;
            target.style.marginTop = srcStyle.marginTop;
            target.style.marginBottom = srcStyle.marginBottom;

            // Note: Copying USS classes from source would require IL2CPP-compatible iteration
            // which is complex. The button will inherit styles from its parent context.
        }
        catch (Exception ex)
        {
            SdkLogger.Msg($"[MenuInjector] Could not copy styles: {ex.Message}");
        }
    }

    private static void OnModsButtonClick()
    {
        SdkLogger.Msg("[MenuInjector] Mods button clicked");
        _showSettingsPanel = true;
    }

    /// <summary>
    /// Toggle settings panel visibility (for keyboard shortcut access).
    /// </summary>
    public static void ToggleSettingsPanel()
    {
        _showSettingsPanel = !_showSettingsPanel;
    }

    // ==================== Helpers ====================

    private static List<T> QueryAll<T>(VisualElement root) where T : VisualElement
    {
        try
        {
            var il2cppList = UQueryExtensions.Query<T>(root, null, (string)null).ToList();
            var result = new List<T>();
            for (int i = 0; i < il2cppList.Count; i++)
            {
                result.Add(il2cppList[i]);
            }
            return result;
        }
        catch
        {
            return new List<T>();
        }
    }

    // ==================== IMGUI Settings Panel ====================

    /// <summary>
    /// Draw the IMGUI settings panel. Called from ModpackLoaderMod.OnGUI().
    /// </summary>
    internal static void Draw()
    {
        if (!_showSettingsPanel) return;

        InitStyles();

        // Panel dimensions
        float panelWidth = Mathf.Min(Screen.width * 0.6f, 700f);
        float panelHeight = Mathf.Min(Screen.height * 0.8f, 800f);
        _panelRect = new Rect(
            (Screen.width - panelWidth) / 2,
            (Screen.height - panelHeight) / 2,
            panelWidth,
            panelHeight
        );

        // Background
        GUI.Box(_panelRect, "", _boxStyle);

        float cx = _panelRect.x + 20;
        float cy = _panelRect.y + 20;
        float cw = _panelRect.width - 40;

        // Title
        GUI.Label(new Rect(cx, cy, cw - 30, 30), "Mod Settings", _titleStyle);

        // Close button
        if (GUI.Button(new Rect(_panelRect.xMax - 40, cy, 24, 24), "X"))
        {
            _showSettingsPanel = false;
        }
        cy += 40;

        // Content area
        float contentHeight = CalculateContentHeight();
        var viewRect = new Rect(cx, cy, cw, _panelRect.yMax - cy - 60);
        var contentRect = new Rect(0, 0, cw - 20, contentHeight);

        _settingsScroll = GUI.BeginScrollView(viewRect, _settingsScroll, contentRect);
        DrawSettingsContent(cw - 20);
        GUI.EndScrollView();

        // Back button
        float btnWidth = 120;
        float btnHeight = 36;
        if (GUI.Button(new Rect(
            _panelRect.x + (_panelRect.width - btnWidth) / 2,
            _panelRect.yMax - btnHeight - 15,
            btnWidth, btnHeight), "Back", _buttonStyle))
        {
            _showSettingsPanel = false;
        }
    }

    private static float CalculateContentHeight()
    {
        float height = 0;
        var mods = ModSettings.GetRegisteredMods().ToList();

        foreach (var modName in mods)
        {
            height += 35; // Mod header
            var settings = ModSettings.GetSettingsForMod(modName);
            foreach (var setting in settings)
            {
                height += setting.Type == SettingType.Header ? 28 : 32;
            }
            height += 20; // Spacing
        }

        return height + 20;
    }

    private static void DrawSettingsContent(float width)
    {
        float y = 0;
        var mods = ModSettings.GetRegisteredMods().ToList();

        foreach (var modName in mods)
        {
            // Mod header
            GUI.Label(new Rect(0, y, width, 30), modName, _modHeaderStyle);
            y += 35;

            var settings = ModSettings.GetSettingsForMod(modName);
            foreach (var setting in settings)
            {
                var rect = new Rect(10, y, width - 20, 28);
                DrawSetting(modName, setting, rect);
                y += setting.Type == SettingType.Header ? 28 : 32;
            }

            y += 20; // Spacing between mods
        }
    }

    private static void DrawSetting(string modName, SettingDefinition setting, Rect rect)
    {
        float labelWidth = rect.width * 0.45f;
        float controlWidth = rect.width - labelWidth - 10;

        switch (setting.Type)
        {
            case SettingType.Header:
                GUI.Label(new Rect(rect.x, rect.y + 5, rect.width, rect.height),
                    "— " + setting.Label + " —", _subHeaderStyle);
                break;

            case SettingType.Toggle:
                GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), setting.Label, _labelStyle);
                bool boolVal = setting.Value is bool b ? b : (bool)(setting.DefaultValue ?? false);
                bool newBoolVal = GUI.Toggle(new Rect(rect.x + labelWidth, rect.y + 4, 24, 24), boolVal, "", _toggleStyle);
                if (newBoolVal != boolVal)
                {
                    ModSettings.Set(modName, setting.Key, newBoolVal);
                }
                break;

            case SettingType.Slider:
                GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), setting.Label, _labelStyle);
                float floatVal = setting.Value is float f ? f : Convert.ToSingle(setting.DefaultValue ?? 0f);

                // Use button-based slider to avoid GUI.HorizontalSlider unstripping issues
                float sliderX = rect.x + labelWidth;
                float step = (setting.Max - setting.Min) / 20f; // 20 steps

                // << button (large decrement)
                if (GUI.Button(new Rect(sliderX, rect.y + 2, 28, 24), "<<", _smallButtonStyle))
                {
                    ModSettings.Set(modName, setting.Key, Math.Max(setting.Min, floatVal - step * 5));
                }

                // < button (small decrement)
                if (GUI.Button(new Rect(sliderX + 30, rect.y + 2, 28, 24), "<", _smallButtonStyle))
                {
                    ModSettings.Set(modName, setting.Key, Math.Max(setting.Min, floatVal - step));
                }

                // Value display
                GUI.Label(new Rect(sliderX + 62, rect.y, 55, rect.height),
                    floatVal.ToString("F2"), _valueStyle);

                // > button (small increment)
                if (GUI.Button(new Rect(sliderX + 120, rect.y + 2, 28, 24), ">", _smallButtonStyle))
                {
                    ModSettings.Set(modName, setting.Key, Math.Min(setting.Max, floatVal + step));
                }

                // >> button (large increment)
                if (GUI.Button(new Rect(sliderX + 150, rect.y + 2, 28, 24), ">>", _smallButtonStyle))
                {
                    ModSettings.Set(modName, setting.Key, Math.Min(setting.Max, floatVal + step * 5));
                }
                break;

            case SettingType.Number:
                GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), setting.Label, _labelStyle);
                int intVal = setting.Value is int i ? i : Convert.ToInt32(setting.DefaultValue ?? 0);
                if (GUI.Button(new Rect(rect.x + labelWidth, rect.y + 2, 28, 24), "-", _smallButtonStyle))
                {
                    ModSettings.Set(modName, setting.Key, Math.Max((int)setting.Min, intVal - 1));
                }
                GUI.Label(new Rect(rect.x + labelWidth + 32, rect.y, 50, rect.height),
                    intVal.ToString(), _valueStyle);
                if (GUI.Button(new Rect(rect.x + labelWidth + 86, rect.y + 2, 28, 24), "+", _smallButtonStyle))
                {
                    ModSettings.Set(modName, setting.Key, Math.Min((int)setting.Max, intVal + 1));
                }
                break;

            case SettingType.Dropdown:
                GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), setting.Label, _labelStyle);
                string strVal = setting.Value as string ?? setting.DefaultValue as string ?? "";
                int currentIdx = Array.IndexOf(setting.Options ?? new string[0], strVal);
                if (currentIdx < 0) currentIdx = 0;

                if (GUI.Button(new Rect(rect.x + labelWidth, rect.y + 2, 28, 24), "<", _smallButtonStyle))
                {
                    int newIdx = (currentIdx - 1 + setting.Options.Length) % setting.Options.Length;
                    ModSettings.Set(modName, setting.Key, setting.Options[newIdx]);
                }
                GUI.Label(new Rect(rect.x + labelWidth + 32, rect.y, controlWidth - 68, rect.height),
                    strVal, _valueStyle);
                if (GUI.Button(new Rect(rect.x + rect.width - 32, rect.y + 2, 28, 24), ">", _smallButtonStyle))
                {
                    int newIdx = (currentIdx + 1) % setting.Options.Length;
                    ModSettings.Set(modName, setting.Key, setting.Options[newIdx]);
                }
                break;

            case SettingType.Text:
                GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), setting.Label, _labelStyle);
                string textVal = setting.Value as string ?? setting.DefaultValue as string ?? "";
                string newTextVal = GUI.TextField(
                    new Rect(rect.x + labelWidth, rect.y + 2, controlWidth, 24),
                    textVal, _textFieldStyle);
                if (newTextVal != textVal)
                {
                    ModSettings.Set(modName, setting.Key, newTextVal);
                }
                break;
        }
    }

    // --- Styles ---

    private static bool _stylesInitialized;
    private static GUIStyle _boxStyle;
    private static GUIStyle _titleStyle;
    private static GUIStyle _modHeaderStyle;
    private static GUIStyle _subHeaderStyle;
    private static GUIStyle _labelStyle;
    private static GUIStyle _valueStyle;
    private static GUIStyle _buttonStyle;
    private static GUIStyle _smallButtonStyle;
    private static GUIStyle _toggleStyle;
    private static GUIStyle _textFieldStyle;

    private static void InitStyles()
    {
        if (_stylesInitialized)
        {
            try
            {
                if (_boxStyle?.normal?.background == null)
                    _stylesInitialized = false;
            }
            catch { _stylesInitialized = false; }
        }

        if (_stylesInitialized) return;
        _stylesInitialized = true;

        // Background
        var bgTex = new Texture2D(1, 1);
        bgTex.hideFlags = HideFlags.HideAndDontSave;
        bgTex.SetPixel(0, 0, new Color(0.12f, 0.12f, 0.15f, 0.98f));
        bgTex.Apply();

        _boxStyle = new GUIStyle(GUI.skin.box);
        _boxStyle.normal.background = bgTex;

        _titleStyle = new GUIStyle(GUI.skin.label);
        _titleStyle.fontSize = 24;
        _titleStyle.fontStyle = FontStyle.Bold;
        _titleStyle.normal.textColor = Color.white;
        _titleStyle.alignment = TextAnchor.MiddleLeft;

        var headerBg = new Texture2D(1, 1);
        headerBg.hideFlags = HideFlags.HideAndDontSave;
        headerBg.SetPixel(0, 0, new Color(0.2f, 0.25f, 0.3f, 1f));
        headerBg.Apply();

        _modHeaderStyle = new GUIStyle(GUI.skin.label);
        _modHeaderStyle.fontSize = 18;
        _modHeaderStyle.fontStyle = FontStyle.Bold;
        _modHeaderStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);
        _modHeaderStyle.normal.background = headerBg;
        _modHeaderStyle.padding = new RectOffset(10, 10, 5, 5);

        _subHeaderStyle = new GUIStyle(GUI.skin.label);
        _subHeaderStyle.fontSize = 13;
        _subHeaderStyle.fontStyle = FontStyle.Italic;
        _subHeaderStyle.normal.textColor = new Color(0.6f, 0.65f, 0.7f);
        _subHeaderStyle.alignment = TextAnchor.MiddleCenter;

        _labelStyle = new GUIStyle(GUI.skin.label);
        _labelStyle.fontSize = 14;
        _labelStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        _labelStyle.alignment = TextAnchor.MiddleLeft;

        _valueStyle = new GUIStyle(GUI.skin.label);
        _valueStyle.fontSize = 14;
        _valueStyle.normal.textColor = new Color(0.8f, 0.85f, 0.9f);
        _valueStyle.alignment = TextAnchor.MiddleCenter;

        var btnBg = new Texture2D(1, 1);
        btnBg.hideFlags = HideFlags.HideAndDontSave;
        btnBg.SetPixel(0, 0, new Color(0.25f, 0.28f, 0.32f, 1f));
        btnBg.Apply();

        _buttonStyle = new GUIStyle(GUI.skin.button);
        _buttonStyle.fontSize = 16;
        _buttonStyle.normal.background = btnBg;
        _buttonStyle.normal.textColor = Color.white;
        _buttonStyle.hover.textColor = Color.white;

        _smallButtonStyle = new GUIStyle(GUI.skin.button);
        _smallButtonStyle.fontSize = 14;
        _smallButtonStyle.fontStyle = FontStyle.Bold;
        _smallButtonStyle.normal.background = btnBg;
        _smallButtonStyle.normal.textColor = Color.white;

        _toggleStyle = new GUIStyle(GUI.skin.toggle);

        var fieldBg = new Texture2D(1, 1);
        fieldBg.hideFlags = HideFlags.HideAndDontSave;
        fieldBg.SetPixel(0, 0, new Color(0.18f, 0.2f, 0.22f, 1f));
        fieldBg.Apply();

        _textFieldStyle = new GUIStyle(GUI.skin.textField);
        _textFieldStyle.normal.background = fieldBg;
        _textFieldStyle.normal.textColor = Color.white;
        _textFieldStyle.fontSize = 14;
    }
}
