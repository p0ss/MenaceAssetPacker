using Menace.Modkit.Core.Bundles;
using Xunit;

namespace Menace.Modkit.Tests.Bundles;

/// <summary>
/// Tests for UnityBinaryPatcher - validates Unity binary string patching logic.
/// These tests verify the fix for clone name patching (both m_Name and m_ID).
/// </summary>
public class UnityBinaryPatcherTests
{
    /// <summary>
    /// Create a mock MonoBehaviour byte array with m_Script PPtr, m_Name, and m_ID.
    /// Layout: [12 bytes m_Script][m_Name string][8 bytes padding][m_ID string][tail data]
    /// </summary>
    private static byte[] CreateMockMonoBehaviour(string mName, string mId)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // m_Script PPtr (12 bytes): FileID (4) + PathID (8)
        bw.Write(0);           // FileID
        bw.Write(12345678L);   // PathID

        // m_Name (length-prefixed, 4-byte aligned)
        WriteAlignedString(bw, mName);

        // Some padding/other fields between m_Name and m_ID
        bw.Write(0x12345678);  // 4 bytes of other data
        bw.Write(0x9ABCDEF0);  // 4 bytes of other data

        // m_ID (length-prefixed, 4-byte aligned)
        WriteAlignedString(bw, mId);

        // Trailing data
        bw.Write(0xDEADBEEF);

