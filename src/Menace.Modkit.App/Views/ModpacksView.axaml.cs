using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Menace.Modkit.App.ViewModels;

namespace Menace.Modkit.App.Views;

public class ModpacksView : UserControl
{
  public ModpacksView()
  {
    Content = BuildUI();
  }

  private Control BuildUI()
  {
    var mainGrid = new Grid
    {
      ColumnDefinitions = new ColumnDefinitions("*,2*")
    };

    // Left: Modpack List + Load Order
    mainGrid.Children.Add(BuildModpackList());
    Grid.SetColumn((Control)mainGrid.Children[0], 0);

    // Right: Modpack Details
    mainGrid.Children.Add(BuildModpackDetails());
    Grid.SetColumn((Control)mainGrid.Children[1], 1);

    return mainGrid;
  }

  private Control BuildModpackList()
  {
    var grid = new Grid
    {
      RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto,Auto,*,Auto,Auto")
    };

    // Title: Staging Modpacks
    var stagingTitle = new TextBlock
    {
      Text = "Staging Modpacks",
      FontSize = 14,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White,
      Margin = new Thickness(0, 0, 0, 12)
    };
    grid.Children.Add(stagingTitle);
    Grid.SetRow(stagingTitle, 0);

    // Create New Button
    var createButton = new Button
    {
      Content = "+ New Modpack",
      Background = new SolidColorBrush(Color.Parse("#064b48")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(16, 8),
      HorizontalAlignment = HorizontalAlignment.Stretch,
      Margin = new Thickness(0, 0, 0, 12)
    };
    createButton.Click += (_, _) => ShowCreateDialog();
    grid.Children.Add(createButton);
    Grid.SetRow(createButton, 1);

    // Staging Modpacks List
    var stagingList = new ListBox
    {
      Background = new SolidColorBrush(Color.Parse("#252525")),
      BorderThickness = new Thickness(0),
      Margin = new Thickness(0, 0, 0, 16)
    };
    stagingList.Bind(ListBox.ItemsSourceProperty,
      new Avalonia.Data.Binding("StagingModpacks"));
    stagingList.Bind(ListBox.SelectedItemProperty,
      new Avalonia.Data.Binding("SelectedModpack"));

    stagingList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ModpackItemViewModel>(
      (modpack, _) => CreateModpackListItem(modpack));

    grid.Children.Add(stagingList);
    Grid.SetRow(stagingList, 2);

    // Title: Active Mods
    var activeTitle = new TextBlock
    {
      Text = "Active Mods",
      FontSize = 14,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White,
      Margin = new Thickness(0, 0, 0, 12)
    };
    grid.Children.Add(activeTitle);
    Grid.SetRow(activeTitle, 3);

    // Active Mods List
    var activeList = new ListBox
    {
      Background = new SolidColorBrush(Color.Parse("#252525")),
      BorderThickness = new Thickness(0),
      Margin = new Thickness(0, 0, 0, 4)
    };
    activeList.Bind(ListBox.ItemsSourceProperty,
      new Avalonia.Data.Binding("ActiveMods"));
    activeList.Bind(ListBox.SelectedItemProperty,
      new Avalonia.Data.Binding("SelectedActiveMod"));

    activeList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ModpackItemViewModel>(
      (modpack, _) => CreateModpackListItem(modpack));

    grid.Children.Add(activeList);
    Grid.SetRow(activeList, 4);

    // Undeploy button for selected active mod
    var undeployBtn = new Button
    {
      Content = "Undeploy Selected",
      FontSize = 11,
      Background = new SolidColorBrush(Color.Parse("#4b0606")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(12, 4),
      HorizontalAlignment = HorizontalAlignment.Stretch,
      Margin = new Thickness(0, 0, 0, 16)
    };
    undeployBtn.Click += (_, _) =>
    {
      if (DataContext is ModpacksViewModel vm)
        vm.UndeploySelectedMod();
    };
    grid.Children.Add(undeployBtn);
    Grid.SetRow(undeployBtn, 5);

    // --- Load Order Section ---
    var loadOrderTitle = new TextBlock
    {
      Text = "Load Order",
      FontSize = 14,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White,
      Margin = new Thickness(0, 8, 0, 8)
    };
    grid.Children.Add(loadOrderTitle);
    Grid.SetRow(loadOrderTitle, 6);

    var loadOrderPanel = BuildLoadOrderPanel();
    grid.Children.Add(loadOrderPanel);
    Grid.SetRow(loadOrderPanel, 7);

    // Conflict status
    var conflictStatus = new TextBlock
    {
      FontSize = 11,
      Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
      Margin = new Thickness(0, 4, 0, 0)
    };
    conflictStatus.Bind(TextBlock.TextProperty,
      new Avalonia.Data.Binding("LoadOrderVM.StatusText"));
    grid.Children.Add(conflictStatus);
    Grid.SetRow(conflictStatus, 8);

    return grid;
  }

  private Control BuildLoadOrderPanel()
  {
    var panel = new StackPanel { Spacing = 4 };

    // Load order list with move buttons
    var loadOrderList = new ListBox
    {
      Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
      BorderThickness = new Thickness(0),
      MaxHeight = 150
    };
    loadOrderList.Bind(ListBox.ItemsSourceProperty,
      new Avalonia.Data.Binding("LoadOrderVM.OrderedModpacks"));

    loadOrderList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<LoadOrderItemViewModel>(
      (item, _) =>
      {
        var stack = new StackPanel
        {
          Orientation = Orientation.Horizontal,
          Spacing = 8,
          Margin = new Thickness(4, 2)
        };

        var orderText = new TextBlock
        {
          FontSize = 11,
          Foreground = new SolidColorBrush(Color.Parse("#888888")),
          VerticalAlignment = VerticalAlignment.Center,
          Width = 30
        };
        orderText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("LoadOrder"));
        stack.Children.Add(orderText);

        var nameText = new TextBlock
        {
          FontSize = 12,
          Foreground = Brushes.White,
          VerticalAlignment = VerticalAlignment.Center
        };
        nameText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("Name"));
        stack.Children.Add(nameText);

        // Indicators
        var codeIndicator = new TextBlock
        {
          Text = "[code]",
          FontSize = 10,
          Foreground = new SolidColorBrush(Color.Parse("#4488CC")),
          VerticalAlignment = VerticalAlignment.Center
        };
        codeIndicator.Bind(TextBlock.IsVisibleProperty, new Avalonia.Data.Binding("HasCode"));
        stack.Children.Add(codeIndicator);

        var patchIndicator = new TextBlock
        {
          Text = "[patches]",
          FontSize = 10,
          Foreground = new SolidColorBrush(Color.Parse("#44CC88")),
          VerticalAlignment = VerticalAlignment.Center
        };
        patchIndicator.Bind(TextBlock.IsVisibleProperty, new Avalonia.Data.Binding("HasPatches"));
        stack.Children.Add(patchIndicator);

        return stack;
      });

    panel.Children.Add(loadOrderList);

    // Move buttons
    var buttonRow = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 8,
      Margin = new Thickness(0, 4, 0, 0)
    };

