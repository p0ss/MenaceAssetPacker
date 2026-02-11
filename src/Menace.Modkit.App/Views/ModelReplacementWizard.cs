using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.Views;

/// <summary>
/// Multi-step wizard for replacing 3D models in modpacks.
/// Guides users through selecting a vanilla model, choosing a replacement,
/// mapping textures, and saving to a modpack.
/// </summary>
public class ModelReplacementWizard : Window
{
    private readonly ModelReplacementWizardViewModel _viewModel;
    private readonly ModpackManager _modpackManager;
    private readonly string _extractedAssetsPath;

    private StackPanel _contentPanel = null!;
    private TextBlock _stepTitle = null!;
    private Button _backButton = null!;
    private Button _nextButton = null!;
    private TextBlock _errorText = null!;

    public ModelReplacementWizard(ModpackManager modpackManager, string extractedAssetsPath)
    {
        _modpackManager = modpackManager;
        _extractedAssetsPath = extractedAssetsPath;
        _viewModel = new ModelReplacementWizardViewModel(extractedAssetsPath, modpackManager);

        Title = "Model Replacement Wizard";
        Width = 650;
        Height = 550;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#1a1a1a"));

        BuildUI();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateStepUI();
    }

    private void BuildUI()
    {
        var mainStack = new StackPanel
        {
            Margin = new Thickness(24)
        };

        // Step indicator
        _stepTitle = new TextBlock
        {
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 16)
        };
        mainStack.Children.Add(_stepTitle);

