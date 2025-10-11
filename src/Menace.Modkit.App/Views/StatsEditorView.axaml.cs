using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Menace.Modkit.App.ViewModels;
using ReactiveUI;

namespace Menace.Modkit.App.Views;

public class StatsEditorView : UserControl
{
  public StatsEditorView()
  {
    Content = BuildUI();
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
      ColumnDefinitions = new ColumnDefinitions("*,2*")
    };

    // Left: Navigation Tree
    mainGrid.Children.Add(BuildNavigation());
    Grid.SetColumn((Control)mainGrid.Children[0], 0);

    // Right: Detail Panel
    mainGrid.Children.Add(BuildDetailPanel());
    Grid.SetColumn((Control)mainGrid.Children[1], 1);

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
      Text = "Expected data location:\n~/.steam/debian-installation/steamapps/common/Menace Demo/UserData/ExtractedData",
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
      Background = new SolidColorBrush(Color.Parse("#064b48")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(24, 12),
      FontSize = 14
    };
    setupButton.Click += OnAutoSetupClick;
    buttonPanel.Children.Add(setupButton);

    var refreshButton = new Button
    {
      Content = "Refresh",
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(1),
      BorderBrush = new SolidColorBrush(Color.Parse("#064b48")),
      Padding = new Thickness(24, 12),
      FontSize = 14
    };
    refreshButton.Click += OnRefreshClick;
    buttonPanel.Children.Add(refreshButton);

