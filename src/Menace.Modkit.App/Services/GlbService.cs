using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SharpGLTF.Schema2;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Information about a material's texture references in a GLB file.
/// </summary>
public class GlbMaterialInfo
{
    public string MaterialName { get; set; } = "";
    public string? BaseColorTextureName { get; set; }
    public string? NormalTextureName { get; set; }
    public string? MetallicRoughnessTextureName { get; set; }
    public bool HasBaseColorTexture { get; set; }
    public bool HasNormalTexture { get; set; }
    public bool HasMetallicRoughnessTexture { get; set; }
}

/// <summary>
/// Information about a linked texture - whether it's embedded or needs to be found externally.
/// </summary>
public class GlbLinkedTexture
{
    public string MaterialName { get; set; } = "";
    public string TextureType { get; set; } = ""; // "BaseColor", "Normal", "MetallicRoughness", "Effect", "Damage"
    public string ExpectedFileName { get; set; } = "";
    public string? FoundPath { get; set; }
    public bool IsEmbedded { get; set; }
    public bool IsFound => IsEmbedded || FoundPath != null;
}

/// <summary>
/// Service for parsing GLB files and managing texture links.
/// Since AssetRipper exports meshes with a generic "DefaultMaterial" and no texture references,
/// we infer texture links from the GLB filename and search for matching textures.
/// </summary>
public class GlbService
{
    private readonly string _extractedAssetsPath;
    private readonly string _texture2DPath;

    // Common prefixes that may be added to texture names but not mesh names
    private static readonly string[] CommonPrefixes = { "", "auto_", "rmc_" };

    // Suffixes to strip from mesh names to get base asset name
    private static readonly Regex LodSuffixPattern = new(@"_LOD\d+(_\d+)?$", RegexOptions.IgnoreCase);
    private static readonly string[] PartSuffixes = { "_barrel", "_tripod", "_turret", "_chassis", "_chasis", "_body", "_head", "_arm", "_leg", "_wheel", "_track", "_pack", "_mesh", "_ready", "_camera" };

    public GlbService(string extractedAssetsPath)
    {
        _extractedAssetsPath = extractedAssetsPath;
        _texture2DPath = Path.Combine(extractedAssetsPath, "Assets", "Texture2D");
    }

