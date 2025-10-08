using System;
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
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
      desktop.MainWindow = new MainWindow(_serviceProvider!);
    }

    base.OnFrameworkInitializationCompleted();
  }
}
