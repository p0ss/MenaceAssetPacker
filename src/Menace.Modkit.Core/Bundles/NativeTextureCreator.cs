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
        public int ColorSpaceOffset { get; set; } = -1; // -1 if not found
        public int ImageDataOffset { get; set; }
        public int ImageDataSizeOffset { get; set; }
        public int CompleteImageSizeOffset { get; set; }

        // Original values for validation
        public int OriginalWidth { get; set; }
        public int OriginalHeight { get; set; }
        public int OriginalImageDataSize { get; set; }
        public int OriginalColorSpace { get; set; } = -1;
    }

    /// <summary>
    /// Find a Texture2D asset to use as a template and extract field offsets.
    /// Returns diagnostic info via out parameter.
    /// </summary>
    public static Texture2DTemplate? FindTemplate(AssetsFile afile, out string diagnostics)
    {
        var reader = afile.Reader;
        var diag = new System.Text.StringBuilder();

        // Count assets by TypeId 28 (Texture2D)
        var texture2dAssets = afile.AssetInfos.Where(i => i.TypeId == 28).ToList();
        diag.AppendLine($"TypeId 28 (Texture2D) assets: {texture2dAssets.Count}");

        // Also try GetAssetsOfType
        var byClassId = afile.GetAssetsOfType(AssetClassID.Texture2D).ToList();
        diag.AppendLine($"GetAssetsOfType(Texture2D): {byClassId.Count}");

        // Use whichever found assets
        var assetsToCheck = texture2dAssets.Count > 0 ? texture2dAssets : byClassId;

        int checked_count = 0;
        int parse_failures = 0;

        foreach (var info in assetsToCheck)
        {
            checked_count++;
            try
            {
                var absOffset = info.GetAbsoluteByteOffset(afile);
                reader.BaseStream.Position = absOffset;
                var bytes = reader.ReadBytes((int)info.ByteSize);

                // Try to parse the Texture2D structure
                var template = TryParseTexture2D(bytes, info, out var parseLog);
                diag.AppendLine($"PathId={info.PathId}, Size={info.ByteSize}:");
                diag.AppendLine(parseLog);

                if (template != null)
                {
                    diag.AppendLine($"SUCCESS: Found template {template.OriginalWidth}x{template.OriginalHeight}");
                    diagnostics = diag.ToString();
                    return template;
                }
                parse_failures++;
            }
            catch (Exception ex)
            {
                diag.AppendLine($"Exception on PathId {info.PathId}: {ex.Message}");
            }

            // Only check first 5 to get detailed logs
            if (checked_count >= 5) break;
        }

        diag.AppendLine($"Checked {checked_count} assets, {parse_failures} parse failures");
        diagnostics = diag.ToString();
        return null;
    }

    /// <summary>
    /// Find a Texture2D asset to use as a template (convenience overload).
    /// </summary>
    public static Texture2DTemplate? FindTemplate(AssetsFile afile)
    {
        return FindTemplate(afile, out _);
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
        return TryParseTexture2D(bytes, info, out _);
    }

    private static Texture2DTemplate? TryParseTexture2D(byte[] bytes, AssetFileInfo info, out string parseLog)
    {
        var log = new System.Text.StringBuilder();
        log.AppendLine($"Parsing {bytes.Length} bytes for PathId {info.PathId}");

        if (bytes.Length < 100)
        {
            parseLog = log.Append("Too small (<100 bytes)").ToString();
            return null;
        }

        try
        {
            int offset = 0;

            // m_Name (string)
            int nameLen = BitConverter.ToInt32(bytes, offset);
            log.AppendLine($"  nameLen={nameLen} at offset {offset}");
            if (nameLen < 0 || nameLen > 500)
            {
                parseLog = log.Append($"  FAIL: nameLen out of range").ToString();
                return null;
            }

            int nameOffset = offset;
            string name = nameLen > 0 ? System.Text.Encoding.UTF8.GetString(bytes, offset + 4, Math.Min(nameLen, 50)) : "";
            log.AppendLine($"  name='{name}'");
            offset += 4 + nameLen;
            offset = Align4(offset);

            // Unity 6 layout: NO booleans between ForcedFallbackFormat and Width
            // Layout: Name -> ForcedFallbackFormat(4) -> Width(4) -> Height(4) -> CompleteImageSize(4) -> ...

            // m_ForcedFallbackFormat (int32)
            int forcedFallback = BitConverter.ToInt32(bytes, offset);
            log.AppendLine($"  @{offset}: m_ForcedFallbackFormat={forcedFallback}");
            offset += 4;

            // m_Width (int32) - immediately follows ForcedFallbackFormat in Unity 6
            int widthOffset = offset;
            int width = BitConverter.ToInt32(bytes, offset);
            log.AppendLine($"  @{offset}: m_Width={width}");
            offset += 4;

            // m_Height (int32)
            int heightOffset = offset;
            int height = BitConverter.ToInt32(bytes, offset);
            log.AppendLine($"  @{offset}: m_Height={height}");
            offset += 4;

            // Validate dimensions
            if (width <= 0 || width > 16384 || height <= 0 || height > 16384)
            {
                parseLog = log.Append($"  FAIL: dimensions invalid ({width}x{height})").ToString();
                return null;
            }

            // m_CompleteImageSize (int32)
            int completeImageSizeOffset = offset;
            int completeImageSize = BitConverter.ToInt32(bytes, offset);
            log.AppendLine($"  m_CompleteImageSize={completeImageSize} at offset {offset}");
            offset += 4;

            // m_MipsStripped (int32)
            int mipsStripped = BitConverter.ToInt32(bytes, offset);
            log.AppendLine($"  m_MipsStripped={mipsStripped} at offset {offset}");
            offset += 4;

            // m_TextureFormat (int32)
            int formatOffset = offset;
            int format = BitConverter.ToInt32(bytes, offset);
            log.AppendLine($"  m_TextureFormat={format} at offset {offset}");
            offset += 4;

            // m_MipCount (int32)
            int mipCountOffset = offset;
            int mipCount = BitConverter.ToInt32(bytes, offset);
            log.AppendLine($"  m_MipCount={mipCount} at offset {offset}");
            offset += 4;

            // Skip remaining fixed fields until we find image data
            // Look for the image data array (will be near the end)
            int imageDataOffset = -1;
            int imageDataSizeOffset = -1;
            int imageDataSize = -1;

            // Search backwards from end for a size that matches expected pixel data
            // RGBA32 = width * height * 4 bytes
            int expectedSize = width * height * 4;
            log.AppendLine($"  Searching for image data (expected ~{expectedSize} bytes for RGBA32)...");

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
                        log.AppendLine($"  Found image data: size={potentialSize} at offset {searchOffset}");
                        break;
                    }
                }
            }

            if (imageDataOffset < 0)
            {
                // Try alternative: look for streaming texture (image data size = 0)
                // These have m_StreamData with external reference
                log.AppendLine($"  No embedded data found, checking for streaming texture...");
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
                        log.AppendLine($"  Found streaming texture marker at offset {searchOffset}");
                        break;
                    }
                }
            }

            // Search for ColorSpace field between MipCount and ImageData
            // ColorSpace is a 4-byte int: 0 = Gamma/sRGB, 1 = Linear
            // It's typically 20-60 bytes after MipCount, before image data
            int colorSpaceOffset = -1;
            int colorSpaceValue = -1;

            // In Unity 6, after MipCount we have several bool fields (aligned), then ColorSpace
            // Scan for a value of 0 or 1 at 4-byte aligned offsets
            // The ColorSpace is usually followed by LightmapFormat (typically 0-6)
            int scanStart = mipCountOffset + 4;
            int scanEnd = imageDataSizeOffset > 0 ? imageDataSizeOffset : bytes.Length - 50;

            for (int scanOffset = scanStart; scanOffset < scanEnd - 8; scanOffset += 4)
            {
                int val = BitConverter.ToInt32(bytes, scanOffset);
                int nextVal = BitConverter.ToInt32(bytes, scanOffset + 4);

                // ColorSpace is 0 or 1, followed by LightmapFormat (0-6 typically)
                if ((val == 0 || val == 1) && nextVal >= 0 && nextVal <= 10)
                {
                    // Additional check: this shouldn't be at the very start (there are bool fields first)
                    if (scanOffset >= scanStart + 16) // Skip at least 16 bytes of bool fields
                    {
                        colorSpaceOffset = scanOffset;
                        colorSpaceValue = val;
                        log.AppendLine($"  Found ColorSpace={val} at offset {scanOffset} (LightmapFormat={nextVal})");
                        break;
                    }
                }
            }

            if (colorSpaceOffset < 0)
            {
                log.AppendLine($"  Warning: Could not find ColorSpace field");
            }

            // Debug: dump bytes between MipCount and ImageData to understand structure
            log.AppendLine($"  MipCountOffset={mipCountOffset}, ImageDataSizeOffset={imageDataSizeOffset}");
            log.AppendLine($"  ColorSpaceOffset={colorSpaceOffset} (detected value={colorSpaceValue})");
            int dumpStart = mipCountOffset + 4;
            int dumpEnd = Math.Min(dumpStart + 60, imageDataSizeOffset > 0 ? imageDataSizeOffset : bytes.Length);
            var dumpBytes = new System.Text.StringBuilder("  Bytes after MipCount: ");
            for (int i = dumpStart; i < dumpEnd; i += 4)
            {
                int val = BitConverter.ToInt32(bytes, i);
                dumpBytes.Append($"[{i}]={val} ");
            }
            log.AppendLine(dumpBytes.ToString());

            log.AppendLine($"  SUCCESS: {width}x{height}, format={format}, imageDataSize={imageDataSize}");
            parseLog = log.ToString();

            return new Texture2DTemplate
            {
                Bytes = bytes,
                Info = info,
                NameOffset = nameOffset,
                WidthOffset = widthOffset,
                HeightOffset = heightOffset,
                FormatOffset = formatOffset,
                MipCountOffset = mipCountOffset,
                ColorSpaceOffset = colorSpaceOffset,
                ImageDataOffset = imageDataOffset,
                ImageDataSizeOffset = imageDataSizeOffset,
                CompleteImageSizeOffset = completeImageSizeOffset,
                OriginalWidth = width,
                OriginalHeight = height,
                OriginalImageDataSize = imageDataSize,
                OriginalColorSpace = colorSpaceValue
            };
        }
        catch (Exception ex)
        {
            parseLog = log.Append($"  EXCEPTION: {ex.Message}").ToString();
            return null;
        }
    }

    /// <summary>
    /// Create a Texture2D asset from a PNG file.
    /// </summary>
    /// <param name="afile">The assets file to add the texture to.</param>
    /// <param name="template">Template Texture2D to clone structure from.</param>
    /// <param name="pngPath">Path to the source PNG file.</param>
    /// <param name="assetName">Name for the new texture asset.</param>
    /// <param name="pathId">PathID to assign to the new asset.</param>
    /// <param name="colorSpace">Color space: 0 = sRGB/Gamma (default, for diffuse/UI), 1 = Linear (for normal maps, data).</param>
    public static TextureCreationResult CreateFromPng(
        AssetsFile afile,
        Texture2DTemplate template,
        string pngPath,
        string assetName,
        long pathId,
        int colorSpace = 0)
    {
        var result = new TextureCreationResult
        {
            PathId = pathId,
            AssetName = assetName
        };

        try
        {
            // Validate template
            if (template == null)
            {
                result.ErrorMessage = "Template is null";
                return result;
            }
            if (template.Bytes == null || template.Bytes.Length == 0)
            {
                result.ErrorMessage = "Template bytes are null or empty";
                return result;
            }

            // Load and decode the PNG
            if (!File.Exists(pngPath))
            {
                result.ErrorMessage = $"PNG file not found: {pngPath}";
                return result;
            }

            Image<Rgba32> image;
            try
            {
                image = Image.Load<Rgba32>(pngPath);
            }
            catch (Exception imgEx)
            {
                result.ErrorMessage = $"Failed to load image: {imgEx.Message}";
                return result;
            }

            using (image)
            {
                result.Width = image.Width;
                result.Height = image.Height;

                // Convert to raw RGBA32 bytes (Unity expects bottom-to-top)
                var pixelData = new byte[image.Width * image.Height * 4];
                try
                {
                    image.ProcessPixelRows(accessor =>
                    {
                        for (int y = 0; y < accessor.Height; y++)
                        {
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
                }
                catch (Exception pixEx)
                {
                    result.ErrorMessage = $"Failed to process pixels: {pixEx.Message}";
                    return result;
                }

                // Build the new Texture2D bytes
                // Use provided colorSpace (default 0 = sRGB for most textures)
                byte[]? textureBytes;
                try
                {
                    textureBytes = BuildTexture2DBytes(template, assetName, image.Width, image.Height, pixelData, colorSpace);
                }
                catch (Exception buildEx)
                {
                    result.ErrorMessage = $"Failed to build texture bytes: {buildEx.Message}";
                    return result;
                }

                if (textureBytes == null)
                {
                    result.ErrorMessage = "Failed to build Texture2D bytes (returned null)";
                    return result;
                }

                // Create and register the asset
                if (afile == null)
                {
                    result.ErrorMessage = "AssetsFile is null";
                    return result;
                }
                if (afile.Metadata == null)
                {
                    result.ErrorMessage = "AssetsFile.Metadata is null";
                    return result;
                }

                try
                {
                    // Unity 6 doesn't have embedded type trees, so AssetFileInfo.Create returns null.
                    // Instead, create the info manually based on the template's info.
                    var newInfo = new AssetFileInfo
                    {
                        PathId = pathId,
                        TypeIdOrIndex = template.Info.TypeIdOrIndex,
                        TypeId = template.Info.TypeId,
                        ScriptTypeIndex = template.Info.ScriptTypeIndex,
                        Stripped = template.Info.Stripped
                    };
                    newInfo.SetNewData(textureBytes);
                    afile.Metadata.AddAssetInfo(newInfo);
                }
                catch (Exception assetEx)
                {
                    result.ErrorMessage = $"Failed to create asset info: {assetEx.Message}";
                    return result;
                }

                result.Success = true;
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Failed to create texture (outer): {ex.Message}\nStack: {ex.StackTrace}";
        }

        return result;
    }

    /// <summary>
    /// Replace an existing Texture2D asset's data in-place.
    /// This modifies the existing asset at its current PathId rather than creating a new one.
    /// Preserves the original texture's ColorSpace to maintain correct rendering.
    /// </summary>
    public static TextureCreationResult ReplaceTextureInPlace(
        AssetsFile afile,
        Texture2DTemplate template,
        string pngPath,
        string assetName,
        AssetFileInfo existingAsset)
    {
        var result = new TextureCreationResult
        {
            PathId = existingAsset.PathId,
            AssetName = assetName
        };

        try
        {
            // Validate inputs
            if (template == null || template.Bytes == null || template.Bytes.Length == 0)
            {
                result.ErrorMessage = "Template is null or empty";
                return result;
            }

            if (!File.Exists(pngPath))
            {
                result.ErrorMessage = $"PNG file not found: {pngPath}";
                return result;
            }

            // Read the original texture's bytes to preserve its ColorSpace
            int originalColorSpace = -1;
            try
            {
                var reader = afile.Reader;
                var absOffset = existingAsset.GetAbsoluteByteOffset(afile);
                reader.BaseStream.Position = absOffset;
                var originalBytes = reader.ReadBytes((int)existingAsset.ByteSize);

                // Parse the original texture to get its ColorSpace
                var originalTemplate = TryParseTexture2D(originalBytes, existingAsset);
                if (originalTemplate != null && originalTemplate.OriginalColorSpace >= 0)
                {
                    originalColorSpace = originalTemplate.OriginalColorSpace;
                }
            }
            catch
            {
                // If we can't read the original, we'll use the template's ColorSpace
            }

            // Load and decode the PNG
            Image<Rgba32> image;
            try
            {
                image = Image.Load<Rgba32>(pngPath);
            }
            catch (Exception imgEx)
            {
                result.ErrorMessage = $"Failed to load image: {imgEx.Message}";
                return result;
            }

            using (image)
            {
                result.Width = image.Width;
                result.Height = image.Height;

                // Convert to raw RGBA32 bytes (Unity expects bottom-to-top)
                var pixelData = new byte[image.Width * image.Height * 4];
                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
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

                // Build the new Texture2D bytes, preserving original ColorSpace if available
                var textureBytes = BuildTexture2DBytes(template, assetName, image.Width, image.Height, pixelData, originalColorSpace);
                if (textureBytes == null)
                {
                    result.ErrorMessage = "Failed to build texture bytes";
                    return result;
                }

                // Replace the existing asset's data in-place
                existingAsset.SetNewData(textureBytes);
                result.Success = true;
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Failed to replace texture: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Build Texture2D bytes by patching the template.
    /// </summary>
    /// <param name="overrideColorSpace">If >= 0, patch the ColorSpace to this value instead of using template's value.</param>
    private static byte[]? BuildTexture2DBytes(
        Texture2DTemplate template,
        string name,
        int width,
        int height,
        byte[] pixelData,
        int overrideColorSpace = -1)
    {
        try
        {
            // Validate inputs
            if (template.ImageDataSizeOffset < 0 || template.ImageDataOffset < 0)
            {
                throw new InvalidOperationException($"Invalid template offsets: ImageDataSizeOffset={template.ImageDataSizeOffset}, ImageDataOffset={template.ImageDataOffset}");
            }

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

            // Calculate original StreamData size (everything after image data in template)
            int origStreamDataStart = template.ImageDataOffset + template.OriginalImageDataSize + origImageDataPadding;
            int origStreamDataSize = template.Bytes.Length - origStreamDataStart;

            // Keep the same StreamData size as the original to maintain correct structure
            // We'll zero it out to indicate inline data instead of streaming
            int newStreamDataSize = origStreamDataSize;
            int streamDataSizeDiff = 0; // Same size as original

        // Calculate new buffer size
        int newSize = template.Bytes.Length + nameSizeDiff + imageSizeDiff + streamDataSizeDiff;
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
        // This includes IsReadable, ColorSpace, TextureSettings, etc.
        int srcImageSizeOffset = template.ImageDataSizeOffset;
        int bytesUntilImageSize = srcImageSizeOffset - srcOffset;
        int dstBeforeCopy = dstOffset; // Remember position before copy for patching
        Array.Copy(template.Bytes, srcOffset, newBytes, dstOffset, bytesUntilImageSize);
        srcOffset += bytesUntilImageSize;
        dstOffset += bytesUntilImageSize;

        // Patch m_IsReadable to true (required for sprites to render correctly)
        // m_IsReadable is the first byte after m_MipCount
        newBytes[dstBeforeCopy] = 1; // true

        // Patch ColorSpace if override is specified
        // ColorSpace is at a fixed offset from MipCount in the template
        if (overrideColorSpace >= 0 && template.ColorSpaceOffset >= 0)
        {
            // Calculate the ColorSpace offset in the new bytes
            // template.ColorSpaceOffset is the absolute offset in template bytes
            // template.MipCountOffset + 4 is where srcOffset was when we started copying
            int templateMipCountEnd = template.MipCountOffset + 4;
            int colorSpaceRelativeOffset = template.ColorSpaceOffset - templateMipCountEnd;
            if (colorSpaceRelativeOffset >= 0 && colorSpaceRelativeOffset + 4 <= bytesUntilImageSize)
            {
                int colorSpaceInNewBytes = dstBeforeCopy + colorSpaceRelativeOffset;
                Array.Copy(BitConverter.GetBytes(overrideColorSpace), 0, newBytes, colorSpaceInNewBytes, 4);
            }
        }

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

        // Copy original StreamData structure but zero it out for inline data
        // This preserves the correct structure size for this Unity version
        // origStreamDataSize was calculated earlier in the function
        // Zero out the StreamData (empty path, offset=0, size=0 tells Unity to use inline data)
        for (int i = 0; i < newStreamDataSize; i++)
            newBytes[dstOffset++] = 0;

        // Trim to actual size used
        if (dstOffset < newBytes.Length)
        {
            Array.Resize(ref newBytes, dstOffset);
        }

        return newBytes;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"BuildTexture2DBytes failed: {ex.Message} (NameOffset={template.NameOffset}, WidthOffset={template.WidthOffset}, ImageDataSizeOffset={template.ImageDataSizeOffset})", ex);
        }
    }

    private static int Align4(int offset) => (offset + 3) & ~3;
}
