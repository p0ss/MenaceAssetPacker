using System;
using System.IO;
using System.Text;

namespace Menace.Modkit.Core.Bundles;

/// <summary>
/// Writes a valid UnityFS format v6 (uncompressed) bundle file from raw serialized
/// AssetsFile bytes. This produces a .bundle that AssetBundle.LoadFromFile() can consume.
/// </summary>
public static class BundleWriter
{
    /// <summary>
    /// Wrap serialized AssetsFile bytes in a UnityFS v6 container (uncompressed).
    /// </summary>
    /// <param name="serializedFileBytes">The raw AssetsFile bytes to wrap.</param>
    /// <param name="outputPath">Path to write the .bundle file.</param>
    /// <param name="unityVersion">Engine version string (e.g. "2020.3.18f1").</param>
    /// <param name="internalName">Internal CAB name for the serialized file entry.</param>
    /// <returns>True if the bundle was written successfully.</returns>
    public static bool WriteBundle(byte[] serializedFileBytes, string outputPath,
        string unityVersion, string internalName = "CAB-modpack")
    {
        if (serializedFileBytes == null || serializedFileBytes.Length == 0)
            return false;

        Console.WriteLine($"[BundleWriter] Writing UnityFS bundle to: {outputPath}");
        Console.WriteLine($"[BundleWriter] Payload size: {serializedFileBytes.Length} bytes, Unity version: {unityVersion}, CAB name: {internalName}");

        try
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var fs = File.Create(outputPath);
            using var writer = new BinaryWriter(fs);

            int dataLen = serializedFileBytes.Length;

            // --- Build Block/Directory info (uncompressed) ---
            // We pre-build this to know its size for the header.
            byte[] blockDirInfo;
            using (var bdMs = new MemoryStream())
            using (var bdWriter = new BinaryWriter(bdMs))
            {
                // Hash placeholder: 16 zero bytes
                bdWriter.Write(new byte[16]);

                // Block count: 1
                WriteBE32(bdWriter, 1);
                // Block entry: uncompressed size, compressed size, flags
                WriteBE32(bdWriter, (uint)dataLen);   // uncompressed
                WriteBE32(bdWriter, (uint)dataLen);   // compressed (same, uncompressed)
                WriteBE16(bdWriter, 0x0000);           // flags: none

                // Directory count: 1
                WriteBE32(bdWriter, 1);
                // Directory entry: offset, size, flags, name
                WriteBE64(bdWriter, 0);                // offset within data block
                WriteBE64(bdWriter, (ulong)dataLen);   // size
                WriteBE32(bdWriter, 4);                // flags: serialized file
                WriteNullTermString(bdWriter, internalName);

                blockDirInfo = bdMs.ToArray();
            }

            int blockInfoSize = blockDirInfo.Length;

            // --- Header ---
            // Signature
            WriteNullTermString(writer, "UnityFS");

            // Format version
            WriteBE32(writer, 6);

            // Unity version strings
            WriteNullTermString(writer, "5.x.x");
            WriteNullTermString(writer, unityVersion);

            // We need the header size to compute totalSize.
            // Header so far: signature + version + two strings + totalSize(8) + cBlockSize(4) + uBlockSize(4) + flags(4)
            // We'll compute totalSize after knowing header length.
            long headerSizeSoFar = fs.Position; // position after the version strings

            // Reserve space for: totalSize(8) + cBlockSize(4) + uBlockSize(4) + flags(4) = 20 bytes
            long totalSizePos = fs.Position;
            WriteBE64(writer, 0);               // placeholder for totalSize
            WriteBE32(writer, (uint)blockInfoSize); // compressed block info size (same, uncompressed)
            WriteBE32(writer, (uint)blockInfoSize); // uncompressed block info size
            WriteBE32(writer, 0x00000000);          // flags: uncompressed, block info at end of header

            long headerEnd = fs.Position;

            // --- Block/Directory info follows header directly (flags bit 7 = 0) ---
            writer.Write(blockDirInfo);

            // --- Data section ---
            writer.Write(serializedFileBytes);

            // --- Patch totalSize ---
            long totalSize = fs.Position;
            fs.Seek(totalSizePos, SeekOrigin.Begin);
            WriteBE64(writer, (ulong)totalSize);

            Console.WriteLine($"[BundleWriter] ✓ Bundle written successfully ({totalSize} bytes total)");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BundleWriter] ❌ Failed to write bundle: {ex.Message}");
            return false;
        }
    }

    private static void WriteBE16(BinaryWriter writer, ushort value)
    {
        writer.Write((byte)(value >> 8));
        writer.Write((byte)(value & 0xFF));
    }

    private static void WriteBE32(BinaryWriter writer, uint value)
    {
        writer.Write((byte)((value >> 24) & 0xFF));
        writer.Write((byte)((value >> 16) & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    private static void WriteBE64(BinaryWriter writer, ulong value)
    {
        WriteBE32(writer, (uint)(value >> 32));
        WriteBE32(writer, (uint)(value & 0xFFFFFFFF));
    }

    private static void WriteNullTermString(BinaryWriter writer, string value)
    {
        writer.Write(Encoding.ASCII.GetBytes(value));
        writer.Write((byte)0);
    }
}
