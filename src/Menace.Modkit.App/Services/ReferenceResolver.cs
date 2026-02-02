using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        void AddRef(string path)
        {
            if (File.Exists(path) && addedPaths.Add(path))
            {
                try
                {
                    refs.Add(MetadataReference.CreateFromFile(path));
                }
                catch { }
            }
        }

        // 1. System references from net6.0 runtime
        AddSystemReferences(refs, addedPaths);

        // 2. MelonLoader DLLs from bundled third_party
        var bundledDir = Path.Combine(AppContext.BaseDirectory, "third_party", "bundled");
        if (Directory.Exists(bundledDir))
        {
            foreach (var dll in Directory.GetFiles(bundledDir, "*.dll", SearchOption.AllDirectories))
                AddRef(dll);
        }

        // 3. Game's Il2CppAssemblies (UnityEngine.*, Assembly-CSharp, etc.)
        if (!string.IsNullOrEmpty(_gameInstallPath))
        {
            var il2cppDir = Path.Combine(_gameInstallPath, "MelonLoader", "Il2CppAssemblies");
            if (Directory.Exists(il2cppDir))
            {
                foreach (var dll in Directory.GetFiles(il2cppDir, "*.dll"))
                    AddRef(dll);
            }

            // Also check game's MelonLoader root for core DLLs
            var mlDir = Path.Combine(_gameInstallPath, "MelonLoader");
            if (Directory.Exists(mlDir))
            {
                foreach (var dll in Directory.GetFiles(mlDir, "*.dll"))
                    AddRef(dll);
            }
        }

        // 4. Specific requested references (resolve by name from known paths)
        if (requestedReferences != null)
        {
            foreach (var refName in requestedReferences)
            {
                // Already added if it was in one of the above directories
                // This is a fallback for explicitly named references
                var candidates = refs
                    .OfType<PortableExecutableReference>()
                    .Where(r => r.FilePath != null &&
                        Path.GetFileNameWithoutExtension(r.FilePath)
                            .Equals(refName, StringComparison.OrdinalIgnoreCase));

                // If not found, try the Vanilla MelonLoader directory
                if (!candidates.Any() && !string.IsNullOrEmpty(_gameInstallPath))
                {
                    var vanillaDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "Documents", "Code", "Menace", "Vanilla", "MelonLoader");
                    var dllPath = Path.Combine(vanillaDir, $"{refName}.dll");
                    AddRef(dllPath);
                }
            }
        }

        return refs;
    }

    private static void AddSystemReferences(List<MetadataReference> refs, HashSet<string> addedPaths)
    {
        // Find net6.0 reference assemblies
        // Try the shared framework path first
        var dotnetRoot = RuntimeEnvironment.GetRuntimeDirectory();

        // The runtime directory already contains the core BCL assemblies
        if (Directory.Exists(dotnetRoot))
        {
            var essentialAssemblies = new[]
            {
                "System.Runtime.dll",
                "System.Collections.dll",
                "System.Linq.dll",
                "System.Threading.dll",
                "System.Threading.Tasks.dll",
                "System.IO.dll",
                "System.Console.dll",
                "System.Text.Json.dll",
                "System.ComponentModel.dll",
                "System.ObjectModel.dll",
                "netstandard.dll",
                "mscorlib.dll",
                "System.Private.CoreLib.dll"
            };

            foreach (var asm in essentialAssemblies)
            {
                var path = Path.Combine(dotnetRoot, asm);
                if (File.Exists(path) && addedPaths.Add(path))
                {
                    try { refs.Add(MetadataReference.CreateFromFile(path)); }
                    catch { }
                }
            }
        }

        // Fallback: use the typeof trick for mscorlib/System.Runtime
        var objectLocation = typeof(object).Assembly.Location;
        if (!string.IsNullOrEmpty(objectLocation) && addedPaths.Add(objectLocation))
        {
            try { refs.Add(MetadataReference.CreateFromFile(objectLocation)); }
            catch { }
        }
    }
}
