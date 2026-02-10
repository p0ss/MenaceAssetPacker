using System;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
using Menace.ModpackLoader;
using Menace.SDK;

namespace Menace.CombinedArms;

/// <summary>
/// Combined Arms - AI Coordination Mod (SDK Edition)
///
/// Makes enemy AI fight more tactically through coordinated behavior:
/// - Agent Sequencing: Suppressors act before damage dealers
/// - Focus Fire: Units prioritize already-targeted enemies
/// - Center of Forces: Units stay together as a group
/// - Formation Depth: Units position by role (assault forward, support back)
///
/// Settings are configured via the DevConsole Settings panel (press ~).
/// </summary>
public class CombinedArmsSDK : IModpackPlugin
{
    public static CombinedArmsSDK Instance;
    private const string MOD_NAME = "Combined Arms";

    private static MelonLogger.Instance _log;
    private static HarmonyLib.Harmony _harmony;
    private static bool _patchesApplied;

    // Diagnostics
    private static bool _loggedFirstTurnStart;
    private static bool _loggedFirstScoreMult;
    private static bool _loggedFirstExecute;
    private static bool _loggedFirstTileScores;

    public void OnInitialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        Instance = this;
        _log = logger;
        _harmony = harmony;

        // Register settings in DevConsole
        RegisterSettings();

        _log.Msg("Combined Arms SDK v2.0.0");
        _log.Msg("  Configure in DevConsole (~) -> Settings -> Combined Arms");

