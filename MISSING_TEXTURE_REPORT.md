# GLB/Mesh Missing Texture Analysis Report

**Generated:** 2026-03-11
**Project:** MenaceAssetPacker
**Analyzed Assets:** Extracted Unity game assets via AssetRipper

---

## Executive Summary

This report analyzes the extracted game assets to identify why many GLB mesh files are missing their textures after AssetRipper extraction.

| Asset Type | Total Files | With Embedded Textures |
|------------|-------------|------------------------|
| Mesh GLBs (Assets/Mesh/) | 2,245 | 0 (0%) |
| Prefab GLBs (Assets/PrefabHierarchyObject/) | 1,695 | 1,023 (60.4%) |
| Texture PNGs (Assets/Texture2D/) | 1,963 | N/A |

### Critical Finding

**100% of mesh exports in `Assets/Mesh/` are missing embedded textures.** All mesh GLB files have:
- `DefaultMaterial` as their material name
- Zero embedded images/textures
- No texture references in material channels (baseColorTexture, normalTexture, etc.)

Meanwhile, **60.4% of prefab exports** in `Assets/PrefabHierarchyObject/` contain embedded textures with proper material names that reference actual game materials.

---

## Root Cause Analysis

### How AssetRipper Exports Meshes vs Prefabs

When AssetRipper exports a Unity project:

1. **Mesh assets** (`Assets/Mesh/*.glb`) are exported as **standalone mesh geometry only**. Unity's Mesh asset type contains vertex data, normals, UVs, and indices - but NOT material or texture references. Materials are assigned at the prefab/GameObject level, not the mesh level.

2. **Prefab assets** (`Assets/PrefabHierarchyObject/*.glb`) are exported with their full component hierarchy, including:
   - MeshRenderer components (which reference materials)
   - Material instances (which reference textures)
   - The actual texture data

This is **by design** in Unity's asset architecture - a single mesh can be used by multiple prefabs with different materials/textures. The mesh itself is just geometry.

### Why This Causes Problems

The modding workflow in MenaceAssetPacker uses `Assets/Mesh/` for mesh browsing and replacement. When users export a mesh from this folder, they get geometry with no textures because:

1. AssetRipper exports meshes with a placeholder `DefaultMaterial`
2. The actual material references are stored in prefabs, not meshes
3. Texture-to-mesh mapping must be reconstructed by name matching

---

## Evidence: Mesh vs Prefab Comparison

Comparing the same asset exported as mesh vs prefab:

### Example: `alien_01_arm_medium_l_generated.glb`

| Property | Mesh Version | Prefab Version |
|----------|--------------|----------------|
| Images (embedded) | 0 | 1 |
| Textures | 0 | 1 |
| Material Name | `DefaultMaterial` | `alien_01_material` |
| Has baseColorTexture | No | Yes |

### Pattern Observed in All 16 Tested Files

Every file that exists in both directories shows the same pattern:
- Prefab: Has embedded textures, named material (e.g., `rmc_default_male_soldier_material`)
- Mesh: No textures, generic `DefaultMaterial`

---

## Statistics by Asset Category

### Mesh Directory Breakdown

| Category | Total Files | With DefaultMaterial | With Textures |
|----------|-------------|---------------------|---------------|
| Other | 896 | 896 (100%) | 0 (0%) |
| Characters | 354 | 354 (100%) | 0 (0%) |
| Weapons | 222 | 222 (100%) | 0 (0%) |
| Structures | 214 | 214 (100%) | 0 (0%) |
| Vehicles | 188 | 188 (100%) | 0 (0%) |
| Environment | 181 | 181 (100%) | 0 (0%) |
| Aliens | 130 | 130 (100%) | 0 (0%) |
| Constructs | 32 | 32 (100%) | 0 (0%) |
| Pirates | 28 | 28 (100%) | 0 (0%) |

---

## Texture Name Matching Analysis

The existing `GlbService.cs` attempts to match textures by name pattern. Testing this approach:

### Matching Success Rate

| Status | Count | Percentage |
|--------|-------|------------|
| Found matching textures | 278 | 12.4% |
| No matching textures | 1,967 | 87.6% |

### Why Name Matching Often Fails

1. **Different naming conventions**: Mesh `SM_Env_Boulder_01.glb` vs Texture `Boulder_01_mat_MaskMap.png`
2. **Generated mesh parts**: `alien_01_arm_medium_l_generated.glb` comes from prefab with material `alien_01_material`
3. **LOD suffixes**: Texture names don't include LOD indicators
4. **Material name != Mesh name**: Material `pirates_building_garbage_2` is used by many different meshes

### Material Name to Texture Mapping (Works Better)

When using the **material name** from prefabs (not mesh filename):

