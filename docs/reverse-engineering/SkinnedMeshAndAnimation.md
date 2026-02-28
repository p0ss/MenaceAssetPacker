# Skinned Mesh and Animation Technical Documentation

This document captures findings from reverse-engineering Unity's skinned mesh and animation formats
for the Menace Modkit project. Last updated: 2026-02-28.

## Quick Start - Testing Animation Commands

Open the dev console (default: backtick \`) and try:

```
anim.rotate "Main Camera" up 30
```

This should make the camera slowly rotate. If it works, the animation system is functional.

To stop:
```
anim.stop "Main Camera"
```

For a more practical test, find a vehicle or object in-game and rotate one of its parts:
```
anim.list                              -- See active animations
anim.rotate "SomeVehicle/Wheel" right 360  -- Rotate a wheel
anim.hover "SomeObject" 0.5 1          -- Make something float
```

## Overview

Unity uses two main components for skeletal animation:
1. **SkinnedMeshRenderer** - Mesh with bone weights and skeleton binding
2. **AnimationClip** - Keyframe data for bone transforms over time

When exporting to GLB/GLTF, these map to:
1. **Skins** - Joint list with inverse bind matrices
2. **Animations** - Channel/sampler pairs targeting node transforms

## Unity SkinnedMeshRenderer Structure

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `mesh` | Mesh | The skinned mesh asset |
| `bones` | Transform[] | Array of bone transforms |
| `rootBone` | Transform | Root of skeleton hierarchy |
| `sharedMaterial` | Material | Material for rendering |
| `localBounds` | Bounds | Bounding box in local space |

### Mesh Bone Data

The Mesh asset contains per-vertex skinning data:

| Property | Type | Description |
|----------|------|-------------|
| `boneWeights` | BoneWeight[] | Per-vertex bone influences (up to 4) |
| `bindposes` | Matrix4x4[] | Inverse bind matrices for each bone |

### BoneWeight Structure

```csharp
struct BoneWeight {
    int boneIndex0, boneIndex1, boneIndex2, boneIndex3;  // Bone indices
    float weight0, weight1, weight2, weight3;             // Blend weights (sum to 1.0)
}
```

## GLTF/GLB Skin Structure

### Skin Object

```json
{
  "skins": [{
    "inverseBindMatrices": 5,  // Accessor index for Matrix4x4[]
    "skeleton": 1,             // Root joint node index
    "joints": [1, 2, 3, 4, 5]  // Joint node indices (order matches bone indices)
  }]
}
```

### Mesh Primitive Attributes

```json
{
  "primitives": [{
    "attributes": {
      "POSITION": 0,
      "NORMAL": 1,
      "JOINTS_0": 2,     // uvec4: bone indices per vertex
      "WEIGHTS_0": 3     // vec4: blend weights per vertex
    }
  }]
}
```

### Node with Skin Reference

```json
{
  "nodes": [{
    "name": "SkinnedMesh",
    "mesh": 0,
    "skin": 0  // Reference to skins array
  }]
}
```

## Unity AnimationClip Structure

### Curve Types in AnimationClip

| Property | Type | Description |
|----------|------|-------------|
| `PositionCurves_C74` | IVector3Curve[] | Translation animations |
| `RotationCurves_C74` | IQuaternionCurve[] | Quaternion rotation animations |
| `EulerCurves_C74` | IVector3Curve[] | Euler angle rotation animations |
| `ScaleCurves_C74` | IVector3Curve[] | Scale animations |

### Curve Structure

Each curve has:
- `Path` (Utf8String): Bone hierarchy path (e.g., "Spine01/Neck/Head")
- `Curve`: Collection of keyframes

### Keyframe Structure

```csharp
// For Vector3 curves (Position, Euler, Scale)
struct Keyframe_Vector3f {
    float Time;
    Vector3f Value;
    Vector3f InSlope;   // Tangent for interpolation
    Vector3f OutSlope;
    int WeightedMode;
}

// For Quaternion curves (Rotation)
struct Keyframe_Quaternionf {
    float Time;
    Quaternionf Value;
    Quaternionf InSlope;
    Quaternionf OutSlope;
    int WeightedMode;
}
```

### Root Motion

Root motion animations use **empty path** ("") to target the skeleton root.
This allows characters to move in world space based on animation data.

## GLTF Animation Structure

### Animation Object

```json
{
  "animations": [{
    "name": "Walk",
    "channels": [{
      "sampler": 0,
      "target": {
        "node": 2,           // Target joint node
        "path": "translation" // or "rotation", "scale"
      }
    }],
    "samplers": [{
      "input": 10,           // Accessor: time values
      "output": 11,          // Accessor: transform values
      "interpolation": "LINEAR"
    }]
  }]
}
```

## Coordinate System Conversion

Unity uses **left-handed Y-up** coordinate system.
GLTF uses **right-handed Y-up** coordinate system.

### Conversion Rules

| Unity | GLTF |
|-------|------|
| Position (X, Y, Z) | (X, Y, -Z) |
| Rotation (X, Y, Z, W) | (X, Y, -Z, -W) |
| Scale (X, Y, Z) | (X, Y, Z) |

## Animation Approaches for Modders

There are three approaches for adding animation to modded content, ordered by complexity:

**Note**: To find GameObject paths for console commands, use the dev console's hierarchy
browser or `find` command. Paths are slash-separated, e.g., `"ParentObject/ChildObject/Wheel"`.

### Approach A: Bone Name Matching (Simplest - for artists)

Create a mesh with bones that match existing game skeleton bone names. The game's
existing animations will automatically drive your mesh.

**Use cases**: Character replacements, creature skins, vehicle variants

**How to**:
1. Export an existing game model with skeleton
2. Create your mesh bound to bones with the same names
3. Export as GLB with skeleton
4. The game's existing animations will play on your mesh

### Approach B: SDK Animation Helpers (for programmers)

Use simple animation components for common effects like rotating wheels or hovering.

#### Console Commands (for testing)

```
anim.rotate <path> <axis> <speed>    - Add rotation animation
anim.hover <path> <amplitude> <freq> - Add hover animation
anim.bob <path> <amplitude> <freq>   - Add bob animation
anim.stop <path>                     - Remove all animations from object
anim.list                            - List active animations
```

**Axis values**: `up`, `right`, `forward`, `down`, `left`, `back` (or `x`, `y`, `z`)

**Examples**:
```
anim.rotate "Vehicle/Wheel_FL" right 360
anim.hover "Aircraft/Body" 0.5 1
anim.bob "Boat/Hull" 0.3 0.5
anim.stop "Vehicle/Wheel_FL"
```

#### Lua Helpers

```lua
add_rotate(path, axis, speed)           -- Add rotation
add_hover(path, amplitude, frequency)   -- Add hover
add_bob(path, amplitude, frequency)     -- Add bob
remove_animations(path)                 -- Remove all animations
```

**Examples**:
```lua
add_rotate("Vehicle/Wheel_FL", "right", 360)
add_hover("Aircraft/Body", 0.5, 1)
add_bob("Boat/Hull", 0.3, 0.5)
remove_animations("Vehicle/Wheel_FL")
```

#### C# API

```csharp
using Menace.SDK;

// Rotate - continuous rotation (wheels, propellers, fans)
var rotator = gameObject.AddComponent<Rotate>();
rotator.axis = Vector3.right;  // Rotation axis
rotator.speed = 360f;          // Degrees per second

// Hover - sine wave movement (floating objects)
var hover = gameObject.AddComponent<Hover>();
hover.amplitude = 0.5f;        // Max displacement
hover.frequency = 1f;          // Cycles per second
hover.axis = Vector3.up;       // Movement axis

// Bob - bobbing with tilt (boats, breathing)
var bob = gameObject.AddComponent<Bob>();
bob.bobAmplitude = 0.3f;       // Vertical displacement
bob.bobFrequency = 0.5f;       // Bob speed
bob.tiltAngle = 5f;            // Max tilt degrees

// Spin - acceleration/deceleration (turbines)
var spin = gameObject.AddComponent<Spin>();
spin.targetSpeed = 360f;       // Target deg/sec
spin.acceleration = 90f;       // Deg/sec^2
spin.active = true;            // Set false to spin down

// Oscillate - pendulum motion (sensors, doors)
var osc = gameObject.AddComponent<Oscillate>();
osc.mode = Oscillate.OscillateMode.Rotation;
osc.axis = Vector3.right;      // Rotation axis
osc.magnitude = 45f;           // Max angle
osc.frequency = 0.5f;          // Cycles per second
```

#### Component Reference

| Component | Use Case | Key Properties |
|-----------|----------|----------------|
| `Rotate` | Wheels, propellers, fans | `axis`, `speed` (deg/sec) |
| `Hover` | Floating objects, magic items | `amplitude`, `frequency`, `axis` |
| `Bob` | Boats, breathing, floating debris | `bobAmplitude`, `bobFrequency`, `tiltAngle` |
| `Spin` | Turbines with spin-up/down | `targetSpeed`, `acceleration`, `active` |
| `Oscillate` | Pendulums, scanning sensors | `mode`, `axis`, `magnitude`, `frequency` |

### Approach C: Unity Prefabs (for complex animations)

For complex animations (multi-bone keyframed sequences, state machines), create
full Unity prefabs with Animation/Animator components.

**How to**:
1. Create your model and animations in Unity
2. Set up Animation/Animator components
3. Export as AssetBundle
4. Load via existing BundleLoader/CompiledAssetLoader

## Implementation Notes

### Export Path (AssetRipper GlbLevelBuilder)

1. Traverse GameObject hierarchy looking for SkinnedMeshRenderer
2. Extract bone transforms from `BonesP` property
3. Build skeleton hierarchy by checking parent relationships
4. Create GLTF joint nodes with local transforms
5. Build skin with joints array and inverse bind matrices
6. Add mesh with JOINTS_0/WEIGHTS_0 attributes
7. Call `SceneBuilder.AddSkinnedMesh()` with joint nodes

### Import Path (GlbLoader - Runtime)

1. Load GLB file via SharpGLTF
2. Find meshes with skin references
3. Build bone hierarchy from joint indices
4. Create GameObjects for each joint with correct transforms
5. Create Mesh with bone weights from JOINTS_0/WEIGHTS_0
6. Create SkinnedMeshRenderer with bones array

### Animation Path Matching

Animation curves use hierarchy paths like "Armature/Spine01/Neck/Head".
These must match the runtime bone names exactly.

Fallback matching strategies:
1. Exact path match
2. Last segment match (just bone name)
3. Case-insensitive match

## File Locations

### AssetRipper Export Code
- `Source/AssetRipper.Export.Modules.Models/GlbLevelBuilder.cs` - Main export logic
- `Source/AssetRipper.Export.Modules.Models/GlbMeshBuilder.cs` - Mesh-only export
- `Source/AssetRipper.Export.Modules.Models/GlbSubMeshBuilder.cs` - Submesh building

### ModpackLoader Runtime Code
- `src/Menace.ModpackLoader/GlbLoader.cs` - Runtime GLB loader with skinned mesh support
- `src/Menace.ModpackLoader/SDK/SimpleAnimations.cs` - Simple animation components

## Known Limitations

1. **Blend shapes/morph targets**: Not currently exported
2. **Multiple materials per mesh**: Exported as submeshes
3. **GLB Animation clips**: Not loaded (use bone matching or SDK helpers instead)
4. **IK constraints**: Not preserved in export
5. **Animation events**: Not currently exported

## References

- [GLTF 2.0 Specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html)
- [Unity Serialized File Format](https://github.com/ata4/disunity/wiki/Serialized-file-format)
- [SharpGLTF Library](https://github.com/vpenades/SharpGLTF)
