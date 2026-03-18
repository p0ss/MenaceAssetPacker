using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Menace.SDK.CustomMaps;

/// <summary>
/// Browse and query available ChunkTemplate assets from the game.
/// ChunkTemplates are the building blocks (structures, compound layouts) that can be placed on the map.
/// </summary>
public static class ChunkBrowser
{
    private const string CHUNK_TEMPLATE_TYPE = "Menace.Tactical.Mapgen.ChunkTemplate";

    // Cached chunks - cleared on reload
    private static List<ChunkInfo> _cachedChunks;
    private static bool _cacheValid;

    /// <summary>
    /// Information about a prefab placed within a chunk.
    /// </summary>
    public class PrefabEntry
    {
        /// <summary>X position within the chunk (in tiles).</summary>
        [JsonPropertyName("x")]
        public int X { get; set; }

        /// <summary>Y position within the chunk (in tiles).</summary>
        [JsonPropertyName("y")]
        public int Y { get; set; }

        /// <summary>Rotation (0=0°, 1=90°, 2=180°, 3=270°).</summary>
        [JsonPropertyName("rotation")]
        public int Rotation { get; set; }

        /// <summary>Name of the prefab asset.</summary>
        [JsonPropertyName("prefab")]
        public string PrefabName { get; set; }

        /// <summary>Width of the prefab (estimated from bounds).</summary>
        [JsonPropertyName("width")]
        public int Width { get; set; } = 1;

        /// <summary>Height of the prefab (estimated from bounds).</summary>
        [JsonPropertyName("height")]
        public int Height { get; set; } = 1;
    }

    /// <summary>
    /// Information about an available chunk template.
    /// </summary>
    public class ChunkInfo
    {
        /// <summary>Name of the chunk template (for placement).</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>Width in tiles.</summary>
        [JsonPropertyName("width")]
        public int Width { get; set; }

        /// <summary>Height in tiles.</summary>
        [JsonPropertyName("height")]
        public int Height { get; set; }

        /// <summary>Chunk type (0=Empty, 1=Fixed, 2=Group).</summary>
        [JsonPropertyName("type")]
        public int Type { get; set; }

        /// <summary>Spawn mode (0=Block, 1=Scatter).</summary>
        [JsonPropertyName("spawnMode")]
        public int SpawnMode { get; set; }

        /// <summary>Number of fixed child entries.</summary>
        [JsonPropertyName("fixedChildCount")]
        public int FixedChildCount { get; set; }

        /// <summary>Number of random child entries.</summary>
        [JsonPropertyName("randomChildCount")]
        public int RandomChildCount { get; set; }

        /// <summary>Number of fixed prefab entries.</summary>
        [JsonPropertyName("fixedPrefabCount")]
        public int FixedPrefabCount { get; set; }

        /// <summary>Maximum spawns allowed.</summary>
        [JsonPropertyName("maxSpawns")]
        public int MaxSpawns { get; set; }

        /// <summary>Prefabs placed within this chunk (with positions).</summary>
        [JsonPropertyName("prefabs")]
        public List<PrefabEntry> Prefabs { get; set; } = new();

        /// <summary>Underlying GameObj pointer (not serialized).</summary>
        [JsonIgnore]
        public GameObj Pointer { get; set; }

        /// <summary>Type string for display (Empty/Fixed/Group).</summary>
        [JsonIgnore]
        public string TypeName => Type switch
        {
            0 => "Empty",
            1 => "Fixed",
            2 => "Group",
            _ => "Unknown"
        };

        public override string ToString() =>
            $"{Name} ({Width}x{Height}, {TypeName})";
    }

    /// <summary>
    /// Get all available chunk templates.
    /// </summary>
    public static List<ChunkInfo> GetAll()
    {
        if (_cacheValid && _cachedChunks != null)
            return _cachedChunks;

        RefreshCache();
        return _cachedChunks ?? new List<ChunkInfo>();
    }