| Material Name | Matching Textures Found |
|---------------|------------------------|
| `alien_01_material` | `alien_01_EffectMap`, `alien_01_Normal`, etc. |
| `construct_gunslinger` | `construct_gunslinger_BaseMap`, `_MaskMap`, `_Normal`, `_EffectMap`, `_Damage` |
| `rmc_default_male_soldier_material` | `rmc_default_male_soldier_BaseMap`, `_MaskMap`, `_EffectMap`, `_Normal` |
| `meat_gore_material` | `meat_gore_BaseMap`, `meat_gore_MaskMap`, `meat_gore_Normal` |

The material name in prefabs reliably maps to texture base names.

---

## Recommendations

### Solution 1: Use Prefab GLBs Instead of Mesh GLBs (Immediate Fix)

For any mesh that needs textures, use the version from `Assets/PrefabHierarchyObject/` instead of `Assets/Mesh/`. The prefab versions already have embedded textures.

**Implementation:**
1. Modify `AssetBrowserViewModel` to prefer prefab GLBs when available
2. Or create a "Complete Model" export option that uses prefab data

### Solution 2: Build a Material-to-Mesh Mapping Index (Recommended)

Create an index that maps each mesh to its corresponding material(s) by parsing prefab metadata:

```csharp
// Pseudocode
Dictionary<string, MaterialInfo> meshToMaterial = new();

foreach (var prefab in PrefabHierarchyObject/*.glb) {
    var meshes = ExtractMeshReferences(prefab);
    var materials = ExtractMaterials(prefab);
    foreach (var mesh in meshes) {
        meshToMaterial[mesh.name] = materials;
    }
}
```

This index can then be used by `GlbService.GetLinkedTextures()` to find the correct textures.

### Solution 3: Modify GlbService to Query Prefabs (Best Long-term)

Update `GlbService.GetLinkedTextures()` to:

1. First check if a corresponding prefab exists in `PrefabHierarchyObject/`
2. Parse the prefab to extract the actual material name
3. Use the material name (not mesh filename) for texture lookup

```csharp
public List<GlbLinkedTexture> GetLinkedTextures(string meshGlbPath)
{
    // Try to find corresponding prefab
    var prefabPath = FindCorrespondingPrefab(meshGlbPath);
    if (prefabPath != null)
    {
        var materialName = ExtractMaterialNameFromPrefab(prefabPath);
        return FindTexturesByMaterialName(materialName);
    }

    // Fallback to filename-based matching
    return GetLinkedTexturesByFilename(meshGlbPath);
}
```

### Solution 4: Enhanced Export Mode in AssetRipper (Upstream)

File a feature request with AssetRipper to add an export option that embeds material/texture references into mesh GLB exports by tracing the prefab dependencies.

---

## Files Affected

### Most Critical (Unique Meshes Without Texture Matches)

These mesh files have no matching textures via filename and no corresponding prefab:

- All 130 `*_generated.glb` files (body part meshes from skinned characters)
- 97% of environment/prop meshes
- 86% of vehicle component meshes

### Partially Affected (Has Prefab Alternative)

Many meshes exist in both `Mesh/` and `PrefabHierarchyObject/`:
- Most character body parts
- Alien models
- Standard props and weapons

For these, using the prefab version provides complete texture data.

---

## Technical Details

### Analyzed Directories

- `/Assets/Mesh/` - 2,245 GLB files (standalone mesh geometry)
- `/Assets/PrefabHierarchyObject/` - 1,695 GLB files (prefabs with hierarchy)
- `/Assets/Texture2D/` - 1,963 PNG texture files

### Texture Naming Conventions Found

| Suffix | Count | Purpose |
|--------|-------|---------|
| `_BaseMap` | 409 | Albedo/diffuse color |
| `_MaskMap` | 365 | HDRP mask (metallic, AO, detail, smoothness) |
| `_Normal` | 296 | Normal map |
| `_EffectMap` | 191 | Custom effect texture |
| `_Damage` | 124 | Damage overlay texture |
| `_Emissive` | 29 | Emission texture |
| Other | 549 | Various (UI, sprites, etc.) |

### Code References

- `GlbService.cs` - Current texture linking implementation
- `GlbLoader.cs` - Runtime GLB loading
- `GlbBundler.cs` - Deploy-time GLB processing
- `AssetBrowserViewModel.cs` - Asset browser UI

---

## Conclusion

The root cause of missing textures in GLB meshes is **architectural**: Unity stores material/texture references at the prefab/GameObject level, not the mesh level. AssetRipper correctly exports meshes as geometry-only, but this creates friction for modding workflows that expect textured mesh files.

**Recommended immediate action:** Use prefab GLBs from `Assets/PrefabHierarchyObject/` when textures are needed.

**Recommended long-term fix:** Build a material mapping index and update `GlbService` to resolve textures via prefab metadata rather than filename matching.
