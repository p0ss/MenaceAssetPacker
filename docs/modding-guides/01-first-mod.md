# Baby's First Mod

Let's make the simplest possible mod: we'll change one stat and see it work in-game. No coding required - just editing a JSON file.

## What We'll Do

We're going to make Marines move faster. That's it. One number change.

## Step 1: Create the Modpack Folder

In the Modkit app:

1. Go to **Modding Tools > Data**
2. Click **Create New Modpack**
3. Name it `SpeedyMarines`

This creates a folder structure for you.

## Step 2: Find the Stat to Change

We need to know:
- What **template type** holds Marine data
- What **field** controls movement speed

In the Modkit:

1. Go to **Modding Tools > Data**
2. Browse to `UnitTemplate`
3. Find `Marine` in the list
4. Look for `movementSpeed` (or similar)

Let's say Marines have `movementSpeed = 3.5`

## Step 3: Edit modpack.json

Open `SpeedyMarines-modpack/modpack.json` in any text editor:

```json
{
  "manifestVersion": 2,
  "name": "SpeedyMarines",
  "version": "1.0.0",
  "author": "YourName",
  "description": "Makes Marines faster",
  "loadOrder": 100,
  "patches": {
    "UnitTemplate": {
      "Marine": {
        "movementSpeed": 7.0
      }
    }
  }
}
```

That's it. We're telling the loader:
- Find the `UnitTemplate` called `Marine`
- Set its `movementSpeed` to `7.0` (double the original)

## Step 4: Enable the Mod

In the Modkit:

1. Go to **Mod Loader > Modpacks**
2. Find `SpeedyMarines` in the list
3. Make sure it's checked/enabled
4. Click **Deploy** (or just launch the game)

## Step 5: Test It

1. Launch Menace
2. Start a mission with Marines
3. Watch them zoom around the battlefield

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

- **TemplateType** - The C# class name (UnitTemplate, WeaponTemplate, etc.)
- **InstanceName** - The Unity object name (Marine, Rifleman, Shotgun, etc.)
- **fieldName** - The property to change
- **newValue** - Your new value (number, string, boolean)

## Nested Fields

You can modify nested objects using dot notation:

```json
"patches": {
  "WeaponTemplate": {
    "Rifle": {
      "damage.base": 25,
      "damage.armorPiercing": 10
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
- Try changing other stats (health, damage, cost)
- Modify multiple units at once
- Read the Stat Adjustments guide for more examples

---

**Next:** [Stat Adjustments](02-stat-changes.md)
