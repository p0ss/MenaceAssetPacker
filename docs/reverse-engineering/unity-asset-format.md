# Unity Asset Binary Format

## Overview

This document describes the binary structure of Unity serialized assets (`.assets` files) as relevant to the Menace modkit's asset patching system. Understanding these structures is critical for maintaining the `BundleCompiler` and related code.

## Why Binary Patching?

Unity 6 IL2CPP builds **do not embed TypeTrees** in standalone builds. TypeTrees are the metadata that describes field layouts, enabling tools like UABEA to parse assets generically. Without them, we must:

1. Rely on known field offsets from reverse engineering
2. Use pattern matching to find fields dynamically
3. Clone existing assets and patch specific bytes

## SerializedFile Structure

A `.assets` file (SerializedFile) contains:

```
┌────────────────────────────────────────┐
│ Header                                 │
│ ├── metadata_size                      │
│ ├── file_size                          │
│ ├── version                            │
│ └── data_offset                        │
├────────────────────────────────────────┤
│ Metadata                               │
│ ├── Unity version string               │
│ ├── Target platform                    │
│ ├── Type definitions (if TypeTrees)    │
│ └── Asset info array                   │
│     ├── PathID (int64)                 │
│     ├── Offset (uint32/64)             │
│     ├── Size (uint32)                  │
│     └── TypeID (int32)                 │
├────────────────────────────────────────┤
│ Asset Data                             │
│ ├── Asset 1 bytes                      │
│ ├── Asset 2 bytes                      │
│ └── ...                                │
└────────────────────────────────────────┘
```

## Object Serialization

### Common Types

| Type | Size | Notes |
|------|------|-------|
| `int8` | 1 byte | Signed byte |
| `uint8` | 1 byte | Unsigned byte |
| `int16` | 2 bytes | Little-endian |
| `uint16` | 2 bytes | Little-endian |
| `int32` | 4 bytes | Little-endian |
| `uint32` | 4 bytes | Little-endian |
| `int64` | 8 bytes | Little-endian |
| `uint64` | 8 bytes | Little-endian |
| `float` | 4 bytes | IEEE 754 |
| `bool` | 1 byte | 0 or 1, followed by 3 padding bytes |
| `string` | 4 + N + padding | See below |

### String Serialization

Strings are length-prefixed with 4-byte alignment:

```
┌─────────────────────────────────────────────────┐
│ Length (int32)    │ Characters (N bytes)        │
│                   │ Padding (0-3 bytes to align)│
└─────────────────────────────────────────────────┘
```

**Example:** String "weapon.laser_rifle" (18 chars)
```
Offset  Content
0x00    12 00 00 00          // Length = 18 (0x12)
0x04    77 65 61 70 6F 6E    // "weapon"
0x0A    2E                   // "."
0x0B    6C 61 73 65 72       // "laser"
0x10    5F                   // "_"
0x11    72 69 66 6C 65       // "rifle"
0x16    00 00                // 2 bytes padding (18 + 4 = 22, align to 24)
```

Total size: 4 (length) + 18 (chars) + 2 (padding) = 24 bytes

**Alignment formula:**
```csharp
int padding = (4 - (length % 4)) % 4;
int totalSize = 4 + length + padding;
```

### PPtr (Object Reference)

PPtrs reference other assets:

```
┌───────────────────────────────────────┐
│ FileID (int32)  │ PathID (int64)      │
│ 4 bytes         │ 8 bytes             │
└───────────────────────────────────────┘
Total: 12 bytes
```

- `FileID = 0`: Reference to asset in same file
- `FileID > 0`: Index into external references array
- `PathID`: Unique identifier within the target file

## MonoBehaviour Structure

MonoBehaviours (scripts attached to GameObjects) have this header:

```
Offset  Size  Field
0x00    12    m_GameObject (PPtr<GameObject>)
0x0C    4     m_Enabled (bool + 3 padding)
0x10    12    m_Script (PPtr<MonoScript>)
0x1C    ...   [Script-specific fields starting with m_Name]
```

**Total header: 28 bytes**

After the header, the script's serialized fields begin, typically starting with `m_Name`.

## ScriptableObject Structure

ScriptableObjects (standalone data assets like DataTemplates) have a **simpler** header:

```
Offset  Size  Field
0x00    12    m_Script (PPtr<MonoScript>)
0x0C    ...   [Script-specific fields starting with m_Name]
```

**Total header: 12 bytes**

ScriptableObjects don't have `m_GameObject` or `m_Enabled` because they're not attached to GameObjects.

### DataTemplate Layout

DataTemplates in this game are ScriptableObjects with this structure:

```
Offset  Size  Field
0x00    12    m_Script (PPtr<MonoScript>)
0x0C    var   m_Name (string) - Display name
        var   [padding to align]
        var   m_ID (string) - Template identifier (e.g., "weapon.laser_rifle")
        ...   [Type-specific fields]
```

**Critical insight:** For DataTemplates, both `m_Name` and `m_ID` typically contain the template identifier (e.g., "weapon.laser_rifle").

## Pattern Matching for Template IDs

Since we don't have TypeTrees, `FindTemplateId` uses pattern matching:

```csharp
// Template ID pattern: "category.name"
// - Category: 2-20 lowercase letters/underscores
// - Separator: exactly one dot
// - Name: lowercase letters, numbers, underscores, dots

bool IsTemplateId(string s) {
    int dotPos = s.IndexOf('.');
    if (dotPos < 2 || dotPos > 20) return false;

    // Validate prefix (before first dot)
    for (int i = 0; i < dotPos; i++) {
        char c = s[i];
        if (!((c >= 'a' && c <= 'z') || c == '_')) return false;
    }

    // Validate name (after first dot)
    for (int i = dotPos + 1; i < s.Length; i++) {
        char c = s[i];
        if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '.'))
            return false;
    }

    return true;
}
```

The search starts at **offset 12** (after `m_Script`) and scans for length-prefixed strings matching this pattern.

## Texture2D Structure

Texture2D assets have this layout (Unity 6):

```
Offset  Size  Field
0x00    var   m_Name (string)
        4     m_ForcedFallbackFormat (int32)
        4     m_Width (int32)
        4     m_Height (int32)
        4     m_CompleteImageSize (int32)
        4     m_MipsStripped (int32)
        4     m_TextureFormat (int32) - See TextureFormat enum
        4     m_MipCount (int32)
        ...   [Several bool fields, aligned]
        4     m_ColorSpace (int32) - 0=sRGB, 1=Linear
        4     m_LightmapFormat (int32)
        ...   [TextureSettings struct]
        var   image data (byte array: length + data)
        var   m_StreamData (string + uint64 + uint64)
```

### TextureFormat Values

| Value | Format | Bytes/Pixel |
|-------|--------|-------------|
| 4 | RGBA32 | 4 |
| 5 | ARGB32 | 4 |
| 7 | RGB24 | 3 |
| 10 | DXT1 | 0.5 (compressed) |
| 12 | DXT5 | 1 (compressed) |
| 34 | ETC2_RGBA8 | 1 (compressed) |

### Color Space

```
0 = sRGB/Gamma  - Standard for diffuse textures, UI, sprites
1 = Linear      - For normal maps, height maps, data textures
```

**Important:** Using the wrong color space causes textures to appear washed out or too dark.

## Sprite Structure

Sprites reference a Texture2D and define a rect within it:

```
Offset  Size  Field
0x00    var   m_Name (string)
        12    m_Texture (PPtr<Texture2D>)
        16    m_Rect (float x, y, width, height)
        8     m_Pivot (float x, y)
        4     m_PixelsPerUnit (float)
        ...   [Additional sprite data]
```

