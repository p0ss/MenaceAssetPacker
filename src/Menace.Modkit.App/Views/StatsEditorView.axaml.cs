using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Menace.Modkit.App.Controls;
using Menace.Modkit.App.Converters;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;
using Menace.Modkit.App.ViewModels;
using ReactiveUI;

namespace Menace.Modkit.App.Views;

public class StatsEditorView : UserControl
{
  // Converter to check if an object is non-null (for visibility bindings)
  private static readonly Avalonia.Data.Converters.FuncValueConverter<object?, bool> ObjectToBoolConverter =
    new(obj => obj != null);

  // Read-only fields that are computed from other properties and cannot be edited
  // These are displayed but editing is disabled with a tooltip explanation
  private static readonly System.Collections.Generic.HashSet<string> ReadOnlyFields = new(StringComparer.OrdinalIgnoreCase)
  {
    // Localized/computed display fields
    "DisplayTitle", "DisplayShortName", "DisplayDescription",
    // Identity fields (set by clone system, not user-editable)
    "name", "m_ID",
    // Computed Icon properties (not directly settable at runtime)
    "HasIcon", "IconAssetName"
  };

  public StatsEditorView()
  {
    Content = BuildUI();
  }

  protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
  {
    base.OnAttachedToVisualTree(e);

    // Refresh modpacks when view becomes visible to pick up newly created ones
    if (DataContext is StatsEditorViewModel vm)
      vm.LoadData();
  }

  private Control BuildUI()
  {
    // Check if we should show warning
    var contentControl = new ContentControl();

    // Bind to show either warning or main UI
    contentControl.Bind(ContentControl.IsVisibleProperty,
      new Avalonia.Data.Binding("!ShowVanillaDataWarning"));

    var warningControl = BuildVanillaDataWarning();
    warningControl.Bind(Control.IsVisibleProperty,
      new Avalonia.Data.Binding("ShowVanillaDataWarning"));

    var mainGrid = new Grid
    {
      ColumnDefinitions = new ColumnDefinitions("300,4,*")
    };

    // Left: Navigation Tree (darker panel)
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

    // Right: Detail Panel (lighter panel)
    mainGrid.Children.Add(BuildDetailPanel());
    Grid.SetColumn((Control)mainGrid.Children[2], 2);

    contentControl.Content = mainGrid;

    // Overlay both
    var overlayGrid = new Grid();
    overlayGrid.Children.Add(contentControl);
    overlayGrid.Children.Add(warningControl);

    return overlayGrid;
  }

  private Control BuildVanillaDataWarning()
  {
    var border = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
      Padding = new Thickness(48)
    };

    var stack = new StackPanel
    {
      VerticalAlignment = VerticalAlignment.Center,
      HorizontalAlignment = HorizontalAlignment.Center,
      Spacing = 24,
      MaxWidth = 600
    };

    stack.Children.Add(new TextBlock
    {
      Text = "Vanilla Game Data Not Found",
      FontSize = 20,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White,
      HorizontalAlignment = HorizontalAlignment.Center
    });

    stack.Children.Add(new TextBlock
    {
      Text = "The Stats Editor requires extracted game data to function. To set this up:",
      Foreground = Brushes.White,
      Opacity = 0.9,
      TextWrapping = TextWrapping.Wrap,
      HorizontalAlignment = HorizontalAlignment.Center,
      TextAlignment = TextAlignment.Center
    });

    var stepsPanel = new StackPanel { Spacing = 16, Margin = new Thickness(0, 16, 0, 0) };

    stepsPanel.Children.Add(CreateStep("1", "Install MelonLoader mod in your game directory"));
    stepsPanel.Children.Add(CreateStep("2", "Install the DataExtractor mod (comes with this modkit)"));
    stepsPanel.Children.Add(CreateStep("3", "Launch the game once to extract template data"));
    stepsPanel.Children.Add(CreateStep("4", "Return here to edit stats"));

    stack.Children.Add(stepsPanel);

    stack.Children.Add(new TextBlock
    {
      Text = $"Expected data location:\n{System.IO.Path.Combine(Services.AppSettings.Instance.GameInstallPath ?? "", "UserData", "ExtractedData")}",
      Foreground = Brushes.White,
      Opacity = 0.6,
      FontSize = 11,
      FontFamily = new FontFamily("monospace"),
      TextWrapping = TextWrapping.Wrap,
      HorizontalAlignment = HorizontalAlignment.Center,
      TextAlignment = TextAlignment.Center,
      Margin = new Thickness(0, 16, 0, 0)
    });

