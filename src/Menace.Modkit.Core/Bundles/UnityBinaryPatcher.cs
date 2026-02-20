using System;
using System.Text;

namespace Menace.Modkit.Core.Bundles;

/// <summary>
/// Utilities for patching Unity serialized binary data.
/// Handles length-prefixed strings with 4-byte alignment.
/// </summary>
public static class UnityBinaryPatcher
{
    /// <summary>
    /// Offset of m_Name field in MonoBehaviour/ScriptableObject serialized data.
    /// Layout: m_Script PPtr (12 bytes: FileID int32 + PathID int64) followed by m_Name.
    /// </summary>
    public const int MONOBEHAVIOUR_NAME_OFFSET = 12;

    /// <summary>
    /// Patch a length-prefixed string at a specific offset in a byte array.
    /// Returns a new byte array with the string replaced, handling size changes.
    /// Unity strings are 4-byte aligned: [4-byte length][chars][padding to 4-byte boundary]
    /// </summary>
    /// <param name="sourceBytes">Original byte array</param>
    /// <param name="offset">Offset where the string length prefix starts</param>
    /// <param name="newValue">New string value to write</param>
    /// <returns>New byte array with patched string, or null if invalid</returns>
    public static byte[]? PatchStringAtOffset(byte[] sourceBytes, int offset, string newValue)
    {
        if (sourceBytes == null || sourceBytes.Length <= offset + 4)
            return null;

        // Read original string length
        int origLen = BitConverter.ToInt32(sourceBytes, offset);
        if (origLen < 0 || origLen > 500 || offset + 4 + origLen > sourceBytes.Length)
            return null;

        // Calculate padding (Unity aligns strings to 4-byte boundaries)
        int origPadding = (4 - (origLen % 4)) % 4;
        int origTotalLen = 4 + origLen + origPadding;

        int newLen = newValue.Length;
        int newPadding = (4 - (newLen % 4)) % 4;
        int newTotalLen = 4 + newLen + newPadding;

        int sizeDiff = newTotalLen - origTotalLen;

        // Create new buffer
        var result = new byte[sourceBytes.Length + sizeDiff];

        // Copy data before the string
        Array.Copy(sourceBytes, 0, result, 0, offset);

        // Write new string length
        Array.Copy(BitConverter.GetBytes(newLen), 0, result, offset, 4);

        // Write new string content
        var newValueBytes = Encoding.ASCII.GetBytes(newValue);
        Array.Copy(newValueBytes, 0, result, offset + 4, newLen);

        // Write padding zeros
        for (int i = 0; i < newPadding; i++)
        {
            result[offset + 4 + newLen + i] = 0;
        }

        // Copy remaining data after the original string
        int afterOrigString = offset + origTotalLen;
        int afterNewString = offset + newTotalLen;
        if (afterOrigString < sourceBytes.Length)
        {
            Array.Copy(sourceBytes, afterOrigString, result, afterNewString, sourceBytes.Length - afterOrigString);
        }

        return result;
    }

    /// <summary>
    /// Read a length-prefixed string at a specific offset.
    /// </summary>
    /// <param name="bytes">Byte array to read from</param>
    /// <param name="offset">Offset where the string length prefix starts</param>
    /// <returns>The string value, or null if invalid</returns>
    public static string? ReadStringAtOffset(byte[] bytes, int offset)
    {
        if (bytes == null || bytes.Length <= offset + 4)
            return null;

        int len = BitConverter.ToInt32(bytes, offset);
        if (len < 0 || len > 500 || offset + 4 + len > bytes.Length)
            return null;

        return Encoding.ASCII.GetString(bytes, offset + 4, len);
    }

    /// <summary>
    /// Calculate the total size of a length-prefixed string including padding.
    /// </summary>
    public static int GetAlignedStringSize(int stringLength)
    {
        int padding = (4 - (stringLength % 4)) % 4;
        return 4 + stringLength + padding;
    }

    /// <summary>
    /// Calculate the total size of a length-prefixed string at an offset.
    /// </summary>
    public static int GetStringTotalSize(byte[] bytes, int offset)
    {
        if (bytes == null || bytes.Length <= offset + 4)
            return -1;

        int len = BitConverter.ToInt32(bytes, offset);
        if (len < 0 || len > 500)
            return -1;

        return GetAlignedStringSize(len);
    }
}
