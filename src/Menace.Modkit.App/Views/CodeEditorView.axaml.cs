using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.ViewModels;

namespace Menace.Modkit.App.Views;

/// <summary>
/// Code editor view: browse vanilla decompiled .cs (read-only) and per-modpack source files.
/// Left panel: tree views. Right panel: code viewer/editor. Bottom panel: build output.
/// </summary>
public class CodeEditorView : UserControl
{

    public CodeEditorView()
    {
        Content = BuildUI();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Refresh modpacks when view becomes visible
        if (DataContext is CodeEditorViewModel vm)
            vm.RefreshAll();
    }

    private Control BuildUI()
    {
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("280,*"),
            RowDefinitions = new RowDefinitions("*,Auto")
        };

        // Left panel: trees + toolbar
        var leftPanel = BuildLeftPanel();
        mainGrid.Children.Add(leftPanel);
        Grid.SetColumn(leftPanel, 0);
        Grid.SetRowSpan(leftPanel, 2);

        // Right panel: code editor
        var rightPanel = BuildRightPanel();
        mainGrid.Children.Add(rightPanel);
        Grid.SetColumn(rightPanel, 1);
        Grid.SetRow(rightPanel, 0);

        // Bottom panel: build output
        var bottomPanel = BuildBottomPanel();
        mainGrid.Children.Add(bottomPanel);
        Grid.SetColumn(bottomPanel, 1);
        Grid.SetRow(bottomPanel, 1);

