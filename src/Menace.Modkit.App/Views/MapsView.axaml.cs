#nullable disable
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Menace.Modkit.App.Styles;
using Menace.Modkit.App.ViewModels;

namespace Menace.Modkit.App.Views;

/// <summary>
/// Map editor view with accordion-style layer panels.
/// Layout:
/// - Left: Accordion panels (Zones, Paths, Chunks, Surfaces) with lists and add/remove
/// - Center: Map canvas
/// - Right: Properties of selected item only
/// - Bottom: Chunk browser (when Chunks expanded)
/// </summary>
public class MapsView : UserControl
{
    private const int TILE_SIZE = 12;

    private MapsViewModel _viewModel;
    private Canvas _mapCanvas;
    private ScrollViewer _canvasScrollViewer;

    // Drag state for zone painting
    private bool _isDragging;
    private int _dragStartX;
    private int _dragStartY;

    // Accordion expanders
    private Expander _zonesExpander;
    private Expander _pathsExpander;
    private Expander _chunksExpander;
    private Expander _surfacesExpander;

    // Bottom chunk browser panel and splitter
    private Border _chunkBrowserPanel;
    private GridSplitter _chunkBrowserSplitter;

    // Property fields
    private StackPanel _propertiesPanel;
    private TextBox _zoneIdTextBox;
    private TextBox _zoneNameTextBox;
    private TextBox _zoneXTextBox;
    private TextBox _zoneYTextBox;
    private TextBox _zoneWidthTextBox;
    private TextBox _zoneHeightTextBox;
    private TextBox _zonePriorityTextBox;
    private ComboBox _zoneTypeCombo;
    private TextBox _pathIdTextBox;
    private ComboBox _pathTypeCombo;
    private TextBox _pathWidthTextBox;
    private StackPanel _zonePropertiesPanel;
    private StackPanel _pathPropertiesPanel;
    private StackPanel _chunkPropertiesPanel;
    private StackPanel _surfacePropertiesPanel;

