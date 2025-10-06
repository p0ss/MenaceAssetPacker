using Menace.Modkit.Cli.Commands;
using Menace.Modkit.Cli.Infrastructure;
using Menace.Modkit.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
  var services = new ServiceCollection();
  services.AddLogging();
  services.AddMenaceModkitCore();

  var registrar = new TypeRegistrar(services);
  var app = new CommandApp(registrar);
  app.Configure(config =>
  {
    config.SetApplicationName("menace-modkit");
    config.AddCommand<CacheTypetreeCommand>("cache-typetrees")
      .WithDescription("Builds a typetree cache from a Menace installation.");
  });

  try
  {
    return await app.RunAsync(args).ConfigureAwait(false);
  }
  catch (Exception ex)
  {
    AnsiConsole.MarkupLine($"[red]Fatal error:[/] {ex.Message}");
    return -99;
  }
}
