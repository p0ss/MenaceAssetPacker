using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Menace.Modkit.App.Views;

public class SettingsView : UserControl
{
  public SettingsView()
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
      Text = "Settings",
      FontSize = 20,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White
    });

    // Component Versions
    var versionsBorder = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1F1F1F")),
      CornerRadius = new CornerRadius(8),
      Padding = new Thickness(24)
    };

    var versionsStack = new StackPanel { Spacing = 16 };

    versionsStack.Children.Add(new TextBlock
    {
      Text = "Component Versions",
      FontSize = 16,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White
    });

    versionsStack.Children.Add(new TextBlock
    {
      Text = "Bundled dependency versions tracked by the modkit",
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
    versionsStack.Children.Add(versionsText);

    versionsBorder.Child = versionsStack;
    stack.Children.Add(versionsBorder);

    // Game Installation Settings
    var installBorder = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1F1F1F")),
      CornerRadius = new CornerRadius(8),
      Padding = new Thickness(24)
    };

    var installStack = new StackPanel { Spacing = 16 };

    installStack.Children.Add(new TextBlock
    {
      Text = "Game Installation",
      FontSize = 16,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White
    });

    installStack.Children.Add(new TextBlock
    {
      Text = "Set the path to the Menace installation directory. This is used for extracting game assets.",
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

    installStack.Children.Add(pathStack);
    installBorder.Child = installStack;
    stack.Children.Add(installBorder);

    // Deployment section
    var deployBorder = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1F1F1F")),
      CornerRadius = new CornerRadius(8),
      Padding = new Thickness(24)
    };

    var deployStack = new StackPanel { Spacing = 16 };

    deployStack.Children.Add(new TextBlock
    {
      Text = "Deployment",
      FontSize = 16,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White
    });

    deployStack.Children.Add(new TextBlock
    {
      Text = "Wipe the game's Mods folder and install fresh runtime dependencies (MelonLoader, DataExtractor, ModpackLoader). After clean redeploy, go to Modpacks and click Deploy All to redeploy your mods.",
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
    deployStack.Children.Add(cleanRedeployButton);

    var cleanRedeployStatusText = new TextBlock
    {
      Opacity = 0.7,
      Foreground = Brushes.White,
      TextWrapping = TextWrapping.Wrap,
      FontSize = 12
    };
    cleanRedeployStatusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("CleanRedeployStatus"));
    deployStack.Children.Add(cleanRedeployStatusText);

    deployBorder.Child = deployStack;
    stack.Children.Add(deployBorder);

    // Extracted Assets Directory
    var assetsBorder = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1F1F1F")),
      CornerRadius = new CornerRadius(8),
      Padding = new Thickness(24)
    };

    var assetsStack = new StackPanel { Spacing = 16 };

    assetsStack.Children.Add(new TextBlock
    {
      Text = "Extracted Assets Directory",
      FontSize = 16,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White
    });

    assetsStack.Children.Add(new TextBlock
    {
      Text = "Point this to your AssetRipper output directory. Allows you to extract once and reuse across app versions. Leave blank to auto-detect.",
      Opacity = 0.7,
      Foreground = Brushes.White,
      TextWrapping = TextWrapping.Wrap
    });

    var assetsPathStack = new StackPanel { Spacing = 8 };
    assetsPathStack.Children.Add(new TextBlock
    {
      Text = "Assets Path",
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White,
      FontSize = 13
    });

    var assetsPathBox = new TextBox
    {
      Watermark = "(auto-detect from game install or out2/assets)",
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      Foreground = Brushes.White,
      BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
      BorderThickness = new Thickness(1),
      Padding = new Thickness(12, 8)
    };
    assetsPathBox.Bind(TextBox.TextProperty,
      new Avalonia.Data.Binding("ExtractedAssetsPath") { Mode = Avalonia.Data.BindingMode.TwoWay });
    assetsPathStack.Children.Add(assetsPathBox);

    // Assets status message
    var assetsStatusText = new TextBlock
    {
      Opacity = 0.7,
      Foreground = Brushes.White,
      TextWrapping = TextWrapping.Wrap,
      FontSize = 12
    };
    assetsStatusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("AssetsPathStatus"));
    assetsPathStack.Children.Add(assetsStatusText);

    assetsStack.Children.Add(assetsPathStack);
    assetsBorder.Child = assetsStack;
    stack.Children.Add(assetsBorder);

    // Extraction Settings
    var extractionBorder = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1F1F1F")),
      CornerRadius = new CornerRadius(8),
      Padding = new Thickness(24)
    };

    var extractionStack = new StackPanel { Spacing = 16 };

    extractionStack.Children.Add(new TextBlock
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

    extractionStack.Children.Add(perfStack);

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

    extractionStack.Children.Add(profileStack);

    // Cache Management
    var cacheStack = new StackPanel { Spacing = 8 };
    cacheStack.Children.Add(new TextBlock
    {
      Text = "Cache Management",
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White,
      FontSize = 13,
      Margin = new Thickness(0, 8, 0, 0)
    });

    var cacheStatusText = new TextBlock
    {
      Foreground = Brushes.White,
      Opacity = 0.7,
      FontSize = 12
    };
    cacheStatusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("CacheStatus"));
    cacheStack.Children.Add(cacheStatusText);

    var cacheButtonStack = new StackPanel
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
    cacheButtonStack.Children.Add(clearCacheButton);

    var forceReExtractButton = new Button
    {
      Content = "Force Re-extract All",
      Background = new SolidColorBrush(Color.Parse("#2A2A2A")),
      Foreground = Brushes.White,
      BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
      Padding = new Thickness(16, 8)
    };
    forceReExtractButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("ForceReExtractCommand"));
    cacheButtonStack.Children.Add(forceReExtractButton);

    cacheStack.Children.Add(cacheButtonStack);
    extractionStack.Children.Add(cacheStack);

    extractionBorder.Child = extractionStack;
    stack.Children.Add(extractionBorder);

    scrollViewer.Content = stack;
    return scrollViewer;
  }
}
