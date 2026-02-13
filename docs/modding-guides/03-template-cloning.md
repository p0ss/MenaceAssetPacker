# Template Cloning

So far we've modified existing weapons and entities. But what if you want to create a new variant without touching the original? That's where **template cloning** comes in.

## Why Clone Templates?

Patching a template changes it everywhere in the game. If you patch the ARC-762 to deal 25 damage, all ARC-762s in the game deal 25 damage.

Sometimes you want:
- A **Heavy Rifle** variant with more damage but lower accuracy
- A **Rapid Fire** weapon with less damage per shot but faster fire rate
- A custom variant that doesn't affect the original

Cloning lets you create new templates based on existing ones. The original stays intact, and you get a new variant to customize.

## The Clones Section

Template cloning uses the `clones` section in your `modpack.json`:

```json
{
  "manifestVersion": 2,
  "name": "MyMod",
  "version": "1.0.0",
  "clones": {
    "TemplateType": {
      "new.template.name": "source.template.name"
    }
  }
}
```

The format is:
- **TemplateType** - The template class (WeaponTemplate, EntityTemplate, SkillTemplate, etc.)
- **new.template.name** - What you want to call your clone
- **source.template.name** - The existing template to copy from

The clone inherits all properties from the source. You can then patch the clone to customize it.

## Step-by-Step: Creating a Heavy Rifle

Let's create a Heavy Rifle - a harder-hitting variant of the ARC-762.

### Step 1: Create the Clone

First, declare the clone in your modpack:

```json
{
  "manifestVersion": 2,
  "name": "HeavyWeapons",
  "version": "1.0.0",
  "author": "YourName",
  "description": "Adds heavy weapon variants",
  "clones": {
    "WeaponTemplate": {
      "weapon.custom_heavy_rifle": "weapon.generic_assault_rifle_tier1_ARC_762"
    }
  }
}
```

This creates a new `WeaponTemplate` called `weapon.custom_heavy_rifle` that starts as an exact copy of the ARC-762.

### Step 2: Customize the Clone

Now patch your new template to make it different:

```json
{
  "manifestVersion": 2,
  "name": "HeavyWeapons",
  "version": "1.0.0",
  "author": "YourName",
  "description": "Adds heavy weapon variants",
  "clones": {
    "WeaponTemplate": {
      "weapon.custom_heavy_rifle": "weapon.generic_assault_rifle_tier1_ARC_762"
    }
  },
  "patches": {
    "WeaponTemplate": {
      "weapon.custom_heavy_rifle": {
        "Damage": 18.0,
        "ArmorPenetration": 20.0,
        "AccuracyBonus": -5.0
      }
    }
  }
}
```

The original ARC-762 is unchanged. Your custom heavy rifle hits harder but is less accurate.

### Step 3: Test It

1. Deploy your modpack
2. Launch the game
3. Check the armory for your new weapon variant

## Cloning Multiple Weapons

Create multiple weapon variants at once:

```json
{
  "manifestVersion": 2,
  "name": "WeaponVariants",
  "version": "1.0.0",
  "clones": {
    "WeaponTemplate": {
      "weapon.custom_heavy_rifle": "weapon.generic_assault_rifle_tier1_ARC_762",
      "weapon.custom_rapid_shotgun": "weapon.generic_combat_shotgun_tier_1_cs185",
      "weapon.custom_marksman_rifle": "weapon.generic_dmr_tier_1_longshot"
    }
  },
  "patches": {
    "WeaponTemplate": {
      "weapon.custom_heavy_rifle": {
        "Damage": 18.0,
        "ArmorPenetration": 20.0,
        "AccuracyBonus": -5.0
      },
      "weapon.custom_rapid_shotgun": {
        "Damage": 18.0,
        "MaxRange": 4
      },
      "weapon.custom_marksman_rifle": {
        "Damage": 25.0,
        "MaxRange": 14,
        "AccuracyBonus": 15.0
      }
    }
  }
}
```

You can clone multiple templates at once, even of different types.

## Cloning Skills

Create skill variants with different tradeoffs:

```json
{
  "manifestVersion": 2,
  "name": "SkillVariants",
  "version": "1.0.0",
  "clones": {
    "SkillTemplate": {
      "skill.custom_quick_overwatch": "skill.overwatch",
      "skill.custom_power_shot": "skill.aimed_shot"
    }
  },
  "patches": {
    "SkillTemplate": {
      "skill.custom_quick_overwatch": {
        "APCost": 1,
        "Cooldown": 2
      },
      "skill.custom_power_shot": {
        "APCost": 2,
        "Cooldown": 3
      }
    }
  }
}
```

## Naming Conventions

Good clone names help avoid conflicts and keep your mod organized:

**Do:**
- Use descriptive prefixes that indicate what your mod does
- Include your mod identifier for unique names: `mymod.weapon.heavy_rifle`
- Follow the game's naming pattern: `weapon.`, `skill.`, `entity.`, etc.

