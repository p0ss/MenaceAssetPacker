using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Menace.Modkit.App.Models;
using Menace.Modkit.Core.Bundles;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Owns the game's Mods/ folder. Handles full deploy/undeploy pipeline:
/// resolve active modpacks in load order → merge data patches (last-wins) →
/// copy assets → write merged runtime manifests → clean removed mods.
/// </summary>
public class DeployManager
{
    private readonly ModpackManager _modpackManager;
    private readonly CompilationService _compilationService = new();
    private string DeployStateFilePath =>
        Path.Combine(Path.GetDirectoryName(_modpackManager.StagingBasePath)!, "deploy-state.json");

    public DeployManager(ModpackManager modpackManager)
    {
        _modpackManager = modpackManager;
    }

    /// <summary>
    /// Detect if the game has been updated and clean up stale files if needed.
    /// Returns true if cleanup was performed (requires user to verify game files via Steam).
    /// </summary>
    private async Task<(bool CleanupPerformed, string? Message)> DetectAndHandleGameUpdate(string modsBasePath)
    {
        var gameInstallPath = _modpackManager.GetGameInstallPath();
        if (string.IsNullOrEmpty(gameInstallPath))
            return (false, null);

        var gameDataDir = Directory.GetDirectories(gameInstallPath, "*_Data").FirstOrDefault();
        if (string.IsNullOrEmpty(gameDataDir))
            return (false, null);

        // Try to read game version from globalgamemanagers
        var currentVersion = await Task.Run(() => DetectGameVersion(gameInstallPath));
        if (string.IsNullOrEmpty(currentVersion))
            return (false, null);

        var previousState = DeployState.LoadFrom(DeployStateFilePath);
        var previousVersion = previousState.GameVersion;

        // If no previous version stored, just record current version
        if (string.IsNullOrEmpty(previousVersion))
        {
            ModkitLog.Info($"[DeployManager] Recording game version: {currentVersion}");
            return (false, null);
        }

        // Check if version changed
        if (currentVersion == previousVersion)
            return (false, null);

        // Game has been updated!
        ModkitLog.Warn($"[DeployManager] Game update detected: {previousVersion} → {currentVersion}");
        ModkitLog.Warn($"[DeployManager] Cleaning up stale patched files from previous version...");

        // Delete patched game data files
        var filesToDelete = new[] { "resources.assets", "globalgamemanagers" };
        foreach (var fileName in filesToDelete)
        {
            var filePath = Path.Combine(gameDataDir, fileName);
            var backupPath = Path.Combine(gameDataDir, fileName + ".original");

            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    ModkitLog.Info($"[DeployManager] Deleted stale patched file: {fileName}");
                }
                catch (Exception ex)
                {
                    ModkitLog.Error($"[DeployManager] Failed to delete {fileName}: {ex.Message}");
                }
            }

