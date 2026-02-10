using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Menace.Modkit.App.Controls;
using Menace.Modkit.App.Converters;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;
using Menace.Modkit.App.ViewModels;
using ReactiveUI;

namespace Menace.Modkit.App.Views;

public class DocsView : UserControl
{
    private Panel? _contentContainer;

    public DocsView()
    {
        Content = BuildUI();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext is DocsViewModel vm)
        {
            // Try multiple paths to find docs folder
            var possiblePaths = new[]
            {
                // Next to executable (deployed)
                System.IO.Path.Combine(AppContext.BaseDirectory, "docs"),
                // Development: relative to working directory
                System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "docs"),
                // Development: up from bin folder
                System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs"))
            };

            foreach (var path in possiblePaths)
            {
                if (System.IO.Directory.Exists(path))
                {
                    vm.Initialize(path);
                    break;
                }
            }
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is DocsViewModel vm && _contentContainer != null)
        {
            // Set up navigation callback for internal doc links
            SimpleMarkdownRenderer.OnNavigateToDocument = relativePath =>
            {
                vm.NavigateToRelativePath(relativePath);
            };

            vm.WhenAnyValue(x => x.MarkdownContent)
                .Subscribe(content =>
                {
                    _contentContainer.Children.Clear();
                    if (!string.IsNullOrEmpty(content))
                    {
                        _contentContainer.Children.Add(SimpleMarkdownRenderer.Render(content));
                    }
                });
        }
    }

    private Control BuildUI()
    {
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("300,4,*")
        };

        // Left panel: Document tree (darker panel)
        var leftWrapper = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#141414")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = BuildDocTreePanel()
        };
        mainGrid.Children.Add(leftWrapper);
        Grid.SetColumn(leftWrapper, 0);

        // Splitter
        var splitter = new GridSplitter
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
            ResizeDirection = GridResizeDirection.Columns
        };
        mainGrid.Children.Add(splitter);
        Grid.SetColumn(splitter, 1);

        // Right panel: Document content (lighter panel)
        mainGrid.Children.Add(BuildContentPanel());
        Grid.SetColumn((Control)mainGrid.Children[2], 2);

        return mainGrid;
    }

    private Control BuildDocTreePanel()
    {
        var border = new Border();  // No background - parent wrapper has it

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*")
        };

        // Row 0: Search box
        var searchBox = new TextBox
        {
            Watermark = "Search docs... (3+ chars or Enter)",
            Margin = new Thickness(8, 8, 8, 12)
        };
        searchBox.Classes.Add("search");
        searchBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SearchText"));
        searchBox.KeyDown += (s, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter && DataContext is DocsViewModel vm)
                vm.ExecuteSearch();
        };
        grid.Children.Add(searchBox);
        Grid.SetRow(searchBox, 0);

        // Row 1: Toggle between Expand/Collapse buttons and Sort dropdown
        var buttonContainer = new Panel();

        // Expand/Collapse buttons (shown when not searching)
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
            if (DataContext is DocsViewModel vm)
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
            if (DataContext is DocsViewModel vm)
                vm.CollapseAll();
        };
        buttonPanel.Children.Add(collapseAllButton);

        buttonContainer.Children.Add(buttonPanel);

        // Section filter + Sort panel (shown when searching)
        var searchControlsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
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
            new Avalonia.Data.Binding("SelectedSectionFilter") { Mode = Avalonia.Data.BindingMode.TwoWay });
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
        sortCombo.Classes.Add("input");
        sortCombo.Items.Add("Relevance");
        sortCombo.Items.Add("Name A-Z");
        sortCombo.Items.Add("Name Z-A");
        sortCombo.Items.Add("Path A-Z");
        sortCombo.Items.Add("Path Z-A");
        sortCombo.SelectedIndex = 0;
        sortCombo.SelectionChanged += (s, e) =>
        {
            if (sortCombo.SelectedIndex >= 0 && DataContext is DocsViewModel vm)
                vm.CurrentSortOption = (SearchPanelBuilder.SortOption)sortCombo.SelectedIndex;
        };
        searchControlsPanel.Children.Add(sortCombo);

        buttonContainer.Children.Add(searchControlsPanel);

        grid.Children.Add(buttonContainer);
        Grid.SetRow(buttonContainer, 1);

        // Row 2: Toggle between TreeView and Search Results ListBox
        var contentContainer = new Panel();

        // Document tree (shown when not searching)
        var treeView = new TreeView
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8),
            ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Panel?>(() => new StackPanel())
        };
        treeView.Bind(TreeView.ItemsSourceProperty,
            new Avalonia.Data.Binding("DocTree"));
        treeView.Bind(TreeView.SelectedItemProperty,
            new Avalonia.Data.Binding("SelectedNode") { Mode = Avalonia.Data.BindingMode.TwoWay });
        treeView.Bind(TreeView.IsVisibleProperty,
            new Avalonia.Data.Binding("IsSearching") { Converter = BoolInverseConverter.Instance });
        treeView.ItemTemplate = CreateDocTreeTemplate();
        treeView.SelectionChanged += OnTreeSelectionChanged;
        treeView.ContainerPrepared += OnTreeContainerPrepared;

        contentContainer.Children.Add(treeView);

        // Search Results ListBox (shown when searching)
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
                DataContext is DocsViewModel vm)
            {
                vm.SelectSearchResult(item);
            }
        };

        // Double-click to select and exit search mode
        searchResultsList.DoubleTapped += (s, e) =>
        {
            if (searchResultsList.SelectedItem is SearchResultItem item &&
                DataContext is DocsViewModel vm)
            {
                vm.SelectAndExitSearch(item);
            }
        };

        contentContainer.Children.Add(searchResultsList);

        var scrollViewer = new ScrollViewer { Content = contentContainer };
        grid.Children.Add(scrollViewer);
        Grid.SetRow(scrollViewer, 2);

        border.Child = grid;
        return border;
    }

    private Avalonia.Controls.Templates.ITreeDataTemplate CreateDocTreeTemplate()
    {
        return new Avalonia.Controls.Templates.FuncTreeDataTemplate<DocTreeNode>(
            (node, _) =>
            {
                // Just the name - TreeViewItem provides the expansion chevron
                var nameBlock = new TextBlock
                {
                    Text = node.Name,
                    FontSize = 12,
                    Foreground = Brushes.White,
                    FontWeight = node.IsFile ? FontWeight.Normal : FontWeight.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 8)
                };
                return nameBlock;
            },
            node => node.Children);
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is TreeView treeView && treeView.SelectedItem is DocTreeNode node)
        {
            if (DataContext is DocsViewModel vm)
                vm.SelectedNode = node;
        }
    }

    private void OnTreeContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is TreeViewItem tvi && tvi.DataContext is DocTreeNode nodeVm)
        {
            tvi.IsExpanded = nodeVm.IsExpanded;
            tvi.Bind(TreeViewItem.IsExpandedProperty,
                new Avalonia.Data.Binding("IsExpanded")
                {
                    Mode = Avalonia.Data.BindingMode.TwoWay
                });
        }
    }

    private Control BuildContentPanel()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1A1A1A")),
            Padding = new Thickness(24)
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*")
        };

        // Title
        var titleText = new TextBlock
        {
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 16)
        };
        titleText.Bind(TextBlock.TextProperty,
            new Avalonia.Data.Binding("SelectedTitle"));
        grid.Children.Add(titleText);
        Grid.SetRow(titleText, 0);

        // Content container for rendered markdown
        var scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        };

        _contentContainer = new StackPanel();
        scrollViewer.Content = _contentContainer;
        grid.Children.Add(scrollViewer);
        Grid.SetRow(scrollViewer, 1);

        // Empty state
        var emptyState = new TextBlock
        {
            Text = "Select a document to view",
            Foreground = Brushes.White,
            Opacity = 0.5,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        emptyState.Bind(TextBlock.IsVisibleProperty,
            new Avalonia.Data.Binding("HasContent")
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<bool, bool>(v => !v)
            });

        var overlayGrid = new Grid();
        overlayGrid.Children.Add(grid);
        overlayGrid.Children.Add(emptyState);

        border.Child = overlayGrid;
        return border;
    }
}
