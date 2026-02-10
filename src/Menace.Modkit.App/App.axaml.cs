using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
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
                ShowSetupWindow();
            }
            else
            {
                // Go directly to main app
                ShowMainWindow();
            }
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) => KillChildProcesses();

        base.OnFrameworkInitializationCompleted();
    }

    private async Task<bool> CheckIfSetupNeededAsync()
    {
        try
        {
            return await ComponentManager.Instance.NeedsSetupAsync();
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[App] Failed to check setup status: {ex.Message}");
            // On error, continue to main app (bundled components may be available)
            return false;
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
        setupViewModel.SetupComplete += () =>
        {
            setupWindow.Close();
            ShowMainWindow();
        };
        setupViewModel.SetupSkipped += () =>
        {
            setupWindow.Close();
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
        }
    }

    private void ShowMainWindow()
    {
        var mainWindow = new MainWindow(_serviceProvider!);

        if (_desktop != null)
        {
            _desktop.MainWindow = mainWindow;
            mainWindow.Show();
        }
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
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
