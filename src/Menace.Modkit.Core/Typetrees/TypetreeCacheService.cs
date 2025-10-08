using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using AssetRipper.IO.Files;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.IO.Files.SerializedFiles.Parser;

namespace Menace.Modkit.Typetrees;

/// <summary>
/// Builds a typetree cache by walking a Menace installation and exporting Unity type metadata.
/// </summary>
public sealed class TypetreeCacheService : ITypetreeCacheBuilder
{
  private const string ManifestFileName = "typetree-cache.json";
  private const string TypetreeDirectoryName = "typetrees";

  private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
  {
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
  };

  private static readonly string[] CandidateExtensions =
  {
    ".assets",
    ".sharedassets",
    ".unity3d",
    ".bundle"
  };

  private static readonly HashSet<string> CandidateNames = new(StringComparer.OrdinalIgnoreCase)
  {
    "globalgamemanagers",
    "globalgamemanagers.assets",
    "resources.assets",
    "unity default resources",
    "unity_builtin_extra"
  };

  private static readonly HashSet<char> InvalidFileNameCharacters = new(Path.GetInvalidFileNameChars());

  public async Task<TypetreeCacheResult> BuildAsync(TypetreeCacheRequest request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var normalizedSource = NormalizeDirectoryPath(request.SourcePath);
    Console.WriteLine($"[TypetreeCache] Source directory: {normalizedSource}");

    if (!Directory.Exists(normalizedSource))
    {
      throw new DirectoryNotFoundException($"Source directory not found: {normalizedSource}");
    }

    var normalizedOutput = NormalizeDirectoryPath(request.OutputPath);
    Console.WriteLine($"[TypetreeCache] Output directory: {normalizedOutput}");
    Directory.CreateDirectory(normalizedOutput);

    var typetreeDirectory = Path.Combine(normalizedOutput, TypetreeDirectoryName);
    Directory.CreateDirectory(typetreeDirectory);

    var cacheFiles = new List<TypetreeCacheFile>();
    var outputNameCounters = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    var candidateFiles = EnumerateCandidateFiles(normalizedSource).ToList();
    Console.WriteLine($"[TypetreeCache] Found {candidateFiles.Count} candidate files");

    foreach (var candidate in candidateFiles)
    {
      cancellationToken.ThrowIfCancellationRequested();
      Console.WriteLine($"[TypetreeCache] Processing: {candidate}");

      if (!TryLoadFile(candidate, out var file))
      {
        Console.WriteLine($"[TypetreeCache]   ❌ Failed to load file");
        continue;
      }

      using (file)
      {
        Console.WriteLine($"[TypetreeCache]   ✓ File loaded as {file.GetType().Name}");
        try
        {
          file.ReadContentsRecursively();
          Console.WriteLine($"[TypetreeCache]   ✓ Contents read");
        }
        catch (Exception ex)
        {
          Console.WriteLine($"[TypetreeCache]   ❌ Failed to read contents: {ex.Message}");
          continue;
        }

       var serializedFiles = ExtractSerializedFiles(file);
       Console.WriteLine($"[TypetreeCache]   Found {serializedFiles.Count} serialized files");

       if (serializedFiles.Count == 0)
       {
         continue;
       }

       var containerRelativePath = Path.GetRelativePath(normalizedSource, candidate);
        var sourceHash = ComputeFileHash(candidate);
        foreach (var serializedFile in serializedFiles)
        {
          cancellationToken.ThrowIfCancellationRequested();
          Console.WriteLine($"[TypetreeCache]     Serialized file: {serializedFile.Name}, Version: {serializedFile.Version}, HasTypeTree: {serializedFile.HasTypeTree}");

          var collection = CreateCollection(serializedFile, containerRelativePath);
          if (collection is null)
          {
            Console.WriteLine($"[TypetreeCache]     ⚠️ No typetree data (skipping)");
            continue;
          }

          Console.WriteLine($"[TypetreeCache]     ✓ Created collection with {collection.Types?.Count ?? 0} types");

          var outputRelativePath = EnsureUniqueRelativePath(
            BuildRelativeOutputPath(containerRelativePath, serializedFile.Name),
            outputNameCounters);

          var outputFullPath = Path.Combine(typetreeDirectory, outputRelativePath);
          var outputDirectory = Path.GetDirectoryName(outputFullPath);
          if (!string.IsNullOrEmpty(outputDirectory))
          {
            Directory.CreateDirectory(outputDirectory);
          }

          await using (var stream = File.Create(outputFullPath))
          {
            await JsonSerializer.SerializeAsync(stream, collection, SerializerOptions, cancellationToken).ConfigureAwait(false);
          }

          cacheFiles.Add(new TypetreeCacheFile
          {
            Source = containerRelativePath.Replace('\\', '/'),
            SerializedFile = serializedFile.Name,
            UnityVersion = serializedFile.Version.ToString(),
            FormatVersion = (int)serializedFile.Generation,
            TypeCount = serializedFile.Types.Length,
            ReferenceTypeCount = serializedFile.RefTypes.Length,
            Output = Path.Combine(TypetreeDirectoryName, outputRelativePath).Replace('\\', '/'),
            Hash = sourceHash
          });
        }
      }
    }

    cacheFiles.Sort((a, b) => string.CompareOrdinal(a.Output, b.Output));

    Console.WriteLine($"[TypetreeCache] Total typetree files extracted: {cacheFiles.Count}");

    var manifest = new TypetreeCacheManifest
    {
      GameVersion = request.GameVersion,
      UnityVersion = request.UnityVersion,
      SourcePath = normalizedSource,
      CreatedAtUtc = DateTimeOffset.UtcNow,
      ToolVersion = "0.1.0-dev",
      SourceFingerprint = ComputeFingerprint(normalizedSource, cacheFiles),
      Files = cacheFiles
    };

    var manifestPath = Path.Combine(normalizedOutput, ManifestFileName);
    await using (var stream = File.Create(manifestPath))
    {
      await JsonSerializer.SerializeAsync(stream, manifest, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    return new TypetreeCacheResult(manifestPath, manifest.CreatedAtUtc);
  }

  private static bool TryLoadFile(string filePath, out FileBase file)
  {
    file = null!;
    try
    {
      if (!SchemeReader.IsReadableFile(filePath, LocalFileSystem.Instance))
      {
        return false;
      }

      file = SchemeReader.LoadFile(filePath, LocalFileSystem.Instance);
      return true;
    }
    catch
    {
      file?.Dispose();
      file = null!;
      return false;
    }
  }

  private static IReadOnlyList<SerializedFile> ExtractSerializedFiles(FileBase file)
  {
    return file switch
    {
      SerializedFile serializedFile => new List<SerializedFile> { serializedFile },
      FileContainer container => container.FetchSerializedFiles().ToList(),
      _ => Array.Empty<SerializedFile>()
    };
  }

  private static TypetreeCollection? CreateCollection(SerializedFile serializedFile, string containerRelativePath)
  {
    if (!serializedFile.HasTypeTree)
    {
      return null;
    }

    var types = serializedFile.Types.ToArray();
    var refTypes = serializedFile.RefTypes.ToArray();

    return new TypetreeCollection
    {
      Source = containerRelativePath.Replace('\\', '/'),
      SerializedFile = serializedFile.Name,
      UnityVersion = serializedFile.Version.ToString(),
      FormatVersion = (int)serializedFile.Generation,
      Types = types.Select(CreateDefinition).ToList(),
      ReferencedTypes = refTypes.Select(CreateDefinition).ToList()
    };
  }

  private static TypetreeDefinition CreateDefinition(SerializedTypeBase type)
  {
    var nodes = type.OldType.Nodes.Select(node => new TypetreeNodeModel
    {
      Level = node.Level,
      TypeFlags = node.TypeFlags,
      Type = node.Type,
      Name = node.Name,
      ByteSize = node.ByteSize,
      Index = node.Index,
      Version = node.Version,
      MetaFlag = (uint)node.MetaFlag,
      TypeOffset = node.TypeStrOffset,
      NameOffset = node.NameStrOffset,
      RefTypeHash = node.RefTypeHash == 0 ? null : node.RefTypeHash
    }).ToList();

    return new TypetreeDefinition
    {
      TypeId = type.TypeID,
      OriginalTypeId = type.OriginalTypeID,
      ScriptTypeIndex = type.ScriptTypeIndex,
      IsStripped = type.IsStrippedType,
      ScriptId = ToHex(type.ScriptID),
      TypeHash = ToHex(type.OldTypeHash),
      Nodes = nodes
    };
  }

  private static IEnumerable<string> EnumerateCandidateFiles(string root)
  {
    foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
    {
      if (IsCandidateFile(file))
      {
        yield return file;
      }
    }
  }

  private static bool IsCandidateFile(string filePath)
  {
    var fileName = Path.GetFileName(filePath);
    if (CandidateNames.Contains(fileName))
    {
      return true;
    }

    if (fileName.StartsWith("cab-", StringComparison.OrdinalIgnoreCase))
    {
      return true;
    }

    if (fileName.StartsWith("level", StringComparison.OrdinalIgnoreCase) && fileName.Skip(5).All(char.IsDigit))
    {
      return true;
    }

    var extension = Path.GetExtension(fileName);
    return CandidateExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
  }

  private static string BuildRelativeOutputPath(string containerRelativePath, string serializedFileName)
  {
    var sanitizedContainer = SanitizePath(containerRelativePath);
    var sanitizedName = SanitizeSegment(serializedFileName);

    if (string.IsNullOrEmpty(sanitizedName))
    {
      sanitizedName = "serialized";
    }

    if (string.IsNullOrEmpty(sanitizedContainer))
    {
      return sanitizedName + ".json";
    }

    return Path.Combine(sanitizedContainer, sanitizedName + ".json");
  }

  private static string EnsureUniqueRelativePath(string relativePath, ConcurrentDictionary<string, int> counters)
  {
    if (counters.TryAdd(relativePath, 0))
    {
      return relativePath;
    }

    var count = counters.AddOrUpdate(relativePath, 1, (_, current) => current + 1);
    var directory = Path.GetDirectoryName(relativePath);
    var fileName = Path.GetFileNameWithoutExtension(relativePath);
    var extension = Path.GetExtension(relativePath);
    var uniqueName = $"{fileName}_{count}{extension}";
    return string.IsNullOrEmpty(directory) ? uniqueName : Path.Combine(directory, uniqueName);
  }

  private static string SanitizePath(string relativePath)
  {
    if (string.IsNullOrWhiteSpace(relativePath))
    {
      return string.Empty;
    }

    var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
    var segments = relativePath.Split(separators, StringSplitOptions.RemoveEmptyEntries);
    var sanitizedSegments = segments.Select(SanitizeSegment).Where(static s => !string.IsNullOrEmpty(s)).ToArray();
    return sanitizedSegments.Length == 0 ? string.Empty : Path.Combine(sanitizedSegments);
  }

  private static string SanitizeSegment(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return string.Empty;
    }

    Span<char> buffer = stackalloc char[value.Length];
    var index = 0;
    foreach (var ch in value)
    {
      buffer[index++] = InvalidFileNameCharacters.Contains(ch) ? '_' : ch;
    }

    var sanitized = new string(buffer[..index]).Trim();
    return string.IsNullOrEmpty(sanitized) ? "_" : sanitized;
  }

  private static string? ToHex(byte[] data)
  {
    return data is { Length: > 0 } ? Convert.ToHexString(data) : null;
  }

  private static string NormalizeDirectoryPath(string path)
  {
    if (string.IsNullOrWhiteSpace(path))
    {
      throw new ArgumentException("A directory path must be provided.", nameof(path));
    }

    return Path.GetFullPath(path.Trim());
  }

  private static string ComputeFingerprint(string sourceRoot, IEnumerable<TypetreeCacheFile> files)
  {
    using var sha256 = SHA256.Create();
    var builder = new StringBuilder();
    builder.AppendLine(sourceRoot);

    foreach (var entry in files.OrderBy(static f => f.Output, StringComparer.OrdinalIgnoreCase))
    {
      builder.Append(entry.Output);
      builder.Append('|');
      builder.Append(entry.Hash);
      builder.Append('|');
      builder.Append(entry.TypeCount);
      builder.Append('|');
      builder.Append(entry.ReferenceTypeCount);
      builder.AppendLine();
    }

    var bytes = Encoding.UTF8.GetBytes(builder.ToString());
    return Convert.ToHexString(sha256.ComputeHash(bytes));
  }

  private static string ComputeFileHash(string filePath)
  {
    using var sha256 = SHA256.Create();
    using var stream = File.OpenRead(filePath);
    var hash = sha256.ComputeHash(stream);
    return Convert.ToHexString(hash);
  }
}
