#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace Menace.SDK;

/// <summary>
/// UIToolkit-based settings panel that uses the game's native UI elements.
/// Replicates the game's SettingsWindow structure for consistent styling.
/// </summary>
public class UISettingsPanel
{
    private VisualElement _root;
    private VisualElement _settingsContainer;
    private bool _isVisible;

    // Cached types from game assembly
    private static Type _labeledToggleType;
    private static Type _labeledIntSliderType;
    private static Type _labeledDropdownType;
    private static bool _typesInitialized;

    // Track elements for value polling
    private readonly List<(object element, string modName, string key, SettingType type)> _elements = new();

    private static void InitializeTypes()
    {
        if (_typesInitialized) return;
        _typesInitialized = true;

        try
        {
            var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            if (gameAssembly != null)
            {
                // IL2CPP proxy types have Il2Cpp prefix on namespace
                _labeledToggleType = gameAssembly.GetType("Il2CppMenace.UI.LabeledToggle");
                _labeledIntSliderType = gameAssembly.GetType("Il2CppMenace.UI.LabeledIntSlider");
                _labeledDropdownType = gameAssembly.GetType("Il2CppMenace.UI.LabeledDropdown");

                SdkLogger.Msg($"[UISettingsPanel] Found game UI types: Toggle={_labeledToggleType != null}, Slider={_labeledIntSliderType != null}, Dropdown={_labeledDropdownType != null}");
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[UISettingsPanel] Failed to find game UI types: {ex.Message}");
        }
    }

    /// <summary>
    /// Create the settings panel using the game's native UI structure.
    /// </summary>
    public void Create(VisualElement documentRoot)
    {
        _root = documentRoot;
        InitializeTypes();

        // Try to find the game's existing SettingsWindow structure
        var settingsWindow = FindElement(documentRoot, "SettingsWindow");
        if (settingsWindow != null)
        {
            // Clone the structure or inject into existing
            SdkLogger.Msg("[UISettingsPanel] Found game SettingsWindow, will inject into similar structure");
        }

        // Create our own settings container matching the game's style
        CreateSettingsOverlay();
    }

    private void CreateSettingsOverlay()
    {
        // Create overlay that matches the game's modal style
        var overlay = new VisualElement();
        overlay.name = "ModSettingsOverlay";
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0;
        overlay.style.top = 0;
        overlay.style.right = 0;
        overlay.style.bottom = 0;
        overlay.style.backgroundColor = new Color(0, 0, 0, 0.8f);
        overlay.style.display = DisplayStyle.None;
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;

        // Try to copy styles from the game's existing window
        var gameWindow = FindElement(_root, "SettingsWindow");

        // Create window container
        var window = new VisualElement();
        window.name = "ModSettingsWindow";
        window.pickingMode = PickingMode.Position;

        // Style to match game's settings window - black background, centered via flexbox
        window.style.width = 600;
        window.style.maxHeight = new Length(70, LengthUnit.Percent);
        window.style.backgroundColor = new Color(0.02f, 0.02f, 0.03f, 0.98f); // Near black
        window.style.borderTopWidth = 1;
        window.style.borderBottomWidth = 1;
        window.style.borderLeftWidth = 1;
        window.style.borderRightWidth = 1;
        window.style.borderTopColor = new Color(0.2f, 0.22f, 0.25f);
        window.style.borderBottomColor = new Color(0.2f, 0.22f, 0.25f);
        window.style.borderLeftColor = new Color(0.2f, 0.22f, 0.25f);
        window.style.borderRightColor = new Color(0.2f, 0.22f, 0.25f);

        // Header - compact
        var header = new VisualElement();
        header.name = "WindowHeader";
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        header.style.paddingTop = 8;
        header.style.paddingBottom = 8;
        header.style.paddingLeft = 12;
        header.style.paddingRight = 12;
        header.style.borderBottomWidth = 1;
        header.style.borderBottomColor = new Color(0.15f, 0.15f, 0.18f);

        var title = new Label("Mod Settings");
        title.name = "WindowTitle";
        title.style.fontSize = 14;
        title.style.color = new Color(0.85f, 0.85f, 0.9f);
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.Add(title);

        var closeBtn = new Button();
        closeBtn.name = "CloseWindowButton";
        closeBtn.text = "✕";
        closeBtn.style.width = 22;
        closeBtn.style.height = 22;
        closeBtn.style.fontSize = 12;
        closeBtn.style.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
        closeBtn.style.color = new Color(0.7f, 0.7f, 0.7f);
        closeBtn.style.borderTopWidth = 0;
        closeBtn.style.borderBottomWidth = 0;
        closeBtn.style.borderLeftWidth = 0;
        closeBtn.style.borderRightWidth = 0;
        closeBtn.clickable.clicked += (Action)Hide;
        header.Add(closeBtn);

        window.Add(header);

        // Scrollable content area - compact
        var scrollView = new ScrollView(ScrollViewMode.Vertical);
        scrollView.name = "ScrollableSettings";
        scrollView.style.flexGrow = 1;
        scrollView.style.paddingTop = 4;
        scrollView.style.paddingBottom = 4;

        _settingsContainer = new VisualElement();
        _settingsContainer.name = "SettingsContainer";
        _settingsContainer.style.paddingLeft = 10;
        _settingsContainer.style.paddingRight = 10;
        scrollView.Add(_settingsContainer);

        window.Add(scrollView);

        // Footer with back button - compact
        var footer = new VisualElement();
        footer.style.flexDirection = FlexDirection.Row;
        footer.style.justifyContent = Justify.Center;
        footer.style.paddingTop = 8;
        footer.style.paddingBottom = 8;
        footer.style.borderTopWidth = 1;
        footer.style.borderTopColor = new Color(0.15f, 0.15f, 0.18f);

        var backBtn = new Button();
        backBtn.name = "BackButton";
        backBtn.text = "Back";
        backBtn.style.width = 80;
        backBtn.style.height = 24;
        backBtn.style.fontSize = 11;
        backBtn.style.backgroundColor = new Color(0.1f, 0.1f, 0.12f);
        backBtn.style.color = new Color(0.8f, 0.8f, 0.85f);
        backBtn.clickable.clicked += (Action)Hide;
        footer.Add(backBtn);

        window.Add(footer);
        overlay.Add(window);
        _root.Add(overlay);

        SdkLogger.Msg("[UISettingsPanel] Created settings overlay");
    }

    private void BuildSettingsContent()
    {
        if (_settingsContainer == null) return;

        _settingsContainer.Clear();
        _elements.Clear();

        var mods = ModSettings.GetRegisteredMods().ToList();

        foreach (var modName in mods)
        {
            // Mod header - compact
            var modHeader = new Label(modName);
            modHeader.style.fontSize = 12;
            modHeader.style.color = new Color(0.7f, 0.75f, 0.8f);
            modHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            modHeader.style.backgroundColor = new Color(0.06f, 0.06f, 0.08f);
            modHeader.style.paddingTop = 4;
            modHeader.style.paddingBottom = 4;
            modHeader.style.paddingLeft = 8;
            modHeader.style.marginTop = 8;
            modHeader.style.marginBottom = 4;
            _settingsContainer.Add(modHeader);

            var settings = ModSettings.GetSettingsForMod(modName);
            foreach (var setting in settings)
            {
                var row = CreateSettingRow(modName, setting);
                if (row != null)
                    _settingsContainer.Add(row);
            }
        }
    }

    private VisualElement CreateSettingRow(string modName, SettingDefinition setting)
    {
        // Try to use game's native elements first
        var nativeElement = TryCreateNativeElement(modName, setting);
        if (nativeElement != null)
            return nativeElement;

        // Fallback to custom styled elements
        return CreateFallbackRow(modName, setting);
    }

    private VisualElement TryCreateNativeElement(string modName, SettingDefinition setting)
    {
        try
        {
            switch (setting.Type)
            {
                case SettingType.Toggle when _labeledToggleType != null:
                    var boolVal = setting.Value is bool b ? b : (bool)(setting.DefaultValue ?? false);
                    var toggle = Activator.CreateInstance(_labeledToggleType, setting.Label, boolVal, true);
                    if (toggle is VisualElement toggleVe)
                    {
                        _elements.Add((toggle, modName, setting.Key, setting.Type));
                        return toggleVe;
                    }
                    break;

                case SettingType.Number when _labeledIntSliderType != null:
                    var intVal = setting.Value is int i ? i : Convert.ToInt32(setting.DefaultValue ?? 0);
                    // LabeledIntSlider needs a format func - use null for default
                    var slider = Activator.CreateInstance(_labeledIntSliderType,
                        setting.Label, intVal, (int)setting.Min, (int)setting.Max, null, true);
                    if (slider is VisualElement sliderVe)
                    {
                        _elements.Add((slider, modName, setting.Key, setting.Type));
                        return sliderVe;
                    }
                    break;

                case SettingType.Dropdown when _labeledDropdownType != null:
                    var strVal = setting.Value as string ?? setting.DefaultValue as string ?? "";
                    var options = setting.Options ?? Array.Empty<string>();
                    var selectedIdx = Array.IndexOf(options, strVal);
                    if (selectedIdx < 0) selectedIdx = 0;

                    // Create IL2CPP list
                    var il2cppList = new Il2CppSystem.Collections.Generic.List<string>();
                    foreach (var opt in options)
                        il2cppList.Add(opt);

                    var dropdown = Activator.CreateInstance(_labeledDropdownType,
                        setting.Label, il2cppList, selectedIdx, true);
                    if (dropdown is VisualElement dropdownVe)
                    {
                        _elements.Add((dropdown, modName, setting.Key, setting.Type));
                        return dropdownVe;
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Msg($"[UISettingsPanel] Native element creation failed for {setting.Key}: {ex.Message}");
        }

        return null;
    }

    private VisualElement CreateFallbackRow(string modName, SettingDefinition setting)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.marginBottom = 4;
        row.style.paddingLeft = 6;
        row.style.paddingRight = 6;
        row.style.minHeight = 20;

        // Common label color
        var labelColor = new Color(0.7f, 0.7f, 0.75f);
        var fontSize = 11;

        switch (setting.Type)
        {
            case SettingType.Header:
                var header = new Label($"— {setting.Label} —");
                header.style.fontSize = 10;
                header.style.color = new Color(0.45f, 0.48f, 0.52f);
                header.style.unityFontStyleAndWeight = FontStyle.Italic;
                header.style.unityTextAlign = TextAnchor.MiddleCenter;
                header.style.flexGrow = 1;
                row.Add(header);
                break;

            case SettingType.Toggle:
                var toggleLabel = new Label(setting.Label);
                toggleLabel.style.flexGrow = 1;
                toggleLabel.style.fontSize = fontSize;
                toggleLabel.style.color = labelColor;
                row.Add(toggleLabel);

                var toggle = new Toggle();
                var boolVal = setting.Value is bool b ? b : (bool)(setting.DefaultValue ?? false);
                toggle.value = boolVal;
                toggle.style.marginRight = 4;
                row.Add(toggle);
                _elements.Add((toggle, modName, setting.Key, setting.Type));
                break;

            case SettingType.Slider:
                var sliderLabel = new Label(setting.Label);
                sliderLabel.style.width = new Length(45, LengthUnit.Percent);
                sliderLabel.style.fontSize = fontSize;
                sliderLabel.style.color = labelColor;
                row.Add(sliderLabel);

                var floatVal = setting.Value is float f ? f : Convert.ToSingle(setting.DefaultValue ?? 0f);
                var slider = new Slider(setting.Min, setting.Max);
                slider.value = floatVal;
                slider.style.flexGrow = 1;
                slider.style.height = 16;
                row.Add(slider);
                _elements.Add((slider, modName, setting.Key, setting.Type));
                break;

            case SettingType.Number:
                var numLabel = new Label(setting.Label);
                numLabel.style.width = new Length(45, LengthUnit.Percent);
                numLabel.style.fontSize = fontSize;
                numLabel.style.color = labelColor;
                row.Add(numLabel);

                var intVal = setting.Value is int i ? i : Convert.ToInt32(setting.DefaultValue ?? 0);
                var sliderInt = new SliderInt((int)setting.Min, (int)setting.Max);
                sliderInt.value = intVal;
                sliderInt.style.flexGrow = 1;
                sliderInt.style.height = 16;
                row.Add(sliderInt);
                _elements.Add((sliderInt, modName, setting.Key, setting.Type));
                break;

            case SettingType.Dropdown:
                var dropLabel = new Label(setting.Label);
                dropLabel.style.width = new Length(45, LengthUnit.Percent);
                dropLabel.style.fontSize = fontSize;
                dropLabel.style.color = labelColor;
                row.Add(dropLabel);

                var strVal = setting.Value as string ?? setting.DefaultValue as string ?? "";
                var il2cppChoices = new Il2CppSystem.Collections.Generic.List<string>();
                foreach (var choice in setting.Options ?? Array.Empty<string>())
                    il2cppChoices.Add(choice);
                var dropdown = new DropdownField();
                dropdown.choices = il2cppChoices;
                dropdown.value = strVal;
                dropdown.style.flexGrow = 1;
                dropdown.style.height = 18;
                row.Add(dropdown);
                _elements.Add((dropdown, modName, setting.Key, setting.Type));
                break;

            case SettingType.Text:
                var textLabel = new Label(setting.Label);
                textLabel.style.width = new Length(45, LengthUnit.Percent);
                textLabel.style.fontSize = fontSize;
                textLabel.style.color = labelColor;
                row.Add(textLabel);

                var textVal = setting.Value as string ?? setting.DefaultValue as string ?? "";
                var textField = new TextField();
                textField.value = textVal;
                textField.style.flexGrow = 1;
                textField.style.height = 18;
                row.Add(textField);
                _elements.Add((textField, modName, setting.Key, setting.Type));
                break;

            default:
                return null;
        }

        return row;
    }

    private VisualElement FindElement(VisualElement root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root[i];
            var found = FindElement(child, name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// Poll for value changes. Call this from OnUpdate.
    /// </summary>
    public void PollChanges()
    {
        if (!_isVisible) return;

        foreach (var (element, modName, key, type) in _elements)
        {
            try
            {
                switch (type)
                {
                    case SettingType.Toggle:
                        if (element is Toggle toggle)
                        {
                            var currentBool = ModSettings.Get<bool>(modName, key);
                            if (toggle.value != currentBool)
                                ModSettings.Set(modName, key, toggle.value);
                        }
                        else
                        {
                            // Native LabeledToggle - use reflection
                            var getValue = element.GetType().GetMethod("GetValue");
                            if (getValue != null)
                            {
                                var val = (bool)getValue.Invoke(element, null);
                                var currentVal = ModSettings.Get<bool>(modName, key);
                                if (val != currentVal)
                                    ModSettings.Set(modName, key, val);
                            }
                        }
                        break;

                    case SettingType.Slider:
                        if (element is Slider slider)
                        {
                            var currentFloat = ModSettings.Get<float>(modName, key);
                            if (Math.Abs(slider.value - currentFloat) > 0.001f)
                                ModSettings.Set(modName, key, slider.value);
                        }
                        break;

                    case SettingType.Number:
                        if (element is SliderInt sliderInt)
                        {
                            var currentInt = ModSettings.Get<int>(modName, key);
                            if (sliderInt.value != currentInt)
                                ModSettings.Set(modName, key, sliderInt.value);
                        }
                        else
                        {
                            // Native LabeledIntSlider
                            var getValue = element.GetType().GetMethod("GetValue");
                            if (getValue != null)
                            {
                                var val = (int)getValue.Invoke(element, null);
                                var currentVal = ModSettings.Get<int>(modName, key);
                                if (val != currentVal)
                                    ModSettings.Set(modName, key, val);
                            }
                        }
                        break;

                    case SettingType.Dropdown:
                        if (element is DropdownField dropdown)
                        {
                            var currentStr = ModSettings.Get<string>(modName, key);
                            if (dropdown.value != currentStr)
                                ModSettings.Set(modName, key, dropdown.value);
                        }
                        else
                        {
                            // Native LabeledDropdown
                            var getValueIdx = element.GetType().GetMethod("GetValueIdx");
                            if (getValueIdx != null)
                            {
                                var idx = (int)getValueIdx.Invoke(element, null);
                                var settings = ModSettings.GetSettingsForMod(modName)
                                    .FirstOrDefault(s => s.Key == key);
                                if (settings?.Options != null && idx >= 0 && idx < settings.Options.Length)
                                {
                                    var newVal = settings.Options[idx];
                                    var currentVal = ModSettings.Get<string>(modName, key);
                                    if (newVal != currentVal)
                                        ModSettings.Set(modName, key, newVal);
                                }
                            }
                        }
                        break;

                    case SettingType.Text:
                        if (element is TextField textField)
                        {
                            var currentText = ModSettings.Get<string>(modName, key);
                            if (textField.value != currentText)
                                ModSettings.Set(modName, key, textField.value);
                        }
                        break;
                }
            }
            catch { /* Ignore polling errors */ }
        }
    }

    public void Show()
    {
        var overlay = FindElement(_root, "ModSettingsOverlay");
        if (overlay == null) return;

        BuildSettingsContent();
        overlay.style.display = DisplayStyle.Flex;
        _isVisible = true;
        SdkLogger.Msg("[UISettingsPanel] Shown");
    }

    public void Hide()
    {
        var overlay = FindElement(_root, "ModSettingsOverlay");
        if (overlay == null) return;

        overlay.style.display = DisplayStyle.None;
        _isVisible = false;
        ModSettings.Save();
        SdkLogger.Msg("[UISettingsPanel] Hidden");
    }

    public bool IsVisible => _isVisible;

    public void Destroy()
    {
        var overlay = FindElement(_root, "ModSettingsOverlay");
        if (overlay != null && _root != null)
        {
            _root.Remove(overlay);
        }
        _settingsContainer = null;
        _elements.Clear();
    }
}
