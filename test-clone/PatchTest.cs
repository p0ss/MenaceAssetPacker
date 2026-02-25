// Test the patching logic directly
using System;
using System.Text;

// Simulate the source bytes (first 40 bytes from weapon template)
byte[] source = new byte[] {
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // m_Script PPtr (12 bytes)
    0x01, 0x00, 0x00, 0x00, // m_Name length = 1
    0x01, 0x00, 0x00, 0x00, // m_Name char (0x01) + padding (but this looks like another int?)
    0x52, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Unknown 8 bytes at offset 20
    0x1C, 0x00, 0x00, 0x00, // m_ID length = 28
    0x77, 0x65, 0x61, 0x70, 0x6F, 0x6E, 0x2E, 0x67 // "weapon.g" (start of m_ID)
};

Console.WriteLine("=== Source Analysis ===");
Console.WriteLine($"Source bytes: {string.Join(" ", source.Select(b => b.ToString("X2")))}");
Console.WriteLine();

// Analyze m_Name at offset 12
int nameOffset = 12;
int nameLen = BitConverter.ToInt32(source, nameOffset);
Console.WriteLine($"m_Name at offset {nameOffset}:");
Console.WriteLine($"  Length field (bytes {nameOffset}-{nameOffset+3}): {nameLen}");
Console.WriteLine($"  Content byte at {nameOffset+4}: 0x{source[nameOffset+4]:X2}");
Console.WriteLine($"  Bytes {nameOffset+5}-{nameOffset+7}: {source[nameOffset+5]:X2} {source[nameOffset+6]:X2} {source[nameOffset+7]:X2}");

// Calculate m_Name total size
int namePadding = (4 - (nameLen % 4)) % 4;
int nameTotalSize = 4 + nameLen + namePadding;
Console.WriteLine($"  Calculated padding: {namePadding}");
Console.WriteLine($"  Calculated total size: {nameTotalSize}");
Console.WriteLine($"  m_Name ends at offset: {nameOffset + nameTotalSize}");
Console.WriteLine();

// What's at offset 20?
Console.WriteLine($"Bytes at offset 20-27: {string.Join(" ", source.Skip(20).Take(8).Select(b => b.ToString("X2")))}");
Console.WriteLine($"  As int32 at 20: {BitConverter.ToInt32(source, 20)} (0x{BitConverter.ToInt32(source, 20):X8})");
Console.WriteLine($"  As int32 at 24: {BitConverter.ToInt32(source, 24)} (0x{BitConverter.ToInt32(source, 24):X8})");
Console.WriteLine();

// Now simulate patching m_Name with "weapon.laser_smg"
Console.WriteLine("=== Patching m_Name with 'weapon.laser_smg' ===");
string newName = "weapon.laser_smg";
int newNameLen = newName.Length; // 16
int newNamePadding = (4 - (newNameLen % 4)) % 4; // 0
int newNameTotalSize = 4 + newNameLen + newNamePadding; // 20
Console.WriteLine($"New name: '{newName}' (length={newNameLen})");
Console.WriteLine($"New padding: {newNamePadding}");
Console.WriteLine($"New total size: {newNameTotalSize}");
Console.WriteLine($"Size difference: {newNameTotalSize - nameTotalSize}");
Console.WriteLine();

// The problem: if nameTotalSize is 8 (assuming padding), but actually
// bytes 16-19 are NOT padding but real data...
Console.WriteLine("=== POTENTIAL ISSUE ===");
Console.WriteLine($"If bytes 16-19 (01 00 00 00) are NOT m_Name padding but separate data,");
Console.WriteLine($"then patching m_Name would OVERWRITE that data!");
Console.WriteLine();
Console.WriteLine($"Bytes 16-19 interpreted as int32: {BitConverter.ToInt32(source, 16)}");
Console.WriteLine($"This looks like it might be a separate field, not padding!");
