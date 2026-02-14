using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Menace.SDK;

/// <summary>
/// Skip splash screen and intro movie on game launch.
/// These settings are dev-mode only to help with debugging and testing.
/// Skipping the boot sequence also works around a timing issue where template
/// cloning during intro scenes can cause inconsistent localization data.
/// </summary>
public static class BootSkip
{
    private const string SettingsGroup = "Modpack Loader";

    // Common scene name patterns for boot sequences
    private static readonly string[] SplashScenePatterns =
    {
        "splash", "logo", "unity", "publisher", "developer", "legal", "disclaimer"
    };

    private static readonly string[] IntroScenePatterns =
    {
        "intro", "cinematic", "movie", "video", "cutscene", "fmv", "opening"
    };

    // Track state
    private static bool _initialized;
    private static bool _settingsRegistered;
    private static bool _hasSkippedSplash;
    private static bool _hasSkippedIntro;
    private static string _mainMenuSceneName;

    // Settings keys
    private const string SkipSplashKey = "SkipSplashScreen";
    private const string SkipIntroKey = "SkipIntroMovie";
    private const string MainMenuSceneKey = "MainMenuScene";

    public static bool SkipSplashEnabled => ModSettings.Get<bool>(SettingsGroup, SkipSplashKey);
    public static bool SkipIntroEnabled => ModSettings.Get<bool>(SettingsGroup, SkipIntroKey);
    public static string MainMenuScene => ModSettings.Get<string>(SettingsGroup, MainMenuSceneKey) ?? "MainMenu";

    /// <summary>
    /// Initialize the boot skip system. Call from ModpackLoaderMod.OnInitializeMelon.
    /// </summary>
    public static void Initialize(HarmonyLib.Harmony harmony)
    {
        if (_initialized) return;
        _initialized = true;

        RegisterSettings();
        ApplyPatches(harmony);

        // Hook scene loading
        GameState.SceneLoaded += OnSceneLoaded;

        SdkLogger.Msg("[BootSkip] Initialized");
    }

    private static void RegisterSettings()
    {
        if (_settingsRegistered) return;
        _settingsRegistered = true;

        ModSettings.Register(SettingsGroup, settings =>
        {
            settings.AddHeader("Boot Sequence");
            settings.AddToggle(SkipSplashKey, "Skip Splash Screen", false);
            settings.AddToggle(SkipIntroKey, "Skip Intro Movie", false);
            settings.AddText(MainMenuSceneKey, "Main Menu Scene", "MainMenu");

            settings.AddHeader("Information");
            settings.AddInfo("LoaderVersion", "Loader Version", () => ModkitVersion.LoaderFull);
            settings.AddInfo("CurrentScene", "Current Scene", () => GameState.CurrentScene);
        });
    }

