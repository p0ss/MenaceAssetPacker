using System;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using MelonLoader;

namespace Menace.ModpackLoader;

/// <summary>
/// Patches the game's bug reporter to append the list of installed mods.
/// This helps developers distinguish between vanilla bugs and mod-related issues.
/// </summary>
public static class BugReporterPatches
{
    private static MelonLogger.Instance _logger;
    private static bool _initialized;

    public static void Initialize(MelonLogger.Instance logger, HarmonyLib.Harmony harmony)
    {
        if (_initialized) return;
        _logger = logger;

        try
        {
            // Find Assembly-CSharp to search for the type
            var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            if (gameAssembly == null)
            {
                _logger.Warning("Assembly-CSharp not loaded - bug reporter patch skipped");
                return;
            }

            // Try to find BugReporterRequest type
            var targetType = gameAssembly.GetType("Menace.UI.BugReporterRequest");

            // If not found, try searching all types (IL2CPP sometimes uses different naming)
            if (targetType == null)
            {
                targetType = gameAssembly.GetTypes()
                    .FirstOrDefault(t => t.Name == "BugReporterRequest");
            }

            if (targetType == null)
            {
                _logger.Warning("BugReporterRequest not found - bug reporter patch skipped");
                return;
            }

            _logger.Msg($"Found BugReporterRequest: {targetType.FullName}");

            var targetMethod = AccessTools.Method(targetType, "Submit");
            if (targetMethod == null)
            {
                _logger.Warning("BugReporterRequest.Submit not found - bug reporter patch skipped");
                return;
            }

            var prefix = new HarmonyMethod(typeof(BugReporterPatches), nameof(SubmitPrefix));
            harmony.Patch(targetMethod, prefix: prefix);

            _initialized = true;
            _logger.Msg("Bug reporter patched - reports will include mod list");
        }
        catch (Exception ex)
        {
            _logger.Warning($"Bug reporter patch failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Prefix patch for BugReporterRequest.Submit.
    /// Appends the mod list to the issue description before submission.
    /// </summary>
    private static void SubmitPrefix(object _issueData)
    {
        try
        {
            if (_issueData == null) return;

            var descField = _issueData.GetType().GetField("description");
            if (descField == null) return;

            var currentDesc = descField.GetValue(_issueData) as string ?? "";
            var modListString = BuildModListString();
            descField.SetValue(_issueData, currentDesc + modListString);
        }
        catch
        {
            // Silently fail - don't break bug reporting
        }
    }

    private static string BuildModListString()
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("════════════════════════════════════════");
        sb.AppendLine("⚠ THIS GAME SESSION WAS RUNNING MODS ⚠");
        sb.AppendLine("════════════════════════════════════════");
        sb.AppendLine();

        var mods = ModRegistry.GetLoadedMods();
        if (mods.Count == 0)
        {
            sb.AppendLine("(No mods registered, but ModpackLoader is active)");
        }
        else
        {
            sb.AppendLine($"Installed Mods ({mods.Count}):");
            foreach (var mod in mods)
            {
                sb.AppendLine($"  - {mod.Name} v{mod.Version} by {mod.Author}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("----------------------------------------");
        sb.AppendLine("Please verify this issue occurs without");
        sb.AppendLine("mods before escalating to developers.");
        sb.AppendLine("----------------------------------------");

        return sb.ToString();
    }
}
