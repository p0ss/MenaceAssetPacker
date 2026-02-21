using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Menace.Modkit.Core.Bundles;

/// <summary>
/// Compiles merged data patches and clones into Unity asset bundles.
/// Takes base game assets + merged patches/clones → produces .bundle files
/// that the runtime loads via AssetBundle.LoadFromFile().
/// </summary>
public class BundleCompiler
{
    /// <summary>
    /// Result of a bundle compilation operation.
    /// </summary>
    public class BundleCompileResult
    {
        public bool Success { get; set; }
        public string? OutputPath { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new();
        public int ClonesCreated { get; set; }
        public int PatchesApplied { get; set; }
        public int AudioClipsCreated { get; set; }
        public int TexturesCreated { get; set; }
        public int SpritesCreated { get; set; }
        public int ModelsCreated { get; set; }
        public int PrefabsCreated { get; set; }
    }

    /// <summary>
    /// Texture entry for native texture/sprite creation.
    /// </summary>
    public class TextureEntry
    {
        /// <summary>Name of the texture asset (without extension).</summary>
        public string AssetName { get; set; } = string.Empty;
        /// <summary>Full path to the source PNG file.</summary>
        public string SourceFilePath { get; set; } = string.Empty;
        /// <summary>Resource path for ResourceManager (e.g., "assets/textures/my_icon").</summary>
        public string ResourcePath { get; set; } = string.Empty;
        /// <summary>Whether to also create a Sprite asset referencing the texture.</summary>
        public bool CreateSprite { get; set; } = true;
        /// <summary>Pixels per unit for the sprite (default 100).</summary>
        public float PixelsPerUnit { get; set; } = 100f;
    }

    /// <summary>
    /// Model entry for native Mesh/Prefab creation from GLB/GLTF files.
    /// </summary>
    public class ModelEntry
    {
        /// <summary>Name of the model asset (without extension).</summary>
        public string AssetName { get; set; } = string.Empty;
        /// <summary>Full path to the source GLB/GLTF file.</summary>
        public string SourceFilePath { get; set; } = string.Empty;
        /// <summary>Resource path for ResourceManager (e.g., "assets/models/my_model").</summary>
        public string ResourcePath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Compile a merged patch set into an asset bundle (legacy signature for compatibility).
    /// </summary>
    public async Task<BundleCompileResult> CompileDataPatchBundleAsync(
        MergedPatchSet mergedPatches,
        string gameDataPath,
        string unityVersion,
        string outputPath,
        CancellationToken ct = default)
    {
        return await CompileDataPatchBundleAsync(mergedPatches, null, gameDataPath, unityVersion, outputPath, ct);
    }

    /// <summary>
    /// Compile merged patches and clones into an asset bundle.
    /// This reads base game assets, creates clones, applies patches, and writes a new bundle.
    /// </summary>
    /// <param name="mergedPatches">The merged patches from all active modpacks.</param>
    /// <param name="mergedClones">The merged clone definitions from all active modpacks.</param>
    /// <param name="gameDataPath">Path to the game's data directory (contains level files, resources.assets, etc.)</param>
    /// <param name="unityVersion">The Unity version string (e.g. "2020.3.18f1").</param>
    /// <param name="outputPath">Path to write the output .bundle file.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<BundleCompileResult> CompileDataPatchBundleAsync(
        MergedPatchSet mergedPatches,
        MergedCloneSet? mergedClones,
        string gameDataPath,
        string unityVersion,
        string outputPath,
        CancellationToken ct = default)
    {
        return await CompileDataPatchBundleAsync(
            mergedPatches, mergedClones, null, gameDataPath, unityVersion, outputPath, ct);
    }

    /// <summary>
    /// Compile merged patches, clones, and audio assets into an asset bundle.
    /// This reads base game assets, creates clones and audio clips, applies patches, and writes a new bundle.
    /// </summary>
    /// <param name="mergedPatches">The merged patches from all active modpacks.</param>
    /// <param name="mergedClones">The merged clone definitions from all active modpacks.</param>
    /// <param name="audioEntries">Audio files to convert to native AudioClip assets.</param>
    /// <param name="gameDataPath">Path to the game's data directory (contains level files, resources.assets, etc.)</param>
    /// <param name="unityVersion">The Unity version string (e.g. "2020.3.18f1").</param>
    /// <param name="outputPath">Path to write the output .bundle file.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<BundleCompileResult> CompileDataPatchBundleAsync(
        MergedPatchSet mergedPatches,
        MergedCloneSet? mergedClones,
        List<AudioBundler.AudioEntry>? audioEntries,
        string gameDataPath,
        string unityVersion,
        string outputPath,
        CancellationToken ct = default)
    {
        return await CompileDataPatchBundleAsync(
            mergedPatches, mergedClones, audioEntries, null, null, gameDataPath, unityVersion, outputPath, ct);
    }

