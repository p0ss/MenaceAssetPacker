namespace Menace.Modkit.Typetrees;

/// <summary>
/// Builds and persists typetree cache data derived from Menace assets.
/// </summary>
public interface ITypetreeCacheBuilder
{
  Task<TypetreeCacheResult> BuildAsync(TypetreeCacheRequest request, CancellationToken cancellationToken = default);
}
