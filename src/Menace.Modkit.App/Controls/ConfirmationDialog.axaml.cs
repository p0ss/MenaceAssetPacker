using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using System.Threading.Tasks;

namespace Menace.Modkit.App.Controls;

public partial class ConfirmationDialog : UserControl
{
    private TaskCompletionSource<bool>? _tcs;

    public ConfirmationDialog()
    {
        InitializeComponent();
        CancelButton.Click += OnCancel;
        ConfirmButton.Click += OnConfirm;
    }

    public void Configure(string title, string message, string confirmText, bool isDestructive)
    {
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;

        if (!isDestructive)
        {
            ConfirmButton.Classes.Remove("destructive");
            ConfirmButton.Classes.Add("primary");
        }
    }

    public Task<bool> ShowAsync()
    {
        _tcs = new TaskCompletionSource<bool>();
        return _tcs.Task;
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        _tcs?.TrySetResult(false);
        if (this.Parent is Window window)
            window.Close();
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        _tcs?.TrySetResult(true);
        if (this.Parent is Window window)
            window.Close();
    }

    /// <summary>
    /// Show a confirmation dialog and wait for user response.
    /// </summary>
    /// <param name="parent">Parent window to center the dialog over</param>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Dialog message</param>
    /// <param name="confirmText">Text for the confirm button (default: "Confirm")</param>
    /// <param name="isDestructive">Whether the action is destructive (uses red button)</param>
    /// <returns>True if user confirmed, false if cancelled</returns>
    public static async Task<bool> ShowAsync(
        Window parent,
        string title,
        string message,
        string confirmText = "Confirm",
        bool isDestructive = true)
    {
        var dialog = new ConfirmationDialog();
        dialog.Configure(title, message, confirmText, isDestructive);

        var window = new Window
        {
            Content = dialog,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
            SystemDecorations = SystemDecorations.BorderOnly,
            Background = Brushes.Transparent,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent }
        };

        var showTask = dialog.ShowAsync();
        await window.ShowDialog(parent);

        // If the window was closed without a button click, treat as cancel
        if (!showTask.IsCompleted)
            dialog._tcs?.TrySetResult(false);

        return await showTask;
    }
}
