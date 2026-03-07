using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Detects legacy installation patterns that need migration or cleanup.
/// This is Phase 0.5 of the infrastructure improvement plan.
///
/// Legacy patterns detected:
/// - .original backup files without backup-metadata.json
/// - Components installed without provenance tracking (no manifest.json in cache)
/// - Old Mods/UserLibs layout (DLLs in wrong places, legacy dependency copies)
/// - Untracked .original files not in deploy-state.json
/// </summary>
public class LegacyInstallDetector
{
    /// <summary>
    /// Result of legacy installation detection.
    /// </summary>
    public record LegacyDetectionResult
    {
        /// <summary>
        /// True if any legacy patterns were detected.
        /// </summary>
        public bool IsLegacyInstall { get; init; }

        /// <summary>
        /// .original files exist but no backup-metadata.json beside them.
        /// </summary>
        public bool HasUnbackedOriginals { get; init; }

        /// <summary>
        /// Components installed but no provenance tracking (no manifest.json in component cache).
        /// </summary>
        public bool HasNoProvenance { get; init; }

        /// <summary>
        /// Old Mods/UserLibs layout detected (DLLs directly in Mods/, wrong placement).
        /// </summary>
        public bool HasOldModsLayout { get; init; }

        /// <summary>
        /// Legacy dependency copies found in Mods/ instead of UserLibs/.
        /// </summary>
        public bool HasLegacyDependencies { get; init; }

        /// <summary>
        /// Human-readable list of detected issues.
        /// </summary>
        public List<string> DetectedIssues { get; init; } = new();

        /// <summary>
        /// Confidence score from 0.0 to 1.0.
        /// Low confidence = recommend "Reset to clean state"
        /// High confidence = can offer "Migrate existing install"
        /// </summary>
        public float ConfidenceScore { get; init; }
    }

