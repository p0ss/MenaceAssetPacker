using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Il2CppInterop.Runtime;
using Menace.SDK;
using UnityEngine;

namespace Menace.ModpackLoader;

/// <summary>
/// Loads compiled assets from resources.assets using the asset manifest.
/// The manifest (asset-manifest.json) is written by BundleCompiler and lists
/// all compiled assets with their resource paths, enabling enumeration by type.
/// </summary>
public static class CompiledAssetLoader
{
    private const string ManifestFileName = "asset-manifest.json";

    // Asset registry: name -> loaded asset
    private static readonly Dictionary<string, UnityEngine.Object> _assetsByName
        = new(StringComparer.OrdinalIgnoreCase);

    // Type+name registry: "TypeName:assetName" -> asset
    private static readonly Dictionary<string, UnityEngine.Object> _assetsByTypeAndName
        = new(StringComparer.OrdinalIgnoreCase);

    // Loaded manifest
    private static CompiledAssetManifest _manifest;

    /// <summary>
    /// Number of successfully loaded assets.
    /// </summary>
    public static int LoadedAssetCount => _assetsByTypeAndName.Count;

    /// <summary>
    /// Load the asset manifest from the specified directory.
    /// Call LoadAssets() later when Unity is ready to actually load the assets.
    /// </summary>
    public static void LoadManifest(string compiledDir)
    {
        var manifestPath = Path.Combine(compiledDir, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            SdkLogger.Msg($"[CompiledAssetLoader] No manifest found at {manifestPath}");
            return;
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            _manifest = JsonSerializer.Deserialize<CompiledAssetManifest>(json, DeserializeOptions);

            if (_manifest?.Assets == null || _manifest.Assets.Count == 0)
            {
                SdkLogger.Msg("[CompiledAssetLoader] Manifest is empty or invalid");
                return;
            }

            SdkLogger.Msg($"[CompiledAssetLoader] Loaded manifest with {_manifest.Assets.Count} asset(s)");
        }
        catch (Exception ex)
        {
            SdkLogger.Error($"[CompiledAssetLoader] Failed to load manifest: {ex.Message}");
        }
    }

    /// <summary>
    /// Whether the manifest has been loaded.
    /// </summary>
    public static bool HasManifest => _manifest?.Assets?.Count > 0;

    /// <summary>
    /// Number of assets in the manifest (whether loaded or not).
    /// </summary>
    public static int ManifestAssetCount => _manifest?.Assets?.Count ?? 0;

    /// <summary>
    /// Load all assets from the manifest via Resources.Load().
    /// Call this after Unity is initialized (e.g., during scene load).
    /// </summary>
    public static void LoadAssets()
    {
        if (_manifest?.Assets == null || _manifest.Assets.Count == 0)
        {
            return;
        }

        if (_assetsLoaded)
        {
            return;
        }

        _assetsLoaded = true;
        LoadAssetsFromManifest();
    }

    private static bool _assetsLoaded = false;

    /// <summary>
    /// Load all assets listed in the manifest via Resources.Load().
    /// </summary>
    private static void LoadAssetsFromManifest()
    {
        if (_manifest?.Assets == null) return;

        int loaded = 0;
        int failed = 0;
        int skippedClones = 0;

        SdkLogger.Msg($"[CompiledAssetLoader] Beginning asset load for {_manifest.Assets.Count} manifest entries...");

        foreach (var entry in _manifest.Assets)
        {
            // Skip clones - they're loaded by the game natively via DataTemplateLoader
            // We don't need to load them ourselves; they're already in resources.assets
            if (entry.Category == AssetCategory.Clone)
            {
                skippedClones++;
                continue;
            }

            if (string.IsNullOrEmpty(entry.ResourcePath))
            {
                SdkLogger.Warning($"  [CompiledAssetLoader] Skipping '{entry.Name}': no resource path");
                failed++;
                continue;
            }

            try
            {
                // Get the IL2CPP type for Resources.Load
                var il2cppType = GetIl2CppType(entry.Type);
                if (il2cppType == null)
                {
                    SdkLogger.Warning($"  [CompiledAssetLoader] Unknown type '{entry.Type}' for '{entry.Name}'");
                    failed++;
                    continue;
                }

                SdkLogger.Msg($"  [CompiledAssetLoader] Attempting: Resources.Load(\"{entry.ResourcePath}\", {entry.Type})");

                var asset = Resources.Load(entry.ResourcePath, il2cppType);
                if (asset == null)
                {
                    // Try without leading "data/" if present
                    if (entry.ResourcePath.StartsWith("data/"))
                    {
                        var altPath = entry.ResourcePath.Substring(5);
                        SdkLogger.Msg($"  [CompiledAssetLoader] Retrying with alt path: {altPath}");
                        asset = Resources.Load(altPath, il2cppType);
                    }
                }

                if (asset == null)
                {
                    SdkLogger.Warning($"  [CompiledAssetLoader] FAILED to load '{entry.Name}' from '{entry.ResourcePath}' - Resources.Load returned null");
                    failed++;
                    continue;
                }

                // Verify asset is valid
                var assetValid = VerifyAsset(asset, entry.Type, entry.Name);
                if (!assetValid)
                {
                    SdkLogger.Warning($"  [CompiledAssetLoader] Asset '{entry.Name}' loaded but FAILED validation");
                    failed++;
                    continue;
                }

                // Register in lookups
                _assetsByName[entry.Name] = asset;
                var key = $"{entry.Type}:{entry.Name}";
                _assetsByTypeAndName[key] = asset;

                SdkLogger.Msg($"  [CompiledAssetLoader] SUCCESS: {entry.Name} ({entry.Type})");
                loaded++;
            }
            catch (Exception ex)
            {
                SdkLogger.Error($"  [CompiledAssetLoader] EXCEPTION loading '{entry.Name}': {ex.GetType().Name}: {ex.Message}");
                SdkLogger.Error($"    Stack: {ex.StackTrace}");
                failed++;
            }
        }

        SdkLogger.Msg($"[CompiledAssetLoader] Asset loading complete: {loaded} loaded, {failed} failed, {skippedClones} clones (loaded by game)");
    }

