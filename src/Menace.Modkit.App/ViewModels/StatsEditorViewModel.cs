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

    public StatsEditorViewModel()
    {
        _modpackManager = new ModpackManager();
        _assetResolver = new AssetReferenceResolver();
        _schemaService = new SchemaService();
        TreeNodes = new ObservableCollection<TreeNodeViewModel>();
        AvailableModpacks = new ObservableCollection<string>();

        LoadData();
    }

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
        Console.WriteLine($"[DEBUG] OnNodeSelected: node={node?.Name}, IsCategory={node?.IsCategory}, HasTemplate={node?.Template != null}");

        if (node?.Template == null)
        {
            Console.WriteLine($"[DEBUG] OnNodeSelected: No template, clearing properties");
            VanillaProperties = null;
            ModifiedProperties = null;
            return;
        }

        // Convert template to dictionary of properties
        var properties = ConvertTemplateToProperties(node.Template);
        VanillaProperties = properties;

        Console.WriteLine($"[DEBUG] OnNodeSelected: Set VanillaProperties with {properties.Count} items");
        if (properties.Count > 0)
        {
            Console.WriteLine($"[DEBUG] First 3 properties: {string.Join(", ", properties.Take(3).Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
        }

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

        ModifiedProperties = modified;
    }

    private void LoadStagingOverrides()
    {
        _stagingOverrides.Clear();

        if (string.IsNullOrEmpty(_currentModpackName))
            return;

        var statsDir = Path.Combine(_modpackManager.StagingBasePath, _currentModpackName, "stats");
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
                            diffs[fieldProp.Name] = ConvertJsonElementToValue(fieldProp.Value);
                        }
                    }

                    if (diffs.Count > 0)
                        _stagingOverrides[compositeKey] = diffs;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Failed to load staging overrides from {file}: {ex.Message}");
            }
        }

        Console.WriteLine($"[DEBUG] Loaded {_stagingOverrides.Count} staging override entries for modpack '{_currentModpackName}'");
    }

    private void FlushCurrentEdits()
    {
        if (_selectedNode?.Template == null || _vanillaProperties == null || _modifiedProperties == null)
            return;

        var key = GetTemplateKey(_selectedNode.Template);
        if (key == null)
            return;

        var diffs = new Dictionary<string, object?>();
        foreach (var kvp in _modifiedProperties)
        {
            if (!_vanillaProperties.TryGetValue(kvp.Key, out var vanillaVal))
                continue;

            if (!ValuesEqual(vanillaVal, kvp.Value))
                diffs[kvp.Key] = kvp.Value;
        }

        if (diffs.Count > 0)
            _pendingChanges[key] = diffs;
        else
            _pendingChanges.Remove(key);
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

            return vanilla.ToString() == modStr;
        }

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

    public void UpdateModifiedProperty(string fieldName, string text)
    {
        if (_modifiedProperties != null && _modifiedProperties.ContainsKey(fieldName))
        {
            _modifiedProperties[fieldName] = text;
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
                    instanceObj[fieldKvp.Key] = ConvertToJsonNode(fieldKvp.Value, typeKvp.Key, instanceKvp.Key, fieldKvp.Key);
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

        SaveStatus = $"Saved {fileCount} template file(s) to '{_currentModpackName}'";
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

            // Try to get the vanilla type from current vanilla properties or by reloading
            if (_vanillaProperties != null && _selectedNode?.Template != null
                && GetTemplateKey(_selectedNode.Template) == vanillaKey)
            {
                _vanillaProperties.TryGetValue(fieldName, out vanillaValue);
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
            }

            // Default: keep as string
            return JsonValue.Create(str);
        }

        if (value is long lv) return JsonValue.Create(lv);
        if (value is double dv) return JsonValue.Create(dv);
        if (value is bool bv) return JsonValue.Create(bv);
        if (value is AssetPropertyValue asset) return JsonValue.Create(asset.AssetName ?? "");

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

            Console.WriteLine($"[DEBUG] ConvertTemplateToProperties: template={template.Name}, type={templateTypeName}, jsonElement.ValueKind={jsonElement.ValueKind}");

            int propCount = 0;
            try
            {
                foreach (var property in jsonElement.EnumerateObject())
                {
                    propCount++;

                    // Convert JsonElement to appropriate type
                    object? value = ConvertJsonElementToValue(property.Value);

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

                    // Try to resolve numeric asset references (legacy path)
                    if (property.Value.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        var resolved = _assetResolver.Resolve(property.Value.GetInt64());
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
                Console.WriteLine($"[DEBUG] ERROR in ConvertTemplateToProperties: {ex.Message}");
                Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine($"[DEBUG] ConvertTemplateToProperties: Found {propCount} properties, result.Count={result.Count}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"ConvertTemplateToProperties: template is not DynamicDataTemplate, it's {template?.GetType().Name}");
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
    // Search index: leaf node → concatenated searchable text
    private readonly Dictionary<TreeNodeViewModel, string> _searchIndex = new();

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

        if (string.IsNullOrWhiteSpace(_searchText))
        {
            foreach (var node in _allTreeNodes)
                TreeNodes.Add(node);
            return;
        }

        var query = _searchText.Trim();
        foreach (var node in _allTreeNodes)
        {
            var filtered = FilterNode(node, query);
            if (filtered != null)
                TreeNodes.Add(filtered);
        }

        // Auto-expand search results so matches are visible
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

    private TreeNodeViewModel? FilterNode(TreeNodeViewModel node, string query)
    {
        // Leaf node: check search index
        if (!node.IsCategory)
        {
            if (_searchIndex.TryGetValue(node, out var indexText))
                return indexText.Contains(query, StringComparison.OrdinalIgnoreCase) ? node : null;
            return node.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ? node : null;
        }

        // Category name matches → include entire subtree
        if (node.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            return node;

        // Otherwise check children recursively
        var matchingChildren = new List<TreeNodeViewModel>();
        foreach (var child in node.Children)
        {
            var filtered = FilterNode(child, query);
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
        _searchIndex.Clear();

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
                .Where(c => c != "ScriptableObject" && c != "SerializedScriptableObject")
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
                var sb = new StringBuilder();
                sb.Append(node.Name);
                sb.Append(' ');
                sb.Append(dyn.Name);
                sb.Append(' ');

                try
                {
                    var json = dyn.GetJsonElement();
                    if (json.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in json.EnumerateObject())
                        {
                            sb.Append(prop.Name);
                            sb.Append(' ');
                            if (prop.Value.ValueKind == JsonValueKind.String)
                            {
                                sb.Append(prop.Value.GetString());
                                sb.Append(' ');
                            }
                        }
                    }
                }
                catch { }

                _searchIndex[node] = sb.ToString();
            }
        }
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
