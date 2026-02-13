# Asset References & Usage

## Overview

Menace uses Unity's ScriptableObject system for all game data. Templates reference each other through direct object references, which Unity serializes as asset GUIDs internally.

## Template Hierarchy

```
DataTemplate (base)
├── EntityTemplate (units, structures, vegetation)
│   ├── Skills[] → SkillTemplate
│   ├── Items[] → ItemTemplate
│   ├── AIRole → RoleData (embedded)
│   ├── Prefabs[] → GameObject
│   └── Badge → Sprite
│
├── SkillTemplate (abilities)
│   ├── EventHandlers[] → SkillEventHandlerTemplate
│   ├── Icon → Sprite
│   └── Tags[] → TagTemplate
│
├── FactionTemplate (factions)
│   ├── Operations[] → OperationTemplate
│   └── EnemyAssets[] → EnemyAssetTemplate
│
├── ArmyTemplate (enemy army composition)
│   ├── FactionTemplate → FactionTemplate
│   └── PossibleUnits[] → ArmyTemplateEntry
│       └── Template → EntityTemplate
│
├── OperationTemplate (campaign operations)
│   └── Missions[] → MissionTemplate
│
├── MissionTemplate (tactical missions)
│   ├── BiomeOverride → BiomeTemplate
│   └── Spawns[] → SpawnEntry
│
├── PlanetTemplate (campaign planets)
│   ├── Neighbors[] → PlanetTemplate
│   └── Operations[] → OperationTemplate
│
├── ItemTemplate (equipment)
│   ├── WeaponTemplate (weapons)
│   │   └── AttackSkill → SkillTemplate
│   └── ArmorTemplate (armor)
│
└── WeaponTemplate / ArmorTemplate
    └── Icon → Sprite
```

## Reference Types

### Direct Object References

Most template-to-template references use Unity's direct serialization:

```c
public class EntityTemplate : DataTemplate {
    public List<SkillTemplate> Skills;     // +0x308 - Direct references
    public List<ItemTemplate> Items;        // +0x2E0
    public FactionTemplate Faction;         // (via spawns)
}
```

Unity stores these as:
- Asset GUID in meta file
- FileID within the asset

### Sprite/Texture References

Sprites are referenced directly:

```c
public class EntityTemplate {
    public Sprite Badge;        // +0x1C0
    public Sprite BadgeWhite;   // +0x1C8
    public Sprite PreviewMapIcon; // +0x1D0
}

public class SkillTemplate {
    public Sprite Icon;         // +0x90
    public Sprite IconDisabled; // +0x98
}

public class ItemTemplate {
    public Sprite IconEquipment;     // +0xB8
    public Sprite IconEquipmentDisabled; // +0xC0
    public Sprite IconSkillBar;      // +0xC8
}
```

### Prefab References

3D models are GameObject prefabs:

```c
public class EntityTemplate {
    public List<GameObject> Prefabs;           // +0x138 - Normal state
    public List<PrefabListTemplate> Decoration; // +0x140
    public List<GameObject> DestroyedPrefabs;   // +0x150 - When destroyed
    public List<GameObject> DestroyedWalls;     // +0x160
}
```

### Effect References

Visual effects:

```c
public class EntityTemplate {
    public GameObject DamageReceivedEffect;     // +0x228
    public GameObject HeavyDamageReceivedEffect; // +0x230
    public GameObject GetDismemberedBloodSprayEffect; // +0x240
    public GameObject DeathEffect;              // +0x258
    public GameObject DeathAttachEffect;        // +0x268
}
```

## Finding Where Assets Are Used

### 1. Template → Entity Usage

To find where an `EntityTemplate` is used:

```
EntityTemplate
  ↓ referenced by
ArmyTemplate.PossibleUnits[].Template
  ↓ referenced by
FactionTemplate (indirectly via army generation)
  ↓ referenced by
OperationTemplate
  ↓ referenced by
PlanetTemplate.Operations[]
  ↓ used in
Campaign gameplay
```

### 2. Skill → Entity Usage

```
SkillTemplate
  ↓ referenced by
EntityTemplate.Skills[]
EntityTemplate.SkillGroups[].Skills[]
ItemTemplate (weapon attacks)
SkillEventHandler.AddSkillEffect
```

### 3. Item → Entity Usage

```
ItemTemplate
  ↓ referenced by
EntityTemplate.Items[]
EntityTemplate.Loot[]
BlackMarket inventory
Reward tables
```

## Asset Loading Flow

### DataTemplateLoader

```c
public class DataTemplateLoader {
    const string DATA_FOLDER = "Data/";

    private Dictionary<Type, DataTemplate[]> m_TemplateArrays;
    private Dictionary<Type, Dictionary<string, DataTemplate>> m_TemplateMaps;

    // Get base folder for a template type
    public static string GetBaseFolder(Type _type);

    // Get all templates of a type
    public static IReadOnlyCollection<T> GetAll<T>();

    // Get template by ID
    public static T Get<T>(string _name);
}
```

### Template Loading Paths

| Template Type | Resource Path |
|---------------|---------------|
| EntityTemplate | Data/Entities/ |
| SkillTemplate | Data/Skills/ |
| ItemTemplate | Data/Items/ |
| FactionTemplate | Data/Factions/ |
| ArmyTemplate | Data/Armies/ |
| OperationTemplate | Data/Operations/ |
| MissionTemplate | Data/Missions/ |
| PlanetTemplate | Data/Planets/ |
| BiomeTemplate | Data/Biomes/ |
| TagTemplate | Data/Tags/ |

