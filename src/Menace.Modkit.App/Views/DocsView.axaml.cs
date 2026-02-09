using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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
            ColumnDefinitions = new ColumnDefinitions("280,*")
        };

        // Left panel: Document tree
        mainGrid.Children.Add(BuildDocTreePanel());
        Grid.SetColumn((Control)mainGrid.Children[0], 0);

        // Right panel: Document content
        mainGrid.Children.Add(BuildContentPanel());
        Grid.SetColumn((Control)mainGrid.Children[1], 1);

        return mainGrid;
    }

    private Control BuildDocTreePanel()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1A1A1A")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
            BorderThickness = new Thickness(0, 0, 1, 0)
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*")
        };

        // Row 0: Header
        var header = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252525")),
            Padding = new Thickness(16, 12)
        };
        header.Child = new TextBlock
        {
            Text = "Documentation",
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        };
        grid.Children.Add(header);
        Grid.SetRow(header, 0);

        // Row 1: Search box
        var searchBox = new TextBox
        {
            Watermark = "Search docs...",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8),
            Margin = new Thickness(8, 8, 8, 12)
        };
        searchBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SearchText"));
        grid.Children.Add(searchBox);
        Grid.SetRow(searchBox, 1);

        // Row 2: Expand/Collapse buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8, 4, 8, 12)
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
            if (DataContext is DocsViewModel vm)
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
            if (DataContext is DocsViewModel vm)
                vm.CollapseAll();
        };
        buttonPanel.Children.Add(collapseAllButton);

        grid.Children.Add(buttonPanel);
        Grid.SetRow(buttonPanel, 2);

        // Row 3: Document tree
        var treeView = new TreeView
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8),
            ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Panel?>(() => new StackPanel())
        };
        treeView.Bind(TreeView.ItemsSourceProperty,
            new Avalonia.Data.Binding("DocTree"));
        treeView.ItemTemplate = CreateDocTreeTemplate();
        treeView.SelectionChanged += OnTreeSelectionChanged;
        treeView.ContainerPrepared += OnTreeContainerPrepared;

        var scrollViewer = new ScrollViewer { Content = treeView };
        grid.Children.Add(scrollViewer);
        Grid.SetRow(scrollViewer, 3);

        border.Child = grid;
        return border;
    }

    private Avalonia.Controls.Templates.ITreeDataTemplate CreateDocTreeTemplate()
    {
        return new Avalonia.Controls.Templates.FuncTreeDataTemplate<DocTreeNode>(
            (node, _) =>
            {
                var panel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Margin = new Thickness(4, 8)
                };

                // Icon for folder/file
                var icon = new TextBlock
                {
                    Text = node.IsFile ? "\ud83d\udcc4" : "\ud83d\udcc1",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.Parse("#888888")),
                    VerticalAlignment = VerticalAlignment.Center
                };
                panel.Children.Add(icon);

                // Name
                var nameBlock = new TextBlock
                {
                    FontSize = 12,
                    Foreground = Brushes.White,
                    FontWeight = node.IsFile ? FontWeight.Normal : FontWeight.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                nameBlock.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("Name"));
                panel.Children.Add(nameBlock);

                return panel;
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
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
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
