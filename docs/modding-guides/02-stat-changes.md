# Stat Adjustments

Now that you've made your first mod, let's explore the full range of data-driven changes you can make without writing any code.

## Template Types

Menace stores game data in **templates** - ScriptableObjects that define weapons, entities, abilities, and more. The main template types are:

| Template Type | What It Controls |
|--------------|------------------|
| `WeaponTemplate` | Guns, melee weapons, grenades |
| `EntityTemplate` | Units, enemies, buildings, objects |
| `SkillTemplate` | Special abilities, skills |
| `BaseItemTemplate` | Equipment, accessories, consumables |
| `ArmorTemplate` | Armor pieces and protection |
| `UnitLeaderTemplate` | Squad leaders and pilots |

## Browsing Templates

Use the Modkit's **Data** tab to explore templates:

1. Select a template type from the dropdown
2. Browse available instances
3. Click an instance to see all its fields
4. Note the field names and current values

## Common Stat Changes

### Weapon Stats

```json
"patches": {
  "WeaponTemplate": {
    "weapon.generic_assault_rifle_tier1_ARC_762": {
      "Damage": 15.0,
      "MaxRange": 9,
      "IdealRange": 6,
      "AccuracyBonus": 5.0,
      "ArmorPenetration": 10.0
    }
  }
}
```

### Multiple Weapons

```json
"patches": {
  "WeaponTemplate": {
    "weapon.generic_assault_rifle_tier1_ARC_762": {
      "Damage": 15.0,
      "MaxRange": 9
    },
    "weapon.generic_combat_shotgun_tier_1_cs185": {
      "Damage": 25.0,
      "MaxRange": 5
    },
    "weapon.generic_dmr_tier_1_longshot": {
      "Damage": 20.0,
      "MaxRange": 12
    }
  }
}
```

### Skill Modifications

```json
"patches": {
  "SkillTemplate": {
    "skill.overwatch": {
      "APCost": 1,
      "Cooldown": 0
    }
  }
}
```

## Multiple Changes at Once

Your modpack can modify many templates in a single file:

```json
{
  "manifestVersion": 2,
  "name": "BalanceOverhaul",
  "version": "1.0.0",
  "author": "YourName",
  "description": "Comprehensive balance changes",
  "patches": {
    "WeaponTemplate": {
      "weapon.generic_assault_rifle_tier1_ARC_762": {
        "Damage": 12.0,
        "AccuracyBonus": 8.0
      },
      "weapon.generic_combat_shotgun_tier_1_cs185": {
        "Damage": 30.0,
        "MaxRange": 4
      }
    },
    "ArmorTemplate": {
      "armor.light_ballistic_vest": {
        "ArmorValue": 3
      }
    }
  }
}
```

## Template Cloning

Want to create a new weapon variant without replacing the original? Use **clones**:

```json
{
  "manifestVersion": 2,
  "name": "HeavyWeapons",
  "version": "1.0.0",
  "clones": {
    "WeaponTemplate": {
      "weapon.custom_heavy_rifle": "weapon.generic_assault_rifle_tier1_ARC_762"
    }
  },
  "patches": {
    "WeaponTemplate": {
      "weapon.custom_heavy_rifle": {
        "Damage": 20.0,
        "ArmorPenetration": 25.0
      }
    }
  }
}
```

This creates a copy of the ARC-762 called `weapon.custom_heavy_rifle`, then patches the copy. The original weapon is unchanged.

## Arrays and Lists

Some fields are arrays. You can replace them entirely:

```json
"patches": {
  "WeaponTemplate": {
    "weapon.generic_assault_rifle_tier1_ARC_762": {
      "Tags": ["Rifle", "Automatic", "Military"]
    }
  }
}
```

## Boolean and String Fields

Not everything is a number:

```json
"patches": {
  "WeaponTemplate": {
    "weapon.generic_assault_rifle_tier1_ARC_762": {
      "IsAutomatic": true,
      "DisplayName": "Enhanced ARC-762"
    }
  }
}
```

## Finding Field Names

The Modkit's Data tab shows all available fields. Some tips:

- Field names typically use **PascalCase** (e.g., `MaxRange`, `ArmorPenetration`)
- Nested objects use dot notation: `Properties.Armor`
- Arrays are shown with indices: `Tags[0]`
- Hover over fields in the Modkit for type information

## Load Order

If multiple mods change the same field, load order determines the winner:

```json
{
  "loadOrder": 50
}
```

- Lower numbers load first
- Higher numbers load last (and win conflicts)
- Default is 100

For a "final word" mod that overrides others, use a high load order like 999.

## Validation

The Modkit validates your patches before deployment:

- Unknown template types → Warning
- Unknown instance names → Warning
- Type mismatches (string where number expected) → Error
- Invalid JSON → Error

Always check the Modkit output for warnings after saving your modpack.

---

**Next:** [Template Cloning](03-template-cloning.md)
