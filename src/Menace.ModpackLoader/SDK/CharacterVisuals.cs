using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using Menace.ModpackLoader;
using UnityEngine;

namespace Menace.SDK;

/// <summary>
/// SDK for replacing character visuals (meshes, materials) at runtime.
///
/// The problem: Character prefabs contain internal mesh references that can't be
/// replaced by simply importing a mesh with the same name. The prefab's LODGroup,
/// Animator, and gameplay components need to be preserved while only swapping
/// the visual mesh/materials.
///
/// Solution: Register visual overrides that are applied when entities spawn.
/// The system hooks into TacticalEventHooks.OnEntitySpawned and swaps meshes/materials
/// on the instantiated character while preserving all components.
///
/// Usage:
/// <code>
/// // Register an override for a character prefab
/// CharacterVisuals.RegisterOverride("rmc_default_female_soldier", config => {
///     config.ReplaceMesh("LOD0", myCustomMeshLOD0);
///     config.ReplaceMesh("LOD1", myCustomMeshLOD1);
///     config.ReplaceMaterial("body_mat", myCustomMaterial);
/// });
///
/// // Or use a GLB as the mesh source
/// CharacterVisuals.RegisterOverrideFromGlb("rmc_default_female_soldier", "my_soldier.glb");
/// </code>
/// </summary>
public static class CharacterVisuals
{
    /// <summary>
    /// Configuration for a character visual override.
    /// </summary>
    public class OverrideConfig
    {
        internal Dictionary<string, Mesh> MeshReplacements { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<string, Material> MaterialReplacements { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal Dictionary<int, Mesh> MeshReplacementsByIndex { get; } = new();
        internal GameObject SourcePrefab { get; set; }

        /// <summary>
        /// Replace a mesh by child GameObject name (e.g., "LOD0", "rmc_default_female_soldier_LOD0").
        /// </summary>
        public OverrideConfig ReplaceMesh(string childName, Mesh mesh)
        {
            if (mesh != null)
                MeshReplacements[childName] = mesh;
            return this;
        }

        /// <summary>
        /// Replace a mesh by LOD index (0 = highest detail, 3 = lowest).
        /// </summary>
        public OverrideConfig ReplaceMeshByLodIndex(int lodIndex, Mesh mesh)
        {
            if (mesh != null)
                MeshReplacementsByIndex[lodIndex] = mesh;
            return this;
        }

        /// <summary>
        /// Replace a material by name.
        /// </summary>
        public OverrideConfig ReplaceMaterial(string materialName, Material material)
        {
            if (material != null)
                MaterialReplacements[materialName] = material;
            return this;
        }

        /// <summary>
        /// Use meshes and materials from a source prefab (e.g., loaded from GLB).
        /// Meshes are matched by child name.
        /// </summary>
        public OverrideConfig UseSourcePrefab(GameObject prefab)
        {
            SourcePrefab = prefab;
            return this;
        }
    }

    // Registered overrides: prefab name -> config
    private static readonly Dictionary<string, OverrideConfig> _overrides = new(StringComparer.OrdinalIgnoreCase);

    // Track which entity instances we've already processed
    private static readonly HashSet<int> _processedInstances = new();

    // Whether we've hooked into spawning events
    private static bool _hooked;

    /// <summary>
    /// Register a visual override for a character prefab.
    /// </summary>
    /// <param name="prefabName">Name of the character prefab to override (e.g., "rmc_default_female_soldier")</param>
    /// <param name="configure">Action to configure mesh/material replacements</param>
    public static void RegisterOverride(string prefabName, Action<OverrideConfig> configure)
    {
        EnsureHooked();

        var config = new OverrideConfig();
        configure(config);
        _overrides[prefabName] = config;

        SdkLogger.Msg($"[CharacterVisuals] Registered override for '{prefabName}'");
    }

    /// <summary>
    /// Register an override using a GLB-loaded prefab as the mesh/material source.
    /// Meshes are matched by child GameObject name.
    /// </summary>
    /// <param name="prefabName">Name of the character prefab to override</param>
    /// <param name="glbAssetName">Name of the GLB asset (registered via GlbLoader)</param>
    public static void RegisterOverrideFromGlb(string prefabName, string glbAssetName)
    {
        EnsureHooked();

        // Find the GLB prefab from BundleLoader
        var glbPrefab = BundleLoader.GetAsset<GameObject>(glbAssetName);
        if (glbPrefab == null)
        {
            SdkLogger.Warning($"[CharacterVisuals] GLB prefab '{glbAssetName}' not found. Make sure it's loaded first.");
            return;
        }

        var config = new OverrideConfig { SourcePrefab = glbPrefab };
        _overrides[prefabName] = config;

        SdkLogger.Msg($"[CharacterVisuals] Registered GLB override for '{prefabName}' using '{glbAssetName}'");
    }

    /// <summary>
    /// Register an override with explicit mesh and material mappings.
    /// </summary>
    /// <param name="prefabName">Name of the character prefab to override</param>
    /// <param name="meshReplacements">Dictionary of child name -> replacement mesh</param>
    /// <param name="materialReplacements">Dictionary of material name -> replacement material</param>
    public static void RegisterOverride(
        string prefabName,
        Dictionary<string, Mesh> meshReplacements = null,
        Dictionary<string, Material> materialReplacements = null)
    {
        RegisterOverride(prefabName, config =>
        {
            if (meshReplacements != null)
            {
                foreach (var (name, mesh) in meshReplacements)
                    config.ReplaceMesh(name, mesh);
            }
            if (materialReplacements != null)
            {
                foreach (var (name, mat) in materialReplacements)
                    config.ReplaceMaterial(name, mat);
            }
        });
    }

    /// <summary>
    /// Remove a registered override.
    /// </summary>
    public static bool RemoveOverride(string prefabName)
    {
        return _overrides.Remove(prefabName);
    }

    /// <summary>
    /// Clear all registered overrides.
    /// </summary>
    public static void ClearOverrides()
    {
        _overrides.Clear();
        _processedInstances.Clear();
    }

    /// <summary>
    /// Get names of all registered overrides.
    /// </summary>
    public static string[] GetRegisteredOverrides()
    {
        return _overrides.Keys.ToArray();
    }

    /// <summary>
    /// Manually apply visual overrides to a GameObject.
    /// Useful for applying to already-spawned entities.
    /// </summary>
    /// <param name="entityGameObject">The entity's root GameObject</param>
    /// <param name="prefabName">Optional: specify which override to use. If null, tries to detect from object name.</param>
    /// <returns>True if an override was applied</returns>
    public static bool ApplyOverride(GameObject entityGameObject, string prefabName = null)
    {
        if (entityGameObject == null) return false;

        prefabName ??= DetectPrefabName(entityGameObject);
        if (string.IsNullOrEmpty(prefabName)) return false;

        if (!_overrides.TryGetValue(prefabName, out var config))
            return false;

        ApplyOverrideConfig(entityGameObject, config, prefabName);
        return true;
    }

    /// <summary>
    /// Apply overrides to all existing entities in the scene.
    /// Call this after registering overrides if entities have already spawned.
    /// </summary>
    /// <returns>Number of entities that had overrides applied</returns>
    public static int ApplyToExistingEntities()
    {
        if (_overrides.Count == 0) return 0;

        int applied = 0;
        try
        {
            // Find all SkinnedMeshRenderers in the scene
            var smrType = Il2CppType.From(typeof(SkinnedMeshRenderer));
            var allSmrs = Resources.FindObjectsOfTypeAll(smrType);

            foreach (var obj in allSmrs)
            {
                try
                {
                    var smr = obj.Cast<SkinnedMeshRenderer>();
                    if (smr == null || smr.gameObject == null) continue;

                    // Walk up to find root entity
                    var root = FindEntityRoot(smr.transform);
                    if (root == null) continue;

                    // Check if we've already processed this instance
                    var instanceId = root.GetInstanceID();
                    if (_processedInstances.Contains(instanceId)) continue;

                    // Try to apply override
                    var prefabName = DetectPrefabName(root);
                    if (!string.IsNullOrEmpty(prefabName) && _overrides.TryGetValue(prefabName, out var config))
                    {
                        ApplyOverrideConfig(root, config, prefabName);
                        _processedInstances.Add(instanceId);
                        applied++;
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("CharacterVisuals.ApplyToExistingEntities", ex.Message);
        }

        if (applied > 0)
            SdkLogger.Msg($"[CharacterVisuals] Applied overrides to {applied} existing entities");

        return applied;
    }

    // --- Internal ---

    private static void EnsureHooked()
    {
        if (_hooked) return;

        try
        {
            TacticalEventHooks.OnEntitySpawned += OnEntitySpawned;
            _hooked = true;
            SdkLogger.Msg("[CharacterVisuals] Hooked into entity spawn events");
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("CharacterVisuals.EnsureHooked",
                $"Failed to hook spawn events: {ex.Message}");
        }
    }

    private static void OnEntitySpawned(IntPtr entityPtr)
    {
        if (entityPtr == IntPtr.Zero || _overrides.Count == 0) return;

        try
        {
            // Get the entity's GameObject
            var entity = new GameObj(entityPtr);
            var gameObject = GetEntityGameObject(entity);
            if (gameObject == null) return;

            // Check if we've already processed this instance
            var instanceId = gameObject.GetInstanceID();
            if (_processedInstances.Contains(instanceId)) return;

            // Detect prefab name and check for override
            var prefabName = DetectPrefabName(gameObject);
            if (string.IsNullOrEmpty(prefabName)) return;

            if (_overrides.TryGetValue(prefabName, out var config))
            {
                ApplyOverrideConfig(gameObject, config, prefabName);
                _processedInstances.Add(instanceId);
            }
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("CharacterVisuals.OnEntitySpawned", ex.Message);
        }
    }

    private static GameObject GetEntityGameObject(GameObj entity)
    {
        try
        {
            // Try to get the visual GameObject from the entity
            // Actors have a visual child or the root is the visual

            // First try: entity might be a MonoBehaviour with a gameObject
            var managedType = GameType.Find("Menace.Tactical.Actor")?.ManagedType;
            if (managedType == null) return null;

            var proxy = Il2CppUtils.GetManagedProxy(entity, managedType);
            if (proxy == null) return null;

            // Try to get GameObject property
            var goProp = managedType.GetProperty("gameObject")
                      ?? managedType.GetProperty("GameObject");
            if (goProp != null)
            {
                var go = goProp.GetValue(proxy);
                if (go != null)
                    return ((Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase)go).Cast<GameObject>();
            }

            // Try GetComponent<Transform>().gameObject
            var transformProp = managedType.GetProperty("transform");
            if (transformProp != null)
            {
                var transform = transformProp.GetValue(proxy);
                if (transform != null)
                {
                    var t = ((Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase)transform).Cast<Transform>();
                    return t?.gameObject;
                }
            }
        }
        catch { }

        return null;
    }

    private static string DetectPrefabName(GameObject gameObject)
    {
        if (gameObject == null) return null;

        var name = gameObject.name;
        if (string.IsNullOrEmpty(name)) return null;

        // Strip "(Clone)" suffix if present
        if (name.EndsWith("(Clone)"))
            name = name.Substring(0, name.Length - 7).Trim();

        // Check if this name or any parent matches a registered override
        if (_overrides.ContainsKey(name))
            return name;

        // Check children for character model roots
        // Character prefabs often have the model as a child
        var transform = gameObject.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child == null) continue;

            var childName = child.name;
            if (!string.IsNullOrEmpty(childName))
            {
                if (childName.EndsWith("(Clone)"))
                    childName = childName.Substring(0, childName.Length - 7).Trim();

                if (_overrides.ContainsKey(childName))
                    return childName;
            }
        }

        return name;
    }

    private static GameObject FindEntityRoot(Transform transform)
    {
        // Walk up until we find an entity-like root
        var current = transform;
        while (current != null)
        {
            // Check for components that indicate this is an entity root
            if (current.GetComponent<Animator>() != null)
                return current.gameObject;

            // Check name patterns
            var name = current.name;
            if (name != null && (name.StartsWith("rmc_") || name.Contains("soldier") || name.Contains("enemy")))
                return current.gameObject;

            current = current.parent;
        }

        return transform?.gameObject;
    }

    private static void ApplyOverrideConfig(GameObject target, OverrideConfig config, string prefabName)
    {
        int meshesReplaced = 0;
        int materialsReplaced = 0;

        try
        {
            // Get all SkinnedMeshRenderers in the hierarchy
            var smrs = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            // If we have a source prefab, extract meshes/materials from it
            if (config.SourcePrefab != null)
            {
                ApplyFromSourcePrefab(target, config.SourcePrefab, smrs,
                    ref meshesReplaced, ref materialsReplaced);
            }

            // Apply explicit mesh replacements by name
            foreach (var smr in smrs)
            {
                if (smr == null) continue;

                var smrName = smr.gameObject.name;

                // Check for name match
                if (config.MeshReplacements.TryGetValue(smrName, out var mesh))
                {
                    smr.sharedMesh = mesh;
                    meshesReplaced++;
                }
                // Also try without prefab prefix (e.g., "LOD0" matches "rmc_soldier_LOD0")
                else
                {
                    foreach (var (pattern, replacementMesh) in config.MeshReplacements)
                    {
                        if (smrName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
                            smrName.EndsWith("_" + pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            smr.sharedMesh = replacementMesh;
                            meshesReplaced++;
                            break;
                        }
                    }
                }

                // Apply material replacements
                if (config.MaterialReplacements.Count > 0 && smr.sharedMaterials != null)
                {
                    var mats = smr.sharedMaterials;
                    bool changed = false;

                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;

                        var matName = mats[i].name;
                        if (config.MaterialReplacements.TryGetValue(matName, out var replacement))
                        {
                            mats[i] = replacement;
                            changed = true;
                            materialsReplaced++;
                        }
                    }

                    if (changed)
                        smr.sharedMaterials = mats;
                }
            }

            // Apply mesh replacements by LOD index
            if (config.MeshReplacementsByIndex.Count > 0)
            {
                var lodGroup = target.GetComponent<LODGroup>();
                if (lodGroup != null)
                {
                    var lods = lodGroup.GetLODs();
                    for (int i = 0; i < lods.Length && i < 4; i++)
                    {
                        if (!config.MeshReplacementsByIndex.TryGetValue(i, out var mesh))
                            continue;

                        foreach (var renderer in lods[i].renderers)
                        {
                            if (renderer is SkinnedMeshRenderer lodSmr)
                            {
                                lodSmr.sharedMesh = mesh;
                                meshesReplaced++;
                            }
                        }
                    }
                }
                else
                {
                    // No LODGroup - apply by index to SMRs in order
                    for (int i = 0; i < smrs.Length; i++)
                    {
                        if (config.MeshReplacementsByIndex.TryGetValue(i, out var mesh))
                        {
                            smrs[i].sharedMesh = mesh;
                            meshesReplaced++;
                        }
                    }
                }
            }

            if (meshesReplaced > 0 || materialsReplaced > 0)
            {
                SdkLogger.Msg($"[CharacterVisuals] Applied override to '{prefabName}': " +
                    $"{meshesReplaced} meshes, {materialsReplaced} materials");
            }
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("CharacterVisuals.ApplyOverrideConfig",
                $"Failed to apply override for '{prefabName}': {ex.Message}");
        }
    }

    private static void ApplyFromSourcePrefab(
        GameObject target,
        GameObject source,
        SkinnedMeshRenderer[] targetSmrs,
        ref int meshesReplaced,
        ref int materialsReplaced)
    {
        // Get SMRs from source prefab
        var sourceSmrs = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (sourceSmrs == null || sourceSmrs.Length == 0)
        {
            // Try MeshFilter + MeshRenderer for static meshes
            var sourceMfs = source.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in sourceMfs)
            {
                if (mf?.sharedMesh == null) continue;

                var mfName = mf.gameObject.name;

                // Find matching target SMR
                foreach (var targetSmr in targetSmrs)
                {
                    if (targetSmr == null) continue;

                    var targetName = targetSmr.gameObject.name;
                    if (NamesMatch(mfName, targetName))
                    {
                        targetSmr.sharedMesh = mf.sharedMesh;
                        meshesReplaced++;

                        // Also copy materials from MeshRenderer
                        var mr = mf.GetComponent<MeshRenderer>();
                        if (mr?.sharedMaterials != null)
                        {
                            targetSmr.sharedMaterials = mr.sharedMaterials;
                            materialsReplaced += mr.sharedMaterials.Length;
                        }
                        break;
                    }
                }
            }
            return;
        }

        // Match source SMRs to target SMRs by name
        foreach (var sourceSmr in sourceSmrs)
        {
            if (sourceSmr?.sharedMesh == null) continue;

            var sourceName = sourceSmr.gameObject.name;

            foreach (var targetSmr in targetSmrs)
            {
                if (targetSmr == null) continue;

                var targetName = targetSmr.gameObject.name;
                if (NamesMatch(sourceName, targetName))
                {
                    targetSmr.sharedMesh = sourceSmr.sharedMesh;
                    meshesReplaced++;

                    if (sourceSmr.sharedMaterials != null)
                    {
                        targetSmr.sharedMaterials = sourceSmr.sharedMaterials;
                        materialsReplaced += sourceSmr.sharedMaterials.Length;
                    }
                    break;
                }
            }
        }
    }

    private static bool NamesMatch(string source, string target)
    {
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            return true;

        // Check if one ends with the other (for LOD matching)
        // e.g., "LOD0" matches "rmc_soldier_LOD0"
        if (target.EndsWith(source, StringComparison.OrdinalIgnoreCase))
            return true;
        if (source.EndsWith(target, StringComparison.OrdinalIgnoreCase))
            return true;

        // Extract LOD suffix and compare
        var sourceLod = ExtractLodSuffix(source);
        var targetLod = ExtractLodSuffix(target);
        if (!string.IsNullOrEmpty(sourceLod) && sourceLod == targetLod)
            return true;

        return false;
    }

    private static string ExtractLodSuffix(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        // Look for LOD0, LOD1, etc.
        var idx = name.LastIndexOf("LOD", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0 && idx + 4 <= name.Length)
        {
            var suffix = name.Substring(idx);
            if (suffix.Length >= 4 && char.IsDigit(suffix[3]))
                return suffix.Substring(0, 4).ToUpperInvariant();
        }

        return null;
    }
}
