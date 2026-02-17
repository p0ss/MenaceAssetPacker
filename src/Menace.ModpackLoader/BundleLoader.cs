using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2CppInterop.Runtime;
using Menace.SDK;
using UnityEngine;

namespace Menace.ModpackLoader;

/// <summary>
/// Loads .bundle files from deployed modpacks via AssetBundle.LoadFromFile and
/// maintains a registry of all loaded assets. Mod code can query loaded assets
/// by name and type, enabling both replacement of existing game content and
/// injection of entirely new content (new templates, textures, prefabs, etc.).
///
/// Assets remain in memory as long as their source AssetBundle is loaded.
/// </summary>
public static class BundleLoader
{
    private static readonly List<AssetBundle> _loadedBundles = new();

    // Asset registry: name → list of loaded UnityEngine.Object (multiple bundles may have same-named assets)
    private static readonly Dictionary<string, List<UnityEngine.Object>> _assetsByName
        = new(StringComparer.OrdinalIgnoreCase);

    // Type+name registry for precise lookups: "TypeName:assetName" → Object
    private static readonly Dictionary<string, UnityEngine.Object> _assetsByTypeAndName
        = new(StringComparer.OrdinalIgnoreCase);

    // Track which modpack each asset came from
    private static readonly Dictionary<string, string> _assetSourceModpack
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Number of loaded bundles.
    /// </summary>
    public static int LoadedBundleCount => _loadedBundles.Count;

    /// <summary>
    /// Total number of registered assets across all loaded bundles.
    /// </summary>
    public static int LoadedAssetCount => _assetsByTypeAndName.Count;

