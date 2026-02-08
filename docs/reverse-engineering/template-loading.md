# Template Loading System

## Overview

Templates in Menace are loaded via `DataTemplateLoader` which maps template types to resource paths under the `Data/` folder.

## DataTemplateLoader.GetBaseFolder Mappings

From decompilation of `GetBaseFolder` (0x18052dea0), here are the type → path mappings:

### Templates with Resource Paths (Loadable via Resources.LoadAll)

| Template Type | Resource Path | Notes |
|---------------|---------------|-------|
| AnimationSequenceTemplate | `Data/???` | StringLiteral_7663 |
| AnimationSoundTemplate | `Data/???` | StringLiteral_7671 |
| ArmyTemplate | `Data/???` | StringLiteral_7679 |
| ConversationEffectsTemplate | `Data/???` | StringLiteral_7831 |
| ConversationStageTemplate | `Data/???` | StringLiteral_7847 |
| SpeakerTemplate | `Data/???` | StringLiteral_7839 |
| BiomeTemplate | `Data/???` | StringLiteral_7687 |
| EmotionalStateTemplate | `Data/???` | StringLiteral_7855 |
| EnemyAssetTemplate | `Data/???` | StringLiteral_7983 |
| StrategicAssetTemplate | `Data/???` | StringLiteral_7991 |
| OperationAssetTemplate | `Data/???` | StringLiteral_7975 |
| FactionTemplate | `Data/???` | StringLiteral_7871 |
| StoryFactionTemplate | `Data/???` | StringLiteral_7911 |
| GlobalDifficultyTemplate | `Data/???` | StringLiteral_7887 |
| MissionTemplate | `Data/???` | StringLiteral_7943 |
| GenericMissionTemplate | `Data/???` | StringLiteral_7943 |
| MissionDifficultyTemplate | `Data/???` | StringLiteral_7927 |
| MissionPOITemplate | `Data/???` | StringLiteral_7919 |
| MissionSetpiece | `Data/???` | StringLiteral_7935 |
| OffmapAbilityTemplate | `Data/???` | StringLiteral_7967 |
| OperationDurationTemplate | `Data/???` | StringLiteral_7999 |
| OperationTemplate | `Data/???` | StringLiteral_8007 |
| OperationIntrosTemplate | `Data/???` | StringLiteral_7879 |
| ModularVehicleTemplate | `Data/???` | StringLiteral_7951 |
| PerkTemplate | `Data/???` | StringLiteral_8087 |
| PerkTreeTemplate | `Data/???` | StringLiteral_8015 |
| PlanetTemplate | `Data/???` | StringLiteral_8023 |
| BasePlayerSettingTemplate | `Data/???` | StringLiteral_8031 |
| ShipUpgradeTemplate | `Data/???` | StringLiteral_8071 |
| ShipUpgradeSlotTemplate | `Data/???` | StringLiteral_8063 |
| SkillTemplate | `Data/???` | StringLiteral_8079 |
| UnitLeaderTemplate | `Data/???` | StringLiteral_8127 |
| UnitRankTemplate | `Data/???` | StringLiteral_8135 |
| RewardTableTemplate | `Data/???` | StringLiteral_8055 |
| TileEffectTemplate | `Data/???` | StringLiteral_8119 |
| WeatherTemplate | `Data/???` | StringLiteral_8151 |
| SurfaceTypeTemplate | `Data/???` | StringLiteral_8103 |
| TagTemplate | `Data/???` | StringLiteral_8111 |
| PropertyDisplayConfigTemplate | `Data/???` | StringLiteral_8047 |
| EntityTemplate | `Data/???` | StringLiteral_7863 |
| VideoTemplate | `Data/???` | StringLiteral_8143 |
| ItemListTemplate | `Data/???` | StringLiteral_7903 |
| ItemFilterTemplate | `Data/???` | StringLiteral_7895 |
| BaseItemTemplate | `Data/???` | Uses inheritance check |

### Singleton Configs (No folder path - loaded individually)

These return specific singleton paths rather than folders:
- BlackMarketConfig
- CampaignProgressConfig
- StrategyConfig
- GameConfig
- EmotionalStatesConfig
- SquaddiesConfig
- TacticalConfig
- TextTooltipsConfig
- UIConfig
- AnimatedSequenceConfig
- MissionPreviewConfigTemplate
- LoadingQuoteConfig
- GlobalAnimatorConfig

### Templates Returning Empty/Null Path

Templates that return `StringLiteral_7655` (empty string) - these are "loose" templates embedded elsewhere:
- Types not matched in the GetBaseFolder switch statement
- Checked via `IsSubclassOf` for inheritance chains

## Key Loading Functions

### DataTemplateLoader.GetAll<T> (0x180a58e60)

