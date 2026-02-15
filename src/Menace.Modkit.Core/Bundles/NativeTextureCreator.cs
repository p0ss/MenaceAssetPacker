using System;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Menace.Modkit.Core.Bundles;

/// <summary>
/// Creates native Texture2D assets using raw binary manipulation.
/// Works with Unity 6 which doesn't embed type trees.
/// Uses template cloning: find an existing Texture2D, copy bytes, patch fields.
/// </summary>
public static class NativeTextureCreator
{
    /// <summary>
    /// Result of texture creation.
    /// </summary>
    public class TextureCreationResult
    {
        public bool Success { get; set; }
        public long PathId { get; set; }
        public string? ErrorMessage { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string AssetName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Texture2D template information extracted from an existing asset.
    /// </summary>
    public class Texture2DTemplate
    {
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public AssetFileInfo Info { get; set; } = null!;

        // Offsets of key fields (detected during template extraction)
        public int NameOffset { get; set; }
        public int WidthOffset { get; set; }
        public int HeightOffset { get; set; }
        public int FormatOffset { get; set; }
        public int MipCountOffset { get; set; }
        public int ImageDataOffset { get; set; }
        public int ImageDataSizeOffset { get; set; }
        public int CompleteImageSizeOffset { get; set; }

        // Original values for validation
        public int OriginalWidth { get; set; }
        public int OriginalHeight { get; set; }
        public int OriginalImageDataSize { get; set; }
    }

    /// <summary>
    /// Find a Texture2D asset to use as a template and extract field offsets.
    /// </summary>
    public static Texture2DTemplate? FindTemplate(AssetsFile afile)
    {
        var reader = afile.Reader;

        foreach (var info in afile.GetAssetsOfType(AssetClassID.Texture2D))
        {
            try
            {
                var absOffset = info.GetAbsoluteByteOffset(afile);
                reader.BaseStream.Position = absOffset;
                var bytes = reader.ReadBytes((int)info.ByteSize);

                // Try to parse the Texture2D structure
                var template = TryParseTexture2D(bytes, info);
                if (template != null)
                    return template;
            }
            catch
            {
                // Try next texture
            }
        }

        return null;
    }

    /// <summary>
    /// Try to parse a Texture2D asset and detect field offsets.
    /// Texture2D layout (Unity 2020+):
    /// - m_Name (string: 4-byte length + chars + padding)
    /// - m_ForcedFallbackFormat (int32)
    /// - m_DownscaleFallback (bool, aligned)
    /// - m_IsAlphaChannelOptional (bool, aligned)
    /// - m_Width (int32)
    /// - m_Height (int32)
    /// - m_CompleteImageSize (int32)
    /// - m_MipsStripped (int32)
    /// - m_TextureFormat (int32)
    /// - m_MipCount (int32)
    /// - ... more fields ...
    /// - image data (byte array: 4-byte size + data)
    /// </summary>
    private static Texture2DTemplate? TryParseTexture2D(byte[] bytes, AssetFileInfo info)
    {
        if (bytes.Length < 100) return null;

        try
        {
            int offset = 0;

            // m_Name (string)
            int nameLen = BitConverter.ToInt32(bytes, offset);
            if (nameLen < 0 || nameLen > 500) return null;
            int nameOffset = offset;
            offset += 4 + nameLen;
            offset = Align4(offset);

            // m_ForcedFallbackFormat (int32)
            offset += 4;

            // m_DownscaleFallback (bool, 1 byte + align)
            offset += 1;
            offset = Align4(offset);

            // m_IsAlphaChannelOptional (bool, 1 byte + align)
            offset += 1;
            offset = Align4(offset);

            // m_Width (int32)
            int widthOffset = offset;
            int width = BitConverter.ToInt32(bytes, offset);
            offset += 4;

            // m_Height (int32)
            int heightOffset = offset;
            int height = BitConverter.ToInt32(bytes, offset);
            offset += 4;

            // Validate dimensions
            if (width <= 0 || width > 16384 || height <= 0 || height > 16384)
                return null;

            // m_CompleteImageSize (int32)
            int completeImageSizeOffset = offset;
            int completeImageSize = BitConverter.ToInt32(bytes, offset);
            offset += 4;

            // m_MipsStripped (int32)
            offset += 4;

            // m_TextureFormat (int32)
            int formatOffset = offset;
            int format = BitConverter.ToInt32(bytes, offset);
            offset += 4;

            // m_MipCount (int32)
            int mipCountOffset = offset;
            offset += 4;

            // Skip remaining fixed fields until we find image data
            // Look for the image data array (will be near the end)
            int imageDataOffset = -1;
            int imageDataSizeOffset = -1;
            int imageDataSize = -1;

            // Search backwards from end for a size that matches expected pixel data
            // RGBA32 = width * height * 4 bytes
            int expectedSize = width * height * 4;

            // Scan for the image data size field
            for (int searchOffset = offset; searchOffset < bytes.Length - 8; searchOffset += 4)
            {
                int potentialSize = BitConverter.ToInt32(bytes, searchOffset);

                // Check if this could be the image data size
                // It should be followed by enough bytes to hold that data
                if (potentialSize > 0 && potentialSize <= bytes.Length - searchOffset - 4)
                {
                    // Verify the remaining bytes are approximately right
                    int remainingBytes = bytes.Length - searchOffset - 4;

                    // The image data is usually the last major field
                    // After image data, there's only m_StreamData (path string + offset + size)
                    if (remainingBytes >= potentialSize && remainingBytes < potentialSize + 200)
                    {
                        imageDataSizeOffset = searchOffset;
                        imageDataSize = potentialSize;
                        imageDataOffset = searchOffset + 4;
                        break;
                    }
                }
            }

            if (imageDataOffset < 0)
            {
                // Try alternative: look for streaming texture (image data size = 0)
                // These have m_StreamData with external reference
                for (int searchOffset = offset; searchOffset < bytes.Length - 20; searchOffset += 4)
                {
                    int potentialSize = BitConverter.ToInt32(bytes, searchOffset);
                    if (potentialSize == 0)
                    {
                        // Check if followed by StreamData (offset=0, size=0, path="")
                        // or external reference
                        imageDataSizeOffset = searchOffset;
                        imageDataSize = 0;
                        imageDataOffset = searchOffset + 4;
                        break;
                    }
                }
            }

            return new Texture2DTemplate
            {
                Bytes = bytes,
                Info = info,
                NameOffset = nameOffset,
                WidthOffset = widthOffset,
                HeightOffset = heightOffset,
                FormatOffset = formatOffset,
                MipCountOffset = mipCountOffset,
                ImageDataOffset = imageDataOffset,
                ImageDataSizeOffset = imageDataSizeOffset,
                CompleteImageSizeOffset = completeImageSizeOffset,
                OriginalWidth = width,
                OriginalHeight = height,
                OriginalImageDataSize = imageDataSize
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Create a Texture2D asset from a PNG file.
    /// </summary>
    public static TextureCreationResult CreateFromPng(
        AssetsFile afile,
        Texture2DTemplate template,
        string pngPath,
        string assetName,
        long pathId)
    {
        var result = new TextureCreationResult
        {
            PathId = pathId,
            AssetName = assetName
        };

        try
        {
            // Load and decode the PNG
            using var image = Image.Load<Rgba32>(pngPath);
            result.Width = image.Width;
            result.Height = image.Height;

            // Convert to raw RGBA32 bytes (Unity expects bottom-to-top)
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

            // Build the new Texture2D bytes
            var textureBytes = BuildTexture2DBytes(template, assetName, image.Width, image.Height, pixelData);
            if (textureBytes == null)
            {
                result.ErrorMessage = "Failed to build Texture2D bytes";
                return result;
            }

            // Create and register the asset
            var newInfo = AssetFileInfo.Create(
                afile,
                pathId,
                (int)AssetClassID.Texture2D,
                0
            );
            newInfo.SetNewData(textureBytes);
            afile.Metadata.AddAssetInfo(newInfo);

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Failed to create texture: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Build Texture2D bytes by patching the template.
    /// </summary>
    private static byte[]? BuildTexture2DBytes(
        Texture2DTemplate template,
        string name,
        int width,
        int height,
        byte[] pixelData)
    {
        // Calculate size changes
        int nameLen = name.Length;
        int namePadding = (4 - (nameLen % 4)) % 4;
        int newNameTotalLen = 4 + nameLen + namePadding;

        int origNameLen = BitConverter.ToInt32(template.Bytes, template.NameOffset);
        int origNamePadding = (4 - (origNameLen % 4)) % 4;
        int origNameTotalLen = 4 + origNameLen + origNamePadding;

        int nameSizeDiff = newNameTotalLen - origNameTotalLen;

        // Original image data size
        int origImageDataTotalLen = 4 + template.OriginalImageDataSize;
        int origImageDataPadding = (4 - (template.OriginalImageDataSize % 4)) % 4;
        origImageDataTotalLen += origImageDataPadding;

        int newImageDataTotalLen = 4 + pixelData.Length;
        int newImageDataPadding = (4 - (pixelData.Length % 4)) % 4;
        newImageDataTotalLen += newImageDataPadding;

        int imageSizeDiff = newImageDataTotalLen - origImageDataTotalLen;

        // Calculate new buffer size
        int newSize = template.Bytes.Length + nameSizeDiff + imageSizeDiff;
        var newBytes = new byte[newSize];

        int srcOffset = 0;
        int dstOffset = 0;

        // Copy and patch name
        Array.Copy(BitConverter.GetBytes(nameLen), 0, newBytes, dstOffset, 4);
        dstOffset += 4;
        srcOffset += 4;

        var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
        Array.Copy(nameBytes, 0, newBytes, dstOffset, nameLen);
        dstOffset += nameLen;
        srcOffset += origNameLen;

        // Name padding
        for (int i = 0; i < namePadding; i++)
            newBytes[dstOffset++] = 0;
        srcOffset += origNamePadding;

        // Copy fields between name and width (need to adjust width/height offsets)
        int bytesToWidth = template.WidthOffset - (template.NameOffset + origNameTotalLen);
        Array.Copy(template.Bytes, srcOffset, newBytes, dstOffset, bytesToWidth);
        srcOffset += bytesToWidth;
        dstOffset += bytesToWidth;

        // Patch width
        Array.Copy(BitConverter.GetBytes(width), 0, newBytes, dstOffset, 4);
        srcOffset += 4;
        dstOffset += 4;

        // Patch height
        Array.Copy(BitConverter.GetBytes(height), 0, newBytes, dstOffset, 4);
        srcOffset += 4;
        dstOffset += 4;

        // Patch CompleteImageSize
        Array.Copy(BitConverter.GetBytes(pixelData.Length), 0, newBytes, dstOffset, 4);
        srcOffset += 4;
        dstOffset += 4;

        // m_MipsStripped = 0
        Array.Copy(BitConverter.GetBytes(0), 0, newBytes, dstOffset, 4);
        srcOffset += 4;
        dstOffset += 4;

        // m_TextureFormat = 4 (RGBA32)
        Array.Copy(BitConverter.GetBytes(4), 0, newBytes, dstOffset, 4);
        srcOffset += 4;
        dstOffset += 4;

        // m_MipCount = 1
        Array.Copy(BitConverter.GetBytes(1), 0, newBytes, dstOffset, 4);
        srcOffset += 4;
        dstOffset += 4;

        // Copy bytes between mip count and image data size
        int adjustedImageDataSizeOffset = template.ImageDataSizeOffset + nameSizeDiff;
        int bytesToImageData = template.ImageDataSizeOffset - srcOffset + (template.NameOffset + origNameTotalLen) - (template.NameOffset + origNameTotalLen);

        // Actually, let's simplify: copy everything from current position to image data size offset
        int srcImageSizeOffset = template.ImageDataSizeOffset;
        int bytesUntilImageSize = srcImageSizeOffset - srcOffset;
        Array.Copy(template.Bytes, srcOffset, newBytes, dstOffset, bytesUntilImageSize);
        srcOffset += bytesUntilImageSize;
        dstOffset += bytesUntilImageSize;

        // Write new image data size
        Array.Copy(BitConverter.GetBytes(pixelData.Length), 0, newBytes, dstOffset, 4);
        srcOffset += 4;
        dstOffset += 4;

        // Write pixel data
        Array.Copy(pixelData, 0, newBytes, dstOffset, pixelData.Length);
        dstOffset += pixelData.Length;
        srcOffset += template.OriginalImageDataSize;

        // Pixel data padding
        for (int i = 0; i < newImageDataPadding; i++)
            newBytes[dstOffset++] = 0;
        srcOffset += origImageDataPadding;

        // Copy remaining bytes (StreamData)
        int remaining = template.Bytes.Length - srcOffset;
        if (remaining > 0)
        {
            Array.Copy(template.Bytes, srcOffset, newBytes, dstOffset, remaining);
            dstOffset += remaining;
        }

        // Zero out StreamData path if present (we're embedding the texture)
        // The StreamData is typically: path (string), offset (uint64), size (uint32)
        // We need to set path to empty string and size to 0
        // This is at the very end of the texture data

        // Trim to actual size used
        if (dstOffset < newBytes.Length)
        {
            Array.Resize(ref newBytes, dstOffset);
        }

        return newBytes;
    }

    private static int Align4(int offset) => (offset + 3) & ~3;
}
