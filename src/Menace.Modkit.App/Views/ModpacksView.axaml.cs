using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Menace.Modkit.App.Controls;
using Menace.Modkit.App.ViewModels;

namespace Menace.Modkit.App.Views;

public class ModpacksView : UserControl
{
  private ModpackItemViewModel? _draggedModpackItem;

  public ModpacksView()
  {
    Content = BuildUI();
  }

  private Control BuildUI()
  {
    var mainGrid = new Grid
    {
      ColumnDefinitions = new ColumnDefinitions("300,4,*")
    };

    // Left: Unified Modpack List (darker panel)
    var leftPanel = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#141414")),
      BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
      BorderThickness = new Thickness(0, 0, 1, 0),
      Child = BuildModpackList()
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

    // Right: Modpack Details (lighter panel)
    mainGrid.Children.Add(BuildModpackDetails());
    Grid.SetColumn((Control)mainGrid.Children[2], 2);

    return mainGrid;
  }

  private Control BuildModpackList()
  {
    var grid = new Grid
    {
      RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
      Margin = new Thickness(12)
    };

    // Row 0: Title
    var title = new TextBlock
    {
      Text = "Modpacks",
      FontSize = 14,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White,
      Margin = new Thickness(0, 0, 0, 12)
    };
    grid.Children.Add(title);
    Grid.SetRow(title, 0);

    // Row 1: Button row with Import, Create, and Update Check
    var buttonRow = new Grid
    {
      ColumnDefinitions = new ColumnDefinitions("*,8,*,8,*"),
      Margin = new Thickness(0, 0, 0, 12)
    };

    var importButton = new Button
    {
      Content = "+ Import Mod",
      HorizontalAlignment = HorizontalAlignment.Stretch
    };
    importButton.Classes.Add("primary");
    importButton.Click += OnImportModClick;
    buttonRow.Children.Add(importButton);
    Grid.SetColumn(importButton, 0);

    var createButton = new Button
    {
      Content = "+ Create New",
      HorizontalAlignment = HorizontalAlignment.Stretch
    };
    createButton.Classes.Add("secondary");
    createButton.Click += (_, _) => ShowCreateDialog();
    buttonRow.Children.Add(createButton);
    Grid.SetColumn(createButton, 2);

    var checkUpdatesButton = new Button
    {
      Content = "Check Updates",
      HorizontalAlignment = HorizontalAlignment.Stretch
    };
    checkUpdatesButton.Classes.Add("secondary");
    checkUpdatesButton.Bind(Button.ContentProperty,
      new Avalonia.Data.Binding("IsCheckingUpdates")
      {
        Converter = new FuncValueConverter<bool, string>(checking => checking ? "Checking..." : "Check Updates")
      });
    checkUpdatesButton.Bind(Button.IsEnabledProperty,
      new Avalonia.Data.Binding("IsCheckingUpdates") { Converter = InvertBoolConverter });
    checkUpdatesButton.Click += async (_, _) =>
    {
      if (DataContext is ModpacksViewModel vm)
        await vm.CheckForUpdatesAsync(forceRefresh: true);
    };
    buttonRow.Children.Add(checkUpdatesButton);
    Grid.SetColumn(checkUpdatesButton, 4);

    grid.Children.Add(buttonRow);
    Grid.SetRow(buttonRow, 1);

    // Row 2: Unified modpack list (star row)
    var modpackList = new ListBox
    {
      Background = new SolidColorBrush(Color.Parse("#252525")),
      BorderThickness = new Thickness(0),
    };
    modpackList.Bind(ListBox.ItemsSourceProperty,
      new Avalonia.Data.Binding("AllModpacks"));
    modpackList.Bind(ListBox.SelectedItemProperty,
      new Avalonia.Data.Binding("SelectedModpack"));

    modpackList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ModpackItemViewModel>(
      (modpack, _) => CreateModpackListItem(modpack));

    // Drag-and-drop: allow items to be dropped onto the list (reordering + zip import)
    DragDrop.SetAllowDrop(modpackList, true);
    modpackList.AddHandler(DragDrop.DragOverEvent, (_, e) =>
    {
      if (_draggedModpackItem != null)
      {
        e.DragEffects = DragDropEffects.Move;
      }
      else if (e.DataTransfer.Contains(DataFormat.File))
      {
        e.DragEffects = DragDropEffects.Copy;
      }
      else
      {
        e.DragEffects = DragDropEffects.None;
      }
    });
    modpackList.AddHandler(DragDrop.DropEvent, (_, e) =>
    {
      if (DataContext is not ModpacksViewModel vm)
        return;

      // Handle modpack reordering
      if (_draggedModpackItem != null)
      {
        var targetItem = FindDropTarget(e);
        if (targetItem != null && targetItem != _draggedModpackItem)
        {
          var targetIndex = vm.AllModpacks.IndexOf(targetItem);
          vm.MoveItem(_draggedModpackItem, targetIndex);
        }
        _draggedModpackItem = null;
        return;
      }

      // Handle archive file drops (.zip, .7z, .rar, etc.)
      if (e.DataTransfer.Contains(DataFormat.File))
      {
        var files = e.DataTransfer.TryGetFiles();
        if (files != null)
        {
          var archivePaths = files
            .Select(f => f.Path.LocalPath)
            .Where(IsArchiveFile)
            .ToList();

          if (archivePaths.Count > 0)
          {
            vm.ImportModpacksFromZips(archivePaths);
          }
        }
      }
    });

    grid.Children.Add(modpackList);
    Grid.SetRow(modpackList, 2);

    // Row 3: Status + actions
    var bottomRow = new Grid
    {
      ColumnDefinitions = new ColumnDefinitions("*,Auto"),
      Margin = new Thickness(0, 6, 0, 0)
    };

    var statusStack = new StackPanel
    {
      Spacing = 2,
      VerticalAlignment = VerticalAlignment.Center
    };

    var conflictStatus = new TextBlock
    {
      FontSize = 11,
      Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
      VerticalAlignment = VerticalAlignment.Center
    };
    conflictStatus.Bind(TextBlock.TextProperty,
      new Avalonia.Data.Binding("LoadOrderVM.StatusText"));
    statusStack.Children.Add(conflictStatus);

    var updateStatus = new TextBlock
    {
      FontSize = 10,
      Foreground = new SolidColorBrush(Color.Parse("#A5B4FC")),
      VerticalAlignment = VerticalAlignment.Center
    };
    updateStatus.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("UpdateStatus"));
    statusStack.Children.Add(updateStatus);

