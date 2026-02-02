using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

[assembly: MelonInfo(typeof(Menace.ModpackLoader.ModpackLoaderMod), "Menace Modpack Loader", "2.0.0", "Menace Modkit")]
[assembly: MelonGame(null, null)]

namespace Menace.ModpackLoader;

public partial class ModpackLoaderMod : MelonMod
{
    private readonly Dictionary<string, Modpack> _loadedModpacks = new();
    private readonly HashSet<string> _registeredAssetPaths = new();
    private bool _templatesLoaded = false;

    public override void OnInitializeMelon()
    {
        LoggerInstance.Msg("Menace Modpack Loader v2.0 initialized");
        LoadModpacks();
        DllLoader.InitializeAllPlugins();
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        DllLoader.NotifySceneLoaded(buildIndex, sceneName);

        if (!_templatesLoaded)
        {
            LoggerInstance.Msg($"First scene '{sceneName}' loaded, waiting for templates...");
            MelonCoroutines.Start(WaitForTemplatesAndApply());
        }

        // Apply asset replacements after every scene load (assets get reloaded per scene)
        if (AssetReplacer.RegisteredCount > 0 || BundleLoader.LoadedAssetCount > 0)
        {
            MelonCoroutines.Start(ApplyAssetReplacementsDelayed(sceneName));
        }
    }

    public override void OnUpdate()
    {
        DllLoader.NotifyUpdate();
    }

    public override void OnGUI()
    {
        DllLoader.NotifyOnGUI();
    }

    private System.Collections.IEnumerator WaitForTemplatesAndApply()
    {
        // Wait a few frames for the game to initialize templates
        for (int i = 0; i < 30; i++)
        {
            yield return null;
        }

        LoggerInstance.Msg("Applying modpack modifications...");
        ApplyAllModpacks();
        _templatesLoaded = true;
    }