    /// <summary>
    /// Verify that a loaded asset is actually valid and usable.
    /// </summary>
    private static bool VerifyAsset(UnityEngine.Object asset, string typeName, string assetName)
    {
        try
        {
            switch (typeName)
            {
                case "Texture2D":
                    var tex = asset.Cast<Texture2D>();
                    if (tex.width <= 0 || tex.height <= 0)
                    {
                        SdkLogger.Warning($"    [Verify] Texture2D '{assetName}' has invalid dimensions: {tex.width}x{tex.height}");
                        return false;
                    }
                    SdkLogger.Msg($"    [Verify] Texture2D '{assetName}': {tex.width}x{tex.height}, format={tex.format}");
                    return true;

                case "Sprite":
                    var sprite = asset.Cast<Sprite>();
                    var rect = sprite.rect;
                    if (rect.width <= 0 || rect.height <= 0)
                    {
                        SdkLogger.Warning($"    [Verify] Sprite '{assetName}' has invalid rect: {rect.width}x{rect.height}");
                        return false;
                    }
                    SdkLogger.Msg($"    [Verify] Sprite '{assetName}': {rect.width}x{rect.height}");
                    return true;

                case "AudioClip":
                    var clip = asset.Cast<AudioClip>();
                    if (clip.length <= 0)
                    {
                        SdkLogger.Warning($"    [Verify] AudioClip '{assetName}' has invalid length: {clip.length}");
                        return false;
                    }
                    SdkLogger.Msg($"    [Verify] AudioClip '{assetName}': {clip.length}s, {clip.channels}ch, {clip.frequency}Hz");
                    return true;

                case "Mesh":
                    var mesh = asset.Cast<Mesh>();
                    if (mesh.vertexCount <= 0)
                    {
                        SdkLogger.Warning($"    [Verify] Mesh '{assetName}' has no vertices");
                        return false;
                    }
                    SdkLogger.Msg($"    [Verify] Mesh '{assetName}': {mesh.vertexCount} vertices");
                    return true;

                default:
                    // Can't verify, assume OK
                    return true;
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"    [Verify] Exception verifying '{assetName}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get an IL2CPP Type for a Unity type name.
    /// </summary>
    private static Il2CppSystem.Type GetIl2CppType(string typeName)
    {
        return typeName switch
        {
            "Texture2D" => Il2CppType.From(typeof(Texture2D)),
            "Sprite" => Il2CppType.From(typeof(Sprite)),
            "AudioClip" => Il2CppType.From(typeof(AudioClip)),
            "Mesh" => Il2CppType.From(typeof(Mesh)),
            "Material" => Il2CppType.From(typeof(Material)),
            "GameObject" => Il2CppType.From(typeof(GameObject)),
            "MonoBehaviour" => Il2CppType.From(typeof(ScriptableObject)),
            _ => null
        };
    }

    /// <summary>
    /// Get a loaded asset by name. Returns the first match regardless of type.
    /// </summary>
    public static UnityEngine.Object GetAsset(string name)
    {
        _assetsByName.TryGetValue(name, out var asset);
        return asset;
    }

    /// <summary>
    /// Get a loaded asset by name, cast to the specified type.
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

        // Fallback: search by name
        if (_assetsByName.TryGetValue(name, out var byName))
        {
            try { return byName.Cast<T>(); }
            catch { return null; }
        }

        return null;
    }

    /// <summary>
    /// Get all loaded assets of a specific type.
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

        return results;
    }

    /// <summary>
    /// Get all manifest entries for clones (for registration with DataTemplateLoader).
    /// </summary>
    public static IEnumerable<CompiledAssetEntry> GetCloneEntries()
    {
        if (_manifest?.Assets == null)
            yield break;

        foreach (var entry in _manifest.Assets)
        {
            if (entry.Category == AssetCategory.Clone)
                yield return entry;
        }
    }

    /// <summary>
    /// Check if any assets have been loaded.
    /// </summary>
    public static bool HasLoadedAssets => _assetsByTypeAndName.Count > 0;

    /// <summary>
    /// Clear all loaded assets.
    /// </summary>
    public static void Clear()
    {
        _assetsByName.Clear();
        _assetsByTypeAndName.Clear();
        _manifest = null;
    }

    // JSON deserialization options
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // =========================================================================
    // Minimal manifest model (duplicated from Core since we can't reference it)
    // =========================================================================

    public class CompiledAssetManifest
    {
        public int Version { get; set; }
        public DateTime CompiledAt { get; set; }
        public List<CompiledAssetEntry> Assets { get; set; } = new();
    }

    public class CompiledAssetEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string ResourcePath { get; set; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AssetCategory Category { get; set; }

        public string SourceTemplate { get; set; }
        public string TemplateType { get; set; }
    }

    public enum AssetCategory
    {
        Clone,
        Texture,
        Sprite,
        Audio,
        Mesh,
        Material,
        Prefab
    }
}
