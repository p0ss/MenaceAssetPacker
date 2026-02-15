using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Menace.Modkit.Core.Bundles;

/// <summary>
/// Collects audio files from modpack asset directories for native asset creation.
/// Audio assets are created directly in resources.assets and indexed in ResourceManager
/// for discovery via Resources.Load().
/// </summary>
public class AudioBundler
{
    /// <summary>
    /// Information about an audio file to be bundled as a native AudioClip.
    /// </summary>
    public class AudioEntry
    {
        /// <summary>
        /// Name for the AudioClip asset (without extension).
        /// </summary>
        public string AssetName { get; set; } = string.Empty;

        /// <summary>
        /// Full path to the source WAV file.
        /// </summary>
        public string SourceFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Optional resource path for ResourceManager indexing.
        /// If not set, will be derived from AssetName.
        /// </summary>
        public string? ResourcePath { get; set; }
    }

    /// <summary>
    /// Result of audio asset collection.
    /// </summary>
    public class CollectResult
    {
        public List<AudioEntry> Entries { get; } = new();
        public List<string> Warnings { get; } = new();
        public int SkippedCount { get; set; }
    }

    /// <summary>
    /// Collect audio files from a modpack's assets directory.
    /// Currently supports WAV files only (PCM format).
    /// </summary>
    /// <param name="assetsDir">Path to the modpack's assets/ directory.</param>
    /// <param name="resourcePathPrefix">Prefix for ResourceManager paths (e.g., "audio/sfx").</param>
    /// <returns>Collection result with audio entries and any warnings.</returns>
    public static CollectResult CollectAudioEntries(string assetsDir, string resourcePathPrefix = "audio")
    {
        var result = new CollectResult();

        if (!Directory.Exists(assetsDir))
            return result;

        // Supported audio extensions
        // WAV is the only format we can parse and convert to native AudioClip
        var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".wav"
        };

        // Unsupported formats that we should warn about
        var unsupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".ogg", ".mp3", ".aif", ".aiff", ".flac"
        };

        foreach (var file in Directory.GetFiles(assetsDir, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file);

            if (supportedExtensions.Contains(ext))
            {
                var assetName = Path.GetFileNameWithoutExtension(file);
                var relativePath = Path.GetRelativePath(assetsDir, file);
                var relativeDir = Path.GetDirectoryName(relativePath) ?? "";

                // Build resource path (e.g., "audio/sfx/laser_fire")
                var resourcePath = string.IsNullOrEmpty(relativeDir)
                    ? $"{resourcePathPrefix}/{assetName}"
                    : $"{resourcePathPrefix}/{relativeDir.Replace('\\', '/')}/{assetName}";

                result.Entries.Add(new AudioEntry
                {
                    AssetName = assetName,
                    SourceFilePath = file,
                    ResourcePath = resourcePath
                });
            }
            else if (unsupportedExtensions.Contains(ext))
            {
                result.Warnings.Add($"Unsupported audio format (convert to WAV): {Path.GetFileName(file)}");
                result.SkippedCount++;
            }
        }

        return result;
    }

    /// <summary>
    /// Collect audio entries from multiple modpack directories.
    /// </summary>
    /// <param name="modpackAssetsDirs">List of modpack assets/ directories.</param>
    /// <param name="resourcePathPrefix">Prefix for ResourceManager paths.</param>
    /// <returns>Combined collection result.</returns>
    public static CollectResult CollectAudioEntriesFromModpacks(
        IEnumerable<string> modpackAssetsDirs,
        string resourcePathPrefix = "audio")
    {
        var combined = new CollectResult();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assetsDir in modpackAssetsDirs)
        {
            var result = CollectAudioEntries(assetsDir, resourcePathPrefix);

            // Add entries, avoiding duplicates (last-wins if same name)
            foreach (var entry in result.Entries)
            {
                if (seenNames.Contains(entry.AssetName))
                {
                    // Remove previous entry with same name (last-wins semantics)
                    combined.Entries.RemoveAll(e =>
                        string.Equals(e.AssetName, entry.AssetName, StringComparison.OrdinalIgnoreCase));
                }
                seenNames.Add(entry.AssetName);
                combined.Entries.Add(entry);
            }

            combined.Warnings.AddRange(result.Warnings);
            combined.SkippedCount += result.SkippedCount;
        }

        return combined;
    }

    /// <summary>
    /// Validate a WAV file can be processed.
    /// </summary>
    /// <param name="wavPath">Path to the WAV file.</param>
    /// <returns>True if the file is a valid PCM WAV.</returns>
    public static bool ValidateWavFile(string wavPath)
    {
        if (!File.Exists(wavPath))
            return false;

        var wavData = AudioAssetCreator.ParseWavFile(wavPath);
        return wavData != null;
    }
}
