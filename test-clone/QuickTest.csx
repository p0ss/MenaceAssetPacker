// Test the patching logic directly
using System;
using System.Linq;
using System.Text;

// Simulate the source bytes (first 40 bytes from weapon template)
byte[] source = new byte[] {
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // m_Script PPtr (12 bytes)
    0x01, 0x00, 0x00, 0x00, // m_Name length = 1
    0x01, 0x00, 0x00, 0x00, // m_Name char (0x01) + padding
    0x52, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Unknown 8 bytes at offset 20
    0x1C, 0x00, 0x00, 0x00, // m_ID length = 28
    0x77, 0x65, 0x61, 0x70, 0x6F, 0x6E, 0x2E, 0x67 // "weapon.g" (start of m_ID)
};

Console.WriteLine("=== Source (40 bytes) ===");
for (int i = 0; i < source.Length; i += 4) {
    var chunk = string.Join(" ", source.Skip(i).Take(4).Select(b => b.ToString("X2")));
    Console.WriteLine($"  [{i,2}-{i+3,2}]: {chunk}");
}

// Parse m_Name
int nameOffset = 12;
int nameLen = BitConverter.ToInt32(source, nameOffset);
int namePadding = (4 - (nameLen % 4)) % 4;
int nameTotalSize = 4 + nameLen + namePadding;
Console.WriteLine($"\nm_Name: len={nameLen}, padding={namePadding}, total={nameTotalSize} bytes (offsets {nameOffset}-{nameOffset+nameTotalSize-1})");

// Patch m_Name
string newName = "weapon.laser_smg";
var patched = PatchStringAtOffset(source, 12, newName);
Console.WriteLine($"\n=== After patching m_Name with '{newName}' ({patched.Length} bytes) ===");
for (int i = 0; i < Math.Min(52, patched.Length); i += 4) {
    var chunk = string.Join(" ", patched.Skip(i).Take(4).Select(b => b.ToString("X2")));
    Console.WriteLine($"  [{i,2}-{i+3,2}]: {chunk}");
}

// Check what happened to the data at original offset 20
Console.WriteLine($"\nOriginal bytes 20-27 were: 52 09 00 00 00 00 00 00");
Console.WriteLine($"After patch, what's at NEW offset {20 + (patched.Length - source.Length)}?");
int newOffset20 = 20 + (patched.Length - source.Length);
Console.WriteLine($"  Bytes {newOffset20}-{newOffset20+7}: {string.Join(" ", patched.Skip(newOffset20).Take(8).Select(b => b.ToString("X2")))}");

static byte[] PatchStringAtOffset(byte[] sourceBytes, int offset, string newValue) {
    int origLen = BitConverter.ToInt32(sourceBytes, offset);
    int origPadding = (4 - (origLen % 4)) % 4;
    int origTotalLen = 4 + origLen + origPadding;
    
    int newLen = newValue.Length;
    int newPadding = (4 - (newLen % 4)) % 4;
    int newTotalLen = 4 + newLen + newPadding;
    
    int sizeDiff = newTotalLen - origTotalLen;
    var result = new byte[sourceBytes.Length + sizeDiff];
    
    Array.Copy(sourceBytes, 0, result, 0, offset);
    Array.Copy(BitConverter.GetBytes(newLen), 0, result, offset, 4);
    Array.Copy(Encoding.ASCII.GetBytes(newValue), 0, result, offset + 4, newLen);
    for (int i = 0; i < newPadding; i++) result[offset + 4 + newLen + i] = 0;
    
    int afterOrigString = offset + origTotalLen;
    int afterNewString = offset + newTotalLen;
    if (afterOrigString < sourceBytes.Length)
        Array.Copy(sourceBytes, afterOrigString, result, afterNewString, sourceBytes.Length - afterOrigString);
    
    return result;
}
