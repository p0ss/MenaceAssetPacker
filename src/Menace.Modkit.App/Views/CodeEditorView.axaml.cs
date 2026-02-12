using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using Menace.Modkit.App.Controls;
using Menace.Modkit.App.Converters;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.ViewModels;
using TextMateSharp.Grammars;

namespace Menace.Modkit.App.Views;

/// <summary>
/// Code editor view: browse vanilla decompiled .cs (read-only) and per-modpack source files.
/// Left panel: tree views. Right panel: code viewer/editor. Bottom panel: build output.
/// </summary>
public class CodeEditorView : UserControl
{
    private TextEditor? _textEditor;
    private CodeEditorViewModel? _boundViewModel;
    private bool _isUpdatingText;
    private bool _textMateReady;

    public CodeEditorView()
    {
        Content = BuildUI();
    }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Refresh modpacks when view becomes visible
        if (DataContext is CodeEditorViewModel vm)
            vm.RefreshAll();

        if (!_textMateReady)
        {
            await SetupTextMateAsync();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_boundViewModel != null)
        {
            _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _boundViewModel = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_boundViewModel != null)
        {
            _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _boundViewModel = null;
        }

        base.OnDataContextChanged(e);

        if (DataContext is CodeEditorViewModel vm)
        {
            _boundViewModel = vm;
            _boundViewModel.PropertyChanged += OnViewModelPropertyChanged;

            if (_textEditor != null)
            {
                _isUpdatingText = true;
                _textEditor.Text = vm.FileContent ?? string.Empty;
                _textEditor.IsReadOnly = vm.IsReadOnly;
                _isUpdatingText = false;
            }
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_textEditor == null || _boundViewModel == null)
            return;

