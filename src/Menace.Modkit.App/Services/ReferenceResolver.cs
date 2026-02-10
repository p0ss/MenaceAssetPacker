using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Discovers reference assemblies for Roslyn compilation:
/// - MelonLoader DLLs from third_party/bundled/
/// - UnityEngine + Assembly-CSharp from game's MelonLoader/Il2CppAssemblies/
/// - System refs from the net6.0 runtime (mod target framework)
/// </summary>
public class ReferenceResolver
{
    private readonly string _gameInstallPath;

    public ReferenceResolver(string gameInstallPath)
    {
        _gameInstallPath = gameInstallPath;
    }

    /// <summary>
    /// Issues encountered during reference resolution.
    /// Check this after calling ResolveReferences to see if there were problems.
    /// </summary>
    public List<string> ResolutionIssues { get; } = new();

    /// <summary>
    /// Resolve all MetadataReferences needed to compile a mod DLL targeting net6.0.
    /// </summary>
    public List<MetadataReference> ResolveReferences(List<string>? requestedReferences = null)
    {
        return ResolveReferencesAsync(requestedReferences).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Resolve all MetadataReferences needed to compile a mod DLL targeting net6.0.
    /// Async variant avoids blocking callers during component download fallback.
    /// </summary>
    public async Task<List<MetadataReference>> ResolveReferencesAsync(
        List<string>? requestedReferences = null,
        CancellationToken ct = default)
    {
        var refs = new List<MetadataReference>();
        var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var addedAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reportedIssues = new HashSet<string>(StringComparer.Ordinal);
        ResolutionIssues.Clear();

        ct.ThrowIfCancellationRequested();

        void ReportIssue(string message)
        {
            if (!reportedIssues.Add(message))
                return;

            ResolutionIssues.Add(message);
            ModkitLog.Warn(message);
        }

        ModkitLog.Info("=== Resolving references ===");

        // Validate game install path first
        if (string.IsNullOrEmpty(_gameInstallPath))
        {
            ReportIssue("Game install path is not configured. Set it in Settings.");
            ModkitLog.Error("Game install path is not set!");
        }
        else if (!Directory.Exists(_gameInstallPath))
        {
            ReportIssue($"Game install path does not exist: {_gameInstallPath}");
            ModkitLog.Error($"Game install path does not exist: {_gameInstallPath}");
        }

        // Core add -- deduplicates by path and assembly filename.
        void AddRef(string path)
        {
            if (!File.Exists(path) || !addedPaths.Add(path))
                return;

            if (!IsManagedAssembly(path))
            {
                ModkitLog.Info($"  [skip-native] {Path.GetFileName(path)}");
                return;
            }

            var name = Path.GetFileNameWithoutExtension(path);
            if (!addedAssemblyNames.Add(name))
            {
                ModkitLog.Info($"  [skip-dup] {name} <- {path}");
                return;
            }

            try
            {
                refs.Add(MetadataReference.CreateFromFile(path));
                ModkitLog.Info($"  [added] {name} <- {path}");
            }
            catch (Exception ex)
            {
                ReportIssue($"Failed to add reference '{path}': {ex.Message}");
            }
        }

        // For bundled/game directories: skip framework assemblies.
        // System refs are provided by AddSystemReferences only -- game dirs have old
        // .NET Framework versions (e.g. System.Core 3.5) that conflict with modern BCL.
        void AddNonSystemRef(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (IsFrameworkAssembly(name))
            {
                ModkitLog.Info($"  [skip-framework] {name} <- {path}");
                return;
            }
            AddRef(path);
        }

        // 1. System references -- prefer game's dotnet runtime only when it matches
        //    the configured DotNetRefs version; otherwise use configured refs.
        ModkitLog.Info("Step 1: System references");
        await AddSystemReferencesAsync(_gameInstallPath, refs, addedPaths, addedAssemblyNames, ReportIssue, ct)
            .ConfigureAwait(false);

        // 2. MelonLoader DLLs from bundled third_party (skip any framework assemblies)
        var bundledDir = Path.Combine(AppContext.BaseDirectory, "third_party", "bundled");
        if (Directory.Exists(bundledDir))
        {
            ModkitLog.Info($"Step 2: Bundled third_party ({bundledDir})");
            foreach (var dll in Directory.GetFiles(bundledDir, "*.dll", SearchOption.AllDirectories))
                AddNonSystemRef(dll);
        }

        // 3. Game directories (skip any framework assemblies)
        if (!string.IsNullOrEmpty(_gameInstallPath) && Directory.Exists(_gameInstallPath))
        {
            var il2cppDir = Path.Combine(_gameInstallPath, "MelonLoader", "Il2CppAssemblies");
            if (Directory.Exists(il2cppDir))
            {
                ModkitLog.Info($"Step 3a: Il2CppAssemblies ({il2cppDir})");
                var il2cppCount = 0;
                foreach (var dll in Directory.GetFiles(il2cppDir, "*.dll"))
                {
                    AddNonSystemRef(dll);
                    il2cppCount++;
                }
                ModkitLog.Info($"  Found {il2cppCount} Il2Cpp assemblies");

                // Check for critical game assemblies
                var assemblyCSharp = Path.Combine(il2cppDir, "Assembly-CSharp.dll");
                if (!File.Exists(assemblyCSharp))
                    ReportIssue("Assembly-CSharp.dll not found in Il2CppAssemblies - game types will be unavailable");
            }
            else
            {
                ReportIssue($"Il2CppAssemblies directory not found at {il2cppDir}. Run the game once with MelonLoader to generate proxy assemblies.");
                ModkitLog.Warn($"Il2CppAssemblies not found: {il2cppDir}");
            }

            var mlDir = Path.Combine(_gameInstallPath, "MelonLoader");
            if (Directory.Exists(mlDir))
            {
                ModkitLog.Info($"Step 3b: MelonLoader ({mlDir})");
                // Search recursively - MelonLoader DLLs are in net6/ subdirectory
                var mlCount = 0;
                foreach (var dll in Directory.GetFiles(mlDir, "*.dll", SearchOption.AllDirectories))
                {
                    AddNonSystemRef(dll);
                    mlCount++;
                }
                ModkitLog.Info($"  Found {mlCount} MelonLoader assemblies");
            }
            else
            {
                ReportIssue("MelonLoader directory not found. Is MelonLoader installed?");
            }

            var modsDir = Path.Combine(_gameInstallPath, "Mods");
            if (Directory.Exists(modsDir))
            {
                ModkitLog.Info($"Step 3c: Mods ({modsDir})");
                foreach (var dll in Directory.GetFiles(modsDir, "*.dll"))
                    AddNonSystemRef(dll);
            }
        }

        // 4. Specific requested references (user-specified)
        if (requestedReferences != null)
        {
            ModkitLog.Info("Step 4: Requested references");
            var searchRoots = BuildRequestedReferenceSearchRoots(_gameInstallPath, bundledDir);

            foreach (var rawRefName in requestedReferences)
            {
                ct.ThrowIfCancellationRequested();
                var refName = Path.GetFileNameWithoutExtension(rawRefName?.Trim() ?? string.Empty);
                if (string.IsNullOrWhiteSpace(refName) || addedAssemblyNames.Contains(refName))
                    continue;

                var foundPath = TryFindRequestedReference(refName, searchRoots);
                if (foundPath != null)
                {
                    AddRef(foundPath);
                }

                if (!addedAssemblyNames.Contains(refName))
                {
                    ReportIssue($"Requested reference '{refName}' was not found in game, component cache, or bundled directories.");
                }
            }
        }

        ModkitLog.Info($"=== Resolved {refs.Count} references ===");
        return refs;
    }

    /// <summary>
    /// Add system/BCL references for compilation.
    /// Priority: matching game dotnet runtime -> configured DotNetRefs -> global .NET 6 SDK refs
    ///           -> global .NET 6 shared framework -> trusted platform assemblies fallback.
    /// </summary>
    private static async Task AddSystemReferencesAsync(
        string gameInstallPath,
        List<MetadataReference> refs,
        HashSet<string> addedPaths,
        HashSet<string> addedAssemblyNames,
        Action<string> reportIssue,
        CancellationToken ct)
    {
        // Helper: scan a directory for managed DLLs and add them as references.
        int AddManagedDlls(string directory, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (!Directory.Exists(directory))
                return 0;

            var count = 0;
            foreach (var dll in Directory.GetFiles(directory, "*.dll", searchOption))
            {
                var name = Path.GetFileNameWithoutExtension(dll);
                if (!addedPaths.Add(dll) || !addedAssemblyNames.Add(name))
                    continue;

                try
                {
                    if (!IsManagedAssembly(dll))
                        continue;

                    refs.Add(MetadataReference.CreateFromFile(dll));
                    count++;
                }
                catch (Exception ex)
                {
                    reportIssue($"Failed to load system reference '{dll}': {ex.Message}");
                }
            }

            return count;
        }

        string ResolveDotNetRefsDirectory(string root)
        {
            var net6 = Path.Combine(root, "net6.0");
            if (Directory.Exists(net6))
                return net6;

            var refNet6 = Path.Combine(root, "ref", "net6.0");
            if (Directory.Exists(refNet6))
                return refNet6;

            return root;
        }

        var expectedDotNetVersion = ReadConfiguredDotNetRefsVersion(reportIssue);

        // 1. Try game runtime first, but only if it matches configured DotNetRefs version.
        if (!string.IsNullOrEmpty(gameInstallPath))
        {
            var gameDotnetDir = Path.Combine(gameInstallPath, "dotnet");
            if (Directory.Exists(gameDotnetDir))
            {
                var gameVersion = DetectGameDotNetVersion(gameDotnetDir);
                var shouldUseGameRuntime = expectedDotNetVersion == null ||
                    (gameVersion != null && IsDotNetVersionMatch(gameVersion, expectedDotNetVersion));

                if (shouldUseGameRuntime)
                {
                    ModkitLog.Info($"  Using game's bundled runtime: {gameDotnetDir}");
                    var count = AddManagedDlls(gameDotnetDir);
                    ModkitLog.Info($"  Added {count} assemblies from game runtime");
                    if (count > 0)
                        return;
                }
                else
                {
                    reportIssue($"Game dotnet runtime version '{gameVersion ?? "unknown"}' does not match configured DotNetRefs version '{expectedDotNetVersion}'. Using configured refs instead.");
                }
            }
        }

        // 2. Try configured DotNetRefs (component cache or bundled). If missing, attempt download.
        var configuredDotNetRefsRoot = await EnsureConfiguredDotNetRefsRootAsync(expectedDotNetVersion, reportIssue, ct)
            .ConfigureAwait(false);
        if (!string.IsNullOrEmpty(configuredDotNetRefsRoot))
        {
            var refsDir = ResolveDotNetRefsDirectory(configuredDotNetRefsRoot);
            ModkitLog.Info($"  Using configured DotNetRefs: {refsDir}");
            var count = AddManagedDlls(refsDir);
            ModkitLog.Info($"  Added {count} assemblies from configured DotNetRefs");
            if (count > 0)
                return;
        }

        // 3. Try global .NET 6 SDK ref pack.
        ModkitLog.Warn("  Configured refs not available -- searching for .NET 6 SDK reference assemblies");
        var sdkRefsDir = FindNet6SdkRefs();
        if (sdkRefsDir != null)
        {
            ModkitLog.Info($"  Found .NET 6 SDK refs: {sdkRefsDir}");
            var count = AddManagedDlls(sdkRefsDir);
            ModkitLog.Info($"  Added {count} assemblies from .NET 6 SDK refs");
            if (count > 0)
                return;
        }

        // 4. Try global .NET 6 shared framework.
        ModkitLog.Warn("  SDK refs not found -- searching for .NET 6 shared framework");
        var net6Dir = FindNet6SharedFramework();
        if (net6Dir != null)
        {
            ModkitLog.Info($"  Found .NET shared framework: {net6Dir}");
            var count = AddManagedDlls(net6Dir);
            ModkitLog.Info($"  Added {count} assemblies from shared framework");
            if (count > 0)
                return;
        }

        // 5. Last resort: use trusted platform assemblies.
        ModkitLog.Error("  CRITICAL: No .NET 6 references found! Using host runtime (may cause version mismatch).");
        var tpaCount = 0;
        var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrEmpty(tpa))
        {
            var separator = OperatingSystem.IsWindows() ? ';' : ':';
            foreach (var path in tpa.Split(separator, StringSplitOptions.RemoveEmptyEntries))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (!IsFrameworkAssembly(name) &&
                    !name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("System", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("netstandard", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!File.Exists(path))
                    continue;
                if (!addedPaths.Add(path) || !addedAssemblyNames.Add(name))
                    continue;

                try
                {
                    if (!IsManagedAssembly(path))
                        continue;

                    refs.Add(MetadataReference.CreateFromFile(path));
                    tpaCount++;
                }
                catch (Exception ex)
                {
                    reportIssue($"Failed to load trusted platform assembly '{path}': {ex.Message}");
                }
            }
        }

        ModkitLog.Info($"  Added {tpaCount} assemblies from trusted platform assemblies");

        if (tpaCount == 0)
        {
            ModkitLog.Error("  CRITICAL: No system references found! Compilation will fail.");
            reportIssue("No usable system references were found for compilation.");
        }
    }

    private static List<string> BuildRequestedReferenceSearchRoots(string gameInstallPath, string bundledDir)
    {
        var roots = new List<string>();

        if (!string.IsNullOrEmpty(gameInstallPath) && Directory.Exists(gameInstallPath))
        {
            roots.Add(Path.Combine(gameInstallPath, "MelonLoader"));
            roots.Add(Path.Combine(gameInstallPath, "MelonLoader", "Il2CppAssemblies"));
            roots.Add(Path.Combine(gameInstallPath, "Mods"));
        }

        var melonLoaderPath = ComponentManager.Instance.GetMelonLoaderPath();
        if (!string.IsNullOrEmpty(melonLoaderPath))
        {
            roots.Add(melonLoaderPath);
            roots.Add(Path.Combine(melonLoaderPath, "Il2CppAssemblies"));
        }

        if (Directory.Exists(bundledDir))
            roots.Add(bundledDir);

        return roots
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists)
            .ToList();
    }

