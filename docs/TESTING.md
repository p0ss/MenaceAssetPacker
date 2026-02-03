# Testing Stat Modifications

## Quick Test Guide

### 1. Open the Data Browser

```bash
cd /home/poss/Documents/Code/Menace/MenaceAssetPacker/tools
firefox data-browser.html
# or: chromium data-browser.html
```

Click "Choose Files" and navigate to:
```
~/.steam/debian-installation/steamapps/common/Menace Demo/UserData/ExtractedData/
```

Select all `.json` files and load them.

### 2. Find a Weapon to Test

**Recommended test weapons (easy to identify):**

Click "WeaponTemplate" in the sidebar, then search for:
- `cannon_long` - Heavy cannon (165 damage baseline)
- `rocket_launcher` - Rocket launcher (180 damage baseline)
- `mortar` - Heavy mortar (25 damage baseline)
- `plasma_rifle` - Plasma rifle (60 damage baseline)

The browser will show you:
- Damage values
- Range (MinRange, IdealRange, MaxRange)
- Accuracy stats
- Armor penetration

### 3. Create a Test Modification

Let's boost the heavy mortar to be overpowered for easy testing:

```bash
cd ~/.steam/debian-installation/steamapps/common/Menace\ Demo/UserData/ModifiedData
```

Create `WeaponTemplate.json` with:

```json
[
  {
    "name": "mod_weapon.heavy.mortar",
    "MinRange": 8,
    "IdealRange": 12,
    "MaxRange": 16,
    "AccuracyBonus": 50.0,
    "AccuracyDropoff": 0.0,
    "Damage": 999.0,
    "DamageDropoff": 0.0,
    "ArmorPenetration": 999.0,
    "ArmorPenetrationDropoff": 0.0
  }
]
```

This makes the heavy mortar:
- Deal 999 damage (up from 25)
- Have 999 armor penetration (up from 35)
- Have perfect accuracy at all ranges
- Keep same range profile

### 4. Launch and Test

```bash
cd ~/.steam/debian-installation/steamapps/common/Menace\ Demo
tail -f MelonLoader/Latest.log | grep -i "stat modifier"
```

Launch the game and check the log output. You should see:

```
Menace Stat Modifier v1.0.0
Modified data path: .../UserData/ModifiedData
✓ Loaded 1 modified WeaponTemplate.json
Loaded 1 modified templates:
  - 1 weapons
  - 0 armor
  - 0 accessories
  - 0 entities
✓ Applied Harmony patches
```

### 5. In-Game Verification

The heavy mortar appears on:
- Heavy vehicles (turret options)
- Some modular vehicle weapon slots
- Infantry support weapons (tripod/heavy weapon teams)

**How to verify:**
1. Start a skirmish/battle with heavy vehicles
2. Check unit details/loadout screen
3. If the weapon damage is displayed, it should show 999
4. In combat, mortar shots should be devastating one-shots

### Alternative: Test with Infantry Weapons

Easier to test with infantry since you can see their weapons in loadout:

**Modify a common assault rifle:**

```json
[
  {
    "name": "weapon.generic_assault_rifle_tier1_ARC_762",
    "MinRange": 1,
    "IdealRange": 3,
    "MaxRange": 7,
    "AccuracyBonus": 50.0,
    "AccuracyDropoff": 0.0,
    "Damage": 500.0,
    "DamageDropoff": 0.0,
    "ArmorPenetration": 500.0,
    "ArmorPenetrationDropoff": 0.0
  }
]
```

The ARC-762 is a starter weapon that should be easy to find in the game.

### 6. Armor Testing

Test armor modifications with:

```json
[
  {
    "name": "armor.heavy_armor",
    "Armor": 999,
    "DurabilityPerElement": 999,
    "DamageResistance": 0.99,
    "HitpointsPerElement": 999,
    "Accuracy": 50,
    "AccuracyMult": 1.5,
    "DefenseMult": 2.0,
    "Discipline": 50.0,
    "Vision": 100,
    "Detection": 100
  }
]
```

**Note:** You'll need to search the ArmorTemplate in the browser first to find valid armor names.

### Troubleshooting Test Issues

**Modifications not applying:**
- Check MelonLoader log for errors
- Verify JSON syntax (use jsonlint.com)
- Ensure template name matches exactly (case-sensitive)
- Make sure StatModifier.dll is in Mods/ folder

**Can't find the weapon in-game:**
- Search WeaponTemplate in browser for full list
- Look for patterns: `weapon.*`, `mod_weapon.*`, `specialweapon.*`, `turret.*`
- Try infantry weapons first (easier to verify)

**Harmony patch errors:**
- Check that HarmonyX package installed correctly
- Verify no other mods conflict with same patches

### Data Browser Features

**Search:** Type any text to filter items (searches all fields)
- `mortar` - Find all mortars
- `plasma` - Find plasma weapons
- `heavy` - Find heavy equipment
- `999` - Find items you've modified with test values

**Export:** Click "Export to ModifiedData" to download a template JSON
- Edit the downloaded file
- Keep only items you want to modify
- Place in ModifiedData/ folder

**Column sorting:** Click column headers to sort (browser dependent)

### Finding Specific Item Types

**Vehicle weapons:** Search WeaponTemplate for:
- `mod_weapon.*` - Modular vehicle weapons
- `turret.*` - Fixed turret weapons

**Infantry weapons:** Search WeaponTemplate for:
- `weapon.*` - Standard infantry weapons
- `specialweapon.*` - Special/heavy infantry weapons

**Armor:** Search ArmorTemplate for:
- `armor.*` - Body armor pieces

**Units:** Search EntityTemplate for:
- `inf.*` - Infantry units
- `vehicle.*` - Vehicle units

## Advanced: Testing Entity Stats

EntityTemplate has nested Properties object. To test:

```json
[
  {
    "name": "inf.generic_rifleman_tier1",
    "ElementsMin": 10,
    "ElementsMax": 12,
    "ArmyPointCost": 50,
    "Properties": {
      "HitpointsPerElement": 999,
      "Armor": 100,
      "ArmorSide": 100,
      "ArmorBack": 100,
      "ArmorDurabilityPerElement": 999.0,
      "ActionPoints": 20,
      "Accuracy": 100.0,
      "AccuracyDropoff": 0.0,
      "DefenseMult": 2.0,
      "Discipline": 100.0,
      "Vision": 30,
      "Detection": 30,
      "Concealment": 50
    }
  }
]
```

**Note:** EntityTemplate.Properties patches aren't implemented yet in MVP. This requires additional Harmony patches for the nested Properties object.
