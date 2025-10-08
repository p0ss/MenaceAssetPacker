using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Menace.Modkit.App.Views;

public class StatsEditorView : UserControl
{
  public StatsEditorView()
  {
    Content = BuildUI();
  }

  private Control BuildUI()
  {
    var grid = new Grid
    {
      RowDefinitions = new RowDefinitions("Auto,*")
    };

    // Title
    var title = new TextBlock
    {
      Text = "Stats Editor",
      FontSize = 20,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White,
      Margin = new Thickness(0, 0, 0, 16)
    };
    grid.Children.Add(title);
    Grid.SetRow(title, 0);

    // Content
    var border = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1F1F1F")),
      CornerRadius = new CornerRadius(8),
      Padding = new Thickness(24)
    };
    border.Child = new TextBlock
    {
      Text = "Stats editor UI coming soon.",
      Opacity = 0.6,
      Foreground = Brushes.White
    };
    grid.Children.Add(border);
    Grid.SetRow(border, 1);

    return grid;
  }
}
