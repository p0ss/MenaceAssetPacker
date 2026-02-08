using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Menace.Modkit.App.Views;
using Menace.Modkit.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Menace.Modkit.App;

public class App : Application
{
  private IServiceProvider? _serviceProvider;

  public override void Initialize()
  {
    // Load only App.axaml for styles
    AvaloniaXamlLoader.Load(this);

    // Set up dependency injection
    var services = new ServiceCollection();
    services.AddMenaceModkitCore();
    _serviceProvider = services.BuildServiceProvider();
  }

  public override void OnFrameworkInitializationCompleted()
  {
    // Log version at startup
    Services.ModkitLog.Info($"[App] {ModkitVersion.AppFull} starting");
    Services.ModkitLog.Info($"[App] Platform: {Environment.OSVersion}, Runtime: {Environment.Version}");

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      desktop.MainWindow = new MainWindow(_serviceProvider!);
      desktop.Exit += OnExit;
    }

    AppDomain.CurrentDomain.ProcessExit += (_, _) => KillChildProcesses();

    base.OnFrameworkInitializationCompleted();
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
