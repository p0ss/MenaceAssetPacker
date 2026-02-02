using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Menace.Modkit.Core.Bundles;

/// <summary>
/// Converts PNG/JPG replacement images into Texture2D assets inside a Unity asset bundle.
/// This is a placeholder for the full texture bundling pipeline.
/// </summary>
public class TextureBundler
{
    /// <summary>
    /// Information about a texture to be bundled.
    /// </summary>
    public class TextureEntry
    {
        public string AssetName { get; set; } = string.Empty;
        public string SourceFilePath { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
    }

    /// <summary>
    /// Collect texture replacement entries from modpack asset directories.
    /// </summary>
    public static List<TextureEntry> CollectTextureEntries(string assetsDir)
    {
        var entries = new List<TextureEntry>();

        if (!Directory.Exists(assetsDir))
            return entries;

        var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".tga", ".bmp"
        };

        foreach (var file in Directory.GetFiles(assetsDir, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file);
            if (!imageExtensions.Contains(ext))
                continue;

            entries.Add(new TextureEntry
            {
                AssetName = Path.GetFileNameWithoutExtension(file),
                SourceFilePath = file
            });
        }

        return entries;
    }

    /// <summary>
    /// Manifest entry describing a deployed texture file.
    /// </summary>
    private class TextureManifestEntry
    {
        public string FileName { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public string OriginalFormat { get; set; } = string.Empty;
    }

    /// <summary>
    /// Create a texture bundle by copying source images into a textures/ subdirectory
    /// and writing a textures.json manifest. The runtime loads these via
    /// AssetInjectionPatches.LoadTextureFromFile() using ImageConversion.LoadImage().
    /// </summary>
    public static bool CreateTextureBundle(
        List<TextureEntry> entries,
        string outputPath,
        string unityVersion)
    {
        if (entries.Count == 0)
            return false;

        var dir = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrEmpty(dir))
            return false;

        var texturesDir = Path.Combine(dir, "textures");
        Directory.CreateDirectory(texturesDir);

        var manifest = new Dictionary<string, TextureManifestEntry>();
        int written = 0;

        foreach (var entry in entries)
        {
            if (!File.Exists(entry.SourceFilePath))
                continue;

            var ext = Path.GetExtension(entry.SourceFilePath);
            var destFileName = $"{entry.AssetName}{ext}";
            var destPath = Path.Combine(texturesDir, destFileName);

            File.Copy(entry.SourceFilePath, destPath, true);

            manifest[entry.AssetName] = new TextureManifestEntry
            {
                FileName = destFileName,
                Width = entry.Width,
                Height = entry.Height,
                OriginalFormat = ext.TrimStart('.').ToUpperInvariant()
            };

            written++;
        }

        if (written == 0)
            return false;

        var manifestPath = Path.Combine(dir, "textures.json");
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(manifestPath, json);

        return true;
    }
}
