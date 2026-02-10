# Template Cloning

So far we've modified existing units and weapons. But what if you want to create a new variant without touching the original? That's where **template cloning** comes in.

## Why Clone Templates?

Patching a template changes it everywhere in the game. If you patch the Marine to have 200 health, all Marines in the game get 200 health.

Sometimes you want:
- An **Elite Marine** that's tougher than regular Marines
- A **Heavy Rifle** variant with more damage but slower fire rate
- A **Rapid Heal** ability that costs less but heals less

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
      "NewTemplateName": "SourceTemplateName"
    }
  }
}
```

The format is:
- **TemplateType** - The template class (UnitTemplate, WeaponTemplate, etc.)
- **NewTemplateName** - What you want to call your clone
- **SourceTemplateName** - The existing template to copy from

The clone inherits all properties from the source. You can then patch the clone to customize it.

## Step-by-Step: Creating an Elite Marine

Let's create an Elite Marine - a tougher, more expensive variant of the regular Marine.

### Step 1: Create the Clone

First, declare the clone in your modpack:

```json
{
  "manifestVersion": 2,
  "name": "EliteSquad",
  "version": "1.0.0",
  "author": "YourName",
  "description": "Adds elite unit variants",
  "clones": {
    "UnitTemplate": {
      "EliteMarine": "Marine"
    }
  }
}
```

This creates a new `UnitTemplate` called `EliteMarine` that starts as an exact copy of `Marine`.

### Step 2: Customize the Clone

Now patch your new template to make it different:

```json
{
  "manifestVersion": 2,
  "name": "EliteSquad",
  "version": "1.0.0",
  "author": "YourName",
  "description": "Adds elite unit variants",
  "clones": {
    "UnitTemplate": {
      "EliteMarine": "Marine"
    }
  },
  "patches": {
    "UnitTemplate": {
      "EliteMarine": {
        "displayName": "Elite Marine",
        "description": "A veteran soldier with superior training and equipment.",
        "maxHealth": 150,
        "armor": 3,
        "accuracy": 85,
        "recruitCost": 250,
        "xpValue": 75
      }
    }
  }
}
```

The original Marine is unchanged. Your EliteMarine has boosted stats and costs more to recruit.

### Step 3: Test It

1. Deploy your modpack
2. Launch the game
3. Check the recruitment screen for your new Elite Marine unit

## Cloning Weapons

The same technique works for weapons. Create a heavy variant of the Assault Rifle:

```json
{
  "manifestVersion": 2,
  "name": "WeaponVariants",
  "version": "1.0.0",
  "clones": {
    "WeaponTemplate": {
      "HeavyAssaultRifle": "AssaultRifle",
      "LightSMG": "SMG",
      "LongRangeSniper": "SniperRifle"
    }
  },
  "patches": {
    "WeaponTemplate": {
      "HeavyAssaultRifle": {
        "displayName": "Heavy Assault Rifle",
        "damage": 25,
        "armorPiercing": 4,
        "fireRate": 0.25,
        "weight": 8,
        "cost": 180
      },
      "LightSMG": {
        "displayName": "Light SMG",
        "damage": 8,
        "fireRate": 0.05,
        "accuracy": 50,
        "weight": 2,
        "cost": 60
      },
      "LongRangeSniper": {
        "displayName": "Long Range Sniper",
        "range": 40,
        "accuracy": 98,
        "damage": 80,
        "cost": 400
      }
    }
  }
}
```

You can clone multiple templates at once, even of different types.

## Cloning Abilities

Create ability variants with different tradeoffs:

```json
{
  "manifestVersion": 2,
  "name": "AbilityVariants",
  "version": "1.0.0",
  "clones": {
    "AbilityTemplate": {
      "QuickHeal": "Heal",
      "ExtendedOverwatch": "Overwatch",
      "PowerShot": "AimedShot"
    }
  },
  "patches": {
    "AbilityTemplate": {
      "QuickHeal": {
        "displayName": "Quick Heal",
        "description": "Fast emergency healing with reduced effect.",
        "healAmount": 15,
        "cooldown": 1,
        "actionPointCost": 1
      },
      "ExtendedOverwatch": {
        "displayName": "Extended Overwatch",
        "description": "Wider overwatch arc with increased reaction time.",
        "arcAngle": 180,
        "range": 30,
        "actionPointCost": 2
      },
      "PowerShot": {
        "displayName": "Power Shot",
        "description": "Devastating aimed shot that ignores armor.",
        "damageMultiplier": 2.5,
        "armorPiercing": 100,
        "cooldown": 3,
        "actionPointCost": 2
      }
    }
  }
}
```

## Naming Conventions

Good clone names help avoid conflicts and keep your mod organized:

**Do:**
- Use descriptive prefixes: `Elite_`, `Heavy_`, `Light_`, `Mk2_`
- Include your mod identifier for unique names: `MyMod_EliteMarine`
- Use PascalCase matching the game's style: `EliteMarine`, `HeavyRifle`

**Don't:**
- Use generic names that other mods might use: `BetterMarine`, `NewRifle`
- Use spaces or special characters: `Elite Marine`, `Heavy-Rifle`
- Override existing template names (that's patching, not cloning)

### Avoiding Conflicts with Other Mods

If two mods clone with the same name, one will overwrite the other. Use a unique prefix:

```json
"clones": {
  "UnitTemplate": {
    "XCom_EliteMarine": "Marine",
    "XCom_VeteranSniper": "Sniper"
  }
}
```

## Combining Clones with Patches

A powerful technique: clone a template, patch it, and also patch the original differently.

```json
{
  "manifestVersion": 2,
  "name": "BalancedSquad",
  "version": "1.0.0",
  "clones": {
    "UnitTemplate": {
      "RookieMarine": "Marine",
      "VeteranMarine": "Marine"
    }
  },
  "patches": {
    "UnitTemplate": {
      "Marine": {
        "displayName": "Marine",
        "maxHealth": 100,
        "recruitCost": 100
      },
      "RookieMarine": {
        "displayName": "Rookie Marine",
        "description": "Fresh recruit with basic training.",
        "maxHealth": 80,
        "accuracy": 60,
        "recruitCost": 50
      },
      "VeteranMarine": {
        "displayName": "Veteran Marine",
        "description": "Battle-hardened soldier with years of experience.",
        "maxHealth": 120,
        "accuracy": 80,
        "armor": 2,
        "recruitCost": 200
      }
    }
  }
}
```

Now you have three tiers: Rookie (cheap but weak), Marine (balanced), and Veteran (expensive but strong).

## Giving Cloned Units Unique Equipment

Cloned units can have different default loadouts:

```json
{
  "manifestVersion": 2,
  "name": "SpecialForces",
  "version": "1.0.0",
  "clones": {
    "UnitTemplate": {
      "Commando": "Marine"
    },
    "WeaponTemplate": {
      "SilencedRifle": "AssaultRifle"
    }
  },
  "patches": {
    "WeaponTemplate": {
      "SilencedRifle": {
        "displayName": "Silenced Rifle",
        "damage": 15,
        "noiseLevel": 0.1,
        "detectionPenalty": -50
      }
    },
    "UnitTemplate": {
      "Commando": {
        "displayName": "Commando",
        "description": "Stealth specialist equipped for covert operations.",
        "defaultWeapon": "SilencedRifle",
        "abilities": ["Overwatch", "Reload", "Stealth", "Ambush"],
        "stealthRating": 80,
        "recruitCost": 300
      }
    }
  }
}
```

## Common Pitfalls

### Cloning Non-Existent Templates

```json
"clones": {
  "UnitTemplate": {
    "SuperSoldier": "UltraMarine"
  }
}
```

If `UltraMarine` doesn't exist, the clone fails silently. Always verify source template names using the Modkit's Data browser.

### Forgetting to Patch the Clone

```json
"clones": {
  "UnitTemplate": {
    "EliteMarine": "Marine"
  }
}
```

This creates an exact copy of Marine. Without patches, your clone is identical to the original - probably not useful.

### Circular or Self-Referencing Clones

```json
"clones": {
  "UnitTemplate": {
    "Marine": "Marine"
  }
}
```

Don't try to clone a template onto itself. This causes undefined behavior.

### Clone Name Matches Existing Template

```json
"clones": {
  "UnitTemplate": {
    "Medic": "Marine"
  }
}
```

If `Medic` already exists, this may overwrite it or fail. Clone names must be unique.

### Patching Before Cloning (Load Order Issues)

Clones are processed before patches, so this works correctly:

```json
{
  "clones": { "UnitTemplate": { "EliteMarine": "Marine" } },
  "patches": { "UnitTemplate": { "EliteMarine": { "maxHealth": 150 } } }
}
```

However, if another mod with lower load order patches Marine, your clone will copy those changes. Use `loadOrder` to control this.

## Processing Order

When the mod loader runs:

1. All `clones` from all mods are processed (in load order)
2. All `patches` from all mods are applied (in load order)

This means:
- A clone always copies the source template's current state
- Patches from lower-load-order mods affect what clones copy
- Your patches to clones apply after all clones are created

## Full Example: Elite Units Mod

Here's a complete modpack that adds elite variants of several units:

```json
{
  "manifestVersion": 2,
  "name": "EliteForces",
  "version": "1.0.0",
  "author": "YourName",
  "description": "Adds elite variants of base units with improved stats and higher costs.",
  "loadOrder": 100,
  "clones": {
    "UnitTemplate": {
      "EliteMarine": "Marine",
      "EliteMedic": "Medic",
      "EliteSniper": "Sniper",
      "EliteEngineer": "Engineer"
    },
    "WeaponTemplate": {
      "EliteRifle": "AssaultRifle",
      "ElitePistol": "Pistol"
    }
  },
  "patches": {
    "UnitTemplate": {
      "EliteMarine": {
        "displayName": "Elite Marine",
        "maxHealth": 150,
        "armor": 3,
        "accuracy": 80,
        "recruitCost": 250
      },
      "EliteMedic": {
        "displayName": "Elite Medic",
        "maxHealth": 120,
        "healAmount": 50,
        "recruitCost": 300
      },
      "EliteSniper": {
        "displayName": "Elite Sniper",
        "maxHealth": 100,
        "accuracy": 95,
        "critChance": 30,
        "recruitCost": 350
      },
      "EliteEngineer": {
        "displayName": "Elite Engineer",
        "maxHealth": 110,
        "repairAmount": 40,
        "hackingSkill": 90,
        "recruitCost": 280
      }
    },
    "WeaponTemplate": {
      "EliteRifle": {
        "displayName": "Elite Assault Rifle",
        "damage": 22,
        "accuracy": 80,
        "armorPiercing": 3
      },
      "ElitePistol": {
        "displayName": "Elite Pistol",
        "damage": 15,
        "accuracy": 90,
        "critChance": 15
      }
    }
  }
}
```

---

**Next:** [Textures & Icons](04-textures-icons.md)
