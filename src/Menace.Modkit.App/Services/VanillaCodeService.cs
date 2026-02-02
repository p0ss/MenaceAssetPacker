using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Menace.Modkit.App.Models;

namespace Menace.Modkit.App.Services;

/// <summary>
/// Scans AssetRipper output for decompiled .cs files.
/// Builds a folder/namespace tree structure for browsing vanilla game code.
/// </summary>
public class VanillaCodeService
{
    private CodeTreeNode? _cachedTree;
    private string? _cachedPath;

    /// <summary>
    /// Build a code tree from decompiled .cs files found under the extracted assets path.
    /// Looks for Scripts/ or MonoScript/ directories from AssetRipper output.
    /// </summary>
    public CodeTreeNode? BuildVanillaCodeTree()
    {
        var assetsPath = AppSettings.GetEffectiveAssetsPath();
        if (assetsPath == null)
            return null;

        // Return cached if same path
        if (_cachedTree != null && _cachedPath == assetsPath)
            return _cachedTree;

        // Look for decompiled C# source directories
        string? scriptsDir = null;
        var candidates = new[]
        {
            Path.Combine(assetsPath, "Assets", "Scripts"),
            Path.Combine(assetsPath, "Assets", "MonoScript"),
            Path.Combine(assetsPath, "Scripts"),
            Path.Combine(assetsPath, "MonoScript"),
            Path.Combine(assetsPath, "ExportedProject", "Assets", "Scripts"),
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                scriptsDir = candidate;
                break;
            }
        }

        if (scriptsDir == null)
        {
            // Fallback: scan for any .cs files in assets
            var csFiles = Directory.GetFiles(assetsPath, "*.cs", SearchOption.AllDirectories);
            if (csFiles.Length == 0)
                return null;

            // Use the assets root
            scriptsDir = assetsPath;
        }

        var root = BuildDirectoryTree(scriptsDir, "Vanilla (read-only)", isReadOnly: true);
        _cachedTree = root;
        _cachedPath = assetsPath;
        return root;
    }

    /// <summary>
    /// Build a code tree for a modpack's src/ directory.
    /// </summary>
    public static CodeTreeNode BuildModSourceTree(string modpackPath, string modpackName)
    {
        var srcDir = Path.Combine(modpackPath, "src");
        if (!Directory.Exists(srcDir))
        {
            Directory.CreateDirectory(srcDir);
            return new CodeTreeNode
            {
                Name = modpackName,
                FullPath = srcDir,
                IsFile = false,
                IsReadOnly = false
            };
        }

        return BuildDirectoryTree(srcDir, modpackName, isReadOnly: false);
    }

    private static CodeTreeNode BuildDirectoryTree(string directory, string displayName, bool isReadOnly)
    {
        var node = new CodeTreeNode
        {
            Name = displayName,
            FullPath = directory,
            IsFile = false,
            IsReadOnly = isReadOnly
        };

        try
        {
            // Add subdirectories
            foreach (var subDir in Directory.GetDirectories(directory).OrderBy(d => Path.GetFileName(d)))
            {
                var dirName = Path.GetFileName(subDir);
                var childNode = BuildDirectoryTree(subDir, dirName, isReadOnly);
                // Only add if it contains .cs files (directly or recursively)
                if (childNode.Children.Count > 0 || HasCsFiles(subDir))
                    node.Children.Add(childNode);
            }

            // Add .cs files
            foreach (var file in Directory.GetFiles(directory, "*.cs").OrderBy(f => Path.GetFileName(f)))
            {
                node.Children.Add(new CodeTreeNode
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    RelativePath = Path.GetRelativePath(directory, file),
                    IsFile = true,
                    IsReadOnly = isReadOnly
                });
            }
        }
        catch { }

        return node;
    }

    private static bool HasCsFiles(string directory)
    {
        try
        {
            return Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories).Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
