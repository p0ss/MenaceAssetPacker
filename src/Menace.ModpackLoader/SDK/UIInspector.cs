#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Menace.SDK;

/// <summary>
/// Inspects the game's UI hierarchy and provides information about visible elements.
/// Supports UIToolkit (UIElements) which is the UI system used by Menace.
/// </summary>
public static class UIInspector
{
    /// <summary>
    /// Information about a UI element.
    /// </summary>
    public class UIElementInfo
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
        public string Document { get; set; }
        public string Path { get; set; }
        public bool Interactable { get; set; } = true;
        public bool Visible { get; set; } = true;
        public int FontSize { get; set; }
        public bool? IsOn { get; set; }
        public float? SliderValue { get; set; }
        public int? SelectedIndex { get; set; }
        public string SelectedText { get; set; }
        public List<string> Options { get; set; }
        public string Placeholder { get; set; }
        // Legacy compatibility
        public string Canvas => Document;
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
    /// Get diagnostic info about UI inspection state.
    /// </summary>
    public static Dictionary<string, object> GetDiagnostics()
    {
        var diag = new Dictionary<string, object>();

        try
        {
            var docs = UnityEngine.Object.FindObjectsOfType<UIDocument>();
            diag["uiSystem"] = "UIToolkit";
            diag["documentCount"] = docs?.Length ?? 0;

            var docInfo = new List<string>();
            foreach (var doc in docs ?? Array.Empty<UIDocument>())
            {
                if (doc == null) continue;
                var root = doc.rootVisualElement;
                var active = doc.gameObject.activeInHierarchy;
                var childCount = root?.childCount ?? 0;
                docInfo.Add($"{doc.gameObject.name} (active={active}, children={childCount})");
            }
            diag["documents"] = docInfo;

            // Count elements in active documents
            int buttonCount = 0, labelCount = 0, toggleCount = 0, totalCount = 0;
            foreach (var doc in docs ?? Array.Empty<UIDocument>())
            {
                if (doc == null || !doc.gameObject.activeInHierarchy) continue;
                var root = doc.rootVisualElement;
                if (root == null) continue;

                buttonCount += QueryAll<Button>(root).Count;
                labelCount += QueryAll<Label>(root).Count;
                toggleCount += QueryAll<Toggle>(root).Count;
                totalCount += QueryAll<VisualElement>(root).Count;
            }

            diag["buttonCount"] = buttonCount;
            diag["labelCount"] = labelCount;
            diag["toggleCount"] = toggleCount;
            diag["totalElementCount"] = totalCount;
        }
        catch (Exception ex)
        {
            diag["error"] = $"{ex.GetType().Name}: {ex.Message}";
        }

        return diag;
    }

