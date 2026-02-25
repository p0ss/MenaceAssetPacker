using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.IO;
using System.Linq;

// Verify mode: just check the deployed clone
if (args.Length > 0 && args[0] == "--verify")
{
    var verifyPath = "/home/poss/.steam/debian-installation/steamapps/common/Menace/Menace_Data/resources.assets";
    Console.WriteLine($"Verifying clone in: {verifyPath}");
    var vam = new AssetsManager();
    var vinst = vam.LoadAssetsFile(verifyPath, false);
    var vfile = vinst.file;

    var clone = vfile.AssetInfos.FirstOrDefault(a => a.PathId == 112925);
    if (clone != null)
    {
        Console.WriteLine($"Found clone: PathId={clone.PathId}, ByteSize={clone.ByteSize}");

        var coffset = clone.GetAbsoluteByteOffset(vfile);
        vfile.Reader.BaseStream.Position = coffset;
        var cbytes = vfile.Reader.ReadBytes((int)clone.ByteSize);

        Console.WriteLine($"First 50 bytes: {string.Join(" ", cbytes.Take(50).Select(b => b.ToString("X2")))}");

        // Find m_ID at offset 28
        if (cbytes.Length > 32)
        {
            int idLen = BitConverter.ToInt32(cbytes, 28);
            if (idLen > 0 && idLen < 100 && 32 + idLen <= cbytes.Length)
            {
                var idStr = System.Text.Encoding.ASCII.GetString(cbytes, 32, idLen);
                Console.WriteLine($"m_ID at offset 28: length={idLen}, value='{idStr}'");
            }
        }
    }
    else
    {
        Console.WriteLine("Clone not found!");
    }
    vam.UnloadAll();
    return;
}

var gameDataPath = "/home/poss/.steam/debian-installation/steamapps/common/Menace/Menace_Data";
var resourcesPath = Path.Combine(gameDataPath, "resources.assets.original");
var outputPath = "/tmp/test-clone-output.assets";

if (!File.Exists(resourcesPath))
{
    resourcesPath = Path.Combine(gameDataPath, "resources.assets");
}

Console.WriteLine($"Loading: {resourcesPath}");

var am = new AssetsManager();
var inst = am.LoadAssetsFile(resourcesPath, false);
var afile = inst.file;

Console.WriteLine($"Loaded {afile.AssetInfos.Count} assets");

// Find a MonoBehaviour to clone
var monoBehaviours = afile.GetAssetsOfType(AssetClassID.MonoBehaviour).ToList();
Console.WriteLine($"Found {monoBehaviours.Count} MonoBehaviours");

// Find weapon.generic_pdw_tier1_scp (or any weapon.* template)
AssetFileInfo? sourceInfo = null;
byte[]? sourceBytes = null;
string? foundId = null;

Console.WriteLine("Searching for weapon templates...");
int searched = 0;
foreach (var info in monoBehaviours)
{
    searched++;
    var offset = info.GetAbsoluteByteOffset(afile);
    afile.Reader.BaseStream.Position = offset;
    var bytes = afile.Reader.ReadBytes((int)info.ByteSize);

    // Look for any "weapon.*" pattern
    if (bytes.Length > 60)
    {
        for (int i = 12; i < Math.Min(150, bytes.Length - 10); i++)
        {
            if (i + 4 > bytes.Length) break;
            int len = BitConverter.ToInt32(bytes, i);
            if (len >= 8 && len <= 60 && i + 4 + len <= bytes.Length)
            {
                try
                {
                    var str = System.Text.Encoding.ASCII.GetString(bytes, i + 4, len);
                    if (str.StartsWith("weapon.") && str.Contains("pdw"))
                    {
                        Console.WriteLine($"Found '{str}' at PathId={info.PathId}, offset={i}, size={bytes.Length}");
                        sourceInfo = info;
                        sourceBytes = bytes;
                        foundId = str;
                        break;
                    }
                }
                catch { }
            }
        }
    }
    if (sourceInfo != null) break;
    if (searched % 1000 == 0) Console.WriteLine($"  Searched {searched} assets...");
}
Console.WriteLine($"Searched {searched} total MonoBehaviours");

