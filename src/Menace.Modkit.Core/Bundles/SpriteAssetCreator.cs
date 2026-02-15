using System;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Menace.Modkit.Core.Bundles;

/// <summary>
/// Creates native Sprite assets that reference Texture2D assets.
/// These assets load naturally via AssetBundle.LoadFromFile().
/// </summary>
public class SpriteAssetCreator
{
    /// <summary>
    /// Result of sprite asset creation.
    /// </summary>
    public class SpriteCreationResult
    {
        public bool Success { get; set; }
        public long PathId { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Create a native Sprite asset referencing a Texture2D.
    /// </summary>
    /// <param name="am">The AssetsManager instance.</param>
    /// <param name="afileInst">The assets file instance to add the sprite to.</param>
    /// <param name="assetName">Name for the sprite asset.</param>
    /// <param name="texturePathId">PathID of the Texture2D this sprite references.</param>
    /// <param name="width">Width of the sprite in pixels.</param>
    /// <param name="height">Height of the sprite in pixels.</param>
    /// <param name="pathId">PathID to assign to the new sprite asset.</param>
    /// <param name="pixelsPerUnit">Pixels per unit (default 100).</param>
    /// <param name="pivotX">Pivot X (0-1, default 0.5 = center).</param>
    /// <param name="pivotY">Pivot Y (0-1, default 0.5 = center).</param>
    /// <returns>Result containing success status and asset info.</returns>
    public static SpriteCreationResult CreateSprite(
        AssetsManager am,
        AssetsFileInstance afileInst,
        string assetName,
        long texturePathId,
        int width,
        int height,
        long pathId,
        float pixelsPerUnit = 100f,
        float pivotX = 0.5f,
        float pivotY = 0.5f)
    {
        var result = new SpriteCreationResult { PathId = pathId };

        try
        {
            var afile = afileInst.file;

            // Find an existing Sprite to use as template and deep copy it
            var templateField = GetSpriteTemplate(am, afileInst);
            if (templateField == null)
            {
                result.ErrorMessage = "No existing Sprite assets found to use as template";
                return result;
            }

            var spriteField = DeepCopyField(templateField);
            if (spriteField == null)
            {
                result.ErrorMessage = "Failed to copy Sprite template";
                return result;
            }

            // Set sprite properties
            SetField(spriteField, "m_Name", assetName);

            // Rect (position and size within the texture)
            SetNestedField(spriteField, "m_Rect", "x", 0f);
            SetNestedField(spriteField, "m_Rect", "y", 0f);
            SetNestedField(spriteField, "m_Rect", "width", (float)width);
            SetNestedField(spriteField, "m_Rect", "height", (float)height);

            // Offset
            SetNestedField(spriteField, "m_Offset", "x", 0f);
            SetNestedField(spriteField, "m_Offset", "y", 0f);

            // Border (for 9-slice, 0 = no border)
            SetNestedField(spriteField, "m_Border", "x", 0f);
            SetNestedField(spriteField, "m_Border", "y", 0f);
            SetNestedField(spriteField, "m_Border", "z", 0f);
            SetNestedField(spriteField, "m_Border", "w", 0f);

            // Pivot
            SetNestedField(spriteField, "m_Pivot", "x", pivotX);
            SetNestedField(spriteField, "m_Pivot", "y", pivotY);

            SetField(spriteField, "m_PixelsToUnits", pixelsPerUnit);
            SetField(spriteField, "m_Extrude", 0U);
            SetField(spriteField, "m_IsPolygon", false);

            // Render data - texture reference
            var rd = spriteField["m_RD"];
            if (rd != null && !rd.IsDummy)
            {
                // Texture PPtr
                var texture = rd["texture"];
                if (texture != null && !texture.IsDummy)
                {
                    SetField(texture, "m_FileID", 0); // Same file
                    SetField(texture, "m_PathID", texturePathId);
                }

                // Alpha texture (none)
                var alphaTexture = rd["alphaTexture"];
                if (alphaTexture != null && !alphaTexture.IsDummy)
                {
                    SetField(alphaTexture, "m_FileID", 0);
                    SetField(alphaTexture, "m_PathID", 0L);
                }

                // Texture rect
                SetNestedField(rd, "textureRect", "x", 0f);
                SetNestedField(rd, "textureRect", "y", 0f);
                SetNestedField(rd, "textureRect", "width", (float)width);
                SetNestedField(rd, "textureRect", "height", (float)height);

                // Texture rect offset
                SetNestedField(rd, "textureRectOffset", "x", 0f);
                SetNestedField(rd, "textureRectOffset", "y", 0f);

                // Atlas rect offset (not using atlas)
                SetNestedField(rd, "atlasRectOffset", "x", 0f);
                SetNestedField(rd, "atlasRectOffset", "y", 0f);

                // Settings raw (packed sprite settings)
                SetField(rd, "settingsRaw", 3U); // Single sprite, packed = false, rotation = 0

                // UV transform
                SetNestedField(rd, "uvTransform", "x", 0f);
                SetNestedField(rd, "uvTransform", "y", 0f);
                SetNestedField(rd, "uvTransform", "z", 1f);
                SetNestedField(rd, "uvTransform", "w", 1f);

                SetField(rd, "downscaleMultiplier", 1f);

                // Clear secondary textures array
                var secondaryTextures = rd["secondaryTextures"];
                if (secondaryTextures != null && !secondaryTextures.IsDummy)
                {
                    secondaryTextures.Children?.Clear();
                }
            }

            // Atlas tags (empty)
            var atlasTags = spriteField["m_AtlasTags"];
            if (atlasTags != null && !atlasTags.IsDummy)
            {
                atlasTags.Children?.Clear();
            }

            // Sprite atlas (none)
            var spriteAtlas = spriteField["m_SpriteAtlas"];
            if (spriteAtlas != null && !spriteAtlas.IsDummy)
            {
                SetField(spriteAtlas, "m_FileID", 0);
                SetField(spriteAtlas, "m_PathID", 0L);
            }

            // Create the asset info
            var assetInfo = AssetFileInfo.Create(
                afile,
                pathId,
                (int)AssetClassID.Sprite,
                0);

            assetInfo.SetNewData(spriteField);
            afile.Metadata.AddAssetInfo(assetInfo);

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Failed to create sprite: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Find an existing Sprite and get its base field to use as template.
    /// </summary>
    private static AssetTypeValueField? GetSpriteTemplate(
        AssetsManager am,
        AssetsFileInstance afileInst)
    {
        foreach (var assetInfo in afileInst.file.GetAssetsOfType(AssetClassID.Sprite))
        {
            try
            {
                var baseField = am.GetBaseField(afileInst, assetInfo);
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
            case float f:
                field.AsFloat = f;
                break;
            case bool b:
                field.AsBool = b;
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
