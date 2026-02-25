using System;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Menace.Modkit.Core.Bundles;

/// <summary>
/// Creates native Sprite assets using raw binary manipulation.
/// Works with Unity 6 which doesn't embed type trees.
/// Uses template cloning: find an existing Sprite, copy bytes, patch fields.
/// </summary>
public static class NativeSpriteCreator
{
    /// <summary>
    /// Result of sprite creation.
    /// </summary>
    public class SpriteCreationResult
    {
        public bool Success { get; set; }
        public long PathId { get; set; }
        public string? ErrorMessage { get; set; }
        public string AssetName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Sprite template information extracted from an existing asset.
    /// </summary>
    public class SpriteTemplate
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public AssetFileInfo Info { get; set; } = null!;

        // Offsets of key fields
        public int NameOffset { get; set; }
        public int RectOffset { get; set; }  // x, y, width, height (4 floats)
        public int OffsetOffset { get; set; } // offset x, y (2 floats)
        public int BorderOffset { get; set; } // border x, y, z, w (4 floats)
        public int PivotOffset { get; set; }  // pivot x, y (2 floats)
        public int PixelsToUnitsOffset { get; set; }
        public int TexturePPtrOffset { get; set; }  // FileID + PathID

        // Original values for validation
        public int OriginalNameLen { get; set; }
    }

    /// <summary>
    /// Find a Sprite asset to use as a template and extract field offsets.
    /// Prefers simple sprites (small byte size, likely non-atlased).
    /// </summary>
    public static SpriteTemplate? FindTemplate(AssetsFile afile) => FindTemplate(afile, 0, 0);

    /// <summary>
    /// Find a Sprite asset to use as a template, preferring one that matches target dimensions.
    /// </summary>
    public static SpriteTemplate? FindTemplate(AssetsFile afile, int targetWidth, int targetHeight)
    {
        var reader = afile.Reader;
        SpriteTemplate? bestTemplate = null;
        int bestScore = int.MaxValue;

        foreach (var info in afile.GetAssetsOfType(AssetClassID.Sprite))
        {
            try
            {
                var absOffset = info.GetAbsoluteByteOffset(afile);
                reader.BaseStream.Position = absOffset;
                var bytes = reader.ReadBytes((int)info.ByteSize);

                var template = TryParseSprite(bytes, info);
                if (template != null)
                {
                    float w = BitConverter.ToSingle(template.Bytes, template.RectOffset + 8);
                    float h = BitConverter.ToSingle(template.Bytes, template.RectOffset + 12);

                    int score;
                    if (targetWidth > 0 && targetHeight > 0)
                    {
                        // If target dimensions specified, prioritize exact or close match
                        int dimDiff = Math.Abs((int)w - targetWidth) + Math.Abs((int)h - targetHeight);
                        if (dimDiff == 0)
                        {
                            // Exact match - use immediately
                            Console.WriteLine($"[NativeSpriteCreator] Found exact match template: {w}x{h}");
                            return template;
                        }
                        score = dimDiff * 10 + bytes.Length / 100;
                    }
                    else
                    {
                        // No target specified - prefer sprites around 100-200px
                        int sizePenalty = Math.Abs((int)w - 128) + Math.Abs((int)h - 128);
                        int bytePenalty = bytes.Length / 100;
                        score = sizePenalty + bytePenalty;
                    }

                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestTemplate = template;
                    }
                }
            }
            catch
            {
                // Try next sprite
            }
        }

        if (bestTemplate != null)
        {
            float w = BitConverter.ToSingle(bestTemplate.Bytes, bestTemplate.RectOffset + 8);
            float h = BitConverter.ToSingle(bestTemplate.Bytes, bestTemplate.RectOffset + 12);
            Console.WriteLine($"[NativeSpriteCreator] Selected template sprite: {w}x{h}, {bestTemplate.Bytes.Length} bytes (target: {targetWidth}x{targetHeight})");
        }