        // Content area
        _contentPanel = new StackPanel();
        var scrollViewer = new ScrollViewer
        {
            Content = _contentPanel,
            Height = 380,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
        mainStack.Children.Add(scrollViewer);

        // Error text
        _errorText = new TextBlock
        {
            Foreground = Brushes.Salmon,
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        mainStack.Children.Add(_errorText);

        // Button bar
        var buttonBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
            Spacing = 8
        };

        var cancelButton = new Button { Content = "Cancel" };
        cancelButton.Click += (_, _) => Close();
        buttonBar.Children.Add(cancelButton);

        _backButton = new Button { Content = "Back" };
        _backButton.Click += OnBackClick;
        buttonBar.Children.Add(_backButton);

        _nextButton = new Button { Content = "Next" };
        _nextButton.Classes.Add("primary");
        _nextButton.Click += OnNextClick;
        buttonBar.Children.Add(_nextButton);

        mainStack.Children.Add(buttonBar);

        Content = mainStack;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_viewModel.CurrentStep))
            UpdateStepUI();
        else if (e.PropertyName == nameof(_viewModel.ErrorMessage))
            _errorText.Text = _viewModel.ErrorMessage;
    }

    private void UpdateStepUI()
    {
        _contentPanel.Children.Clear();

        switch (_viewModel.CurrentStep)
        {
            case 1:
                _stepTitle.Text = "Step 1: Select Vanilla Model";
                BuildStep1();
                break;
            case 2:
                _stepTitle.Text = "Step 2: Choose Replacement File";
                BuildStep2();
                break;
            case 3:
                _stepTitle.Text = "Step 3: Texture Mapping";
                BuildStep3();
                break;
            case 4:
                _stepTitle.Text = "Step 4: Save to Modpack";
                BuildStep4();
                break;
        }

        _backButton.IsEnabled = _viewModel.CurrentStep > 1;
        _nextButton.Content = _viewModel.CurrentStep == 4 ? "Save Replacement" : "Next";
    }

    private void BuildStep1()
    {
        var desc = new TextBlock
        {
            Text = "Select the vanilla model you want to replace:",
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 12)
        };
        _contentPanel.Children.Add(desc);

        // Search box
        var searchBox = new TextBox
        {
            Watermark = "Search models...",
            Margin = new Thickness(0, 0, 0, 8)
        };
        searchBox.TextChanged += (_, _) => _viewModel.FilterModels(searchBox.Text ?? "");
        _contentPanel.Children.Add(searchBox);

        // Model list
        var listBox = new ListBox
        {
            Height = 280,
            ItemsSource = _viewModel.FilteredModels,
            Background = new SolidColorBrush(Color.Parse("#0d0d0d"))
        };
        listBox.SelectionChanged += (_, _) =>
        {
            if (listBox.SelectedItem is ModelEntry entry)
                _viewModel.SelectedVanillaModel = entry;
        };
        _contentPanel.Children.Add(listBox);

        // Info text
        var info = new TextBlock
        {
            Text = $"Found {_viewModel.AllModels.Count} models in extracted assets",
            Foreground = Brushes.Gray,
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0)
        };
        _contentPanel.Children.Add(info);
    }

    private void BuildStep2()
    {
        var desc = new TextBlock
        {
            Text = $"Select a replacement for: {_viewModel.SelectedVanillaModel?.Name}",
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 12)
        };
        _contentPanel.Children.Add(desc);

        // Current selection display
        if (!string.IsNullOrEmpty(_viewModel.ReplacementFilePath))
        {
            var selectedPanel = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1f3a1f")),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var selectedText = new TextBlock
            {
                Text = $"Selected: {Path.GetFileName(_viewModel.ReplacementFilePath)}",
                Foreground = Brushes.LightGreen
            };
            selectedPanel.Child = selectedText;
            _contentPanel.Children.Add(selectedPanel);
        }

        // File picker button
        var pickButton = new Button
        {
            Content = "Browse for GLB/GLTF File...",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        pickButton.Classes.Add("primary");
        pickButton.Click += async (_, _) => await PickReplacementFile();
        _contentPanel.Children.Add(pickButton);

        // Supported formats info
        var formatsInfo = new TextBlock
        {
            Text = "Supported formats: GLB, GLTF (recommended), FBX, OBJ\n\n" +
                   "GLB files with embedded textures work best.\n" +
                   "Use Blender to convert other formats to GLB.",
            Foreground = Brushes.Gray,
            FontSize = 12,
            Margin = new Thickness(0, 16, 0, 0)
        };
        _contentPanel.Children.Add(formatsInfo);
    }

    private async Task PickReplacementFile()
    {
        var filters = new List<FilePickerFileType>
        {
            new("3D Models") { Patterns = new[] { "*.glb", "*.gltf", "*.fbx", "*.obj" } },
            new("GLB Files") { Patterns = new[] { "*.glb" } },
            new("All Files") { Patterns = new[] { "*" } }
        };

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Replacement Model",
            AllowMultiple = false,
            FileTypeFilter = filters
        });

        if (files.Count > 0)
        {
            _viewModel.ReplacementFilePath = files[0].Path.LocalPath;
            _viewModel.LoadReplacementInfo();
            UpdateStepUI();
        }
    }

    private void BuildStep3()
    {
        var desc = new TextBlock
        {
            Text = "Review texture dependencies:",
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 12)
        };
        _contentPanel.Children.Add(desc);

        if (_viewModel.TextureMappings.Count == 0)
        {
            var noTextures = new TextBlock
            {
                Text = "No external texture dependencies detected.\n\n" +
                       "If your model uses embedded textures, they will be included automatically.",
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 8, 0, 0)
            };
            _contentPanel.Children.Add(noTextures);
        }
        else
        {
            foreach (var mapping in _viewModel.TextureMappings)
            {
                var row = new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#0d0d0d")),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 4, 0, 0)
                };

                var rowStack = new StackPanel();

                var typeBadge = new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#2a4a5a")),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                typeBadge.Child = new TextBlock
                {
                    Text = mapping.TextureType,
                    FontSize = 11,
                    Foreground = Brushes.White
                };
                rowStack.Children.Add(typeBadge);

                var statusColor = mapping.IsResolved ? Brushes.LightGreen : Brushes.Salmon;
                var statusText = mapping.IsResolved
                    ? $"✓ Found: {Path.GetFileName(mapping.ResolvedPath)}"
                    : $"⚠ Missing: {mapping.ExpectedName}";

                var status = new TextBlock
                {
                    Text = statusText,
                    Foreground = statusColor,
                    FontSize = 12,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                rowStack.Children.Add(status);

                row.Child = rowStack;
                _contentPanel.Children.Add(row);
            }
        }

        // Tip
        var tip = new TextBlock
        {
            Text = "\nTip: For best results, export your model from Blender with textures embedded in the GLB file.",
            Foreground = Brushes.Gray,
            FontSize = 11,
            FontStyle = FontStyle.Italic,
            TextWrapping = TextWrapping.Wrap
        };
        _contentPanel.Children.Add(tip);
    }

    private void BuildStep4()
    {
        var desc = new TextBlock
        {
            Text = "Select the modpack to save the replacement to:",
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 0, 0, 12)
        };
        _contentPanel.Children.Add(desc);

        // Modpack selector
        var modpackCombo = new ComboBox
        {
            Width = 300,
            HorizontalAlignment = HorizontalAlignment.Left,
            ItemsSource = _viewModel.AvailableModpacks
        };
        modpackCombo.SelectionChanged += (_, _) =>
        {
            if (modpackCombo.SelectedItem is string name)
                _viewModel.SelectedModpack = name;
        };
        if (_viewModel.AvailableModpacks.Count > 0)
        {
            modpackCombo.SelectedIndex = 0;
            _viewModel.SelectedModpack = _viewModel.AvailableModpacks[0];
        }
        _contentPanel.Children.Add(modpackCombo);

        // Summary
        var summaryPanel = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#0d0d0d")),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 16, 0, 0)
        };

        var summaryStack = new StackPanel { Spacing = 4 };
        summaryStack.Children.Add(new TextBlock
        {
            Text = "Summary",
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });
        summaryStack.Children.Add(new TextBlock
        {
            Text = $"Vanilla model: {_viewModel.SelectedVanillaModel?.Name}",
            Foreground = Brushes.LightGray,
            FontSize = 12
        });
        summaryStack.Children.Add(new TextBlock
        {
            Text = $"Replacement: {Path.GetFileName(_viewModel.ReplacementFilePath)}",
            Foreground = Brushes.LightGray,
            FontSize = 12
        });
        summaryStack.Children.Add(new TextBlock
        {
            Text = $"Textures: {_viewModel.TextureMappings.Count(t => t.IsResolved)} resolved, {_viewModel.TextureMappings.Count(t => !t.IsResolved)} missing",
            Foreground = Brushes.LightGray,
            FontSize = 12
        });

        summaryPanel.Child = summaryStack;
        _contentPanel.Children.Add(summaryPanel);
    }

    private void OnBackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.GoBack();
    }

    private async void OnNextClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel.CurrentStep == 4)
        {
            // Save and close
            if (await _viewModel.SaveReplacement())
            {
                Close(true);
            }
        }
        else
        {
            _viewModel.GoNext();
        }
    }
}

