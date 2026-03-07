using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Menace;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;
using Menace.Modkit.App.ViewModels;
using ReactiveUI;

namespace Menace.Modkit.App.Views;

public class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private StackPanel? _subNavPanel;
    private Button? _modLoaderTab;
    private Button? _moddingToolsTab;
    private Border? _healthStatusBar;
    private TextBlock? _healthStatusText;
    private TextBlock? _healthActionText;

    public MainWindow(IServiceProvider serviceProvider)
    {
        _viewModel = new MainViewModel(serviceProvider);
        DataContext = _viewModel;

        Width = 1200;
        Height = 750;
        Title = ModkitVersion.AppFull;
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

        // Subscribe to health state changes to update status bar
        _viewModel.HealthState.HealthStatusChanged += OnHealthStatusChanged;
    }

    private void OnHealthStatusChanged(object? sender, InstallHealthStatus status)
    {
        UpdateHealthStatusBar(status);
    }

    private void UpdateHealthStatusBar(InstallHealthStatus status)
    {
        if (_healthStatusBar == null) return;

        // Show/hide based on health state
        _healthStatusBar.IsVisible = status.State != InstallHealthState.Healthy;

        if (!_healthStatusBar.IsVisible) return;

        // Update colors based on severity
        var bgColor = status.State switch
        {
            InstallHealthState.UpdatePendingRestart => Color.Parse("#1A3A5C"), // Blue for info
            InstallHealthState.NeedsSetup => Color.Parse("#4A3A1A"),           // Orange/amber for setup
            InstallHealthState.NeedsRepair => Color.Parse("#4A3A1A"),          // Orange/amber for repair
            InstallHealthState.RepairableFromBackup => Color.Parse("#4A3A1A"), // Orange/amber
            _ => Color.Parse("#4A1A1A")                                        // Red for errors
        };

        var textColor = status.State switch
        {
            InstallHealthState.UpdatePendingRestart => Color.Parse("#6BB3FF"),
            InstallHealthState.NeedsSetup => Color.Parse("#FFB74D"),
            InstallHealthState.NeedsRepair => Color.Parse("#FFB74D"),
            InstallHealthState.RepairableFromBackup => Color.Parse("#FFB74D"),
            _ => Color.Parse("#FF6B6B")
        };

        _healthStatusBar.Background = new SolidColorBrush(bgColor);

        if (_healthStatusText != null)
        {
            _healthStatusText.Text = status.ShortSummary;
            _healthStatusText.Foreground = new SolidColorBrush(textColor);
        }

        if (_healthActionText != null)
        {
            _healthActionText.Text = !string.IsNullOrEmpty(status.RequiredUserAction)
                ? status.RequiredUserAction
                : status.BlockingReason;
            _healthActionText.Foreground = new SolidColorBrush(Color.Parse("#AAAAAA"));
        }
    }

    private Control BuildUI()
    {
        var mainGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*")
        };

        // Top menu bar with logo and main tabs
        mainGrid.Children.Add(BuildMenuBar());
        Grid.SetRow((Control)mainGrid.Children[0], 0);

        // Sub-navigation bar (changes based on section)
        var subNavBar = BuildSubNavBar();
        mainGrid.Children.Add(subNavBar);
        Grid.SetRow(subNavBar, 1);

        // Health status bar (hidden when healthy)
        var healthBar = BuildHealthStatusBar();
        mainGrid.Children.Add(healthBar);
        Grid.SetRow(healthBar, 2);

        // Content area
        var contentArea = BuildContentArea();
        mainGrid.Children.Add(contentArea);
        Grid.SetRow(contentArea, 3);

        return mainGrid;
    }

    private Control BuildHealthStatusBar()
    {
        _healthStatusBar = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#4A3A1A")),
            BorderBrush = new SolidColorBrush(Color.Parse("#6A5A2A")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 8),
            IsVisible = false // Hidden by default, shown when health state != Healthy
        };

        var contentStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Warning icon
        var iconText = new TextBlock
        {
            Text = "\u26A0", // Warning triangle
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse("#FFB74D")),
            VerticalAlignment = VerticalAlignment.Center
        };
        contentStack.Children.Add(iconText);

        // Status text
        _healthStatusText = new TextBlock
        {
            Text = "",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#FFB74D")),
            VerticalAlignment = VerticalAlignment.Center
        };
        contentStack.Children.Add(_healthStatusText);

        // Separator
        contentStack.Children.Add(new Border
        {
            Width = 1,
            Background = new SolidColorBrush(Color.Parse("#5A4A2A")),
            Margin = new Thickness(4, 2)
        });

        // Action/reason text
        _healthActionText = new TextBlock
        {
            Text = "",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 800
        };
        contentStack.Children.Add(_healthActionText);

        _healthStatusBar.Child = contentStack;

        // Initialize with current state
        var currentStatus = _viewModel.HealthState.CurrentStatus;
        UpdateHealthStatusBar(currentStatus);

        return _healthStatusBar;
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
            BorderThickness = new Thickness(2, 0, 0, 0),
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(16, 10),
            Margin = new Thickness(2),
            FontSize = 14,
            FontWeight = FontWeight.Medium,
            CornerRadius = new CornerRadius(4)
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
            Padding = new Thickness(16, 8),  // Add vertical padding for breathing room
            MinHeight = 44  // Increased to accommodate padding
        };

        _subNavPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,  // Increased spacing between nav items
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
        // Active state: grey background with teal left border
        var activeBg = new SolidColorBrush(Color.Parse("#2A2A2A"));
        var activeBorder = new SolidColorBrush(Color.Parse("#004f43"));
        var inactiveBg = Brushes.Transparent;
        var inactiveBorder = Brushes.Transparent;

        if (_modLoaderTab != null)
        {
            _modLoaderTab.Background = _viewModel.IsModLoader ? activeBg : inactiveBg;
            _modLoaderTab.BorderBrush = _viewModel.IsModLoader ? activeBorder : inactiveBorder;
        }

        if (_moddingToolsTab != null)
        {
            _moddingToolsTab.Background = _viewModel.IsModdingTools ? activeBg : inactiveBg;
            _moddingToolsTab.BorderBrush = _viewModel.IsModdingTools ? activeBorder : inactiveBorder;
        }
    }

    private Button CreateSubTab(string text, string subSection, Action onClick)
    {
        var button = new Button
        {
            Content = text,
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(2, 0, 0, 0),
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(16, 10),
            Margin = new Thickness(2),
            FontSize = 12,
            Tag = subSection,
            CornerRadius = new CornerRadius(4)
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private void UpdateSubNavHighlight()
    {
        if (_subNavPanel == null) return;

        // Active state: grey background with teal left border
        var activeBg = new SolidColorBrush(Color.Parse("#2A2A2A"));
        var activeBorder = new SolidColorBrush(Color.Parse("#004f43"));
        var inactiveBg = Brushes.Transparent;
        var inactiveBorder = Brushes.Transparent;

        foreach (var child in _subNavPanel.Children)
        {
            if (child is Button btn)
            {
                var isActive = btn.Tag?.ToString() == _viewModel.CurrentSubSection;
                btn.Background = isActive ? activeBg : inactiveBg;
                btn.BorderBrush = isActive ? activeBorder : inactiveBorder;
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