```c
IReadOnlyCollection<T> GetAll<T>() {
    var singleton = GetSingleton();
    Type type = typeof(T);

    if (singleton.m_TemplateArrays.TryGetValue(type, out var cached)) {
        return cached;
    }

    LoadTemplates<T>(out T[] templates, out Dictionary<string, DataTemplate> map);
    singleton.m_TemplateArrays[type] = templates;
    singleton.m_TemplateMaps[type] = map;

    return templates;
}
```

### DataTemplateLoader.LoadTemplates<T> (0x180a595d0)

```c
void LoadTemplates<T>(out T[] templates, out Dictionary<string, DataTemplate> map) {
    string folder = GetBaseFolder(typeof(T));

    if (string.IsNullOrEmpty(folder)) {
        // Return empty - template type has no folder path
        templates = Array.Empty<T>();
        map = new Dictionary<string, DataTemplate>();
        return;
    }

    // Load all assets from Resources folder
    templates = Resources.LoadAll<T>(folder);

    // Build name -> template map
    map = new Dictionary<string, DataTemplate>();
    foreach (var template in templates) {
        map[template.name] = template;
    }
}
```

### ConversationTemplate.LoadAllUncached (0x180558090)

Special loader that bypasses the cache:

```c
ConversationTemplate[] LoadAllUncached() {
    return Resources.LoadAll<ConversationTemplate>("Data/Conversations/");
}
```

## Template Hierarchy Observations

### FactionTemplate Structure
```c
public class FactionTemplate : DataTemplate {
    public LocalizedLine Name;              // 0x78
    public LocalizedMultiLine Description;  // 0x80
    public Sprite Icon;                     // 0x88
    public Sprite TurnOrderIcon;            // 0x90
    public Sprite TurnOrderInactiveIcon;    // 0x98
    public FactionType AlliedFactionType;   // 0xA0
    public FactionType EnemyFactionType;    // 0xA4
    public OperationTemplate[] Operations;  // 0xA8
    public EnemyAssetTemplate[] EnemyAssets; // 0xB0
    public RewardTableTemplate[] OperationRewardTables; // 0xB8
}
```

### ArmyTemplate Structure
```c
public class ArmyTemplate : DataTemplate {
    public FactionType FactionType;         // 0x78
    public FactionTemplate FactionTemplate; // 0x80  <- Reference to FactionTemplate
    public Vector2Int ReqCampaignProgress;  // 0x88
    public List<ArmyTemplateEntry> PossibleUnits; // 0x90
}

public class ArmyTemplateEntry {
    public EntityTemplate Template;  // 0x10
    public int Weight;               // 0x18
    public float WeightMultAfterPick; // 0x1C
}
```

Note: `ArmyListTemplate` doesn't exist as a separate type. Army composition is handled through:
1. `ArmyTemplate` - defines possible units for an army
2. `ArmyTemplateEntry` - individual unit entries with weights
3. `SwapArmyListEntryEffect` - game effect for modifying army composition

## Asset Bundle Investigation

From the search results, asset bundles appear to be primarily used for:
- Unity Editor analytics (BuildAssetBundleAnalytic)
- Not extensively used for template loading

Most templates appear to be in the Resources folder structure, loaded via `Resources.LoadAll<T>()`.

## Template Loading Call Sites

### FactionTemplate Access

FactionTemplate is accessed via:
1. `ArmyTemplate.FactionTemplate` field (direct reference at +0x80)
2. `DataTemplateLoader.GetAll<FactionTemplate>()` for enumeration
3. Individual lookup via `DataTemplateLoader.Get<FactionTemplate>(name)`

### Game State Loading

Based on scene analysis:
- **Main Menu**: Minimal template loading, primarily UI configs
- **Campaign/Strategy**: Loads FactionTemplate, OperationTemplate, MissionTemplate
- **Tactical Combat**: Loads EntityTemplate, SkillTemplate, WeaponTemplate, ArmorTemplate
- **Mission Briefing**: MissionTemplate, EntityTemplate for preview

## DataTemplateRedirection System

The game includes a redirection system for template migration:

```c
public class DataTemplateRedirection {
    public string BaseFolder;            // 0x10
    public string OldName;               // 0x18
    public DataTemplateRedirectAction Action; // 0x20
    public DataTemplate NewTemplate;     // 0x28
}

public enum DataTemplateRedirectAction {
    ToDo = 0,
    ReplaceWith = 1,
    Ignore = 2
}
```

This allows:
- Renaming templates while maintaining save compatibility
- Deprecating templates gracefully
- Redirecting old references to new templates

## String Literal Resolution Needed

The exact resource paths (StringLiteral_7xxx) need to be resolved by:
1. Reading the global-metadata.dat string table
2. Or runtime logging of GetBaseFolder calls

Known paths from dump.cs:
- `"Data/Conversations/"` - ConversationTemplate
- `"Data/"` - Base folder constant
- `"Resources/Localization/"` - Localization data