    /// <summary>
    /// Get all visible UI elements from all active UIDocuments.
    /// </summary>
    public static List<UIElementInfo> GetAllElements()
    {
        var elements = new List<UIElementInfo>();

        try
        {
            var docs = UnityEngine.Object.FindObjectsOfType<UIDocument>();
            if (docs == null || docs.Length == 0)
            {
                SdkLogger.Msg("[UIInspector] No UIDocument found");
                return elements;
            }

            foreach (var doc in docs)
            {
                if (doc == null || !doc.gameObject.activeInHierarchy) continue;
                var root = doc.rootVisualElement;
                if (root == null) continue;

                var docName = doc.gameObject.name;
                SdkLogger.Msg($"[UIInspector] Scanning UIDocument: {docName}");

                // Extract buttons
                foreach (var btn in QueryAll<Button>(root))
                {
                    if (!IsVisible(btn)) continue;
                    elements.Add(new UIElementInfo
                    {
                        Type = "Button",
                        Name = btn.name,
                        Text = GetButtonText(btn),
                        Document = docName,
                        Path = GetPath(btn),
                        Interactable = btn.enabledSelf,
                        Visible = true
                    });
                }

                // Extract toggles
                foreach (var toggle in QueryAll<Toggle>(root))
                {
                    if (!IsVisible(toggle)) continue;
                    elements.Add(new UIElementInfo
                    {
                        Type = "Toggle",
                        Name = toggle.name,
                        Text = toggle.label,
                        Document = docName,
                        Path = GetPath(toggle),
                        Interactable = toggle.enabledSelf,
                        IsOn = toggle.value,
                        Visible = true
                    });
                }

                // Extract text fields
                foreach (var field in QueryAll<TextField>(root))
                {
                    if (!IsVisible(field)) continue;
                    elements.Add(new UIElementInfo
                    {
                        Type = "TextField",
                        Name = field.name,
                        Text = field.value,
                        Document = docName,
                        Path = GetPath(field),
                        Interactable = field.enabledSelf,
                        Placeholder = field.textEdition?.placeholder,
                        Visible = true
                    });
                }

                // Extract dropdowns
                foreach (var dropdown in QueryAll<DropdownField>(root))
                {
                    if (!IsVisible(dropdown)) continue;
                    elements.Add(new UIElementInfo
                    {
                        Type = "Dropdown",
                        Name = dropdown.name,
                        Text = dropdown.value,
                        Document = docName,
                        Path = GetPath(dropdown),
                        Interactable = dropdown.enabledSelf,
                        SelectedIndex = dropdown.index,
                        SelectedText = dropdown.value,
                        Options = ConvertChoices(dropdown.choices),
                        Visible = true
                    });
                }

                // Extract sliders
                foreach (var slider in QueryAll<Slider>(root))
                {
                    if (!IsVisible(slider)) continue;
                    elements.Add(new UIElementInfo
                    {
                        Type = "Slider",
                        Name = slider.name,
                        Text = slider.label,
                        Document = docName,
                        Path = GetPath(slider),
                        Interactable = slider.enabledSelf,
                        SliderValue = slider.value,
                        Visible = true
                    });
                }

                // Extract standalone labels (not part of other controls)
                foreach (var label in QueryAll<Label>(root))
                {
                    if (!IsVisible(label)) continue;
                    // Skip labels that are children of interactive elements
                    if (IsChildOfInteractiveElement(label)) continue;
                    // Skip empty labels
                    if (string.IsNullOrWhiteSpace(label.text)) continue;

                    elements.Add(new UIElementInfo
                    {
                        Type = IsHeaderLabel(label) ? "Header" : "Label",
                        Name = label.name,
                        Text = label.text,
                        Document = docName,
                        Path = GetPath(label),
                        FontSize = GetFontSize(label),
                        Visible = true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[UIInspector] Failed to inspect UI: {ex.Message}");
        }

        return elements;
    }

    /// <summary>
    /// Click a button by path or name.
    /// </summary>
    public static ClickResult ClickButton(string path = null, string name = null)
    {
        if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(name))
            return new ClickResult { Success = false, Error = "Specify 'path' or 'name'" };

        try
        {
            var docs = UnityEngine.Object.FindObjectsOfType<UIDocument>();
            if (docs == null || docs.Length == 0)
                return new ClickResult { Success = false, Error = "No UIDocument found" };

            foreach (var doc in docs)
            {
                if (doc == null || !doc.gameObject.activeInHierarchy) continue;
                var root = doc.rootVisualElement;
                if (root == null) continue;

                foreach (var btn in QueryAll<Button>(root))
                {
                    if (!IsVisible(btn) || !btn.enabledSelf) continue;

                    var btnPath = GetPath(btn);
                    var btnText = GetButtonText(btn);

                    // Match by path
                    if (!string.IsNullOrWhiteSpace(path) && btnPath == path)
                    {
                        return DoClick(btn, btnPath);
                    }

                    // Match by name
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        if (btn.name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                            (btnText != null && btnText.Equals(name, StringComparison.OrdinalIgnoreCase)))
                        {
                            return DoClick(btn, btnPath);
                        }
                    }
                }
            }

            return new ClickResult { Success = false, Error = $"Button not found: {path ?? name}" };
        }
        catch (Exception ex)
        {
            return new ClickResult { Success = false, Error = $"Click failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// Set a toggle value by path or name.
    /// </summary>
    public static ClickResult SetToggle(string path = null, string name = null, bool value = true)
    {
        if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(name))
            return new ClickResult { Success = false, Error = "Specify 'path' or 'name'" };

        try
        {
            var docs = UnityEngine.Object.FindObjectsOfType<UIDocument>();
            foreach (var doc in docs ?? Array.Empty<UIDocument>())
            {
                if (doc == null || !doc.gameObject.activeInHierarchy) continue;
                var root = doc.rootVisualElement;
                if (root == null) continue;

                foreach (var toggle in QueryAll<Toggle>(root))
                {
                    if (!IsVisible(toggle) || !toggle.enabledSelf) continue;

                    var togglePath = GetPath(toggle);

                    if ((!string.IsNullOrWhiteSpace(path) && togglePath == path) ||
                        (!string.IsNullOrWhiteSpace(name) && toggle.name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        toggle.value = value;
                        return new ClickResult
                        {
                            Success = true,
                            ClickedName = toggle.name,
                            ClickedPath = togglePath
                        };
                    }
                }
            }

            return new ClickResult { Success = false, Error = $"Toggle not found: {path ?? name}" };
        }
        catch (Exception ex)
        {
            return new ClickResult { Success = false, Error = $"SetToggle failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// Set a text field value by path or name.
    /// </summary>
    public static ClickResult SetTextField(string path = null, string name = null, string value = "")
    {
        if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(name))
            return new ClickResult { Success = false, Error = "Specify 'path' or 'name'" };

        try
        {
            var docs = UnityEngine.Object.FindObjectsOfType<UIDocument>();
            foreach (var doc in docs ?? Array.Empty<UIDocument>())
            {
                if (doc == null || !doc.gameObject.activeInHierarchy) continue;
                var root = doc.rootVisualElement;
                if (root == null) continue;

                foreach (var field in QueryAll<TextField>(root))
                {
                    if (!IsVisible(field) || !field.enabledSelf) continue;

                    var fieldPath = GetPath(field);

                    if ((!string.IsNullOrWhiteSpace(path) && fieldPath == path) ||
                        (!string.IsNullOrWhiteSpace(name) && field.name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        field.value = value;
                        return new ClickResult
                        {
                            Success = true,
                            ClickedName = field.name,
                            ClickedPath = fieldPath
                        };
                    }
                }
            }

            return new ClickResult { Success = false, Error = $"TextField not found: {path ?? name}" };
        }
        catch (Exception ex)
        {
            return new ClickResult { Success = false, Error = $"SetTextField failed: {ex.Message}" };
        }
    }

    // ==================== Helpers ====================

    private static List<T> QueryAll<T>(VisualElement root) where T : VisualElement
    {
        try
        {
            var il2cppList = UQueryExtensions.Query<T>(root, null, (string)null).ToList();
            // Convert IL2CPP list to System list
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

    private static ClickResult DoClick(Button btn, string path)
    {
        try
        {
            // UIToolkit buttons - invoke the clicked callback directly
            // The clicked property is a Clickable manipulator with a clicked Action
            var clickable = btn.clickable;
            if (clickable != null)
            {
                // Use the clicked Action delegate directly
                var clickedAction = clickable.clicked;
                clickedAction?.Invoke();
            }

            return new ClickResult
            {
                Success = true,
                ClickedName = btn.name,
                ClickedPath = path
            };
        }
        catch (Exception ex)
        {
            return new ClickResult { Success = false, Error = $"Click failed: {ex.Message}" };
        }
    }

    private static List<string> ConvertChoices(Il2CppSystem.Collections.Generic.List<string> choices)
    {
        if (choices == null) return null;
        var result = new List<string>();
        for (int i = 0; i < choices.Count; i++)
        {
            result.Add(choices[i]);
        }
        return result;
    }

    private static bool IsVisible(VisualElement element)
    {
        if (element == null) return false;
        // Check if element is displayed and has non-zero size
        if (element.resolvedStyle.display == DisplayStyle.None) return false;
        if (element.resolvedStyle.visibility == Visibility.Hidden) return false;
        if (element.resolvedStyle.opacity < 0.01f) return false;
        return true;
    }

    private static string GetButtonText(Button btn)
    {
        // Try direct text property
        if (!string.IsNullOrWhiteSpace(btn.text))
            return btn.text;

        // Try to find a label child
        var label = UQueryExtensions.Q<Label>(btn, null, (string)null);
        if (label != null && !string.IsNullOrWhiteSpace(label.text))
            return label.text;

        return null;
    }

    private static bool IsChildOfInteractiveElement(VisualElement element)
    {
        var parent = element.parent;
        while (parent != null)
        {
            if (parent is Button || parent is Toggle || parent is TextField ||
                parent is DropdownField || parent is Slider)
                return true;
            parent = parent.parent;
        }
        return false;
    }

    private static bool IsHeaderLabel(Label label)
    {
        var fontSize = GetFontSize(label);
        return fontSize >= 20;
    }

    private static int GetFontSize(VisualElement element)
    {
        try
        {
            // In IL2CPP, resolvedStyle.fontSize is a float
            var size = element.resolvedStyle.fontSize;
            return (int)size;
        }
        catch
        {
            return 14;
        }
    }

    private static string GetPath(VisualElement element)
    {
        var parts = new List<string>();
        var current = element;
        while (current != null)
        {
            var name = current.name;
            if (string.IsNullOrEmpty(name))
                name = current.GetType().Name;
            parts.Insert(0, name);
            current = current.parent;
        }
        return string.Join("/", parts);
    }
}
