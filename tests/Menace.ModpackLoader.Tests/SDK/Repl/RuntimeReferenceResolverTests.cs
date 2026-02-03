using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Menace.ModpackLoader.Tests.Helpers;
using Menace.SDK.Repl;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Menace.ModpackLoader.Tests.SDK.Repl;

public class RuntimeReferenceResolverTests : IDisposable
{
    public RuntimeReferenceResolverTests()
    {
        // Clear ModError state since ResolveAll logs an Info message
        Menace.SDK.ModError.Clear();
    }

    public void Dispose()
    {
        Menace.SDK.ModError.Clear();
    }

    [Fact]
    public void ResolveAll_IncludesLoadedAssemblies()
    {
        var resolver = new RuntimeReferenceResolver();

        var refs = resolver.ResolveAll();

        // Should find at least BCL assemblies from the current AppDomain
        Assert.NotEmpty(refs);
        Assert.True(refs.Count >= 5, $"Expected at least 5 references, got {refs.Count}");
    }

    [Fact]
    public void ResolveAll_CachesResults()
    {
        var resolver = new RuntimeReferenceResolver();

        var first = resolver.ResolveAll();
        var second = resolver.ResolveAll();

        Assert.Same(first, second);
    }

    [Fact]
    public void Invalidate_ClearsCache()
    {
        var resolver = new RuntimeReferenceResolver();

        var first = resolver.ResolveAll();
        resolver.Invalidate();
        var second = resolver.ResolveAll();

        Assert.NotSame(first, second);
        // Both should contain references (re-resolution works)
        Assert.NotEmpty(second);
    }

    [Fact]
    public void AddFromDirectory_FindsDlls()
    {
        using var tmp = new TemporaryDirectory();

        // Copy a real DLL into the temp dir so MetadataReference.CreateFromFile succeeds
        var srcDll = typeof(object).Assembly.Location;
        var dstDll = Path.Combine(tmp.Path, "TestAssembly.dll");
        File.Copy(srcDll, dstDll);

        // Call the private static AddFromDirectory via reflection
        var method = typeof(RuntimeReferenceResolver).GetMethod("AddFromDirectory",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var refs = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);
        method.Invoke(null, new object[] { refs, tmp.Path, false, true });

        Assert.NotEmpty(refs);
        Assert.True(refs.ContainsKey("TestAssembly.dll"));
    }

    [Fact]
    public void IsFrameworkAssembly_FiltersSystemPrefixes()
    {
        var method = typeof(RuntimeReferenceResolver).GetMethod("IsFrameworkAssembly",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        Assert.True((bool)method.Invoke(null, new object[] { "System.Runtime.dll" }));
        Assert.True((bool)method.Invoke(null, new object[] { "Microsoft.CSharp.dll" }));
        Assert.True((bool)method.Invoke(null, new object[] { "netstandard.dll" }));
        Assert.True((bool)method.Invoke(null, new object[] { "mscorlib.dll" }));
        Assert.False((bool)method.Invoke(null, new object[] { "MyMod.dll" }));
        Assert.False((bool)method.Invoke(null, new object[] { "Assembly-CSharp.dll" }));
    }
}