    bottomRow.Children.Add(statusStack);
    Grid.SetColumn(statusStack, 0);

    var refreshBtn = new Button
    {
      Content = "Refresh",
      FontSize = 11
    };
    refreshBtn.Classes.Add("secondary");
    refreshBtn.Click += (_, _) =>
    {
      if (DataContext is ModpacksViewModel vm)
        vm.RefreshModpacks();
    };
    bottomRow.Children.Add(refreshBtn);
    Grid.SetColumn(refreshBtn, 1);

    grid.Children.Add(bottomRow);
    Grid.SetRow(bottomRow, 3);

    return grid;
  }

  private Control CreateModpackListItem(ModpackItemViewModel modpack)
  {
    // Outer: [4px teal indicator] [content]
    var outerGrid = new Grid
    {
      ColumnDefinitions = new ColumnDefinitions("4,*"),
      Margin = new Thickness(0, 1)
    };

    // Teal deployed indicator (left edge)
    var deployedIndicator = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#0d9488")),
      CornerRadius = new CornerRadius(2, 0, 0, 2)
    };
    deployedIndicator.Bind(Border.IsVisibleProperty,
      new Avalonia.Data.Binding("IsDeployed"));
    outerGrid.Children.Add(deployedIndicator);
    Grid.SetColumn(deployedIndicator, 0);

    // Content: [checkbox] [info...] [arrows] [order#] [grip]
    var contentGrid = new Grid
    {
      ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto"),
      Margin = new Thickness(12, 8)
    };

    // Col 0: Functional checkbox
    var checkbox = new CheckBox
    {
      VerticalAlignment = VerticalAlignment.Center,
      Margin = new Thickness(0, 0, 6, 0),
    };
    checkbox.Bind(CheckBox.IsCheckedProperty,
      new Avalonia.Data.Binding("IsDeployed"));
    checkbox.Click += async (sender, e) =>
    {
      if (sender is CheckBox cb && cb.DataContext is ModpackItemViewModel item)
      {
        // Revert visual — let the async operation + refresh set the real state
        cb.IsChecked = item.IsDeployed;
        if (DataContext is ModpacksViewModel vm && !vm.IsDeploying)
        {
          vm.SelectedModpack = item;
          await vm.ToggleDeploySelectedAsync();
        }
      }
    };
    contentGrid.Children.Add(checkbox);
    Grid.SetColumn(checkbox, 0);

    // Col 1: Info panel (fills)
    var infoStack = new StackPanel
    {
      Spacing = 1,
      VerticalAlignment = VerticalAlignment.Center
    };

    // Name + Version row
    var nameRow = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 6
    };
    var nameText = new TextBlock
    {
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White,
      FontSize = 13
    };
    nameText.Bind(TextBlock.TextProperty,
      new Avalonia.Data.Binding("Name"));
    nameRow.Children.Add(nameText);

    var versionText = new TextBlock
    {
      FontSize = 11,
      Foreground = new SolidColorBrush(Color.Parse("#888888")),
      VerticalAlignment = VerticalAlignment.Center
    };
    versionText.Bind(TextBlock.TextProperty,
      new Avalonia.Data.Binding("VersionDisplay"));
    nameRow.Children.Add(versionText);

    // [DLL] badge for standalone mods
    var dllBadge = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#333333")),
      CornerRadius = new CornerRadius(3),
      Padding = new Thickness(4, 1),
      VerticalAlignment = VerticalAlignment.Center,
      Child = new TextBlock
      {
        Text = "DLL",
        FontSize = 9,
        Foreground = new SolidColorBrush(Color.Parse("#999999")),
        FontWeight = FontWeight.SemiBold
      }
    };
    dllBadge.Bind(Border.IsVisibleProperty,
      new Avalonia.Data.Binding("IsStandalone"));
    nameRow.Children.Add(dllBadge);

    // UPDATE badge when a newer release is available
    var updateBadge = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#3A1F00")),
      BorderBrush = new SolidColorBrush(Color.Parse("#EAB308")),
      BorderThickness = new Thickness(1),
      CornerRadius = new CornerRadius(3),
      Padding = new Thickness(4, 1),
      VerticalAlignment = VerticalAlignment.Center,
      Child = new TextBlock
      {
        Text = "UPDATE",
        FontSize = 9,
        Foreground = new SolidColorBrush(Color.Parse("#FACC15")),
        FontWeight = FontWeight.SemiBold
      }
    };
    ToolTip.SetTip(updateBadge, "A newer version is available for this mod");
    updateBadge.Bind(Border.IsVisibleProperty,
      new Avalonia.Data.Binding("HasUpdateAvailable"));
    nameRow.Children.Add(updateBadge);
    infoStack.Children.Add(nameRow);

    // Author + Security Status + Conflict warning row
    var authorRow = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 8
    };
    var authorText = new TextBlock
    {
      FontSize = 11,
      Opacity = 0.6,
      Foreground = Brushes.White
    };
    authorText.Bind(TextBlock.TextProperty,
      new Avalonia.Data.Binding("Author"));
    authorRow.Children.Add(authorText);

    var securityText = new TextBlock
    {
      FontSize = 10,
      Foreground = new SolidColorBrush(Color.Parse("#888888")),
      VerticalAlignment = VerticalAlignment.Center
    };
    securityText.Bind(TextBlock.TextProperty,
      new Avalonia.Data.Binding("SecurityStatusDisplay"));
    authorRow.Children.Add(securityText);

    // Conflict warning badge — amber when deployed, grey when not
    var conflictBadge = new Border
    {
      CornerRadius = new CornerRadius(3),
      Padding = new Thickness(4, 1),
      VerticalAlignment = VerticalAlignment.Center
    };
    ToolTip.SetTip(conflictBadge, "This modpack modifies files that conflict with another deployed modpack");
    var conflictBadgeText = new TextBlock
    {
      Text = "CONFLICT",
      FontSize = 9,
      FontWeight = FontWeight.SemiBold
    };
    conflictBadge.Child = conflictBadgeText;
    var conflictBgConverter = new FuncValueConverter<bool, IBrush>(
      deployed => deployed
        ? new SolidColorBrush(Color.Parse("#3d2e00"))
        : new SolidColorBrush(Color.Parse("#333333")));
    var conflictFgConverter = new FuncValueConverter<bool, IBrush>(
      deployed => deployed
        ? new SolidColorBrush(Color.Parse("#c89b3c"))
        : new SolidColorBrush(Color.Parse("#888888")));
    conflictBadge.Bind(Border.BackgroundProperty,
      new Avalonia.Data.Binding("IsDeployed") { Converter = conflictBgConverter });
    conflictBadgeText.Bind(TextBlock.ForegroundProperty,
      new Avalonia.Data.Binding("IsDeployed") { Converter = conflictFgConverter });
    conflictBadge.Bind(Border.IsVisibleProperty,
      new Avalonia.Data.Binding("HasConflict"));
    authorRow.Children.Add(conflictBadge);
    infoStack.Children.Add(authorRow);

    contentGrid.Children.Add(infoStack);
    Grid.SetColumn(infoStack, 1);

    // Col 2: Up/Down arrows
    var arrowStack = new StackPanel
    {
      VerticalAlignment = VerticalAlignment.Center,
      Margin = new Thickness(4, 0)
    };

    var upArrow = new Button
    {
      Content = "\u25B2",
      FontSize = 8,
      Padding = new Thickness(4, 1),
      Background = Brushes.Transparent,
      Foreground = new SolidColorBrush(Color.Parse("#888888")),
      BorderThickness = new Thickness(0),
      MinWidth = 0,
      MinHeight = 0,
      HorizontalContentAlignment = HorizontalAlignment.Center,
      Cursor = new Cursor(StandardCursorType.Hand)
    };
    ToolTip.SetTip(upArrow, "Move up (loads earlier)");
    upArrow.Click += (sender, e) =>
    {
      if ((sender as Button)?.DataContext is ModpackItemViewModel item
          && DataContext is ModpacksViewModel vm)
      {
        vm.MoveItemUp(item);
      }
      e.Handled = true;
    };
    arrowStack.Children.Add(upArrow);

    var downArrow = new Button
    {
      Content = "\u25BC",
      FontSize = 8,
      Padding = new Thickness(4, 1),
      Background = Brushes.Transparent,
      Foreground = new SolidColorBrush(Color.Parse("#888888")),
      BorderThickness = new Thickness(0),
      MinWidth = 0,
      MinHeight = 0,
      HorizontalContentAlignment = HorizontalAlignment.Center,
      Cursor = new Cursor(StandardCursorType.Hand)
    };
    ToolTip.SetTip(downArrow, "Move down (loads later)");
    downArrow.Click += (sender, e) =>
    {
      if ((sender as Button)?.DataContext is ModpackItemViewModel item
          && DataContext is ModpacksViewModel vm)
      {
        vm.MoveItemDown(item);
      }
      e.Handled = true;
    };
    arrowStack.Children.Add(downArrow);

    contentGrid.Children.Add(arrowStack);
    Grid.SetColumn(arrowStack, 2);

    // Col 3: Load order number
    var orderText = new TextBlock
    {
      FontSize = 11,
      Foreground = new SolidColorBrush(Color.Parse("#666666")),
      VerticalAlignment = VerticalAlignment.Center,
      TextAlignment = TextAlignment.Right,
      Width = 24,
      Margin = new Thickness(2, 0)
    };
    orderText.Bind(TextBlock.TextProperty,
      new Avalonia.Data.Binding("LoadOrder"));
    contentGrid.Children.Add(orderText);
    Grid.SetColumn(orderText, 3);

    // Col 4: Drag grip — wide touch-friendly handle
    var gripArea = new Border
    {
      MinWidth = 44,
      MinHeight = 44,
      Background = Brushes.Transparent,
      Cursor = new Cursor(StandardCursorType.SizeAll),
      HorizontalAlignment = HorizontalAlignment.Stretch,
      VerticalAlignment = VerticalAlignment.Stretch,
      Child = new TextBlock
      {
        Text = "\u22EE",
        FontSize = 18,
        Foreground = new SolidColorBrush(Color.Parse("#555555")),
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
      }
    };
    ToolTip.SetTip(gripArea, "Drag to reorder modpacks");
    gripArea.PointerPressed += async (sender, e) =>
    {
      if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed
          && sender is Control ctrl
          && ctrl.DataContext is ModpackItemViewModel item)
      {
        _draggedModpackItem = item;
        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateText(item.Name));
        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
        _draggedModpackItem = null;
      }
    };
    contentGrid.Children.Add(gripArea);
    Grid.SetColumn(gripArea, 4);

    outerGrid.Children.Add(contentGrid);
    Grid.SetColumn(contentGrid, 1);

    return outerGrid;
  }

  private static ModpackItemViewModel? FindDropTarget(DragEventArgs e)
  {
    var target = e.Source as Control;
    while (target != null)
    {
      if (target.DataContext is ModpackItemViewModel item)
        return item;
      target = target.Parent as Control;
    }
    return null;
  }

  private static readonly FuncValueConverter<bool, bool> InvertBoolConverter =
    new(v => !v);

  private Control BuildModpackDetails()
  {
    var border = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1A1A1A")),
      Padding = new Thickness(24)
    };

    var mainStack = new StackPanel();

    // --- Editable modpack fields (hidden for standalone) ---
    var editableSection = new StackPanel();
    editableSection.Bind(StackPanel.IsVisibleProperty,
      new Avalonia.Data.Binding("SelectedModpack.IsStandalone") { Converter = InvertBoolConverter });

    // Name field
    editableSection.Children.Add(CreateLabel("Name"));
    var nameBox = CreateTextBox();
    nameBox.FontSize = 16;
    nameBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SelectedModpack.Name") { Mode = Avalonia.Data.BindingMode.TwoWay });
    editableSection.Children.Add(nameBox);

    // Author field
    editableSection.Children.Add(CreateLabel("Author"));
    var authorBox = CreateTextBox();
    authorBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SelectedModpack.Author") { Mode = Avalonia.Data.BindingMode.TwoWay });
    editableSection.Children.Add(authorBox);

    // Version field
    editableSection.Children.Add(CreateLabel("Version"));
    var versionBox = CreateTextBox();
    versionBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SelectedModpack.Version") { Mode = Avalonia.Data.BindingMode.TwoWay });
    editableSection.Children.Add(versionBox);

    // Load Order field
    editableSection.Children.Add(CreateLabel("Load Order"));
    var loadOrderBox = CreateTextBox();
    loadOrderBox.Watermark = "0-999";
    ToolTip.SetTip(loadOrderBox, "Lower numbers load first. Use this to control mod priority.");
    loadOrderBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SelectedModpack.LoadOrder") { Mode = Avalonia.Data.BindingMode.TwoWay });
    editableSection.Children.Add(loadOrderBox);

    // Dependencies field
    editableSection.Children.Add(CreateLabel("Dependencies (comma-separated)"));
    var depsBox = CreateTextBox();
    depsBox.Watermark = "e.g., CoreMod, UIEnhancements";
    ToolTip.SetTip(depsBox, "List modpacks this one depends on, separated by commas. These will be loaded before this modpack.");
    depsBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SelectedModpack.DependenciesText") { Mode = Avalonia.Data.BindingMode.TwoWay });
    editableSection.Children.Add(depsBox);

    // Description field
    editableSection.Children.Add(CreateLabel("Description"));
    var descBox = CreateTextBox();
    descBox.AcceptsReturn = true;
    descBox.TextWrapping = TextWrapping.Wrap;
    descBox.MinHeight = 80;
    descBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SelectedModpack.Description") { Mode = Avalonia.Data.BindingMode.TwoWay });
    editableSection.Children.Add(descBox);

    // Security Status display
    editableSection.Children.Add(CreateLabel("Security Status"));
    var secText = new TextBlock
    {
      Foreground = Brushes.White,
      FontSize = 12,
      Margin = new Thickness(0, 0, 0, 16)
    };
    secText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("SelectedModpack.SecurityStatusDisplay"));
    editableSection.Children.Add(secText);

    // Stats Changes section
    var statsLabel = CreateLabel("Stats Changes");
    statsLabel.Bind(TextBlock.IsVisibleProperty, new Avalonia.Data.Binding("SelectedModpack.HasStatsPatches"));
    editableSection.Children.Add(statsLabel);

    var statsItemsControl = new ItemsControl
    {
      Margin = new Thickness(0, 0, 0, 16)
    };
    statsItemsControl.Bind(ItemsControl.IsVisibleProperty, new Avalonia.Data.Binding("SelectedModpack.HasStatsPatches"));
    statsItemsControl.Bind(ItemsControl.ItemsSourceProperty, new Avalonia.Data.Binding("SelectedModpack.StatsPatches"));
    statsItemsControl.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<StatsPatchEntry>((entry, _) =>
    {
      var btn = new Button
      {
        Margin = new Thickness(0, 1),
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
        Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
      };
      btn.Classes.Add("listItem");
      var stack = new StackPanel();
      var nameText = new TextBlock
      {
        FontSize = 12,
        FontWeight = FontWeight.SemiBold,
        Foreground = new SolidColorBrush(Color.Parse("#4FC3F7"))
      };
      nameText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("DisplayName"));
      stack.Children.Add(nameText);

      var fieldsText = new TextBlock
      {
        FontSize = 10,
        Opacity = 0.7,
        Foreground = Brushes.White,
        TextWrapping = TextWrapping.Wrap
      };
      fieldsText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("FieldSummary"));
      stack.Children.Add(fieldsText);

      btn.Content = stack;
      btn.Click += (s, e) =>
      {
        if (btn.DataContext is StatsPatchEntry patch && DataContext is ModpacksViewModel vm)
        {
          var modpackName = vm.SelectedModpack?.Name;
          if (modpackName != null)
            vm.NavigateToStatsEntry?.Invoke(modpackName, patch.TemplateType, patch.InstanceName);
        }
      };
      return btn;
    });
    editableSection.Children.Add(statsItemsControl);

    // Files list
    editableSection.Children.Add(CreateLabel("Files"));
    var filesListBox = new ListBox
    {
      Background = new SolidColorBrush(Color.Parse("#252525")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(8),
      FontFamily = new FontFamily("monospace"),
      FontSize = 11,
      MaxHeight = 200,
      Margin = new Thickness(0, 0, 0, 16)
    };
    filesListBox.Bind(ListBox.ItemsSourceProperty, new Avalonia.Data.Binding("SelectedModpack.Files"));
    editableSection.Children.Add(filesListBox);

    mainStack.Children.Add(editableSection);

    // --- Read-only standalone section (shown for standalone) ---
    var standaloneSection = new StackPanel();
    standaloneSection.Bind(StackPanel.IsVisibleProperty,
      new Avalonia.Data.Binding("SelectedModpack.IsStandalone"));

    var standaloneTitle = new TextBlock { FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 4) };
    standaloneTitle.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("SelectedModpack.Name"));
    standaloneSection.Children.Add(standaloneTitle);

    var standaloneBadge = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#333333")),
      CornerRadius = new CornerRadius(3),
      Padding = new Thickness(6, 2),
      HorizontalAlignment = HorizontalAlignment.Left,
      Margin = new Thickness(0, 0, 0, 12),
      Child = new TextBlock
      {
        Text = "Standalone DLL",
        FontSize = 10,
        Foreground = new SolidColorBrush(Color.Parse("#999999")),
        FontWeight = FontWeight.SemiBold
      }
    };
    standaloneSection.Children.Add(standaloneBadge);

    standaloneSection.Children.Add(CreateLabel("Author"));
    var saAuthor = new TextBlock { Foreground = Brushes.White, FontSize = 12, Margin = new Thickness(0, 0, 0, 12) };
    saAuthor.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("SelectedModpack.Author"));
    standaloneSection.Children.Add(saAuthor);

    standaloneSection.Children.Add(CreateLabel("Version"));
    var saVersion = new TextBlock { Foreground = Brushes.White, FontSize = 12, Margin = new Thickness(0, 0, 0, 12) };
    saVersion.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("SelectedModpack.VersionDisplay"));
    standaloneSection.Children.Add(saVersion);

    standaloneSection.Children.Add(CreateLabel("Description"));
    var saDesc = new TextBlock
    {
      Foreground = Brushes.White,
      FontSize = 12,
      TextWrapping = TextWrapping.Wrap,
      Margin = new Thickness(0, 0, 0, 12)
    };
    saDesc.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("SelectedModpack.Description"));
    standaloneSection.Children.Add(saDesc);

    standaloneSection.Children.Add(CreateLabel("DLL File"));
    var saDll = new TextBlock
    {
      Foreground = new SolidColorBrush(Color.Parse("#BBBBBB")),
      FontSize = 12,
      FontFamily = new FontFamily("monospace"),
      Margin = new Thickness(0, 0, 0, 16)
    };
    saDll.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("SelectedModpack.DllFileName"));
    standaloneSection.Children.Add(saDll);

    // Conflict warning banner — amber when deployed, grey when inactive
    var conflictBanner = new Border
    {
      BorderThickness = new Thickness(1),
      CornerRadius = new CornerRadius(4),
      Padding = new Thickness(12, 8),
      Margin = new Thickness(0, 0, 0, 16)
    };
    var bannerBgConverter = new FuncValueConverter<bool, IBrush>(
      deployed => deployed
        ? new SolidColorBrush(Color.Parse("#2e2400"))
        : new SolidColorBrush(Color.Parse("#2a2a2a")));
    var bannerBorderConverter = new FuncValueConverter<bool, IBrush>(
      deployed => deployed
        ? new SolidColorBrush(Color.Parse("#c89b3c"))
        : new SolidColorBrush(Color.Parse("#555555")));
    var bannerFgConverter = new FuncValueConverter<bool, IBrush>(
      deployed => deployed
        ? new SolidColorBrush(Color.Parse("#c89b3c"))
        : new SolidColorBrush(Color.Parse("#999999")));
    conflictBanner.Bind(Border.BackgroundProperty,
      new Avalonia.Data.Binding("SelectedModpack.IsDeployed") { Converter = bannerBgConverter });
    conflictBanner.Bind(Border.BorderBrushProperty,
      new Avalonia.Data.Binding("SelectedModpack.IsDeployed") { Converter = bannerBorderConverter });
    var conflictText = new TextBlock
    {
      FontSize = 12,
      TextWrapping = TextWrapping.Wrap
    };
    conflictText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("SelectedModpack.ConflictWarning"));
    conflictText.Bind(TextBlock.ForegroundProperty,
      new Avalonia.Data.Binding("SelectedModpack.IsDeployed") { Converter = bannerFgConverter });
    conflictBanner.Child = conflictText;
    conflictBanner.Bind(Border.IsVisibleProperty, new Avalonia.Data.Binding("SelectedModpack.HasConflict"));
    standaloneSection.Children.Add(conflictBanner);

    mainStack.Children.Add(standaloneSection);

    // Shared update status panel for selected modpack
    var updateStatusPanel = new Border
    {
      CornerRadius = new CornerRadius(4),
      BorderThickness = new Thickness(1),
      Padding = new Thickness(12, 8),
      Margin = new Thickness(0, 0, 0, 16)
    };
    updateStatusPanel.Bind(Border.IsVisibleProperty, new Avalonia.Data.Binding("SelectedModpack.ShowUpdateStatus"));

    var updateStateToBackground = new FuncValueConverter<ModUpdateState, IBrush>(state => state switch
    {
      ModUpdateState.UpdateAvailable => new SolidColorBrush(Color.Parse("#3A1F00")),
      ModUpdateState.Error => new SolidColorBrush(Color.Parse("#2A1A1A")),
      ModUpdateState.Checking => new SolidColorBrush(Color.Parse("#1F2937")),
      ModUpdateState.UpToDate => new SolidColorBrush(Color.Parse("#0F2A1F")),
      _ => new SolidColorBrush(Color.Parse("#252525"))
    });
    var updateStateToBorder = new FuncValueConverter<ModUpdateState, IBrush>(state => state switch
    {
      ModUpdateState.UpdateAvailable => new SolidColorBrush(Color.Parse("#EAB308")),
      ModUpdateState.Error => new SolidColorBrush(Color.Parse("#F87171")),
      ModUpdateState.Checking => new SolidColorBrush(Color.Parse("#60A5FA")),
      ModUpdateState.UpToDate => new SolidColorBrush(Color.Parse("#34D399")),
      _ => new SolidColorBrush(Color.Parse("#555555"))
    });
    var updateStateToText = new FuncValueConverter<ModUpdateState, IBrush>(state => state switch
    {
      ModUpdateState.UpdateAvailable => new SolidColorBrush(Color.Parse("#FACC15")),
      ModUpdateState.Error => new SolidColorBrush(Color.Parse("#FCA5A5")),
      ModUpdateState.Checking => new SolidColorBrush(Color.Parse("#93C5FD")),
      ModUpdateState.UpToDate => new SolidColorBrush(Color.Parse("#6EE7B7")),
      _ => new SolidColorBrush(Color.Parse("#CCCCCC"))
    });
    updateStatusPanel.Bind(Border.BackgroundProperty,
      new Avalonia.Data.Binding("SelectedModpack.UpdateState") { Converter = updateStateToBackground });
    updateStatusPanel.Bind(Border.BorderBrushProperty,
      new Avalonia.Data.Binding("SelectedModpack.UpdateState") { Converter = updateStateToBorder });

    var updateStatusText = new TextBlock
    {
      FontSize = 12,
      TextWrapping = TextWrapping.Wrap
    };
    updateStatusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("SelectedModpack.UpdateSummary"));
    updateStatusText.Bind(TextBlock.ForegroundProperty,
      new Avalonia.Data.Binding("SelectedModpack.UpdateState") { Converter = updateStateToText });
    updateStatusPanel.Child = updateStatusText;
    mainStack.Children.Add(updateStatusPanel);

    // Per-modpack action buttons
    var buttonPanel = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 12,
      Margin = new Thickness(0, 8, 0, 0)
    };

    // Deploy / Undeploy toggle button
    var deployToggleButton = new Button
    {
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(16, 8)
    };
    var deployToggleContentConverter = new FuncValueConverter<bool, string>(
      deploying => deploying ? "Deploying\u2026" : null!);
    // Show "Deploying..." when busy, otherwise the normal toggle text
    var deployToggleContentBinding = new Avalonia.Data.MultiBinding
    {
      Converter = new FuncMultiValueConverter<object, string>(values =>
      {
        var vals = values.ToList();
        var isDeploying = vals.Count > 0 && vals[0] is bool b && b;
        var toggleText = vals.Count > 1 ? vals[1] as string ?? "" : "";
        return isDeploying ? "Deploying\u2026" : toggleText;
      }),
      Bindings =
      {
        new Avalonia.Data.Binding("IsDeploying"),
        new Avalonia.Data.Binding("DeployToggleText")
      }
    };
    deployToggleButton.Bind(Button.ContentProperty, deployToggleContentBinding);
    var deployBgConverter = new FuncValueConverter<string, IBrush>(
      text => text == "Undeploy"
        ? new SolidColorBrush(Color.Parse("#4b0606"))
        : new SolidColorBrush(Color.Parse("#004f43")));
    deployToggleButton.Bind(Button.BackgroundProperty,
      new Avalonia.Data.Binding("DeployToggleText") { Converter = deployBgConverter });
    deployToggleButton.Bind(Button.IsEnabledProperty,
      new Avalonia.Data.Binding("IsDeploying") { Converter = InvertBoolConverter });
    deployToggleButton.Click += OnToggleDeployClick;
    buttonPanel.Children.Add(deployToggleButton);

    var exportButton = new Button
    {
      Content = "Export Modpack"
    };
    exportButton.Classes.Add("secondary");
    exportButton.Click += OnExportClick;
    exportButton.Bind(Button.IsEnabledProperty,
      new Avalonia.Data.Binding("IsDeploying") { Converter = InvertBoolConverter });
    // Hide Export for standalone mods
    exportButton.Bind(Button.IsVisibleProperty,
      new Avalonia.Data.Binding("SelectedModpack.IsStandalone") { Converter = InvertBoolConverter });
    buttonPanel.Children.Add(exportButton);

    var deleteButton = new Button
    {
      Content = "Delete Modpack"
    };
    deleteButton.Classes.Add("destructive");
    deleteButton.Click += async (_, _) =>
    {
      if (DataContext is ModpacksViewModel vm && vm.SelectedModpack != null)
      {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window window) return;

        var confirmed = await ConfirmationDialog.ShowAsync(
          window,
          "Delete Modpack",
          $"Are you sure you want to delete '{vm.SelectedModpack.Name}'? This cannot be undone.",
          "Delete",
          isDestructive: true
        );

        if (confirmed)
          vm.DeleteSelectedModpack();
      }
    };
    deleteButton.Bind(Button.IsEnabledProperty,
      new Avalonia.Data.Binding("IsDeploying") { Converter = InvertBoolConverter });
    // Hide Delete for standalone mods
    deleteButton.Bind(Button.IsVisibleProperty,
      new Avalonia.Data.Binding("SelectedModpack.IsStandalone") { Converter = InvertBoolConverter });
    buttonPanel.Children.Add(deleteButton);

    mainStack.Children.Add(buttonPanel);

    // --- Global deploy section ---
    var deploySeparator = new Border
    {
      Height = 1,
      Background = new SolidColorBrush(Color.Parse("#3E3E3E")),
      Margin = new Thickness(0, 20, 0, 16)
    };
    mainStack.Children.Add(deploySeparator);

    mainStack.Children.Add(CreateLabel("Global Deployment"));

    var globalButtonPanel = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 12,
      Margin = new Thickness(0, 4, 0, 8)
    };

    var deployAllButton = new Button
    {
      Content = "Deploy All",
      Padding = new Thickness(20, 8)
    };
    deployAllButton.Classes.Add("primary");
    deployAllButton.Bind(Button.IsEnabledProperty,
      new Avalonia.Data.Binding("IsDeploying") { Converter = InvertBoolConverter });
    deployAllButton.Click += OnDeployAllClick;
    globalButtonPanel.Children.Add(deployAllButton);

    var undeployAllButton = new Button
    {
      Content = "Undeploy All",
      Padding = new Thickness(20, 8)
    };
    undeployAllButton.Classes.Add("destructive");
    undeployAllButton.Bind(Button.IsEnabledProperty,
      new Avalonia.Data.Binding("IsDeploying") { Converter = InvertBoolConverter });
    undeployAllButton.Click += OnUndeployAllClick;
    globalButtonPanel.Children.Add(undeployAllButton);

    mainStack.Children.Add(globalButtonPanel);

    var deployStatusText = new SelectableTextBlock
    {
      FontSize = 12,
      Foreground = new SolidColorBrush(Color.Parse("#BBBBBB")),
      TextWrapping = TextWrapping.Wrap,
      Margin = new Thickness(0, 4, 0, 0)
    };
    deployStatusText.Bind(SelectableTextBlock.TextProperty, new Avalonia.Data.Binding("DeployStatus"));
    mainStack.Children.Add(deployStatusText);

    var scrollViewer = new ScrollViewer
    {
      Content = mainStack
    };

    border.Child = scrollViewer;
    return border;
  }

  private static TextBlock CreateLabel(string text) => new TextBlock
  {
    Text = text,
    FontSize = 11,
    FontWeight = FontWeight.SemiBold,
    Foreground = Brushes.White,
    Opacity = 0.8,
    Margin = new Thickness(0, 0, 0, 6)
  };

  private static TextBox CreateTextBox()
  {
    var textBox = new TextBox
    {
      Margin = new Thickness(0, 0, 0, 16)
    };
    textBox.Classes.Add("input");
    return textBox;
  }

  private async void OnToggleDeployClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    try
    {
      if (DataContext is ModpacksViewModel vm && vm.SelectedModpack != null && !vm.IsDeploying)
      {
        await vm.ToggleDeploySelectedAsync();
      }
    }
    catch (Exception ex)
    {
      Services.ModkitLog.Error($"Deploy toggle failed: {ex.Message}");
    }
  }

  private async void OnExportClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    try
    {
      if (DataContext is ModpacksViewModel vm && vm.SelectedModpack != null)
      {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var storageProvider = topLevel.StorageProvider;
        var result = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
          Title = "Export Modpack",
          SuggestedFileName = $"{vm.SelectedModpack.Name}.zip",
          FileTypeChoices = new[]
          {
            new FilePickerFileType("ZIP Archive") { Patterns = new[] { "*.zip" } }
          }
        });

        if (result != null)
        {
          vm.SelectedModpack.Export(result.Path.LocalPath);
        }
      }
    }
    catch (Exception ex)
    {
      Services.ModkitLog.Error($"Export failed: {ex.Message}");
    }
  }

  private async void OnDeployAllClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    try
    {
      if (DataContext is ModpacksViewModel vm && !vm.IsDeploying)
      {
        await vm.DeployAllAsync();
      }
    }
    catch (Exception ex)
    {
      Services.ModkitLog.Error($"Deploy all failed: {ex.Message}");
    }
  }

  private async void OnUndeployAllClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    try
    {
      if (DataContext is ModpacksViewModel vm && !vm.IsDeploying)
      {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window window) return;

        var confirmed = await ConfirmationDialog.ShowAsync(
          window,
          "Undeploy All Modpacks",
          "This will undeploy all active modpacks. Continue?",
          "Undeploy All",
          isDestructive: true
        );

        if (confirmed)
          await vm.UndeployAllAsync();
      }
    }
    catch (Exception ex)
    {
      Services.ModkitLog.Error($"Undeploy all failed: {ex.Message}");
    }
  }

  private async void ShowCreateDialog()
  {
    try
    {
      if (DataContext is ModpacksViewModel vm)
      {
        var dialog = new CreateModpackDialog();
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window window)
        {
          var result = await dialog.ShowDialog<CreateModpackResult?>(window);
          if (result != null)
            vm.CreateNewModpack(result.Name, result.Author, result.Description);
        }
      }
    }
    catch (Exception ex)
    {
      Services.ModkitLog.Error($"Create dialog failed: {ex.Message}");
    }
  }

  private async void OnImportModClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    try
    {
      if (DataContext is not ModpacksViewModel vm)
        return;

      var topLevel = TopLevel.GetTopLevel(this);
      if (topLevel == null) return;

      var storageProvider = topLevel.StorageProvider;
      var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
      {
        Title = "Import Mod",
        AllowMultiple = true,
        FileTypeFilter = new[]
        {
          new FilePickerFileType("Mod Package") { Patterns = new[] { "*.zip", "*.7z", "*.rar" } },
          new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
        }
      });

      if (result.Count > 0)
      {
        var archivePaths = result.Select(f => f.Path.LocalPath).ToList();
        vm.ImportModpacksFromZips(archivePaths);
      }
    }
    catch (Exception ex)
    {
      Services.ModkitLog.Error($"Import failed: {ex.Message}");
    }
  }

  /// <summary>
  /// Check if a file path is a supported archive format.
  /// </summary>
  private static bool IsArchiveFile(string path)
  {
    var ext = Path.GetExtension(path).ToLowerInvariant();
    return ext is ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".tgz" or ".bz2";
  }
}
