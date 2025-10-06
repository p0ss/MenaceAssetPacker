using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Menace.Modkit.Typetrees;

/// <summary>
/// Default implementation that prepares typetree cache directories and manifest metadata.
/// </summary>
public sealed class TypetreeCacheService : ITypetreeCacheBuilder
{
  private const string ManifestFileName = "typetree-cache.json";

  private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
  {
    WriteIndented = true
  };

  public async Task<TypetreeCacheResult> BuildAsync(TypetreeCacheRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var normalizedSource = NormalizeDirectoryPath(request.SourcePath);
    if (!Directory.Exists(normalizedSource))
    {
      throw new DirectoryNotFoundException($"Source directory not found: {normalizedSource}");
    }

    var normalizedOutput = NormalizeDirectoryPath(request.OutputPath);
    Directory.CreateDirectory(normalizedOutput);

    var manifest = CreateManifest(request, normalizedSource);

    var manifestPath = Path.Combine(normalizedOutput, ManifestFileName);
    await using var stream = File.Create(manifestPath);
    await JsonSerializer.SerializeAsync(stream, manifest, SerializerOptions, cancellationToken).ConfigureAwait(false);

    return new TypetreeCacheResult(manifestPath, manifest.CreatedAtUtc);
  }

  private static TypetreeCacheManifest CreateManifest(TypetreeCacheRequest request, string normalizedSource)
  {
    return new TypetreeCacheManifest
    {
      GameVersion = request.GameVersion,
      UnityVersion = request.UnityVersion,
      SourcePath = normalizedSource,
      SourceFingerprint = ComputeFingerprint(normalizedSource),
      CreatedAtUtc = DateTimeOffset.UtcNow
    };
  }

  private static string NormalizeDirectoryPath(string path)
  {
    if (string.IsNullOrWhiteSpace(path))
    {
      throw new ArgumentException("A directory path must be provided.", nameof(path));
    }

    return Path.GetFullPath(path.Trim());
  }

  private static string ComputeFingerprint(string sourcePath)
  {
    using var sha256 = SHA256.Create();
    var buffer = Encoding.UTF8.GetBytes(sourcePath);
    var hash = sha256.ComputeHash(buffer);
    return Convert.ToHexString(hash);
  }
}
