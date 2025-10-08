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
      Spacing = 24
    };

    // Title
    stack.Children.Add(new TextBlock
    {
      Text = "Settings",
      FontSize = 20,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White
    });

    // Settings panel
    var settingsBorder = new Border
    {
      Background = new SolidColorBrush(Color.Parse("#1F1F1F")),
      CornerRadius = new CornerRadius(8),
      Padding = new Thickness(24)
    };

    var settingsStack = new StackPanel { Spacing = 16 };

    // Header
    settingsStack.Children.Add(new TextBlock
    {
      Text = "Menace Installation",
      FontSize = 16,
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White
    });
    settingsStack.Children.Add(new TextBlock
    {
      Text = "Provide the path to the Menace installation so the tool can detect the Unity version and build typetree caches.",
      Opacity = 0.7,
      Foreground = Brushes.White,
      TextWrapping = TextWrapping.Wrap
    });

    // Install path
    var pathStack = new StackPanel { Spacing = 12 };
    pathStack.Children.Add(new TextBlock
    {
      Text = "Install Path",
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White
    });
    var pathBox = new TextBox { Watermark = "/path/to/Menace" };
    pathBox.Bind(TextBox.TextProperty,
      new Avalonia.Data.Binding("MenaceInstallPath") { Mode = Avalonia.Data.BindingMode.TwoWay });
    pathStack.Children.Add(pathBox);
    settingsStack.Children.Add(pathStack);

    // Buttons
    var buttonPanel = new StackPanel
    {
      Orientation = Orientation.Horizontal,
      Spacing = 12
    };

    var detectButton = new Button { Content = "Detect Unity Version" };
    detectButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("DetectUnityVersion"));
    buttonPanel.Children.Add(detectButton);

    var buildButton = new Button { Content = "Build Typetree Cache" };
    buildButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("BuildTypetreeCache"));
    buttonPanel.Children.Add(buildButton);

    settingsStack.Children.Add(buttonPanel);

    // Unity version status
    var versionStack = new StackPanel { Spacing = 8 };
    versionStack.Children.Add(new TextBlock
    {
      Text = "Unity Version",
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White
    });
    var versionStatus = new TextBlock { Opacity = 0.8, Foreground = Brushes.White };
    versionStatus.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("UnityVersionStatus"));
    versionStack.Children.Add(versionStatus);
    settingsStack.Children.Add(versionStack);

    // Typetree status
    var typetreeStack = new StackPanel { Spacing = 8 };
    typetreeStack.Children.Add(new TextBlock
    {
      Text = "Typetree Cache",
      FontWeight = FontWeight.SemiBold,
      Foreground = Brushes.White
    });
    var typetreeStatus = new TextBlock { Opacity = 0.8, Foreground = Brushes.White };
    typetreeStatus.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("TypetreeStatus"));
    typetreeStack.Children.Add(typetreeStatus);
    settingsStack.Children.Add(typetreeStack);

    settingsBorder.Child = settingsStack;
    stack.Children.Add(settingsBorder);

    scrollViewer.Content = stack;
    return scrollViewer;
  }
}