    public MapsView()
    {
        Content = BuildUI();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is MapsViewModel vm)
            vm.RefreshAll();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Zones.CollectionChanged -= OnCollectionChanged;
            _viewModel.Tiles.CollectionChanged -= OnCollectionChanged;
            _viewModel.Paths.CollectionChanged -= OnCollectionChanged;
            _viewModel.ChunkPlacements.CollectionChanged -= OnCollectionChanged;
        }

        base.OnDataContextChanged(e);

        if (DataContext is MapsViewModel vm)
        {
            _viewModel = vm;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.Zones.CollectionChanged += OnCollectionChanged;
            _viewModel.Tiles.CollectionChanged += OnCollectionChanged;
            _viewModel.Paths.CollectionChanged += OnCollectionChanged;
            _viewModel.ChunkPlacements.CollectionChanged += OnCollectionChanged;
            UpdatePropertyPanels();
            RedrawCanvas();
        }
    }

    private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapsViewModel.MapSize) ||
            e.PropertyName == nameof(MapsViewModel.SelectedZone) ||
            e.PropertyName == nameof(MapsViewModel.SelectedPath) ||
            e.PropertyName == nameof(MapsViewModel.SelectedChunkPlacement) ||
            e.PropertyName == nameof(MapsViewModel.HoverTileX) ||
            e.PropertyName == nameof(MapsViewModel.HoverTileY) ||
            e.PropertyName == nameof(MapsViewModel.BrushSize) ||
            e.PropertyName == nameof(MapsViewModel.BrushSmooth) ||
            e.PropertyName == nameof(MapsViewModel.CurrentTool))
        {
            RedrawCanvas();
        }

        if (e.PropertyName == nameof(MapsViewModel.SelectedZone) ||
            e.PropertyName == nameof(MapsViewModel.SelectedPath) ||
            e.PropertyName == nameof(MapsViewModel.SelectedChunkPlacement))
        {
            UpdatePropertyPanels();
        }
    }

    private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        RedrawCanvas();
    }

    private Control BuildUI()
    {
        // Main grid with resizable chunk browser row
        var mainGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto,140,Auto")
        };

        // Row 0: Top toolbar
        var toolbar = BuildToolbar();
        mainGrid.Children.Add(toolbar);
        Grid.SetRow(toolbar, 0);

        // Row 1: Main content area (left accordion + canvas + right properties)
        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("240,Auto,*,Auto,260")
        };

        // Left: Accordion panels
        var leftPanel = BuildAccordionPanel();
        leftPanel.MinWidth = 180;
        contentGrid.Children.Add(leftPanel);
        Grid.SetColumn(leftPanel, 0);

        // Left splitter (draggable)
        var leftSplitter = new GridSplitter
        {
            Width = 4,
            Background = ThemeColors.BrushBorder,
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            Cursor = new Cursor(StandardCursorType.SizeWestEast)
        };
        contentGrid.Children.Add(leftSplitter);
        Grid.SetColumn(leftSplitter, 1);

        // Center: Canvas
        var canvasPanel = BuildCanvasPanel();
        contentGrid.Children.Add(canvasPanel);
        Grid.SetColumn(canvasPanel, 2);

        // Right splitter (draggable)
        var rightSplitter = new GridSplitter
        {
            Width = 4,
            Background = ThemeColors.BrushBorder,
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            Cursor = new Cursor(StandardCursorType.SizeWestEast)
        };
        contentGrid.Children.Add(rightSplitter);
        Grid.SetColumn(rightSplitter, 3);

        // Right: Properties only
        var rightPanel = BuildPropertiesPanel();
        rightPanel.MinWidth = 200;
        contentGrid.Children.Add(rightPanel);
        Grid.SetColumn(rightPanel, 4);

        mainGrid.Children.Add(contentGrid);
        Grid.SetRow(contentGrid, 1);

        // Row 2: Chunk browser splitter (draggable, for resizing browser height)
        _chunkBrowserSplitter = new GridSplitter
        {
            Height = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = ThemeColors.BrushBorder,
            ResizeDirection = GridResizeDirection.Rows,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            Cursor = new Cursor(StandardCursorType.SizeNorthSouth),
            IsVisible = false
        };
        mainGrid.Children.Add(_chunkBrowserSplitter);
        Grid.SetRow(_chunkBrowserSplitter, 2);

        // Row 3: Chunk browser (conditionally visible, resizable)
        _chunkBrowserPanel = BuildChunkBrowserPanel();
        _chunkBrowserPanel.MinHeight = 80;
        mainGrid.Children.Add(_chunkBrowserPanel);
        Grid.SetRow(_chunkBrowserPanel, 3);

        // Row 4: Status bar
        var statusBar = BuildStatusBar();
        mainGrid.Children.Add(statusBar);
        Grid.SetRow(statusBar, 4);

        return mainGrid;
    }

    private Control BuildToolbar()
    {
        var border = new Border
        {
            Background = ThemeColors.BrushBgElevated,
            BorderBrush = ThemeColors.BrushBorder,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 8)
        };

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        // Modpack selector
        stack.Children.Add(new TextBlock
        {
            Text = "Modpack:",
            FontSize = 12,
            Foreground = ThemeColors.BrushTextSecondary,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        });

        var modpackCombo = new ComboBox { MinWidth = 120, FontSize = 12 };
        modpackCombo.Classes.Add("input");
        modpackCombo.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding("AvailableModpacks"));
        modpackCombo.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding("SelectedModpack") { Mode = Avalonia.Data.BindingMode.TwoWay });
        stack.Children.Add(modpackCombo);

        // Map selector
        stack.Children.Add(new TextBlock
        {
            Text = "Map:",
            FontSize = 12,
            Foreground = ThemeColors.BrushTextSecondary,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 4, 0)
        });

        var mapCombo = new ComboBox { MinWidth = 120, FontSize = 12 };
        mapCombo.Classes.Add("input");
        mapCombo.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding("AvailableMaps"));
        mapCombo.Bind(ComboBox.SelectedItemProperty, new Avalonia.Data.Binding("SelectedMap") { Mode = Avalonia.Data.BindingMode.TwoWay });
        stack.Children.Add(mapCombo);

        // Separator
        stack.Children.Add(CreateToolbarSeparator());

        // Tool buttons
        AddToolButton(stack, "Select", MapEditorTool.Select);
        AddToolButton(stack, "Erase", MapEditorTool.Eraser);

        // Separator
        stack.Children.Add(CreateToolbarSeparator());

        // Save button
        var saveButton = new Button { Content = "Save", FontSize = 11 };
        saveButton.Classes.Add("primary");
        saveButton.Click += (_, _) => _viewModel?.SaveMap();
        stack.Children.Add(saveButton);

        // New button
        var newButton = new Button { Content = "New", FontSize = 11 };
        newButton.Classes.Add("secondary");
        newButton.Click += (_, _) => _viewModel?.NewMap();
        stack.Children.Add(newButton);

        border.Child = stack;
        return border;
    }

    private void AddToolButton(StackPanel panel, string text, MapEditorTool tool)
    {
        var button = new ToggleButton
        {
            Content = text,
            FontSize = 11,
            Padding = new Thickness(10, 4),
            Tag = tool
        };
        button.Classes.Add("tabToggle");

        if (DataContext is MapsViewModel vm)
            button.IsChecked = vm.CurrentTool == tool;

        button.Click += (_, _) =>
        {
            if (DataContext is MapsViewModel vm)
            {
                vm.CurrentTool = tool;
                foreach (var child in panel.Children)
                {
                    if (child is ToggleButton tb && tb.Tag is MapEditorTool t)
                        tb.IsChecked = t == tool;
                }
            }
        };

        panel.Children.Add(button);
    }

    private Border CreateToolbarSeparator()
    {
        return new Border
        {
            Width = 1,
            Background = ThemeColors.BrushBorderLight,
            Margin = new Thickness(8, 4)
        };
    }

    private Control BuildAccordionPanel()
    {
        var border = new Border
        {
            Background = ThemeColors.BrushBgPanelLeft,
            BorderBrush = ThemeColors.BrushBorder,
            BorderThickness = new Thickness(0, 0, 1, 0)
        };

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var stack = new StackPanel { Spacing = 1 };

        // Map settings at the top
        stack.Children.Add(BuildMapSettingsSection());

        // Accordion sections
        _zonesExpander = BuildZonesExpander();
        _pathsExpander = BuildPathsExpander();
        _chunksExpander = BuildChunksExpander();
        _surfacesExpander = BuildSurfacesExpander();

        stack.Children.Add(_zonesExpander);
        stack.Children.Add(_pathsExpander);
        stack.Children.Add(_chunksExpander);
        stack.Children.Add(_surfacesExpander);

        scroll.Content = stack;
        border.Child = scroll;
        return border;
    }

    private Control BuildMapSettingsSection()
    {
        var panel = new StackPanel
        {
            Background = ThemeColors.BrushBgElevated,
            Margin = new Thickness(0, 0, 0, 1)
        };

        panel.Children.Add(new TextBlock
        {
            Text = "Map Settings",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = ThemeColors.BrushTextSecondary,
            Margin = new Thickness(12, 8, 12, 4)
        });

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            Margin = new Thickness(12, 4, 12, 8)
        };

        AddSettingsRow(grid, 0, "ID:", "MapId");
        AddSettingsRow(grid, 1, "Name:", "MapName");
        AddSettingsRow(grid, 2, "Size:", "MapSize");
        AddSettingsRow(grid, 3, "Seed:", "MapSeed");

        panel.Children.Add(grid);
        return panel;
    }

    private void AddSettingsRow(Grid grid, int row, string label, string binding)
    {
        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = ThemeColors.BrushTextTertiary,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 8, 2)
        };
        grid.Children.Add(labelBlock);
        Grid.SetRow(labelBlock, row);
        Grid.SetColumn(labelBlock, 0);

        var textBox = new TextBox { FontSize = 10, Padding = new Thickness(4, 2) };
        textBox.Classes.Add("input");
        textBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding(binding) { Mode = Avalonia.Data.BindingMode.TwoWay });
        grid.Children.Add(textBox);
        Grid.SetRow(textBox, row);
        Grid.SetColumn(textBox, 1);
    }

    private Expander BuildZonesExpander()
    {
        var expander = new Expander
        {
            Header = CreateExpanderHeader("Zones", "Zones.Count"),
            IsExpanded = true,
            Padding = new Thickness(0),
            Background = ThemeColors.BrushBgElevated
        };

        var content = new StackPanel();

        // Zones list
        var zonesList = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            MaxHeight = 150,
            Margin = new Thickness(4, 0)
        };
        zonesList.Bind(ListBox.ItemsSourceProperty, new Avalonia.Data.Binding("Zones"));
        zonesList.Bind(ListBox.SelectedItemProperty, new Avalonia.Data.Binding("SelectedZone") { Mode = Avalonia.Data.BindingMode.TwoWay });
        zonesList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<MapZoneViewModel>(
            (zone, _) => new TextBlock
            {
                Text = zone?.Name ?? "Zone",
                FontSize = 11,
                Foreground = ThemeColors.BrushTextPrimary,
                Padding = new Thickness(4, 2)
            }, true);
        content.Children.Add(zonesList);

        // Buttons
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(8, 4, 8, 8)
        };

        var addBtn = new Button { Content = "+ Add", FontSize = 10, Padding = new Thickness(8, 2) };
        addBtn.Classes.Add("secondary");
        addBtn.Click += (_, _) =>
        {
            _viewModel?.AddZone();
            _viewModel.SelectedLayer = EditorLayer.Zones;
        };
        buttons.Children.Add(addBtn);

        var removeBtn = new Button { Content = "Remove", FontSize = 10, Padding = new Thickness(8, 2) };
        removeBtn.Classes.Add("destructive");
        removeBtn.Click += (_, _) => _viewModel?.RemoveSelectedZone();
        buttons.Children.Add(removeBtn);

        content.Children.Add(buttons);

        // Zone type selector for new zones
        var typeRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(8, 0, 8, 8)
        };
        typeRow.Children.Add(new TextBlock
        {
            Text = "Type:",
            FontSize = 10,
            Foreground = ThemeColors.BrushTextTertiary,
            VerticalAlignment = VerticalAlignment.Center
        });
        var zoneTypeCombo = new ComboBox { FontSize = 10, MinWidth = 120 };
        zoneTypeCombo.Classes.Add("input");
        zoneTypeCombo.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding("ZoneTypes"));
        zoneTypeCombo.Bind(ComboBox.SelectedValueProperty, new Avalonia.Data.Binding("SelectedZoneType") { Mode = Avalonia.Data.BindingMode.TwoWay });
        typeRow.Children.Add(zoneTypeCombo);
        content.Children.Add(typeRow);

        expander.Content = content;

        expander.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == "IsExpanded" && expander.IsExpanded)
            {
                if (_viewModel != null) _viewModel.SelectedLayer = EditorLayer.Zones;
            }
        };

        return expander;
    }

    private Expander BuildPathsExpander()
    {
        var expander = new Expander
        {
            Header = CreateExpanderHeader("Paths", "Paths.Count"),
            IsExpanded = false,
            Padding = new Thickness(0),
            Background = ThemeColors.BrushBgElevated
        };

        var content = new StackPanel();

        // Paths list
        var pathsList = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            MaxHeight = 150,
            Margin = new Thickness(4, 0)
        };
        pathsList.Bind(ListBox.ItemsSourceProperty, new Avalonia.Data.Binding("Paths"));
        pathsList.Bind(ListBox.SelectedItemProperty, new Avalonia.Data.Binding("SelectedPath") { Mode = Avalonia.Data.BindingMode.TwoWay });
        pathsList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<MapPathViewModel>(
            (path, _) => new TextBlock
            {
                Text = $"{path?.Id ?? "Path"} ({path?.Waypoints?.Count ?? 0} pts)",
                FontSize = 11,
                Foreground = ThemeColors.BrushTextPrimary,
                Padding = new Thickness(4, 2)
            }, true);
        content.Children.Add(pathsList);

        // Buttons
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(8, 4, 8, 8)
        };

        var addBtn = new Button { Content = "+ Add", FontSize = 10, Padding = new Thickness(8, 2) };
        addBtn.Classes.Add("secondary");
        addBtn.Click += (_, _) =>
        {
            _viewModel?.AddPath();
            _viewModel.SelectedLayer = EditorLayer.Paths;
        };
        buttons.Children.Add(addBtn);

        var finishBtn = new Button { Content = "Finish", FontSize = 10, Padding = new Thickness(8, 2) };
        finishBtn.Classes.Add("secondary");
        finishBtn.Click += (_, _) => _viewModel?.FinishPath();
        buttons.Children.Add(finishBtn);

        var removeBtn = new Button { Content = "Remove", FontSize = 10, Padding = new Thickness(8, 2) };
        removeBtn.Classes.Add("destructive");
        removeBtn.Click += (_, _) => _viewModel?.RemoveSelectedPath();
        buttons.Children.Add(removeBtn);

        content.Children.Add(buttons);

        // Path width setting
        var widthRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(8, 0, 8, 8)
        };
        widthRow.Children.Add(new TextBlock
        {
            Text = "Width:",
            FontSize = 10,
            Foreground = ThemeColors.BrushTextTertiary,
            VerticalAlignment = VerticalAlignment.Center
        });
        var widthBox = new TextBox { FontSize = 10, Width = 40 };
        widthBox.Classes.Add("input");
        widthBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("PathWidth") { Mode = Avalonia.Data.BindingMode.TwoWay });
        widthRow.Children.Add(widthBox);
        content.Children.Add(widthRow);

        expander.Content = content;

        expander.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == "IsExpanded" && expander.IsExpanded)
            {
                if (_viewModel != null) _viewModel.SelectedLayer = EditorLayer.Paths;
            }
        };

        return expander;
    }

    private Expander BuildChunksExpander()
    {
        var expander = new Expander
        {
            Header = CreateExpanderHeader("Chunks", "ChunkPlacements.Count"),
            IsExpanded = false,
            Padding = new Thickness(0),
            Background = ThemeColors.BrushBgElevated
        };

        var content = new StackPanel();

        // Placements list
        var placementsList = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            MaxHeight = 120,
            Margin = new Thickness(4, 0)
        };
        placementsList.Bind(ListBox.ItemsSourceProperty, new Avalonia.Data.Binding("ChunkPlacements"));
        placementsList.Bind(ListBox.SelectedItemProperty, new Avalonia.Data.Binding("SelectedChunkPlacement") { Mode = Avalonia.Data.BindingMode.TwoWay });
        placementsList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ChunkPlacementViewModel>(
            (chunk, _) => new TextBlock
            {
                Text = chunk?.DisplayText ?? "Chunk",
                FontSize = 10,
                Foreground = ThemeColors.BrushTextPrimary,
                Padding = new Thickness(4, 2)
            }, true);
        content.Children.Add(placementsList);

        // Buttons
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(8, 4, 8, 8)
        };

        var removeBtn = new Button { Content = "Remove", FontSize = 10, Padding = new Thickness(8, 2) };
        removeBtn.Classes.Add("destructive");
        removeBtn.Click += (_, _) => _viewModel?.RemoveSelectedChunkPlacement();
        buttons.Children.Add(removeBtn);

        var rotateCCWBtn = new Button { Content = "-90°", FontSize = 10, Padding = new Thickness(8, 2) };
        rotateCCWBtn.Classes.Add("secondary");
        rotateCCWBtn.Click += (_, _) => _viewModel?.RotateSelectedChunk(-90);
        buttons.Children.Add(rotateCCWBtn);

        var rotateCWBtn = new Button { Content = "+90°", FontSize = 10, Padding = new Thickness(8, 2) };
        rotateCWBtn.Classes.Add("secondary");
        rotateCWBtn.Click += (_, _) => _viewModel?.RotateSelectedChunk(90);
        buttons.Children.Add(rotateCWBtn);

        content.Children.Add(buttons);

        // Info text
        content.Children.Add(new TextBlock
        {
            Text = "Select a chunk from the browser below, then click on the map to place it.",
            FontSize = 9,
            Foreground = ThemeColors.BrushTextTertiary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 0, 8, 8)
        });

        expander.Content = content;

        expander.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == "IsExpanded")
            {
                if (_viewModel != null && expander.IsExpanded)
                    _viewModel.SelectedLayer = EditorLayer.Chunks;

                // Show/hide chunk browser and its splitter
                if (_chunkBrowserPanel != null)
                    _chunkBrowserPanel.IsVisible = expander.IsExpanded;
                if (_chunkBrowserSplitter != null)
                    _chunkBrowserSplitter.IsVisible = expander.IsExpanded;
            }
        };

        return expander;
    }

    private Expander BuildSurfacesExpander()
    {
        var expander = new Expander
        {
            Header = CreateExpanderHeader("Surfaces", "Tiles.Count"),
            IsExpanded = false,
            Padding = new Thickness(0),
            Background = ThemeColors.BrushBgElevated
        };

        var content = new StackPanel();

        // Terrain type selector
        var terrainRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(8, 4, 8, 4)
        };
        terrainRow.Children.Add(new TextBlock
        {
            Text = "Paint:",
            FontSize = 10,
            Foreground = ThemeColors.BrushTextTertiary,
            VerticalAlignment = VerticalAlignment.Center
        });
        var terrainCombo = new ComboBox { FontSize = 10, MinWidth = 100 };
        terrainCombo.Classes.Add("input");
        terrainCombo.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding("TerrainTypes"));
        terrainCombo.Bind(ComboBox.SelectedValueProperty, new Avalonia.Data.Binding("SelectedTerrainType") { Mode = Avalonia.Data.BindingMode.TwoWay });
        terrainRow.Children.Add(terrainCombo);
        content.Children.Add(terrainRow);

        // Brush radius slider (0 = single tile, 1 = 3x3, 2 = 5x5, etc.)
        var brushSizeRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(8, 4, 8, 0)
        };
        brushSizeRow.Children.Add(new TextBlock
        {
            Text = "Radius:",
            FontSize = 10,
            Foreground = ThemeColors.BrushTextTertiary,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 40
        });
        var brushSizeSlider = new Slider
        {
            Minimum = 0,
            Maximum = 9,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            Width = 80,
            VerticalAlignment = VerticalAlignment.Center
        };
        brushSizeSlider.Bind(Slider.ValueProperty, new Avalonia.Data.Binding("BrushSize") { Mode = Avalonia.Data.BindingMode.TwoWay });
        brushSizeRow.Children.Add(brushSizeSlider);
        var brushSizeLabel = new TextBlock
        {
            FontSize = 10,
            Foreground = ThemeColors.BrushTextSecondary,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 20
        };
        brushSizeLabel.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("BrushSize"));
        brushSizeRow.Children.Add(brushSizeLabel);
        content.Children.Add(brushSizeRow);

        // Scatter slider (0 = solid fill, 1 = very sparse)
        var jitterRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(8, 4, 8, 0)
        };
        jitterRow.Children.Add(new TextBlock
        {
            Text = "Scatter:",
            FontSize = 10,
            Foreground = ThemeColors.BrushTextTertiary,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 40
        });
        var jitterSlider = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            Width = 80,
            VerticalAlignment = VerticalAlignment.Center
        };
        jitterSlider.Bind(Slider.ValueProperty, new Avalonia.Data.Binding("BrushJitter") { Mode = Avalonia.Data.BindingMode.TwoWay });
        jitterRow.Children.Add(jitterSlider);
        var smoothCheck = new CheckBox
        {
            Content = "Smooth",
            FontSize = 10,
            Foreground = ThemeColors.BrushTextSecondary,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        };
        smoothCheck.Bind(CheckBox.IsCheckedProperty, new Avalonia.Data.Binding("BrushSmooth") { Mode = Avalonia.Data.BindingMode.TwoWay });
        jitterRow.Children.Add(smoothCheck);
        content.Children.Add(jitterRow);

        // Stats
        var statsLabel = new TextBlock
        {
            FontSize = 10,
            Foreground = ThemeColors.BrushTextTertiary,
            Margin = new Thickness(8, 4, 8, 4)
        };
        statsLabel.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("Tiles.Count")
        {
            StringFormat = "{0} tiles painted"
        });
        content.Children.Add(statsLabel);

        // Clear button
        var clearBtn = new Button { Content = "Clear All", FontSize = 10, Margin = new Thickness(8, 4, 8, 8) };
        clearBtn.Classes.Add("destructive");
        clearBtn.Click += (_, _) =>
        {
            if (_viewModel != null)
            {
                _viewModel.Tiles.Clear();
                _viewModel.HasUnsavedChanges = true;
            }
        };
        content.Children.Add(clearBtn);

        // Legend
        var legendGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            Margin = new Thickness(8, 0, 8, 8)
        };

        AddLegendItem(legendGrid, 0, 0, "#228B22", "Trees");
        AddLegendItem(legendGrid, 0, 2, "#4169E1", "Water");
        AddLegendItem(legendGrid, 1, 0, "#8B4513", "HighGround");
        AddLegendItem(legendGrid, 1, 2, "#696969", "Road");
        AddLegendItem(legendGrid, 2, 0, "#F4A460", "Sand");
        AddLegendItem(legendGrid, 2, 2, "#808080", "Concrete");

        content.Children.Add(legendGrid);

        expander.Content = content;

        expander.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == "IsExpanded" && expander.IsExpanded)
            {
                if (_viewModel != null) _viewModel.SelectedLayer = EditorLayer.Surfaces;
            }
        };

        return expander;
    }

    private Control CreateExpanderHeader(string title, string countBinding)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });

        var countLabel = new TextBlock
        {
            FontSize = 10,
            Foreground = ThemeColors.BrushTextTertiary,
            VerticalAlignment = VerticalAlignment.Center
        };
        countLabel.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(countBinding)
        {
            StringFormat = "({0})"
        });
        panel.Children.Add(countLabel);

        return panel;
    }

    private void AddLegendItem(Grid grid, int row, int col, string colorHex, string text)
    {
        var colorRect = new Border
        {
            Width = 10,
            Height = 10,
            Background = new SolidColorBrush(Color.Parse(colorHex)),
            Margin = new Thickness(0, 1, 4, 1)
        };
        grid.Children.Add(colorRect);
        Grid.SetRow(colorRect, row);
        Grid.SetColumn(colorRect, col);

        var label = new TextBlock
        {
            Text = text,
            FontSize = 9,
            Foreground = ThemeColors.BrushTextTertiary,
            VerticalAlignment = VerticalAlignment.Center
        };
        grid.Children.Add(label);
        Grid.SetRow(label, row);
        Grid.SetColumn(label, col + 1);
    }

    private Control BuildCanvasPanel()
    {
        var border = new Border
        {
            Background = ThemeColors.BrushBgSurface,
            ClipToBounds = true
        };

        _canvasScrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        _mapCanvas = new Canvas
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 35))
        };

        _mapCanvas.PointerPressed += OnCanvasPointerPressed;
        _mapCanvas.PointerReleased += OnCanvasPointerReleased;
        _mapCanvas.PointerMoved += OnCanvasPointerMoved;

        _canvasScrollViewer.Content = _mapCanvas;
        border.Child = _canvasScrollViewer;

        return border;
    }

    private void OnCanvasPointerPressed(object sender, PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(_mapCanvas);
        var tileX = (int)(pos.X / TILE_SIZE);
        var tileY = (int)(pos.Y / TILE_SIZE);

        _isDragging = true;
        _dragStartX = tileX;
        _dragStartY = tileY;

        if (_viewModel?.CurrentTool != MapEditorTool.PaintZone)
        {
            _viewModel?.OnTileClicked(tileX, tileY);
        }

        RedrawCanvas();
    }

    private void OnCanvasPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (_isDragging && _viewModel?.CurrentTool == MapEditorTool.PaintZone)
        {
            var pos = e.GetPosition(_mapCanvas);
            var tileX = (int)(pos.X / TILE_SIZE);
            var tileY = (int)(pos.Y / TILE_SIZE);

            _viewModel.OnTileDrag(_dragStartX, _dragStartY, tileX, tileY);
        }

        _isDragging = false;
        RedrawCanvas();
    }

    private void OnCanvasPointerMoved(object sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(_mapCanvas);
        var tileX = (int)(pos.X / TILE_SIZE);
        var tileY = (int)(pos.Y / TILE_SIZE);

        if (_viewModel != null)
        {
            _viewModel.HoverTileX = tileX;
            _viewModel.HoverTileY = tileY;
        }
    }

    private Control BuildPropertiesPanel()
    {
        var border = new Border
        {
            Background = ThemeColors.BrushBgPanelLeft,
            BorderBrush = ThemeColors.BrushBorder,
            BorderThickness = new Thickness(1, 0, 0, 0)
        };

        var scroll = new ScrollViewer();
        _propertiesPanel = new StackPanel();

        // Header
        _propertiesPanel.Children.Add(new TextBlock
        {
            Text = "Properties",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(12, 12, 12, 8)
        });

        // Zone properties
        _zonePropertiesPanel = BuildZonePropertiesPanel();
        _zonePropertiesPanel.IsVisible = false;
        _propertiesPanel.Children.Add(_zonePropertiesPanel);

        // Path properties
        _pathPropertiesPanel = BuildPathPropertiesPanel();
        _pathPropertiesPanel.IsVisible = false;
        _propertiesPanel.Children.Add(_pathPropertiesPanel);

        // Chunk properties
        _chunkPropertiesPanel = BuildChunkPropertiesPanel();
        _chunkPropertiesPanel.IsVisible = false;
        _propertiesPanel.Children.Add(_chunkPropertiesPanel);

        // Surface properties (just info)
        _surfacePropertiesPanel = BuildSurfacePropertiesPanel();
        _surfacePropertiesPanel.IsVisible = false;
        _propertiesPanel.Children.Add(_surfacePropertiesPanel);

        // No selection message
        var noSelectionMsg = new TextBlock
        {
            Text = "Select an item from the left panel to edit its properties.",
            FontSize = 11,
            Foreground = ThemeColors.BrushTextTertiary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(12, 8)
        };
        _propertiesPanel.Children.Add(noSelectionMsg);

        scroll.Content = _propertiesPanel;
        border.Child = scroll;
        return border;
    }

    private StackPanel BuildZonePropertiesPanel()
    {
        var panel = new StackPanel();

        panel.Children.Add(new TextBlock
        {
            Text = "Zone",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = ThemeColors.BrushTextSecondary,
            Margin = new Thickness(12, 0, 12, 8)
        });

        _zoneIdTextBox = CreateTextBox();
        panel.Children.Add(CreateLabeledRow("ID:", _zoneIdTextBox));

        _zoneNameTextBox = CreateTextBox();
        panel.Children.Add(CreateLabeledRow("Name:", _zoneNameTextBox));

        _zoneTypeCombo = new ComboBox { FontSize = 11 };
        _zoneTypeCombo.Classes.Add("input");
        _zoneTypeCombo.Bind(ComboBox.ItemsSourceProperty, new Avalonia.Data.Binding("ZoneTypes"));
        panel.Children.Add(CreateLabeledRow("Type:", _zoneTypeCombo));

        var posRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,8,*"),
            Margin = new Thickness(12, 4)
        };
        _zoneXTextBox = CreateTextBox();
        _zoneYTextBox = CreateTextBox();
        posRow.Children.Add(CreateCompactField("X:", _zoneXTextBox));
        Grid.SetColumn(posRow.Children[0], 0);
        posRow.Children.Add(CreateCompactField("Y:", _zoneYTextBox));
        Grid.SetColumn(posRow.Children[1], 2);
        panel.Children.Add(posRow);

        var sizeRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,8,*"),
            Margin = new Thickness(12, 4)
        };
        _zoneWidthTextBox = CreateTextBox();
        _zoneHeightTextBox = CreateTextBox();
        sizeRow.Children.Add(CreateCompactField("W:", _zoneWidthTextBox));
        Grid.SetColumn(sizeRow.Children[0], 0);
        sizeRow.Children.Add(CreateCompactField("H:", _zoneHeightTextBox));
        Grid.SetColumn(sizeRow.Children[1], 2);
        panel.Children.Add(sizeRow);

        _zonePriorityTextBox = CreateTextBox();
        panel.Children.Add(CreateLabeledRow("Priority:", _zonePriorityTextBox));

        var applyBtn = new Button { Content = "Apply Changes", FontSize = 11, Margin = new Thickness(12, 12, 12, 8) };
        applyBtn.Classes.Add("primary");
        applyBtn.Click += OnApplyZoneChanges;
        panel.Children.Add(applyBtn);

        return panel;
    }

    private StackPanel BuildPathPropertiesPanel()
    {
        var panel = new StackPanel();

        panel.Children.Add(new TextBlock
        {
            Text = "Path",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = ThemeColors.BrushTextSecondary,
            Margin = new Thickness(12, 0, 12, 8)
        });

        _pathIdTextBox = CreateTextBox();
        panel.Children.Add(CreateLabeledRow("ID:", _pathIdTextBox));

        _pathTypeCombo = new ComboBox { FontSize = 11 };
        _pathTypeCombo.Classes.Add("input");
        _pathTypeCombo.ItemsSource = new[] { "Road", "River", "Trail", "Trench" };
        panel.Children.Add(CreateLabeledRow("Type:", _pathTypeCombo));

        _pathWidthTextBox = CreateTextBox();
        panel.Children.Add(CreateLabeledRow("Width:", _pathWidthTextBox));

        var waypointLabel = new TextBlock
        {
            FontSize = 11,
            Foreground = ThemeColors.BrushTextTertiary,
            Margin = new Thickness(12, 8)
        };
        waypointLabel.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("SelectedPath.Waypoints.Count")
        {
            StringFormat = "{0} waypoints",
            FallbackValue = "0 waypoints"
        });
        panel.Children.Add(waypointLabel);

        var applyBtn = new Button { Content = "Apply Changes", FontSize = 11, Margin = new Thickness(12, 8) };
        applyBtn.Classes.Add("primary");
        applyBtn.Click += OnApplyPathChanges;
        panel.Children.Add(applyBtn);

        var clearBtn = new Button { Content = "Clear Waypoints", FontSize = 10, Margin = new Thickness(12, 4) };
        clearBtn.Classes.Add("secondary");
        clearBtn.Click += (_, _) =>
        {
            if (_viewModel?.SelectedPath != null)
            {
                _viewModel.SelectedPath.Waypoints.Clear();
                _viewModel.HasUnsavedChanges = true;
                RedrawCanvas();
            }
        };
        panel.Children.Add(clearBtn);

        return panel;
    }

    private StackPanel BuildChunkPropertiesPanel()
    {
        var panel = new StackPanel();

        panel.Children.Add(new TextBlock
        {
            Text = "Chunk Placement",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = ThemeColors.BrushTextSecondary,
            Margin = new Thickness(12, 0, 12, 8)
        });

        var templateLabel = new TextBlock
        {
            FontSize = 11,
            Foreground = ThemeColors.BrushTextPrimary,
            Margin = new Thickness(12, 4)
        };
        templateLabel.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("SelectedChunkPlacement.ChunkTemplate"));
        panel.Children.Add(templateLabel);

        var posLabel = new TextBlock
        {
            FontSize = 10,
            Foreground = ThemeColors.BrushTextTertiary,
            Margin = new Thickness(12, 2)
        };
        posLabel.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("SelectedChunkPlacement.DisplayText"));
        panel.Children.Add(posLabel);

        return panel;
    }

    private StackPanel BuildSurfacePropertiesPanel()
    {
        var panel = new StackPanel();

        panel.Children.Add(new TextBlock
        {
            Text = "Surface Painting",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = ThemeColors.BrushTextSecondary,
            Margin = new Thickness(12, 0, 12, 8)
        });

        panel.Children.Add(new TextBlock
        {
            Text = "Click and drag on the map to paint terrain. Use the eraser tool to remove painted tiles.",
            FontSize = 10,
            Foreground = ThemeColors.BrushTextTertiary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(12, 4)
        });

        return panel;
    }

    private TextBox CreateTextBox()
    {
        var textBox = new TextBox { FontSize = 11 };
        textBox.Classes.Add("input");
        return textBox;
    }

    private Control CreateLabeledRow(string label, Control input)
    {
        var stack = new StackPanel { Margin = new Thickness(12, 4) };
        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = ThemeColors.BrushTextTertiary,
            Margin = new Thickness(0, 0, 0, 2)
        });
        stack.Children.Add(input);
        return stack;
    }

    private Control CreateCompactField(string label, TextBox input)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = ThemeColors.BrushTextTertiary
        });
        stack.Children.Add(input);
        return stack;
    }

    private void OnApplyZoneChanges(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel?.SelectedZone == null) return;

        var zone = _viewModel.SelectedZone;
        zone.Id = _zoneIdTextBox.Text ?? zone.Id;
        zone.Name = _zoneNameTextBox.Text ?? zone.Name;

        if (int.TryParse(_zoneXTextBox.Text, out var x)) zone.X = x;
        if (int.TryParse(_zoneYTextBox.Text, out var y)) zone.Y = y;
        if (int.TryParse(_zoneWidthTextBox.Text, out var w)) zone.Width = w;
        if (int.TryParse(_zoneHeightTextBox.Text, out var h)) zone.Height = h;
        if (int.TryParse(_zonePriorityTextBox.Text, out var p)) zone.Priority = p;

        if (_zoneTypeCombo.SelectedItem is string zoneTypeStr &&
            Enum.TryParse<ZoneType>(zoneTypeStr, out var zoneType))
        {
            zone.ZoneType = zoneType;
        }

        _viewModel.HasUnsavedChanges = true;
        _viewModel.StatusText = $"Updated zone: {zone.Name}";
        RedrawCanvas();
    }

    private void OnApplyPathChanges(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel?.SelectedPath == null) return;

        var path = _viewModel.SelectedPath;
        path.Id = _pathIdTextBox.Text ?? path.Id;
        path.Type = _pathTypeCombo.SelectedItem?.ToString() ?? path.Type;

        if (int.TryParse(_pathWidthTextBox.Text, out var w)) path.Width = w;

        _viewModel.HasUnsavedChanges = true;
        _viewModel.StatusText = $"Updated path: {path.Id}";
        RedrawCanvas();
    }

    private void UpdatePropertyPanels()
    {
        if (_zonePropertiesPanel == null) return;

        // Hide all
        _zonePropertiesPanel.IsVisible = false;
        _pathPropertiesPanel.IsVisible = false;
        _chunkPropertiesPanel.IsVisible = false;
        _surfacePropertiesPanel.IsVisible = false;

        // Show relevant panel
        if (_viewModel?.SelectedZone != null)
        {
            _zonePropertiesPanel.IsVisible = true;
            var zone = _viewModel.SelectedZone;
            _zoneIdTextBox.Text = zone.Id;
            _zoneNameTextBox.Text = zone.Name;
            _zoneXTextBox.Text = zone.X.ToString();
            _zoneYTextBox.Text = zone.Y.ToString();
            _zoneWidthTextBox.Text = zone.Width.ToString();
            _zoneHeightTextBox.Text = zone.Height.ToString();
            _zonePriorityTextBox.Text = zone.Priority.ToString();
            _zoneTypeCombo.SelectedItem = zone.ZoneType.ToString();
        }
        else if (_viewModel?.SelectedPath != null)
        {
            _pathPropertiesPanel.IsVisible = true;
            var path = _viewModel.SelectedPath;
            _pathIdTextBox.Text = path.Id;
            _pathTypeCombo.SelectedItem = path.Type;
            _pathWidthTextBox.Text = path.Width.ToString();
        }
        else if (_viewModel?.SelectedChunkPlacement != null)
        {
            _chunkPropertiesPanel.IsVisible = true;
        }
        else if (_viewModel?.SelectedLayer == EditorLayer.Surfaces)
        {
            _surfacePropertiesPanel.IsVisible = true;
        }
    }

    private Border BuildChunkBrowserPanel()
    {
        var border = new Border
        {
            Background = ThemeColors.BrushBgElevated,
            BorderBrush = ThemeColors.BrushBorder,
            BorderThickness = new Thickness(0, 0, 0, 0),
            IsVisible = false
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("180,*")
        };

        // Left: Header and search
        var leftPanel = new StackPanel
        {
            Margin = new Thickness(12, 8)
        };

        leftPanel.Children.Add(new TextBlock
        {
            Text = "Chunk Browser",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var searchBox = new TextBox
        {
            Watermark = "Search...",
            FontSize = 11
        };
        searchBox.Classes.Add("input");
        searchBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("ChunkSearchFilter") { Mode = Avalonia.Data.BindingMode.TwoWay });
        leftPanel.Children.Add(searchBox);

        var selectedLabel = new TextBlock
        {
            FontSize = 10,
            Foreground = ThemeColors.BrushPrimary,
            Margin = new Thickness(0, 8, 0, 0)
        };
        selectedLabel.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("SelectedChunk.Name")
        {
            StringFormat = "Selected: {0}",
            FallbackValue = "Click to select"
        });
        leftPanel.Children.Add(selectedLabel);

        // Rotation setting
        var rotationRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(0, 8, 0, 0)
        };
        rotationRow.Children.Add(new TextBlock
        {
            Text = "Rotation:",
            FontSize = 10,
            Foreground = ThemeColors.BrushTextTertiary,
            VerticalAlignment = VerticalAlignment.Center
        });
        var rotationBox = new TextBox { FontSize = 10, Width = 50 };
        rotationBox.Classes.Add("input");
        rotationBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("ChunkRotation") { Mode = Avalonia.Data.BindingMode.TwoWay });
        rotationRow.Children.Add(rotationBox);
        leftPanel.Children.Add(rotationRow);

        grid.Children.Add(leftPanel);
        Grid.SetColumn(leftPanel, 0);

        // Right: Chunk list (horizontal scrolling)
        var chunksScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var chunksPanel = new ItemsControl
        {
            Margin = new Thickness(8)
        };
        chunksPanel.Bind(ItemsControl.ItemsSourceProperty, new Avalonia.Data.Binding("AvailableChunks"));
        chunksPanel.ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Panel>(() =>
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 });
        chunksPanel.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ChunkTemplateViewModel>(
            (chunk, _) =>
            {
                var itemBorder = new Border
                {
                    Width = 70,
                    Height = 70,
                    Background = ThemeColors.BrushBgSurface,
                    BorderBrush = ThemeColors.BrushBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    ClipToBounds = true
                };

                var stack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Container for preview image or fallback icon
                var previewContainer = new Grid
                {
                    Width = 50,
                    Height = 50,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // Preview image (bound to PreviewBitmap)
                var previewImage = new Avalonia.Controls.Image
                {
                    Width = 50,
                    Height = 50,
                    Stretch = Avalonia.Media.Stretch.Uniform
                };
                previewImage.Bind(Avalonia.Controls.Image.SourceProperty,
                    new Avalonia.Data.Binding("PreviewBitmap"));
                previewContainer.Children.Add(previewImage);

                // Fallback icon (shown when no preview available)
                var fallbackIcon = new TextBlock
                {
                    Text = "\u2302",
                    FontSize = 24,
                    Foreground = ThemeColors.BrushTextTertiary,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                // Hide icon when preview is available
                fallbackIcon.Bind(TextBlock.IsVisibleProperty,
                    new Avalonia.Data.Binding("PreviewBitmap")
                    {
                        Converter = new Avalonia.Data.Converters.FuncValueConverter<object, bool>(
                            val => val == null)
                    });
                previewContainer.Children.Add(fallbackIcon);

                // Loading indicator
                var loadingIndicator = new TextBlock
                {
                    Text = "...",
                    FontSize = 12,
                    Foreground = ThemeColors.BrushTextTertiary,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                loadingIndicator.Bind(TextBlock.IsVisibleProperty,
                    new Avalonia.Data.Binding("IsLoadingPreview"));
                previewContainer.Children.Add(loadingIndicator);

                stack.Children.Add(previewContainer);

                var nameLabel = new TextBlock
                {
                    Text = chunk?.DisplayName ?? "Chunk",
                    FontSize = 8,
                    Foreground = ThemeColors.BrushTextPrimary,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 66
                };
                stack.Children.Add(nameLabel);

                itemBorder.Child = stack;

                // Tooltip with name and size info
                if (chunk != null)
                {
                    var tooltipText = chunk.DisplayName;
                    if (!string.IsNullOrEmpty(chunk.SizeInfo))
                        tooltipText += $" ({chunk.SizeInfo} tiles)";
                    ToolTip.SetTip(itemBorder, tooltipText);
                }

                itemBorder.PointerPressed += (_, _) =>
                {
                    if (_viewModel != null && chunk != null)
                    {
                        _viewModel.SelectedChunk = chunk;
                        _viewModel.CurrentTool = MapEditorTool.PlaceChunk;
                    }
                };

                return itemBorder;
            }, true);

        chunksScroll.Content = chunksPanel;
        grid.Children.Add(chunksScroll);
        Grid.SetColumn(chunksScroll, 1);

        border.Child = grid;
        return border;
    }

    private Control BuildStatusBar()
    {
        var border = new Border
        {
            Background = ThemeColors.BrushBgSurface,
            BorderBrush = ThemeColors.BrushBorder,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 6)
        };

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16
        };

        var statusText = new TextBlock
        {
            FontSize = 11,
            Foreground = ThemeColors.BrushTextSecondary
        };
        statusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("StatusText"));
        stack.Children.Add(statusText);

        var layerText = new TextBlock
        {
            FontSize = 11,
            Foreground = ThemeColors.BrushTextTertiary
        };
        layerText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("SelectedLayer")
        {
            StringFormat = "Layer: {0}"
        });
        stack.Children.Add(layerText);

        var unsavedText = new TextBlock
        {
            Text = "(unsaved)",
            FontSize = 11,
            Foreground = ThemeColors.BrushWarning
        };
        unsavedText.Bind(TextBlock.IsVisibleProperty, new Avalonia.Data.Binding("HasUnsavedChanges"));
        stack.Children.Add(unsavedText);

        border.Child = stack;
        return border;
    }

    private void RedrawCanvas()
    {
        if (_mapCanvas == null || _viewModel == null)
            return;

        _mapCanvas.Children.Clear();

        var mapSize = _viewModel.MapSize;
        var canvasWidth = mapSize * TILE_SIZE;
        var canvasHeight = mapSize * TILE_SIZE;

        _mapCanvas.Width = canvasWidth;
        _mapCanvas.Height = canvasHeight;

        // Draw grid lines
        for (int i = 0; i <= mapSize; i++)
        {
            var vLine = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Point(i * TILE_SIZE, 0),
                EndPoint = new Point(i * TILE_SIZE, canvasHeight),
                Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                StrokeThickness = 1
            };
            _mapCanvas.Children.Add(vLine);

            var hLine = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Point(0, i * TILE_SIZE),
                EndPoint = new Point(canvasWidth, i * TILE_SIZE),
                Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                StrokeThickness = 1
            };
            _mapCanvas.Children.Add(hLine);
        }

        // Draw zones
        var zoneOpacity = _viewModel.SelectedLayer == EditorLayer.Zones ? 1.0 : 0.3;
        foreach (var zone in _viewModel.Zones)
        {
            var isSelected = zone == _viewModel.SelectedZone;
            var zoneColor = Color.Parse(zone.Color);
            var zoneRect = new Avalonia.Controls.Shapes.Rectangle
            {
                Width = zone.Width * TILE_SIZE,
                Height = zone.Height * TILE_SIZE,
                Fill = new SolidColorBrush(Color.FromArgb((byte)(isSelected ? 80 : 40), zoneColor.R, zoneColor.G, zoneColor.B)),
                Stroke = isSelected ? ThemeColors.BrushPrimary : new SolidColorBrush(zoneColor),
                StrokeThickness = isSelected ? 2 : 1,
                Opacity = zoneOpacity
            };
            Canvas.SetLeft(zoneRect, zone.X * TILE_SIZE);
            Canvas.SetTop(zoneRect, zone.Y * TILE_SIZE);
            _mapCanvas.Children.Add(zoneRect);

            var label = new TextBlock
            {
                Text = zone.Name,
                FontSize = 10,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(2),
                Opacity = zoneOpacity
            };
            Canvas.SetLeft(label, zone.X * TILE_SIZE + 2);
            Canvas.SetTop(label, zone.Y * TILE_SIZE + 2);
            _mapCanvas.Children.Add(label);
        }

        // Draw terrain tiles
        var tileOpacity = _viewModel.SelectedLayer == EditorLayer.Surfaces ? 1.0 : 0.5;
        foreach (var tile in _viewModel.Tiles)
        {
            IBrush tileBrush = tile.Terrain switch
            {
                "Trees" => new SolidColorBrush(Color.FromArgb(180, 34, 139, 34)),
                "Water" => new SolidColorBrush(Color.FromArgb(180, 65, 105, 225)),
                "HighGround" => new SolidColorBrush(Color.FromArgb(180, 139, 69, 19)),
                "Road" => new SolidColorBrush(Color.FromArgb(180, 105, 105, 105)),
                "Sand" => new SolidColorBrush(Color.FromArgb(180, 244, 164, 96)),
                "Concrete" => new SolidColorBrush(Color.FromArgb(180, 128, 128, 128)),
                _ => new SolidColorBrush(Color.FromArgb(100, 150, 150, 150))
            };

            var tileRect = new Avalonia.Controls.Shapes.Rectangle
            {
                Width = TILE_SIZE - 1,
                Height = TILE_SIZE - 1,
                Fill = tileBrush,
                Opacity = tileOpacity
            };
            Canvas.SetLeft(tileRect, tile.X * TILE_SIZE + 0.5);
            Canvas.SetTop(tileRect, tile.Y * TILE_SIZE + 0.5);
            _mapCanvas.Children.Add(tileRect);
        }

        // Draw chunk placements
        var chunkOpacity = _viewModel.SelectedLayer == EditorLayer.Chunks ? 1.0 : 0.4;
        foreach (var chunk in _viewModel.ChunkPlacements)
        {
            var isSelected = chunk == _viewModel.SelectedChunkPlacement;
            var chunkRect = new Avalonia.Controls.Shapes.Rectangle
            {
                Width = TILE_SIZE * 3 - 1,
                Height = TILE_SIZE * 3 - 1,
                Fill = new SolidColorBrush(Color.FromArgb(isSelected ? (byte)120 : (byte)80, 200, 100, 200)),
                Stroke = isSelected ? ThemeColors.BrushPrimary : new SolidColorBrush(Color.FromRgb(200, 100, 200)),
                StrokeThickness = isSelected ? 2 : 1,
                Opacity = chunkOpacity
            };
            Canvas.SetLeft(chunkRect, chunk.X * TILE_SIZE);
            Canvas.SetTop(chunkRect, chunk.Y * TILE_SIZE);
            _mapCanvas.Children.Add(chunkRect);

            var label = new TextBlock
            {
                Text = $"{chunk.ChunkTemplate} R{chunk.Rotation}°",
                FontSize = 8,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Padding = new Thickness(1),
                Opacity = chunkOpacity
            };
            Canvas.SetLeft(label, chunk.X * TILE_SIZE + 1);
            Canvas.SetTop(label, chunk.Y * TILE_SIZE + 1);
            _mapCanvas.Children.Add(label);
        }

        // Draw paths
        var pathOpacity = _viewModel.SelectedLayer == EditorLayer.Paths ? 1.0 : 0.4;
        foreach (var path in _viewModel.Paths)
        {
            if (path.Waypoints.Count < 2)
                continue;

            var isSelected = path == _viewModel.SelectedPath;
            var pathColor = path.Type switch
            {
                "River" => Color.FromRgb(50, 100, 200),
                "Trail" => Color.FromRgb(150, 100, 50),
                "Trench" => Color.FromRgb(100, 100, 100),
                _ => Color.FromRgb(200, 200, 100)
            };

            for (int i = 0; i < path.Waypoints.Count - 1; i++)
            {
                var wp1 = path.Waypoints[i];
                var wp2 = path.Waypoints[i + 1];

                var line = new Avalonia.Controls.Shapes.Line
                {
                    StartPoint = new Point(wp1.X * TILE_SIZE + TILE_SIZE / 2, wp1.Y * TILE_SIZE + TILE_SIZE / 2),
                    EndPoint = new Point(wp2.X * TILE_SIZE + TILE_SIZE / 2, wp2.Y * TILE_SIZE + TILE_SIZE / 2),
                    Stroke = new SolidColorBrush(pathColor),
                    StrokeThickness = isSelected ? path.Width * 2 : path.Width,
                    Opacity = pathOpacity
                };
                _mapCanvas.Children.Add(line);
            }

            foreach (var wp in path.Waypoints)
            {
                var marker = new Avalonia.Controls.Shapes.Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = isSelected ? ThemeColors.BrushPrimary : new SolidColorBrush(pathColor),
                    Opacity = pathOpacity
                };
                Canvas.SetLeft(marker, wp.X * TILE_SIZE + TILE_SIZE / 2 - 4);
                Canvas.SetTop(marker, wp.Y * TILE_SIZE + TILE_SIZE / 2 - 4);
                _mapCanvas.Children.Add(marker);
            }
        }

        // Draw hover highlight / brush preview
        if (_viewModel.HoverTileX >= 0 && _viewModel.HoverTileY >= 0 &&
            _viewModel.HoverTileX < mapSize && _viewModel.HoverTileY < mapSize)
        {
            // For terrain painting with radius > 0, show brush preview
            if (_viewModel.CurrentTool == MapEditorTool.PaintTerrain && _viewModel.BrushSize > 0)
            {
                var radius = _viewModel.BrushSize;
                var diameter = radius * 2 + 1; // radius 1 = 3x3, radius 2 = 5x5, etc.
                var centerX = _viewModel.HoverTileX;
                var centerY = _viewModel.HoverTileY;

                if (_viewModel.BrushSmooth)
                {
                    // Draw circular brush preview
                    var circle = new Avalonia.Controls.Shapes.Ellipse
                    {
                        Width = diameter * TILE_SIZE,
                        Height = diameter * TILE_SIZE,
                        Stroke = ThemeColors.BrushPrimary,
                        StrokeThickness = 2,
                        Fill = new SolidColorBrush(Color.FromArgb(30, 0, 200, 200)),
                        StrokeDashArray = _viewModel.BrushJitter > 0
                            ? new Avalonia.Collections.AvaloniaList<double> { 3, 2 }
                            : null
                    };
                    Canvas.SetLeft(circle, (centerX - radius) * TILE_SIZE);
                    Canvas.SetTop(circle, (centerY - radius) * TILE_SIZE);
                    _mapCanvas.Children.Add(circle);
                }
                else
                {
                    // Draw square brush preview
                    var rect = new Avalonia.Controls.Shapes.Rectangle
                    {
                        Width = diameter * TILE_SIZE,
                        Height = diameter * TILE_SIZE,
                        Stroke = ThemeColors.BrushPrimary,
                        StrokeThickness = 2,
                        Fill = new SolidColorBrush(Color.FromArgb(30, 0, 200, 200)),
                        StrokeDashArray = _viewModel.BrushJitter > 0
                            ? new Avalonia.Collections.AvaloniaList<double> { 3, 2 }
                            : null
                    };
                    Canvas.SetLeft(rect, (centerX - radius) * TILE_SIZE);
                    Canvas.SetTop(rect, (centerY - radius) * TILE_SIZE);
                    _mapCanvas.Children.Add(rect);
                }
            }
            else
            {
                // Standard single-tile hover
                var hoverRect = new Avalonia.Controls.Shapes.Rectangle
                {
                    Width = TILE_SIZE - 1,
                    Height = TILE_SIZE - 1,
                    Stroke = ThemeColors.BrushPrimary,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent
                };
                Canvas.SetLeft(hoverRect, _viewModel.HoverTileX * TILE_SIZE + 0.5);
                Canvas.SetTop(hoverRect, _viewModel.HoverTileY * TILE_SIZE + 0.5);
                _mapCanvas.Children.Add(hoverRect);
            }
        }

        // Draw drag rectangle for zone painting
        if (_isDragging && _viewModel.CurrentTool == MapEditorTool.PaintZone)
        {
            var minX = Math.Min(_dragStartX, _viewModel.HoverTileX);
            var minY = Math.Min(_dragStartY, _viewModel.HoverTileY);
            var width = Math.Abs(_viewModel.HoverTileX - _dragStartX) + 1;
            var height = Math.Abs(_viewModel.HoverTileY - _dragStartY) + 1;

            var dragRect = new Avalonia.Controls.Shapes.Rectangle
            {
                Width = width * TILE_SIZE,
                Height = height * TILE_SIZE,
                Stroke = ThemeColors.BrushPrimary,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(60, 0, 200, 200)),
                StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 2 }
            };
            Canvas.SetLeft(dragRect, minX * TILE_SIZE);
            Canvas.SetTop(dragRect, minY * TILE_SIZE);
            _mapCanvas.Children.Add(dragRect);
        }
    }
}
