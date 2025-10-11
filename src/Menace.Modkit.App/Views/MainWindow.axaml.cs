using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Menace.Modkit.App.ViewModels;
using ReactiveUI;

namespace Menace.Modkit.App.Views;

public class MainWindow : Window
{
  private readonly MainViewModel _viewModel;

  public MainWindow(IServiceProvider serviceProvider)
  {
    _viewModel = new MainViewModel(serviceProvider);
    DataContext = _viewModel;

    Width = 1100;
    Height = 700;
    Title = "Menace Modkit";
    Background = new SolidColorBrush(Color.Parse("#121212"));

    // Set app icon
    try
    {
      var iconUri = new Uri("avares://Menace.Modkit.App/Assets/icon.jpg");
      Icon = new WindowIcon(AssetLoader.Open(iconUri));
    }
    catch
    {
      // Icon loading failed, continue without it
    }

    Content = BuildUI();
  }

  private Control BuildUI()
  {
    var mainGrid = new Grid
    {
      RowDefinitions = new RowDefinitions("Auto,*")
    };

    // Top menu bar
    mainGrid.Children.Add(BuildMenuBar());
    Grid.SetRow((Control)mainGrid.Children[0], 0);

    // Content area - each view handles its own layout
    var contentArea = BuildContentArea();
    mainGrid.Children.Add(contentArea);
    Grid.SetRow(contentArea, 1);

    return mainGrid;
  }

  private Control BuildMenuBar()
  {
    var border = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
      BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
      BorderThickness = new Thickness(0, 0, 0, 1),
      Padding = new Thickness(16, 8)
    };

    var stack = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 16
    };

    // Logo/Title
    var title = new TextBlock
    {
      Text = "Menace Modkit",
      FontSize = 16,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White,
      VerticalAlignment = VerticalAlignment.Center,
      Margin = new Thickness(0, 0, 32, 0)
    };
    stack.Children.Add(title);

    // Mode tabs
    stack.Children.Add(CreateTabButton("Modpacks", () => Navigate(_viewModel.Modpacks)));
    stack.Children.Add(CreateTabButton("Stats", () => Navigate(_viewModel.StatsEditor)));
    stack.Children.Add(CreateTabButton("Assets", () => Navigate(_viewModel.AssetBrowser)));
    stack.Children.Add(CreateTabButton("Settings", () => Navigate(_viewModel.Settings)));

    border.Child = stack;
    return border;
  }

  private Button CreateTabButton(string text, System.Action onClick)
  {
    var button = new Button
    {
      Content = text,
      Background = Brushes.Transparent,
      Foreground = Brushes.White,
      BorderThickness = new Thickness(0),
      Padding = new Thickness(16, 6),
      FontSize = 14
    };
    button.Click += (_, _) => onClick();
    return button;
  }

  private Control BuildContentArea()
  {
    var contentView = new ContentControl
    {
      Background = new SolidColorBrush(Color.Parse("#121212"))
    };
    contentView.Bind(ContentControl.ContentProperty, _viewModel.WhenAnyValue(x => x.SelectedViewModel));

    return contentView;
  }

  private void Navigate(ViewModelBase viewModel)
  {
    if (viewModel == _viewModel.Modpacks)
      _viewModel.ShowModpacks.Execute().Subscribe();
    else if (viewModel == _viewModel.AssetBrowser)
      _viewModel.ShowAssetBrowser.Execute().Subscribe();
    else if (viewModel == _viewModel.StatsEditor)
      _viewModel.ShowStatsEditor.Execute().Subscribe();
    else if (viewModel == _viewModel.Settings)
      _viewModel.ShowSettings.Execute().Subscribe();
  }
}
