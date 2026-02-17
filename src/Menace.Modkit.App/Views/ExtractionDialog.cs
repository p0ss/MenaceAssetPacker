using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.App.Views;

/// <summary>
/// Dialog states for the extraction flow.
/// </summary>
public enum ExtractionDialogState
{
    ModsDetected,
    Undeploying,
    PendingLaunch,
    WaitingForExtraction,
    Redeploying,
    Complete,
    Error
}

/// <summary>
/// Result of the extraction dialog.
/// </summary>
public record ExtractionDialogResult(bool Success, string? ErrorMessage = null);

/// <summary>
/// Blocking modal dialog that handles the entire extraction flow:
/// 1. Detects deployed mods
/// 2. Undeploys them
/// 3. Waits for game to extract data
/// 4. Redeploys mods
/// </summary>
public class ExtractionDialog : Window
{
    private readonly string _gameInstallPath;
    private readonly DeployManager _deployManager;
    private readonly ModpackManager _modpackManager;
    private readonly List<string> _deployedModpackNames;
    private readonly ModLoaderInstaller _installer;

    private ExtractionDialogState _currentState;
    private CancellationTokenSource? _pollCts;
    private string? _errorMessage;

    // UI elements
    private readonly TextBlock _statusIcon;
    private readonly TextBlock _statusText;
    private readonly TextBlock _detailsText;
    private readonly StackPanel _modListPanel;
    private readonly Button _primaryButton;
    private readonly Button _secondaryButton;
    private readonly ProgressBar _progressBar;

    private const int PollIntervalMs = 2000;
    private const int TimeoutMinutes = 30;

    public ExtractionDialog(
        string gameInstallPath,
        DeployManager deployManager,
        ModpackManager modpackManager,
        List<string> deployedModpackNames)
    {
        _gameInstallPath = gameInstallPath;
        _deployManager = deployManager;
        _modpackManager = modpackManager;
        _deployedModpackNames = deployedModpackNames;
        _installer = new ModLoaderInstaller(gameInstallPath);

        Title = "Data Extraction";
        Width = 500;
        Height = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
        CanResize = false;

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
            VerticalAlignment = VerticalAlignment.Center
        };
        headerStack.Children.Add(_statusIcon);

        var titleText = new TextBlock
        {
            Text = "Data Extraction",
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
            FontSize = 14,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        };
        mainStack.Children.Add(_statusText);

        // Progress bar (hidden initially)
        _progressBar = new ProgressBar
        {
            IsIndeterminate = true,
            Height = 4,
            IsVisible = false
        };
        mainStack.Children.Add(_progressBar);