    var moveUpBtn = new Button
    {
      Content = "Move Up",
      FontSize = 11,
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(12, 4)
    };
    moveUpBtn.Click += (_, _) =>
    {
      if (DataContext is ModpacksViewModel vm)
      {
        var selected = loadOrderList.SelectedItem as LoadOrderItemViewModel;
        if (selected != null)
          vm.LoadOrderVM.MoveUp(selected);
      }
    };
    buttonRow.Children.Add(moveUpBtn);

    var moveDownBtn = new Button
    {
      Content = "Move Down",
      FontSize = 11,
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(12, 4)
    };
    moveDownBtn.Click += (_, _) =>
    {
      if (DataContext is ModpacksViewModel vm)
      {
        var selected = loadOrderList.SelectedItem as LoadOrderItemViewModel;
        if (selected != null)
          vm.LoadOrderVM.MoveDown(selected);
      }
    };
    buttonRow.Children.Add(moveDownBtn);

    var refreshBtn = new Button
    {
      Content = "Refresh",
      FontSize = 11,
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(12, 4)
    };
    refreshBtn.Click += (_, _) =>
    {
      if (DataContext is ModpacksViewModel vm)
        vm.LoadOrderVM.Refresh();
    };
    buttonRow.Children.Add(refreshBtn);

    panel.Children.Add(buttonRow);

