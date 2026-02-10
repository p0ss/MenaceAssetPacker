# 3D Models

Replacing 3D models is more complex than textures, but the Modkit makes it manageable. This guide covers mesh replacement for units, weapons, and props.

## Prerequisites

You'll need:
- A 3D modeling tool (Blender recommended - free and powerful)
- Basic understanding of 3D concepts (meshes, rigs, UV maps)
- Patience (this is the most complex modding task)

## Supported Formats

The Modpack Loader supports:
- **GLB/GLTF** - Recommended, open standard
- **FBX** - Common interchange format

GLB is preferred because it's a single file containing mesh, textures, and animations.

## Step 1: Export the Original

Use the Modkit to export the model you want to replace:

1. Go to **Modding Tools > Assets**
2. Find the mesh (e.g., `Marine_Mesh`)
3. Click **Export** - saves as GLB

This gives you a reference for:
- Scale and proportions
- Bone structure (for animated models)
- UV layout (for texturing)

## Step 2: Create Your Model

In Blender or your preferred tool:

**For static props:**
- Model your replacement
- Match the original's scale and origin point
- UV unwrap and texture

**For animated characters:**
- Import the original's skeleton (armature)
- Model your character around it
- Weight paint to the existing bones
- Don't modify the bone structure

> **Important:** The game expects specific bone names and hierarchy for animations to work. Modifying bones will break animations.

## Step 3: Export as GLB

In Blender:
1. Select your model
2. File > Export > glTF 2.0 (.glb)
3. Settings:
   - Format: GLB
   - Include: Selected Objects
   - Transform: +Y Up
   - Mesh: Apply Modifiers

## Step 4: Add to Modpack

```
MyMod-modpack/
  modpack.json
  assets/
    models/
      custom_marine.glb
```

```json
{
  "manifestVersion": 2,
  "name": "CustomMarineModel",
  "version": "1.0.0",
  "assets": {
    "Assets/Models/Units/Marine.fbx": "assets/models/custom_marine.glb"
  }
}
```

## Working with Rigs

Menace characters use skeletal animation. Key bones typically include:

```
Root
├── Hips
│   ├── Spine
│   │   ├── Chest
│   │   │   ├── Neck
│   │   │   │   └── Head
│   │   │   ├── Shoulder_L
│   │   │   │   └── Arm_L
│   │   │   │       └── Forearm_L
│   │   │   │           └── Hand_L
│   │   │   └── Shoulder_R
│   │   │       └── ...
│   │   └── ...
│   ├── Thigh_L
│   │   └── Calf_L
│   │       └── Foot_L
│   └── Thigh_R
│       └── ...
```

Your model must be rigged to these exact bone names for animations to work.

## Weapon Models

Weapons are simpler - usually no skeleton, just a static mesh:

```json
"assets": {
  "Assets/Models/Weapons/AssaultRifle.fbx": "assets/weapons/my_rifle.glb"
}
```

Tips for weapons:
- Origin point should be at the grip/handle
- Match the original's orientation
- Include attachment points if the original has them

## Materials and Textures

GLB can embed textures, but you can also reference external files:

```json
"assets": {
  "Assets/Models/Units/Marine.fbx": "assets/models/marine.glb",
  "Assets/Textures/Units/Marine_Diffuse.png": "assets/textures/marine_color.png",
  "Assets/Textures/Units/Marine_Normal.png": "assets/textures/marine_normal.png"
}
```

## LOD (Level of Detail)

Some models have multiple LOD versions:
- `Marine_LOD0.fbx` - High detail (close up)
- `Marine_LOD1.fbx` - Medium detail
- `Marine_LOD2.fbx` - Low detail (distant)

For best results, replace all LOD levels.

## Common Issues

**Model is too big/small**
- Check your export scale settings
- Blender: ensure scale is applied (Ctrl+A > Scale)
- Compare against the original's bounding box

**Model is rotated wrong**
- Unity uses Y-up, some tools use Z-up
- In Blender, export with +Y Up option

**Animations don't work**
- Bone names don't match
- Bone hierarchy is different
- Weights aren't properly assigned

**Textures missing**
- Embed textures in GLB, or
- Add separate texture replacements

**Model looks flat/unlit**
- Missing normal map
- Incorrect material setup

## Testing

1. Enable your mod
2. Launch the game
3. Spawn/encounter the modified unit
4. Check from multiple angles and distances
5. Verify animations play correctly

## Performance Considerations

- **Polycount** - Match or stay below original (check original's triangle count)
- **Texture size** - Don't exceed original texture resolution
- **Bone count** - Keep the same skeleton

Higher detail models will impact performance, especially with many units on screen.

---

**Next:** [Audio](06-audio.md)
