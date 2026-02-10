using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Menace.Modkit.App.Views;

/// <summary>
/// Return type for the create modpack dialog.
/// </summary>
public record CreateModpackResult(string Name, string Author, string Description);

/// <summary>
/// Modal dialog for creating a new modpack with name, author, and description fields.
/// Returns a CreateModpackResult, or null if cancelled.
/// </summary>
public class CreateModpackDialog : Window
{
    private readonly TextBox _nameBox;
    private readonly TextBox _authorBox;
    private readonly TextBox _descriptionBox;

    public CreateModpackDialog()
    {
        Title = "Create New Modpack";
        Width = 450;
        Height = 350;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
        CanResize = false;

        var stack = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 8
        };

        // Title
        stack.Children.Add(new TextBlock
        {
            Text = "Create New Modpack",
            Foreground = Brushes.White,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Name
        stack.Children.Add(CreateLabel("Name"));
        _nameBox = CreateTextBox("My Modpack");
        stack.Children.Add(_nameBox);

        // Author
        stack.Children.Add(CreateLabel("Author"));
        _authorBox = CreateTextBox("Modder");
        stack.Children.Add(_authorBox);

        // Description
        stack.Children.Add(CreateLabel("Description"));
        _descriptionBox = new TextBox
        {
            Watermark = "Optional description...",
            FontSize = 13,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 60
        };
        _descriptionBox.Classes.Add("input");
        stack.Children.Add(_descriptionBox);

        // Button row
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var createButton = new Button
        {
            Content = "Create",
            FontSize = 13
        };
        createButton.Classes.Add("primary");
        createButton.Click += (_, _) =>
        {
            var name = _nameBox.Text?.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                Close(new CreateModpackResult(
                    name,
                    _authorBox.Text?.Trim() ?? "",
                    _descriptionBox.Text?.Trim() ?? ""));
            }
        };
        buttonRow.Children.Add(createButton);

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

    private static TextBlock CreateLabel(string text) => new TextBlock
    {
        Text = text,
        FontSize = 11,
        FontWeight = FontWeight.SemiBold,
        Foreground = Brushes.White,
        Opacity = 0.8
    };

    private static TextBox CreateTextBox(string watermark = "")
    {
        var textBox = new TextBox
        {
            Watermark = watermark,
            FontSize = 13
        };
        textBox.Classes.Add("input");
        return textBox;
    }
}
