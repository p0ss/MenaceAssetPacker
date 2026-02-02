using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.Views;

/// <summary>
/// Modal dialog for browsing and selecting assets from AssetRipper output,
/// filtered by asset type (Sprite, Texture2D, etc.).
/// Returns the selected asset file path, or null if cancelled.
/// </summary>
public class AssetPickerDialog : Window
{
    private readonly string _assetType;
    private readonly ListBox _assetListBox;
    private readonly TextBox _searchBox;
    private readonly Image _previewImage;
    private readonly TextBlock _statusText;
    private List<AssetItem> _allAssets = new();
    private List<AssetItem> _filteredAssets = new();

    public class AssetItem
    {
        public string Name { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string RelativePath { get; set; } = "";
    }

    public AssetPickerDialog(string assetType)
    {
        _assetType = assetType;
        Title = $"Select {assetType} Asset";
        Width = 700;
        Height = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#1E1E1E"));

        // Build UI
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("2*,*"),
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(16)
        };

        // Search box (row 0, spans both columns)
        _searchBox = new TextBox
        {
            Watermark = $"Search {assetType} assets...",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8),
            Margin = new Thickness(0, 0, 0, 12)
        };
        _searchBox.TextChanged += OnSearchTextChanged;
        mainGrid.Children.Add(_searchBox);
        Grid.SetRow(_searchBox, 0);
        Grid.SetColumnSpan(_searchBox, 2);

        // Asset list (row 1, column 0)
        _assetListBox = new ListBox
        {
            Background = new SolidColorBrush(Color.Parse("#252525")),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 12, 0)
        };
        _assetListBox.SelectionChanged += OnAssetSelectionChanged;

        // Custom item template
        _assetListBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<AssetItem>((item, _) =>
        {
            var panel = new StackPanel { Spacing = 2, Margin = new Thickness(4, 2) };
            panel.Children.Add(new TextBlock
            {
                Text = item.Name,
                Foreground = Brushes.White,
                FontSize = 12
            });
            panel.Children.Add(new TextBlock
            {
                Text = item.RelativePath,
                Foreground = new SolidColorBrush(Color.Parse("#888888")),
                FontSize = 10
            });
            return panel;
        });

        mainGrid.Children.Add(_assetListBox);
        Grid.SetRow(_assetListBox, 1);
        Grid.SetColumn(_assetListBox, 0);

        // Preview panel (row 1, column 1)
        var previewPanel = new StackPanel
        {
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Top
        };

        previewPanel.Children.Add(new TextBlock
        {
            Text = "Preview",
            Foreground = Brushes.White,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold
        });

        var previewBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1A1A1A")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            MinHeight = 128,
            MinWidth = 128,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _previewImage = new Image
        {
            Width = 128,
            Height = 128,
            Stretch = Stretch.Uniform
        };
        previewBorder.Child = _previewImage;
        previewPanel.Children.Add(previewBorder);

        _statusText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        previewPanel.Children.Add(_statusText);

        mainGrid.Children.Add(previewPanel);
        Grid.SetRow(previewPanel, 1);
        Grid.SetColumn(previewPanel, 1);

        // Button row (row 2, spans both columns)
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var okButton = new Button
        {
            Content = "Select",
            Background = new SolidColorBrush(Color.Parse("#064b48")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(24, 8),
            FontSize = 13
        };
        okButton.Click += (_, _) =>
        {
            if (_assetListBox.SelectedItem is AssetItem selected)
                Close(selected.FilePath);
        };
        buttonRow.Children.Add(okButton);

        var cancelButton = new Button
        {
            Content = "Cancel",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            Padding = new Thickness(24, 8),
            FontSize = 13
        };
        cancelButton.Click += (_, _) => Close(null);
        buttonRow.Children.Add(cancelButton);

        mainGrid.Children.Add(buttonRow);
        Grid.SetRow(buttonRow, 2);
        Grid.SetColumnSpan(buttonRow, 2);

        Content = mainGrid;

        // Load assets
        LoadAssets();
    }

    private void LoadAssets()
    {
        _allAssets.Clear();

        var assetOutputPath = GetAssetOutputPath();
        if (assetOutputPath == null)
        {
            _statusText.Text = "No extracted assets found.";
            return;
        }

        // Search in the specific asset type directory
        var typeDir = Path.Combine(assetOutputPath, "Assets", _assetType);
        if (Directory.Exists(typeDir))
        {
            ScanDirectory(typeDir, assetOutputPath);
        }

        // Also check common alternative locations
        if (_assetType == "Sprite" || _assetType == "Texture2D")
        {
            // Sprites and Textures may be under Resources or other paths
            var resourcesDir = Path.Combine(assetOutputPath, "Assets", "Resources");
            if (Directory.Exists(resourcesDir))
                ScanDirectory(resourcesDir, assetOutputPath, "*.png");
        }

        // Sort by name
        _allAssets = _allAssets.OrderBy(a => a.Name).ToList();

        _filteredAssets = new List<AssetItem>(_allAssets);
        _assetListBox.ItemsSource = _filteredAssets;
        _statusText.Text = $"{_allAssets.Count} {_assetType} assets found";
    }

    private void ScanDirectory(string directory, string rootPath, string? pattern = null)
    {
        try
        {
            var extensions = pattern != null
                ? new[] { pattern }
                : GetExtensionsForType(_assetType);

            foreach (var ext in extensions)
            {
                foreach (var file in Directory.GetFiles(directory, ext, SearchOption.AllDirectories))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    var relativePath = Path.GetRelativePath(rootPath, file);

                    // Avoid duplicates
                    if (_allAssets.Any(a => a.FilePath == file))
                        continue;

                    _allAssets.Add(new AssetItem
                    {
                        Name = name,
                        FilePath = file,
                        RelativePath = relativePath
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AssetPickerDialog] Error scanning {directory}: {ex.Message}");
        }
    }

    private static string[] GetExtensionsForType(string assetType)
    {
        return assetType switch
        {
            "Sprite" => new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tga" },
            "Texture2D" => new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tga", "*.exr" },
            "Material" => new[] { "*.mat" },
            "Mesh" => new[] { "*.obj", "*.fbx", "*.asset" },
            "AudioClip" => new[] { "*.ogg", "*.wav", "*.mp3", "*.aif" },
            "AnimationClip" => new[] { "*.anim" },
            _ => new[] { "*.*" },
        };
    }

    private static string? GetAssetOutputPath()
    {
        return AppSettings.GetEffectiveAssetsPath();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = _searchBox.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(query))
        {
            _filteredAssets = new List<AssetItem>(_allAssets);
        }
        else
        {
            _filteredAssets = _allAssets
                .Where(a => a.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            a.RelativePath.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        _assetListBox.ItemsSource = _filteredAssets;
        _statusText.Text = $"{_filteredAssets.Count} of {_allAssets.Count} assets shown";
    }

    private void OnAssetSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_assetListBox.SelectedItem is not AssetItem selected)
        {
            _previewImage.Source = null;
            return;
        }

        // Show preview for image assets
        if (_assetType is "Sprite" or "Texture2D" && File.Exists(selected.FilePath))
        {
            try
            {
                _previewImage.Source = new Bitmap(selected.FilePath);
                _statusText.Text = selected.Name;
            }
            catch
            {
                _previewImage.Source = null;
                _statusText.Text = $"{selected.Name} (preview unavailable)";
            }
        }
        else
        {
            _previewImage.Source = null;
            _statusText.Text = selected.Name;
        }
    }
}