    /// <summary>
    /// Parse a GLB file and return information about its materials and texture references.
    /// </summary>
    public List<GlbMaterialInfo> GetMaterialInfo(string glbPath)
    {
        var materials = new List<GlbMaterialInfo>();

        try
        {
            var model = ModelRoot.Load(glbPath);

            foreach (var material in model.LogicalMaterials)
            {
                var info = new GlbMaterialInfo
                {
                    MaterialName = material.Name ?? $"Material_{materials.Count}"
                };

                var pbr = material.FindChannel("BaseColor");
                if (pbr != null)
                {
                    info.HasBaseColorTexture = pbr.Value.Texture != null;
                    if (info.HasBaseColorTexture)
                        info.BaseColorTextureName = pbr.Value.Texture?.PrimaryImage?.Name;
                }

                var normal = material.FindChannel("Normal");
                if (normal != null)
                {
                    info.HasNormalTexture = normal.Value.Texture != null;
                    if (info.HasNormalTexture)
                        info.NormalTextureName = normal.Value.Texture?.PrimaryImage?.Name;
                }

                var mr = material.FindChannel("MetallicRoughness");
                if (mr != null)
                {
                    info.HasMetallicRoughnessTexture = mr.Value.Texture != null;
                    if (info.HasMetallicRoughnessTexture)
                        info.MetallicRoughnessTextureName = mr.Value.Texture?.PrimaryImage?.Name;
                }

                materials.Add(info);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing GLB: {ex.Message}");
        }

        return materials;
    }

    /// <summary>
    /// Returns logical mesh names found in the GLB.
    /// </summary>
    public List<string> GetMeshNames(string glbPath)
    {
        try
        {
            var model = ModelRoot.Load(glbPath);
            return model.LogicalMeshes
                .Select(m => m.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Get all linked textures for a GLB, checking which are embedded vs need external files.
    /// Uses both GLB filename and material names to infer texture names.
    /// </summary>
    public List<GlbLinkedTexture> GetLinkedTextures(string glbPath)
    {
        var textures = new List<GlbLinkedTexture>();
        var materials = GetMaterialInfo(glbPath);

        // Get base name from GLB filename for texture lookup
        var glbFileName = Path.GetFileNameWithoutExtension(glbPath);
        var baseNames = GetPossibleBaseNames(glbFileName);

        // Also try deriving base names from material names (e.g., "heavy_cannon_long_marine_material" -> "heavy_cannon_long_marine", "heavy_cannon_long")
        foreach (var mat in materials)
        {
            var matBaseNames = GetBaseNamesFromMaterial(mat.MaterialName);
            foreach (var name in matBaseNames)
            {
                if (!baseNames.Contains(name))
                    baseNames.Add(name);
            }
        }

        // Check if any textures are embedded in the GLB
        bool hasEmbeddedBaseColor = materials.Any(m => m.HasBaseColorTexture);
        bool hasEmbeddedNormal = materials.Any(m => m.HasNormalTexture);
        bool hasEmbeddedMR = materials.Any(m => m.HasMetallicRoughnessTexture);

        // Use GLB filename as display name
        var displayName = glbFileName;

        // Look for BaseColor/BaseMap texture
        if (!hasEmbeddedBaseColor)
        {
            var foundPath = FindTextureByBaseNames(baseNames, new[] { "_BaseMap.png", "_MainTex.png", ".png" });
            textures.Add(new GlbLinkedTexture
            {
                MaterialName = displayName,
                TextureType = "BaseColor",
                ExpectedFileName = $"{baseNames.FirstOrDefault()}_BaseMap.png",
                FoundPath = foundPath,
                IsEmbedded = false
            });
        }
        else
        {
            var embeddedName = materials.FirstOrDefault(m => m.HasBaseColorTexture)?.BaseColorTextureName;
            textures.Add(new GlbLinkedTexture
            {
                MaterialName = displayName,
                TextureType = "BaseColor",
                ExpectedFileName = embeddedName ?? "embedded",
                IsEmbedded = true
            });
        }

        // Look for Normal texture
        if (!hasEmbeddedNormal)
        {
            var foundPath = FindTextureByBaseNames(baseNames, new[] { "_Normal.png", "_BumpMap.png" });
            if (foundPath != null)
            {
                textures.Add(new GlbLinkedTexture
                {
                    MaterialName = displayName,
                    TextureType = "Normal",
                    ExpectedFileName = $"{baseNames.FirstOrDefault()}_Normal.png",
                    FoundPath = foundPath,
                    IsEmbedded = false
                });
            }
        }
        else
        {
            var embeddedName = materials.FirstOrDefault(m => m.HasNormalTexture)?.NormalTextureName;
            textures.Add(new GlbLinkedTexture
            {
                MaterialName = displayName,
                TextureType = "Normal",
                ExpectedFileName = embeddedName ?? "embedded",
                IsEmbedded = true
            });
        }

        // Look for MaskMap/MetallicRoughness texture
        if (!hasEmbeddedMR)
        {
            var foundPath = FindTextureByBaseNames(baseNames, new[] { "_MaskMap.png", "_MetallicGlossMap.png" });
            if (foundPath != null)
            {
                textures.Add(new GlbLinkedTexture
                {
                    MaterialName = displayName,
                    TextureType = "MaskMap",
                    ExpectedFileName = $"{baseNames.FirstOrDefault()}_MaskMap.png",
                    FoundPath = foundPath,
                    IsEmbedded = false
                });
            }
        }
        else
        {
            var embeddedName = materials.FirstOrDefault(m => m.HasMetallicRoughnessTexture)?.MetallicRoughnessTextureName;
            textures.Add(new GlbLinkedTexture
            {
                MaterialName = displayName,
                TextureType = "MaskMap",
                ExpectedFileName = embeddedName ?? "embedded",
                IsEmbedded = true
            });
        }

        // Also look for additional textures common in Unity (EffectMap, Damage)
        var effectPath = FindTextureByBaseNames(baseNames, new[] { "_EffectMap.png" });
        if (effectPath != null)
        {
            textures.Add(new GlbLinkedTexture
            {
                MaterialName = displayName,
                TextureType = "EffectMap",
                ExpectedFileName = Path.GetFileName(effectPath),
                FoundPath = effectPath,
                IsEmbedded = false
            });
        }

        var damagePath = FindTextureByBaseNames(baseNames, new[] { "_Damage.png" });
        if (damagePath != null)
        {
            textures.Add(new GlbLinkedTexture
            {
                MaterialName = displayName,
                TextureType = "Damage",
                ExpectedFileName = Path.GetFileName(damagePath),
                FoundPath = damagePath,
                IsEmbedded = false
            });
        }

        return textures;
    }

    /// <summary>
    /// Get possible base names for texture lookup from a GLB filename.
    /// E.g., "grenade_launcher_barrel_LOD0" -> ["grenade_launcher_barrel", "grenade_launcher", "auto_grenade_launcher", etc.]
    /// Also handles abbreviations like "local_forces" -> "lf_"
    /// </summary>
    private List<string> GetPossibleBaseNames(string glbFileName)
    {
        var names = new List<string>();

        // Start with the filename stripped of LOD suffix
        var baseName = LodSuffixPattern.Replace(glbFileName, "");

        // Add the base name with common prefixes
        foreach (var prefix in CommonPrefixes)
        {
            names.Add(prefix + baseName);
        }

        // Also try stripping part suffixes (e.g., _barrel, _tripod)
        string strippedBase = baseName;
        foreach (var partSuffix in PartSuffixes)
        {
            if (strippedBase.EndsWith(partSuffix, StringComparison.OrdinalIgnoreCase))
            {
                strippedBase = strippedBase.Substring(0, strippedBase.Length - partSuffix.Length);
                foreach (var prefix in CommonPrefixes)
                {
                    names.Add(prefix + strippedBase);
                }
            }
        }

        // Handle abbreviations: local_forces_soldier -> lf_soldier
        var words = strippedBase.Split('_');
        if (words.Length >= 2)
        {
            // First letters of first N-1 words + last word
            // local_forces_soldier -> lf_soldier
            string abbrev = string.Concat(words.Take(words.Length - 1).Select(w => w.Length > 0 ? w[0].ToString() : ""));
            string lastWord = words[words.Length - 1];

            foreach (var prefix in CommonPrefixes)
            {
                names.Add(prefix + abbrev + "_" + lastWord);
                names.Add(prefix + abbrev.ToLower() + "_" + lastWord);
            }

            // Just abbreviation prefix (e.g., lf_)
            foreach (var prefix in CommonPrefixes)
            {
                names.Add(prefix + abbrev + "_");
                names.Add(prefix + abbrev.ToLower() + "_");
            }
        }

        // Without common mesh prefixes
        foreach (var meshPrefix in new[] { "pf_", "prefab_", "mesh_" })
        {
            if (strippedBase.StartsWith(meshPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var withoutMeshPrefix = strippedBase.Substring(meshPrefix.Length);
                foreach (var prefix in CommonPrefixes)
                {
                    names.Add(prefix + withoutMeshPrefix);
                }
            }
        }

        return names.Distinct().ToList();
    }

    /// <summary>
    /// Get possible base names from a material name.
    /// E.g., "heavy_cannon_long_marine_material" -> ["heavy_cannon_long_marine", "heavy_cannon_long"]
    /// Also handles "DefaultMaterial" (ignored) and "_mat" suffix.
    /// </summary>
    private List<string> GetBaseNamesFromMaterial(string materialName)
    {
        var names = new List<string>();

        if (string.IsNullOrWhiteSpace(materialName))
            return names;

        // Skip generic material names that don't help with texture lookup
        if (materialName.Equals("DefaultMaterial", StringComparison.OrdinalIgnoreCase) ||
            materialName.Equals("Material", StringComparison.OrdinalIgnoreCase) ||
            materialName.StartsWith("Material_", StringComparison.OrdinalIgnoreCase))
            return names;

        // Strip common material suffixes
        var baseName = materialName;
        foreach (var suffix in new[] { "_material", "_mat", "_Material", "_Mat" })
        {
            if (baseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                baseName = baseName.Substring(0, baseName.Length - suffix.Length);
                break;
            }
        }

        // Add the base name with common prefixes
        foreach (var prefix in CommonPrefixes)
        {
            names.Add(prefix + baseName);
        }

        // Also try progressively stripping the last segment (e.g., "heavy_cannon_long_marine" -> "heavy_cannon_long")
        var parts = baseName.Split('_');
        for (int i = parts.Length - 1; i >= 2; i--)
        {
            var shorterName = string.Join("_", parts.Take(i));
            foreach (var prefix in CommonPrefixes)
            {
                if (!names.Contains(prefix + shorterName))
                    names.Add(prefix + shorterName);
            }
        }

        return names;
    }

    /// <summary>
    /// Find a texture by trying multiple base names with multiple suffixes.
    /// </summary>
    private string? FindTextureByBaseNames(List<string> baseNames, string[] suffixes)
    {
        if (!Directory.Exists(_texture2DPath))
            return null;

        foreach (var baseName in baseNames)
        {
            foreach (var suffix in suffixes)
            {
                var path = Path.Combine(_texture2DPath, baseName + suffix);
                if (File.Exists(path))
                    return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Export a GLB with all linked textures embedded.
    /// </summary>
    public bool ExportPackaged(string sourceGlbPath, string outputPath)
    {
        try
        {
            var model = ModelRoot.Load(sourceGlbPath);
            var linkedTextures = GetLinkedTextures(sourceGlbPath);

            // Get the first (usually only) material
            var material = model.LogicalMaterials.FirstOrDefault();
            if (material == null)
            {
                // No material exists - save as-is
                model.SaveGLB(outputPath);
                return true;
            }

            // Find and embed BaseColor texture if not embedded
            var baseColorLink = linkedTextures.FirstOrDefault(t =>
                t.TextureType == "BaseColor" &&
                !t.IsEmbedded &&
                t.FoundPath != null);

            if (baseColorLink != null)
            {
                var imageBytes = File.ReadAllBytes(baseColorLink.FoundPath!);
                var image = model.CreateImage();
                image.Content = new SharpGLTF.Memory.MemoryImage(imageBytes);
                image.Name = Path.GetFileNameWithoutExtension(baseColorLink.FoundPath);

                // Ensure material has PBR metallic roughness and set texture
                var channel = material.FindChannel("BaseColor");
                if (channel.HasValue)
                {
                    channel.Value.SetTexture(0, image);
                }
            }

            // Find and embed Normal texture if not embedded
            var normalLink = linkedTextures.FirstOrDefault(t =>
                t.TextureType == "Normal" &&
                !t.IsEmbedded &&
                t.FoundPath != null);

            if (normalLink != null)
            {
                var imageBytes = File.ReadAllBytes(normalLink.FoundPath!);
                var image = model.CreateImage();
                image.Content = new SharpGLTF.Memory.MemoryImage(imageBytes);
                image.Name = Path.GetFileNameWithoutExtension(normalLink.FoundPath);

                var channel = material.FindChannel("Normal");
                if (channel.HasValue)
                {
                    channel.Value.SetTexture(0, image);
                }
            }

            // Find and embed MaskMap/MetallicRoughness texture if not embedded
            var mrLink = linkedTextures.FirstOrDefault(t =>
                (t.TextureType == "MaskMap" || t.TextureType == "MetallicRoughness") &&
                !t.IsEmbedded &&
                t.FoundPath != null);

            if (mrLink != null)
            {
                var imageBytes = File.ReadAllBytes(mrLink.FoundPath!);
                var image = model.CreateImage();
                image.Content = new SharpGLTF.Memory.MemoryImage(imageBytes);
                image.Name = Path.GetFileNameWithoutExtension(mrLink.FoundPath);

                var channel = material.FindChannel("MetallicRoughness");
                if (channel.HasValue)
                {
                    channel.Value.SetTexture(0, image);
                }
            }

            model.SaveGLB(outputPath);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error exporting packaged GLB: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Import a GLB (potentially edited in Blender), extracting textures back to Texture2D folder.
    /// </summary>
    public bool ImportAndExtractTextures(string importedGlbPath, string originalGlbPath)
    {
        try
        {
            var model = ModelRoot.Load(importedGlbPath);

            foreach (var image in model.LogicalImages)
            {
                if (image.Content.IsEmpty)
                    continue;

                var imageName = image.Name;
                if (string.IsNullOrEmpty(imageName))
                    continue;

                // Determine output path
                var outputPath = Path.Combine(_texture2DPath, imageName + ".png");

                // Save the image
                var imageData = image.Content.Content.ToArray();
                File.WriteAllBytes(outputPath, imageData);
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error importing GLB: {ex.Message}");
            return false;
        }
    }

}
