using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;

namespace Menace.ModpackLoader;

/// <summary>
/// Loads compiled mod DLLs from deployed modpacks, discovers IModpackPlugin
/// implementations, and forwards lifecycle events to them.
/// </summary>
public static class DllLoader
{
    private static readonly List<Assembly> _loadedAssemblies = new();
    private static readonly List<PluginInstance> _loadedPlugins = new();

    private class PluginInstance
    {
        public IModpackPlugin Plugin { get; }
        public string ModpackName { get; }
        public string TypeName { get; }
        public HarmonyLib.Harmony Harmony { get; }
        public string ModId => $"{ModpackName}.{TypeName}";

        public PluginInstance(IModpackPlugin plugin, string modpackName, string typeName, HarmonyLib.Harmony harmony)
        {
            Plugin = plugin;
            ModpackName = modpackName;
            TypeName = typeName;
            Harmony = harmony;
        }
    }

    /// <summary>
    /// Controls whether unverified DLLs are loaded. Set to true only if user explicitly approves.
    /// </summary>
    public static bool AllowUnverifiedDlls { get; set; } = false;

    /// <summary>
    /// Load all DLLs found in a modpack's dlls/ directory and discover IModpackPlugin implementations.
    /// </summary>
    /// <param name="modpackDir">Path to the modpack directory.</param>
    /// <param name="modpackName">Name of the modpack for logging.</param>
    /// <param name="securityStatus">Security verification status of the modpack.</param>
    /// <param name="forceLoad">If true, loads even unverified DLLs regardless of AllowUnverifiedDlls setting.</param>
    public static void LoadModDlls(string modpackDir, string modpackName, string securityStatus, bool forceLoad = false)
    {
        var dllDir = Path.Combine(modpackDir, "dlls");
        if (!Directory.Exists(dllDir))
            return;

        var dllFiles = Directory.GetFiles(dllDir, "*.dll");
        if (dllFiles.Length == 0)
            return;

        // Security check: refuse to load unverified DLLs unless explicitly approved
        var isUnverified = securityStatus != "SourceVerified" && securityStatus != "SourceWithWarnings";
        if (isUnverified && !forceLoad && !AllowUnverifiedDlls)
        {
            MelonLogger.Warning($"  [{modpackName}] Skipping {dllFiles.Length} unverified DLL(s). " +
                "Set AllowUnverifiedDlls=true or forceLoad=true to override.");
            return;
        }

        foreach (var dllPath in dllFiles)
        {
            try
            {
                var assembly = Assembly.LoadFrom(dllPath);
                _loadedAssemblies.Add(assembly);

                var trustLabel = securityStatus switch
                {
                    "SourceVerified" => "source-verified",
                    "SourceWithWarnings" => "source (warnings)",
                    _ => "UNVERIFIED"
                };

                MelonLogger.Msg($"  [{modpackName}] Loaded DLL: {Path.GetFileName(dllPath)} [{trustLabel}]");

                DiscoverPlugins(assembly, modpackName);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"  [{modpackName}] Failed to load DLL {Path.GetFileName(dllPath)}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Scan an assembly for IModpackPlugin implementations and instantiate them.
    /// </summary>
    private static void DiscoverPlugins(Assembly assembly, string modpackName)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // IL2CPP proxy assemblies may have types that can't be loaded
            types = ex.Types.Where(t => t != null).ToArray()!;
            MelonLogger.Warning($"  [{modpackName}] Some types could not be loaded from {assembly.GetName().Name}, scanning partial type list");
        }

        foreach (var type in types)
        {
            if (!typeof(IModpackPlugin).IsAssignableFrom(type))
                continue;
            if (type.IsInterface || type.IsAbstract)
                continue;
            if (type.GetConstructor(Type.EmptyTypes) == null)
                continue;

            try
            {
                var plugin = (IModpackPlugin)Activator.CreateInstance(type)!;
                var assemblyName = assembly.GetName().Name;
                var harmonyId = $"com.menace.modpack.{assemblyName}.{type.Name}";
                var harmony = new HarmonyLib.Harmony(harmonyId);

                _loadedPlugins.Add(new PluginInstance(plugin, modpackName, type.Name, harmony));

                MelonLogger.Msg($"  [{modpackName}] Discovered plugin: {type.Name}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"  [{modpackName}] Failed to instantiate plugin {type.Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Call OnInitialize on all discovered plugins, providing each with a logger and its harmony instance.
    /// </summary>
    public static void InitializeAllPlugins()
    {
        foreach (var p in _loadedPlugins)
        {
            try
            {
                var logger = new MelonLogger.Instance(p.ModpackName);
                p.Plugin.OnInitialize(logger, p.Harmony);
                MelonLogger.Msg($"  [{p.ModpackName}] Initialized plugin: {p.TypeName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"  [{p.ModpackName}] Failed to initialize plugin {p.TypeName}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Forward scene-loaded events to all plugins.
    /// </summary>
    public static void NotifySceneLoaded(int buildIndex, string sceneName)
    {
        foreach (var p in _loadedPlugins)
        {
            try
            {
                p.Plugin.OnSceneLoaded(buildIndex, sceneName);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"  [{p.ModpackName}] Plugin {p.TypeName} OnSceneLoaded failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Forward per-frame update to all plugins.
    /// </summary>
    public static void NotifyUpdate()
    {
        foreach (var p in _loadedPlugins)
        {
            try
            {
                p.Plugin.OnUpdate();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"  [{p.ModpackName}] Plugin {p.TypeName} OnUpdate failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Forward IMGUI draw calls to all plugins.
    /// </summary>
    public static void NotifyOnGUI()
    {
        foreach (var p in _loadedPlugins)
        {
            try
            {
                p.Plugin.OnGUI();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"  [{p.ModpackName}] Plugin {p.TypeName} OnGUI failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Forward unload notification to all plugins (e.g., for hot-reload or shutdown).
    /// </summary>
    public static void NotifyUnload()
    {
        foreach (var p in _loadedPlugins)
        {
            try
            {
                p.Plugin.OnUnload();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"  [{p.ModpackName}] Plugin {p.TypeName} OnUnload failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Get all loaded mod assemblies.
    /// </summary>
    public static IReadOnlyList<Assembly> GetLoadedAssemblies() => _loadedAssemblies.AsReadOnly();

    /// <summary>
    /// Get a comma-separated summary of loaded plugin type names, or null if none.
    /// </summary>
    public static string GetPluginSummary()
    {
        if (_loadedPlugins.Count == 0) return null;
        return string.Join(", ", _loadedPlugins.Select(p => p.TypeName));
    }
}