    return panel;
  }

  private Control CreateModpackListItem(ModpackItemViewModel modpack)
  {
    var stack = new StackPanel
    {
      Margin = new Thickness(12, 8)
    };

    var nameRow = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 8
    };

    var nameText = new TextBlock
    {
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White,
      FontSize = 13
    };
    nameText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("Name"));
    nameRow.Children.Add(nameText);

    // Security status indicator
    var securityText = new TextBlock
    {
      FontSize = 10,
      VerticalAlignment = VerticalAlignment.Center
    };
    securityText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("SecurityStatusDisplay"));
    securityText.Foreground = new SolidColorBrush(Color.Parse("#888888"));
    nameRow.Children.Add(securityText);

    stack.Children.Add(nameRow);

    var authorText = new TextBlock
    {
      Opacity = 0.6,
      Foreground = Brushes.White,
      FontSize = 11
    };
    authorText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("Author"));
    stack.Children.Add(authorText);

    return stack;
  }

  private Control BuildModpackDetails()
  {
    var border = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
      BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
      BorderThickness = new Thickness(1, 0, 0, 0),
      Padding = new Thickness(24)
    };

    var mainStack = new StackPanel();

    // Name field
    mainStack.Children.Add(CreateLabel("Name"));
    var nameBox = CreateTextBox();
    nameBox.FontSize = 16;
    nameBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SelectedModpack.Name") { Mode = Avalonia.Data.BindingMode.TwoWay });
    mainStack.Children.Add(nameBox);

    // Author field
    mainStack.Children.Add(CreateLabel("Author"));
    var authorBox = CreateTextBox();
    authorBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SelectedModpack.Author") { Mode = Avalonia.Data.BindingMode.TwoWay });
    mainStack.Children.Add(authorBox);

    // Version field
    mainStack.Children.Add(CreateLabel("Version"));
    var versionBox = CreateTextBox();
    versionBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SelectedModpack.Version") { Mode = Avalonia.Data.BindingMode.TwoWay });
    mainStack.Children.Add(versionBox);

    // Load Order field
    mainStack.Children.Add(CreateLabel("Load Order"));
    var loadOrderBox = CreateTextBox();
    loadOrderBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SelectedModpack.LoadOrder") { Mode = Avalonia.Data.BindingMode.TwoWay });
    mainStack.Children.Add(loadOrderBox);

    // Dependencies field
    mainStack.Children.Add(CreateLabel("Dependencies (comma-separated)"));
    var depsBox = CreateTextBox();
    depsBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SelectedModpack.DependenciesText") { Mode = Avalonia.Data.BindingMode.TwoWay });
    mainStack.Children.Add(depsBox);

    // Description field
    mainStack.Children.Add(CreateLabel("Description"));
    var descBox = CreateTextBox();
    descBox.AcceptsReturn = true;
    descBox.TextWrapping = TextWrapping.Wrap;
    descBox.MinHeight = 80;
    descBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SelectedModpack.Description") { Mode = Avalonia.Data.BindingMode.TwoWay });
    mainStack.Children.Add(descBox);

    // Security Status display
    mainStack.Children.Add(CreateLabel("Security Status"));
    var secText = new TextBlock
    {
      Foreground = Brushes.White,
      FontSize = 12,
      Margin = new Thickness(0, 0, 0, 16)
    };
    secText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("SelectedModpack.SecurityStatusDisplay"));
    mainStack.Children.Add(secText);

    // Files list
    mainStack.Children.Add(CreateLabel("Files"));
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
    mainStack.Children.Add(filesListBox);

    // Per-modpack action buttons
    var buttonPanel = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 12,
      Margin = new Thickness(0, 8, 0, 0)
    };

    var deployButton = new Button
    {
      Content = "Deploy to Game",
      Background = new SolidColorBrush(Color.Parse("#064b48")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(16, 8)
    };
    deployButton.Click += OnDeployClick;
    buttonPanel.Children.Add(deployButton);

    var exportButton = new Button
    {
      Content = "Export Modpack",
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(16, 8)
    };
    exportButton.Click += OnExportClick;
    buttonPanel.Children.Add(exportButton);

    var deleteButton = new Button
    {
      Content = "Delete Modpack",
      Background = new SolidColorBrush(Color.Parse("#4b0606")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(16, 8)
    };
    deleteButton.Click += (_, _) =>
    {
      if (DataContext is ModpacksViewModel vm)
        vm.DeleteSelectedModpack();
    };
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
      Background = new SolidColorBrush(Color.Parse("#064b48")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(20, 8)
    };
    deployAllButton.Click += OnDeployAllClick;
    globalButtonPanel.Children.Add(deployAllButton);

    var undeployAllButton = new Button
    {
      Content = "Undeploy All",
      Background = new SolidColorBrush(Color.Parse("#4b0606")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(20, 8)
    };
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
    Margin = new Thickness(0, 0, 0, 4)
  };

  private static TextBox CreateTextBox() => new TextBox
  {
    Foreground = Brushes.White,
    Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
    BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
    BorderThickness = new Thickness(1),
    Padding = new Thickness(8, 6),
    Margin = new Thickness(0, 0, 0, 16)
  };

  private async void OnDeployClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    if (DataContext is ModpacksViewModel vm && vm.SelectedModpack != null && !vm.IsDeploying)
    {
      await vm.DeploySingleAsync();
    }
  }

  private async void OnExportClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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

  private async void OnDeployAllClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    if (DataContext is ModpacksViewModel vm && !vm.IsDeploying)
    {
      await vm.DeployAllAsync();
    }
  }

  private async void OnUndeployAllClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    if (DataContext is ModpacksViewModel vm && !vm.IsDeploying)
    {
      await vm.UndeployAllAsync();
    }
  }

  private async void ShowCreateDialog()
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
}
