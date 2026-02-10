using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Menace.Modkit.App.Controls;
using Menace.Modkit.App.Converters;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;
using Menace.Modkit.App.ViewModels;

namespace Menace.Modkit.App.Views;

public class AssetBrowserView : UserControl
{
    public AssetBrowserView()
    {
        Content = BuildUI();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Refresh modpacks when view becomes visible to pick up newly created ones
        if (DataContext is AssetBrowserViewModel vm)
            vm.RefreshModpacks();
    }

    private Control BuildUI()
    {
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("300,4,*")
        };

        // Left: Asset Navigation Tree (darker panel)
        var leftPanel = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#141414")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = BuildNavigation()
        };
        mainGrid.Children.Add(leftPanel);
        Grid.SetColumn(leftPanel, 0);

        // Splitter
        var splitter = new GridSplitter
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
            ResizeDirection = GridResizeDirection.Columns
        };
        mainGrid.Children.Add(splitter);
        Grid.SetColumn(splitter, 1);

        // Right: Asset Viewer (lighter panel)
        mainGrid.Children.Add(BuildAssetViewer());
        Grid.SetColumn((Control)mainGrid.Children[2], 2);

        return mainGrid;
    }

    private Control BuildNavigation()
    {
        var border = new Border();  // No padding - use consistent margins on children

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto")
        };

        // Row 0: Search Box
        var searchBox = new TextBox
        {
            Watermark = "Search assets... (3+ chars or Enter)",
            Margin = new Thickness(8, 8, 8, 12)
        };
        searchBox.Classes.Add("search");
        searchBox.Bind(TextBox.TextProperty,
            new Avalonia.Data.Binding("SearchText"));
        searchBox.KeyDown += (s, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter && DataContext is AssetBrowserViewModel vm)
                vm.ExecuteSearch();
        };
        grid.Children.Add(searchBox);
        Grid.SetRow(searchBox, 0);

        // Row 1: Toggle between Expand/Collapse buttons and Sort dropdown
        var buttonContainer = new Panel();

        // Expand/Collapse + Modpack Only buttons (shown when not searching)
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8, 4, 8, 12)
        };
        buttonPanel.Bind(StackPanel.IsVisibleProperty,
            new Avalonia.Data.Binding("IsSearching") { Converter = BoolInverseConverter.Instance });

        var expandAllButton = new Button
        {
            Content = "Expand All",
            FontSize = 11
        };
        expandAllButton.Classes.Add("secondary");
        expandAllButton.Click += (_, _) =>
        {
            if (DataContext is AssetBrowserViewModel vm)
                vm.ExpandAll();
        };
        buttonPanel.Children.Add(expandAllButton);

        var collapseAllButton = new Button
        {
            Content = "Collapse All",
            FontSize = 11
        };
        collapseAllButton.Classes.Add("secondary");
        collapseAllButton.Click += (_, _) =>
        {
            if (DataContext is AssetBrowserViewModel vm)
                vm.CollapseAll();
        };
        buttonPanel.Children.Add(collapseAllButton);

        var modpackOnlyToggle = new ToggleButton
        {
            Content = "Modpack Only",
            FontSize = 11
        };
        modpackOnlyToggle.Classes.Add("secondary");
        modpackOnlyToggle.Bind(ToggleButton.IsCheckedProperty,
            new Avalonia.Data.Binding("ShowModpackOnly")
            {
                Mode = Avalonia.Data.BindingMode.TwoWay
            });
        buttonPanel.Children.Add(modpackOnlyToggle);

        buttonContainer.Children.Add(buttonPanel);

        // Sort and filter panel (shown when searching)
        var searchControlsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8, 4, 8, 12)
        };
        searchControlsPanel.Bind(StackPanel.IsVisibleProperty,
            new Avalonia.Data.Binding("IsSearching"));

        // Section filter dropdown
        var sectionCombo = new ComboBox
        {
            FontSize = 11,
            MinWidth = 120
        };
        sectionCombo.Classes.Add("input");
        sectionCombo.Bind(ComboBox.ItemsSourceProperty,
            new Avalonia.Data.Binding("SectionFilters"));
        sectionCombo.Bind(ComboBox.SelectedItemProperty,
            new Avalonia.Data.Binding("SelectedSectionFilter"));
        searchControlsPanel.Children.Add(sectionCombo);

        // Sort dropdown
        var sortLabel = new TextBlock
        {
            Text = "Sort:",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        searchControlsPanel.Children.Add(sortLabel);

        var sortCombo = new ComboBox
        {
            FontSize = 11,
            MinWidth = 100
        };
        sortCombo.Items.Add("Relevance");
        sortCombo.Items.Add("Name A-Z");
        sortCombo.Items.Add("Name Z-A");
        sortCombo.Items.Add("Path A-Z");
        sortCombo.Items.Add("Path Z-A");
        sortCombo.SelectedIndex = 0;
        sortCombo.SelectionChanged += (s, e) =>
        {
            if (sortCombo.SelectedIndex >= 0 && DataContext is AssetBrowserViewModel vm)
                vm.CurrentSortOption = (SearchPanelBuilder.SortOption)sortCombo.SelectedIndex;
        };
        sortCombo.Classes.Add("input");
        searchControlsPanel.Children.Add(sortCombo);

        buttonContainer.Children.Add(searchControlsPanel);

        grid.Children.Add(buttonContainer);
        Grid.SetRow(buttonContainer, 1);

        // Row 2: Toggle between TreeView and Search Results ListBox
        var contentContainer = new Panel();

        // Asset Tree (non-virtualizing so Expand All works fully) - shown when not searching
        var treeView = new TreeView
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(8, 0),
            ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Avalonia.Controls.Panel?>(() => new StackPanel())
        };
        treeView.Bind(TreeView.ItemsSourceProperty,
            new Avalonia.Data.Binding("FolderTree"));
        treeView.Bind(TreeView.SelectedItemProperty,
            new Avalonia.Data.Binding("SelectedNode") { Mode = Avalonia.Data.BindingMode.TwoWay });
        treeView.Bind(TreeView.IsVisibleProperty,
            new Avalonia.Data.Binding("IsSearching") { Converter = BoolInverseConverter.Instance });

        // Tree item template: folders bold/13pt, files normal/12pt
        treeView.ItemTemplate = new Avalonia.Controls.Templates.FuncTreeDataTemplate<AssetTreeNode>(
            (node, _) => new TextBlock
            {
                Text = node.Name,
                FontWeight = node.IsFile ? FontWeight.Normal : FontWeight.SemiBold,
                Foreground = Brushes.White,
                FontSize = node.IsFile ? 12 : 13,
                Margin = new Thickness(8, 6)
            },
            node => node.Children);

        // Bind IsExpanded two-way via ContainerPrepared
        treeView.ContainerPrepared += (_, e) =>
        {
            if (e.Container is TreeViewItem tvi && tvi.DataContext is AssetTreeNode nodeVm)
            {
                tvi.IsExpanded = nodeVm.IsExpanded;
                tvi.Bind(TreeViewItem.IsExpandedProperty,
                    new Avalonia.Data.Binding("IsExpanded")
                    {
                        Mode = Avalonia.Data.BindingMode.TwoWay
                    });
            }
        };

        contentContainer.Children.Add(treeView);

        // Search Results ListBox - shown when searching
        var searchResultsList = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(8, 0)
        };
        searchResultsList.Bind(ListBox.ItemsSourceProperty,
            new Avalonia.Data.Binding("SearchResults"));
        searchResultsList.Bind(ListBox.IsVisibleProperty,
            new Avalonia.Data.Binding("IsSearching"));

        searchResultsList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<SearchResultItem>(
            (item, _) => SearchPanelBuilder.CreateSearchResultControl(item), true);

        searchResultsList.SelectionChanged += (s, e) =>
        {
            if (searchResultsList.SelectedItem is SearchResultItem item &&
                DataContext is AssetBrowserViewModel vm)
            {
                vm.SelectSearchResult(item);
            }
        };

        // Double-click to select and exit search mode
        searchResultsList.DoubleTapped += (s, e) =>
        {
            if (searchResultsList.SelectedItem is SearchResultItem item &&
                DataContext is AssetBrowserViewModel vm)
            {
                vm.SelectAndExitSearch(item);
            }
        };

        contentContainer.Children.Add(searchResultsList);

        grid.Children.Add(contentContainer);
        Grid.SetRow(contentContainer, 2);

        // Row 3: Extraction Status
        var statusText = new SelectableTextBlock
        {
            Foreground = Brushes.White,
            Opacity = 0.8,
            FontSize = 11,
            Margin = new Thickness(8, 12, 8, 8),
            TextWrapping = TextWrapping.Wrap
        };
        statusText.Bind(SelectableTextBlock.TextProperty,
            new Avalonia.Data.Binding("ExtractionStatus"));
        grid.Children.Add(statusText);
        Grid.SetRow(statusText, 3);

        // Row 4: Extract Assets Button
        var extractButton = new Button
        {
            Content = "Extract Assets",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontSize = 13,
            Margin = new Thickness(8, 0, 8, 8)
        };
        extractButton.Classes.Add("primary");
        extractButton.Click += OnExtractAssetsClick;
        extractButton.Bind(Button.IsEnabledProperty,
            new Avalonia.Data.Binding("!IsExtracting"));
        grid.Children.Add(extractButton);
        Grid.SetRow(extractButton, 4);

        border.Child = grid;
        return border;
    }

    private Control BuildAssetViewer()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1A1A1A")),
            Padding = new Thickness(24)
        };

        var outerGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto")
        };

        // Row 0: Toolbar
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(0, 0, 0, 12),
            VerticalAlignment = VerticalAlignment.Center
        };

        toolbar.Children.Add(new TextBlock
        {
            Text = "Modpack:",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12
        });

        var modpackCombo = new ComboBox
        {
            MinWidth = 200,
            FontSize = 12
        };
        modpackCombo.Classes.Add("input");
        modpackCombo.Bind(ComboBox.ItemsSourceProperty,
            new Avalonia.Data.Binding("AvailableModpacks"));
        modpackCombo.Bind(ComboBox.SelectedItemProperty,
            new Avalonia.Data.Binding("CurrentModpackName"));
        toolbar.Children.Add(modpackCombo);

        var statusText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11,
            Opacity = 0.9
        };
        statusText.Bind(TextBlock.TextProperty,
            new Avalonia.Data.Binding("SaveStatus"));
        toolbar.Children.Add(statusText);

        outerGrid.Children.Add(toolbar);
        Grid.SetRow(toolbar, 0);

        // Row 1: Two-column Vanilla | Modified
        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*")
        };

        contentGrid.Children.Add(BuildVanillaPanel());
        Grid.SetColumn((Control)contentGrid.Children[0], 0);

        contentGrid.Children.Add(BuildModifiedPanel());
        Grid.SetColumn((Control)contentGrid.Children[1], 1);

        outerGrid.Children.Add(contentGrid);
        Grid.SetRow(contentGrid, 1);

        // Row 2: Referenced By panel
        var backlinksPanel = BuildAssetBacklinksPanel();
        outerGrid.Children.Add(backlinksPanel);
        Grid.SetRow(backlinksPanel, 2);

        border.Child = outerGrid;
        return border;
    }

    private Control BuildAssetBacklinksPanel()
    {
        var panel = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1A1A1A")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 8),
            Margin = new Thickness(0, 12, 0, 0)
        };

        var stack = new StackPanel { Spacing = 6 };

        var header = new TextBlock
        {
            Text = "Referenced By",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
            Margin = new Thickness(0, 0, 0, 4)
        };
        stack.Children.Add(header);

        var itemsControl = new ItemsControl();
        itemsControl.Bind(ItemsControl.ItemsSourceProperty,
            new Avalonia.Data.Binding("AssetBacklinks"));

        itemsControl.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ReferenceEntry>((entry, _) =>
        {
            var button = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 2),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var textPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

            var typeBadge = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#2A3A4A")),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1),
                VerticalAlignment = VerticalAlignment.Center
            };
            typeBadge.Child = new TextBlock
            {
                Text = entry.SourceTemplateType,
                Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
                FontSize = 10
            };
            textPanel.Children.Add(typeBadge);

            textPanel.Children.Add(new TextBlock
            {
                Text = entry.DisplayName,
                Foreground = Brushes.White,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });

            // Add "Open in Stats Editor" hint
            textPanel.Children.Add(new TextBlock
            {
                Text = "(click to open)",
                Foreground = Brushes.White,
                Opacity = 0.5,
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            });

            button.Content = textPanel;

            button.Click += (_, _) =>
            {
                if (DataContext is AssetBrowserViewModel vm)
                {
                    vm.RequestNavigateToTemplate(entry);
                }
            };

            return button;
        });

        stack.Children.Add(itemsControl);

        // Empty state message
        var emptyText = new TextBlock
        {
            Text = "No templates reference this asset",
            Foreground = Brushes.White,
            Opacity = 0.5,
            FontSize = 11,
            FontStyle = FontStyle.Italic
        };
        emptyText.Bind(TextBlock.IsVisibleProperty,
            new Avalonia.Data.Binding("AssetBacklinks.Count")
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<int, bool>(c => c == 0)
            });
        stack.Children.Add(emptyText);

        panel.Child = stack;

        // Hide the entire panel when no file is selected
        panel.Bind(Border.IsVisibleProperty,
            new Avalonia.Data.Binding("SelectedNode")
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<object?, bool>(n =>
                    n is AssetTreeNode node && node.IsFile)
            });

        return panel;
    }

    private Control BuildVanillaPanel()
    {
        var panel = new Grid
        {
            Margin = new Thickness(0, 0, 12, 0),
            RowDefinitions = new RowDefinitions("Auto,*")
        };

        var header = new TextBlock
        {
            Text = "Vanilla",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 12)
        };
        panel.Children.Add(header);
        Grid.SetRow(header, 0);

        var previewBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16)
        };

        var previewContainer = new Panel();

        // Preview content
        var previewStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        // Image preview with dimension border
        var imageContainer = BuildImageWithDimensionBorder(
            "PreviewImage",
            "VanillaImageWidth",
            "VanillaImageHeight",
            "HasImagePreview");
        previewStack.Children.Add(imageContainer);

        // Text preview
        var textScrollViewer = new ScrollViewer
        {
            MaxHeight = 400,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var textPreview = new TextBlock
        {
            Foreground = Brushes.White,
            FontFamily = new FontFamily("monospace"),
            FontSize = 12,
            TextWrapping = TextWrapping.NoWrap
        };
        textPreview.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("PreviewText"));
        textPreview.Bind(TextBlock.IsVisibleProperty, new Avalonia.Data.Binding("HasTextPreview"));
        textScrollViewer.Content = textPreview;
        textScrollViewer.Bind(ScrollViewer.IsVisibleProperty, new Avalonia.Data.Binding("HasTextPreview"));
        previewStack.Children.Add(textScrollViewer);

        // Info text under image
        var infoText = new TextBlock
        {
            Foreground = Brushes.White,
            Opacity = 0.7,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        infoText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("PreviewText"));
        infoText.Bind(TextBlock.IsVisibleProperty, new Avalonia.Data.Binding("HasImagePreview"));
        previewStack.Children.Add(infoText);

        // GLB linked textures panel
        var glbPanel = BuildGlbLinkedTexturesPanel();
        previewStack.Children.Add(glbPanel);

        previewStack.Bind(StackPanel.IsVisibleProperty, new Avalonia.Data.Binding("SelectedNode")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<object?, bool>(obj =>
                obj is AssetTreeNode node && node.IsFile)
        });

        // Empty state
        var emptyStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 12
        };
        emptyStack.Children.Add(new TextBlock
        {
            Text = "Select a file to preview",
            FontSize = 14,
            Foreground = Brushes.White,
            Opacity = 0.6,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        emptyStack.Bind(StackPanel.IsVisibleProperty, new Avalonia.Data.Binding("SelectedNode")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<object?, bool>(obj =>
                obj == null || (obj is AssetTreeNode node && !node.IsFile))
        });

        previewContainer.Children.Add(previewStack);
        previewContainer.Children.Add(emptyStack);
        previewBorder.Child = previewContainer;
        panel.Children.Add(previewBorder);
        Grid.SetRow(previewBorder, 1);

        return panel;
    }

    /// <summary>
    /// Creates a panel showing GLB 3D preview and linked textures with export/import buttons.
    /// </summary>
    private Control BuildGlbLinkedTexturesPanel()
    {
        var panel = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0)
        };
        panel.Bind(StackPanel.IsVisibleProperty, new Avalonia.Data.Binding("HasGlbPreview"));

        // 3D Preview Image
        var previewBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E1E24")),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var previewImage = new Image
        {
            Width = 200,
            Height = 200,
            Stretch = Stretch.Uniform
        };
        previewImage.Bind(Image.SourceProperty, new Avalonia.Data.Binding("GlbPreviewImage"));
        previewBorder.Child = previewImage;
        previewBorder.Bind(Border.IsVisibleProperty, new Avalonia.Data.Binding("GlbPreviewImage")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<object?, bool>(obj => obj != null)
        });
        panel.Children.Add(previewBorder);

        // Header
        var header = new TextBlock
        {
            Text = "Linked Textures",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
            Margin = new Thickness(0, 0, 0, 4)
        };
        panel.Children.Add(header);

        // Texture list
        var textureList = new ItemsControl();
        textureList.Bind(ItemsControl.ItemsSourceProperty, new Avalonia.Data.Binding("GlbLinkedTextures"));
        textureList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<GlbLinkedTexture>((texture, _) =>
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 2)
            };

            // Status indicator - use existing teal/red palette
            var statusDot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center,
                Background = texture.IsFound
                    ? new SolidColorBrush(Color.Parse("#8ECDC8")) // Teal for found/embedded
                    : new SolidColorBrush(Color.Parse("#FF8888")) // Red for missing
            };
            row.Children.Add(statusDot);

            // Material name badge
            var materialBadge = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#2A3A4A")),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1),
                VerticalAlignment = VerticalAlignment.Center
            };
            materialBadge.Child = new TextBlock
            {
                Text = texture.MaterialName,
                Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
                FontSize = 10
            };
            row.Children.Add(materialBadge);

            // Texture type
            var typeText = new TextBlock
            {
                Text = texture.TextureType,
                Foreground = Brushes.White,
                Opacity = 0.7,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Children.Add(typeText);

            // Status text - use existing teal/red palette
            var statusText = new TextBlock
            {
                Text = texture.IsEmbedded ? "(embedded)" : texture.IsFound ? "(linked)" : "(missing)",
                Foreground = texture.IsFound
                    ? new SolidColorBrush(Color.Parse("#8ECDC8")) // Teal for found
                    : new SolidColorBrush(Color.Parse("#FF8888")), // Red for missing
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Children.Add(statusText);

            // Navigate button (only if found)
            if (texture.IsFound && !texture.IsEmbedded)
            {
                var navButton = new Button
                {
                    Content = "→",
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(4, 0),
                    FontSize = 14,
                    Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                    VerticalAlignment = VerticalAlignment.Center
                };
                navButton.Click += (_, _) =>
                {
                    if (DataContext is AssetBrowserViewModel vm)
                        vm.NavigateToLinkedTexture(texture);
                };
                ToolTip.SetTip(navButton, "Navigate to texture");
                row.Children.Add(navButton);
            }

            return row;
        });
        panel.Children.Add(textureList);

        // Export button
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var exportButton = new Button
        {
            Content = "Export Packaged GLB",
            FontSize = 11
        };
        exportButton.Classes.Add("primary");
        exportButton.Click += async (_, _) =>
        {
            if (DataContext is AssetBrowserViewModel vm)
            {
                var outputPath = await vm.ExportPackagedGlbAsync();
                if (outputPath != null)
                {
                    // Open containing folder
                    var folderPath = System.IO.Path.GetDirectoryName(outputPath);
                    if (folderPath != null)
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = folderPath,
                                UseShellExecute = true
                            });
                        }
                        catch { }
                    }
                }
            }
        };
        ToolTip.SetTip(exportButton, "Export GLB with all linked textures embedded");
        buttonPanel.Children.Add(exportButton);

        var importButton = new Button
        {
            Content = "Import Edited GLB",
            FontSize = 11
        };
        importButton.Classes.Add("secondary");
        importButton.Click += async (_, _) =>
        {
            if (DataContext is AssetBrowserViewModel vm)
            {
                var dialog = new Avalonia.Controls.OpenFileDialog
                {
                    Title = "Import Edited GLB",
                    AllowMultiple = false,
                    Filters = new List<FileDialogFilter>
                    {
                        new FileDialogFilter { Name = "GLB Files", Extensions = { "glb" } }
                    }
                };

                var window = TopLevel.GetTopLevel(this) as Window;
                if (window != null)
                {
                    var result = await dialog.ShowAsync(window);
                    if (result?.Length > 0)
                    {
                        await vm.ImportGlbAsync(result[0]);
                    }
                }
            }
        };
        ToolTip.SetTip(importButton, "Import edited GLB and extract textures back");
        buttonPanel.Children.Add(importButton);

        panel.Children.Add(buttonPanel);

        return panel;
    }

    /// <summary>
    /// Creates an image preview with dimension labels on the borders.
    /// Shows width label on top, height label on left side.
    /// </summary>
    private Control BuildImageWithDimensionBorder(
        string imageBinding,
        string widthBinding,
        string heightBinding,
        string visibilityBinding)
    {
        // Outer container: column for width label + row for height label + image
        var outerStack = new StackPanel { Spacing = 4 };
        outerStack.Bind(StackPanel.IsVisibleProperty, new Avalonia.Data.Binding(visibilityBinding));

        // Width label (centered above image)
        var widthLabel = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
            FontSize = 10,
            FontFamily = new FontFamily("monospace"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(24, 0, 0, 0) // offset to account for height label space
        };
        widthLabel.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(widthBinding)
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<int, string>(w => $"{w}px")
        });
        outerStack.Children.Add(widthLabel);

        // Row: height label + image with border
        var imageRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4
        };

        // Height label (rotated, on left side)
        var heightContainer = new Border
        {
            Width = 20,
            VerticalAlignment = VerticalAlignment.Center
        };
        var heightLabel = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
            FontSize = 10,
            FontFamily = new FontFamily("monospace"),
            RenderTransform = new RotateTransform(-90),
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative)
        };
        heightLabel.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(heightBinding)
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<int, string>(h => $"{h}px")
        });
        heightContainer.Child = heightLabel;
        imageRow.Children.Add(heightContainer);

        // Image with subtle border
        var imageBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.Parse("#0A0A0A")),
            Padding = new Thickness(2)
        };

        var imagePreview = new Image
        {
            MaxWidth = 350,
            MaxHeight = 350,
            Stretch = Avalonia.Media.Stretch.Uniform
        };
        imagePreview.Bind(Image.SourceProperty, new Avalonia.Data.Binding(imageBinding));
        imageBorder.Child = imagePreview;
        imageRow.Children.Add(imageBorder);

        outerStack.Children.Add(imageRow);
        return outerStack;
    }

    private Control BuildModifiedPanel()
    {
        var panel = new Grid
        {
            Margin = new Thickness(12, 0, 0, 0),
            RowDefinitions = new RowDefinitions("Auto,*,Auto")
        };

        var header = new TextBlock
        {
            Text = "Modified",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 12)
        };
        panel.Children.Add(header);
        Grid.SetRow(header, 0);

        var previewBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16)
        };

        var previewContainer = new Panel();

        // Modified preview content
        var previewStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        // Modified image preview with dimension border
        var modImageContainer = BuildImageWithDimensionBorder(
            "ModifiedPreviewImage",
            "ModifiedImageWidth",
            "ModifiedImageHeight",
            "HasModifiedImagePreview");
        previewStack.Children.Add(modImageContainer);

        // Dimension mismatch warning
        var mismatchWarning = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#4b2020")),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4),
            Margin = new Thickness(24, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var mismatchText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#FF8888")),
            FontSize = 11,
            Text = "⚠ Dimensions don't match vanilla"
        };
        mismatchWarning.Child = mismatchText;
        // Bind visibility: show when both have images AND dimensions differ
        mismatchWarning.Bind(Border.IsVisibleProperty, new Avalonia.Data.MultiBinding
        {
            Bindings =
            {
                new Avalonia.Data.Binding("HasImagePreview"),
                new Avalonia.Data.Binding("HasModifiedImagePreview"),
                new Avalonia.Data.Binding("VanillaImageWidth"),
                new Avalonia.Data.Binding("VanillaImageHeight"),
                new Avalonia.Data.Binding("ModifiedImageWidth"),
                new Avalonia.Data.Binding("ModifiedImageHeight")
            },
            Converter = new DimensionMismatchConverter()
        });
        previewStack.Children.Add(mismatchWarning);

        // Modified text preview
        var modTextScrollViewer = new ScrollViewer
        {
            MaxHeight = 400,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var modTextPreview = new TextBlock
        {
            Foreground = Brushes.White,
            FontFamily = new FontFamily("monospace"),
            FontSize = 12,
            TextWrapping = TextWrapping.NoWrap
        };
        modTextPreview.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("ModifiedPreviewText"));
        modTextPreview.Bind(TextBlock.IsVisibleProperty, new Avalonia.Data.Binding("HasModifiedTextPreview"));
        modTextScrollViewer.Content = modTextPreview;
        modTextScrollViewer.Bind(ScrollViewer.IsVisibleProperty, new Avalonia.Data.Binding("HasModifiedTextPreview"));
        previewStack.Children.Add(modTextScrollViewer);

        // Info text under modified image
        var modInfoText = new TextBlock
        {
            Foreground = Brushes.White,
            Opacity = 0.7,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(24, 8, 0, 0)
        };
        modInfoText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("ModifiedPreviewText"));
        modInfoText.Bind(TextBlock.IsVisibleProperty, new Avalonia.Data.Binding("HasModifiedImagePreview"));
        previewStack.Children.Add(modInfoText);

        previewStack.Bind(StackPanel.IsVisibleProperty, new Avalonia.Data.Binding("HasModifiedReplacement"));

        // No replacement state
        var noReplacementStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 12
        };
        noReplacementStack.Children.Add(new TextBlock
        {
            Text = "No replacement",
            FontSize = 14,
            Foreground = Brushes.White,
            Opacity = 0.6,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        noReplacementStack.Bind(StackPanel.IsVisibleProperty, new Avalonia.Data.Binding("HasModifiedReplacement")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<bool, bool>(b => !b)
        });

        previewContainer.Children.Add(previewStack);
        previewContainer.Children.Add(noReplacementStack);

        previewBorder.Child = previewContainer;
        panel.Children.Add(previewBorder);
        Grid.SetRow(previewBorder, 1);

        // Action buttons
        var actionStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var replaceButton = new Button
        {
            Content = "Replace Asset...",
            FontSize = 13
        };
        replaceButton.Classes.Add("primary");
        replaceButton.Click += OnReplaceAssetClick;
        replaceButton.Bind(Button.IsEnabledProperty, new Avalonia.Data.Binding("SelectedNode")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<object?, bool>(obj =>
                obj is AssetTreeNode node && node.IsFile)
        });
        actionStack.Children.Add(replaceButton);

        var clearButton = new Button
        {
            Content = "Clear Replacement",
            FontSize = 13
        };
        clearButton.Classes.Add("secondary");
        clearButton.Click += OnClearReplacementClick;
        clearButton.Bind(Button.IsEnabledProperty, new Avalonia.Data.Binding("HasModifiedReplacement"));
        actionStack.Children.Add(clearButton);

        var exportAssetButton = new Button
        {
            Content = "Export...",
            FontSize = 13
        };
        exportAssetButton.Classes.Add("secondary");
        exportAssetButton.Click += OnExportAssetClick;
        exportAssetButton.Bind(Button.IsEnabledProperty, new Avalonia.Data.Binding("SelectedNode")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<object?, bool>(obj =>
                obj is AssetTreeNode node && node.IsFile)
        });
        actionStack.Children.Add(exportAssetButton);

        panel.Children.Add(actionStack);
        Grid.SetRow(actionStack, 2);

        return panel;
    }

    private async void OnReplaceAssetClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is AssetBrowserViewModel vm && vm.SelectedNode?.IsFile == true)
        {
            var dialog = new OpenFileDialog
            {
                Title = $"Select replacement for {vm.SelectedNode.Name}"
            };

            var ext = System.IO.Path.GetExtension(vm.SelectedNode.Name).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp")
            {
                dialog.Filters = new System.Collections.Generic.List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "Images", Extensions = { "png", "jpg", "jpeg", "bmp" } },
                    new FileDialogFilter { Name = "All Files", Extensions = { "*" } }
                };
            }

            if (this.VisualRoot is Window window)
            {
                var files = await dialog.ShowAsync(window);
                if (files != null && files.Length > 0)
                {
                    vm.ReplaceAssetInModpack(files[0]);
                }
            }
        }
    }

    private async void OnClearReplacementClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is AssetBrowserViewModel vm && vm.SelectedNode != null)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is not Window window) return;

            var confirmed = await ConfirmationDialog.ShowAsync(
                window,
                "Clear Replacement",
                $"Remove the custom asset replacement for '{vm.SelectedNode.Name}'?",
                "Clear",
                isDestructive: true
            );

            if (confirmed)
                vm.ClearAssetReplacement();
        }
    }

    private async void OnExportAssetClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is AssetBrowserViewModel vm && vm.SelectedNode?.IsFile == true)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export asset",
                DefaultExtension = System.IO.Path.GetExtension(vm.SelectedNode.Name),
                InitialFileName = vm.SelectedNode.Name
            };

            if (this.VisualRoot is Window window)
            {
                var file = await dialog.ShowAsync(window);
                if (file != null)
                {
                    vm.ExportAsset(file);
                }
            }
        }
    }

    private async void OnExtractAssetsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is AssetBrowserViewModel vm)
        {
            await vm.ExtractAssetsAsync();
        }
    }
}

/// <summary>
/// Converter that returns true when image dimensions don't match.
/// Expects 6 values: hasVanilla, hasModified, vanillaW, vanillaH, modifiedW, modifiedH
/// </summary>
public class DimensionMismatchConverter : Avalonia.Data.Converters.IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (values.Count < 6)
            return false;

        var hasVanilla = values[0] is bool hv && hv;
        var hasModified = values[1] is bool hm && hm;

        if (!hasVanilla || !hasModified)
            return false;

        var vanillaW = values[2] is int vw ? vw : 0;
        var vanillaH = values[3] is int vh ? vh : 0;
        var modifiedW = values[4] is int mw ? mw : 0;
        var modifiedH = values[5] is int mh ? mh : 0;

        return vanillaW != modifiedW || vanillaH != modifiedH;
    }
}
