using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Menace.Modkit.App.Services;
using Menace.Modkit.App.ViewModels;

namespace Menace.Modkit.App.Views;

/// <summary>
/// Setup/update screen that shows component status and handles downloads.
/// </summary>
public class SetupView : UserControl
{
    public SetupView()
    {
        Content = BuildUI();
    }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext is SetupViewModel vm)
        {
            await vm.LoadComponentsAsync();
        }
    }

    private Control BuildUI()
    {
        var mainBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#0D0D0D")),
            Padding = new Thickness(48)
        };

        var mainStack = new StackPanel
        {
            MaxWidth = 700,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 24
        };

        // Header
        mainStack.Children.Add(BuildHeader());

        // Loading indicator
        var loadingPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 12
        };
        loadingPanel.Children.Add(new ProgressBar
        {
            IsIndeterminate = true,
            Width = 200,
            Height = 4
        });
        loadingPanel.Children.Add(new TextBlock
        {
            Text = "Checking for updates...",
            Foreground = Brushes.White,
            Opacity = 0.7,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        loadingPanel.Bind(StackPanel.IsVisibleProperty, new Avalonia.Data.Binding("IsLoading"));
        mainStack.Children.Add(loadingPanel);

        // Content (hidden while loading)
        var contentPanel = new StackPanel { Spacing = 24 };
        contentPanel.Bind(StackPanel.IsVisibleProperty, new Avalonia.Data.Binding("!IsLoading"));

        // Required Components Section
        contentPanel.Children.Add(BuildRequiredSection());

        // Optional Components Section
        contentPanel.Children.Add(BuildOptionalSection());

        // Download Progress Section
        contentPanel.Children.Add(BuildProgressSection());

        // Action Buttons
        contentPanel.Children.Add(BuildActionButtons());

        mainStack.Children.Add(contentPanel);
        mainBorder.Child = mainStack;

        return new ScrollViewer
        {
            Content = mainBorder,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private Control BuildHeader()
    {
        var stack = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 16)
        };

        stack.Children.Add(new TextBlock
        {
            Text = "Menace Modkit Setup",
            FontSize = 28,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White
        });

        stack.Children.Add(new TextBlock
        {
            Text = "The following components are needed to run the modkit.",
            FontSize = 14,
            Foreground = Brushes.White,
            Opacity = 0.7
        });

        return stack;
    }

    private Control BuildRequiredSection()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#141414")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20)
        };

        var stack = new StackPanel { Spacing = 12 };

        // Section header
        var headerStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        headerStack.Children.Add(new TextBlock
        {
            Text = "REQUIRED COMPONENTS",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")),
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(headerStack);

        // Component list
        var itemsControl = new ItemsControl();
        itemsControl.Bind(ItemsControl.ItemsSourceProperty, new Avalonia.Data.Binding("RequiredComponents"));
        itemsControl.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ComponentStatusViewModel>(
            (component, _) => BuildComponentRow(component, isRequired: true), true);
        stack.Children.Add(itemsControl);

        border.Child = stack;
        return border;
    }

    private Control BuildOptionalSection()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#141414")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20)
        };

        var stack = new StackPanel { Spacing = 12 };

        // Section header
        stack.Children.Add(new TextBlock
        {
            Text = "OPTIONAL ADD-ONS",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#888888"))
        });

        // Component list
        var itemsControl = new ItemsControl();
        itemsControl.Bind(ItemsControl.ItemsSourceProperty, new Avalonia.Data.Binding("OptionalComponents"));
        itemsControl.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ComponentStatusViewModel>(
            (component, _) => BuildComponentRow(component, isRequired: false), true);
        stack.Children.Add(itemsControl);

        // Hide if no optional components
        border.Bind(Border.IsVisibleProperty, new Avalonia.Data.Binding("OptionalComponents.Count")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<int, bool>(c => c > 0)
        });

        border.Child = stack;
        return border;
    }

    private Control BuildComponentRow(ComponentStatusViewModel component, bool isRequired)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            Margin = new Thickness(0, 6)
        };

        // Column 0: Status indicator or checkbox
        if (isRequired)
        {
            var statusIcon = new TextBlock
            {
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };

            // Bind icon and color based on state
            statusIcon.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("State")
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<ComponentState, string>(state => state switch
                {
                    ComponentState.UpToDate => "\u2713",      // Checkmark
                    ComponentState.Outdated => "\u2191",      // Up arrow
                    ComponentState.NotInstalled => "\u2717",  // X
                    _ => "?"
                })
            });
            statusIcon.Bind(TextBlock.ForegroundProperty, new Avalonia.Data.Binding("State")
            {
                Converter = new Avalonia.Data.Converters.FuncValueConverter<ComponentState, IBrush>(state => state switch
                {
                    ComponentState.UpToDate => new SolidColorBrush(Color.Parse("#8ECDC8")),
                    ComponentState.Outdated => new SolidColorBrush(Color.Parse("#FFD700")),
                    ComponentState.NotInstalled => new SolidColorBrush(Color.Parse("#FF6B6B")),
                    _ => Brushes.White
                })
            });

            grid.Children.Add(statusIcon);
            Grid.SetColumn(statusIcon, 0);
        }
        else
        {
            var checkbox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            checkbox.Bind(CheckBox.IsCheckedProperty, new Avalonia.Data.Binding("IsSelected")
            {
                Mode = Avalonia.Data.BindingMode.TwoWay
            });
            // Disable if already installed
            checkbox.Bind(CheckBox.IsEnabledProperty, new Avalonia.Data.Binding("NeedsAction"));

            grid.Children.Add(checkbox);
            Grid.SetColumn(checkbox, 0);
        }

        // Column 1: Name and description
        var infoStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        var nameStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        nameStack.Children.Add(new TextBlock
        {
            Text = component.Name,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });
        nameStack.Children.Add(new TextBlock
        {
            Text = $"v{component.LatestVersion}",
            FontSize = 12,
            Foreground = Brushes.White,
            Opacity = 0.5,
            VerticalAlignment = VerticalAlignment.Center
        });
        infoStack.Children.Add(nameStack);

        infoStack.Children.Add(new TextBlock
        {
            Text = component.Description,
            FontSize = 12,
            Foreground = Brushes.White,
            Opacity = 0.6
        });

        grid.Children.Add(infoStack);
        Grid.SetColumn(infoStack, 1);

        // Column 2: Size
        var sizeText = new TextBlock
        {
            Text = component.DownloadSize,
            FontSize = 12,
            Foreground = Brushes.White,
            Opacity = 0.5,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0)
        };
        // Hide size if already installed
        sizeText.Bind(TextBlock.IsVisibleProperty, new Avalonia.Data.Binding("NeedsAction"));
        grid.Children.Add(sizeText);
        Grid.SetColumn(sizeText, 2);

        // Column 3: Status badge
        var statusBadge = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4),
            VerticalAlignment = VerticalAlignment.Center
        };

        var statusText = new TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeight.SemiBold
        };

        // Bind badge appearance based on state
        statusBadge.Bind(Border.BackgroundProperty, new Avalonia.Data.Binding("State")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<ComponentState, IBrush>(state => state switch
            {
                ComponentState.UpToDate => new SolidColorBrush(Color.Parse("#1a3a2a")),
                ComponentState.Outdated => new SolidColorBrush(Color.Parse("#3a3a1a")),
                ComponentState.NotInstalled => new SolidColorBrush(Color.Parse("#3a1a1a")),
                _ => Brushes.Transparent
            })
        });

        statusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("State")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<ComponentState, string>(state => state switch
            {
                ComponentState.UpToDate => "Installed",
                ComponentState.Outdated => "Update",
                ComponentState.NotInstalled => "Download",
                _ => ""
            })
        });

        statusText.Bind(TextBlock.ForegroundProperty, new Avalonia.Data.Binding("State")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<ComponentState, IBrush>(state => state switch
            {
                ComponentState.UpToDate => new SolidColorBrush(Color.Parse("#8ECDC8")),
                ComponentState.Outdated => new SolidColorBrush(Color.Parse("#FFD700")),
                ComponentState.NotInstalled => new SolidColorBrush(Color.Parse("#FF6B6B")),
                _ => Brushes.White
            })
        });

        statusBadge.Child = statusText;
        grid.Children.Add(statusBadge);
        Grid.SetColumn(statusBadge, 3);

        return grid;
    }

    private Control BuildProgressSection()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1a2a3a")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 8, 0, 0)
        };
        border.Bind(Border.IsVisibleProperty, new Avalonia.Data.Binding("IsDownloading"));

        var stack = new StackPanel { Spacing = 12 };

        // Current component
        var componentText = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        };
        componentText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("CurrentComponent")
        {
            StringFormat = "Downloading {0}..."
        });
        stack.Children.Add(componentText);

        // Progress bar
        var progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 8
        };
        progressBar.Bind(ProgressBar.ValueProperty, new Avalonia.Data.Binding("OverallProgress"));
        stack.Children.Add(progressBar);

        // Status text
        var statusStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16
        };

        var statusText = new TextBlock
        {
            FontSize = 12,
            Foreground = Brushes.White,
            Opacity = 0.7
        };
        statusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("DownloadStatus"));
        statusStack.Children.Add(statusText);

        var speedText = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#8ECDC8"))
        };
        speedText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("DownloadSpeed"));
        statusStack.Children.Add(speedText);

        stack.Children.Add(statusStack);

        border.Child = stack;
        return border;
    }

    private Control BuildActionButtons()
    {
        var stack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 16,
            Margin = new Thickness(0, 16, 0, 0)
        };

        // Download button
        var downloadButton = new Button
        {
            FontSize = 14,
            Padding = new Thickness(24, 12),
            MinWidth = 200
        };
        downloadButton.Classes.Add("primary");

        // Bind content based on state
        var downloadPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var downloadIcon = new TextBlock
        {
            Text = "\u2193", // Down arrow
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center
        };
        downloadPanel.Children.Add(downloadIcon);

        var downloadText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        downloadText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("TotalDownloadSize")
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<string, string>(size =>
                string.IsNullOrEmpty(size) ? "Continue" : $"Download ({size})")
        });
        downloadPanel.Children.Add(downloadText);

        downloadButton.Content = downloadPanel;
        downloadButton.Bind(Button.IsEnabledProperty, new Avalonia.Data.Binding("CanDownload"));
        downloadButton.Click += async (_, _) =>
        {
            if (DataContext is SetupViewModel vm)
            {
                if (vm.HasPendingDownloads)
                    await vm.DownloadAsync();
                else
                    vm.Continue();
            }
        };
        stack.Children.Add(downloadButton);

        // Cancel button (shown during download)
        var cancelButton = new Button
        {
            Content = "Cancel",
            FontSize = 14,
            Padding = new Thickness(24, 12)
        };
        cancelButton.Classes.Add("secondary");
        cancelButton.Bind(Button.IsVisibleProperty, new Avalonia.Data.Binding("IsDownloading"));
        cancelButton.Click += (_, _) =>
        {
            if (DataContext is SetupViewModel vm)
                vm.CancelDownload();
        };
        stack.Children.Add(cancelButton);

        // Skip button (shown when not downloading and no required pending)
        var skipButton = new Button
        {
            Content = "Skip for Now",
            FontSize = 14,
            Padding = new Thickness(24, 12)
        };
        skipButton.Classes.Add("secondary");
        skipButton.Bind(Button.IsVisibleProperty, new Avalonia.Data.Binding("HasRequiredPending")
        {
            Converter = Avalonia.Data.Converters.BoolConverters.Not
        });
        skipButton.Bind(Button.IsEnabledProperty, new Avalonia.Data.Binding("CanSkip"));
        skipButton.Click += (_, _) =>
        {
            if (DataContext is SetupViewModel vm)
                vm.Skip();
        };
        stack.Children.Add(skipButton);

        return stack;
    }
}
