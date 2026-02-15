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
        return await Task.Run(() =>
        {
            try
            {
                return CompileDataPatchBundleCore(mergedPatches, mergedClones, gameDataPath, unityVersion, outputPath);
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
        string gameDataPath,
        string unityVersion,
        string outputPath)
    {
        var result = new BundleCompileResult();

        bool hasPatches = mergedPatches.Patches.Count > 0;
        bool hasClones = mergedClones?.HasClones == true;

        if (!hasPatches && !hasClones)
        {
            result.Success = false;
            result.Message = "No patches or clones to compile";
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

            // Build lookup of existing assets by name for cloning and patching
            var assetsByName = BuildAssetNameLookup(am, primaryFileInst);
            result.Warnings.Add($"Asset lookup indexed {assetsByName.Count} MonoBehaviour assets from {primaryDataFile}");

            // ===== PHASE 1: PROCESS CLONES =====
            var clonedAssets = new Dictionary<string, AssetFileInfo>();

            if (hasClones)
            {
                var cloneResult = ProcessClones(
                    am, primaryFileInst, mergedClones!, mergedPatches,
                    assetsByName, clonedAssets, ref nextPathId);

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
            int totalPatched = 0;

            if (hasPatches)
            {
                foreach (var templateType in mergedPatches.GetTemplateTypes())
                {
                    var instances = mergedPatches.Patches[templateType];

                    foreach (var (assetName, fieldPatches) in instances)
                    {
                        // Check if this is a cloned asset we just created
                        if (clonedAssets.TryGetValue(assetName, out var clonedInfo))
                        {
                            // Already patched during clone creation, skip
                            continue;
                        }

                        // Find existing asset
                        if (assetsByName.TryGetValue(assetName, out var existing))
                        {
                            try
                            {
                                TemplatePatchSerializer.ApplyPatches(existing.field, fieldPatches);
                                existing.info.SetNewData(existing.field);
                                totalPatched++;
                            }
                            catch (Exception ex)
                            {
                                result.Warnings.Add($"Patch failed for '{assetName}': {ex.Message}");
                            }
                        }
                        else
                        {
                            result.Warnings.Add($"Patch target '{assetName}' not found");
                        }
                    }
                }
            }

            result.PatchesApplied = totalPatched;

            // Check if we have any work to write
            if (result.ClonesCreated == 0 && totalPatched == 0)
            {
                result.Success = false;
                result.Message = "No assets were modified. Check warnings for details.";
                return result;
            }

            // ===== PHASE 3: WRITE BUNDLE =====
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            bool bundleWritten = false;
            try
            {
                using var ms = new MemoryStream();
                using (var writer = new AssetsFileWriter(ms))
                {
                    afile.Write(writer);
                }

                var serializedBytes = ms.ToArray();
                bundleWritten = BundleWriter.WriteBundle(serializedBytes, outputPath, unityVersion);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"UnityFS bundle writing failed: {ex.Message}");
            }

            if (bundleWritten)
            {
                result.Success = true;
                result.OutputPath = outputPath;
                var parts = new List<string>();
                if (result.ClonesCreated > 0) parts.Add($"{result.ClonesCreated} clone(s)");
                if (result.PatchesApplied > 0) parts.Add($"{result.PatchesApplied} patch(es)");
                result.Message = $"Compiled {string.Join(" and ", parts)} into UnityFS bundle";
            }
            else
            {
                // Fallback: write individual .asset files + JSON manifest
                result.Warnings.Add("Falling back to JSON manifest + individual asset files");
                WriteFallbackAssets(am, primaryFileInst, mergedPatches, assetsByName, dir!, outputPath);
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
    /// Build a lookup dictionary of all MonoBehaviour assets by name and ID.
    /// </summary>
    private static Dictionary<string, (AssetFileInfo info, AssetTypeValueField field)> BuildAssetNameLookup(
        AssetsManager am,
        AssetsFileInstance afileInst)
    {
        var lookup = new Dictionary<string, (AssetFileInfo, AssetTypeValueField)>();
        int readCount = 0;
        int failCount = 0;

        foreach (var info in afileInst.file.GetAssetsOfType(AssetClassID.MonoBehaviour))
        {
            try
            {
                var baseField = am.GetBaseField(afileInst, info);
                if (baseField == null)
                {
                    failCount++;
                    continue;
                }

                readCount++;

                // Try m_Name first
                var nameField = baseField.Children.FirstOrDefault(f => f.FieldName == "m_Name");
                var name = nameField?.Value?.AsString;

                if (!string.IsNullOrEmpty(name) && !lookup.ContainsKey(name))
                {
                    lookup[name] = (info, baseField);
                }

                // Also index by m_ID if present (templates use this as identifier)
                var idField = baseField.Children.FirstOrDefault(f => f.FieldName == "m_ID");
                var id = idField?.Value?.AsString;

                if (!string.IsNullOrEmpty(id) && !lookup.ContainsKey(id))
                {
                    lookup[id] = (info, baseField);
                }
            }
            catch
            {
                failCount++;
            }
        }

        return lookup;
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
    /// Process all clone definitions, creating new native assets.
    /// </summary>
    private CloneProcessResult ProcessClones(
        AssetsManager am,
        AssetsFileInstance afileInst,
        MergedCloneSet mergedClones,
        MergedPatchSet mergedPatches,
        Dictionary<string, (AssetFileInfo info, AssetTypeValueField field)> assetsByName,
        Dictionary<string, AssetFileInfo> clonedAssets,
        ref long nextPathId)
    {
        var result = new CloneProcessResult();
        var afile = afileInst.file;

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
                    // Deep copy the source field
                    var cloneField = DeepCopyField(source.field);

                    // Change identity fields
                    SetFieldValue(cloneField, "m_Name", newName);
                    SetFieldValue(cloneField, "m_ID", newName);

                    // Apply any patches targeting this clone
                    if (mergedPatches.Patches.TryGetValue(templateType, out var typePatches) &&
                        typePatches.TryGetValue(newName, out var clonePatches))
                    {
                        TemplatePatchSerializer.ApplyPatches(cloneField, clonePatches);
                    }

                    // Create new asset info with same type as source
                    var pathId = nextPathId++;
                    var newInfo = AssetFileInfo.Create(
                        afile,
                        pathId,
                        source.info.TypeId,
                        source.info.ScriptTypeIndex
                    );

                    // Register the clone in the assets file
                    newInfo.SetNewData(cloneField);
                    afile.Metadata.AddAssetInfo(newInfo);

                    // Track for later reference
                    clonedAssets[newName] = newInfo;
                    assetsByName[newName] = (newInfo, cloneField);
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
    /// Create a deep copy of an AssetTypeValueField, preserving all type information.
    /// </summary>
    private static AssetTypeValueField DeepCopyField(AssetTypeValueField source)
    {
        if (source == null) return null!;

        var copy = new AssetTypeValueField
        {
            TemplateField = source.TemplateField  // Preserve type definition (FieldName is derived from this)
        };

        // Copy value - create new AssetTypeValue with same data
        if (source.Value != null)
        {
            // Get the raw object value and create a new AssetTypeValue
            var valueType = source.Value.ValueType;
            object? rawValue = valueType switch
            {
                AssetValueType.Bool => source.Value.AsBool,
                AssetValueType.Int8 => source.Value.AsInt,
                AssetValueType.UInt8 => source.Value.AsUInt,
                AssetValueType.Int16 => source.Value.AsInt,
                AssetValueType.UInt16 => source.Value.AsUInt,
                AssetValueType.Int32 => source.Value.AsInt,
                AssetValueType.UInt32 => source.Value.AsUInt,
                AssetValueType.Int64 => source.Value.AsLong,
                AssetValueType.UInt64 => source.Value.AsULong,
                AssetValueType.Float => source.Value.AsFloat,
                AssetValueType.Double => source.Value.AsDouble,
                AssetValueType.String => source.Value.AsString,
                AssetValueType.ByteArray => source.Value.AsByteArray?.ToArray(), // Copy array
                _ => source.Value.AsObject
            };
            copy.Value = new AssetTypeValue(valueType, rawValue);
        }

        // Recursively copy children
        if (source.Children != null && source.Children.Count > 0)
        {
            copy.Children = new List<AssetTypeValueField>(source.Children.Count);
            foreach (var child in source.Children)
            {
                copy.Children.Add(DeepCopyField(child));
            }
        }

        return copy;
    }

    /// <summary>
    /// Set a string field value by name, searching the immediate children.
    /// </summary>
    private static void SetFieldValue(AssetTypeValueField root, string fieldName, string value)
    {
        var field = root.Children?.FirstOrDefault(f => f.FieldName == fieldName);
        if (field != null)
        {
            field.Value = new AssetTypeValue(AssetValueType.String, value);
        }
    }

    /// <summary>
    /// Write fallback assets when bundle creation fails.
    /// </summary>
    private static void WriteFallbackAssets(
        AssetsManager am,
        AssetsFileInstance afileInst,
        MergedPatchSet mergedPatches,
        Dictionary<string, (AssetFileInfo info, AssetTypeValueField field)> assetsByName,
        string dir,
        string outputPath)
    {
        var manifest = new Dictionary<string, string>();

        foreach (var templateType in mergedPatches.GetTemplateTypes())
        {
            var instances = mergedPatches.Patches[templateType];

            foreach (var (assetName, _) in instances)
            {
                if (assetsByName.TryGetValue(assetName, out var asset))
                {
                    try
                    {
                        var patched = asset.field.WriteToByteArray();
                        var assetPath = Path.Combine(dir, $"{assetName}.asset");
                        File.WriteAllBytes(assetPath, patched);
                        manifest[assetName] = Path.GetFileName(assetPath);
                    }
                    catch
                    {
                        // Skip assets that fail to serialize
                    }
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
}