    private void LoadModpacks()
    {
        var modsPath = Path.Combine(Directory.GetCurrentDirectory(), "Mods");
        if (!Directory.Exists(modsPath))
        {
            LoggerInstance.Warning($"Mods directory not found: {modsPath}");
            return;
        }

        LoggerInstance.Msg($"Loading modpacks from: {modsPath}");

        var modpackFiles = Directory.GetFiles(modsPath, "modpack.json", SearchOption.AllDirectories);

        // Sort by load order (parse manifestVersion to determine format)
        var modpackEntries = new List<(string file, int order, int version)>();

        foreach (var modpackFile in modpackFiles)
        {
            try
            {
                var json = File.ReadAllText(modpackFile);
                var obj = JObject.Parse(json);
                var manifestVersion = obj.Value<int?>("manifestVersion") ?? 1;
                var loadOrder = obj.Value<int?>("loadOrder") ?? 100;
                modpackEntries.Add((modpackFile, loadOrder, manifestVersion));
            }
            catch
            {
                modpackEntries.Add((modpackFile, 100, 1));
            }
        }

        // Load in order
        foreach (var (modpackFile, _, manifestVersion) in modpackEntries.OrderBy(e => e.order))
        {
            try
            {
                var modpackDir = Path.GetDirectoryName(modpackFile);
                var json = File.ReadAllText(modpackFile);
                var modpack = JsonConvert.DeserializeObject<Modpack>(json);

                if (modpack != null)
                {
                    modpack.DirectoryPath = modpackDir;
                    modpack.ManifestVersion = manifestVersion;
                    _loadedModpacks[modpack.Name] = modpack;

                    var vLabel = manifestVersion >= 2 ? "v2" : "v1 (legacy)";
                    LoggerInstance.Msg($"  Loaded [{vLabel}]: {modpack.Name} v{modpack.Version} (order: {modpack.LoadOrder})");

                    // V2: Load bundles and DLLs
                    if (manifestVersion >= 2 && !string.IsNullOrEmpty(modpackDir))
                    {
                        BundleLoader.LoadBundles(modpackDir, modpack.Name);
                        DllLoader.LoadModDlls(modpackDir, modpack.Name, modpack.SecurityStatus ?? "Unreviewed");
                    }

                    // Load asset replacements (both V1 and V2)
                    if (modpack.Assets != null)
                    {
                        LoadModpackAssets(modpack);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Failed to load modpack from {modpackFile}: {ex.Message}");
            }
        }

        LoggerInstance.Msg($"Loaded {_loadedModpacks.Count} modpack(s)");
    }

    private void LoadModpackAssets(Modpack modpack)
    {
        if (modpack.Assets == null || string.IsNullOrEmpty(modpack.DirectoryPath))
            return;

        foreach (var (assetPath, replacementFile) in modpack.Assets)
        {
            try
            {
                var fullPath = Path.Combine(modpack.DirectoryPath, replacementFile);
                if (File.Exists(fullPath))
                {
                    _registeredAssetPaths.Add(assetPath);
                    AssetReplacer.RegisterAssetReplacement(assetPath, fullPath);
                    LoggerInstance.Msg($"  Registered asset replacement: {assetPath}");
                }
                else
                {
                    LoggerInstance.Warning($"  Asset file not found: {fullPath}");
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"  Failed to load asset {assetPath}: {ex.Message}");
            }
        }
    }

    private void ApplyAllModpacks()
    {
        if (_loadedModpacks.Count == 0)
        {
            LoggerInstance.Msg("No modpacks to apply");
            return;
        }

        foreach (var modpack in _loadedModpacks.Values.OrderBy(m => m.LoadOrder))
        {
            // V2 modpacks with bundles: assets are already loaded via AssetBundle.LoadFromFile
            // V2 bundles override templates via Unity's native deserialization — no reflection needed.
            // However, V2 "patches" still need the legacy template injection path for now,
            // until the full bundle compiler (Phase 5) produces real asset bundles.

            // Use "patches" for V2, "templates" for V1
            var hasPatches = modpack.Patches != null && modpack.Patches.Count > 0;
            var hasTemplates = modpack.Templates != null && modpack.Templates.Count > 0;

            if (!hasPatches && !hasTemplates)
            {
                LoggerInstance.Msg($"Modpack '{modpack.Name}' has no template modifications");
                continue;
            }

            LoggerInstance.Msg($"Applying modpack: {modpack.Name}");

            if (hasPatches && modpack.ManifestVersion >= 2)
            {
                // V2 patches use the same runtime reflection approach for now
                // Convert patches format to legacy templates format for applying
                ApplyModpackPatches(modpack);
            }
            else if (hasTemplates)
            {
                // V1 legacy path
                ApplyModpackTemplates(modpack);
            }
        }
    }

    /// <summary>
    /// Apply V2-format patches (same field-level injection as V1, different JSON structure).
    /// </summary>
    private void ApplyModpackPatches(Modpack modpack)
    {
        // V2 "patches" has the same structure as V1 "templates":
        // templateType → instanceName → field → value
        // We reuse the same application logic.
        if (modpack.Patches == null) return;

        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (gameAssembly == null)
        {
            LoggerInstance.Error("Assembly-CSharp not found, cannot apply patches");
            return;
        }

        foreach (var (templateTypeName, templateInstances) in modpack.Patches)
        {
            try
            {
                var templateType = gameAssembly.GetTypes()
                    .FirstOrDefault(t => t.Name == templateTypeName && !t.IsAbstract);

                if (templateType == null)
                {
                    LoggerInstance.Warning($"  Template type '{templateTypeName}' not found");
                    continue;
                }

                var il2cppType = Il2CppType.From(templateType);
                var objects = Resources.FindObjectsOfTypeAll(il2cppType);

                if (objects == null || objects.Length == 0)
                {
                    LoggerInstance.Warning($"  No {templateTypeName} instances found");
                    continue;
                }

                int appliedCount = 0;
                foreach (var obj in objects)
                {
                    if (obj == null) continue;
                    var templateName = obj.name;
                    if (templateInstances.ContainsKey(templateName))
                    {
                        var modifications = templateInstances[templateName];
                        ApplyTemplateModifications(obj, templateType, modifications);
                        appliedCount++;
                    }
                }

                if (appliedCount > 0)
                    LoggerInstance.Msg($"  Applied patches to {appliedCount} {templateTypeName} instance(s)");
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"  Failed to apply patches to {templateTypeName}: {ex.Message}");
            }
        }
    }

    private void ApplyModpackTemplates(Modpack modpack)
    {
        var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

        if (gameAssembly == null)
        {
            LoggerInstance.Error("Assembly-CSharp not found, cannot apply modpack");
            return;
        }

        foreach (var (templateTypeName, templateInstances) in modpack.Templates)
        {
            try
            {
                var templateType = gameAssembly.GetTypes()
                    .FirstOrDefault(t => t.Name == templateTypeName && !t.IsAbstract);

                if (templateType == null)
                {
                    LoggerInstance.Warning($"  Template type '{templateTypeName}' not found in Assembly-CSharp");
                    continue;
                }

                var il2cppType = Il2CppType.From(templateType);
                var objects = Resources.FindObjectsOfTypeAll(il2cppType);

                if (objects == null || objects.Length == 0)
                {
                    LoggerInstance.Warning($"  No {templateTypeName} instances found in game");
                    continue;
                }

                int appliedCount = 0;
                foreach (var obj in objects)
                {
                    if (obj == null) continue;
                    var templateName = obj.name;
                    if (templateInstances.ContainsKey(templateName))
                    {
                        var modifications = templateInstances[templateName];
                        ApplyTemplateModifications(obj, templateType, modifications);
                        appliedCount++;
                    }
                }

                if (appliedCount > 0)
                {
                    LoggerInstance.Msg($"  Applied modifications to {appliedCount} {templateTypeName} instance(s)");
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"  Failed to apply modifications to {templateTypeName}: {ex.Message}");
            }
        }
    }

    private System.Collections.IEnumerator ApplyAssetReplacementsDelayed(string sceneName)
    {
        LoggerInstance.Msg($"Asset replacement queued for scene: {sceneName} ({AssetReplacer.RegisteredCount} disk, {BundleLoader.LoadedAssetCount} bundle)");

        // Wait frames for textures to finish loading
        for (int i = 0; i < 15; i++)
            yield return null;

        LoggerInstance.Msg($"Applying asset replacements for scene: {sceneName}");
        AssetReplacer.ApplyAllReplacements();
    }

    // ApplyTemplateModifications is implemented in TemplateInjection.cs (partial class)
}

public class Modpack
{
    [JsonProperty("manifestVersion")]
    public int ManifestVersion { get; set; } = 1;

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("version")]
    public string Version { get; set; }

    [JsonProperty("author")]
    public string Author { get; set; }

    [JsonProperty("loadOrder")]
    public int LoadOrder { get; set; } = 100;

    /// <summary>
    /// V1 format: template modifications
    /// </summary>
    [JsonProperty("templates")]
    public Dictionary<string, Dictionary<string, Dictionary<string, object>>> Templates { get; set; }

    /// <summary>
    /// V2 format: data patches (same structure as templates, preferred in V2)
    /// </summary>
    [JsonProperty("patches")]
    public Dictionary<string, Dictionary<string, Dictionary<string, object>>> Patches { get; set; }

    [JsonProperty("assets")]
    public Dictionary<string, string> Assets { get; set; }

    [JsonProperty("bundles")]
    public List<string> Bundles { get; set; }

    [JsonProperty("securityStatus")]
    public string SecurityStatus { get; set; }

    [JsonIgnore]
    public string DirectoryPath { get; set; }
}
