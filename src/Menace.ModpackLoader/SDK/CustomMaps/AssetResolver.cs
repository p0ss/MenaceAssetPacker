using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace Menace.SDK.CustomMaps;

/// <summary>
/// Resolves asset path strings to actual game objects.
/// Used for prefab swapping in generator configurations.
///
/// Asset paths use a simplified format:
/// - "Buildings/SmallOutpost" → looks for prefab with matching name
/// - "Props/Rock_Large" → looks for prop prefab
///
/// The resolver searches through loaded assets and caches results.
/// </summary>
public static class AssetResolver
{
    private static readonly Dictionary<string, GameObject> _prefabCache = new();
    private static readonly Dictionary<string, string[]> _categoryCache = new();
    private static bool _catalogBuilt;

    /// <summary>
    /// Resolve a single prefab by path reference.
    /// </summary>
    public static GameObject ResolvePrefab(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;

        // Check cache first
        if (_prefabCache.TryGetValue(assetPath, out var cached))
            return cached;

        try
        {
            // Extract name from path (e.g., "Buildings/SmallOutpost" → "SmallOutpost")
            var name = assetPath;
            var lastSlash = assetPath.LastIndexOf('/');
            if (lastSlash >= 0)
                name = assetPath.Substring(lastSlash + 1);

            // Search for prefab by name
            var prefab = FindPrefabByName(name);
            if (prefab != null)
            {
                _prefabCache[assetPath] = prefab;
                return prefab;
            }

            // Try full path as name
            prefab = FindPrefabByName(assetPath);
            if (prefab != null)
            {
                _prefabCache[assetPath] = prefab;
                return prefab;
            }

            ModError.WarnInternal("AssetResolver.ResolvePrefab",
                $"Prefab not found: {assetPath}");
            return null;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AssetResolver.ResolvePrefab",
                $"Failed to resolve {assetPath}", ex);
            return null;
        }
    }

    /// <summary>
    /// Resolve multiple prefab paths to an array.
    /// </summary>
    public static GameObject[] ResolvePrefabArray(IEnumerable<string> assetPaths)
    {
        if (assetPaths == null)
            return Array.Empty<GameObject>();

        var result = new List<GameObject>();
        foreach (var path in assetPaths)
        {
            var prefab = ResolvePrefab(path);
            if (prefab != null)
                result.Add(prefab);
        }
        return result.ToArray();
    }

    /// <summary>
    /// Find a prefab by its name using Resources.FindObjectsOfTypeAll.
    /// </summary>
    private static GameObject FindPrefabByName(string name)
    {
        try
        {
            // Search all loaded GameObjects
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            foreach (var obj in allObjects)
            {
                if (obj != null && obj.name == name)
                {
                    // Verify it's a prefab (not an instance)
                    // Prefabs typically have hideFlags or are in asset bundles
                    return obj;
                }
            }

            // Also try with common suffixes/prefixes
            var variants = new[]
            {
                name,
                name + "_Prefab",
                "Prefab_" + name,
                name.Replace("_", ""),
                name.Replace(" ", "_")
            };

            foreach (var variant in variants)
            {
                foreach (var obj in allObjects)
                {
                    if (obj != null && obj.name.Equals(variant, StringComparison.OrdinalIgnoreCase))
                        return obj;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("AssetResolver.FindPrefabByName", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Get all available prefabs matching a category.
    /// Categories: Buildings, Props, Cover, Features
    /// </summary>
    public static string[] GetPrefabsInCategory(string category)
    {
        if (_categoryCache.TryGetValue(category, out var cached))
            return cached;

        if (!_catalogBuilt)
            BuildAssetCatalog();

        return _categoryCache.TryGetValue(category, out var result)
            ? result
            : Array.Empty<string>();
    }

    /// <summary>
    /// Build a catalog of all available prefabs organized by category.
    /// </summary>
    public static void BuildAssetCatalog()
    {
        if (_catalogBuilt)
            return;

        try
        {
            var buildings = new List<string>();
            var props = new List<string>();
            var cover = new List<string>();
            var features = new List<string>();

            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            foreach (var obj in allObjects)
            {
                if (obj == null)
                    continue;

                var name = obj.name;

                // Categorize based on naming conventions
                // These patterns will be refined based on actual game assets
                if (IsBuilding(name))
                    buildings.Add($"Buildings/{name}");
                else if (IsCover(name))
                    cover.Add($"Cover/{name}");
                else if (IsFeature(name))
                    features.Add($"Features/{name}");
                else if (IsProp(name))
                    props.Add($"Props/{name}");
            }

            _categoryCache["Buildings"] = buildings.Distinct().OrderBy(x => x).ToArray();
            _categoryCache["Props"] = props.Distinct().OrderBy(x => x).ToArray();
            _categoryCache["Cover"] = cover.Distinct().OrderBy(x => x).ToArray();
            _categoryCache["Features"] = features.Distinct().OrderBy(x => x).ToArray();

            _catalogBuilt = true;

            SdkLogger.Msg($"[AssetResolver] Catalog built: {buildings.Count} buildings, " +
                $"{props.Count} props, {cover.Count} cover, {features.Count} features");
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("AssetResolver.BuildAssetCatalog",
                "Failed to build catalog", ex);
        }
    }

    /// <summary>
    /// Get all categories.
    /// </summary>
    public static string[] GetCategories()
    {
        return new[] { "Buildings", "Props", "Cover", "Features" };
    }

    /// <summary>
    /// Clear the prefab cache.
    /// </summary>
    public static void ClearCache()
    {
        _prefabCache.Clear();
        _categoryCache.Clear();
        _catalogBuilt = false;
    }

    /// <summary>
    /// Search for prefabs matching a pattern.
    /// </summary>
    public static string[] SearchPrefabs(string pattern)
    {
        if (!_catalogBuilt)
            BuildAssetCatalog();

        var results = new List<string>();
        var lowerPattern = pattern?.ToLowerInvariant() ?? "";

        foreach (var category in _categoryCache.Values)
        {
            foreach (var prefab in category)
            {
                if (prefab.ToLowerInvariant().Contains(lowerPattern))
                    results.Add(prefab);
            }
        }

        return results.ToArray();
    }

    // ==================== Classification Helpers ====================
    // These will be refined based on actual game asset naming

    private static bool IsBuilding(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.Contains("building") ||
               lower.Contains("structure") ||
               lower.Contains("house") ||
               lower.Contains("outpost") ||
               lower.Contains("bunker") ||
               lower.Contains("tower") ||
               lower.Contains("chunk");
    }

    private static bool IsCover(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.Contains("cover") ||
               lower.Contains("barrier") ||
               lower.Contains("sandbag") ||
               lower.Contains("wall_low");
    }

    private static bool IsFeature(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.Contains("rock") ||
               lower.Contains("boulder") ||
               lower.Contains("tree") ||
               lower.Contains("vegetation") ||
               lower.Contains("cliff") ||
               lower.Contains("terrain");
    }

    private static bool IsProp(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.Contains("prop") ||
               lower.Contains("debris") ||
               lower.Contains("barrel") ||
               lower.Contains("crate") ||
               lower.Contains("decor") ||
               lower.Contains("vehicle") ||
               lower.Contains("trash");
    }

    /// <summary>
    /// Register console commands for asset browsing.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // listassets [category] - List available prefabs
        DevConsole.RegisterCommand("listassets", "[category]",
            "List available prefabs (Buildings, Props, Cover, Features)", args =>
        {
            if (!_catalogBuilt)
                BuildAssetCatalog();

            if (args.Length == 0)
            {
                // Show summary
                var lines = new List<string> { "Asset Categories:" };
                foreach (var cat in GetCategories())
                {
                    var count = GetPrefabsInCategory(cat).Length;
                    lines.Add($"  {cat}: {count} prefabs");
                }
                lines.Add("\nUse: listassets <category> to see details");
                return string.Join("\n", lines);
            }

            var category = args[0];
            var prefabs = GetPrefabsInCategory(category);
            if (prefabs.Length == 0)
                return $"No prefabs in category: {category}";

            var result = new List<string> { $"{category} ({prefabs.Length}):" };
            foreach (var prefab in prefabs.Take(50))
                result.Add($"  {prefab}");

            if (prefabs.Length > 50)
                result.Add($"  ... and {prefabs.Length - 50} more");

            return string.Join("\n", result);
        });

        // searchassets <pattern> - Search for prefabs
        DevConsole.RegisterCommand("searchassets", "<pattern>",
            "Search for prefabs by name pattern", args =>
        {
            if (args.Length == 0)
                return "Usage: searchassets <pattern>";

            var results = SearchPrefabs(args[0]);
            if (results.Length == 0)
                return $"No prefabs matching: {args[0]}";

            var lines = new List<string> { $"Found {results.Length} matches:" };
            foreach (var prefab in results.Take(30))
                lines.Add($"  {prefab}");

            if (results.Length > 30)
                lines.Add($"  ... and {results.Length - 30} more");

            return string.Join("\n", lines);
        });

        // rebuildcatalog - Force rebuild of asset catalog
        DevConsole.RegisterCommand("rebuildcatalog", "",
            "Rebuild the asset catalog", args =>
        {
            ClearCache();
            BuildAssetCatalog();
            return "Asset catalog rebuilt";
        });
    }
}
