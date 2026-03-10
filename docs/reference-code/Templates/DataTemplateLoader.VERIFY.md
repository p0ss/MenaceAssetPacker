# DataTemplateLoader.cs Verification Report

**Verification Date:** 2026-03-10
**Binary Addresses Verified Against:** Ghidra decompilation
**Status:** PARTIALLY ACCURATE - Several architectural differences found

---

## Executive Summary

The reference code in `DataTemplateLoader.cs` provides a reasonable high-level abstraction of the template loading system, but the actual binary implementation differs significantly in structure. The core concepts (type-to-path mapping, caching, Resources.LoadAll) are correct, but the implementation details differ.

---

## Function-by-Function Verification

### 1. GetBaseFolder (Address: 0x18052dea0)

**Reference Code Claims:**
```csharp
public static string GetBaseFolder(Type templateType)
{
    // Check direct mapping via dictionary
    if (TemplatePaths.TryGetValue(templateType, out string path))
        return path;

    // Check inheritance (for BaseItemTemplate subclasses, etc.)
    foreach (var kvp in TemplatePaths)
    {
        if (templateType.IsSubclassOf(kvp.Key))
            return kvp.Value;
    }

    return string.Empty;
}
```

**Actual Implementation (Decompiled):**

The actual function `DataTemplateLoader_GetBaseFolder_MapsTypeToResourcePath` does NOT use a Dictionary. Instead, it:

1. **Uses sequential type comparisons:** The function compares the input Type against each known template type using `Unity_Burst_Unsafe__AreSame<>()` calls in a large if-else chain.

2. **Has inline class initialization:** The function first initializes all template type classes via `il2cpp_class_init_lock()` calls on first invocation (guarded by `DAT_183b7f298`).

3. **Returns string literals directly:** Each type match returns a specific StringLiteral reference (e.g., `StringLiteral_7663`, `StringLiteral_7671`, etc.)

4. **Supports inheritance checking:** For certain types (BaseItemTemplate, BasePlayerSettingTemplate, TileEffectTemplate), it uses virtual method calls via `(*param_1 + 0x2a8)` to check `IsSubclassOf`.

5. **Logs warning for unknown types:** If no match found, logs a warning message via `LogWarning()`.

**Verified Template Types (from decompiled code):**
- AnimationSequenceTemplate
- AnimationSoundTemplate
- ArmyTemplate
- BaseItemTemplate (with inheritance support)
- BasePlayerSettingTemplate (with inheritance support)
- BiomeTemplate
- BlackMarketConfig
- CampaignProgressConfig
- ConversationEffectsTemplate
- ConversationStageTemplate
- EmotionalStateTemplate
- EmotionalStatesConfig
- EnemyAssetTemplate
- EntityTemplate
- FactionTemplate
- GameConfig
- GenericMissionTemplate
- GlobalAnimatorConfig (NOT in reference code)
- GlobalDifficultyTemplate
- ItemFilterTemplate
- ItemListTemplate
- LoadingQuoteConfig
- MissionDifficultyTemplate
- MissionPOITemplate
- MissionPreviewConfigTemplate (NOT in reference code)
- MissionSetpiece
- MissionTemplate
- ModularVehicleTemplate
- OffmapAbilityTemplate
- OperationAssetTemplate
- OperationDurationTemplate
- OperationIntrosTemplate
- OperationTemplate
- PerkTemplate
- PerkTreeTemplate
- PlanetTemplate
- PropertyDisplayConfigTemplate
- RewardTableTemplate
- ShipUpgradeSlotTemplate
- ShipUpgradeTemplate
- SkillTemplate
- SpeakerTemplate
- SquaddiesConfig
- StoryFactionTemplate
- StrategicAssetTemplate
- StrategyConfig
- SurfaceTypeTemplate
- TacticalConfig
- TagTemplate
- TextTooltipsConfig
- TileEffectTemplate (with inheritance support)
- UIConfig
- UnitLeaderTemplate
- UnitRankTemplate
- VideoTemplate
- WeatherTemplate

**Discrepancies:**