    // Button panel with Auto Setup and Refresh
    var buttonPanel = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 12,
      HorizontalAlignment = HorizontalAlignment.Center,
      Margin = new Thickness(0, 24, 0, 0)
    };

    var setupButton = new Button
    {
      Content = "Auto Setup (Install MelonLoader & DataExtractor)",
      Padding = new Thickness(24, 12),  // Larger for setup screen
      FontSize = 14
    };
    setupButton.Classes.Add("primary");
    setupButton.Click += OnAutoSetupClick;
    buttonPanel.Children.Add(setupButton);

    var refreshButton = new Button
    {
      Content = "Refresh",
      Padding = new Thickness(24, 12),  // Larger for setup screen
      FontSize = 14
    };
    refreshButton.Classes.Add("selected");  // Grey with teal border
    refreshButton.Click += OnRefreshClick;
    buttonPanel.Children.Add(refreshButton);

    var launchButton = new Button
    {
      Content = "Launch Game to Update Data",
      Padding = new Thickness(24, 12),  // Larger for setup screen
      FontSize = 14
    };
    launchButton.Classes.Add("selected");  // Grey with teal border
    launchButton.Click += OnLaunchGameClick;
    buttonPanel.Children.Add(launchButton);

    stack.Children.Add(buttonPanel);

    // Status text for setup progress
    var statusText = new TextBlock
    {
      Foreground = Brushes.White,
      Opacity = 0.8,
      FontSize = 12,
      TextWrapping = TextWrapping.Wrap,
      HorizontalAlignment = HorizontalAlignment.Center,
      TextAlignment = TextAlignment.Center,
      Margin = new Thickness(0, 12, 0, 0)
    };
    statusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("SetupStatus"));
    stack.Children.Add(statusText);

    border.Child = stack;
    return border;
  }

  private async void OnAutoSetupClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    try
    {
      if (DataContext is StatsEditorViewModel vm)
      {
        await vm.AutoSetupAsync();
      }
    }
    catch (Exception ex)
    {
      Services.ModkitLog.Error($"Auto setup failed: {ex.Message}");
    }
  }

  private void OnRefreshClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    if (DataContext is StatsEditorViewModel vm)
    {
      vm.LoadData();
    }
  }

  private async void OnLaunchGameClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    try
    {
      if (DataContext is StatsEditorViewModel vm)
      {
        await vm.LaunchGameToUpdateDataAsync();
      }
    }
    catch (Exception ex)
    {
      Services.ModkitLog.Error($"Launch game failed: {ex.Message}");
    }
  }

  private Control CreateStep(string number, string text)
  {
    var panel = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 12
    };

    var numberBorder = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#004f43")),
      CornerRadius = new CornerRadius(16),
      Width = 32,
      Height = 32
    };

    var numberText = new TextBlock
    {
      Text = number,
      Foreground = Brushes.White,
      FontWeight = FontWeight.Bold,
      VerticalAlignment = VerticalAlignment.Center,
      HorizontalAlignment = HorizontalAlignment.Center
    };
    numberBorder.Child = numberText;

    panel.Children.Add(numberBorder);
    panel.Children.Add(new TextBlock
    {
      Text = text,
      Foreground = Brushes.White,
      VerticalAlignment = VerticalAlignment.Center,
      TextWrapping = TextWrapping.Wrap
    });

    return panel;
  }

  private Control BuildNavigation()
  {
    var grid = new Grid
    {
      RowDefinitions = new RowDefinitions("Auto,Auto,*")
    };

    // Row 0: Search Box
    var searchBox = new TextBox
    {
      Watermark = "Search templates... (3+ chars or Enter)",
      Margin = new Thickness(8, 8, 8, 12)
    };
    searchBox.Classes.Add("search");
    searchBox.Bind(TextBox.TextProperty,
      new Avalonia.Data.Binding("SearchText"));
    searchBox.KeyDown += (s, e) =>
    {
      if (e.Key == Avalonia.Input.Key.Enter && DataContext is StatsEditorViewModel vm)
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
      if (DataContext is StatsEditorViewModel vm)
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
      if (DataContext is StatsEditorViewModel vm)
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

    // Spacer to push Create Modpack to the right
    buttonPanel.Children.Add(new Border { Width = 8 });

    var createModpackButton = new Button
    {
      Content = "+ Create Modpack",
      FontSize = 11
    };
    createModpackButton.Classes.Add("primary");
    createModpackButton.Click += async (_, _) => await ShowCreateModpackDialogAsync();
    buttonPanel.Children.Add(createModpackButton);

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
      MinWidth = 120,
      MaxDropDownHeight = 500
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
      if (sortCombo.SelectedIndex >= 0 && DataContext is StatsEditorViewModel vm)
        vm.CurrentSortOption = (SearchPanelBuilder.SortOption)sortCombo.SelectedIndex;
    };
    searchControlsPanel.Children.Add(sortCombo);

    buttonContainer.Children.Add(searchControlsPanel);

    grid.Children.Add(buttonContainer);
    Grid.SetRow(buttonContainer, 1);

    // Row 2: Toggle between TreeView and Search Results ListBox
    var contentContainer = new Panel();

    // Hierarchical TreeView (non-virtualizing so Expand All works fully) - shown when not searching
    var treeView = new TreeView
    {
      Background = Brushes.Transparent,
      BorderThickness = new Thickness(0),
      ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Panel?>(() => new StackPanel())
    };
    treeView.Bind(TreeView.ItemsSourceProperty,
      new Avalonia.Data.Binding("TreeNodes"));
    treeView.Bind(TreeView.SelectedItemProperty,
      new Avalonia.Data.Binding("SelectedNode") { Mode = Avalonia.Data.BindingMode.TwoWay });
    treeView.Bind(TreeView.IsVisibleProperty,
      new Avalonia.Data.Binding("IsSearching") { Converter = BoolInverseConverter.Instance });

    // Tree item template
    treeView.ItemTemplate = new Avalonia.Controls.Templates.FuncTreeDataTemplate<TreeNodeViewModel>(
      (node, _) =>
      {
        var text = new TextBlock
        {
          Text = node.Name,
          FontWeight = node.IsCategory ? FontWeight.SemiBold : FontWeight.Normal,
          Foreground = Brushes.White,
          FontSize = node.IsCategory ? 13 : 12,
          Margin = new Thickness(8, 8)
        };
        return text;
      },
      node => node.Children);

    // Bind IsExpanded to TreeViewItem containers
    treeView.ContainerPrepared += (_, e) =>
    {
      if (e.Container is TreeViewItem tvi && tvi.DataContext is TreeNodeViewModel nodeVm)
      {
        // Set initial value before binding so the container starts in the correct state.
        // Without this, Avalonia defaults to collapsed and the binding may not propagate
        // in time for cascading expansion to work.
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
          DataContext is StatsEditorViewModel vm)
      {
        vm.SelectSearchResult(item);
      }
    };

    // Double-click to select and exit search mode
    searchResultsList.DoubleTapped += (s, e) =>
    {
      if (searchResultsList.SelectedItem is SearchResultItem item &&
          DataContext is StatsEditorViewModel vm)
      {
        vm.SelectAndExitSearch(item);
      }
    };

    contentContainer.Children.Add(searchResultsList);

    grid.Children.Add(contentContainer);
    Grid.SetRow(contentContainer, 2);

    return grid;
  }

  private Control BuildDetailPanel()
  {
    var border = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1A1A1A")),
      Padding = new Thickness(24)
    };

    // Outer grid: toolbar row + content row + splitter + backlinks row
    // Initial size: 3/4 for content, 1/4 for backlinks
    var outerGrid = new Grid
    {
      RowDefinitions = new RowDefinitions("Auto,3*,4,*")
    };

    // Toolbar row
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
          selected == StatsEditorViewModel.CreateNewModOption)
      {
        isHandlingCreateNew = true;
        try
        {
          // Clear selection immediately to prevent re-triggering
          var vm = DataContext as StatsEditorViewModel;
          var previousSelection = vm?.CurrentModpackName;

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

    var saveButton = new Button
    {
      Content = "Save",
      FontSize = 12
    };
    saveButton.Classes.Add("primary");
    saveButton.Click += OnSaveClick;
    toolbar.Children.Add(saveButton);

    var cloneButton = new Button
    {
      Content = "Clone",
      FontSize = 12
    };
    cloneButton.Classes.Add("secondary");
    cloneButton.Click += OnCloneClick;
    toolbar.Children.Add(cloneButton);

    var cloneWizardButton = new Button
    {
      Content = "Clone with Wizard...",
      FontSize = 12
    };
    cloneWizardButton.Classes.Add("secondary");
    cloneWizardButton.Click += OnCloneWithWizardClick;
    toolbar.Children.Add(cloneWizardButton);

    var reloadButton = new Button
    {
      Content = "Reload Data",
      FontSize = 12
    };
    reloadButton.Classes.Add("secondary");
    reloadButton.Click += (_, _) =>
    {
      if (DataContext is StatsEditorViewModel vm)
      {
        vm.LoadData();
        vm.SaveStatus = "Data reloaded";
      }
    };
    toolbar.Children.Add(reloadButton);

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

    // Content row: two-column vanilla/modified grid
    var mainGrid = new Grid
    {
      ColumnDefinitions = new ColumnDefinitions("*,*")
    };

    // Left: Vanilla Stats (use Grid so ScrollViewer gets constrained height)
    var vanillaPanel = new Grid
    {
      Margin = new Thickness(0, 0, 12, 0),
      RowDefinitions = new RowDefinitions("Auto,*")
    };
    var vanillaHeader = new TextBlock
    {
      Text = "Vanilla",
      FontSize = 16,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White,
      Margin = new Thickness(0, 0, 0, 12)
    };
    vanillaPanel.Children.Add(vanillaHeader);
    Grid.SetRow(vanillaHeader, 0);

    var vanillaScrollViewer = new ScrollViewer
    {
      Background = new SolidColorBrush(Color.Parse("#252525")),
      Padding = new Thickness(16),
      VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
    };

    var vanillaContent = new ContentControl();
    vanillaContent.Bind(ContentControl.ContentProperty,
      new Avalonia.Data.Binding("VanillaProperties"));
    vanillaContent.ContentTemplate = CreatePropertyGridTemplate(isEditable: false);

    vanillaScrollViewer.Content = vanillaContent;
    vanillaPanel.Children.Add(vanillaScrollViewer);
    Grid.SetRow(vanillaScrollViewer, 1);

    mainGrid.Children.Add(vanillaPanel);
    Grid.SetColumn(vanillaPanel, 0);

    // Right: Modified Stats (use Grid so ScrollViewer gets constrained height)
    var modifiedPanel = new Grid
    {
      Margin = new Thickness(12, 0, 0, 0),
      RowDefinitions = new RowDefinitions("Auto,*")
    };

    // Header row with title and Reset button
    var modifiedHeaderRow = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Margin = new Thickness(0, 0, 0, 12)
    };
    var modifiedHeader = new TextBlock
    {
      Text = "Modified",
      FontSize = 16,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White,
      VerticalAlignment = VerticalAlignment.Center
    };
    modifiedHeaderRow.Children.Add(modifiedHeader);

    var resetButton = new Button
    {
      Content = "Reset to Vanilla",
      FontSize = 11,
      Margin = new Thickness(12, 0, 0, 0),
      VerticalAlignment = VerticalAlignment.Center
    };
    resetButton.Classes.Add("destructive");
    resetButton.Click += OnResetToVanillaClick;
    // Show reset button when a node is selected, enable only when there are modifications
    resetButton.Bind(Button.IsVisibleProperty,
      new Avalonia.Data.Binding("SelectedNode") { Converter = ObjectToBoolConverter });
    resetButton.Bind(Button.IsEnabledProperty,
      new Avalonia.Data.Binding("HasModifications"));
    modifiedHeaderRow.Children.Add(resetButton);

    modifiedPanel.Children.Add(modifiedHeaderRow);
    Grid.SetRow(modifiedHeaderRow, 0);

    var modifiedScrollViewer = new ScrollViewer
    {
      Background = new SolidColorBrush(Color.Parse("#252525")),
      Padding = new Thickness(16),
      VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
    };

    var modifiedContent = new ContentControl();
    modifiedContent.Bind(ContentControl.ContentProperty,
      new Avalonia.Data.Binding("ModifiedProperties"));
    modifiedContent.ContentTemplate = CreatePropertyGridTemplate(isEditable: true);

    modifiedScrollViewer.Content = modifiedContent;
    modifiedPanel.Children.Add(modifiedScrollViewer);
    Grid.SetRow(modifiedScrollViewer, 1);

    mainGrid.Children.Add(modifiedPanel);
    Grid.SetColumn(modifiedPanel, 1);

    outerGrid.Children.Add(mainGrid);
    Grid.SetRow(mainGrid, 1);

    // Horizontal splitter between content and backlinks
    var backlinksSplitter = new GridSplitter
    {
      Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
      ResizeDirection = GridResizeDirection.Rows
    };
    outerGrid.Children.Add(backlinksSplitter);
    Grid.SetRow(backlinksSplitter, 2);

    // What Links Here panel at the bottom
    var backlinksPanel = BuildBacklinksPanel();
    outerGrid.Children.Add(backlinksPanel);
    Grid.SetRow(backlinksPanel, 3);

    border.Child = outerGrid;
    return border;
  }

  private Control BuildBacklinksPanel()
  {
    var panel = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1A1A1A")),
      BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
      BorderThickness = new Thickness(0, 1, 0, 0),
      Padding = new Thickness(12, 8)
    };

    // Use a Grid with ScrollViewer for the content
    var panelGrid = new Grid
    {
      RowDefinitions = new RowDefinitions("Auto,*")
    };

    var header = new TextBlock
    {
      Text = "What Links Here",
      FontSize = 13,
      FontWeight = FontWeight.SemiBold,
      Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
      Margin = new Thickness(0, 0, 0, 8)
    };
    panelGrid.Children.Add(header);
    Grid.SetRow(header, 0);

    var scrollViewer = new ScrollViewer();
    var stack = new StackPanel { Spacing = 6 };

    var itemsControl = new ItemsControl();
    itemsControl.Bind(ItemsControl.ItemsSourceProperty,
      new Avalonia.Data.Binding("Backlinks"));

    itemsControl.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Models.ReferenceEntry>((entry, _) =>
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

      button.Content = textPanel;

      button.Click += (_, _) =>
      {
        if (DataContext is StatsEditorViewModel vm)
        {
          vm.NavigateToEntry(vm.CurrentModpackName ?? "", entry.SourceTemplateType, entry.SourceInstanceName);
        }
      };

      return button;
    });

    stack.Children.Add(itemsControl);

    // Empty state message
    var emptyText = new TextBlock
    {
      Text = "No other templates reference this one",
      Foreground = Brushes.White,
      Opacity = 0.5,
      FontSize = 11,
      FontStyle = FontStyle.Italic
    };
    emptyText.Bind(TextBlock.IsVisibleProperty,
      new Avalonia.Data.Binding("Backlinks.Count")
      {
        Converter = new Avalonia.Data.Converters.FuncValueConverter<int, bool>(c => c == 0)
      });
    stack.Children.Add(emptyText);

    // Wrap stack in scroll viewer and add to grid
    scrollViewer.Content = stack;
    panelGrid.Children.Add(scrollViewer);
    Grid.SetRow(scrollViewer, 1);

    panel.Child = panelGrid;

    // Hide the entire panel when no template is selected
    panel.Bind(Border.IsVisibleProperty,
      new Avalonia.Data.Binding("SelectedNode")
      {
        Converter = new Avalonia.Data.Converters.FuncValueConverter<object?, bool>(n =>
          n is TreeNodeViewModel node && node.Template != null)
      });

    return panel;
  }

  private void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    if (DataContext is StatsEditorViewModel vm)
    {
      vm.SaveToStaging();
    }
  }

  private async void OnResetToVanillaClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    if (DataContext is StatsEditorViewModel vm)
    {
      var topLevel = TopLevel.GetTopLevel(this);
      if (topLevel is not Window window) return;

      var confirmed = await ConfirmationDialog.ShowAsync(
        window,
        "Reset to Vanilla",
        "This will delete all your stat overrides and revert to vanilla values. This cannot be undone.",
        "Reset",
        isDestructive: true
      );

      if (confirmed)
        vm.ResetToVanilla();
    }
  }

  private async void OnCloneClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    try
    {
      if (DataContext is not StatsEditorViewModel vm)
        return;

      if (vm.SelectedNode?.Template == null)
      {
        vm.SaveStatus = "Select a template to clone";
        return;
      }

      if (string.IsNullOrEmpty(vm.CurrentModpackName))
      {
        vm.SaveStatus = "Select a modpack first";
        return;
      }

    // Show a simple dialog to get the clone name
    var dialog = new Window
    {
      Title = "Clone Template",
      Width = 450,
      Height = 180,
      WindowStartupLocation = WindowStartupLocation.CenterOwner,
      Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
      CanResize = false
    };

    var panel = new StackPanel
    {
      Margin = new Thickness(24),
      Spacing = 12
    };

    panel.Children.Add(new TextBlock
    {
      Text = $"Clone '{vm.SelectedNode.Template.Name}'",
      Foreground = Brushes.White,
      FontSize = 14,
      FontWeight = FontWeight.SemiBold
    });

    panel.Children.Add(new TextBlock
    {
      Text = "Enter a name for the new template (e.g. enemy.pirate_scavengers_elite):",
      Foreground = Brushes.White,
      Opacity = 0.8,
      FontSize = 12
    });

    var nameInput = new TextBox
    {
      Text = vm.SelectedNode.Template.Name + "_clone",
      FontSize = 12
    };
    nameInput.Classes.Add("input");
    panel.Children.Add(nameInput);

    var buttonRow = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 8,
      HorizontalAlignment = HorizontalAlignment.Right
    };

    var cancelBtn = new Button
    {
      Content = "Cancel",
      FontSize = 12
    };
    cancelBtn.Classes.Add("secondary");
    cancelBtn.Click += (_, _) => dialog.Close();
    buttonRow.Children.Add(cancelBtn);

    var okBtn = new Button
    {
      Content = "Clone",
      FontSize = 12
    };
    okBtn.Classes.Add("primary");
    okBtn.Click += (_, _) =>
    {
      var newName = nameInput.Text?.Trim();
      if (string.IsNullOrEmpty(newName))
        return;

      if (vm.CloneTemplate(newName))
      {
        vm.SaveStatus = $"Cloned template as '{newName}'";
        dialog.Close();
      }
      else
      {
        vm.SaveStatus = $"Clone failed — name '{newName}' may already exist";
      }
    };
    buttonRow.Children.Add(okBtn);

    panel.Children.Add(buttonRow);
    dialog.Content = panel;

    var topLevel = TopLevel.GetTopLevel(this);
    if (topLevel is Window parentWindow)
      await dialog.ShowDialog(parentWindow);
    }
    catch (Exception ex)
    {
      Services.ModkitLog.Error($"Clone template failed: {ex.Message}");
    }
  }

  private async void OnCloneWithWizardClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    try
    {
      if (DataContext is not StatsEditorViewModel vm)
        return;

      var context = vm.GetCloneWizardContext();
      if (context == null)
      {
        vm.SaveStatus = "Select a template and modpack first";
        return;
      }

      var (templateType, instanceName, modpackName, refGraph, schema, vanillaPath) = context.Value;

      var dialog = new CloningWizardDialog(
        templateType,
        instanceName,
        modpackName,
        refGraph,
        schema,
        vanillaPath);

      var topLevel = TopLevel.GetTopLevel(this);
      if (topLevel is Window parentWindow)
      {
        var result = await dialog.ShowDialog<Models.CloneWizardResult?>(parentWindow);
        if (result != null)
        {
          vm.ApplyCloneWizardResult(result);
        }
      }
    }
    catch (Exception ex)
    {
      Services.ModkitLog.Error($"Clone wizard failed: {ex.Message}");
    }
  }

  private Avalonia.Controls.Templates.IDataTemplate CreatePropertyGridTemplate(bool isEditable)
  {
    return new Avalonia.Controls.Templates.FuncDataTemplate<System.Collections.Generic.Dictionary<string, object?>>((props, _) =>
    {
      if (props == null)
      {
        return new TextBlock
        {
          Text = "Select a template to view stats",
          Foreground = Brushes.White,
          Opacity = 0.6
        };
      }

      var panel = new StackPanel { Spacing = 12, Margin = new Thickness(0, 0, 0, 60) };

      string? currentGroup = null;
      StackPanel? groupPanel = null;

      foreach (var kvp in props)
      {
        var dotIdx = kvp.Key.IndexOf('.');
        if (dotIdx > 0)
        {
          var prefix = kvp.Key[..dotIdx];
          if (prefix != currentGroup)
          {
            currentGroup = prefix;
            // Section header for the nested object group
            var header = new TextBlock
            {
              Text = prefix,
              FontSize = 13,
              FontWeight = FontWeight.SemiBold,
              Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
              Margin = new Thickness(0, 8, 0, 4)
            };
            panel.Children.Add(header);
            groupPanel = new StackPanel { Spacing = 8, Margin = new Thickness(16, 0, 0, 0) };
            panel.Children.Add(groupPanel);
          }
          var fieldControl = CreatePropertyField(kvp.Key, kvp.Value, isEditable, 0);
          groupPanel!.Children.Add(fieldControl);
        }
        else
        {
          currentGroup = null;
          groupPanel = null;
          var fieldControl = CreatePropertyField(kvp.Key, kvp.Value, isEditable, 0);
          panel.Children.Add(fieldControl);
        }
      }

      return panel;
    });
  }

  private Control CreatePropertyField(string name, object? value, bool isEditable, int indent)
  {
    var fieldStack = new StackPanel { Spacing = 4, Margin = new Thickness(indent * 16, 0, 0, 0) };

    // Check if this is a read-only computed field (e.g., DisplayTitle derives from Title)
    var fieldNamePart = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
    var isReadOnlyField = ReadOnlyFields.Contains(fieldNamePart);
    if (isReadOnlyField)
      isEditable = false;

    // Property label (show just sub-field name for dotted keys)
    var displayName = fieldNamePart;
    var label = new TextBlock
    {
      Text = isReadOnlyField ? $"{displayName} (read-only)" : displayName,
      Foreground = isReadOnlyField ? new SolidColorBrush(Color.Parse("#888888")) : Brushes.White,
      Opacity = isReadOnlyField ? 0.6 : 0.8,
      FontSize = 11,
      FontWeight = FontWeight.SemiBold
    };
    if (isReadOnlyField)
      ToolTip.SetTip(label, "This field is computed from other properties and cannot be edited directly. Edit 'Title', 'ShortName', or 'Description' instead.");
    fieldStack.Children.Add(label);

    // Handle AssetPropertyValue (unity_asset fields)
    if (value is AssetPropertyValue assetValue)
    {
      fieldStack.Children.Add(CreateAssetFieldControl(assetValue, isEditable));
      return fieldStack;
    }

    // Handle nested objects and arrays
    if (value is System.Text.Json.JsonElement jsonElement)
    {
      switch (jsonElement.ValueKind)
      {
        case System.Text.Json.JsonValueKind.Object:
          // Deeply nested object (2+ levels) — render editable with sync callback.
          // The parent field key (name) is used to update the entire nested object.
          var nestedDict = new System.Collections.Generic.Dictionary<string, object?>();
          foreach (var prop in jsonElement.EnumerateObject())
            nestedDict[prop.Name] = prop.Value.Clone();

          var nestedObjPanel = new StackPanel { Spacing = 8, Margin = new Thickness(16, 4, 0, 0) };
          foreach (var prop in nestedDict.ToList())
          {
            nestedObjPanel.Children.Add(CreateObjectFieldControl(
              prop.Key, prop.Value, nestedDict, null, isEditable, () =>
              {
                // Sync the entire nested object back to ModifiedProperties
                if (DataContext is StatsEditorViewModel nestedVm)
                {
                  var json = SerializeDict(nestedDict);
                  nestedVm.UpdateComplexArrayProperty(name, json.GetRawText());
                }
              }));
          }
          fieldStack.Children.Add(nestedObjPanel);
          return fieldStack;

        case System.Text.Json.JsonValueKind.Array:
          // Check if this is a template reference collection
          if (DataContext is StatsEditorViewModel arrVm)
          {
            var elementType = arrVm.GetTemplateRefElementType(name);
            if (elementType != null)
            {
              fieldStack.Children.Add(CreateTemplateRefListControl(name, jsonElement, elementType, isEditable));
              return fieldStack;
            }
          }

          // Check if array elements are objects — render as expandable groups
          if (ArrayContainsObjects(jsonElement))
          {
            fieldStack.Children.Add(CreateObjectArrayControl(name, jsonElement, isEditable));
            return fieldStack;
          }

          // Non-template array — render as individual rows with serialized node detection
          {
            var dummyElement = new System.Collections.Generic.Dictionary<string, object?> { { name, jsonElement.Clone() } };
            fieldStack.Children.Add(CreatePrimitiveArrayControl(
              name, jsonElement, dummyElement, isEditable, () =>
              {
                if (DataContext is StatsEditorViewModel primVm && dummyElement.TryGetValue(name, out var val))
                {
                  if (val is System.Text.Json.JsonElement je2)
                    primVm.UpdateComplexArrayProperty(name, je2.GetRawText());
                }
              }));
            return fieldStack;
          }

        default:
          // Extract the actual primitive value from JsonElement
          value = jsonElement.ValueKind switch
          {
            System.Text.Json.JsonValueKind.String => jsonElement.GetString(),
            System.Text.Json.JsonValueKind.Number => jsonElement.GetDouble().ToString(),
            System.Text.Json.JsonValueKind.True => (object)true,
            System.Text.Json.JsonValueKind.False => (object)false,
            System.Text.Json.JsonValueKind.Null => "null",
            _ => jsonElement.ToString()
          };
          break;
      }
    }

    // Coerce string booleans back to bool (can happen from staging overrides)
    if (value is string strBool && bool.TryParse(strBool, out var parsedBool))
      value = parsedBool;

    // Boolean fields: render as CheckBox instead of TextBox to avoid string conversion issues
    if (value is bool boolVal)
    {
      var checkBox = new CheckBox
      {
        IsChecked = boolVal,
        IsEnabled = isEditable,
        Content = boolVal ? "True" : "False",
        Foreground = Brushes.White,
        FontSize = 12,
        Tag = name,
        Margin = new Thickness(0, 2)
      };
      if (isEditable)
      {
        checkBox.IsCheckedChanged += (s, _) =>
        {
          if (s is CheckBox cb && cb.Tag is string fieldName && DataContext is StatsEditorViewModel vm)
          {
            var isChecked = cb.IsChecked ?? false;
            cb.Content = isChecked ? "True" : "False";
            vm.UpdateModifiedBoolProperty(fieldName, isChecked);
          }
        };
      }
      fieldStack.Children.Add(checkBox);
      return fieldStack;
    }

    // Property value (other primitive types)
    if (isEditable)
    {
      var textBox = new TextBox
      {
        Text = value?.ToString() ?? "",
        Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        Foreground = Brushes.White,
        BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(8, 6),
        FontSize = 12,
        Tag = name,
        TextWrapping = TextWrapping.Wrap,
        AcceptsReturn = true,
        MaxHeight = 200  // Prevent excessively tall text boxes
      };
      textBox.LostFocus += OnEditableTextBoxLostFocus;  // Use LostFocus instead of TextChanged for stability
      fieldStack.Children.Add(textBox);
    }
    else
    {
      // Use a TextBox for vanilla side too, but make it read-only
      // This ensures consistent height with the editable side
      var valueBox = new TextBox
      {
        Text = value?.ToString() ?? "null",
        Background = Brushes.Transparent,
        Foreground = Brushes.White,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(8, 6),
        FontSize = 12,
        IsReadOnly = true,
        Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Arrow),
        TextWrapping = TextWrapping.Wrap
      };
      fieldStack.Children.Add(valueBox);
    }

    return fieldStack;
  }

  /// <summary>
  /// Creates the visual control for an asset field (Sprite, Texture2D, etc.)
  /// </summary>
  private Control CreateAssetFieldControl(AssetPropertyValue assetValue, bool isEditable)
  {
    var container = new StackPanel { Spacing = 6 };

    // Row 1: Type badge + asset name
    var headerRow = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 8,
      VerticalAlignment = VerticalAlignment.Center
    };

    // Asset type badge
    var badgeColor = GetAssetTypeBadgeColor(assetValue.AssetType);
    var badge = new Border
    {
      Background = new SolidColorBrush(badgeColor),
      CornerRadius = new CornerRadius(3),
      Padding = new Thickness(6, 2),
      VerticalAlignment = VerticalAlignment.Center
    };
    badge.Child = new TextBlock
    {
      Text = assetValue.AssetType,
      Foreground = Brushes.White,
      FontSize = 10,
      FontWeight = FontWeight.SemiBold
    };
    headerRow.Children.Add(badge);

    // Asset name or "(unresolved)" / "null"
    var nameText = new TextBlock
    {
      Text = assetValue.DisplayText,
      Foreground = assetValue.IsResolved
        ? Brushes.White
        : new SolidColorBrush(Color.Parse("#888888")),
      FontSize = 12,
      FontStyle = assetValue.IsResolved ? FontStyle.Normal : FontStyle.Italic,
      VerticalAlignment = VerticalAlignment.Center,
      TextWrapping = TextWrapping.Wrap
    };
    headerRow.Children.Add(nameText);

    container.Children.Add(headerRow);

    // Row 2: Thumbnail preview (if available for Sprite/Texture2D)
    if (assetValue.HasThumbnail && assetValue.ThumbnailPath != null)
    {
      try
      {
        if (System.IO.File.Exists(assetValue.ThumbnailPath))
        {
          var thumbnailBorder = new Border
          {
            Background = new SolidColorBrush(Color.Parse("#1A1A1A")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Left
          };

          var image = new Image
          {
            Width = 32,
            Height = 32,
            Stretch = Stretch.Uniform,
            Source = new Bitmap(assetValue.ThumbnailPath)
          };

          thumbnailBorder.Child = image;
          container.Children.Add(thumbnailBorder);
        }
      }
      catch
      {
        // Silently ignore thumbnail load failures
      }
    }

    // Row 3: Browse/Clear buttons (editable side only)
    if (isEditable)
    {
      var buttonRow = new StackPanel
      {
        Orientation = Orientation.Horizontal,
        Spacing = 6
      };

      var browseButton = new Button
      {
        Content = "Browse...",
        FontSize = 11
      };
      browseButton.Classes.Add("primary");
      browseButton.Click += async (_, _) =>
      {
        try
        {
          if (DataContext is StatsEditorViewModel vm)
          {
            var modpackName = vm.CurrentModpackName;
            var modpackMgr = vm.ModpackManager;
            var dialog = new AssetPickerDialog(assetValue.AssetType, modpackName, modpackMgr);
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window window)
            {
              var result = await dialog.ShowDialog<string?>(window);
              if (result != null)
              {
                // Update the asset value
                assetValue.AssetName = System.IO.Path.GetFileNameWithoutExtension(result);
                assetValue.AssetFilePath = result;
                assetValue.ThumbnailPath = result;
                nameText.Text = assetValue.DisplayText;
                nameText.Foreground = Brushes.White;
                nameText.FontStyle = FontStyle.Normal;

                // Mark this field as edited for change tracking
                vm.MarkAssetFieldEdited(assetValue.FieldName);
              }
            }
          }
        }
        catch (System.Exception ex)
        {
          Services.ModkitLog.Error($"AssetPickerDialog browse failed: {ex}");
        }
      };
      buttonRow.Children.Add(browseButton);

      var clearButton = new Button
      {
        Content = "Clear",
        FontSize = 11
      };
      clearButton.Classes.Add("secondary");
      clearButton.Click += (_, _) =>
      {
        assetValue.AssetName = null;
        assetValue.AssetFilePath = null;
        assetValue.ThumbnailPath = null;
        assetValue.RawValue = null;
        nameText.Text = "null";
        nameText.Foreground = new SolidColorBrush(Color.Parse("#888888"));
        nameText.FontStyle = FontStyle.Italic;

        // Mark this field as edited for change tracking
        if (DataContext is StatsEditorViewModel vm)
          vm.MarkAssetFieldEdited(assetValue.FieldName);
      };
      buttonRow.Children.Add(clearButton);

      container.Children.Add(buttonRow);
    }

    return container;
  }

  private static Color GetAssetTypeBadgeColor(string assetType)
  {
    return assetType switch
    {
      "Sprite" => Color.Parse("#2D6A4F"),
      "Texture2D" => Color.Parse("#1B4332"),
      "Material" => Color.Parse("#4A3068"),
      "Mesh" => Color.Parse("#3A5A80"),
      "AudioClip" => Color.Parse("#7A4420"),
      "AnimationClip" => Color.Parse("#6B3030"),
      "GameObject" => Color.Parse("#5A5A20"),
      _ => Color.Parse("#3E3E3E"),
    };
  }

  private Control CreateTemplateRefListControl(string fieldName, System.Text.Json.JsonElement jsonElement, string elementType, bool isEditable)
  {
    var vm = DataContext as StatsEditorViewModel;
    var items = new System.Collections.Generic.List<string>();
    foreach (var el in jsonElement.EnumerateArray())
    {
      var s = el.ValueKind == System.Text.Json.JsonValueKind.String ? el.GetString() : el.GetRawText();
      if (!string.IsNullOrEmpty(s))
        items.Add(s);
    }

    var outerPanel = new StackPanel { Spacing = 4 };
    var itemsPanel = new StackPanel { Spacing = 0 };

    void RebuildItemsPanel()
    {
      itemsPanel.Children.Clear();
      if (items.Count == 0)
      {
        itemsPanel.Children.Add(new TextBlock
        {
          Text = "(empty)",
          Foreground = new SolidColorBrush(Color.Parse("#888888")),
          FontStyle = FontStyle.Italic,
          FontSize = 12,
          Padding = new Thickness(8, 4)
        });
        return;
      }
      for (int i = 0; i < items.Count; i++)
      {
        var idx = i;
        var rowBg = i % 2 == 0
          ? new SolidColorBrush(Color.Parse("#1E1E1E"))
          : new SolidColorBrush(Color.Parse("#252525"));

        var row = new Grid
        {
          ColumnDefinitions = isEditable
            ? new ColumnDefinitions("*,Auto")
            : new ColumnDefinitions("*"),
          Background = rowBg
        };

        var nameBlock = new TextBlock
        {
          Text = items[idx],
          Foreground = Brushes.White,
          FontSize = 12,
          Padding = new Thickness(8, 4),
          VerticalAlignment = VerticalAlignment.Center,
          TextTrimming = TextTrimming.CharacterEllipsis
        };
        row.Children.Add(nameBlock);
        Grid.SetColumn(nameBlock, 0);

        if (isEditable)
        {
          var removeBtn = new Button
          {
            Content = "\u2715",
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.Parse("#CC4444")),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 2),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
          };
          removeBtn.Click += (_, _) =>
          {
            items.RemoveAt(idx);
            RebuildItemsPanel();
            SyncCollectionToViewModel(fieldName, items);
          };
          row.Children.Add(removeBtn);
          Grid.SetColumn(removeBtn, 1);
        }

        itemsPanel.Children.Add(row);
      }
    }

    RebuildItemsPanel();
    outerPanel.Children.Add(itemsPanel);

    if (isEditable && vm != null)
    {
      var instanceNames = vm.GetTemplateInstanceNames(elementType);

      var addRow = new Grid
      {
        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        Margin = new Thickness(0, 4, 0, 0)
      };

      var autoComplete = new AutoCompleteBox
      {
        Watermark = $"Add {elementType}...",
        ItemsSource = instanceNames,
        FilterMode = AutoCompleteFilterMode.ContainsOrdinal,
        MinimumPrefixLength = 0,
        Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        Foreground = Brushes.White,
        BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
        BorderThickness = new Thickness(1),
        FontSize = 12,
        MinWidth = 100
      };
      addRow.Children.Add(autoComplete);
      Grid.SetColumn(autoComplete, 0);

      var addBtn = new Button
      {
        Content = "+",
        FontSize = 14,
        Margin = new Thickness(4, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center
      };
      addBtn.Classes.Add("primary");
      addBtn.Click += (_, _) =>
      {
        var selected = autoComplete.Text;
        if (!string.IsNullOrWhiteSpace(selected) && !items.Contains(selected))
        {
          items.Add(selected);
          RebuildItemsPanel();
          SyncCollectionToViewModel(fieldName, items);
          autoComplete.Text = "";
        }
      };
      addRow.Children.Add(addBtn);
      Grid.SetColumn(addBtn, 1);

      outerPanel.Children.Add(addRow);
    }

    return outerPanel;
  }

  private static bool ArrayContainsObjects(System.Text.Json.JsonElement arrayElement)
  {
    foreach (var el in arrayElement.EnumerateArray())
      return el.ValueKind == System.Text.Json.JsonValueKind.Object;
    return false;
  }

  private Control CreateObjectArrayControl(string fieldName, System.Text.Json.JsonElement arrayElement, bool isEditable)
  {
    // Get element type from schema for the current template's field
    string? elementType = null;
    if (DataContext is StatsEditorViewModel vm)
    {
      elementType = vm.GetCollectionElementType(fieldName);
    }

    return CreateObjectArrayControlCore(fieldName, elementType, null, arrayElement, isEditable, json =>
    {
      if (DataContext is StatsEditorViewModel vmInner)
        vmInner.UpdateComplexArrayProperty(fieldName, json);
    });
  }

  private Control CreateObjectArrayControlCore(
    string fieldName,
    string? elementTypeName,
    string? parentClassName,
    System.Text.Json.JsonElement arrayElement,
    bool isEditable,
    System.Action<string> onChanged)
  {
    var elements = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object?>>();
    foreach (var el in arrayElement.EnumerateArray())
    {
      if (el.ValueKind != System.Text.Json.JsonValueKind.Object) continue;
      var dict = new System.Collections.Generic.Dictionary<string, object?>();
      foreach (var prop in el.EnumerateObject())
        dict[prop.Name] = prop.Value.Clone();
      elements.Add(dict);
    }

    var outerPanel = new StackPanel { Spacing = 2 };
    var countLabel = new TextBlock
    {
      Text = $"({elements.Count} entries)",
      Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
      FontSize = 10,
      Margin = new Thickness(0, 0, 0, 4)
    };
    outerPanel.Children.Add(countLabel);

    var itemsPanel = new StackPanel { Spacing = 2 };
    var initialized = false;

    void SyncToViewModel()
    {
      if (!initialized || !isEditable) return;
      var json = SerializeElementList(elements);
      onChanged(json);
    }

    void RebuildItemsPanel()
    {
      var wasInit = initialized;
      initialized = false;
      itemsPanel.Children.Clear();
      countLabel.Text = $"({elements.Count} entries)";

      for (int i = 0; i < elements.Count; i++)
      {
        var idx = i;
        var element = elements[idx];

        // Check if this is a small element with only primitive fields — render inline
        bool isSmallElement = element.Count <= 2 && element.Values.All(v =>
          v is not System.Text.Json.JsonElement je ||
          (je.ValueKind != System.Text.Json.JsonValueKind.Array &&
           je.ValueKind != System.Text.Json.JsonValueKind.Object));

        if (isSmallElement)
        {
          var inlineGrid = new Grid
          {
            ColumnDefinitions = isEditable
              ? new ColumnDefinitions("Auto,*,Auto")
              : new ColumnDefinitions("Auto,*"),
            Background = new SolidColorBrush(Color.Parse("#252525")),
            Margin = new Thickness(0, 1)
          };

          var indexLabel = new TextBlock
          {
            Text = $"[{idx}]",
            Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 4)
          };
          inlineGrid.Children.Add(indexLabel);
          Grid.SetColumn(indexLabel, 0);

          var fieldsRow = new StackPanel
          {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 4)
          };
          foreach (var kvp in element.ToList())
          {
            fieldsRow.Children.Add(new TextBlock
            {
              Text = kvp.Key + ":",
              Foreground = Brushes.White,
              Opacity = 0.7,
              FontSize = 12,
              VerticalAlignment = VerticalAlignment.Center
            });

            object? fv = kvp.Value;
            if (fv is System.Text.Json.JsonElement fje)
            {
              fv = fje.ValueKind switch
              {
                System.Text.Json.JsonValueKind.String => fje.GetString(),
                System.Text.Json.JsonValueKind.Number => fje.TryGetInt64(out var fl) ? (object)fl : fje.GetDouble(),
                System.Text.Json.JsonValueKind.True => (object)true,
                System.Text.Json.JsonValueKind.False => (object)false,
                _ => fje.GetRawText()
              };
            }

            if (isEditable)
            {
              var fieldName = kvp.Key;
              var origVal = fv;
              var tb = new TextBox
              {
                Text = fv?.ToString() ?? "",
                Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 3),
                FontSize = 12,
                MinWidth = 60
              };
              tb.TextChanged += (_, _) =>
              {
                var text = tb.Text ?? "";
                if (origVal is long)
                  element[fieldName] = long.TryParse(text, out var l) ? l : (object)text;
                else if (origVal is double)
                  element[fieldName] = double.TryParse(text, out var d) ? d : (object)text;
                else
                  element[fieldName] = text;
                SyncToViewModel();
              };
              fieldsRow.Children.Add(tb);
            }
            else
            {
              fieldsRow.Children.Add(new TextBlock
              {
                Text = fv?.ToString() ?? "null",
                Foreground = Brushes.White,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
              });
            }
          }
          inlineGrid.Children.Add(fieldsRow);
          Grid.SetColumn(fieldsRow, 1);

          if (isEditable)
          {
            var removeBtn = new Button
            {
              Content = "\u2715",
              Background = Brushes.Transparent,
              Foreground = new SolidColorBrush(Color.Parse("#CC4444")),
              BorderThickness = new Thickness(0),
              Padding = new Thickness(6, 2),
              FontSize = 12,
              VerticalAlignment = VerticalAlignment.Center,
              Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };
            removeBtn.Click += (_, _) =>
            {
              elements.RemoveAt(idx);
              RebuildItemsPanel();
              SyncToViewModel();
            };
            inlineGrid.Children.Add(removeBtn);
            Grid.SetColumn(removeBtn, 2);
          }

          itemsPanel.Children.Add(inlineGrid);
          continue;
        }

        // Larger elements: use collapsible Expander
        var summary = BuildElementSummary(element, idx);

        var headerGrid = new Grid
        {
          ColumnDefinitions = isEditable
            ? new ColumnDefinitions("*,Auto")
            : new ColumnDefinitions("*")
        };

        var summaryText = new TextBlock
        {
          Text = summary,
          Foreground = Brushes.White,
          FontSize = 12,
          VerticalAlignment = VerticalAlignment.Center,
          TextTrimming = TextTrimming.CharacterEllipsis
        };
        headerGrid.Children.Add(summaryText);
        Grid.SetColumn(summaryText, 0);

        if (isEditable)
        {
          var removeBtn = new Button
          {
            Content = "\u2715",
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.Parse("#CC4444")),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 2),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
          };
          removeBtn.Click += (_, _) =>
          {
            elements.RemoveAt(idx);
            RebuildItemsPanel();
            SyncToViewModel();
          };
          headerGrid.Children.Add(removeBtn);
          Grid.SetColumn(removeBtn, 1);
        }

        var expander = new Expander
        {
          Header = headerGrid,
          IsExpanded = false,
          Margin = new Thickness(0, 1),
          Padding = new Thickness(0),
          Background = new SolidColorBrush(Color.Parse("#252525")),
          BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
          BorderThickness = new Thickness(1)
        };

        var bodyPanel = new StackPanel { Spacing = 8, Margin = new Thickness(12, 8, 0, 8) };
        foreach (var kvp in element.ToList())
        {
          bodyPanel.Children.Add(CreateObjectFieldControl(
            kvp.Key, kvp.Value, element, elementTypeName, isEditable, SyncToViewModel));
        }

        expander.Content = bodyPanel;
        itemsPanel.Children.Add(expander);
      }

      initialized = wasInit;
    }

    RebuildItemsPanel();
    outerPanel.Children.Add(itemsPanel);

    if (isEditable)
    {
      var vm = DataContext as StatsEditorViewModel;
      var hasSchema = elementTypeName != null && vm != null && vm.HasEmbeddedClassSchema(elementTypeName);

      var addEntryBtn = new Button
      {
        Content = hasSchema ? $"+ Add {elementTypeName}" : "+ Add Entry",
        FontSize = 11,
        Margin = new Thickness(0, 8, 0, 0),
        HorizontalAlignment = HorizontalAlignment.Left
      };
      addEntryBtn.Classes.Add("primary");
      addEntryBtn.Click += (_, _) =>
      {
        System.Collections.Generic.Dictionary<string, object?> newElement;

        // If we have schema for the element type, create default values from schema
        if (hasSchema && vm != null)
        {
          newElement = vm.CreateDefaultElement(elementTypeName!);
        }
        else if (elements.Count > 0)
        {
          // Fallback: clone last element
          newElement = new System.Collections.Generic.Dictionary<string, object?>();
          foreach (var kvp in elements[^1])
            newElement[kvp.Key] = kvp.Value;
        }
        else
        {
          newElement = new System.Collections.Generic.Dictionary<string, object?>();
        }

        elements.Add(newElement);
        RebuildItemsPanel();
        SyncToViewModel();
      };
      outerPanel.Children.Add(addEntryBtn);
    }

    initialized = true;
    return outerPanel;
  }

  private Control CreateObjectFieldControl(
    string propName,
    object? propValue,
    System.Collections.Generic.Dictionary<string, object?> element,
    string? parentClassName,
    bool isEditable,
    System.Action syncToViewModel)
  {
    var fieldStack = new StackPanel { Spacing = 4 };
    var vm = DataContext as StatsEditorViewModel;

    // Get field metadata from schema if we know the parent class
    Services.SchemaService.FieldMeta? fieldMeta = null;
    if (vm != null && !string.IsNullOrEmpty(parentClassName))
    {
      fieldMeta = vm.GetEmbeddedFieldMetadata(parentClassName, propName);
    }

    var label = new TextBlock
    {
      Text = propName,
      Foreground = Brushes.White,
      Opacity = 0.8,
      FontSize = 11,
      FontWeight = FontWeight.SemiBold
    };
    fieldStack.Children.Add(label);

    if (propValue is System.Text.Json.JsonElement je)
    {
      switch (je.ValueKind)
      {
        case System.Text.Json.JsonValueKind.Array:
          if (ArrayContainsObjects(je))
          {
            // Get nested element type from schema
            string? nestedElementType = null;
            if (fieldMeta?.Category == "collection" && !string.IsNullOrEmpty(fieldMeta.ElementType))
              nestedElementType = fieldMeta.ElementType;
            else if (vm != null && !string.IsNullOrEmpty(parentClassName))
              nestedElementType = vm.GetEmbeddedCollectionElementType(parentClassName, propName);

            fieldStack.Children.Add(CreateObjectArrayControlCore(propName, nestedElementType, parentClassName, je, isEditable, json =>
            {
              try
              {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                element[propName] = doc.RootElement.Clone();
              }
              catch { }
              syncToViewModel();
            }));
            return fieldStack;
          }

          // Non-object array — render as individual rows
          fieldStack.Children.Add(CreatePrimitiveArrayControl(
            propName, je, element, isEditable, syncToViewModel));
          return fieldStack;

        case System.Text.Json.JsonValueKind.Object:
          // Get nested object type from schema
          string? nestedObjType = fieldMeta?.Type;
          var nestedDict = new System.Collections.Generic.Dictionary<string, object?>();
          foreach (var prop in je.EnumerateObject())
            nestedDict[prop.Name] = prop.Value.Clone();
          var nestedPanel = new StackPanel { Spacing = 8, Margin = new Thickness(16, 4, 0, 0) };
          foreach (var nkvp in nestedDict.ToList())
          {
            nestedPanel.Children.Add(CreateObjectFieldControl(
              nkvp.Key, nkvp.Value, nestedDict, nestedObjType, isEditable, () =>
              {
                element[propName] = SerializeDict(nestedDict);
                syncToViewModel();
              }));
          }
          fieldStack.Children.Add(nestedPanel);
          return fieldStack;

        default:
          propValue = je.ValueKind switch
          {
            System.Text.Json.JsonValueKind.String => je.GetString(),
            System.Text.Json.JsonValueKind.Number => je.TryGetInt64(out var l) ? (object)l : je.GetDouble(),
            System.Text.Json.JsonValueKind.True => (object)true,
            System.Text.Json.JsonValueKind.False => (object)false,
            System.Text.Json.JsonValueKind.Null => null,
            _ => je.GetRawText()
          };
          break;
      }
    }

    // Coerce string booleans
    if (propValue is string strBool && bool.TryParse(strBool, out var parsedBool))
      propValue = parsedBool;

    // Boolean
    if (propValue is bool boolVal)
    {
      var originalBool = boolVal;
      var checkBox = new CheckBox
      {
        IsChecked = boolVal,
        IsEnabled = isEditable,
        Content = boolVal ? "True" : "False",
        Foreground = Brushes.White,
        FontSize = 12,
        Margin = new Thickness(0, 2)
      };
      if (isEditable)
      {
        checkBox.IsCheckedChanged += (s, _) =>
        {
          if (s is CheckBox cb)
          {
            var isChecked = cb.IsChecked ?? false;
            // Skip sync if value hasn't actually changed (avoids false positives on control initialization)
            if (isChecked == originalBool) return;
            originalBool = isChecked; // Update so subsequent changes are detected
            cb.Content = isChecked ? "True" : "False";
            element[propName] = isChecked;
            syncToViewModel();
          }
        };
      }
      fieldStack.Children.Add(checkBox);
      return fieldStack;
    }

    // Reference fields — use schema to determine the target type
    if (isEditable && fieldMeta?.Category == "reference" && propValue is string)
    {
      var refType = fieldMeta.Type; // e.g., "EntityTemplate", "MissionTemplate"
      var instanceNames = vm?.GetTemplateInstanceNames(refType)
        ?? new System.Collections.Generic.List<string>();

      var originalText = propValue?.ToString() ?? "";
      var autoComplete = new AutoCompleteBox
      {
        Text = originalText,
        ItemsSource = instanceNames,
        FilterMode = AutoCompleteFilterMode.ContainsOrdinal,
        MinimumPrefixLength = 0,
        Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        Foreground = Brushes.White,
        BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
        BorderThickness = new Thickness(1),
        FontSize = 12
      };
      autoComplete.GetObservable(AutoCompleteBox.TextProperty)
        .Subscribe(text =>
        {
          // Skip sync if text hasn't actually changed (avoids false positives on control initialization)
          if (text == originalText) return;
          element[propName] = text ?? "";
          syncToViewModel();
        });
      fieldStack.Children.Add(autoComplete);
      return fieldStack;
    }

    // Fallback: String field named "Template" — AutoCompleteBox with entity names (legacy behavior)
    if (isEditable && propName.Equals("Template", System.StringComparison.OrdinalIgnoreCase) && propValue is string)
    {
      var instanceNames = vm?.GetTemplateInstanceNames("EntityTemplate")
        ?? new System.Collections.Generic.List<string>();

      var originalText = propValue?.ToString() ?? "";
      var autoComplete = new AutoCompleteBox
      {
        Text = originalText,
        ItemsSource = instanceNames,
        FilterMode = AutoCompleteFilterMode.ContainsOrdinal,
        MinimumPrefixLength = 0,
        Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        Foreground = Brushes.White,
        BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
        BorderThickness = new Thickness(1),
        FontSize = 12
      };
      autoComplete.GetObservable(AutoCompleteBox.TextProperty)
        .Subscribe(text =>
        {
          // Skip sync if text hasn't actually changed (avoids false positives on control initialization)
          if (text == originalText) return;
          element[propName] = text ?? "";
          syncToViewModel();
        });
      fieldStack.Children.Add(autoComplete);
      return fieldStack;
    }

    // Other primitives
    if (isEditable)
    {
      var originalText = propValue?.ToString() ?? "";
      var textBox = new TextBox
      {
        Text = originalText,
        Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
        Foreground = Brushes.White,
        BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(8, 6),
        FontSize = 12
      };
      var originalValue = propValue;
      textBox.TextChanged += (_, _) =>
      {
        var text = textBox.Text ?? "";
        // Skip sync if text hasn't actually changed (avoids false positives on control initialization)
        if (text == originalText) return;
        if (originalValue is long)
          element[propName] = long.TryParse(text, out var l) ? l : (object)text;
        else if (originalValue is double)
          element[propName] = double.TryParse(text, out var d) ? d : (object)text;
        else
          element[propName] = text;
        syncToViewModel();
      };
      fieldStack.Children.Add(textBox);
    }
    else
    {
      fieldStack.Children.Add(new TextBox
      {
        Text = propValue?.ToString() ?? "null",
        Background = Brushes.Transparent,
        Foreground = Brushes.White,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(8, 6),
        FontSize = 12,
        IsReadOnly = true,
        TextWrapping = TextWrapping.Wrap,
        Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Arrow)
      });
    }

    return fieldStack;
  }

  private Control CreatePrimitiveArrayControl(
    string propName,
    System.Text.Json.JsonElement arrayElement,
    System.Collections.Generic.Dictionary<string, object?> element,
    bool isEditable,
    System.Action syncToViewModel)
  {
    var items = new System.Collections.Generic.List<string>();
    foreach (var el in arrayElement.EnumerateArray())
    {
      items.Add(el.ValueKind == System.Text.Json.JsonValueKind.String
        ? el.GetString() ?? ""
        : el.GetRawText());
    }

    var outerPanel = new StackPanel { Spacing = 0 };
    var itemsPanel = new StackPanel { Spacing = 0 };

    void SyncItems()
    {
      // Determine if original was all strings to preserve type
      bool allStrings = true;
      foreach (var el in arrayElement.EnumerateArray())
      {
        if (el.ValueKind != System.Text.Json.JsonValueKind.String)
        { allStrings = false; break; }
      }

      using var ms = new System.IO.MemoryStream();
      using (var writer = new System.Text.Json.Utf8JsonWriter(ms))
      {
        writer.WriteStartArray();
        foreach (var item in items)
        {
          if (allStrings)
            writer.WriteStringValue(item);
          else
          {
            // Try to write as raw JSON for non-string items
            try
            {
              using var doc = System.Text.Json.JsonDocument.Parse(item);
              doc.RootElement.WriteTo(writer);
            }
            catch
            {
              writer.WriteStringValue(item);
            }
          }
        }
        writer.WriteEndArray();
      }
      using var doc2 = System.Text.Json.JsonDocument.Parse(ms.ToArray());
      element[propName] = doc2.RootElement.Clone();
      syncToViewModel();
    }

    void RebuildItems()
    {
      itemsPanel.Children.Clear();
      if (items.Count == 0)
      {
        itemsPanel.Children.Add(new TextBlock
        {
          Text = "(empty)",
          Foreground = new SolidColorBrush(Color.Parse("#888888")),
          FontStyle = FontStyle.Italic,
          FontSize = 12,
          Padding = new Thickness(8, 4)
        });
        return;
      }

      for (int i = 0; i < items.Count; i++)
      {
        var idx = i;
        var rowBg = i % 2 == 0
          ? new SolidColorBrush(Color.Parse("#1E1E1E"))
          : new SolidColorBrush(Color.Parse("#252525"));

        // Try structured rendering for "Type|{json}" serialized nodes
        var nodeControl = TryCreateSerializedNodeControl(
          items[idx], isEditable, newValue => { items[idx] = newValue; SyncItems(); });
        if (nodeControl != null)
        {
          var nodeContainer = new Border
          {
            Background = rowBg,
            Padding = new Thickness(4, 2),
            Margin = new Thickness(0, 1)
          };
          if (isEditable)
          {
            var nodeRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            nodeRow.Children.Add(nodeControl);
            Grid.SetColumn(nodeControl, 0);
            var removeBtn = new Button
            {
              Content = "\u2715",
              Background = Brushes.Transparent,
              Foreground = new SolidColorBrush(Color.Parse("#CC4444")),
              BorderThickness = new Thickness(0),
              Padding = new Thickness(6, 2),
              FontSize = 12,
              VerticalAlignment = VerticalAlignment.Top,
              Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };
            removeBtn.Click += (_, _) =>
            {
              items.RemoveAt(idx);
              RebuildItems();
              SyncItems();
            };
            nodeRow.Children.Add(removeBtn);
            Grid.SetColumn(removeBtn, 1);
            nodeContainer.Child = nodeRow;
          }
          else
          {
            nodeContainer.Child = nodeControl;
          }
          itemsPanel.Children.Add(nodeContainer);
          continue;
        }

        // Plain text rendering
        if (isEditable)
        {
          var row = new Grid
          {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Background = rowBg
          };
          var textBox = new TextBox
          {
            Text = items[idx],
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
          };
          textBox.LostFocus += (_, _) =>
          {
            items[idx] = textBox.Text ?? "";
            SyncItems();
          };
          row.Children.Add(textBox);
          Grid.SetColumn(textBox, 0);

          var removeBtn = new Button
          {
            Content = "\u2715",
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.Parse("#CC4444")),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 2),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
          };
          removeBtn.Click += (_, _) =>
          {
            items.RemoveAt(idx);
            RebuildItems();
            SyncItems();
          };
          row.Children.Add(removeBtn);
          Grid.SetColumn(removeBtn, 1);

          itemsPanel.Children.Add(row);
        }
        else
        {
          itemsPanel.Children.Add(new TextBlock
          {
            Text = items[idx],
            Foreground = Brushes.White,
            FontSize = 12,
            Padding = new Thickness(8, 4),
            TextWrapping = TextWrapping.Wrap,
            Background = rowBg
          });
        }
      }
    }

    RebuildItems();
    outerPanel.Children.Add(itemsPanel);

    if (isEditable)
    {
      var addItemBtn = new Button
      {
        Content = "+ Add",
        FontSize = 11,
        Margin = new Thickness(0, 4, 0, 0),
        HorizontalAlignment = HorizontalAlignment.Left
      };
      addItemBtn.Classes.Add("primary");
      addItemBtn.Click += (_, _) =>
      {
        items.Add("");
        RebuildItems();
        SyncItems();
      };
      outerPanel.Children.Add(addItemBtn);
    }

    return outerPanel;
  }

  /// <summary>
  /// Tries to parse a "Type|{json}" serialized node string and render it as structured fields.
  /// Returns null if the string doesn't match the pattern.
  /// </summary>
  private Control? TryCreateSerializedNodeControl(
    string value,
    bool isEditable,
    System.Action<string> onChanged)
  {
    var pipeIdx = value.IndexOf('|');
    if (pipeIdx <= 0 || pipeIdx >= value.Length - 1)
      return null;

    var typeName = value[..pipeIdx];
    foreach (var c in typeName)
      if (!char.IsLetterOrDigit(c) && c != '_')
        return null;

    var jsonPart = value[(pipeIdx + 1)..];
    System.Text.Json.JsonElement bodyElement;
    try
    {
      using var doc = System.Text.Json.JsonDocument.Parse(jsonPart);
      bodyElement = doc.RootElement.Clone();
    }
    catch
    {
      return null;
    }

    if (bodyElement.ValueKind != System.Text.Json.JsonValueKind.Object)
      return null;

    var fields = new System.Collections.Generic.Dictionary<string, object?>();
    foreach (var prop in bodyElement.EnumerateObject())
      fields[prop.Name] = prop.Value.Clone();

    var nodeInit = false;

    void SyncNode()
    {
      if (!nodeInit) return;
      var serialized = SerializeDict(fields);
      var reconstructed = typeName + "|" + serialized.GetRawText();
      onChanged(reconstructed);
    }

    var panel = new StackPanel { Spacing = 2 };

    // Type badge
    var badgeColor = typeName switch
    {
      "SAY" => "#2D6A4F",
      "VARIATION" => "#4A3068",
      "EMPTY" => "#3E3E3E",
      _ => "#3A5A80"
    };
    var badge = new Border
    {
      Background = new SolidColorBrush(Color.Parse(badgeColor)),
      CornerRadius = new CornerRadius(3),
      Padding = new Thickness(6, 2),
      HorizontalAlignment = HorizontalAlignment.Left,
      Margin = new Thickness(0, 2, 0, 0)
    };
    badge.Child = new TextBlock
    {
      Text = typeName,
      Foreground = Brushes.White,
      FontSize = 10,
      FontWeight = FontWeight.SemiBold
    };
    panel.Children.Add(badge);

    if (fields.Count > 0)
    {
      var fieldsPanel = new StackPanel { Spacing = 6, Margin = new Thickness(8, 4, 0, 0) };
      foreach (var kvp in fields.ToList())
      {
        // Serialized nodes don't have schema, pass null for parentClassName
        fieldsPanel.Children.Add(CreateObjectFieldControl(
          kvp.Key, kvp.Value, fields, null, isEditable, SyncNode));
      }
      panel.Children.Add(fieldsPanel);
    }

    nodeInit = true;
    return panel;
  }

  private static string BuildElementSummary(
    System.Collections.Generic.Dictionary<string, object?> element, int index)
  {
    var parts = new System.Collections.Generic.List<string>();
    var skipNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
      { "Guid", "Id", "Uid" };
    var priorityNames = new[] { "Text", "Name", "Template", "Title", "Description" };

    // First pass: priority fields (Text, Name, etc.)
    foreach (var pn in priorityNames)
    {
      if (parts.Count >= 2) break;
      if (element.TryGetValue(pn, out var pv) && pv is System.Text.Json.JsonElement pje
          && (pje.ValueKind == System.Text.Json.JsonValueKind.String
              || pje.ValueKind == System.Text.Json.JsonValueKind.Number))
      {
        var text = pje.ValueKind == System.Text.Json.JsonValueKind.String
          ? pje.GetString() ?? "" : pje.ToString();
        if (text.Length > 50) text = text[..47] + "...";
        parts.Add(text);
      }
    }

    // Second pass: other primitive fields, skipping Guid/Id
    foreach (var kvp in element)
    {
      if (parts.Count >= 3) break;
      if (skipNames.Contains(kvp.Key)) continue;
      if (Array.Exists(priorityNames, n => n.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase)))
        continue;
      if (kvp.Value is System.Text.Json.JsonElement je)
      {
        if (je.ValueKind == System.Text.Json.JsonValueKind.Array
            || je.ValueKind == System.Text.Json.JsonValueKind.Object)
          continue;
        var str = je.ToString();
        if (str.Length > 30) str = str[..27] + "...";
        parts.Add($"{kvp.Key}: {str}");
      }
    }

    // If still empty, try to extract text from serialized nodes in array fields
    if (parts.Count == 0)
    {
      foreach (var kvp in element)
      {
        if (kvp.Value is System.Text.Json.JsonElement je
            && je.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
          var extracted = TryExtractTextFromSerializedNodes(je);
          if (extracted != null)
          {
            parts.Add(extracted);
            break;
          }
        }
      }
    }

    // Last resort: include Guid/Id if nothing else
    if (parts.Count == 0)
    {
      foreach (var kvp in element)
      {
        if (parts.Count >= 1) break;
        if (kvp.Value is System.Text.Json.JsonElement je
            && je.ValueKind != System.Text.Json.JsonValueKind.Array
            && je.ValueKind != System.Text.Json.JsonValueKind.Object)
          parts.Add($"{kvp.Key}: {je}");
      }
    }

    var summary = parts.Count > 0 ? string.Join(", ", parts) : "";
    return $"[{index}] {summary}";
  }

  /// <summary>
  /// Tries to extract human-readable text from serialized node strings (Type|{json} format)
  /// within a JSON array. Recurses through VARIATION → Variations → m_SerializedNodes → SAY → Text.
  /// </summary>
  private static string? TryExtractTextFromSerializedNodes(System.Text.Json.JsonElement arrayElement)
  {
    foreach (var el in arrayElement.EnumerateArray())
    {
      if (el.ValueKind != System.Text.Json.JsonValueKind.String) continue;
      var str = el.GetString();
      if (str == null) continue;
      var pipeIdx = str.IndexOf('|');
      if (pipeIdx <= 0 || pipeIdx >= str.Length - 1) continue;

      var typeName = str[..pipeIdx];
      var jsonPart = str[(pipeIdx + 1)..];
      try
      {
        using var doc = System.Text.Json.JsonDocument.Parse(jsonPart);
        var root = doc.RootElement;

        // Direct Text field (SAY nodes)
        if (root.TryGetProperty("Text", out var textProp)
            && textProp.ValueKind == System.Text.Json.JsonValueKind.String)
        {
          var text = textProp.GetString() ?? "";
          if (text.Length > 50) text = text[..47] + "...";
          return $"{typeName}: \"{text}\"";
        }

        // Recurse: VARIATION → Variations[] → m_SerializedNodes[]
        if (root.TryGetProperty("Variations", out var vars)
            && vars.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
          foreach (var v in vars.EnumerateArray())
          {
            if (v.TryGetProperty("m_SerializedNodes", out var nodes)
                && nodes.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
              var nested = TryExtractTextFromSerializedNodes(nodes);
              if (nested != null)
                return $"{typeName} > {nested}";
            }
          }
        }
      }
      catch { }
    }
    return null;
  }

  private static string SerializeElementList(
    System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object?>> elements)
  {
    using var ms = new System.IO.MemoryStream();
    using (var writer = new System.Text.Json.Utf8JsonWriter(ms))
    {
      writer.WriteStartArray();
      foreach (var element in elements)
      {
        writer.WriteStartObject();
        foreach (var kvp in element)
        {
          writer.WritePropertyName(kvp.Key);
          WriteJsonValue(writer, kvp.Value);
        }
        writer.WriteEndObject();
      }
      writer.WriteEndArray();
    }
    return System.Text.Encoding.UTF8.GetString(ms.ToArray());
  }

  private static System.Text.Json.JsonElement SerializeDict(
    System.Collections.Generic.Dictionary<string, object?> dict)
  {
    using var ms = new System.IO.MemoryStream();
    using (var writer = new System.Text.Json.Utf8JsonWriter(ms))
    {
      writer.WriteStartObject();
      foreach (var kvp in dict)
      {
        writer.WritePropertyName(kvp.Key);
        WriteJsonValue(writer, kvp.Value);
      }
      writer.WriteEndObject();
    }
    using var doc = System.Text.Json.JsonDocument.Parse(ms.ToArray());
    return doc.RootElement.Clone();
  }

  private static void WriteJsonValue(System.Text.Json.Utf8JsonWriter writer, object? value)
  {
    switch (value)
    {
      case null:
        writer.WriteNullValue();
        break;
      case System.Text.Json.JsonElement je:
        je.WriteTo(writer);
        break;
      case bool b:
        writer.WriteBooleanValue(b);
        break;
      case long l:
        writer.WriteNumberValue(l);
        break;
      case double d:
        writer.WriteNumberValue(d);
        break;
      case string s:
        writer.WriteStringValue(s);
        break;
      default:
        writer.WriteStringValue(value.ToString());
        break;
    }
  }

  private void SyncCollectionToViewModel(string fieldName, System.Collections.Generic.List<string> items)
  {
    if (DataContext is StatsEditorViewModel vm)
      vm.UpdateCollectionProperty(fieldName, items);
  }

  private void OnEditableTextBoxChanged(object? sender, TextChangedEventArgs e)
  {
    if (sender is TextBox tb && tb.Tag is string fieldName && DataContext is StatsEditorViewModel vm)
    {
      vm.UpdateModifiedProperty(fieldName, tb.Text ?? "");
    }
  }

  private void OnEditableTextBoxLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    if (sender is TextBox tb && tb.Tag is string fieldName && DataContext is StatsEditorViewModel vm)
    {
      vm.UpdateModifiedProperty(fieldName, tb.Text ?? "");
    }
  }

  private async Task ShowCreateModpackDialogAsync()
  {
    try
    {
      if (DataContext is StatsEditorViewModel vm)
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
      ModkitLog.Error($"Create modpack dialog failed: {ex.Message}");
    }
  }
}