    /// <summary>
    /// Compile merged patches, clones, audio assets, textures, and models into an asset bundle.
    /// This is the main compilation entry point that handles all asset types.
    /// </summary>
    /// <param name="mergedPatches">The merged patches from all active modpacks.</param>
    /// <param name="mergedClones">The merged clone definitions from all active modpacks.</param>
    /// <param name="audioEntries">Audio files to convert to native AudioClip assets.</param>
    /// <param name="textureEntries">PNG files to convert to native Texture2D/Sprite assets.</param>
    /// <param name="modelEntries">GLB/GLTF files to convert to native Mesh/Prefab assets.</param>
    /// <param name="gameDataPath">Path to the game's data directory (contains level files, resources.assets, etc.)</param>
    /// <param name="unityVersion">The Unity version string (e.g. "2020.3.18f1").</param>
    /// <param name="outputPath">Path to write the output .bundle file.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<BundleCompileResult> CompileDataPatchBundleAsync(
        MergedPatchSet mergedPatches,
        MergedCloneSet? mergedClones,
        List<AudioBundler.AudioEntry>? audioEntries,
        List<TextureEntry>? textureEntries,
        List<ModelEntry>? modelEntries,
        string gameDataPath,
        string unityVersion,
        string outputPath,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                return CompileDataPatchBundleCore(mergedPatches, mergedClones, audioEntries, textureEntries, modelEntries, gameDataPath, unityVersion, outputPath);
            }
            catch (Exception ex)
            {
                return new BundleCompileResult
                {
                    Success = false,
                    Message = $"Bundle compilation failed: {ex.Message}"
                };
            }
        }, ct);
    }

    private BundleCompileResult CompileDataPatchBundleCore(
        MergedPatchSet mergedPatches,
        MergedCloneSet? mergedClones,
        List<AudioBundler.AudioEntry>? audioEntries,
        List<TextureEntry>? textureEntries,
        List<ModelEntry>? modelEntries,
        string gameDataPath,
        string unityVersion,
        string outputPath)
    {
        var result = new BundleCompileResult();

        bool hasPatches = mergedPatches.Patches.Count > 0;
        bool hasClones = mergedClones?.HasClones == true;
        bool hasAudio = audioEntries?.Count > 0;
        bool hasTextures = textureEntries?.Count > 0;
        bool hasModels = modelEntries?.Count > 0;

        if (!hasPatches && !hasClones && !hasAudio && !hasTextures && !hasModels)
        {
            result.Success = false;
            result.Message = "No patches, clones, audio, texture, or model assets to compile";
            return result;
        }

        var am = new AssetsManager();

        try
        {
            // Find the game's main data file(s)
            var dataFiles = FindGameDataFiles(gameDataPath);
            if (dataFiles.Count == 0)
            {
                result.Success = false;
                result.Message = $"No game data files found in {gameDataPath}";
                return result;
            }

            // Load the primary assets file (resources.assets preferred)
            var primaryDataFile = dataFiles.FirstOrDefault(f => f.EndsWith("resources.assets")) ?? dataFiles[0];
            var primaryFileInst = am.LoadAssetsFile(primaryDataFile, false);
            var afile = primaryFileInst.file;

            // Track next available PathID for new assets
            long nextPathId = afile.AssetInfos.Count > 0
                ? afile.AssetInfos.Max(i => i.PathId) + 1
                : 1;

            // Build lookup of existing assets by ID for cloning
            // Uses raw byte scanning (Unity 6 doesn't embed type trees)
            var assetsByName = BuildAssetNameLookup(afile);
            result.Warnings.Add($"Asset lookup indexed {assetsByName.Count} template assets from {primaryDataFile}");

            // Load globalgamemanagers to get ResourceManager path mappings
            var ggmPath = Path.Combine(Path.GetDirectoryName(primaryDataFile)!, "globalgamemanagers");
            var resourcePathLookup = new Dictionary<string, string>(); // templateName -> resourcePath
            var originalResourceRefs = new Dictionary<string, (int FileId, long PathId)>(StringComparer.OrdinalIgnoreCase); // resourcePath -> original (FileId, PathId)
            byte[]? ggmBytes = null;

            if (File.Exists(ggmPath) && (hasClones || hasAudio || hasTextures || hasModels))
            {
                try
                {
                    var ggmInst = am.LoadAssetsFile(ggmPath, false);
                    var ggmFile = ggmInst.file;

                    // Parse ResourceManager to build path lookups
                    foreach (var info in ggmFile.AssetInfos)
                    {
                        if (info.TypeId == 147) // ResourceManager
                        {
                            var absOffset = info.GetAbsoluteByteOffset(ggmFile);
                            ggmFile.Reader.BaseStream.Position = absOffset;
                            var rmBytes = ggmFile.Reader.ReadBytes((int)info.ByteSize);

                            (resourcePathLookup, originalResourceRefs) = ParseResourceManager(rmBytes, assetsByName);
                            result.Warnings.Add($"ResourceManager indexed {resourcePathLookup.Count} template paths");
                            break;
                        }
                    }

                    // Read the full globalgamemanagers file for later patching
                    ggmBytes = File.ReadAllBytes(ggmPath);
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Failed to parse globalgamemanagers: {ex.Message}");
                }
            }

            // ===== PHASE 1: PROCESS CLONES =====
            var clonedAssets = new Dictionary<string, AssetFileInfo>();
            var cloneSourceMap = new Dictionary<string, string>(); // cloneName -> sourceName

            if (hasClones)
            {
                var cloneResult = ProcessClones(
                    afile, mergedClones!, assetsByName, clonedAssets, cloneSourceMap, ref nextPathId);

                result.ClonesCreated = clonedAssets.Count;
                result.Warnings.AddRange(cloneResult.Warnings);

                if (!cloneResult.Success && clonedAssets.Count == 0)
                {
                    // Only fail if no clones were created and there are no patches
                    if (!hasPatches)
                    {
                        result.Success = false;
                        result.Message = "Clone processing failed and no patches to apply";
                        return result;
                    }
                }
            }

            // ===== PHASE 2: PROCESS PATCHES =====
            // NOTE: Raw byte cloning doesn't support field-level patching.
            // Patches for cloned assets should be applied at runtime via the injection system.
            // For now, we just track which patches couldn't be applied.
            int totalPatched = 0;

            if (hasPatches)
            {
                foreach (var templateType in mergedPatches.GetTemplateTypes())
                {
                    var instances = mergedPatches.Patches[templateType];

                    foreach (var (assetName, fieldPatches) in instances)
                    {
                        // Check if this is a cloned asset we just created
                        if (clonedAssets.ContainsKey(assetName))
                        {
                            // Clones will have patches applied at runtime
                            // (raw byte cloning doesn't support field-level patching)
                            continue;
                        }

                        // For non-clone assets, we can't patch without type trees
                        // The runtime injection system will handle these
                        if (!assetsByName.ContainsKey(assetName))
                        {
                            result.Warnings.Add($"Patch target '{assetName}' not found");
                        }
                    }
                }
            }

            result.PatchesApplied = totalPatched;

            // ===== PHASE 2.5: PROCESS AUDIO ASSETS =====
            var audioAssets = new Dictionary<string, AssetFileInfo>();
            var audioResourcePaths = new Dictionary<string, string>(); // assetName -> resourcePath

            if (hasAudio)
            {
                var audioResult = ProcessAudioAssets(
                    afile, audioEntries!, audioAssets, audioResourcePaths, ref nextPathId);

                result.AudioClipsCreated = audioAssets.Count;
                result.Warnings.AddRange(audioResult.Warnings);

                if (audioAssets.Count > 0)
                {
                    result.Warnings.Add($"Created {audioAssets.Count} native AudioClip asset(s)");
                }
            }

            // ===== PHASE 2.6: PROCESS TEXTURE ASSETS =====
            var textureAssets = new Dictionary<string, AssetFileInfo>();
            var spriteAssets = new Dictionary<string, AssetFileInfo>();
            var textureResourcePaths = new Dictionary<string, string>(); // assetName -> resourcePath
            var spriteResourcePaths = new Dictionary<string, string>(); // assetName -> resourcePath

            if (hasTextures)
            {
                var textureResult = ProcessTextureAssets(
                    afile, am, gameDataPath, textureEntries!, textureAssets, spriteAssets,
                    textureResourcePaths, spriteResourcePaths, originalResourceRefs, ref nextPathId);

                result.TexturesCreated = textureAssets.Count;
                result.SpritesCreated = spriteAssets.Count;
                result.Warnings.AddRange(textureResult.Warnings);

                if (textureAssets.Count > 0)
                {
                    result.Warnings.Add($"Created {textureAssets.Count} native Texture2D asset(s)");
                }
                if (spriteAssets.Count > 0)
                {
                    result.Warnings.Add($"Created {spriteAssets.Count} native Sprite asset(s)");
                }
            }

            // ===== PHASE 2.7: PROCESS MODEL ASSETS =====
            var modelAssets = new Dictionary<string, AssetFileInfo>(); // meshName -> AssetFileInfo
            var prefabAssets = new Dictionary<string, AssetFileInfo>(); // prefabName -> AssetFileInfo
            var modelResourcePaths = new Dictionary<string, string>(); // assetName -> resourcePath
            var prefabResourcePaths = new Dictionary<string, string>(); // prefabName -> resourcePath

            if (hasModels)
            {
                foreach (var entry in modelEntries!)
                {
                    try
                    {
                        var glbResult = GlbBundler.ConvertToNativeAssets(
                            entry.SourceFilePath, am, primaryFileInst, ref nextPathId, unityVersion);

                        if (glbResult.Success)
                        {
                            // Track created meshes
                            foreach (var meshInfo in glbResult.CreatedMeshes)
                            {
                                var meshAssetInfo = afile.AssetInfos.FirstOrDefault(a => a.PathId == meshInfo.PathId);
                                if (meshAssetInfo != null)
                                {
                                    modelAssets[meshInfo.Name] = meshAssetInfo;
                                    modelResourcePaths[meshInfo.Name] = !string.IsNullOrEmpty(entry.ResourcePath)
                                        ? $"{entry.ResourcePath}/{meshInfo.Name}".ToLowerInvariant()
                                        : $"assets/models/{entry.AssetName}/{meshInfo.Name}".ToLowerInvariant();
                                }
                            }

                            // Track created prefabs
                            foreach (var prefabInfo in glbResult.CreatedPrefabs)
                            {
                                var prefabAssetInfo = afile.AssetInfos.FirstOrDefault(a => a.PathId == prefabInfo.GameObjectPathId);
                                if (prefabAssetInfo != null)
                                {
                                    prefabAssets[prefabInfo.Name] = prefabAssetInfo;
                                    prefabResourcePaths[prefabInfo.Name] = !string.IsNullOrEmpty(prefabInfo.ResourcePath)
                                        ? prefabInfo.ResourcePath
                                        : $"assets/prefabs/{entry.AssetName}/{prefabInfo.Name}".ToLowerInvariant();
                                }
                            }

                            result.Warnings.AddRange(glbResult.Warnings);
                            result.Warnings.Add($"Model '{entry.AssetName}': {glbResult.Message}");
                        }
                        else
                        {
                            result.Warnings.Add($"Model '{entry.AssetName}' failed: {glbResult.ErrorMessage}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Model '{entry.AssetName}' exception: {ex.Message}");
                    }
                }

                result.ModelsCreated = modelAssets.Count;
                result.PrefabsCreated = prefabAssets.Count;

                if (modelAssets.Count > 0)
                {
                    result.Warnings.Add($"Created {modelAssets.Count} native Mesh asset(s)");
                }
                if (prefabAssets.Count > 0)
                {
                    result.Warnings.Add($"Created {prefabAssets.Count} native Prefab asset(s)");
                }
            }

            // Check if we have any work to write
            if (result.ClonesCreated == 0 && totalPatched == 0 && result.AudioClipsCreated == 0 && result.TexturesCreated == 0 && result.ModelsCreated == 0)
            {
                result.Success = false;
                result.Message = "No assets were modified. Check warnings for details.";
                return result;
            }

            // ===== PHASE 3: WRITE OUTPUT =====
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            bool bundleWritten = false;
            byte[]? serializedBytes = null;

            try
            {
                using var ms = new MemoryStream();
                using (var writer = new AssetsFileWriter(ms))
                {
                    afile.Write(writer);
                }

                serializedBytes = ms.ToArray();

                // Write raw assets file for manual testing/replacement
                // This can be copied over resources.assets to test if cloning works
                var rawAssetsPath = Path.Combine(dir!, "resources.assets.patched");
                File.WriteAllBytes(rawAssetsPath, serializedBytes);
                result.Warnings.Add($"Raw assets file written: {rawAssetsPath} ({serializedBytes.Length / 1024 / 1024}MB)");

                // Patch globalgamemanagers ResourceManager with new entries (clones + audio + textures + models)
                if (ggmBytes != null && (clonedAssets.Count > 0 || audioAssets.Count > 0 || textureAssets.Count > 0 || spriteAssets.Count > 0 || modelAssets.Count > 0 || prefabAssets.Count > 0))
                {
                    try
                    {
                        var (patchedGgm, rmDebugInfo) = PatchResourceManager(
                            ggmBytes,
                            clonedAssets,
                            cloneSourceMap,
                            resourcePathLookup,
                            audioAssets,
                            audioResourcePaths,
                            textureAssets,
                            textureResourcePaths,
                            spriteAssets,
                            spriteResourcePaths,
                            modelAssets,
                            modelResourcePaths,
                            prefabAssets,
                            prefabResourcePaths);

                        if (!string.IsNullOrEmpty(rmDebugInfo))
                        {
                            result.Warnings.Add($"[ResourceManager Debug] {rmDebugInfo}");
                        }

                        if (patchedGgm != null)
                        {
                            var ggmPatchedPath = Path.Combine(dir!, "globalgamemanagers.patched");
                            File.WriteAllBytes(ggmPatchedPath, patchedGgm);
                            var totalEntries = clonedAssets.Count + audioAssets.Count + textureAssets.Count + spriteAssets.Count + modelAssets.Count + prefabAssets.Count;
                            result.Warnings.Add($"GlobalGameManagers patched with {totalEntries} ResourceManager entries ({clonedAssets.Count} clones, {audioAssets.Count} audio, {textureAssets.Count} textures, {spriteAssets.Count} sprites, {modelAssets.Count} models, {prefabAssets.Count} prefabs): {ggmPatchedPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Failed to patch globalgamemanagers: {ex.Message}");
                    }
                }

                // Also try to write UnityFS bundle (may fail to load due to IL2CPP issues)
                bundleWritten = BundleWriter.WriteBundle(serializedBytes, outputPath, unityVersion);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Asset writing failed: {ex.Message}");
            }

            if (bundleWritten || serializedBytes != null)
            {
                result.Success = true;
                result.OutputPath = outputPath;
                var parts = new List<string>();
                if (result.ClonesCreated > 0) parts.Add($"{result.ClonesCreated} clone(s)");
                if (result.PatchesApplied > 0) parts.Add($"{result.PatchesApplied} patch(es)");
                if (result.AudioClipsCreated > 0) parts.Add($"{result.AudioClipsCreated} audio clip(s)");
                if (result.TexturesCreated > 0) parts.Add($"{result.TexturesCreated} texture(s)");
                if (result.SpritesCreated > 0) parts.Add($"{result.SpritesCreated} sprite(s)");
                if (result.ModelsCreated > 0) parts.Add($"{result.ModelsCreated} model(s)");
                if (result.PrefabsCreated > 0) parts.Add($"{result.PrefabsCreated} prefab(s)");
                var format = bundleWritten ? "UnityFS bundle + raw assets" : "raw assets only";
                result.Message = $"Compiled {string.Join(" and ", parts)} ({format})";
            }
            else
            {
                // Fallback: write individual .asset files + JSON manifest
                result.Warnings.Add("Falling back to JSON manifest + individual asset files");
                WriteFallbackAssets(clonedAssets, assetsByName, dir!, outputPath);
                result.Success = true;
                result.OutputPath = outputPath;
                result.Message = $"Compiled assets (JSON manifest fallback)";
            }
        }
        finally
        {
            am.UnloadAll();
        }

        return result;
    }

    /// <summary>
    /// Build a lookup dictionary of all MonoBehaviour assets by their m_ID field.
    /// Uses raw binary scanning since Unity 6 doesn't embed type trees.
    /// Dynamically searches for template ID pattern (xxx.yyy format) to be robust
    /// against game updates that might change field offsets.
    /// </summary>
    private static Dictionary<string, (AssetFileInfo info, byte[] bytes, int idOffset)> BuildAssetNameLookup(
        AssetsFile afile)
    {
        var lookup = new Dictionary<string, (AssetFileInfo, byte[], int)>();
        var reader = afile.Reader;

        foreach (var info in afile.GetAssetsOfType(AssetClassID.MonoBehaviour))
        {
            try
            {
                var absOffset = info.GetAbsoluteByteOffset(afile);
                reader.BaseStream.Position = absOffset;
                var bytes = reader.ReadBytes((int)info.ByteSize);

                // Search for template ID pattern in first 200 bytes
                // Template IDs are length-prefixed strings with format "category.name"
                var (id, idOffset) = FindTemplateId(bytes, maxSearchOffset: 200);
                if (id != null && idOffset >= 0 && !lookup.ContainsKey(id))
                {
                    lookup[id] = (info, bytes, idOffset);
                }
            }
            catch
            {
                // Skip assets that fail to read
            }
        }

        return lookup;
    }

    /// <summary>
    /// Search for a template ID string in asset bytes.
    /// Template IDs have format "category.name" (e.g., "weapon.laser_rifle").
    /// Returns the ID and its offset, or (null, -1) if not found.
    /// </summary>
    private static (string? id, int offset) FindTemplateId(byte[] bytes, int maxSearchOffset)
    {
        for (int offset = 16; offset < Math.Min(maxSearchOffset, bytes.Length - 8); offset++)
        {
            // Read potential string length
            if (offset + 4 > bytes.Length) break;
            int len = BitConverter.ToInt32(bytes, offset);

            // Validate length (template IDs are typically 10-60 chars)
            if (len < 5 || len > 100 || offset + 4 + len > bytes.Length)
                continue;

            // Check if it matches template ID pattern: prefix.name
            // - prefix: 2-20 lowercase letters/underscores (e.g., "weapon", "squad_leader")
            // - separator: exactly one dot
            // - name: lowercase letters, numbers, underscores (can contain more dots)
            int dotPos = -1;
            bool validPrefix = true;
            bool validName = true;

            // Find first dot and validate prefix (characters before it)
            for (int i = 0; i < len; i++)
            {
                byte b = bytes[offset + 4 + i];
                if (b == '.')
                {
                    dotPos = i;
                    break;
                }
                // Prefix must be lowercase letters or underscore
                if (!((b >= 'a' && b <= 'z') || b == '_'))
                {
                    validPrefix = false;
                    break;
                }
            }

            // Prefix must be 2-20 chars and valid
            if (!validPrefix || dotPos < 2 || dotPos > 20)
                continue;

            // Validate name portion (after first dot)
            for (int i = dotPos + 1; i < len; i++)
            {
                byte b = bytes[offset + 4 + i];
                // Name can have lowercase letters, numbers, underscores, and dots
                if (!((b >= 'a' && b <= 'z') || (b >= '0' && b <= '9') || b == '_' || b == '.'))
                {
                    validName = false;
                    break;
                }
            }

            // Name must have at least 1 char after the dot
            if (!validName || len - dotPos - 1 < 1)
                continue;

            var str = System.Text.Encoding.ASCII.GetString(bytes, offset + 4, len);
            return (str, offset);
        }

        return (null, -1);
    }

    /// <summary>
    /// Result of clone processing.
    /// </summary>
    private class CloneProcessResult
    {
        public bool Success { get; set; } = true;
        public List<string> Warnings { get; } = new();
    }

    /// <summary>
    /// Process all clone definitions, creating new native assets using raw byte cloning.
    /// </summary>
    private CloneProcessResult ProcessClones(
        AssetsFile afile,
        MergedCloneSet mergedClones,
        Dictionary<string, (AssetFileInfo info, byte[] bytes, int idOffset)> assetsByName,
        Dictionary<string, AssetFileInfo> clonedAssets,
        Dictionary<string, string> cloneSourceMap,
        ref long nextPathId)
    {
        var result = new CloneProcessResult();

        foreach (var (templateType, cloneMap) in mergedClones.Clones)
        {
            foreach (var (newName, sourceName) in cloneMap)
            {
                // Skip if clone already exists
                if (assetsByName.ContainsKey(newName))
                {
                    result.Warnings.Add($"Clone '{newName}' skipped: asset already exists");
                    continue;
                }

                // Find source asset
                if (!assetsByName.TryGetValue(sourceName, out var source))
                {
                    result.Warnings.Add($"Clone '{newName}' failed: source '{sourceName}' not found");
                    result.Success = false;
                    continue;
                }

                try
                {
                    // Clone using raw byte patching at the dynamically found offset
                    var cloneBytes = CloneWithNewId(source.bytes, newName, source.idOffset);
                    if (cloneBytes == null)
                    {
                        result.Warnings.Add($"Clone '{newName}' failed: could not patch m_ID");
                        result.Success = false;
                        continue;
                    }

                    // Create new asset info with same type as source
                    var pathId = nextPathId++;
                    var newInfo = AssetFileInfo.Create(
                        afile,
                        pathId,
                        source.info.TypeId,
                        source.info.ScriptTypeIndex
                    );

                    // Unity 6 files without type trees return null from Create
                    // In that case, manually create the AssetFileInfo from the source template
                    if (newInfo == null)
                    {
                        newInfo = new AssetFileInfo
                        {
                            PathId = pathId,
                            TypeIdOrIndex = source.info.TypeIdOrIndex,
                            TypeId = source.info.TypeId,
                            ScriptTypeIndex = source.info.ScriptTypeIndex,
                            Stripped = source.info.Stripped
                        };
                    }

                    // Register the clone in the assets file
                    newInfo.SetNewData(cloneBytes);
                    afile.Metadata.AddAssetInfo(newInfo);

                    // Track for later reference (use same offset as source)
                    clonedAssets[newName] = newInfo;
                    cloneSourceMap[newName] = sourceName;
                    assetsByName[newName] = (newInfo, cloneBytes, source.idOffset);
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Clone '{newName}' failed: {ex.Message}");
                    result.Success = false;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Process audio files and create native AudioClip assets.
    /// </summary>
    private AudioProcessResult ProcessAudioAssets(
        AssetsFile afile,
        List<AudioBundler.AudioEntry> audioEntries,
        Dictionary<string, AssetFileInfo> audioAssets,
        Dictionary<string, string> audioResourcePaths,
        ref long nextPathId)
    {
        var result = new AudioProcessResult();

        // Find an existing AudioClip to use as template
        var template = AudioAssetCreator.FindAudioClipTemplate(afile);
        if (template == null)
        {
            result.Warnings.Add("No existing AudioClip found to use as template - cannot create audio assets");
            result.Success = false;
            return result;
        }

        var (templateBytes, templateInfo) = template.Value;

        foreach (var entry in audioEntries)
        {
            try
            {
                var audioResult = AudioAssetCreator.CreateAudioClip(
                    afile,
                    entry.SourceFilePath,
                    entry.AssetName,
                    nextPathId++,
                    templateBytes,
                    templateInfo);

                if (audioResult.Success)
                {
                    // Find the created asset info
                    var newInfo = afile.AssetInfos.FirstOrDefault(i => i.PathId == audioResult.PathId);
                    if (newInfo != null)
                    {
                        audioAssets[entry.AssetName] = newInfo;
                        if (!string.IsNullOrEmpty(entry.ResourcePath))
                        {
                            audioResourcePaths[entry.AssetName] = entry.ResourcePath;
                        }
                    }
                }
                else
                {
                    result.Warnings.Add($"Audio '{entry.AssetName}' failed: {audioResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Audio '{entry.AssetName}' failed: {ex.Message}");
            }
        }

        result.Success = audioAssets.Count > 0;
        return result;
    }

    /// <summary>
    /// Result of audio processing.
    /// </summary>
    private class AudioProcessResult
    {
        public bool Success { get; set; } = true;
        public List<string> Warnings { get; } = new();
    }

    /// <summary>
    /// Result of texture processing.
    /// </summary>
    private class TextureProcessResult
    {
        public bool Success { get; set; } = true;
        public List<string> Warnings { get; } = new();
    }

    /// <summary>
    /// Process PNG files and create native Texture2D/Sprite assets.
    /// For textures that exist in originalResourceRefs, replace in-place.
    /// For new textures, create with new PathId.
    /// </summary>
    private TextureProcessResult ProcessTextureAssets(
        AssetsFile afile,
        AssetsManager am,
        string gameDataPath,
        List<TextureEntry> textureEntries,
        Dictionary<string, AssetFileInfo> textureAssets,
        Dictionary<string, AssetFileInfo> spriteAssets,
        Dictionary<string, string> textureResourcePaths,
        Dictionary<string, string> spriteResourcePaths,
        Dictionary<string, (int FileId, long PathId)> originalResourceRefs,
        ref long nextPathId)
    {
        var result = new TextureProcessResult();

        // Find an existing Texture2D to use as template
        // First try resources.assets, then search sharedassets files
        var textureTemplate = NativeTextureCreator.FindTemplate(afile, out var resDiag);
        result.Warnings.Add($"[resources.assets] {resDiag.Replace("\n", " | ").Trim()}");

        if (textureTemplate == null)
        {
            result.Warnings.Add("No Texture2D in resources.assets, searching sharedassets files...");

            // Search sharedassets files for a texture template
            var dataDir = Path.GetDirectoryName(gameDataPath) ?? gameDataPath;
            var sharedAssetsFiles = Directory.GetFiles(dataDir, "sharedassets*.assets")
                .OrderBy(f => f)
                .ToList();

            foreach (var sharedFile in sharedAssetsFiles)
            {
                try
                {
                    var sharedInst = am.LoadAssetsFile(sharedFile, false);
                    textureTemplate = NativeTextureCreator.FindTemplate(sharedInst.file, out var sharedDiag);
                    result.Warnings.Add($"[{Path.GetFileName(sharedFile)}] {sharedDiag.Replace("\n", " | ").Trim()}");

                    if (textureTemplate != null)
                    {
                        result.Warnings.Add($"Found Texture2D template in {Path.GetFileName(sharedFile)}");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Failed to search {Path.GetFileName(sharedFile)}: {ex.Message}");
                }
            }
        }

        if (textureTemplate == null)
        {
            result.Warnings.Add("No existing Texture2D found in any asset file - cannot create texture assets");
            result.Success = false;
            return result;
        }

        // Find an existing Sprite to use as template (only needed if any entry wants sprites)
        NativeSpriteCreator.SpriteTemplate? spriteTemplate = null;
        if (textureEntries.Any(e => e.CreateSprite))
        {
            spriteTemplate = NativeSpriteCreator.FindTemplate(afile);
            if (spriteTemplate == null)
            {
                // Also search sharedassets for Sprite template
                var dataDir = Path.GetDirectoryName(gameDataPath) ?? gameDataPath;
                var sharedAssetsFiles = Directory.GetFiles(dataDir, "sharedassets*.assets")
                    .OrderBy(f => f)
                    .ToList();

                foreach (var sharedFile in sharedAssetsFiles)
                {
                    try
                    {
                        var sharedInst = am.LoadAssetsFile(sharedFile, false);
                        spriteTemplate = NativeSpriteCreator.FindTemplate(sharedInst.file);
                        if (spriteTemplate != null)
                        {
                            result.Warnings.Add($"Found Sprite template in {Path.GetFileName(sharedFile)}");
                            break;
                        }
                    }
                    catch
                    {
                        // Continue searching
                    }
                }
            }

            if (spriteTemplate == null)
            {
                result.Warnings.Add("No existing Sprite found to use as template - sprites will be skipped");
            }
        }

        foreach (var entry in textureEntries)
        {
            try
            {
                // Verify the source file exists
                if (!File.Exists(entry.SourceFilePath))
                {
                    result.Warnings.Add($"Texture '{entry.AssetName}' skipped: source file not found ({entry.SourceFilePath})");
                    continue;
                }

                // Check if this is a replacement (existing resource path in original ResourceManager)
                var resourcePath = !string.IsNullOrEmpty(entry.ResourcePath) ? entry.ResourcePath : $"assets/textures/{entry.AssetName}";
                bool isReplacement = originalResourceRefs.TryGetValue(resourcePath, out var originalRef);

                if (isReplacement)
                {
                    // IN-PLACE REPLACEMENT: Replace existing asset bytes at original PathId
                    // This preserves all existing references (Sprites, Materials, etc.)
                    var existingAsset = afile.AssetInfos.FirstOrDefault(i => i.PathId == originalRef.PathId);
                    if (existingAsset != null)
                    {
                        var texResult = NativeTextureCreator.ReplaceTextureInPlace(
                            afile,
                            textureTemplate,
                            entry.SourceFilePath,
                            entry.AssetName,
                            existingAsset);

                        if (texResult.Success)
                        {
                            textureAssets[entry.AssetName] = existingAsset;
                            result.Warnings.Add($"Texture '{entry.AssetName}' REPLACED in-place at PathId={originalRef.PathId}");
                        }
                        else
                        {
                            result.Warnings.Add($"Texture '{entry.AssetName}' in-place replace failed: {texResult.ErrorMessage}");
                        }
                    }
                    else
                    {
                        result.Warnings.Add($"Texture '{entry.AssetName}' replacement failed: original asset PathId={originalRef.PathId} not found");
                    }
                }
                else
                {
                    // NEW TEXTURE: Create with new PathId
                    var texturePathId = nextPathId++;
                    var texResult = NativeTextureCreator.CreateFromPng(
                        afile,
                        textureTemplate,
                        entry.SourceFilePath,
                        entry.AssetName,
                        texturePathId);

                    if (texResult.Success)
                    {
                        // Find the created asset info
                        var texInfo = afile.AssetInfos.FirstOrDefault(i => i.PathId == texResult.PathId);
                        if (texInfo != null)
                        {
                            textureAssets[entry.AssetName] = texInfo;
                            textureResourcePaths[entry.AssetName] = resourcePath;
                        }

                        // Create Sprite if requested (only for new textures, not replacements)
                        if (entry.CreateSprite && spriteTemplate != null)
                        {
                            var spritePathId = nextPathId++;
                            var spriteName = entry.AssetName + "_sprite";
                            var spriteResult = NativeSpriteCreator.CreateSprite(
                                afile,
                                spriteTemplate,
                                spriteName,
                                texturePathId,
                                texResult.Width,
                                texResult.Height,
                                spritePathId,
                                entry.PixelsPerUnit);

                            if (spriteResult.Success)
                            {
                                var spriteInfo = afile.AssetInfos.FirstOrDefault(i => i.PathId == spriteResult.PathId);
                                if (spriteInfo != null)
                                {
                                    spriteAssets[spriteName] = spriteInfo;
                                    spriteResourcePaths[spriteName] = resourcePath + "_sprite";
                                }
                            }
                            else
                            {
                                result.Warnings.Add($"Sprite for '{entry.AssetName}' failed: {spriteResult.ErrorMessage}");
                            }
                        }
                    }
                    else
                    {
                        result.Warnings.Add($"Texture '{entry.AssetName}' failed: {texResult.ErrorMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Texture '{entry.AssetName}' failed: {ex.Message}");
            }
        }

        result.Success = textureAssets.Count > 0;
        return result;
    }

    /// <summary>
    /// Create a clone of asset bytes with new m_Name and m_ID strings.
    /// For MonoBehaviour/ScriptableObject, m_Name is at offset 12 (after the m_Script PPtr),
    /// and m_ID is at a later offset found by FindTemplateId.
    /// Both must be patched for Unity to correctly identify the clone by name.
    /// </summary>
    private static byte[]? CloneWithNewId(byte[] sourceBytes, string newId, int idOffset)
    {
        if (sourceBytes.Length <= idOffset + 4) return null;
        if (sourceBytes.Length <= UnityBinaryPatcher.MONOBEHAVIOUR_NAME_OFFSET + 4) return null;

        // Step 1: Patch m_Name at offset 12
        // CRITICAL: m_Name MUST be patched for runtime patch lookup to work.
        // The runtime uses obj.name (which is m_Name) to find patches by template name.
        var afterNamePatch = UnityBinaryPatcher.PatchStringAtOffset(
            sourceBytes, UnityBinaryPatcher.MONOBEHAVIOUR_NAME_OFFSET, newId);

        if (afterNamePatch == null)
        {
            // m_Name patch failed - this is a critical error since runtime lookup won't work
            Console.WriteLine($"[BundleCompiler] WARNING: m_Name patch failed for clone '{newId}' - runtime patches may not apply");
            // Still try to patch m_ID on original bytes, but clone will likely have issues
            return UnityBinaryPatcher.PatchStringAtOffset(sourceBytes, idOffset, newId);
        }

        // Step 2: Calculate the new m_ID offset after m_Name patch shifted the data
        int nameSizeDiff = afterNamePatch.Length - sourceBytes.Length;
        int newIdOffset = idOffset + nameSizeDiff;

        // Sanity check: m_ID offset should still be valid
        if (newIdOffset < UnityBinaryPatcher.MONOBEHAVIOUR_NAME_OFFSET || newIdOffset + 4 > afterNamePatch.Length)
        {
            // m_ID offset calculation failed, but m_Name IS patched in afterNamePatch.
            // Return the name-patched version so runtime lookup will work.
            // m_ID won't match m_Name, but at least patches can be found and applied.
            Console.WriteLine($"[BundleCompiler] WARNING: m_ID offset invalid after m_Name patch for '{newId}' - returning name-only patch");
            return afterNamePatch;
        }

        // Step 3: Patch m_ID at the adjusted offset
        var result = UnityBinaryPatcher.PatchStringAtOffset(afterNamePatch, newIdOffset, newId);
        return result ?? afterNamePatch; // If m_ID patch fails, at least m_Name is patched
    }

    /// <summary>
    /// Write fallback assets when bundle creation fails.
    /// With raw byte cloning, we write the raw bytes directly.
    /// </summary>
    private static void WriteFallbackAssets(
        Dictionary<string, AssetFileInfo> clonedAssets,
        Dictionary<string, (AssetFileInfo info, byte[] bytes, int idOffset)> assetsByName,
        string dir,
        string outputPath)
    {
        var manifest = new Dictionary<string, string>();

        // Write cloned assets
        foreach (var (assetName, info) in clonedAssets)
        {
            if (assetsByName.TryGetValue(assetName, out var asset))
            {
                try
                {
                    var assetPath = Path.Combine(dir, $"{assetName}.asset");
                    File.WriteAllBytes(assetPath, asset.bytes);
                    manifest[assetName] = Path.GetFileName(assetPath);
                }
                catch
                {
                    // Skip assets that fail to write
                }
            }
        }

        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(outputPath, manifestJson);
    }

    /// <summary>
    /// Find game data files (.assets, level files) in the game's data directory.
    /// </summary>
    private static List<string> FindGameDataFiles(string gameDataPath)
    {
        var files = new List<string>();

        if (!Directory.Exists(gameDataPath))
            return files;

        // Look for *_Data directories (Unity convention)
        var dataDirs = Directory.GetDirectories(gameDataPath, "*_Data");
        foreach (var dataDir in dataDirs)
        {
            // resources.assets is the main container for ScriptableObjects
            var resourcesAssets = Path.Combine(dataDir, "resources.assets");
            if (File.Exists(resourcesAssets))
                files.Add(resourcesAssets);

            // Also check numbered level files
            foreach (var levelFile in Directory.GetFiles(dataDir, "level*"))
            {
                if (!levelFile.EndsWith(".resS") && !levelFile.EndsWith(".resource"))
                    files.Add(levelFile);
            }
        }

        // Direct .assets files in the path
        foreach (var assetsFile in Directory.GetFiles(gameDataPath, "*.assets"))
        {
            if (!files.Contains(assetsFile))
                files.Add(assetsFile);
        }

        return files;
    }

    /// <summary>
    /// Parse ResourceManager bytes to build lookups:
    /// - templateName -> resourcePath (for cloning)
    /// - resourcePath -> (FileId, PathId) (for in-place replacement)
    /// </summary>
    private static (Dictionary<string, string> nameToPath, Dictionary<string, (int FileId, long PathId)> pathToRef) ParseResourceManager(
        byte[] rmBytes,
        Dictionary<string, (AssetFileInfo info, byte[] bytes, int idOffset)> assetsByName)
    {
        var nameToPath = new Dictionary<string, string>();
        var pathToRef = new Dictionary<string, (int FileId, long PathId)>(StringComparer.OrdinalIgnoreCase);

        // Build PathID -> name reverse lookup
        var pathIdToName = new Dictionary<long, string>();
        foreach (var (name, data) in assetsByName)
        {
            pathIdToName[data.info.PathId] = name;
        }

        // Parse ResourceManager entries
        // Format: 4-byte count, then entries of (string, PPtr)
        if (rmBytes.Length < 4) return (nameToPath, pathToRef);

        int entryCount = BitConverter.ToInt32(rmBytes, 0);
        int offset = 4;

        for (int i = 0; i < entryCount && offset < rmBytes.Length - 20; i++)
        {
            // Read string length
            int strLen = BitConverter.ToInt32(rmBytes, offset);
            offset += 4;

            if (strLen <= 0 || strLen > 2000 || offset + strLen > rmBytes.Length)
                break;

            // Read path string
            string path = System.Text.Encoding.UTF8.GetString(rmBytes, offset, strLen);
            offset += strLen;

            // Align to 4 bytes
            int padding = (4 - (strLen % 4)) % 4;
            offset += padding;

            // Read PPtr (FileID + PathID)
            if (offset + 12 > rmBytes.Length) break;
            int fileId = BitConverter.ToInt32(rmBytes, offset);
            long pathId = BitConverter.ToInt64(rmBytes, offset + 4);
            offset += 12;

            // Store resourcePath -> (FileId, PathId) for all entries (for in-place replacement)
            pathToRef[path] = (fileId, pathId);

            // Map PathID to template name and store the path (for cloning)
            if (pathIdToName.TryGetValue(pathId, out var templateName))
            {
                nameToPath[templateName] = path;
            }
        }

        return (nameToPath, pathToRef);
    }

    /// <summary>
    /// Represents a ResourceManager entry (path -> asset reference).
    /// </summary>
    private class ResourceManagerEntry
    {
        public string Path { get; set; } = "";
        public int FileId { get; set; }
        public long PathId { get; set; }
    }

    /// <summary>
    /// Patch the globalgamemanagers file to add new ResourceManager entries for clones, audio, texture, and model assets.
    /// Entries must be inserted in sorted order (Unity uses binary search).
    /// Returns (patchedBytes, debugInfo).
    /// </summary>
    private static (byte[]?, string) PatchResourceManager(
        byte[] ggmBytes,
        Dictionary<string, AssetFileInfo> clonedAssets,
        Dictionary<string, string> cloneSourceMap,
        Dictionary<string, string> resourcePathLookup,
        Dictionary<string, AssetFileInfo>? audioAssets = null,
        Dictionary<string, string>? audioResourcePaths = null,
        Dictionary<string, AssetFileInfo>? textureAssets = null,
        Dictionary<string, string>? textureResourcePaths = null,
        Dictionary<string, AssetFileInfo>? spriteAssets = null,
        Dictionary<string, string>? spriteResourcePaths = null,
        Dictionary<string, AssetFileInfo>? modelAssets = null,
        Dictionary<string, string>? modelResourcePaths = null,
        Dictionary<string, AssetFileInfo>? prefabAssets = null,
        Dictionary<string, string>? prefabResourcePaths = null)
    {
        var debugLog = new System.Text.StringBuilder();

        if (clonedAssets.Count == 0 &&
            (audioAssets?.Count ?? 0) == 0 &&
            (textureAssets?.Count ?? 0) == 0 &&
            (spriteAssets?.Count ?? 0) == 0 &&
            (modelAssets?.Count ?? 0) == 0 &&
            (prefabAssets?.Count ?? 0) == 0) return (null, "No assets to patch");

        // Load globalgamemanagers using AssetsTools.NET for proper modification
        using var ggmStream = new MemoryStream(ggmBytes);
        var am = new AssetsManager();

        try
        {
            var ggmInst = am.LoadAssetsFile(ggmStream, "globalgamemanagers", false);
            var ggmFile = ggmInst.file;

            // Find ResourceManager asset
            AssetFileInfo? rmInfo = null;
            foreach (var info in ggmFile.AssetInfos)
            {
                if (info.TypeId == 147) // ResourceManager
                {
                    rmInfo = info;
                    break;
                }
            }

            if (rmInfo == null) return (null, "ResourceManager not found");

            // Read current ResourceManager data
            var reader = ggmFile.Reader;
            reader.BaseStream.Position = rmInfo.GetAbsoluteByteOffset(ggmFile);
            var rmBytes = reader.ReadBytes((int)rmInfo.ByteSize);

            // Parse all existing entries (container map)
            // ResourceManager has: container map + preload table + other data
            // We only modify the container map but must preserve everything else
            var entries = new List<ResourceManagerEntry>();
            int entryCount = BitConverter.ToInt32(rmBytes, 0);
            int offset = 4;

            for (int i = 0; i < entryCount; i++)
            {
                if (offset + 4 > rmBytes.Length) break;
                int strLen = BitConverter.ToInt32(rmBytes, offset);
                offset += 4;

                if (strLen <= 0 || strLen > 2000 || offset + strLen > rmBytes.Length)
                    break;

                string path = System.Text.Encoding.UTF8.GetString(rmBytes, offset, strLen);
                offset += strLen;
                offset += (4 - (strLen % 4)) % 4;

                if (offset + 12 > rmBytes.Length) break;
                int fileId = BitConverter.ToInt32(rmBytes, offset);
                long pathId = BitConverter.ToInt64(rmBytes, offset + 4);
                offset += 12;

                entries.Add(new ResourceManagerEntry { Path = path, FileId = fileId, PathId = pathId });
            }

            // Log ResourceManager stats and sample paths for debugging
            var fileIdGroups = entries.GroupBy(e => e.FileId).OrderByDescending(g => g.Count()).Take(5);
            debugLog.AppendLine($"ResourceManager has {entries.Count} entries. FileId distribution:");
            foreach (var g in fileIdGroups)
            {
                debugLog.AppendLine($"  FileId={g.Key}: {g.Count()} entries");
            }
            // Show sample paths containing "texture" or "ui"
            var samplePaths = entries.Where(e => e.Path.Contains("texture") || e.Path.Contains("ui/"))
                .Take(10).Select(e => $"  '{e.Path}' -> FileId={e.FileId}");
            debugLog.AppendLine("Sample texture/ui paths in ResourceManager:");
            foreach (var p in samplePaths) debugLog.AppendLine(p);

            // Track where container entries end - everything after this is preserved
            int containerEndOffset = offset;

            // Build new entries for each clone
            int addedCount = 0;
            foreach (var (cloneName, cloneInfo) in clonedAssets)
            {
                // Get source template's resource path
                if (!cloneSourceMap.TryGetValue(cloneName, out var sourceName))
                    continue;

                if (!resourcePathLookup.TryGetValue(sourceName, out var sourcePath))
                    continue;

                // Derive clone's resource path
                var lastSlash = sourcePath.LastIndexOf('/');
                if (lastSlash < 0) continue;

                var folder = sourcePath.Substring(0, lastSlash);
                var clonePath = $"{folder}/{cloneName}";

                entries.Add(new ResourceManagerEntry
                {
                    Path = clonePath,
                    FileId = 4, // resources.assets
                    PathId = cloneInfo.PathId
                });
                addedCount++;
            }

            // Build new entries for each audio asset
            if (audioAssets != null && audioResourcePaths != null)
            {
                foreach (var (audioName, audioInfo) in audioAssets)
                {
                    if (!audioResourcePaths.TryGetValue(audioName, out var resourcePath))
                        continue;

                    entries.Add(new ResourceManagerEntry
                    {
                        Path = resourcePath,
                        FileId = 4, // resources.assets
                        PathId = audioInfo.PathId
                    });
                    addedCount++;
                }
            }

            // Build new entries for each texture asset
            // If a texture path matches an existing entry, UPDATE it (replacement) instead of adding duplicate
            int textureReplacements = 0;
            int textureAdditions = 0;
            if (textureAssets != null && textureResourcePaths != null)
            {
                debugLog.AppendLine($"Processing {textureAssets.Count} textures, {textureResourcePaths.Count} resource paths");
                foreach (var (textureName, textureInfo) in textureAssets)
                {
                    if (!textureResourcePaths.TryGetValue(textureName, out var resourcePath))
                    {
                        debugLog.AppendLine($"  Texture '{textureName}' has no resource path mapping, skipping");
                        continue;
                    }

                    // Check if this path already exists in the ResourceManager
                    var existingEntry = entries.FirstOrDefault(e =>
                        string.Equals(e.Path, resourcePath, StringComparison.OrdinalIgnoreCase));

                    if (existingEntry != null)
                    {
                        // UPDATE existing entry to point to our new texture (replacement)
                        debugLog.AppendLine($"  FOUND '{resourcePath}' - REPLACING (old FileId={existingEntry.FileId}, PathId={existingEntry.PathId} -> new FileId=4, PathId={textureInfo.PathId})");
                        existingEntry.FileId = 4; // resources.assets
                        existingEntry.PathId = textureInfo.PathId;
                        addedCount++;
                        textureReplacements++;
                    }
                    else
                    {
                        // ADD new entry (new texture asset)
                        debugLog.AppendLine($"  NOT FOUND '{resourcePath}' - Adding as NEW (PathId={textureInfo.PathId})");
                        entries.Add(new ResourceManagerEntry
                        {
                            Path = resourcePath,
                            FileId = 4, // resources.assets
                            PathId = textureInfo.PathId
                        });
                        addedCount++;
                        textureAdditions++;
                    }
                }
            }
            debugLog.AppendLine($"Texture summary: {textureReplacements} REPLACEMENTS, {textureAdditions} ADDITIONS");

            // Build new entries for each sprite asset
            // If a sprite path matches an existing entry, UPDATE it (replacement) instead of adding duplicate
            if (spriteAssets != null && spriteResourcePaths != null)
            {
                foreach (var (spriteName, spriteInfo) in spriteAssets)
                {
                    if (!spriteResourcePaths.TryGetValue(spriteName, out var resourcePath))
                        continue;

                    // Check if this path already exists in the ResourceManager
                    var existingEntry = entries.FirstOrDefault(e =>
                        string.Equals(e.Path, resourcePath, StringComparison.OrdinalIgnoreCase));

                    if (existingEntry != null)
                    {
                        // UPDATE existing entry to point to our new sprite (replacement)
                        existingEntry.FileId = 4; // resources.assets
                        existingEntry.PathId = spriteInfo.PathId;
                        addedCount++;
                    }
                    else
                    {
                        // ADD new entry (new sprite asset)
                        entries.Add(new ResourceManagerEntry
                        {
                            Path = resourcePath,
                            FileId = 4, // resources.assets
                            PathId = spriteInfo.PathId
                        });
                        addedCount++;
                    }
                }
            }

            // Build new entries for each model (mesh) asset
            if (modelAssets != null && modelResourcePaths != null)
            {
                foreach (var (modelName, modelInfo) in modelAssets)
                {
                    if (!modelResourcePaths.TryGetValue(modelName, out var resourcePath))
                        continue;

                    entries.Add(new ResourceManagerEntry
                    {
                        Path = resourcePath,
                        FileId = 4, // resources.assets
                        PathId = modelInfo.PathId
                    });
                    addedCount++;
                }
            }

            // Build new entries for each prefab asset
            if (prefabAssets != null && prefabResourcePaths != null)
            {
                foreach (var (prefabName, prefabInfo) in prefabAssets)
                {
                    if (!prefabResourcePaths.TryGetValue(prefabName, out var resourcePath))
                        continue;

                    entries.Add(new ResourceManagerEntry
                    {
                        Path = resourcePath,
                        FileId = 4, // resources.assets
                        PathId = prefabInfo.PathId
                    });
                    addedCount++;
                }
            }

            if (addedCount == 0) return (null, debugLog.ToString());

            // Sort all entries alphabetically (Unity uses binary search)
            entries.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal));

            // Serialize the new ResourceManager data
            // Must preserve all data after container entries (preload table, etc.)
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            // Write entry count
            bw.Write(entries.Count);

            // Write each entry
            foreach (var entry in entries)
            {
                var pathBytes = System.Text.Encoding.UTF8.GetBytes(entry.Path);
                int pathLen = pathBytes.Length;
                int pathPadding = (4 - (pathLen % 4)) % 4;

                bw.Write(pathLen);
                bw.Write(pathBytes);
                for (int i = 0; i < pathPadding; i++)
                    bw.Write((byte)0);
                bw.Write(entry.FileId);
                bw.Write(entry.PathId);
            }

            // Append all remaining data (preload table, dependencies, etc.)
            if (containerEndOffset < rmBytes.Length)
            {
                bw.Write(rmBytes, containerEndOffset, rmBytes.Length - containerEndOffset);
            }

            var newRmData = ms.ToArray();

            // Update the asset with new data
            rmInfo.SetNewData(newRmData);

            // Write the modified file
            using var outStream = new MemoryStream();
            using (var writer = new AssetsFileWriter(outStream))
            {
                ggmFile.Write(writer);
            }

            return (outStream.ToArray(), debugLog.ToString());
        }
        finally
        {
            am.UnloadAll();
        }
    }
}