## AudioClip Structure

```
Offset  Size  Field
0x00    var   m_Name (string)
        4     m_LoadType (int32)
        4     m_Channels (int32)
        4     m_Frequency (int32)
        4     m_BitsPerSample (int32)
        4     m_Length (float) - Duration in seconds
        ...   [Format-specific data]
        var   m_AudioData (byte array)
```

## Binary Patching Operations

### Cloning Assets

When cloning a DataTemplate:

1. Copy source asset bytes entirely
2. Patch `m_Name` at offset 12:
   - Calculate new aligned size
   - Shift all subsequent data if size differs
   - Write new length + characters + padding
3. Patch `m_ID` at adjusted offset:
   - Original offset + (new m_Name size - old m_Name size)
   - Same string patching process

### String Patching Algorithm

```csharp
byte[] PatchString(byte[] bytes, int offset, string newValue) {
    // Read old string
    int oldLen = BitConverter.ToInt32(bytes, offset);
    int oldPadding = (4 - (oldLen % 4)) % 4;
    int oldTotal = 4 + oldLen + oldPadding;

    // Calculate new size
    int newLen = newValue.Length;
    int newPadding = (4 - (newLen % 4)) % 4;
    int newTotal = 4 + newLen + newPadding;

    // Create result buffer
    int sizeDiff = newTotal - oldTotal;
    byte[] result = new byte[bytes.Length + sizeDiff];

    // Copy before string
    Array.Copy(bytes, 0, result, 0, offset);

    // Write new string
    Array.Copy(BitConverter.GetBytes(newLen), 0, result, offset, 4);
    Array.Copy(Encoding.UTF8.GetBytes(newValue), 0, result, offset + 4, newLen);
    // Padding bytes are already 0

    // Copy after string
    Array.Copy(bytes, offset + oldTotal, result, offset + newTotal,
               bytes.Length - offset - oldTotal);

    return result;
}
```

## ResourceManager Patching

The `ResourceManager` in `globalgamemanagers` maps resource paths to assets:

```
ResourceManager structure:
├── m_Container (array)
│   ├── [0] { path: "data/entities/weapon.laser_rifle", asset: PPtr }
│   ├── [1] { path: "data/skills/skill.overwatch", asset: PPtr }
│   └── ...
```

When adding new assets, we must add entries here for `Resources.Load()` to find them.

## Offset Constants Summary

| Context | Offset | Field |
|---------|--------|-------|
| ScriptableObject | 0 | m_Script (PPtr) |
| ScriptableObject | 12 | m_Name (string) |
| MonoBehaviour | 0 | m_GameObject (PPtr) |
| MonoBehaviour | 12 | m_Enabled (bool) |
| MonoBehaviour | 16 | m_Script (PPtr) |
| MonoBehaviour | 28 | m_Name (string) |

**Our code uses:**
- `MONOBEHAVIOUR_NAME_OFFSET = 12` (correct for ScriptableObjects)
- `FindTemplateId` starts at 12 (correct for ScriptableObjects)

## External References

- [disunity wiki - Serialized file format](https://github.com/ata4/disunity/wiki/Serialized-file-format)
- [UnityPack Format Documentation](https://github.com/HearthSim/UnityPack/wiki/Format-Documentation)
- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) - The library we use
- [UABEA](https://github.com/nesrak1/UABEA) - Built on AssetsTools.NET

## Common Pitfalls

1. **Confusing MonoBehaviour and ScriptableObject offsets** - DataTemplates are ScriptableObjects (offset 12), not MonoBehaviours (offset 28)

2. **Forgetting string alignment** - Strings must be padded to 4-byte boundaries

3. **Wrong color space** - Most textures should use sRGB (0), not Linear (1)

4. **Not adjusting offsets after patching** - When string size changes, all subsequent offsets shift

5. **Assuming TypeTrees exist** - Unity 6 IL2CPP builds don't include them