        return bestTemplate;
    }

    /// <summary>
    /// Try to parse a Sprite asset and detect field offsets.
    /// Sprite layout (Unity 2020+):
    /// - m_Name (string)
    /// - m_Rect (Rect: x, y, width, height - 4 floats)
    /// - m_Offset (Vector2: x, y - 2 floats)
    /// - m_Border (Vector4: x, y, z, w - 4 floats)
    /// - m_PixelsToUnits (float)
    /// - m_Pivot (Vector2: x, y - 2 floats)
    /// - m_Extrude (uint32)
    /// - m_IsPolygon (bool, aligned)
    /// - m_RenderDataKey (pair of GUID + second ID)
    /// - m_AtlasTags (string array)
    /// - m_SpriteAtlas (PPtr)
    /// - m_RD (SpriteRenderData):
    ///   - texture (PPtr<Texture2D>)
    ///   - alphaTexture (PPtr<Texture2D>)
    ///   - ... more fields
    /// </summary>
    private static SpriteTemplate? TryParseSprite(byte[] bytes, AssetFileInfo info)
    {
        if (bytes.Length < 100) return null;

        try
        {
            int offset = 0;

            // m_Name (string)
            int nameLen = BitConverter.ToInt32(bytes, offset);
            if (nameLen < 0 || nameLen > 500) return null;
            int nameOffset = offset;
            int origNameLen = nameLen;
            offset += 4 + nameLen;
            offset = Align4(offset);

            // m_Rect (4 floats = 16 bytes)
            int rectOffset = offset;
            float rectX = BitConverter.ToSingle(bytes, offset);
            float rectY = BitConverter.ToSingle(bytes, offset + 4);
            float rectW = BitConverter.ToSingle(bytes, offset + 8);
            float rectH = BitConverter.ToSingle(bytes, offset + 12);
            offset += 16;

            // Validate rect (width and height should be positive)
            if (rectW <= 0 || rectH <= 0 || rectW > 16384 || rectH > 16384)
                return null;

            // m_Offset (2 floats = 8 bytes)
            int offsetOffset = offset;
            offset += 8;

            // m_Border (4 floats = 16 bytes)
            int borderOffset = offset;
            offset += 16;

            // m_PixelsToUnits (float = 4 bytes)
            int pixelsToUnitsOffset = offset;
            float ppu = BitConverter.ToSingle(bytes, offset);
            offset += 4;

            // Validate PPU (should be positive and reasonable)
            if (ppu <= 0 || ppu > 10000)
                return null;

            // m_Pivot (2 floats = 8 bytes)
            int pivotOffset = offset;
            offset += 8;

            // m_Extrude (uint32 = 4 bytes)
            offset += 4;

            // m_IsPolygon (bool = 1 byte + align)
            offset += 1;
            offset = Align4(offset);

            // m_RenderDataKey (GUID = 16 bytes + second = 8 bytes)
            // This is pair<GUID, long> in Unity
            offset += 16 + 8;

            // m_AtlasTags (string array)
            // Array size (4 bytes) followed by strings
            int atlasTagsCount = BitConverter.ToInt32(bytes, offset);
            offset += 4;
            for (int i = 0; i < atlasTagsCount && offset < bytes.Length - 4; i++)
            {
                int tagLen = BitConverter.ToInt32(bytes, offset);
                offset += 4 + tagLen;
                offset = Align4(offset);
            }

            // m_SpriteAtlas (PPtr = 4 + 8 bytes)
            offset += 12;

            // m_RD (SpriteRenderData)
            // First field is texture PPtr
            int texturePPtrOffset = offset;

            // Validate: FileID should be 0 or small positive number
            int fileId = BitConverter.ToInt32(bytes, offset);
            if (fileId < 0 || fileId > 100)
                return null;

            return new SpriteTemplate
            {
                Bytes = bytes,
                Info = info,
                NameOffset = nameOffset,
                RectOffset = rectOffset,
                OffsetOffset = offsetOffset,
                BorderOffset = borderOffset,
                PixelsToUnitsOffset = pixelsToUnitsOffset,
                PivotOffset = pivotOffset,
                TexturePPtrOffset = texturePPtrOffset,
                OriginalNameLen = origNameLen
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Create a Sprite asset referencing a Texture2D.
    /// </summary>
    public static SpriteCreationResult CreateSprite(
        AssetsFile afile,
        SpriteTemplate template,
        string spriteName,
        long texturePathId,
        int width,
        int height,
        long spritePathId,
        float pixelsPerUnit = 100f,
        float pivotX = 0.5f,
        float pivotY = 0.5f)
    {
        var result = new SpriteCreationResult
        {
            PathId = spritePathId,
            AssetName = spriteName
        };

        try
        {
            var spriteBytes = BuildSpriteBytes(
                template, spriteName, texturePathId, width, height, pixelsPerUnit, pivotX, pivotY);

            if (spriteBytes == null)
            {
                result.ErrorMessage = "Failed to build Sprite bytes";
                return result;
            }

            // Unity 6 doesn't have embedded type trees, so AssetFileInfo.Create returns null.
            // Instead, create the info manually based on the template's info.
            var newInfo = new AssetFileInfo
            {
                PathId = spritePathId,
                TypeIdOrIndex = template.Info.TypeIdOrIndex,
                TypeId = template.Info.TypeId,
                ScriptTypeIndex = template.Info.ScriptTypeIndex,
                Stripped = template.Info.Stripped
            };
            newInfo.SetNewData(spriteBytes);
            afile.Metadata.AddAssetInfo(newInfo);

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Failed to create sprite: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Build Sprite bytes by patching the template.
    /// </summary>
    private static byte[]? BuildSpriteBytes(
        SpriteTemplate template,
        string name,
        long texturePathId,
        int width,
        int height,
        float pixelsPerUnit,
        float pivotX,
        float pivotY)
    {
        // Calculate name size change
        int nameLen = name.Length;
        int namePadding = (4 - (nameLen % 4)) % 4;
        int newNameTotalLen = 4 + nameLen + namePadding;

        int origNameLen = template.OriginalNameLen;
        int origNamePadding = (4 - (origNameLen % 4)) % 4;
        int origNameTotalLen = 4 + origNameLen + origNamePadding;

        int nameSizeDiff = newNameTotalLen - origNameTotalLen;

        // New buffer
        int newSize = template.Bytes.Length + nameSizeDiff;
        var newBytes = new byte[newSize];

        int srcOffset = 0;
        int dstOffset = 0;

        // Write new name
        Array.Copy(BitConverter.GetBytes(nameLen), 0, newBytes, dstOffset, 4);
        dstOffset += 4;
        srcOffset += 4;

        var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
        Array.Copy(nameBytes, 0, newBytes, dstOffset, nameLen);
        dstOffset += nameLen;
        srcOffset += origNameLen;

        for (int i = 0; i < namePadding; i++)
            newBytes[dstOffset++] = 0;
        srcOffset += origNamePadding;

        // Write rect (x=0, y=0, width, height)
        Array.Copy(BitConverter.GetBytes(0f), 0, newBytes, dstOffset, 4);      // x
        Array.Copy(BitConverter.GetBytes(0f), 0, newBytes, dstOffset + 4, 4);  // y
        Array.Copy(BitConverter.GetBytes((float)width), 0, newBytes, dstOffset + 8, 4);   // width
        Array.Copy(BitConverter.GetBytes((float)height), 0, newBytes, dstOffset + 12, 4); // height
        srcOffset += 16;
        dstOffset += 16;

        // Write offset (0, 0)
        Array.Copy(BitConverter.GetBytes(0f), 0, newBytes, dstOffset, 4);
        Array.Copy(BitConverter.GetBytes(0f), 0, newBytes, dstOffset + 4, 4);
        srcOffset += 8;
        dstOffset += 8;

        // Write border (0, 0, 0, 0)
        Array.Copy(BitConverter.GetBytes(0f), 0, newBytes, dstOffset, 4);
        Array.Copy(BitConverter.GetBytes(0f), 0, newBytes, dstOffset + 4, 4);
        Array.Copy(BitConverter.GetBytes(0f), 0, newBytes, dstOffset + 8, 4);
        Array.Copy(BitConverter.GetBytes(0f), 0, newBytes, dstOffset + 12, 4);
        srcOffset += 16;
        dstOffset += 16;

        // Write pixelsToUnits
        Array.Copy(BitConverter.GetBytes(pixelsPerUnit), 0, newBytes, dstOffset, 4);
        srcOffset += 4;
        dstOffset += 4;

        // Write pivot
        Array.Copy(BitConverter.GetBytes(pivotX), 0, newBytes, dstOffset, 4);
        Array.Copy(BitConverter.GetBytes(pivotY), 0, newBytes, dstOffset + 4, 4);
        srcOffset += 8;
        dstOffset += 8;

        // Copy bytes until texture PPtr (extrude, isPolygon, renderDataKey, atlasTags, spriteAtlas)
        int adjustedTexturePPtrOffset = template.TexturePPtrOffset + nameSizeDiff;
        int bytesToTexturePPtr = template.TexturePPtrOffset - srcOffset;
        Array.Copy(template.Bytes, srcOffset, newBytes, dstOffset, bytesToTexturePPtr);
        srcOffset += bytesToTexturePPtr;
        dstOffset += bytesToTexturePPtr;

        // Write texture PPtr (FileID = 0 = same file, PathID = texture)
        Array.Copy(BitConverter.GetBytes(0), 0, newBytes, dstOffset, 4);         // FileID
        Array.Copy(BitConverter.GetBytes(texturePathId), 0, newBytes, dstOffset + 4, 8); // PathID
        srcOffset += 12;
        dstOffset += 12;

        // Copy alphaTexture PPtr (set PathID to 0 = no alpha texture)
        Array.Copy(BitConverter.GetBytes(0), 0, newBytes, dstOffset, 4);  // FileID
        Array.Copy(BitConverter.GetBytes(0L), 0, newBytes, dstOffset + 4, 8); // PathID = 0 (no alpha tex)
        srcOffset += 12;
        dstOffset += 12;

        // Copy the rest of the sprite data from template
        int remaining = template.Bytes.Length - srcOffset;
        if (remaining > 0)
        {
            Array.Copy(template.Bytes, srcOffset, newBytes, dstOffset, remaining);
            dstOffset += remaining;
        }

        // Trim if needed
        if (dstOffset < newBytes.Length)
        {
            Array.Resize(ref newBytes, dstOffset);
        }

        // Now patch textureRect inside m_RD by searching for it
        // After alphaTexture PPtr, we have: secondaryTextures array, then textureRect
        // The textureRect should contain the template's original dimensions
        // Find and replace with our dimensions
        PatchTextureRectInRD(newBytes, template, width, height);

        return newBytes;
    }

    /// <summary>
    /// Patch the textureRect inside m_RD to match our texture dimensions.
    /// Searches for the template's original rect dimensions and replaces with new ones.
    /// </summary>
    private static void PatchTextureRectInRD(byte[] spriteBytes, SpriteTemplate template, int newWidth, int newHeight)
    {
        // Get the template's original rect dimensions from RectOffset
        float origWidth = BitConverter.ToSingle(template.Bytes, template.RectOffset + 8);
        float origHeight = BitConverter.ToSingle(template.Bytes, template.RectOffset + 12);

        // Search for this rect pattern (x=0, y=0, width, height) in the sprite data
        // This should appear in m_RD.textureRect
        // The pattern is: 0f, 0f, origWidth, origHeight (16 bytes)
        byte[] searchPattern = new byte[16];
        Array.Copy(BitConverter.GetBytes(0f), 0, searchPattern, 0, 4);
        Array.Copy(BitConverter.GetBytes(0f), 0, searchPattern, 4, 4);
        Array.Copy(BitConverter.GetBytes(origWidth), 0, searchPattern, 8, 4);
        Array.Copy(BitConverter.GetBytes(origHeight), 0, searchPattern, 12, 4);

        // Search starting from after the main rect (which we already patched at RectOffset)
        // The textureRect in m_RD should be later in the byte stream
        int searchStart = template.RectOffset + 100; // Skip past the header area

        for (int i = searchStart; i < spriteBytes.Length - 16; i += 4)
        {
            bool match = true;
            for (int j = 0; j < 16; j++)
            {
                if (spriteBytes[i + j] != searchPattern[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                // Found it - patch with new dimensions
                Array.Copy(BitConverter.GetBytes(0f), 0, spriteBytes, i, 4);
                Array.Copy(BitConverter.GetBytes(0f), 0, spriteBytes, i + 4, 4);
                Array.Copy(BitConverter.GetBytes((float)newWidth), 0, spriteBytes, i + 8, 4);
                Array.Copy(BitConverter.GetBytes((float)newHeight), 0, spriteBytes, i + 12, 4);
                return; // Only patch first occurrence
            }
        }

        // If exact match not found, try a more lenient search (just width/height pattern)
        // Some sprites may have non-zero x,y in textureRect
        for (int i = searchStart; i < spriteBytes.Length - 8; i += 4)
        {
            float w = BitConverter.ToSingle(spriteBytes, i);
            float h = BitConverter.ToSingle(spriteBytes, i + 4);

            // Check if this looks like our template dimensions (within tolerance)
            if (Math.Abs(w - origWidth) < 0.5f && Math.Abs(h - origHeight) < 0.5f)
            {
                // Found it - patch with new dimensions
                Array.Copy(BitConverter.GetBytes((float)newWidth), 0, spriteBytes, i, 4);
                Array.Copy(BitConverter.GetBytes((float)newHeight), 0, spriteBytes, i + 4, 4);
                return;
            }
        }
    }

    /// <summary>
    /// Update an existing Sprite's texture reference to point to a new texture PathId.
    /// Uses pattern matching to find and replace PPtr references.
    /// </summary>
    public static bool UpdateSpriteTextureReference(
        AssetsFile afile,
        AssetFileInfo spriteAsset,
        long oldTexturePathId,
        long newTexturePathId,
        int newWidth,
        int newHeight)
    {
        try
        {
            // Read existing sprite bytes
            var reader = afile.Reader;
            reader.BaseStream.Position = spriteAsset.GetAbsoluteByteOffset(afile);
            var bytes = reader.ReadBytes((int)spriteAsset.ByteSize);

            // Create a copy of the bytes to modify
            var newBytes = (byte[])bytes.Clone();
            bool foundAndPatched = false;

            // Search for and patch all occurrences of the old texture PathId
            // PPtr format: FileId (4 bytes) + PathId (8 bytes)
            for (int i = 0; i < newBytes.Length - 12; i += 4) // 4-byte aligned
            {
                int fileId = BitConverter.ToInt32(newBytes, i);
                long pathId = BitConverter.ToInt64(newBytes, i + 4);

                // Match: FileId=0 (same file) and PathId matches old texture
                if (fileId == 0 && pathId == oldTexturePathId)
                {
                    // Patch to new PathId
                    Array.Copy(BitConverter.GetBytes(newTexturePathId), 0, newBytes, i + 4, 8);
                    foundAndPatched = true;
                    // Continue searching in case there are multiple references
                }
            }

            if (!foundAndPatched)
                return false;

            // Try to update rect dimensions if we can parse the sprite
            var template = TryParseSprite(bytes, spriteAsset);
            if (template != null)
            {
                // Rect is at template.RectOffset: x(4), y(4), width(4), height(4)
                Array.Copy(BitConverter.GetBytes(0f), 0, newBytes, template.RectOffset, 4);      // x = 0
                Array.Copy(BitConverter.GetBytes(0f), 0, newBytes, template.RectOffset + 4, 4);  // y = 0
                Array.Copy(BitConverter.GetBytes((float)newWidth), 0, newBytes, template.RectOffset + 8, 4);
                Array.Copy(BitConverter.GetBytes((float)newHeight), 0, newBytes, template.RectOffset + 12, 4);
            }

            // Apply the changes
            spriteAsset.SetNewData(newBytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Find all Sprites that reference a given texture PathId.
    /// Uses pattern matching to find PPtr references rather than full parsing.
    /// </summary>
    public static List<AssetFileInfo> FindSpritesReferencingTexture(AssetsFile afile, long texturePathId)
    {
        var result = new List<AssetFileInfo>();
        var reader = afile.Reader;

        // PPtr pattern: FileId (4 bytes, typically 0 for same file) + PathId (8 bytes)
        var pathIdBytes = BitConverter.GetBytes(texturePathId);

        foreach (var info in afile.GetAssetsOfType(AssetClassID.Sprite))
        {
            try
            {
                reader.BaseStream.Position = info.GetAbsoluteByteOffset(afile);
                var bytes = reader.ReadBytes((int)info.ByteSize);

                // Search for the PathId pattern in the sprite bytes
                // The texture PPtr is FileId(4) + PathId(8), FileId is typically 0 for same-file refs
                for (int i = 0; i < bytes.Length - 12; i += 4) // 4-byte aligned
                {
                    int fileId = BitConverter.ToInt32(bytes, i);
                    long pathId = BitConverter.ToInt64(bytes, i + 4);

                    // Match: FileId=0 (same file) and PathId matches target
                    if (fileId == 0 && pathId == texturePathId)
                    {
                        result.Add(info);
                        break; // Found a match, no need to continue searching this sprite
                    }
                }
            }
            catch
            {
                // Skip problematic sprites
            }
        }

        return result;
    }

    private static int Align4(int offset) => (offset + 3) & ~3;
}