        // Register console commands
        AICoordination.RegisterConsoleCommands();
        RegisterCommands();
    }

    public void OnSceneLoaded(int buildIndex, string sceneName)
    {
        if (!_patchesApplied && sceneName == "Tactical")
        {
            ApplyPatches();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Settings (DevConsole -> Settings panel)
    // ═══════════════════════════════════════════════════════════════════

    private void RegisterSettings()
    {
        ModSettings.Register(MOD_NAME, settings =>
        {
            // Master toggle
            settings.AddToggle("Enabled", "Enable Combined Arms", true);

            // Agent Sequencing
            settings.AddHeader("Agent Sequencing");
            settings.AddToggle("EnableSequencing", "Enable Sequencing", true);
            settings.AddSlider("SuppressorBoost", "Suppressor Priority Boost", 1.0f, 3.0f, 1.5f);
            settings.AddSlider("DamagePenalty", "Damage Dealer Penalty", 0.3f, 1.0f, 0.7f);

            // Focus Fire
            settings.AddHeader("Focus Fire");
            settings.AddToggle("EnableFocusFire", "Enable Focus Fire", true);
            settings.AddSlider("FocusFireBoost", "Focus Fire Boost", 1.0f, 3.0f, 1.4f);

            // Center of Forces
            settings.AddHeader("Center of Forces");
            settings.AddToggle("EnableCoF", "Enable Center of Forces", true);
            settings.AddSlider("CoFWeight", "CoF Weight", 0.0f, 10.0f, 3.0f);
            settings.AddNumber("CoFRange", "CoF Max Range", 4, 24, 12);
            settings.AddNumber("CoFMinAllies", "CoF Min Allies", 1, 6, 2);

            // Formation Depth
            settings.AddHeader("Formation Depth");
            settings.AddToggle("EnableDepth", "Enable Formation Depth", true);
            settings.AddSlider("DepthWeight", "Depth Weight", 0.0f, 10.0f, 2.5f);
            settings.AddNumber("DepthRange", "Depth Max Range", 6, 30, 18);
        });

        // Subscribe to setting changes for live updates
        ModSettings.OnSettingChanged += OnSettingChanged;
    }

    private static void OnSettingChanged(string modName, string key, object value)
    {
        if (modName != MOD_NAME) return;

        // Log changes
        _log?.Msg($"[CombinedArms] Setting changed: {key} = {value}");
    }

    /// <summary>
    /// Build a CoordinationConfig from current ModSettings values.
    /// </summary>
    private static AICoordination.CoordinationConfig GetConfig()
    {
        return new AICoordination.CoordinationConfig
        {
            EnableSequencing = ModSettings.Get<bool>(MOD_NAME, "EnableSequencing"),
            SuppressorPriorityBoost = ModSettings.Get<float>(MOD_NAME, "SuppressorBoost"),
            DamageDealerPenalty = ModSettings.Get<float>(MOD_NAME, "DamagePenalty"),

            EnableFocusFire = ModSettings.Get<bool>(MOD_NAME, "EnableFocusFire"),
            FocusFireBoost = ModSettings.Get<float>(MOD_NAME, "FocusFireBoost"),

            EnableCenterOfForces = ModSettings.Get<bool>(MOD_NAME, "EnableCoF"),
            CenterOfForcesWeight = ModSettings.Get<float>(MOD_NAME, "CoFWeight"),
            CenterOfForcesMaxRange = ModSettings.Get<int>(MOD_NAME, "CoFRange"),
            CenterOfForcesMinAllies = ModSettings.Get<int>(MOD_NAME, "CoFMinAllies"),

            EnableFormationDepth = ModSettings.Get<bool>(MOD_NAME, "EnableDepth"),
            FormationDepthWeight = ModSettings.Get<float>(MOD_NAME, "DepthWeight"),
            FormationDepthMaxRange = ModSettings.Get<int>(MOD_NAME, "DepthRange"),
            FrontlineFraction = 0.33f,
            MidlineFraction = 0.34f,
            FormationDepthMinEnemies = 1
        };
    }

    /// <summary>
    /// Check if the mod is enabled.
    /// </summary>
    private static bool IsEnabled()
    {
        return ModSettings.Get<bool>(MOD_NAME, "Enabled");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Console Commands
    // ═══════════════════════════════════════════════════════════════════

    private void RegisterCommands()
    {
        DevConsole.RegisterCommand("ca", "", "Show Combined Arms status", _ =>
        {
            var cfg = GetConfig();
            return $"Combined Arms: {(IsEnabled() ? "ENABLED" : "DISABLED")}\n" +
                   $"  Sequencing: {cfg.EnableSequencing} (boost={cfg.SuppressorPriorityBoost:F1}x, penalty={cfg.DamageDealerPenalty:F1}x)\n" +
                   $"  Focus Fire: {cfg.EnableFocusFire} (boost={cfg.FocusFireBoost:F1}x)\n" +
                   $"  Center of Forces: {cfg.EnableCenterOfForces} (weight={cfg.CenterOfForcesWeight:F1}, range={cfg.CenterOfForcesMaxRange})\n" +
                   $"  Formation Depth: {cfg.EnableFormationDepth} (weight={cfg.FormationDepthWeight:F1}, range={cfg.FormationDepthMaxRange})\n" +
                   $"\nConfigure in DevConsole -> Settings -> Combined Arms";
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Harmony Patches
    // ═══════════════════════════════════════════════════════════════════

    private void ApplyPatches()
    {
        if (_patchesApplied) return;

        try
        {
            // Find types
            var aiFactionType = FindType("AIFaction");
            var agentType = FindType("Agent");

            if (aiFactionType == null || agentType == null)
            {
                _log.Error("Could not find AI types - patches not applied");
                return;
            }

            // Patch AIFaction.OnTurnStart
            var onTurnStart = aiFactionType.GetMethod("OnTurnStart",
                BindingFlags.Public | BindingFlags.Instance);
            if (onTurnStart != null)
            {
                _harmony.Patch(onTurnStart,
                    postfix: new HarmonyMethod(typeof(CombinedArmsSDK), nameof(OnTurnStart_Postfix)));
                _log.Msg("Patched AIFaction.OnTurnStart");
            }

            // Patch Agent.GetScoreMultForPickingThisAgent
            var getScoreMult = agentType.GetMethod("GetScoreMultForPickingThisAgent",
                BindingFlags.Public | BindingFlags.Instance);
            if (getScoreMult != null)
            {
                _harmony.Patch(getScoreMult,
                    postfix: new HarmonyMethod(typeof(CombinedArmsSDK), nameof(GetScoreMult_Postfix)));
                _log.Msg("Patched Agent.GetScoreMultForPickingThisAgent");
            }

            // Patch Agent.Execute
            var execute = agentType.GetMethod("Execute",
                BindingFlags.Public | BindingFlags.Instance);
            if (execute != null)
            {
                _harmony.Patch(execute,
                    postfix: new HarmonyMethod(typeof(CombinedArmsSDK), nameof(Execute_Postfix)));
                _log.Msg("Patched Agent.Execute");
            }

            // Patch Agent.PostProcessTileScores
            var postProcess = agentType.GetMethod("PostProcessTileScores",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (postProcess != null)
            {
                _harmony.Patch(postProcess,
                    postfix: new HarmonyMethod(typeof(CombinedArmsSDK), nameof(PostProcessTileScores_Postfix)));
                _log.Msg("Patched Agent.PostProcessTileScores");
            }

            _patchesApplied = true;
            _log.Msg("Combined Arms SDK patches applied successfully");
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to apply patches: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private static Type FindType(string shortName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name != "Assembly-CSharp") continue;

            foreach (var t in asm.GetTypes())
            {
                if (t.Name == shortName || t.Name == "Il2Cpp" + shortName)
                    return t;
            }
        }
        return null;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Hook: OnTurnStart - Initialize state (SAFE - single threaded)
    // ═══════════════════════════════════════════════════════════════════

    public static void OnTurnStart_Postfix(object __instance)
    {
        if (!IsEnabled()) return;

        try
        {
            if (__instance is not Il2CppObjectBase il2cpp) return;

            var faction = new GameObj(il2cpp.Pointer);
            int factionIndex = AICoordination.GetFactionIndex(faction);
            if (factionIndex < 0) return;

            // Initialize turn state using SDK
            AICoordination.InitializeTurnState(factionIndex);

            if (!_loggedFirstTurnStart)
            {
                _loggedFirstTurnStart = true;
                var state = AICoordination.GetFactionState(factionIndex);
                _log?.Msg($"[CombinedArms] OnTurnStart: faction={factionIndex} " +
                          $"allies={state.AllyPositions.Count} at ({state.AllyCentroid.x:F1},{state.AllyCentroid.y:F1}) " +
                          $"enemies={state.EnemyPositions.Count} at ({state.EnemyCentroid.x:F1},{state.EnemyCentroid.y:F1})");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[CombinedArms] OnTurnStart error: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Hook: GetScoreMult - Agent selection priority (parallel, read-only)
    // ═══════════════════════════════════════════════════════════════════

    public static void GetScoreMult_Postfix(object __instance, ref float __result)
    {
        if (!IsEnabled()) return;

        try
        {
            if (__instance is not Il2CppObjectBase il2cpp) return;

            var agent = new GameObj(il2cpp.Pointer);
            if (agent.IsNull) return;

            var actor = agent.ReadObj("m_Actor");
            if (actor.IsNull) return;

            int factionIndex = AICoordination.GetAgentFactionIndex(agent);
            if (factionIndex < 0) return;

            var state = AICoordination.GetFactionState(factionIndex);
            var config = GetConfig();

            // Calculate coordination multiplier
            float multiplier = AICoordination.CalculateAgentScoreMultiplier(actor, state, config);

            if (!_loggedFirstScoreMult && Math.Abs(multiplier - 1.0f) > 0.01f)
            {
                _loggedFirstScoreMult = true;
                var role = AICoordination.ClassifyUnit(actor);
                _log?.Msg($"[CombinedArms] GetScoreMult: {actor.GetName()} role={role} " +
                          $"original={__result:F3} multiplier={multiplier:F3}");
            }

            __result *= multiplier;
        }
        catch (Exception ex)
        {
            _log?.Error($"[CombinedArms] GetScoreMult error: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Hook: Execute - Track completed actions (SAFE - sequential)
    // ═══════════════════════════════════════════════════════════════════

    public static void Execute_Postfix(object __instance)
    {
        if (!IsEnabled()) return;

        try
        {
            if (__instance is not Il2CppObjectBase il2cpp) return;

            var agent = new GameObj(il2cpp.Pointer);
            if (agent.IsNull) return;

            int factionIndex = AICoordination.GetAgentFactionIndex(agent);
            if (factionIndex < 0) return;

            // Record execution using SDK
            AICoordination.RecordAgentExecution(agent, factionIndex);

            if (!_loggedFirstExecute)
            {
                _loggedFirstExecute = true;
                var actor = agent.ReadObj("m_Actor");
                var state = AICoordination.GetFactionState(factionIndex);
                _log?.Msg($"[CombinedArms] Execute: {actor.GetName()} " +
                          $"suppressor_acted={state.HasSuppressorActed} " +
                          $"targeted_tiles={state.TargetedTiles.Count}");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[CombinedArms] Execute error: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Hook: PostProcessTileScores - Modify tile utilities (per-agent safe)
    // ═══════════════════════════════════════════════════════════════════

    public static void PostProcessTileScores_Postfix(object __instance)
    {
        if (!IsEnabled()) return;

        var config = GetConfig();
        if (!config.EnableCenterOfForces && !config.EnableFormationDepth) return;

        try
        {
            if (__instance is not Il2CppObjectBase il2cpp) return;

            var agent = new GameObj(il2cpp.Pointer);
            if (agent.IsNull) return;

            int factionIndex = AICoordination.GetAgentFactionIndex(agent);
            if (factionIndex < 0) return;

            var state = AICoordination.GetFactionState(factionIndex);

            // Apply tile score modifiers using SDK
            // This is safe because each agent has its own m_Tiles dictionary
            AICoordination.ApplyTileScoreModifiers(agent, state, config);

            if (!_loggedFirstTileScores)
            {
                _loggedFirstTileScores = true;
                var actor = agent.ReadObj("m_Actor");
                var band = AICoordination.ClassifyFormationBand(actor);
                _log?.Msg($"[CombinedArms] PostProcessTileScores: {actor.GetName()} " +
                          $"band={band} " +
                          $"CoF={config.EnableCenterOfForces} Depth={config.EnableFormationDepth}");
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[CombinedArms] PostProcessTileScores error: {ex.Message}");
        }
    }
}
