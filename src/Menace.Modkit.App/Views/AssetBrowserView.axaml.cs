using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Menace.Modkit.App.ViewModels;

namespace Menace.Modkit.App.Views;

public class AssetBrowserView : UserControl
{
  public AssetBrowserView()
  {
    Content = BuildUI();
  }

  private Control BuildUI()
  {
    var mainGrid = new Grid
    {
      ColumnDefinitions = new ColumnDefinitions("*,2*")
    };

    // Left: Asset Navigation Tree
    mainGrid.Children.Add(BuildNavigation());
    Grid.SetColumn((Control)mainGrid.Children[0], 0);

    // Right: Asset Viewer/Replacer
    mainGrid.Children.Add(BuildAssetViewer());
    Grid.SetColumn((Control)mainGrid.Children[1], 1);

    return mainGrid;
  }

  private Control BuildNavigation()
  {
    var border = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
      BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
      BorderThickness = new Thickness(0, 0, 1, 0),
      Padding = new Thickness(12)
    };

    var grid = new Grid
    {
      RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto")
    };

    // Search Box
    var searchBox = new TextBox
    {
      Watermark = "Search assets...",
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

    // Asset Tree with hierarchical folder/file display
    var scrollViewer = new ScrollViewer
    {
      HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
      VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
    };

    var treeView = new TreeView
    {
      Background = Brushes.Transparent,
      BorderThickness = new Thickness(0)
    };
    treeView.Bind(TreeView.ItemsSourceProperty,
      new Avalonia.Data.Binding("FolderTree"));

    scrollViewer.Content = treeView;
    grid.Children.Add(scrollViewer);
    Grid.SetRow(scrollViewer, 1);

    // Extraction Status Text
    var statusText = new TextBlock
    {
      Foreground = Brushes.White,
      Opacity = 0.8,
      FontSize = 11,
      Margin = new Thickness(0, 12, 0, 8),
      TextWrapping = TextWrapping.Wrap
    };
    statusText.Bind(TextBlock.TextProperty,
      new Avalonia.Data.Binding("ExtractionStatus"));
    grid.Children.Add(statusText);
    Grid.SetRow(statusText, 2);

    // Extract Assets Button
    var extractButton = new Button
    {
      Content = "Extract Assets",
      Background = new SolidColorBrush(Color.Parse("#064b48")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(16, 10),
      HorizontalAlignment = HorizontalAlignment.Stretch,
      FontSize = 13
    };
    extractButton.Click += OnExtractAssetsClick;
    extractButton.Bind(Button.IsEnabledProperty,
      new Avalonia.Data.Binding("!IsExtracting"));
    grid.Children.Add(extractButton);
    Grid.SetRow(extractButton, 3);

    border.Child = grid;
    return border;
  }

  private Control BuildAssetViewer()
  {
    var border = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#121212")),
      Padding = new Thickness(24)
    };

    var grid = new Grid
    {
      RowDefinitions = new RowDefinitions("Auto,*,Auto")
    };

    // Header with selected asset info
    var headerStack = new StackPanel
    {
      Spacing = 8,
      Margin = new Thickness(0, 0, 0, 16)
    };

    var titleText = new TextBlock
    {
      Text = "Asset Preview",
      FontSize = 18,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White
    };
    headerStack.Children.Add(titleText);

    var infoText = new TextBlock
    {
      Foreground = Brushes.White,
      Opacity = 0.7,
      FontSize = 12,
      TextWrapping = TextWrapping.Wrap
    };
    infoText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("PreviewText"));
    headerStack.Children.Add(infoText);

    grid.Children.Add(headerStack);
    Grid.SetRow(headerStack, 0);