    /// <summary>
    /// Known support libraries that should be in UserLibs, not Mods.
    /// These are dependencies of ModpackLoader.
    /// </summary>
    private static readonly HashSet<string> KnownSupportLibraries = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.CodeAnalysis.dll",
        "Microsoft.CodeAnalysis.CSharp.dll",
        "System.Collections.Immutable.dll",
        "System.Reflection.Metadata.dll",
        "System.Text.Encoding.CodePages.dll",
        "Newtonsoft.Json.dll",
        "SharpGLTF.Core.dll",
        "0Harmony.dll"
    };

    /// <summary>
    /// Files that indicate a proper modpack subfolder structure.
    /// </summary>
    private static readonly HashSet<string> ModpackIndicatorFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "modpack.json",
        "manifest.json"
    };

    /// <summary>
    /// Detect legacy installation patterns at the specified game path.
    /// This method is designed to be fast for startup checks.
    /// </summary>
    /// <param name="gamePath">The game installation directory.</param>
    /// <returns>Detection result with flags and confidence score.</returns>
    public LegacyDetectionResult Detect(string gamePath)
    {
        if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
        {
            ModkitLog.Info("[LegacyInstallDetector] No valid game path provided, skipping detection");
            return new LegacyDetectionResult
            {
                IsLegacyInstall = false,
                ConfidenceScore = 0f
            };
        }

        var issues = new List<string>();
        var detectionFlags = new DetectionFlags();

        try
        {
            // Check for .original files without backup metadata
            CheckUnbackedOriginals(gamePath, issues, detectionFlags);

            // Check for component provenance
            CheckComponentProvenance(issues, detectionFlags);

            // Check for old Mods layout
            CheckOldModsLayout(gamePath, issues, detectionFlags);

            // Check for untracked .original files
            CheckUntrackedOriginals(gamePath, issues, detectionFlags);

            // Check for legacy dependencies in Mods/
            CheckLegacyDependencies(gamePath, issues, detectionFlags);
        }
        catch (Exception ex)
        {
            ModkitLog.Warn($"[LegacyInstallDetector] Error during detection: {ex.Message}");
            issues.Add($"Detection error: {ex.Message}");
        }

        var isLegacy = detectionFlags.HasAnyIssue;
        var confidence = CalculateConfidence(detectionFlags);

        if (isLegacy)
        {
            ModkitLog.Info($"[LegacyInstallDetector] Legacy install detected with {issues.Count} issue(s), confidence: {confidence:P0}");
            foreach (var issue in issues)
            {
                ModkitLog.Info($"[LegacyInstallDetector]   - {issue}");
            }
        }
        else
        {
            ModkitLog.Info("[LegacyInstallDetector] No legacy patterns detected");
        }

        return new LegacyDetectionResult
        {
            IsLegacyInstall = isLegacy,
            HasUnbackedOriginals = detectionFlags.UnbackedOriginals,
            HasNoProvenance = detectionFlags.NoProvenance,
            HasOldModsLayout = detectionFlags.OldModsLayout,
            HasLegacyDependencies = detectionFlags.LegacyDependencies,
            DetectedIssues = issues,
            ConfidenceScore = confidence
        };
    }

    /// <summary>
    /// Check for .original files without backup-metadata.json beside them.
    /// </summary>
    private void CheckUnbackedOriginals(string gamePath, List<string> issues, DetectionFlags flags)
    {
        var gameDataDir = FindGameDataDirectory(gamePath);
        if (string.IsNullOrEmpty(gameDataDir))
            return;

        var originalFiles = new[] { "resources.assets.original", "globalgamemanagers.original" };
        var foundOriginals = new List<string>();

        foreach (var originalFileName in originalFiles)
        {
            var originalPath = Path.Combine(gameDataDir, originalFileName);
            if (File.Exists(originalPath))
            {
                foundOriginals.Add(originalFileName);
            }
        }

        if (foundOriginals.Count == 0)
            return;

        // Check if backup-metadata.json exists
        var metadataPath = Path.Combine(gameDataDir, "backup-metadata.json");
        if (!File.Exists(metadataPath))
        {
            flags.UnbackedOriginals = true;
            issues.Add($"Found {foundOriginals.Count} .original backup file(s) without backup-metadata.json tracking");
        }
    }

    /// <summary>
    /// Check for components installed without provenance tracking.
    /// </summary>
    private void CheckComponentProvenance(List<string> issues, DetectionFlags flags)
    {
        var componentsCachePath = ComponentManager.Instance.ComponentsCachePath;

        if (!Directory.Exists(componentsCachePath))
            return;

        var manifestPath = Path.Combine(componentsCachePath, "manifest.json");

        // Check if any component directories exist but no manifest
        var componentDirs = Directory.GetDirectories(componentsCachePath);
        var hasComponents = componentDirs.Any(d =>
            !Path.GetFileName(d).StartsWith(".") && // Skip hidden directories
            !Path.GetFileName(d).Equals("update-staging", StringComparison.OrdinalIgnoreCase));

        if (hasComponents && !File.Exists(manifestPath))
        {
            flags.NoProvenance = true;
            issues.Add("Components exist in cache but no provenance manifest found");
        }
        else if (File.Exists(manifestPath))
        {
            // Validate the manifest has proper provenance info
            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<LocalManifest>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (manifest?.Components != null)
                {
                    // Check if any installed components lack InstalledAt timestamps (old format)
                    var componentsWithoutTimestamp = manifest.Components
                        .Where(kvp => kvp.Value.InstalledAt == default)
                        .ToList();

                    if (componentsWithoutTimestamp.Count > 0)
                    {
                        flags.NoProvenance = true;
                        issues.Add($"{componentsWithoutTimestamp.Count} component(s) missing installation timestamps");
                    }
                }
            }
            catch
            {
                // Malformed manifest counts as no provenance
                flags.NoProvenance = true;
                issues.Add("Component manifest exists but is malformed");
            }
        }
    }

    /// <summary>
    /// Check for old Mods layout patterns.
    /// </summary>
    private void CheckOldModsLayout(string gamePath, List<string> issues, DetectionFlags flags)
    {
        var modsPath = Path.Combine(gamePath, "Mods");
        if (!Directory.Exists(modsPath))
            return;

        ModkitLog.Info($"[LegacyDetector] Checking Mods layout in: {modsPath}");

        // Check for loose DLLs directly in Mods/ that aren't Menace.* infrastructure
        var looseDlls = Directory.GetFiles(modsPath, "*.dll")
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Where(name => !name!.StartsWith("Menace.", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (looseDlls.Count > 0)
        {
            flags.OldModsLayout = true;
            issues.Add($"Found {looseDlls.Count} loose DLL(s) in Mods/ (expected in modpack subfolders): {string.Join(", ", looseDlls.Take(3))}");
            ModkitLog.Warn($"[LegacyDetector] Loose DLLs in Mods root: {string.Join(", ", looseDlls)}");
        }

        // Check for DLL files that look like they're from old manual mod installations
        // (not in a proper modpack subfolder with modpack.json)
        var subDirs = Directory.GetDirectories(modsPath);
        foreach (var subDir in subDirs)
        {
            var dirName = Path.GetFileName(subDir);

            // Skip compiled output directories
            if (dirName.Equals("compiled", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("dll", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("dlls", StringComparison.OrdinalIgnoreCase))
                continue;

            // Check if directory has DLLs (in any subdirectory, including dlls/)
            var dllFiles = Directory.GetFiles(subDir, "*.dll", SearchOption.AllDirectories);
            if (dllFiles.Length == 0)
                continue;

            // Check for modpack indicators in the TOP LEVEL of this directory only
            var hasModpackJson = ModpackIndicatorFiles.Any(f =>
                File.Exists(Path.Combine(subDir, f)));

            if (!hasModpackJson)
            {
                // Additional check: Skip if DLLs are ONLY in a dlls/ subdirectory
                // (legitimate deploy structure is <modpack>/dlls/*.dll with <modpack>/modpack.json)
                var dllsSubdir = Path.Combine(subDir, "dlls");
                var allDllsInSubdir = dllFiles.All(dll =>
                    dll.StartsWith(dllsSubdir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    dll.StartsWith(dllsSubdir + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

                if (allDllsInSubdir)
                {
                    // This looks like a properly deployed modpack that's missing its modpack.json
                    // (possibly mid-deploy or corrupted). Log but don't flag as legacy.
                    ModkitLog.Warn($"[LegacyDetector] Directory '{dirName}' has dlls/ subdirectory but no modpack.json - may be mid-deploy or corrupted");
                    continue;
                }

                // DLLs are scattered or in root - this is likely a legacy manual install
                flags.OldModsLayout = true;
                var dllCount = dllFiles.Length;
                var dllLocations = string.Join(", ", dllFiles.Take(3).Select(Path.GetFileName));
                issues.Add($"Directory '{dirName}' in Mods/ has {dllCount} DLL(s) but no modpack.json (possible legacy manual install): {dllLocations}");
                ModkitLog.Warn($"[LegacyDetector] Potential legacy install in '{dirName}': {dllCount} DLLs, no modpack.json");
            }
        }
    }

    /// <summary>
    /// Check for .original files not tracked in deploy-state.json.
    /// </summary>
    private void CheckUntrackedOriginals(string gamePath, List<string> issues, DetectionFlags flags)
    {
        var gameDataDir = FindGameDataDirectory(gamePath);
        if (string.IsNullOrEmpty(gameDataDir))
            return;

        // Find all .original files
        var originalFiles = new[] { "resources.assets.original", "globalgamemanagers.original" }
            .Where(f => File.Exists(Path.Combine(gameDataDir, f)))
            .ToList();

        if (originalFiles.Count == 0)
            return;

        // Load deploy state to see if we're tracking these
        var deployStatePath = GetDeployStatePath(gamePath);
        if (string.IsNullOrEmpty(deployStatePath) || !File.Exists(deployStatePath))
        {
            // No deploy state but .original files exist = untracked
            flags.UnbackedOriginals = true; // Overlaps with unbacked, but distinct issue
            issues.Add($"Found {originalFiles.Count} .original file(s) but no deploy-state.json to track them");
            return;
        }

        // If deploy state exists but is empty or has no modpacks, the originals are orphaned
        try
        {
            var state = DeployState.LoadFrom(deployStatePath);
            if (state.DeployedModpacks.Count == 0 && state.DeployedFiles.Count == 0)
            {
                // Deploy state is empty but originals exist - user may have manually created them
                // or a previous undeploy left orphaned backups
                issues.Add("Empty deploy state but .original backup files exist (orphaned backups)");
            }
        }
        catch
        {
            issues.Add("Deploy state exists but is malformed");
        }
    }

    /// <summary>
    /// Check for legacy dependency DLLs in Mods/ instead of UserLibs/.
    /// </summary>
    private void CheckLegacyDependencies(string gamePath, List<string> issues, DetectionFlags flags)
    {
        var modsPath = Path.Combine(gamePath, "Mods");
        if (!Directory.Exists(modsPath))
            return;

        var userLibsPath = Path.Combine(gamePath, "UserLibs");
        var legacyDepsInMods = new List<string>();

        foreach (var dllPath in Directory.GetFiles(modsPath, "*.dll"))
        {
            var dllName = Path.GetFileName(dllPath);
            if (KnownSupportLibraries.Contains(dllName))
            {
                legacyDepsInMods.Add(dllName);
            }
        }

        if (legacyDepsInMods.Count > 0)
        {
            flags.LegacyDependencies = true;
            issues.Add($"Found {legacyDepsInMods.Count} support library/libraries in Mods/ instead of UserLibs/: {string.Join(", ", legacyDepsInMods.Take(3))}");
        }

        // Also check UserLibs for DLLs that aren't from current ModpackLoader
        // This is harder to detect without version info, so we'll just note the existence
        if (Directory.Exists(userLibsPath))
        {
            var userLibDlls = Directory.GetFiles(userLibsPath, "*.dll");
            var unexpectedDlls = userLibDlls
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .Where(name => !KnownSupportLibraries.Contains(name!))
                .Where(name => !name!.StartsWith("Menace.", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (unexpectedDlls.Count > 0)
            {
                // This isn't necessarily a problem, just informational
                // Don't set a flag, just note it
                issues.Add($"UserLibs/ contains {unexpectedDlls.Count} unexpected DLL(s): {string.Join(", ", unexpectedDlls.Take(3))}");
            }
        }
    }

    /// <summary>
    /// Find the game's data directory (e.g., Menace_Data).
    /// </summary>
    private string? FindGameDataDirectory(string gamePath)
    {
        try
        {
            var dataDirs = Directory.GetDirectories(gamePath, "*_Data");
            return dataDirs.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get the path to deploy-state.json.
    /// </summary>
    private string? GetDeployStatePath(string gamePath)
    {
        // Deploy state is stored in ~/Documents/MenaceModkit/deploy-state.json
        // (parent of staging directory, which is ~/Documents/MenaceModkit/staging/)
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var modkitPath = Path.Combine(documentsPath, "MenaceModkit");
        var deployStatePath = Path.Combine(modkitPath, "deploy-state.json");

        if (File.Exists(deployStatePath))
            return deployStatePath;

        return null;
    }

    /// <summary>
    /// Calculate confidence score based on detected issues.
    /// Higher confidence means we understand the state well enough to migrate.
    /// Lower confidence means we should recommend a clean reset.
    /// </summary>
    private float CalculateConfidence(DetectionFlags flags)
    {
        if (!flags.HasAnyIssue)
            return 1.0f; // No issues = fully confident it's a clean install

        float confidence = 1.0f;

        // Unbacked originals are recoverable - moderate confidence reduction
        if (flags.UnbackedOriginals)
            confidence -= 0.2f;

        // No provenance means we don't know what's installed - larger reduction
        if (flags.NoProvenance)
            confidence -= 0.3f;

        // Old mods layout is common and understandable - small reduction
        if (flags.OldModsLayout)
            confidence -= 0.15f;

        // Legacy dependencies are easy to fix - small reduction
        if (flags.LegacyDependencies)
            confidence -= 0.1f;

        return Math.Max(0.0f, confidence);
    }

    /// <summary>
    /// Internal tracking of detection flags.
    /// </summary>
    private class DetectionFlags
    {
        public bool UnbackedOriginals { get; set; }
        public bool NoProvenance { get; set; }
        public bool OldModsLayout { get; set; }
        public bool LegacyDependencies { get; set; }

        public bool HasAnyIssue =>
            UnbackedOriginals || NoProvenance || OldModsLayout || LegacyDependencies;
    }
}
