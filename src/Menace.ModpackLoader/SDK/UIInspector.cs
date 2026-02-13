#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// Inspects the game's UI hierarchy and provides information about visible elements.
/// Uses IL2CPP type lookup to work with Unity UI types.
/// </summary>
public static class UIInspector
{
    // Cached GameType wrappers for Unity UI (resolved at runtime)
    private static GameType _canvasType;
    private static GameType _buttonType;
    private static GameType _textType;
    private static GameType _tmpTextType;
    private static GameType _toggleType;
    private static GameType _inputFieldType;
    private static GameType _dropdownType;
    private static GameType _imageType;
    private static GameType _selectableType;
    private static bool _typesResolved;

    /// <summary>
    /// Information about a UI element.
    /// </summary>
    public class UIElementInfo
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public string Canvas { get; set; }
        public string Path { get; set; }
        public bool Interactable { get; set; } = true;
        public int FontSize { get; set; }

        // For toggles
        public bool? IsOn { get; set; }

        // For dropdowns
        public int? SelectedIndex { get; set; }
        public string SelectedText { get; set; }
        public List<string> Options { get; set; }

        // For input fields
        public string Placeholder { get; set; }
    }

    /// <summary>
    /// Result of a click operation.
    /// </summary>
    public class ClickResult
    {
        public bool Success { get; set; }
        public string ClickedName { get; set; }
        public string ClickedPath { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Initialize the UI inspector by resolving Unity UI types via IL2CPP.
    /// Called automatically on first use.
    /// </summary>
    public static void Initialize()
    {
        if (_typesResolved) return;
        _typesResolved = true;

        try
        {
            // Use GameType.Find for proper IL2CPP type resolution
            // Canvas is in UnityEngine.UIModule
            _canvasType = GameType.Find("UnityEngine.Canvas", "UnityEngine.UIModule");

            // Standard Unity UI types are in UnityEngine.UI assembly
            _buttonType = GameType.Find("UnityEngine.UI.Button", "UnityEngine.UI");
            _textType = GameType.Find("UnityEngine.UI.Text", "UnityEngine.UI");
            _toggleType = GameType.Find("UnityEngine.UI.Toggle", "UnityEngine.UI");
            _inputFieldType = GameType.Find("UnityEngine.UI.InputField", "UnityEngine.UI");
            _dropdownType = GameType.Find("UnityEngine.UI.Dropdown", "UnityEngine.UI");
            _imageType = GameType.Find("UnityEngine.UI.Image", "UnityEngine.UI");
            _selectableType = GameType.Find("UnityEngine.UI.Selectable", "UnityEngine.UI");

            // TextMeshPro types
            _tmpTextType = GameType.Find("TMPro.TMP_Text", "Unity.TextMeshPro");
            if (!_tmpTextType.IsValid)
                _tmpTextType = GameType.Find("TMPro.TextMeshProUGUI", "Unity.TextMeshPro");

            SdkLogger.Msg($"[UIInspector] Types resolved - Canvas:{_canvasType?.IsValid}, Button:{_buttonType?.IsValid}, Text:{_textType?.IsValid}, TMP:{_tmpTextType?.IsValid}");
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[UIInspector] Failed to resolve UI types: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all visible UI elements from all active canvases.
    /// </summary>
    public static List<UIElementInfo> GetAllElements()
    {
        Initialize();
        var elements = new List<UIElementInfo>();

        if (_canvasType == null || !_canvasType.IsValid)
        {
            SdkLogger.Warning("[UIInspector] Canvas type not resolved");
            return elements;
        }

        try
        {
            // Use managed type for Il2CppType.From
            var managedCanvasType = _canvasType.ManagedType;
            if (managedCanvasType == null)
            {
                SdkLogger.Warning("[UIInspector] No managed Canvas type available");
                return elements;
            }

            var il2cppCanvasType = Il2CppType.From(managedCanvasType);
            var canvases = UnityEngine.Object.FindObjectsOfType(il2cppCanvasType);

            SdkLogger.Msg($"[UIInspector] Found {canvases?.Length ?? 0} canvases");

            foreach (var canvas in canvases)
            {
                if (canvas == null) continue;

                var go = GetGameObject(canvas);
                if (go == null || !go.activeInHierarchy) continue;

                SdkLogger.Msg($"[UIInspector] Scanning canvas: {go.name}");
                ExtractUIElements(go.transform, elements, 0, go.name);
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[UIInspector] Failed to inspect UI: {ex.Message}\n{ex.StackTrace}");
        }

        return elements;
    }

    /// <summary>
    /// Click a button by path or name.
    /// </summary>
    public static ClickResult ClickButton(string path = null, string name = null)
    {
        Initialize();

        if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(name))
        {
            return new ClickResult { Success = false, Error = "Specify 'path' or 'name'" };
        }

        if (_buttonType == null || !_buttonType.IsValid)
        {
            return new ClickResult { Success = false, Error = "Button type not resolved" };
        }

        var managedButtonType = _buttonType.ManagedType;
        if (managedButtonType == null)
        {
            return new ClickResult { Success = false, Error = "No managed Button type available" };
        }

        try
        {
            Component targetButton = null;
            GameObject targetGo = null;

            if (!string.IsNullOrWhiteSpace(path))
            {
                // Find by exact path
                targetGo = GameObject.Find(path);
                if (targetGo != null)
                {
                    targetButton = targetGo.GetComponent(Il2CppType.From(managedButtonType));
                }
            }
            else if (!string.IsNullOrWhiteSpace(name))
            {
                // Find by name (search all buttons)
                var il2cppButtonType = Il2CppType.From(managedButtonType);
                var buttons = UnityEngine.Object.FindObjectsOfType(il2cppButtonType);

                foreach (var btn in buttons)
                {
                    if (btn == null) continue;

                    var go = GetGameObject(btn);
                    if (go == null) continue;

                    if (go.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        targetButton = btn as Component;
                        targetGo = go;
                        break;
                    }

                    // Also check button text
                    var btnText = GetTextFromChildren(go);
                    if (btnText != null && btnText.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        targetButton = btn as Component;
                        targetGo = go;
                        break;
                    }
                }
            }

            if (targetButton == null)
            {
                return new ClickResult { Success = false, Error = $"Button not found: {path ?? name}" };
            }

            // Check interactable
            var interactableProp = managedButtonType.GetProperty("interactable");
            if (interactableProp != null)
            {
                var interactable = (bool)interactableProp.GetValue(targetButton);
                if (!interactable)
                {
                    return new ClickResult { Success = false, Error = "Button is not interactable" };
                }
            }

            // Invoke onClick
            var onClickProp = managedButtonType.GetProperty("onClick");
            if (onClickProp != null)
            {
                var onClick = onClickProp.GetValue(targetButton);
                if (onClick != null)
                {
                    var invokeMethod = onClick.GetType().GetMethod("Invoke", Type.EmptyTypes);
                    invokeMethod?.Invoke(onClick, null);
                }
            }

            return new ClickResult
            {
                Success = true,
                ClickedName = targetGo.name,
                ClickedPath = GetPath(targetGo.transform)
            };
        }
        catch (Exception ex)
        {
            return new ClickResult { Success = false, Error = $"Click failed: {ex.Message}" };
        }
    }

    private static void ExtractUIElements(Transform parent, List<UIElementInfo> elements, int depth, string canvasName)
    {
        if (depth > 15 || parent == null) return;

        var go = parent.gameObject;
        if (!go.activeInHierarchy) return;

        bool addedElement = false;

        // Check for button
        if (_buttonType != null && _buttonType.IsValid)
        {
            var managedButtonType = _buttonType.ManagedType;
            if (managedButtonType != null)
            {
                var button = go.GetComponent(Il2CppType.From(managedButtonType));
                if (button != null)
                {
                    var interactable = GetPropertyBool(button, "interactable", true);
                    if (interactable)
                    {
                        var buttonText = GetTextFromChildren(go);
                        elements.Add(new UIElementInfo
                        {
                            Type = "Button",
                            Name = go.name,
                            Text = buttonText,
                            Canvas = canvasName,
                            Path = GetPath(parent),
                            Interactable = true
                        });
                        addedElement = true;
                    }
                }
            }
        }

        // Check for toggle
        if (_toggleType != null && _toggleType.IsValid && !addedElement)
        {
            var managedToggleType = _toggleType.ManagedType;
            if (managedToggleType != null)
            {
                var toggle = go.GetComponent(Il2CppType.From(managedToggleType));
                if (toggle != null)
                {
                    var toggleText = GetTextFromChildren(go);
                    elements.Add(new UIElementInfo
                    {
                        Type = "Toggle",
                        Name = go.name,
                        Text = toggleText,
                        Canvas = canvasName,
                        Path = GetPath(parent),
                        IsOn = GetPropertyBool(toggle, "isOn", false),
                        Interactable = GetPropertyBool(toggle, "interactable", true)
                    });
                    addedElement = true;
                }
            }
        }

        // Check for input field
        if (_inputFieldType != null && _inputFieldType.IsValid && !addedElement)
        {
            var managedInputFieldType = _inputFieldType.ManagedType;
            if (managedInputFieldType != null)
            {
                var inputField = go.GetComponent(Il2CppType.From(managedInputFieldType));
                if (inputField != null)
                {
                    var text = GetPropertyString(inputField, "text");
                    var placeholder = GetPlaceholderText(inputField);
                    elements.Add(new UIElementInfo
                    {
                        Type = "InputField",
                        Name = go.name,
                        Text = text,
                        Placeholder = placeholder,
                        Canvas = canvasName,
                        Path = GetPath(parent),
                        Interactable = GetPropertyBool(inputField, "interactable", true)
                    });
                    addedElement = true;
                }
            }
        }

        // Check for dropdown
        if (_dropdownType != null && _dropdownType.IsValid && !addedElement)
        {
            var managedDropdownType = _dropdownType.ManagedType;
            if (managedDropdownType != null)
            {
                var dropdown = go.GetComponent(Il2CppType.From(managedDropdownType));
                if (dropdown != null)
                {
                    var options = GetDropdownOptions(dropdown);
                    var selectedIndex = GetPropertyInt(dropdown, "value", 0);
                    elements.Add(new UIElementInfo
                    {
                        Type = "Dropdown",
                        Name = go.name,
                        SelectedIndex = selectedIndex,
                        SelectedText = selectedIndex < options.Count ? options[selectedIndex] : null,
                        Options = options,
                        Canvas = canvasName,
                        Path = GetPath(parent),
                        Interactable = GetPropertyBool(dropdown, "interactable", true)
                    });
                    addedElement = true;
                }
            }
        }

        // Check for standalone text (headers, labels)
        if (!addedElement)
        {
            var textInfo = GetStandaloneText(go);
            if (textInfo != null)
            {
                // Skip if it's a child of another element we already found
                if (!HasParentWithComponent(go, _buttonType) && !HasParentWithComponent(go, _toggleType))
                {
                    elements.Add(new UIElementInfo
                    {
                        Type = textInfo.Value.fontSize >= 20 ? "Header" : "Text",
                        Name = go.name,
                        Text = textInfo.Value.text,
                        Canvas = canvasName,
                        Path = GetPath(parent),
                        FontSize = textInfo.Value.fontSize
                    });
                }
            }
        }

        // Recurse into children
        for (int i = 0; i < parent.childCount; i++)
        {
            ExtractUIElements(parent.GetChild(i), elements, depth + 1, canvasName);
        }
    }

    private static (string text, int fontSize)? GetStandaloneText(GameObject go)
    {
        // Try Unity UI Text
        if (_textType != null && _textType.IsValid)
        {
            var managedTextType = _textType.ManagedType;
            if (managedTextType != null)
            {
                var text = go.GetComponent(Il2CppType.From(managedTextType));
                if (text != null)
                {
                    var textStr = GetPropertyString(text, "text");
                    if (!string.IsNullOrWhiteSpace(textStr))
                    {
                        var fontSize = GetPropertyInt(text, "fontSize", 14);
                        return (textStr, fontSize);
                    }
                }
            }
        }

        // Try TMPro
        if (_tmpTextType != null && _tmpTextType.IsValid)
        {
            var managedTmpType = _tmpTextType.ManagedType;
            if (managedTmpType != null)
            {
                var tmp = go.GetComponent(Il2CppType.From(managedTmpType));
                if (tmp != null)
                {
                    var textStr = GetPropertyString(tmp, "text");
                    if (!string.IsNullOrWhiteSpace(textStr))
                    {
                        var fontSize = (int)GetPropertyFloat(tmp, "fontSize", 14f);
                        return (textStr, fontSize);
                    }
                }
            }
        }

        return null;
    }

    private static string GetTextFromChildren(GameObject go)
    {
        // Try Unity UI Text
        if (_textType != null && _textType.IsValid)
        {
            var text = FindComponentInChildren(go, _textType);
            if (text != null)
            {
                var textStr = GetPropertyString(text, "text");
                if (!string.IsNullOrWhiteSpace(textStr))
                    return textStr;
            }
        }

        // Try TMPro
        if (_tmpTextType != null && _tmpTextType.IsValid)
        {
            var tmp = FindComponentInChildren(go, _tmpTextType);
            if (tmp != null)
            {
                var textStr = GetPropertyString(tmp, "text");
                if (!string.IsNullOrWhiteSpace(textStr))
                    return textStr;
            }
        }

        return null;
    }

    private static string GetPlaceholderText(object inputField)
    {
        try
        {
            if (_inputFieldType == null || !_inputFieldType.IsValid) return null;
            var managedType = _inputFieldType.ManagedType;
            if (managedType == null) return null;

            var placeholderProp = managedType.GetProperty("placeholder");
            if (placeholderProp != null)
            {
                var placeholder = placeholderProp.GetValue(inputField);
                if (placeholder != null && _textType != null && _textType.IsValid)
                {
                    return GetPropertyString(placeholder, "text");
                }
            }
        }
        catch { }
        return null;
    }

    private static List<string> GetDropdownOptions(object dropdown)
    {
        var result = new List<string>();
        try
        {
            if (_dropdownType == null || !_dropdownType.IsValid) return result;
            var managedType = _dropdownType.ManagedType;
            if (managedType == null) return result;

            var optionsProp = managedType.GetProperty("options");
            if (optionsProp != null)
            {
                var options = optionsProp.GetValue(dropdown);
                if (options != null)
                {
                    // It's a List<Dropdown.OptionData>, iterate using reflection
                    var countProp = options.GetType().GetProperty("Count");
                    var itemProp = options.GetType().GetProperty("Item");

                    if (countProp != null && itemProp != null)
                    {
                        var count = (int)countProp.GetValue(options);
                        for (int i = 0; i < count; i++)
                        {
                            var item = itemProp.GetValue(options, new object[] { i });
                            if (item != null)
                            {
                                var textProp = item.GetType().GetProperty("text");
                                if (textProp != null)
                                {
                                    var text = textProp.GetValue(item) as string;
                                    result.Add(text ?? "");
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }
        return result;
    }

    private static bool HasParentWithComponent(GameObject go, GameType gameType)
    {
        if (gameType == null || !gameType.IsValid) return false;

        var managedType = gameType.ManagedType;
        if (managedType == null) return false;

        var parent = go.transform.parent;
        while (parent != null)
        {
            var component = parent.GetComponent(Il2CppType.From(managedType));
            if (component != null) return true;
            parent = parent.parent;
        }
        return false;
    }

    // --- Reflection helpers ---

    private static GameObject GetGameObject(object component)
    {
        if (component == null) return null;
        if (component is GameObject go) return go;
        if (component is Component c) return c.gameObject;

        var goProp = component.GetType().GetProperty("gameObject");
        return goProp?.GetValue(component) as GameObject;
    }

    private static Component FindComponentInChildren(GameObject go, GameType gameType)
    {
        if (go == null || gameType == null || !gameType.IsValid) return null;

        try
        {
            var managedType = gameType.ManagedType;
            if (managedType == null) return null;

            var il2cppType = Il2CppType.From(managedType);
            return go.GetComponentInChildren(il2cppType);
        }
        catch
        {
            return null;
        }
    }

    private static string GetPath(Transform t)
    {
        var parts = new List<string>();
        while (t != null)
        {
            parts.Insert(0, t.name);
            t = t.parent;
        }
        return string.Join("/", parts);
    }

    private static string GetPropertyString(object obj, string propertyName)
    {
        try
        {
            var prop = obj.GetType().GetProperty(propertyName);
            return prop?.GetValue(obj) as string;
        }
        catch
        {
            return null;
        }
    }

    private static bool GetPropertyBool(object obj, string propertyName, bool defaultValue)
    {
        try
        {
            var prop = obj.GetType().GetProperty(propertyName);
            if (prop != null)
                return (bool)prop.GetValue(obj);
        }
        catch { }
        return defaultValue;
    }

    private static int GetPropertyInt(object obj, string propertyName, int defaultValue)
    {
        try
        {
            var prop = obj.GetType().GetProperty(propertyName);
            if (prop != null)
                return Convert.ToInt32(prop.GetValue(obj));
        }
        catch { }
        return defaultValue;
    }

    private static float GetPropertyFloat(object obj, string propertyName, float defaultValue)
    {
        try
        {
            var prop = obj.GetType().GetProperty(propertyName);
            if (prop != null)
                return Convert.ToSingle(prop.GetValue(obj));
        }
        catch { }
        return defaultValue;
    }
}
