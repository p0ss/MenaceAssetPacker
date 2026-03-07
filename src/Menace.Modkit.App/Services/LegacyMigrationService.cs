using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Service for migrating legacy Modkit installations to the new format.
/// Handles both in-place migration and clean reset operations.
/// </summary>
public class LegacyMigrationService
{
    private static readonly Lazy<LegacyMigrationService> _instance = new(() => new LegacyMigrationService());
    public static LegacyMigrationService Instance => _instance.Value;

    private LegacyMigrationService() { }

    /// <summary>
    /// Result of a migration operation.
    /// </summary>
    public record MigrationResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public List<string> Details { get; init; } = new();
    }

    /// <summary>
    /// Attempts to migrate an existing legacy installation in place.
    /// This preserves existing mods while fixing structural issues.
    /// </summary>
    /// <param name="gamePath">The game installation path.</param>
    /// <param name="detectionResult">The legacy detection result with issue details.</param>
    /// <param name="progress">Optional progress callback.</param>
    /// <returns>Result of the migration operation.</returns>
    public async Task<MigrationResult> MigrateExistingAsync(
        string gamePath,
        LegacyInstallDetector.LegacyDetectionResult detectionResult,
        IProgress<string>? progress = null)
    {
        var details = new List<string>();

        try
        {
            ModkitLog.Info("[LegacyMigrationService] Starting in-place migration");
            progress?.Report("Starting migration...");

            // 1. Create backup-metadata.json if missing
            if (detectionResult.HasUnbackedOriginals)
            {
                progress?.Report("Creating backup metadata...");
                var backupResult = await CreateBackupMetadataAsync(gamePath);
                details.Add(backupResult);
                ModkitLog.Info($"[LegacyMigrationService] {backupResult}");
            }

            // 2. Move legacy dependencies from Mods/ to UserLibs/
            if (detectionResult.HasLegacyDependencies)
            {
                progress?.Report("Moving support libraries to UserLibs...");
                var moveResult = await MoveLegacyDependenciesAsync(gamePath);
                details.Add(moveResult);
                ModkitLog.Info($"[LegacyMigrationService] {moveResult}");
            }

            // 3. Create component provenance manifest if missing
            if (detectionResult.HasNoProvenance)
            {
                progress?.Report("Creating component provenance manifest...");
                var provenanceResult = await CreateProvenanceManifestAsync();
                details.Add(provenanceResult);
                ModkitLog.Info($"[LegacyMigrationService] {provenanceResult}");
            }

            // 4. Handle old Mods layout issues (informational - can't automatically fix)
            if (detectionResult.HasOldModsLayout)
            {
                details.Add("Old Mods layout detected - manual review recommended for loose DLLs");
                ModkitLog.Info("[LegacyMigrationService] Old Mods layout requires manual review");
            }

            // Invalidate health cache so next check reflects changes
            InstallHealthService.Instance.InvalidateCache();

            progress?.Report("Migration complete!");
            ModkitLog.Info("[LegacyMigrationService] Migration completed successfully");

            return new MigrationResult
            {
                Success = true,
                Message = "Migration completed successfully",
                Details = details
            };
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[LegacyMigrationService] Migration failed: {ex.Message}");
            return new MigrationResult
            {
                Success = false,
                Message = $"Migration failed: {ex.Message}",
                Details = details
            };
        }
    }

    /// <summary>
    /// Performs a clean reset, restoring vanilla game files and removing mod infrastructure.
    /// Preserves user-authored modpacks in the staging directory.
    /// </summary>
    /// <param name="gamePath">The game installation path.</param>
    /// <param name="progress">Optional progress callback.</param>
    /// <returns>Result of the reset operation.</returns>
    public async Task<MigrationResult> CleanResetAsync(
        string gamePath,
        IProgress<string>? progress = null)
    {
        var details = new List<string>();

        try
        {
            ModkitLog.Info("[LegacyMigrationService] Starting clean reset");
            progress?.Report("Starting clean reset...");

            var gameDataDir = FindGameDataDirectory(gamePath);

            // CRITICAL: First restore vanilla game files from .original backups
            // This must happen BEFORE we delete anything else
            if (!string.IsNullOrEmpty(gameDataDir))
            {
                var filesToRestore = new[] { "resources.assets", "globalgamemanagers" };
                foreach (var fileName in filesToRestore)
                {
                    var originalPath = Path.Combine(gameDataDir, fileName + ".original");
                    var targetPath = Path.Combine(gameDataDir, fileName);

                    if (File.Exists(originalPath))
                    {
                        progress?.Report($"Restoring vanilla {fileName}...");
                        await Task.Run(() =>
                        {
                            // Delete the patched file first
                            if (File.Exists(targetPath))
                                File.Delete(targetPath);
                            // Copy backup to restore vanilla (keep backup for safety)
                            File.Copy(originalPath, targetPath);
                        });
                        details.Add($"Restored vanilla {fileName}");
                        ModkitLog.Info($"[LegacyMigrationService] Restored vanilla {fileName}");
                    }
                }

                // Now safe to remove backups and metadata
                var backupMetadataPath = Path.Combine(gameDataDir, "backup-metadata.json");
                if (File.Exists(backupMetadataPath))
                {
                    await Task.Run(() => File.Delete(backupMetadataPath));
                    details.Add("Removed backup-metadata.json");
                    ModkitLog.Info("[LegacyMigrationService] Removed backup-metadata.json");
                }

                foreach (var fileName in filesToRestore)
                {
                    var originalPath = Path.Combine(gameDataDir, fileName + ".original");
                    if (File.Exists(originalPath))
                    {
                        await Task.Run(() => File.Delete(originalPath));
                        details.Add($"Removed {fileName}.original");
                        ModkitLog.Info($"[LegacyMigrationService] Removed {fileName}.original");
                    }
                }
            }

            // 2. Delete Mods/ directory (deployed mods)
            var modsPath = Path.Combine(gamePath, "Mods");
            if (Directory.Exists(modsPath))
            {
                progress?.Report("Removing Mods directory...");
                await Task.Run(() => Directory.Delete(modsPath, recursive: true));
                details.Add("Removed Mods/ directory");
                ModkitLog.Info("[LegacyMigrationService] Removed Mods/ directory");
            }

            // 3. Delete UserLibs/ directory
            var userLibsPath = Path.Combine(gamePath, "UserLibs");
            if (Directory.Exists(userLibsPath))
            {
                progress?.Report("Removing UserLibs directory...");
                await Task.Run(() => Directory.Delete(userLibsPath, recursive: true));
                details.Add("Removed UserLibs/ directory");
                ModkitLog.Info("[LegacyMigrationService] Removed UserLibs/ directory");
            }

            // 4. Delete MelonLoader directory (mod loader)
            // CRITICAL: Preserve Il2CppAssemblies - these are build dependencies, not user mods
            var melonLoaderPath = Path.Combine(gamePath, "MelonLoader");
            if (Directory.Exists(melonLoaderPath))
            {
                var il2cppAssembliesPath = Path.Combine(melonLoaderPath, "Il2CppAssemblies");
                List<string>? preservedAssemblies = null;

                // Backup Il2CppAssemblies if they exist
                if (Directory.Exists(il2cppAssembliesPath))
                {
                    progress?.Report("Preserving IL2CPP proxy assemblies...");
                    var tempBackupPath = Path.Combine(Path.GetTempPath(), $"Il2CppAssemblies_backup_{Guid.NewGuid()}");
                    await Task.Run(() =>
                    {
                        Directory.CreateDirectory(tempBackupPath);
                        foreach (var file in Directory.GetFiles(il2cppAssembliesPath))
                        {
                            var fileName = Path.GetFileName(file);
                            File.Copy(file, Path.Combine(tempBackupPath, fileName));
                        }
                    });
                    preservedAssemblies = Directory.GetFiles(tempBackupPath).Select(Path.GetFileName).Where(n => n != null).ToList()!;
                    ModkitLog.Info($"[LegacyMigrationService] Backed up {preservedAssemblies.Count} IL2CPP assemblies to temp");

                    // Delete MelonLoader
                    progress?.Report("Removing MelonLoader...");
                    await Task.Run(() => Directory.Delete(melonLoaderPath, recursive: true));
                    details.Add("Removed MelonLoader/ directory");
                    ModkitLog.Info("[LegacyMigrationService] Removed MelonLoader/ directory");

                    // Restore Il2CppAssemblies
                    progress?.Report("Restoring IL2CPP proxy assemblies...");
                    await Task.Run(() =>
                    {
                        Directory.CreateDirectory(melonLoaderPath);
                        Directory.CreateDirectory(il2cppAssembliesPath);
                        foreach (var file in Directory.GetFiles(tempBackupPath))
                        {
                            var fileName = Path.GetFileName(file);
                            File.Move(file, Path.Combine(il2cppAssembliesPath, fileName));
                        }
                        Directory.Delete(tempBackupPath);
                    });
                    details.Add($"Preserved {preservedAssemblies!.Count} IL2CPP proxy assemblies (required for code mod compilation)");
                    ModkitLog.Info($"[LegacyMigrationService] Restored {preservedAssemblies.Count} IL2CPP assemblies");
                }
                else
                {
                    // No Il2CppAssemblies to preserve - just delete MelonLoader
                    progress?.Report("Removing MelonLoader...");
                    await Task.Run(() => Directory.Delete(melonLoaderPath, recursive: true));
                    details.Add("Removed MelonLoader/ directory");
                    ModkitLog.Info("[LegacyMigrationService] Removed MelonLoader/ directory (no Il2CppAssemblies found)");
                }
            }

            // 5. Delete deploy-state.json only (NOT staging - user modpacks live there)
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var modkitPath = Path.Combine(documentsPath, "MenaceModkit");
            var deployStatePath = Path.Combine(modkitPath, "deploy-state.json");
            if (File.Exists(deployStatePath))
            {
                progress?.Report("Removing deploy state...");
                await Task.Run(() => File.Delete(deployStatePath));
                details.Add("Removed deploy-state.json");
                ModkitLog.Info("[LegacyMigrationService] Removed deploy-state.json");
            }

            // NOTE: We intentionally preserve ~/Documents/MenaceModkit/staging/
            // This contains user-authored modpacks that shouldn't be deleted
            details.Add("Preserved user modpacks in staging directory");

            // Invalidate health cache
            InstallHealthService.Instance.InvalidateCache();

            progress?.Report("Clean reset complete!");
            ModkitLog.Info("[LegacyMigrationService] Clean reset completed successfully");

            return new MigrationResult
            {
                Success = true,
                Message = "Clean reset completed. Vanilla game files restored. Your modpacks are preserved.",
                Details = details
            };
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[LegacyMigrationService] Clean reset failed: {ex.Message}");
            return new MigrationResult
            {
                Success = false,
                Message = $"Clean reset failed: {ex.Message}",
                Details = details
            };
        }
    }

    /// <summary>
    /// Creates backup-metadata.json for existing .original files.
    /// Uses BackupMetadata class for proper hash tracking and validation support.
    /// </summary>
    private async Task<string> CreateBackupMetadataAsync(string gamePath)
    {
        var gameDataDir = FindGameDataDirectory(gamePath);
        if (string.IsNullOrEmpty(gameDataDir))
            return "Could not find game data directory";

        var originalFiles = new[] { "resources.assets.original", "globalgamemanagers.original" };
        var existingBackups = originalFiles
            .Where(f => File.Exists(Path.Combine(gameDataDir, f)))
            .ToList();

        if (existingBackups.Count == 0)
            return "No .original files found to track";

        // Detect game version from globalgamemanagers (use the backup if it exists, else original)
        var gameVersion = DetectGameVersionForLegacy(gameDataDir);

        // Create metadata with full hash computation for proper validation
        // This runs synchronously but we wrap in Task.Run to not block UI
        var metadata = await Task.Run(() => BackupMetadata.Create(gameDataDir, gameVersion, existingBackups));
        metadata.MigratedFromLegacy = true;

        // For legacy installs, use the backup file's timestamp as the backup creation time
        // (since we don't know when the original backup was actually made)
        var oldestBackup = existingBackups
            .Select(f => new FileInfo(Path.Combine(gameDataDir, f)))
            .OrderBy(fi => fi.LastWriteTimeUtc)
            .FirstOrDefault();
        if (oldestBackup != null)
        {
            metadata.BackupCreatedAt = oldestBackup.LastWriteTimeUtc;
        }

        metadata.SaveTo(gameDataDir);

        return $"Created backup-metadata.json tracking {existingBackups.Count} backup file(s) with SHA256 hashes";
    }

    /// <summary>
    /// Detect game version for legacy migration.
    /// Checks globalgamemanagers.original first, then falls back to globalgamemanagers.
    /// </summary>
    private static string DetectGameVersionForLegacy(string gameDataDir)
    {
        // For legacy detection, prefer the backup file (it's the original vanilla version)
        var ggmBackupPath = Path.Combine(gameDataDir, "globalgamemanagers.original");
        var ggmPath = Path.Combine(gameDataDir, "globalgamemanagers");

        var targetPath = File.Exists(ggmBackupPath) ? ggmBackupPath : ggmPath;
        if (!File.Exists(targetPath))
            return "unknown";

        try
        {
            using var fs = File.OpenRead(targetPath);
            using var reader = new BinaryReader(fs);

            fs.Seek(0x14, SeekOrigin.Begin);
            var bytes = new List<byte>();
            byte b;
            while ((b = reader.ReadByte()) != 0 && bytes.Count < 50)
                bytes.Add(b);

            var unityVersion = System.Text.Encoding.ASCII.GetString(bytes.ToArray());
            var fileSize = new FileInfo(targetPath).Length;
            return $"{unityVersion}_{fileSize}";
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Moves support libraries from Mods/ to UserLibs/.
    /// </summary>
    private async Task<string> MoveLegacyDependenciesAsync(string gamePath)
    {
        var modsPath = Path.Combine(gamePath, "Mods");
        var userLibsPath = Path.Combine(gamePath, "UserLibs");

        if (!Directory.Exists(modsPath))
            return "Mods/ directory not found";

        // Known support libraries that belong in UserLibs
        var supportLibraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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

        var moved = 0;

        foreach (var dllPath in Directory.GetFiles(modsPath, "*.dll"))
        {
            var dllName = Path.GetFileName(dllPath);
            if (supportLibraries.Contains(dllName))
            {
                Directory.CreateDirectory(userLibsPath);
                var targetPath = Path.Combine(userLibsPath, dllName);

                // Don't overwrite if already exists in UserLibs
                if (!File.Exists(targetPath))
                {
                    await Task.Run(() => File.Move(dllPath, targetPath));
                    moved++;
                }
                else
                {
                    // Remove duplicate from Mods/
                    await Task.Run(() => File.Delete(dllPath));
                    moved++;
                }
            }
        }

        return moved > 0
            ? $"Moved {moved} support library/libraries to UserLibs/"
            : "No support libraries found to move";
    }

    /// <summary>
    /// Creates or updates the component provenance manifest.
    /// </summary>
    private async Task<string> CreateProvenanceManifestAsync()
    {
        var componentsCachePath = ComponentManager.Instance.ComponentsCachePath;
        var manifestPath = Path.Combine(componentsCachePath, "manifest.json");

        // Check what's installed
        var installedComponents = new Dictionary<string, object>();

        // Check for DataExtractor
        var dataExtractorPath = ComponentManager.Instance.GetDataExtractorPath();
        if (!string.IsNullOrEmpty(dataExtractorPath))
        {
            installedComponents["DataExtractor"] = new
            {
                InstalledAt = DateTime.UtcNow.ToString("O"),
                Version = "unknown",
                MigratedFromLegacy = true
            };
        }

        // Check for ModpackLoader
        var modpackLoaderPath = ComponentManager.Instance.GetModpackLoaderPath();
        if (!string.IsNullOrEmpty(modpackLoaderPath))
        {
            installedComponents["ModpackLoader"] = new
            {
                InstalledAt = DateTime.UtcNow.ToString("O"),
                Version = "unknown",
                MigratedFromLegacy = true
            };
        }

        if (installedComponents.Count == 0)
            return "No components found to create provenance for";

        var manifest = new
        {
            MigratedAt = DateTime.UtcNow.ToString("O"),
            Components = installedComponents
        };

        Directory.CreateDirectory(componentsCachePath);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(manifestPath, json);

        return $"Created provenance manifest for {installedComponents.Count} component(s)";
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
}