| Issue | Reference | Actual |
|-------|-----------|--------|
| Data structure | Dictionary<Type, string> | Sequential if-else type comparisons |
| Separate WeaponTemplate/ArmorTemplate paths | Yes ("Data/Items/Weapons/", "Data/Items/Armor/") | No - these appear to use BaseItemTemplate inheritance path |
| GlobalAnimatorConfig | Not listed | Present in binary |
| MissionPreviewConfigTemplate | Not listed | Present in binary |
| AnimatedSequenceConfig | Listed | Present (same as AnimationSequenceConfig) |
| Return type handling | string.Empty for not found | Returns StringLiteral_7655 + logs warning |

**VERDICT: PARTIALLY CORRECT**
- Core concept of type-to-path mapping is accurate
- Dictionary implementation is a simplification - actual code uses if-else chain
- Most template types are correctly identified
- Some template types missing from reference
- Inheritance handling exists but is limited to specific types

---

### 2. GetAll<T> (Address: 0x180a58e60)

**Reference Code Claims:**
```csharp
public static IReadOnlyCollection<T> GetAll<T>() where T : DataTemplate
{
    Type type = typeof(T);

    // Check cache
    if (Instance.m_TemplateArrays.TryGetValue(type, out var cached))
        return (T[])cached;

    // Load and cache
    Instance.LoadTemplates<T>(out T[] templates, out var map);
    Instance.m_TemplateArrays[type] = templates;
    Instance.m_TemplateMaps[type] = map;

    return templates;
}
```

**Actual Implementation (Decompiled):**

The function `Menace_Tools_DataTemplateLoader__GetAll<object>` shows:

1. **Singleton access:** Calls `Menace_Tools_DataTemplateLoader__GetSingleton(0)` to get instance (offset stored at `+0x10`)

2. **Cache lookup:** Uses `System_Collections_Generic_Dictionary<object,_object>__TryGetValue` with method reference `Method_System_Collections_Generic_Dictionary<Type,_DataTemplate[]>_TryGetValue__`

3. **Field offsets:**
   - Instance at offset `+0x10` holds the templates dictionary
   - Generic parameter info at `param_1 + 0x38`

4. **On cache miss:** Calls `Menace_Tools_DataTemplateLoader__LoadTemplates<object>` with output parameters

5. **Debug logging:** Contains verbose logging when `LogVerbose(7,0)` returns true

6. **Type casting:** Includes type safety checks via `thunk_FUN_1804607c0` for array type verification

**Discrepancies:**

| Issue | Reference | Actual |
|-------|-----------|--------|
| Singleton access | Instance property | GetSingleton() method call |
| Cache dictionary offset | m_TemplateArrays field | Offset +0x10 from instance |
| Logging | None | Verbose logging present |
| Type casting | Simple cast | Runtime type verification |

**VERDICT: MOSTLY CORRECT**
- Core logic is accurate (check cache, load if missing, return)
- Field access patterns match
- Singleton pattern exists but uses method instead of property
- Additional logging not shown in reference

---

### 3. LoadTemplates<T> (Address: 0x180a595d0)

**Reference Code Claims:**
```csharp
private void LoadTemplates<T>(out T[] templates, out Dictionary<string, DataTemplate> map)
    where T : DataTemplate
{
    string folder = GetBaseFolder(typeof(T));

    if (string.IsNullOrEmpty(folder))
    {
        templates = Array.Empty<T>();
        map = new Dictionary<string, DataTemplate>();
        return;
    }

    // Load all assets from Resources folder
    templates = Resources.LoadAll<T>(folder);

    // Build name -> template map for fast lookup
    map = new Dictionary<string, DataTemplate>(templates.Length);
    foreach (var template in templates)
    {
        map[template.name] = template;
    }
}
```

**Actual Implementation (Decompiled):**

The function `Menace_Tools_DataTemplateLoader__LoadTemplates<object>` shows:

1. **Timing/Debug:** Creates a `StopwatchScope` for performance measurement

2. **GetBaseFolder call:** Correctly calls `DataTemplateLoader_GetBaseFolder_MapsTypeToResourcePath`

3. **Resources.LoadAll:** Uses `UnityEngine_Resources__LoadAll<object>` with the folder path

4. **Empty check:** If `templates.Length < 1`, logs a warning (not shown in reference):
   - Warning message format: "No {TypeName} found in folder {folder}"

