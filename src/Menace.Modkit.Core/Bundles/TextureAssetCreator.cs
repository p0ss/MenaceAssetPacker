using System;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Menace.Modkit.Core.Bundles;

/// <summary>
/// Creates native Texture2D assets from image files (PNG, JPG, etc.)
/// using AssetsTools.NET. These assets load naturally via AssetBundle.LoadFromFile().
/// </summary>
public class TextureAssetCreator
{
    /// <summary>
    /// Result of texture asset creation.
    /// </summary>
    public class TextureCreationResult
    {
        public bool Success { get; set; }
        public long PathId { get; set; }
        public string? ErrorMessage { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    /// <summary>
    /// Create a native Texture2D asset from an image file.
    /// </summary>
    /// <param name="am">The AssetsManager instance.</param>
    /// <param name="afileInst">The assets file instance to add the texture to.</param>
    /// <param name="imagePath">Path to the source image file (PNG, JPG, etc.)</param>
    /// <param name="assetName">Name for the texture asset.</param>
    /// <param name="pathId">PathID to assign to the new asset.</param>
    /// <returns>Result containing success status and asset info.</returns>
    public static TextureCreationResult CreateTexture2D(
        AssetsManager am,
        AssetsFileInstance afileInst,
        string imagePath,
        string assetName,
        long pathId)
    {
        var result = new TextureCreationResult { PathId = pathId };

        try
        {
            // Load and decode the image
            using var image = Image.Load<Rgba32>(imagePath);
            result.Width = image.Width;
            result.Height = image.Height;

            // Convert to raw RGBA32 bytes (Unity expects bottom-to-top, but most images are top-to-bottom)
            var pixelData = new byte[image.Width * image.Height * 4];
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    // Flip Y coordinate for Unity's bottom-to-top texture coordinate system
                    int destY = accessor.Height - 1 - y;
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < accessor.Width; x++)
                    {
                        int destIndex = (destY * accessor.Width + x) * 4;
                        pixelData[destIndex + 0] = row[x].R;
                        pixelData[destIndex + 1] = row[x].G;
                        pixelData[destIndex + 2] = row[x].B;
                        pixelData[destIndex + 3] = row[x].A;
                    }
                }
            });

            // Create the Texture2D asset structure
            var afile = afileInst.file;

            // Find an existing Texture2D to use as template and deep copy it
            var templateField = GetTexture2DTemplate(am, afileInst);
            if (templateField == null)
            {
                result.ErrorMessage = "No existing Texture2D found to use as template";
                return result;
            }

            var texField = DeepCopyField(templateField);
            if (texField == null)
            {
                result.ErrorMessage = "Failed to copy Texture2D template";
                return result;
            }

            // Set texture properties
            SetField(texField, "m_Name", assetName);
            SetField(texField, "m_ForcedFallbackFormat", 4); // RGBA32
            SetField(texField, "m_DownscaleFallback", false);
            SetField(texField, "m_IsAlphaChannelOptional", false);
            SetField(texField, "m_Width", image.Width);
            SetField(texField, "m_Height", image.Height);
            SetField(texField, "m_CompleteImageSize", pixelData.Length);
            SetField(texField, "m_MipsStripped", 0);
            SetField(texField, "m_TextureFormat", 4); // RGBA32
            SetField(texField, "m_MipCount", 1);
            SetField(texField, "m_IsReadable", true);
            SetField(texField, "m_IsPreProcessed", false);
            SetField(texField, "m_IgnoreMipmapLimit", false);
            SetField(texField, "m_StreamingMipmaps", false);
            SetField(texField, "m_StreamingMipmapsPriority", 0);
            SetField(texField, "m_ImageCount", 1);
            SetField(texField, "m_TextureDimension", 2); // 2D

            // Texture settings
            SetNestedField(texField, "m_TextureSettings", "m_FilterMode", 1); // Bilinear
            SetNestedField(texField, "m_TextureSettings", "m_Aniso", 1);
            SetNestedField(texField, "m_TextureSettings", "m_MipBias", 0f);
            SetNestedField(texField, "m_TextureSettings", "m_WrapU", 1); // Clamp
            SetNestedField(texField, "m_TextureSettings", "m_WrapV", 1); // Clamp
            SetNestedField(texField, "m_TextureSettings", "m_WrapW", 1); // Clamp

