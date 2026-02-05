using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
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
        var refs = new List<MetadataReference>();
        var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var addedAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ResolutionIssues.Clear();

        ModkitLog.Info("=== Resolving references ===");

        // Validate game install path first
        if (string.IsNullOrEmpty(_gameInstallPath))
        {
            ResolutionIssues.Add("Game install path is not configured. Set it in Settings.");
            ModkitLog.Error("Game install path is not set!");
        }
        else if (!Directory.Exists(_gameInstallPath))
        {
            ResolutionIssues.Add($"Game install path does not exist: {_gameInstallPath}");
            ModkitLog.Error($"Game install path does not exist: {_gameInstallPath}");
        }

        // Core add — deduplicates by path and assembly filename.
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
                ModkitLog.Info($"  [skip-dup] {name} ← {path}");
                return;
            }

            try
            {
                refs.Add(MetadataReference.CreateFromFile(path));
                ModkitLog.Info($"  [added] {name} ← {path}");
            }
            catch { }
        }

        // For bundled/game directories: skip framework assemblies.
        // System refs are provided by AddSystemReferences only — game dirs have old
        // .NET Framework versions (e.g. System.Core 3.5) that conflict with modern BCL.
        void AddNonSystemRef(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (IsFrameworkAssembly(name))
            {
                ModkitLog.Info($"  [skip-framework] {name} ← {path}");
                return;
            }
            AddRef(path);
        }

        // 1. System references — use the game's bundled net6.0 runtime (in <game>/dotnet/)
        //    so compiled DLLs reference the correct assembly versions for MelonLoader's CLR.
        //    Falls back to the host runtime if the game's dotnet dir isn't found.
        ModkitLog.Info("Step 1: System references");
        AddSystemReferences(_gameInstallPath, refs, addedPaths, addedAssemblyNames);

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
                    ResolutionIssues.Add("Assembly-CSharp.dll not found in Il2CppAssemblies - game types will be unavailable");
            }
            else
            {
                ResolutionIssues.Add($"Il2CppAssemblies directory not found at {il2cppDir}. Run the game once with MelonLoader to generate proxy assemblies.");
                ModkitLog.Warn($"Il2CppAssemblies not found: {il2cppDir}");
            }

            var mlDir = Path.Combine(_gameInstallPath, "MelonLoader");
            if (Directory.Exists(mlDir))
            {
                ModkitLog.Info($"Step 3b: MelonLoader root ({mlDir})");
                foreach (var dll in Directory.GetFiles(mlDir, "*.dll"))
                    AddNonSystemRef(dll);
            }
            else
            {
                ResolutionIssues.Add($"MelonLoader directory not found. Is MelonLoader installed?");
            }

            var modsDir = Path.Combine(_gameInstallPath, "Mods");
            if (Directory.Exists(modsDir))
            {
                ModkitLog.Info($"Step 3c: Mods ({modsDir})");
                foreach (var dll in Directory.GetFiles(modsDir, "*.dll"))
                    AddNonSystemRef(dll);
            }
        }
        else if (!string.IsNullOrEmpty(_gameInstallPath))
        {
            ResolutionIssues.Add($"Game install path does not exist: {_gameInstallPath}");
        }

        // 4. Specific requested references (user-specified, may need Vanilla fallback)
        if (requestedReferences != null)
        {
            ModkitLog.Info("Step 4: Requested references");
            foreach (var refName in requestedReferences)
            {
                if (addedAssemblyNames.Contains(refName))
                    continue;

                // Try the Vanilla MelonLoader directory
                if (!string.IsNullOrEmpty(_gameInstallPath))
                {
                    var vanillaDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "Documents", "Code", "Menace", "Vanilla", "MelonLoader");

                    // Search both root and Il2CppAssemblies under Vanilla
                    var candidates = new[]
                    {
                        Path.Combine(vanillaDir, $"{refName}.dll"),
                        Path.Combine(vanillaDir, "Il2CppAssemblies", $"{refName}.dll")
                    };

                    foreach (var dllPath in candidates)
                    {
                        if (File.Exists(dllPath))
                        {
                            AddNonSystemRef(dllPath);
                            break;
                        }
                    }
                }
            }
        }

        ModkitLog.Info($"=== Resolved {refs.Count} references ===");
        return refs;
    }

    /// <summary>
    /// Add system/BCL references for compilation.
    /// Priority: bundled .NET 6 reference assemblies (guaranteed correct version)
    ///           → game's bundled dotnet/ → global .NET 6 shared framework → fallbacks.
    /// Using .NET 6 refs is critical: if we compile against net8/10 assemblies,
    /// the DLL references System.Linq Version=8.0.0.0 which doesn't exist in MelonLoader's runtime.
    /// </summary>
    private static void AddSystemReferences(string gameInstallPath, List<MetadataReference> refs,
        HashSet<string> addedPaths, HashSet<string> addedAssemblyNames)
    {
        // Helper: scan a directory for managed DLLs and add them as references.
        int AddManagedDlls(string directory)
        {
            if (!Directory.Exists(directory))
                return 0;

            var count = 0;
            foreach (var dll in Directory.GetFiles(directory, "*.dll"))
            {
                var name = Path.GetFileNameWithoutExtension(dll);
                if (addedPaths.Add(dll) && addedAssemblyNames.Add(name))
                {
                    try
                    {
                        if (!IsManagedAssembly(dll))
                            continue;
                        refs.Add(MetadataReference.CreateFromFile(dll));
                        count++;
                    }
                    catch { }
                }
            }
            return count;
        }

        // 1. Try bundled .NET 6 reference assemblies (from Microsoft.NETCore.App.Ref NuGet package)
        //    This ensures we always compile against .NET 6 assembly versions, regardless of
        //    what's installed on the user's system. MelonLoader runs on .NET 6, so referencing
        //    System.Linq Version=6.0.0.0 (not 8.0.0.0 or 10.0.0.0) is critical.
        var bundledRefsDir = Path.Combine(AppContext.BaseDirectory, "third_party", "bundled", "dotnet-refs", "net6.0");
        if (Directory.Exists(bundledRefsDir))
        {
            ModkitLog.Info($"  Using bundled .NET 6 reference assemblies: {bundledRefsDir}");
            var count = AddManagedDlls(bundledRefsDir);
            ModkitLog.Info($"  Added {count} assemblies from bundled refs");
            if (count > 0)
                return;
        }

        // 2. Try the game's bundled .NET runtime (e.g. <game>/dotnet/)
        //    MelonLoader may ship a self-contained net6.0 runtime here.
        if (!string.IsNullOrEmpty(gameInstallPath))
        {
            var gameDotnetDir = Path.Combine(gameInstallPath, "dotnet");
            if (Directory.Exists(gameDotnetDir))
            {
                ModkitLog.Info($"  Using game's bundled runtime: {gameDotnetDir}");
                var count = AddManagedDlls(gameDotnetDir);
                ModkitLog.Info($"  Added {count} assemblies from game runtime");

                if (count > 0)
                    return;
            }
        }

        // 3. Try global .NET 6 shared framework installation (SDK ref packs).
        ModkitLog.Warn("  Bundled refs not found — searching for .NET 6 SDK reference assemblies");
        var sdkRefsDir = FindNet6SdkRefs();
        if (sdkRefsDir != null)
        {
            ModkitLog.Info($"  Found .NET 6 SDK refs: {sdkRefsDir}");
            var count = AddManagedDlls(sdkRefsDir);
            ModkitLog.Info($"  Added {count} assemblies from .NET 6 SDK refs");
            if (count > 0)
                return;
        }

        // 4. Try global .NET 6 shared framework (runtime assemblies).
        //    WARNING: Using .NET 8/10 here will cause version mismatch errors at runtime!
        ModkitLog.Warn("  SDK refs not found — searching for .NET 6 shared framework");
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
        //    WARNING: This may use .NET 8/10 assemblies and cause runtime errors!
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
                catch { }
            }
        }

        ModkitLog.Info($"  Added {tpaCount} assemblies from trusted platform assemblies");

        if (tpaCount == 0)
        {
            ModkitLog.Error("  CRITICAL: No system references found! Compilation will fail.");
            ModkitLog.Error("  The bundled .NET 6 reference assemblies are missing from third_party/bundled/dotnet-refs/net6.0/");
        }
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
    /// These must NOT be added from game/bundled directories — AddSystemReferences
    /// provides the canonical set from the host runtime. Game directories often
    /// contain old .NET Framework versions (e.g. System.Core 3.5, mscorlib 2.0)
    /// whose type definitions conflict with modern BCL assemblies.
    /// </summary>
    private static bool IsFrameworkAssembly(string name)
    {
        // Exact matches
        if (name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("netstandard", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("System", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("WindowsBase", StringComparison.OrdinalIgnoreCase))
            return true;

        // System.* — covers System.Core, System.Linq, System.Runtime, System.Xml, etc.
        if (name.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
            return true;

        // Microsoft BCL assemblies (but NOT Microsoft.CodeAnalysis or similar tools)
        if (name.StartsWith("Microsoft.CSharp", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Microsoft.VisualBasic", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Microsoft.Win32", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
