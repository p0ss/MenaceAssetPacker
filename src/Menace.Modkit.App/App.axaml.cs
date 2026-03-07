using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Menace.Modkit.App.Models;
using Menace.Modkit.App.Services;
using Menace.Modkit.App.ViewModels;
using Menace.Modkit.App.Views;
using Menace.Modkit.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Menace.Modkit.App;

public class App : Application
{
    private IServiceProvider? _serviceProvider;
    private IClassicDesktopStyleApplicationLifetime? _desktop;

    public override void Initialize()
    {
        // Load only App.axaml for styles
        AvaloniaXamlLoader.Load(this);

        // Set up dependency injection
        var services = new ServiceCollection();
        services.AddMenaceModkitCore();
        _serviceProvider = services.BuildServiceProvider();
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        // Log version at startup
        ModkitLog.Info($"[App] {ModkitVersion.AppFull} starting");
        ModkitLog.Info($"[App] Platform: {Environment.OSVersion}, Runtime: {Environment.Version}");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;
            desktop.Exit += OnExit;

            // Check if setup is needed
            var needsSetup = await CheckIfSetupNeededAsync();

            if (needsSetup)
            {
                // Show setup window first
                ModkitLog.Info("[App] Opening setup window");
                ShowSetupWindow();
            }
            else
            {
                // Check for legacy installation before showing main window
                // Keep showing the dialog until legacy state is resolved
                await EnforceLegacyMigrationAsync();

                // Go to main app
                ModkitLog.Info("[App] Opening main window");
                ShowMainWindow();
            }
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) => KillChildProcesses();

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Check for legacy installation patterns and show migration dialog if needed.
    /// </summary>
    /// <returns>True if legacy install was handled (migrated or reset), false otherwise.</returns>
    private async Task<bool> CheckAndHandleLegacyInstallAsync()
    {
        try
        {
            var gamePath = AppSettings.Instance.GameInstallPath;
            if (string.IsNullOrEmpty(gamePath))
            {
                ModkitLog.Info("[App] No game path configured, skipping legacy check");
                return false;
            }

            ModkitLog.Info("[App] Checking for legacy installation patterns...");

            // Get health status to check for legacy install
            var healthStatus = await InstallHealthService.Instance.GetCurrentHealthAsync(forceRefresh: true);

            if (healthStatus.State != InstallHealthState.LegacyInstallDetected)
            {
                ModkitLog.Info("[App] No legacy installation detected");
                return false;
            }

            ModkitLog.Info("[App] Legacy installation detected, showing migration dialog");

            // Run detection again to get full details
            var detector = new LegacyInstallDetector();
            var detectionResult = detector.Detect(gamePath);

            // Create a temporary window to host the dialog
            var hostWindow = new Window
            {
                Title = "Menace Modkit",
                Width = 600,
                Height = 500,
                Background = new SolidColorBrush(Color.Parse("#0A0A0A")),
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowInTaskbar = true
            };

            // Set app icon
            try
            {
                var iconUri = new Uri("avares://Menace.Modkit.App/Assets/icon.jpg");
                hostWindow.Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(iconUri));
            }
            catch { /* Icon loading failed */ }

            if (_desktop != null)
            {
                _desktop.MainWindow = hostWindow;
                hostWindow.Show();
            }

            // Show the migration dialog
            var result = await LegacyMigrationDialog.ShowAsync(hostWindow, detectionResult, gamePath);

            // Close the host window
            hostWindow.Close();

            ModkitLog.Info($"[App] Legacy migration dialog result: {result}");
            return result;
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[App] Error checking for legacy install: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Enforce legacy migration - keep showing the dialog until the user resolves legacy state.
    /// This prevents proceeding to the main app with unresolved legacy installation.
    /// If legacy state cannot be resolved after max attempts, the app exits.
    /// </summary>
    private async Task EnforceLegacyMigrationAsync()
    {
        const int maxAttempts = 10; // Prevent infinite loops in case of bugs
        int attempts = 0;

        while (attempts < maxAttempts)
        {
            attempts++;
            var handled = await CheckAndHandleLegacyInstallAsync();

            if (handled)
            {
                ModkitLog.Info("[App] Legacy migration completed successfully");
                return;
            }

            // Check if legacy state still exists
            var healthStatus = await InstallHealthService.Instance.GetCurrentHealthAsync(forceRefresh: true);
            if (healthStatus.State != InstallHealthState.LegacyInstallDetected)
            {
                ModkitLog.Info("[App] No legacy state detected, proceeding");
                return;
            }

            // Legacy state still exists but user cancelled - show dialog again
            ModkitLog.Warn($"[App] Legacy migration cancelled by user (attempt {attempts}), showing dialog again");
        }

        // Exceeded max attempts - fail closed, do NOT proceed with unresolved legacy state
        ModkitLog.Error($"[App] Legacy migration not resolved after {maxAttempts} attempts. " +
            "This may indicate a bug. Please report this issue. Exiting.");

        // Exit the application - we cannot safely proceed with unresolved legacy state
        if (_desktop != null)
        {
            _desktop.Shutdown(1);
        }
        Environment.Exit(1);
    }

    private async Task<bool> CheckIfSetupNeededAsync()
    {
        try
        {
            ModkitLog.Info("[App] Checking setup status...");

            // Migrate legacy bundled components to the manifest
            // This ensures existing installs are tracked before checking setup status
            ComponentManager.Instance.MigrateLegacyComponents();

            var needsSetupTask = ComponentManager.Instance.NeedsSetupAsync();
            var completed = await Task.WhenAny(needsSetupTask, Task.Delay(TimeSpan.FromSeconds(15)));

            if (completed != needsSetupTask)
            {
                ModkitLog.Warn("[App] Setup check timed out after 15s, defaulting to setup screen");
                return true;
            }

            var needsSetup = await needsSetupTask;
            ModkitLog.Info($"[App] Setup status check complete: needsSetup={needsSetup}");

            return needsSetup;
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[App] Failed to check setup status: {ex.Message}");
            // On error, require setup - fail closed to prevent incomplete installs from proceeding
            return true;
        }
    }

    private void ShowSetupWindow()
    {
        var setupWindow = new Window
        {
            Title = "Menace Modkit Setup",
            Width = 800,
            Height = 600,
            Background = new SolidColorBrush(Color.Parse("#0D0D0D")),
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        // Set app icon
        try
        {
            var iconUri = new Uri("avares://Menace.Modkit.App/Assets/icon.jpg");
            setupWindow.Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(iconUri));
        }
        catch { /* Icon loading failed */ }

        var setupViewModel = new SetupViewModel();
        setupViewModel.SetupComplete += async () =>
        {
            setupWindow.Close();
            // Check for legacy install after setup completes - enforce resolution
            await EnforceLegacyMigrationAsync();
            ShowMainWindow();
        };
        setupViewModel.SetupSkipped += async () =>
        {
            setupWindow.Close();
            // Check for legacy install even if setup was skipped - enforce resolution
            await EnforceLegacyMigrationAsync();
            ShowMainWindow();
        };

        var setupView = new SetupView
        {
            DataContext = setupViewModel
        };

        setupWindow.Content = setupView;

        if (_desktop != null)
        {
            _desktop.MainWindow = setupWindow;
            if (!setupWindow.IsVisible)
            {
                setupWindow.Show();
            }
        }
    }

    private void ShowMainWindow()
    {
        var mainWindow = new MainWindow(_serviceProvider!);

        if (_desktop != null)
        {
            _desktop.MainWindow = mainWindow;
            mainWindow.Show();

            // Start UI state service for MCP server integration
            if (mainWindow.DataContext is ViewModels.MainViewModel viewModel)
            {
                UIStateService.Instance.Start(
                    mainWindow,
                    () => $"{viewModel.CurrentSection}/{viewModel.CurrentSubSection}",
                    () => viewModel.SelectedViewModel?.GetType().Name ?? "Unknown"
                );

                // Start HTTP server for UI automation/testing
                UIHttpServer.Instance.Start(viewModel);
            }

            // Run provenance validation in background (non-blocking)
            _ = ValidateComponentProvenanceAsync();
        }
    }

    /// <summary>
    /// Validate component provenance on startup (runs in background).
    /// Logs warnings for legacy components without provenance tracking.
    /// </summary>
    private async Task ValidateComponentProvenanceAsync()
    {
        try
        {
            // Small delay to not interfere with startup
            await Task.Delay(2000);

            var results = await ComponentManager.Instance.ValidateProvenanceAsync();

            // Log summary
            var summary = ComponentManager.Instance.GetProvenanceSummary();
            if (summary.TotalComponents > 0)
            {
                if (summary.HasLegacyComponents)
                {
                    ModkitLog.Warn($"[App] {summary.Legacy} component(s) have legacy installation without provenance tracking");
                }
                if (summary.MissingProvenance > 0)
                {
                    ModkitLog.Warn($"[App] {summary.MissingProvenance} downloaded component(s) are missing provenance data");
                }
            }
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[App] Error validating component provenance: {ex.Message}");
        }
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        UIHttpServer.Instance.Dispose();
        KillChildProcesses();
    }

    private static void KillChildProcesses()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("AssetRipper.GUI.Free"))
            {
                try { proc.Kill(); } catch { }
            }
        }
        catch { }
    }
}
