# Offset Stability & API Considerations

## Can You Build an API on These Offsets?

**Short answer:** Yes, but with significant caveats.

**Long answer:** IL2CPP offsets are game-version-specific. A robust API requires version detection and offset tables per version.

## What Changes Offsets

### 1. Field Additions

Adding a new field to a class shifts all subsequent fields:

```c
// Version 1.0
class Entity {
    int m_ID;          // +0x10
    bool m_IsAlive;    // +0x14
}

// Version 1.1 - New field added
class Entity {
    int m_ID;          // +0x10
    int m_NewField;    // +0x14  <- NEW
    bool m_IsAlive;    // +0x18  <- SHIFTED
}
```

### 2. Field Reordering

Compiler or developer reordering changes layout:

```c
// Before
class Stats {
    int m_Health;      // +0x10
    float m_Speed;     // +0x14
}

// After (developer reordered for logical grouping)
class Stats {
    float m_Speed;     // +0x10
    int m_Health;      // +0x14
}
```

### 3. Base Class Changes

Changes to parent classes cascade:

```c
// If Entity (base) adds a field
class Actor : Entity {
    // All Actor fields shift by parent's new field size
}
```

### 4. Unity Version Changes

Unity updates can change:
- UnityEngine class layouts
- IL2CPP codegen alignment rules
- Metadata format

### 5. Compiler/IL2CPP Version

Different IL2CPP versions may:
- Reorder fields for alignment optimization
- Change virtual method table layouts
- Alter generic instantiation

## What Stays Stable

### Template Field Order

ScriptableObject-derived templates (SkillTemplate, EntityTemplate, etc.) are **more stable** because:
- Field order is defined in C# source
- Unity serializes based on declared order
- Developers rarely reorder serialized fields

### Method Signatures

Method signatures are stable within a version:
- Parameter types don't change for existing methods
- Return types remain consistent

### Class Hierarchy

Base class relationships are very stable:
- `Actor : Entity` won't become `Actor : Component`
- Interface implementations persist

## Building a Robust API

### Strategy 1: Version-Specific Offset Tables

```csharp
public static class Offsets {
    private static readonly Dictionary<string, OffsetTable> VersionTables = new() {
        ["1.0.0"] = new OffsetTable {
            EntityProperties_Accuracy = 0x68,
            EntityProperties_AccuracyMult = 0x6C,
            // ...
        },
        ["1.1.0"] = new OffsetTable {
            EntityProperties_Accuracy = 0x70,  // Shifted
            EntityProperties_AccuracyMult = 0x74,
            // ...
        }
    };

    public static OffsetTable GetForVersion(string version) {
        return VersionTables[version];
    }
}
```

### Strategy 2: Pattern Scanning

Find offsets at runtime by scanning for known patterns:

```csharp
// Find GetAccuracy method, extract offset from code
nint GetAccuracyMethod = FindMethod("EntityProperties", "GetAccuracy");
byte[] code = ReadMemory(GetAccuracyMethod, 32);
// Parse x64 instructions to find field offset
int accuracyOffset = ExtractFieldOffset(code);
```

### Strategy 3: Metadata-Based Discovery

Use Il2CppDumper output at runtime:

```csharp
// Parse dump.cs or il2cpp metadata
var entityProps = Il2CppMetadata.FindClass("EntityProperties");
var accuracyField = entityProps.FindField("m_Accuracy");
int offset = accuracyField.Offset;
```

### Strategy 4: Harmony Patching (Recommended)

Avoid offsets entirely by patching methods:

```csharp
[HarmonyPatch(typeof(EntityProperties), "GetAccuracy")]
class AccuracyPatch {
    static void Postfix(EntityProperties __instance, ref float __result) {
        // Modify accuracy without knowing field offsets
        __result *= 1.5f;
    }
}
```

## Recommended Approach

For your modkit, I recommend a **hybrid approach**:

### 1. Use Harmony for Behavior Changes

```csharp
// Modify how accuracy is calculated - no offsets needed
[HarmonyPatch(typeof(EntityProperties), "GetAccuracy")]
class AccuracyPatch { ... }
```

### 2. Use Reflection for Field Access

```csharp
// Access fields by name - survives layout changes
var field = typeof(EntityProperties).GetField("m_Accuracy",
    BindingFlags.NonPublic | BindingFlags.Instance);
float accuracy = (float)field.GetValue(entityProps);
```

### 3. Cache Offsets with Version Check

```csharp
public class OffsetCache {
    private static int? accuracyOffset;

    public static int GetAccuracyOffset() {
        if (accuracyOffset == null) {
            // Get field offset via Il2Cpp API
            var field = Il2CppClass.Find("EntityProperties")
                .GetField("m_Accuracy");
            accuracyOffset = field.Offset;
        }
        return accuracyOffset.Value;
    }
}
```

### 4. Version Detection

```csharp
public static string GetGameVersion() {
    // Read from game files or Application.version
    return Application.version;
}

public static void ValidateOffsets() {
    string version = GetGameVersion();
    if (!SupportedVersions.Contains(version)) {
        Logger.Warning($"Untested game version: {version}. Offsets may be incorrect.");
    }
}
```

## Risk Assessment by Category

| Category | Stability | Risk |
|----------|-----------|------|
| Template field order | High | Low |
| EntityProperties layout | Medium | Medium |
| Actor layout | Medium | Medium |
| Skill layout | Medium | Medium |
| UI classes | Low | High |
| Internal helpers | Very Low | Very High |

## Practical Recommendations

### DO:
- Use Harmony patching over direct memory access
- Use reflection when possible
- Implement version checks
- Log warnings for unknown versions
- Test each game update before releasing mod updates

### DON'T:
- Hardcode offsets without version awareness
- Assume offsets are stable across updates
- Skip null checks when accessing fields
- Release mods without testing on current game version

## Maintaining Offset Tables

When the game updates:

1. Run Il2CppDumper on new version
2. Compare dump.cs to identify changed classes
3. Update offset tables for changed classes
4. Test all functionality
5. Release mod update

### Automation

Consider automating offset extraction:

```bash
#!/bin/bash
# Run after each game update
./Il2CppDumper GameAssembly.dll global-metadata.dat output/
python extract_offsets.py output/dump.cs > offsets_v${VERSION}.json
```

## Conclusion

You **can** build an API on these offsets, but it requires:
1. Version awareness
2. Offset tables per version
3. Update process for each game patch
4. Fallback to Harmony/reflection when possible

The safest approach combines Harmony patching (which survives most changes) with version-specific offset tables for performance-critical direct memory access.
