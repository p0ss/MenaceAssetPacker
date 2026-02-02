using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Menace.Modkit.App.Models;
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
      RowDefinitions = new RowDefinitions("Auto,Auto,*")
    };

    // Row 0: Search Box
    var searchBox = new TextBox
    {
      Watermark = "Search templates...",
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(12, 8),
      Margin = new Thickness(0, 0, 0, 8)
    };
    searchBox.Bind(TextBox.TextProperty,
      new Avalonia.Data.Binding("SearchText"));
    grid.Children.Add(searchBox);
    Grid.SetRow(searchBox, 0);

    // Row 1: Expand/Collapse buttons
    var buttonPanel = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 8,
      Margin = new Thickness(0, 0, 0, 8)
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
      if (DataContext is StatsEditorViewModel vm)
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
      if (DataContext is StatsEditorViewModel vm)
        vm.CollapseAll();
    };
    buttonPanel.Children.Add(collapseAllButton);

    var modpackOnlyToggle = new ToggleButton
    {
      Content = "Modpack Only",
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(1),
      BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
      Padding = new Thickness(10, 4),
      FontSize = 11
    };
    modpackOnlyToggle.Bind(ToggleButton.IsCheckedProperty,
      new Avalonia.Data.Binding("ShowModpackOnly")
      {
        Mode = Avalonia.Data.BindingMode.TwoWay
      });
    buttonPanel.Children.Add(modpackOnlyToggle);

    grid.Children.Add(buttonPanel);
    Grid.SetRow(buttonPanel, 1);

    // Row 2: Hierarchical TreeView
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

    // Bind IsExpanded to TreeViewItem containers
    treeView.ContainerPrepared += (_, e) =>
    {
      if (e.Container is TreeViewItem tvi && tvi.DataContext is TreeNodeViewModel nodeVm)
      {
        tvi.Bind(TreeViewItem.IsExpandedProperty,
          new Avalonia.Data.Binding("IsExpanded")
          {
            Mode = Avalonia.Data.BindingMode.TwoWay
          });
      }
    };

    grid.Children.Add(treeView);
    Grid.SetRow(treeView, 2);

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

    // Outer grid: toolbar row + content row
    var outerGrid = new Grid
    {
      RowDefinitions = new RowDefinitions("Auto,*")
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
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(1),
      BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
      FontSize = 12
    };
    modpackCombo.Bind(ComboBox.ItemsSourceProperty,
      new Avalonia.Data.Binding("AvailableModpacks"));
    modpackCombo.Bind(ComboBox.SelectedItemProperty,
      new Avalonia.Data.Binding("CurrentModpackName"));
    toolbar.Children.Add(modpackCombo);

    var saveButton = new Button
    {
      Content = "Save",
      Background = new SolidColorBrush(Color.Parse("#064b48")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(16, 6),
      FontSize = 12
    };
    saveButton.Click += OnSaveClick;
    toolbar.Children.Add(saveButton);

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
      Padding = new Thickness(16)
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
    var modifiedHeader = new TextBlock
    {
      Text = "Modified",
      FontSize = 16,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White,
      Margin = new Thickness(0, 0, 0, 12)
    };
    modifiedPanel.Children.Add(modifiedHeader);
    Grid.SetRow(modifiedHeader, 0);

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
    Grid.SetRow(modifiedScrollViewer, 1);

    mainGrid.Children.Add(modifiedPanel);
    Grid.SetColumn(modifiedPanel, 1);

    outerGrid.Children.Add(mainGrid);
    Grid.SetRow(mainGrid, 1);

    border.Child = outerGrid;
    return border;
  }

  private void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    if (DataContext is StatsEditorViewModel vm)
    {
      vm.SaveToStaging();
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
        FontSize = 12,
        Tag = name
      };
      textBox.TextChanged += OnEditableTextBoxChanged;
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
        Background = new SolidColorBrush(Color.Parse("#064b48")),
        Foreground = Brushes.White,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(10, 4),
        FontSize = 11
      };
      browseButton.Click += async (_, _) =>
      {
        if (DataContext is StatsEditorViewModel vm)
        {
          var dialog = new AssetPickerDialog(assetValue.AssetType);
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
            }
          }
        }
      };
      buttonRow.Children.Add(browseButton);

      var clearButton = new Button
      {
        Content = "Clear",
        Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
        Foreground = Brushes.White,
        BorderThickness = new Thickness(1),
        BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
        Padding = new Thickness(10, 4),
        FontSize = 11
      };
      clearButton.Click += (_, _) =>
      {
        assetValue.AssetName = null;
        assetValue.AssetFilePath = null;
        assetValue.ThumbnailPath = null;
        assetValue.RawValue = null;
        nameText.Text = "null";
        nameText.Foreground = new SolidColorBrush(Color.Parse("#888888"));
        nameText.FontStyle = FontStyle.Italic;
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

  private void OnEditableTextBoxChanged(object? sender, TextChangedEventArgs e)
  {
    if (sender is TextBox tb && tb.Tag is string fieldName && DataContext is StatsEditorViewModel vm)
    {
      vm.UpdateModifiedProperty(fieldName, tb.Text ?? "");
    }
  }
}
