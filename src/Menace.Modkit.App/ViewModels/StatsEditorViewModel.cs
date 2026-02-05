using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ReactiveUI;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.ViewModels;

public sealed class StatsEditorViewModel : ViewModelBase
{
    private DataTemplateLoader? _dataLoader;
    private readonly ModpackManager _modpackManager;
    private readonly AssetReferenceResolver _assetResolver;
    private readonly SchemaService _schemaService;
    private string? _assetOutputPath;

    // Change tracking: key = "{TemplateTypeName}/{instanceName}", value = { "field": value }
    private readonly Dictionary<string, Dictionary<string, object?>> _pendingChanges = new();
    private readonly Dictionary<string, Dictionary<string, object?>> _stagingOverrides = new();

    // Clone tracking: compositeKey ("TemplateType/newName") → sourceName
    private readonly Dictionary<string, string> _cloneDefinitions = new();

    // Tracks which fields the user explicitly edited in the current selection.
    // Only these fields are checked for diffs on flush, preventing false diffs
    // from TextChanged events during rendering or type mismatches.
    private readonly HashSet<string> _userEditedFields = new();

    // Flag to suppress UpdateModifiedProperty during initial render
    // (TextBoxes fire TextChanged when created, which would create false diffs)
    private bool _suppressPropertyUpdates;

    // Cache: template type name -> sorted list of instance names
    private readonly Dictionary<string, List<string>> _templateInstanceNamesCache = new();

    public StatsEditorViewModel()
    {
        _modpackManager = new ModpackManager();
        _assetResolver = new AssetReferenceResolver();
        _schemaService = new SchemaService();
        TreeNodes = new ObservableCollection<TreeNodeViewModel>();
        AvailableModpacks = new ObservableCollection<string>();

        LoadData();
    }

    public ModpackManager ModpackManager => _modpackManager;

