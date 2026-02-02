using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using UnityEngine;

namespace Menace.ModpackLoader;

/// <summary>
/// Loads .bundle files from deployed modpacks via AssetBundle.LoadFromFile.
/// V2 manifest bundles are loaded and their assets registered with the game's template system.
/// </summary>
public static class BundleLoader
{
    private static readonly List<AssetBundle> _loadedBundles = new();

    /// <summary>
    /// Load all .bundle files found in a modpack's directory.
    /// </summary>
    public static void LoadBundles(string modpackDir, string modpackName)
    {
        if (!Directory.Exists(modpackDir))
            return;

        // Look for .bundle files in the modpack directory and subdirectories
        var bundleFiles = Directory.GetFiles(modpackDir, "*.bundle", SearchOption.AllDirectories);

        foreach (var bundlePath in bundleFiles)
        {
            try
            {
                var bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle != null)
                {
                    _loadedBundles.Add(bundle);
                    MelonLogger.Msg($"  [{modpackName}] Loaded bundle: {Path.GetFileName(bundlePath)}");

                    // Load all assets from the bundle
                    var allAssets = bundle.LoadAllAssets();
                    if (allAssets != null)
                    {
                        MelonLogger.Msg($"    Contains {allAssets.Length} asset(s)");

                        foreach (var asset in allAssets)
                        {
                            if (asset != null)
                            {
                                MelonLogger.Msg($"    - {asset.name} ({asset.GetIl2CppType().Name})");
                            }
                        }
                    }
                }
                else
                {
                    MelonLogger.Warning($"  [{modpackName}] Failed to load bundle: {Path.GetFileName(bundlePath)}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"  [{modpackName}] Error loading bundle {Path.GetFileName(bundlePath)}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Unload all loaded bundles.
    /// </summary>
    public static void UnloadAll()
    {
        foreach (var bundle in _loadedBundles)
        {
            try
            {
                bundle.Unload(false);
            }
            catch { }
        }
        _loadedBundles.Clear();
    }
}
