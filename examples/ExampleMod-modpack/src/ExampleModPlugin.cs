using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Menace.ExampleMod;

/// <summary>
/// Comprehensive example demonstrating all major Menace SDK features.
/// Copy and adapt these patterns for your own mods.
///
/// Features demonstrated:
/// - ModSettings: All control types (toggle, slider, number, dropdown, text)
/// - Templates API: Reading and writing game data
/// - GamePatch: Harmony patching for runtime behavior
/// - DevConsole: Custom panels, logging, watching values
/// - GameState: Scene awareness, delayed execution
/// - ModError: Error reporting
/// </summary>
public class ExampleModPlugin : IModpackPlugin
{
    private MelonLogger.Instance _log;
    private HarmonyLib.Harmony _harmony;

    // =========================================================================
    // INITIALIZATION
    // =========================================================================

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        _log = logger;
        _harmony = harmony;
        _log.Msg("Example Mod initialized");

        // Register all settings
        RegisterSettings();

        // Register a custom DevConsole panel
        RegisterCustomPanel();

        // Register some watched values
        RegisterWatches();

        // Subscribe to setting changes
        ModSettings.OnSettingChanged += OnSettingChanged;
    }

    // =========================================================================
    // MOD SETTINGS - All control types demonstrated
    // =========================================================================

    private void RegisterSettings()
    {
        ModSettings.Register("Example Mod", settings =>
        {
            // --- Gameplay Section ---
            settings.AddHeader("Gameplay");

            // Slider: floating-point value with min/max
            settings.AddSlider("DamageMultiplier", "Damage Multiplier", 0.5f, 3.0f, 1.0f);
            settings.AddSlider("SupplyMultiplier", "Supply Income", 0.25f, 4.0f, 1.0f);

            // Number: integer value with +/- buttons
            settings.AddNumber("StartingSquadSize", "Starting Squad Size", 1, 12, 4);
            settings.AddNumber("BonusSkillPoints", "Bonus Skill Points", 0, 50, 0);

            // --- Options Section ---
            settings.AddHeader("Options");

            // Toggle: boolean on/off
            settings.AddToggle("ShowDamageNumbers", "Show Damage Numbers", true);
            settings.AddToggle("EnableTutorials", "Enable Tutorials", true);

            // Dropdown: selection from fixed list
            settings.AddDropdown("Difficulty", "Difficulty Preset",
                new[] { "Easy", "Normal", "Hard", "Brutal" }, "Normal");
            settings.AddDropdown("StartingFaction", "Starting Faction",
                new[] { "Federation", "Empire", "Pirates", "Rebels" }, "Federation");

            // --- Advanced Section ---
            settings.AddHeader("Advanced");

            // Text: free-form string input
            settings.AddText("PlayerName", "Commander Name", "Commander");
            settings.AddText("CustomSeed", "Random Seed (blank=random)", "");
        });
    }

    /// <summary>
    /// Called when any setting changes. Use this to apply changes immediately.
    /// </summary>
    private void OnSettingChanged(string modName, string key, object value)
    {
        if (modName != "Example Mod") return;

        _log.Msg($"Setting changed: {key} = {value}");

        // Apply the change based on which setting was modified
        switch (key)
        {
            case "DamageMultiplier":
                ApplyDamageMultiplier((float)value);
                break;
            case "SupplyMultiplier":
                ApplySupplyMultiplier((float)value);
                break;
            case "StartingSquadSize":
                ApplySquadSize(Convert.ToInt32(value));
                break;
            case "BonusSkillPoints":
                ApplyBonusSkillPoints(Convert.ToInt32(value));
                break;
            case "Difficulty":
                ApplyDifficultyPreset((string)value);
                break;
        }
    }

    /// <summary>
    /// Read settings anywhere in your code using ModSettings.Get&lt;T&gt;()
    /// </summary>
    private void ExampleReadingSettings()
    {
        // Reading different types
        float damage = ModSettings.Get<float>("Example Mod", "DamageMultiplier");
        int squadSize = ModSettings.Get<int>("Example Mod", "StartingSquadSize");
        bool showDamage = ModSettings.Get<bool>("Example Mod", "ShowDamageNumbers");
        string difficulty = ModSettings.Get<string>("Example Mod", "Difficulty");
        string playerName = ModSettings.Get<string>("Example Mod", "PlayerName");

        _log.Msg($"Current settings: damage={damage}x, squad={squadSize}, " +
                 $"showDmg={showDamage}, difficulty={difficulty}, name={playerName}");
    }

    // =========================================================================
    // TEMPLATES API - Reading and writing game data
    // =========================================================================

    /// <summary>
    /// Apply damage multiplier by modifying weapon templates.
    /// CUSTOMIZE: Replace template/field names for your game.
    /// </summary>
    private void ApplyDamageMultiplier(float multiplier)
    {
        // Find all weapon templates and scale their damage
        var weapons = Templates.FindAll("WeaponTemplate");
        int modified = 0;

        foreach (var weapon in weapons)
        {
            // Read base damage, apply multiplier, write back
            var baseDamage = Templates.ReadField(weapon, "BaseDamage");
            if (baseDamage != null)
            {
                float newDamage = Convert.ToSingle(baseDamage) * multiplier;
                if (Templates.WriteField(weapon, "Damage", newDamage))
                    modified++;
            }
        }

        if (modified > 0)
            _log.Msg($"Applied {multiplier}x damage to {modified} weapons");
    }

    /// <summary>
    /// Apply supply multiplier to campaign/economy settings.
    /// </summary>
    private void ApplySupplyMultiplier(float multiplier)
    {
        // Example: Find campaign settings and modify economy
        var settings = Templates.Find("CampaignSettings", "Default");
        if (!settings.IsNull)
        {
            Templates.WriteField(settings, "SupplyMultiplier", multiplier);
            _log.Msg($"Applied supply multiplier: {multiplier}x");
        }
        else
        {
            // Template not found - report as warning, not error
            ModError.Warn("Example Mod", "CampaignSettings template not found");
        }
    }

    private void ApplySquadSize(int size)
    {
        var settings = Templates.Find("GameSettings", "Default");
        if (!settings.IsNull)
        {
            Templates.WriteField(settings, "MaxSquadSize", size);
            _log.Msg($"Applied squad size: {size}");
        }
    }

    private void ApplyBonusSkillPoints(int points)
    {
        // Example: Modify all soldier templates to have bonus XP
        var soldiers = Templates.FindAll("SoldierTemplate");
        foreach (var soldier in soldiers)
        {
            Templates.WriteField(soldier, "BonusSkillPoints", points);
        }
        _log.Msg($"Applied {points} bonus skill points to {soldiers.Length} soldiers");
    }

    private void ApplyDifficultyPreset(string preset)
    {
        // Example: Apply a preset that changes multiple values
        float damageToPlayer = preset switch
        {
            "Easy" => 0.5f,
            "Normal" => 1.0f,
            "Hard" => 1.5f,
            "Brutal" => 2.0f,
            _ => 1.0f
        };

        float enemyHealth = preset switch
        {
            "Easy" => 0.75f,
            "Normal" => 1.0f,
            "Hard" => 1.25f,
            "Brutal" => 1.5f,
            _ => 1.0f
        };

        var settings = Templates.Find("DifficultySettings", preset);
        if (!settings.IsNull)
        {
            Templates.WriteFields(settings, new Dictionary<string, object>
            {
                { "DamageToPlayerMultiplier", damageToPlayer },
                { "EnemyHealthMultiplier", enemyHealth }
            });
            _log.Msg($"Applied difficulty preset: {preset}");
        }
    }

    // =========================================================================
    // GAME PATCH - Harmony patching for runtime behavior
    // =========================================================================

    /// <summary>
    /// Example of patching a game method using GamePatch.
    /// This runs BEFORE the original method.
    /// </summary>
    private void SetupPatches()
    {
        // Prefix patch: runs before the original method
        // Return false from prefix to skip the original method
        GamePatch.Prefix(
            "DamageCalculator",           // Type name
            "CalculateDamage",            // Method name
            typeof(ExampleModPlugin),     // Class containing patch method
            nameof(DamageCalculatorPrefix) // Patch method name
        );

        // Postfix patch: runs after the original method
        // Can modify the return value via __result parameter
        GamePatch.Postfix(
            "ExperienceManager",
            "GetExperienceForKill",
            typeof(ExampleModPlugin),
            nameof(ExperiencePostfix)
        );
    }

    /// <summary>
    /// Prefix patch example - modifies damage before calculation.
    /// Parameter names must match the original method's parameters.
    /// </summary>
    private static void DamageCalculatorPrefix(ref float baseDamage, ref float armor)
    {
        // Example: Apply damage multiplier from settings
        float mult = ModSettings.Get<float>("Example Mod", "DamageMultiplier");
        baseDamage *= mult;
    }

    /// <summary>
    /// Postfix patch example - modifies return value.
    /// Use __result to access/modify the return value.
    /// </summary>
    private static void ExperiencePostfix(ref int __result)
    {
        // Example: Double XP if setting is enabled
        bool doubleXP = ModSettings.Get<bool>("Example Mod", "DoubleXP");
        if (doubleXP)
            __result *= 2;
    }

    // =========================================================================
    // DEV CONSOLE - Custom panels and logging
    // =========================================================================

    private void RegisterCustomPanel()
    {
        // Register a custom panel that appears in DevConsole tabs
        DevConsole.RegisterPanel("Example", DrawExamplePanel);
    }

    /// <summary>
    /// Custom panel drawing using IMGUI.
    /// Called every frame when the panel is visible.
    /// </summary>
    private void DrawExamplePanel(Rect area)
    {
        float y = area.y;
        float lineHeight = 22f;

        // Display current settings
        GUI.Label(new Rect(area.x, y, area.width, lineHeight),
            $"Damage Multiplier: {ModSettings.Get<float>("Example Mod", "DamageMultiplier"):F2}x");
        y += lineHeight;

        GUI.Label(new Rect(area.x, y, area.width, lineHeight),
            $"Squad Size: {ModSettings.Get<int>("Example Mod", "StartingSquadSize")}");
        y += lineHeight;

        GUI.Label(new Rect(area.x, y, area.width, lineHeight),
            $"Difficulty: {ModSettings.Get<string>("Example Mod", "Difficulty")}");
        y += lineHeight + 10;

        // Add a button
        if (GUI.Button(new Rect(area.x, y, 150, 24), "Apply All Settings"))
        {
            ApplyAllSettings();
            DevConsole.Log("[Example] All settings applied!");
        }
        y += 30;

        if (GUI.Button(new Rect(area.x, y, 150, 24), "Log Template Info"))
        {
            LogTemplateInfo();
        }
    }

    private void RegisterWatches()
    {
        // Watch expressions update every frame in the Watch panel
        DevConsole.Watch("Damage Mult",
            () => $"{ModSettings.Get<float>("Example Mod", "DamageMultiplier"):F2}x");

        DevConsole.Watch("Squad Size",
            () => ModSettings.Get<int>("Example Mod", "StartingSquadSize").ToString());

        DevConsole.Watch("Scene",
            () => GameState.CurrentScene);
    }

    private void LogTemplateInfo()
    {
        // Log to DevConsole (appears in Log panel)
        DevConsole.Log("[Example] Scanning templates...");

        var weapons = Templates.FindAll("WeaponTemplate");
        DevConsole.Log($"[Example] Found {weapons.Length} WeaponTemplates");

        foreach (var weapon in weapons)
        {
            var name = weapon.GetName();
            var damage = Templates.ReadField(weapon, "Damage");
            DevConsole.Log($"  {name}: damage={damage}");
        }
    }

    // =========================================================================
    // GAME STATE - Scene awareness and delayed execution
    // =========================================================================

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        _log.Msg($"Scene loaded: {sceneName}");

        // GameState.CurrentScene is also available
        DevConsole.Log($"[Example] Scene: {GameState.CurrentScene}");

        // Run code after a delay (in frames)
        // Useful for waiting for game objects to initialize
        GameState.RunDelayed(60, () =>
        {
            _log.Msg("Delayed code running (60 frames after scene load)");
            ApplyAllSettings();
        });

        // Run code when a condition becomes true
        // Polls every frame until condition is met
        GameState.RunWhen(
            () => Templates.FindAll("WeaponTemplate").Length > 0,  // Condition
            () =>
            {
                _log.Msg("Weapons loaded, applying modifications...");
                ApplyDamageMultiplier(ModSettings.Get<float>("Example Mod", "DamageMultiplier"));
            }
        );
    }

    private void ApplyAllSettings()
    {
        ApplyDamageMultiplier(ModSettings.Get<float>("Example Mod", "DamageMultiplier"));
        ApplySupplyMultiplier(ModSettings.Get<float>("Example Mod", "SupplyMultiplier"));
        ApplySquadSize(ModSettings.Get<int>("Example Mod", "StartingSquadSize"));
        ApplyBonusSkillPoints(ModSettings.Get<int>("Example Mod", "BonusSkillPoints"));
        ApplyDifficultyPreset(ModSettings.Get<string>("Example Mod", "Difficulty"));
    }

    // =========================================================================
    // ERROR HANDLING - Using ModError for diagnostics
    // =========================================================================

    private void ExampleErrorHandling()
    {
        // Report an error (appears in DevConsole Errors panel and MelonLoader log)
        ModError.Report("Example Mod", "Something went wrong!");

        // Report a warning (less severe)
        ModError.Warn("Example Mod", "This might be a problem");

        // Report info (diagnostic, not an error)
        ModError.Info("Example Mod", "Just letting you know...");

        // Get all errors for this mod
        var errors = ModError.GetErrors("Example Mod");
        foreach (var error in errors)
        {
            _log.Msg($"Error: {error}");
        }
    }

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    public void OnUpdate()
    {
        // Called every frame
        // Keep this lightweight - heavy code here causes lag
    }

    public void OnGUI()
    {
        // Called for IMGUI rendering
        // Custom panel drawing is handled by DevConsole.RegisterPanel
    }
}