### Usage in Code

```c
// Get all entities
var entities = DataTemplateLoader.GetAll<EntityTemplate>();

// Get specific template by ID
var pirate = DataTemplateLoader.Get<EntityTemplate>("enemy.pirate_boarding_commandos");

// Get template from runtime object
Entity entity = ...;
EntityTemplate template = entity.GetTemplate();
```

## Spawn System - Where Entities Appear

### SpawnEntityAction

```c
public class SpawnEntityAction : TacticalAction {
    // Spawns entity at tile from template
    private static void SpawnEntity(EntityTemplate _template, Tile _tile);
}
```

### SpawnEntity Skill Effect

```c
public class SpawnEntity : SkillEventHandlerTemplate {
    public SpawnEntity.SpawnTrigger Trigger; // OnUse or OnTileHit
    // Entity to spawn defined in skill
}
```

### Army Generation

```c
public class Army {
    public readonly ArmyTemplate Template;
    private readonly List<ArmyEntry> m_Entries;

    // Generate army from template
    private static Army CreateArmy(
        PseudoRandom _random,
        ArmyTemplate _template,
        int _budget,
        int _campaignProgress
    );
}
```

### Mission Generation

```c
public class Operation {
    private Mission GenerateMission(
        MissionTemplate _missionTemplate,
        MissionLayer _layer,
        int _layerIdx,
        ...
    );
}
```

## Finding Asset Usages

### Method 1: Search dump.cs

Search for template type names to find all referencing classes:

```bash
grep "EntityTemplate" dump.cs | grep -v "class EntityTemplate"
```

### Method 2: Unity Asset References

In Unity Editor or via asset bundle tools:
1. Find asset GUID in `.meta` file
2. Search all `.asset` files for that GUID

### Method 3: Runtime Inspection

Hook template loading to track usage:

```csharp
[HarmonyPatch(typeof(DataTemplateLoader), "Get")]
class TemplateLoadPatch {
    static void Postfix<T>(string _name, T __result) {
        Logger.Msg($"Loaded {typeof(T).Name}: {_name}");
    }
}
```

## Asset Reference Chains

### Entity → Full Chain

```
EntityTemplate
├── Skills[] ─────────→ SkillTemplate
│   ├── Icon ─────────→ Sprite (texture in atlas)
│   └── Handlers[] ───→ SkillEventHandlerTemplate
│       └── (various effect templates)
│
├── Items[] ──────────→ ItemTemplate
│   ├── WeaponTemplate
│   │   ├── Icon ─────→ Sprite
│   │   └── AttackSkill → SkillTemplate
│   └── ArmorTemplate
│       └── Icon ─────→ Sprite
│
├── Prefabs[] ────────→ GameObject
│   ├── MeshRenderer ─→ Materials[]
│   │   └── Shader ───→ Shader
│   │   └── Textures ─→ Texture2D
│   └── Animator ─────→ RuntimeAnimatorController
│       └── Clips[] ──→ AnimationClip
│
├── Badge ────────────→ Sprite
├── DeathEffect ──────→ GameObject (VFX prefab)
└── AIRole ───────────→ RoleData (embedded struct)
```

### Faction → Combat Chain

```
FactionTemplate
├── Operations[] ─────→ OperationTemplate
│   └── Missions[] ───→ MissionTemplate
│       └── Spawns ───→ (spawn configurations)
│
└── EnemyAssets[] ────→ EnemyAssetTemplate
    └── Armies[] ─────→ ArmyTemplate
        ├── FactionTemplate → (back reference)
        └── PossibleUnits[]
            └── Template → EntityTemplate
```

## Broken References

Common causes of broken asset references:

1. **Missing Sprites**: Template Icon field is null
2. **Missing Prefabs**: Prefabs list is empty or contains null
3. **Circular References**: A → B → A (usually handled OK)
4. **Version Mismatch**: Template references removed asset

### Detecting Broken References

```csharp
foreach (var entity in DataTemplateLoader.GetAll<EntityTemplate>()) {
    if (entity.Badge == null) {
        Logger.Warning($"{entity.GetID()} has no badge sprite");
    }
    if (entity.Prefabs == null || entity.Prefabs.Count == 0) {
        Logger.Warning($"{entity.GetID()} has no prefabs");
    }
    foreach (var skill in entity.Skills) {
        if (skill == null) {
            Logger.Error($"{entity.GetID()} has null skill reference");
        }
    }
}
```

## Modding Asset References

### Replacing Sprites

```csharp
var entity = DataTemplateLoader.Get<EntityTemplate>("enemy.pirate_boarding_commandos");
// Load replacement sprite from your modpack assets
var newBadge = LoadSprite("custom_badge.png");
// Replace via reflection
typeof(EntityTemplate).GetField("Badge").SetValue(entity, newBadge);
```

### Adding Skills

```csharp
var entity = DataTemplateLoader.Get<EntityTemplate>("enemy.pirate_boarding_commandos");
// Find a real skill name using template_list SkillTemplate
var newSkill = DataTemplateLoader.Get<SkillTemplate>("skill.overwatch");
entity.Skills.Add(newSkill);
```

### Replacing Prefabs

Best done via the Modkit's asset replacement system:
1. Export the original model from the asset browser
2. Create your replacement in Blender
3. Add to modpack assets with the original's path

For advanced cases using asset bundles directly:

```csharp
var bundle = AssetBundle.LoadFromFile("custom_models.bundle");
var newPrefab = bundle.LoadAsset<GameObject>("CustomModel");
entity.Prefabs[0] = newPrefab;
```