    /// <summary>
    /// Get chunk templates filtered by minimum size.
    /// </summary>
    public static List<ChunkInfo> GetByMinSize(int minWidth, int minHeight)
    {
        return GetAll()
            .Where(c => c.Width >= minWidth && c.Height >= minHeight)
            .ToList();
    }

    /// <summary>
    /// Get chunk templates filtered by type.
    /// </summary>
    public static List<ChunkInfo> GetByType(int type)
    {
        return GetAll()
            .Where(c => c.Type == type)
            .ToList();
    }

    /// <summary>
    /// Search chunks by name (case-insensitive contains).
    /// </summary>
    public static List<ChunkInfo> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return GetAll();

        var lowerQuery = query.ToLowerInvariant();
        return GetAll()
            .Where(c => c.Name.ToLowerInvariant().Contains(lowerQuery))
            .ToList();
    }

    /// <summary>
    /// Get a specific chunk by name.
    /// </summary>
    public static ChunkInfo Get(string name)
    {
        return GetAll().FirstOrDefault(c =>
            c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Check if a chunk exists by name.
    /// </summary>
    public static bool Exists(string name)
    {
        return Get(name) != null;
    }

    /// <summary>
    /// Get the total count of available chunks.
    /// </summary>
    public static int Count => GetAll().Count;

    /// <summary>
    /// Force refresh the chunk cache.
    /// </summary>
    public static void RefreshCache()
    {
        _cachedChunks = new List<ChunkInfo>();

        try
        {
            var templates = Templates.FindAll(CHUNK_TEMPLATE_TYPE);
            SdkLogger.Msg($"[ChunkBrowser] Found {templates.Length} ChunkTemplate assets");

            foreach (var template in templates)
            {
                if (template.IsNull) continue;

                try
                {
                    var info = new ChunkInfo
                    {
                        Name = template.GetName() ?? "Unknown",
                        Pointer = template
                    };

                    // Read ChunkTemplate fields based on RE offsets
                    // Width at 0x58, Height at 0x5C
                    info.Width = template.ReadInt(0x58);
                    info.Height = template.ReadInt(0x5C);

                    // Type at 0x68 (enum ChunkType)
                    info.Type = template.ReadInt(0x68);

                    // SpawnMode at 0x88
                    info.SpawnMode = template.ReadInt(0x88);

                    // MaxSpawns at 0x8C
                    info.MaxSpawns = template.ReadInt(0x8C);

                    // Count arrays - FixedChildren at 0x70, RandomChildren at 0x78, FixedPrefabs at 0x80
                    info.FixedChildCount = GetArrayLength(template, 0x70);
                    info.RandomChildCount = GetArrayLength(template, 0x78);
                    info.FixedPrefabCount = GetArrayLength(template, 0x80);

                    _cachedChunks.Add(info);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("ChunkBrowser.RefreshCache",
                        $"Failed to read chunk info: {ex.Message}");
                }
            }

            _cachedChunks = _cachedChunks.OrderBy(c => c.Name).ToList();
            _cacheValid = true;

            SdkLogger.Msg($"[ChunkBrowser] Cached {_cachedChunks.Count} chunks");
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("ChunkBrowser.RefreshCache",
                "Failed to enumerate chunk templates", ex);
        }
    }

    /// <summary>
    /// Invalidate the cache (call when templates may have changed).
    /// </summary>
    public static void InvalidateCache()
    {
        _cacheValid = false;
        _cachedChunks = null;
    }

    /// <summary>
    /// Get length of an IL2CPP array at the given offset.
    /// </summary>
    private static int GetArrayLength(GameObj obj, uint offset)
    {
        try
        {
            var arrayPtr = obj.ReadPtr(offset);
            if (arrayPtr == IntPtr.Zero) return 0;

            // IL2CPP array length is at offset 0x18
            var arrayObj = new GameObj(arrayPtr);
            return arrayObj.ReadInt(0x18);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Read prefab entries from a chunk template's FixedPrefabs array.
    /// </summary>
    /// <param name="chunk">The chunk to read from.</param>
    /// <returns>List of prefab entries with positions.</returns>
    public static List<PrefabEntry> GetPrefabEntries(ChunkInfo chunk)
    {
        var result = new List<PrefabEntry>();
        if (chunk?.Pointer == null || chunk.Pointer.IsNull)
            return result;

        try
        {
            // FixedPrefabs array at offset 0x80
            var arrayPtr = chunk.Pointer.ReadPtr(0x80);
            if (arrayPtr == IntPtr.Zero)
                return result;

            var arrayObj = new GameObj(arrayPtr);
            var length = arrayObj.ReadInt(0x18);

            // IL2CPP array elements start at offset 0x20
            const uint ARRAY_DATA_OFFSET = 0x20;
            const uint PTR_SIZE = 8; // 64-bit pointers

            for (int i = 0; i < length && i < 100; i++) // Cap at 100 to avoid runaway
            {
                try
                {
                    var entryPtr = arrayObj.ReadPtr(ARRAY_DATA_OFFSET + (uint)(i * PTR_SIZE));
                    if (entryPtr == IntPtr.Zero)
                        continue;

                    var entry = new GameObj(entryPtr);

                    // FixedPrefabEntry structure (from RE):
                    // 0x10: X (int)
                    // 0x14: Y (int)
                    // 0x18: Prefab (GameObject ptr)
                    // 0x20: Rotation (enum, int)
                    var prefabEntry = new PrefabEntry
                    {
                        X = entry.ReadInt(0x10),
                        Y = entry.ReadInt(0x14),
                        Rotation = entry.ReadInt(0x20)
                    };

                    // Try to get prefab name
                    var prefabPtr = entry.ReadPtr(0x18);
                    if (prefabPtr != IntPtr.Zero)
                    {
                        var prefabObj = new GameObj(prefabPtr);
                        prefabEntry.PrefabName = prefabObj.GetName() ?? $"prefab_{i}";

                        // TODO: Could read bounds/size from prefab if needed
                        // For now, estimate 1x1 - could be improved
                    }
                    else
                    {
                        prefabEntry.PrefabName = $"prefab_{i}";
                    }

                    result.Add(prefabEntry);
                }
                catch (Exception ex)
                {
                    ModError.WarnInternal("ChunkBrowser.GetPrefabEntries",
                        $"Failed to read prefab entry {i}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            ModError.WarnInternal("ChunkBrowser.GetPrefabEntries",
                $"Failed to read prefabs for {chunk.Name}: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Get detailed chunk info including prefab positions.
    /// </summary>
    public static ChunkInfo GetDetailed(string name)
    {
        var chunk = Get(name);
        if (chunk == null)
            return null;

        // Load prefab entries
        chunk.Prefabs = GetPrefabEntries(chunk);
        return chunk;
    }

    /// <summary>
    /// Export all chunk layouts to a JSON file.
    /// </summary>
    /// <param name="outputPath">Path to write the JSON file.</param>
    /// <returns>Number of chunks exported.</returns>
    public static int ExportChunkLayouts(string outputPath)
    {
        var chunks = GetAll();
        var exportData = new List<ChunkInfo>();

        foreach (var chunk in chunks)
        {
            try
            {
                chunk.Prefabs = GetPrefabEntries(chunk);
                exportData.Add(chunk);
            }
            catch (Exception ex)
            {
                ModError.WarnInternal("ChunkBrowser.ExportChunkLayouts",
                    $"Failed to export {chunk.Name}: {ex.Message}");
            }
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(exportData, options);
            File.WriteAllText(outputPath, json);

            SdkLogger.Msg($"[ChunkBrowser] Exported {exportData.Count} chunks to {outputPath}");
            return exportData.Count;
        }
        catch (Exception ex)
        {
            ModError.ReportInternal("ChunkBrowser.ExportChunkLayouts",
                $"Failed to write export file", ex);
            return 0;
        }
    }

    /// <summary>
    /// Register console commands for chunk browsing.
    /// </summary>
    public static void RegisterConsoleCommands()
    {
        // listchunks [query] - List available chunks
        DevConsole.RegisterCommand("listchunks", "[query]", "List available chunk templates", args =>
        {
            var chunks = args.Length > 0 ? Search(args[0]) : GetAll();

            if (chunks.Count == 0)
                return args.Length > 0
                    ? $"No chunks matching '{args[0]}'"
                    : "No chunk templates found";

            var lines = new List<string> { $"Chunk Templates ({chunks.Count}):" };
            foreach (var chunk in chunks.Take(50))
            {
                lines.Add($"  {chunk.Name}: {chunk.Width}x{chunk.Height} ({chunk.TypeName})");
            }

            if (chunks.Count > 50)
                lines.Add($"  ... and {chunks.Count - 50} more");

            return string.Join("\n", lines);
        });

        // chunkinfo <name> - Show detailed chunk info
        DevConsole.RegisterCommand("chunkinfo", "<name>", "Show detailed chunk template info", args =>
        {
            if (args.Length == 0)
                return "Usage: chunkinfo <name>";

            var chunk = Get(args[0]);
            if (chunk == null)
                return $"Chunk not found: {args[0]}";

            var lines = new List<string>
            {
                $"Chunk: {chunk.Name}",
                $"  Size: {chunk.Width}x{chunk.Height}",
                $"  Type: {chunk.TypeName} ({chunk.Type})",
                $"  SpawnMode: {(chunk.SpawnMode == 0 ? "Block" : "Scatter")}",
                $"  MaxSpawns: {chunk.MaxSpawns}",
                $"  FixedChildren: {chunk.FixedChildCount}",
                $"  RandomChildren: {chunk.RandomChildCount}",
                $"  FixedPrefabs: {chunk.FixedPrefabCount}"
            };

            return string.Join("\n", lines);
        });

        // refreshchunks - Refresh chunk cache
        DevConsole.RegisterCommand("refreshchunks", "", "Refresh chunk template cache", _ =>
        {
            InvalidateCache();
            RefreshCache();
            return $"Refreshed chunk cache: {Count} templates";
        });

        // chunkprefabs <name> - Show prefabs in a chunk
        DevConsole.RegisterCommand("chunkprefabs", "<name>", "Show prefab positions in a chunk", args =>
        {
            if (args.Length == 0)
                return "Usage: chunkprefabs <name>";

            var chunk = GetDetailed(args[0]);
            if (chunk == null)
                return $"Chunk not found: {args[0]}";

            if (chunk.Prefabs.Count == 0)
                return $"No prefabs in chunk: {chunk.Name}";

            var lines = new List<string>
            {
                $"Chunk: {chunk.Name} ({chunk.Width}x{chunk.Height})",
                $"Prefabs ({chunk.Prefabs.Count}):"
            };

            foreach (var prefab in chunk.Prefabs.Take(20))
            {
                var rotDeg = prefab.Rotation * 90;
                lines.Add($"  [{prefab.X},{prefab.Y}] R{rotDeg}° {prefab.PrefabName}");
            }

            if (chunk.Prefabs.Count > 20)
                lines.Add($"  ... and {chunk.Prefabs.Count - 20} more");

            return string.Join("\n", lines);
        });

        // exportchunks [path] - Export all chunk layouts to JSON
        DevConsole.RegisterCommand("exportchunks", "[path]", "Export chunk layouts to JSON for editor preview", args =>
        {
            var path = args.Length > 0
                ? args[0]
                : Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "chunk_layouts.json");

            var count = ExportChunkLayouts(path);
            return count > 0
                ? $"Exported {count} chunk layouts to:\n{path}"
                : "Export failed - check console for errors";
        });
    }
}
