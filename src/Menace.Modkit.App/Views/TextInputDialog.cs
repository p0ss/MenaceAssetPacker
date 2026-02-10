using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Menace.Modkit.App.Views;

/// <summary>
/// Reusable single-field text input dialog.
/// Returns the entered string, or null if cancelled.
/// </summary>
public class TextInputDialog : Window
{
    private readonly TextBox _inputBox;

    public TextInputDialog(string title, string prompt, string defaultValue = "")
    {
        Title = title;
        Width = 400;
        Height = 180;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
        CanResize = false;

        var stack = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12
        };

        // Prompt label
        stack.Children.Add(new TextBlock
        {
            Text = prompt,
            Foreground = Brushes.White,
            FontSize = 13
        });

        // Input field
        _inputBox = new TextBox
        {
            Text = defaultValue,
            FontSize = 13
        };
        _inputBox.Classes.Add("input");
        _inputBox.SelectAll();
        stack.Children.Add(_inputBox);

        // Button row
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 4, 0, 0)
        };

        var okButton = new Button
        {
            Content = "OK",
            FontSize = 13
        };
        okButton.Classes.Add("primary");
        okButton.Click += (_, _) =>
        {
            var text = _inputBox.Text?.Trim();
            if (!string.IsNullOrEmpty(text))
                Close(text);
        };
        buttonRow.Children.Add(okButton);

        var cancelButton = new Button
        {
            Content = "Cancel",
            FontSize = 13
        };
        cancelButton.Classes.Add("secondary");
        cancelButton.Click += (_, _) => Close(null);
        buttonRow.Children.Add(cancelButton);

        stack.Children.Add(buttonRow);
        Content = stack;
    }
}
