using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;
using Menace.Modkit.App.ViewModels;

namespace Menace.Modkit.App.Views;

/// <summary>
/// Settings view for modders creating mods.
/// </summary>
public class ToolSettingsView : UserControl
{
    private ToolSettingsViewModel? _viewModel;

    public ToolSettingsView()
    {
        Content = BuildUI();

        // Subscribe to DataContext changes to hook up ViewModel events
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from old ViewModel
        if (_viewModel != null)
        {
            _viewModel.ExtractionDialogRequested -= OnExtractionDialogRequested;
            _viewModel.RecoveryDialogRequested -= OnRecoveryDialogRequested;
            _viewModel.UpdateFlowRequested -= OnUpdateFlowRequested;
        }

        // Subscribe to new ViewModel
        _viewModel = DataContext as ToolSettingsViewModel;
        if (_viewModel != null)
        {
            _viewModel.ExtractionDialogRequested += OnExtractionDialogRequested;
            _viewModel.RecoveryDialogRequested += OnRecoveryDialogRequested;
            _viewModel.UpdateFlowRequested += OnUpdateFlowRequested;
        }
    }

    private async void OnExtractionDialogRequested(object? sender, ExtractionDialogRequestEventArgs e)
    {
        try
        {
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow == null)
            {
                e.Result.TrySetResult(false);
                return;
            }

            var result = await ExtractionDialog.ShowAsync(
                parentWindow,
                AppSettings.Instance.GameInstallPath,
                e.DeployManager,
                e.ModpackManager,
                e.DeployedModpackNames);

            e.Result.TrySetResult(result?.Success ?? false);
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[ToolSettingsView] Extraction dialog error: {ex}");
            e.Result.TrySetException(ex);
        }
    }

    private async void OnRecoveryDialogRequested(object? sender, RecoveryDialogRequestEventArgs e)
    {
        try
        {
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow == null)
            {
                e.Result.TrySetResult(false);
                return;
            }

            // Show a simple confirmation dialog for recovery
            var confirmed = await Controls.ConfirmationDialog.ShowAsync(
                parentWindow,
                "Complete Mod Redeploy",
                $"A previous extraction completed while the app was closed.\n\n" +
                $"The following mods were undeployed and need to be redeployed:\n" +
                $"- {string.Join("\n- ", e.PendingState.DeployedModpacks)}\n\n" +
                $"Would you like to redeploy them now?",
                "Redeploy Mods",
                isDestructive: false);

            if (confirmed)
            {
                // Perform the redeploy
                var progress = new Progress<string>(msg =>
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_viewModel != null)
                        {
                            // Update status through ViewModel (reflection to access private setter)
                            var prop = typeof(ToolSettingsViewModel).GetProperty("ExtractionStatus");
                            // Can't use private setter, so we'll just log
                            ModkitLog.Info($"[Recovery] {msg}");
                        }
                    }));

                var result = await e.DeployManager.DeployAllAsync(progress);

                // Clear pending state
                PendingRedeployState.Delete(AppSettings.Instance.GameInstallPath);

                e.Result.TrySetResult(result.Success);
            }
            else
            {
                // User declined - clear the pending state
                PendingRedeployState.Delete(AppSettings.Instance.GameInstallPath);
                e.Result.TrySetResult(false);
            }
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[ToolSettingsView] Recovery dialog error: {ex}");
            e.Result.TrySetException(ex);
        }
    }

    private async void OnUpdateFlowRequested(object? sender, UpdateFlowRequestEventArgs e)
    {
        try
        {
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow == null)
            {
                e.Result.TrySetResult(false);
                return;
            }

            // Create and show the setup window as a dialog
            var setupWindow = new Window
            {
                Title = "Menace Modkit Update",
                Width = 800,
                Height = 650,
                Background = new SolidColorBrush(Color.Parse("#0D0D0D")),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            // Set app icon
            try
            {
                var iconUri = new Uri("avares://Menace.Modkit.App/Assets/icon.jpg");
                setupWindow.Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(iconUri));
            }
            catch { /* Icon loading failed */ }

            var setupViewModel = new SetupViewModel();
            var completionSource = new TaskCompletionSource<bool>();

            setupViewModel.SetupComplete += () =>
            {
                completionSource.TrySetResult(true);
                setupWindow.Close();
            };
            setupViewModel.SetupSkipped += () =>
            {
                completionSource.TrySetResult(false);
                setupWindow.Close();
            };

            var setupView = new SetupView
            {
                DataContext = setupViewModel
            };

            setupWindow.Content = setupView;
            setupWindow.Closed += (_, _) => completionSource.TrySetResult(false);

            // Show as dialog (blocks until closed)
            await setupWindow.ShowDialog(parentWindow);

            var success = await completionSource.Task;
            e.Result.TrySetResult(success);
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[ToolSettingsView] Update flow error: {ex}");
            e.Result.TrySetException(ex);
        }
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
            Text = "Modkit Version",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });

        // Current version display
        var versionRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12
        };

        var currentVersionLabel = new TextBlock
        {
            Text = "Current Version:",
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        };
        versionRow.Children.Add(currentVersionLabel);

        var currentVersionText = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#4EC9B0")), // Teal color
            VerticalAlignment = VerticalAlignment.Center
        };
        currentVersionText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("CurrentAppVersion"));
        versionRow.Children.Add(currentVersionText);

        stack.Children.Add(versionRow);

        // Update status row
        var updateRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12
        };

        var updateStatusText = new TextBlock
        {
            Foreground = Brushes.White,
            Opacity = 0.8,
            VerticalAlignment = VerticalAlignment.Center
        };
        updateStatusText.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding("AppUpdateStatus"));
        updateRow.Children.Add(updateStatusText);

        // Update button (only visible when update available)
        var updateButton = new Button
        {
            Content = "Run Update",
            Margin = new Thickness(8, 0, 0, 0)
        };
        updateButton.Classes.Add("primary");
        updateButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("StartUpdateCommand"));
        updateButton.Bind(Button.IsVisibleProperty, new Avalonia.Data.Binding("HasAppUpdate"));
        updateRow.Children.Add(updateButton);

        // Check for updates button
        var checkButton = new Button
        {
            Content = "Check for Updates",
            Margin = new Thickness(8, 0, 0, 0)
        };
        checkButton.Classes.Add("secondary");
        checkButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("CheckForAppUpdateCommand"));
        updateRow.Children.Add(checkButton);

        stack.Children.Add(updateRow);

        // Separator
        stack.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.Parse("#3E3E3E")),
            Margin = new Thickness(0, 8, 0, 8)
        });

        // Component versions header
        stack.Children.Add(new TextBlock
        {
            Text = "Component Versions",
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Bundled dependency versions tracked by the modkit.",
            Opacity = 0.7,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12
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
            Watermark = "(auto-detect from game install or out2/assets)"
        };
        pathBox.Classes.Add("input");
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

        var autoExtractCheck = new CheckBox
        {
            Content = "Enable automatic data extraction on game launch",
            Foreground = Brushes.White
        };
        autoExtractCheck.Bind(CheckBox.IsCheckedProperty,
            new Avalonia.Data.Binding("EnableAutoExtraction") { Mode = Avalonia.Data.BindingMode.TwoWay });
        perfStack.Children.Add(autoExtractCheck);

        // Add tooltip explaining the setting
        var autoExtractHint = new TextBlock
        {
            Text = "When disabled, the game won't freeze to extract data. Enable if you create mods.",
            FontSize = 11,
            Foreground = Brushes.White,
            Opacity = 0.6,
            Margin = new Thickness(20, 0, 0, 4)
        };
        perfStack.Children.Add(autoExtractHint);

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
            Margin = new Thickness(0, 4, 0, 0)
        };
        validateButton.Classes.Add("secondary");
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
            Content = "Clear Cache"
        };
        clearCacheButton.Classes.Add("secondary");
        clearCacheButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("ClearCacheCommand"));
        buttonStack.Children.Add(clearCacheButton);

        var forceExtractDataButton = new Button
        {
            Content = "Force Extract Data"
        };
        forceExtractDataButton.Classes.Add("secondary");
        forceExtractDataButton.Bind(Button.CommandProperty, new Avalonia.Data.Binding("ForceExtractDataCommand"));
        buttonStack.Children.Add(forceExtractDataButton);

        // Pending extraction indicator
        var pendingIndicator = new TextBlock
        {
            Text = "⏳ Pending",
            Foreground = new SolidColorBrush(Color.Parse("#FFB347")), // Orange
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        pendingIndicator.Bind(TextBlock.IsVisibleProperty, new Avalonia.Data.Binding("IsExtractionPending"));
        buttonStack.Children.Add(pendingIndicator);

        var forceExtractAssetsButton = new Button
        {
            Content = "Force Extract Assets"
        };
        forceExtractAssetsButton.Classes.Add("secondary");
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