    var launchButton = new Button
    {
      Content = "Launch Game to Update Data",
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(1),
      BorderBrush = new SolidColorBrush(Color.Parse("#064b48")),
      Padding = new Thickness(24, 12),
      FontSize = 14
    };
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
    if (DataContext is StatsEditorViewModel vm)
    {
      await vm.AutoSetupAsync();
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
    if (DataContext is StatsEditorViewModel vm)
    {
      await vm.LaunchGameToUpdateDataAsync();
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
      Background = new SolidColorBrush(Color.Parse("#064b48")),
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
      RowDefinitions = new RowDefinitions("Auto,*")
    };

    // Search Box
    var searchBox = new TextBox
    {
      Watermark = "Search templates...",
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(12, 8),
      Margin = new Thickness(0, 0, 0, 12)
    };
    searchBox.Bind(TextBox.TextProperty,
      new Avalonia.Data.Binding("SearchText"));
    grid.Children.Add(searchBox);
    Grid.SetRow(searchBox, 0);

    // Hierarchical TreeView
    var treeView = new TreeView
    {
      Background = Brushes.Transparent,
      BorderThickness = new Thickness(0)
    };
    treeView.Bind(TreeView.ItemsSourceProperty,
      new Avalonia.Data.Binding("TreeNodes"));
    treeView.Bind(TreeView.SelectedItemProperty,
      new Avalonia.Data.Binding("SelectedNode"));

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
          Margin = new Thickness(4, 2)
        };
        return text;
      },
      node => node.Children);

    grid.Children.Add(treeView);
    Grid.SetRow(treeView, 1);

    return grid;
  }

  private Control BuildDetailPanel()
  {
    var border = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
      BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
      BorderThickness = new Thickness(1, 0, 0, 0),
      Padding = new Thickness(24)
    };

    var mainGrid = new Grid
    {
      ColumnDefinitions = new ColumnDefinitions("*,*")
    };

    // Left: Vanilla Stats
    var vanillaPanel = new StackPanel
    {
      Margin = new Thickness(0, 0, 12, 0)
    };
    vanillaPanel.Children.Add(new TextBlock
    {
      Text = "Vanilla",
      FontSize = 16,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White,
      Margin = new Thickness(0, 0, 0, 12)
    });

    var vanillaScrollViewer = new ScrollViewer
    {
      Background = new SolidColorBrush(Color.Parse("#252525")),
      Padding = new Thickness(16)
    };

    var vanillaPropertiesPanel = new StackPanel
    {
      Spacing = 8
    };
    vanillaPropertiesPanel.Bind(StackPanel.DataContextProperty,
      new Avalonia.Data.Binding("VanillaProperties"));

    // Dynamically build property fields for vanilla (read-only)
    var vanillaContent = new ContentControl();
    vanillaContent.Bind(ContentControl.ContentProperty,
      new Avalonia.Data.Binding("VanillaProperties"));
    vanillaContent.ContentTemplate = CreatePropertyGridTemplate(isEditable: false);

    vanillaScrollViewer.Content = vanillaContent;
    vanillaPanel.Children.Add(vanillaScrollViewer);

    mainGrid.Children.Add(vanillaPanel);
    Grid.SetColumn(vanillaPanel, 0);

    // Right: Modified Stats
    var modifiedPanel = new StackPanel
    {
      Margin = new Thickness(12, 0, 0, 0)
    };
    modifiedPanel.Children.Add(new TextBlock
    {
      Text = "Modified",
      FontSize = 16,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White,
      Margin = new Thickness(0, 0, 0, 12)
    });

    var modifiedScrollViewer = new ScrollViewer
    {
      Background = new SolidColorBrush(Color.Parse("#252525")),
      Padding = new Thickness(16)
    };

    var modifiedContent = new ContentControl();
    modifiedContent.Bind(ContentControl.ContentProperty,
      new Avalonia.Data.Binding("ModifiedProperties"));
    modifiedContent.ContentTemplate = CreatePropertyGridTemplate(isEditable: true);

    modifiedScrollViewer.Content = modifiedContent;
    modifiedPanel.Children.Add(modifiedScrollViewer);

    mainGrid.Children.Add(modifiedPanel);
    Grid.SetColumn(modifiedPanel, 1);

    border.Child = mainGrid;
    return border;
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

      var panel = new StackPanel { Spacing = 12 };

      foreach (var kvp in props)
      {
        var fieldControl = CreatePropertyField(kvp.Key, kvp.Value, isEditable, 0);
        panel.Children.Add(fieldControl);
      }

      return panel;
    });
  }

  private Control CreatePropertyField(string name, object? value, bool isEditable, int indent)
  {
    var fieldStack = new StackPanel { Spacing = 4, Margin = new Thickness(indent * 16, 0, 0, 0) };

    // Property label
    var label = new TextBlock
    {
      Text = name,
      Foreground = Brushes.White,
      Opacity = 0.8,
      FontSize = 11,
      FontWeight = FontWeight.SemiBold
    };
    fieldStack.Children.Add(label);

    // Handle nested objects and arrays
    if (value is System.Text.Json.JsonElement jsonElement)
    {
      switch (jsonElement.ValueKind)
      {
        case System.Text.Json.JsonValueKind.Object:
          // Nested object - render as indented group
          var nestedPanel = new StackPanel { Spacing = 8, Margin = new Thickness(16, 4, 0, 0) };
          foreach (var prop in jsonElement.EnumerateObject())
          {
            var nestedField = CreatePropertyField(prop.Name, prop.Value, isEditable, 0);
            nestedPanel.Children.Add(nestedField);
          }
          fieldStack.Children.Add(nestedPanel);
          return fieldStack;

        case System.Text.Json.JsonValueKind.Array:
          // Array - show as formatted list
          var arrayPanel = new StackPanel { Spacing = 4, Margin = new Thickness(16, 4, 0, 0) };
          int idx = 0;
          foreach (var item in jsonElement.EnumerateArray())
          {
            var itemField = CreatePropertyField($"[{idx}]", item, isEditable, 0);
            arrayPanel.Children.Add(itemField);
            idx++;
          }
          fieldStack.Children.Add(arrayPanel);
          return fieldStack;

        default:
          // Extract the actual primitive value from JsonElement
          value = jsonElement.ValueKind switch
          {
            System.Text.Json.JsonValueKind.String => jsonElement.GetString(),
            System.Text.Json.JsonValueKind.Number => jsonElement.GetDouble().ToString(),
            System.Text.Json.JsonValueKind.True => "true",
            System.Text.Json.JsonValueKind.False => "false",
            System.Text.Json.JsonValueKind.Null => "null",
            _ => jsonElement.ToString()
          };
          break;
      }
    }

    // Property value (primitive types)
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
        FontSize = 12
      };
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
}
