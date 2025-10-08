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
    var grid = new Grid
    {
      RowDefinitions = new RowDefinitions("Auto,Auto,*")
    };

    // Title
    var title = new TextBlock
    {
      Text = "Asset Overview",
      FontSize = 18,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White,
      Margin = new Thickness(0, 0, 0, 16)
    };
    grid.Children.Add(title);
    Grid.SetRow(title, 0);

    // Search bar
    var searchPanel = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 12,
      Margin = new Thickness(0, 0, 0, 24)
    };
    var searchBox = new TextBox
    {
      Width = 300,
        Watermark = "Search assets…"
    };
    searchBox.Bind(TextBox.TextProperty,
      new Avalonia.Data.Binding("SearchText") { Mode = Avalonia.Data.BindingMode.TwoWay });
    searchPanel.Children.Add(searchBox);

    var filterBorder = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      CornerRadius = new CornerRadius(6),
      Padding = new Thickness(12, 8),
      Opacity = 0.7
    };
    filterBorder.Child = new TextBlock
    {
      Text = "Placeholder filters",
      FontSize = 12,
      Foreground = Brushes.White
    };
    searchPanel.Children.Add(filterBorder);

    grid.Children.Add(searchPanel);
    Grid.SetRow(searchPanel, 1);

    // Items list
    var scrollViewer = new ScrollViewer();
    var itemsControl = new ItemsControl();
    itemsControl.Bind(ItemsControl.ItemsSourceProperty, new Avalonia.Data.Binding("Items"));

    // Item template
    itemsControl.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<AssetBrowserItemViewModel>(
      (item, _) => BuildAssetItem(item!));

    scrollViewer.Content = itemsControl;
    grid.Children.Add(scrollViewer);
    Grid.SetRow(scrollViewer, 2);

    return grid;
  }

  private Control BuildAssetItem(AssetBrowserItemViewModel item)
  {
    var border = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1F1F1F")),
      CornerRadius = new CornerRadius(12),
      Padding = new Thickness(20),
      Margin = new Thickness(0, 0, 0, 16)
    };

    var grid = new Grid
    {
      ColumnDefinitions = new ColumnDefinitions("*,Auto"),
      RowDefinitions = new RowDefinitions("Auto,Auto")
    };

    // Left: File info
    var leftStack = new StackPanel { Spacing = 4 };
    leftStack.Children.Add(new TextBlock
    {
      Text = item.FileName,
      FontSize = 16,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White
    });
    leftStack.Children.Add(new TextBlock
    {
      Text = item.SourcePath,
      Opacity = 0.6,
      FontSize = 12,
      Foreground = Brushes.White
    });
    grid.Children.Add(leftStack);
    Grid.SetColumn(leftStack, 0);
    Grid.SetRow(leftStack, 0);

    // Right: Version and counts
    var rightStack = new StackPanel
    {
      Spacing = 4,
      HorizontalAlignment = HorizontalAlignment.Right
    };
    rightStack.Children.Add(new TextBlock
    {
      Text = item.UnityVersion,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White
    });

    var countsPanel = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 12,
      HorizontalAlignment = HorizontalAlignment.Right
    };
    countsPanel.Children.Add(new TextBlock
    {
      Text = $"Types: {item.TypeCount}",
      Opacity = 0.7,
      Foreground = Brushes.White
    });
    countsPanel.Children.Add(new TextBlock
    {
      Text = $"Refs: {item.ReferenceTypeCount}",
      Opacity = 0.7,
      Foreground = Brushes.White
    });
    rightStack.Children.Add(countsPanel);

    grid.Children.Add(rightStack);
    Grid.SetColumn(rightStack, 1);
    Grid.SetRow(rightStack, 0);

    // Bottom: Summary
    var summaryBorder = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#242424")),
      CornerRadius = new CornerRadius(8),
      Padding = new Thickness(12),
      Margin = new Thickness(0, 12, 0, 0)
    };
    summaryBorder.Child = new TextBlock
    {
      Text = "Bundle summary placeholder – details will appear after typetree integration.",
      Opacity = 0.6,
      FontSize = 12,
      Foreground = Brushes.White
    };
    grid.Children.Add(summaryBorder);
    Grid.SetColumn(summaryBorder, 0);
    Grid.SetColumnSpan(summaryBorder, 2);
    Grid.SetRow(summaryBorder, 1);

    border.Child = grid;
    return border;
  }
}
