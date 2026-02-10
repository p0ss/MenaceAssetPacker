using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Menace.SDK.Repl;

/// <summary>
/// Discovers MetadataReferences at runtime for Roslyn compilation by scanning
/// the game's directory structure for system, engine, and mod assemblies.
/// </summary>
public class RuntimeReferenceResolver
{
    private List<MetadataReference> _cached;

    // Known framework assembly names/prefixes to exclude from game dirs.
    // These are provided by the runtime reference set and can conflict when pulled from legacy locations.
    private static readonly HashSet<string> FrameworkExactNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System",
        "mscorlib",
        "netstandard",
        "WindowsBase",
        "PresentationCore",
        "PresentationFramework",
        "Accessibility",
    };

    private static readonly string[] FrameworkPrefixes =
    {
        "System.",
        "Microsoft.CSharp",
        "Microsoft.VisualBasic",
        "Microsoft.Win32",
        "Mono.",
    };

    /// <summary>
    /// Resolve all available MetadataReferences for Roslyn compilation.
    /// Results are cached after first call.
    /// </summary>
    public List<MetadataReference> ResolveAll()
    {
        if (_cached != null) return _cached;

        var refs = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);
        var gameRoot = FindGameRoot();

        if (!string.IsNullOrEmpty(gameRoot))
        {
            // 1. System/BCL references from the game's dotnet/ or MelonLoader/net6/ directory
            AddFromDirectory(refs, Path.Combine(gameRoot, "dotnet"), isSystem: true);
            AddFromDirectory(refs, Path.Combine(gameRoot, "MelonLoader", "net6"), isSystem: true);

            // 2. MelonLoader + Il2CppInterop
            AddFromDirectory(refs, Path.Combine(gameRoot, "MelonLoader"), isSystem: false, recurse: false);

            // 3. Game IL2CPP assemblies
            AddFromDirectory(refs, Path.Combine(gameRoot, "MelonLoader", "Il2CppAssemblies"), isSystem: false);

            // 4. Mod DLLs
            var modsDir = Path.Combine(gameRoot, "Mods");
            if (Directory.Exists(modsDir))
            {
                foreach (var modDir in Directory.GetDirectories(modsDir))
                {
                    AddFromDirectory(refs, Path.Combine(modDir, "dlls"), isSystem: false);
                }
                // Also add DLLs directly in Mods/
                AddFromDirectory(refs, modsDir, isSystem: false, recurse: false);
            }
        }

        // 5. ModpackLoader.dll itself (this assembly)
        try
        {
            var selfPath = typeof(RuntimeReferenceResolver).Assembly.Location;
            if (!string.IsNullOrEmpty(selfPath) && File.Exists(selfPath))
                refs.TryAdd(Path.GetFileName(selfPath), MetadataReference.CreateFromFile(selfPath));
        }
        catch { }

        // 6. Fallback: add references from loaded assemblies in the current domain
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                var loc = asm.Location;
                if (string.IsNullOrEmpty(loc) || !File.Exists(loc)) continue;
                refs.TryAdd(Path.GetFileName(loc), MetadataReference.CreateFromFile(loc));
            }
        }
        catch { }

        _cached = refs.Values.ToList();
        ModError.Info("Menace.SDK.Repl", $"Resolved {_cached.Count} metadata references");
        return _cached;
    }

    /// <summary>
    /// Force re-resolution on next call.
    /// </summary>
    public void Invalidate()
    {
        _cached = null;
    }

    private static string FindGameRoot()
    {
        // Try MelonLoader's environment first
        try
        {
            Type melonEnvType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.FullName == "MelonLoader.MelonEnvironment")
                        {
                            melonEnvType = t;
                            break;
                        }
                    }
                }
                catch { }
                if (melonEnvType != null) break;
            }

            if (melonEnvType != null)
            {
                var prop = melonEnvType.GetProperty("GameRootDirectory",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop != null)
                {
                    var value = prop.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(value) && Directory.Exists(value))
                        return value;
                }
            }
        }
        catch { }

        // Fallback to current directory
        var cwd = Directory.GetCurrentDirectory();
        if (Directory.Exists(Path.Combine(cwd, "MelonLoader")))
            return cwd;

        return cwd;
    }

    private static void AddFromDirectory(Dictionary<string, MetadataReference> refs,
        string dir, bool isSystem, bool recurse = true)
    {
        if (!Directory.Exists(dir)) return;

        var searchOption = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        try
        {
            foreach (var dll in Directory.GetFiles(dir, "*.dll", searchOption))
            {
                var fileName = Path.GetFileName(dll);

                // Skip framework assemblies in non-system directories
                if (!isSystem && IsFrameworkAssembly(fileName))
                    continue;

                if (refs.ContainsKey(fileName))
                    continue;

                try
                {
                    refs[fileName] = MetadataReference.CreateFromFile(dll);
                }
                catch { }
            }
        }
        catch { }
    }

    private static bool IsFrameworkAssembly(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        if (FrameworkExactNames.Contains(name))
            return true;

        foreach (var prefix in FrameworkPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