**Don't:**
- Use generic names that other mods might use
- Use spaces or special characters
- Override existing template names (that's patching, not cloning)

### Avoiding Conflicts with Other Mods

If two mods clone with the same name, one will overwrite the other. Use a unique prefix:

```json
"clones": {
  "WeaponTemplate": {
    "xcom_mod.weapon.elite_rifle": "weapon.generic_assault_rifle_tier1_ARC_762",
    "xcom_mod.weapon.elite_shotgun": "weapon.generic_combat_shotgun_tier_1_cs185"
  }
}
```

## Combining Clones with Patches

A powerful technique: clone a template, patch it, and also patch the original differently.

```json
{
  "manifestVersion": 2,
  "name": "WeaponTiers",
  "version": "1.0.0",
  "clones": {
    "WeaponTemplate": {
      "weapon.arc762_mk2": "weapon.generic_assault_rifle_tier1_ARC_762",
      "weapon.arc762_mk3": "weapon.generic_assault_rifle_tier1_ARC_762"
    }
  },
  "patches": {
    "WeaponTemplate": {
      "weapon.generic_assault_rifle_tier1_ARC_762": {
        "Damage": 10.0
      },
      "weapon.arc762_mk2": {
        "Damage": 14.0,
        "AccuracyBonus": 5.0
      },
      "weapon.arc762_mk3": {
        "Damage": 18.0,
        "AccuracyBonus": 10.0,
        "ArmorPenetration": 15.0
      }
    }
  }
}
```

Now you have three tiers: base ARC-762 (standard), Mk2 (improved), and Mk3 (elite).

## Common Pitfalls

### Cloning Non-Existent Templates

```json
"clones": {
  "WeaponTemplate": {
    "weapon.super_rifle": "weapon.ultra_rifle_9000"
  }
}
```

If `weapon.ultra_rifle_9000` doesn't exist, the clone fails silently. Always verify source template names using the Modkit's Data browser.

### Forgetting to Patch the Clone

```json
"clones": {
  "WeaponTemplate": {
    "weapon.custom_rifle": "weapon.generic_assault_rifle_tier1_ARC_762"
  }
}
```

This creates an exact copy of the ARC-762. Without patches, your clone is identical to the original - probably not useful.

### Circular or Self-Referencing Clones

```json
"clones": {
  "WeaponTemplate": {
    "weapon.generic_assault_rifle_tier1_ARC_762": "weapon.generic_assault_rifle_tier1_ARC_762"
  }
}
```

Don't try to clone a template onto itself. This causes undefined behavior.

### Clone Name Matches Existing Template

```json
"clones": {
  "WeaponTemplate": {
    "weapon.generic_combat_shotgun_tier_1_cs185": "weapon.generic_assault_rifle_tier1_ARC_762"
  }
}
```

If the target name already exists, this may overwrite it or fail. Clone names must be unique.

### Patching Before Cloning (Load Order Issues)

Clones are processed before patches, so this works correctly:

```json
{
  "clones": { "WeaponTemplate": { "weapon.custom_rifle": "weapon.generic_assault_rifle_tier1_ARC_762" } },
  "patches": { "WeaponTemplate": { "weapon.custom_rifle": { "Damage": 15.0 } } }
}
```

However, if another mod with lower load order patches the ARC-762, your clone will copy those changes. Use `loadOrder` to control this.

## Processing Order

When the mod loader runs:

1. All `clones` from all mods are processed (in load order)
2. All `patches` from all mods are applied (in load order)

This means:
- A clone always copies the source template's current state
- Patches from lower-load-order mods affect what clones copy
- Your patches to clones apply after all clones are created

## Full Example: Weapon Variants Mod

Here's a complete modpack that adds variants of several weapons:

```json
{
  "manifestVersion": 2,
  "name": "WeaponVariants",
  "version": "1.0.0",
  "author": "YourName",
  "description": "Adds weapon variants with different stat tradeoffs.",
  "loadOrder": 100,
  "clones": {
    "WeaponTemplate": {
      "weapon.custom_heavy_arc762": "weapon.generic_assault_rifle_tier1_ARC_762",
      "weapon.custom_rapid_arc762": "weapon.generic_assault_rifle_tier1_ARC_762",
      "weapon.custom_heavy_shotgun": "weapon.generic_combat_shotgun_tier_1_cs185",
      "weapon.custom_precision_dmr": "weapon.generic_dmr_tier_1_longshot"
    }
  },
  "patches": {
    "WeaponTemplate": {
      "weapon.custom_heavy_arc762": {
        "Damage": 15.0,
        "ArmorPenetration": 20.0,
        "AccuracyBonus": -5.0
      },
      "weapon.custom_rapid_arc762": {
        "Damage": 7.0,
        "AccuracyBonus": 10.0
      },
      "weapon.custom_heavy_shotgun": {
        "Damage": 35.0,
        "MaxRange": 3
      },
      "weapon.custom_precision_dmr": {
        "Damage": 25.0,
        "MaxRange": 14,
        "AccuracyBonus": 15.0
      }
    }
  }
}
```

---

**Next:** [Textures & Icons](04-textures-icons.md)
