using MelonLoader;

namespace Menace.ModpackLoader;

/// <summary>
/// Interface for modpack plugins loaded dynamically by DllLoader.
/// Implement this in your mod DLL instead of subclassing MelonMod.
/// </summary>
public interface IModpackPlugin
{
    /// <summary>
    /// Called once after the plugin DLL is loaded. Use this to store the logger
    /// and harmony instance for later use (e.g., applying patches on scene load).
    /// </summary>
    void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony);

    /// <summary>
    /// Called when a Unity scene finishes loading, matching MelonMod.OnSceneWasLoaded.
    /// </summary>
    void OnSceneLoaded(int buildIndex, string sceneName);

    /// <summary>
    /// Called every frame, matching MelonMod.OnUpdate.
    /// Default no-op — only override if your plugin needs per-frame logic.
    /// </summary>
    void OnUpdate() { }

    /// <summary>
    /// Called every frame for IMGUI drawing, matching MelonMod.OnGUI.
    /// Default no-op — only override if your plugin draws IMGUI elements.
    /// </summary>
    void OnGUI() { }
}
