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
    try
    {
      // Register assembly resolver for Roslyn DLLs in roslyn/ subdirectory
      // (excluded from single-file bundle because Roslyn needs assemblies on disk)
      RegisterRoslynAssemblyResolver();

      BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
    catch (Exception ex)
    {
      // Show error to user on crash - helps diagnose startup failures
      var errorMsg = $"Menace Modkit failed to start:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
      Console.Error.WriteLine(errorMsg);

      // Write to error log file next to the executable
      try
      {
        var logPath = Path.Combine(AppContext.BaseDirectory, "modkit_crash.log");
        File.WriteAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{errorMsg}\n\nInner: {ex.InnerException}");
      }
      catch { /* ignore logging failures */ }

      // Re-throw to show system error dialog on Windows
      throw;
    }
  }

  private static void RegisterRoslynAssemblyResolver()
  {
    var appDir = AppContext.BaseDirectory;
    var roslynDir = Path.Combine(appDir, "roslyn");

    if (!Directory.Exists(roslynDir))
      return;

    AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
    {
      // Roslyn and its dependencies are in the roslyn/ subdirectory
      // (excluded from single-file bundle because Roslyn needs assemblies on disk)
      var isRoslynAssembly = assemblyName.Name?.StartsWith("Microsoft.CodeAnalysis") == true
                          || assemblyName.Name == "System.Collections.Immutable"
                          || assemblyName.Name == "System.Reflection.Metadata";

      if (isRoslynAssembly)
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