// Also check what ScriptTypeIndex 0 assets look like - find any with non-zero m_Script
Console.WriteLine("\nLooking for assets with same TypeIdOrIndex that have valid m_Script...");
if (sourceInfo != null)
{
    int foundWithScript = 0;
    foreach (var info in monoBehaviours.Where(m => m.TypeIdOrIndex == sourceInfo.TypeIdOrIndex).Take(100))
    {
        var off = info.GetAbsoluteByteOffset(afile);
        afile.Reader.BaseStream.Position = off;
        var b = afile.Reader.ReadBytes(Math.Min(20, (int)info.ByteSize));

        // Check if m_Script PPtr is non-zero
        bool hasScript = b.Take(12).Any(x => x != 0);
        if (hasScript)
        {
            Console.WriteLine($"  PathId={info.PathId} has m_Script: {string.Join(" ", b.Take(12).Select(x => x.ToString("X2")))}");
            foundWithScript++;
            if (foundWithScript >= 5) break;
        }
    }
    if (foundWithScript == 0)
    {
        Console.WriteLine("  No assets with this TypeIdOrIndex have valid m_Script references!");
    }
}

if (sourceInfo == null || sourceBytes == null)
{
    Console.WriteLine("Could not find source asset!");
    return;
}

Console.WriteLine($"Source: PathId={sourceInfo.PathId}, TypeId={sourceInfo.TypeId}, TypeIdOrIndex={sourceInfo.TypeIdOrIndex}, ScriptTypeIndex={sourceInfo.ScriptTypeIndex}, ByteSize={sourceInfo.ByteSize}");
Console.WriteLine($"Source first 40 bytes: {string.Join(" ", sourceBytes.Take(40).Select(b => b.ToString("X2")))}");
// Analyze the m_Name at offset 12
int nameLen = BitConverter.ToInt32(sourceBytes, 12);
Console.WriteLine($"m_Name at offset 12: length={nameLen}");
if (nameLen >= 0 && nameLen < 100 && 16 + nameLen <= sourceBytes.Length)
{
    var nameStr = System.Text.Encoding.ASCII.GetString(sourceBytes, 16, nameLen);
    Console.WriteLine($"m_Name value: '{nameStr}' (hex: {string.Join(" ", sourceBytes.Skip(16).Take(nameLen).Select(b => b.ToString("X2")))})");
    int namePadding = (4 - (nameLen % 4)) % 4;
    int nameEnd = 16 + nameLen + namePadding;
    Console.WriteLine($"m_Name ends at offset {nameEnd} (with {namePadding} padding bytes)");
    Console.WriteLine($"Bytes {nameEnd} to {nameEnd+8}: {string.Join(" ", sourceBytes.Skip(nameEnd).Take(8).Select(b => b.ToString("X2")))}");
}

// Create clone - patch ONLY the m_ID at offset 28 (skip m_Name patching)
var cloneBytes = (byte[])sourceBytes.Clone();
var newId = "weapon.laser_smg";

// The template ID was found at offset 28 - let's patch it there
int idOffset = 28; // This is where "weapon.generic_pdw_tier1_scp" starts

// Read original ID at offset 28
int origIdLen = BitConverter.ToInt32(sourceBytes, idOffset);
Console.WriteLine($"Original m_ID at offset {idOffset}: length={origIdLen}");
if (origIdLen > 0 && origIdLen < 100)
{
    var origId = System.Text.Encoding.ASCII.GetString(sourceBytes, idOffset + 4, origIdLen);
    Console.WriteLine($"Original m_ID value: '{origId}'");
}

// Patch the m_ID
cloneBytes = PatchStringAtOffset(sourceBytes, idOffset, newId);
if (cloneBytes == null)
{
    Console.WriteLine("Failed to patch m_ID!");
    return;
}
Console.WriteLine($"Patched m_ID to '{newId}', new size: {cloneBytes.Length} (was {sourceBytes.Length})");

var nextPathId = afile.AssetInfos.Max(a => a.PathId) + 1;

Console.WriteLine($"Creating clone with PathId={nextPathId}");

var newInfo = new AssetFileInfo
{
    PathId = nextPathId,
    TypeIdOrIndex = sourceInfo.TypeIdOrIndex,
    TypeId = sourceInfo.TypeId,
    ScriptTypeIndex = sourceInfo.ScriptTypeIndex,
    Stripped = sourceInfo.Stripped
};

