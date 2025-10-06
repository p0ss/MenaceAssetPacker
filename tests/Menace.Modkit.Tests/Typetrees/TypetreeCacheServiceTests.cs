using Menace.Modkit.Typetrees;

namespace Menace.Modkit.Tests.Typetrees;

public sealed class TypetreeCacheServiceTests
{
  [Fact]
  public async Task BuildAsync_WritesManifestToOutputDirectory()
  {
    using var source = new TemporaryDirectory();
    using var output = new TemporaryDirectory();

    var request = new TypetreeCacheRequest(source.Path, output.Path, "1.0.0", "2021.3");
    var service = new TypetreeCacheService();

    var result = await service.BuildAsync(request);

    var manifestExists = File.Exists(result.ManifestPath);

    Assert.True(manifestExists);
  }

  private sealed class TemporaryDirectory : IDisposable
  {
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "menace-modkit-test", Guid.NewGuid().ToString("N"));

    public TemporaryDirectory()
    {
      Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
      try
      {
        if (Directory.Exists(Path))
        {
          Directory.Delete(Path, true);
        }
      }
      catch
      {
        // Best effort cleanup.
      }
    }
  }
}
