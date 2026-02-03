using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;

namespace Menace.ModpackLoader;

/// <summary>
/// General-purpose asset replacement for modpacks. Supports any Unity asset type
/// (Texture2D, AudioClip, Mesh, Sprite, Material, etc.) via type-specific
/// in-place overwrite strategies.
///
/// Two replacement sources:
///   1. Disk files registered via the modpack "assets" map — matched by name,
///      loaded using format-appropriate loaders (PNG/JPG for textures, WAV/OGG for audio).
///   2. Bundle-loaded assets from BundleLoader — if a bundle asset has the same
///      name and type as an existing game object, its data overwrites the original.
///
/// NOTE: Harmony patching of Resources.Load is NOT used. Resources.Load is generic
/// in IL2CPP and cannot be hooked by Harmony. Instead, after each scene loads we
/// find all matching objects already in memory and overwrite them.
/// </summary>
public static class AssetReplacer
{
    /// <summary>
    /// A registered disk-file replacement.
    /// </summary>
    private class Replacement
    {
        public string AssetPath;   // Original Unity asset path (e.g. "Assets/Resources/ui/textures/backgrounds/bg.png")
        public string DiskPath;    // Absolute path to replacement file on disk
        public string AssetName;   // Filename without extension, used for matching (e.g. "bg")
        public AssetKind Kind;     // Inferred from file extension
    }

    public enum AssetKind
    {
        Texture,    // PNG, JPG, JPEG, TGA, BMP — can load from disk
        Audio,      // WAV, OGG, MP3 — bundle-sourced
        Model,      // GLB, GLTF, FBX, OBJ — bundle-sourced (prefab hierarchy)
        Material,   // MAT — bundle-sourced
        Unknown     // Anything else — can still be replaced via bundles
    }

    // All disk-file replacements, keyed by asset name (case-insensitive)
    private static readonly Dictionary<string, Replacement> _replacements
        = new(StringComparer.OrdinalIgnoreCase);

    // Byte cache to avoid re-reading files from disk
    private static readonly Dictionary<string, byte[]> _bytesCache = new();

    public static int RegisteredCount => _replacements.Count;

    /// <summary>
    /// Register a disk-file asset replacement. Called during modpack loading.
    /// The asset kind is inferred from the file extension.
    /// </summary>
    public static void RegisterAssetReplacement(string assetPath, string diskFilePath)
    {
        var name = Path.GetFileNameWithoutExtension(assetPath);
        var ext = Path.GetExtension(assetPath).ToLowerInvariant();
        var kind = InferKind(ext);

        _replacements[name] = new Replacement
        {
            AssetPath = assetPath,
            DiskPath = diskFilePath,
            AssetName = name,
            Kind = kind
        };
    }

    /// <summary>
    /// Check if there is a registered replacement for the given asset path.
    /// </summary>
    public static bool HasReplacement(string assetPath)
    {
        var name = Path.GetFileNameWithoutExtension(assetPath);
        return _replacements.ContainsKey(name);
    }

    /// <summary>
    /// Apply all registered replacements and all bundle-sourced replacements.
    /// Call this after a scene loads (with a short delay for objects to initialize).
    /// </summary>
    public static void ApplyAllReplacements()
    {
        int total = 0;

        // 1. Disk-file replacements grouped by kind
        total += ApplyTextureReplacements();
        total += ApplyAudioReplacements();

        // 2. Bundle-sourced replacements: if BundleLoader has an asset with the same
        //    name and type as an existing game object, overwrite the original.
        total += ApplyBundleReplacements();

        if (total > 0)
        {
            MelonLogger.Msg($"Asset replacement complete: {total} asset(s) replaced");
            UnityEngine.Debug.Log($"[MODDED] Assets replaced in scene: {total}");
        }
    }

    // ------------------------------------------------------------------
    // Texture replacements (PNG, JPG, TGA, BMP → ImageConversion.LoadImage)
    // ------------------------------------------------------------------

