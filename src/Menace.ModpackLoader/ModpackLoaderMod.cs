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

[assembly: MelonInfo(typeof(Menace.ModpackLoader.ModpackLoaderMod), "Menace Modpack Loader", "1.0.0", "Menace Modkit")]
[assembly: MelonGame(null, null)]

namespace Menace.ModpackLoader;

public partial class ModpackLoaderMod : MelonMod
{
    private readonly Dictionary<string, Modpack> _loadedModpacks = new();
    private readonly Dictionary<string, Texture2D> _assetReplacements = new();
    private bool _templatesLoaded = false;

    public override void OnInitializeMelon()
    {
        LoggerInstance.Msg("Menace Modpack Loader initialized");
        LoadModpacks();
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (!_templatesLoaded && sceneName == "MainMenu")
        {
            LoggerInstance.Msg("MainMenu loaded, waiting for templates...");
            MelonCoroutines.Start(WaitForTemplatesAndApply());
        }
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

        // Look for modpack.json files in subdirectories
        var modpackFiles = Directory.GetFiles(modsPath, "modpack.json", SearchOption.AllDirectories);

        foreach (var modpackFile in modpackFiles)
        {
            try
            {
                var modpackDir = Path.GetDirectoryName(modpackFile);
                var json = File.ReadAllText(modpackFile);
                var modpack = JsonConvert.DeserializeObject<Modpack>(json);

                if (modpack != null)
                {
                    modpack.DirectoryPath = modpackDir;
                    _loadedModpacks[modpack.Name] = modpack;
                    LoggerInstance.Msg($"✓ Loaded modpack: {modpack.Name} v{modpack.Version}");

                    // Load asset replacements
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
                    // For now, just register the path. We'll load textures on demand.
                    _assetReplacements[assetPath] = null;
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

        // Find all template objects
        foreach (var modpack in _loadedModpacks.Values)
        {
            if (modpack.Templates == null || modpack.Templates.Count == 0)
            {
                LoggerInstance.Msg($"Modpack '{modpack.Name}' has no template modifications");
                continue;
            }

            LoggerInstance.Msg($"Applying modpack: {modpack.Name}");
            ApplyModpackTemplates(modpack);
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
                // Find the proxy type from Assembly-CSharp
                var templateType = gameAssembly.GetTypes()
                    .FirstOrDefault(t => t.Name == templateTypeName && !t.IsAbstract);

                if (templateType == null)
                {
                    LoggerInstance.Warning($"  Template type '{templateTypeName}' not found in Assembly-CSharp");
                    continue;
                }

                // Find all instances of this specific type
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

    // ApplyTemplateModifications is implemented in TemplateInjection.cs (partial class)
}

public class Modpack
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("version")]
    public string Version { get; set; }

    [JsonProperty("author")]
    public string Author { get; set; }

    [JsonProperty("templates")]
    public Dictionary<string, Dictionary<string, Dictionary<string, object>>> Templates { get; set; }

    [JsonProperty("assets")]
    public Dictionary<string, string> Assets { get; set; }

    [JsonIgnore]
    public string DirectoryPath { get; set; }
}
