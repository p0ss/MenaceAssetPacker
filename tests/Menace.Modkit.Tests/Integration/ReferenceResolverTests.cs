using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Menace.Modkit.App.Services;

namespace Menace.Modkit.Tests.Integration;

/// <summary>
/// Verifies that ReferenceResolver produces a clean set of references
/// with no duplicate assembly names and uses the correct runtime.
/// </summary>
public class ReferenceResolverTests
{
    [Fact]
    public void ResolveReferences_NoDuplicateAssemblyNames()
    {
        var resolver = new ReferenceResolver(string.Empty);
        var refs = resolver.ResolveReferences();

        AssertNoDuplicateNames(refs);
    }

    [Fact]
    public void ResolveReferences_ContainsEssentialSystemAssemblies()
    {
        var resolver = new ReferenceResolver(string.Empty);
        var refs = resolver.ResolveReferences();

        var names = GetRefNames(refs);

        Assert.Contains("System.Runtime", names);
        Assert.Contains("System.Linq", names);
        Assert.Contains("System.Collections", names);
    }

    [Fact]
    public void ResolveReferences_PrefersGameBundledRuntime()
    {
        // Simulate a game directory with a bundled dotnet/ dir
        using var tmp = new Helpers.TemporaryDirectory();
        var dotnetDir = tmp.CreateSubdirectory("dotnet");

        // Copy a real managed DLL in as System.Runtime.dll to simulate the game's BCL
        var testAssembly = typeof(ReferenceResolverTests).Assembly.Location;
        File.Copy(testAssembly, Path.Combine(dotnetDir, "System.Runtime.dll"));

        var resolver = new ReferenceResolver(tmp.Path);
        var refs = resolver.ResolveReferences();

        // The System.Runtime reference should come from the game's dotnet/ dir
        var systemRuntime = refs
            .OfType<PortableExecutableReference>()
            .FirstOrDefault(r => r.FilePath != null &&
                Path.GetFileNameWithoutExtension(r.FilePath) == "System.Runtime");

        Assert.NotNull(systemRuntime);
        Assert.StartsWith(dotnetDir, systemRuntime!.FilePath!);
    }

    [Fact]
    public void ResolveReferences_Il2CppSystemAssembliesFiltered()
    {
        // Even with the game's dotnet/ providing BCL, the Il2CppAssemblies dir
        // must NOT contribute duplicate system assemblies
        using var tmp = new Helpers.TemporaryDirectory();

        // Set up a minimal game dotnet/ dir
        var dotnetDir = tmp.CreateSubdirectory("dotnet");
        var runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        foreach (var asm in new[] { "System.Runtime.dll", "System.Linq.dll", "System.Private.CoreLib.dll" })
        {
            var src = Path.Combine(runtimeDir, asm);
            if (File.Exists(src))
                File.Copy(src, Path.Combine(dotnetDir, asm));
        }

        // Put conflicting old-framework assemblies in Il2CppAssemblies
        var il2cppDir = tmp.CreateSubdirectory("MelonLoader/Il2CppAssemblies");
        foreach (var asm in new[] { "System.Core.dll", "System.Linq.dll", "mscorlib.dll" })
        {
            var src = Path.Combine(runtimeDir, asm);
            if (File.Exists(src))
                File.Copy(src, Path.Combine(il2cppDir, asm));
        }

        var resolver = new ReferenceResolver(tmp.Path);
        var refs = resolver.ResolveReferences();

        // No references should come from Il2CppAssemblies for system assemblies
        var leakedPaths = refs
            .OfType<PortableExecutableReference>()
            .Where(r => r.FilePath != null && r.FilePath.StartsWith(il2cppDir))
            .Select(r => Path.GetFileName(r.FilePath!))
            .ToList();

        Assert.True(leakedPaths.Count == 0,
            $"Framework assemblies leaked from Il2CppAssemblies: {string.Join(", ", leakedPaths)}");

        AssertNoDuplicateNames(refs);
    }

    [Fact]
    public void ResolveReferences_GameDirNonSystemAssembliesKept()
    {
        using var tmp = new Helpers.TemporaryDirectory();
        var il2cppDir = tmp.CreateSubdirectory("MelonLoader/Il2CppAssemblies");

        // Use the test assembly as a stand-in for a game-specific DLL
        var testAssembly = typeof(ReferenceResolverTests).Assembly.Location;
        File.Copy(testAssembly, Path.Combine(il2cppDir, "Assembly-CSharp.dll"));

        var resolver = new ReferenceResolver(tmp.Path);
        var refs = resolver.ResolveReferences();

        var names = GetRefNames(refs);
        Assert.Contains("Assembly-CSharp", names);
    }

    // -- Helpers --

    private static HashSet<string> GetRefNames(List<MetadataReference> refs)
    {
        return refs
            .OfType<PortableExecutableReference>()
            .Where(r => r.FilePath != null)
            .Select(r => Path.GetFileNameWithoutExtension(r.FilePath!))
            .ToHashSet(System.StringComparer.OrdinalIgnoreCase);
    }

    private static void AssertNoDuplicateNames(List<MetadataReference> refs)
    {
        var names = refs
            .OfType<PortableExecutableReference>()
            .Where(r => r.FilePath != null)
            .Select(r => Path.GetFileNameWithoutExtension(r.FilePath!))
            .ToList();

        var duplicates = names
            .GroupBy(n => n, System.StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} (x{g.Count()})")
            .ToList();

        Assert.True(duplicates.Count == 0,
            $"Duplicate assembly names: {string.Join(", ", duplicates)}");
    }
}
