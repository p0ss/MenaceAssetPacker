using System;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Menace.Modkit.Core.Bundles;

/// <summary>
/// Creates native AudioClip assets from WAV files using AssetsTools.NET.
/// These assets load naturally via Resources.Load() when added to resources.assets
/// and indexed in the ResourceManager.
///
/// Supports PCM WAV files with 8/16/24/32-bit depth, mono or stereo.
/// Uses raw binary cloning approach since Unity 6 doesn't embed type trees.
/// </summary>
public class AudioAssetCreator
{
    /// <summary>
    /// Result of audio asset creation.
    /// </summary>
    public class AudioCreationResult
    {
        public bool Success { get; set; }
        public long PathId { get; set; }
        public string? ErrorMessage { get; set; }
        public int Channels { get; set; }
        public int Frequency { get; set; }
        public int BitsPerSample { get; set; }
        public float Duration { get; set; }
        public int DataSize { get; set; }
    }

    /// <summary>
    /// Parsed WAV file data.
    /// </summary>
    public class WavData
    {
        public int Channels { get; set; }
        public int SampleRate { get; set; }
        public int BitsPerSample { get; set; }
        public int TotalSamples { get; set; }
        public float Duration { get; set; }
        public byte[] PcmData { get; set; } = Array.Empty<byte>();
    }

