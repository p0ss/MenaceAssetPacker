using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Menace.Modkit.App.ViewModels;

namespace Menace.Modkit.App.Views;

public class AssetBrowserView : UserControl
{
    public AssetBrowserView()
    {
        Content = BuildUI();
    }

    private Control BuildUI()
    {
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,2*")
        };

        // Left: Asset Navigation Tree
        mainGrid.Children.Add(BuildNavigation());
        Grid.SetColumn((Control)mainGrid.Children[0], 0);

        // Right: Asset Viewer (Vanilla | Modified)
        mainGrid.Children.Add(BuildAssetViewer());
        Grid.SetColumn((Control)mainGrid.Children[1], 1);

        return mainGrid;
    }

    private Control BuildNavigation()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(12)
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto")
        };

        // Row 0: Search Box
        var searchBox = new TextBox
        {
            Watermark = "Search assets...",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8),
            Margin = new Thickness(0, 0, 0, 8)
        };
        searchBox.Bind(TextBox.TextProperty,
            new Avalonia.Data.Binding("SearchText"));
        grid.Children.Add(searchBox);
        Grid.SetRow(searchBox, 0);

        // Row 1: Expand/Collapse + Modpack Only buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var expandAllButton = new Button
        {
            Content = "Expand All",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            Padding = new Thickness(10, 4),
            FontSize = 11
        };
        expandAllButton.Click += (_, _) =>
        {
            if (DataContext is AssetBrowserViewModel vm)
                vm.ExpandAll();
        };
        buttonPanel.Children.Add(expandAllButton);

        var collapseAllButton = new Button
        {
            Content = "Collapse All",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            Padding = new Thickness(10, 4),
            FontSize = 11
        };
        collapseAllButton.Click += (_, _) =>
        {
            if (DataContext is AssetBrowserViewModel vm)
                vm.CollapseAll();
        };
        buttonPanel.Children.Add(collapseAllButton);

        var modpackOnlyToggle = new ToggleButton
        {
            Content = "Modpack Only",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            Padding = new Thickness(10, 4),
            FontSize = 11
        };
        modpackOnlyToggle.Bind(ToggleButton.IsCheckedProperty,
            new Avalonia.Data.Binding("ShowModpackOnly")
            {
                Mode = Avalonia.Data.BindingMode.TwoWay
            });
        buttonPanel.Children.Add(modpackOnlyToggle);

        grid.Children.Add(buttonPanel);
        Grid.SetRow(buttonPanel, 1);

        // Row 2: Asset Tree (non-virtualizing so Expand All works fully)
        var treeView = new TreeView
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Avalonia.Controls.Panel?>(() => new StackPanel())
        };
        treeView.Bind(TreeView.ItemsSourceProperty,
            new Avalonia.Data.Binding("FolderTree"));
        treeView.Bind(TreeView.SelectedItemProperty,
            new Avalonia.Data.Binding("SelectedNode"));

        // Tree item template: folders bold/13pt, files normal/12pt
        treeView.ItemTemplate = new Avalonia.Controls.Templates.FuncTreeDataTemplate<AssetTreeNode>(
            (node, _) => new TextBlock
            {
                Text = node.Name,
                FontWeight = node.IsFile ? FontWeight.Normal : FontWeight.SemiBold,
                Foreground = Brushes.White,
                FontSize = node.IsFile ? 12 : 13,
                Margin = new Thickness(4, 2)
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

        grid.Children.Add(treeView);
        Grid.SetRow(treeView, 2);

        // Row 3: Extraction Status
        var statusText = new SelectableTextBlock
        {
            Foreground = Brushes.White,
            Opacity = 0.8,
            FontSize = 11,
            Margin = new Thickness(0, 12, 0, 8),
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
            Background = new SolidColorBrush(Color.Parse("#064b48")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(16, 10),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontSize = 13
        };
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
            Background = new SolidColorBrush(Color.Parse("#121212")),
            Padding = new Thickness(24)
        };

        var outerGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*")
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
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            FontSize = 12
        };
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

        border.Child = outerGrid;
        return border;
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
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Image preview
        var imagePreview = new Image
        {
            MaxWidth = 400,
            MaxHeight = 400,
            Stretch = Avalonia.Media.Stretch.Uniform
        };
        imagePreview.Bind(Image.SourceProperty, new Avalonia.Data.Binding("PreviewImage"));
        imagePreview.Bind(Image.IsVisibleProperty, new Avalonia.Data.Binding("HasImagePreview"));
        previewStack.Children.Add(imagePreview);

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
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Modified image preview
        var modImagePreview = new Image
        {
            MaxWidth = 400,
            MaxHeight = 400,
            Stretch = Avalonia.Media.Stretch.Uniform
        };
        modImagePreview.Bind(Image.SourceProperty, new Avalonia.Data.Binding("ModifiedPreviewImage"));
        modImagePreview.Bind(Image.IsVisibleProperty, new Avalonia.Data.Binding("HasModifiedImagePreview"));
        previewStack.Children.Add(modImagePreview);

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
            Margin = new Thickness(0, 8, 0, 0)
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
            Background = new SolidColorBrush(Color.Parse("#064b48")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(20, 10),
            FontSize = 13
        };
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
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(20, 10),
            FontSize = 13
        };
        clearButton.Click += OnClearReplacementClick;
        clearButton.Bind(Button.IsEnabledProperty, new Avalonia.Data.Binding("HasModifiedReplacement"));
        actionStack.Children.Add(clearButton);

        var exportButton = new Button
        {
            Content = "Export...",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(20, 10),
            FontSize = 13
        };
        exportButton.Click += OnExportAssetClick;
        exportButton.Bind(Button.IsEnabledProperty, new Avalonia.Data.Binding("SelectedNode")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<object?, bool>(obj =>
                obj is AssetTreeNode node && node.IsFile)
        });
        actionStack.Children.Add(exportButton);

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

    private void OnClearReplacementClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is AssetBrowserViewModel vm)
        {
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
