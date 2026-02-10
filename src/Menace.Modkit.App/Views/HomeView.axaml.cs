using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using Menace.Modkit.App.ViewModels;

namespace Menace.Modkit.App.Views;

public class HomeView : UserControl
{
    public HomeView()
    {
        Content = BuildUI();
    }

    private MainViewModel? GetMainViewModel()
    {
        var window = this.FindAncestorOfType<Window>();
        return window?.DataContext as MainViewModel;
    }

    private Control BuildUI()
    {
        var mainGrid = new Grid
        {
            Background = new SolidColorBrush(Color.Parse("#0A0A0A")),
            RowDefinitions = new RowDefinitions("*,Auto,*")
        };

        // Center content
        var centerStack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 48
        };

        // Title (no icon, just text)
        var headerStack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 12
        };

        headerStack.Children.Add(new TextBlock
        {
            Text = "Menace Modkit",
            FontSize = 36,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        headerStack.Children.Add(new TextBlock
        {
            Text = "Tools for modding and managing Menace",
            FontSize = 14,
            Foreground = Brushes.White,
            Opacity = 0.6,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        centerStack.Children.Add(headerStack);

        // Navigation tiles
        var tilesPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 32
        };

        tilesPanel.Children.Add(CreateImageTile(
            "Mod Loader",
            "Manage load order, saves, and settings",
            "avares://Menace.Modkit.App/Assets/marines.png",
            "avares://Menace.Modkit.App/Assets/marines-alt.png",
            "#004f43", // Teal
            () => GetMainViewModel()?.NavigateToModLoader()
        ));

        tilesPanel.Children.Add(CreateImageTile(
            "Modding Tools",
            "Create and edit mods: data, assets, code",
            "avares://Menace.Modkit.App/Assets/pirates.png",
            "avares://Menace.Modkit.App/Assets/pirates-alt.png",
            "#410511", // Maroon
            () => GetMainViewModel()?.NavigateToModdingTools()
        ));

        centerStack.Children.Add(tilesPanel);

        mainGrid.Children.Add(centerStack);
        Grid.SetRow(centerStack, 1);

        // Version info at bottom
        var versionText = new TextBlock
        {
            Text = ModkitVersion.Short,
            FontSize = 11,
            Foreground = Brushes.White,
            Opacity = 0.3,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 16)
        };
        mainGrid.Children.Add(versionText);
        Grid.SetRow(versionText, 2);

        return mainGrid;
    }

    private Control CreateImageTile(string title, string description, string imageUri, string altImageUri, string overlayColor, Action onClick)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1A1A1A")),
            CornerRadius = new CornerRadius(12),
            Width = 300,
            Height = 220,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            ClipToBounds = true
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto")
        };

        // Image area with overlay
        var imageContainer = new Grid();

        // Background image (normal)
        Image? normalImage = null;
        Image? altImage = null;

        try
        {
            var bitmap = new Bitmap(AssetLoader.Open(new Uri(imageUri)));
            normalImage = new Image
            {
                Source = bitmap,
                Stretch = Stretch.UniformToFill,
                Opacity = 0.7,
                IsVisible = true
            };
            imageContainer.Children.Add(normalImage);
        }
        catch
        {
            // Fallback solid color if image fails
            imageContainer.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.Parse(overlayColor))
            });
        }

        // Alt image (for hover)
        try
        {
            var altBitmap = new Bitmap(AssetLoader.Open(new Uri(altImageUri)));
            altImage = new Image
            {
                Source = altBitmap,
                Stretch = Stretch.UniformToFill,
                Opacity = 0.7,
                IsVisible = false
            };
            imageContainer.Children.Add(altImage);
        }
        catch
        {
            // Alt image not available, that's fine
        }

        // Gradient overlay with custom color
        var colorBase = Color.Parse(overlayColor);
        var overlay = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(0, colorBase.R, colorBase.G, colorBase.B), 0),
                    new GradientStop(Color.FromArgb(204, colorBase.R, colorBase.G, colorBase.B), 0.7),
                    new GradientStop(Color.FromArgb(255, colorBase.R, colorBase.G, colorBase.B), 1)
                }
            }
        };
        imageContainer.Children.Add(overlay);

        grid.Children.Add(imageContainer);
        Grid.SetRow(imageContainer, 0);
        Grid.SetRowSpan(imageContainer, 2);

        // Text content at bottom
        var textStack = new StackPanel
        {
            Margin = new Thickness(20, 0, 20, 20),
            VerticalAlignment = VerticalAlignment.Bottom,
            Spacing = 6
        };

        textStack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });

        textStack.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12,
            Foreground = Brushes.White,
            Opacity = 0.85,
            TextWrapping = TextWrapping.Wrap
        });

        grid.Children.Add(textStack);
        Grid.SetRow(textStack, 1);

        border.Child = grid;

        // Hover effect - scale and swap images
        border.PointerEntered += (_, _) =>
        {
            border.RenderTransform = new ScaleTransform(1.02, 1.02);
            if (normalImage != null && altImage != null)
            {
                normalImage.IsVisible = false;
                altImage.IsVisible = true;
            }
        };
        border.PointerExited += (_, _) =>
        {
            border.RenderTransform = null;
            if (normalImage != null && altImage != null)
            {
                normalImage.IsVisible = true;
                altImage.IsVisible = false;
            }
        };

        border.PointerPressed += (_, _) => onClick();

        return border;
    }
}
