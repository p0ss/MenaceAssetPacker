using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.Views;

/// <summary>
/// Dialog that displays diagnostic check results.
/// Shows a list of checks with pass/warn/fail/error status indicators.
/// </summary>
public class DiagnosticResultsDialog : Window
{
    private readonly bool _isDestructive;
    private readonly ModpackManager? _modpackManager;
    private CancellationTokenSource? _cts;

    // UI elements
    private readonly TextBlock _statusIcon;
    private readonly TextBlock _statusText;
    private readonly ProgressBar _progressBar;
    private readonly StackPanel _resultsPanel;
    private readonly Button _copyButton;
    private readonly Button _closeButton;
    private readonly ScrollViewer _scrollViewer;

    private DiagnosticReport? _report;

    public DiagnosticResultsDialog(bool isDestructive, ModpackManager? modpackManager = null)
    {
        _isDestructive = isDestructive;
        _modpackManager = modpackManager;

        Title = isDestructive ? "Destructive Diagnostic Test" : "Diagnostic Results";
        Width = 600;
        Height = 550;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
        CanResize = true;
        MinWidth = 450;
        MinHeight = 350;

        // Build UI
        var mainStack = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 16
        };

        // Header with icon and title
        var headerStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12
        };

        _statusIcon = new TextBlock
        {
            FontSize = 24,
            VerticalAlignment = VerticalAlignment.Center,
            Text = "\u23f3", // Hourglass
            Foreground = new SolidColorBrush(Color.Parse(isDestructive ? "#8B0000" : "#006666"))
        };
        headerStack.Children.Add(_statusIcon);

        var titleText = new TextBlock
        {
            Text = isDestructive ? "Destructive Diagnostic Test" : "Dry Run Diagnostics",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };
        headerStack.Children.Add(titleText);

        mainStack.Children.Add(headerStack);

        // Status text
        _statusText = new TextBlock
        {
            Text = "Running diagnostic checks...",
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")),
            TextWrapping = TextWrapping.Wrap
        };
        mainStack.Children.Add(_statusText);

        // Progress bar
        _progressBar = new ProgressBar
        {
            IsIndeterminate = true,
            Height = 4,
            IsVisible = true
        };
        mainStack.Children.Add(_progressBar);

        // Results area with scroll
        var resultsBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            MinHeight = 280
        };

        _scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        _resultsPanel = new StackPanel { Spacing = 8 };
        _scrollViewer.Content = _resultsPanel;
        resultsBorder.Child = _scrollViewer;

        // Make results area stretch
        Grid.SetIsSharedSizeScope(resultsBorder, true);
        mainStack.Children.Add(resultsBorder);

        // Button row
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        _copyButton = new Button
        {
            Content = "Copy Results",
            FontSize = 13,
            MinWidth = 100,
            IsEnabled = false
        };
        _copyButton.Classes.Add("secondary");
        _copyButton.Click += OnCopyResultsClick;
        buttonRow.Children.Add(_copyButton);

        _closeButton = new Button
        {
            Content = "Cancel",
            FontSize = 13,
            MinWidth = 80
        };
        _closeButton.Classes.Add("primary");
        _closeButton.Click += OnCloseButtonClick;
        buttonRow.Children.Add(_closeButton);

        mainStack.Children.Add(buttonRow);

        Content = mainStack;
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        await RunDiagnosticsAsync();
    }

    private async Task RunDiagnosticsAsync()
    {
        _cts = new CancellationTokenSource();

        var progress = new Progress<DiagnosticProgress>(p =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                _statusText.Text = $"Running: {p.CurrentCheck} ({p.Completed}/{p.Total})";

                // Add result indicator for completed check
                if (p.Completed > 0)
                {
                    // We'll update the full list after completion
                }
            });
        });

        try
        {
            if (_isDestructive && _modpackManager != null)
            {
                _report = await DiagnosticService.Instance.RunDestructiveAsync(
                    _modpackManager, progress, _cts.Token);
            }
            else
            {
                _report = await DiagnosticService.Instance.RunDryAsync(progress, _cts.Token);
            }

            await Dispatcher.UIThread.InvokeAsync(() => DisplayResults(_report));
        }
        catch (OperationCanceledException)
        {
            _statusText.Text = "Diagnostics cancelled.";
            _progressBar.IsVisible = false;
            _closeButton.Content = "Close";
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Error running diagnostics: {ex.Message}";
            _progressBar.IsVisible = false;
            _closeButton.Content = "Close";
        }
    }

    private void DisplayResults(DiagnosticReport report)
    {
        _progressBar.IsVisible = false;
        _copyButton.IsEnabled = true;
        _closeButton.Content = "Close";

        // Update summary
        var passCount = 0;
        var warnCount = 0;
        var failCount = 0;
        var errorCount = 0;

        foreach (var check in report.Checks)
        {
            switch (check.Status)
            {
                case DiagnosticStatus.Pass: passCount++; break;
                case DiagnosticStatus.Warn: warnCount++; break;
                case DiagnosticStatus.Fail: failCount++; break;
                case DiagnosticStatus.Error: errorCount++; break;
            }
        }

        // Update status icon
        if (failCount > 0 || errorCount > 0)
        {
            _statusIcon.Text = "\u2716"; // X
            _statusIcon.Foreground = new SolidColorBrush(Color.Parse("#FF6B6B"));
        }
        else if (warnCount > 0)
        {
            _statusIcon.Text = "\u26a0"; // Warning
            _statusIcon.Foreground = new SolidColorBrush(Color.Parse("#FFB347"));
        }
        else
        {
            _statusIcon.Text = "\u2714"; // Checkmark
            _statusIcon.Foreground = new SolidColorBrush(Color.Parse("#4EC9B0"));
        }

        _statusText.Text = $"Completed: {report.Summary}  ({report.Duration.TotalSeconds:F1}s)";

        // Clear and populate results
        _resultsPanel.Children.Clear();

        // Add header info
        var headerInfo = new TextBlock
        {
            Text = $"Platform: {report.Platform}\nModkit: {report.ModkitVersion}\nRuntime: {report.RuntimeVersion}",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#888888")),
            Margin = new Thickness(0, 0, 0, 8)
        };
        _resultsPanel.Children.Add(headerInfo);

        // Add each check result
        foreach (var check in report.Checks)
        {
            var checkPanel = CreateCheckResultPanel(check);
            _resultsPanel.Children.Add(checkPanel);
        }
    }

    private Border CreateCheckResultPanel(DiagnosticCheck check)
    {
        var (icon, color) = check.Status switch
        {
            DiagnosticStatus.Pass => ("\u2714", "#4EC9B0"),    // Green check
            DiagnosticStatus.Warn => ("\u26a0", "#FFB347"),    // Yellow warning
            DiagnosticStatus.Fail => ("\u2716", "#FF6B6B"),    // Red X
            DiagnosticStatus.Error => ("\u26d4", "#FF6B6B"),   // Red circle
            DiagnosticStatus.Skipped => ("\u23ed", "#888888"), // Skip icon
            _ => ("?", "#888888")
        };

        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1E1E1E")),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 2, 0, 2)
        };

        var mainStack = new StackPanel { Spacing = 4 };

        // Header row: icon + name + status
        var headerRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        var iconBlock = new TextBlock
        {
            Text = icon,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse(color)),
            VerticalAlignment = VerticalAlignment.Center
        };
        headerRow.Children.Add(iconBlock);

        var nameBlock = new TextBlock
        {
            Text = check.Name,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };
        headerRow.Children.Add(nameBlock);

        mainStack.Children.Add(headerRow);

        // Message
        var messageBlock = new TextBlock
        {
            Text = check.Message,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#CCCCCC")),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(22, 0, 0, 0)
        };
        mainStack.Children.Add(messageBlock);

        // Details (if any)
        if (!string.IsNullOrEmpty(check.Details))
        {
            var detailsBlock = new TextBlock
            {
                Text = check.Details,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#888888")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(22, 4, 0, 0),
                FontFamily = new FontFamily("Consolas, Menlo, monospace")
            };
            mainStack.Children.Add(detailsBlock);
        }

        border.Child = mainStack;
        return border;
    }

    private async void OnCopyResultsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_report == null) return;

        var json = _report.ToJson();
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(json);
            _copyButton.Content = "Copied!";
            await Task.Delay(1500);
            _copyButton.Content = "Copy Results";
        }
    }

    private void OnCloseButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _cts?.Cancel();
        base.OnClosing(e);
    }

    /// <summary>
    /// Show the diagnostic dialog with dry-run (non-destructive) checks.
    /// </summary>
    public static async Task ShowDryRunAsync(Window parent)
    {
        var dialog = new DiagnosticResultsDialog(isDestructive: false);
        await dialog.ShowDialog(parent);
    }

    /// <summary>
    /// Show the diagnostic dialog with destructive (deploy/undeploy) tests.
    /// </summary>
    public static async Task ShowDestructiveAsync(Window parent, ModpackManager modpackManager)
    {
        var dialog = new DiagnosticResultsDialog(isDestructive: true, modpackManager);
        await dialog.ShowDialog(parent);
    }
}
