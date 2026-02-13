# Template Modding Guide

Templates are the game's core data objects -- ScriptableObjects loaded by `DataTemplateLoader` at startup. Every weapon, agent, armor set, skill, and entity definition is a template. Menace supports two approaches to modifying templates: JSON-based patches in `modpack.json` (no code required) and code-based modification via the `Templates` SDK class.

---

## What Are Templates?

Templates are Unity `ScriptableObject` instances that define game data. The game's `DataTemplateLoader` singleton loads them at startup and stores them in typed dictionaries keyed by their `m_ID` field. Examples:

- `WeaponTemplate` -- damage, range, accuracy, armor penetration
- `EntityTemplate` -- unit type, actor type, faction, visual properties
- `ArmorTemplate` -- protection values, weight, coverage
- `SkillTemplate` -- skill effects, cooldowns, AP costs
- `UnitLeaderTemplate` -- leader stats, hiring costs, perk trees

Each template instance has a unique name (its `m_ID`). For example, the weapon `weapon.generic_assault_rifle_tier1_ARC_762` is a `WeaponTemplate` instance.

---

## JSON-Based Template Patching

The simplest way to modify templates. Define patches in your `modpack.json` under the `"patches"` key (manifest version 2):

```json
{
  "manifestVersion": 2,
  "name": "My Balance Mod",
  "version": "1.0.0",
  "author": "Me",
  "loadOrder": 100,
  "patches": {
    "WeaponTemplate": {
      "weapon.generic_assault_rifle_tier1_ARC_762": {
        "Damage": 15.0,
        "MaxRange": 9,
        "AccuracyBonus": 5.0
      },
      "weapon.generic_combat_shotgun_tier_1_cs185": {
        "Damage": 45.0,
        "ArmorPenetration": 30.0
      }
    },
    "UnitLeaderTemplate": {
      "squad_leader.pike": {
        "HiringCosts": 20,
        "GrowthPotential": 5
      }
    }
  }
}
```

Structure: `patches -> TypeName -> InstanceName -> FieldName -> Value`.

The modpack loader applies these patches after the game loads templates, using managed reflection through IL2CppInterop proxy types. It retries across scene loads until all template types are found.

### V1 Legacy Format

Manifest version 1 uses `"templates"` instead of `"patches"` with the same structure. Both formats are supported, but V2 (`"patches"`) is preferred for new mods.

### Supported Field Types

JSON patches support any field type that IL2CppInterop exposes as a public property on the proxy type:

