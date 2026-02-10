# Stat Adjustments

Now that you've made your first mod, let's explore the full range of data-driven changes you can make without writing any code.

## Template Types

Menace stores game data in **templates** - ScriptableObjects that define units, weapons, abilities, and more. The main template types are:

| Template Type | What It Controls |
|--------------|------------------|
| `UnitTemplate` | Soldiers, enemies, civilians |
| `WeaponTemplate` | Guns, melee weapons, grenades |
| `AbilityTemplate` | Special abilities, skills |
| `ItemTemplate` | Equipment, consumables |
| `FactionTemplate` | Faction relationships, colors |
| `MissionTemplate` | Mission parameters, objectives |

## Browsing Templates

Use the Modkit's **Data** tab to explore templates:

1. Select a template type from the dropdown
2. Browse available instances
3. Click an instance to see all its fields
4. Note the field names and current values

## Common Stat Changes

### Unit Stats

```json
"patches": {
  "UnitTemplate": {
    "Marine": {
      "maxHealth": 120,
      "armor": 2,
      "movementSpeed": 4.0,
      "actionPoints": 3,
      "sightRange": 15,
      "recruitCost": 150
    }
  }
}
```

### Weapon Stats

```json
"patches": {
  "WeaponTemplate": {
    "AssaultRifle": {
      "damage": 18,
      "accuracy": 75,
      "range": 20,
      "clipSize": 30,
      "fireRate": 0.15,
      "armorPiercing": 2
    }
  }
}
```

### Ability Modifications

```json
"patches": {
  "AbilityTemplate": {
    "Overwatch": {
      "cooldown": 1,
      "actionPointCost": 1,
      "range": 25
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
    "UnitTemplate": {
      "Marine": { "maxHealth": 100 },
      "Medic": { "maxHealth": 80, "healAmount": 40 },
      "Sniper": { "accuracy": 95, "damage": 50 }
    },
    "WeaponTemplate": {
      "Shotgun": { "damage": 35, "range": 8 },
      "SMG": { "fireRate": 0.08, "accuracy": 60 }
    }
  }
}
```

## Template Cloning

Want to create a new unit variant without replacing the original? Use **clones**:

```json
{
  "manifestVersion": 2,
  "name": "EliteSquad",
  "version": "1.0.0",
  "clones": {
    "UnitTemplate": {
      "EliteMarine": "Marine"
    }
  },
  "patches": {
    "UnitTemplate": {
      "EliteMarine": {
        "maxHealth": 150,
        "armor": 3,
        "displayName": "Elite Marine"
      }
    }
  }
}
```

This creates a copy of `Marine` called `EliteMarine`, then patches the copy. The original Marine is unchanged.

## Arrays and Lists

Some fields are arrays. You can replace them entirely:

```json
"patches": {
  "UnitTemplate": {
    "Marine": {
      "abilities": ["Overwatch", "Reload", "Sprint", "Grenade"]
    }
  }
}
```

## Boolean and String Fields

Not everything is a number:

```json
"patches": {
  "UnitTemplate": {
    "Marine": {
      "canSwim": true,
      "displayName": "Space Marine",
      "description": "An elite soldier from the future"
    }
  }
}
```

## Finding Field Names

The Modkit's Data tab shows all available fields. Some tips:

- Field names are **camelCase** (e.g., `maxHealth`, not `MaxHealth`)
- Nested objects use dot notation: `weaponStats.damage`
- Arrays are shown with indices: `abilities[0]`
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