        // Details/mod list area
        var detailsBorder = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#252526")),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12),
            MinHeight = 120,
            MaxHeight = 180
        };

        var detailsScroll = new ScrollViewer();

        var detailsStack = new StackPanel { Spacing = 4 };

        _detailsText = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")),
            TextWrapping = TextWrapping.Wrap
        };
        detailsStack.Children.Add(_detailsText);

        _modListPanel = new StackPanel { Spacing = 2 };
        detailsStack.Children.Add(_modListPanel);

        detailsScroll.Content = detailsStack;
        detailsBorder.Child = detailsScroll;
        mainStack.Children.Add(detailsBorder);

        // Button row
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        _secondaryButton = new Button
        {
            Content = "Cancel",
            FontSize = 13,
            MinWidth = 80
        };
        _secondaryButton.Classes.Add("secondary");
        _secondaryButton.Click += OnSecondaryButtonClick;
        buttonRow.Children.Add(_secondaryButton);

        _primaryButton = new Button
        {
            Content = "Continue",
            FontSize = 13,
            MinWidth = 100
        };
        _primaryButton.Classes.Add("primary");
        _primaryButton.Click += OnPrimaryButtonClick;
        buttonRow.Children.Add(_primaryButton);

        mainStack.Children.Add(buttonRow);

        Content = mainStack;

        // Initialize to ModsDetected state
        SetState(ExtractionDialogState.ModsDetected);
    }

    private void SetState(ExtractionDialogState state)
    {
        _currentState = state;
        UpdateUI();
    }

    private void UpdateUI()
    {
        _modListPanel.Children.Clear();
        _progressBar.IsVisible = false;

        switch (_currentState)
        {
            case ExtractionDialogState.ModsDetected:
                _statusIcon.Text = "\u26a0"; // Warning
                _statusIcon.Foreground = new SolidColorBrush(Color.Parse("#FFB347"));
                _statusText.Text = "Mods are currently deployed. They must be temporarily undeployed to extract vanilla game data.";
                _detailsText.Text = "The following mods will be undeployed and automatically redeployed after extraction:";

                foreach (var modName in _deployedModpackNames)
                {
                    _modListPanel.Children.Add(new TextBlock
                    {
                        Text = $"  - {modName}",
                        FontSize = 12,
                        Foreground = Brushes.White
                    });
                }

                _primaryButton.Content = "Continue";
                _primaryButton.IsEnabled = true;
                _secondaryButton.Content = "Cancel";
                _secondaryButton.IsEnabled = true;
                _secondaryButton.IsVisible = true;
                break;

            case ExtractionDialogState.Undeploying:
                _statusIcon.Text = "\u23f3"; // Hourglass
                _statusIcon.Foreground = new SolidColorBrush(Color.Parse("#4EC9B0"));
                _statusText.Text = "Undeploying mods...";
                _detailsText.Text = "Please wait while mods are being undeployed.";
                _progressBar.IsVisible = true;
                _primaryButton.IsEnabled = false;
                _secondaryButton.IsEnabled = false;
                break;

            case ExtractionDialogState.PendingLaunch:
                _statusIcon.Text = "\U0001F3AE"; // Game controller
                _statusIcon.Foreground = new SolidColorBrush(Color.Parse("#4EC9B0"));
                _statusText.Text = "Launch the game to extract data.";
                _detailsText.Text = "The DataExtractor mod will automatically extract vanilla game data when the game starts. " +
                    "You can close the game after reaching the main menu.";
                _primaryButton.Content = "Launch Game";
                _primaryButton.IsEnabled = true;
                _secondaryButton.Content = "Cancel & Redeploy";
                _secondaryButton.IsEnabled = true;
                break;

            case ExtractionDialogState.WaitingForExtraction:
                _statusIcon.Text = "\u23f3"; // Hourglass
                _statusIcon.Foreground = new SolidColorBrush(Color.Parse("#4EC9B0"));
                _statusText.Text = "Waiting for extraction to complete...";
                _detailsText.Text = "The game is extracting data. This dialog will automatically detect when extraction is complete. " +
                    "You can close the game once you see the main menu.";
                _progressBar.IsVisible = true;
                _primaryButton.Content = "Waiting...";
                _primaryButton.IsEnabled = false;
                _secondaryButton.Content = "Cancel & Redeploy";
                _secondaryButton.IsEnabled = true;
                break;

            case ExtractionDialogState.Redeploying:
                _statusIcon.Text = "\u23f3"; // Hourglass
                _statusIcon.Foreground = new SolidColorBrush(Color.Parse("#4EC9B0"));
                _statusText.Text = "Redeploying mods...";
                _detailsText.Text = "Please wait while mods are being redeployed.";
                _progressBar.IsVisible = true;
                _primaryButton.IsEnabled = false;
                _secondaryButton.IsEnabled = false;
                break;

            case ExtractionDialogState.Complete:
                _statusIcon.Text = "\u2714"; // Checkmark
                _statusIcon.Foreground = new SolidColorBrush(Color.Parse("#4EC9B0"));
                _statusText.Text = "Extraction complete!";
                _detailsText.Text = "Vanilla game data has been extracted and mods have been redeployed successfully.";
                _primaryButton.Content = "Close";
                _primaryButton.IsEnabled = true;
                _secondaryButton.IsVisible = false;
                break;

            case ExtractionDialogState.Error:
                _statusIcon.Text = "\u2716"; // X
                _statusIcon.Foreground = new SolidColorBrush(Color.Parse("#FF6B6B"));
                _statusText.Text = "An error occurred";
                _detailsText.Text = _errorMessage ?? "Unknown error";
                _primaryButton.Content = "Redeploy Anyway";
                _primaryButton.IsEnabled = true;
                _secondaryButton.Content = "Close";
                _secondaryButton.IsEnabled = true;
                _secondaryButton.IsVisible = true;
                break;
        }
    }

    private async void OnPrimaryButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        switch (_currentState)
        {
            case ExtractionDialogState.ModsDetected:
                await UndeployModsAsync();
                break;

            case ExtractionDialogState.PendingLaunch:
                await LaunchGameAndWaitAsync();
                break;

            case ExtractionDialogState.Complete:
                Close(new ExtractionDialogResult(true));
                break;

            case ExtractionDialogState.Error:
                // Redeploy anyway
                await RedeployModsAsync();
                break;
        }
    }

    private async void OnSecondaryButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        switch (_currentState)
        {
            case ExtractionDialogState.ModsDetected:
                Close(new ExtractionDialogResult(false, "Cancelled by user"));
                break;

            case ExtractionDialogState.PendingLaunch:
            case ExtractionDialogState.WaitingForExtraction:
                // Cancel and redeploy
                _pollCts?.Cancel();
                await RedeployModsAsync();
                Close(new ExtractionDialogResult(false, "Cancelled by user"));
                break;

            case ExtractionDialogState.Error:
                // Just close without redeploying
                PendingRedeployState.Delete(_gameInstallPath);
                Close(new ExtractionDialogResult(false, _errorMessage));
                break;
        }
    }

    private async Task UndeployModsAsync()
    {
        SetState(ExtractionDialogState.Undeploying);

        try
        {
            // Save pending redeploy state before undeploying
            var pendingState = new PendingRedeployState
            {
                DeployedModpacks = _deployedModpackNames.ToList(),
                UndeployTimestamp = DateTime.UtcNow,
                RedeployPending = true
            };
            pendingState.SaveTo(_gameInstallPath);

            // Undeploy all mods
            var progress = new Progress<string>(msg =>
                Dispatcher.UIThread.Post(() => _detailsText.Text = msg));

            var result = await _deployManager.UndeployAllAsync(progress);

            if (!result.Success)
            {
                _errorMessage = $"Failed to undeploy mods: {result.Message}";
                SetState(ExtractionDialogState.Error);
                return;
            }

            // Delete the fingerprint to force re-extraction
            var fingerprintPath = Path.Combine(_gameInstallPath, "UserData", "ExtractedData", "_extraction_fingerprint.txt");
            if (File.Exists(fingerprintPath))
            {
                try { File.Delete(fingerprintPath); } catch { }
            }

            // Ensure DataExtractor is installed and write the force extraction flag
            await _installer.InstallDataExtractorAsync(msg =>
                Dispatcher.UIThread.Post(() => _detailsText.Text = msg), forceExtraction: true);

            SetState(ExtractionDialogState.PendingLaunch);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error during undeploy: {ex.Message}";
            SetState(ExtractionDialogState.Error);
        }
    }

    private async Task LaunchGameAndWaitAsync()
    {
        try
        {
            // Launch the game
            await _installer.LaunchGameAsync(msg =>
                Dispatcher.UIThread.Post(() => _detailsText.Text = msg));

            SetState(ExtractionDialogState.WaitingForExtraction);

            // Start polling for extraction completion
            _pollCts = new CancellationTokenSource();
            _ = PollForExtractionCompletionAsync(_pollCts.Token);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error launching game: {ex.Message}";
            SetState(ExtractionDialogState.Error);
        }
    }

    private async Task PollForExtractionCompletionAsync(CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        var pendingState = PendingRedeployState.LoadFrom(_gameInstallPath);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollIntervalMs, ct);

                // Check if extraction is complete
                if (pendingState != null && pendingState.IsExtractionComplete(_gameInstallPath))
                {
                    // Extraction complete! Redeploy mods
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await RedeployModsAsync();
                    });
                    return;
                }

                // Check for timeout
                if ((DateTime.UtcNow - startTime).TotalMinutes > TimeoutMinutes)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _errorMessage = $"Extraction timed out after {TimeoutMinutes} minutes. " +
                            "You can try launching the game manually or redeploy mods now.";
                        SetState(ExtractionDialogState.Error);
                    });
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _errorMessage = $"Error while waiting: {ex.Message}";
                    SetState(ExtractionDialogState.Error);
                });
                return;
            }
        }
    }

    private async Task RedeployModsAsync()
    {
        SetState(ExtractionDialogState.Redeploying);

        try
        {
            var progress = new Progress<string>(msg =>
                Dispatcher.UIThread.Post(() => _detailsText.Text = msg));

            var result = await _deployManager.DeployAllAsync(progress);

            // Clear pending redeploy state
            PendingRedeployState.Delete(_gameInstallPath);

            if (!result.Success)
            {
                _errorMessage = $"Failed to redeploy mods: {result.Message}";
                SetState(ExtractionDialogState.Error);
                return;
            }

            SetState(ExtractionDialogState.Complete);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error during redeploy: {ex.Message}";
            SetState(ExtractionDialogState.Error);
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _pollCts?.Cancel();
        base.OnClosing(e);
    }

    /// <summary>
    /// Show the extraction dialog to handle mod undeploy/redeploy around extraction.
    /// Returns the result of the extraction flow.
    /// </summary>
    public static async Task<ExtractionDialogResult?> ShowAsync(
        Window parent,
        string gameInstallPath,
        DeployManager deployManager,
        ModpackManager modpackManager,
        List<string> deployedModpackNames)
    {
        var dialog = new ExtractionDialog(
            gameInstallPath,
            deployManager,
            modpackManager,
            deployedModpackNames);

        return await dialog.ShowDialog<ExtractionDialogResult?>(parent);
    }
}
