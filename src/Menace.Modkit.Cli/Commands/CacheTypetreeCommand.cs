using System.ComponentModel;
using Menace.Modkit.Typetrees;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Menace.Modkit.Cli.Commands;

internal sealed class CacheTypetreeCommand : AsyncCommand<CacheTypetreeCommand.Settings>
{
  private readonly ITypetreeCacheBuilder _cacheBuilder;

  public CacheTypetreeCommand(ITypetreeCacheBuilder cacheBuilder)
  {
    _cacheBuilder = cacheBuilder;
  }

  public override ValidationResult Validate(CommandContext context, Settings settings)
  {
    if (string.IsNullOrWhiteSpace(settings.Source))
    {
      return ValidationResult.Error("A source directory must be provided via --source.");
    }

    if (string.IsNullOrWhiteSpace(settings.Output))
    {
      return ValidationResult.Error("An output directory must be provided via --output.");
    }

    return ValidationResult.Success();
  }

  public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
  {
    var request = new TypetreeCacheRequest(
      settings.Source!,
      settings.Output!,
      settings.GameVersion,
      settings.UnityVersion
    );

    try
    {
      var result = await _cacheBuilder.BuildAsync(request, context.CancellationToken).ConfigureAwait(false);
      AnsiConsole.MarkupLine($"[green]Typetree manifest written:[/] {result.ManifestPath}");
      return 0;
    }
    catch (Exception ex)
    {
      AnsiConsole.MarkupLine($"[red]Failed to build typetree cache:[/] {ex.Message}");
      return -1;
    }
  }

  internal sealed class Settings : CommandSettings
  {
    [CommandOption("-s|--source <DIRECTORY>")]
    [Description("Path to the Menace game installation containing asset bundles.")]
    public string? Source { get; init; }

    [CommandOption("-o|--output <DIRECTORY>")]
    [Description("Destination directory for typetree cache artifacts.")]
    public string? Output { get; init; }

    [CommandOption("--game-version <VERSION>")]
    [Description("Optional game version string recorded in the cache manifest.")]
    public string? GameVersion { get; init; }

    [CommandOption("--unity-version <VERSION>")]
    [Description("Optional Unity version string recorded in the cache manifest.")]
    public string? UnityVersion { get; init; }
  }
}