        return ms.ToArray();
    }

    private static void WriteAlignedString(BinaryWriter bw, string value)
    {
        bw.Write(value.Length);
        bw.Write(System.Text.Encoding.ASCII.GetBytes(value));
        int padding = (4 - (value.Length % 4)) % 4;
        for (int i = 0; i < padding; i++)
            bw.Write((byte)0);
    }

    [Fact]
    public void ReadStringAtOffset_ReadsCorrectly()
    {
        var bytes = CreateMockMonoBehaviour("original_name", "weapon.laser_rifle");

        var name = UnityBinaryPatcher.ReadStringAtOffset(bytes, 12);

        Assert.Equal("original_name", name);
    }

    [Fact]
    public void PatchStringAtOffset_SameLength_PatchesCorrectly()
    {
        var bytes = CreateMockMonoBehaviour("original_name", "weapon.laser_rifle");

        var patched = UnityBinaryPatcher.PatchStringAtOffset(bytes, 12, "patched__name");

        Assert.NotNull(patched);
        Assert.Equal(bytes.Length, patched.Length); // Same length string = same total size
        Assert.Equal("patched__name", UnityBinaryPatcher.ReadStringAtOffset(patched, 12));
    }

    [Fact]
    public void PatchStringAtOffset_ShorterString_ShrinksByteArray()
    {
        var bytes = CreateMockMonoBehaviour("original_name", "weapon.laser_rifle");
        int originalLength = bytes.Length;

        var patched = UnityBinaryPatcher.PatchStringAtOffset(bytes, 12, "short");

        Assert.NotNull(patched);
        Assert.True(patched.Length < originalLength);
        Assert.Equal("short", UnityBinaryPatcher.ReadStringAtOffset(patched, 12));
    }

    [Fact]
    public void PatchStringAtOffset_LongerString_GrowsByteArray()
    {
        var bytes = CreateMockMonoBehaviour("original_name", "weapon.laser_rifle");
        int originalLength = bytes.Length;

        var patched = UnityBinaryPatcher.PatchStringAtOffset(bytes, 12, "this_is_a_much_longer_name_than_before");

        Assert.NotNull(patched);
        Assert.True(patched.Length > originalLength);
        Assert.Equal("this_is_a_much_longer_name_than_before", UnityBinaryPatcher.ReadStringAtOffset(patched, 12));
    }

    [Fact]
    public void PatchStringAtOffset_PreservesDataBefore()
    {
        var bytes = CreateMockMonoBehaviour("original_name", "weapon.laser_rifle");

        var patched = UnityBinaryPatcher.PatchStringAtOffset(bytes, 12, "new_name");

        Assert.NotNull(patched);
        // First 12 bytes (m_Script PPtr) should be unchanged
        for (int i = 0; i < 12; i++)
        {
            Assert.Equal(bytes[i], patched[i]);
        }
    }

    [Fact]
    public void PatchStringAtOffset_PreservesDataAfter()
    {
        var bytes = CreateMockMonoBehaviour("original_name", "weapon.laser_rifle");

        var patched = UnityBinaryPatcher.PatchStringAtOffset(bytes, 12, "new_name");

        Assert.NotNull(patched);
        // Last 4 bytes (0xDEADBEEF) should still be present
        int lastFourStart = patched.Length - 4;
        Assert.Equal(0xEF, patched[lastFourStart]);
        Assert.Equal(0xBE, patched[lastFourStart + 1]);
        Assert.Equal(0xAD, patched[lastFourStart + 2]);
        Assert.Equal(0xDE, patched[lastFourStart + 3]);
    }

    [Fact]
    public void PatchStringAtOffset_InvalidOffset_ReturnsNull()
    {
        var bytes = CreateMockMonoBehaviour("name", "id");

        var result = UnityBinaryPatcher.PatchStringAtOffset(bytes, bytes.Length + 10, "new");

        Assert.Null(result);
    }

    [Fact]
    public void PatchStringAtOffset_NullBytes_ReturnsNull()
    {
        var result = UnityBinaryPatcher.PatchStringAtOffset(null!, 0, "test");

        Assert.Null(result);
    }

    [Fact]
    public void GetAlignedStringSize_CalculatesCorrectly()
    {
        // Length 4 -> 4 (length) + 4 (chars) + 0 (padding) = 8
        Assert.Equal(8, UnityBinaryPatcher.GetAlignedStringSize(4));

        // Length 5 -> 4 (length) + 5 (chars) + 3 (padding) = 12
        Assert.Equal(12, UnityBinaryPatcher.GetAlignedStringSize(5));

        // Length 8 -> 4 (length) + 8 (chars) + 0 (padding) = 12
        Assert.Equal(12, UnityBinaryPatcher.GetAlignedStringSize(8));

        // Length 13 -> 4 (length) + 13 (chars) + 3 (padding) = 20
        Assert.Equal(20, UnityBinaryPatcher.GetAlignedStringSize(13));
    }

    [Fact]
    public void SequentialPatches_BothFieldsPatched()
    {
        // This simulates what CloneWithNewId does - patch m_Name then m_ID
        var bytes = CreateMockMonoBehaviour("original_name", "weapon.laser_rifle");
        string newName = "weapon.my_clone";

        // Find m_ID offset in original
        int mIdOffset = 12 + UnityBinaryPatcher.GetAlignedStringSize("original_name".Length) + 8;

        // Patch m_Name first
        var afterNamePatch = UnityBinaryPatcher.PatchStringAtOffset(bytes, 12, newName);
        Assert.NotNull(afterNamePatch);

        // Calculate new m_ID offset
        int sizeDiff = afterNamePatch.Length - bytes.Length;
        int newMIdOffset = mIdOffset + sizeDiff;

        // Patch m_ID
        var final = UnityBinaryPatcher.PatchStringAtOffset(afterNamePatch, newMIdOffset, newName);
        Assert.NotNull(final);

        // Verify both fields are patched
        Assert.Equal(newName, UnityBinaryPatcher.ReadStringAtOffset(final, 12));

        // m_ID should also be the new name
        int finalMIdOffset = 12 + UnityBinaryPatcher.GetAlignedStringSize(newName.Length) + 8;
        Assert.Equal(newName, UnityBinaryPatcher.ReadStringAtOffset(final, finalMIdOffset));
    }

    [Fact]
    public void MonoBehaviourNameOffset_IsCorrect()
    {
        // Verify the constant matches expected layout
        Assert.Equal(12, UnityBinaryPatcher.MONOBEHAVIOUR_NAME_OFFSET);
    }
}
