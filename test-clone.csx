#!/usr/bin/env dotnet-script
#r "nuget: AssetsTools.NET, 3.0.0"

using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.IO;
using System.Linq;

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

// Find weapon.generic_pdw_tier1_scp
AssetFileInfo? sourceInfo = null;
byte[]? sourceBytes = null;

foreach (var info in monoBehaviours.Take(5000))
{
    var offset = info.GetAbsoluteByteOffset(afile);
    afile.Reader.BaseStream.Position = offset;
    var bytes = afile.Reader.ReadBytes((int)info.ByteSize);

    // Check for template ID at offset 28 (after m_Script PPtr + m_Name)
    if (bytes.Length > 60)
    {
        // Look for "weapon.generic_pdw_tier1_scp" pattern
        for (int i = 12; i < Math.Min(100, bytes.Length - 30); i++)
        {
            if (i + 4 > bytes.Length) break;
            int len = BitConverter.ToInt32(bytes, i);
            if (len >= 10 && len <= 50 && i + 4 + len <= bytes.Length)
            {
                var str = System.Text.Encoding.ASCII.GetString(bytes, i + 4, len);
                if (str == "weapon.generic_pdw_tier1_scp")
                {
                    Console.WriteLine($"Found source at PathId={info.PathId}, offset={i}, size={bytes.Length}");
                    sourceInfo = info;
                    sourceBytes = bytes;
                    break;
                }
            }
        }
    }
    if (sourceInfo != null) break;
}

if (sourceInfo == null)
{
    Console.WriteLine("Could not find source asset!");
    return;
}

Console.WriteLine($"Source: PathId={sourceInfo.PathId}, TypeId={sourceInfo.TypeId}, TypeIdOrIndex={sourceInfo.TypeIdOrIndex}, ScriptTypeIndex={sourceInfo.ScriptTypeIndex}, ByteSize={sourceInfo.ByteSize}");
Console.WriteLine($"Source first 20 bytes: {string.Join(" ", sourceBytes.Take(20).Select(b => b.ToString("X2")))}");

// Create clone - just copy bytes for now (no patching)
var cloneBytes = (byte[])sourceBytes.Clone();
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

Console.WriteLine($"Written {new FileInfo(outputPath).Length / 1024 / 1024}MB");

// Try to read it back
Console.WriteLine("Validating...");
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
Console.WriteLine("Done!");
