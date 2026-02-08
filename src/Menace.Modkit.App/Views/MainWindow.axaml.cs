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
    private StackPanel? _subNavPanel;
    private Button? _modLoaderTab;
    private Button? _moddingToolsTab;

    public MainWindow(IServiceProvider serviceProvider)
    {
        _viewModel = new MainViewModel(serviceProvider);
        DataContext = _viewModel;

        Width = 1200;
        Height = 750;
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

        // Subscribe to section changes to update sub-nav
        _viewModel.WhenAnyValue(x => x.CurrentSection)
            .Subscribe(_ => UpdateSubNav());

        _viewModel.WhenAnyValue(x => x.CurrentSubSection)
            .Subscribe(_ => UpdateSubNavHighlight());
    }

    private Control BuildUI()
    {
        var mainGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*")
        };

        // Top menu bar with logo and main tabs
        mainGrid.Children.Add(BuildMenuBar());
        Grid.SetRow((Control)mainGrid.Children[0], 0);

        // Sub-navigation bar (changes based on section)
        var subNavBar = BuildSubNavBar();
        mainGrid.Children.Add(subNavBar);
        Grid.SetRow(subNavBar, 1);

        // Content area
        var contentArea = BuildContentArea();
        mainGrid.Children.Add(contentArea);
        Grid.SetRow(contentArea, 2);

        return mainGrid;
    }

    private Control BuildMenuBar()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 6)
        };

        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        // Logo/Title - clickable to go home
        var logoButton = new Button
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        };

        var logoStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10
        };

        // Load icon image for logo
        try
        {
            var iconUri = new Uri("avares://Menace.Modkit.App/Assets/icon.jpg");
            var bitmap = new Bitmap(AssetLoader.Open(iconUri));
            var iconImage = new Image
            {
                Source = bitmap,
                Width = 28,
                Height = 28,
                VerticalAlignment = VerticalAlignment.Center
            };
            logoStack.Children.Add(iconImage);
        }
        catch
        {
            // Fallback text if icon not available
        }

        logoStack.Children.Add(new TextBlock
        {
            Text = "Menace Modkit",
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });

        logoButton.Content = logoStack;
        logoButton.Click += (_, _) => _viewModel.NavigateToHome();
        stack.Children.Add(logoButton);

        // Separator
        stack.Children.Add(new Border
        {
            Width = 1,
            Background = new SolidColorBrush(Color.Parse("#3E3E3E")),
            Margin = new Thickness(16, 4)
        });

        // Main section tabs
        _modLoaderTab = CreateMainTab("Mod Loader", () => _viewModel.NavigateToModLoader());
        stack.Children.Add(_modLoaderTab);

        _moddingToolsTab = CreateMainTab("Modding Tools", () => _viewModel.NavigateToModdingTools());
        stack.Children.Add(_moddingToolsTab);

        border.Child = stack;
        return border;
    }

    private Button CreateMainTab(string text, Action onClick)
    {
        var button = new Button
        {
            Content = text,
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(16, 8),
            FontSize = 14,
            FontWeight = FontWeight.Medium
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private Control BuildSubNavBar()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#161616")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2D2D2D")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 0),
            MinHeight = 36
        };

        _subNavPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };

        border.Child = _subNavPanel;
        return border;
    }

    private void UpdateSubNav()
    {
        if (_subNavPanel == null) return;

        _subNavPanel.Children.Clear();

        // Update main tab highlighting
        UpdateMainTabHighlight();

        switch (_viewModel.CurrentSection)
        {
            case NavigationSection.Home:
                // No sub-nav for home
                break;

            case NavigationSection.ModLoader:
                _subNavPanel.Children.Add(CreateSubTab("Load Order", "Load Order", _viewModel.NavigateToLoadOrder));
                _subNavPanel.Children.Add(CreateSubTab("Saves", "Saves", _viewModel.NavigateToSaves));
                _subNavPanel.Children.Add(CreateSubTab("Settings", "Settings", _viewModel.NavigateToLoaderSettings));
                break;

            case NavigationSection.ModdingTools:
                _subNavPanel.Children.Add(CreateSubTab("Data", "Data", _viewModel.NavigateToData));
                _subNavPanel.Children.Add(CreateSubTab("Assets", "Assets", _viewModel.NavigateToAssets));
                _subNavPanel.Children.Add(CreateSubTab("Code", "Code", _viewModel.NavigateToCode));
                _subNavPanel.Children.Add(CreateSubTab("Docs", "Docs", _viewModel.NavigateToDocs));
                _subNavPanel.Children.Add(CreateSubTab("Settings", "Settings", _viewModel.NavigateToToolSettings));
                break;
        }

        UpdateSubNavHighlight();
    }

    private void UpdateMainTabHighlight()
    {
        var activeColor = new SolidColorBrush(Color.Parse("#064b48"));
        var inactiveColor = Brushes.Transparent;

        if (_modLoaderTab != null)
        {
            _modLoaderTab.Background = _viewModel.IsModLoader ? activeColor : inactiveColor;
        }

        if (_moddingToolsTab != null)
        {
            _moddingToolsTab.Background = _viewModel.IsModdingTools ? activeColor : inactiveColor;
        }
    }

    private Button CreateSubTab(string text, string subSection, Action onClick)
    {
        var button = new Button
        {
            Content = text,
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 6),
            FontSize = 12,
            Tag = subSection
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private void UpdateSubNavHighlight()
    {
        if (_subNavPanel == null) return;

        var activeColor = new SolidColorBrush(Color.Parse("#2A2A2A"));
        var inactiveColor = Brushes.Transparent;

        foreach (var child in _subNavPanel.Children)
        {
            if (child is Button btn)
            {
                var isActive = btn.Tag?.ToString() == _viewModel.CurrentSubSection;
                btn.Background = isActive ? activeColor : inactiveColor;
                btn.FontWeight = isActive ? FontWeight.SemiBold : FontWeight.Normal;
            }
        }
    }

    private Control BuildContentArea()
    {
        var contentView = new ContentControl
        {
            Background = new SolidColorBrush(Color.Parse("#121212"))
        };

        // Bind to selected view model
        contentView.Bind(ContentControl.ContentProperty, _viewModel.WhenAnyValue(x => x.SelectedViewModel));

        return contentView;
    }
}