        return mainGrid;
    }

    private Control BuildLeftPanel()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1A1A1A")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
            BorderThickness = new Thickness(0, 0, 1, 0)
        };

        // Use a Grid layout so TreeViews get proper height allocation
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,150,Auto,Auto,*")
        };

        // Row 0: Search box
        var searchBox = new TextBox
        {
            Watermark = "Search code...",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 8),
            Margin = new Thickness(8, 8, 8, 4)
        };
        searchBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SearchText"));
        grid.Children.Add(searchBox);
        Grid.SetRow(searchBox, 0);

        // Row 1: Expand/Collapse buttons
        var expandCollapsePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8, 4, 8, 4)
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
            if (DataContext is CodeEditorViewModel vm)
                vm.ExpandAll();
        };
        expandCollapsePanel.Children.Add(expandAllButton);

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
            if (DataContext is CodeEditorViewModel vm)
                vm.CollapseAll();
        };
        expandCollapsePanel.Children.Add(collapseAllButton);

        grid.Children.Add(expandCollapsePanel);
        Grid.SetRow(expandCollapsePanel, 1);

        // Row 2: Toolbar
        var toolbar = new StackPanel
        {
            Margin = new Thickness(8),
            Spacing = 8
        };

        var modpackLabel = new TextBlock
        {
            Text = "Modpack:",
            FontSize = 11,
            Foreground = Brushes.White,
            Opacity = 0.7
        };
        toolbar.Children.Add(modpackLabel);

        var modpackCombo = new ComboBox
        {
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontSize = 12
        };
        modpackCombo.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding("AvailableModpacks"));
        modpackCombo.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding("SelectedModpack") { Mode = Avalonia.Data.BindingMode.TwoWay });
        toolbar.Children.Add(modpackCombo);

        // Add/Remove file buttons
        var fileButtonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        var addButton = new Button
        {
            Content = "+ Add File",
            FontSize = 11,
            Background = new SolidColorBrush(Color.Parse("#064b48")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 4)
        };
        addButton.Click += OnAddFileClick;
        fileButtonRow.Children.Add(addButton);

        var removeButton = new Button
        {
            Content = "- Remove",
            FontSize = 11,
            Background = new SolidColorBrush(Color.Parse("#4b0606")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 4)
        };
        removeButton.Click += OnRemoveFileClick;
        fileButtonRow.Children.Add(removeButton);

        toolbar.Children.Add(fileButtonRow);
        grid.Children.Add(toolbar);
        Grid.SetRow(toolbar, 2);

        // Row 3: Separator
        var sep1 = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
            Margin = new Thickness(0, 4)
        };
        grid.Children.Add(sep1);
        Grid.SetRow(sep1, 3);

        // Row 4: Mod Source label
        var modSourceLabel = new TextBlock
        {
            Text = "Mod Sources",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(8, 8, 8, 4)
        };
        grid.Children.Add(modSourceLabel);
        Grid.SetRow(modSourceLabel, 4);

        // Row 5: Mod Source Tree (fixed height)
        var modTree = new TreeView
        {
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            Margin = new Thickness(8),
            ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Avalonia.Controls.Panel?>(() => new StackPanel())
        };
        modTree.Bind(TreeView.ItemsSourceProperty, new Avalonia.Data.Binding("ModSourceTree"));
        modTree.ItemTemplate = CreateCodeTreeTemplate();
        modTree.SelectionChanged += OnTreeSelectionChanged;
        modTree.ContainerPrepared += OnTreeContainerPrepared;

        var modTreeScroll = new ScrollViewer { Content = modTree };
        grid.Children.Add(modTreeScroll);
        Grid.SetRow(modTreeScroll, 5);

        // Row 6: Separator
        var sep2 = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
            Margin = new Thickness(0, 4)
        };
        grid.Children.Add(sep2);
        Grid.SetRow(sep2, 6);

        // Row 7: Vanilla Code label
        var vanillaLabel = new TextBlock
        {
            Text = "Vanilla Code (read-only)",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            Opacity = 0.7,
            Margin = new Thickness(8, 8, 8, 4)
        };
        grid.Children.Add(vanillaLabel);
        Grid.SetRow(vanillaLabel, 7);

        // Row 8: Vanilla Code Tree (takes remaining space)
        var vanillaTree = new TreeView
        {
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            Margin = new Thickness(8),
            ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Avalonia.Controls.Panel?>(() => new StackPanel())
        };
        vanillaTree.Bind(TreeView.ItemsSourceProperty, new Avalonia.Data.Binding("VanillaCodeTree"));
        vanillaTree.ItemTemplate = CreateCodeTreeTemplate();
        vanillaTree.SelectionChanged += OnTreeSelectionChanged;
        vanillaTree.ContainerPrepared += OnTreeContainerPrepared;

        var vanillaTreeScroll = new ScrollViewer { Content = vanillaTree };
        grid.Children.Add(vanillaTreeScroll);
        Grid.SetRow(vanillaTreeScroll, 8);

        border.Child = grid;
        return border;
    }

    private Avalonia.Controls.Templates.ITreeDataTemplate CreateCodeTreeTemplate()
    {
        return new Avalonia.Controls.Templates.FuncTreeDataTemplate<CodeTreeNode>(
            (node, _) =>
            {
                var textBlock = new TextBlock
                {
                    FontSize = 12,
                    Foreground = node.IsReadOnly
                        ? new SolidColorBrush(Color.Parse("#999999"))
                        : Brushes.White,
                    FontFamily = node.IsFile ? new FontFamily("monospace") : FontFamily.Default,
                    FontWeight = node.IsFile ? FontWeight.Normal : FontWeight.SemiBold
                };
                textBlock.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("Name"));
                return textBlock;
            },
            node => node.Children);
    }

    private Control BuildRightPanel()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E1E1E"))
        };

        var stack = new DockPanel();

        // File path header
        var header = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252525")),
            Padding = new Thickness(12, 6)
        };

        var headerRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12
        };

        var pathText = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")),
            FontFamily = new FontFamily("monospace"),
            VerticalAlignment = VerticalAlignment.Center
        };
        pathText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("CurrentFilePath"));
        headerRow.Children.Add(pathText);

        var saveButton = new Button
        {
            Content = "Save",
            FontSize = 11,
            Background = new SolidColorBrush(Color.Parse("#064b48")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 4)
        };
        saveButton.Click += OnSaveClick;
        headerRow.Children.Add(saveButton);

        // Build button placeholder (Phase 4 will wire compilation)
        var buildButton = new Button
        {
            Content = "Build",
            FontSize = 11,
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 4)
        };
        buildButton.Click += OnBuildClick;
        headerRow.Children.Add(buildButton);

        header.Child = headerRow;
        DockPanel.SetDock(header, Dock.Top);
        stack.Children.Add(header);

        // Use TextBox as fallback since TextEditor isn't rendering
        // TODO: Debug AvaloniaEdit TextEditor rendering issue
        var codeEditor = new TextBox
        {
            FontFamily = new FontFamily("Cascadia Code, Consolas, Menlo, monospace"),
            FontSize = 13,
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            BorderThickness = new Thickness(0),
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.NoWrap,
            Padding = new Thickness(8),
            Text = "// Select a .cs file from the tree to view code"
        };
        codeEditor.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("FileContent") { Mode = Avalonia.Data.BindingMode.TwoWay });
        codeEditor.Bind(TextBox.IsReadOnlyProperty, new Avalonia.Data.Binding("IsReadOnly"));

        stack.Children.Add(codeEditor);

        border.Child = stack;
        return border;
    }

    private Control BuildBottomPanel()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1A1A1A")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
            BorderThickness = new Thickness(0, 1, 0, 0),
            MaxHeight = 150,
            Padding = new Thickness(12, 8)
        };

        var stack = new StackPanel();

        var statusRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12
        };

        var outputLabel = new TextBlock
        {
            Text = "Output",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            Opacity = 0.7
        };
        statusRow.Children.Add(outputLabel);

        var statusText = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC"))
        };
        statusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("BuildStatus"));
        statusRow.Children.Add(statusText);

        stack.Children.Add(statusRow);

        var outputBox = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("monospace"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")),
            Background = new SolidColorBrush(Color.Parse("#1A1A1A")),
            BorderThickness = new Thickness(0),
            MaxHeight = 100,
            Margin = new Thickness(0, 4, 0, 0)
        };
        outputBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("BuildOutput"));
        stack.Children.Add(outputBox);

        border.Child = stack;
        return border;
    }

    // ---------------------------------------------------------------
    // Event handlers
    // ---------------------------------------------------------------

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is TreeView treeView && treeView.SelectedItem is CodeTreeNode node)
        {
            Services.ModkitLog.Info($"[CodeEditorView] Tree selection: {node.Name}, IsFile={node.IsFile}, Path={node.FullPath}");
            if (DataContext is CodeEditorViewModel vm)
                vm.SelectedFile = node;
        }
    }

    private void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is CodeEditorViewModel vm)
            vm.SaveFile();
    }

    private async void OnBuildClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is CodeEditorViewModel vm)
        {
            await vm.BuildModpackAsync();
        }
    }

    private async void OnAddFileClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is CodeEditorViewModel vm)
        {
            var dialog = new TextInputDialog("Add Source File", "Enter file name:", "NewScript.cs");
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window window)
            {
                var result = await dialog.ShowDialog<string?>(window);
                if (result != null)
                    vm.AddFile(result);
            }
        }
    }

    private void OnRemoveFileClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is CodeEditorViewModel vm)
            vm.RemoveFile();
    }

    private void OnTreeContainerPrepared(object? sender, Avalonia.Controls.ContainerPreparedEventArgs e)
    {
        if (e.Container is TreeViewItem tvi && tvi.DataContext is CodeTreeNode nodeVm)
        {
            tvi.IsExpanded = nodeVm.IsExpanded;
            tvi.Bind(TreeViewItem.IsExpandedProperty,
                new Avalonia.Data.Binding("IsExpanded")
                {
                    Mode = Avalonia.Data.BindingMode.TwoWay
                });
        }
    }
}
