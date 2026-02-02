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
    /// Resolve all MetadataReferences needed to compile a mod DLL targeting net6.0.
    /// </summary>
    public List<MetadataReference> ResolveReferences(List<string>? requestedReferences = null)
    {
        var refs = new List<MetadataReference>();
        var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var addedAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ModkitLog.Info("=== Resolving references ===");

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
        if (!string.IsNullOrEmpty(_gameInstallPath))
        {
            var il2cppDir = Path.Combine(_gameInstallPath, "MelonLoader", "Il2CppAssemblies");
            if (Directory.Exists(il2cppDir))
            {
                ModkitLog.Info($"Step 3a: Il2CppAssemblies ({il2cppDir})");
                foreach (var dll in Directory.GetFiles(il2cppDir, "*.dll"))
                    AddNonSystemRef(dll);
            }

            var mlDir = Path.Combine(_gameInstallPath, "MelonLoader");
            if (Directory.Exists(mlDir))
            {
                ModkitLog.Info($"Step 3b: MelonLoader root ({mlDir})");
                foreach (var dll in Directory.GetFiles(mlDir, "*.dll"))
                    AddNonSystemRef(dll);
            }

            var modsDir = Path.Combine(_gameInstallPath, "Mods");
            if (Directory.Exists(modsDir))
            {
                ModkitLog.Info($"Step 3c: Mods ({modsDir})");
                foreach (var dll in Directory.GetFiles(modsDir, "*.dll"))
                    AddNonSystemRef(dll);
            }
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
    /// Priority: game's bundled dotnet/ directory (net6.0, matches MelonLoader's runtime)
    ///           → host runtime (fallback).
    /// Using the game's runtime is critical: if we compile against net10.0 assemblies,
    /// the DLL references System.Linq Version=10.0.0.0 which doesn't exist at runtime.
    /// </summary>
    private static void AddSystemReferences(string gameInstallPath, List<MetadataReference> refs,
        HashSet<string> addedPaths, HashSet<string> addedAssemblyNames)
    {
        // 1. Try the game's bundled .NET runtime (e.g. <game>/dotnet/)
        //    MelonLoader ships a self-contained net6.0 runtime here.
        if (!string.IsNullOrEmpty(gameInstallPath))
        {
            var gameDotnetDir = Path.Combine(gameInstallPath, "dotnet");
            if (Directory.Exists(gameDotnetDir))
            {
                ModkitLog.Info($"  Using game's bundled runtime: {gameDotnetDir}");
                var count = 0;
                foreach (var dll in Directory.GetFiles(gameDotnetDir, "*.dll"))
                {
                    var name = Path.GetFileNameWithoutExtension(dll);
                    if (addedPaths.Add(dll) && addedAssemblyNames.Add(name))
                    {
                        try
                        {
                            // Skip native stubs that have PE headers but no useful metadata
                            if (!IsManagedAssembly(dll))
                                continue;

                            refs.Add(MetadataReference.CreateFromFile(dll));
                            count++;
                        }
                        catch { }
                    }
                }
                ModkitLog.Info($"  Added {count} assemblies from game runtime");

                if (count > 0)
                    return; // Game runtime found and populated — don't add host runtime
            }
        }

        // 2. Fallback: use the host's runtime directory
        ModkitLog.Warn("  Game's bundled dotnet/ not found — falling back to host runtime");
        var dotnetRoot = RuntimeEnvironment.GetRuntimeDirectory();
        ModkitLog.Info($"  Host runtime: {dotnetRoot}");

        if (Directory.Exists(dotnetRoot))
        {
            foreach (var dll in Directory.GetFiles(dotnetRoot, "*.dll"))
            {
                var name = Path.GetFileNameWithoutExtension(dll);
                if (addedPaths.Add(dll) && addedAssemblyNames.Add(name))
                {
                    try
                    {
                        if (!IsManagedAssembly(dll))
                            continue;
                        refs.Add(MetadataReference.CreateFromFile(dll));
                    }
                    catch { }
                }
            }
        }

        // Last resort: typeof(object) assembly
        var objectLocation = typeof(object).Assembly.Location;
        if (!string.IsNullOrEmpty(objectLocation) && addedPaths.Add(objectLocation))
        {
            addedAssemblyNames.Add(Path.GetFileNameWithoutExtension(objectLocation));
            try { refs.Add(MetadataReference.CreateFromFile(objectLocation)); }
            catch { }
        }
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