- **Primitives**: `int`, `float`, `double`, `bool`, `byte`, `short`, `long`, `string`
- **Enums**: pass as integer (the enum's underlying value)
- **UnityEngine.Object references**: pass the object's name as a string -- the loader resolves it via `Resources.FindObjectsOfTypeAll`
- **Arrays**: `Il2CppStructArray<T>`, `Il2CppReferenceArray<T>`, `Il2CppStringArray`, managed arrays -- pass as a JSON array
- **IL2CPP Lists**: `Il2CppSystem.Collections.Generic.List<T>` -- pass as a JSON array (full replacement) or a JSON object with `$op` keys (incremental operations)
- **Nested IL2CPP objects**: pass as a JSON object with field names as keys -- the loader constructs the object and recursively sets its properties
- **Dotted paths**: access nested properties with `"Parent.Child"` syntax (e.g., `"Properties.HitpointsPerElement": 100`)

### Collection Patching

Collections (arrays and lists) can be patched by providing a JSON array. This performs a **full replacement** -- the existing contents are cleared and replaced with the new values.

```json
{
  "patches": {
    "WeaponTemplate": {
      "weapon.generic_combat_shotgun_tier_1_cs185": {
        "DamageDropoff": -2.0
      }
    }
  }
}
```

For lists of UnityEngine.Object references, pass object names as strings:

```json
{
  "patches": {
    "SquadTemplate": {
      "squad.raiders": {
        "Members": ["enemy.raider_rifleman", "enemy.raider_shotgunner", "enemy.raider_medic"]
      }
    }
  }
}
```

### Complex Object Construction

Lists of complex IL2CPP objects (like `List<Army>` or `List<ArmyEntry>`) can be patched by providing JSON objects for each element. The loader constructs new IL2CPP proxy objects and recursively sets their properties, including nested lists and UnityEngine.Object references resolved by name.

```json
{
  "patches": {
    "ArmyListTemplate": {
      "army_list.pirates": {
        "Compositions": [
          {
            "Flags": 1,
            "ProgressRequired": 0,
            "Cost": 80,
            "Entries": [
              { "Template": "enemy.pirate_scavengers", "Amount": 4, "SpawnAsGroup": false },
              { "Template": "enemy.pirate_chaingun_team", "Amount": 2, "SpawnAsGroup": true }
            ]
          }
        ]
      }
    }
  }
}
```

In the example above, each object in `Compositions` is constructed as an `Army` instance, and each object in `Entries` is constructed as an `ArmyEntry` instance. The `Template` field is a `UnityEngine.Object` reference resolved by name.

### Incremental List Operations

For IL2CPP lists, you can use **incremental operations** instead of full replacement. Pass a JSON object with operation keys instead of a JSON array:

- **`$remove`** -- array of indices to remove (applied highest-index-first to preserve positions)
- **`$update`** -- map of index → field overrides (applied to existing elements in-place)
- **`$append`** -- array of new elements to add at the end

Operations are applied in order: **remove → update → append**.

```json
{
  "patches": {
    "ArmyListTemplate": {
      "army_list.pirates": {
        "Compositions": {
          "$remove": [3],
          "$update": {
            "0": { "Cost": 120 }
          },
          "$append": [
            {
              "Flags": 2,
              "ProgressRequired": 50,
              "Cost": 150,
              "Entries": [
                { "Template": "enemy.pirate_heavy_infantry", "Amount": 3, "SpawnAsGroup": true }
              ]
            }
          ]
        }
      }
    }
  }
}
```

This is useful when multiple mods need to modify the same list without overwriting each other's changes. A mod that only appends entries will not interfere with a mod that only updates existing entries.

**Notes:**
- Invalid indices in `$remove` or `$update` are logged as warnings and skipped.
- `$update` modifies existing objects in-place -- only the fields you specify are changed.
- All three operation keys are optional. You can use any combination (e.g., just `$append`).

---

## Code-Based Template Modification

The `Templates` class provides runtime access to template fields through managed reflection. Use this when you need conditional logic, computed values, or access to fields that JSON patches cannot reach.

### Finding Templates

```csharp
// Find a specific template by type and name
GameObj rifle = Templates.Find("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");
if (rifle.IsNull)
{
    ModError.Report("MyMod", "ARC-762 not found");
    return;
}

// Find all templates of a type
GameObj[] allWeapons = Templates.FindAll("WeaponTemplate");

// Check existence
bool exists = Templates.Exists("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");
```

### Reading Fields

`Templates.ReadField` reads a property value via managed reflection on the IL2CppInterop proxy type. It supports dotted paths for nested properties.

```csharp
GameObj weapon = Templates.Find("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");

object damage = Templates.ReadField(weapon, "Damage");         // returns boxed float
object maxRange = Templates.ReadField(weapon, "MaxRange");     // returns boxed int
object accuracy = Templates.ReadField(weapon, "AccuracyBonus"); // returns boxed float
object name = Templates.ReadField(weapon, "name");              // Unity object name

// Dotted paths for nested objects (if applicable)
object deployCost = Templates.ReadField(weapon, "DeployCosts.m_Supplies");
```

Returns `null` on failure (field not found, no managed proxy, reflection error).

### Writing Fields

`Templates.WriteField` sets a property value. Handles type conversion automatically (int/float/double/bool/string/enum).

```csharp
GameObj weapon = Templates.Find("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");

Templates.WriteField(weapon, "Damage", 15.0f);
Templates.WriteField(weapon, "MaxRange", 9);
Templates.WriteField(weapon, "AccuracyBonus", 5.0f);
```

Returns `false` on failure.

### Batch Writes

```csharp
GameObj weapon = Templates.Find("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");
int written = Templates.WriteFields(weapon, new Dictionary<string, object>
{
    { "Damage", 15.0f },
    { "MaxRange", 9 },
    { "AccuracyBonus", 5.0f },
    { "ArmorPenetration", 25.0f }
});
// written == number of fields successfully set
```

---

## Cloning Templates

Clone an existing template to create a new variant. The clone is a deep copy via `UnityEngine.Object.Instantiate` -- all serialized fields are copied.

### JSON-Based Cloning

Define clones in `modpack.json` under the `"clones"` key:

```json
{
  "manifestVersion": 2,
  "name": "New Weapons Mod",
  "patches": {
    "WeaponTemplate": {
      "weapon.custom_heavy_rifle": {
        "Damage": 20.0,
        "MaxRange": 10,
        "ArmorPenetration": 40.0
      }
    }
  },
  "clones": {
    "WeaponTemplate": {
      "weapon.custom_heavy_rifle": "weapon.generic_assault_rifle_tier1_ARC_762"
    }
  }
}
```

Structure: `clones -> TypeName -> NewName -> SourceName`.

Clones are applied before patches, so you can clone a template and then patch the clone's fields in the same modpack. The loader automatically:

1. Deep-copies the source via `Instantiate`
2. Sets the clone's `m_ID` field to the new name
3. Registers the clone in `DataTemplateLoader`'s internal dictionaries
4. Marks the clone with `HideFlags.DontUnloadUnusedAsset` so Unity does not garbage-collect it

### Code-Based Cloning

```csharp
GameObj clone = Templates.Clone("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762", "weapon.custom_heavy_rifle");
if (!clone.IsNull)
{
    Templates.WriteField(clone, "Damage", 20.0f);
    Templates.WriteField(clone, "MaxRange", 10);
}
```

Note: `Templates.Clone` creates the object and sets its name and `HideFlags`, but does **not** register it in `DataTemplateLoader`. If the game needs to look up your clone by ID via `DataTemplateLoader.Get<T>()`, use the JSON clone system instead -- it handles registration automatically.

---

## When to Use JSON vs Code

| Scenario | Approach |
|----------|---------|
| Static stat tweaks (damage, range, HP) | JSON patches |
| Replacing or appending to lists | JSON patches (full replacement or `$append`) |
| Modifying individual list entries | JSON patches (`$update`) |
| Setting UnityEngine.Object references by name | JSON patches |
| Conditional changes (only if another mod is loaded) | Code |
| Computed values (scale damage by difficulty) | Code |
| Creating new template variants | JSON clones + patches |
| Simple balance mod with no DLL | JSON patches only |
| Complex multi-step template surgery | Code |

JSON patches are simpler, require no compilation, and are easier for end users to review. Prefer them when possible.

---

## Example: Weapon Balance (JSON)

A balance mod that buffs assault rifles and shotguns:

```json
{
  "manifestVersion": 2,
  "name": "Weapon Balance",
  "version": "1.0.0",
  "loadOrder": 100,
  "patches": {
    "WeaponTemplate": {
      "weapon.generic_assault_rifle_tier1_ARC_762": {
        "Damage": 14.0,
        "AccuracyBonus": 5.0
      },
      "weapon.generic_assault_rifle_tier1_kpac": {
        "Damage": 11.0,
        "MaxRange": 9
      },
      "weapon.generic_combat_shotgun_tier_1_cs185": {
        "Damage": 50.0,
        "ArmorPenetration": 35.0
      }
    }
  }
}
```

## Example: Cloned Weapon (Code)

A mod plugin that clones a weapon and modifies the clone at runtime:

```csharp
public class WeaponModPlugin : IModpackPlugin
{
    private MelonLogger.Instance _log;

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _log = logger;
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        if (sceneName != "Tactical") return;

        GameState.RunDelayed(30, () =>
        {
            var source = Templates.Find("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762");
            if (source.IsNull)
            {
                ModError.Report("WeaponMod", "Source weapon not found");
                return;
            }

            var clone = Templates.Clone("WeaponTemplate", "weapon.generic_assault_rifle_tier1_ARC_762", "weapon.custom_stealth_rifle");
            if (clone.IsNull)
            {
                ModError.Report("WeaponMod", "Clone failed");
                return;
            }

            Templates.WriteField(clone, "Damage", 10.0f);
            Templates.WriteField(clone, "AccuracyBonus", 15.0f);
            Templates.WriteField(clone, "Suppression", 5.0f);
            _log.Msg("Created weapon.custom_stealth_rifle from ARC-762");
        });
    }
}
```

---

## Timing

Template patches (both JSON and code) must run after `DataTemplateLoader` has loaded the templates. The modpack loader handles this automatically for JSON patches by retrying across scene loads. For code-based modifications, use `GameState.RunDelayed` or apply them in `OnSceneLoaded` with a frame delay to ensure templates are initialized.
