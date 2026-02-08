using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Menace.Modkit.App.Views;

/// <summary>
/// Settings view for modders creating mods.
/// </summary>
public class ToolSettingsView : UserControl
{
    public ToolSettingsView()
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
            Text = "Tool Settings",
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Configure settings for modding tools - extraction, assets, and caching.",
            Opacity = 0.7,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Component Versions
        stack.Children.Add(BuildVersionsSection());

        // Extracted Assets Directory
        stack.Children.Add(BuildAssetsSection());

        // Extraction Settings
        stack.Children.Add(BuildExtractionSettingsSection());

        // Cache Management
        stack.Children.Add(BuildCacheSection());

        scrollViewer.Content = stack;
        return scrollViewer;
    }

    private Control BuildVersionsSection()
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
            Text = "Component Versions",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Bundled dependency versions tracked by the modkit.",
            Opacity = 0.7,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });

        var versionsText = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        };
        versionsText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("DependencyVersionsText"));
        stack.Children.Add(versionsText);

        border.Child = stack;
        return border;
    }

    private Control BuildAssetsSection()
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
            Text = "Extracted Assets Directory",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Point this to your AssetRipper output directory. Allows you to extract once and reuse across app versions. Leave blank to auto-detect.",
            Opacity = 0.7,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });

        var pathStack = new StackPanel { Spacing = 8 };
        pathStack.Children.Add(new TextBlock
        {
            Text = "Assets Path",
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            FontSize = 13
        });

        var pathBox = new TextBox
        {
            Watermark = "(auto-detect from game install or out2/assets)",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 8)
        };
        pathBox.Bind(TextBox.TextProperty,
            new Avalonia.Data.Binding("ExtractedAssetsPath") { Mode = Avalonia.Data.BindingMode.TwoWay });
        pathStack.Children.Add(pathBox);

        // Assets status message
        var statusText = new TextBlock
        {
            Opacity = 0.7,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
        };
        statusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("AssetsPathStatus"));
        pathStack.Children.Add(statusText);

        stack.Children.Add(pathStack);
        border.Child = stack;
        return border;
    }

    private Control BuildExtractionSettingsSection()
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
            Text = "Extraction Settings",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });

        // Performance & Caching checkboxes
        var perfStack = new StackPanel { Spacing = 8 };
        perfStack.Children.Add(new TextBlock
        {
            Text = "Performance & Caching",
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            FontSize = 13
        });

        var autoUpdateCheck = new CheckBox
        {
            Content = "Auto-update on game version change",
            Foreground = Brushes.White
        };
        autoUpdateCheck.Bind(CheckBox.IsCheckedProperty,
            new Avalonia.Data.Binding("AutoUpdateOnGameChange") { Mode = Avalonia.Data.BindingMode.TwoWay });
        perfStack.Children.Add(autoUpdateCheck);

        var cachingCheck = new CheckBox
        {
            Content = "Enable caching (huge speed improvement)",
            Foreground = Brushes.White
        };
        cachingCheck.Bind(CheckBox.IsCheckedProperty,
            new Avalonia.Data.Binding("EnableCaching") { Mode = Avalonia.Data.BindingMode.TwoWay });
        perfStack.Children.Add(cachingCheck);

        var fullDumpCheck = new CheckBox
        {
            Content = "Keep full IL2CPP dump (35MB) for reference",
            Foreground = Brushes.White
        };
        fullDumpCheck.Bind(CheckBox.IsCheckedProperty,
            new Avalonia.Data.Binding("KeepFullIL2CppDump") { Mode = Avalonia.Data.BindingMode.TwoWay });
        perfStack.Children.Add(fullDumpCheck);

        var progressCheck = new CheckBox
        {
            Content = "Show extraction progress notifications",
            Foreground = Brushes.White
        };
        progressCheck.Bind(CheckBox.IsCheckedProperty,
            new Avalonia.Data.Binding("ShowExtractionProgress") { Mode = Avalonia.Data.BindingMode.TwoWay });
        perfStack.Children.Add(progressCheck);

        stack.Children.Add(perfStack);

        // Asset Ripper Profile
        var profileStack = new StackPanel { Spacing = 8 };
        profileStack.Children.Add(new TextBlock
        {
            Text = "Asset Ripper Profile",
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            FontSize = 13,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var essentialRadio = new RadioButton
        {
            Content = "Essential - Sprites, Textures, Audio, Text only (~30s, ~100MB)",
            GroupName = "AssetProfile",
            Foreground = Brushes.White
        };
        essentialRadio.Bind(RadioButton.IsCheckedProperty,
            new Avalonia.Data.Binding("IsEssentialProfile") { Mode = Avalonia.Data.BindingMode.TwoWay });
        profileStack.Children.Add(essentialRadio);

        var standardRadio = new RadioButton
        {
            Content = "Standard - Essential + Meshes, Shaders, VFX, Prefabs (~1-2min, ~250MB) [Recommended]",
            GroupName = "AssetProfile",
            Foreground = Brushes.White
        };
        standardRadio.Bind(RadioButton.IsCheckedProperty,
            new Avalonia.Data.Binding("IsStandardProfile") { Mode = Avalonia.Data.BindingMode.TwoWay });
        profileStack.Children.Add(standardRadio);

        var completeRadio = new RadioButton
        {
            Content = "Complete - Everything including Unity internals (~5-10min, ~1-2GB)",
            GroupName = "AssetProfile",
            Foreground = Brushes.White
        };
        completeRadio.Bind(RadioButton.IsCheckedProperty,
            new Avalonia.Data.Binding("IsCompleteProfile") { Mode = Avalonia.Data.BindingMode.TwoWay });
        profileStack.Children.Add(completeRadio);

        var customRadio = new RadioButton
        {
            Content = "Custom - User-defined filter settings",
            GroupName = "AssetProfile",
            Foreground = Brushes.White
        };
        customRadio.Bind(RadioButton.IsCheckedProperty,
            new Avalonia.Data.Binding("IsCustomProfile") { Mode = Avalonia.Data.BindingMode.TwoWay });
        profileStack.Children.Add(customRadio);

        stack.Children.Add(profileStack);

        // Validation
        var validationStack = new StackPanel { Spacing = 8 };
        validationStack.Children.Add(new TextBlock
        {
            Text = "Data Validation",
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            FontSize = 13,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var validationStatusText = new TextBlock
        {
            Foreground = Brushes.White,
            Opacity = 0.7,
            FontSize = 12
        };
        validationStatusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("ValidationStatus"));
        validationStack.Children.Add(validationStatusText);

        var validateButton = new Button
        {
            Content = "Validate Extraction",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            Padding = new Thickness(12, 6),
            Margin = new Thickness(0, 4, 0, 0)
        };
        validateButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("ValidateExtractionCommand"));
        validationStack.Children.Add(validateButton);

        stack.Children.Add(validationStack);

        border.Child = stack;
        return border;
    }

    private Control BuildCacheSection()
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
            Text = "Cache Management",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });

        var cacheStatusText = new TextBlock
        {
            Foreground = Brushes.White,
            Opacity = 0.7,
            FontSize = 12
        };
        cacheStatusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("CacheStatus"));
        stack.Children.Add(cacheStatusText);

        var buttonStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var clearCacheButton = new Button
        {
            Content = "Clear Cache",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            Padding = new Thickness(16, 8)
        };
        clearCacheButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("ClearCacheCommand"));
        buttonStack.Children.Add(clearCacheButton);

        var forceExtractDataButton = new Button
        {
            Content = "Force Extract Data",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            Padding = new Thickness(16, 8)
        };
        forceExtractDataButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("ForceExtractDataCommand"));
        buttonStack.Children.Add(forceExtractDataButton);

        var forceExtractAssetsButton = new Button
        {
            Content = "Force Extract Assets",
            Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            Padding = new Thickness(16, 8)
        };
        forceExtractAssetsButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("ForceExtractAssetsCommand"));
        buttonStack.Children.Add(forceExtractAssetsButton);

        stack.Children.Add(buttonStack);

        // Extraction status text
        var extractionStatusText = new TextBlock
        {
            Foreground = Brushes.White,
            Opacity = 0.7,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        extractionStatusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("ExtractionStatus"));
        stack.Children.Add(extractionStatusText);

        border.Child = stack;
        return border;
    }
}