            SetField(texField, "m_LightmapFormat", 0);
            SetField(texField, "m_ColorSpace", 1); // Linear (0 = Gamma/sRGB)

            // Platform blob - empty for now
            var platformBlob = texField["m_PlatformBlob"];
            if (platformBlob != null)
            {
                platformBlob.Children?.Clear();
            }

            // Image data
            SetField(texField, "image data", pixelData);

            // Streaming info - not streaming, so offset/size are 0
            SetNestedField(texField, "m_StreamData", "offset", 0UL);
            SetNestedField(texField, "m_StreamData", "size", 0U);
            SetNestedField(texField, "m_StreamData", "path", "");

            // Create the asset info
            var info = AssetFileInfo.Create(
                afile,
                pathId,
                (int)AssetClassID.Texture2D,
                0);

            info.SetNewData(texField);
            afile.Metadata.AddAssetInfo(info);

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Failed to create texture: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Find an existing Texture2D and get its base field to use as template.
    /// </summary>
    private static AssetTypeValueField? GetTexture2DTemplate(
        AssetsManager am,
        AssetsFileInstance afileInst)
    {
        // Try to find an existing Texture2D to use as template
        foreach (var info in afileInst.file.GetAssetsOfType(AssetClassID.Texture2D))
        {
            try
            {
                var baseField = am.GetBaseField(afileInst, info);
                if (baseField != null)
                    return baseField;
            }
            catch
            {
                // Try next one
            }
        }

        return null;
    }

    /// <summary>
    /// Create a deep copy of an AssetTypeValueField, preserving all type information.
    /// </summary>
    private static AssetTypeValueField DeepCopyField(AssetTypeValueField source)
    {
        if (source == null) return null!;

        var copy = new AssetTypeValueField
        {
            TemplateField = source.TemplateField
        };

        if (source.Value != null)
        {
            var valueType = source.Value.ValueType;
            object? rawValue = valueType switch
            {
                AssetValueType.Bool => source.Value.AsBool,
                AssetValueType.Int8 => source.Value.AsInt,
                AssetValueType.UInt8 => source.Value.AsUInt,
                AssetValueType.Int16 => source.Value.AsInt,
                AssetValueType.UInt16 => source.Value.AsUInt,
                AssetValueType.Int32 => source.Value.AsInt,
                AssetValueType.UInt32 => source.Value.AsUInt,
                AssetValueType.Int64 => source.Value.AsLong,
                AssetValueType.UInt64 => source.Value.AsULong,
                AssetValueType.Float => source.Value.AsFloat,
                AssetValueType.Double => source.Value.AsDouble,
                AssetValueType.String => source.Value.AsString,
                AssetValueType.ByteArray => source.Value.AsByteArray?.ToArray(),
                _ => source.Value.AsObject
            };
            copy.Value = new AssetTypeValue(valueType, rawValue);
        }

        if (source.Children != null && source.Children.Count > 0)
        {
            copy.Children = new List<AssetTypeValueField>(source.Children.Count);
            foreach (var child in source.Children)
            {
                copy.Children.Add(DeepCopyField(child));
            }
        }

        return copy;
    }

    private static void SetField(AssetTypeValueField root, string fieldName, object value)
    {
        var field = root[fieldName];
        if (field == null || field.IsDummy) return;

        switch (value)
        {
            case string s:
                field.AsString = s;
                break;
            case int i:
                field.AsInt = i;
                break;
            case uint u:
                field.AsUInt = u;
                break;
            case long l:
                field.AsLong = l;
                break;
            case ulong ul:
                field.AsULong = ul;
                break;
            case float f:
                field.AsFloat = f;
                break;
            case bool b:
                field.AsBool = b;
                break;
            case byte[] bytes:
                field.AsByteArray = bytes;
                break;
        }
    }

    private static void SetNestedField(AssetTypeValueField root, string parentName, string fieldName, object value)
    {
        var parent = root[parentName];
        if (parent == null || parent.IsDummy) return;

        SetField(parent, fieldName, value);
    }
}
