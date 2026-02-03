using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Menace.ModpackLoader.Tests.Helpers;

/// <summary>
/// Provides MetadataReferences from loaded assemblies for Roslyn compilation in tests.
/// </summary>
internal static class TestReferences
{
    private static IReadOnlyList<MetadataReference> _cached;

    public static IReadOnlyList<MetadataReference> GetAll()
    {
        if (_cached != null) return _cached;

        var refs = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.IsDynamic) continue;
            try
            {
                var loc = asm.Location;
                if (string.IsNullOrEmpty(loc) || !File.Exists(loc)) continue;
                var key = Path.GetFileName(loc);
                refs.TryAdd(key, MetadataReference.CreateFromFile(loc));
            }
            catch { }
        }

        _cached = refs.Values.ToList();
        return _cached;
    }
}