    /// <summary>
    /// Load all .bundle files found in a modpack's directory.
    /// Assets from each bundle are registered in the asset registry.
    /// </summary>
    public static void LoadBundles(string modpackDir, string modpackName)
    {
        if (!Directory.Exists(modpackDir))
            return;

        var bundleFiles = Directory.GetFiles(modpackDir, "*.bundle", SearchOption.AllDirectories);

        foreach (var bundlePath in bundleFiles)
        {
            var bundleFileName = Path.GetFileName(bundlePath);
            AssetBundle bundle = null;

            try
            {
                SdkLogger.Msg($"  [{modpackName}] Loading bundle: {bundleFileName}");

                // Unity 6 has IL2CPP binding issues with LoadFromFile (ReadOnlySpan.GetPinnableReference)
                // Use LoadFromMemory as a workaround
                var bundleBytes = File.ReadAllBytes(bundlePath);
                bundle = AssetBundle.LoadFromMemory(bundleBytes);
            }
            catch (Exception loadEx)
            {
                SdkLogger.Error($"  [{modpackName}] LoadFromMemory failed for {bundleFileName}: {loadEx.Message}");
                continue;
            }

            if (bundle == null)
            {
                SdkLogger.Warning($"  [{modpackName}] Failed to load bundle: {bundleFileName}");
                continue;
            }

            _loadedBundles.Add(bundle);
            SdkLogger.Msg($"  [{modpackName}] Loaded bundle: {bundleFileName}");

            // Try GetAllAssetNames first (may not work for all bundle types)
            string[] assetNames = null;
            try
            {
                assetNames = bundle.GetAllAssetNames();
            }
            catch (Exception namesEx)
            {
                SdkLogger.Warning($"    GetAllAssetNames failed: {namesEx.Message}");
            }

            if (assetNames != null && assetNames.Length > 0)
            {
                SdkLogger.Msg($"    Contains {assetNames.Length} asset(s)");

                foreach (var assetPath in assetNames)
                {
                    try
                    {
                        var asset = bundle.LoadAsset(assetPath);
                        if (asset != null)
                            RegisterAsset(asset, modpackName);
                    }
                    catch (Exception assetEx)
                    {
                        SdkLogger.Warning($"    Failed to load asset '{assetPath}': {assetEx.Message}");
                    }
                }
            }
            else
            {
                // Fallback: try LoadAllAssets for bundles without asset names
                SdkLogger.Msg($"    No asset names, trying LoadAllAssets fallback...");
                try
                {
                    var allAssets = bundle.LoadAllAssets();
                    if (allAssets != null)
                    {
                        SdkLogger.Msg($"    Contains {allAssets.Length} asset(s) (fallback)");
                        foreach (var asset in allAssets)
                        {
                            if (asset == null) continue;
                            RegisterAsset(asset, modpackName);
                        }
                    }
                }
                catch (Exception fallbackEx)
                {
                    SdkLogger.Warning($"    LoadAllAssets fallback failed: {fallbackEx.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Register a loaded asset in the name and type+name registries.
    /// </summary>
    private static void RegisterAsset(UnityEngine.Object asset, string modpackName)
    {
        var assetName = asset.name;
        var typeName = asset.GetIl2CppType().Name;
        SdkLogger.Msg($"    - {assetName} ({typeName})");

        RegisterAssetInternal(assetName, asset, typeName, modpackName);
    }

    /// <summary>
    /// Register an asset created outside of bundle loading (e.g., from GlbLoader).
    /// </summary>
    public static void RegisterAsset(string name, UnityEngine.Object asset, string typeName, string modpackName = "Runtime")
    {
        SdkLogger.Msg($"  [BundleLoader] RegisterAsset: '{name}' type={typeName} asset={asset?.name ?? "null"} from={modpackName}");
        RegisterAssetInternal(name, asset, typeName, modpackName);
    }

    private static void RegisterAssetInternal(string assetName, UnityEngine.Object asset, string typeName, string modpackName)
    {
        // Name-only registry (may have multiple assets with same name but different types)
        if (!_assetsByName.TryGetValue(assetName, out var list))
        {
            list = new List<UnityEngine.Object>();
            _assetsByName[assetName] = list;
        }
        list.Add(asset);

        // Type+name registry (last loaded wins for same type+name — load order matters)
        var key = $"{typeName}:{assetName}";
        _assetsByTypeAndName[key] = asset;
        _assetSourceModpack[key] = modpackName;
    }

    /// <summary>
    /// Get a loaded asset by name. Returns the first match regardless of type.
    /// Returns null if no asset with that name was loaded from any bundle.
    /// </summary>
    public static UnityEngine.Object GetAsset(string name)
    {
        if (_assetsByName.TryGetValue(name, out var list) && list.Count > 0)
            return list[0];
        return null;
    }

    /// <summary>
    /// Get a loaded asset by name, cast to the specified IL2CPP type.
    /// Returns null if not found or if the cast fails.
    /// </summary>
    public static T GetAsset<T>(string name) where T : UnityEngine.Object
    {
        var typeName = Il2CppType.From(typeof(T)).Name;
        var key = $"{typeName}:{name}";
        if (_assetsByTypeAndName.TryGetValue(key, out var obj))
        {
            try { return obj.Cast<T>(); }
            catch { return null; }
        }

        // Fallback: search by name, try casting each match
        if (_assetsByName.TryGetValue(name, out var list))
        {
            foreach (var item in list)
            {
                try { return item.Cast<T>(); }
                catch { }
            }
        }

        return null;
    }

    /// <summary>
    /// Get all loaded assets with the given name (may be multiple types).
    /// </summary>
    public static List<UnityEngine.Object> GetAllAssets(string name)
    {
        if (_assetsByName.TryGetValue(name, out var list))
            return new List<UnityEngine.Object>(list);
        return new List<UnityEngine.Object>();
    }

    /// <summary>
    /// Get all loaded assets of a specific IL2CPP type name (e.g. "Texture2D", "EntityTemplate").
    /// </summary>
    public static List<UnityEngine.Object> GetAssetsByType(string typeName)
    {
        var results = new List<UnityEngine.Object>();
        var prefix = $"{typeName}:";
        foreach (var kvp in _assetsByTypeAndName)
        {
            if (kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                results.Add(kvp.Value);
        }

        // Debug logging for GameObject lookups
        if (typeName.Equals("GameObject", StringComparison.OrdinalIgnoreCase))
        {
            SdkLogger.Msg($"    [BundleLoader] GetAssetsByType(GameObject): found {results.Count} assets");
            foreach (var asset in results)
            {
                SdkLogger.Msg($"      - {asset?.name ?? "null"}");
            }
        }

        return results;
    }

    /// <summary>
    /// Check whether any bundle has loaded an asset with the given name.
    /// </summary>
    public static bool HasAsset(string name)
    {
        return _assetsByName.ContainsKey(name);
    }

    /// <summary>
    /// Unload all loaded bundles and clear the asset registry.
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
        _assetsByName.Clear();
        _assetsByTypeAndName.Clear();
        _assetSourceModpack.Clear();
    }
}
