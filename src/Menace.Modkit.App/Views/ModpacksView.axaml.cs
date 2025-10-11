using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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

    // Left: Modpack List
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
      RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,*")
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
      BorderThickness = new Thickness(0)
    };
    activeList.Bind(ListBox.ItemsSourceProperty,
      new Avalonia.Data.Binding("ActiveMods"));

    activeList.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ModpackItemViewModel>(
      (modpack, _) => CreateModpackListItem(modpack));

    grid.Children.Add(activeList);
    Grid.SetRow(activeList, 4);

    return grid;
  }

  private Control CreateModpackListItem(ModpackItemViewModel modpack)
  {
    var stack = new StackPanel
    {
      Margin = new Thickness(12, 8)
    };

    var nameText = new TextBlock
    {
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White,
      FontSize = 13
    };
    nameText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("Name"));
    stack.Children.Add(nameText);

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

    // Name field (editable)
    var nameLabel = new TextBlock { Text = "Name", FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, Opacity = 0.8, Margin = new Thickness(0, 0, 0, 4) };
    mainStack.Children.Add(nameLabel);

    var nameBox = new TextBox
    {
      FontSize = 16,
      Foreground = Brushes.White,
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
      BorderThickness = new Thickness(1),
      Padding = new Thickness(8, 6),
      Margin = new Thickness(0, 0, 0, 16)
    };
    nameBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SelectedModpack.Name") { Mode = Avalonia.Data.BindingMode.TwoWay });
    mainStack.Children.Add(nameBox);

    // Author field (editable)
    var authorLabel = new TextBlock { Text = "Author", FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, Opacity = 0.8, Margin = new Thickness(0, 0, 0, 4) };
    mainStack.Children.Add(authorLabel);

    var authorBox = new TextBox
    {
      Foreground = Brushes.White,
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
      BorderThickness = new Thickness(1),
      Padding = new Thickness(8, 6),
      Margin = new Thickness(0, 0, 0, 16)
    };
    authorBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SelectedModpack.Author") { Mode = Avalonia.Data.BindingMode.TwoWay });
    mainStack.Children.Add(authorBox);

    // Version field (editable)
    var versionLabel = new TextBlock { Text = "Version", FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, Opacity = 0.8, Margin = new Thickness(0, 0, 0, 4) };
    mainStack.Children.Add(versionLabel);

    var versionBox = new TextBox
    {
      Foreground = Brushes.White,
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
      BorderThickness = new Thickness(1),
      Padding = new Thickness(8, 6),
      Margin = new Thickness(0, 0, 0, 16)
    };
    versionBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SelectedModpack.Version") { Mode = Avalonia.Data.BindingMode.TwoWay });
    mainStack.Children.Add(versionBox);

    // Description field (editable multiline)
    var descLabel = new TextBlock { Text = "Description", FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, Opacity = 0.8, Margin = new Thickness(0, 0, 0, 4) };
    mainStack.Children.Add(descLabel);

    var descBox = new TextBox
    {
      Foreground = Brushes.White,
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
      BorderThickness = new Thickness(1),
      Padding = new Thickness(8, 6),
      AcceptsReturn = true,
      TextWrapping = TextWrapping.Wrap,
      MinHeight = 80,
      Margin = new Thickness(0, 0, 0, 16)
    };
    descBox.Bind(TextBox.TextProperty, new Avalonia.Data.Binding("SelectedModpack.Description") { Mode = Avalonia.Data.BindingMode.TwoWay });
    mainStack.Children.Add(descBox);

    // Files list
    var filesLabel = new TextBlock { Text = "Files", FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, Opacity = 0.8, Margin = new Thickness(0, 0, 0, 8) };
    mainStack.Children.Add(filesLabel);

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

    // Action Buttons
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
    buttonPanel.Children.Add(deployButton);

    var exportButton = new Button
    {
      Content = "Export Modpack",
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(16, 8)
    };
    buttonPanel.Children.Add(exportButton);

    mainStack.Children.Add(buttonPanel);

    var scrollViewer = new ScrollViewer
    {
      Content = mainStack
    };

    border.Child = scrollViewer;
    return border;
  }

  private async void ShowCreateDialog()
  {
    // TODO: Show proper dialog
    // For now, create a test modpack
    if (DataContext is ModpacksViewModel vm)
    {
      vm.CreateNewModpack(
        "Test Modpack",
        "Modder",
        "A test modpack created from the UI");
    }
  }
}