    private static void ApplyPatches(HarmonyLib.Harmony harmony)
    {
        try
        {
            // Patch VideoPlayer.Play to detect and skip intro videos
            // Use reflection since UnityEngine.Video may not be available in all Unity versions
            var videoPlayerType = FindVideoPlayerType();

            if (videoPlayerType != null)
            {
                var playMethod = AccessTools.Method(videoPlayerType, "Play");

                if (playMethod != null)
                {
                    var prefix = new HarmonyMethod(typeof(BootSkip), nameof(VideoPlayerPlayPrefix));
                    harmony.Patch(playMethod, prefix: prefix);
                    SdkLogger.Msg("[BootSkip] Patched VideoPlayer.Play");
                }
                else
                {
                    SdkLogger.Msg("[BootSkip] VideoPlayer.Play not found (no video support)");
                }
            }
            else
            {
                SdkLogger.Msg("[BootSkip] VideoPlayer type not found (no video support)");
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[BootSkip] VideoPlayer patch failed: {ex.Message}");
        }
    }

    private static Type FindVideoPlayerType()
    {
        // Try to find VideoPlayer type in loaded assemblies
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var name = assembly.GetName().Name;
                if (!name.Contains("Unity") && !name.Contains("Assembly"))
                    continue;

                var type = assembly.GetType("UnityEngine.Video.VideoPlayer");
                if (type != null)
                    return type;
            }
            catch
            {
                // Some assemblies throw on GetType
            }
        }
        return null;
    }

    /// <summary>
    /// Prefix patch for VideoPlayer.Play - skip intro videos if enabled.
    /// </summary>
    private static bool VideoPlayerPlayPrefix(object __instance)
    {
        if (!SkipIntroEnabled || _hasSkippedIntro)
            return true; // Allow video to play

        try
        {
            var sceneName = GameState.CurrentScene.ToLowerInvariant();

            // Check if this is an intro scene
            if (IsIntroScene(sceneName) || IsSplashScene(sceneName))
            {
                SdkLogger.Msg($"[BootSkip] Skipping video in scene: {sceneName}");

                // Mark as skipped and trigger immediate scene transition
                if (IsIntroScene(sceneName))
                    _hasSkippedIntro = true;
                else
                    _hasSkippedSplash = true;

                // Skip to main menu
                TrySkipToMainMenu();

                return false; // Prevent video from playing
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[BootSkip] VideoPlayer prefix error: {ex.Message}");
        }

        return true; // Allow video to play
    }

    /// <summary>
    /// Called when a scene loads - check if we should skip it.
    /// </summary>
    private static void OnSceneLoaded(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        var lowerName = sceneName.ToLowerInvariant();

        // Check for splash screen skip
        if (SkipSplashEnabled && !_hasSkippedSplash && IsSplashScene(lowerName))
        {
            SdkLogger.Msg($"[BootSkip] Detected splash scene: {sceneName}, skipping...");
            _hasSkippedSplash = true;

            // Use delayed execution to allow the scene to fully load before skipping
            GameState.RunDelayed(5, () => TrySkipToNextScene(sceneName));
            return;
        }

        // Check for intro scene skip
        if (SkipIntroEnabled && !_hasSkippedIntro && IsIntroScene(lowerName))
        {
            SdkLogger.Msg($"[BootSkip] Detected intro scene: {sceneName}, skipping...");
            _hasSkippedIntro = true;

            // Use delayed execution to allow the scene to fully load before skipping
            GameState.RunDelayed(5, () => TrySkipToMainMenu());
            return;
        }

        // If this is the main menu, reset skip state for next run
        if (IsMainMenuScene(lowerName))
        {
            // Reset state so settings can be changed between runs
            _hasSkippedSplash = false;
            _hasSkippedIntro = false;
            _mainMenuSceneName = sceneName;
        }
    }

    private static bool IsSplashScene(string sceneName)
    {
        return SplashScenePatterns.Any(p => sceneName.Contains(p));
    }

    private static bool IsIntroScene(string sceneName)
    {
        return IntroScenePatterns.Any(p => sceneName.Contains(p));
    }

    private static bool IsMainMenuScene(string sceneName)
    {
        // Check against saved main menu scene name
        var configuredMainMenu = MainMenuScene.ToLowerInvariant();
        if (sceneName.Contains(configuredMainMenu))
            return true;

        // Common patterns for main menu
        return sceneName.Contains("mainmenu") ||
               sceneName.Contains("main_menu") ||
               sceneName.Contains("title") ||
               sceneName.Contains("menu");
    }

    private static void TrySkipToNextScene(string currentScene)
    {
        try
        {
            // Get current build index
            var activeScene = SceneManager.GetActiveScene();
            var nextIndex = activeScene.buildIndex + 1;

            // Validate next scene exists
            if (nextIndex < SceneManager.sceneCountInBuildSettings)
            {
                SdkLogger.Msg($"[BootSkip] Loading next scene (index {nextIndex})");
                SceneManager.LoadScene(nextIndex);
            }
            else
            {
                // Try loading main menu directly
                TrySkipToMainMenu();
            }
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[BootSkip] Failed to skip to next scene: {ex.Message}");
        }
    }

    private static void TrySkipToMainMenu()
    {
        try
        {
            var mainMenu = _mainMenuSceneName ?? MainMenuScene;

            // Try loading by name first
            SdkLogger.Msg($"[BootSkip] Attempting to load main menu: {mainMenu}");

            // Use the scene manager to find and load the main menu
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(i);
                var sceneName = System.IO.Path.GetFileNameWithoutExtension(path);

                if (IsMainMenuScene(sceneName.ToLowerInvariant()))
                {
                    SdkLogger.Msg($"[BootSkip] Found main menu scene: {sceneName} (index {i})");
                    SceneManager.LoadScene(i);
                    return;
                }
            }

            // Fallback: try loading by configured name directly
            SceneManager.LoadScene(mainMenu);
        }
        catch (Exception ex)
        {
            SdkLogger.Warning($"[BootSkip] Failed to load main menu: {ex.Message}");
        }
    }

    /// <summary>
    /// Console command to skip current boot scene.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        DevConsole.RegisterCommand("skipboot", "", "Skip current splash/intro scene", args =>
        {
            var scene = GameState.CurrentScene;
            if (IsSplashScene(scene.ToLowerInvariant()) || IsIntroScene(scene.ToLowerInvariant()))
            {
                TrySkipToMainMenu();
                return "Skipping to main menu...";
            }
            return "Not in a boot scene";
        });

        DevConsole.RegisterCommand("bootinfo", "", "Show boot skip status", args =>
        {
            return $"Splash skip: {(SkipSplashEnabled ? "ON" : "OFF")} (done: {_hasSkippedSplash})\n" +
                   $"Intro skip: {(SkipIntroEnabled ? "ON" : "OFF")} (done: {_hasSkippedIntro})\n" +
                   $"Main menu scene: {MainMenuScene}\n" +
                   $"Current scene: {GameState.CurrentScene}";
        });
    }
}
