#!/usr/bin/env dotnet script
#r "nuget: AssetsTools.NET, 3.0.0"

using System;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

var ggmPath = args.Length > 0 ? args[0] : "/home/poss/.steam/debian-installation/steamapps/common/Menace/Menace_Data/globalgamemanagers";

Console.WriteLine($"Reading: {ggmPath}");

var ggmBytes = File.ReadAllBytes(ggmPath);
using var ggmStream = new MemoryStream(ggmBytes);
var am = new AssetsManager();
var ggmInst = am.LoadAssetsFile(ggmStream, "globalgamemanagers", false);
var ggmFile = ggmInst.file;

// Find ResourceManager asset (TypeId 147)
AssetFileInfo rmInfo = null;
foreach (var info in ggmFile.AssetInfos)
{
    if (info.TypeId == 147)
    {
        rmInfo = info;
        break;
    }
}

if (rmInfo == null)
{
    Console.WriteLine("ResourceManager not found!");
    return;
}

Console.WriteLine($"ResourceManager found: PathId={rmInfo.PathId}, ByteSize={rmInfo.ByteSize}");

// Read ResourceManager data
var reader = ggmFile.Reader;
reader.BaseStream.Position = rmInfo.GetAbsoluteByteOffset(ggmFile);
var rmBytes = reader.ReadBytes((int)rmInfo.ByteSize);

// Parse entries
int entryCount = BitConverter.ToInt32(rmBytes, 0);
Console.WriteLine($"Entry count: {entryCount}");

int offset = 4;
var searchTerms = new[] { "loading_bg", "faction_backbone", "faction_dice", "speakersbig", "unitleadersbig" };

for (int i = 0; i < entryCount && offset < rmBytes.Length - 4; i++)
{
    int strLen = BitConverter.ToInt32(rmBytes, offset);
    offset += 4;

    if (strLen <= 0 || strLen > 2000 || offset + strLen > rmBytes.Length)
        break;

    string path = System.Text.Encoding.UTF8.GetString(rmBytes, offset, strLen);
    offset += strLen;
    offset += (4 - (strLen % 4)) % 4;

    if (offset + 12 > rmBytes.Length) break;
    int fileId = BitConverter.ToInt32(rmBytes, offset);
    long pathId = BitConverter.ToInt64(rmBytes, offset + 4);
    offset += 12;

    // Print if matches search terms
    bool matches = searchTerms.Any(t => path.Contains(t, StringComparison.OrdinalIgnoreCase));
    if (matches)
    {
        Console.WriteLine($"  [{i}] FileId={fileId}, PathId={pathId}, Path={path}");
    }
}

Console.WriteLine("Done.");
am.UnloadAll();