    private static int ApplyTextureReplacements()
    {
        var textureReplacements = _replacements.Values
            .Where(r => r.Kind == AssetKind.Texture)
            .ToList();

        if (textureReplacements.Count == 0)
            return 0;

        MelonLogger.Msg($"  Searching for {textureReplacements.Count} texture replacement(s)...");

        try
        {
            var il2cppType = Il2CppType.From(typeof(Texture2D));
            var allTextures = Resources.FindObjectsOfTypeAll(il2cppType);
            if (allTextures == null || allTextures.Length == 0)
            {
                MelonLogger.Warning("  FindObjectsOfTypeAll(Texture2D) returned 0 objects");
                return 0;
            }

            MelonLogger.Msg($"  Found {allTextures.Length} Texture2D objects in memory");

            // Build a secondary lookup: filename-only → replacement
            // Handles cases where the game texture's .name includes path components
            // e.g. "ui/textures/backgrounds/title_bg_02" should still match "title_bg_02"
            var byFilename = new Dictionary<string, Replacement>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in textureReplacements)
                byFilename[r.AssetName] = r;

            int replaced = 0;
            var unmatchedNames = new HashSet<string>(
                textureReplacements.Select(r => r.AssetName), StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < allTextures.Length; i++)
            {
                var obj = allTextures[i];
                if (obj == null) continue;

                var texName = obj.name;
                if (string.IsNullOrEmpty(texName)) continue;

                // Try direct match first (most textures use filename-only names)
                if (!byFilename.TryGetValue(texName, out var replacement))
                {
                    // Fallback: extract filename from path-style names
                    // e.g. "ui/textures/backgrounds/title_bg_02" → "title_bg_02"
                    var lastSep = texName.LastIndexOfAny(new[] { '/', '\\' });
                    if (lastSep >= 0)
                    {
                        var nameOnly = texName.Substring(lastSep + 1);
                        byFilename.TryGetValue(nameOnly, out replacement);
                    }
                }

                if (replacement == null || replacement.Kind != AssetKind.Texture)
                    continue;

                unmatchedNames.Remove(replacement.AssetName);

                try
                {
                    var tex = obj.Cast<Texture2D>();
                    if (tex == null) continue;

                    var bytes = GetOrLoadBytes(replacement.DiskPath);
                    if (bytes == null)
                    {
                        MelonLogger.Warning($"  Could not read replacement file: {replacement.DiskPath}");
                        continue;
                    }

                    MelonLogger.Msg($"  Applying texture replacement: '{texName}' ({tex.width}x{tex.height}, {tex.format}) ← {bytes.Length} bytes");

                    // Explicit Il2Cpp array conversion for reliability
                    var il2cppBytes = new Il2CppStructArray<byte>(bytes);
                    bool success = ImageConversion.LoadImage(tex, il2cppBytes);

                    if (success)
                    {
                        replaced++;
                        MelonLogger.Msg($"  Replaced texture: {texName} → now {tex.width}x{tex.height}");
                    }
                    else
                    {
                        MelonLogger.Warning($"  ImageConversion.LoadImage FAILED for '{texName}' — texture may be read-only or compressed");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"  Failed to replace texture {texName}: {ex.Message}");
                }
            }

            // Log unmatched replacements to help diagnose name mismatches
            if (unmatchedNames.Count > 0)
            {
                MelonLogger.Warning($"  {unmatchedNames.Count} texture replacement(s) found NO matching game texture:");
                foreach (var name in unmatchedNames)
                    MelonLogger.Warning($"    No match for: '{name}'");

                // Dump a sample of actual texture names to help debug
                MelonLogger.Msg("  Sample of game texture names (first 30):");
                int dumped = 0;
                for (int i = 0; i < allTextures.Length && dumped < 30; i++)
                {
                    var obj = allTextures[i];
                    if (obj == null) continue;
                    var n = obj.name;
                    if (string.IsNullOrEmpty(n)) continue;
                    // Only dump names that look potentially relevant (contain keywords from unmatched)
                    foreach (var unmatched in unmatchedNames)
                    {
                        if (n.Contains(unmatched, StringComparison.OrdinalIgnoreCase) ||
                            unmatched.Contains(n, StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("bg", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("title", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("loading", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("background", StringComparison.OrdinalIgnoreCase))
                        {
                            MelonLogger.Msg($"    [{i}] '{n}'");
                            dumped++;
                            break;
                        }
                    }
                }
                if (dumped == 0)
                {
                    // Just dump the first 20 names regardless
                    MelonLogger.Msg("  First 20 texture names:");
                    dumped = 0;
                    for (int i = 0; i < allTextures.Length && dumped < 20; i++)
                    {
                        var obj = allTextures[i];
                        if (obj == null) continue;
                        var n = obj.name;
                        if (string.IsNullOrEmpty(n)) continue;
                        MelonLogger.Msg($"    [{i}] '{n}'");
                        dumped++;
                    }
                }
            }

            return replaced;
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"ApplyTextureReplacements failed: {ex.Message}");
            return 0;
        }
    }

    // ------------------------------------------------------------------
    // Audio replacements (WAV, OGG → AudioClip data copy)
    // ------------------------------------------------------------------

    private static int ApplyAudioReplacements()
    {
        var audioReplacements = _replacements.Values
            .Where(r => r.Kind == AssetKind.Audio)
            .ToList();

        if (audioReplacements.Count == 0)
            return 0;

        // Audio replacement from raw files requires format-specific loading.
        // For now, log what we found — full WAV/OGG loading will be added
        // when the audio modding pipeline is implemented. Bundle-sourced
        // audio replacements (below) work for all formats.
        foreach (var r in audioReplacements)
        {
            MelonLogger.Msg($"  Audio replacement registered (pending bundle support): {r.AssetName}");
        }

        return 0;
    }

    // ------------------------------------------------------------------
    // Bundle-sourced replacements (any type loaded from AssetBundle)
    // ------------------------------------------------------------------

    private static int ApplyBundleReplacements()
    {
        if (BundleLoader.LoadedAssetCount == 0)
            return 0;

        int replaced = 0;

        // For each type that BundleLoader has assets for, find matching game objects
        // and overwrite them. Textures get pixel-level copy, others get property copy.
        replaced += ApplyBundleTextureReplacements();
        replaced += ApplyBundleAudioReplacements();
        replaced += ApplyBundleMeshReplacements();
        replaced += ApplyBundleMaterialReplacements();
        replaced += ApplyBundlePrefabReplacements();

        return replaced;
    }

    /// <summary>
    /// For bundle-loaded Texture2D assets, find the matching game texture and copy pixels.
    /// </summary>
    private static int ApplyBundleTextureReplacements()
    {
        var bundleTextures = BundleLoader.GetAssetsByType("Texture2D");
        if (bundleTextures.Count == 0)
            return 0;

        try
        {
            var il2cppType = Il2CppType.From(typeof(Texture2D));
            var allTextures = Resources.FindObjectsOfTypeAll(il2cppType);
            if (allTextures == null || allTextures.Length == 0)
                return 0;

            // Build a lookup of bundle texture names for fast matching
            var bundleTexByName = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            foreach (var bt in bundleTextures)
            {
                try
                {
                    var tex = bt.Cast<Texture2D>();
                    if (tex != null && !string.IsNullOrEmpty(bt.name))
                        bundleTexByName[bt.name] = tex;
                }
                catch { }
            }

            int replaced = 0;
            for (int i = 0; i < allTextures.Length; i++)
            {
                var obj = allTextures[i];
                if (obj == null) continue;

                var texName = obj.name;
                if (string.IsNullOrEmpty(texName)) continue;

                if (!bundleTexByName.TryGetValue(texName, out var bundleTex))
                    continue;

                // Don't overwrite the bundle-loaded texture with itself
                if (obj.GetInstanceID() == bundleTex.GetInstanceID())
                    continue;

                try
                {
                    var gameTex = obj.Cast<Texture2D>();
                    if (gameTex == null) continue;

                    Graphics.CopyTexture(bundleTex, gameTex);
                    replaced++;
                    MelonLogger.Msg($"  Replaced texture from bundle: {texName}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"  Failed to replace texture {texName} from bundle: {ex.Message}");
                }
            }

            return replaced;
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"ApplyBundleTextureReplacements failed: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// For bundle-loaded AudioClip assets, find matching game clips and swap data.
    /// </summary>
    private static int ApplyBundleAudioReplacements()
    {
        var bundleClips = BundleLoader.GetAssetsByType("AudioClip");
        if (bundleClips.Count == 0)
            return 0;

        try
        {
            var il2cppType = Il2CppType.From(typeof(AudioClip));
            var allClips = Resources.FindObjectsOfTypeAll(il2cppType);
            if (allClips == null || allClips.Length == 0)
                return 0;

            var bundleClipByName = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
            foreach (var bc in bundleClips)
            {
                try
                {
                    var clip = bc.Cast<AudioClip>();
                    if (clip != null && !string.IsNullOrEmpty(bc.name))
                        bundleClipByName[bc.name] = clip;
                }
                catch { }
            }

            int replaced = 0;
            for (int i = 0; i < allClips.Length; i++)
            {
                var obj = allClips[i];
                if (obj == null) continue;

                var clipName = obj.name;
                if (string.IsNullOrEmpty(clipName)) continue;

                if (!bundleClipByName.TryGetValue(clipName, out var bundleClip))
                    continue;

                if (obj.GetInstanceID() == bundleClip.GetInstanceID())
                    continue;

                try
                {
                    var gameClip = obj.Cast<AudioClip>();
                    if (gameClip == null) continue;

                    // Copy sample data from bundle clip to game clip
                    var samples = new float[bundleClip.samples * bundleClip.channels];
                    bundleClip.GetData(samples, 0);
                    gameClip.SetData(samples, 0);
                    replaced++;
                    MelonLogger.Msg($"  Replaced audio clip from bundle: {clipName}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"  Failed to replace audio clip {clipName} from bundle: {ex.Message}");
                }
            }

            return replaced;
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"ApplyBundleAudioReplacements failed: {ex.Message}");
            return 0;
        }
    }

    // ------------------------------------------------------------------
    // Mesh replacements (bundle-sourced: copy vertex/triangle/normal/UV data)
    // ------------------------------------------------------------------

    private static int ApplyBundleMeshReplacements()
    {
        var bundleMeshes = BundleLoader.GetAssetsByType("Mesh");
        if (bundleMeshes.Count == 0)
            return 0;

        try
        {
            var il2cppType = Il2CppType.From(typeof(Mesh));
            var allMeshes = Resources.FindObjectsOfTypeAll(il2cppType);
            if (allMeshes == null || allMeshes.Length == 0)
                return 0;

            var bundleMeshByName = new Dictionary<string, Mesh>(StringComparer.OrdinalIgnoreCase);
            foreach (var bm in bundleMeshes)
            {
                try
                {
                    var mesh = bm.Cast<Mesh>();
                    if (mesh != null && !string.IsNullOrEmpty(bm.name))
                        bundleMeshByName[bm.name] = mesh;
                }
                catch { }
            }

            int replaced = 0;
            for (int i = 0; i < allMeshes.Length; i++)
            {
                var obj = allMeshes[i];
                if (obj == null) continue;

                var meshName = obj.name;
                if (string.IsNullOrEmpty(meshName)) continue;

                if (!bundleMeshByName.TryGetValue(meshName, out var bundleMesh))
                    continue;

                if (obj.GetInstanceID() == bundleMesh.GetInstanceID())
                    continue;

                try
                {
                    var gameMesh = obj.Cast<Mesh>();
                    if (gameMesh == null) continue;

                    // Clear and copy all mesh data from the bundle mesh
                    gameMesh.Clear();
                    gameMesh.vertices = bundleMesh.vertices;
                    gameMesh.normals = bundleMesh.normals;
                    gameMesh.tangents = bundleMesh.tangents;
                    gameMesh.uv = bundleMesh.uv;
                    gameMesh.uv2 = bundleMesh.uv2;
                    gameMesh.colors32 = bundleMesh.colors32;
                    gameMesh.triangles = bundleMesh.triangles;
                    gameMesh.boneWeights = bundleMesh.boneWeights;
                    gameMesh.bindposes = bundleMesh.bindposes;

                    // Copy submeshes if the bundle mesh has multiple
                    if (bundleMesh.subMeshCount > 1)
                    {
                        gameMesh.subMeshCount = bundleMesh.subMeshCount;
                        for (int s = 0; s < bundleMesh.subMeshCount; s++)
                            gameMesh.SetSubMesh(s, bundleMesh.GetSubMesh(s));
                    }

                    gameMesh.RecalculateBounds();
                    replaced++;
                    MelonLogger.Msg($"  Replaced mesh from bundle: {meshName}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"  Failed to replace mesh {meshName} from bundle: {ex.Message}");
                }
            }

            return replaced;
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"ApplyBundleMeshReplacements failed: {ex.Message}");
            return 0;
        }
    }

    // ------------------------------------------------------------------
    // Material replacements (bundle-sourced: swap references on Renderers)
    // Materials can't be overwritten in-place — instead we find all
    // Renderers using the old material and swap to the bundle-loaded one.
    // ------------------------------------------------------------------

    private static int ApplyBundleMaterialReplacements()
    {
        var bundleMaterials = BundleLoader.GetAssetsByType("Material");
        if (bundleMaterials.Count == 0)
            return 0;

        try
        {
            var bundleMatByName = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            foreach (var bm in bundleMaterials)
            {
                try
                {
                    var mat = bm.Cast<Material>();
                    if (mat != null && !string.IsNullOrEmpty(bm.name))
                        bundleMatByName[bm.name] = mat;
                }
                catch { }
            }

            if (bundleMatByName.Count == 0)
                return 0;

            // Find all Renderers in the scene and swap matching materials
            var rendererType = Il2CppType.From(typeof(Renderer));
            var allRenderers = Resources.FindObjectsOfTypeAll(rendererType);
            if (allRenderers == null || allRenderers.Length == 0)
                return 0;

            int replaced = 0;
            for (int i = 0; i < allRenderers.Length; i++)
            {
                var obj = allRenderers[i];
                if (obj == null) continue;

                try
                {
                    var renderer = obj.Cast<Renderer>();
                    if (renderer == null) continue;

                    var materials = renderer.sharedMaterials;
                    if (materials == null || materials.Length == 0) continue;

                    bool changed = false;
                    for (int m = 0; m < materials.Length; m++)
                    {
                        var mat = materials[m];
                        if (mat == null) continue;

                        if (bundleMatByName.TryGetValue(mat.name, out var bundleMat))
                        {
                            if (mat.GetInstanceID() != bundleMat.GetInstanceID())
                            {
                                materials[m] = bundleMat;
                                changed = true;
                            }
                        }
                    }

                    if (changed)
                    {
                        renderer.sharedMaterials = materials;
                        replaced++;
                        MelonLogger.Msg($"  Swapped material(s) on renderer: {renderer.name}");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"  Failed to swap materials on renderer: {ex.Message}");
                }
            }

            return replaced;
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"ApplyBundleMaterialReplacements failed: {ex.Message}");
            return 0;
        }
    }

    // ------------------------------------------------------------------
    // Prefab / GameObject replacements (bundle-sourced: full hierarchy swap)
    // GLB/FBX imports arrive in bundles as GameObjects with a full child
    // hierarchy (MeshFilter, Renderer, bones, Animator, etc.). We find
    // matching game objects by name and copy the hierarchy across.
    // ------------------------------------------------------------------

    private static int ApplyBundlePrefabReplacements()
    {
        var bundlePrefabs = BundleLoader.GetAssetsByType("GameObject");
        if (bundlePrefabs.Count == 0)
            return 0;

        try
        {
            var goType = Il2CppType.From(typeof(GameObject));
            var allGameObjects = Resources.FindObjectsOfTypeAll(goType);
            if (allGameObjects == null || allGameObjects.Length == 0)
                return 0;

            var bundlePrefabByName = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            var bundleInstanceIds = new HashSet<int>();
            foreach (var bp in bundlePrefabs)
            {
                try
                {
                    var go = bp.Cast<GameObject>();
                    if (go != null && !string.IsNullOrEmpty(bp.name))
                    {
                        bundlePrefabByName[bp.name] = go;
                        bundleInstanceIds.Add(bp.GetInstanceID());
                        // Also track all children so we don't try to replace them
                        foreach (var childTransform in go.GetComponentsInChildren<Transform>(true))
                        {
                            if (childTransform != null && childTransform.gameObject != null)
                                bundleInstanceIds.Add(childTransform.gameObject.GetInstanceID());
                        }
                    }
                }
                catch { }
            }

            if (bundlePrefabByName.Count == 0)
                return 0;

            int replaced = 0;
            for (int i = 0; i < allGameObjects.Length; i++)
            {
                var obj = allGameObjects[i];
                if (obj == null) continue;

                // Skip bundle-loaded objects
                if (bundleInstanceIds.Contains(obj.GetInstanceID()))
                    continue;

                var goName = obj.name;
                if (string.IsNullOrEmpty(goName)) continue;

                if (!bundlePrefabByName.TryGetValue(goName, out var bundlePrefab))
                    continue;

                try
                {
                    var gameGO = obj.Cast<GameObject>();
                    if (gameGO == null) continue;

                    CopyPrefabComponents(bundlePrefab, gameGO);
                    replaced++;
                    MelonLogger.Msg($"  Replaced prefab from bundle: {goName}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"  Failed to replace prefab {goName} from bundle: {ex.Message}");
                }
            }

            return replaced;
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"ApplyBundlePrefabReplacements failed: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Copy key 3D components from a bundle-loaded prefab to a game object.
    /// Handles MeshFilter, Renderer materials, SkinnedMeshRenderer, and
    /// recurses into matching children by name.
    /// </summary>
    private static void CopyPrefabComponents(GameObject source, GameObject target)
    {
        // MeshFilter → swap shared mesh
        var srcMF = source.GetComponent<MeshFilter>();
        var tgtMF = target.GetComponent<MeshFilter>();
        if (srcMF != null && tgtMF != null && srcMF.sharedMesh != null)
        {
            tgtMF.sharedMesh = srcMF.sharedMesh;
        }

        // MeshRenderer → swap materials
        var srcMR = source.GetComponent<MeshRenderer>();
        var tgtMR = target.GetComponent<MeshRenderer>();
        if (srcMR != null && tgtMR != null)
        {
            tgtMR.sharedMaterials = srcMR.sharedMaterials;
        }

        // SkinnedMeshRenderer → swap mesh, materials, and bones
        var srcSMR = source.GetComponent<SkinnedMeshRenderer>();
        var tgtSMR = target.GetComponent<SkinnedMeshRenderer>();
        if (srcSMR != null && tgtSMR != null)
        {
            if (srcSMR.sharedMesh != null)
                tgtSMR.sharedMesh = srcSMR.sharedMesh;
            tgtSMR.sharedMaterials = srcSMR.sharedMaterials;
        }

        // Recurse into children, matching by name
        var srcTransform = source.transform;
        var tgtTransform = target.transform;
        for (int c = 0; c < srcTransform.childCount; c++)
        {
            var srcChild = srcTransform.GetChild(c);
            if (srcChild == null) continue;

            // Find matching child in target by name
            var tgtChild = tgtTransform.Find(srcChild.name);
            if (tgtChild != null)
            {
                CopyPrefabComponents(srcChild.gameObject, tgtChild.gameObject);
            }
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static AssetKind InferKind(string extension)
    {
        return extension switch
        {
            ".png" or ".jpg" or ".jpeg" or ".tga" or ".bmp" => AssetKind.Texture,
            ".wav" or ".ogg" or ".mp3" => AssetKind.Audio,
            ".glb" or ".gltf" or ".fbx" or ".obj" => AssetKind.Model,
            ".mat" => AssetKind.Material,
            _ => AssetKind.Unknown
        };
    }

    private static byte[] GetOrLoadBytes(string diskPath)
    {
        if (_bytesCache.TryGetValue(diskPath, out var cached))
            return cached;

        if (!File.Exists(diskPath))
            return null;

        try
        {
            var bytes = File.ReadAllBytes(diskPath);
            _bytesCache[diskPath] = bytes;
            return bytes;
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Failed to read {diskPath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Load a Texture2D from a file on disk. Utility for plugins that need to
    /// load textures outside the normal replacement pipeline.
    /// </summary>
    public static Texture2D LoadTextureFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var texture = new Texture2D(2, 2);
            var il2cppBytes = new Il2CppStructArray<byte>(bytes);
            if (!ImageConversion.LoadImage(texture, il2cppBytes))
            {
                MelonLogger.Warning($"ImageConversion.LoadImage failed for: {filePath}");
                return null;
            }
            return texture;
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Failed to load texture from {filePath}: {ex.Message}");
            return null;
        }
    }
}

// Keep the old name as an alias so ModpackLoaderMod doesn't break
// while we transition. Will be removed once all references are updated.
public static class AssetInjectionPatches
{
    public static int RegisteredCount => AssetReplacer.RegisteredCount;

    public static void RegisterAssetReplacement(string assetPath, string diskFilePath)
        => AssetReplacer.RegisterAssetReplacement(assetPath, diskFilePath);

    public static bool HasReplacement(string assetPath)
        => AssetReplacer.HasReplacement(assetPath);

    public static void ApplyAllReplacements()
        => AssetReplacer.ApplyAllReplacements();

    public static Texture2D LoadTextureFromFile(string filePath)
        => AssetReplacer.LoadTextureFromFile(filePath);
}
