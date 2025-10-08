using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Menace.Modkit.App.ViewModels;
using ReactiveUI;

namespace Menace.Modkit.App.Views;

public class MainWindow : Window
{
  private readonly MainViewModel _viewModel;
  private ContentControl? _contentArea;

  public MainWindow(IServiceProvider serviceProvider)
  {
    _viewModel = new MainViewModel(serviceProvider);
    DataContext = _viewModel;

    Width = 1100;
    Height = 700;
    Title = "Menace Modkit";
    Background = new SolidColorBrush(Color.Parse("#121212"));

    Content = BuildUI();
  }

  private Control BuildUI()
  {
    var mainGrid = new Grid
    {
      ColumnDefinitions = new ColumnDefinitions("260,*")
    };

    // Left sidebar
    mainGrid.Children.Add(BuildSidebar());
    Grid.SetColumn((Control)mainGrid.Children[0], 0);

    // Right content area
    var rightPanel = BuildContentArea();
    mainGrid.Children.Add(rightPanel);
    Grid.SetColumn(rightPanel, 1);

    return mainGrid;
  }

  private Control BuildSidebar()
  {
    var border = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#181818")),
      Padding = new Thickness(32),
      CornerRadius = new CornerRadius(0, 16, 16, 0)
    };

    var stack = new StackPanel
    {
      Spacing = 32
    };

    // Header
    var header = new StackPanel { Spacing = 4 };
    header.Children.Add(new TextBlock
    {
      Text = "Menace Modkit",
      FontSize = 24,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White
    });
    header.Children.Add(new TextBlock
    {
      Text = "Modding toolkit",
      Opacity = 0.6,
      Foreground = Brushes.White
    });
    stack.Children.Add(header);

    // Navigation buttons
    var navButtons = new StackPanel { Spacing = 16 };
    navButtons.Children.Add(CreateNavButton("Asset Browser", () => Navigate(_viewModel.AssetBrowser)));
    navButtons.Children.Add(CreateNavButton("Stats Editor", () => Navigate(_viewModel.StatsEditor)));
    navButtons.Children.Add(CreateNavButton("Settings", () => Navigate(_viewModel.Settings)));
    stack.Children.Add(navButtons);

    // Roadmap
    var roadmap = new StackPanel { Spacing = 4 };
    roadmap.Children.Add(new TextBlock
    {
      Text = "Roadmap",
      FontWeight = FontWeight.SemiBold,
      Opacity = 0.7,
      Foreground = Brushes.White
    });
    roadmap.Children.Add(new TextBlock
    {
      Text = "Code modding & launcher exploration coming soon.",
      Opacity = 0.5,
      FontSize = 12,
      Foreground = Brushes.White,
      TextWrapping = TextWrapping.Wrap
    });
    stack.Children.Add(roadmap);

    border.Child = stack;
    return border;
  }

  private Button CreateNavButton(string text, System.Action onClick)
  {
    var button = new Button
    {
      Content = text,
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(16, 12),
      CornerRadius = new CornerRadius(8),
      HorizontalAlignment = HorizontalAlignment.Stretch,
      HorizontalContentAlignment = HorizontalAlignment.Left
    };
    button.Click += (_, _) => onClick();
    return button;
  }

  private Control BuildContentArea()
  {
    var grid = new Grid
    {
      Margin = new Thickness(32),
      RowDefinitions = new RowDefinitions("Auto,*")
    };

    // Title
    var titlePanel = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      VerticalAlignment = VerticalAlignment.Center,
      Margin = new Thickness(0, 0, 0, 24)
    };
    var titleText = new TextBlock
    {
      FontSize = 24,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White
    };
    titleText.Bind(TextBlock.TextProperty, _viewModel.WhenAnyValue(x => x.CurrentSectionTitle));
    titlePanel.Children.Add(titleText);
    grid.Children.Add(titlePanel);
    Grid.SetRow(titlePanel, 0);

    // Content area
    var contentBorder = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1C1C1C")),
      CornerRadius = new CornerRadius(16),
      Padding = new Thickness(32)
    };

    _contentArea = new ContentControl();
    _contentArea.Bind(ContentControl.ContentProperty, _viewModel.WhenAnyValue(x => x.SelectedViewModel));
    contentBorder.Child = _contentArea;

    grid.Children.Add(contentBorder);
    Grid.SetRow(contentBorder, 1);

    return grid;
  }

  private void Navigate(ViewModelBase viewModel)
  {
    if (viewModel == _viewModel.AssetBrowser)
      _viewModel.ShowAssetBrowser.Execute().Subscribe();
    else if (viewModel == _viewModel.StatsEditor)
      _viewModel.ShowStatsEditor.Execute().Subscribe();
    else if (viewModel == _viewModel.Settings)
      _viewModel.ShowSettings.Execute().Subscribe();
  }
}