    // Preview area (image or text)
    var previewBorder = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
      CornerRadius = new CornerRadius(8),
      Padding = new Thickness(16)
    };

    var previewStack = new StackPanel
    {
      VerticalAlignment = VerticalAlignment.Center,
      HorizontalAlignment = HorizontalAlignment.Center
    };

    // Image preview
    var imagePreview = new Image
    {
      MaxWidth = 600,
      MaxHeight = 600,
      Stretch = Avalonia.Media.Stretch.Uniform
    };
    imagePreview.Bind(Image.SourceProperty, new Avalonia.Data.Binding("PreviewImage"));
    imagePreview.Bind(Image.IsVisibleProperty, new Avalonia.Data.Binding("HasImagePreview"));
    previewStack.Children.Add(imagePreview);

    // Text preview (for scripts, json, etc.)
    var textScrollViewer = new ScrollViewer
    {
      MaxHeight = 600,
      HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
    };

    var textPreview = new TextBlock
    {
      Foreground = Brushes.White,
      FontFamily = new FontFamily("monospace"),
      FontSize = 12,
      TextWrapping = TextWrapping.NoWrap
    };
    textPreview.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("PreviewText"));
    textPreview.Bind(TextBlock.IsVisibleProperty, new Avalonia.Data.Binding("HasTextPreview"));

    textScrollViewer.Content = textPreview;
    textScrollViewer.Bind(ScrollViewer.IsVisibleProperty, new Avalonia.Data.Binding("HasTextPreview"));
    previewStack.Children.Add(textScrollViewer);

    // Empty state
    var emptyStack = new StackPanel
    {
      VerticalAlignment = VerticalAlignment.Center,
      HorizontalAlignment = HorizontalAlignment.Center,
      Spacing = 12
    };

    var emptyIcon = new TextBlock
    {
      Text = "📄",
      FontSize = 48,
      HorizontalAlignment = HorizontalAlignment.Center
    };
    emptyStack.Children.Add(emptyIcon);

    var emptyText = new TextBlock
    {
      Text = "Select an asset to preview",
      FontSize = 14,
      Foreground = Brushes.White,
      Opacity = 0.6,
      HorizontalAlignment = HorizontalAlignment.Center
    };
    emptyStack.Children.Add(emptyText);

    // Show empty state when no asset selected
    var isAssetSelected = new Avalonia.Data.Binding("SelectedAsset")
    {
      Converter = new Avalonia.Data.Converters.FuncValueConverter<object?, bool>(obj => obj == null)
    };
    emptyStack.Bind(StackPanel.IsVisibleProperty, isAssetSelected);
    previewStack.Bind(StackPanel.IsVisibleProperty, new Avalonia.Data.Binding("SelectedAsset")
    {
      Converter = new Avalonia.Data.Converters.FuncValueConverter<object?, bool>(obj => obj != null)
    });

    var previewContainer = new Panel();
    previewContainer.Children.Add(previewStack);
    previewContainer.Children.Add(emptyStack);

    previewBorder.Child = previewContainer;
    grid.Children.Add(previewBorder);
    Grid.SetRow(previewBorder, 1);

    // Action buttons (Replace Asset, Export, etc.)
    var actionStack = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 12,
      Margin = new Thickness(0, 16, 0, 0)
    };

    var replaceButton = new Button
    {
      Content = "Replace Asset...",
      Background = new SolidColorBrush(Color.Parse("#064b48")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(20, 10),
      FontSize = 13
    };
    replaceButton.Click += OnReplaceAssetClick;
    replaceButton.Bind(Button.IsEnabledProperty, new Avalonia.Data.Binding("SelectedAsset")
    {
      Converter = new Avalonia.Data.Converters.FuncValueConverter<object?, bool>(obj => obj != null)
    });
    actionStack.Children.Add(replaceButton);

    var exportButton = new Button
    {
      Content = "Export to File...",
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      Foreground = Brushes.White,
      BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
      BorderThickness = new Thickness(1),
      Padding = new Thickness(20, 10),
      FontSize = 13
    };
    exportButton.Click += OnExportAssetClick;
    exportButton.Bind(Button.IsEnabledProperty, new Avalonia.Data.Binding("SelectedAsset")
    {
      Converter = new Avalonia.Data.Converters.FuncValueConverter<object?, bool>(obj => obj != null)
    });
    actionStack.Children.Add(exportButton);

    grid.Children.Add(actionStack);
    Grid.SetRow(actionStack, 2);

    border.Child = grid;
    return border;
  }

  private async void OnReplaceAssetClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    if (DataContext is AssetBrowserViewModel vm && vm.SelectedAsset != null)
    {
      var dialog = new OpenFileDialog
      {
        Title = $"Select replacement for {vm.SelectedAsset.Name}"
      };

      // Set file filter based on asset type
      var ext = System.IO.Path.GetExtension(vm.SelectedAsset.Name).ToLowerInvariant();
      if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
      {
        dialog.Filters = new System.Collections.Generic.List<FileDialogFilter>
        {
          new FileDialogFilter { Name = "Images", Extensions = { "png", "jpg", "jpeg", "bmp" } },
          new FileDialogFilter { Name = "All Files", Extensions = { "*" } }
        };
      }

      if (this.VisualRoot is Window window)
      {
        var files = await dialog.ShowAsync(window);
        if (files != null && files.Length > 0)
        {
          // TODO: Get current modpack ID from app state or show selector
          // For now, use a default modpack or prompt user
          var currentModpackId = "default"; // TODO: Get from ModpacksViewModel or app state

          var success = await vm.ReplaceAssetInModpackAsync(
            vm.SelectedAsset.FullPath,
            files[0],
            currentModpackId
          );

          if (success)
          {
            // Show success notification
            var messageBox = new TextBlock
            {
              Text = $"✓ Asset replacement added to modpack '{currentModpackId}'",
              Foreground = Brushes.LightGreen,
              FontSize = 14,
              Margin = new Thickness(16)
            };

            // TODO: Show proper notification/toast
            System.Diagnostics.Debug.WriteLine($"✓ Replaced {vm.SelectedAsset.Name} with {files[0]}");
          }
          else
          {
            // Show error
            System.Diagnostics.Debug.WriteLine($"❌ Failed to replace asset");
          }
        }
      }
    }
  }

  private async void OnExportAssetClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    if (DataContext is AssetBrowserViewModel vm && vm.SelectedAsset != null)
    {
      var dialog = new SaveFileDialog
      {
        Title = "Export asset",
        DefaultExtension = System.IO.Path.GetExtension(vm.SelectedAsset.Name),
        InitialFileName = vm.SelectedAsset.Name
      };

      if (this.VisualRoot is Window window)
      {
        var file = await dialog.ShowAsync(window);
        if (file != null)
        {
          try
          {
            System.IO.File.Copy(vm.SelectedAsset.FullPath, file, true);
          }
          catch (Exception ex)
          {
            System.Diagnostics.Debug.WriteLine($"Export failed: {ex.Message}");
          }
        }
      }
    }
  }

  private async void OnExtractAssetsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    if (DataContext is AssetBrowserViewModel vm)
    {
      await vm.ExtractAssetsAsync();
    }
  }
}
