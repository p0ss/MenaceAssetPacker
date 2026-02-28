using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Menace.Modkit.App.Controls;
using Menace.Modkit.App.Converters;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;
using Menace.Modkit.App.ViewModels;
using ReactiveUI;

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

        var addAssetButton = new Button
        {
            Content = "Add Asset",
            FontSize = 11
        };
        addAssetButton.Classes.Add("secondary");
        addAssetButton.Bind(Button.IsEnabledProperty,
            new Avalonia.Data.Binding("CanAddAsset"));
        addAssetButton.Click += async (_, _) =>
        {
            if (DataContext is AssetBrowserViewModel vm)
                await AddAssetAsync(vm);
        };
        buttonPanel.Children.Add(addAssetButton);

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
        var isHandlingCreateNew = false;
        modpackCombo.SelectionChanged += async (sender, e) =>
        {
            if (isHandlingCreateNew) return;
            if (sender is ComboBox combo &&
                combo.SelectedItem is string selected &&
                selected == AssetBrowserViewModel.CreateNewModOption)
            {
                isHandlingCreateNew = true;
                try
                {
                    // Clear selection immediately to prevent re-triggering
                    var vm = DataContext as AssetBrowserViewModel;

                    // Set to first real modpack or null
                    if (vm != null && vm.AvailableModpacks.Count > 1)
                        vm.CurrentModpackName = vm.AvailableModpacks[1];
                    else if (vm != null)
                        vm.CurrentModpackName = null;

                    await ShowCreateModpackDialogAsync();
                }
                finally
                {
                    isHandlingCreateNew = false;
                }
            }
        };
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

        // Spacer
        toolbar.Children.Add(new Border { Width = 1 });

        // Model Replacement Wizard button
        var modelWizardButton = new Button
        {
            Content = "Model Wizard...",
            FontSize = 12,
            Padding = new Thickness(12, 6)
        };
        modelWizardButton.Click += OnModelWizardClick;
        toolbar.Children.Add(modelWizardButton);

        outerGrid.Children.Add(toolbar);
        Grid.SetRow(toolbar, 0);

        // Row 1: Content container for either bulk editor or file preview panels
        var contentContainer = new Panel();

        // Build the bulk editor panel (shown when folder selected)
        // Wrap in a container so visibility binding doesn't break when BulkEditorPanel's DataContext changes
        var bulkEditorWrapper = new Border();
        bulkEditorWrapper.Bind(Control.IsVisibleProperty,
            new Avalonia.Data.Binding("SelectedNode")
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<object?, bool>(obj =>
                    obj is AssetTreeNode node && !node.IsFile && node.Children.Count > 0)
            });
        var thumbnailGrid = BuildAssetThumbnailGrid();
        bulkEditorWrapper.Child = thumbnailGrid;
        contentContainer.Children.Add(bulkEditorWrapper);

        // Build the file preview panels (shown when file selected)
        var previewPanels = BuildFilePreviewPanels();
        previewPanels.Bind(Control.IsVisibleProperty,
            new Avalonia.Data.Binding("SelectedNode")
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<object?, bool>(obj =>
                    obj is AssetTreeNode node && node.IsFile)
            });
        contentContainer.Children.Add(previewPanels);

        // Empty state (when no selection or empty folder)
        var emptyState = BuildAssetEmptyState();
        emptyState.Bind(Control.IsVisibleProperty,
            new Avalonia.Data.Binding("SelectedNode")
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<object?, bool>(obj =>
                    obj == null || (obj is AssetTreeNode node && !node.IsFile && node.Children.Count == 0))
            });
        contentContainer.Children.Add(emptyState);

        outerGrid.Children.Add(contentContainer);
        Grid.SetRow(contentContainer, 1);

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

    private Control BuildAssetEmptyState()
    {
        return new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "Select an asset file or folder to view details",
                    FontSize = 14,
                    Foreground = Brushes.White,
                    Opacity = 0.6
                }
            }
        };
    }

    private Control BuildAssetThumbnailGrid()
    {
        var outerPanel = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*")
        };

        // Header showing folder path and count
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 12),
            Spacing = 12
        };

        var folderNameText = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        };
        header.Children.Add(folderNameText);

        var countText = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(countText);

        // Legend for highlighting
        var legendPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(24, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        legendPanel.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#8ECDC8")),
            Width = 12,
            Height = 12,
            CornerRadius = new CornerRadius(2)
        });
        legendPanel.Children.Add(new TextBlock
        {
            Text = "= Has replacement",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(legendPanel);

        outerPanel.Children.Add(header);
        Grid.SetRow(header, 0);

        // Thumbnail grid using ItemsControl with WrapPanel
        var itemsControl = new ItemsControl();
        var scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = itemsControl
        };

        // Use WrapPanel for layout
        itemsControl.ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Panel?>(() =>
            new WrapPanel { Orientation = Orientation.Horizontal });

        outerPanel.Children.Add(scrollViewer);
        Grid.SetRow(scrollViewer, 1);

        IDisposable? currentSubscription = null;
        AssetTreeNode? lastLoadedNode = null;

        // Subscribe to selection changes
        this.GetObservable(DataContextProperty).Subscribe(dc =>
        {
            currentSubscription?.Dispose();
            currentSubscription = null;
            lastLoadedNode = null;

            if (dc is AssetBrowserViewModel vm)
            {
                currentSubscription = vm.WhenAnyValue(x => x.SelectedNode).Subscribe(node =>
                {
                    if (node is AssetTreeNode treeNode && !treeNode.IsFile && treeNode.Children.Count > 0)
                    {
                        if (ReferenceEquals(treeNode, lastLoadedNode))
                            return;
                        lastLoadedNode = treeNode;

                        // Update header
                        folderNameText.Text = treeNode.FullPath;
                        var files = GetAllFilesInFolder(treeNode).ToList();
                        countText.Text = $"({files.Count} files)";

                        // Build thumbnail items
                        var items = new List<Control>();
                        foreach (var file in files)
                        {
                            var hasReplacement = vm.HasModpackReplacement(file.FullPath);
                            items.Add(CreateAssetThumbnail(file, hasReplacement, vm));
                        }
                        itemsControl.ItemsSource = items;
                    }
                });
            }
        });

        return outerPanel;
    }

    private Control CreateAssetThumbnail(AssetTreeNode file, bool hasReplacement, AssetBrowserViewModel vm)
    {
        var card = new Border
        {
            Width = 100,
            Height = 120,
            Margin = new Thickness(4),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.Parse("#252525")),
            BorderThickness = new Thickness(2),
            BorderBrush = hasReplacement
                ? new SolidColorBrush(Color.Parse("#8ECDC8"))  // Teal for replaced
                : new SolidColorBrush(Color.Parse("#3E3E3E")), // Grey for normal
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Check if this is an image file we can show a thumbnail for
        var ext = System.IO.Path.GetExtension(file.Name).ToLowerInvariant();
        var isImage = ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif";

        if (isImage && System.IO.File.Exists(file.FullPath))
        {
            try
            {
                // Load image thumbnail
                var image = new Avalonia.Controls.Image
                {
                    Width = 80,
                    Height = 80,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(4)
                };

                // Load asynchronously to avoid blocking UI
                _ = LoadImageThumbnailAsync(image, file.FullPath);

                stack.Children.Add(image);
            }
            catch
            {
                // Fall back to icon if image load fails
                AddIconToStack(stack, file.Name, hasReplacement);
            }
        }
        else
        {
            // Use icon for non-image files
            AddIconToStack(stack, file.Name, hasReplacement);
        }

        // File name
        var nameText = new TextBlock
        {
            Text = file.Name,
            FontSize = 10,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 90,
            Margin = new Thickness(4, 4, 4, 4),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        stack.Children.Add(nameText);

        card.Child = stack;

        // Click to select this file
        card.PointerPressed += (s, e) =>
        {
            vm.SelectedNode = file;
        };

        // Tooltip with full path
        ToolTip.SetTip(card, file.FullPath + (hasReplacement ? "\n(Has modpack replacement)" : ""));

        return card;
    }

    private static void AddIconToStack(StackPanel stack, string fileName, bool hasReplacement)
    {
        var icon = new TextBlock
        {
            Text = GetFileTypeIcon(fileName),
            FontSize = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 16, 0, 8),
            Foreground = hasReplacement
                ? new SolidColorBrush(Color.Parse("#8ECDC8"))
                : new SolidColorBrush(Color.Parse("#888888"))
        };
        stack.Children.Add(icon);
    }

    private static async System.Threading.Tasks.Task LoadImageThumbnailAsync(Avalonia.Controls.Image imageControl, string filePath)
    {
        try
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                using var stream = System.IO.File.OpenRead(filePath);
                var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);

                // Dispatch to UI thread
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    imageControl.Source = bitmap;
                });
            });
        }
        catch (Exception ex)
        {
            Services.ModkitLog.Warn($"Failed to load thumbnail for {filePath}: {ex.Message}");
        }
    }

    private static string GetFileTypeIcon(string fileName)
    {
        var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tga" or ".dds" => "🖼",
            ".wav" or ".ogg" or ".mp3" or ".aiff" => "🔊",
            ".glb" or ".gltf" or ".fbx" or ".obj" or ".mesh" => "🎲",
            ".json" or ".xml" or ".txt" or ".cfg" => "📄",
            ".mat" or ".shader" => "🎨",
            ".prefab" => "📦",
            ".anim" or ".controller" => "🎬",
            ".asset" => "⚙️",
            _ => "📁"
        };
    }

    private IEnumerable<AssetTreeNode> GetAllFilesInFolder(AssetTreeNode folder)
    {
        foreach (var child in folder.Children)
        {
            if (child.IsFile)
            {
                yield return child;
            }
        }
    }

    private Control BuildFilePreviewPanels()
    {
        // Two-column Vanilla | Modified
        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*")
        };

        contentGrid.Children.Add(BuildVanillaPanel());
        Grid.SetColumn((Control)contentGrid.Children[0], 0);

        contentGrid.Children.Add(BuildModifiedPanel());
        Grid.SetColumn((Control)contentGrid.Children[1], 1);

        return contentGrid;
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

        var noTexturesText = new TextBlock
        {
            Text = "No linked textures detected",
            Foreground = Brushes.White,
            Opacity = 0.45,
            FontSize = 11,
            FontStyle = FontStyle.Italic
        };
        noTexturesText.Bind(TextBlock.IsVisibleProperty,
            new Avalonia.Data.Binding("GlbLinkedTextures.Count")
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<int, bool>(c => c == 0)
            });
        panel.Children.Add(noTexturesText);

        var prefabHeader = new TextBlock
        {
            Text = "Linked Prefabs",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
            Margin = new Thickness(0, 10, 0, 4)
        };
        panel.Children.Add(prefabHeader);

        var prefabList = new ItemsControl();
        prefabList.Bind(ItemsControl.ItemsSourceProperty, new Avalonia.Data.Binding("GlbPrefabMatches"));
        prefabList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<GlbPrefabMatch>((match, _) =>
        {
            var row = new StackPanel
            {
                Spacing = 2,
                Margin = new Thickness(0, 2)
            };

            var topLine = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };

            var confidenceColor = match.Confidence switch
            {
                GlbPrefabMatchConfidence.Verified => Color.Parse("#8ECDC8"),
                GlbPrefabMatchConfidence.Likely => Color.Parse("#E8CC77"),
                _ => Color.Parse("#A0A0A0")
            };
            var confidenceText = match.Confidence switch
            {
                GlbPrefabMatchConfidence.Verified => "VERIFIED",
                GlbPrefabMatchConfidence.Likely => "LIKELY?",
                _ => "HEURISTIC"
            };

            topLine.Children.Add(new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(confidenceColor)
            });

            var confidenceBadge = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#2A3A4A")),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = confidenceText,
                    Foreground = new SolidColorBrush(confidenceColor),
                    FontSize = 10
                }
            };
            topLine.Children.Add(confidenceBadge);

            topLine.Children.Add(new TextBlock
            {
                Text = match.PrefabName,
                Foreground = Brushes.White,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });

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
                    vm.NavigateToPrefabMatch(match);
            };
            ToolTip.SetTip(navButton, "Navigate to prefab");
            topLine.Children.Add(navButton);

            row.Children.Add(topLine);

            row.Children.Add(new TextBlock
            {
                Text = match.RelativePath,
                Foreground = Brushes.White,
                Opacity = 0.55,
                FontSize = 10
            });

            row.Children.Add(new TextBlock
            {
                Text = match.Evidence,
                Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
                Opacity = 0.9,
                FontSize = 10
            });

            return row;
        });
        panel.Children.Add(prefabList);

        var noPrefabsText = new TextBlock
        {
            Text = "No matching prefabs found",
            Foreground = Brushes.White,
            Opacity = 0.45,
            FontSize = 11,
            FontStyle = FontStyle.Italic
        };
        noPrefabsText.Bind(TextBlock.IsVisibleProperty,
            new Avalonia.Data.Binding("GlbPrefabMatches.Count")
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<int, bool>(c => c == 0)
            });
        panel.Children.Add(noPrefabsText);

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
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var result = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Import Edited GLB",
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType("GLB Files") { Patterns = new[] { "*.glb" } }
                        }
                    });

                    if (result.Count > 0)
                    {
                        await vm.ImportGlbAsync(result[0].Path.LocalPath);
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

        var addButton = new Button
        {
            Content = "Add Asset...",
            FontSize = 13
        };
        addButton.Classes.Add("primary");
        addButton.Click += OnAddAssetClick;
        addButton.Bind(Button.IsEnabledProperty, new MultiBinding
        {
            Bindings =
            {
                new Avalonia.Data.Binding("SelectedNode"),
                new Avalonia.Data.Binding("CurrentModpackName")
            },
            Converter = new FuncMultiValueConverter<object?, bool>(values =>
            {
                var list = values.ToList();
                if (list.Count < 2)
                    return false;
                var node = list[0] as AssetTreeNode;
                var modpackName = list[1] as string;
                return node != null
                    && !node.IsFile
                    && !string.IsNullOrWhiteSpace(modpackName)
                    && modpackName != AssetBrowserViewModel.CreateNewModOption;
            })
        });
        actionStack.Children.Add(addButton);

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

        var removeButton = new Button
        {
            Content = "Remove Asset",
            FontSize = 13
        };
        removeButton.Classes.Add("destructive");
        removeButton.Click += OnRemoveAssetClick;
        removeButton.Bind(Button.IsEnabledProperty, new Avalonia.Data.Binding("HasModifiedReplacement"));
        actionStack.Children.Add(removeButton);

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

    private async Task AddAssetAsync(AssetBrowserViewModel vm)
    {
        var targetFolder = vm.GetTargetFolderForAdd();
        if (targetFolder == null || string.IsNullOrWhiteSpace(vm.CurrentModpackName))
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return;

        // Only include file types that ModpackLoader actually supports at runtime:
        // - PNG/JPG: LoadTextureFromFile uses Unity's ImageConversion.LoadImage
        // - WAV: Full LoadWavFile implementation
        // - GLB: Full GlbLoader implementation
        // Note: OGG/MP3/GLTF/FBX/OBJ are categorized but not actually implemented
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Add asset to {targetFolder.Name}",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("All Supported") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.wav", "*.glb", "*.bundle" } },
                new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } },
                new FilePickerFileType("Audio") { Patterns = new[] { "*.wav" } },
                new FilePickerFileType("3D Models") { Patterns = new[] { "*.glb" } },
                new FilePickerFileType("Unity Bundles") { Patterns = new[] { "*.bundle" } }
            }
        });

        if (files.Count > 0)
            vm.AddAssetToModpackFolder(files[0].Path.LocalPath, targetFolder);
    }

    private async void OnAddAssetClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not AssetBrowserViewModel vm)
            return;
        if (vm.SelectedNode == null || vm.SelectedNode.IsFile || string.IsNullOrWhiteSpace(vm.CurrentModpackName))
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Add asset to {vm.SelectedNode.Name}",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (files.Count > 0)
            vm.AddAssetToModpackFolder(files[0].Path.LocalPath, vm.SelectedNode);
    }

    private async void OnReplaceAssetClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is AssetBrowserViewModel vm && vm.SelectedNode?.IsFile == true)
        {
            var ext = System.IO.Path.GetExtension(vm.SelectedNode.Name).ToLowerInvariant();
            var filters = new List<FilePickerFileType>();

            // Add appropriate filters based on asset type
            if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tga")
                filters.Add(new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.tga" } });
            else if (ext is ".glb" or ".gltf" or ".fbx" or ".obj")
                filters.Add(new FilePickerFileType("3D Models") { Patterns = new[] { "*.glb", "*.gltf", "*.fbx", "*.obj" } });
            else if (ext is ".wav" or ".ogg" or ".mp3")
                filters.Add(new FilePickerFileType("Audio") { Patterns = new[] { "*.wav", "*.ogg", "*.mp3" } });

            filters.Add(new FilePickerFileType("All Files") { Patterns = new[] { "*" } });

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = $"Select replacement for {vm.SelectedNode.Name}",
                    AllowMultiple = false,
                    FileTypeFilter = filters
                });

                if (files.Count > 0)
                {
                    vm.ReplaceAssetInModpack(files[0].Path.LocalPath);
                }
            }
        }
    }

    private async void OnRemoveAssetClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is AssetBrowserViewModel vm && vm.SelectedNode != null)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is not Window window) return;

            var confirmed = await ConfirmationDialog.ShowAsync(
                window,
                "Remove Asset",
                $"Remove '{vm.SelectedNode.Name}' from this modpack?\n\nIf this overrides a vanilla asset, the vanilla file will still be used.",
                "Remove",
                isDestructive: true
            );

            if (confirmed)
                vm.RemoveAssetFromModpack();
        }
    }

    private async void OnExportAssetClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is AssetBrowserViewModel vm && vm.SelectedNode?.IsFile == true)
        {
            var extension = System.IO.Path.GetExtension(vm.SelectedNode.Name);
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Export asset",
                    SuggestedFileName = vm.SelectedNode.Name,
                    DefaultExtension = extension,
                    ShowOverwritePrompt = true,
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("Asset File") { Patterns = new[] { $"*{extension}" } },
                        new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                    }
                });

                if (file != null)
                {
                    vm.ExportAsset(file.Path.LocalPath);
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

    private async void OnModelWizardClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is AssetBrowserViewModel vm)
        {
            var extractedPath = AppSettings.GetEffectiveAssetsPath();
            if (string.IsNullOrEmpty(extractedPath) || !System.IO.Directory.Exists(extractedPath))
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel is Window window)
                {
                    await ConfirmationDialog.ShowAsync(
                        window,
                        "Assets Not Extracted",
                        "Please extract game assets first using the 'Extract Assets' button.",
                        "OK",
                        isDestructive: false);
                }
                return;
            }

            var modpackManager = vm.GetModpackManager();
            if (modpackManager == null)
                return;

            var wizard = new ModelReplacementWizard(modpackManager, extractedPath);
            var parent = TopLevel.GetTopLevel(this) as Window;
            if (parent != null)
            {
                await wizard.ShowDialog(parent);
            }
        }
    }

    private async Task ShowCreateModpackDialogAsync()
    {
        try
        {
            if (DataContext is AssetBrowserViewModel vm)
            {
                var dialog = new CreateModpackDialog();
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel is Window window)
                {
                    var result = await dialog.ShowDialog<CreateModpackResult?>(window);
                    if (result != null)
                    {
                        vm.CreateModpack(result.Name, result.Author, result.Description);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Services.ModkitLog.Error($"Create modpack dialog failed: {ex.Message}");
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
