namespace Menace.Modkit.Typetrees;

/// <summary>
/// Describes the parameters required to build a typetree cache from a Menace installation.
/// </summary>
/// <param name="SourcePath">Root directory of the Menace install to inspect.</param>
/// <param name="OutputPath">Destination directory for generated typetree cache artifacts.</param>
/// <param name="GameVersion">Optional game version string recorded in the cache manifest.</param>
/// <param name="UnityVersion">Optional Unity player version string recorded in the cache manifest.</param>
public sealed record TypetreeCacheRequest(
  string SourcePath,
  string OutputPath,
  string? GameVersion,
  string? UnityVersion
);