/// <summary>
/// ViewModel for the Model Replacement Wizard.
/// </summary>
public class ModelReplacementWizardViewModel : INotifyPropertyChanged
{
    private readonly string _extractedAssetsPath;
    private readonly ModpackManager _modpackManager;
    private readonly GlbService? _glbService;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ModelReplacementWizardViewModel(string extractedAssetsPath, ModpackManager modpackManager)
    {
        _extractedAssetsPath = extractedAssetsPath;
        _modpackManager = modpackManager;

        if (Directory.Exists(extractedAssetsPath))
            _glbService = new GlbService(extractedAssetsPath);

        LoadModels();
        LoadModpacks();
    }

    // Step tracking
    private int _currentStep = 1;
    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            _currentStep = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentStep)));
        }
    }

    private string _errorMessage = "";
    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            _errorMessage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorMessage)));
        }
    }

    // Step 1: Model selection
    public ObservableCollection<ModelEntry> AllModels { get; } = new();
    public ObservableCollection<ModelEntry> FilteredModels { get; } = new();

    private ModelEntry? _selectedVanillaModel;
    public ModelEntry? SelectedVanillaModel
    {
        get => _selectedVanillaModel;
        set
        {
            _selectedVanillaModel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedVanillaModel)));
        }
    }

    // Step 2: Replacement file
    private string _replacementFilePath = "";
    public string ReplacementFilePath
    {
        get => _replacementFilePath;
        set
        {
            _replacementFilePath = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReplacementFilePath)));
        }
    }

    // Step 3: Texture mappings
    public ObservableCollection<TextureMapping> TextureMappings { get; } = new();

    // Step 4: Modpack selection
    public ObservableCollection<string> AvailableModpacks { get; } = new();
    public string SelectedModpack { get; set; } = "";

    private void LoadModels()
    {
        var meshPath = Path.Combine(_extractedAssetsPath, "Assets", "Mesh");
        if (!Directory.Exists(meshPath))
            return;

        var files = Directory.GetFiles(meshPath, "*.glb", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(meshPath, "*.fbx", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(meshPath, "*.obj", SearchOption.AllDirectories));

        foreach (var file in files.OrderBy(f => Path.GetFileName(f)))
        {
            var entry = new ModelEntry
            {
                Name = Path.GetFileName(file),
                FullPath = file,
                RelativePath = Path.GetRelativePath(_extractedAssetsPath, file)
            };
            AllModels.Add(entry);
            FilteredModels.Add(entry);
        }
    }

    private void LoadModpacks()
    {
        foreach (var modpack in _modpackManager.GetStagingModpacks())
        {
            AvailableModpacks.Add(modpack.Name);
        }
    }

    public void FilterModels(string filter)
    {
        FilteredModels.Clear();
        var query = string.IsNullOrWhiteSpace(filter)
            ? AllModels
            : AllModels.Where(m => m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));

        foreach (var model in query)
            FilteredModels.Add(model);
    }

    public void LoadReplacementInfo()
    {
        TextureMappings.Clear();

        if (string.IsNullOrEmpty(ReplacementFilePath) || _glbService == null)
            return;

        var ext = Path.GetExtension(ReplacementFilePath).ToLowerInvariant();
        if (ext is not ".glb" and not ".gltf")
            return;

        try
        {
            var linkedTextures = _glbService.GetLinkedTextures(ReplacementFilePath);
            foreach (var tex in linkedTextures)
            {
                TextureMappings.Add(new TextureMapping
                {
                    TextureType = tex.TextureType,
                    ExpectedName = tex.ExpectedFileName,
                    IsResolved = tex.IsFound,
                    ResolvedPath = tex.FoundPath,
                    IsEmbedded = tex.IsEmbedded
                });
            }
        }
        catch
        {
            // Ignore parsing errors
        }
    }

    public void GoNext()
    {
        ErrorMessage = "";

        switch (CurrentStep)
        {
            case 1:
                if (SelectedVanillaModel == null)
                {
                    ErrorMessage = "Please select a vanilla model to replace.";
                    return;
                }
                break;
            case 2:
                if (string.IsNullOrEmpty(ReplacementFilePath) || !File.Exists(ReplacementFilePath))
                {
                    ErrorMessage = "Please select a replacement file.";
                    return;
                }
                break;
            case 3:
                // Texture mapping is optional
                break;
        }

        if (CurrentStep < 4)
            CurrentStep++;
    }

    public void GoBack()
    {
        ErrorMessage = "";
        if (CurrentStep > 1)
            CurrentStep--;
    }

    public async Task<bool> SaveReplacement()
    {
        if (string.IsNullOrEmpty(SelectedModpack))
        {
            ErrorMessage = "Please select a modpack.";
            return false;
        }

        if (SelectedVanillaModel == null || string.IsNullOrEmpty(ReplacementFilePath))
        {
            ErrorMessage = "Missing model or replacement file.";
            return false;
        }

        try
        {
            // Save the replacement file to the modpack's assets folder
            _modpackManager.SaveStagingAsset(SelectedModpack, SelectedVanillaModel.RelativePath, ReplacementFilePath);
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save: {ex.Message}";
            return false;
        }
    }
}

public class ModelEntry
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string RelativePath { get; set; } = "";

    public override string ToString() => Name;
}

public class TextureMapping
{
    public string TextureType { get; set; } = "";
    public string ExpectedName { get; set; } = "";
    public bool IsResolved { get; set; }
    public string? ResolvedPath { get; set; }
    public bool IsEmbedded { get; set; }
}
