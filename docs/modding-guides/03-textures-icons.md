# Textures & Icons

Ready to make visual changes? This guide covers replacing 2D images - textures, icons, portraits, and UI elements.

## How Asset Replacement Works

The Modpack Loader intercepts Unity's asset loading. When the game requests a texture, the loader checks if your modpack has a replacement. If so, your file is loaded instead.

No game files are modified - the replacement happens in memory at runtime.

## Supported Formats

- **PNG** - Recommended for most textures
- **JPG** - Okay for photos/backgrounds (lossy compression)
- **DDS** - Advanced, preserves GPU compression

PNG is the safest choice. Use 32-bit PNG with alpha channel for transparency.

## Step 1: Extract Original Assets

First, extract the game's assets so you can see what you're replacing:

1. In Modkit, go to **Settings**
2. Set your **Extracted Assets Path**
3. Click **Extract Assets** (this takes a while)

Once complete, browse the extracted folder to find textures.

## Step 2: Find the Asset Path

Every asset has a **path** that identifies it. In the Modkit:

1. Go to **Modding Tools > Assets**
2. Browse or search for the texture you want to replace
3. Note the full asset path (e.g., `Assets/Textures/Units/Marine_Portrait.png`)

## Step 3: Create Your Replacement

Open the original in an image editor (GIMP, Photoshop, etc.):

- Keep the same dimensions (or the game may scale it oddly)
- Keep the same format (PNG for PNG, etc.)
- Preserve alpha channels for transparency

Save your modified version.

## Step 4: Add to Modpack

Place your replacement in the modpack's `assets/` folder, then reference it in `modpack.json`:

```json
{
  "manifestVersion": 2,
  "name": "CustomPortraits",
  "version": "1.0.0",
  "assets": {
    "Assets/Textures/Units/Marine_Portrait.png": "assets/my_marine.png"
  }
}
```

The format is:
```
"original/asset/path": "path/in/modpack"
```

## Example: Custom Unit Icons

Let's replace the Marine's unit icon:

```
MyMod-modpack/
  modpack.json
  assets/
    marine_icon.png
```

```json
{
  "manifestVersion": 2,
  "name": "NewMarineIcon",
  "version": "1.0.0",
  "assets": {
    "Assets/Textures/UI/Icons/Unit_Marine.png": "assets/marine_icon.png"
  }
}
```

## Multiple Asset Replacements

Replace as many assets as you want:

```json
"assets": {
  "Assets/Textures/UI/Icons/Unit_Marine.png": "assets/icons/marine.png",
  "Assets/Textures/UI/Icons/Unit_Medic.png": "assets/icons/medic.png",
  "Assets/Textures/UI/Icons/Unit_Sniper.png": "assets/icons/sniper.png",
  "Assets/Textures/Weapons/Rifle_Diffuse.png": "assets/weapons/rifle.png"
}
```

## Texture Types

Different textures serve different purposes:

| Suffix | Purpose |
|--------|---------|
| `_Diffuse` | Base color/albedo |
| `_Normal` | Surface detail (bumps) |
| `_Specular` | Shininess/reflection |
| `_Emission` | Glow effects |
| `_Mask` | Transparency/cutout |

When replacing a texture, usually you only need to change `_Diffuse`. But for complete visual changes, replace all related textures.

## Tips for Good Results

**Match the original size**
If the original is 512x512, make yours 512x512. Larger textures waste memory; smaller ones look blurry.

**Preserve aspect ratio**
Don't stretch square textures into rectangles.

**Test in-game**
Some textures look different in the editor vs. in-game due to lighting and shaders.

**Check mipmaps**
For 3D textures, the game uses smaller versions at distance. Your PNG will have mipmaps generated automatically.

## Troubleshooting

**Texture not showing?**
- Double-check the asset path (case-sensitive!)
- Verify your image file exists and isn't corrupt
- Check Modkit output for loading errors

**Texture looks wrong?**
- Wrong dimensions - resize to match original
- Missing alpha channel - use 32-bit PNG
- Color space issues - use sRGB for diffuse textures

**Performance issues?**
- Textures too large - downscale to original size
- Too many replacements - combine into texture atlases

---

**Next:** [3D Models](04-3d-models.md)
