using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Menace.Modkit.App.Views;

/// <summary>
/// Settings view for end users running mods.
/// </summary>
public class LoaderSettingsView : UserControl
{
    public LoaderSettingsView()
    {
        Content = BuildUI();
    }

    private Control BuildUI()
    {
        var scrollViewer = new ScrollViewer();
        var stack = new StackPanel
        {
            Spacing = 24,
            Margin = new Thickness(24)
        };

        // Title
        stack.Children.Add(new TextBlock
        {
            Text = "Loader Settings",
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Configure settings for running mods with Menace.",
            Opacity = 0.7,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Game Installation Settings
        stack.Children.Add(BuildGameInstallSection());

        // Deployment section
        stack.Children.Add(BuildDeploymentSection());

        // Logs section
        stack.Children.Add(BuildLogsSection());

        // Quick Actions section
        stack.Children.Add(BuildQuickActionsSection());

        // Uninstall section
        stack.Children.Add(BuildUninstallSection());

        scrollViewer.Content = stack;
        return scrollViewer;
    }

    private Control BuildGameInstallSection()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1F1F1F")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24)
        };

        var stack = new StackPanel { Spacing = 16 };

        stack.Children.Add(new TextBlock
        {
            Text = "Game Installation",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Set the path to your Menace game installation. This is required to load mods.",
            Opacity = 0.7,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });

        // Install path field
        var pathStack = new StackPanel { Spacing = 8 };
        pathStack.Children.Add(new TextBlock
        {
            Text = "Install Path",
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            FontSize = 13
        });

        var pathBox = new TextBox
        {
            Watermark = "~/.steam/debian-installation/steamapps/common/Menace",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 8)
        };
        pathBox.Bind(TextBox.TextProperty,
            new Avalonia.Data.Binding("GameInstallPath") { Mode = Avalonia.Data.BindingMode.TwoWay });
        pathStack.Children.Add(pathBox);

        // Status message
        var statusText = new TextBlock
        {
            Opacity = 0.7,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        };
        statusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("InstallPathStatus"));
        pathStack.Children.Add(statusText);

        stack.Children.Add(pathStack);
        border.Child = stack;
        return border;
    }

    private Control BuildDeploymentSection()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1F1F1F")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24)
        };

        var stack = new StackPanel { Spacing = 16 };

        stack.Children.Add(new TextBlock
        {
            Text = "Deployment",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Wipe the game's Mods folder and install fresh runtime dependencies (MelonLoader, DataExtractor, ModpackLoader). Use this if mods aren't loading correctly.",
            Opacity = 0.7,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });

        var cleanRedeployButton = new Button
        {
            Content = "Clean Redeploy",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            Padding = new Thickness(16, 8)
        };
        cleanRedeployButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("CleanRedeployCommand"));
        stack.Children.Add(cleanRedeployButton);

        var cleanRedeployStatusText = new TextBlock
        {
            Opacity = 0.7,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        };
        cleanRedeployStatusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("CleanRedeployStatus"));
        stack.Children.Add(cleanRedeployStatusText);

        border.Child = stack;
        return border;
    }

    private Control BuildLogsSection()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1F1F1F")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24)
        };

        var stack = new StackPanel { Spacing = 16 };

        stack.Children.Add(new TextBlock
        {
            Text = "Logs",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Quick access to log files for troubleshooting. Share these when reporting issues.",
            Opacity = 0.7,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });

        // Modkit Log
        var modkitLogStack = new StackPanel { Spacing = 4 };
        modkitLogStack.Children.Add(new TextBlock
        {
            Text = "Modkit Log",
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            FontSize = 13
        });

        var modkitLogPathText = new TextBlock
        {
            Opacity = 0.6,
            Foreground = Brushes.White,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        };
        modkitLogPathText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("ModkitLogPath"));
        modkitLogStack.Children.Add(modkitLogPathText);

        var modkitLogButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 4, 0, 0)
        };

        var openModkitLogButton = new Button
        {
            Content = "Open Log",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            Padding = new Thickness(12, 6)
        };
        openModkitLogButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("OpenModkitLogCommand"));
        modkitLogButtons.Children.Add(openModkitLogButton);

        var openModkitFolderButton = new Button
        {
            Content = "Open Folder",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            Padding = new Thickness(12, 6)
        };
        openModkitFolderButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("OpenModkitLogFolderCommand"));
        modkitLogButtons.Children.Add(openModkitFolderButton);

        modkitLogStack.Children.Add(modkitLogButtons);
        stack.Children.Add(modkitLogStack);

        // MelonLoader Log
        var mlLogStack = new StackPanel { Spacing = 4, Margin = new Thickness(0, 12, 0, 0) };
        mlLogStack.Children.Add(new TextBlock
        {
            Text = "MelonLoader Log",
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            FontSize = 13
        });

        var mlLogPathText = new TextBlock
        {
            Opacity = 0.6,
            Foreground = Brushes.White,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        };
        mlLogPathText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("MelonLoaderLogPath"));
        mlLogStack.Children.Add(mlLogPathText);

        var mlLogButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 4, 0, 0)
        };

        var openMlLogButton = new Button
        {
            Content = "Open Log",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            Padding = new Thickness(12, 6)
        };
        openMlLogButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("OpenMelonLoaderLogCommand"));
        mlLogButtons.Children.Add(openMlLogButton);

        var openMlFolderButton = new Button
        {
            Content = "Open Folder",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            Padding = new Thickness(12, 6)
        };
        openMlFolderButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("OpenMelonLoaderLogFolderCommand"));
        mlLogButtons.Children.Add(openMlFolderButton);

        mlLogStack.Children.Add(mlLogButtons);
        stack.Children.Add(mlLogStack);

        border.Child = stack;
        return border;
    }

    private Control BuildQuickActionsSection()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1F1F1F")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24)
        };

        var stack = new StackPanel { Spacing = 16 };

        stack.Children.Add(new TextBlock
        {
            Text = "Quick Actions",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });

        var buttonStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        var openSavesFolderButton = new Button
        {
            Content = "Open Saves Folder",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            Padding = new Thickness(12, 6)
        };
        openSavesFolderButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("OpenSavesFolderCommand"));
        buttonStack.Children.Add(openSavesFolderButton);

        stack.Children.Add(buttonStack);

        border.Child = stack;
        return border;
    }

    private Control BuildUninstallSection()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1F1F1F")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24)
        };

        var stack = new StackPanel { Spacing = 16 };

        stack.Children.Add(new TextBlock
        {
            Text = "Uninstall",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Remove the mod loader from the game. This deletes MelonLoader, deployed mods, and runtime files. Your saves in UserData/Saves are preserved.",
            Opacity = 0.7,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });

        var uninstallButton = new Button
        {
            Content = "Uninstall from Game",
            Background = new SolidColorBrush(Color.Parse("#4A1515")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#6A2020")),
            Padding = new Thickness(16, 8)
        };
        uninstallButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("UninstallFromGameCommand"));
        stack.Children.Add(uninstallButton);

        var uninstallStatusText = new TextBlock
        {
            Opacity = 0.7,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        };
        uninstallStatusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("UninstallStatus"));
        stack.Children.Add(uninstallStatusText);

        // Additional info about full uninstall
        stack.Children.Add(new TextBlock
        {
            Text = "To fully uninstall the Modkit app itself, delete:\n" +
                   "  - This app folder\n" +
                   "  - ~/.menace-modkit/ (component cache)\n" +
                   "  - ~/.config/MenaceModkit/ (settings)",
            Opacity = 0.5,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Margin = new Thickness(0, 8, 0, 0)
        });

        border.Child = stack;
        return border;
    }
}
