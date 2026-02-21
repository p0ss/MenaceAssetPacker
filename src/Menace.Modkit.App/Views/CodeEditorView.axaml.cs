using System;
using System.ComponentModel;
using System.Threading.Tasks;
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
/// Code editor view: Lua script editor with API reference.
/// Left panel: Scripts tree + Lua API reference. Right panel: code editor.
/// </summary>
public class CodeEditorView : UserControl
{
    private TextEditor? _textEditor;
    private CodeEditorViewModel? _boundViewModel;
    private bool _isUpdatingText;
    private bool _textMateReady;
    private TextMate.Installation? _textMateInstallation;
    private RegistryOptions? _registryOptions;

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
            _boundViewModel.InsertTextRequested -= OnInsertTextRequested;
            _boundViewModel = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_boundViewModel != null)
        {
            _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _boundViewModel.InsertTextRequested -= OnInsertTextRequested;
            _boundViewModel = null;
        }

        base.OnDataContextChanged(e);

        if (DataContext is CodeEditorViewModel vm)
        {
            _boundViewModel = vm;
            _boundViewModel.PropertyChanged += OnViewModelPropertyChanged;
            _boundViewModel.InsertTextRequested += OnInsertTextRequested;

            if (_textEditor != null)
            {
                _isUpdatingText = true;
                _textEditor.Text = vm.FileContent ?? string.Empty;
                _textEditor.IsReadOnly = vm.IsReadOnly;
                _isUpdatingText = false;
            }
        }
    }

    private void OnInsertTextRequested(string text)
    {
        if (_textEditor == null || _boundViewModel == null)
            return;

        // If no file is open or file is read-only, don't insert
        if (_boundViewModel.IsReadOnly)
        {
            _boundViewModel.BuildStatus = "Select a script file first";
            return;
        }

        // Insert text at cursor position
        var offset = _textEditor.CaretOffset;
        _textEditor.Document.Insert(offset, text);
        _textEditor.CaretOffset = offset + text.Length;
        _textEditor.Focus();
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
        else if (e.PropertyName == nameof(CodeEditorViewModel.SelectedFile))
        {
            // Switch syntax highlighting based on file extension
            UpdateGrammarForCurrentFile();
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
                _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
                _textMateInstallation = _textEditor.InstallTextMate(_registryOptions);
                // Set initial grammar based on current file, default to Lua
                UpdateGrammarForCurrentFile();
            });
            _textMateReady = true;
        }
        catch (Exception ex)
        {
            Services.ModkitLog.Warn($"[CodeEditorView] TextMate setup failed: {ex.Message}");
        }
    }

    private void UpdateGrammarForCurrentFile()
    {
        if (_textMateInstallation == null || _registryOptions == null)
            return;

        var filePath = _boundViewModel?.SelectedFile?.FullPath;
        var languageId = "lua"; // Default to Lua

        if (!string.IsNullOrEmpty(filePath))
        {
            var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            languageId = extension switch
            {
                ".cs" => "csharp",
                ".lua" => "lua",
                _ => "lua"
            };
        }

        try
        {
            var scope = _registryOptions.GetScopeByLanguageId(languageId);
            _textMateInstallation.SetGrammar(scope);
        }
        catch (Exception ex)
        {
            Services.ModkitLog.Warn($"[CodeEditorView] Failed to set grammar for {languageId}: {ex.Message}");
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
        // Sub-grid for Scripts and Lua API Reference
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,150,Auto,Auto,Auto,*")
        };

        // Row 0: Scripts label
        var scriptsLabel = new TextBlock
        {
            Text = "Scripts",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(8, 8, 8, 4)
        };
        grid.Children.Add(scriptsLabel);
        Grid.SetRow(scriptsLabel, 0);

        // Row 1: Scripts Tree (fixed height)
        var scriptsTree = new TreeView
        {
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            Margin = new Thickness(8),
            ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Avalonia.Controls.Panel?>(() => new StackPanel())
        };
        scriptsTree.Bind(TreeView.ItemsSourceProperty, new Avalonia.Data.Binding("ScriptsTree"));
        scriptsTree.Bind(TreeView.SelectedItemProperty, new Avalonia.Data.Binding("SelectedFile") { Mode = Avalonia.Data.BindingMode.TwoWay });
        scriptsTree.ItemTemplate = CreateCodeTreeTemplate();
        scriptsTree.SelectionChanged += OnTreeSelectionChanged;
        scriptsTree.ContainerPrepared += OnTreeContainerPrepared;

        var scriptsTreeScroll = new ScrollViewer { Content = scriptsTree };
        grid.Children.Add(scriptsTreeScroll);
        Grid.SetRow(scriptsTreeScroll, 1);

        // Row 2: Template dropdown + Add/Remove file buttons
        var fileButtonRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            Margin = new Thickness(8, 4, 8, 8)
        };

        var templateCombo = new ComboBox
        {
            MinWidth = 120,
            FontSize = 11,
            Margin = new Thickness(0, 0, 8, 0)
        };
        templateCombo.Classes.Add("input");
        templateCombo.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding("ScriptTemplates"));
        templateCombo.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding("SelectedTemplate") { Mode = Avalonia.Data.BindingMode.TwoWay });
        templateCombo.PlaceholderText = "Template...";
        fileButtonRow.Children.Add(templateCombo);
        Grid.SetColumn(templateCombo, 0);

        var addButton = new Button
        {
            Content = "+ Add",
            FontSize = 11,
            Margin = new Thickness(0, 0, 4, 0)
        };
        addButton.Classes.Add("primary");
        addButton.Click += OnAddFileClick;
        fileButtonRow.Children.Add(addButton);
        Grid.SetColumn(addButton, 1);

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
        Grid.SetColumn(removeButton, 2);

        grid.Children.Add(fileButtonRow);
        Grid.SetRow(fileButtonRow, 2);

        // Row 3: Separator
        var sep = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
            Margin = new Thickness(0, 4)
        };
        grid.Children.Add(sep);
        Grid.SetRow(sep, 3);

        // Row 4: Lua API Reference label
        var apiLabel = new TextBlock
        {
            Text = "Lua API Reference",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(8, 8, 8, 4)
        };
        grid.Children.Add(apiLabel);
        Grid.SetRow(apiLabel, 4);

        // Row 5: Lua API Tree (takes remaining space)
        var apiTree = new TreeView
        {
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            Margin = new Thickness(8),
            ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Avalonia.Controls.Panel?>(() => new StackPanel())
        };
        apiTree.Bind(TreeView.ItemsSourceProperty, new Avalonia.Data.Binding("LuaApiTree"));
        apiTree.Bind(TreeView.SelectedItemProperty, new Avalonia.Data.Binding("SelectedApiItem") { Mode = Avalonia.Data.BindingMode.TwoWay });
        apiTree.ItemTemplate = CreateLuaApiTreeTemplate();
        apiTree.DoubleTapped += OnApiTreeDoubleTapped;
        apiTree.ContainerPrepared += OnApiTreeContainerPrepared;

        var apiTreeScroll = new ScrollViewer { Content = apiTree };
        grid.Children.Add(apiTreeScroll);
        Grid.SetRow(apiTreeScroll, 5);

        return grid;
    }

    private Avalonia.Controls.Templates.ITreeDataTemplate CreateLuaApiTreeTemplate()
    {
        return new Avalonia.Controls.Templates.FuncTreeDataTemplate<LuaApiItem>(
            (item, _) =>
            {
                var panel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6
                };

                // Icon/type indicator
                var typeText = item.ItemType switch
                {
                    LuaApiItemType.Category => "",
                    LuaApiItemType.Function => "fn",
                    LuaApiItemType.Event => "ev",
                    _ => ""
                };

                if (!string.IsNullOrEmpty(typeText))
                {
                    var typeBadge = new Border
                    {
                        Background = item.ItemType == LuaApiItemType.Function
                            ? new SolidColorBrush(Color.Parse("#3B7DD8"))
                            : new SolidColorBrush(Color.Parse("#8B5CF6")),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(4, 1),
                        Child = new TextBlock
                        {
                            Text = typeText,
                            FontSize = 9,
                            Foreground = Brushes.White,
                            FontFamily = new FontFamily("monospace")
                        }
                    };
                    panel.Children.Add(typeBadge);
                }

                var nameBlock = new TextBlock
                {
                    FontSize = 12,
                    Foreground = item.IsCategory
                        ? Brushes.White
                        : new SolidColorBrush(Color.Parse("#9CDCFE")),
                    FontWeight = item.IsCategory ? FontWeight.SemiBold : FontWeight.Normal,
                    FontFamily = item.IsCategory ? FontFamily.Default : new FontFamily("monospace")
                };
                nameBlock.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("Name"));
                panel.Children.Add(nameBlock);

                // Add description for non-categories
                if (!item.IsCategory && !string.IsNullOrEmpty(item.Description))
                {
                    var descBlock = new TextBlock
                    {
                        Text = " - " + item.Description,
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.Parse("#666666")),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = 200
                    };
                    panel.Children.Add(descBlock);
                }

                return panel;
            },
            item => item.Children);
    }

    private void OnApiTreeDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (DataContext is not CodeEditorViewModel vm)
            return;

        // Get the tapped item from the event source - walk up the visual tree to find the TreeViewItem
        var source = e.Source as Control;
        while (source != null && source is not TreeViewItem)
        {
            source = source.Parent as Control;
        }

        if (source is TreeViewItem tvi && tvi.DataContext is LuaApiItem item)
        {
            vm.InsertApiItem(item);
        }
    }

    private void OnApiTreeContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container is TreeViewItem tvi && tvi.DataContext is LuaApiItem item)
        {
            tvi.IsExpanded = item.IsExpanded;
        }
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
        var isHandlingCreateNew = false;
        modpackCombo.SelectionChanged += async (sender, e) =>
        {
            if (isHandlingCreateNew) return;
            if (sender is ComboBox combo &&
                combo.SelectedItem is string selected &&
                selected == CodeEditorViewModel.CreateNewModOption)
            {
                isHandlingCreateNew = true;
                try
                {
                    // Clear selection immediately to prevent re-triggering
                    var vm = DataContext as CodeEditorViewModel;

                    // Set to first real modpack or null
                    if (vm != null && vm.AvailableModpacks.Count > 1)
                        vm.SelectedModpack = vm.AvailableModpacks[1];
                    else if (vm != null)
                        vm.SelectedModpack = null;

                    await ShowCreateModpackDialogAsync();
                }
                finally
                {
                    isHandlingCreateNew = false;
                }
            }
        };
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

        // Docs button - opens Lua scripting documentation
        var docsButton = new Button
        {
            Content = "Lua Docs",
            FontSize = 11
        };
        docsButton.Classes.Add("secondary");
        docsButton.Click += OnDocsClick;
        headerRow.Children.Add(docsButton);

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
            Padding = new Thickness(12, 6)
        };

        var statusRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12
        };

        var statusLabel = new TextBlock
        {
            Text = "Status:",
            FontSize = 11,
            Foreground = Brushes.White,
            Opacity = 0.7
        };
        statusRow.Children.Add(statusLabel);

        var statusText = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC"))
        };
        statusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("BuildStatus"));
        statusRow.Children.Add(statusText);

        // Hint text
        var hintText = new TextBlock
        {
            Text = "Double-click API items to insert code at cursor",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#666666")),
            Margin = new Thickness(20, 0, 0, 0)
        };
        statusRow.Children.Add(hintText);

        border.Child = statusRow;
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

    private void OnDocsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Navigate to Lua scripting documentation in Docs tab
        if (DataContext is CodeEditorViewModel vm)
        {
            if (vm.NavigateToLuaDocs != null)
            {
                vm.NavigateToLuaDocs();
            }
            else
            {
                vm.BuildStatus = "Navigation not available";
            }
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

    private async Task ShowCreateModpackDialogAsync()
    {
        try
        {
            if (DataContext is CodeEditorViewModel vm)
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
