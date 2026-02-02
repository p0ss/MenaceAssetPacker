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
/// Compiles merged data patches into Unity asset bundles.
/// Takes base game assets + merged patches → produces .bundle files
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
    }

    /// <summary>
    /// Compile a merged patch set into an asset bundle.
    /// This reads base game assets, applies the patches, and writes a new bundle.
    /// </summary>
    /// <param name="mergedPatches">The merged patches from all active modpacks.</param>
    /// <param name="gameDataPath">Path to the game's data directory (contains level files, resources.assets, etc.)</param>
    /// <param name="unityVersion">The Unity version string (e.g. "2020.3.18f1").</param>
    /// <param name="outputPath">Path to write the output .bundle file.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<BundleCompileResult> CompileDataPatchBundleAsync(
        MergedPatchSet mergedPatches,
        string gameDataPath,
        string unityVersion,
        string outputPath,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                return CompileDataPatchBundleCore(mergedPatches, gameDataPath, unityVersion, outputPath);
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
        string gameDataPath,
        string unityVersion,
        string outputPath)
    {
        var result = new BundleCompileResult();

        if (mergedPatches.Patches.Count == 0)
        {
            result.Success = false;
            result.Message = "No patches to compile";
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

            // Track which file had the most patches applied (we'll use it for the bundle)
            AssetsFileInstance? bestFileInst = null;
            int bestPatchCount = 0;
            int totalPatched = 0;

            foreach (var templateType in mergedPatches.GetTemplateTypes())
            {
                var instances = mergedPatches.Patches[templateType];

                foreach (var dataFile in dataFiles)
                {
                    try
                    {
                        var afileInst = am.LoadAssetsFile(dataFile, false);
                        var afile = afileInst.file;
                        int patchCount = 0;

                        foreach (var info in afile.GetAssetsOfType(AssetClassID.MonoBehaviour))
                        {
                            var baseField = am.GetBaseField(afileInst, info);
                            if (baseField == null) continue;

                            var nameField = baseField.Children.FirstOrDefault(f => f.FieldName == "m_Name");
                            var assetName = nameField?.Value?.AsString;

                            if (assetName != null && instances.TryGetValue(assetName, out var fieldPatches))
                            {
                                // Apply patches to this asset
                                TemplatePatchSerializer.ApplyPatches(baseField, fieldPatches);

                                // Replace asset data in-place using AssetsTools.NET replacer
                                info.SetNewData(baseField);
                                patchCount++;
                                totalPatched++;
                            }
                        }

                        if (patchCount > bestPatchCount)
                        {
                            bestPatchCount = patchCount;
                            bestFileInst = afileInst;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Warnings.Add($"Failed to process {Path.GetFileName(dataFile)} for {templateType}: {ex.Message}");
                    }
                }
            }

            if (totalPatched == 0 || bestFileInst == null)
            {
                result.Success = false;
                result.Message = "No matching game assets found for patching. Legacy JSON loader will be used.";
                return result;
            }

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // Write the modified AssetsFile to memory, then wrap in UnityFS
            bool bundleWritten = false;
            try
            {
                using var ms = new MemoryStream();
                using (var writer = new AssetsFileWriter(ms))
                {
                    bestFileInst.file.Write(writer);
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
                result.Message = $"Compiled {totalPatched} patched asset(s) into UnityFS bundle";
            }
            else
            {
                // Fallback: write individual .asset files + JSON manifest
                result.Warnings.Add("Falling back to JSON manifest + individual asset files");

                var manifest = new Dictionary<string, string>();
                foreach (var templateType in mergedPatches.GetTemplateTypes())
                {
                    var instances = mergedPatches.Patches[templateType];
                    foreach (var dataFile in dataFiles)
                    {
                        try
                        {
                            var afileInst = am.LoadAssetsFile(dataFile, false);
                            var afile = afileInst.file;

                            foreach (var info in afile.GetAssetsOfType(AssetClassID.MonoBehaviour))
                            {
                                var baseField = am.GetBaseField(afileInst, info);
                                if (baseField == null) continue;

                                var nameField = baseField.Children.FirstOrDefault(f => f.FieldName == "m_Name");
                                var assetName = nameField?.Value?.AsString;

                                if (assetName != null && instances.ContainsKey(assetName))
                                {
                                    var patched = baseField.WriteToByteArray();
                                    var assetPath = Path.Combine(dir!, $"{assetName}.asset");
                                    File.WriteAllBytes(assetPath, patched);
                                    manifest[assetName] = Path.GetFileName(assetPath);
                                }
                            }
                        }
                        catch { }
                    }
                }

                var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(outputPath, manifestJson);

                result.Success = true;
                result.OutputPath = outputPath;
                result.Message = $"Compiled {totalPatched} patched asset(s) (JSON manifest fallback)";
            }
        }
        finally
        {
            am.UnloadAll();
        }

        return result;
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
