using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Menace.Modkit.App.Controls;

/// <summary>
/// A small info icon button that shows a tooltip on hover and focus.
/// Designed for accessibility with full keyboard navigation support.
/// </summary>
public class InfoButton : Button
{
    public static readonly StyledProperty<string?> TooltipTextProperty =
        AvaloniaProperty.Register<InfoButton, string?>(nameof(TooltipText));

    public string? TooltipText
    {
        get => GetValue(TooltipTextProperty);
        set => SetValue(TooltipTextProperty, value);
    }

    private Popup? _tooltipPopup;
    private TextBlock? _tooltipTextBlock;
    private bool _isPopupAttached;

    public InfoButton()
    {
        Content = "\u2139"; // "ℹ" info symbol
        Width = 16;
        Height = 16;
        MinWidth = 16;
        MinHeight = 16;
        FontSize = 10;
        Padding = new Thickness(0);
        Background = Brushes.Transparent;
        Foreground = new SolidColorBrush(Color.Parse("#8ECDC8")); // Teal accent
        BorderThickness = new Thickness(0);
        Cursor = new Cursor(StandardCursorType.Help);
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;

        // Focusable is true by default for Button, ensuring tab navigation works
        Focusable = true;

        // Create tooltip content
        _tooltipTextBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300,
            Foreground = Brushes.White,
            FontSize = 12
        };

        var tooltipBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#2D2D2D")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3E3E3E")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(4),
            Child = _tooltipTextBlock
        };

        _tooltipPopup = new Popup
        {
            Child = tooltipBorder,
            Placement = PlacementMode.Right,
            PlacementTarget = this,
            IsLightDismissEnabled = false
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Attach popup to visual tree via TopLevel
        if (!_isPopupAttached && _tooltipPopup != null)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window window)
            {
                // For popups to work, they need to be in the visual tree
                // We can achieve this by setting the popup's PlacementTarget
                _tooltipPopup.PlacementTarget = this;
                _isPopupAttached = true;
            }
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        ShowTooltip();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        HideTooltip();
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        ShowTooltip();
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        HideTooltip();
    }

    private void ShowTooltip()
    {
        if (_tooltipPopup != null && _tooltipTextBlock != null && !string.IsNullOrEmpty(TooltipText))
        {
            _tooltipTextBlock.Text = TooltipText;
            _tooltipPopup.IsOpen = true;
        }
    }

    private void HideTooltip()
    {
        if (_tooltipPopup != null)
        {
            _tooltipPopup.IsOpen = false;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Clean up popup when detached
        if (_tooltipPopup != null)
        {
            _tooltipPopup.IsOpen = false;
        }
        _isPopupAttached = false;
    }
}
