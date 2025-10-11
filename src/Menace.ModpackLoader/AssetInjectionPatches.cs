using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Menace.ModpackLoader;

/// <summary>
/// Harmony patches for intercepting Unity asset loading and injecting modpack assets
/// </summary>
public static class AssetInjectionPatches
{
    private static readonly Dictionary<string, Texture2D> _textureCache = new();
    private static readonly Dictionary<string, Sprite> _spriteCache = new();
    private static ModpackLoaderMod _modInstance;

    public static void Initialize(ModpackLoaderMod modInstance)
    {
        _modInstance = modInstance;
    }

    /// <summary>
    /// Hook Resources.Load to replace assets with modpack versions
    /// </summary>
    [HarmonyPatch(typeof(Resources), nameof(Resources.Load), typeof(string))]
    public static class ResourcesLoadPatch
    {
        public static void Postfix(string path, ref UnityEngine.Object __result)
        {
            if (__result == null)
                return;

            try
            {
                var assetType = __result.GetType();

                // Check if we have a replacement for this asset
                if (assetType == typeof(Texture2D))
                {
                    var replacement = GetTextureReplacement(path);
                    if (replacement != null)
                    {
                        __result = replacement;
                        MelonLogger.Msg($"Replaced Texture2D: {path}");
                    }
                }
                else if (assetType == typeof(Sprite))
                {
                    var replacement = GetSpriteReplacement(path);
                    if (replacement != null)
                    {
                        __result = replacement;
                        MelonLogger.Msg($"Replaced Sprite: {path}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ResourcesLoadPatch: {ex.Message}");
            }
        }
    }

    private static Texture2D GetTextureReplacement(string assetPath)
    {
        // Check cache first
        if (_textureCache.TryGetValue(assetPath, out var cached))
            return cached;

        // Look for replacement in loaded modpacks
        // For now, we would need to pass the modpack data here
        // This is a placeholder for the actual implementation

        return null;
    }

    private static Sprite GetSpriteReplacement(string assetPath)
    {
        // Check cache first
        if (_spriteCache.TryGetValue(assetPath, out var cached))
            return cached;

        // Load texture first
        var texture = GetTextureReplacement(assetPath);
        if (texture != null)
        {
            // Create sprite from texture
            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );
            _spriteCache[assetPath] = sprite;
            return sprite;
        }

        return null;
    }

    public static Texture2D LoadTextureFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var texture = new Texture2D(2, 2);
            ImageConversion.LoadImage(texture, bytes);
            return texture;
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Failed to load texture from {filePath}: {ex.Message}");
            return null;
        }
    }
}