        if (e.PropertyName == nameof(CodeEditorViewModel.FileContent))
        {
            if (_isUpdatingText)
                return;

            var vmText = _boundViewModel.FileContent ?? string.Empty;
            if (!string.Equals(_textEditor.Text, vmText, StringComparison.Ordinal))
            {
                _isUpdatingText = true;
                _textEditor.Text = vmText;
                _isUpdatingText = false;
            }
        }
        else if (e.PropertyName == nameof(CodeEditorViewModel.IsReadOnly))
        {
            _textEditor.IsReadOnly = _boundViewModel.IsReadOnly;
        }
    }

    private async System.Threading.Tasks.Task SetupTextMateAsync()
    {
        if (_textEditor == null)
            return;

        try
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
                var textMateInstallation = _textEditor.InstallTextMate(registryOptions);
                textMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId("csharp"));
            });
            _textMateReady = true;
        }
        catch (Exception ex)
        {
            Services.ModkitLog.Warn($"[CodeEditorView] TextMate setup failed: {ex.Message}");
        }
    }

    private Control BuildUI()
    {
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("300,4,*"),
            RowDefinitions = new RowDefinitions("*,Auto")
        };

        // Left panel: trees + toolbar (darker panel)
        var leftWrapper = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#141414")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = BuildLeftPanel()
        };
        mainGrid.Children.Add(leftWrapper);
        Grid.SetColumn(leftWrapper, 0);
        Grid.SetRowSpan(leftWrapper, 2);

        // Splitter
        var splitter = new GridSplitter
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
            ResizeDirection = GridResizeDirection.Columns
        };
        mainGrid.Children.Add(splitter);
        Grid.SetColumn(splitter, 1);
        Grid.SetRowSpan(splitter, 2);

        // Right panel: code editor (lighter panel)
        var rightPanel = BuildRightPanel();
        mainGrid.Children.Add(rightPanel);
        Grid.SetColumn(rightPanel, 2);
        Grid.SetRow(rightPanel, 0);

        // Bottom panel: build output
        var bottomPanel = BuildBottomPanel();
        mainGrid.Children.Add(bottomPanel);
        Grid.SetColumn(bottomPanel, 2);
        Grid.SetRow(bottomPanel, 1);

        return mainGrid;
    }

    private Control BuildLeftPanel()
    {
        var border = new Border();  // No background - parent wrapper has it

        // Use a Grid layout so TreeViews get proper height allocation
        // Row order: Search, Expand/Collapse or Sort, Content (trees or search results)
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*")
        };

        // Row 0: Search box
        var searchBox = new TextBox
        {
            Watermark = "Search code... (3+ chars or Enter)",
            Margin = new Thickness(8, 8, 8, 12)
        };
        searchBox.Classes.Add("search");
        searchBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SearchText"));
        searchBox.KeyDown += (s, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter && DataContext is CodeEditorViewModel vm)
                vm.ExecuteSearch();
        };
        grid.Children.Add(searchBox);
        Grid.SetRow(searchBox, 0);

        // Row 1: Toggle between Expand/Collapse buttons and Sort dropdown
        var buttonContainer = new Panel();

        // Expand/Collapse buttons (shown when not searching)
        var expandCollapsePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8, 4, 8, 12)
        };
        expandCollapsePanel.Bind(StackPanel.IsVisibleProperty,
            new Avalonia.Data.Binding("IsSearching") { Converter = BoolInverseConverter.Instance });

        var expandAllButton = new Button
        {
            Content = "Expand All",
            FontSize = 11
        };
        expandAllButton.Classes.Add("secondary");
        expandAllButton.Click += (_, _) =>
        {
            if (DataContext is CodeEditorViewModel vm)
                vm.ExpandAll();
        };
        expandCollapsePanel.Children.Add(expandAllButton);

        var collapseAllButton = new Button
        {
            Content = "Collapse All",
            FontSize = 11
        };
        collapseAllButton.Classes.Add("secondary");
        collapseAllButton.Click += (_, _) =>
        {
            if (DataContext is CodeEditorViewModel vm)
                vm.CollapseAll();
        };
        expandCollapsePanel.Children.Add(collapseAllButton);

        buttonContainer.Children.Add(expandCollapsePanel);

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
            if (sortCombo.SelectedIndex >= 0 && DataContext is CodeEditorViewModel vm)
                vm.CurrentSortOption = (SearchPanelBuilder.SortOption)sortCombo.SelectedIndex;
        };
        searchControlsPanel.Children.Add(sortCombo);

        buttonContainer.Children.Add(searchControlsPanel);

        grid.Children.Add(buttonContainer);
        Grid.SetRow(buttonContainer, 1);

        // Row 2: Content - toggle between trees and search results
        var contentPanel = new Panel();

        // Trees container (shown when not searching)
        var treesContainer = BuildTreesContainer();
        treesContainer.Bind(Control.IsVisibleProperty,
            new Avalonia.Data.Binding("IsSearching") { Converter = BoolInverseConverter.Instance });
        contentPanel.Children.Add(treesContainer);

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
                DataContext is CodeEditorViewModel vm)
            {
                vm.SelectSearchResult(item);
            }
        };

        // Double-click to select and exit search mode
        searchResultsList.DoubleTapped += (s, e) =>
        {
            if (searchResultsList.SelectedItem is SearchResultItem item &&
                DataContext is CodeEditorViewModel vm)
            {
                vm.SelectAndExitSearch(item);
            }
        };

        contentPanel.Children.Add(searchResultsList);

        grid.Children.Add(contentPanel);
        Grid.SetRow(contentPanel, 2);

        border.Child = grid;
        return border;
    }

    private Control BuildTreesContainer()
    {
        // Sub-grid for the two trees
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,150,Auto,Auto,Auto,*")
        };

        // Row 0: Mod Source label
        var modSourceLabel = new TextBlock
        {
            Text = "Mod Sources",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(8, 8, 8, 4)
        };
        grid.Children.Add(modSourceLabel);
        Grid.SetRow(modSourceLabel, 0);

        // Row 1: Mod Source Tree (fixed height)
        var modTree = new TreeView
        {
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            Margin = new Thickness(8),
            ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Avalonia.Controls.Panel?>(() => new StackPanel())
        };
        modTree.Bind(TreeView.ItemsSourceProperty, new Avalonia.Data.Binding("ModSourceTree"));
        modTree.Bind(TreeView.SelectedItemProperty, new Avalonia.Data.Binding("SelectedFile") { Mode = Avalonia.Data.BindingMode.TwoWay });
        modTree.ItemTemplate = CreateCodeTreeTemplate();
        modTree.SelectionChanged += OnTreeSelectionChanged;
        modTree.ContainerPrepared += OnTreeContainerPrepared;

        var modTreeScroll = new ScrollViewer { Content = modTree };
        grid.Children.Add(modTreeScroll);
        Grid.SetRow(modTreeScroll, 1);

        // Row 2: Add/Remove file buttons
        var fileButtonRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(8, 4, 8, 8)
        };

        var addButton = new Button
        {
            Content = "+ Add File",
            FontSize = 11
        };
        addButton.Classes.Add("primary");
        addButton.Click += OnAddFileClick;
        fileButtonRow.Children.Add(addButton);
        Grid.SetColumn(addButton, 0);

        var removeButton = new Button
        {
            Content = "Remove",
            FontSize = 11
        };
        removeButton.Classes.Add("destructive");
        removeButton.Bind(Button.IsEnabledProperty, new Avalonia.Data.Binding("SelectedFile")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<CodeTreeNode?, bool>(
                node => node != null && node.IsFile && !node.IsReadOnly)
        });
        removeButton.Click += OnRemoveFileClick;
        fileButtonRow.Children.Add(removeButton);
        Grid.SetColumn(removeButton, 1);

        grid.Children.Add(fileButtonRow);
        Grid.SetRow(fileButtonRow, 2);

        // Row 3: Separator
        var sep2 = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
            Margin = new Thickness(0, 4)
        };
        grid.Children.Add(sep2);
        Grid.SetRow(sep2, 3);

        // Row 4: Vanilla Code label (hidden when no code available)
        var vanillaLabel = new TextBlock
        {
            Text = "Vanilla Code (read-only)",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            Opacity = 0.7,
            Margin = new Thickness(8, 8, 8, 4)
        };
        vanillaLabel.Bind(TextBlock.IsVisibleProperty,
            new Avalonia.Data.Binding("ShowVanillaCodeWarning") { Converter = BoolInverseConverter.Instance });
        grid.Children.Add(vanillaLabel);
        Grid.SetRow(vanillaLabel, 4);

        // Row 5: Vanilla Code Tree (takes remaining space, hidden when no code available)
        var vanillaTree = new TreeView
        {
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            Margin = new Thickness(8),
            ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Avalonia.Controls.Panel?>(() => new StackPanel())
        };
        vanillaTree.Bind(TreeView.ItemsSourceProperty, new Avalonia.Data.Binding("VanillaCodeTree"));
        vanillaTree.Bind(TreeView.SelectedItemProperty, new Avalonia.Data.Binding("SelectedFile") { Mode = Avalonia.Data.BindingMode.TwoWay });
        vanillaTree.ItemTemplate = CreateCodeTreeTemplate();
        vanillaTree.SelectionChanged += OnTreeSelectionChanged;
        vanillaTree.ContainerPrepared += OnTreeContainerPrepared;

        var vanillaTreeScroll = new ScrollViewer { Content = vanillaTree };
        vanillaTreeScroll.Bind(ScrollViewer.IsVisibleProperty,
            new Avalonia.Data.Binding("ShowVanillaCodeWarning") { Converter = BoolInverseConverter.Instance });
        grid.Children.Add(vanillaTreeScroll);
        Grid.SetRow(vanillaTreeScroll, 5);

        // Row 5 (overlay): Message when no vanilla code available
        var noCodePanel = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1A1A1A")),
            Padding = new Thickness(16),
            Margin = new Thickness(8)
        };
        var noCodeStack = new StackPanel { Spacing = 12 };
        noCodeStack.Children.Add(new TextBlock
        {
            Text = "No Decompiled Code Found",
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });
        noCodeStack.Children.Add(new TextBlock
        {
            Text = "To browse vanilla game code, run asset extraction first:\n\n" +
                   "1. Go to Tool Settings (under Modding Tools)\n" +
                   "2. Click 'Force Extract Assets'\n" +
                   "3. Wait for AssetRipper to complete\n\n" +
                   "This extracts decompiled C# source files from the game.\n\n" +
                   "You can still create and edit modpack source files in the panel above.",
            FontSize = 12,
            Foreground = Brushes.White,
            Opacity = 0.8,
            TextWrapping = TextWrapping.Wrap
        });
        noCodePanel.Child = noCodeStack;
        noCodePanel.Bind(Border.IsVisibleProperty, new Avalonia.Data.Binding("ShowVanillaCodeWarning"));
        grid.Children.Add(noCodePanel);
        Grid.SetRow(noCodePanel, 5);

        return grid;
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
            Background = new SolidColorBrush(Color.Parse("#1A1A1A"))
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

        // Modpack dropdown
        var modpackLabel = new TextBlock
        {
            Text = "Modpack:",
            FontSize = 11,
            Foreground = Brushes.White,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center
        };
        headerRow.Children.Add(modpackLabel);

        var modpackCombo = new ComboBox
        {
            MinWidth = 150,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        modpackCombo.Classes.Add("input");
        modpackCombo.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding("AvailableModpacks"));
        modpackCombo.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding("SelectedModpack") { Mode = Avalonia.Data.BindingMode.TwoWay });
        headerRow.Children.Add(modpackCombo);

        // Separator
        headerRow.Children.Add(new Border
        {
            Width = 1,
            Background = new SolidColorBrush(Color.Parse("#3E3E3E")),
            Margin = new Thickness(4, 0)
        });

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
            FontSize = 11
        };
        saveButton.Classes.Add("primary");
        saveButton.Click += OnSaveClick;
        headerRow.Children.Add(saveButton);

        // Build button placeholder (Phase 4 will wire compilation)
        var buildButton = new Button
        {
            Content = "Build",
            FontSize = 11
        };
        buildButton.Classes.Add("secondary");
        buildButton.Click += OnBuildClick;
        headerRow.Children.Add(buildButton);

        header.Child = headerRow;
        DockPanel.SetDock(header, Dock.Top);
        stack.Children.Add(header);

        _textEditor = new TextEditor
        {
            FontFamily = new FontFamily("Cascadia Code, Consolas, Menlo, monospace"),
            FontSize = 13,
            ShowLineNumbers = true,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            Foreground = new SolidColorBrush(Color.Parse("#D4D4D4")),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8)
        };
        _textEditor.TextChanged += (_, _) =>
        {
            if (_isUpdatingText || _boundViewModel == null)
                return;

            _isUpdatingText = true;
            _boundViewModel.FileContent = _textEditor.Text;
            _isUpdatingText = false;
        };

        stack.Children.Add(_textEditor);

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

    private async void OnRemoveFileClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not CodeEditorViewModel vm || vm.SelectedFile == null || !vm.SelectedFile.IsFile)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window window)
            return;

        var fileName = vm.SelectedFile.Name;
        var confirmed = await ConfirmationDialog.ShowAsync(
            window,
            "Remove File",
            $"Are you sure you want to remove '{fileName}' from this modpack? This cannot be undone.",
            "Remove",
            isDestructive: true
        );

        if (confirmed)
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
