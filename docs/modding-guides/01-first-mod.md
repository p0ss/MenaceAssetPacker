# Baby's First Mod

Let's make the simplest possible mod: we'll change one stat and see it work in-game. No coding required - just editing a JSON file.

## What We'll Do

We're going to make the ARC-762 assault rifle deal more damage. That's it. One number change.

## Step 1: Create the Modpack Folder

In the Modkit app:

1. Go to **Modding Tools > Data**
2. Click **Create New Modpack**
3. Name it `BiggerGuns`

This creates a folder structure for you.

## Step 2: Find the Stat to Change

We need to know:
- What **template type** holds weapon data
- What **field** controls damage

In the Modkit:

1. Go to **Modding Tools > Data**
2. Browse to `WeaponTemplate`
3. Find `weapon.generic_assault_rifle_tier1_ARC_762` in the list
4. Look for `Damage` field

Let's say this weapon has `Damage = 10`

## Step 3: Edit modpack.json

Open `BiggerGuns-modpack/modpack.json` in any text editor:

```json
{
  "manifestVersion": 2,
  "name": "BiggerGuns",
  "version": "1.0.0",
  "author": "YourName",
  "description": "Makes the ARC-762 hit harder",
  "loadOrder": 100,
  "patches": {
    "WeaponTemplate": {
      "weapon.generic_assault_rifle_tier1_ARC_762": {
        "Damage": 20.0
      }
    }
  }
}
```

That's it. We're telling the loader:
- Find the `WeaponTemplate` called `weapon.generic_assault_rifle_tier1_ARC_762`
- Set its `Damage` to `20.0` (double the original)

## Step 4: Enable the Mod

In the Modkit:

1. Go to **Mod Loader > Modpacks**
2. Find `BiggerGuns` in the list
3. Make sure it's checked/enabled
4. Click **Deploy** (or just launch the game)

## Step 5: Test It

1. Launch Menace
2. Start a mission with a squad using the ARC-762
3. Watch enemies drop faster

Congratulations! You've made your first mod.

## Understanding the Patch Format

The `patches` section follows this structure:

```json
"patches": {
  "TemplateType": {
    "InstanceName": {
      "fieldName": newValue,
      "nestedObject.fieldName": newValue
    }
  }
}
```

- **TemplateType** - The C# class name (WeaponTemplate, EntityTemplate, SkillTemplate, etc.)
- **InstanceName** - The Unity object name (e.g., `weapon.generic_assault_rifle_tier1_ARC_762`)
- **fieldName** - The property to change
- **newValue** - Your new value (number, string, boolean)

## Nested Fields

You can modify nested objects using dot notation:

```json
"patches": {
  "WeaponTemplate": {
    "weapon.generic_assault_rifle_tier1_ARC_762": {
      "Damage": 25.0,
      "ArmorPenetration": 15.0
    }
  }
}
```

## What Can Go Wrong?

**Mod not loading?**
- Check that the modpack folder is in the game's `Mods/` directory
- Verify `modpack.json` has valid JSON (no trailing commas, matching brackets)

**Change not visible?**
- Double-check the template type name (case-sensitive)
- Double-check the instance name (must match exactly)
- Make sure you're testing in the right context (some values only matter in combat)

**Game crashes?**
- You probably set an invalid value type (string where number expected)
- Try reverting to a simpler change

## Next Steps

Now that you understand the basics:
- Try changing other stats (MaxRange, AccuracyBonus, ArmorPenetration)
- Modify multiple weapons at once
- Read the Stat Adjustments guide for more examples

---

**Next:** [Stat Adjustments](02-stat-changes.md)