    private static string? TryFindRequestedReference(string refName, List<string> roots)
    {
        var dllName = $"{refName}.dll";

        foreach (var root in roots)
        {
            var directCandidates = new[]
            {
                Path.Combine(root, dllName),
                Path.Combine(root, "Il2CppAssemblies", dllName),
                Path.Combine(root, "net6", dllName),
                Path.Combine(root, "net6.0", dllName),
            };

            foreach (var candidate in directCandidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            try
            {
                var found = Directory.GetFiles(root, dllName, SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrEmpty(found))
                    return found;
            }
            catch
            {
                // Continue to other roots.
            }
        }

        return null;
    }

    private static string? ReadConfiguredDotNetRefsVersion(Action<string> reportIssue)
    {
        var versionsPath = FindVersionsJsonPath();
        if (versionsPath == null)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(versionsPath));
            if (!doc.RootElement.TryGetProperty("components", out var components) ||
                !components.TryGetProperty("DotNetRefs", out var dotNetRefs) ||
                !dotNetRefs.TryGetProperty("version", out var versionElement))
            {
                return null;
            }

            return versionElement.GetString();
        }
        catch (Exception ex)
        {
            reportIssue($"Failed to read DotNetRefs version from '{versionsPath}': {ex.Message}");
            return null;
        }
    }

    private static async Task<string?> EnsureConfiguredDotNetRefsRootAsync(
        string? expectedVersion,
        Action<string> reportIssue,
        CancellationToken ct)
    {
        var componentManager = ComponentManager.Instance;
        var installedRoot = componentManager.GetComponentPath("DotNetRefs");
        var installedVersion = ReadInstalledComponentVersion("DotNetRefs");

        if (!string.IsNullOrEmpty(installedRoot))
        {
            if (expectedVersion == null ||
                (!string.IsNullOrEmpty(installedVersion) && IsDotNetVersionMatch(installedVersion, expectedVersion)))
            {
                return installedRoot;
            }

            if (!string.IsNullOrEmpty(expectedVersion))
            {
                reportIssue($"Installed DotNetRefs version '{installedVersion ?? "unknown"}' does not match configured version '{expectedVersion}'.");
            }
        }

        var bundledRoot = FindBundledDotNetRefsRoot();
        if (!string.IsNullOrEmpty(bundledRoot))
            return bundledRoot;

        // Nothing local matched. Attempt to fetch configured refs.
        try
        {
            ModkitLog.Info("  Downloading DotNetRefs component to satisfy configured runtime references...");
            var ok = await componentManager.DownloadComponentAsync("DotNetRefs", ct: ct).ConfigureAwait(false);
            if (!ok)
            {
                reportIssue("Automatic DotNetRefs download failed. Use Setup to install required components.");
                return null;
            }

            var downloadedRoot = componentManager.GetComponentPath("DotNetRefs");
            if (!string.IsNullOrEmpty(downloadedRoot) && Directory.Exists(downloadedRoot))
                return downloadedRoot;

            reportIssue("DotNetRefs download reported success but install path was not found.");
            return null;
        }
        catch (Exception ex)
        {
            reportIssue($"Failed to download DotNetRefs component: {ex.Message}");
            return null;
        }
    }

    private static string? FindBundledDotNetRefsRoot()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "third_party", "bundled", "dotnet-refs"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "third_party", "bundled", "dotnet-refs"),
        };

        foreach (var candidate in candidates)
        {
            var resolved = Path.GetFullPath(candidate);
            if (Directory.Exists(resolved))
                return resolved;
        }

        return null;
    }

    private static string? FindVersionsJsonPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "third_party", "versions.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "third_party", "versions.json"),
        };

        foreach (var candidate in candidates)
        {
            var resolved = Path.GetFullPath(candidate);
            if (File.Exists(resolved))
                return resolved;
        }

        return null;
    }

    private static string? ReadInstalledComponentVersion(string componentName)
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var manifestPath = Path.Combine(home, ".menace-modkit", "components", "manifest.json");
            if (!File.Exists(manifestPath))
                return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (!doc.RootElement.TryGetProperty("components", out var components) ||
                !components.TryGetProperty(componentName, out var component) ||
                !component.TryGetProperty("version", out var versionElement))
            {
                return null;
            }

            return versionElement.GetString();
        }
        catch
        {
            return null;
        }
    }

    private static string? DetectGameDotNetVersion(string gameDotnetDir)
    {
        var sharedBase = Path.Combine(gameDotnetDir, "shared", "Microsoft.NETCore.App");
        var sharedVersion = FindHighestVersionFolder(sharedBase);
        if (!string.IsNullOrEmpty(sharedVersion))
            return sharedVersion;

        var fxrBase = Path.Combine(gameDotnetDir, "host", "fxr");
        var fxrVersion = FindHighestVersionFolder(fxrBase);
        if (!string.IsNullOrEmpty(fxrVersion))
            return fxrVersion;

        return null;
    }

    private static string? FindHighestVersionFolder(string baseDir)
    {
        if (!Directory.Exists(baseDir))
            return null;

        var versions = Directory.GetDirectories(baseDir)
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => new { Raw = n!, Parsed = ParseLeadingVersion(n!) })
            .Where(x => x.Parsed != null)
            .OrderByDescending(x => x.Parsed)
            .ToList();

        return versions.FirstOrDefault()?.Raw;
    }

    private static bool IsDotNetVersionMatch(string actualVersion, string expectedVersion)
    {
        if (string.Equals(actualVersion, expectedVersion, StringComparison.OrdinalIgnoreCase))
            return true;

        var actual = ParseLeadingVersion(actualVersion);
        var expected = ParseLeadingVersion(expectedVersion);
        if (actual == null || expected == null)
            return false;

        if (actual.Major != expected.Major || actual.Minor != expected.Minor)
            return false;

        // If an expected build is specified (e.g. 6.0.36), enforce it.
        if (expected.Build >= 0 && actual.Build >= 0 && actual.Build != expected.Build)
            return false;

        return true;
    }

    private static Version? ParseLeadingVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var token = new string(value
            .TakeWhile(c => char.IsDigit(c) || c == '.')
            .ToArray())
            .TrimEnd('.');

        if (string.IsNullOrWhiteSpace(token))
            return null;

        return Version.TryParse(token, out var parsed) ? parsed : null;
    }

    /// <summary>
    /// Find .NET 6 SDK reference assemblies (Microsoft.NETCore.App.Ref pack).
    /// These are the ideal compilation references since they contain only metadata.
    /// </summary>
    private static string? FindNet6SdkRefs()
    {
        var candidates = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            candidates.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "dotnet", "packs", "Microsoft.NETCore.App.Ref"));
            candidates.Add(@"C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref");
        }
        else if (OperatingSystem.IsLinux())
        {
            candidates.Add("/usr/share/dotnet/packs/Microsoft.NETCore.App.Ref");
            candidates.Add("/usr/lib/dotnet/packs/Microsoft.NETCore.App.Ref");
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            candidates.Add(Path.Combine(home, ".dotnet", "packs", "Microsoft.NETCore.App.Ref"));
        }
        else if (OperatingSystem.IsMacOS())
        {
            candidates.Add("/usr/local/share/dotnet/packs/Microsoft.NETCore.App.Ref");
        }

        foreach (var basePath in candidates.Distinct())
        {
            if (!Directory.Exists(basePath))
                continue;

            // Look for 6.x version directories
            var dirs = Directory.GetDirectories(basePath)
                .Select(Path.GetFileName)
                .Where(n => n != null && n.StartsWith("6."))
                .OrderByDescending(n => n)
                .ToList();

            if (dirs.Count > 0)
            {
                var refDir = Path.Combine(basePath, dirs[0]!, "ref", "net6.0");
                if (Directory.Exists(refDir))
                {
                    ModkitLog.Info($"  Found .NET 6 SDK refs: {refDir}");
                    return refDir;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Find .NET 6 shared framework directory from a global .NET installation.
    /// ONLY returns .NET 6 - does NOT fall back to 8 or 10 since those would cause
    /// assembly version mismatches at runtime (MelonLoader runs on .NET 6).
    /// </summary>
    private static string? FindNet6SharedFramework()
    {
        var candidates = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            candidates.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "dotnet", "shared", "Microsoft.NETCore.App"));
            candidates.Add(@"C:\Program Files\dotnet\shared\Microsoft.NETCore.App");
            candidates.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "dotnet", "shared", "Microsoft.NETCore.App"));
        }
        else if (OperatingSystem.IsLinux())
        {
            candidates.Add("/usr/share/dotnet/shared/Microsoft.NETCore.App");
            candidates.Add("/usr/lib/dotnet/shared/Microsoft.NETCore.App");
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            candidates.Add(Path.Combine(home, ".dotnet", "shared", "Microsoft.NETCore.App"));
        }
        else if (OperatingSystem.IsMacOS())
        {
            candidates.Add("/usr/local/share/dotnet/shared/Microsoft.NETCore.App");
        }

        // ONLY use .NET 6 - do NOT fall back to 8 or 10!
        // Using newer runtimes causes assembly version mismatches at runtime.
        foreach (var basePath in candidates.Distinct())
        {
            if (!Directory.Exists(basePath))
                continue;

            var dirs = Directory.GetDirectories(basePath)
                .Select(Path.GetFileName)
                .Where(n => n != null && n.StartsWith("6."))
                .OrderByDescending(n => n)
                .ToList();

            if (dirs.Count > 0)
            {
                ModkitLog.Info($"  Using .NET {dirs[0]} shared framework");
                return Path.Combine(basePath, dirs[0]!);
            }
        }

        return null;
    }

    /// <summary>
    /// Check if a DLL file contains managed (CLR) metadata.
    /// Returns false for native DLLs like capstone.dll, version.dll, etc.
    /// </summary>
    private static bool IsManagedAssembly(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);
            return peReader.HasMetadata;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns true if the assembly name belongs to the .NET BCL / framework.
    /// These must NOT be added from game/bundled directories -- AddSystemReferences
    /// provides the canonical set from the host runtime. Game directories often
    /// contain old .NET Framework versions (e.g. System.Core 3.5, mscorlib 2.0)
    /// whose type definitions conflict with modern BCL assemblies.
    /// </summary>
    private static bool IsFrameworkAssembly(string name)
    {
        if (name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("netstandard", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("System", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("WindowsBase", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("PresentationCore", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("PresentationFramework", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Accessibility", StringComparison.OrdinalIgnoreCase))
            return true;

        if (name.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
            return true;

        if (name.StartsWith("Microsoft.CSharp", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Microsoft.VisualBasic", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Microsoft.Win32", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Mono.", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