            if (File.Exists(backupPath))
            {
                try
                {
                    File.Delete(backupPath);
                    ModkitLog.Info($"[DeployManager] Deleted stale backup: {fileName}.original");
                }
                catch (Exception ex)
                {
                    ModkitLog.Error($"[DeployManager] Failed to delete {fileName}.original: {ex.Message}");
                }
            }
        }

        // Delete compiled mod assets (they may be incompatible with new version)
        var compiledDir = Path.Combine(modsBasePath, "compiled");
        if (Directory.Exists(compiledDir))
        {
            try
            {
                Directory.Delete(compiledDir, true);
                ModkitLog.Info($"[DeployManager] Deleted compiled assets directory");
            }
            catch (Exception ex)
            {
                ModkitLog.Error($"[DeployManager] Failed to delete compiled directory: {ex.Message}");
            }
        }

        var message = $"Game update detected ({previousVersion} → {currentVersion}).\n\n" +
                     $"Cleaned up stale files from the previous version.\n\n" +
                     $"ACTION REQUIRED:\n" +
                     $"1. Verify game files via Steam to download updated game data\n" +
                     $"2. Then redeploy mods\n\n" +
                     $"In Steam: Right-click Menace → Properties → Installed Files → Verify integrity";

        return (true, message);
    }

    /// <summary>
    /// Detect the game version by reading from globalgamemanagers or app info.
    /// Returns null if detection fails.
    /// </summary>
    private static string? DetectGameVersion(string gameInstallPath)
    {
        // Try reading from globalgamemanagers
        var dataDirs = Directory.GetDirectories(gameInstallPath, "*_Data");
        foreach (var dataDir in dataDirs)
        {
            var ggmPath = Path.Combine(dataDir, "globalgamemanagers");
            if (!File.Exists(ggmPath))
                continue;

            try
            {
                // Read the Unity version string from globalgamemanagers
                // Format: offset 0x14 contains a null-terminated version string
                using var fs = File.OpenRead(ggmPath);
                using var reader = new BinaryReader(fs);

                // Skip to version offset
                fs.Seek(0x14, SeekOrigin.Begin);
                var bytes = new List<byte>();
                byte b;
                while ((b = reader.ReadByte()) != 0 && bytes.Count < 50)
                    bytes.Add(b);

                var unityVersion = System.Text.Encoding.ASCII.GetString(bytes.ToArray());

                // Also try to get game-specific version from nearby bytes
                // For now, return Unity version + file size as a fingerprint
                var fileSize = new FileInfo(ggmPath).Length;
                return $"{unityVersion}_{fileSize}";
            }
            catch
            {
                // Fall back to file checksum if version reading fails
                try
                {
                    var fileInfo = new FileInfo(ggmPath);
                    return $"build_{fileInfo.Length}_{fileInfo.LastWriteTimeUtc:yyyyMMddHHmmss}";
                }
                catch
                {
                    continue;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Deploy a single staging modpack to the game's Mods/ folder (with compilation).
    /// </summary>
    public async Task<DeployResult> DeploySingleAsync(ModpackManifest modpack, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var modsBasePath = _modpackManager.ModsBasePath;
        if (string.IsNullOrEmpty(modsBasePath))
            return new DeployResult { Success = false, Message = "Game install path not set" };

        // Check for game updates and clean up stale files if needed
        progress?.Report("Checking game version...");
        var (cleanupPerformed, cleanupMessage) = await DetectAndHandleGameUpdate(modsBasePath);
        if (cleanupPerformed)
        {
            return new DeployResult
            {
                Success = false,
                Message = cleanupMessage ?? "Game update detected. Please verify game files via Steam before deploying mods."
            };
        }

        try
        {
            // Refresh runtime DLLs from bundled directory to pick up latest builds
            _modpackManager.RefreshRuntimeDlls();

            // Deploy runtime DLLs first so ModpackLoader.dll is available as a reference
            progress?.Report("Deploying runtime DLLs...");
            await Task.Run(() => DeployRuntimeDlls(modsBasePath), ct);

            // Compile source code if present
            if (modpack.Code.HasAnySources)
            {
                progress?.Report($"Compiling {modpack.Name}...");
                ModkitLog.Info($"Compiling {modpack.Name}: sources={string.Join(", ", modpack.Code.Sources)}, refs={string.Join(", ", modpack.Code.References)}");
                var compileResult = await _compilationService.CompileModpackAsync(modpack, ct);
                foreach (var diag in compileResult.Diagnostics)
                    ModkitLog.Info($"  [{diag.Severity}] {diag.File}:{diag.Line} — {diag.Message}");
                if (!compileResult.Success)
                {
                    // Include both errors and warnings - warnings often explain WHY errors occurred
                    // (e.g., "Il2CppAssemblies not found" warning explains "UnityEngine not found" errors)
                    var errors = string.Join("\n", compileResult.Diagnostics
                        .Where(d => d.Severity == Models.DiagnosticSeverity.Error)
                        .Select(d => $"{d.File}:{d.Line} — {d.Message}"));
                    var warnings = compileResult.Diagnostics
                        .Where(d => d.Severity == Models.DiagnosticSeverity.Warning)
                        .Select(d => d.Message)
                        .ToList();
                    var msg = $"Compile failed for {modpack.Name}:\n{errors}";
                    if (warnings.Count > 0)
                        msg += $"\n\nPossible causes:\n• " + string.Join("\n• ", warnings);
                    ModkitLog.Error(msg);
                    return new DeployResult { Success = false, Message = msg };
                }
                ModkitLog.Info($"Compiled {modpack.Name} → {compileResult.OutputDllPath}");
            }

            progress?.Report($"Deploying {modpack.Name}...");
            await Task.Run(() => DeployModpack(modpack, modsBasePath), ct);

            // NOTE: No need to restore .original files here before compilation.
            // BundleCompiler.CompileDataPatchBundleAsync already prefers .original backup files
            // over potentially-modified live files.

            // Compile merged bundle with all staging modpacks (not just this one)
            // This ensures clones, patches, and audio work correctly
            progress?.Report("Compiling asset bundles...");
            var allModpacks = _modpackManager.GetStagingModpacks()
                .Where(m => !IsDevOnlyModpack(m.Name) || AppSettings.Instance.EnableDeveloperTools)
                .OrderBy(m => m.LoadOrder)
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            await TryCompileBundleAsync(allModpacks, modsBasePath, ct);

            // Deploy patched game data
            progress?.Report("Deploying patched game data...");
            await Task.Run(() => DeployPatchedGameData(modsBasePath), ct);

            ModkitLog.Info($"Deployed {modpack.Name} to {modsBasePath}");
            progress?.Report($"Deployed {modpack.Name}");
            return new DeployResult { Success = true, Message = $"Deployed {modpack.Name}", DeployedCount = 1 };
        }
        catch (OperationCanceledException)
        {
            return new DeployResult { Success = false, Message = "Deployment cancelled" };
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"Deploy single failed: {ex}");
            return new DeployResult { Success = false, Message = $"Deploy failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// Deploy all active staging modpacks to the game's Mods/ folder.
    /// Uses transactional deployment: all files are staged first, then committed atomically.
    /// On any failure, changes are rolled back to preserve the previous state.
    /// </summary>
    public async Task<DeployResult> DeployAllAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var modsBasePath = _modpackManager.ModsBasePath;
        if (string.IsNullOrEmpty(modsBasePath))
            return new DeployResult { Success = false, Message = "Game install path not set" };

        // Check for game updates and clean up stale files if needed
        progress?.Report("Checking game version...");
        var (cleanupPerformed, cleanupMessage) = await DetectAndHandleGameUpdate(modsBasePath);
        if (cleanupPerformed)
        {
            // Game was updated - user needs to verify files via Steam first
            return new DeployResult
            {
                Success = false,
                Message = cleanupMessage ?? "Game update detected. Please verify game files via Steam before deploying mods."
            };
        }

        // Get staging modpacks, ordered by load order, excluding dev-only unless enabled.
        // Use DistinctBy on Name to avoid deploying duplicate modpacks if multiple
        // staging directories have the same manifest Name.
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var modpacks = _modpackManager.GetStagingModpacks()
            .Where(m => !IsDevOnlyModpack(m.Name) || AppSettings.Instance.EnableDeveloperTools)
            .OrderBy(m => m.LoadOrder)
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Where(m => seenNames.Add(m.Name)) // Keep first occurrence only
            .ToList();

        if (modpacks.Count == 0)
            return new DeployResult { Success = false, Message = "No staging modpacks found" };

        var previousState = DeployState.LoadFrom(DeployStateFilePath);
        var deployedFiles = new List<string>();
        var deployedFileInfos = new List<DeployedFileInfo>();
        var deployedModpacks = new List<DeployedModpack>();

        // Determine game data directory for transactional game file patching
        var gameInstallPath = _modpackManager.GetGameInstallPath();
        var gameDataDir = string.IsNullOrEmpty(gameInstallPath)
            ? null
            : Directory.GetDirectories(gameInstallPath, "*_Data").FirstOrDefault();

        // Create transaction for atomic deployment
        using var transaction = new DeployTransaction(modsBasePath, gameDataDir);

        try
        {
            // BEGIN TRANSACTION
            progress?.Report("Beginning transactional deploy...");
            transaction.Begin();
            ModkitLog.Info("[DeployManager] Transaction started - all changes will be staged before commit");

            // Step 0: Stage removal of modpacks that existed in previous deploy but aren't in current staging
            // This replaces the old CleanPreviousDeployment which happened outside the transaction.
            var currentModpackNames = new HashSet<string>(modpacks.Select(m => m.Name), StringComparer.OrdinalIgnoreCase);
            foreach (var oldModpack in previousState.DeployedModpacks)
            {
                if (string.IsNullOrWhiteSpace(oldModpack.Name))
                    continue;

                if (!currentModpackNames.Contains(oldModpack.Name))
                {
                    ModkitLog.Info($"[DeployManager] Modpack '{oldModpack.Name}' no longer in staging, scheduling removal");
                    transaction.StageDirectoryRemoval(oldModpack.Name);
                }
            }

            // Step 1: Refresh runtime DLLs from bundled directory to pick up latest builds
            // This just copies bundled DLLs to the cached runtime directory (non-destructive).
            _modpackManager.RefreshRuntimeDlls();

            // Step 2: Compile and stage each modpack
            // NOTE: Runtime DLLs are deployed AFTER commit to keep them inside the transaction boundary.
            // Modpack code compilation uses the cached runtime DLLs directory for references,
            // which doesn't require the DLLs to be deployed to Mods/ yet.
            int total = modpacks.Count;
            for (int i = 0; i < total; i++)
            {
                ct.ThrowIfCancellationRequested();
                var modpack = modpacks[i];

                // Compile source code if present
                if (modpack.Code.HasAnySources)
                {
                    progress?.Report($"Compiling {modpack.Name} ({i + 1}/{total})...");
                    ModkitLog.Info($"Compiling {modpack.Name}: sources={string.Join(", ", modpack.Code.Sources)}, refs={string.Join(", ", modpack.Code.References)}");
                    var compileResult = await _compilationService.CompileModpackAsync(modpack, ct);
                    foreach (var diag in compileResult.Diagnostics)
                        ModkitLog.Info($"  [{diag.Severity}] {diag.File}:{diag.Line} — {diag.Message}");
                    if (!compileResult.Success)
                    {
                        // Include both errors and warnings - warnings often explain WHY errors occurred
                        // (e.g., "Il2CppAssemblies not found" warning explains "UnityEngine not found" errors)
                        var errors = string.Join("\n", compileResult.Diagnostics
                            .Where(d => d.Severity == Models.DiagnosticSeverity.Error)
                            .Select(d => $"{d.File}:{d.Line} — {d.Message}"));
                        var warnings = compileResult.Diagnostics
                            .Where(d => d.Severity == Models.DiagnosticSeverity.Warning)
                            .Select(d => d.Message)
                            .ToList();
                        var msg = $"Compilation failed for {modpack.Name}:\n{errors}";
                        if (warnings.Count > 0)
                            msg += $"\n\nPossible causes:\n• " + string.Join("\n• ", warnings);
                        ModkitLog.Error(msg);
                        // Transaction will be disposed and cleaned up automatically
                        return new DeployResult { Success = false, Message = msg };
                    }
                    ModkitLog.Info($"Compiled {modpack.Name} → {compileResult.OutputDllPath}");
                }

                progress?.Report($"Staging {modpack.Name} ({i + 1}/{total})...");

                // Stage modpack to transaction (writes to .deploy-staging/)
                var (files, fileInfos) = await Task.Run(() => StageModpackToTransaction(modpack, transaction, modsBasePath), ct);
                deployedFiles.AddRange(files);
                deployedFileInfos.AddRange(fileInfos);

                deployedModpacks.Add(new DeployedModpack
                {
                    Name = modpack.Name,
                    Version = modpack.Version,
                    LoadOrder = modpack.LoadOrder,
                    ContentHash = ComputeDirectoryHash(modpack.Path),
                    SecurityStatus = modpack.SecurityStatus
                });
            }

            // NOTE: No need to restore .original files here before compilation.
            // BundleCompiler.CompileDataPatchBundleAsync already prefers .original backup files
            // over potentially-modified live files. This keeps all mutations inside the transaction.

            // Step 3: Try to compile merged patches into an asset bundle
            // Compilation happens to a temp directory, then files are staged through the transaction.
            progress?.Report("Compiling asset bundles...");
            var (bundleFiles, bundleFileInfos) = await TryCompileBundleWithInfoAsync(modpacks, modsBasePath, transaction, ct);
            deployedFiles.AddRange(bundleFiles);
            deployedFileInfos.AddRange(bundleFileInfos);
            // NOTE: Game file patches (resources.assets.patched, globalgamemanagers.patched) are now
            // staged inside TryCompileBundleWithInfoAsync while the temp compiled directory still exists.

            // COMMIT TRANSACTION - atomically move all staged files into place
            progress?.Report("Committing deployment...");
            transaction.Commit();
            ModkitLog.Info("[DeployManager] Transaction committed successfully");

            // Step 6: Deploy runtime DLLs AFTER transaction commit
            // This ensures runtime infrastructure is only deployed if the entire transaction succeeded.
            // Runtime DLLs (ModpackLoader, DataExtractor, etc.) are infrastructure and not rolled back,
            // but deploying after commit prevents orphaned runtime DLLs if transaction fails.
            progress?.Report("Deploying runtime DLLs...");
            var runtimeFiles = await Task.Run(() => DeployRuntimeDlls(modsBasePath), ct);
            deployedFiles.AddRange(runtimeFiles);

            // Step 7: Save deploy state with current game version
            var gameVersion = await Task.Run(() => DetectGameVersion(gameInstallPath ?? ""));

            var state = new DeployState
            {
                DeployedModpacks = deployedModpacks,
                DeployedFiles = deployedFiles,
                DeployedFileInfos = deployedFileInfos,
                LastDeployTimestamp = DateTime.Now,
                GameVersion = gameVersion
            };
            state.SaveTo(DeployStateFilePath);

            progress?.Report($"Deployed {total} modpack(s) successfully");
            return new DeployResult
            {
                Success = true,
                Message = $"Deployed {total} modpack(s)",
                DeployedCount = total
            };
        }
        catch (OperationCanceledException)
        {
            ModkitLog.Warn("[DeployManager] Deployment cancelled - rolling back transaction");
            // Transaction rollback/cleanup happens automatically via Dispose
            return new DeployResult { Success = false, Message = "Deployment cancelled" };
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[DeployManager] Deploy failed: {ex}");
            ModkitLog.Error("[DeployManager] Transaction will be rolled back automatically");
            // Transaction rollback/cleanup happens automatically via Dispose
            return new DeployResult { Success = false, Message = $"Deploy failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// Stage a modpack to the transaction instead of deploying directly.
    /// Files are written to .deploy-staging/ and only committed on success.
    /// </summary>
    private (List<string> Files, List<DeployedFileInfo> FileInfos) StageModpackToTransaction(
        ModpackManifest modpack, DeployTransaction transaction, string modsBasePath)
    {
        var files = new List<string>();
        var fileInfos = new List<DeployedFileInfo>();

        // Guard against empty modpack names
        if (string.IsNullOrWhiteSpace(modpack.Name))
        {
            ModkitLog.Error($"[DeployManager] Cannot stage modpack with empty name (path: {modpack.Path})");
            return (files, fileInfos);
        }

        // Create a temporary directory to prepare modpack contents
        var tempDir = Path.Combine(Path.GetTempPath(), $"modpack-stage-{Guid.NewGuid():N}");
        var deployTime = DateTime.Now;

        try
        {
            // Copy modpack contents to temp directory (applying exclusions)
            CopyDirectory(modpack.Path, tempDir);

            // Deploy DLLs to temp directory
            DeployDllsToDirectory(modpack, tempDir);

            // Build runtime manifest in temp directory
            BuildRuntimeManifest(modpack, tempDir);

            // Stage the entire prepared directory to the transaction
            transaction.StageDirectory(tempDir, modpack.Name);

            // Track what files will be deployed with detailed info
            foreach (var fullPath in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.Combine(modpack.Name, Path.GetRelativePath(tempDir, fullPath));
                files.Add(relativePath);

                try
                {
                    var fi = new FileInfo(fullPath);
                    fileInfos.Add(new DeployedFileInfo
                    {
                        RelativePath = relativePath,
                        FileHash = DeployState.ComputeFileHash(fullPath),
                        SourceModpack = modpack.Name,
                        DeployedAt = deployTime,
                        FileSize = fi.Length
                    });
                }
                catch (Exception ex)
                {
                    ModkitLog.Warn($"[DeployManager] Failed to compute file info for {relativePath}: {ex.Message}");
                }
            }

            ModkitLog.Info($"[DeployManager] Staged modpack {modpack.Name}: {files.Count} files");
        }
        finally
        {
            // Clean up temp directory
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch { /* Best effort cleanup */ }
        }

        return (files, fileInfos);
    }

    /// <summary>
    /// Copy compiled DLLs and prebuilt DLLs to a specified directory.
    /// Used by StageModpackToTransaction to prepare modpack contents.
    /// </summary>
    private void DeployDllsToDirectory(ModpackManifest modpack, string targetDir)
    {
        var dllDir = Path.Combine(targetDir, "dlls");

        // Compiled DLLs from build/
        var buildDir = Path.Combine(modpack.Path, "build");
        if (Directory.Exists(buildDir))
        {
            foreach (var dll in Directory.GetFiles(buildDir, "*.dll"))
            {
                Directory.CreateDirectory(dllDir);
                File.Copy(dll, Path.Combine(dllDir, Path.GetFileName(dll)), true);
            }
        }

        // Prebuilt DLLs
        foreach (var prebuiltRelPath in modpack.Code.PrebuiltDlls)
        {
            var fullPath = Path.Combine(modpack.Path, prebuiltRelPath);
            if (File.Exists(fullPath))
            {
                Directory.CreateDirectory(dllDir);
                File.Copy(fullPath, Path.Combine(dllDir, Path.GetFileName(fullPath)), true);
            }
        }
    }

    /// <summary>
    /// Stage patched game data files to the transaction.
    /// Verifies backups exist before staging, and adds patches to the transaction
    /// for atomic commit with rollback support.
    /// </summary>
    private void StageGameDataToTransaction(string modsBasePath, DeployTransaction transaction)
    {
        var compiledDir = Path.Combine(modsBasePath, "compiled");
        if (!Directory.Exists(compiledDir))
            return;

        var filesToPatch = new[]
        {
            ("resources.assets.patched", "resources.assets"),
            ("globalgamemanagers.patched", "globalgamemanagers")
        };

        foreach (var (patchedName, originalName) in filesToPatch)
        {
            var patchedPath = Path.Combine(compiledDir, patchedName);
            if (!File.Exists(patchedPath))
                continue;

            try
            {
                // StageGameFilePatch will verify backup exists and stage the patch
                transaction.StageGameFilePatch(patchedPath, originalName);
            }
            catch (Exception ex)
            {
                ModkitLog.Error($"[DeployManager] Failed to stage game file patch {originalName}: {ex.Message}");
                throw; // Re-throw to trigger transaction rollback
            }
        }
    }

    /// <summary>
    /// Remove all deployed mods from the game's Mods/ folder.
    /// Core infrastructure DLLs (ModpackLoader, DataExtractor) are preserved.
    /// Performs pre-undeploy validation and logs any unexpected changes.
    /// </summary>
    public async Task<DeployResult> UndeployAllAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var modsBasePath = _modpackManager.ModsBasePath;
        if (string.IsNullOrEmpty(modsBasePath))
            return new DeployResult { Success = false, Message = "Game install path not set" };

        var state = DeployState.LoadFrom(DeployStateFilePath);

        try
        {
            // Step 1: Pre-undeploy validation
            progress?.Report("Validating deployment state...");
            var validation = await ValidateBeforeUndeployAsync(progress);

            // Log validation results
            if (!validation.IsValid)
            {
                ModkitLog.Warn($"[DeployManager] Pre-undeploy validation found issues: {validation.Summary}");

                foreach (var modified in validation.ModifiedFiles)
                {
                    ModkitLog.Warn($"[DeployManager] Modified since deploy: {modified.RelativePath} " +
                        $"(expected {modified.ExpectedSize}B, actual {modified.ActualSize}B)");
                }

                foreach (var missing in validation.MissingFiles)
                {
                    ModkitLog.Info($"[DeployManager] Already removed: {missing}");
                }

                foreach (var unknown in validation.UnknownFiles)
                {
                    ModkitLog.Info($"[DeployManager] Unknown file (will be preserved): {unknown}");
                }
            }

            // Check for critical issues that would prevent safe undeploy
            if (!validation.ShouldProceed)
            {
                var errorMsg = $"Cannot proceed with undeploy: {validation.Summary}";
                ModkitLog.Error($"[DeployManager] {errorMsg}");
                return new DeployResult { Success = false, Message = errorMsg };
            }

            // Log warnings about backup validation
            if (validation.BackupValidation != null && !validation.BackupValidation.IsValid)
            {
                if (validation.BackupValidation.IsCritical)
                {
                    ModkitLog.Error($"[DeployManager] Critical backup issue: {validation.BackupValidation.Summary}");
                }
                else
                {
                    ModkitLog.Warn($"[DeployManager] Backup issue: {validation.BackupValidation.Summary}");
                }
            }

            // Step 2: Proceed with file removal
            progress?.Report("Removing deployed mods...");

            await Task.Run(() =>
            {
                // Remove deployed modpack directories
                foreach (var mp in state.DeployedModpacks)
                {
                    // Skip empty/invalid names to avoid deleting Mods folder itself
                    if (string.IsNullOrWhiteSpace(mp.Name))
                    {
                        ModkitLog.Warn($"[DeployManager] Skipping invalid modpack with empty name");
                        continue;
                    }

                    var dir = Path.Combine(modsBasePath, mp.Name);

                    // Safety: never delete the Mods folder itself
                    if (Path.GetFullPath(dir) == Path.GetFullPath(modsBasePath))
                    {
                        ModkitLog.Warn($"[DeployManager] Skipping deletion of Mods folder itself");
                        continue;
                    }

                    if (Directory.Exists(dir))
                    {
                        ModkitLog.Info($"[DeployManager] Removing modpack directory: {mp.Name}");
                        Directory.Delete(dir, true);
                    }
                }

                // Also remove any tracked loose files, but protect core DLLs
                foreach (var file in state.DeployedFiles)
                {
                    var fileName = Path.GetFileName(file);

                    // Never remove core infrastructure DLLs (Menace.*.dll)
                    if (fileName.StartsWith("Menace.", StringComparison.OrdinalIgnoreCase) &&
                        fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        ModkitLog.Info($"[DeployManager] Protected from removal: {file}");
                        continue;
                    }

                    var fullPath = Path.Combine(modsBasePath, file);
                    if (File.Exists(fullPath))
                    {
                        ModkitLog.Info($"[DeployManager] Removing: {file}");
                        File.Delete(fullPath);
                    }
                }

                // Clean up deployment artifacts that shouldn't persist
                var artifactDirs = new[] { "compiled", "dll", "dlls" };
                foreach (var artifactName in artifactDirs)
                {
                    var artifactDir = Path.Combine(modsBasePath, artifactName);
                    if (Directory.Exists(artifactDir))
                    {
                        ModkitLog.Info($"[DeployManager] Removing artifact directory: {artifactName}");
                        Directory.Delete(artifactDir, true);
                    }
                }

                // Log preserved core DLLs
                foreach (var dllPath in Directory.GetFiles(modsBasePath, "Menace.*.dll"))
                {
                    ModkitLog.Info($"[DeployManager] Core DLL preserved: {Path.GetFileName(dllPath)}");
                }
            }, ct);

            // Restore original game data files
            progress?.Report("Restoring original game data...");
            await Task.Run(() => RestoreOriginalGameData(modsBasePath), ct);

            // Clear deploy state
            var emptyState = new DeployState();
            emptyState.SaveTo(DeployStateFilePath);

            // Force garbage collection to release any cached file handles or data
            // This fixes an issue where undeploy+redeploy without app restart would fail
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            progress?.Report("All mods undeployed");
            return new DeployResult { Success = true, Message = "All mods undeployed" };
        }
        catch (Exception ex)
        {
            return new DeployResult { Success = false, Message = $"Undeploy failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// Get the current deploy state (what's deployed vs what's in staging).
    /// </summary>
    public DeployState GetDeployState()
    {
        return DeployState.LoadFrom(DeployStateFilePath);
    }

    /// <summary>
    /// Check if any staging modpack has changed since last deploy.
    /// </summary>
    public bool HasChangedSinceDeploy()
    {
        var state = GetDeployState();
        var staging = _modpackManager.GetStagingModpacks();

        // Different count
        if (state.DeployedModpacks.Count != staging.Count)
            return true;

        // Check each modpack for changes
        foreach (var deployed in state.DeployedModpacks)
        {
            var stagingMatch = staging.FirstOrDefault(s => s.Name == deployed.Name);
            if (stagingMatch == null)
                return true; // modpack removed from staging

            var currentHash = ComputeDirectoryHash(stagingMatch.Path);
            if (currentHash != deployed.ContentHash)
                return true; // content changed
        }

        // Check for new staging modpacks not yet deployed
        foreach (var s in staging)
        {
            if (!state.DeployedModpacks.Any(d => d.Name == s.Name))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Validate the current deployment state before undeploy.
    /// Checks for modified files, missing files, and unknown files.
    /// </summary>
    /// <param name="progress">Optional progress reporter</param>
    /// <returns>Validation result with details about any issues found</returns>
    public async Task<UndeployValidationResult> ValidateBeforeUndeployAsync(IProgress<string>? progress = null)
    {
        var result = new UndeployValidationResult { IsValid = true };
        var modsBasePath = _modpackManager.ModsBasePath;

        if (string.IsNullOrEmpty(modsBasePath))
        {
            result.IsValid = false;
            result.Summary = "Game install path not set";
            result.ShouldProceed = false;
            return result;
        }

        var state = DeployState.LoadFrom(DeployStateFilePath);

        if (state.DeployedModpacks.Count == 0 && state.DeployedFiles.Count == 0 && state.DeployedFileInfos.Count == 0)
        {
            result.IsValid = true;
            result.Summary = "No deployment to validate";
            return result;
        }

        progress?.Report("Validating deployed files...");

        await Task.Run(() =>
        {
            // Check for modified and missing files
            if (state.DeployedFileInfos.Count > 0)
            {
                var validationErrors = state.ValidateDeployedFiles(modsBasePath);

                foreach (var error in validationErrors)
                {
                    switch (error.ErrorType)
                    {
                        case FileValidationErrorType.Missing:
                            result.MissingFiles.Add(error.RelativePath);
                            break;

                        case FileValidationErrorType.SizeMismatch:
                        case FileValidationErrorType.HashMismatch:
                            result.ModifiedFiles.Add(new ModifiedFileInfo
                            {
                                RelativePath = error.RelativePath,
                                ExpectedSize = error.ExpectedSize,
                                ActualSize = error.ActualSize,
                                ExpectedHash = error.ExpectedHash,
                                ActualHash = error.ActualHash,
                                ModificationType = error.ErrorType == FileValidationErrorType.SizeMismatch
                                    ? ModificationType.SizeChanged
                                    : ModificationType.ContentChanged
                            });
                            break;
                    }
                }
            }

            // Check for orphaned/unknown files in Mods/ folder
            var excludePatterns = new[] { "Menace.*.dll", "*.pdb", "deploy-state.json" };
            var orphanedFiles = state.GetOrphanedFiles(modsBasePath, excludePatterns);

            // Separate orphaned files into modpack directories (untracked) vs root level (unknown)
            foreach (var orphaned in orphanedFiles)
            {
                var firstSegment = orphaned.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).FirstOrDefault();
                bool isInModpackDir = state.DeployedModpacks.Any(m =>
                    string.Equals(m.Name, firstSegment, StringComparison.OrdinalIgnoreCase));

                if (isInModpackDir)
                {
                    result.UntrackedFiles.Add(orphaned);
                }
                else
                {
                    result.UnknownFiles.Add(orphaned);
                }
            }
        });

        // Validate backup files for game restoration
        progress?.Report("Validating backup files...");
        result.BackupValidation = await ValidateBackupsAsync();

        result.BuildSummary();
        return result;
    }

    /// <summary>
    /// Validate backup files needed for game restoration.
    /// </summary>
    private async Task<BackupValidationSummary> ValidateBackupsAsync()
    {
        var summary = new BackupValidationSummary { IsValid = true };

        var gameInstallPath = _modpackManager.GetGameInstallPath();
        if (string.IsNullOrEmpty(gameInstallPath))
        {
            summary.IsValid = false;
            summary.IsCritical = true;
            summary.Summary = "Cannot locate game install path";
            return summary;
        }

        var gameDataDir = Directory.GetDirectories(gameInstallPath, "*_Data").FirstOrDefault();
        if (string.IsNullOrEmpty(gameDataDir))
        {
            summary.IsValid = false;
            summary.IsCritical = true;
            summary.Summary = "Cannot locate game data directory";
            return summary;
        }

        return await Task.Run(() =>
        {
            // Check for backup files
            var backupFiles = new[] { "resources.assets.original", "globalgamemanagers.original" };
            foreach (var backupFile in backupFiles)
            {
                var backupPath = Path.Combine(gameDataDir, backupFile);
                if (!File.Exists(backupPath))
                {
                    // Missing backup is only critical if the patched file exists
                    var originalFile = backupFile.Replace(".original", "");
                    var originalPath = Path.Combine(gameDataDir, originalFile);
                    if (File.Exists(originalPath))
                    {
                        summary.MissingBackups.Add(backupFile);
                    }
                }
            }

            // Load and validate backup metadata if available
            var metadata = BackupMetadata.LoadFrom(gameDataDir);
            summary.HasMetadata = metadata != null;

            if (metadata != null)
            {
                var validationResult = metadata.ValidateBackups(gameDataDir);
                if (!validationResult.IsValid)
                {
                    summary.IsValid = false;
                    // Include both hash mismatches AND size mismatches as corrupted
                    summary.CorruptedBackups.AddRange(validationResult.HashMismatches);
                    summary.CorruptedBackups.AddRange(validationResult.SizeMismatches.Keys);

                    // Check if corrupted files are critical - BOTH hash AND size mismatches count
                    var allCorrupted = validationResult.HashMismatches
                        .Concat(validationResult.SizeMismatches.Keys)
                        .ToList();
                    if (allCorrupted.Any(f =>
                        f.Contains("resources.assets") || f.Contains("globalgamemanagers")))
                    {
                        summary.IsCritical = true;
                    }

                    summary.Summary = validationResult.GetSummary();
                }
            }
            else if (summary.MissingBackups.Count > 0)
            {
                summary.IsValid = false;
                summary.IsCritical = true;
                summary.Summary = $"Missing backup files: {string.Join(", ", summary.MissingBackups)}. " +
                                 "Game files cannot be restored. You may need to verify game files via Steam.";
            }

            if (summary.IsValid && summary.MissingBackups.Count == 0)
            {
                summary.Summary = "All backup files are valid";
            }

            return summary;
        });
    }

    /// <summary>
    /// Clean up orphaned files in the Mods/ folder that aren't tracked in deploy state.
    /// </summary>
    /// <param name="options">Options controlling cleanup behavior</param>
    /// <param name="progress">Optional progress reporter</param>
    /// <returns>List of files that were removed (or would be removed in dry-run mode)</returns>
    public async Task<List<string>> CleanupOrphanedFilesAsync(
        OrphanedFileCleanupOptions options,
        IProgress<string>? progress = null)
    {
        var removedFiles = new List<string>();
        var modsBasePath = _modpackManager.ModsBasePath;

        if (string.IsNullOrEmpty(modsBasePath))
            return removedFiles;

        var state = DeployState.LoadFrom(DeployStateFilePath);

        // Always exclude core DLLs and common config files
        var excludePatterns = new List<string>(options.ProtectedPatterns)
        {
            "Menace.*.dll",
            "*.pdb",
            "deploy-state.json"
        };

        var orphanedFiles = state.GetOrphanedFiles(modsBasePath, excludePatterns);

        progress?.Report($"Found {orphanedFiles.Count} orphaned file(s)");

        await Task.Run(() =>
        {
            foreach (var relativePath in orphanedFiles)
            {
                var fullPath = Path.Combine(modsBasePath, relativePath);

                if (options.DryRun)
                {
                    ModkitLog.Info($"[DeployManager] Would remove orphaned file: {relativePath}");
                    removedFiles.Add(relativePath);
                }
                else
                {
                    try
                    {
                        File.Delete(fullPath);
                        ModkitLog.Info($"[DeployManager] Removed orphaned file: {relativePath}");
                        removedFiles.Add(relativePath);
                    }
                    catch (Exception ex)
                    {
                        ModkitLog.Warn($"[DeployManager] Failed to remove orphaned file {relativePath}: {ex.Message}");
                    }
                }
            }

            // Remove empty directories if requested
            if (options.RemoveEmptyDirectories && !options.DryRun)
            {
                RemoveEmptyDirectories(modsBasePath);
            }
        });

        return removedFiles;
    }

    /// <summary>
    /// Remove empty directories recursively.
    /// </summary>
    private static void RemoveEmptyDirectories(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            return;

        foreach (var dir in Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories)
            .OrderByDescending(d => d.Length)) // Process deepest directories first
        {
            try
            {
                if (Directory.GetFileSystemEntries(dir).Length == 0)
                {
                    Directory.Delete(dir);
                    ModkitLog.Info($"[DeployManager] Removed empty directory: {Path.GetRelativePath(rootPath, dir)}");
                }
            }
            catch
            {
                // Ignore errors removing directories
            }
        }
    }

    // ---------------------------------------------------------------
    // Internal
    // ---------------------------------------------------------------

    /// <summary>
    /// Merge all modpack patches and clones, then attempt to compile them into an asset bundle.
    /// Returns list of deployed files (relative to modsBasePath). Falls back silently
    /// if compilation fails — the runtime JSON loader will handle the patches instead.
    /// NOTE: This non-transactional version is used by single deploy which doesn't use transactions.
    /// </summary>
    private async Task<List<string>> TryCompileBundleAsync(
        List<ModpackManifest> modpacks, string modsBasePath, CancellationToken ct)
    {
        var (files, _) = await TryCompileBundleWithInfoAsync(modpacks, modsBasePath, transaction: null, ct);
        return files;
    }

    /// <summary>
    /// Merge all modpack patches and clones, then attempt to compile them into an asset bundle.
    /// Returns list of deployed files and detailed file info. Falls back silently
    /// if compilation fails — the runtime JSON loader will handle the patches instead.
    /// When a transaction is provided, compiled files are staged through it for atomic deployment.
    /// </summary>
    private async Task<(List<string> Files, List<DeployedFileInfo> FileInfos)> TryCompileBundleWithInfoAsync(
        List<ModpackManifest> modpacks, string modsBasePath, DeployTransaction? transaction, CancellationToken ct)
    {
        var files = new List<string>();
        var fileInfos = new List<DeployedFileInfo>();

        // Collect ordered patch sets from all modpacks
        var orderedPatchSets = new List<Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>>();

        // Collect clone definitions from all modpacks
        var mergedClones = new MergedCloneSet();

        foreach (var modpack in modpacks)
        {
            // Clones from clones/*.json files
            var clonesDir = Path.Combine(modpack.Path, "clones");
            if (Directory.Exists(clonesDir))
            {
                var modpackClones = new Dictionary<string, Dictionary<string, string>>();
                foreach (var cloneFile in Directory.GetFiles(clonesDir, "*.json"))
                {
                    var templateType = Path.GetFileNameWithoutExtension(cloneFile);
                    try
                    {
                        var json = File.ReadAllText(cloneFile);
                        var cloneMap = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                        if (cloneMap != null && cloneMap.Count > 0)
                            modpackClones[templateType] = cloneMap;
                    }
                    catch { }
                }
                if (modpackClones.Count > 0)
                    mergedClones.AddFromModpack(modpackClones);
            }

            // Patches from stats/*.json files
            var statsDir = Path.Combine(modpack.Path, "stats");
            if (Directory.Exists(statsDir))
            {
                var statsPatches = new Dictionary<string, Dictionary<string, Dictionary<string, JsonElement>>>();
                foreach (var statsFile in Directory.GetFiles(statsDir, "*.json"))
                {
                    var templateType = Path.GetFileNameWithoutExtension(statsFile);
                    try
                    {
                        var json = File.ReadAllText(statsFile);
                        var instances = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(json);
                        if (instances != null)
                            statsPatches[templateType] = instances;
                    }
                    catch { }
                }
                if (statsPatches.Count > 0)
                    orderedPatchSets.Add(statsPatches);
            }

            // Patches from manifest
            if (modpack.Patches.Count > 0)
                orderedPatchSets.Add(modpack.Patches);
        }

        // Collect audio entries from all modpacks for native AudioClip creation
        var modpackAssetsDirs = modpacks
            .Select(m => Path.Combine(m.Path, "assets"))
            .Where(Directory.Exists)
            .ToList();
        var audioCollectResult = AudioBundler.CollectAudioEntriesFromModpacks(modpackAssetsDirs);
        var audioEntries = audioCollectResult.Entries;

        if (audioCollectResult.Warnings.Count > 0)
        {
            foreach (var warn in audioCollectResult.Warnings)
                ModkitLog.Warn($"[DeployManager] Audio: {warn}");
        }

        // Collect texture and model entries from all modpacks for native asset creation
        var allTextureEntries = new List<BundleCompiler.TextureEntry>();
        var allModelEntries = new List<BundleCompiler.ModelEntry>();
        foreach (var modpack in modpacks)
        {
            var assetsDir = Path.Combine(modpack.Path, "assets");
            var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".png", ".jpg", ".jpeg", ".tga", ".bmp" };

            // First try modpack.Assets dictionary for proper game path mappings
            if (modpack.Assets != null && modpack.Assets.Count > 0)
            {
                foreach (var (gameAssetPath, localPath) in modpack.Assets)
                {
                    var ext = Path.GetExtension(gameAssetPath);
                    if (!imageExtensions.Contains(ext))
                        continue;

                    var fullLocalPath = Path.Combine(modpack.Path, localPath);
                    if (!File.Exists(fullLocalPath))
                        continue;

                    var assetName = Path.GetFileNameWithoutExtension(gameAssetPath);

                    // Build resource path from game asset path (strip extension, lowercase)
                    // Unity ResourceManager uses paths RELATIVE to Resources folder
                    // e.g., "Assets/Resources/ui/textures/bg.png" -> "ui/textures/bg"
                    var resourcePath = gameAssetPath;
                    if (resourcePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                        resourcePath = resourcePath[..^ext.Length];
                    resourcePath = resourcePath.Replace('\\', '/');

                    // Strip "Assets/Resources/" prefix if present (Unity uses relative paths)
                    if (resourcePath.StartsWith("Assets/Resources/", StringComparison.OrdinalIgnoreCase))
                        resourcePath = resourcePath.Substring("Assets/Resources/".Length);
                    else if (resourcePath.StartsWith("assets/resources/", StringComparison.OrdinalIgnoreCase))
                        resourcePath = resourcePath.Substring("assets/resources/".Length);

                    resourcePath = resourcePath.ToLowerInvariant();

                    allTextureEntries.Add(new BundleCompiler.TextureEntry
                    {
                        AssetName = assetName,
                        SourceFilePath = fullLocalPath,
                        ResourcePath = resourcePath,
                        CreateSprite = true // Create sprites by default for UI textures
                    });
                }
            }
            // Fallback: scan assets folder directly if Assets dictionary is empty
            else if (Directory.Exists(assetsDir))
            {
                var imageFiles = imageExtensions
                    .SelectMany(ext => Directory.GetFiles(assetsDir, $"*{ext}", SearchOption.AllDirectories))
                    .ToList();

                foreach (var imagePath in imageFiles)
                {
                    var assetName = Path.GetFileNameWithoutExtension(imagePath);
                    var relativePath = Path.GetRelativePath(assetsDir, imagePath).Replace('\\', '/');

                    // Build a resource path based on the modpack name and relative path
                    var resourcePath = $"assets/textures/{modpack.Name}/{Path.GetFileNameWithoutExtension(relativePath)}".ToLowerInvariant();

                    allTextureEntries.Add(new BundleCompiler.TextureEntry
                    {
                        AssetName = assetName,
                        SourceFilePath = imagePath,
                        ResourcePath = resourcePath,
                        CreateSprite = true
                    });
                }
            }

            // Collect GLB/GLTF model entries for native asset creation
            if (Directory.Exists(assetsDir))
            {
                var glbFiles = Directory.GetFiles(assetsDir, "*.glb", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(assetsDir, "*.gltf", SearchOption.AllDirectories));

                foreach (var glbPath in glbFiles)
                {
                    var assetName = Path.GetFileNameWithoutExtension(glbPath);

                    // Build resource path based on modpack assets mapping or default
                    var relativePath = Path.GetRelativePath(assetsDir, glbPath).Replace('\\', '/');
                    var resourcePath = $"assets/models/{modpack.Name}/{assetName}".ToLowerInvariant();

                    allModelEntries.Add(new BundleCompiler.ModelEntry
                    {
                        AssetName = assetName,
                        SourceFilePath = glbPath,
                        ResourcePath = resourcePath
                    });
                }
            }
        }

        if (allTextureEntries.Count > 0)
        {
            ModkitLog.Info($"[DeployManager] Collected {allTextureEntries.Count} texture(s) for native asset creation");
        }
        if (allModelEntries.Count > 0)
        {
            ModkitLog.Info($"[DeployManager] Collected {allModelEntries.Count} model(s) for native asset creation");
        }

        var merged = MergedPatchSet.MergePatchSets(orderedPatchSets);

        // Only skip compilation if there are no patches, no clones, no audio, AND no models
        // NOTE: Textures are now handled at runtime to avoid ColorSpace issues
        bool hasAudio = audioEntries.Count > 0;
        bool hasModels = allModelEntries.Count > 0;
        if (merged.Patches.Count == 0 && !mergedClones.HasClones && !hasAudio && !hasModels)
            return (files, fileInfos);

        // Determine game data path and Unity version
        var gameInstallPath = _modpackManager.GetGameInstallPath();
        if (string.IsNullOrEmpty(gameInstallPath))
            return (files, fileInfos);

        var unityVersion = DetectUnityVersion(gameInstallPath);

        // Use a temp directory for compilation when we have a transaction
        // This ensures compiled artifacts don't pollute the final location if transaction fails
        var useTransaction = transaction != null;
        var tempCompiledDir = useTransaction
            ? Path.Combine(Path.GetTempPath(), $"modkit-compiled-{Guid.NewGuid():N}")
            : null;
        var compiledDir = tempCompiledDir ?? Path.Combine(modsBasePath, "compiled");
        var outputPath = Path.Combine(compiledDir, "templates.bundle");

        try
        {
            // Copy texture files to compiled/textures for runtime loading
            // (asset-file texture creation had ColorSpace issues, runtime loading works correctly)
            var texturesDir = Path.Combine(compiledDir, "textures");
            if (allTextureEntries.Count > 0)
            {
                Directory.CreateDirectory(texturesDir);
                ModkitLog.Info($"[DeployManager] Copying {allTextureEntries.Count} texture(s) for runtime loading...");
                foreach (var tex in allTextureEntries)
                {
                    try
                    {
                        var destPath = Path.Combine(texturesDir, $"{tex.AssetName}.png");
                        File.Copy(tex.SourceFilePath, destPath, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        ModkitLog.Warn($"[DeployManager] Failed to copy texture '{tex.AssetName}': {ex.Message}");
                    }
                }
            }

            // NOTE: Textures are handled at runtime via copied PNG files
            // Asset-file texture creation always resulted in washed-out colors
            ModkitLog.Info($"[DeployManager] Compiling bundle: {mergedClones.TotalCloneCount} clone(s), {merged.Patches.Count} patch type(s), {audioEntries.Count} audio file(s), {allModelEntries.Count} model(s), {allTextureEntries.Count} texture(s) (runtime)");
            var compiler = new BundleCompiler();
            var result = await compiler.CompileDataPatchBundleAsync(
                merged,
                mergedClones,
                audioEntries.Count > 0 ? audioEntries : null,
                null, // Textures handled at runtime - asset creation had ColorSpace issues
                allModelEntries.Count > 0 ? allModelEntries : null,
                gameInstallPath,
                unityVersion,
                outputPath,
                ct);

            if (result.Success && result.OutputPath != null)
            {
                ModkitLog.Info($"[DeployManager] Bundle compiled: {result.Message}");
                // Log any warnings even on success
                foreach (var warn in result.Warnings)
                    ModkitLog.Info($"[DeployManager]   - {warn}");

                // Track all files in the compiled directory with detailed info
                var deployTime = DateTime.Now;
                foreach (var fullPath in Directory.GetFiles(compiledDir, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.Combine("compiled", Path.GetRelativePath(compiledDir, fullPath));
                    files.Add(relativePath);

                    try
                    {
                        var fi = new FileInfo(fullPath);
                        fileInfos.Add(new DeployedFileInfo
                        {
                            RelativePath = relativePath,
                            FileHash = DeployState.ComputeFileHash(fullPath),
                            SourceModpack = "compiled", // Special marker for compiled assets
                            DeployedAt = deployTime,
                            FileSize = fi.Length
                        });
                    }
                    catch (Exception hashEx)
                    {
                        ModkitLog.Warn($"[DeployManager] Failed to compute file info for {relativePath}: {hashEx.Message}");
                    }
                }

                // Stage compiled files through transaction if available
                if (useTransaction && tempCompiledDir != null)
                {
                    ModkitLog.Info($"[DeployManager] Staging {files.Count} compiled file(s) through transaction");
                    transaction!.StageDirectory(tempCompiledDir, "compiled");

                    // Also stage game file patches (resources.assets.patched, globalgamemanagers.patched)
                    // These are in the temp compiled directory and need to be staged before we clean up
                    var filesToPatch = new[]
                    {
                        ("resources.assets.patched", "resources.assets"),
                        ("globalgamemanagers.patched", "globalgamemanagers")
                    };

                    foreach (var (patchedName, originalName) in filesToPatch)
                    {
                        var patchedPath = Path.Combine(tempCompiledDir, patchedName);
                        if (!File.Exists(patchedPath))
                            continue;

                        try
                        {
                            // StageGameFilePatch will verify backup exists and stage the patch
                            transaction.StageGameFilePatch(patchedPath, originalName);
                            ModkitLog.Info($"[DeployManager] Staged game file patch: {originalName}");
                        }
                        catch (Exception ex)
                        {
                            ModkitLog.Error($"[DeployManager] Failed to stage game file patch {originalName}: {ex.Message}");
                            throw; // Re-throw to trigger transaction rollback
                        }
                    }
                }
            }
            else
            {
                ModkitLog.Warn($"[DeployManager] Bundle compilation failed: {result.Message}");
                foreach (var warn in result.Warnings)
                    ModkitLog.Warn($"[DeployManager]   - {warn}");
            }
        }
        catch (Exception ex)
        {
            ModkitLog.Error($"[DeployManager] Bundle compilation exception: {ex.Message}");
        }
        finally
        {
            // Clean up temp directory if we used one
            if (tempCompiledDir != null && Directory.Exists(tempCompiledDir))
            {
                try
                {
                    Directory.Delete(tempCompiledDir, recursive: true);
                }
                catch { /* Best effort cleanup */ }
            }
        }

        return (files, fileInfos);
    }

    /// <summary>
    /// Try to detect the Unity version from the game install.
    /// Returns a fallback string if detection fails.
    /// </summary>
    private static string DetectUnityVersion(string gameInstallPath)
    {
        // Look for globalgamemanagers or data.unity3d to read version from
        var dataDirs = Directory.GetDirectories(gameInstallPath, "*_Data");
        foreach (var dataDir in dataDirs)
        {
            var ggm = Path.Combine(dataDir, "globalgamemanagers");
            if (File.Exists(ggm))
            {
                try
                {
                    // The Unity version is stored near the start of globalgamemanagers
                    using var fs = File.OpenRead(ggm);
                    using var reader = new BinaryReader(fs);
                    // Skip header bytes and read version string
                    // This is a simplified approach — the full detector in Core is more robust
                    fs.Seek(0x14, SeekOrigin.Begin);
                    var bytes = new List<byte>();
                    byte b;
                    while ((b = reader.ReadByte()) != 0 && bytes.Count < 30)
                        bytes.Add(b);
                    var version = Encoding.ASCII.GetString(bytes.ToArray());
                    if (version.Contains('.') && version.Length > 3)
                        return version;
                }
                catch { }
            }
        }

        return "2020.3.0f1"; // Fallback
    }

    private List<string> DeployModpack(ModpackManifest modpack, string modsBasePath)
    {
        var (files, _) = DeployModpackWithInfo(modpack, modsBasePath);
        return files;
    }

    /// <summary>
    /// Deploy a modpack and return both the file list and detailed file info.
    /// This is the core deployment logic that tracks per-file hashes and metadata.
    /// </summary>
    private (List<string> Files, List<DeployedFileInfo> FileInfos) DeployModpackWithInfo(ModpackManifest modpack, string modsBasePath)
    {
        // Guard against empty modpack names which would deploy to root Mods folder
        if (string.IsNullOrWhiteSpace(modpack.Name))
        {
            ModkitLog.Error($"[DeployManager] Cannot deploy modpack with empty name (path: {modpack.Path})");
            return (new List<string>(), new List<DeployedFileInfo>());
        }

        var deployDir = Path.Combine(modsBasePath, modpack.Name);
        var files = new List<string>();
        var fileInfos = new List<DeployedFileInfo>();
        var deployTime = DateTime.Now;

        // Copy all modpack files to deploy directory
        CopyDirectory(modpack.Path, deployDir);

        // Deploy compiled DLLs from build/ directory
        DeployDlls(modpack, deployDir);

        // Build runtime manifest (must happen before tracking files so modpack.json is included)
        BuildRuntimeManifest(modpack, deployDir);

        // Track deployed files with detailed info
        foreach (var fullPath in Directory.GetFiles(deployDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(modsBasePath, fullPath);
            files.Add(relativePath);

            try
            {
                var fi = new FileInfo(fullPath);
                fileInfos.Add(new DeployedFileInfo
                {
                    RelativePath = relativePath,
                    FileHash = DeployState.ComputeFileHash(fullPath),
                    SourceModpack = modpack.Name,
                    DeployedAt = deployTime,
                    FileSize = fi.Length
                });
            }
            catch (Exception ex)
            {
                ModkitLog.Warn($"[DeployManager] Failed to compute file info for {relativePath}: {ex.Message}");
                // Still track the file path even if we couldn't get full info
            }
        }

        return (files, fileInfos);
    }

    /// <summary>
    /// Copy compiled DLLs and prebuilt DLLs to the deploy directory.
    /// </summary>
    private void DeployDlls(ModpackManifest modpack, string deployDir)
    {
        var dllDir = Path.Combine(deployDir, "dlls");

        // Compiled DLLs from build/
        var buildDir = Path.Combine(modpack.Path, "build");
        if (Directory.Exists(buildDir))
        {
            foreach (var dll in Directory.GetFiles(buildDir, "*.dll"))
            {
                Directory.CreateDirectory(dllDir);
                File.Copy(dll, Path.Combine(dllDir, Path.GetFileName(dll)), true);
            }
        }

        // Prebuilt DLLs
        foreach (var prebuiltRelPath in modpack.Code.PrebuiltDlls)
        {
            var fullPath = Path.Combine(modpack.Path, prebuiltRelPath);
            if (File.Exists(fullPath))
            {
                Directory.CreateDirectory(dllDir);
                File.Copy(fullPath, Path.Combine(dllDir, Path.GetFileName(fullPath)), true);
            }
        }
    }

    private void BuildRuntimeManifest(ModpackManifest modpack, string deployPath)
    {
        var runtimeObj = new JsonObject
        {
            ["manifestVersion"] = 2,
            ["name"] = modpack.Name,
            ["version"] = modpack.Version,
            ["author"] = modpack.Author,
            ["loadOrder"] = modpack.LoadOrder
        };

        // Merge patches from manifest and stats/*.json files
        var patches = new JsonObject();
        var legacyTemplates = new JsonObject();

        // Stats files first
        var statsDir = Path.Combine(modpack.Path, "stats");
        if (Directory.Exists(statsDir))
        {
            foreach (var statsFile in Directory.GetFiles(statsDir, "*.json"))
            {
                var templateType = Path.GetFileNameWithoutExtension(statsFile);
                try
                {
                    var node = JsonNode.Parse(File.ReadAllText(statsFile));
                    if (node != null)
                    {
                        patches[templateType] = JsonNode.Parse(node.ToJsonString());
                        legacyTemplates[templateType] = node;
                    }
                }
                catch { }
            }
        }

        // Manifest patches (stats files take priority)
        if (modpack.Patches.Count > 0)
        {
            var patchJson = JsonSerializer.Serialize(modpack.Patches);
            var patchNode = JsonNode.Parse(patchJson)?.AsObject();
            if (patchNode != null)
            {
                foreach (var kvp in patchNode)
                {
                    if (!patches.ContainsKey(kvp.Key) && kvp.Value != null)
                        patches[kvp.Key] = JsonNode.Parse(kvp.Value.ToJsonString());
                }
            }
        }

        runtimeObj["patches"] = patches;
        runtimeObj["templates"] = legacyTemplates; // v1 backward compat

        // Clones from clones/*.json files
        var clones = new JsonObject();
        var clonesDir = Path.Combine(deployPath, "clones");
        if (Directory.Exists(clonesDir))
        {
            foreach (var file in Directory.GetFiles(clonesDir, "*.json"))
            {
                var templateType = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var node = JsonNode.Parse(File.ReadAllText(file));
                    if (node != null)
                        clones[templateType] = node;
                }
                catch { }
            }
        }
        if (clones.Count > 0)
            runtimeObj["clones"] = clones;

        // Assets: start from manifest entries, then scan for unregistered files
        var assetsObj = new JsonObject();
        if (modpack.Assets.Count > 0)
        {
            foreach (var kvp in modpack.Assets)
                assetsObj[kvp.Key] = kvp.Value;
        }

        // Fallback scan: pick up any files in assets/ not already in the manifest
        var assetsDir = Path.Combine(modpack.Path, "assets");
        if (Directory.Exists(assetsDir))
        {
            foreach (var file in Directory.GetFiles(assetsDir, "*", SearchOption.AllDirectories))
            {
                var relPath = Path.GetRelativePath(assetsDir, file);
                if (!assetsObj.ContainsKey(relPath))
                    assetsObj[relPath] = Path.Combine("assets", relPath);
            }
        }

        runtimeObj["assets"] = assetsObj;

        // Code
        if (modpack.Code.HasAnyCode)
        {
            var codeObj = new JsonObject
            {
                ["sources"] = new JsonArray(modpack.Code.Sources.Select(s => (JsonNode)JsonValue.Create(s)!).ToArray()),
                ["references"] = new JsonArray(modpack.Code.References.Select(r => (JsonNode)JsonValue.Create(r)!).ToArray()),
                ["prebuiltDlls"] = new JsonArray(modpack.Code.PrebuiltDlls.Select(d => (JsonNode)JsonValue.Create(d)!).ToArray())
            };
            runtimeObj["code"] = codeObj;
        }

        // Bundles
        if (modpack.Bundles.Count > 0)
        {
            runtimeObj["bundles"] = new JsonArray(modpack.Bundles.Select(b => (JsonNode)JsonValue.Create(b)!).ToArray());
        }

        runtimeObj["securityStatus"] = modpack.SecurityStatus.ToString();

        var manifestPath = Path.Combine(deployPath, "modpack.json");
        File.WriteAllText(manifestPath, runtimeObj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Copy runtime DLLs from runtime/ into the game install.
    /// Menace.* mod DLLs go to Mods/, support libraries go to UserLibs/.
    /// Note: Core DLLs are NOT tracked in deploy state - they are infrastructure
    /// that should persist across undeploy/deploy cycles.
    /// </summary>
    private List<string> DeployRuntimeDlls(string modsBasePath)
    {
        // We intentionally return an empty list - core DLLs should not be tracked
        // in deploy state since they're infrastructure, not user content.
        // UndeployAll should not remove them.
        var runtimeDlls = _modpackManager.GetRuntimeDlls();

        ModkitLog.Info($"[DeployManager] DeployRuntimeDlls: Found {runtimeDlls.Count} runtime DLLs to deploy");

        if (runtimeDlls.Count == 0)
        {
            ModkitLog.Warn($"[DeployManager] DeployRuntimeDlls: No runtime DLLs found in {_modpackManager.RuntimeDllsPath}");
            return new List<string>();
        }

        var gameInstallPath = Path.GetDirectoryName(modsBasePath) ?? modsBasePath;
        var userLibsPath = Path.Combine(gameInstallPath, "UserLibs");
        Directory.CreateDirectory(userLibsPath);

        foreach (var (fileName, sourcePath) in runtimeDlls)
        {
            var isModDll = fileName.StartsWith("Menace.", StringComparison.OrdinalIgnoreCase);
            var destPath = Path.Combine(isModDll ? modsBasePath : userLibsPath, fileName);
            try
            {
                // Copy if destination doesn't exist or source is different size/newer
                bool needsCopy = !File.Exists(destPath);
                if (!needsCopy)
                {
                    var srcInfo = new FileInfo(sourcePath);
                    var destInfo = new FileInfo(destPath);
                    needsCopy = srcInfo.Length != destInfo.Length ||
                                srcInfo.LastWriteTimeUtc > destInfo.LastWriteTimeUtc;
                }

                if (needsCopy)
                {
                    File.Copy(sourcePath, destPath, true);
                    ModkitLog.Info($"[DeployManager] Deployed runtime DLL: {fileName} -> {(isModDll ? "Mods" : "UserLibs")}");
                }

                // Remove legacy support-library copies from Mods/ to avoid duplicate load contexts.
                if (!isModDll)
                {
                    var legacyModsPath = Path.Combine(modsBasePath, fileName);
                    if (File.Exists(legacyModsPath))
                    {
                        File.Delete(legacyModsPath);
                        ModkitLog.Info($"[DeployManager] Removed legacy dependency from Mods: {fileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                ModkitLog.Warn($"[DeployManager] Failed to deploy {fileName}: {ex.Message}");
            }
        }

        // Return empty list - core DLLs are not tracked for undeploy
        return new List<string>();
    }

    private void CleanPreviousDeployment(DeployState previousState, string modsBasePath)
    {
        foreach (var mp in previousState.DeployedModpacks)
        {
            // Skip empty/invalid names to avoid deleting Mods folder itself
            if (string.IsNullOrWhiteSpace(mp.Name))
            {
                ModkitLog.Warn($"[DeployManager] CleanPreviousDeployment: Skipping invalid modpack with empty name");
                continue;
            }

            var dir = Path.Combine(modsBasePath, mp.Name);

            // Safety: never delete the Mods folder itself
            if (Path.GetFullPath(dir) == Path.GetFullPath(modsBasePath))
            {
                ModkitLog.Warn($"[DeployManager] CleanPreviousDeployment: Skipping deletion of Mods folder itself");
                continue;
            }

            if (Directory.Exists(dir))
            {
                ModkitLog.Info($"[DeployManager] CleanPreviousDeployment: Removing {mp.Name}");
                Directory.Delete(dir, true);
            }
        }
    }

    private static string ComputeDirectoryHash(string directory)
    {
        if (!Directory.Exists(directory))
            return string.Empty;

        using var sha = SHA256.Create();
        var sb = new StringBuilder();

        foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories).OrderBy(f => f))
        {
            var relativePath = Path.GetRelativePath(directory, file);
            sb.Append(relativePath);
            sb.Append(new FileInfo(file).LastWriteTimeUtc.Ticks);
            sb.Append(new FileInfo(file).Length);
        }

        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Directories to exclude when copying modpacks to Mods/ folder.
    /// These are development artifacts that shouldn't be deployed.
    /// </summary>
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "src",      // Source code (compiled to build/)
        "build",    // Build output (DLLs deployed separately via DeployDlls)
        "obj",      // MSBuild intermediate files
        "bin",      // MSBuild output files
        "dll",      // Legacy DLL folder (use dlls/ instead)
        ".git",     // Git repository data
        ".vs",      // Visual Studio data
    };

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            // Skip excluded directories
            if (ExcludedDirectories.Contains(dirName))
                continue;
            CopyDirectory(dir, Path.Combine(destDir, dirName));
        }
    }

    /// <summary>
    /// Check if a modpack is a developer-only modpack that should be excluded
    /// from deployment unless EnableDeveloperTools is enabled.
    /// </summary>
    private static bool IsDevOnlyModpack(string modpackName)
    {
        // Modpacks starting with "Test" are developer tools
        if (modpackName.StartsWith("Test", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Deploy patched game data files (resources.assets, globalgamemanagers) to the game's data directory.
    /// Creates backups of originals if they don't exist.
    /// </summary>
    private void DeployPatchedGameData(string modsBasePath)
    {
        var gameInstallPath = _modpackManager.GetGameInstallPath();
        if (string.IsNullOrEmpty(gameInstallPath))
            return;

        var gameDataDir = Directory.GetDirectories(gameInstallPath, "*_Data").FirstOrDefault();
        if (string.IsNullOrEmpty(gameDataDir))
            return;

        var compiledDir = Path.Combine(modsBasePath, "compiled");
        if (!Directory.Exists(compiledDir))
            return;

        // Files to patch: resources.assets and globalgamemanagers
        var filesToPatch = new[]
        {
            ("resources.assets.patched", "resources.assets"),
            ("globalgamemanagers.patched", "globalgamemanagers")
        };

        foreach (var (patchedName, originalName) in filesToPatch)
        {
            var patchedPath = Path.Combine(compiledDir, patchedName);
            if (!File.Exists(patchedPath))
                continue;

            var originalPath = Path.Combine(gameDataDir, originalName);
            var backupPath = Path.Combine(gameDataDir, originalName + ".original");

            // Only create backup if one doesn't exist yet (preserve vanilla backup)
            // CRITICAL: Don't overwrite existing .original - it may be the only vanilla copy!
            // Previous bug: always creating "fresh backup" would copy a patched file to .original
            if (File.Exists(originalPath) && !File.Exists(backupPath))
            {
                ModkitLog.Info($"[DeployManager] Creating first-time backup: {originalName} -> {originalName}.original");
                File.Copy(originalPath, backupPath);
            }

            // Copy patched file over original
            ModkitLog.Info($"[DeployManager] Deploying patched: {patchedName} -> {originalName}");
            File.Copy(patchedPath, originalPath, overwrite: true);
        }
    }

    /// <summary>
    /// Restore original game data files from backups.
    /// Verifies backup integrity using BackupMetadata before restoring.
    /// </summary>
    private void RestoreOriginalGameData(string modsBasePath)
    {
        ModkitLog.Info("[DeployManager] RestoreOriginalGameData starting...");

        var gameInstallPath = _modpackManager.GetGameInstallPath();
        if (string.IsNullOrEmpty(gameInstallPath))
        {
            ModkitLog.Warn("[DeployManager] RestoreOriginalGameData: gameInstallPath is empty, cannot restore");
            return;
        }

        ModkitLog.Info($"[DeployManager] RestoreOriginalGameData: gameInstallPath = {gameInstallPath}");

        var gameDataDir = Directory.GetDirectories(gameInstallPath, "*_Data").FirstOrDefault();
        if (string.IsNullOrEmpty(gameDataDir))
        {
            ModkitLog.Warn($"[DeployManager] RestoreOriginalGameData: No *_Data directory found in {gameInstallPath}");
            return;
        }

        ModkitLog.Info($"[DeployManager] RestoreOriginalGameData: gameDataDir = {gameDataDir}");

        // Load backup metadata for hash verification
        var backupMetadata = BackupMetadata.LoadFrom(gameDataDir);
        if (backupMetadata != null)
        {
            ModkitLog.Info($"[DeployManager] Found backup metadata: version={backupMetadata.GameVersion}, " +
                $"created={backupMetadata.BackupCreatedAt:yyyy-MM-dd HH:mm}, files={backupMetadata.FileHashes.Count}");
        }
        else
        {
            ModkitLog.Warn("[DeployManager] No backup metadata found - proceeding with size-based validation only");
        }

        // Files to restore
        var filesToRestore = new[] { "resources.assets", "globalgamemanagers" };

        // Expected minimum sizes for validation (vanilla game files)
        var expectedMinSizes = new Dictionary<string, long>
        {
            { "resources.assets", 500 * 1024 * 1024 }, // ~518MB for vanilla
            { "globalgamemanagers", 5 * 1024 * 1024 }  // ~6MB for vanilla
        };

        foreach (var originalName in filesToRestore)
        {
            var originalPath = Path.Combine(gameDataDir, originalName);
            var backupPath = Path.Combine(gameDataDir, originalName + ".original");

            if (File.Exists(backupPath))
            {
                try
                {
                    var backupSize = new FileInfo(backupPath).Length;

                    // Validate backup isn't corrupted (too small)
                    if (expectedMinSizes.TryGetValue(originalName, out var minSize) && backupSize < minSize)
                    {
                        ModkitLog.Error($"[DeployManager] Backup {originalName}.original appears corrupted: {backupSize / 1024 / 1024}MB (expected >{minSize / 1024 / 1024}MB). Use Steam to verify game files, then use Clean Redeploy.");
                        continue;
                    }

                    // Verify hash against metadata if available
                    bool hashVerified = false;
                    if (backupMetadata != null && backupMetadata.FileHashes.TryGetValue(originalName, out var expectedHash))
                    {
                        ModkitLog.Info($"[DeployManager] Verifying backup hash for {originalName}...");
                        var actualHash = DeployState.ComputeFileHash(backupPath);
                        if (string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                        {
                            ModkitLog.Info($"[DeployManager] Backup hash verified: {originalName} matches metadata");
                            hashVerified = true;
                        }
                        else
                        {
                            // CRITICAL: Do NOT restore corrupted backups - this could damage the game installation
                            ModkitLog.Error($"[DeployManager] Backup hash mismatch for {originalName}! " +
                                $"Expected: {expectedHash[..16]}..., Actual: {actualHash[..16]}... " +
                                "SKIPPING restore - backup may be corrupted. Use Steam to verify game files.");
                            continue;
                        }
                    }

                    ModkitLog.Info($"[DeployManager] Restoring original: {originalName}.original ({backupSize / 1024 / 1024}MB) -> {originalName}");
                    File.Copy(backupPath, originalPath, overwrite: true);

                    // Verify restored file
                    var restoredSize = new FileInfo(originalPath).Length;
                    if (restoredSize != backupSize)
                    {
                        ModkitLog.Error($"[DeployManager] Restore verification failed for {originalName}: " +
                            $"size mismatch (backup={backupSize}, restored={restoredSize})");
                    }
                    else if (hashVerified || backupMetadata == null)
                    {
                        ModkitLog.Info($"[DeployManager] Restored {originalName}: {restoredSize / 1024 / 1024}MB (verified)");
                    }
                    else
                    {
                        // Verify hash of restored file if metadata exists
                        var restoredHash = DeployState.ComputeFileHash(originalPath);
                        if (backupMetadata.FileHashes.TryGetValue(originalName, out var expectedRestoredHash) &&
                            string.Equals(restoredHash, expectedRestoredHash, StringComparison.OrdinalIgnoreCase))
                        {
                            ModkitLog.Info($"[DeployManager] Restored {originalName}: {restoredSize / 1024 / 1024}MB (hash verified after copy)");
                        }
                        else
                        {
                            ModkitLog.Info($"[DeployManager] Restored {originalName}: {restoredSize / 1024 / 1024}MB");
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModkitLog.Error($"[DeployManager] Failed to restore {originalName}: {ex.Message}");
                }
            }
            else
            {
                ModkitLog.Warn($"[DeployManager] No backup found for {originalName} at {backupPath}");
            }
        }

        ModkitLog.Info("[DeployManager] RestoreOriginalGameData complete");
    }
}

public class DeployResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int DeployedCount { get; set; }
}
