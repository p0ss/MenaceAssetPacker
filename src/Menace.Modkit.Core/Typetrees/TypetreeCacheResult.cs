namespace Menace.Modkit.Typetrees;

/// <summary>
/// Summary of a typetree cache operation.
/// </summary>
/// <param name="ManifestPath">Full path to the generated manifest file.</param>
/// <param name="CreatedAt">Timestamp recorded in the manifest.</param>
public sealed record TypetreeCacheResult(
  string ManifestPath,
  DateTimeOffset CreatedAt
);