newInfo.SetNewData(cloneBytes);
afile.Metadata.AddAssetInfo(newInfo);

Console.WriteLine($"Added clone to metadata. Total assets now: {afile.AssetInfos.Count}");

// Write file
Console.WriteLine($"Writing to: {outputPath}");
using (var fs = File.Create(outputPath))
using (var writer = new AssetsFileWriter(fs))
{
    afile.Write(writer);
}

var outputSize = new FileInfo(outputPath).Length;
Console.WriteLine($"Written {outputSize / 1024 / 1024}MB ({outputSize} bytes)");

// Try to read it back
Console.WriteLine("Validating with AssetsTools.NET...");
var am2 = new AssetsManager();
try
{
    var inst2 = am2.LoadAssetsFile(outputPath, false);
    var afile2 = inst2.file;
    Console.WriteLine($"Validation: Loaded {afile2.AssetInfos.Count} assets");

    var clone = afile2.AssetInfos.FirstOrDefault(a => a.PathId == nextPathId);
    if (clone != null)
    {
        Console.WriteLine($"Clone found: PathId={clone.PathId}, ByteSize={clone.ByteSize}");

        // Read clone bytes
        var cloneOffset = clone.GetAbsoluteByteOffset(afile2);
        afile2.Reader.BaseStream.Position = cloneOffset;
        var readBytes = afile2.Reader.ReadBytes((int)clone.ByteSize);
        Console.WriteLine($"Clone first 20 bytes: {string.Join(" ", readBytes.Take(20).Select(b => b.ToString("X2")))}");

        // Compare with original
        if (readBytes.SequenceEqual(cloneBytes))
        {
            Console.WriteLine("SUCCESS: Clone bytes match original!");
        }
        else
        {
            Console.WriteLine("WARNING: Clone bytes differ from original!");
        }
    }
    else
    {
        Console.WriteLine("Clone NOT found!");
    }
    am2.UnloadAll();
}
catch (Exception ex)
{
    Console.WriteLine($"Validation FAILED: {ex.Message}");
}

am.UnloadAll();

// Now copy to game and see if it works
Console.WriteLine("\n=== Copying to game directory for testing ===");
var gamePath = Path.Combine(gameDataPath, "resources.assets");
var backupPath = gamePath + ".backup-test";

if (!File.Exists(backupPath) && File.Exists(gamePath))
{
    Console.WriteLine($"Creating backup: {backupPath}");
    File.Copy(gamePath, backupPath);
}

Console.WriteLine($"Copying test file to: {gamePath}");
File.Copy(outputPath, gamePath, true);

Console.WriteLine("\nDone! Try launching the game now.");
Console.WriteLine($"To restore: cp \"{backupPath}\" \"{gamePath}\"");

// Helper function
static byte[]? PatchStringAtOffset(byte[] sourceBytes, int offset, string newValue)
{
    if (sourceBytes == null || sourceBytes.Length <= offset + 4)
        return null;

    int origLen = BitConverter.ToInt32(sourceBytes, offset);
    if (origLen < 0 || origLen > 500 || offset + 4 + origLen > sourceBytes.Length)
        return null;

    int origPadding = (4 - (origLen % 4)) % 4;
    int origTotalLen = 4 + origLen + origPadding;

    int newLen = newValue.Length;
    int newPadding = (4 - (newLen % 4)) % 4;
    int newTotalLen = 4 + newLen + newPadding;

    int sizeDiff = newTotalLen - origTotalLen;

    var result = new byte[sourceBytes.Length + sizeDiff];

    Array.Copy(sourceBytes, 0, result, 0, offset);
    Array.Copy(BitConverter.GetBytes(newLen), 0, result, offset, 4);
    var newValueBytes = System.Text.Encoding.ASCII.GetBytes(newValue);
    Array.Copy(newValueBytes, 0, result, offset + 4, newLen);

    for (int i = 0; i < newPadding; i++)
        result[offset + 4 + newLen + i] = 0;

    int afterOrigString = offset + origTotalLen;
    int afterNewString = offset + newTotalLen;
    if (afterOrigString < sourceBytes.Length)
        Array.Copy(sourceBytes, afterOrigString, result, afterNewString, sourceBytes.Length - afterOrigString);

    return result;
}