    public void LoadData()
    {
        // Check if vanilla data exists
        if (!_modpackManager.HasVanillaData())
        {
            ShowVanillaDataWarning = true;
            TreeNodes.Clear();
            return;
        }

        ShowVanillaDataWarning = false;
        _dataLoader = new DataTemplateLoader(_modpackManager.VanillaDataPath);
        _templateInstanceNamesCache.Clear();

        // Load asset references from game install
        var gameInstallPath = _modpackManager.GetGameInstallPath();
        if (!string.IsNullOrEmpty(gameInstallPath))
        {
            _assetResolver.LoadReferences(gameInstallPath);
        }

        // Load schema for field metadata (asset type detection)
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "schema.json");
        if (!File.Exists(schemaPath))
            schemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "schema.json");
        _schemaService.LoadSchema(schemaPath);

        // Determine asset output path via centralized setting
        _assetOutputPath = AppSettings.GetEffectiveAssetsPath();

        // Populate available modpacks
        AvailableModpacks.Clear();
        foreach (var mp in _modpackManager.GetStagingModpacks())
            AvailableModpacks.Add(mp.Name);

        LoadAllTemplates();
    }

    public ObservableCollection<TreeNodeViewModel> TreeNodes { get; }
    public ObservableCollection<string> AvailableModpacks { get; }

    private bool _showVanillaDataWarning;
    public bool ShowVanillaDataWarning
    {
        get => _showVanillaDataWarning;
        set => this.RaiseAndSetIfChanged(ref _showVanillaDataWarning, value);
    }

    private string? _currentModpackName;
    public string? CurrentModpackName
    {
        get => _currentModpackName;
        set
        {
            if (_currentModpackName != value)
            {
                FlushCurrentEdits();
                this.RaiseAndSetIfChanged(ref _currentModpackName, value);
                _pendingChanges.Clear();
                LoadStagingOverrides();
                // Re-render current node with new overrides
                if (_selectedNode?.Template != null)
                    OnNodeSelected(_selectedNode);
            }
        }
    }

    private string _saveStatus = string.Empty;
    public string SaveStatus
    {
        get => _saveStatus;
        set => this.RaiseAndSetIfChanged(ref _saveStatus, value);
    }

    private TreeNodeViewModel? _selectedNode;
    public TreeNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode != value)
            {
                // Flush edits BEFORE changing _selectedNode so the key is correct
                FlushCurrentEdits();
                this.RaiseAndSetIfChanged(ref _selectedNode, value);
                OnNodeSelected(value);
            }
        }
    }

    private System.Collections.Generic.Dictionary<string, object?>? _vanillaProperties;
    public System.Collections.Generic.Dictionary<string, object?>? VanillaProperties
    {
        get => _vanillaProperties;
        set => this.RaiseAndSetIfChanged(ref _vanillaProperties, value);
    }

    private System.Collections.Generic.Dictionary<string, object?>? _modifiedProperties;
    public System.Collections.Generic.Dictionary<string, object?>? ModifiedProperties
    {
        get => _modifiedProperties;
        set => this.RaiseAndSetIfChanged(ref _modifiedProperties, value);
    }

    private void OnNodeSelected(TreeNodeViewModel? node)
    {
        if (node?.Template == null)
        {
            VanillaProperties = null;
            ModifiedProperties = null;
            return;
        }

        // Convert template to dictionary of properties
        var properties = ConvertTemplateToProperties(node.Template);

        // Build modified properties: vanilla → staging overrides → pending changes
        var modified = new Dictionary<string, object?>(properties);
        var key = GetTemplateKey(node.Template);
        if (key != null)
        {
            if (_stagingOverrides.TryGetValue(key, out var stagingDiffs))
            {
                foreach (var kvp in stagingDiffs)
                    if (modified.ContainsKey(kvp.Key))
                        modified[kvp.Key] = kvp.Value;
            }
            if (_pendingChanges.TryGetValue(key, out var pendingDiffs))
            {
                foreach (var kvp in pendingDiffs)
                    if (modified.ContainsKey(kvp.Key))
                        modified[kvp.Key] = kvp.Value;
            }
        }

        // Suppress TextChanged events during initial render of the property panels
        // (TextBoxes fire TextChanged when created with their initial text, which
        // would convert all values to strings and create false diffs)
        _suppressPropertyUpdates = true;
        VanillaProperties = properties;
        ModifiedProperties = modified;
        _suppressPropertyUpdates = false;
    }

    private void LoadStagingOverrides()
    {
        _stagingOverrides.Clear();
        _cloneDefinitions.Clear();

        if (string.IsNullOrEmpty(_currentModpackName))
            return;

        // Load clone definitions
        var clones = _modpackManager.LoadStagingClones(_currentModpackName);
        foreach (var (templateType, cloneMap) in clones)
        {
            foreach (var (newName, sourceName) in cloneMap)
            {
                var compositeKey = $"{templateType}/{newName}";
                _cloneDefinitions[compositeKey] = sourceName;
            }
        }

        // Insert cloned templates into the tree
        LoadCloneTemplatesIntoTree();

        var statsDir = Path.Combine(_modpackManager.ResolveStagingDir(_currentModpackName), "stats");
        if (!Directory.Exists(statsDir))
            return;

        foreach (var file in Directory.GetFiles(statsDir, "*.json"))
        {
            var templateType = Path.GetFileNameWithoutExtension(file);
            try
            {
                var json = File.ReadAllText(file);
                using var doc = JsonDocument.Parse(json);

                // Each file is { "instanceName": { "field": value, ... }, ... }
                foreach (var instanceProp in doc.RootElement.EnumerateObject())
                {
                    var compositeKey = $"{templateType}/{instanceProp.Name}";
                    var diffs = new Dictionary<string, object?>();

                    if (instanceProp.Value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var fieldProp in instanceProp.Value.EnumerateObject())
                        {
                            var val = ConvertJsonElementToValue(fieldProp.Value);

                            // Skip empty-string values — these are corrupted booleans
                            // (or other fields) from a previous save bug.
                            if (val is string s && s.Length == 0)
                                continue;

                            diffs[fieldProp.Name] = val;
                        }
                    }

                    if (diffs.Count > 0)
                        _stagingOverrides[compositeKey] = diffs;
                }
            }
            catch (Exception ex)
            {
                ModkitLog.Warn($"Failed to load staging overrides from {file}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// For each clone definition, create a virtual DynamicDataTemplate from the source's JSON
    /// and insert it into the tree so it appears alongside real templates.
    /// </summary>
    private void LoadCloneTemplatesIntoTree()
    {
        if (_dataLoader == null || _cloneDefinitions.Count == 0)
            return;

        foreach (var (compositeKey, sourceName) in _cloneDefinitions)
        {
            var slash = compositeKey.IndexOf('/');
            if (slash < 0) continue;
            var templateType = compositeKey[..slash];
            var newName = compositeKey[(slash + 1)..];

            // Skip if already in tree
            if (FindNode(_allTreeNodes, templateType, newName) != null)
                continue;

            // Find the source template to copy its JSON
            var sourceNode = FindNode(_allTreeNodes, templateType, sourceName);
            if (sourceNode?.Template is not DynamicDataTemplate sourceDyn)
                continue;

            // Create clone template from source JSON with new name
            var sourceJson = sourceDyn.GetJsonElement();
            var jsonString = sourceJson.GetRawText();

            using var doc = System.Text.Json.JsonDocument.Parse(jsonString);
            var writer = new MemoryStream();
            using (var jsonWriter = new System.Text.Json.Utf8JsonWriter(writer))
            {
                jsonWriter.WriteStartObject();
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Name == "name")
                        jsonWriter.WriteString("name", newName);
                    else
                        prop.WriteTo(jsonWriter);
                }
                jsonWriter.WriteEndObject();
            }

            var newJsonString = Encoding.UTF8.GetString(writer.ToArray());
            using var newDoc = System.Text.Json.JsonDocument.Parse(newJsonString);
            var cloneTemplate = new DynamicDataTemplate(newName, newDoc.RootElement.Clone(), templateType);

            var cloneLeaf = new TreeNodeViewModel
            {
                Name = FormatNodeName(newName.Split('.').Last()),
                IsCategory = false,
                Template = cloneTemplate
            };

            // Insert near source in the tree
            if (InsertCloneInTree(_allTreeNodes, sourceNode, cloneLeaf))
            {
                BuildSearchIndex(new[] { cloneLeaf });
            }
        }

        // Refresh the displayed nodes
        ApplySearchFilter();
    }

    private void FlushCurrentEdits()
    {
        if (_selectedNode?.Template == null || _vanillaProperties == null || _modifiedProperties == null)
            return;

        var key = GetTemplateKey(_selectedNode.Template);
        if (key == null)
            return;

        // Only check fields the user explicitly edited in this session.
        // This prevents false diffs from type mismatches, TextChanged events
        // during render, or stale staging values after data re-extraction.
        var diffs = new Dictionary<string, object?>();
        foreach (var fieldName in _userEditedFields)
        {
            if (!_modifiedProperties.TryGetValue(fieldName, out var modVal))
                continue;
            if (!_vanillaProperties.TryGetValue(fieldName, out var vanillaVal))
                continue;

            if (!ValuesEqual(vanillaVal, modVal))
                diffs[fieldName] = modVal;
        }

        if (diffs.Count > 0)
            _pendingChanges[key] = diffs;
        else
            _pendingChanges.Remove(key);

        _userEditedFields.Clear();
    }

    private static bool ValuesEqual(object? vanilla, object? modified)
    {
        if (vanilla == null && modified == null) return true;
        if (vanilla == null || modified == null) return false;

        // If modified is a string (from TextBox), compare against typed vanilla
        if (modified is string modStr)
        {
            if (vanilla is string vanStr)
                return vanStr == modStr;
            if (vanilla is long l)
                return long.TryParse(modStr, out var parsed) && parsed == l;
            if (vanilla is double d)
                return double.TryParse(modStr, out var parsed) && parsed == d;
            if (vanilla is bool b)
                return bool.TryParse(modStr, out var parsed) && parsed == b;

            // Array comparison: vanilla JsonElement array vs modified text
            if (vanilla is JsonElement vanJe && vanJe.ValueKind == JsonValueKind.Array)
                return vanJe.GetRawText() == modStr;

            return vanilla.ToString() == modStr;
        }

        // Cross-type numeric comparison (long vs double from JSON int/float differences)
        if (vanilla is long vl && modified is double md)
            return (double)vl == md;
        if (vanilla is double vd && modified is long ml)
            return vd == (double)ml;

        // JsonElement comparison by raw text
        if (vanilla is JsonElement vanEl && modified is JsonElement modEl)
            return vanEl.GetRawText() == modEl.GetRawText();

        // AssetPropertyValue comparison by asset name
        if (vanilla is AssetPropertyValue vanAsset && modified is AssetPropertyValue modAsset)
            return vanAsset.AssetName == modAsset.AssetName;

        return vanilla.Equals(modified);
    }

    private static string? GetTemplateKey(DataTemplate template)
    {
        if (template is DynamicDataTemplate dyn && !string.IsNullOrEmpty(dyn.TemplateTypeName))
            return $"{dyn.TemplateTypeName}/{template.Name}";
        return null;
    }

    /// <summary>
    /// Navigate to a specific template instance. Sets the modpack, then finds and
    /// selects the matching tree node.
    /// </summary>
    public void NavigateToEntry(string modpackName, string templateType, string instanceName)
    {
        // Set modpack if needed
        if (_currentModpackName != modpackName)
            CurrentModpackName = modpackName;

        // Search all tree nodes for a matching leaf
        var target = FindNode(_allTreeNodes, templateType, instanceName);
        if (target != null)
            SelectedNode = target;
    }

    private TreeNodeViewModel? FindNode(IEnumerable<TreeNodeViewModel> nodes, string templateType, string instanceName)
    {
        foreach (var node in nodes)
        {
            if (!node.IsCategory && node.Template is DynamicDataTemplate dyn
                && dyn.TemplateTypeName == templateType
                && node.Template.Name == instanceName)
                return node;

            var found = FindNode(node.Children, templateType, instanceName);
            if (found != null)
                return found;
        }
        return null;
    }

    public void UpdateModifiedBoolProperty(string fieldName, bool value)
    {
        if (_suppressPropertyUpdates)
            return;
        if (_modifiedProperties == null || !_modifiedProperties.ContainsKey(fieldName))
            return;

        _userEditedFields.Add(fieldName);
        _modifiedProperties[fieldName] = value;
    }

    public void UpdateModifiedProperty(string fieldName, string text)
    {
        if (_suppressPropertyUpdates)
            return;
        if (_modifiedProperties == null || !_modifiedProperties.ContainsKey(fieldName))
            return;

        // Boolean fields must only be updated via UpdateModifiedBoolProperty (from CheckBox).
        // Reject string overwrites — these come from spurious TextChanged events during render
        // and would corrupt the bool to an empty string.
        if (_modifiedProperties[fieldName] is bool)
            return;

        _userEditedFields.Add(fieldName);

        // If the vanilla value is an array, try to parse the edited text back as JSON array
        if (_vanillaProperties != null
            && _vanillaProperties.TryGetValue(fieldName, out var vanillaVal)
            && vanillaVal is JsonElement vanJe
            && vanJe.ValueKind == JsonValueKind.Array)
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    // Store as a JsonElement so it round-trips correctly
                    _modifiedProperties[fieldName] = doc.RootElement.Clone();
                    return;
                }
            }
            catch
            {
                // Not valid JSON — store as raw string (will be caught at save time)
            }
        }

        _modifiedProperties[fieldName] = text;
    }

    /// <summary>
    /// Returns the element type name if the field is a template reference collection
    /// for the currently selected template, null otherwise.
    /// </summary>
    public string? GetTemplateRefElementType(string fieldName)
    {
        if (_selectedNode?.Template is not DynamicDataTemplate dyn)
            return null;
        var templateTypeName = dyn.TemplateTypeName ?? "";
        if (!_schemaService.IsLoaded || string.IsNullOrEmpty(templateTypeName))
            return null;
        if (!_schemaService.IsTemplateRefCollection(templateTypeName, fieldName))
            return null;
        var meta = _schemaService.GetFieldMetadata(templateTypeName, fieldName);
        if (meta == null || string.IsNullOrEmpty(meta.ElementType))
            return null;
        return meta.ElementType;
    }

    /// <summary>
    /// Returns all instance names for a given template type, sorted. Cached per session.
    /// </summary>
    public List<string> GetTemplateInstanceNames(string templateTypeName)
    {
        if (_templateInstanceNamesCache.TryGetValue(templateTypeName, out var cached))
            return cached;

        var names = new List<string>();
        if (_dataLoader != null)
        {
            var templates = _dataLoader.LoadTemplatesGeneric(templateTypeName);
            foreach (var t in templates)
            {
                if (!string.IsNullOrEmpty(t.Name))
                    names.Add(t.Name);
            }
        }

        // Include cloned template names from the current modpack
        var prefix = templateTypeName + "/";
        foreach (var compositeKey in _cloneDefinitions.Keys)
        {
            if (compositeKey.StartsWith(prefix, StringComparison.Ordinal))
            {
                var cloneName = compositeKey[prefix.Length..];
                if (!names.Contains(cloneName))
                    names.Add(cloneName);
            }
        }

        names.Sort(StringComparer.Ordinal);
        _templateInstanceNamesCache[templateTypeName] = names;
        return names;
    }

    /// <summary>
    /// Replaces a collection field's value with a new list of strings.
    /// Stores as a JsonElement array so it integrates with the existing save pipeline.
    /// </summary>
    public void UpdateCollectionProperty(string fieldName, List<string> items)
    {
        if (_modifiedProperties == null || !_modifiedProperties.ContainsKey(fieldName))
            return;

        _userEditedFields.Add(fieldName);

        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"');
            sb.Append(items[i].Replace("\\", "\\\\").Replace("\"", "\\\""));
            sb.Append('"');
        }
        sb.Append(']');

        using var doc = JsonDocument.Parse(sb.ToString());
        _modifiedProperties[fieldName] = doc.RootElement.Clone();
    }

    /// <summary>
    /// Replaces a complex array field's value with new JSON text.
    /// Used by the structured object array editor for full-array replacement on any sub-field edit.
    /// </summary>
    public void UpdateComplexArrayProperty(string fieldName, string jsonText)
    {
        if (_suppressPropertyUpdates)
            return;
        if (_modifiedProperties == null || !_modifiedProperties.ContainsKey(fieldName))
            return;

        _userEditedFields.Add(fieldName);
        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            _modifiedProperties[fieldName] = doc.RootElement.Clone();
        }
        catch
        {
            // Invalid JSON — ignore the update
        }
    }

    public void SaveToStaging()
    {
        if (string.IsNullOrEmpty(_currentModpackName))
        {
            SaveStatus = "No modpack selected";
            return;
        }

        // Flush whatever's on screen right now
        FlushCurrentEdits();

        // Merge staging overrides + pending changes, grouped by template type
        var byType = new Dictionary<string, Dictionary<string, Dictionary<string, object?>>>();

        void AddToByType(Dictionary<string, Dictionary<string, object?>> source)
        {
            foreach (var kvp in source)
            {
                // key = "TemplateType/instanceName"
                var slash = kvp.Key.IndexOf('/');
                if (slash < 0) continue;
                var templateType = kvp.Key[..slash];
                var instanceName = kvp.Key[(slash + 1)..];

                if (!byType.TryGetValue(templateType, out var instances))
                {
                    instances = new Dictionary<string, Dictionary<string, object?>>();
                    byType[templateType] = instances;
                }

                if (!instances.TryGetValue(instanceName, out var fields))
                {
                    fields = new Dictionary<string, object?>();
                    instances[instanceName] = fields;
                }

                // Pending changes overwrite staging overrides for same field
                foreach (var field in kvp.Value)
                    fields[field.Key] = field.Value;
            }
        }

        AddToByType(_stagingOverrides);
        AddToByType(_pendingChanges);

        if (byType.Count == 0)
        {
            SaveStatus = "No changes to save";
            return;
        }

        // Serialize and write each template type
        int fileCount = 0;
        foreach (var typeKvp in byType)
        {
            var root = new JsonObject();
            foreach (var instanceKvp in typeKvp.Value)
            {
                var instanceObj = new JsonObject();
                foreach (var fieldKvp in instanceKvp.Value)
                {
                    var node = ConvertToJsonNode(fieldKvp.Value, typeKvp.Key, instanceKvp.Key, fieldKvp.Key);
                    if (node != null)
                        instanceObj[fieldKvp.Key] = node;
                }
                root[instanceKvp.Key] = instanceObj;
            }

            var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            _modpackManager.SaveStagingTemplate(_currentModpackName, typeKvp.Key, json);
            fileCount++;
        }

        // Move pending into staging overrides, clear pending
        foreach (var kvp in _pendingChanges)
        {
            if (!_stagingOverrides.TryGetValue(kvp.Key, out var existing))
            {
                existing = new Dictionary<string, object?>();
                _stagingOverrides[kvp.Key] = existing;
            }
            foreach (var field in kvp.Value)
                existing[field.Key] = field.Value;
        }
        _pendingChanges.Clear();

        // Persist clone definitions
        SaveCloneDefinitions();

        SaveStatus = $"Saved {fileCount} template file(s) to '{_currentModpackName}'";
    }

    private void SaveCloneDefinitions()
    {
        if (string.IsNullOrEmpty(_currentModpackName))
            return;

        // Group clone definitions by template type
        var byType = new Dictionary<string, Dictionary<string, string>>();
        foreach (var (compositeKey, sourceName) in _cloneDefinitions)
        {
            var slash = compositeKey.IndexOf('/');
            if (slash < 0) continue;
            var templateType = compositeKey[..slash];
            var newName = compositeKey[(slash + 1)..];

            if (!byType.TryGetValue(templateType, out var dict))
            {
                dict = new Dictionary<string, string>();
                byType[templateType] = dict;
            }
            dict[newName] = sourceName;
        }

        foreach (var (templateType, clones) in byType)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(clones,
                new JsonSerializerOptions { WriteIndented = true });
            _modpackManager.SaveStagingClones(_currentModpackName, templateType, json);
        }
    }

    /// <summary>
    /// Clone the currently selected template with a new name.
    /// Creates a virtual DynamicDataTemplate from the source's JSON data,
    /// adds it to the tree, and registers it as a clone.
    /// </summary>
    public bool CloneTemplate(string newName)
    {
        if (_selectedNode?.Template is not DynamicDataTemplate sourceDyn)
            return false;

        var templateTypeName = sourceDyn.TemplateTypeName;
        if (string.IsNullOrEmpty(templateTypeName))
            return false;

        var compositeKey = $"{templateTypeName}/{newName}";

        // Check if this name already exists
        if (_cloneDefinitions.ContainsKey(compositeKey))
            return false;

        // Check existing templates
        var existingNode = FindNode(_allTreeNodes, templateTypeName, newName);
        if (existingNode != null)
            return false;

        // Create a new DynamicDataTemplate from the source's JSON, but with the new name
        var sourceJson = sourceDyn.GetJsonElement();
        var jsonString = sourceJson.GetRawText();

        // Replace the "name" field in the JSON with the new name
        using var doc = System.Text.Json.JsonDocument.Parse(jsonString);
        var writer = new System.IO.MemoryStream();
        using (var jsonWriter = new System.Text.Json.Utf8JsonWriter(writer))
        {
            jsonWriter.WriteStartObject();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == "name")
                    jsonWriter.WriteString("name", newName);
                else
                    prop.WriteTo(jsonWriter);
            }
            jsonWriter.WriteEndObject();
        }

        var newJsonString = System.Text.Encoding.UTF8.GetString(writer.ToArray());
        using var newDoc = System.Text.Json.JsonDocument.Parse(newJsonString);
        var newTemplate = new DynamicDataTemplate(newName, newDoc.RootElement.Clone(), templateTypeName);

        // Register clone definition
        _cloneDefinitions[compositeKey] = sourceDyn.Name;

        // Invalidate the cached template names so autocomplete lists pick up the new clone
        _templateInstanceNamesCache.Remove(templateTypeName);

        // Add to tree: find the parent category of the source and add the clone as a sibling
        var cloneLeaf = new TreeNodeViewModel
        {
            Name = FormatNodeName(newName.Split('.').Last()),
            IsCategory = false,
            Template = newTemplate
        };

        // Find the source node's parent in the tree and add the clone there
        if (InsertCloneInTree(_allTreeNodes, _selectedNode, cloneLeaf))
        {
            // Rebuild search index for new node
            _searchEntries.Remove(cloneLeaf); // clear if stale
            BuildSearchIndex(new[] { cloneLeaf });
        }

        // Refresh the filtered view
        ApplySearchFilter();

        // Select the new clone
        var found = FindNode(TreeNodes.ToList(), templateTypeName, newName);
        if (found != null)
            SelectedNode = found;

        return true;
    }

    /// <summary>
    /// Whether the given composite key represents a clone (vs a vanilla template).
    /// </summary>
    public bool IsClone(string compositeKey)
    {
        return _cloneDefinitions.ContainsKey(compositeKey);
    }

    private bool InsertCloneInTree(IEnumerable<TreeNodeViewModel> nodes, TreeNodeViewModel sourceNode, TreeNodeViewModel cloneNode)
    {
        foreach (var node in nodes)
        {
            if (!node.IsCategory)
                continue;

            // Check if the source node is a direct child
            var idx = node.Children.IndexOf(sourceNode);
            if (idx >= 0)
            {
                node.Children.Insert(idx + 1, cloneNode);
                return true;
            }

            // Recurse into children
            if (InsertCloneInTree(node.Children, sourceNode, cloneNode))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Convert a property value to a JsonNode, using the vanilla type as guide for type conversion.
    /// </summary>
    private JsonNode? ConvertToJsonNode(object? value, string templateType, string instanceName, string fieldName)
    {
        if (value == null)
            return null;

        // If the value is a string from a TextBox, try to convert back to the vanilla type
        if (value is string str)
        {
            // Look up the vanilla value to determine target type
            var vanillaKey = $"{templateType}/{instanceName}";
            object? vanillaValue = null;

            // Try to get the vanilla type from current vanilla properties (fast path)
            if (_vanillaProperties != null && _selectedNode?.Template != null
                && GetTemplateKey(_selectedNode.Template) == vanillaKey)
            {
                _vanillaProperties.TryGetValue(fieldName, out vanillaValue);
            }

            // Fallback: look up the template's original JSON to determine the field type.
            // This handles clones and any template that isn't currently selected.
            if (vanillaValue == null)
            {
                var node = FindNode(_allTreeNodes, templateType, instanceName);
                if (node?.Template is DynamicDataTemplate dyn)
                {
                    var json = dyn.GetJsonElement();
                    var dotIndex = fieldName.IndexOf('.');
                    if (dotIndex >= 0)
                    {
                        // Nested field: "AIRole.Move" → json["AIRole"]["Move"]
                        var parentKey = fieldName[..dotIndex];
                        var childKey = fieldName[(dotIndex + 1)..];
                        if (json.TryGetProperty(parentKey, out var parent)
                            && parent.ValueKind == JsonValueKind.Object
                            && parent.TryGetProperty(childKey, out var child))
                        {
                            vanillaValue = ConvertJsonElementToValue(child);
                        }
                    }
                    else if (json.TryGetProperty(fieldName, out var fieldEl))
                    {
                        vanillaValue = ConvertJsonElementToValue(fieldEl);
                    }
                }
            }

            if (vanillaValue is long)
            {
                if (long.TryParse(str, out var l))
                    return JsonValue.Create(l);
            }
            else if (vanillaValue is double)
            {
                if (double.TryParse(str, out var d))
                    return JsonValue.Create(d);
            }
            else if (vanillaValue is bool)
            {
                if (bool.TryParse(str, out var b))
                    return JsonValue.Create(b);
                // Empty/unparseable string for a boolean field — drop it
                return null;
            }

            // Drop empty strings that aren't genuinely string fields (likely corrupted)
            if (str.Length == 0 && vanillaValue != null && vanillaValue is not string)
                return null;

            // Default: keep as string
            return JsonValue.Create(str);
        }

        if (value is long lv) return JsonValue.Create(lv);
        if (value is double dv) return JsonValue.Create(dv);
        if (value is bool bv) return JsonValue.Create(bv);
        if (value is AssetPropertyValue asset) return JsonValue.Create(asset.AssetName ?? "");

        // Preserve JsonElement arrays/objects (from vanilla data or parsed array edits)
        if (value is JsonElement je && (je.ValueKind == JsonValueKind.Array || je.ValueKind == JsonValueKind.Object))
            return JsonNode.Parse(je.GetRawText());

        return JsonValue.Create(value.ToString());
    }

    private Dictionary<string, object?> ConvertTemplateToProperties(DataTemplate template)
    {
        var result = new System.Collections.Generic.Dictionary<string, object?>();

        // All templates should be DynamicDataTemplate instances
        if (template is DynamicDataTemplate dynamicTemplate)
        {
            var jsonElement = dynamicTemplate.GetJsonElement();
            var templateTypeName = dynamicTemplate.TemplateTypeName ?? "";
            int propCount = 0;
            try
            {
                foreach (var property in jsonElement.EnumerateObject())
                {
                    propCount++;

                    // Convert JsonElement to appropriate type
                    object? value = ConvertJsonElementToValue(property.Value);

                    // Flatten nested objects with dotted keys (one level deep).
                    // E.g., Properties.HitpointsPerElement, AIRole.Move, etc.
                    // This prevents collisions between nested sub-field names and
                    // top-level field names (e.g., AnimatorTemplate.name vs name).
                    if (property.Value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var subProp in property.Value.EnumerateObject())
                        {
                            var qualifiedKey = $"{property.Name}.{subProp.Name}";
                            result[qualifiedKey] = ConvertJsonElementToValue(subProp.Value);
                        }
                        continue;
                    }

                    // Check if this is a unity_asset field via schema
                    if (_schemaService.IsLoaded && _schemaService.IsAssetField(templateTypeName, property.Name))
                    {
                        var fieldMeta = _schemaService.GetFieldMetadata(templateTypeName, property.Name);
                        if (fieldMeta != null)
                        {
                            var assetValue = new AssetPropertyValue
                            {
                                FieldName = property.Name,
                                AssetType = fieldMeta.Type,
                                RawValue = value,
                            };

                            // Determine if the value is an actual asset name or a placeholder
                            if (value is string strVal && !string.IsNullOrEmpty(strVal))
                            {
                                if (!strVal.StartsWith("(") || !strVal.EndsWith(")"))
                                {
                                    // This looks like an actual asset name
                                    assetValue.AssetName = strVal;
                                    // Try to find the asset file in the AssetRipper output
                                    assetValue.AssetFilePath = ResolveAssetFilePath(fieldMeta.Type, strVal);
                                    assetValue.ThumbnailPath = assetValue.AssetFilePath;
                                }
                            }

                            result[property.Name] = assetValue;
                            continue;
                        }
                    }

                    // Check if this is an enum field — resolve int to name
                    if (_schemaService.IsLoaded)
                    {
                        var fieldMeta = _schemaService.GetFieldMetadata(templateTypeName, property.Name);
                        if (fieldMeta?.Category == "enum" && value is long enumIntVal)
                        {
                            var enumName = _schemaService.ResolveEnumName(fieldMeta.Type, (int)enumIntVal);
                            if (enumName != null)
                                value = $"{enumName} ({enumIntVal})";
                        }
                    }

                    // Try to resolve numeric asset references (legacy path)
                    if (property.Value.ValueKind == System.Text.Json.JsonValueKind.Number
                        && property.Value.TryGetInt64(out var longVal))
                    {
                        var resolved = _assetResolver.Resolve(longVal);
                        if (resolved.IsReference)
                        {
                            if (resolved.HasAssetFile)
                                value = $"{resolved.DisplayValue} → {resolved.AssetPath}";
                            else if (!string.IsNullOrEmpty(resolved.AssetName))
                                value = $"{resolved.DisplayValue} (no asset file)";
                            else
                                value = resolved.DisplayValue;
                        }
                    }

                    result[property.Name] = value;
                }
            }
            catch (Exception ex)
            {
                ModkitLog.Warn($"Error in ConvertTemplateToProperties: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// Try to find the actual asset file in the AssetRipper output directory.
    /// Searches under Assets/{AssetType}/ for files matching the asset name.
    /// </summary>
    private string? ResolveAssetFilePath(string assetType, string assetName)
    {
        if (_assetOutputPath == null || string.IsNullOrEmpty(assetName))
            return null;

        // Search in the expected type directory
        var typeDir = Path.Combine(_assetOutputPath, "Assets", assetType);
        if (Directory.Exists(typeDir))
        {
            var match = FindAssetFileByName(typeDir, assetName);
            if (match != null) return match;
        }

        // Also try common alternative paths
        string[] altPaths = { "Assets/Sprite", "Assets/Texture2D", "Assets/Resources" };
        foreach (var alt in altPaths)
        {
            var altDir = Path.Combine(_assetOutputPath, alt);
            if (Directory.Exists(altDir))
            {
                var match = FindAssetFileByName(altDir, assetName);
                if (match != null) return match;
            }
        }

        return null;
    }

    private static string? FindAssetFileByName(string directory, string assetName)
    {
        try
        {
            // Look for files matching the asset name (with any extension)
            foreach (var file in Directory.GetFiles(directory, $"{assetName}.*"))
            {
                return file;
            }
            // Also search subdirectories one level deep
            foreach (var subDir in Directory.GetDirectories(directory))
            {
                foreach (var file in Directory.GetFiles(subDir, $"{assetName}.*"))
                {
                    return file;
                }
            }
        }
        catch { }
        return null;
    }

    private object? ConvertJsonElementToValue(System.Text.Json.JsonElement element)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => element.GetString(),
            System.Text.Json.JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Null => null,
            System.Text.Json.JsonValueKind.Array => element, // Keep arrays as JsonElement for View to handle
            System.Text.Json.JsonValueKind.Object => element, // Keep objects as JsonElement for View to handle
            _ => element.ToString()
        };
    }

    // Master copy of all tree nodes (unfiltered)
    private List<TreeNodeViewModel> _allTreeNodes = new();

    // Tiered search index for ranked results
    private class SearchEntry
    {
        public string Name = "";        // node.Name + m_ID + templateType
        public string Title = "";       // DisplayTitle, DisplayShortName, Title, ShortName
        public string Fields = "";      // other string property values (excl description)
        public string Description = ""; // Description, DisplayDescription
    }
    private readonly Dictionary<TreeNodeViewModel, SearchEntry> _searchEntries = new();

    private bool _showModpackOnly;
    public bool ShowModpackOnly
    {
        get => _showModpackOnly;
        set
        {
            if (_showModpackOnly != value)
            {
                this.RaiseAndSetIfChanged(ref _showModpackOnly, value);
                ApplySearchFilter();
            }
        }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                this.RaiseAndSetIfChanged(ref _searchText, value);
                ApplySearchFilter();
            }
        }
    }

    private void ApplySearchFilter()
    {
        TreeNodes.Clear();

        var hasQuery = !string.IsNullOrWhiteSpace(_searchText);
        var query = hasQuery ? _searchText.Trim() : null;

        if (!hasQuery && !_showModpackOnly)
        {
            foreach (var node in _allTreeNodes)
                TreeNodes.Add(node);
            return;
        }

        var scores = new Dictionary<TreeNodeViewModel, int>();

        foreach (var node in _allTreeNodes)
        {
            var filtered = FilterNode(node, query, scores);
            if (filtered != null)
                TreeNodes.Add(filtered);
        }

        // Sort results by score when there's an active search query
        if (hasQuery)
        {
            // Propagate scores through the tree and sort children
            foreach (var node in TreeNodes)
                SortByScore(node, scores);

            // Sort root-level nodes by propagated score
            var sortedRoots = TreeNodes.OrderByDescending(n =>
                scores.TryGetValue(n, out var s) ? s : 0).ToList();
            TreeNodes.Clear();
            foreach (var n in sortedRoots)
                TreeNodes.Add(n);
        }

        // Auto-expand filtered results so matches are visible
        SetExpansionState(TreeNodes, true);
    }

    public void ExpandAll()
    {
        SetExpansionState(TreeNodes, true);
    }

    public void CollapseAll()
    {
        SetExpansionState(TreeNodes, false);
    }

    private static void SetExpansionState(IEnumerable<TreeNodeViewModel> nodes, bool expanded)
    {
        foreach (var node in nodes)
        {
            if (node.IsCategory)
            {
                node.IsExpanded = expanded;
                SetExpansionState(node.Children, expanded);
            }
        }
    }

    private TreeNodeViewModel? FilterNode(TreeNodeViewModel node, string? query, Dictionary<TreeNodeViewModel, int> scores)
    {
        // Leaf node
        if (!node.IsCategory)
        {
            // Modpack-only filter: exclude items without modpack changes
            if (_showModpackOnly)
            {
                var key = node.Template != null ? GetTemplateKey(node.Template) : null;
                if (key == null || (!_stagingOverrides.ContainsKey(key) && !_pendingChanges.ContainsKey(key)))
                    return null;
            }

            if (query == null)
                return node;

            var score = ScoreMatch(node, query);
            if (score < 0)
                return null;

            scores[node] = score;
            return node;
        }

        // Check children recursively
        var matchingChildren = new List<TreeNodeViewModel>();
        foreach (var child in node.Children)
        {
            var filtered = FilterNode(child, query, scores);
            if (filtered != null)
                matchingChildren.Add(filtered);
        }

        if (matchingChildren.Count == 0)
            return null;

        // If all children match, return original node to avoid unnecessary copies
        if (matchingChildren.Count == node.Children.Count)
            return node;

        var copy = new TreeNodeViewModel
        {
            Name = node.Name,
            IsCategory = true
        };
        foreach (var child in matchingChildren)
            copy.Children.Add(child);

        return copy;
    }

    private void LoadAllTemplates()
    {
        TreeNodes.Clear();
        _allTreeNodes.Clear();
        _searchEntries.Clear();

        if (_dataLoader == null) return;

        var placedInstances = new HashSet<string>(StringComparer.Ordinal);
        var rootDict = new Dictionary<string, TreeNodeViewModel>();

        // Get all template types, sorted by inheritance depth descending (most specific first)
        var templateTypes = _dataLoader.GetTemplateTypes()
            .Where(t => t != "AssetReferences" && t != "menu")
            .OrderByDescending(t => _schemaService.GetInheritanceDepth(t))
            .ToList();

        foreach (var templateType in templateTypes)
        {
            var templates = _dataLoader.LoadTemplatesGeneric(templateType);
            if (templates.Count == 0) continue;

            // Get filtered inheritance chain (exclude ScriptableObject, SerializedScriptableObject)
            var chain = _schemaService.GetInheritanceChain(templateType)
                .Where(c => c != "ScriptableObject" && c != "SerializedScriptableObject" && c != "DataTemplate")
                .ToList();

            foreach (var template in templates)
            {
                if (placedInstances.Contains(template.Name))
                    continue;
                placedInstances.Add(template.Name);

                // Set TemplateTypeName on DynamicDataTemplate
                if (template is DynamicDataTemplate dyn)
                    dyn.TemplateTypeName = templateType;

                var nameParts = template.Name.Split('.', StringSplitOptions.RemoveEmptyEntries);

                // path = inheritance chain + all name parts except last
                var pathParts = new List<string>(chain);
                for (int i = 0; i < nameParts.Length - 1; i++)
                    pathParts.Add(nameParts[i]);

                var leafName = nameParts.Length > 0 ? nameParts[^1] : template.Name;

                // Navigate/create tree nodes along the path
                var currentDict = rootDict;
                TreeNodeViewModel? parentNode = null;

                foreach (var part in pathParts)
                {
                    if (!currentDict.TryGetValue(part, out var node))
                    {
                        node = new TreeNodeViewModel
                        {
                            Name = FormatNodeName(part),
                            IsCategory = true,
                            ChildrenDict = new Dictionary<string, TreeNodeViewModel>()
                        };
                        currentDict[part] = node;

                        if (parentNode != null)
                            parentNode.Children.Add(node);
                    }

                    parentNode = node;
                    currentDict = node.ChildrenDict ??= new Dictionary<string, TreeNodeViewModel>();
                }

                // Place leaf
                var leaf = new TreeNodeViewModel
                {
                    Name = FormatNodeName(leafName),
                    IsCategory = false,
                    Template = template
                };

                if (parentNode != null)
                    parentNode.Children.Add(leaf);
                else
                    rootDict[leafName] = leaf;
            }
        }

        // Add root-level nodes to TreeNodes
        foreach (var node in rootDict.Values)
            TreeNodes.Add(node);

        // Build search index
        BuildSearchIndex(TreeNodes);

        // Store master copy for search filtering
        _allTreeNodes = TreeNodes.ToList();
    }

    private string FormatNodeName(string name)
    {
        // "pirate_laser_lance" -> "Pirate Laser Lance"
        return string.Join(" ", name.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Length > 0 ? char.ToUpper(w[0]) + w.Substring(1) : ""));
    }

    private static readonly HashSet<string> TitleFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Title", "ShortName", "DisplayTitle", "DisplayShortName"
    };

    private void BuildSearchIndex(IEnumerable<TreeNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsCategory)
            {
                BuildSearchIndex(node.Children);
            }
            else if (node.Template is DynamicDataTemplate dyn)
            {
                var entry = new SearchEntry();

                // Name tier: node display name + m_ID + templateType
                var nameSb = new StringBuilder();
                nameSb.Append(node.Name);
                nameSb.Append(' ');
                nameSb.Append(dyn.Name);
                nameSb.Append(' ');
                nameSb.Append(dyn.TemplateTypeName);
                entry.Name = nameSb.ToString();

                // Title tier from known properties on the model
                var titleSb = new StringBuilder();
                if (!string.IsNullOrEmpty(dyn.DisplayTitle)) { titleSb.Append(dyn.DisplayTitle); titleSb.Append(' '); }
                if (!string.IsNullOrEmpty(dyn.DisplayShortName)) { titleSb.Append(dyn.DisplayShortName); titleSb.Append(' '); }

                var fieldsSb = new StringBuilder();
                var descSb = new StringBuilder();

                try
                {
                    var json = dyn.GetJsonElement();
                    if (json.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in json.EnumerateObject())
                        {
                            if (prop.Value.ValueKind != JsonValueKind.String)
                                continue;

                            var sv = prop.Value.GetString();
                            if (string.IsNullOrEmpty(sv))
                                continue;

                            if (prop.Name.Contains("Description", StringComparison.OrdinalIgnoreCase))
                            {
                                descSb.Append(sv);
                                descSb.Append(' ');
                            }
                            else if (TitleFieldNames.Contains(prop.Name))
                            {
                                titleSb.Append(sv);
                                titleSb.Append(' ');
                            }
                            else
                            {
                                fieldsSb.Append(sv);
                                fieldsSb.Append(' ');
                            }
                        }
                    }
                }
                catch { }

                entry.Title = titleSb.ToString();
                entry.Fields = fieldsSb.ToString();
                entry.Description = descSb.ToString();

                _searchEntries[node] = entry;
            }
        }
    }

    private int ScoreMatch(TreeNodeViewModel node, string query)
    {
        if (!_searchEntries.TryGetValue(node, out var entry)) return -1;
        if (entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) return 100;
        if (entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)) return 80;
        if (entry.Fields.Contains(query, StringComparison.OrdinalIgnoreCase)) return 40;
        if (entry.Description.Contains(query, StringComparison.OrdinalIgnoreCase)) return 20;
        return -1;
    }

    private int SortByScore(TreeNodeViewModel node, Dictionary<TreeNodeViewModel, int> scores)
    {
        if (!node.IsCategory)
            return scores.TryGetValue(node, out var s) ? s : 0;

        int maxChild = 0;
        foreach (var child in node.Children)
        {
            var childScore = SortByScore(child, scores);
            if (childScore > maxChild) maxChild = childScore;
        }

        var sorted = node.Children.OrderByDescending(c =>
            scores.TryGetValue(c, out var s) ? s : 0).ToList();
        node.Children.Clear();
        foreach (var c in sorted) node.Children.Add(c);

        scores[node] = maxChild;
        return maxChild;
    }

    private string _setupStatus = string.Empty;
    public string SetupStatus
    {
        get => _setupStatus;
        set => this.RaiseAndSetIfChanged(ref _setupStatus, value);
    }

    public async Task AutoSetupAsync()
    {
        var gameInstallPath = AppSettings.Instance.GameInstallPath;

        if (string.IsNullOrWhiteSpace(gameInstallPath) || !Directory.Exists(gameInstallPath))
        {
            SetupStatus = "❌ Please set a valid game installation path in Settings first";
            return;
        }

        var installer = new ModLoaderInstaller(gameInstallPath);

        // Check what's already installed
        var melonLoaderInstalled = installer.IsMelonLoaderInstalled();
        var dataExtractorInstalled = installer.IsDataExtractorInstalled();

        SetupStatus = "Starting auto setup...";

        // Install MelonLoader if needed
        if (!melonLoaderInstalled)
        {
            var success = await installer.InstallMelonLoaderAsync((status) => SetupStatus = status);
            if (!success)
            {
                return;
            }
        }
        else
        {
            SetupStatus = "✓ MelonLoader already installed";
        }

        // Install DataExtractor if needed
        if (!dataExtractorInstalled)
        {
            var success = await installer.InstallDataExtractorAsync((status) => SetupStatus = status);
            if (!success)
            {
                return;
            }
        }
        else
        {
            SetupStatus = "✓ DataExtractor already installed";
        }

        // Offer to launch game
        SetupStatus = "Setup complete! Launch the game once to extract template data, then reload this tab.";

        // Optionally auto-launch the game
        // await installer.LaunchGameAsync((status) => SetupStatus = status);
    }

    public async Task LaunchGameToUpdateDataAsync()
    {
        var gameInstallPath = AppSettings.Instance.GameInstallPath;

        if (string.IsNullOrWhiteSpace(gameInstallPath) || !Directory.Exists(gameInstallPath))
        {
            SetupStatus = "❌ Please set a valid game installation path in Settings first";
            return;
        }

        var installer = new ModLoaderInstaller(gameInstallPath);

        SetupStatus = "Launching game to update template data...";
        SetupStatus = "The game will extract updated templates. Close the game when you reach the main menu.";

        var success = await installer.LaunchGameAsync((status) => SetupStatus = status);

        if (success)
        {
            SetupStatus = "Game launched! Close it when ready, then click Refresh to load updated data.";
        }
    }

}

public sealed class TreeNodeViewModel : ViewModelBase
{
    public string Name { get; set; } = string.Empty;
    public bool IsCategory { get; set; }
    public DataTemplate? Template { get; set; }
    public ObservableCollection<TreeNodeViewModel> Children { get; } = new();

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    // Helper for hierarchy building
    public Dictionary<string, TreeNodeViewModel>? ChildrenDict { get; set; }
}

public sealed class TemplateItemViewModel : ViewModelBase
{
    private readonly DataTemplate _template;

    public TemplateItemViewModel(DataTemplate template)
    {
        _template = template;
    }

    public string Name => _template.Name;
    public string DisplayName => _template.GetDisplayName();
    public string? Description => _template.DisplayDescription;
    public bool HasIcon => _template.HasIcon;

    // Expose the underlying template for property editing
    public DataTemplate Template => _template;
}