    /// <summary>
    /// Create a native AudioClip asset from a WAV file.
    /// Uses raw binary cloning from an existing AudioClip template.
    /// </summary>
    /// <param name="afile">The assets file to add the audio clip to.</param>
    /// <param name="wavPath">Path to the source WAV file.</param>
    /// <param name="assetName">Name for the audio clip asset.</param>
    /// <param name="pathId">PathID to assign to the new asset.</param>
    /// <param name="templateBytes">Raw bytes of an existing AudioClip to use as template.</param>
    /// <param name="templateInfo">AssetFileInfo of the template AudioClip.</param>
    /// <returns>Result containing success status and asset info.</returns>
    public static AudioCreationResult CreateAudioClip(
        AssetsFile afile,
        string wavPath,
        string assetName,
        long pathId,
        byte[] templateBytes,
        AssetFileInfo templateInfo)
    {
        var result = new AudioCreationResult { PathId = pathId };

        try
        {
            // Parse WAV file
            var wavData = ParseWavFile(wavPath);
            if (wavData == null)
            {
                result.ErrorMessage = "Failed to parse WAV file";
                return result;
            }

            result.Channels = wavData.Channels;
            result.Frequency = wavData.SampleRate;
            result.BitsPerSample = wavData.BitsPerSample;
            result.Duration = wavData.Duration;
            result.DataSize = wavData.PcmData.Length;

            // Clone the template and patch fields
            var clonedBytes = CloneAudioClipWithNewData(
                templateBytes,
                assetName,
                wavData);

            if (clonedBytes == null)
            {
                result.ErrorMessage = "Failed to patch AudioClip template";
                return result;
            }

            // Create the asset info
            var info = AssetFileInfo.Create(
                afile,
                pathId,
                templateInfo.TypeId,
                templateInfo.ScriptTypeIndex);

            info.SetNewData(clonedBytes);
            afile.Metadata.AddAssetInfo(info);

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Failed to create audio clip: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Parse a WAV file and extract audio data.
    /// Supports PCM format (audioFormat=1) with 8/16/24/32-bit depth.
    /// </summary>
    public static WavData? ParseWavFile(string filePath)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            return ParseWavBytes(bytes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parse WAV data from a byte array.
    /// </summary>
    public static WavData? ParseWavBytes(byte[] bytes)
    {
        if (bytes.Length < 44)
            return null;

        // Verify RIFF header
        if (bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F')
            return null;

        // Verify WAVE format
        if (bytes[8] != 'W' || bytes[9] != 'A' || bytes[10] != 'V' || bytes[11] != 'E')
            return null;

        // Find fmt and data chunks
        int pos = 12;
        int channels = 0;
        int sampleRate = 0;
        int bitsPerSample = 0;
        int dataOffset = 0;
        int dataSize = 0;

        while (pos < bytes.Length - 8)
        {
            var chunkId = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
            var chunkSize = BitConverter.ToInt32(bytes, pos + 4);

            if (chunkId == "fmt ")
            {
                var audioFormat = BitConverter.ToInt16(bytes, pos + 8);
                if (audioFormat != 1) // PCM only
                    return null;

                channels = BitConverter.ToInt16(bytes, pos + 10);
                sampleRate = BitConverter.ToInt32(bytes, pos + 12);
                bitsPerSample = BitConverter.ToInt16(bytes, pos + 22);
            }
            else if (chunkId == "data")
            {
                dataOffset = pos + 8;
                dataSize = chunkSize;
                break;
            }

            pos += 8 + chunkSize;
            // Align to 2-byte boundary
            if (chunkSize % 2 != 0) pos++;
        }

        if (channels == 0 || sampleRate == 0 || bitsPerSample == 0 || dataOffset == 0)
            return null;

        // Calculate sample count and duration
        int bytesPerSample = bitsPerSample / 8;
        int totalSamples = dataSize / (bytesPerSample * channels);
        float duration = (float)totalSamples / sampleRate;

        // Extract PCM data
        var pcmData = new byte[dataSize];
        if (dataOffset + dataSize <= bytes.Length)
        {
            Array.Copy(bytes, dataOffset, pcmData, 0, dataSize);
        }
        else
        {
            // Handle truncated file
            int available = bytes.Length - dataOffset;
            if (available > 0)
                Array.Copy(bytes, dataOffset, pcmData, 0, available);
        }

        return new WavData
        {
            Channels = channels,
            SampleRate = sampleRate,
            BitsPerSample = bitsPerSample,
            TotalSamples = totalSamples,
            Duration = duration,
            PcmData = pcmData
        };
    }

    /// <summary>
    /// Find an existing AudioClip in the assets file to use as template.
    /// Returns the raw bytes and AssetFileInfo of the first AudioClip found.
    /// </summary>
    public static (byte[] bytes, AssetFileInfo info)? FindAudioClipTemplate(AssetsFile afile)
    {
        foreach (var info in afile.GetAssetsOfType(AssetClassID.AudioClip))
        {
            try
            {
                var absOffset = info.GetAbsoluteByteOffset(afile);
                afile.Reader.BaseStream.Position = absOffset;
                var bytes = afile.Reader.ReadBytes((int)info.ByteSize);

                // Verify it looks like a valid AudioClip (has a name string at the start)
                if (bytes.Length > 20)
                {
                    int nameLen = BitConverter.ToInt32(bytes, 0);
                    if (nameLen > 0 && nameLen < 200 && 4 + nameLen < bytes.Length)
                    {
                        return (bytes, info);
                    }
                }
            }
            catch
            {
                // Try next one
            }
        }

        return null;
    }

    /// <summary>
    /// Clone an AudioClip's raw bytes and patch with new audio data.
    ///
    /// AudioClip binary layout (approximate, Unity version dependent):
    /// - m_Name: string (4-byte length + chars + padding)
    /// - m_LoadType: int32 (0=DecompressOnLoad, 1=CompressedInMemory, 2=Streaming)
    /// - m_Channels: int32
    /// - m_Frequency: int32
    /// - m_BitsPerSample: int32
    /// - m_Length: float (duration in seconds)
    /// - m_IsTrackerFormat: bool
    /// - m_Ambisonic: bool
    /// - m_SubsoundIndex: int32
    /// - m_PreloadAudioData: bool
    /// - m_LoadInBackground: bool
    /// - m_Legacy3D: bool
    /// - m_CompressionFormat: int32 (0=PCM, 1=Vorbis, 2=ADPCM, etc.)
    /// - m_Resource: StreamedResource
    ///   - m_Source: string (path to .resS file, empty for inline)
    ///   - m_Offset: uint64
    ///   - m_Size: uint64
    /// - m_AudioData: byte[] (inline audio data, empty if streaming)
    /// </summary>
    private static byte[]? CloneAudioClipWithNewData(
        byte[] templateBytes,
        string newName,
        WavData wavData)
    {
        if (templateBytes.Length < 50)
            return null;

        // Step 1: Parse template to find field offsets
        var offsets = ParseAudioClipOffsets(templateBytes);
        if (offsets == null)
            return null;

        // Step 2: Build new AudioClip bytes
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // Write m_Name (aligned string)
        WriteAlignedString(bw, newName);

        // Write m_LoadType (0 = DecompressOnLoad for inline PCM)
        bw.Write(0); // m_LoadType

        // Write audio properties
        bw.Write(wavData.Channels);        // m_Channels
        bw.Write(wavData.SampleRate);      // m_Frequency
        bw.Write(wavData.BitsPerSample);   // m_BitsPerSample
        bw.Write(wavData.Duration);        // m_Length (float)

        // Write boolean flags
        bw.Write((byte)0); // m_IsTrackerFormat
        bw.Write((byte)0); // m_Ambisonic
        // Align to 4 bytes after bools
        var pos = ms.Position;
        var padding = (4 - (pos % 4)) % 4;
        for (int i = 0; i < padding; i++)
            bw.Write((byte)0);

        bw.Write(0); // m_SubsoundIndex

        bw.Write((byte)1); // m_PreloadAudioData = true
        bw.Write((byte)0); // m_LoadInBackground = false
        bw.Write((byte)0); // m_Legacy3D = false
        // Align to 4 bytes
        pos = ms.Position;
        padding = (4 - (pos % 4)) % 4;
        for (int i = 0; i < padding; i++)
            bw.Write((byte)0);

        bw.Write(0); // m_CompressionFormat = PCM

        // Write m_Resource (StreamedResource) - empty for inline data
        WriteAlignedString(bw, ""); // m_Source (empty = inline)
        bw.Write(0UL); // m_Offset
        bw.Write(0UL); // m_Size

        // Write m_AudioData (inline PCM data)
        bw.Write(wavData.PcmData.Length);
        bw.Write(wavData.PcmData);
        // Align to 4 bytes
        pos = ms.Position;
        padding = (4 - (pos % 4)) % 4;
        for (int i = 0; i < padding; i++)
            bw.Write((byte)0);

        return ms.ToArray();
    }

    /// <summary>
    /// Parse AudioClip template bytes to find field offsets.
    /// Returns null if parsing fails.
    /// </summary>
    private static AudioClipOffsets? ParseAudioClipOffsets(byte[] bytes)
    {
        try
        {
            int offset = 0;

            // m_Name
            int nameLen = BitConverter.ToInt32(bytes, offset);
            if (nameLen < 0 || nameLen > 500 || offset + 4 + nameLen > bytes.Length)
                return null;

            offset += 4 + nameLen;
            offset += (4 - (nameLen % 4)) % 4; // padding

            // We have enough info to know the structure is valid
            return new AudioClipOffsets
            {
                NameOffset = 0,
                DataStartOffset = offset
            };
        }
        catch
        {
            return null;
        }
    }

    private class AudioClipOffsets
    {
        public int NameOffset { get; set; }
        public int DataStartOffset { get; set; }
    }

    /// <summary>
    /// Write an aligned string (4-byte length prefix, chars, padding to 4-byte boundary).
    /// </summary>
    private static void WriteAlignedString(BinaryWriter bw, string value)
    {
        var strBytes = System.Text.Encoding.UTF8.GetBytes(value);
        bw.Write(strBytes.Length);
        bw.Write(strBytes);

        // Pad to 4-byte alignment
        int padding = (4 - (strBytes.Length % 4)) % 4;
        for (int i = 0; i < padding; i++)
            bw.Write((byte)0);
    }
}