5. **Dictionary operations:**
   - Creates dictionary: `Dictionary<string, DataTemplate>`
   - For each template, calls `Menace_Tools_DataTemplate__GetID` (NOT `.name`)
   - Checks for duplicate keys via `ContainsKey`
   - If duplicate found, logs ERROR (not just overwrites)

6. **Caching:**
   - Stores in dictionary at offset `+0x10` (templates array by type)
   - Stores in dictionary at offset `+0x18` (name-to-template map by type)

7. **GetID vs name:** Uses `DataTemplate.GetID()` method instead of `.name` property

**Discrepancies:**

| Issue | Reference | Actual |
|-------|-----------|--------|
| Template identifier | template.name | DataTemplate.GetID() |
| Duplicate handling | Overwrites silently | Logs ERROR, then overwrites |
| Empty result handling | Returns silently | Logs warning |
| Performance tracking | None | StopwatchScope timing |
| Field offsets | m_TemplateArrays, m_TemplateMaps | +0x10 and +0x18 |

**VERDICT: PARTIALLY CORRECT**
- Core algorithm is correct (get path, load resources, build map)
- IMPORTANT: Uses GetID() not .name for dictionary keys
- Additional error handling and logging present
- Performance measurement not mentioned

---

## Field Offset Summary

From the decompiled code, the DataTemplateLoader instance has:

| Offset | Field | Type |
|--------|-------|------|
| +0x10 | Template arrays cache | Dictionary<Type, DataTemplate[]> |
| +0x18 | Template name maps | Dictionary<Type, Dictionary<string, DataTemplate>> |

---

## Missing From Reference Code

1. **GlobalAnimatorConfig** - Template type exists in binary but not in reference
2. **MissionPreviewConfigTemplate** - Template type exists in binary but not in reference
3. **DataTemplate.GetID()** - Method used instead of `.name` property
4. **StopwatchScope** - Performance measurement wrapper
5. **LogVerbose/LogDebug/LogWarning calls** - Extensive logging infrastructure
6. **Duplicate template ID detection** - Error logging for conflicts

---

## Corrections Needed

### High Priority

1. **Change template key from `.name` to `GetID()`:**
```csharp
// INCORRECT:
map[template.name] = template;

// CORRECT:
map[template.GetID()] = template;
```

2. **Add missing template types to TemplatePaths:**
```csharp
{ typeof(GlobalAnimatorConfig), "Config/GlobalAnimatorConfig" },  // Singleton
{ typeof(MissionPreviewConfigTemplate), "Data/MissionPreviews/" }, // Path TBD
```

### Medium Priority

3. **Document GetID() method:**
```csharp
/// <summary>
/// DataTemplate.GetID() returns the template's unique identifier.
/// This may be the asset name or a custom ID field.
/// </summary>
public abstract string GetID();
```

4. **Add duplicate detection:**
```csharp
if (map.ContainsKey(id))
{
    Debug.LogError($"Duplicate template ID '{id}' in type {typeof(T).Name}");
}
map[id] = template;
```

### Low Priority

5. **Add performance measurement comments:**
```csharp
// Note: Actual implementation uses StopwatchScope for timing
```

6. **Add logging documentation:**
```csharp
// Note: Debug/Verbose logging occurs at various points
```

---

## Architectural Notes

The reference code uses a Dictionary-based approach for TemplatePaths, which is a clean abstraction. The actual binary uses a large if-else chain of type comparisons, likely for performance reasons (avoiding Dictionary lookup overhead and allowing inlining).

The singleton pattern is implemented via a `GetSingleton()` method rather than a property, which is typical for IL2CPP compiled Unity code.

The use of `Unity_Burst_Unsafe__AreSame<>()` for type comparison is a low-level optimization that wouldn't appear in C# source code directly.

---

## Conclusion

The reference code provides a **reasonable high-level understanding** of the DataTemplateLoader system. The main concepts are correct:
- Templates are loaded via Resources.LoadAll from type-specific paths
- Results are cached by type
- Both array and dictionary caches are maintained

However, users relying on this for modding or reverse engineering should note:
- Template keys use `GetID()` not `.name`
- Some template types are missing
- The actual implementation has more error handling and logging
- The Dictionary-based path mapping is a simplification

**Overall Accuracy: 75%** - Conceptually correct, but implementation details differ.
