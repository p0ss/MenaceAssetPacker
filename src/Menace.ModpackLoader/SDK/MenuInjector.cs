using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Events;

namespace Menace.SDK;

/// <summary>
/// Injects a "Mods" menu item into the game's main menu.
/// Uses reflection to work with the game's UI system without compile-time dependencies.
/// </summary>
public static class MenuInjector
{
    private static bool _injected;
    private static bool _initialized;
    private static GameObject _modsButton;

    // Cached reflection types for Unity UI (resolved at runtime)
    private static Type _buttonType;
    private static Type _textType;
    private static Type _tmpTextType;
    private static Type _verticalLayoutGroupType;
    private static Type _scrollRectType;
    private static Type _toggleType;
    private static Type _sliderType;
    private static Type _dropdownType;
    private static Type _inputFieldType;
    private static Type _imageType;

    // IMGUI settings panel state
    private static bool _showSettingsPanel;
    private static Vector2 _settingsScroll;
    private static Rect _panelRect;

    // Configuration - can be set before Initialize() if needed
    public static string MenuSceneName { get; set; } = ""; // Auto-detect if empty
    public static string MenuButtonText { get; set; } = "Mods";

    /// <summary>
    /// Initialize the menu injector. Called automatically by ModpackLoader.
    /// </summary>
    internal static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        // Resolve Unity UI types via reflection (they live in UnityEngine.UI assembly)
        ResolveUITypes();
    }

    private static void ResolveUITypes()
    {
        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            // Log assemblies that might contain UI types for debugging
            SdkLogger.Msg("[MenuInjector] Searching for UI assemblies...");
            foreach (var asm in assemblies)
            {
                var asmName = asm.GetName().Name;

                // Log any assembly that might contain UI
                if (asmName.Contains("UI") || asmName.Contains("TMPro") || asmName.Contains("TextMesh"))
                {
                    SdkLogger.Msg($"[MenuInjector]   Found assembly: {asmName}");
                }

                if (asmName.Contains("UnityEngine.UI") || asmName.Contains("Unity.UI"))
                {
                    _buttonType ??= asm.GetType("UnityEngine.UI.Button");
                    _textType ??= asm.GetType("UnityEngine.UI.Text");
                    _verticalLayoutGroupType ??= asm.GetType("UnityEngine.UI.VerticalLayoutGroup");
                    _scrollRectType ??= asm.GetType("UnityEngine.UI.ScrollRect");
                    _toggleType ??= asm.GetType("UnityEngine.UI.Toggle");
                    _sliderType ??= asm.GetType("UnityEngine.UI.Slider");
                    _dropdownType ??= asm.GetType("UnityEngine.UI.Dropdown");
                    _inputFieldType ??= asm.GetType("UnityEngine.UI.InputField");
                    _imageType ??= asm.GetType("UnityEngine.UI.Image");
                }

                // Also check for TMPro
                if (asmName.Contains("TextMeshPro") || asmName.Contains("TMPro"))
                {
                    _tmpTextType ??= asm.GetType("TMPro.TMP_Text") ?? asm.GetType("TMPro.TextMeshProUGUI");
                }
            }

            SdkLogger.Msg($"[MenuInjector] UI types resolved - Button:{_buttonType != null}, Text:{_textType != null}, TMP:{_tmpTextType != null}, VLG:{_verticalLayoutGroupType != null}");
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[MenuInjector] Failed to resolve UI types: {ex.Message}");
        }
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
        SdkLogger.Msg($"[MenuInjector] HasAnySettings: {ModSettings.HasAnySettings()}");
        SdkLogger.Msg($"[MenuInjector] Registered mods: {string.Join(", ", ModSettings.GetRegisteredMods())}");

        // Check if this looks like a main menu scene
        if (!IsMainMenuScene(sceneName))
        {
            SdkLogger.Msg($"[MenuInjector] Scene '{sceneName}' does not look like main menu, skipping");
            return;
        }

        SdkLogger.Msg($"[MenuInjector] Scene '{sceneName}' looks like main menu, will attempt injection...");

        // Delay injection to let the menu fully initialize
        GameState.RunDelayed(30, () => TryInjectMenuButton());
    }

    private static bool IsMainMenuScene(string sceneName)
    {
        // If explicitly configured, use that
        if (!string.IsNullOrEmpty(MenuSceneName))
            return sceneName.Equals(MenuSceneName, StringComparison.OrdinalIgnoreCase);

        // Auto-detect common main menu scene names
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
        if (!ModSettings.HasAnySettings()) return;
        if (_buttonType == null)
        {
            SdkLogger.Msg("[MenuInjector] Button type not resolved, cannot inject");
            return;
        }

        try
        {
            SdkLogger.Msg("[MenuInjector] Attempting to inject Mods menu button...");

            // Strategy 1: Find a vertical layout group with Button children (common menu pattern)
            var menuContainer = FindMenuContainer();
            if (menuContainer != null)
            {
                InjectIntoContainer(menuContainer);
                return;
            }

            // Strategy 2: Find buttons by common names and inject near them
            var referenceButton = FindReferenceButton();
            if (referenceButton != null)
            {
                InjectNearButton(referenceButton);
                return;
            }

            SdkLogger.Msg("[MenuInjector] Could not find menu structure to inject into");
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[MenuInjector] Injection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Find a container that holds menu buttons.
    /// </summary>
    private static GameObject FindMenuContainer()
    {
        if (_verticalLayoutGroupType == null) return null;

        var il2cppType = Il2CppType.From(_verticalLayoutGroupType);
        var layoutGroups = UnityEngine.Object.FindObjectsOfType(il2cppType);

        foreach (var layout in layoutGroups)
        {
            if (layout == null) continue;

            var go = GetGameObject(layout);
            if (go == null) continue;

            // Check if this layout has multiple Button children
            var buttons = FindComponentsInChildren(go, _buttonType);
            if (buttons.Count < 2) continue;

            // Check if buttons have text that looks like menu items
            int menuLikeCount = 0;
            foreach (var btn in buttons)
            {
                var text = GetButtonText(btn);
                if (IsMenuItemText(text))
                    menuLikeCount++;
            }

            if (menuLikeCount >= 2)
            {
                SdkLogger.Msg($"[MenuInjector] Found menu container: {go.name} with {buttons.Count} buttons");
                return go;
            }
        }

        return null;
    }

    /// <summary>
    /// Find a button that looks like a main menu item we can clone/inject near.
    /// </summary>
    private static Component FindReferenceButton()
    {
        if (_buttonType == null) return null;

        var il2cppType = Il2CppType.From(_buttonType);
        var allButtons = UnityEngine.Object.FindObjectsOfType(il2cppType);

        // Priority order of button names to look for
        string[] priorityNames = {
            "settings", "options", "config",
            "newgame", "new game", "start",
            "continue", "load",
            "tutorial", "help",
            "quit", "exit"
        };

        foreach (var targetName in priorityNames)
        {
            foreach (var btn in allButtons)
            {
                if (btn == null) continue;

                var go = GetGameObject(btn);
                if (go == null) continue;

                var text = GetButtonText(btn)?.ToLowerInvariant() ?? "";
                var objName = go.name.ToLowerInvariant();

                if (text.Contains(targetName) || objName.Contains(targetName.Replace(" ", "")))
                {
                    SdkLogger.Msg($"[MenuInjector] Found reference button: {go.name} ('{text}')");
                    return btn as Component;
                }
            }
        }

        return null;
    }

    private static string GetButtonText(object btn)
    {
        if (btn == null) return null;

        var go = GetGameObject(btn);
        if (go == null) return null;

        // Try to find Text component in children
        if (_textType != null)
        {
            var textComponent = FindComponentInChildren(go, _textType);
            if (textComponent != null)
            {
                var textProp = _textType.GetProperty("text");
                if (textProp != null)
                    return textProp.GetValue(textComponent) as string;
            }
        }

        // Try TMPro if available
        if (_tmpTextType != null)
        {
            var tmpComponent = FindComponentInChildren(go, _tmpTextType);
            if (tmpComponent != null)
            {
                var textProp = _tmpTextType.GetProperty("text");
                if (textProp != null)
                    return textProp.GetValue(tmpComponent) as string;
            }
        }

        return null;
    }

    private static bool IsMenuItemText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        var lower = text.ToLowerInvariant().Trim();
        string[] menuKeywords = {
            "new", "continue", "load", "save", "settings", "options",
            "tutorial", "quit", "exit", "credits", "extras", "play",
            "start", "campaign", "multiplayer", "skirmish"
        };

        return menuKeywords.Any(k => lower.Contains(k));
    }

    /// <summary>
    /// Inject a Mods button into a menu container.
    /// </summary>
    private static void InjectIntoContainer(GameObject container)
    {
        // Find an existing button to clone
        var buttons = FindComponentsInChildren(container, _buttonType);
        if (buttons.Count == 0)
        {
            SdkLogger.Warning("[MenuInjector] No button found in container to clone");
            return;
        }

        var existingButton = buttons[0];
        var existingGo = GetGameObject(existingButton);

        // Clone the button
        _modsButton = UnityEngine.Object.Instantiate(existingGo, container.transform);
        _modsButton.name = "ModsButton";

        // Set button text
        SetButtonText(_modsButton, MenuButtonText);

        // Position it (try to put it before Settings or Quit, or at the end)
        PositionModsButton(container);

        // Hook up click handler
        HookButtonClick(_modsButton, OnModsButtonClick);

        _modsButton.SetActive(true);
        _injected = true;

        SdkLogger.Msg("[MenuInjector] Successfully injected Mods button into menu");
    }

    /// <summary>
    /// Inject near a reference button (when no container is found).
    /// </summary>
    private static void InjectNearButton(Component referenceButton)
    {
        var refGo = GetGameObject(referenceButton);
        var parent = refGo.transform.parent;
        if (parent == null)
        {
            SdkLogger.Warning("[MenuInjector] Reference button has no parent");
            return;
        }

        // Clone the reference button
        _modsButton = UnityEngine.Object.Instantiate(refGo, parent);
        _modsButton.name = "ModsButton";

        // Set button text
        SetButtonText(_modsButton, MenuButtonText);

        // Position it after the reference button
        int siblingIndex = refGo.transform.GetSiblingIndex();
        _modsButton.transform.SetSiblingIndex(siblingIndex + 1);

        // Hook up click handler
        HookButtonClick(_modsButton, OnModsButtonClick);

        _modsButton.SetActive(true);
        _injected = true;

        SdkLogger.Msg("[MenuInjector] Successfully injected Mods button near reference button");
    }

    private static void SetButtonText(GameObject buttonObj, string text)
    {
        // Try Unity UI Text
        if (_textType != null)
        {
            var textComponent = FindComponentInChildren(buttonObj, _textType);
            if (textComponent != null)
            {
                var textProp = _textType.GetProperty("text");
                textProp?.SetValue(textComponent, text);
                return;
            }
        }

        // Try TMPro
        if (_tmpTextType != null)
        {
            var tmpComponent = FindComponentInChildren(buttonObj, _tmpTextType);
            if (tmpComponent != null)
            {
                var textProp = _tmpTextType.GetProperty("text");
                textProp?.SetValue(tmpComponent, text);
            }
        }
    }

    private static void HookButtonClick(GameObject buttonObj, Action callback)
    {
        if (_buttonType == null) return;

        var btn = buttonObj.GetComponent(Il2CppType.From(_buttonType));
        if (btn == null) return;

        // Get onClick property
        var onClickProp = _buttonType.GetProperty("onClick");
        if (onClickProp == null) return;

        var onClick = onClickProp.GetValue(btn);
        if (onClick == null) return;

        // Remove existing listeners
        var removeMethod = onClick.GetType().GetMethod("RemoveAllListeners");
        removeMethod?.Invoke(onClick, null);

        // Add new listener
        var addMethod = onClick.GetType().GetMethod("AddListener");
        if (addMethod != null)
        {
            // Create UnityAction delegate
            var action = (UnityAction)(() => callback());
            addMethod.Invoke(onClick, new object[] { action });
        }
    }

    private static void PositionModsButton(GameObject container)
    {
        if (_modsButton == null) return;

        // Try to position before Settings, Options, or Quit
        string[] insertBeforeNames = { "settings", "options", "config", "quit", "exit" };

        for (int i = 0; i < container.transform.childCount; i++)
        {
            var child = container.transform.GetChild(i);
            var childName = child.name.ToLowerInvariant();

            // Also check button text
            var btn = _buttonType != null ? child.GetComponent(Il2CppType.From(_buttonType)) : null;
            var btnText = btn != null ? GetButtonText(btn)?.ToLowerInvariant() ?? "" : "";

            foreach (var target in insertBeforeNames)
            {
                if (childName.Contains(target) || btnText.Contains(target))
                {
                    _modsButton.transform.SetSiblingIndex(i);
                    return;
                }
            }
        }

        // Default: put at end but before last item (usually Quit)
        int lastIndex = container.transform.childCount - 1;
        if (lastIndex > 0)
            _modsButton.transform.SetSiblingIndex(lastIndex);
    }

    private static void OnModsButtonClick()
    {
        SdkLogger.Msg("[MenuInjector] Mods button clicked");
        _showSettingsPanel = true;
    }

    // --- Reflection helpers ---

    private static GameObject GetGameObject(object component)
    {
        if (component == null) return null;

        if (component is GameObject go) return go;
        if (component is Component c) return c.gameObject;

        // Try reflection for IL2CPP types
        var goProp = component.GetType().GetProperty("gameObject");
        return goProp?.GetValue(component) as GameObject;
    }

    private static Component FindComponentInChildren(GameObject go, Type componentType)
    {
        if (go == null || componentType == null) return null;

        try
        {
            var il2cppType = Il2CppType.From(componentType);
            return go.GetComponentInChildren(il2cppType);
        }
        catch
        {
            return null;
        }
    }

    private static List<Component> FindComponentsInChildren(GameObject go, Type componentType)
    {
        var result = new List<Component>();
        if (go == null || componentType == null) return result;

        try
        {
            var il2cppType = Il2CppType.From(componentType);
            var components = go.GetComponentsInChildren(il2cppType, true);
            if (components != null)
            {
                foreach (var c in components)
                    result.Add(c);
            }
        }
        catch { }

        return result;
    }

    // --- IMGUI Settings Panel ---

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
