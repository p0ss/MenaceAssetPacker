using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Avalonia;
using Avalonia.ReactiveUI;

namespace Menace.Modkit.App;

internal static class Program
{
  [STAThread]
  public static void Main(string[] args)
  {
    // Register assembly resolver for Roslyn DLLs in roslyn/ subdirectory
    // (excluded from single-file bundle because Roslyn needs assemblies on disk)
    RegisterRoslynAssemblyResolver();

    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
  }

  private static void RegisterRoslynAssemblyResolver()
  {
    var appDir = AppContext.BaseDirectory;
    var roslynDir = Path.Combine(appDir, "roslyn");

    if (!Directory.Exists(roslynDir))
      return;

    AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
    {
      if (assemblyName.Name?.StartsWith("Microsoft.CodeAnalysis") == true)
      {
        var dllPath = Path.Combine(roslynDir, $"{assemblyName.Name}.dll");
        if (File.Exists(dllPath))
          return context.LoadFromAssemblyPath(dllPath);
      }
      return null;
    };
  }

  public static AppBuilder BuildAvaloniaApp()
  {
    return AppBuilder.Configure<App>()
      .UsePlatformDetect()
      .WithInterFont()
      .LogToTrace()
      .UseReactiveUI();
  }
}
