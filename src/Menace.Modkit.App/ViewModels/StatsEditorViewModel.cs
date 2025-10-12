using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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

    public StatsEditorViewModel()
    {
        _modpackManager = new ModpackManager();
        _assetResolver = new AssetReferenceResolver();
        TreeNodes = new ObservableCollection<TreeNodeViewModel>();

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

        LoadAllTemplates();
    }

    public ObservableCollection<TreeNodeViewModel> TreeNodes { get; }

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
        set => this.RaiseAndSetIfChanged(ref _currentModpackName, value);
    }

    private TreeNodeViewModel? _selectedNode;
    public TreeNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode != value)
            {
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

        // TODO: Load modified properties from staging if exists
        ModifiedProperties = new System.Collections.Generic.Dictionary<string, object?>(properties); // For now, start with vanilla
    }

    private System.Collections.Generic.Dictionary<string, object?> ConvertTemplateToProperties(DataTemplate template)
    {
        var result = new System.Collections.Generic.Dictionary<string, object?>();

        // All templates should be DynamicDataTemplate instances
        if (template is DynamicDataTemplate dynamicTemplate)
        {
            var jsonElement = dynamicTemplate.GetJsonElement();

            Console.WriteLine($"[DEBUG] ConvertTemplateToProperties: template={template.Name}, jsonElement.ValueKind={jsonElement.ValueKind}");

            int propCount = 0;
            try
            {
                foreach (var property in jsonElement.EnumerateObject())
                {
                    propCount++;
                    Console.WriteLine($"[DEBUG]   Processing property {propCount}: {property.Name}");

                    // Convert JsonElement to appropriate type
                    object? value = ConvertJsonElementToValue(property.Value);

                    // Try to resolve asset references
                    if (property.Value.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        var resolved = _assetResolver.Resolve(property.Value.GetInt64());
                        if (resolved.IsReference)
                        {
                            // Return a formatted string with asset info
                            if (resolved.HasAssetFile)
                            {
                                value = $"{resolved.DisplayValue} → {resolved.AssetPath}";
                            }
                            else if (!string.IsNullOrEmpty(resolved.AssetName))
                            {
                                value = $"{resolved.DisplayValue} (no asset file)";
                            }
                            else
                            {
                                value = resolved.DisplayValue;  // Show [Ref:ID]
                            }
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

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                this.RaiseAndSetIfChanged(ref _searchText, value);
                // TODO: Implement search filtering
            }
        }
    }

    private void LoadAllTemplates()
    {
        TreeNodes.Clear();

        // Load menu.json (hierarchical structure)
        var menuPath = Path.Combine(_modpackManager.VanillaDataPath, "menu.json");

        if (!File.Exists(menuPath))
        {
            // Fallback to old method if menu.json doesn't exist
            LoadAllTemplatesOld();
            return;
        }

        try
        {
            var menuJson = File.ReadAllText(menuPath);
            var menuRoot = System.Text.Json.JsonDocument.Parse(menuJson).RootElement;

            // Build tree from menu structure
            foreach (var topLevelProp in menuRoot.EnumerateObject())
            {
                var node = BuildTreeNodeFromJson(topLevelProp.Name, topLevelProp.Value);
                if (node != null)
                {
                    TreeNodes.Add(node);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load menu.json: {ex.Message}");
            LoadAllTemplatesOld();
        }
    }

    private TreeNodeViewModel? BuildTreeNodeFromJson(string name, System.Text.Json.JsonElement element)
    {
        // Check if this is a leaf node (has template_type, name, data)
        if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            var hasTemplateType = element.TryGetProperty("template_type", out _);
            var hasData = element.TryGetProperty("data", out var dataElement);

            if (hasTemplateType && hasData)
            {
                // This is a leaf node - create template
                // Get the template name from the data
                var templateName = dataElement.TryGetProperty("name", out var nameProperty)
                    ? nameProperty.GetString() ?? name
                    : name;

                var template = new DynamicDataTemplate(templateName, dataElement);
                return new TreeNodeViewModel
                {
                    Name = FormatNodeName(name),
                    IsCategory = false,
                    Template = template
                };
            }
            else
            {
                // This is a category node - recurse into children
                var node = new TreeNodeViewModel
                {
                    Name = FormatNodeName(name),
                    IsCategory = true
                };

                foreach (var childProp in element.EnumerateObject())
                {
                    var childNode = BuildTreeNodeFromJson(childProp.Name, childProp.Value);
                    if (childNode != null)
                    {
                        node.Children.Add(childNode);
                    }
                }

                return node.Children.Count > 0 ? node : null;
            }
        }

        return null;
    }

    private void LoadAllTemplatesOld()
    {
        // Old method - kept as fallback
        if (_dataLoader == null) return;

        foreach (var templateType in _dataLoader.GetTemplateTypes())
        {
            var templates = _dataLoader.LoadTemplatesGeneric(templateType);
            if (templates.Count == 0) continue;

            var rootNode = new TreeNodeViewModel
            {
                Name = FormatTemplateTypeName(templateType),
                IsCategory = true
            };

            var hierarchy = BuildHierarchy(templates);
            foreach (var child in hierarchy)
            {
                rootNode.Children.Add(child);
            }

            TreeNodes.Add(rootNode);
        }
    }

    private string FormatTemplateTypeName(string templateType)
    {
        // "WeaponTemplate" -> "Weapon Template"
        return System.Text.RegularExpressions.Regex.Replace(
            templateType.Replace("Template", ""),
            "([a-z])([A-Z])",
            "$1 $2");
    }

    private List<TreeNodeViewModel> BuildHierarchy(List<DataTemplate> templates)
    {
        var root = new Dictionary<string, TreeNodeViewModel>();

        foreach (var template in templates)
        {
            var parts = template.Name.Split('.');
            var currentLevel = root;
            TreeNodeViewModel? parentNode = null;

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var isLeaf = i == parts.Length - 1;

                if (!currentLevel.ContainsKey(part))
                {
                    var node = new TreeNodeViewModel
                    {
                        Name = FormatNodeName(part),
                        IsCategory = !isLeaf,
                        Template = isLeaf ? template : null
                    };

                    currentLevel[part] = node;

                    if (parentNode != null)
                    {
                        parentNode.Children.Add(node);
                    }
                }

                if (!isLeaf)
                {
                    parentNode = currentLevel[part];
                    if (parentNode.ChildrenDict == null)
                    {
                        parentNode.ChildrenDict = new Dictionary<string, TreeNodeViewModel>();
                    }
                    currentLevel = parentNode.ChildrenDict;
                }
            }
        }

        return root.Values.ToList();
    }

    private string FormatNodeName(string name)
    {
        // "pirate_laser_lance" -> "Pirate Laser Lance"
        return string.Join(" ", name.Split('_')
            .Select(w => char.ToUpper(w[0]) + w.Substring(1)));
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
